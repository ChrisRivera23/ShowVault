using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace ShowVault.Agent.Plugins;

public sealed class FileSystemDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : IDiscoveryPlugin
{
    public const string PluginId = "showvault.filesystem";
    private const int MaximumFileLimit = 10_000;

    public AgentPluginManifest Manifest { get; } = new(
        PluginId,
        "ShowVault Filesystem Discovery",
        "0.1.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    public async Task<DiscoveryResult> DiscoverAsync(
        DiscoveryRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RootPath);
        if (!Path.IsPathFullyQualified(request.RootPath))
        {
            throw new ArgumentException("Discovery root must be an absolute path.", nameof(request));
        }

        if (request.MaxFiles is < 1 or > MaximumFileLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                $"File limit must be between 1 and {MaximumFileLimit}.");
        }

        var rootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(request.RootPath));
        var allowedPath = options.Value.DiscoveryRoots
            .Select(allowedRoot => Path.TrimEndingDirectorySeparator(Path.GetFullPath(allowedRoot)))
            .FirstOrDefault(allowedRoot => IsWithinRoot(rootPath, allowedRoot));
        if (allowedPath is null)
        {
            throw new UnauthorizedAccessException("Discovery root is not locally authorized.");
        }

        if (!Directory.Exists(rootPath))
        {
            throw new DirectoryNotFoundException("Discovery root does not exist.");
        }

        EnsureUnlinkedDirectoryPath(allowedPath, rootPath);

        var files = new List<DiscoveryFile>();
        var truncated = false;
        var directories = new Stack<string>();
        directories.Push(rootPath);
        while (directories.Count > 0 && !truncated)
        {
            var directory = directories.Pop();
            foreach (var path in Directory.EnumerateFileSystemEntries(directory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var attributes = File.GetAttributes(path);
                if (IsLink(path, attributes))
                {
                    throw new UnauthorizedAccessException(
                        "Discovery content contains a symbolic link or reparse point.");
                }

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    directories.Push(path);
                    continue;
                }

                if ((attributes & FileAttributes.Device) != 0)
                {
                    throw new IOException("Discovery content contains a non-regular file.");
                }

                if (files.Count == request.MaxFiles)
                {
                    truncated = true;
                    break;
                }

                var info = new FileInfo(path);
                await using var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 65_536,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                if (IsLink(path, File.GetAttributes(path)))
                {
                    throw new UnauthorizedAccessException(
                        "Discovery content changed to a symbolic link or reparse point.");
                }

                var hash = await SHA256.HashDataAsync(stream, cancellationToken);
                files.Add(new DiscoveryFile(
                    Path.GetRelativePath(rootPath, path),
                    info.Length,
                    info.LastWriteTimeUtc,
                    Convert.ToHexStringLower(hash)));
            }
        }

        return new DiscoveryResult(
            Manifest.Id,
            Manifest.Version,
            rootPath,
            timeProvider.GetUtcNow(),
            truncated,
            files);
    }

    private static void EnsureUnlinkedDirectoryPath(string allowedPath, string requestedPath)
    {
        EnsureDirectoryIsNotLink(allowedPath);
        var relativePath = Path.GetRelativePath(allowedPath, requestedPath);
        if (relativePath == ".")
        {
            return;
        }

        var currentPath = allowedPath;
        foreach (var segment in relativePath.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            currentPath = Path.Combine(currentPath, segment);
            EnsureDirectoryIsNotLink(currentPath);
        }
    }

    private static void EnsureDirectoryIsNotLink(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.Directory) == 0 || IsLink(path, attributes))
        {
            throw new UnauthorizedAccessException(
                "Discovery root contains a symbolic link or reparse point.");
        }
    }

    private static bool IsLink(string path, FileAttributes attributes)
    {
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            return true;
        }

        FileSystemInfo info = (attributes & FileAttributes.Directory) != 0
            ? new DirectoryInfo(path)
            : new FileInfo(path);
        return info.LinkTarget is not null;
    }

    private static bool IsWithinRoot(string requestedPath, string configuredRoot)
    {
        var allowedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(configuredRoot));
        var relativePath = Path.GetRelativePath(allowedPath, requestedPath);
        return relativePath == "." ||
            (!Path.IsPathFullyQualified(relativePath) &&
             relativePath != ".." &&
             !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
             !relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal));
    }
}
