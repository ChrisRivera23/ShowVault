namespace ShowVault.Api.Billing;

using ShowVault.Platform.Billing;

public sealed record BillingOffering(
    string Code,
    string DisplayName,
    string PlanCode,
    string LicenseTypeCode,
    string RecurringPriceId,
    string LicensePriceId,
    string PolicyVersion);

public interface IBillingOfferingCatalog
{
    BillingOffering? Find(string code);
    BillingOffering? Current { get; }
}

public sealed class DisabledBillingOfferingCatalog : IBillingOfferingCatalog
{
    public BillingOffering? Find(string code) => null;
    public BillingOffering? Current => null;
}

public sealed record BillingCheckoutCommand(
    Guid OrganizationId,
    Guid AttemptId,
    BillingProviderEnvironment Environment,
    BillingOffering Offering,
    Uri SuccessUrl,
    Uri CancelUrl);

public sealed record BillingHostedSession(
    string Id,
    Uri Url,
    DateTimeOffset ExpiresAt);

public enum BillingLicensePaymentState
{
    Pending,
    Paid,
    FullyRefunded,
    PartialOrAmbiguous,
    Disputed
}

public sealed record BillingProviderSnapshot(
    Guid OrganizationId,
    string OfferingCode,
    string CheckoutSessionId,
    string CustomerId,
    string SubscriptionId,
    string InitialInvoiceId,
    string RecurringPriceId,
    string LicensePriceId,
    string SubscriptionStatus,
    DateTimeOffset? CurrentPeriodEndsAt,
    bool CancelAtPeriodEnd,
    BillingLicensePaymentState LicensePaymentState,
    DateTimeOffset ProviderModifiedAt,
    string ProviderRevision);

public interface IBillingProvider
{
    bool IsAvailable { get; }
    Task<BillingHostedSession> CreateCheckoutAsync(
        BillingCheckoutCommand command, string idempotencyKey,
        CancellationToken cancellationToken);
    Task<BillingHostedSession> CreatePortalAsync(
        string customerId, Uri returnUrl, CancellationToken cancellationToken);
    Task<BillingProviderSnapshot?> RetrieveCurrentStateAsync(
        string eventType, string providerObjectId,
        CancellationToken cancellationToken);
}

public sealed class DisabledBillingProvider : IBillingProvider
{
    public bool IsAvailable => false;

    public Task<BillingHostedSession> CreateCheckoutAsync(
        BillingCheckoutCommand command, string idempotencyKey,
        CancellationToken cancellationToken) => Unavailable<BillingHostedSession>();

    public Task<BillingHostedSession> CreatePortalAsync(
        string customerId, Uri returnUrl, CancellationToken cancellationToken) =>
        Unavailable<BillingHostedSession>();

    public Task<BillingProviderSnapshot?> RetrieveCurrentStateAsync(
        string eventType, string providerObjectId,
        CancellationToken cancellationToken) => Unavailable<BillingProviderSnapshot?>();

    private static Task<T> Unavailable<T>() => Task.FromException<T>(
        new InvalidOperationException("Provider billing is disabled."));
}

public sealed class BillingOptions
{
    public const string SectionName = "Billing";
    public bool Enabled { get; set; }
    public BillingProviderEnvironment Environment { get; set; } =
        BillingProviderEnvironment.Sandbox;
    public string? ReturnOrigin { get; set; }
    public string? ProviderApiVersion { get; set; }
    public int CheckoutLifetimeMinutes { get; set; } = 30;

    public bool TryGetReturnOrigin(out Uri origin)
    {
        origin = null!;
        if (!Uri.TryCreate(ReturnOrigin, UriKind.Absolute, out var parsed) ||
            parsed.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(parsed.UserInfo) ||
            !string.IsNullOrEmpty(parsed.Query) || !string.IsNullOrEmpty(parsed.Fragment) ||
            parsed.AbsolutePath != "/") return false;
        origin = parsed;
        return Enabled && Environment == BillingProviderEnvironment.Sandbox &&
            CheckoutLifetimeMinutes is >= 30 and <= 60 &&
            !string.IsNullOrWhiteSpace(ProviderApiVersion) &&
            ProviderApiVersion.Length <= 40;
    }
}

public sealed class StripeApiOptions
{
    public const string SectionName = "Billing:Stripe";
    public string? SecretKey { get; set; }
    public int RequestTimeoutSeconds { get; set; } = 15;

    public bool IsValid() => SecretKey is { Length: <= 255 } key &&
        key.StartsWith("rk_test_", StringComparison.Ordinal) &&
        RequestTimeoutSeconds is >= 2 and <= 30;
}

public sealed class BillingOfferingOptions
{
    public const string SectionName = "Billing:Offering";
    public string? Code { get; set; }
    public string? DisplayName { get; set; }
    public string? PlanCode { get; set; }
    public string? LicenseTypeCode { get; set; }
    public string? RecurringPriceId { get; set; }
    public string? LicensePriceId { get; set; }
    public string? PolicyVersion { get; set; }

    public BillingOffering? GetOffering()
    {
        if (!Bounded(Code, 80) || !Bounded(DisplayName, 120) ||
            !Bounded(PlanCode, 80) || !Bounded(LicenseTypeCode, 80) ||
            !Bounded(PolicyVersion, 80) || !Price(RecurringPriceId) ||
            !Price(LicensePriceId) || RecurringPriceId == LicensePriceId) return null;
        return new(Code!, DisplayName!, PlanCode!, LicenseTypeCode!,
            RecurringPriceId!, LicensePriceId!, PolicyVersion!);
    }

    private static bool Bounded(string? value, int maximum) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maximum;
    private static bool Price(string? value) =>
        Bounded(value, 255) && value!.StartsWith("price_", StringComparison.Ordinal);
}
