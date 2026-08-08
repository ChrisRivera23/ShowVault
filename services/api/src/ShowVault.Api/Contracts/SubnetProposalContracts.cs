namespace ShowVault.Api.Contracts;

public sealed record SubnetProposalSummary(Guid Id, Guid AgentId, string AgentName,
    string Network, int PrefixLength, string InterfaceType, string Evidence,
    string Decision, DateTimeOffset DetectedAt, DateTimeOffset? DecidedAt);

public sealed record DecideSubnetProposalRequest(bool Approved);
