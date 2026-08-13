using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ShowVault.Api.Billing;
using ShowVault.Platform.Billing;
using Xunit;

namespace ShowVault.Api.Tests;

public sealed class StripeBillingProviderTests
{
    [Fact]
    public void Adapter_is_disabled_unless_every_sandbox_setting_is_complete()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, "{}"));
        var billing = Billing();
        var stripe = Options.Create(new StripeApiOptions
        {
            SecretKey = "sk_test_fixture_never_real",
            RequestTimeoutSeconds = 5
        });
        var catalog = Catalog(billing);

        var provider = new StripeBillingProvider(new HttpClient(handler), billing,
            stripe, catalog, TimeProvider.System);

        Assert.False(provider.IsAvailable);
    }

    [Fact]
    public async Task Checkout_uses_exact_catalog_fixed_urls_metadata_and_idempotency()
    {
        var expires = DateTimeOffset.UtcNow.AddMinutes(20).ToUnixTimeSeconds();
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK,
            JsonSerializer.Serialize(new
            {
                id = "cs_test_fixture",
                url = "https://checkout.stripe.com/c/pay/fixture",
                expires_at = expires
            })));
        var (provider, offering) = Provider(handler);
        var organizationId = Guid.Parse("0198a8ad-9c00-7000-8000-000000000001");
        var attemptId = Guid.Parse("0198a8ad-9c00-7000-8000-000000000002");

        var session = await provider.CreateCheckoutAsync(new BillingCheckoutCommand(
            organizationId, attemptId, BillingProviderEnvironment.Sandbox, offering,
            new Uri("https://account.showvault.test/billing/checkout/return"),
            new Uri("https://account.showvault.test/billing/checkout/canceled")),
            attemptId.ToString("N"), CancellationToken.None);

        Assert.Equal("cs_test_fixture", session.Id);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/v1/checkout/sessions", request.Uri.AbsolutePath);
        Assert.Equal(StripeBillingProvider.SupportedApiVersion, request.StripeVersion);
        Assert.Equal(attemptId.ToString("N"), request.IdempotencyKey);
        Assert.Equal("subscription", request.Form["mode"]);
        Assert.Equal("card", request.Form["payment_method_types[0]"]);
        Assert.Equal(offering.RecurringPriceId, request.Form["line_items[0][price]"]);
        Assert.Equal(offering.LicensePriceId, request.Form["line_items[1][price]"]);
        Assert.Equal("1", request.Form["line_items[0][quantity]"]);
        Assert.Equal("1", request.Form["line_items[1][quantity]"]);
        Assert.InRange(long.Parse(request.Form["expires_at"]),
            DateTimeOffset.UtcNow.AddMinutes(29).ToUnixTimeSeconds(),
            DateTimeOffset.UtcNow.AddMinutes(31).ToUnixTimeSeconds());
        Assert.Equal(organizationId.ToString("D"),
            request.Form["metadata[showvault_organization_id]"]);
        Assert.DoesNotContain(request.Form.Keys, key => key.Contains("email",
            StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Portal_uses_only_bound_customer_and_fixed_return_url()
    {
        var handler = new RecordingHandler(_ => Ok(JsonSerializer.Serialize(new
        {
            id = "bps_fixture",
            url = "https://billing.stripe.com/p/session/fixture"
        })));
        var (provider, _) = Provider(handler);

        var session = await provider.CreatePortalAsync("cus_fixture",
            new Uri("https://account.showvault.test/billing/return"),
            CancellationToken.None);

        Assert.Equal("bps_fixture", session.Id);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("cus_fixture", request.Form["customer"]);
        Assert.Equal("https://account.showvault.test/billing/return",
            request.Form["return_url"]);
        Assert.Null(request.IdempotencyKey);
        Assert.Equal(StripeBillingProvider.SupportedApiVersion, request.StripeVersion);
    }

    [Fact]
    public async Task Retrieval_reconciles_current_paid_state_from_exact_provider_objects()
    {
        var organizationId = Guid.Parse("0198a8ad-9c00-7000-8000-000000000003");
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var handler = new RecordingHandler(request => request.RequestUri!.PathAndQuery switch
        {
            "/v1/checkout/sessions?subscription=sub_fixture&limit=10" => Ok(
                "{" + Objects("cs_test_fixture") + "}"),
            "/v1/invoice_payments?payment%5Btype%5D=payment_intent&payment%5Bpayment_intent%5D=pi_fixture&limit=10" =>
                Ok(JsonSerializer.Serialize(new
                {
                    data = new[] { new { invoice = "in_fixture" } },
                    has_more = false
                })),
            "/v1/checkout/sessions/cs_test_fixture" => Ok(JsonSerializer.Serialize(new
            {
                id = "cs_test_fixture",
                livemode = false,
                created = now,
                customer = "cus_fixture",
                subscription = "sub_fixture",
                metadata = new Dictionary<string, string>
                {
                    ["showvault_organization_id"] = organizationId.ToString("D"),
                    ["showvault_offering_code"] = "showvault-standard"
                }
            })),
            "/v1/checkout/sessions/cs_test_fixture/line_items?limit=10" => Ok(
                "{\"data\":[{\"price\":{\"id\":\"price_recurring_fixture\"}},{\"price\":{\"id\":\"price_license_fixture\"}}],\"has_more\":false}"),
            "/v1/subscriptions/sub_fixture" => Ok(JsonSerializer.Serialize(new
            {
                id = "sub_fixture",
                status = "active",
                created = now,
                cancel_at_period_end = false,
                items = new
                {
                    data = new[]
                    {
                        new
                        {
                            price = new { id = "price_recurring_fixture" },
                            current_period_end = now + 2592000
                        }
                    },
                    has_more = false
                }
            })),
            "/v1/invoices?subscription=sub_fixture&limit=100" => Ok(
                JsonSerializer.Serialize(new
                {
                    data = new[]
                    {
                        new
                        {
                            id = "in_fixture", created = now
                        }
                    },
                    has_more = false
                })),
            "/v1/invoices/in_fixture" => Ok(JsonSerializer.Serialize(new
            {
                id = "in_fixture",
                created = now,
                status = "paid",
                parent = new
                {
                    type = "subscription_details",
                    subscription_details = new { subscription = "sub_fixture" }
                },
                status_transitions = new { paid_at = now }
            })),
            "/v1/invoice_payments?invoice=in_fixture&limit=10" => Ok(
                JsonSerializer.Serialize(new
                {
                    data = new[]
                    {
                        new
                        {
                            payment = new
                            {
                                type = "payment_intent", payment_intent = "pi_fixture"
                            }
                        }
                    },
                    has_more = false
                })),
            "/v1/invoices/in_fixture/lines?limit=100" => Ok(
                "{\"data\":[{\"price\":{\"id\":\"price_recurring_fixture\"}},{\"pricing\":{\"price_details\":{\"price\":\"price_license_fixture\"}}}],\"has_more\":false}"),
            "/v1/payment_intents/pi_fixture" => Ok(
                "{\"id\":\"pi_fixture\",\"latest_charge\":\"ch_fixture\"}"),
            "/v1/charges/ch_fixture" => Ok(JsonSerializer.Serialize(new
            {
                id = "ch_fixture",
                payment_intent = "pi_fixture",
                created = now,
                amount = 1100,
                amount_refunded = 0,
                disputed = false
            })),
            _ => Json(HttpStatusCode.NotFound, "{}")
        });
        var (provider, _) = Provider(handler);

        var snapshot = await provider.RetrieveCurrentStateAsync(
            "charge.refunded", "ch_fixture", CancellationToken.None);

        Assert.True(snapshot is not null,
            string.Join("\n", handler.Requests.Select(value => value.Uri.PathAndQuery)));
        Assert.Equal(organizationId, snapshot.OrganizationId);
        Assert.Equal("cs_test_fixture", snapshot.CheckoutSessionId);
        Assert.Equal("cus_fixture", snapshot.CustomerId);
        Assert.Equal("sub_fixture", snapshot.SubscriptionId);
        Assert.Equal("in_fixture", snapshot.InitialInvoiceId);
        Assert.Equal(BillingLicensePaymentState.Paid, snapshot.LicensePaymentState);
        Assert.Equal("active", snapshot.SubscriptionStatus);
        Assert.Equal(64, snapshot.ProviderRevision.Length);
        Assert.All(handler.Requests, request =>
            Assert.Equal(StripeBillingProvider.SupportedApiVersion, request.StripeVersion));
    }

    private static (StripeBillingProvider Provider, BillingOffering Offering) Provider(
        RecordingHandler handler)
    {
        var billing = Billing();
        var catalog = Catalog(billing);
        var provider = new StripeBillingProvider(new HttpClient(handler), billing,
            Options.Create(new StripeApiOptions
            {
                SecretKey = "rk_test_fixture_never_real",
                RequestTimeoutSeconds = 5
            }), catalog, TimeProvider.System);
        Assert.True(provider.IsAvailable);
        return (provider, catalog.Current!);
    }

    private static IOptions<BillingOptions> Billing() => Options.Create(new BillingOptions
    {
        Enabled = true,
        Environment = BillingProviderEnvironment.Sandbox,
        ReturnOrigin = "https://account.showvault.test/",
        ProviderApiVersion = StripeBillingProvider.SupportedApiVersion,
        CheckoutLifetimeMinutes = 30
    });

    private static ConfiguredBillingOfferingCatalog Catalog(
        IOptions<BillingOptions> billing) => new(billing,
        Options.Create(new BillingOfferingOptions
        {
            Code = "showvault-standard",
            DisplayName = "ShowVault standard sandbox",
            PlanCode = "synthetic.standard",
            LicenseTypeCode = "showvault.perpetual",
            RecurringPriceId = "price_recurring_fixture",
            LicensePriceId = "price_license_fixture",
            PolicyVersion = "sandbox-proof-1"
        }));

    private static string Objects(string id) =>
        $"\"data\":[{{\"id\":\"{id}\"}}],\"has_more\":false";
    private static HttpResponseMessage Ok(string body) => Json(HttpStatusCode.OK, body);
    private static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = request.Content is null ? "" :
                await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new(request.Method, request.RequestUri!,
                request.Headers.GetValues("Stripe-Version").Single(),
                request.Headers.TryGetValues("Idempotency-Key", out var keys)
                    ? keys.Single() : null, ParseForm(content)));
            return response(request);
        }

        private static Dictionary<string, string> ParseForm(string value) => value
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Split('=', 2))
            .ToDictionary(item => Decode(item[0]), item => Decode(item[1]));
        private static string Decode(string value) =>
            Uri.UnescapeDataString(value.Replace('+', ' '));
    }

    private sealed record RecordedRequest(HttpMethod Method, Uri Uri,
        string StripeVersion, string? IdempotencyKey,
        Dictionary<string, string> Form);
}
