using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using ShowVault.Agent.Recovery;

namespace ShowVault.LocalEngine;

internal sealed class LocalRestoreCoordinator(
    LocalEngineLimits limits,
    TimeProvider timeProvider,
    Action<string>? testHook)
{
    internal const string PublicationName = "ShowVault Restored Files";
    private const string IntentName = "intent.json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<LocalRestoreResult> RestoreAsync(
        LocalRecoveryEngine engine,
        LocalRestoreRequest request,
        IProgress<LocalRestoreProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRecoveryPointId(request.RecoveryPointId);
        var inspection = await engine.InspectVaultStateAsync(
            request.SelectedVault, cancellationToken);
        if (!inspection.RecoveryPoints.Any(point =>
                point.RecoveryPointId == request.RecoveryPointId &&
                point.LocalStatus == "verified"))
        {
            throw new LocalEngineException("Only a freshly verified recovery point can be restored.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(limits.EffectiveTimeout);
        using var vault = LocalVaultLayout.OpenOrCreate(request.SelectedVault);
        var queue = new LocalVaultQueueStore(vault.QueueDatabasePath);
        await queue.InitializeAsync(timeout.Token);
        var record = await queue.GetQueuedAsync(request.RecoveryPointId, timeout.Token)
            ?? throw new LocalEngineException("The selected recovery point is unavailable.");

        var packageSegments = ValidatePackagePath(record.PackageRelativePath);
        using var backups = vault.Root.OpenDirectoryReadOnly(packageSegments[0]);
        using var product = backups.OpenDirectoryReadOnly(packageSegments[1]);
        using var package = product.OpenDirectoryReadOnly(packageSegments[2]);
        using var manifests = vault.Root.OpenDirectoryReadOnly("Manifests");
        using var independent = manifests.OpenDirectoryReadOnly(request.RecoveryPointId);

        progress?.Report(new("verifying_package", 0, 1));
        var packageNames = package.EnumerateNames();
        if (!packageNames.Order(StringComparer.Ordinal).SequenceEqual(
                new[] { "content", "manifest.json", "summary.txt", "verification.json" }
                    .Order(StringComparer.Ordinal)) ||
            !independent.EnumerateNames().Order(StringComparer.Ordinal).SequenceEqual(
                new[] { "manifest.json", "verification.json" }.Order(StringComparer.Ordinal)))
        {
            throw new LocalEngineException("The selected recovery point has invalid topology.");
        }

        var manifestBytes = await ReadBoundedAsync(package, "manifest.json", 16 * 1024 * 1024,
            timeout.Token);
        var independentManifest = await ReadBoundedAsync(
            independent, "manifest.json", 16 * 1024 * 1024, timeout.Token);
        var evidenceBytes = await ReadBoundedAsync(
            package, "verification.json", 1024 * 1024, timeout.Token);
        var independentEvidence = await ReadBoundedAsync(
            independent, "verification.json", 1024 * 1024, timeout.Token);
        if (!manifestBytes.AsSpan().SequenceEqual(independentManifest) ||
            !evidenceBytes.AsSpan().SequenceEqual(independentEvidence) ||
            !string.Equals(Convert.ToHexStringLower(SHA256.HashData(manifestBytes)),
                request.RecoveryPointId, StringComparison.Ordinal))
        {
            throw new LocalEngineException("The selected recovery point evidence does not match.");
        }

        LocalRecoveryManifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<LocalRecoveryManifest>(manifestBytes, JsonOptions)
                ?? throw new JsonException();
        }
        catch (JsonException)
        {
            throw new LocalEngineException("The selected recovery point manifest is invalid.");
        }
        if (manifest.FormatVersion != "1.0" || manifest.Files.Count is < 1 ||
            manifest.Files.Count > limits.MaximumFileCount || manifest.Dependencies.Count != 0 ||
            manifest.CompatibilityRules.Count != 0)
        {
            throw new LocalEngineException("The selected recovery point is outside Restore bounds.");
        }

        var expectedEvidence = await LocalRecoveryVerifier.VerifyAsync(
            package.Path, request.RecoveryPointId, ReadVerifiedAt(evidenceBytes),
            limits, timeout.Token);
        if (!evidenceBytes.AsSpan().SequenceEqual(
                JsonSerializer.SerializeToUtf8Bytes(expectedEvidence, JsonOptions)) ||
            !package.IsSameDirectoryAt(product, packageSegments[2]))
        {
            throw new LocalEngineException("The selected recovery point changed during verification.");
        }

        using var content = package.OpenDirectoryReadOnly("content");
        await using var source = await StableSourceSnapshot.CaptureBoundedAsync(
            content, limits.MaximumFileCount, limits.MaximumDirectoryCount,
            limits.MaximumRelativePathLength, limits.MaximumFileBytes,
            limits.MaximumTotalBytes, timeout.Token);
        var expectedFiles = manifest.Files.Select(file =>
            new RecoveryPackageFile(file.RelativePath, file.Size, file.Sha256)).ToArray();
        source.RequireExactFiles(expectedFiles);
        source.RequireNoEmptyDirectories();
        foreach (var file in source.Files)
        {
            if (LocalRecoveryEngine.GetLinkCount(source.GetFile(file.RelativePath)) != 1)
                throw new LocalEngineException("The recovery point contains multiply-linked content.");
        }
        progress?.Report(new("verifying_package", 1, 1));

        RejectLexicalOverlap(request.SelectedVault, request.SelectedTarget);
        if (!Directory.Exists(request.SelectedTarget))
            throw new LocalEngineException("The Restore sandbox must already exist.");
        var targetPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(request.SelectedTarget));
        var targetParentPath = Path.GetDirectoryName(targetPath);
        var targetName = Path.GetFileName(targetPath);
        if (string.IsNullOrWhiteSpace(targetParentPath) || string.IsNullOrWhiteSpace(targetName))
            throw new LocalEngineException("The Restore sandbox must have a retained parent.");
        using var targetParent = StableDirectoryTree.OpenReadOnlyNoFollowPath(targetParentPath);
        using var target = targetParent.OpenDirectoryReadOnly(targetName);
        if (!targetParent.HasSameVolume(target))
            throw new LocalEngineException("The Restore sandbox is a mounted filesystem substitution.");
        if (target.HasSameIdentity(vault.Root))
            throw new LocalEngineException("The Restore sandbox and vault must be different folders.");
        if (!target.IsSameDirectoryAt(targetParent, targetName))
            throw new LocalEngineException("The Restore sandbox identity changed.");

        var stageName = $".showvault-restore-{request.RecoveryPointId}";
        await PrepareTargetAsync(target, stageName, request.RecoveryPointId, expectedFiles,
            timeout.Token);
        await queue.MarkInterruptedRestoresFailedAsync(
            request.RecoveryPointId, timeProvider.GetUtcNow(), timeout.Token);
        if (target.EnumerateNames().Contains(PublicationName, StringComparer.Ordinal))
        {
            return await AdoptPublishedAsync(
                vault, queue, target, stageName, request.RecoveryPointId,
                manifestBytes, expectedFiles, timeout.Token);
        }

        var now = timeProvider.GetUtcNow();
        var attemptId = Guid.CreateVersion7(now).ToString("N");
        await queue.RecordRestoreStagingAsync(
            attemptId, request.RecoveryPointId, request.RecoveryPointId,
            manifest.Files.Count, manifest.Files.Sum(file => file.Size), now, timeout.Token);
        StableDirectoryTree? stage = null;
        StableDirectoryTree? restored = null;
        StableSourceSnapshot? stagedSnapshot = null;
        var published = false;
        try
        {
            stage = target.CreateDirectory(stageName);
            await WriteAsync(stage, IntentName, JsonSerializer.SerializeToUtf8Bytes(
                new LocalRestoreIntent("1.0", request.RecoveryPointId, PublicationName), JsonOptions),
                timeout.Token);
            restored = stage.CreateDirectory("restored");
            for (var index = 0; index < source.Files.Count; index++)
            {
                timeout.Token.ThrowIfCancellationRequested();
                var file = source.Files[index];
                await CopyAsync(restored, file.RelativePath, source.GetFile(file.RelativePath),
                    file.Size, file.Sha256, timeout.Token);
                progress?.Report(new("copying", index + 1, source.Files.Count));
                testHook?.Invoke("restore_file_copied");
            }

            stagedSnapshot = await StableSourceSnapshot.CaptureBoundedAsync(
                restored, limits.MaximumFileCount, limits.MaximumDirectoryCount,
                limits.MaximumRelativePathLength, limits.MaximumFileBytes,
                limits.MaximumTotalBytes, timeout.Token);
            stagedSnapshot.RequireExactFiles(expectedFiles);
            stagedSnapshot.RequireNoEmptyDirectories();
            foreach (var file in stagedSnapshot.Files)
            {
                if (LocalRecoveryEngine.GetLinkCount(stagedSnapshot.GetFile(file.RelativePath)) != 1)
                    throw new LocalEngineException("Restore staging contains multiply-linked content.");
            }
            await source.ValidateStableAsync(rehashFiles: true, timeout.Token);
            if (!package.IsSameDirectoryAt(product, packageSegments[2]))
                throw new LocalEngineException("The recovery point identity changed during Restore.");
            RequireTargetNames(target, stageName);
            if (!target.IsSameDirectoryAt(targetParent, targetName))
                throw new LocalEngineException("The Restore sandbox identity changed.");
            timeout.Token.ThrowIfCancellationRequested();
            testHook?.Invoke("restore_staging_verified");
            stage.MoveDirectoryChildTo("restored", restored, target, PublicationName);
            published = true;
            progress?.Report(new("publishing", 1, 1));
            testHook?.Invoke("restore_published");

            using var finalization = new CancellationTokenSource(limits.EffectiveTimeout);
            if (!target.IsSameDirectoryAt(targetParent, targetName))
                throw new LocalEngineException("The Restore sandbox identity changed during publication.");
            await queue.TransitionRestoreAsync(
                attemptId, "staging", "published", timeProvider.GetUtcNow(), finalization.Token);
            await stagedSnapshot.ValidateStableAtAsync(
                target, PublicationName, rehashFiles: true, finalization.Token);
            RequireTargetNames(target, stageName, PublicationName);
            await source.ValidateStableAsync(rehashFiles: true, finalization.Token);
            await queue.TransitionRestoreAsync(
                attemptId, "published", "verified", timeProvider.GetUtcNow(), finalization.Token);
            var result = await WriteEvidenceAndCompleteAsync(
                vault, queue, attemptId, request.RecoveryPointId,
                manifestBytes, manifest.Files.Count, manifest.Files.Sum(file => file.Size),
                finalization.Token);
            progress?.Report(new("completed", 1, 1));
            return result;
        }
        catch (OperationCanceledException) when (!published)
        {
            if (stage is not null) target.DeleteChildTreeIfSame(stageName, stage);
            await queue.RecordRestoreTerminalAsync(
                attemptId, "cancelled", "cancelled", timeProvider.GetUtcNow(), CancellationToken.None);
            throw;
        }
        catch (Exception exception)
        {
            if (published && restored is not null)
                target.DeleteChildTreeIfSame(PublicationName, restored);
            if (stage is not null) target.DeleteChildTreeIfSame(stageName, stage);
            await queue.RecordRestoreTerminalAsync(
                attemptId, "failed", "restore_failed", timeProvider.GetUtcNow(), CancellationToken.None);
            throw exception is LocalEngineException
                ? exception
                : new LocalEngineException("The local Restore could not be completed safely.");
        }
        finally
        {
            if (stagedSnapshot is not null) await stagedSnapshot.DisposeAsync();
            restored?.Dispose();
            stage?.Dispose();
        }
    }

    private async Task<LocalRestoreResult> AdoptPublishedAsync(
        LocalVaultLayout vault,
        LocalVaultQueueStore queue,
        StableDirectoryTree target,
        string stageName,
        string recoveryPointId,
        byte[] manifestBytes,
        IReadOnlyList<RecoveryPackageFile> expectedFiles,
        CancellationToken cancellationToken)
    {
        using var published = target.OpenDirectoryReadOnly(PublicationName);
        await using var snapshot = await StableSourceSnapshot.CaptureBoundedAsync(
            published, limits.MaximumFileCount, limits.MaximumDirectoryCount,
            limits.MaximumRelativePathLength, limits.MaximumFileBytes,
            limits.MaximumTotalBytes, cancellationToken);
        snapshot.RequireExactFiles(expectedFiles);
        snapshot.RequireNoEmptyDirectories();
        RequireTargetNames(target, stageName, PublicationName);
        var completed = await queue.GetCompletedRestoreAsync(recoveryPointId, cancellationToken);
        if (completed is not null)
        {
            var evidenceName = $"{completed.EvidenceId}.json";
            using var reports = vault.Root.OpenDirectoryReadOnly("Reports");
            using var restores = reports.OpenDirectoryReadOnly("Restores");
            var evidenceBytes = await ReadBoundedAsync(
                restores, evidenceName, 1024 * 1024, cancellationToken);
            LocalRestoreEvidence evidence;
            try
            {
                evidence = JsonSerializer.Deserialize<LocalRestoreEvidence>(evidenceBytes, JsonOptions)
                    ?? throw new JsonException();
            }
            catch (JsonException)
            {
                throw new LocalEngineException("Stored Restore evidence is invalid.");
            }
            if (evidence.FormatVersion != "1.0" || !evidence.Passed ||
                evidence.RecoveryPointId != recoveryPointId ||
                evidence.ManifestSha256 != recoveryPointId ||
                evidence.EvidenceSha256 != completed.EvidenceId ||
                evidence.RestoredFileCount != completed.FileCount ||
                evidence.RestoredBytes != completed.TotalBytes ||
                evidence.CompletedAt != completed.CompletedAt ||
                ComputeEvidenceId(evidence.RecoveryPointId, evidence.ManifestSha256,
                    evidence.CompletedAt, evidence.RestoredFileCount,
                    evidence.RestoredBytes) != evidence.EvidenceSha256 ||
                !evidenceBytes.AsSpan().SequenceEqual(
                    JsonSerializer.SerializeToUtf8Bytes(evidence, JsonOptions)))
                throw new LocalEngineException("Stored Restore evidence does not match.");
            return new(recoveryPointId, completed.EvidenceId, completed.FileCount,
                completed.TotalBytes, completed.CompletedAt, "restored");
        }
        var now = timeProvider.GetUtcNow();
        var attemptId = Guid.CreateVersion7(now).ToString("N");
        var totalBytes = expectedFiles.Sum(file => file.Size);
        await queue.RecordRestoreStagingAsync(attemptId, recoveryPointId, recoveryPointId,
            expectedFiles.Count, totalBytes, now, cancellationToken);
        await queue.TransitionRestoreAsync(attemptId, "staging", "published", now, cancellationToken);
        await queue.TransitionRestoreAsync(attemptId, "published", "verified", now, cancellationToken);
        return await WriteEvidenceAndCompleteAsync(
            vault, queue, attemptId, recoveryPointId, manifestBytes,
            expectedFiles.Count, totalBytes, cancellationToken);
    }

    private async Task PrepareTargetAsync(
        StableDirectoryTree target,
        string stageName,
        string recoveryPointId,
        IReadOnlyList<RecoveryPackageFile> expectedFiles,
        CancellationToken cancellationToken)
    {
        var names = target.EnumerateNames();
        if (names.Count == 0) return;
        if (names.Any(name => name != stageName && name != PublicationName) ||
            !names.Contains(stageName, StringComparer.Ordinal))
            throw new LocalEngineException("The Restore sandbox is not empty or owned by this recovery point.");

        using var stage = target.OpenDirectory(stageName);
        var stageNames = stage.EnumerateNames();
        if (!stageNames.Contains(IntentName, StringComparer.Ordinal) ||
            stageNames.Any(name => name is not (IntentName or "restored")))
            throw new LocalEngineException("Restore staging requires operator attention.");
        LocalRestoreIntent intent;
        try
        {
            intent = JsonSerializer.Deserialize<LocalRestoreIntent>(
                await ReadBoundedAsync(stage, IntentName, 4096, cancellationToken), JsonOptions)
                ?? throw new JsonException();
        }
        catch (JsonException)
        {
            throw new LocalEngineException("Restore staging requires operator attention.");
        }
        if (intent != new LocalRestoreIntent("1.0", recoveryPointId, PublicationName))
            throw new LocalEngineException("Restore staging belongs to another operation.");

        if (names.Contains(PublicationName, StringComparer.Ordinal))
        {
            if (stageNames.Count != 1)
                throw new LocalEngineException("Published Restore staging is ambiguous.");
            return;
        }
        if (stageNames.Contains("restored", StringComparer.Ordinal))
        {
            using var partial = stage.OpenDirectoryReadOnly("restored");
            await using var bounded = await StableSourceSnapshot.CaptureBoundedAsync(
                partial, limits.MaximumFileCount, limits.MaximumDirectoryCount,
                limits.MaximumRelativePathLength, limits.MaximumFileBytes,
                limits.MaximumTotalBytes, cancellationToken);
            if (bounded.Files.Count > expectedFiles.Count)
                throw new LocalEngineException("Restore staging exceeds expected bounds.");
        }
        target.DeleteChildTreeIfSame(stageName, stage);
        if (target.EnumerateNames().Count != 0)
            throw new LocalEngineException("Restore staging cleanup requires operator attention.");
    }

    private async Task<LocalRestoreResult> WriteEvidenceAndCompleteAsync(
        LocalVaultLayout vault,
        LocalVaultQueueStore queue,
        string attemptId,
        string recoveryPointId,
        byte[] manifestBytes,
        int fileCount,
        long totalBytes,
        CancellationToken cancellationToken)
    {
        var completedAt = timeProvider.GetUtcNow();
        var manifestSha256 = Convert.ToHexStringLower(SHA256.HashData(manifestBytes));
        var evidenceId = ComputeEvidenceId(
            recoveryPointId, manifestSha256, completedAt, fileCount, totalBytes);
        var evidence = new LocalRestoreEvidence(
            "1.0", recoveryPointId, manifestSha256, completedAt,
            true, fileCount, totalBytes, evidenceId);
        using var reports = vault.Root.OpenDirectory("Reports");
        using var restores = reports.GetOrCreateDirectory("Restores");
        var finalName = $"{evidenceId}.json";
        if (restores.EnumerateNames().Contains(finalName, StringComparer.Ordinal))
        {
            var existing = await ReadBoundedAsync(restores, finalName, 1024 * 1024, cancellationToken);
            if (!existing.AsSpan().SequenceEqual(
                    JsonSerializer.SerializeToUtf8Bytes(evidence, JsonOptions)))
                throw new LocalEngineException("Restore evidence identity conflicts.");
        }
        else
        {
            var temporaryName = $".staging-{attemptId}.json";
            await using var temporary = restores.CreateFile(temporaryName);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(evidence, JsonOptions);
            await temporary.WriteAsync(bytes, cancellationToken);
            await temporary.FlushAsync(cancellationToken);
            restores.MoveChildTo(temporaryName, temporary.SafeFileHandle, restores, finalName);
        }
        await queue.CompleteRestoreAsync(attemptId, evidenceId, completedAt, cancellationToken);
        return new(recoveryPointId, evidenceId, fileCount, totalBytes, completedAt, "restored");
    }

    private static async Task CopyAsync(
        StableDirectoryTree root,
        string relativePath,
        FileStream source,
        long expectedSize,
        string expectedHash,
        CancellationToken cancellationToken)
    {
        var segments = relativePath.Split('/');
        var current = root.Duplicate();
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
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = ArrayPool<byte>.Shared.Rent(65_536);
            try
            {
                int read;
                while ((read = await source.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
                {
                    hash.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
                await output.FlushAsync(cancellationToken);
                if (output.Length != expectedSize || source.Length != expectedSize ||
                    !string.Equals(Convert.ToHexStringLower(hash.GetHashAndReset()),
                        expectedHash, StringComparison.OrdinalIgnoreCase))
                    throw new LocalEngineException("Restored content did not match the recovery point.");
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        finally
        {
            current.Dispose();
        }
    }

    private static string ComputeEvidenceId(
        string recoveryPointId,
        string manifestSha256,
        DateTimeOffset completedAt,
        int fileCount,
        long totalBytes)
    {
        var seed = JsonSerializer.SerializeToUtf8Bytes(new
        {
            formatVersion = "1.0",
            recoveryPointId,
            manifestSha256,
            completedAt,
            passed = true,
            restoredFileCount = fileCount,
            restoredBytes = totalBytes
        }, JsonOptions);
        return Convert.ToHexStringLower(SHA256.HashData(seed));
    }

    private static async Task<byte[]> ReadBoundedAsync(
        StableDirectoryTree directory,
        string name,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        await using var stream = directory.OpenRegularFile(name);
        if (stream.Length is < 2 || stream.Length > maximumBytes)
            throw new LocalEngineException("A bounded local Restore record is invalid.");
        var bytes = new byte[checked((int)stream.Length)];
        await stream.ReadExactlyAsync(bytes, cancellationToken);
        return bytes;
    }

    private static async Task WriteAsync(
        StableDirectoryTree directory,
        string name,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        await using var stream = directory.CreateFile(name);
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static DateTimeOffset ReadVerifiedAt(byte[] evidenceBytes)
    {
        try
        {
            return (JsonSerializer.Deserialize<LocalVerificationEvidence>(evidenceBytes, JsonOptions)
                ?? throw new JsonException()).VerifiedAt;
        }
        catch (JsonException)
        {
            throw new LocalEngineException("The recovery-point evidence is invalid.");
        }
    }

    private static string[] ValidatePackagePath(string relativePath)
    {
        var segments = relativePath.Split('/');
        if (segments.Length != 3 || segments[0] != "Backups" ||
            segments.Any(segment => segment is "" or "." or ".." ||
                segment.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0))
            throw new LocalEngineException("The recovery-point identity is invalid.");
        return segments;
    }

    private static void ValidateRecoveryPointId(string value)
    {
        if (value.Length != 64 || !value.All(character =>
                character is >= '0' and <= '9' or >= 'a' and <= 'f'))
            throw new LocalEngineException("The recovery-point identity is invalid.");
    }

    private static void RequireTargetNames(StableDirectoryTree target, params string[] expected)
    {
        if (!target.EnumerateNames().Order(StringComparer.Ordinal).SequenceEqual(
                expected.Order(StringComparer.Ordinal)))
            throw new LocalEngineException("The Restore sandbox changed during Restore.");
    }

    private static void RejectLexicalOverlap(string vaultPath, string targetPath)
    {
        var vault = Path.TrimEndingDirectorySeparator(Path.GetFullPath(vaultPath));
        var target = Path.TrimEndingDirectorySeparator(Path.GetFullPath(targetPath));
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(vault, target, comparison) ||
            IsWithin(vault, target, comparison) || IsWithin(target, vault, comparison))
            throw new LocalEngineException("The Restore sandbox and vault cannot contain each other.");
    }

    private static bool IsWithin(string path, string parent, StringComparison comparison) =>
        path.StartsWith(parent + Path.DirectorySeparatorChar, comparison);
}
