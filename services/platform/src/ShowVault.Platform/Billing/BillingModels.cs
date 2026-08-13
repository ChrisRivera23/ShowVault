namespace ShowVault.Platform.Billing;

public enum BillingProviderEnvironment
{
    Sandbox,
    Live
}

public enum BillingPurchaseAttemptState
{
    Creating,
    Open,
    Completed,
    Failed,
    Expired
}

public enum BillingEventProcessingState
{
    Pending,
    Processed,
    Ignored,
    Attention
}

public sealed class BillingAccountBinding
{
    public Guid OrganizationId { get; set; }
    public string Provider { get; set; } = "stripe";
    public BillingProviderEnvironment Environment { get; set; }
    public string ProviderCustomerId { get; set; } = "";
    public string? ProviderSubscriptionId { get; set; }
    public string? InitialInvoiceId { get; set; }
    public string OfferingCode { get; set; } = "";
    public DateTimeOffset? ProviderModifiedAt { get; set; }
    public string? ProviderRevision { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public long Revision { get; set; }
}

public sealed class BillingPurchaseAttempt
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Provider { get; set; } = "stripe";
    public BillingProviderEnvironment Environment { get; set; }
    public string OfferingCode { get; set; } = "";
    public BillingPurchaseAttemptState State { get; set; }
    public string? ActiveSlot { get; set; }
    public string? ProviderSessionId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public long Revision { get; set; }
}

public sealed class BillingEventReceipt
{
    public Guid Id { get; set; }
    public string Provider { get; set; } = "stripe";
    public BillingProviderEnvironment Environment { get; set; }
    public string ProviderEventId { get; set; } = "";
    public string EventType { get; set; } = "";
    public string ProviderObjectId { get; set; } = "";
    public DateTimeOffset ProviderCreatedAt { get; set; }
    public string? ApiVersion { get; set; }
    public string PayloadSha256 { get; set; } = "";
    public Guid? OrganizationId { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public BillingEventProcessingState State { get; set; }
    public string OutcomeCode { get; set; } = "pending";
    public long Revision { get; set; }
}

public sealed class BillingAttention
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string ReasonCode { get; set; } = "";
    public DateTimeOffset OpenedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public long Revision { get; set; }
}
