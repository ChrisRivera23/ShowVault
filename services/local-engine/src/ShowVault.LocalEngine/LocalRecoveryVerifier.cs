using System.Security.Cryptography;
using System.Text.Json;
using ShowVault.Agent.Recovery;

namespace ShowVault.LocalEngine;

internal static class LocalRecoveryVerifier
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<LocalVerificationEvidence> VerifyAsync(
        string packagePath,
        string expectedId,
        DateTimeOffset verifiedAt,
        LocalEngineLimits limits,
        CancellationToken cancellationToken)
    {
        using var root = StableDirectoryTree.OpenReadOnlyNoFollowPath(packagePath);
        var retained = await RetainVerifiedContentAsync(
            root, expectedId, verifiedAt, limits, cancellationToken);
        await using (retained.Snapshot)
        {
            return retained.Evidence;
        }
    }

    internal static async Task<RetainedVerifiedPackage> RetainVerifiedContentAsync(
        StableDirectoryTree root,
        string expectedId,
        DateTimeOffset verifiedAt,
        LocalEngineLimits limits,
        CancellationToken cancellationToken)
    {
        var names = root.EnumerateNames();
        var expectedNames = names.Contains("verification.json", StringComparer.Ordinal)
            ? new[] { "content", "manifest.json", "summary.txt", "verification.json" }
            : new[] { "content", "manifest.json", "summary.txt" };
        if (!names.Order(StringComparer.Ordinal).SequenceEqual(
                expectedNames.Order(StringComparer.Ordinal)))
        {
            throw new LocalEngineException("Local verification found unexpected package entries.");
        }

        byte[] manifestBytes;
        await using (var stream = root.OpenRegularFile("manifest.json"))
        {
            if (stream.Length is < 2 or > 16 * 1024 * 1024)
            {
                throw new LocalEngineException("The local manifest exceeds its bound.");
            }
            manifestBytes = new byte[checked((int)stream.Length)];
            await stream.ReadExactlyAsync(manifestBytes, cancellationToken);
        }
        var manifestHash = Convert.ToHexStringLower(SHA256.HashData(manifestBytes));
        if (!string.Equals(manifestHash, expectedId, StringComparison.Ordinal))
        {
            throw new LocalEngineException("The local manifest identity is invalid.");
        }

        LocalRecoveryManifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<LocalRecoveryManifest>(manifestBytes, JsonOptions)
                ?? throw new JsonException();
        }
        catch (JsonException)
        {
            throw new LocalEngineException("The local manifest is invalid.");
        }
        if (manifest.FormatVersion != "1.0" || manifest.Files.Count is < 1 ||
            manifest.Files.Count > limits.MaximumFileCount ||
            manifest.Dependencies.Count != 0 || manifest.CompatibilityRules.Count != 0)
        {
            throw new LocalEngineException("The local manifest is outside milestone-2 bounds.");
        }

        using var content = root.OpenDirectoryReadOnly("content");
        var snapshot = await StableSourceSnapshot.CaptureBoundedAsync(
            content, limits.MaximumFileCount, limits.MaximumDirectoryCount,
            limits.MaximumRelativePathLength, limits.MaximumFileBytes,
            limits.MaximumTotalBytes, cancellationToken);
        try
        {
            var expected = manifest.Files.Select(file => new RecoveryPackageFile(
                file.RelativePath, file.Size, file.Sha256)).ToArray();
            snapshot.RequireExactFiles(expected);
            snapshot.RequireNoEmptyDirectories();
            await snapshot.ValidateStableAtAsync(
                root, "content", rehashFiles: true, cancellationToken);
            var totalBytes = manifest.Files.Sum(file => file.Size);
            var evidenceSeed = JsonSerializer.SerializeToUtf8Bytes(new
            {
                recoveryPointId = expectedId,
                manifestSha256 = manifestHash,
                verifiedFileCount = manifest.Files.Count,
                verifiedBytes = totalBytes,
                passed = true
            }, JsonOptions);
            var evidence = new LocalVerificationEvidence(
                "1.0", expectedId, manifestHash, verifiedAt, true,
                manifest.Files.Count, totalBytes,
                Convert.ToHexStringLower(SHA256.HashData(evidenceSeed)));
            return new(evidence, manifest, snapshot);
        }
        catch
        {
            await snapshot.DisposeAsync();
            throw;
        }
    }
}

internal sealed record RetainedVerifiedPackage(
    LocalVerificationEvidence Evidence,
    LocalRecoveryManifest Manifest,
    StableSourceSnapshot Snapshot);
