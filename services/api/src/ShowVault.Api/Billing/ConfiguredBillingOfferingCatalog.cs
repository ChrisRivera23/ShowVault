namespace ShowVault.Api.Billing;

using Microsoft.Extensions.Options;

public sealed class ConfiguredBillingOfferingCatalog(
    IOptions<BillingOptions> billing,
    IOptions<BillingOfferingOptions> offering) : IBillingOfferingCatalog
{
    public BillingOffering? Find(string code)
    {
        var current = Current;
        return current is not null && string.Equals(current.Code, code,
            StringComparison.Ordinal) ? current : null;
    }

    public BillingOffering? Current => billing.Value.TryGetReturnOrigin(out _)
        ? offering.Value.GetOffering() : null;
}
