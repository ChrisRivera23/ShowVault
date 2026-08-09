namespace ShowVault.Api.Contracts;

public sealed record SubnetProposalSummary(Guid Id, Guid AgentId, string AgentName,
    string Network, int PrefixLength, string InterfaceType, string Evidence,
    string Decision, DateTimeOffset DetectedAt, DateTimeOffset? DecidedAt,
    Guid? DiscoveryCommandId, string? DiscoveryStatus, int? AttemptedHostCount,
    int? RespondingHostCount, int? PassiveCandidateCount, int? FallbackTargetCount,
    string? DiscoveryMessage, DateTimeOffset? DiscoveredAt,
    Guid? IdentificationCommandId, string? IdentificationStatus,
    int? IdentificationAttemptedHostCount, int? IdentifiedHostCount,
    string? IdentifiedProductFamilies, string? IdentificationMessage, DateTimeOffset? IdentifiedAt,
    Guid? YamahaIdentificationCommandId, string? YamahaIdentificationStatus,
    int? YamahaIdentificationAttemptedHostCount, int? YamahaIdentifiedHostCount,
    string? YamahaIdentifiedProductFamilies, string? YamahaIdentificationMessage,
    DateTimeOffset? YamahaIdentifiedAt,
    Guid? GrandMa2IdentificationCommandId, string? GrandMa2IdentificationStatus,
    int? GrandMa2IdentificationAttemptedHostCount, int? GrandMa2IdentifiedHostCount,
    string? GrandMa2IdentifiedProductFamilies, string? GrandMa2IdentificationMessage,
    DateTimeOffset? GrandMa2IdentifiedAt);

public sealed record DecideSubnetProposalRequest(bool Approved);

public sealed record DiscoverSubnetRequest(int MaxHosts = 32, int TimeoutMilliseconds = 500);

public sealed record IdentifyMaLightingRequest(int TimeoutMilliseconds = 500);
public sealed record IdentifyYamahaDmeRequest(int TimeoutMilliseconds = 500);
public sealed record IdentifyGrandMa2Request(int TimeoutMilliseconds = 500);
