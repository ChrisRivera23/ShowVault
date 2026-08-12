using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;

namespace ShowVault.Agent.Recovery;

public sealed class RecoveryPackageWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly StringComparer FileSystemPathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
    private readonly string _packageDirectory;
    private readonly AgentOptions _options;
    private readonly ISourceSnapshotRaceProbe? _sourceSnapshotRaceProbe;

    public RecoveryPackageWriter(IOptions<AgentOptions> options)
        : this(options, null)
    {
    }

    internal RecoveryPackageWriter(
        IOptions<AgentOptions> options,
        ISourceSnapshotRaceProbe? sourceSnapshotRaceProbe)
    {
        _options = options.Value;
        _packageDirectory = ResolvePackageDirectory(_options);
        _sourceSnapshotRaceProbe = sourceSnapshotRaceProbe;
    }

    public async Task<CreatedRecoveryPackage> CreateAsync(
        Guid agentId,
        Guid discoveryCommandId,
        DiscoveryResult discovery,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        var isMaLighting = MaLightingShowExportDiscoveryPluginBase.IsMaLightingPlugin(
            discovery.PluginId);
        using var profileTimeout = string.Equals(
                discovery.PluginId,
                ResolumeUserDataDiscoveryPlugin.PluginId,
                StringComparison.Ordinal) || isMaLighting
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : null;
        profileTimeout?.CancelAfter(isMaLighting
            ? MaLightingShowExportDiscoveryPluginBase.MaximumPackageDuration
            : ResolumeUserDataDiscoveryPlugin.MaximumPackageDuration);
        var operationToken = profileTimeout?.Token ?? cancellationToken;

        if (discovery.Truncated)
        {
            throw new InvalidOperationException(
                "A truncated discovery inventory cannot create a recovery package.");
        }

        var rootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(discovery.RootPath));
        var manifestFiles = discovery.Files
            .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
            .Select(file => new RecoveryPackageFile(
                NormalizeRelativePath(rootPath, file.RelativePath),
                file.Size,
                file.Sha256.ToLowerInvariant()))
            .ToList();
        var isGrandMa = MaLightingShowExportDiscoveryPluginBase.IsMaLightingPlugin(
            discovery.PluginId);
        var manifest = new RecoveryPackageManifest(
            RecoveryPackageFormat.Version,
            agentId,
            discoveryCommandId,
            new RecoveryPackageSource(
                rootPath,
                discovery.PluginId,
                discovery.PluginVersion,
                ProductVersion: isGrandMa
                    ? MaLightingShowExportDiscoveryPluginBase.GetProductVersion(
                        discovery.PluginId,
                        rootPath)
                    : null,
                FirmwareVersion: null),
            createdAt,
            manifestFiles,
            Dependencies: [],
            Relationships: [],
            RestorePrerequisites: isGrandMa
                ? [
                    "Restore only to a new empty ShowVault-controlled target.",
                    "An operator must place or import the verified export using the vendor workflow; never restore directly into a live console or onPC tree."
                ]
                : [],
            CompatibilityRules: isGrandMa
                ? [
                    new RecoveryPackageCompatibilityRule(
                        "vendor-forward-only-show-file",
                        "Validate with an equal or newer compatible grandMA software version before operator import."),
                    new RecoveryPackageCompatibilityRule(
                        "source-version-evidence",
                        MaLightingShowExportDiscoveryPluginBase.GetProductVersion(
                            discovery.PluginId,
                            rootPath) is { } version
                            ? $"Export path records grandMA2 version {version}; application load compatibility is not yet verified."
                            : "The export path does not encode a product version; operator confirmation and application validation are required.")
                ]
                : [],
            VerificationRecords: []);
        var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions);
        var packageId = Convert.ToHexStringLower(SHA256.HashData(manifestBytes));
        var packagePath = Path.Combine(_packageDirectory, packageId);

        Directory.CreateDirectory(_packageDirectory);
        EnsureDirectoryIsNotLink(_packageDirectory);
        if (Directory.Exists(packagePath))
        {
            await EnsureExistingPackageMatchesAsync(
                packagePath,
                manifestBytes,
                manifestFiles,
                operationToken);
            return new CreatedRecoveryPackage(packageId, packagePath, manifest);
        }

        await using var stableSource = await CaptureStableSourceAsync(
            discovery,
            rootPath,
            manifestFiles,
            operationToken);
        _sourceSnapshotRaceProbe?.Reached(
            SourceSnapshotRacePoint.SnapshotCaptured,
            string.Empty);

        var stagingPath = Path.Combine(_packageDirectory, $".staging-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(
            stagingPath,
            RecoveryPackageFormat.ContentDirectoryName));
        try
        {
            foreach (var file in manifestFiles)
            {
                operationToken.ThrowIfCancellationRequested();
                var sourcePath = ResolveContainedPath(rootPath, file.RelativePath);
                EnsurePathContainsNoLinks(rootPath, sourcePath);
                var destinationPath = Path.Combine(
                    stagingPath,
                    RecoveryPackageFormat.ContentDirectoryName,
                    file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                _sourceSnapshotRaceProbe?.Reached(
                    SourceSnapshotRacePoint.SourceCopyStarted,
                    file.RelativePath);
                var copiedHash = stableSource is null
                    ? await CopyAndHashAsync(sourcePath, destinationPath, operationToken)
                    : await CopyAndHashAsync(
                        stableSource.GetFile(file.RelativePath),
                        destinationPath,
                        operationToken);
                var copiedLength = new FileInfo(destinationPath).Length;
                if (copiedLength != file.Size ||
                    !string.Equals(copiedHash, file.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Source changed after discovery: {file.RelativePath}");
                }
            }

            if (stableSource is not null)
            {
                await stableSource.ValidateStableAsync(
                    rehashFiles: true,
                    operationToken);
            }

            await File.WriteAllBytesAsync(
                Path.Combine(stagingPath, RecoveryPackageFormat.ManifestFileName),
                manifestBytes,
                operationToken);
            MakeFilesReadOnly(stagingPath);
            try
            {
                Directory.Move(stagingPath, packagePath);
            }
            catch (IOException) when (Directory.Exists(packagePath))
            {
                await EnsureExistingPackageMatchesAsync(
                    packagePath,
                    manifestBytes,
                    manifestFiles,
                    operationToken);
            }
        }
        finally
        {
            if (Directory.Exists(stagingPath))
            {
                DeleteStagingDirectory(stagingPath);
            }
        }

        await EnsureExistingPackageMatchesAsync(
            packagePath,
            manifestBytes,
            manifestFiles,
            operationToken);
        return new CreatedRecoveryPackage(packageId, packagePath, manifest);
    }

    private static async Task<string> CopyAndHashAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        await using var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            65_536,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return await CopyAndHashAsync(source, destinationPath, cancellationToken);
    }

    private static async Task<string> CopyAndHashAsync(
        Stream source,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        await using var destination = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            65_536,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (source.CanSeek)
        {
            source.Position = 0;
        }

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
                await destination.WriteAsync(
                    buffer.AsMemory(0, bytesRead),
                    cancellationToken);
            }

            await destination.FlushAsync(cancellationToken);
            return Convert.ToHexStringLower(hash.GetHashAndReset());
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private async Task<StableSourceSnapshot?> CaptureStableSourceAsync(
        DiscoveryResult discovery,
        string rootPath,
        IReadOnlyList<RecoveryPackageFile> manifestFiles,
        CancellationToken cancellationToken)
    {
        var isPortableBundle = string.Equals(
            discovery.PluginId,
            ResolumeDiscoveryPlugin.PluginId,
            StringComparison.Ordinal);
        var isUserData = string.Equals(
            discovery.PluginId,
            ResolumeUserDataDiscoveryPlugin.PluginId,
            StringComparison.Ordinal);
        var isMaLighting = MaLightingShowExportDiscoveryPluginBase.IsMaLightingPlugin(
            discovery.PluginId);
        if (!isPortableBundle && !isUserData && !isMaLighting)
        {
            return null;
        }

        if (isMaLighting &&
            (!MaLightingShowExportDiscoveryPluginBase.IsAuthorizedRoot(
                _options,
                discovery.PluginId,
                rootPath) ||
             !MaLightingShowExportDiscoveryPluginBase.IsRecognizedRoot(
                 discovery.PluginId,
                 rootPath)))
        {
            throw new UnauthorizedAccessException(
                "grandMA export root is no longer authorized by local Agent configuration.");
        }

        StableSourceSnapshot snapshot;
        try
        {
            snapshot = isPortableBundle
                ? await StableSourceSnapshot.CaptureAsync(
                    rootPath,
                    ResolumeDiscoveryPlugin.MaximumFileLimit,
                    cancellationToken)
                : isUserData
                ? await ResolumeUserDataDiscoveryPlugin.CaptureSnapshotAsync(
                    rootPath,
                    ResolumeUserDataDiscoveryPlugin.MaximumFileLimit,
                    cancellationToken)
                : await MaLightingShowExportDiscoveryPluginBase.CaptureSnapshotAsync(
                    rootPath,
                    MaLightingShowExportDiscoveryPluginBase.MaximumFileLimit,
                    cancellationToken);
        }
        catch (IOException exception) when (isMaLighting)
        {
            throw new IOException("grandMA show export could not be recaptured safely.", exception);
        }
        try
        {
            snapshot.RequireExactFiles(manifestFiles);
            return snapshot;
        }
        catch
        {
            await snapshot.DisposeAsync();
            throw;
        }
    }

    private static string NormalizeRelativePath(string rootPath, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        var fullPath = ResolveContainedPath(rootPath, relativePath);
        return Path.GetRelativePath(rootPath, fullPath)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private static string ResolveContainedPath(string rootPath, string relativePath)
    {
        if (Path.IsPathFullyQualified(relativePath))
        {
            throw new InvalidOperationException("Package file paths must be relative.");
        }

        var fullPath = Path.GetFullPath(Path.Combine(
            rootPath,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var relativeToRoot = Path.GetRelativePath(rootPath, fullPath);
        if (relativeToRoot == ".." ||
            relativeToRoot.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            Path.IsPathFullyQualified(relativeToRoot))
        {
            throw new InvalidOperationException("Package file path escapes the discovery root.");
        }

        return fullPath;
    }

    private static void EnsurePathContainsNoLinks(string rootPath, string sourcePath)
    {
        var relativePath = Path.GetRelativePath(rootPath, sourcePath);
        var currentPath = rootPath;
        EnsurePathIsNotLink(currentPath);
        foreach (var segment in relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries))
        {
            currentPath = Path.Combine(currentPath, segment);
            EnsurePathIsNotLink(currentPath);
        }
    }

    private static async Task EnsureExistingPackageMatchesAsync(
        string packagePath,
        byte[] expectedManifest,
        IReadOnlyList<RecoveryPackageFile> expectedContentFiles,
        CancellationToken cancellationToken)
    {
        EnsureDirectoryIsNotLink(packagePath);

        var expectedFiles = new Dictionary<string, RecoveryPackageFile?>(FileSystemPathComparer)
        {
            [RecoveryPackageFormat.ManifestFileName] = null
        };
        var expectedDirectories = new HashSet<string>(FileSystemPathComparer)
        {
            RecoveryPackageFormat.ContentDirectoryName
        };
        foreach (var file in expectedContentFiles)
        {
            var packageRelativePath = $"{RecoveryPackageFormat.ContentDirectoryName}/{file.RelativePath}";
            if (!expectedFiles.TryAdd(packageRelativePath, file))
            {
                throw new InvalidOperationException(
                    "A recovery package manifest contains duplicate file paths.");
            }

            var parent = Path.GetDirectoryName(
                packageRelativePath.Replace('/', Path.DirectorySeparatorChar));
            while (!string.IsNullOrEmpty(parent))
            {
                expectedDirectories.Add(NormalizePackagePath(parent));
                parent = Path.GetDirectoryName(parent);
            }
        }

        var directories = new Stack<string>();
        directories.Push(packagePath);
        while (directories.TryPop(out var directory))
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var attributes = File.GetAttributes(entry);
                if (IsLink(entry, attributes))
                {
                    throw new InvalidOperationException(
                        "An existing content-addressed package contains a filesystem link.");
                }

                if ((attributes & FileAttributes.Device) != 0)
                {
                    throw new InvalidOperationException(
                        "An existing content-addressed package contains a non-regular entry.");
                }

                var relativePath = NormalizePackagePath(Path.GetRelativePath(packagePath, entry));
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    if (!expectedDirectories.Remove(relativePath))
                    {
                        throw new InvalidOperationException(
                            "An existing content-addressed package contains an unexpected directory.");
                    }

                    directories.Push(entry);
                    continue;
                }

                if (!expectedFiles.Remove(relativePath, out var expectedFile))
                {
                    throw new InvalidOperationException(
                        "An existing content-addressed package contains an unexpected file.");
                }

                if (expectedFile is null)
                {
                    if (!CryptographicOperations.FixedTimeEquals(
                        await File.ReadAllBytesAsync(entry, cancellationToken),
                        expectedManifest))
                    {
                        throw new InvalidOperationException(
                            "An existing content-addressed package has an unexpected manifest.");
                    }

                    continue;
                }

                if (new FileInfo(entry).Length != expectedFile.Size ||
                    !string.Equals(
                        await HashFileAsync(entry, cancellationToken),
                        expectedFile.Sha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "An existing content-addressed package has unexpected file content.");
                }
            }
        }

        if (expectedDirectories.Count != 0 || expectedFiles.Count != 0)
        {
            throw new InvalidOperationException(
                "An existing content-addressed package is incomplete.");
        }
    }

    private static async Task<string> HashFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            65_536,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        EnsurePathIsNotLink(path);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
    }

    private static void EnsureDirectoryIsNotLink(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.Directory) == 0 || IsLink(path, attributes))
        {
            throw new InvalidOperationException(
                "A recovery package directory cannot be a filesystem link.");
        }
    }

    private static void EnsurePathIsNotLink(string path)
    {
        var attributes = File.GetAttributes(path);
        if (IsLink(path, attributes))
        {
            throw new InvalidOperationException(
                "Recovery package paths cannot traverse filesystem links.");
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

    private static string NormalizePackagePath(string path) =>
        path.Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');

    private static void MakeFilesReadOnly(string packagePath)
    {
        foreach (var path in Directory.EnumerateFiles(packagePath, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
        }
    }

    private static void DeleteStagingDirectory(string stagingPath)
    {
        foreach (var path in Directory.EnumerateFiles(stagingPath, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }

        Directory.Delete(stagingPath, recursive: true);
    }

    internal static string ResolvePackageDirectory(AgentOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.PackageDirectory))
        {
            return Path.GetFullPath(options.PackageDirectory);
        }

        var dataDirectory = string.IsNullOrWhiteSpace(options.DataDirectory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "ShowVault",
                "Agent")
            : Path.GetFullPath(options.DataDirectory);
        return Path.Combine(dataDirectory, "packages");
    }
}
