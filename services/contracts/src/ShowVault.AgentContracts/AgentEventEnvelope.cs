namespace ShowVault.AgentContracts;

public sealed record AgentEventEnvelope(
    Guid EventId,
    Guid AgentId,
    AgentEventType Type,
    string ProtocolVersion,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string Payload);
