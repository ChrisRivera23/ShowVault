using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ShowVault.Api.Contracts;
using ShowVault.Api.Data;
using ShowVault.Platform.Organizations;
using Xunit;

namespace ShowVault.Api.Tests;

public sealed class AccountAdministrationTests(TenantApiFactory factory)
    : IClassFixture<TenantApiFactory>
{
    [Fact]
    public async Task Owner_creates_code_once_member_accepts_and_owner_suspends()
    {
        var owner = $"auth0|owner-{Guid.NewGuid():N}";
        var member = $"auth0|member-{Guid.NewGuid():N}";
        var organizationId = await SeedOrganizationAsync(owner);
        using var ownerClient = Client(owner, steppedUp: true);
        using var memberClient = Client(member);

        var create = await ownerClient.PostAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/account/invitations",
            new { displayLabel = "Guest operator", role = "technician" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Equal("no-store", create.Headers.CacheControl?.ToString());
        var created = await create.Content.ReadFromJsonAsync<ApiResponse<CreatedAccountInvitation>>();
        Assert.NotNull(created);
        Assert.Equal(43, created.Payload.InvitationCode.Length);

        var accept = await memberClient.PostAsJsonAsync(
            "/api/v1/account/invitations/accept",
            new { invitationCode = created.Payload.InvitationCode });
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        var accepted = await accept.Content
            .ReadFromJsonAsync<ApiResponse<AcceptedAccountInvitation>>();
        Assert.NotNull(accepted);
        Assert.Equal("technician", accepted.Payload.Membership.Role);
        Assert.True(accepted.Payload.Membership.IsCurrentUser);

        var replay = await memberClient.PostAsJsonAsync(
            "/api/v1/account/invitations/accept",
            new { invitationCode = created.Payload.InvitationCode });
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);

        var suspend = await ownerClient.PatchAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/account/members/" +
            accepted.Payload.Membership.Id,
            new { action = "suspend", expectedRevision = accepted.Payload.Membership.Revision });
        Assert.Equal(HttpStatusCode.OK, suspend.StatusCode);
        var suspended = await suspend.Content
            .ReadFromJsonAsync<ApiResponse<AccountMemberSummary>>();
        Assert.NotNull(suspended);
        Assert.Equal("suspended", suspended.Payload.State);

        var memberOrganizations = await memberClient.GetFromJsonAsync<
            ApiResponse<IReadOnlyList<OrganizationSummary>>>("/api/v1/organizations");
        Assert.NotNull(memberOrganizations);
        Assert.Empty(memberOrganizations.Payload);

        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var storedInvitation = database.OrganizationInvitations.Single(value =>
            value.Id == created.Payload.Id);
        Assert.DoesNotContain(created.Payload.InvitationCode,
            Convert.ToHexString(storedInvitation.TokenDigest), StringComparison.Ordinal);
        Assert.Equal(3, database.AccountAuditEvents.Count(value =>
            value.OrganizationId == organizationId));
    }

    [Fact]
    public async Task Sensitive_mutation_requires_fresh_step_up_and_owner_target_is_denied()
    {
        var owner = $"auth0|owner-{Guid.NewGuid():N}";
        var organizationId = await SeedOrganizationAsync(owner);
        using var ordinaryOwner = Client(owner);
        var denied = await ordinaryOwner.PostAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/account/invitations",
            new { displayLabel = "Denied", role = "viewer" });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        using var steppedUp = Client(owner, steppedUp: true);
        Guid ownerMembershipId;
        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            ownerMembershipId = database.Memberships.Single(value =>
                value.OrganizationId == organizationId).Id;
        }
        var mutateOwner = await steppedUp.PatchAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/account/members/{ownerMembershipId}",
            new { action = "suspend", expectedRevision = 1 });
        Assert.Equal(HttpStatusCode.Conflict, mutateOwner.StatusCode);
    }

    [Fact]
    public async Task Unknown_fields_and_unknown_codes_are_rejected_uniformly()
    {
        var owner = $"auth0|owner-{Guid.NewGuid():N}";
        var organizationId = await SeedOrganizationAsync(owner);
        using var ownerClient = Client(owner, steppedUp: true);
        var unknownField = await ownerClient.PostAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/account/invitations",
            new { displayLabel = "Test", role = "viewer", identitySubject = "forbidden" });
        Assert.Equal(HttpStatusCode.BadRequest, unknownField.StatusCode);
        var invalidLabel = await ownerClient.PostAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/account/invitations",
            new { displayLabel = " ", role = "viewer" });
        Assert.Equal(HttpStatusCode.BadRequest, invalidLabel.StatusCode);

        Guid ownerMembershipId;
        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            ownerMembershipId = database.Memberships.Single(value =>
                value.OrganizationId == organizationId).Id;
        }
        var invalidMutation = await ownerClient.PatchAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/account/members/{ownerMembershipId}",
            new { action = "suspend", expectedRevision = 1, role = "viewer" });
        Assert.Equal(HttpStatusCode.BadRequest, invalidMutation.StatusCode);

        using var memberClient = Client($"auth0|member-{Guid.NewGuid():N}");
        var unknown = await memberClient.PostAsJsonAsync(
            "/api/v1/account/invitations/accept",
            new { invitationCode = new string('a', 43) });
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Contains("invitation_unavailable",
            await unknown.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    private async Task<Guid> SeedOrganizationAsync(string owner)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var organization = Organization.Create("Account fixture", $"account-{Guid.NewGuid():N}");
        database.Organizations.Add(organization);
        database.Memberships.Add(Membership.Create(organization.Id, owner,
            OrganizationRole.Owner, DateTimeOffset.UtcNow, "Owner"));
        await database.SaveChangesAsync();
        return organization.Id;
    }

    private HttpClient Client(string subject, bool steppedUp = false)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Subject", subject);
        if (steppedUp)
        {
            client.DefaultRequestHeaders.Add("X-Test-Scope", "openid manage:members");
            client.DefaultRequestHeaders.Add("X-Test-Mfa", "[\"mfa\"]");
            client.DefaultRequestHeaders.Add("X-Test-Iat",
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
        }
        return client;
    }
}
