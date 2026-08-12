using System.Security.Cryptography;

namespace ShowVault.Agent.Recovery;

internal enum SourceSnapshotRacePoint
{
    SnapshotCaptured,
    SourceCopyStarted
}

internal interface ISourceSnapshotRaceProbe
{
    void Reached(SourceSnapshotRacePoint point, string relativePath);
}

internal sealed record StableSourceFile(
    string RelativePath,
    long Size,
    DateTimeOffset LastModifiedAt,
    string Sha256);

internal sealed class StableSourceSnapshot : IAsyncDisposable
{
    private const int MaximumDirectoryCount = 512;
    private const int MaximumRelativePathLength = 1_024;
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private readonly string _rootPath;
    private readonly StableDirectoryTree _root;
    private readonly Dictionary<string, HeldDirectory> _directories = new(PathComparer);
    private readonly Dictionary<string, HeldFile> _files = new(PathComparer);
    private bool _disposed;

    private StableSourceSnapshot(string rootPath, StableDirectoryTree root)
    {
        _rootPath = rootPath;
        _root = root;
        _directories.Add(string.Empty, new HeldDirectory(string.Empty, string.Empty, null, root));
    }

    public IReadOnlyList<StableSourceFile> Files => _files.Values
        .Select(file => file.Metadata)
        .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
        .ToList();

