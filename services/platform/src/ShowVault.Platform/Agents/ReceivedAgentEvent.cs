using ShowVault.AgentContracts;

namespace ShowVault.Platform.Agents;

public sealed record ReceivedAgentEvent(
    Guid EventId,
    Guid AgentId,
    AgentEventType Type,
    string ProtocolVersion,
    DateTimeOffset OccurredAt,
    DateTimeOffset ReceivedAt,
    string CorrelationId,
    string Payload)
{
    public static ReceivedAgentEvent FromEnvelope(
        AgentEventEnvelope envelope,
        DateTimeOffset receivedAt) =>
        new(
            envelope.EventId,
            envelope.AgentId,
            envelope.Type,
            envelope.ProtocolVersion,
            envelope.OccurredAt,
            receivedAt,
            envelope.CorrelationId,
            envelope.Payload);
}
