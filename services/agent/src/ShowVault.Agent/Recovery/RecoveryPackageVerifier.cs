using System.Security.Cryptography;
using System.Text.Json;

namespace ShowVault.Agent.Recovery;

public sealed class RecoveryPackageVerifier
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<RecoveryPackageVerificationResult> VerifyAsync(
        Guid verificationId,
        Guid expectedAgentId,
        string expectedPackageId,
        string packagePath,
        DateTimeOffset verifiedAt,
        CancellationToken cancellationToken)
    {
        var structuralIssues = new List<string>();
        var cryptographicIssues = new List<string>();
        RecoveryPackageManifest? manifest = null;
        byte[]? manifestBytes = null;

        if (!Directory.Exists(packagePath))
        {
            structuralIssues.Add("Package directory is missing.");
        }
        else
        {
            if (IsLink(packagePath))
            {
                structuralIssues.Add("Package directory cannot be a filesystem link.");
            }

            if (!string.Equals(
                Path.GetFileName(Path.TrimEndingDirectorySeparator(packagePath)),
                expectedPackageId,
                StringComparison.Ordinal))
            {
                structuralIssues.Add("Package directory name does not match the expected package ID.");
            }

            var manifestPath = Path.Combine(packagePath, RecoveryPackageFormat.ManifestFileName);
            if (!File.Exists(manifestPath) || IsLink(manifestPath))
            {
                structuralIssues.Add("A regular manifest.json file is required.");
            }
            else
            {
                manifestBytes = await File.ReadAllBytesAsync(manifestPath, cancellationToken);
                try
                {
                    manifest = JsonSerializer.Deserialize<RecoveryPackageManifest>(
                        manifestBytes,
                        JsonOptions);
                }
                catch (JsonException)
                {
                    structuralIssues.Add("The manifest is not valid recovery-package JSON.");
                }

                if (manifest is null)
                {
                    structuralIssues.Add("The manifest is empty or incomplete.");
                }
            }
        }

        if (manifest is not null)
        {
            ValidateManifest(expectedAgentId, manifest, structuralIssues);
            if (structuralIssues.Count == 0)
            {
                ValidateLayout(packagePath, manifest, structuralIssues);
            }
        }

        if (manifestBytes is not null)
        {
            var actualPackageId = Convert.ToHexStringLower(SHA256.HashData(manifestBytes));
            if (!string.Equals(actualPackageId, expectedPackageId, StringComparison.Ordinal))
            {
                cryptographicIssues.Add("Manifest digest does not match the package ID.");
            }
        }
        else
        {
            cryptographicIssues.Add("Manifest digest could not be evaluated.");
        }

        if (manifest is not null && structuralIssues.Count == 0)
        {
            foreach (var file in manifest.Files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var contentPath = Path.Combine(
                    packagePath,
                    RecoveryPackageFormat.ContentDirectoryName,
                    file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                var info = new FileInfo(contentPath);
                if (info.Length != file.Size)
                {
                    cryptographicIssues.Add($"File size mismatch: {file.RelativePath}");
                    continue;
                }

                await using var stream = new FileStream(
                    contentPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    65_536,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                var hash = Convert.ToHexStringLower(
                    await SHA256.HashDataAsync(stream, cancellationToken));
                if (!string.Equals(hash, file.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    cryptographicIssues.Add($"File digest mismatch: {file.RelativePath}");
                }
            }
        }
        else if (manifest is not null)
        {
            cryptographicIssues.Add("Content hashes were not evaluated because structure is invalid.");
        }

        var levels = new[]
        {
            CreateLevel("structural", structuralIssues),
            CreateLevel("cryptographic", cryptographicIssues)
        };
        return new RecoveryPackageVerificationResult(
            verificationId,
            expectedPackageId,
            verifiedAt,
            levels.All(level => level.Passed),
            levels);
    }

    public static string Serialize(RecoveryPackageVerificationResult result) =>
        JsonSerializer.Serialize(result, JsonOptions);

    private static void ValidateManifest(
        Guid expectedAgentId,
        RecoveryPackageManifest manifest,
        List<string> issues)
    {
        if (manifest.FormatVersion != RecoveryPackageFormat.Version)
        {
            issues.Add($"Unsupported package format: {manifest.FormatVersion}");
        }

        if (manifest.AgentId != expectedAgentId)
        {
            issues.Add("Manifest Agent ID does not match the verifying Agent.");
        }

        if (manifest.Source is null || manifest.Files is null ||
            manifest.Dependencies is null || manifest.Relationships is null ||
            manifest.RestorePrerequisites is null || manifest.CompatibilityRules is null ||
            manifest.VerificationRecords is null)
        {
            issues.Add("Manifest is missing required fields.");
            return;
        }

        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in manifest.Files)
        {
            if (file is null)
            {
                issues.Add("Manifest contains an empty file entry.");
                continue;
            }

            if (!IsSafeManifestPath(file.RelativePath))
            {
                issues.Add($"Unsafe manifest path: {file.RelativePath}");
            }
            else if (!paths.Add(file.RelativePath))
            {
                issues.Add($"Duplicate manifest path: {file.RelativePath}");
            }

            if (file.Size < 0 || !IsSha256(file.Sha256))
            {
                issues.Add($"Invalid file metadata: {file.RelativePath}");
            }
        }

        if (manifest.Files.Any(file => file is null))
        {
            return;
        }

        if (!manifest.Files.SequenceEqual(
            manifest.Files.OrderBy(file => file.RelativePath, StringComparer.Ordinal)))
        {
            issues.Add("Manifest file entries are not path-sorted.");
        }
    }

    private static void ValidateLayout(
        string packagePath,
        RecoveryPackageManifest manifest,
        List<string> issues)
    {
        var contentPath = Path.Combine(packagePath, RecoveryPackageFormat.ContentDirectoryName);
        if (!Directory.Exists(contentPath) || IsLink(contentPath))
        {
            issues.Add("A regular content directory is required.");
            return;
        }

        var expectedFiles = manifest.Files
            .Where(file => IsSafeManifestPath(file.RelativePath))
            .Select(file => file.RelativePath)
            .ToHashSet(StringComparer.Ordinal);
        var expectedDirectories = manifest.Files
            .Where(file => IsSafeManifestPath(file.RelativePath))
            .SelectMany(file => GetParentPaths(file.RelativePath))
            .ToHashSet(StringComparer.Ordinal);
        var directories = new Queue<string>();
        directories.Enqueue(contentPath);
        while (directories.TryDequeue(out var directory))
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(directory))
            {
                var relativePath = Path.GetRelativePath(contentPath, path)
                    .Replace(Path.DirectorySeparatorChar, '/');
                if (IsLink(path))
                {
                    issues.Add($"Package content cannot contain links: {relativePath}");
                }
                else if (Directory.Exists(path))
                {
                    if (!expectedDirectories.Contains(relativePath))
                    {
                        issues.Add($"Unexpected package directory: {relativePath}");
                    }
                    else
                    {
                        directories.Enqueue(path);
                    }
                }
                else if (!expectedFiles.Remove(relativePath))
                {
                    issues.Add($"Unexpected package file: {relativePath}");
                }
            }
        }

        foreach (var missing in expectedFiles.Order(StringComparer.Ordinal))
        {
            issues.Add($"Missing package file: {missing}");
        }

        foreach (var topLevel in Directory.EnumerateFileSystemEntries(packagePath))
        {
            var name = Path.GetFileName(topLevel);
            if (name != RecoveryPackageFormat.ManifestFileName &&
                name != RecoveryPackageFormat.ContentDirectoryName)
            {
                issues.Add($"Unexpected package entry: {name}");
            }
        }
    }

    private static bool IsSafeManifestPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            Path.IsPathFullyQualified(path) ||
            path.Contains('\\', StringComparison.Ordinal))
        {
            return false;
        }

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length > 0 &&
            segments.All(segment => segment is not "." and not "..");
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

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static bool IsLink(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private static RecoveryPackageVerificationLevel CreateLevel(
        string level,
        IReadOnlyList<string> issues) =>
        new(level, issues.Count == 0, issues.Count == 0 ? ["Passed."] : issues);
}

public sealed record RecoveryPackageVerificationResult(
    Guid VerificationId,
    string PackageId,
    DateTimeOffset VerifiedAt,
    bool Passed,
    IReadOnlyList<RecoveryPackageVerificationLevel> Levels);

public sealed record RecoveryPackageVerificationLevel(
    string Level,
    bool Passed,
    IReadOnlyList<string> Evidence);
