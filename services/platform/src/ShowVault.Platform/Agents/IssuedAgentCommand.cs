using ShowVault.AgentContracts;

namespace ShowVault.Platform.Agents;

public enum IssuedAgentCommandStatus
{
    Pending,
    Acknowledged
}

public sealed class IssuedAgentCommand
{
    private IssuedAgentCommand(
        Guid commandId,
        Guid agentId,
        AgentCommandType type,
        string protocolVersion,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        string correlationId,
        string payload)
    {
        CommandId = commandId;
        AgentId = agentId;
        Type = type;
        ProtocolVersion = protocolVersion;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
        CorrelationId = correlationId;
        Payload = payload;
        Status = IssuedAgentCommandStatus.Pending;
    }

    public Guid CommandId { get; }
    public Guid AgentId { get; }
    public AgentCommandType Type { get; }
    public string ProtocolVersion { get; }
    public DateTimeOffset IssuedAt { get; }
    public DateTimeOffset ExpiresAt { get; }
    public string CorrelationId { get; }
    public string Payload { get; }
    public IssuedAgentCommandStatus Status { get; private set; }
    public DateTimeOffset? AcknowledgedAt { get; private set; }

    public static IssuedAgentCommand FromEnvelope(AgentCommandEnvelope envelope) =>
        new(
            envelope.CommandId,
            envelope.AgentId,
            envelope.Type,
            envelope.ProtocolVersion,
            envelope.IssuedAt,
            envelope.ExpiresAt,
            envelope.CorrelationId,
            envelope.Payload);

    public AgentCommandEnvelope ToEnvelope() =>
        new(
            CommandId,
            AgentId,
            Type,
            ProtocolVersion,
            IssuedAt,
            ExpiresAt,
            CorrelationId,
            Payload);

    public void Acknowledge(DateTimeOffset acknowledgedAt)
    {
        if (Status == IssuedAgentCommandStatus.Acknowledged)
        {
            return;
        }

        Status = IssuedAgentCommandStatus.Acknowledged;
        AcknowledgedAt = acknowledgedAt;
    }
}
