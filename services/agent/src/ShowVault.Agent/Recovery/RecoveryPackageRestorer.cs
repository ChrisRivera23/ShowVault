using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ShowVault.Agent.Queue;

namespace ShowVault.Agent.Recovery;

public sealed class RecoveryPackageRestorer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IReadOnlyList<string> _restoreRoots;
    private readonly RecoveryPackageVerifier _verifier;
    private readonly AgentQueueStore _queueStore;
    private readonly IRestoreRaceProbe? _raceProbe;

    public RecoveryPackageRestorer(
        IOptions<AgentOptions> options,
        RecoveryPackageVerifier verifier,
        AgentQueueStore queueStore)
        : this(options, verifier, queueStore, null)
    {
    }

    internal RecoveryPackageRestorer(
        IOptions<AgentOptions> options,
        RecoveryPackageVerifier verifier,
        AgentQueueStore queueStore,
        IRestoreRaceProbe? raceProbe)
    {
        _restoreRoots = options.Value.RestoreRoots
            .Select(path => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)))
            .ToList();
        _verifier = verifier;
        _queueStore = queueStore;
        _raceProbe = raceProbe;
    }

    public async Task<RecoveryRestorationResult> RestoreAsync(
        Guid restorationId,
        Guid agentId,
        StoredRecoveryPackage package,
        Guid verificationId,
        string targetPath,
        DateTimeOffset restoredAt,
        CancellationToken cancellationToken)
    {
        var (normalizedTarget, allowedRoot) = ResolveTargetPath(targetPath);
        var intent = await _queueStore.GetRecoveryRestoreIntentAsync(
            restorationId,
            cancellationToken);
        if (intent is null)
        {
            EnsureTargetIsAbsentOrEmpty(normalizedTarget);
            await _queueStore.StoreRecoveryRestoreIntentAsync(
                restorationId,
                package.PackageId,
                normalizedTarget,
                restoredAt,
                cancellationToken);
            intent = await _queueStore.GetRecoveryRestoreIntentAsync(
                restorationId,
                cancellationToken)
                ?? throw new InvalidOperationException("Restore intent was not stored.");
        }

        if (intent.PackageId != package.PackageId || intent.TargetPath != normalizedTarget)
        {
            throw new InvalidOperationException("Stored restore intent does not match this operation.");
        }

        restoredAt = intent.CreatedAt;

        var preRestoreVerification = await _verifier.VerifyAsync(
            restorationId,
            agentId,
            package.PackageId,
            package.PackagePath,
            restoredAt,
            cancellationToken);
        if (!preRestoreVerification.Passed)
        {
            throw new InvalidOperationException(
                "Recovery package failed immediate pre-restore verification.");
        }

        var manifestPath = Path.Combine(
            package.PackagePath,
            RecoveryPackageFormat.ManifestFileName);
        var manifest = await ReadVerifiedManifestAsync(
            manifestPath,
            package.PackageId,
            cancellationToken);
        EnsureParentPathIsSafe(allowedRoot, normalizedTarget);
        if (IsLinkIfPresent(normalizedTarget))
        {
            throw new InvalidOperationException("Restore target cannot be a filesystem link.");
        }

        if (Directory.Exists(normalizedTarget) &&
            Directory.EnumerateFileSystemEntries(normalizedTarget).Any())
        {
            await EnsureRestoredTargetMatchesAsync(
                allowedRoot,
                normalizedTarget,
                manifest,
                cancellationToken);
            return CreateResult(
                restorationId,
                package.PackageId,
                verificationId,
                normalizedTarget,
                restoredAt,
                manifest.Files.Count);
        }

        var parentPath = Path.GetDirectoryName(normalizedTarget)
            ?? throw new InvalidOperationException("Restore target must have a parent directory.");
        var stagingPath = Path.Combine(parentPath, $".showvault-restore-{restorationId:N}");
        using var targetParent = OpenTargetParent(allowedRoot, normalizedTarget);
        var stagingName = Path.GetFileName(stagingPath);
        var targetName = Path.GetFileName(normalizedTarget);
        EnsureParentPathIsSafe(allowedRoot, normalizedTarget);
        if (File.Exists(stagingPath) || IsLinkIfPresent(stagingPath))
        {
            throw new InvalidOperationException("Restore staging path already exists.");
        }

        if (Directory.Exists(stagingPath))
        {
            using var staleStaging = targetParent.OpenDirectory(stagingName);
            targetParent.DeleteChildTreeIfSame(stagingName, staleStaging);
        }

        using var staging = targetParent.CreateDirectory(stagingName);

        try
        {
            foreach (var file in manifest.Files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sourcePath = Path.Combine(
                    package.PackagePath,
                    RecoveryPackageFormat.ContentDirectoryName,
                    file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                EnsureNoLinks(
                    Path.Combine(package.PackagePath, RecoveryPackageFormat.ContentDirectoryName),
                    sourcePath);
                await using var source = RecoveryPackageVerifier.OpenRegularFile(sourcePath)
                    ?? throw new InvalidOperationException(
                        $"Restore source must be a regular file: {file.RelativePath}");
                var temporaryName = $".showvault-file-{Guid.NewGuid():N}";
                string restoredHash;
                long restoredLength;
                await using (var temporary = staging.CreateFile(temporaryName))
                {
                    restoredHash = await CopyAndHashAsync(
                        source,
                        temporary,
                        cancellationToken);
                    restoredLength = temporary.Length;
                }

                if (source.Length != file.Size || restoredLength != file.Size ||
                    !string.Equals(restoredHash, file.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Restored content does not match the package: {file.RelativePath}");
                }

                using var destinationParent = OpenOrCreateRelativeParent(
                    staging,
                    file.RelativePath);
                _raceProbe?.Reached(RestoreRacePoint.DestinationFileOpened, file.RelativePath);
                if (!IsExpectedRelativeParent(staging, file.RelativePath, destinationParent))
                {
                    throw new InvalidOperationException(
                        "Restore destination directory identity changed.");
                }

                staging.MoveChildTo(
                    temporaryName,
                    destinationParent,
                    Path.GetFileName(file.RelativePath));
            }

            EnsureParentPathIsSafe(allowedRoot, normalizedTarget);
            EnsureTargetIsAbsentOrEmpty(normalizedTarget);
            if (!staging.IsSameDirectoryAt(targetParent, stagingName))
            {
                throw new InvalidOperationException("Restore staging directory identity changed.");
            }

            if (Directory.Exists(normalizedTarget))
            {
                using var emptyTarget = targetParent.OpenDirectory(targetName);
                if (emptyTarget.EnumerateNames().Count != 0)
                {
                    throw new InvalidOperationException("Restore target must be empty.");
                }

                targetParent.DeleteChildTreeIfSame(targetName, emptyTarget);
            }

            targetParent.RenameChild(stagingName, staging, targetName);
        }
        catch (IOException exception)
        {
            throw new InvalidOperationException(
                "Restore filesystem boundary changed during execution.",
                exception);
        }
        finally
        {
            targetParent.DeleteChildTreeIfSame(stagingName, staging);
        }

        return CreateResult(
            restorationId,
            package.PackageId,
            verificationId,
            normalizedTarget,
            restoredAt,
            manifest.Files.Count);
    }

    public static string Serialize(RecoveryRestorationResult result) =>
        JsonSerializer.Serialize(result, JsonOptions);

    internal string NormalizeAndValidateTargetPath(string targetPath) =>
        ResolveTargetPath(targetPath).NormalizedTarget;

    private (string NormalizedTarget, string AllowedRoot) ResolveTargetPath(string targetPath)
    {
        var normalizedTarget = Path.TrimEndingDirectorySeparator(Path.GetFullPath(targetPath));
        var allowedRoot = _restoreRoots.FirstOrDefault(root => IsStrictDescendant(root, normalizedTarget))
            ?? throw new UnauthorizedAccessException(
                "Restore target is not beneath a locally configured restore root.");
        EnsureParentPathIsSafe(allowedRoot, normalizedTarget);
        return (normalizedTarget, allowedRoot);
    }

    private static async Task<RecoveryPackageManifest> ReadVerifiedManifestAsync(
        string manifestPath,
        string expectedPackageId,
        CancellationToken cancellationToken)
    {
        await using var manifestStream = RecoveryPackageVerifier.OpenRegularFile(manifestPath)
            ?? throw new InvalidOperationException("Recovery package manifest must be a regular file.");
        if (manifestStream.Length > RecoveryPackageVerifier.MaximumManifestBytes)
        {
            throw new InvalidOperationException("Recovery package manifest is too large.");
        }

        var manifestBytes = new byte[checked((int)manifestStream.Length)];
        await manifestStream.ReadExactlyAsync(manifestBytes, cancellationToken);
        var actualPackageId = Convert.ToHexStringLower(SHA256.HashData(manifestBytes));
        if (!string.Equals(actualPackageId, expectedPackageId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Recovery package manifest identity changed after verification.");
        }

        return JsonSerializer.Deserialize<RecoveryPackageManifest>(manifestBytes, JsonOptions)
            ?? throw new InvalidOperationException("Recovery package manifest is invalid.");
    }

    private static async Task<string> CopyAndHashAsync(
        FileStream source,
        FileStream destination,
        CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(65_536);
        try
        {
            int bytesRead;
            while ((bytesRead = await source.ReadAsync(
                buffer.AsMemory(0, buffer.Length),
                cancellationToken)) > 0)
            {
                hash.AppendData(buffer, 0, bytesRead);
                await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            }

            await destination.FlushAsync(cancellationToken);
            return Convert.ToHexStringLower(hash.GetHashAndReset());
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private async Task EnsureRestoredTargetMatchesAsync(
        string allowedRoot,
        string targetPath,
        RecoveryPackageManifest manifest,
        CancellationToken cancellationToken)
    {
        var expectedFiles = manifest.Files
            .Select(file => file.RelativePath)
            .ToHashSet(StringComparer.Ordinal);
        var expectedDirectories = manifest.Files
            .SelectMany(file => GetParentPaths(file.RelativePath))
            .ToHashSet(StringComparer.Ordinal);
        using var parent = OpenTargetParent(allowedRoot, targetPath);
        using var root = parent.OpenDirectory(Path.GetFileName(targetPath));
        await VerifyDirectoryAsync(
            root,
            string.Empty,
            manifest,
            expectedFiles,
            expectedDirectories,
            cancellationToken);

        if (expectedFiles.Count != 0)
        {
            throw new InvalidOperationException("Published restore target is incomplete.");
        }
    }

    private async Task VerifyDirectoryAsync(
        StableDirectoryTree directory,
        string parentRelativePath,
        RecoveryPackageManifest manifest,
        HashSet<string> expectedFiles,
        HashSet<string> expectedDirectories,
        CancellationToken cancellationToken)
    {
        foreach (var name in directory.EnumerateNames())
        {
            var relativePath = parentRelativePath.Length == 0
                ? name
                : $"{parentRelativePath}/{name}";
            if (expectedDirectories.Contains(relativePath))
            {
                StableDirectoryTree child;
                try
                {
                    child = directory.OpenDirectory(name);
                }
                catch (IOException exception)
                {
                    throw new InvalidOperationException(
                        "Published restore target contains a linked or invalid directory.",
                        exception);
                }

                using (child)
                {
                    _raceProbe?.Reached(RestoreRacePoint.AdoptionDirectoryOpened, relativePath);
                    await VerifyDirectoryAsync(
                        child,
                        relativePath,
                        manifest,
                        expectedFiles,
                        expectedDirectories,
                        cancellationToken);
                    if (!child.IsSameDirectoryAt(directory, name))
                    {
                        throw new InvalidOperationException(
                            "Published restore target directory identity changed.");
                    }
                }

                continue;
            }

            if (!expectedFiles.Remove(relativePath))
            {
                throw new InvalidOperationException(
                    "Published restore target contains unexpected entries.");
            }

            var expected = manifest.Files.Single(file => file.RelativePath == relativePath);
            try
            {
                await using var stream = directory.OpenRegularFile(name);
                var hash = Convert.ToHexStringLower(
                    await SHA256.HashDataAsync(stream, cancellationToken));
                if (stream.Length != expected.Size ||
                    !string.Equals(hash, expected.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Published restore target failed integrity checks.");
                }
            }
            catch (IOException exception)
            {
                throw new InvalidOperationException(
                    "Published restore target must contain only regular files.",
                    exception);
            }
        }
    }

    private static StableDirectoryTree OpenTargetParent(string rootPath, string targetPath)
    {
        var parentPath = Path.GetDirectoryName(targetPath)
            ?? throw new InvalidOperationException("Restore target must have a parent directory.");
        var current = StableDirectoryTree.Open(rootPath);
        foreach (var segment in Path.GetRelativePath(rootPath, parentPath).Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
            {
                continue;
            }

            var next = current.OpenDirectory(segment);
            current.Dispose();
            current = next;
        }

        return current;
    }

    private static StableDirectoryTree OpenOrCreateRelativeParent(
        StableDirectoryTree root,
        string relativePath)
    {
        var segments = relativePath.Split('/');
        if (segments.Length == 1)
        {
            return root.Duplicate();
        }

        var current = root.Duplicate();
        for (var index = 0; index < segments.Length - 1; index++)
        {
            var next = current.GetOrCreateDirectory(segments[index]);
            current.Dispose();
            current = next;
        }

        return current;
    }

    private static bool IsExpectedRelativeParent(
        StableDirectoryTree root,
        string relativePath,
        StableDirectoryTree expected)
    {
        var segments = relativePath.Split('/');
        using var currentRoot = root.Duplicate();
        var current = currentRoot;
        var disposeCurrent = false;
        try
        {
            for (var index = 0; index < segments.Length - 1; index++)
            {
                var next = current.OpenDirectory(segments[index]);
                if (disposeCurrent)
                {
                    current.Dispose();
                }

                current = next;
                disposeCurrent = true;
            }

            return current.HasSameIdentity(expected);
        }
        catch (IOException)
        {
            return false;
        }
        finally
        {
            if (disposeCurrent)
            {
                current.Dispose();
            }
        }
    }


    private static IEnumerable<string> GetParentPaths(string relativePath)
    {
        var parent = Path.GetDirectoryName(relativePath.Replace('/', Path.DirectorySeparatorChar));
        while (!string.IsNullOrEmpty(parent))
        {
            yield return parent.Replace(Path.DirectorySeparatorChar, '/');
            parent = Path.GetDirectoryName(parent);
        }
    }

    private static RecoveryRestorationResult CreateResult(
        Guid restorationId,
        string packageId,
        Guid verificationId,
        string targetPath,
        DateTimeOffset restoredAt,
        int fileCount) =>
        new(
            restorationId,
            packageId,
            verificationId,
            targetPath,
            restoredAt,
            fileCount,
            true,
            ["Pre-restore package verification passed.", "Restored file sizes and SHA-256 hashes passed."]);

    private static bool IsStrictDescendant(string rootPath, string targetPath)
    {
        var relativePath = Path.GetRelativePath(rootPath, targetPath);
        return relativePath != "." &&
            relativePath != ".." &&
            !Path.IsPathFullyQualified(relativePath) &&
            !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
            !relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }

    private static void EnsureParentPathIsSafe(string rootPath, string targetPath)
    {
        if (!Directory.Exists(rootPath) || IsLink(rootPath))
        {
            throw new InvalidOperationException("Configured restore root must be an existing directory, not a link.");
        }

        var parentPath = Path.GetDirectoryName(targetPath)
            ?? throw new InvalidOperationException("Restore target must have a parent directory.");
        if (!Directory.Exists(parentPath))
        {
            throw new InvalidOperationException("Restore target parent directory must already exist.");
        }

        var currentPath = rootPath;
        foreach (var segment in Path.GetRelativePath(rootPath, parentPath).Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries))
        {
            currentPath = Path.Combine(currentPath, segment);
            if (!Directory.Exists(currentPath) || IsLink(currentPath))
            {
                throw new InvalidOperationException("Restore target parent path cannot traverse links.");
            }
        }
    }

    private static void EnsureNoLinks(string rootPath, string path)
    {
        if (!Directory.Exists(rootPath) || IsLink(rootPath))
        {
            throw new InvalidOperationException("Restore source root cannot be a filesystem link.");
        }

        var currentPath = rootPath;
        foreach (var segment in Path.GetRelativePath(rootPath, path).Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries))
        {
            currentPath = Path.Combine(currentPath, segment);
            if (IsLink(currentPath))
            {
                throw new InvalidOperationException("Restore source cannot traverse filesystem links.");
            }
        }
    }

    private static void EnsureTargetIsAbsentOrEmpty(string targetPath)
    {
        if (File.Exists(targetPath) || IsLinkIfPresent(targetPath))
        {
            throw new InvalidOperationException("Restore target must not be a file or link.");
        }

        if (Directory.Exists(targetPath) && Directory.EnumerateFileSystemEntries(targetPath).Any())
        {
            throw new InvalidOperationException("Restore target must be empty.");
        }
    }

    private static bool IsLinkIfPresent(string path) =>
        (File.Exists(path) || Directory.Exists(path)) && IsLink(path);

    private static bool IsLink(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
}

public sealed record RecoveryRestorationResult(
    Guid RestorationId,
    string PackageId,
    Guid VerificationId,
    string TargetPath,
    DateTimeOffset RestoredAt,
    int FileCount,
    bool Passed,
    IReadOnlyList<string> Evidence);
