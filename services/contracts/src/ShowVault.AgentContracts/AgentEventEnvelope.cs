namespace ShowVault.AgentContracts;

public sealed record AgentEventEnvelope(
    Guid EventId,
    Guid AgentId,
    AgentEventType Type,
    string ProtocolVersion,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string Payload)
{
    public static AgentEventEnvelope Create(
        Guid agentId,
        AgentEventType type,
        string correlationId,
        string payload,
        DateTimeOffset occurredAt)
    {
        if (agentId == Guid.Empty)
        {
            throw new ArgumentException("Agent ID must not be empty.", nameof(agentId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentNullException.ThrowIfNull(payload);
        var envelope = new AgentEventEnvelope(
            Guid.NewGuid(),
            agentId,
            type,
            AgentProtocol.Version,
            occurredAt,
            correlationId,
            payload);
        if (!AgentEventValidation.TryValidate(envelope, out var error))
        {
            throw new ArgumentException(error);
        }

        return envelope;
    }
}
