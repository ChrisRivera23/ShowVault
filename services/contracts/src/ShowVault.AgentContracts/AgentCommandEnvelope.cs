namespace ShowVault.AgentContracts;

public sealed record ApplyRecoveryCandidateDecisionPayload(Guid CandidateId, bool Approved);

public sealed record ValidateRecoveryCandidatePayload(Guid CandidateId, int MaxFiles = 1_000);

public sealed record ApplySubnetProposalDecisionPayload(Guid ProposalId, bool Approved);

public sealed record DiscoverApprovedSubnetPayload(
    Guid ProposalId,
    int MaxHosts = 32,
    int TimeoutMilliseconds = 500);

public sealed record IdentifyMaLightingPayload(
    Guid ProposalId,
    Guid DiscoveryCommandId,
    int TimeoutMilliseconds = 500);

public sealed record IdentifyYamahaDmePayload(
    Guid ProposalId,
    Guid DiscoveryCommandId,
    int TimeoutMilliseconds = 500);

public sealed record IdentifyGrandMa2Payload(
    Guid ProposalId,
    Guid DiscoveryCommandId,
    int TimeoutMilliseconds = 500);

public sealed record IdentifyProjectorsPayload(
    Guid ProposalId,
    Guid DiscoveryCommandId,
    int TimeoutMilliseconds = 500);

public sealed record IdentifyBlackmagicVideohubPayload(
    Guid ProposalId,
    Guid DiscoveryCommandId,
    int TimeoutMilliseconds = 500);

public sealed record AgentCommandEnvelope(
    Guid CommandId,
    Guid AgentId,
    AgentCommandType Type,
    string ProtocolVersion,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    string CorrelationId,
    string Payload)
{
    public static AgentCommandEnvelope Create(
        Guid agentId,
        AgentCommandType type,
        string correlationId,
        string payload,
        TimeSpan validity) =>
        Create(agentId, type, correlationId, payload, DateTimeOffset.UtcNow, validity);

    public static AgentCommandEnvelope Create(
        Guid agentId,
        AgentCommandType type,
        string correlationId,
        string payload,
        DateTimeOffset issuedAt,
        TimeSpan validity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentNullException.ThrowIfNull(payload);

        if (agentId == Guid.Empty)
        {
            throw new ArgumentException("Agent ID must not be empty.", nameof(agentId));
        }

        if (validity <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(validity),
                "Command validity must be positive.");
        }

        return new AgentCommandEnvelope(
            Guid.NewGuid(),
            agentId,
            type,
            AgentProtocol.Version,
            issuedAt,
            issuedAt.Add(validity),
            correlationId,
            payload);
    }
}
