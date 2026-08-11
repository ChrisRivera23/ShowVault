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
        var envelope = new AgentCommandEnvelope(
            Guid.NewGuid(),
            agentId,
            type,
            AgentProtocol.Version,
            issuedAt,
            issuedAt.Add(validity),
            correlationId,
            payload);
        if (!AgentCommandValidation.TryValidate(envelope, out var error))
        {
            throw new ArgumentException(error);
        }

        return envelope;
    }
}
