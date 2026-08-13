using ShowVault.Platform.Commercial;
using Xunit;

namespace ShowVault.Platform.Tests;

public sealed class CommercialEntitlementTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-13T12:00:00Z");
    private static readonly ICommercialPlanPolicyCatalog Policies =
        new SyntheticCommercialPlanPolicyCatalog();

    [Fact]
    public void Missing_or_inactive_license_denies_closed()
    {
        Assert.Equal(CommercialReasonCodes.LicenseMissing,
            CommercialEntitlementEvaluator.Evaluate(null, Subscription(), Now, Policies).ReasonCode);
        foreach (var state in new[]
                 {
                     CommercialLicenseState.Pending,
                     CommercialLicenseState.Refunded,
                     CommercialLicenseState.Revoked
                 })
        {
            Assert.Equal(CommercialReasonCodes.LicenseInactive,
                CommercialEntitlementEvaluator.Evaluate(License(state), Subscription(), Now,
                    Policies).ReasonCode);
        }
        Assert.Equal(CommercialReasonCodes.LicenseInactive,
            CommercialEntitlementEvaluator.Evaluate(
                License(effectiveAt: Now.AddTicks(1)), Subscription(), Now, Policies).ReasonCode);
    }

    [Theory]
    [InlineData(ServiceSubscriptionState.Trialing, true)]
    [InlineData(ServiceSubscriptionState.Active, true)]
    [InlineData(ServiceSubscriptionState.Paused, false)]
    [InlineData(ServiceSubscriptionState.Canceled, false)]
    public void Subscription_state_is_evaluated_deterministically(
        ServiceSubscriptionState state, bool eligible)
    {
        var result = CommercialEntitlementEvaluator.Evaluate(
            License(), Subscription(state), Now, Policies);

        Assert.Equal(eligible, result.Eligible);
        Assert.Equal(eligible ? CommercialReasonCodes.Eligible :
            CommercialReasonCodes.SubscriptionInactive, result.ReasonCode);
    }

    [Fact]
    public void Past_due_is_eligible_only_strictly_before_grace_deadline()
    {
        Assert.True(CommercialEntitlementEvaluator.Evaluate(License(),
            Subscription(ServiceSubscriptionState.PastDue, Now.AddTicks(1)), Now, Policies).Eligible);
        Assert.Equal(CommercialReasonCodes.GraceExpired,
            CommercialEntitlementEvaluator.Evaluate(License(),
                Subscription(ServiceSubscriptionState.PastDue, Now), Now, Policies).ReasonCode);
        Assert.Equal(CommercialReasonCodes.GraceExpired,
            CommercialEntitlementEvaluator.Evaluate(License(),
                Subscription(ServiceSubscriptionState.PastDue, null), Now, Policies).ReasonCode);
    }

    [Fact]
    public void Missing_subscription_and_unknown_policy_deny_closed()
    {
        Assert.Equal(CommercialReasonCodes.SubscriptionMissing,
            CommercialEntitlementEvaluator.Evaluate(License(), null, Now, Policies).ReasonCode);
        var unsupported = Subscription();
        unsupported.PlanCode = "unknown";
        Assert.Equal(CommercialReasonCodes.PlanUnsupported,
            CommercialEntitlementEvaluator.Evaluate(License(), unsupported, Now, Policies).ReasonCode);
    }

    [Fact]
    public void Disabled_non_development_catalog_denies_even_synthetic_plan()
    {
        var result = CommercialEntitlementEvaluator.Evaluate(License(), Subscription(), Now,
            new DisabledCommercialPlanPolicyCatalog());

        Assert.False(result.Eligible);
        Assert.Equal(CommercialReasonCodes.PlanUnsupported, result.ReasonCode);
    }

    private static CommercialLicense License(
        CommercialLicenseState state = CommercialLicenseState.Active,
        DateTimeOffset? effectiveAt = null) => new()
        {
            State = state,
            EffectiveAt = effectiveAt ?? Now.AddDays(-1)
        };

    private static ServiceSubscription Subscription(
        ServiceSubscriptionState state = ServiceSubscriptionState.Active,
        DateTimeOffset? graceEndsAt = null) => new()
        {
            State = state,
            PlanCode = SyntheticCommercialPlanPolicyCatalog.PlanCode,
            GraceEndsAt = graceEndsAt
        };
}
