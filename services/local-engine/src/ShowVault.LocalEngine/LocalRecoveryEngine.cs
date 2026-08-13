using System.Security.Cryptography;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using ShowVault.Agent.Recovery;

namespace ShowVault.LocalEngine;

public sealed class LocalRecoveryEngine
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly LocalCatalogAuthorizer _authorizer;
    private readonly LocalEngineLimits _limits;
    private readonly TimeProvider _timeProvider;
    private readonly Action<string>? _testHook;

    public LocalRecoveryEngine(
        LocalCatalogAuthorizer? authorizer = null,
        LocalEngineLimits? limits = null,
        TimeProvider? timeProvider = null)
    {
        _authorizer = authorizer ?? new LocalCatalogAuthorizer();
        _limits = limits ?? new LocalEngineLimits();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    internal LocalRecoveryEngine(
        LocalCatalogAuthorizer authorizer,
        LocalEngineLimits? limits,
        TimeProvider timeProvider,
        Action<string> testHook)
        : this(authorizer, limits, timeProvider)
    {
        _testHook = testHook;
    }

    public async Task<LocalSaveResult> SaveAsync(
        LocalSaveRequest request,
        IProgress<LocalSaveProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var source = _authorizer.Authorize(request.CandidateKey, request.SelectedSource);
        RejectLexicalOverlap(request.SelectedSource, request.SelectedVault);
        using var sourceRoot = StableDirectoryTree.OpenReadOnlyNoFollowPath(request.SelectedSource);
        if (Directory.Exists(request.SelectedVault))
        {
            using var selectedVaultRoot = StableDirectoryTree.OpenReadOnlyNoFollowPath(
                request.SelectedVault);
            if (sourceRoot.HasSameIdentity(selectedVaultRoot))
            {
                throw new LocalEngineException(
                    "The source and local vault must be different folders.");
            }
        }
        using var vault = LocalVaultLayout.OpenOrCreate(request.SelectedVault);
        RejectOverlap(sourceRoot, vault.Root, request.SelectedSource, request.SelectedVault);

        var queue = new LocalVaultQueueStore(vault.QueueDatabasePath);
        await queue.InitializeAsync(cancellationToken);
        var now = _timeProvider.GetUtcNow();
        var operationId = Guid.CreateVersion7(now).ToString("N");
        await queue.RecordStagingAsync(
            operationId, source.CandidateKey, source.ProductName, now, cancellationToken);

        var backupsRoot = Path.Combine(vault.RootPath, "Backups");
        var parentName = SafeName(source.ProductName);
        var parentPath = Path.Combine(backupsRoot, parentName);
        Directory.CreateDirectory(parentPath);
        using var parent = StableDirectoryTree.OpenReadOnlyNoFollowPath(parentPath);
        var stagingName = $".staging-{operationId}";
        using var staging = parent.CreateDirectory(stagingName);
        var published = false;
        string? finalName = null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_limits.EffectiveTimeout);
        try
        {
            progress?.Report(new("enumerating", 0, 1));
            await using var snapshot = await StableSourceSnapshot.CaptureBoundedAsync(
                request.SelectedSource, _limits.MaximumFileCount,
                _limits.MaximumDirectoryCount, _limits.MaximumRelativePathLength,
                _limits.MaximumFileBytes, _limits.MaximumTotalBytes, timeout.Token);
            snapshot.RequireNoEmptyDirectories();
            if (snapshot.Files.Count == 0)
            {
                throw new LocalEngineException("The selected source contains no regular files.");
            }
            _testHook?.Invoke("snapshot_captured");
            timeout.Token.ThrowIfCancellationRequested();
            progress?.Report(new("enumerating", 1, 1));
            foreach (var file in snapshot.Files)
            {
                if (GetLinkCount(snapshot.GetFile(file.RelativePath)) != 1)
                {
                    throw new LocalEngineException(
                        "The selected source contains a multiply-linked file.");
                }
            }
            using var content = staging.CreateDirectory("content");
            for (var index = 0; index < snapshot.Files.Count; index++)
            {
                var file = snapshot.Files[index];
                timeout.Token.ThrowIfCancellationRequested();
                await CopyFileAsync(content, file.RelativePath, snapshot.GetFile(file.RelativePath), timeout.Token);
                _testHook?.Invoke("file_copied");
                timeout.Token.ThrowIfCancellationRequested();
                progress?.Report(new("copying", index + 1, snapshot.Files.Count));
            }
            await snapshot.ValidateStableAsync(rehashFiles: true, timeout.Token);

            var manifest = new LocalRecoveryManifest(
                "1.0", source.CandidateKey, source.PluginId, "desktop-0.2.0",
                source.ProductName, now,
                snapshot.Files.Select(file => new LocalRecoveryFile(
                    file.RelativePath, file.Size, file.Sha256)).ToArray(),
                [], []);
            var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions);
            var recoveryPointId = Convert.ToHexStringLower(SHA256.HashData(manifestBytes));
            await WriteFileAsync(staging, "manifest.json", manifestBytes, timeout.Token);
            await WriteFileAsync(staging, "summary.txt", Encoding.UTF8.GetBytes(
                $"ShowVault Pro recovery point\nSystem: {source.ProductName}\n" +
                $"Created: {now:O}\nFiles: {snapshot.Files.Count}\n" +
                $"Bytes: {snapshot.Files.Sum(file => file.Size)}\n" +
                "Local protection: verified before publication\n" +
                "Cloud synchronization: tracked separately\n"),
                timeout.Token);

            progress?.Report(new("verifying", 0, 1));
            var evidence = await LocalRecoveryVerifier.VerifyAsync(
                staging.Path, recoveryPointId, now, _limits, timeout.Token);
            var evidenceBytes = JsonSerializer.SerializeToUtf8Bytes(evidence, JsonOptions);
            await WriteFileAsync(staging, "verification.json", evidenceBytes, timeout.Token);
            progress?.Report(new("verifying", 1, 1));
            _testHook?.Invoke("staging_verified");
            timeout.Token.ThrowIfCancellationRequested();
            finalName = $"{Timestamp(now)}__{recoveryPointId}";
            if (Directory.Exists(Path.Combine(parentPath, finalName)))
            {
                throw new LocalEngineException("An immutable recovery point already has this identity.");
            }
            parent.RenameChild(stagingName, staging, finalName);
            published = true;
            _testHook?.Invoke("published");
            timeout.Token.ThrowIfCancellationRequested();
            progress?.Report(new("publishing", 1, 1));
            var packageRelativePath = Path.Combine("Backups", parentName, finalName)
                .Replace(Path.DirectorySeparatorChar, '/');

            var manifestsPath = Path.Combine(vault.RootPath, "Manifests");
            using var manifests = StableDirectoryTree.OpenReadOnlyNoFollowPath(manifestsPath);
            var metadataStagingName = $".staging-{operationId}";
            using var metadataStaging = manifests.CreateDirectory(metadataStagingName);
            var metadataPublished = false;
            try
            {
                await WriteFileAsync(metadataStaging, "manifest.json", manifestBytes, timeout.Token);
                await WriteFileAsync(metadataStaging, "verification.json", evidenceBytes, timeout.Token);
                manifests.RenameChild(metadataStagingName, metadataStaging, recoveryPointId);
                metadataPublished = true;
            }
            finally
            {
                if (!metadataPublished)
                {
                    manifests.DeleteChildTreeIfSame(metadataStagingName, metadataStaging);
                }
            }
            _testHook?.Invoke("independent_written");
            timeout.Token.ThrowIfCancellationRequested();

            var finalPath = Path.Combine(parentPath, finalName);
            var reverified = await VerifyPublishedAsync(
                finalPath, Path.Combine(manifestsPath, recoveryPointId),
                recoveryPointId, manifestBytes, evidenceBytes,
                timeout.Token);
            await queue.RecordVerifiedAsync(
                operationId, recoveryPointId, packageRelativePath,
                reverified.VerifiedFileCount, reverified.VerifiedBytes, now, timeout.Token);
            _testHook?.Invoke("queue_verified");
            timeout.Token.ThrowIfCancellationRequested();
            await queue.RecordQueuedAsync(operationId, now, timeout.Token);
            progress?.Report(new("queued", 1, 1));
            return new(
                recoveryPointId, source.ProductName, reverified.VerifiedFileCount,
                reverified.VerifiedBytes, "verified", "queued");
        }
        catch (OperationCanceledException)
        {
            if (published && finalName is not null)
            {
                TryQuarantinePublished(vault, parent, finalName, staging, operationId);
            }
            await queue.RecordTerminalAsync(
                operationId, "cancelled", "cancelled", _timeProvider.GetUtcNow(),
                CancellationToken.None);
            throw;
        }
        catch (Exception exception)
        {
            if (published && finalName is not null)
            {
                TryQuarantinePublished(vault, parent, finalName, staging, operationId);
            }
            await queue.RecordTerminalAsync(
                operationId, "failed", "save_failed", _timeProvider.GetUtcNow(),
                CancellationToken.None);
            throw exception is LocalEngineException
                ? exception
                : new LocalEngineException("The local Save could not be completed safely.");
        }
        finally
        {
            if (!published)
            {
                parent.DeleteChildTreeIfSame(stagingName, staging);
            }
        }
    }

    public Task<LocalSaveResult> SaveAsync(
        LocalSaveRequest request,
        CancellationToken cancellationToken) =>
        SaveAsync(request, progress: null, cancellationToken);

    public Task<LocalRestoreResult> RestoreAsync(
        LocalRestoreRequest request,
        IProgress<LocalRestoreProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        new LocalRestoreCoordinator(_limits, _timeProvider, _testHook)
            .RestoreAsync(this, request, progress, cancellationToken);

    public Task<LocalRestoreResult> RestoreAsync(
        LocalRestoreRequest request,
        CancellationToken cancellationToken) =>
        RestoreAsync(request, progress: null, cancellationToken);

    public async Task<IReadOnlyList<LocalRecoveryPointSummary>> InspectVaultAsync(
        string selectedVault,
        CancellationToken cancellationToken = default) =>
        (await InspectVaultStateAsync(selectedVault, cancellationToken)).RecoveryPoints;

    public async Task<LocalVaultInspection> InspectVaultStateAsync(
        string selectedVault,
        CancellationToken cancellationToken = default)
    {
        using var vault = LocalVaultLayout.OpenOrCreate(selectedVault);
        var queue = new LocalVaultQueueStore(vault.QueueDatabasePath);
        await queue.InitializeAsync(cancellationToken);
        await RepairInterruptedStateAsync(vault, queue, cancellationToken);
        var records = await queue.ListQueuedAsync(_limits.MaximumRecoveryPointCount, cancellationToken);
        var summaries = new List<LocalRecoveryPointSummary>();
        foreach (var record in records)
        {
            var packagePath = ResolveContained(vault.RootPath, record.PackageRelativePath);
            var manifestPath = Path.Combine(
                vault.RootPath, "Manifests", record.RecoveryPointId, "manifest.json");
            var evidencePath = Path.Combine(
                vault.RootPath, "Manifests", record.RecoveryPointId, "verification.json");
            var manifestBytes = await ReadBoundedFileAsync(manifestPath, 16 * 1024 * 1024, cancellationToken);
            var evidenceBytes = await ReadBoundedFileAsync(evidencePath, 1024 * 1024, cancellationToken);
            var verified = await VerifyPublishedAsync(
                packagePath, Path.GetDirectoryName(manifestPath)!, record.RecoveryPointId,
                manifestBytes, evidenceBytes, cancellationToken);
            summaries.Add(new(
                record.RecoveryPointId, record.CandidateKey, record.ProductName,
                verified.VerifiedFileCount, verified.VerifiedBytes, record.CreatedAt,
                "verified", "queued"));
        }
        return new(
            summaries,
            await queue.CountAttentionAsync(cancellationToken),
            await queue.CountRestoreAttentionAsync(cancellationToken));
    }

    private async Task RepairInterruptedStateAsync(
        LocalVaultLayout vault,
        LocalVaultQueueStore queue,
        CancellationToken cancellationToken)
    {
        if (await queue.CountRecoveryPointsAsync(cancellationToken) >
            _limits.MaximumRecoveryPointCount)
        {
            throw new LocalEngineException(
                "The local vault contains too many recovery-point records.");
        }
        var repairable = await queue.ListRepairableAsync(
            _limits.MaximumRecoveryPointCount, cancellationToken);
        foreach (var record in repairable)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (record.Status == "staging")
            {
                QuarantineInterruptedStaging(vault, record);
                await queue.RecordTerminalAsync(
                    record.OperationId, "failed", "restart_interrupted",
                    _timeProvider.GetUtcNow(), cancellationToken);
                continue;
            }

            try
            {
                if (record.RecoveryPointId is null || record.PackageRelativePath is null)
                {
                    throw new LocalEngineException("Verified local state is incomplete.");
                }
                var packagePath = ResolveContained(vault.RootPath, record.PackageRelativePath);
                var independentPath = Path.Combine(
                    vault.RootPath, "Manifests", record.RecoveryPointId);
                var manifest = await ReadBoundedFileAsync(
                    Path.Combine(independentPath, "manifest.json"),
                    16 * 1024 * 1024, cancellationToken);
                var evidence = await ReadBoundedFileAsync(
                    Path.Combine(independentPath, "verification.json"),
                    1024 * 1024, cancellationToken);
                await VerifyPublishedAsync(
                    packagePath, independentPath, record.RecoveryPointId,
                    manifest, evidence, cancellationToken);
                await queue.RecordQueuedAsync(
                    record.OperationId, _timeProvider.GetUtcNow(), cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                if (record.PackageRelativePath is not null)
                {
                    TryQuarantineRelativePackage(
                        vault, record.PackageRelativePath, $"repair-{record.OperationId}");
                }
                await queue.RecordTerminalAsync(
                    record.OperationId, "failed", "restart_reverify_failed",
                    _timeProvider.GetUtcNow(), cancellationToken);
            }
        }

        var known = await queue.ListKnownPackagePathsAsync(
            _limits.MaximumRecoveryPointCount, cancellationToken);
        QuarantineUnknownPackages(vault, known, cancellationToken);
    }

    private static void QuarantineInterruptedStaging(
        LocalVaultLayout vault,
        RepairableRecoveryPoint record)
    {
        if (record.OperationId.Length != 32 ||
            !record.OperationId.All(Uri.IsHexDigit)) return;
        var productPath = Path.Combine(
            vault.RootPath, "Backups", SafeName(record.ProductName));
        if (!Directory.Exists(productPath)) return;
        using var product = StableDirectoryTree.OpenReadOnlyNoFollowPath(productPath);
        var name = $".staging-{record.OperationId}";
        if (!product.EnumerateNames().Contains(name, StringComparer.Ordinal)) return;
        using var staging = product.OpenDirectory(name);
        using var quarantine = StableDirectoryTree.OpenReadOnlyNoFollowPath(
            Path.Combine(vault.RootPath, "Quarantine"));
        product.MoveDirectoryChildTo(
            name, staging, quarantine, $"staging-{record.OperationId}");
    }

    private static bool TryQuarantineRelativePackage(
        LocalVaultLayout vault,
        string relativePath,
        string quarantineName)
    {
        try
        {
            var segments = relativePath.Split('/');
            if (segments.Length != 3 || segments[0] != "Backups") return false;
            using var backups = StableDirectoryTree.OpenReadOnlyNoFollowPath(
                Path.Combine(vault.RootPath, "Backups"));
            using var product = backups.OpenDirectory(segments[1]);
            using var package = product.OpenDirectory(segments[2]);
            using var quarantine = StableDirectoryTree.OpenReadOnlyNoFollowPath(
                Path.Combine(vault.RootPath, "Quarantine"));
            product.MoveDirectoryChildTo(segments[2], package, quarantine, quarantineName);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or Win32Exception)
        {
            return false;
        }
    }

    private void QuarantineUnknownPackages(
        LocalVaultLayout vault,
        IReadOnlySet<string> known,
        CancellationToken cancellationToken)
    {
        using var backups = StableDirectoryTree.OpenReadOnlyNoFollowPath(
            Path.Combine(vault.RootPath, "Backups"));
        using var quarantine = StableDirectoryTree.OpenReadOnlyNoFollowPath(
            Path.Combine(vault.RootPath, "Quarantine"));
        var inspected = 0;
        foreach (var productName in backups.EnumerateNames())
        {
            using var product = backups.OpenDirectory(productName);
            foreach (var packageName in product.EnumerateNames())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (++inspected > _limits.MaximumRecoveryPointCount)
                {
                    throw new LocalEngineException(
                        "The local vault contains too many recovery points.");
                }
                var relative = $"Backups/{productName}/{packageName}";
                if (known.Contains(relative)) continue;
                using var package = product.OpenDirectory(packageName);
                var digest = Convert.ToHexStringLower(
                    SHA256.HashData(Encoding.UTF8.GetBytes(relative)))[..16];
                product.MoveDirectoryChildTo(
                    packageName, package, quarantine, $"orphan-{digest}");
            }
        }
    }

    private async Task<LocalVerificationEvidence> VerifyPublishedAsync(
        string packagePath,
        string manifestsPath,
        string recoveryPointId,
        byte[] expectedManifest,
        byte[] expectedEvidence,
        CancellationToken cancellationToken)
    {
        var packageManifest = await ReadBoundedFileAsync(
            Path.Combine(packagePath, "manifest.json"), 16 * 1024 * 1024, cancellationToken);
        var packageEvidence = await ReadBoundedFileAsync(
            Path.Combine(packagePath, "verification.json"), 1024 * 1024, cancellationToken);
        var independentManifest = await ReadBoundedFileAsync(
            Path.Combine(manifestsPath, "manifest.json"), 16 * 1024 * 1024, cancellationToken);
        var independentEvidence = await ReadBoundedFileAsync(
            Path.Combine(manifestsPath, "verification.json"),
            1024 * 1024, cancellationToken);
        if (!expectedManifest.AsSpan().SequenceEqual(packageManifest) ||
            !expectedManifest.AsSpan().SequenceEqual(independentManifest) ||
            !expectedEvidence.AsSpan().SequenceEqual(packageEvidence) ||
            !expectedEvidence.AsSpan().SequenceEqual(independentEvidence))
        {
            throw new LocalEngineException("Local recovery evidence does not match.");
        }
        LocalVerificationEvidence recorded;
        try
        {
            recorded = JsonSerializer.Deserialize<LocalVerificationEvidence>(
                expectedEvidence, JsonOptions) ?? throw new JsonException();
        }
        catch (JsonException)
        {
            throw new LocalEngineException("Local verification evidence is invalid.");
        }
        if (!recorded.Passed ||
            !string.Equals(recorded.RecoveryPointId, recoveryPointId, StringComparison.Ordinal))
        {
            throw new LocalEngineException("Local verification evidence is not passing.");
        }
        var reverified = await LocalRecoveryVerifier.VerifyAsync(
            packagePath, recoveryPointId, recorded.VerifiedAt, _limits, cancellationToken);
        var reverifiedBytes = JsonSerializer.SerializeToUtf8Bytes(reverified, JsonOptions);
        if (!expectedEvidence.AsSpan().SequenceEqual(reverifiedBytes))
        {
            throw new LocalEngineException("Local verification evidence is stale or invalid.");
        }
        return reverified;
    }

    private static void RejectOverlap(
        StableDirectoryTree source,
        StableDirectoryTree vault,
        string sourcePath,
        string vaultPath)
    {
        if (source.HasSameIdentity(vault))
        {
            throw new LocalEngineException("The source and local vault must be different folders.");
        }
        RejectLexicalOverlap(sourcePath, vaultPath);
    }

    private static void RejectLexicalOverlap(string sourcePath, string vaultPath)
    {
        var sourceFull = Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourcePath));
        var vaultFull = Path.TrimEndingDirectorySeparator(Path.GetFullPath(vaultPath));
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(sourceFull, vaultFull, comparison) ||
            IsWithin(sourceFull, vaultFull, comparison) ||
            IsWithin(vaultFull, sourceFull, comparison))
        {
            throw new LocalEngineException("The source and local vault cannot contain each other.");
        }
    }

    private static bool IsWithin(string path, string candidateParent, StringComparison comparison) =>
        path.StartsWith(candidateParent + Path.DirectorySeparatorChar, comparison);

    private static async Task CopyFileAsync(
        StableDirectoryTree content,
        string relativePath,
        FileStream source,
        CancellationToken cancellationToken)
    {
        var segments = relativePath.Split('/');
        var current = content.Duplicate();
        try
        {
            for (var index = 0; index < segments.Length - 1; index++)
            {
                var next = current.GetOrCreateDirectory(segments[index]);
                current.Dispose();
                current = next;
            }
            await using var output = current.CreateFile(segments[^1]);
            source.Position = 0;
            await source.CopyToAsync(output, cancellationToken);
            await output.FlushAsync(cancellationToken);
        }
        finally
        {
            current.Dispose();
        }
    }

    private static async Task WriteFileAsync(
        StableDirectoryTree directory,
        string name,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        await using var stream = directory.CreateFile(name);
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task<byte[]> ReadBoundedFileAsync(
        string path,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        if (await FileSystemEntityTypeAsync(path) != FileSystemEntityType.File)
        {
            throw new LocalEngineException("Local recovery evidence is missing or linked.");
        }
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read,
            65_536, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length > maximumBytes)
        {
            throw new LocalEngineException("Local recovery evidence exceeds its bound.");
        }
        var bytes = new byte[checked((int)stream.Length)];
        await stream.ReadExactlyAsync(bytes, cancellationToken);
        return bytes;
    }

    private static async Task<FileSystemEntityType> FileSystemEntityTypeAsync(string path)
    {
        await Task.Yield();
        if (!File.Exists(path)) return FileSystemEntityType.Missing;
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            return FileSystemEntityType.Link;
        }
        if ((attributes & FileAttributes.Device) != 0)
        {
            return FileSystemEntityType.Other;
        }
        return FileSystemEntityType.File;
    }

    private static string ResolveContained(string root, string relative)
    {
        if (Path.IsPathFullyQualified(relative) || relative.Split('/').Any(
                segment => segment is "" or "." or ".."))
        {
            throw new LocalEngineException("The local package identity is invalid.");
        }
        var candidate = Path.GetFullPath(Path.Combine(
            root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsWithin(candidate, Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new LocalEngineException("The local package identity escaped its vault.");
        }
        return candidate;
    }

    private static string SafeName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var sanitized = new string(value.Select(character =>
            invalid.Contains(character) || char.IsControl(character) ? '-' : character).ToArray())
            .Trim().Trim('.');
        return string.IsNullOrWhiteSpace(sanitized) ? "Unknown System" : sanitized;
    }

    private static string Timestamp(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH-mm-ss'Z'");

    private static bool TryQuarantinePublished(
        LocalVaultLayout vault,
        StableDirectoryTree parent,
        string finalName,
        StableDirectoryTree package,
        string operationId)
    {
        try
        {
            using var quarantine = StableDirectoryTree.OpenReadOnlyNoFollowPath(
                Path.Combine(vault.RootPath, "Quarantine"));
            parent.MoveDirectoryChildTo(
                finalName, package, quarantine, $"failed-{operationId}");
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or Win32Exception)
        {
            return false;
        }
    }

    internal static ulong GetLinkCount(FileStream stream)
    {
        if (OperatingSystem.IsWindows())
        {
            if (!GetFileInformationByHandle(
                    stream.SafeFileHandle.DangerousGetHandle(), out var information))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            return information.NumberOfLinks;
        }

        var descriptor = checked((int)stream.SafeFileHandle.DangerousGetHandle());
        if (OperatingSystem.IsMacOS())
        {
            if (MacFstat(descriptor, out var status) != 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            return status.LinkCount;
        }

        if (LinuxFstat(descriptor, out var linuxStatus) != 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        return linuxStatus.LinkCount;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        IntPtr file,
        out WindowsFileInformation information);

    [DllImport("libc", EntryPoint = "fstat", SetLastError = true)]
    private static extern int MacFstat(int file, out MacStat status);

    [DllImport("libc", EntryPoint = "fstat", SetLastError = true)]
    private static extern int LinuxFstat(int file, out LinuxStat status);

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowsFileInformation
    {
        public uint FileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MacStat
    {
        public int Device;
        public ushort Mode;
        public ushort LinkCount;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 136)]
        public byte[] Remaining;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LinuxStat
    {
        public ulong Device;
        public ulong Inode;
        public ulong LinkCount;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 120)]
        public byte[] Remaining;
    }

    private enum FileSystemEntityType { Missing, File, Link, Other }
}
