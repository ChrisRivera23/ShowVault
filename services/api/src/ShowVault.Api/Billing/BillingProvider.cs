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
        return CheckoutLifetimeMinutes is >= 5 and <= 60 &&
            !string.IsNullOrWhiteSpace(ProviderApiVersion) &&
            ProviderApiVersion.Length <= 40;
    }
}
