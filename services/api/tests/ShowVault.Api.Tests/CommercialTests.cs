using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ShowVault.Api.Commercial;
using ShowVault.Api.Contracts;
using ShowVault.Api.Data;
using ShowVault.Api.HostedSync;
using ShowVault.Platform.Commercial;
using ShowVault.Platform.Organizations;
using ShowVault.Platform.Venues;
using Xunit;

namespace ShowVault.Api.Tests;

public sealed class CommercialTests(TenantApiFactory factory) : IClassFixture<TenantApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Owner_reads_minimized_server_derived_plan_and_usage()
    {
        var tenant = await CreateTenantAsync("plan-owner", entitled: true);

        var response = await Client("plan-owner").GetAsync(
            $"/api/v1/organizations/{tenant.OrganizationId}/plan");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var plan = JsonSerializer.Deserialize<ApiResponse<OrganizationPlanSnapshot>>(
            json, JsonOptions)!.Payload;
        Assert.Equal(SyntheticCommercialPlanPolicyCatalog.PlanCode, plan.PlanCode);
        Assert.Equal("active", plan.LicenseStatus);
        Assert.Equal("active", plan.SubscriptionStatus);
        Assert.True(plan.Eligible);
        Assert.Equal(SyntheticCommercialPlanPolicyCatalog.LogicalStorageLimitBytes,
            plan.LogicalStorageLimitBytes);
        Assert.DoesNotContain("email", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("provider", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("payment", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("path", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(OrganizationRole.Viewer)]
    [InlineData(OrganizationRole.Manager)]
    [InlineData(OrganizationRole.Administrator)]
    public async Task Plan_details_are_owner_only(OrganizationRole role)
    {
        var tenant = await CreateTenantAsync("detail-owner", entitled: true);
        var subject = $"detail-{role}";
        await AddMembershipAsync(tenant.OrganizationId, subject, role);

        var response = await Client(subject).GetAsync(
            $"/api/v1/organizations/{tenant.OrganizationId}/plan");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Plan_requires_authentication_and_exact_tenant_owner_membership()
    {
        var first = await CreateTenantAsync("first-owner", entitled: true);
        var second = await CreateTenantAsync("second-owner", entitled: true);

        var unauthenticated = await factory.CreateClient().GetAsync(
            $"/api/v1/organizations/{first.OrganizationId}/plan");
        var crossTenant = await Client("first-owner").GetAsync(
            $"/api/v1/organizations/{second.OrganizationId}/plan");

        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, crossTenant.StatusCode);
    }

    [Fact]
    public async Task Missing_commercial_state_denies_new_session_and_audits_bounded_reason()
    {
        var tenant = await CreateTenantAsync("missing-owner", entitled: false);
        var request = BeginRequest(12);

        var response = await Client("missing-owner").PostAsJsonAsync(
            Root(tenant, request.Manifest.RecoveryPointId) + "/begin", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("commercial_access_required",
            JsonDocument.Parse(await response.Content.ReadAsStringAsync())
                .RootElement.GetProperty("code").GetString());
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = Assert.Single(database.CommercialAuditEvents.Where(value =>
            value.OrganizationId == tenant.OrganizationId));
        Assert.Equal(CommercialReasonCodes.LicenseMissing, audit.ReasonCode);
        Assert.Equal("denied", audit.Outcome);
        Assert.Equal(12, audit.RequestedBytes);
        Assert.DoesNotContain("/", audit.CorrelationId, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Quota_is_reserved_once_and_over_limit_request_denies_without_session()
    {
        var tenant = await CreateTenantAsync("quota-owner", entitled: true);
        var request = BeginRequest(23);
        var root = Root(tenant, request.Manifest.RecoveryPointId);

        Assert.Equal(HttpStatusCode.OK,
            (await Client("quota-owner").PostAsJsonAsync(root + "/begin", request)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await Client("quota-owner").PostAsJsonAsync(root + "/begin", request)).StatusCode);
        var tooLarge = BeginRequest(
            SyntheticCommercialPlanPolicyCatalog.LogicalStorageLimitBytes);
        var denied = await Client("quota-owner").PostAsJsonAsync(
            Root(tenant, tooLarge.Manifest.RecoveryPointId) + "/begin", tooLarge);

        Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
        Assert.Equal("quota_exceeded",
            JsonDocument.Parse(await denied.Content.ReadAsStringAsync())
                .RootElement.GetProperty("code").GetString());
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var usage = database.OrganizationStorageUsages.Single(value =>
            value.OrganizationId == tenant.OrganizationId);
        Assert.Equal(23, usage.ReservedBytes);
        Assert.Equal(0, usage.CommittedBytes);
        Assert.Single(database.HostedSyncReservations.Where(value =>
            value.OrganizationId == tenant.OrganizationId));
    }

    private HttpClient Client(string subject)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Subject", subject);
        return client;
    }

    private async Task<(Guid OrganizationId, Guid VenueId)> CreateTenantAsync(
        string subject, bool entitled)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var organization = Organization.Create("Synthetic", $"commercial-{Guid.NewGuid():N}");
        var venue = Venue.Create(organization.Id, "Synthetic Venue", "UTC");
        database.Organizations.Add(organization);
        database.Venues.Add(venue);
        database.Memberships.Add(Membership.Create(organization.Id, subject,
            OrganizationRole.Owner, DateTimeOffset.UtcNow));
        if (entitled)
        {
            var now = DateTimeOffset.UtcNow;
            database.CommercialLicenses.Add(new CommercialLicense
            {
                Id = Guid.NewGuid(),
                OrganizationId = organization.Id,
                LicenseTypeCode = "synthetic.perpetual",
                State = CommercialLicenseState.Active,
                EffectiveAt = now.AddDays(-1),
                UpdatedAt = now
            });
            database.ServiceSubscriptions.Add(new ServiceSubscription
            {
                Id = Guid.NewGuid(),
                OrganizationId = organization.Id,
                PlanCode = SyntheticCommercialPlanPolicyCatalog.PlanCode,
                State = ServiceSubscriptionState.Active,
                CurrentPeriodEndsAt = now.AddDays(30),
                UpdatedAt = now
            });
        }
        await database.SaveChangesAsync();
        return (organization.Id, venue.Id);
    }

    private async Task AddMembershipAsync(Guid organizationId, string subject,
        OrganizationRole role)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        database.Memberships.Add(Membership.Create(
            organizationId, subject, role, DateTimeOffset.UtcNow));
        await database.SaveChangesAsync();
    }

    private static HostedSyncBeginRequest BeginRequest(long totalBytes)
    {
        var recoveryPointId = Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes(Guid.NewGuid().ToString("N"))));
        var manifest = new HostedSyncManifest("1.0", recoveryPointId, recoveryPointId,
            "macos.serato-dj-pro.user-data", "showvault.serato-dj-pro",
            DateTimeOffset.Parse("2026-08-13T12:00:00Z"), 1, totalBytes,
            [new("Subcrates/synthetic.crate", totalBytes, new string('a', 64))]);
        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        return new(manifest, Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes(json))));
    }

    private static string Root((Guid OrganizationId, Guid VenueId) tenant,
        string recoveryPointId) =>
        $"/api/v1/organizations/{tenant.OrganizationId}/venues/{tenant.VenueId}" +
        $"/hosted-sync/{recoveryPointId}";
}
