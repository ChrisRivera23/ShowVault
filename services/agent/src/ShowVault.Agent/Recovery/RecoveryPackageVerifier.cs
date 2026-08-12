using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using Microsoft.Extensions.Options;

namespace ShowVault.Agent.Recovery;

public sealed class RecoveryPackageVerifier(IOptions<AgentOptions> options)
{
    internal const long MaximumManifestBytes = 16 * 1024 * 1024;
    private const int LinuxOpenFlags = 0x0008_0000 | 0x0002_0000 | 0x0000_0800;
    private const int MacOsOpenFlags = 0x0100_0000 | 0x0000_0100 | 0x0000_0004;
    private const int SeekCurrent = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly StringComparer FileSystemPathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
    private readonly string _packageDirectory = RecoveryPackageWriter.ResolvePackageDirectory(
        options.Value);

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
        var resolvedPackagePath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(packagePath));
        var expectedPackagePath = Path.TrimEndingDirectorySeparator(Path.Combine(
            _packageDirectory,
            expectedPackageId));

        var packagePathIsAuthorized = FileSystemPathComparer.Equals(
            resolvedPackagePath,
            expectedPackagePath);
        if (!packagePathIsAuthorized)
        {
            structuralIssues.Add("Package directory is outside the configured package store.");
        }
        else if (Directory.Exists(_packageDirectory) && IsLink(_packageDirectory))
        {
            structuralIssues.Add("Configured package directory cannot be a filesystem link.");
        }
        else if (!Directory.Exists(resolvedPackagePath))
        {
            structuralIssues.Add("Package directory is missing.");
        }
        else if (IsLink(resolvedPackagePath))
        {
            structuralIssues.Add("Package directory cannot be a filesystem link.");
        }
        else
        {

            if (!string.Equals(
                Path.GetFileName(resolvedPackagePath),
                expectedPackageId,
                StringComparison.Ordinal))
            {
                structuralIssues.Add("Package directory name does not match the expected package ID.");
            }

            var manifestPath = Path.Combine(
                resolvedPackagePath,
                RecoveryPackageFormat.ManifestFileName);
            if (!File.Exists(manifestPath) ||
                IsLink(manifestPath) ||
                (File.GetAttributes(manifestPath) & FileAttributes.Device) != 0)
            {
                structuralIssues.Add("A regular manifest.json file is required.");
            }
            else
            {
                await using var manifestStream = OpenRegularFile(manifestPath);
                if (manifestStream is null || manifestStream.Length > MaximumManifestBytes)
                {
                    structuralIssues.Add("A bounded regular manifest.json file is required.");
                }
                else
                {
                    manifestBytes = new byte[checked((int)manifestStream.Length)];
                    await manifestStream.ReadExactlyAsync(manifestBytes, cancellationToken);
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
        }

        if (manifest is not null)
        {
            ValidateManifest(expectedAgentId, manifest, structuralIssues);
            if (structuralIssues.Count == 0)
            {
                ValidateLayout(resolvedPackagePath, manifest, structuralIssues);
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
                    resolvedPackagePath,
                    RecoveryPackageFormat.ContentDirectoryName,
                    file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                await using var stream = OpenRegularFile(contentPath);
                if (stream is null)
                {
                    structuralIssues.Add($"Package content must be regular: {file.RelativePath}");
                    cryptographicIssues.Add($"File digest could not be evaluated: {file.RelativePath}");
                    continue;
                }

                if (stream.Length != file.Size)
                {
                    cryptographicIssues.Add($"File size mismatch: {file.RelativePath}");
                    continue;
                }

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

        if (manifest.DiscoveryCommandId == Guid.Empty || manifest.CreatedAt == default)
        {
            issues.Add("Manifest discovery identity and creation time are required.");
        }

        if (manifest.Source is null || manifest.Files is null ||
            manifest.Dependencies is null || manifest.Relationships is null ||
            manifest.RestorePrerequisites is null || manifest.CompatibilityRules is null ||
            manifest.VerificationRecords is null)
        {
            issues.Add("Manifest is missing required fields.");
            return;
        }

        ValidateSource(manifest.Source, issues);
        ValidateDependencies(manifest.Dependencies, issues);
        ValidateRelationships(manifest.Relationships, issues);
        ValidateRequiredStrings(
            manifest.RestorePrerequisites,
            "restore prerequisite",
            issues);
        ValidateCompatibilityRules(manifest.CompatibilityRules, issues);
        ValidateVerificationRecords(manifest.VerificationRecords, issues);

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

    private static void ValidateSource(RecoveryPackageSource source, List<string> issues)
    {
        if (IsMissing(source.Identity) ||
            IsMissing(source.PluginId) ||
            IsMissing(source.PluginVersion) ||
            IsPresentButEmpty(source.ProductVersion) ||
            IsPresentButEmpty(source.FirmwareVersion))
        {
            issues.Add("Manifest source metadata is incomplete.");
        }
    }

    private static void ValidateDependencies(
        IReadOnlyList<RecoveryPackageDependency> dependencies,
        List<string> issues)
    {
        foreach (var dependency in dependencies)
        {
            if (dependency is null ||
                IsMissing(dependency.Kind) ||
                IsMissing(dependency.Identity) ||
                IsPresentButEmpty(dependency.Version))
            {
                issues.Add("Manifest contains invalid dependency metadata.");
            }
        }
    }

    private static void ValidateRelationships(
        IReadOnlyList<RecoveryPackageRelationship> relationships,
        List<string> issues)
    {
        foreach (var relationship in relationships)
        {
            if (relationship is null ||
                IsMissing(relationship.SourceIdentity) ||
                IsMissing(relationship.Relationship) ||
                IsMissing(relationship.TargetIdentity))
            {
                issues.Add("Manifest contains invalid relationship metadata.");
            }
        }
    }

    private static void ValidateRequiredStrings(
        IReadOnlyList<string> values,
        string field,
        List<string> issues)
    {
        if (values.Any(IsMissing))
        {
            issues.Add($"Manifest contains an invalid {field}.");
        }
    }

    private static void ValidateCompatibilityRules(
        IReadOnlyList<RecoveryPackageCompatibilityRule> rules,
        List<string> issues)
    {
        foreach (var rule in rules)
        {
            if (rule is null || IsMissing(rule.Kind) || IsMissing(rule.Requirement))
            {
                issues.Add("Manifest contains invalid compatibility metadata.");
            }
        }
    }

    private static void ValidateVerificationRecords(
        IReadOnlyList<RecoveryPackageVerificationRecord> records,
        List<string> issues)
    {
        foreach (var record in records)
        {
            if (record is null ||
                IsMissing(record.Level) ||
                record.VerifiedAt == default ||
                IsMissing(record.Evidence))
            {
                issues.Add("Manifest contains invalid verification metadata.");
            }
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
                var attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    issues.Add($"Package content cannot contain links: {relativePath}");
                }
                else if ((attributes & FileAttributes.Device) != 0)
                {
                    issues.Add($"Package content must be regular: {relativePath}");
                }
                else if ((attributes & FileAttributes.Directory) != 0)
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

        var segments = path.Split('/');
        return segments.Length > 0 &&
            segments.All(segment =>
                !string.IsNullOrEmpty(segment) && segment is not "." and not "..");
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

    private static bool IsMissing(string? value) => string.IsNullOrWhiteSpace(value);

    private static bool IsPresentButEmpty(string? value) =>
        value is not null && string.IsNullOrWhiteSpace(value);

    private static bool IsLink(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    internal static FileStream? OpenRegularFile(string path)
    {
        try
        {
            if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            {
                return new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    65_536,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
            }

            var flags = OperatingSystem.IsLinux() ? LinuxOpenFlags : MacOsOpenFlags;
            var descriptor = Open(path, flags);
            if (descriptor < 0)
            {
                return null;
            }

            var handle = new SafeFileHandle((IntPtr)descriptor, ownsHandle: true);
            if (Seek(descriptor, 0, SeekCurrent) < 0)
            {
                handle.Dispose();
                return null;
            }

            try
            {
                return new FileStream(handle, FileAccess.Read, 65_536, isAsync: false);
            }
            catch
            {
                handle.Dispose();
                throw;
            }
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    private static extern int Open(string path, int flags);

    [DllImport("libc", EntryPoint = "lseek", SetLastError = true)]
    private static extern long Seek(int descriptor, long offset, int origin);

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
