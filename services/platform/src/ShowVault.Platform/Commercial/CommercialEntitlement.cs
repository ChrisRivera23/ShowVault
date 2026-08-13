namespace ShowVault.Platform.Commercial;

public static class CommercialReasonCodes
{
    public const string Eligible = "eligible";
    public const string LicenseMissing = "license_missing";
    public const string LicenseInactive = "license_inactive";
    public const string SubscriptionMissing = "subscription_missing";
    public const string SubscriptionInactive = "subscription_inactive";
    public const string GraceExpired = "grace_expired";
    public const string PlanUnsupported = "plan_unsupported";
    public const string StateInconsistent = "state_inconsistent";
    public const string QuotaExceeded = "quota_exceeded";
    public const string BillingAttention = "billing_attention";
}

public sealed record CommercialPlanPolicy(
    string PlanCode,
    long LogicalStorageLimitBytes,
    string PolicyVersion);

public interface ICommercialPlanPolicyCatalog
{
    CommercialPlanPolicy? Find(string planCode);
}

public sealed class SyntheticCommercialPlanPolicyCatalog : ICommercialPlanPolicyCatalog
{
    public const string PlanCode = "synthetic.standard";
    public const string PolicyVersion = "synthetic-1";
    public const long LogicalStorageLimitBytes = 100 * 1024 * 1024;

    public CommercialPlanPolicy? Find(string planCode) =>
        string.Equals(planCode, PlanCode, StringComparison.Ordinal)
            ? new(PlanCode, LogicalStorageLimitBytes, PolicyVersion)
            : null;
}

public sealed class DisabledCommercialPlanPolicyCatalog : ICommercialPlanPolicyCatalog
{
    public CommercialPlanPolicy? Find(string planCode) => null;
}

public sealed record CommercialEntitlement(
    bool Eligible,
    string ReasonCode,
    CommercialPlanPolicy? Policy);

public static class CommercialEntitlementEvaluator
{
    public static CommercialEntitlement Evaluate(
        CommercialLicense? license,
        ServiceSubscription? subscription,
        DateTimeOffset now,
        ICommercialPlanPolicyCatalog policies)
    {
        ArgumentNullException.ThrowIfNull(policies);
        if (license is null)
            return Denied(CommercialReasonCodes.LicenseMissing);
        if (license.State != CommercialLicenseState.Active ||
            license.EffectiveAt is null || license.EffectiveAt > now)
            return Denied(CommercialReasonCodes.LicenseInactive);
        if (subscription is null)
            return Denied(CommercialReasonCodes.SubscriptionMissing);
        var policy = policies.Find(subscription.PlanCode);
        if (policy is null)
            return Denied(CommercialReasonCodes.PlanUnsupported);
        if (subscription.State == ServiceSubscriptionState.PastDue)
            return subscription.GraceEndsAt is not null && now < subscription.GraceEndsAt
                ? Allowed(policy)
                : new(false, CommercialReasonCodes.GraceExpired, policy);
        return subscription.State is ServiceSubscriptionState.Trialing or
            ServiceSubscriptionState.Active
            ? Allowed(policy)
            : new(false, CommercialReasonCodes.SubscriptionInactive, policy);
    }

    private static CommercialEntitlement Allowed(CommercialPlanPolicy policy) =>
        new(true, CommercialReasonCodes.Eligible, policy);

    private static CommercialEntitlement Denied(string reasonCode) =>
        new(false, reasonCode, null);
}
