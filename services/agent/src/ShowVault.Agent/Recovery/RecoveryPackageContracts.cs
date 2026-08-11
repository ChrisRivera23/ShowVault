namespace ShowVault.Agent.Recovery;

public static class RecoveryPackageFormat
{
    public const string Version = "1.0";
    public const string ManifestFileName = "manifest.json";
    public const string ContentDirectoryName = "content";
}

public sealed record RecoveryPackageManifest(
    string FormatVersion,
    Guid AgentId,
    Guid DiscoveryCommandId,
    RecoveryPackageSource Source,
    DateTimeOffset CreatedAt,
    IReadOnlyList<RecoveryPackageFile> Files,
    IReadOnlyList<RecoveryPackageDependency> Dependencies,
    IReadOnlyList<RecoveryPackageRelationship> Relationships,
    IReadOnlyList<string> RestorePrerequisites,
    IReadOnlyList<RecoveryPackageCompatibilityRule> CompatibilityRules,
    IReadOnlyList<RecoveryPackageVerificationRecord> VerificationRecords);

public sealed record RecoveryPackageSource(
    string Identity,
    string PluginId,
    string PluginVersion,
    string? ProductVersion,
    string? FirmwareVersion);

public sealed record RecoveryPackageFile(
    string RelativePath,
    long Size,
    string Sha256);

public sealed record RecoveryPackageDependency(
    string Kind,
    string Identity,
    string? Version,
    bool Required);

public sealed record RecoveryPackageRelationship(
    string SourceIdentity,
    string Relationship,
    string TargetIdentity);

public sealed record RecoveryPackageCompatibilityRule(
    string Kind,
    string Requirement);

public sealed record RecoveryPackageVerificationRecord(
    string Level,
    DateTimeOffset VerifiedAt,
    bool Passed,
    string Evidence);

public sealed record CreatedRecoveryPackage(
    string PackageId,
    string PackagePath,
    RecoveryPackageManifest Manifest);
