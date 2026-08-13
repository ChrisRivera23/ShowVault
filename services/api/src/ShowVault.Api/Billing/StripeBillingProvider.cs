namespace ShowVault.Api.Billing;

using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

public sealed class StripeBillingProvider(
    HttpClient httpClient,
    IOptions<BillingOptions> billingOptions,
    IOptions<StripeApiOptions> stripeOptions,
    IBillingOfferingCatalog offerings,
    TimeProvider clock) : IBillingProvider
{
    public const string SupportedApiVersion = "2026-07-29.dahlia";
    private const int MaximumResponseBytes = 2 * 1024 * 1024;

    public bool IsAvailable => billingOptions.Value.TryGetReturnOrigin(out _) &&
        stripeOptions.Value.IsValid() && offerings.Current is not null &&
        string.Equals(billingOptions.Value.ProviderApiVersion, SupportedApiVersion,
            StringComparison.Ordinal);

    public async Task<BillingHostedSession> CreateCheckoutAsync(
        BillingCheckoutCommand command, string idempotencyKey,
        CancellationToken cancellationToken)
    {
        EnsureAvailable();
        if (command.Environment != ShowVault.Platform.Billing.BillingProviderEnvironment.Sandbox ||
            !string.Equals(command.Offering.Code, offerings.Current!.Code,
                StringComparison.Ordinal))
            throw new InvalidOperationException("The billing command is not allowlisted.");
        var correlation = command.AttemptId.ToString("N");
        using var document = await SendFormAsync("v1/checkout/sessions", new()
        {
            ["mode"] = "subscription",
            ["payment_method_types[0]"] = "card",
            ["line_items[0][price]"] = command.Offering.RecurringPriceId,
            ["line_items[0][quantity]"] = "1",
            ["line_items[1][price]"] = command.Offering.LicensePriceId,
            ["line_items[1][quantity]"] = "1",
            ["success_url"] = command.SuccessUrl.AbsoluteUri,
            ["cancel_url"] = command.CancelUrl.AbsoluteUri,
            ["expires_at"] = clock.GetUtcNow()
                .AddMinutes(billingOptions.Value.CheckoutLifetimeMinutes)
                .ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            ["client_reference_id"] = correlation,
            ["metadata[showvault_attempt_id]"] = correlation,
            ["metadata[showvault_organization_id]"] = command.OrganizationId.ToString("D"),
            ["metadata[showvault_offering_code]"] = command.Offering.Code,
            ["subscription_data[metadata][showvault_attempt_id]"] = correlation,
            ["subscription_data[metadata][showvault_organization_id]"] =
                command.OrganizationId.ToString("D"),
            ["subscription_data[metadata][showvault_offering_code]"] = command.Offering.Code
        }, idempotencyKey, cancellationToken);
        return HostedSession(document.RootElement, "cs_test_", "checkout.stripe.com");
    }

    public async Task<BillingHostedSession> CreatePortalAsync(
        string customerId, Uri returnUrl, CancellationToken cancellationToken)
    {
        EnsureAvailable();
        if (!Identifier(customerId, "cus_"))
            throw new InvalidOperationException("The provider Customer ID is invalid.");
        using var document = await SendFormAsync("v1/billing_portal/sessions", new()
        {
            ["customer"] = customerId,
            ["return_url"] = returnUrl.AbsoluteUri
        }, null, cancellationToken);
        return HostedSession(document.RootElement, "bps_", "billing.stripe.com");
    }

    public async Task<BillingProviderSnapshot?> RetrieveCurrentStateAsync(
        string eventType, string providerObjectId, CancellationToken cancellationToken)
    {
        EnsureAvailable();
        var sessionId = await ResolveSessionIdAsync(eventType, providerObjectId,
            cancellationToken);
        return sessionId is null ? null :
            await BuildSnapshotAsync(sessionId, cancellationToken);
    }

    private async Task<string?> ResolveSessionIdAsync(string eventType, string objectId,
        CancellationToken cancellationToken)
    {
        if (eventType.StartsWith("checkout.session.", StringComparison.Ordinal))
            return Identifier(objectId, "cs_test_") ? objectId : null;
        string? subscriptionId;
        if (eventType.StartsWith("customer.subscription.", StringComparison.Ordinal))
            subscriptionId = Identifier(objectId, "sub_") ? objectId : null;
        else if (eventType.StartsWith("invoice.", StringComparison.Ordinal))
            subscriptionId = await SubscriptionFromInvoiceAsync(objectId, cancellationToken);
        else if (eventType.StartsWith("charge.dispute.", StringComparison.Ordinal))
        {
            using var dispute = await GetAsync($"v1/disputes/{Escape(objectId)}",
                cancellationToken);
            subscriptionId = await SubscriptionFromChargeAsync(
                ObjectId(dispute.RootElement, "charge") ?? "", cancellationToken);
        }
        else if (eventType.StartsWith("charge.", StringComparison.Ordinal))
            subscriptionId = await SubscriptionFromChargeAsync(objectId, cancellationToken);
        else return null;
        if (!Identifier(subscriptionId, "sub_")) return null;
        using var sessions = await GetAsync(
            $"v1/checkout/sessions?subscription={Escape(subscriptionId!)}&limit=10",
            cancellationToken);
        var values = sessions.RootElement.GetProperty("data").EnumerateArray()
            .Select(value => RequiredString(value, "id", 255)).Distinct().ToList();
        return values.Count == 1 && Identifier(values[0], "cs_test_") ? values[0] : null;
    }

    private async Task<string?> SubscriptionFromInvoiceAsync(string invoiceId,
        CancellationToken cancellationToken)
    {
        if (!Identifier(invoiceId, "in_")) return null;
        using var invoice = await GetAsync($"v1/invoices/{Escape(invoiceId)}",
            cancellationToken);
        return SubscriptionId(invoice.RootElement);
    }

    private async Task<string?> SubscriptionFromChargeAsync(string chargeId,
        CancellationToken cancellationToken)
    {
        if (!Identifier(chargeId, "ch_")) return null;
        using var charge = await GetAsync($"v1/charges/{Escape(chargeId)}", cancellationToken);
        var paymentIntentId = ObjectId(charge.RootElement, "payment_intent");
        if (!Identifier(paymentIntentId, "pi_")) return null;
        using var invoicePayments = await GetAsync(
            "v1/invoice_payments?payment%5Btype%5D=payment_intent&" +
            $"payment%5Bpayment_intent%5D={Escape(paymentIntentId!)}&limit=10",
            cancellationToken);
        if (invoicePayments.RootElement.GetProperty("has_more").GetBoolean()) return null;
        var invoiceIds = invoicePayments.RootElement.GetProperty("data").EnumerateArray()
            .Select(value => ObjectId(value, "invoice")).Where(value => value is not null)
            .Distinct(StringComparer.Ordinal).ToList();
        if (invoiceIds.Count != 1) return null;
        var invoiceId = invoiceIds[0];
        return await SubscriptionFromInvoiceAsync(invoiceId ?? "", cancellationToken);
    }

    private async Task<BillingProviderSnapshot?> BuildSnapshotAsync(string sessionId,
        CancellationToken cancellationToken)
    {
        using var session = await GetAsync($"v1/checkout/sessions/{Escape(sessionId)}",
            cancellationToken);
        var root = session.RootElement;
        if (root.GetProperty("livemode").GetBoolean()) return null;
        var metadata = root.GetProperty("metadata");
        if (!Guid.TryParse(RequiredString(metadata, "showvault_organization_id", 36),
                out var organizationId)) return null;
        var offeringCode = RequiredString(metadata, "showvault_offering_code", 80);
        var customerId = ObjectId(root, "customer");
        var subscriptionId = ObjectId(root, "subscription");
        if (!Identifier(customerId, "cus_") || !Identifier(subscriptionId, "sub_")) return null;

        using var lineItems = await GetAsync(
            $"v1/checkout/sessions/{Escape(sessionId)}/line_items?limit=10",
            cancellationToken);
        if (lineItems.RootElement.GetProperty("has_more").GetBoolean()) return null;
        var checkoutPrices = PriceIds(lineItems.RootElement.GetProperty("data"));
        if (checkoutPrices.Count != 2) return null;

        using var subscription = await GetAsync(
            $"v1/subscriptions/{Escape(subscriptionId!)}", cancellationToken);
        var subscriptionRoot = subscription.RootElement;
        using var invoices = await GetAsync(
            $"v1/invoices?subscription={Escape(subscriptionId!)}&limit=100",
            cancellationToken);
        if (invoices.RootElement.GetProperty("has_more").GetBoolean()) return null;
        var invoiceValues = invoices.RootElement.GetProperty("data").EnumerateArray().ToList();
        if (invoiceValues.Count == 0) return null;
        var initialInvoice = invoiceValues.OrderBy(value => RequiredInt64(value, "created"))
            .First();
        var initialInvoiceId = RequiredString(initialInvoice, "id", 255);
        using var invoiceDocument = await GetAsync(
            $"v1/invoices/{Escape(initialInvoiceId)}", cancellationToken);
        initialInvoice = invoiceDocument.RootElement;
        if (!string.Equals(SubscriptionId(initialInvoice), subscriptionId,
                StringComparison.Ordinal)) return null;
        using var invoiceLines = await GetAsync(
            $"v1/invoices/{Escape(initialInvoiceId)}/lines?limit=100", cancellationToken);
        if (invoiceLines.RootElement.GetProperty("has_more").GetBoolean()) return null;
        var invoicePrices = PriceIds(invoiceLines.RootElement.GetProperty("data"));

        var configured = offerings.Find(offeringCode);
        if (configured is null || checkoutPrices.Count(value => value ==
                configured.RecurringPriceId) != 1 || checkoutPrices.Count(value => value ==
                configured.LicensePriceId) != 1 ||
            invoicePrices.Count(value => value == configured.LicensePriceId) != 1) return null;

        var subscriptionItems = subscriptionRoot.GetProperty("items");
        if (subscriptionItems.GetProperty("has_more").GetBoolean()) return null;
        var recurringItems = subscriptionItems.GetProperty("data").EnumerateArray()
            .Where(value => string.Equals(PriceId(value), configured.RecurringPriceId,
                StringComparison.Ordinal)).ToList();
        if (recurringItems.Count != 1) return null;

        var charge = await InitialChargeAsync(initialInvoice, cancellationToken);
        var paymentState = PaymentState(initialInvoice, charge);
        var status = RequiredString(subscriptionRoot, "status", 40);
        var periodEnd = OptionalInt64(recurringItems[0], "current_period_end");
        var cancelAt = OptionalInt64(subscriptionRoot, "cancel_at");
        var cancelAtPeriodEnd = OptionalBoolean(subscriptionRoot, "cancel_at_period_end") ||
            cancelAt is not null && cancelAt == periodEnd;
        var modifiedSeconds = new[]
        {
            RequiredInt64(root, "created"), RequiredInt64(subscriptionRoot, "created"),
            RequiredInt64(initialInvoice, "created"),
            OptionalInt64(initialInvoice.GetProperty("status_transitions"), "paid_at") ?? 0,
            charge is null ? 0 : RequiredInt64(charge.Value, "created")
        }.Max();
        var revision = Revision(sessionId, customerId!, subscriptionId!, initialInvoiceId,
            checkoutPrices, status, periodEnd, cancelAt, cancelAtPeriodEnd, paymentState, charge);
        return new(organizationId, offeringCode, sessionId, customerId!, subscriptionId!,
            initialInvoiceId, configured.RecurringPriceId, configured.LicensePriceId,
            status, periodEnd is null ? null : DateTimeOffset.FromUnixTimeSeconds(periodEnd.Value),
            cancelAtPeriodEnd, paymentState, DateTimeOffset.FromUnixTimeSeconds(modifiedSeconds),
            revision);
    }

    private async Task<JsonElement?> InitialChargeAsync(JsonElement invoice,
        CancellationToken cancellationToken)
    {
        var invoiceId = RequiredString(invoice, "id", 255);
        using var invoicePayments = await GetAsync(
            $"v1/invoice_payments?invoice={Escape(invoiceId)}&limit=10", cancellationToken);
        var payments = invoicePayments.RootElement;
        if (payments.GetProperty("has_more").GetBoolean()) return null;
        var paymentIntentIds = payments.GetProperty("data").EnumerateArray()
            .Where(value => string.Equals(OptionalString(value.GetProperty("payment"), "type"),
                "payment_intent", StringComparison.Ordinal))
            .Select(value => ObjectId(value.GetProperty("payment"), "payment_intent"))
            .Where(value => Identifier(value, "pi_")).Distinct(StringComparer.Ordinal).ToList();
        if (paymentIntentIds.Count != 1) return null;
        var paymentIntentId = paymentIntentIds[0];
        if (!Identifier(paymentIntentId, "pi_")) return null;
        using var paymentIntent = await GetAsync(
            $"v1/payment_intents/{Escape(paymentIntentId!)}", cancellationToken);
        var chargeId = ObjectId(paymentIntent.RootElement, "latest_charge");
        if (!Identifier(chargeId, "ch_")) return null;
        using var charge = await GetAsync($"v1/charges/{Escape(chargeId!)}", cancellationToken);
        return charge.RootElement.Clone();
    }

    private static BillingLicensePaymentState PaymentState(
        JsonElement invoice, JsonElement? charge)
    {
        if (charge is not null)
        {
            if (OptionalBoolean(charge.Value, "disputed"))
                return BillingLicensePaymentState.Disputed;
            var amount = RequiredInt64(charge.Value, "amount");
            var refunded = RequiredInt64(charge.Value, "amount_refunded");
            if (refunded > 0 && refunded == amount)
                return BillingLicensePaymentState.FullyRefunded;
            if (refunded > 0) return BillingLicensePaymentState.PartialOrAmbiguous;
        }
        return charge is not null &&
            string.Equals(OptionalString(invoice, "status"), "paid", StringComparison.Ordinal)
            ? BillingLicensePaymentState.Paid : BillingLicensePaymentState.Pending;
    }

    private async Task<JsonDocument> SendFormAsync(string path,
        Dictionary<string, string> values, string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        using var request = Request(HttpMethod.Post, path);
        request.Content = new FormUrlEncodedContent(values);
        if (idempotencyKey is not null)
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        return await SendAsync(request, cancellationToken);
    }

    private Task<JsonDocument> GetAsync(string path, CancellationToken cancellationToken) =>
        SendAsync(Request(HttpMethod.Get, path), cancellationToken);

    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, new Uri("https://api.stripe.com/" + path));
        var key = stripeOptions.Value.SecretKey!;
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(key + ":")));
        request.Headers.TryAddWithoutValidation("Stripe-Version",
            billingOptions.Value.ProviderApiVersion);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private async Task<JsonDocument> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        using (request)
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(stripeOptions.Value.RequestTimeoutSeconds));
            using var response = await httpClient.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            var bytes = await ReadBoundedAsync(response.Content, timeout.Token);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Stripe sandbox request failed with HTTP {(int)response.StatusCode}.");
            return JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
        }
    }

    private static async Task<byte[]> ReadBoundedAsync(HttpContent content,
        CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength > MaximumResponseBytes)
            throw new InvalidOperationException("Stripe response exceeded the configured bound.");
        await using var input = await content.ReadAsStreamAsync(cancellationToken);
        await using var output = new MemoryStream();
        var buffer = new byte[16 * 1024];
        while (true)
        {
            var count = await input.ReadAsync(buffer, cancellationToken);
            if (count == 0) break;
            if (output.Length + count > MaximumResponseBytes)
                throw new InvalidOperationException("Stripe response exceeded the configured bound.");
            await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
        }
        return output.ToArray();
    }

    private void EnsureAvailable()
    {
        if (!IsAvailable) throw new InvalidOperationException("Stripe sandbox billing is disabled.");
    }

    private BillingHostedSession HostedSession(JsonElement root, string prefix,
        string expectedHost)
    {
        var id = RequiredString(root, "id", 255);
        var url = new Uri(RequiredString(root, "url", 2048), UriKind.Absolute);
        var expires = OptionalInt64(root, "expires_at") ??
            clock.GetUtcNow().AddMinutes(5).ToUnixTimeSeconds();
        if (!Identifier(id, prefix) || url.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(url.IdnHost, expectedHost, StringComparison.Ordinal))
            throw new InvalidOperationException("Unexpected Stripe hosted session.");
        return new(id, url, DateTimeOffset.FromUnixTimeSeconds(expires));
    }

    private static List<string> PriceIds(JsonElement data) => data.EnumerateArray()
        .Select(PriceId).Where(value => value is not null).Cast<string>().ToList();

    private static string? PriceId(JsonElement line)
    {
        var direct = ObjectId(line, "price");
        if (Identifier(direct, "price_")) return direct;
        if (line.TryGetProperty("pricing", out var pricing) &&
            pricing.TryGetProperty("price_details", out var details))
        {
            var value = OptionalString(details, "price");
            if (Identifier(value, "price_")) return value;
        }
        return null;
    }

    private static string Revision(string sessionId, string customerId,
        string subscriptionId, string invoiceId, IEnumerable<string> prices,
        string status, long? periodEnd, long? cancelAt, bool cancelAtPeriodEnd,
        BillingLicensePaymentState paymentState, JsonElement? charge)
    {
        var material = string.Join('|', new[]
        {
            sessionId, customerId, subscriptionId, invoiceId,
            string.Join(',', prices.Order(StringComparer.Ordinal)), status,
            periodEnd?.ToString(CultureInfo.InvariantCulture) ?? "",
            cancelAt?.ToString(CultureInfo.InvariantCulture) ?? "",
            cancelAtPeriodEnd ? "1" : "0", paymentState.ToString(),
            charge is null ? "" : RequiredInt64(charge.Value, "amount_refunded")
                .ToString(CultureInfo.InvariantCulture),
            charge is null ? "" : (OptionalBoolean(charge.Value, "disputed") ? "1" : "0")
        });
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }

    private static string? ObjectId(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind ==
            JsonValueKind.Null) return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Object => OptionalString(value, "id"),
            _ => null
        };
    }

    private static string? SubscriptionId(JsonElement invoice)
    {
        if (!invoice.TryGetProperty("parent", out var parent) ||
            parent.ValueKind != JsonValueKind.Object ||
            !parent.TryGetProperty("subscription_details", out var details) ||
            details.ValueKind != JsonValueKind.Object) return null;
        return ObjectId(details, "subscription");
    }

    private static string RequiredString(JsonElement element, string property, int maximum)
    {
        var value = OptionalString(element, property);
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximum)
            throw new InvalidOperationException("Stripe response shape is invalid.");
        return value;
    }

    private static string? OptionalString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;
    private static long RequiredInt64(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.TryGetInt64(out var parsed)
            ? parsed : throw new InvalidOperationException("Stripe response shape is invalid.");
    private static long? OptionalInt64(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.Null &&
        value.TryGetInt64(out var parsed) ? parsed : null;
    private static bool OptionalBoolean(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;
    private static bool Identifier(string? value, string prefix) =>
        value is { Length: <= 255 } && value.StartsWith(prefix, StringComparison.Ordinal);
    private static string Escape(string value) => Uri.EscapeDataString(value);
}
