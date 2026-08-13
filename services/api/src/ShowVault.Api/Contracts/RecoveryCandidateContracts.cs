namespace ShowVault.Api.Contracts;

public sealed record SubmitComputerScanRequest(IReadOnlyList<string>? CandidateKeys);

public sealed record SubmitComputerScanResponse(
    Guid ScanId,
    int CandidateCount,
    DateTimeOffset CompletedAt);

public sealed record DirectRecoveryCandidateSummary(
    Guid Id,
    string CandidateKey,
    string ProductName,
    string CandidateType,
    string Evidence,
    string Decision,
    DateTimeOffset DetectedAt,
    bool DirectDesktopScan);
