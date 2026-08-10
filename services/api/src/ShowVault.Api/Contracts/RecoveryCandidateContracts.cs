namespace ShowVault.Api.Contracts;

public sealed record RecoveryCandidateSummary(
    Guid Id,
    Guid AgentId,
    string AgentName,
    string PluginId,
    string ProductName,
    string CandidateType,
    string Evidence,
    string Decision,
    DateTimeOffset DetectedAt,
    DateTimeOffset? DecidedAt,
    Guid? ValidationCommandId,
    string? ValidationStatus,
    int? ValidationFileCount,
    bool? ValidationTruncated,
    string? ValidationMessage,
    DateTimeOffset? ValidatedAt,
    bool DirectDesktopScan = false,
    string? CandidateKey = null);

public sealed record SubmitComputerScanRequest(IReadOnlyList<string> CandidateKeys);

public sealed record SubmitComputerScanResponse(
    Guid ScanId,
    int CandidateCount,
    DateTimeOffset CompletedAt);

public sealed record DecideRecoveryCandidateRequest(bool Approved);

public sealed record ValidateRecoveryCandidateRequest(int MaxFiles = 1_000);
