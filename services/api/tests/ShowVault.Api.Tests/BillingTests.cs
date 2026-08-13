using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ShowVault.Api.Billing;
using ShowVault.Api.Data;
using ShowVault.Api.Security;
using ShowVault.Platform.Billing;
using ShowVault.Platform.Commercial;
using ShowVault.Platform.Organizations;
using Xunit;

namespace ShowVault.Api.Tests;

public sealed class BillingTests(TenantApiFactory factory) : IClassFixture<TenantApiFactory>
{
    [Fact]
    public async Task Owner_checkout_is_server_catalogued_durable_and_idempotent()
    {
        var organizationId = await CreateOrganizationAsync("billing-checkout-owner");
        var client = Client("billing-checkout-owner");
        var before = factory.BillingProvider.CheckoutCreationCount;

        var requests = await Task.WhenAll(
            client.PostAsJsonAsync(Root(organizationId) + "/checkout-sessions",
                new { offeringCode = TestBillingOfferingCatalog.Offering.Code }),
            client.PostAsJsonAsync(Root(organizationId) + "/checkout-sessions",
                new { offeringCode = TestBillingOfferingCatalog.Offering.Code }));
        var (first, second) = (requests[0], requests[1]);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(before + 1, factory.BillingProvider.CheckoutCreationCount);
        var firstJson = JsonDocument.Parse(await first.Content.ReadAsStringAsync()).RootElement;
        var secondJson = JsonDocument.Parse(await second.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(firstJson.GetProperty("payload").GetProperty("attemptId").GetString(),
            secondJson.GetProperty("payload").GetProperty("attemptId").GetString());
        Assert.StartsWith("https://checkout.stripe.test/",
            firstJson.GetProperty("payload").GetProperty("url").GetString());
        using var scope = factory.Services.CreateScope();
        var attempt = Assert.Single(scope.ServiceProvider.GetRequiredService<PlatformDbContext>()
            .BillingPurchaseAttempts.Where(value => value.OrganizationId == organizationId));
        Assert.Equal(BillingPurchaseAttemptState.Open, attempt.State);
        Assert.DoesNotContain("checkout.stripe.test", attempt.ProviderSessionId);
    }

    [Fact]
    public async Task Locally_signed_event_reconciles_current_state_and_duplicate_is_once()
    {
        var organizationId = await CreateOrganizationAsync("billing-event-owner");
        var client = Client("billing-event-owner");
        await client.PostAsJsonAsync(Root(organizationId) + "/checkout-sessions",
            new { offeringCode = TestBillingOfferingCatalog.Offering.Code });
        string sessionId;
        using (var scope = factory.Services.CreateScope())
        {
            sessionId = scope.ServiceProvider.GetRequiredService<PlatformDbContext>()
                .BillingPurchaseAttempts.Single(value =>
                    value.OrganizationId == organizationId).ProviderSessionId!;
        }
        var modified = DateTimeOffset.UtcNow;
        factory.BillingProvider.Snapshot = new BillingProviderSnapshot(
            organizationId, TestBillingOfferingCatalog.Offering.Code, sessionId,
            "cus_fixture_unique", "sub_fixture_unique", "in_fixture_unique",
            TestBillingOfferingCatalog.Offering.RecurringPriceId,
            TestBillingOfferingCatalog.Offering.LicensePriceId, "active",
            modified.AddDays(30), false, BillingLicensePaymentState.Paid,
            modified, "revision-1");
        var body = JsonSerializer.SerializeToUtf8Bytes(new
        {
            id = "evt_fixture_once",
            type = "invoice.paid",
            created = modified.ToUnixTimeSeconds(),
            livemode = false,
            api_version = "2026-07-01.fixture",
            data = new { @object = new { id = "in_fixture_unique" } }
        });

        var first = await SignedWebhookAsync(body, modified);
        var duplicate = await SignedWebhookAsync(body, modified);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        using var verifyScope = factory.Services.CreateScope();
        var database = verifyScope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var receipt = Assert.Single(database.BillingEventReceipts.Where(value =>
            value.ProviderEventId == "evt_fixture_once"));
        Assert.Equal(BillingEventProcessingState.Processed, receipt.State);
        Assert.Equal("projection_updated", receipt.OutcomeCode);
        Assert.Equal(64, receipt.PayloadSha256.Length);
        Assert.Equal(CommercialLicenseState.Active, database.CommercialLicenses.Single(value =>
            value.OrganizationId == organizationId).State);
        var subscription = database.ServiceSubscriptions.Single(value =>
            value.OrganizationId == organizationId);
        Assert.Equal(ServiceSubscriptionState.Active, subscription.State);
        Assert.Null(subscription.GraceEndsAt);
        Assert.Single(database.BillingAccountBindings.Where(value =>
            value.OrganizationId == organizationId));
    }

    [Fact]
    public async Task Portal_uses_existing_binding_and_returns_ephemeral_url()
    {
        var organizationId = await CreateOrganizationAsync("billing-portal-owner");
        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            database.BillingAccountBindings.Add(new BillingAccountBinding
            {
                OrganizationId = organizationId,
                Environment = BillingProviderEnvironment.Sandbox,
                ProviderCustomerId = "cus_portal_fixture",
                OfferingCode =
                    TestBillingOfferingCatalog.Offering.Code,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await database.SaveChangesAsync();
        }

        var response = await Client("billing-portal-owner").PostAsync(
            Root(organizationId) + "/portal-sessions", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("https://billing.stripe.test/session/fixture", json);
        using var verifyScope = factory.Services.CreateScope();
        Assert.DoesNotContain("billing.stripe.test",
            JsonSerializer.Serialize(verifyScope.ServiceProvider
                .GetRequiredService<PlatformDbContext>().BillingAccountBindings.ToList()));
    }

    [Fact]
    public async Task Personal_beta_identity_cannot_create_a_billing_session()
    {
        var organizationId = await CreateOrganizationAsync("personal-beta-billing-owner");
        var client = Client("personal-beta-billing-owner");
        client.DefaultRequestHeaders.Add("X-Test-Authentication-Type",
            PersonalBetaAuthenticationHandler.SchemeName);

        var response = await client.PostAsJsonAsync(Root(organizationId) +
            "/checkout-sessions", new
            { offeringCode = TestBillingOfferingCatalog.Offering.Code });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Empty(scope.ServiceProvider.GetRequiredService<PlatformDbContext>()
            .BillingPurchaseAttempts.Where(value => value.OrganizationId == organizationId));
    }

    [Fact]
    public void Signature_verifier_accepts_exact_bytes_and_rejects_stale_or_changed_body()
    {
        var verifier = new StripeWebhookSignatureVerifier();
        var now = DateTimeOffset.Parse("2026-08-13T12:00:00Z");
        var body = Encoding.UTF8.GetBytes("{\"id\":\"evt_fixture\"}");
        var options = new StripeWebhookOptions
        {
            EndpointSecrets = ["whsec_fixture"],
            TimestampToleranceSeconds = 300
        };
        var header = Signature(body, now.ToUnixTimeSeconds(), "whsec_fixture");

        Assert.True(verifier.Verify(body, header, now, options));
        Assert.False(verifier.Verify(Encoding.UTF8.GetBytes("{}"), header, now, options));
        Assert.False(verifier.Verify(body, header, now.AddMinutes(6), options));
    }

    [Fact]
    public async Task Invalid_signature_creates_no_receipt_and_unknown_signed_event_is_ignored()
    {
        var now = DateTimeOffset.UtcNow;
        var invalidBody = EventBody("evt_invalid_signature", "invoice.paid", now);
        var invalidRequest = new HttpRequestMessage(HttpMethod.Post,
            "/api/v1/provider-webhooks/stripe")
        { Content = new ByteArrayContent(invalidBody) };
        invalidRequest.Content.Headers.ContentType = new("application/json");
        invalidRequest.Headers.Add("Stripe-Signature",
            Signature(invalidBody, now.ToUnixTimeSeconds(), "wrong_secret"));

        var invalid = await factory.CreateClient().SendAsync(invalidRequest);
        var unknownBody = EventBody("evt_unknown_fixture", "customer.created", now);
        var unknown = await SignedWebhookAsync(unknownBody, now);

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);
        using var scope = factory.Services.CreateScope();
        var receipts = scope.ServiceProvider.GetRequiredService<PlatformDbContext>()
            .BillingEventReceipts.Where(value => value.ProviderEventId ==
                "evt_invalid_signature" || value.ProviderEventId == "evt_unknown_fixture")
            .ToList();
        var receipt = Assert.Single(receipts);
        Assert.Equal("evt_unknown_fixture", receipt.ProviderEventId);
        Assert.Equal(BillingEventProcessingState.Ignored, receipt.State);
    }

    [Fact]
    public async Task Older_reconciliation_is_noop_and_dispute_attention_denies_plan()
    {
        var organizationId = await CreateOrganizationAsync("billing-order-owner");
        var client = Client("billing-order-owner");
        await client.PostAsJsonAsync(Root(organizationId) + "/checkout-sessions",
            new { offeringCode = TestBillingOfferingCatalog.Offering.Code });
        string sessionId;
        using (var scope = factory.Services.CreateScope())
            sessionId = scope.ServiceProvider.GetRequiredService<PlatformDbContext>()
                .BillingPurchaseAttempts.Single(value => value.OrganizationId == organizationId)
                .ProviderSessionId!;
        var newer = DateTimeOffset.UtcNow;
        factory.BillingProvider.Snapshot = Snapshot(organizationId, sessionId, newer,
            "revision-new", "active", BillingLicensePaymentState.Paid);
        Assert.Equal(HttpStatusCode.OK,
            (await SignedWebhookAsync(EventBody("evt_order_new", "invoice.paid", newer), newer))
            .StatusCode);

        factory.BillingProvider.Snapshot = Snapshot(organizationId, sessionId,
            newer.AddMinutes(-1), "revision-old", "past_due",
            BillingLicensePaymentState.Pending);
        Assert.Equal(HttpStatusCode.OK,
            (await SignedWebhookAsync(EventBody("evt_order_old", "invoice.payment_failed",
                newer), newer)).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            Assert.Equal(ServiceSubscriptionState.Active,
                database.ServiceSubscriptions.Single(value =>
                    value.OrganizationId == organizationId).State);
            Assert.Equal("stale_noop", database.BillingEventReceipts.Single(value =>
                value.ProviderEventId == "evt_order_old").OutcomeCode);
        }

        var disputedAt = newer.AddMinutes(1);
        factory.BillingProvider.Snapshot = Snapshot(organizationId, sessionId, disputedAt,
            "revision-dispute", "active", BillingLicensePaymentState.Disputed);
        await SignedWebhookAsync(EventBody("evt_dispute_fixture", "charge.dispute.created",
            disputedAt), disputedAt);
        var plan = await client.GetAsync($"/api/v1/organizations/{organizationId}/plan");
        var payload = JsonDocument.Parse(await plan.Content.ReadAsStringAsync()).RootElement
            .GetProperty("payload");
        Assert.False(payload.GetProperty("eligible").GetBoolean());
        Assert.Equal("billing_attention", payload.GetProperty("reasonCode").GetString());
    }

    private async Task<HttpResponseMessage> SignedWebhookAsync(byte[] body, DateTimeOffset now)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/provider-webhooks/stripe")
        {
            Content = new ByteArrayContent(body)
        };
        request.Content.Headers.ContentType = new("application/json");
        request.Headers.Add("Stripe-Signature",
            Signature(body, now.ToUnixTimeSeconds(), "whsec_local_fixture_only"));
        var response = await factory.CreateClient().SendAsync(request);
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<BillingReconciliationService>()
            .ProcessPendingAsync(25, CancellationToken.None);
        return response;
    }

    private static string Signature(byte[] body, long timestamp, string secret)
    {
        var prefix = Encoding.UTF8.GetBytes(timestamp + ".");
        var signed = prefix.Concat(body).ToArray();
        return $"t={timestamp},v1={Convert.ToHexStringLower(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), signed))}";
    }

    private static byte[] EventBody(string id, string type, DateTimeOffset now) =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            id,
            type,
            created = now.ToUnixTimeSeconds(),
            livemode = false,
            api_version = "2026-07-01.fixture",
            data = new { @object = new { id = "obj_fixture" } }
        });

    private static BillingProviderSnapshot Snapshot(Guid organizationId, string sessionId,
        DateTimeOffset modified, string revision, string subscriptionStatus,
        BillingLicensePaymentState paymentState) => new(
            organizationId, TestBillingOfferingCatalog.Offering.Code, sessionId,
            "cus_order_fixture", "sub_order_fixture", "in_order_fixture",
            TestBillingOfferingCatalog.Offering.RecurringPriceId,
            TestBillingOfferingCatalog.Offering.LicensePriceId, subscriptionStatus,
            modified.AddDays(30), false, paymentState, modified, revision);

    private HttpClient Client(string subject)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Subject", subject);
        return client;
    }

    private async Task<Guid> CreateOrganizationAsync(string subject)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var organization = Organization.Create("Billing fixture", $"billing-{Guid.NewGuid():N}");
        database.Organizations.Add(organization);
        database.Memberships.Add(Membership.Create(organization.Id, subject,
            OrganizationRole.Owner, DateTimeOffset.UtcNow));
        await database.SaveChangesAsync();
        return organization.Id;
    }

    private static string Root(Guid organizationId) =>
        $"/api/v1/organizations/{organizationId}/billing";
}
