namespace ShowVault.Api.Contracts;

public sealed record SubnetProposalSummary(Guid Id, Guid AgentId, string AgentName,
    string Network, int PrefixLength, string InterfaceType, string Evidence,
    string Decision, DateTimeOffset DetectedAt, DateTimeOffset? DecidedAt,
    Guid? DiscoveryCommandId, string? DiscoveryStatus, int? AttemptedHostCount,
    int? RespondingHostCount, string? DiscoveryMessage, DateTimeOffset? DiscoveredAt);

public sealed record DecideSubnetProposalRequest(bool Approved);

public sealed record DiscoverSubnetRequest(int MaxHosts = 32, int TimeoutMilliseconds = 500);

public sealed record IdentifyMaLightingRequest(int TimeoutMilliseconds = 500);
