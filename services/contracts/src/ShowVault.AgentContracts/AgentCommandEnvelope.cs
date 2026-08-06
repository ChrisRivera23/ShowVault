namespace ShowVault.AgentContracts;

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

        var issuedAt = DateTimeOffset.UtcNow;
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
