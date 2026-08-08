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
    DateTimeOffset? DecidedAt);

public sealed record DecideRecoveryCandidateRequest(bool Approved);

public sealed record ValidateRecoveryCandidateRequest(int MaxFiles = 1_000);
