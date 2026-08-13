namespace ShowVault.LocalEngine;

internal sealed record LocalRecoveryManifest(
    string FormatVersion,
    string CandidateKey,
    string PluginId,
    string PluginVersion,
    string ProductName,
    DateTimeOffset CreatedAt,
    IReadOnlyList<LocalRecoveryFile> Files,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<string> CompatibilityRules);

internal sealed record LocalRecoveryFile(string RelativePath, long Size, string Sha256);

internal sealed record LocalVerificationEvidence(
    string FormatVersion,
    string RecoveryPointId,
    string ManifestSha256,
    DateTimeOffset VerifiedAt,
    bool Passed,
    int VerifiedFileCount,
    long VerifiedBytes,
    string EvidenceSha256);
