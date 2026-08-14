namespace ShowVault.SupportAdmin.Clients;

public sealed record SupportMemberCount(string Role, string State, long Count);
public sealed record SupportCommercialOverview(string? PlanCode, string LicenseState,
    string SubscriptionState, DateTimeOffset? CurrentPeriodEndsAt,
    DateTimeOffset? GraceEndsAt, bool Eligible, string EligibilityReason,
    long CommittedBytes, long ReservedBytes, long LimitBytes);
public sealed record SupportBillingAttentionOverview(long OpenCount,
    IReadOnlyList<string> ReasonCodes, DateTimeOffset? OldestOpenedAt);
public sealed record SupportHostedSyncCount(string Status, long Count);
public sealed record SupportHostedSyncOverview(IReadOnlyList<SupportHostedSyncCount> Counts,
    DateTimeOffset? LatestActivityAt);
public sealed record SupportActivityOverview(DateTimeOffset? LastAccountActivityAt,
    DateTimeOffset? LastCommercialActivityAt);
public sealed record SupportOrganizationOverview(Guid OrganizationId, string DisplayName,
    IReadOnlyList<SupportMemberCount> Members, SupportCommercialOverview Commercial,
    SupportBillingAttentionOverview BillingAttention, SupportHostedSyncOverview HostedSync,
    SupportActivityOverview Activity);

public sealed class SupportApiUnavailableException : Exception;
