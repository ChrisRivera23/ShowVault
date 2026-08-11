using ShowVault.AgentContracts;

namespace ShowVault.Platform.Agents;

public enum IssuedAgentCommandStatus
{
    Pending,
    Acknowledged,
    Expired
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

    public static IssuedAgentCommand FromEnvelope(AgentCommandEnvelope envelope)
    {
        if (!AgentCommandValidation.TryValidate(envelope, out var validationError))
        {
            throw new ArgumentException(validationError, nameof(envelope));
        }

        return new(
            envelope.CommandId,
            envelope.AgentId,
            envelope.Type,
            envelope.ProtocolVersion,
            envelope.IssuedAt,
            envelope.ExpiresAt,
            envelope.CorrelationId,
            envelope.Payload);
    }

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

    public bool Acknowledge(DateTimeOffset acknowledgedAt)
    {
        if (Status == IssuedAgentCommandStatus.Acknowledged)
        {
            return true;
        }

        if (Status != IssuedAgentCommandStatus.Pending)
        {
            return false;
        }

        Status = IssuedAgentCommandStatus.Acknowledged;
        AcknowledgedAt = acknowledgedAt;
        return true;
    }

    public bool Expire()
    {
        if (Status != IssuedAgentCommandStatus.Pending)
        {
            return false;
        }

        Status = IssuedAgentCommandStatus.Expired;
        return true;
    }
}
