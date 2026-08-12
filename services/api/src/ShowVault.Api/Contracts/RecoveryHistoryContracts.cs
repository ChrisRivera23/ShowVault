namespace ShowVault.Api.Contracts;

public sealed record RecoveryRunSummary(
    Guid DiscoveryCommandId,
    Guid AgentId,
    string AgentName,
    DateTimeOffset StartedAt,
    string Status,
    IReadOnlyList<RecoveryStageSummary> Stages);

public sealed record RecoveryStageSummary(
    string Stage,
    string Status,
    Guid? CommandId,
    DateTimeOffset? OccurredAt);