    public static async Task<StableSourceSnapshot> CaptureAsync(
        string rootPath,
        int maximumFileCount,
        CancellationToken cancellationToken)
    {
        if (maximumFileCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumFileCount));
        }

        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        var root = StableDirectoryTree.Open(normalizedRoot);
        var snapshot = new StableSourceSnapshot(normalizedRoot, root);
        try
        {
            await snapshot.CaptureDirectoryAsync(
                snapshot._directories[string.Empty],
                maximumFileCount,
                cancellationToken);
            await snapshot.ValidateStableAsync(rehashFiles: true, cancellationToken);
            return snapshot;
        }
        catch
        {
            await snapshot.DisposeAsync();
            throw;
        }
    }

    public void RequireExactFiles(IReadOnlyList<RecoveryPackageFile> expectedFiles)
    {
        var expected = new Dictionary<string, RecoveryPackageFile>(PathComparer);
        foreach (var file in expectedFiles)
        {
            if (!expected.TryAdd(file.RelativePath, file))
            {
                throw new InvalidOperationException("Source manifest contains duplicate file paths.");
            }
        }

        if (expected.Count != _files.Count)
        {
            throw new InvalidOperationException("Source file topology changed after discovery.");
        }

        foreach (var (relativePath, heldFile) in _files)
        {
            if (!expected.TryGetValue(relativePath, out var expectedFile) ||
                heldFile.Metadata.Size != expectedFile.Size ||
                !string.Equals(
                    heldFile.Metadata.Sha256,
                    expectedFile.Sha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Source content changed after discovery.");
            }
        }
    }

    public FileStream GetFile(string relativePath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_files.TryGetValue(relativePath, out var heldFile))
        {
            throw new InvalidOperationException("Source file is not present in the retained snapshot.");
        }

        heldFile.Stream.Position = 0;
        return heldFile.Stream;
    }

    public async Task ValidateStableAsync(
        bool rehashFiles,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var currentRoot = StableDirectoryTree.Open(_rootPath);
        if (!_root.HasSameIdentity(currentRoot))
        {
            throw new IOException("Source root identity changed during capture.");
        }

        foreach (var directory in _directories.Values
                     .Where(directory => directory.Parent is not null)
                     .OrderBy(directory => directory.RelativePath, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!directory.Directory.IsSameDirectoryAt(
                    directory.Parent!.Directory,
                    directory.Name))
            {
                throw new IOException("Source directory identity changed during capture.");
            }
        }

        foreach (var directory in _directories.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var expectedNames = _directories.Values
                .Where(candidate => ReferenceEquals(candidate.Parent, directory))
                .Select(candidate => candidate.Name)
                .Concat(_files.Values
                    .Where(file => ReferenceEquals(file.Parent, directory))
                    .Select(file => file.Name))
                .ToHashSet(PathComparer);
            var actualNames = directory.Directory.EnumerateNames().ToHashSet(PathComparer);
            if (!expectedNames.SetEquals(actualNames))
            {
                throw new IOException("Source topology changed during capture.");
            }
        }

        foreach (var heldFile in _files.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!heldFile.Parent.Directory.IsSameFileAt(
                    heldFile.Name,
                    heldFile.Stream.SafeFileHandle) ||
                heldFile.Stream.Length != heldFile.Metadata.Size)
            {
                throw new IOException("Source file identity changed during capture.");
            }

            if (rehashFiles)
            {
                var currentHash = await HashAsync(heldFile.Stream, cancellationToken);
                if (!string.Equals(
                        currentHash,
                        heldFile.Metadata.Sha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException("Source file content changed during capture.");
                }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var file in _files.Values)
        {
            await file.Stream.DisposeAsync();
        }

        foreach (var directory in _directories.Values
                     .Where(directory => directory.Parent is not null)
                     .OrderByDescending(directory => directory.RelativePath.Length))
        {
            directory.Directory.Dispose();
        }

        _root.Dispose();
    }

    private async Task CaptureDirectoryAsync(
        HeldDirectory directory,
        int maximumFileCount,
        CancellationToken cancellationToken)
    {
        foreach (var name in directory.Directory.EnumerateNames())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = CombineRelative(directory.RelativePath, name);
            if (relativePath.Length > MaximumRelativePathLength)
            {
                throw new IOException("Source contains an overlong relative path.");
            }

            StableDirectoryTree? childDirectory = null;
            try
            {
                childDirectory = directory.Directory.OpenDirectory(name);
            }
            catch (IOException)
            {
                // The entry may be a regular file. Opening it below remains no-follow.
            }

            if (childDirectory is not null)
            {
                if (_directories.Count == MaximumDirectoryCount)
                {
                    childDirectory.Dispose();
                    throw new IOException("Source contains too many directories.");
                }

                var heldDirectory = new HeldDirectory(
                    relativePath,
                    name,
                    directory,
                    childDirectory);
                if (!_directories.TryAdd(relativePath, heldDirectory))
                {
                    childDirectory.Dispose();
                    throw new IOException("Source contains duplicate directory paths.");
                }

                await CaptureDirectoryAsync(
                    heldDirectory,
                    maximumFileCount,
                    cancellationToken);
                continue;
            }

            if (_files.Count == maximumFileCount)
            {
                throw new IOException("Source contains more files than the allowed limit.");
            }

            FileStream stream;
            try
            {
                stream = directory.Directory.OpenRegularFile(name);
            }
            catch (IOException exception)
            {
                throw new IOException(
                    "Source contains a link, reparse point, or non-regular entry.",
                    exception);
            }

            try
            {
                var sizeBeforeHash = stream.Length;
                var sha256 = await HashAsync(stream, cancellationToken);
                if (stream.Length != sizeBeforeHash)
                {
                    throw new IOException("Source file size changed during capture.");
                }

                var metadata = new StableSourceFile(
                    relativePath,
                    sizeBeforeHash,
                    File.GetLastWriteTimeUtc(stream.SafeFileHandle),
                    sha256);
                if (!_files.TryAdd(
                        relativePath,
                        new HeldFile(name, directory, stream, metadata)))
                {
                    throw new IOException("Source contains duplicate file paths.");
                }
            }
            catch
            {
                await stream.DisposeAsync();
                throw;
            }
        }
    }

    private static async Task<string> HashAsync(
        FileStream stream,
        CancellationToken cancellationToken)
    {
        stream.Position = 0;
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        stream.Position = 0;
        return Convert.ToHexStringLower(hash);
    }

    private static string CombineRelative(string parent, string name) =>
        string.IsNullOrEmpty(parent) ? name : $"{parent}/{name}";

    private sealed record HeldDirectory(
        string RelativePath,
        string Name,
        HeldDirectory? Parent,
        StableDirectoryTree Directory);

    private sealed record HeldFile(
        string Name,
        HeldDirectory Parent,
        FileStream Stream,
        StableSourceFile Metadata);
}
