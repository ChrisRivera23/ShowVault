namespace ShowVault.Platform.Commercial;

public enum CommercialLicenseState
{
    Pending,
    Active,
    Refunded,
    Revoked
}

public enum ServiceSubscriptionState
{
    Incomplete,
    Trialing,
    Active,
    PastDue,
    Unpaid,
    Paused,
    Canceled
}

public enum HostedSyncReservationState
{
    Reserved,
    Committed
}

public sealed class CommercialLicense
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string LicenseTypeCode { get; set; } = "";
    public CommercialLicenseState State { get; set; }
    public DateTimeOffset? EffectiveAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public long Revision { get; set; }
}

public sealed class ServiceSubscription
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string PlanCode { get; set; } = "";
    public ServiceSubscriptionState State { get; set; }
    public DateTimeOffset? CurrentPeriodEndsAt { get; set; }
    public DateTimeOffset? GraceEndsAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public long Revision { get; set; }
}

public sealed class OrganizationStorageUsage
{
    public Guid OrganizationId { get; set; }
    public long CommittedBytes { get; set; }
    public long ReservedBytes { get; set; }
    public long Revision { get; set; }
}

public sealed class HostedSyncReservation
{
    public Guid HostedSyncSessionId { get; set; }
    public Guid OrganizationId { get; set; }
    public long LogicalBytes { get; set; }
    public HostedSyncReservationState State { get; set; }
    public DateTimeOffset ReservedAt { get; set; }
    public DateTimeOffset? CommittedAt { get; set; }
    public long Revision { get; set; }
}

public sealed class CommercialAuditEvent
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string? ActorSubject { get; set; }
    public string Action { get; set; } = "";
    public string Outcome { get; set; } = "";
    public string ReasonCode { get; set; } = "";
    public long? RequestedBytes { get; set; }
    public long ReservedBytes { get; set; }
    public long CommittedBytes { get; set; }
    public string CorrelationId { get; set; } = "";
    public string PolicyVersion { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
}
