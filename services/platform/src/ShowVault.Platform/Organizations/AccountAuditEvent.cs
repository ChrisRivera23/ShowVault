namespace ShowVault.Platform.Organizations;

public sealed class AccountAuditEvent
{
    private AccountAuditEvent()
    {
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string ActorSubject { get; private set; } = "";
    public string TargetEntityType { get; private set; } = "";
    public Guid TargetEntityId { get; private set; }
    public string Action { get; private set; } = "";
    public string Outcome { get; private set; } = "";
    public string ReasonCode { get; private set; } = "";
    public string CorrelationId { get; private set; } = "";
    public string PolicyVersion { get; private set; } = "";
    public DateTimeOffset OccurredAt { get; private set; }

    public static AccountAuditEvent Create(
        Guid organizationId,
        string actorSubject,
        string targetEntityType,
        Guid targetEntityId,
        string action,
        string outcome,
        string reasonCode,
        string correlationId,
        string policyVersion,
        DateTimeOffset occurredAt)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException("Organization ID must not be empty.", nameof(organizationId));
        if (targetEntityId == Guid.Empty)
            throw new ArgumentException("Target entity ID must not be empty.", nameof(targetEntityId));

        return new AccountAuditEvent
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            ActorSubject = Bounded(actorSubject, 255, nameof(actorSubject)),
            TargetEntityType = Bounded(targetEntityType, 40, nameof(targetEntityType)),
            TargetEntityId = targetEntityId,
            Action = Bounded(action, 80, nameof(action)),
            Outcome = Bounded(outcome, 32, nameof(outcome)),
            ReasonCode = Bounded(reasonCode, 80, nameof(reasonCode)),
            CorrelationId = Bounded(correlationId, 100, nameof(correlationId)),
            PolicyVersion = Bounded(policyVersion, 40, nameof(policyVersion)),
            OccurredAt = occurredAt
        };
    }

    private static string Bounded(string value, int maximum, string parameter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameter);
        var normalized = value.Trim();
        if (normalized.Length > maximum)
            throw new ArgumentException($"Value must not exceed {maximum} characters.", parameter);
        return normalized;
    }
}
