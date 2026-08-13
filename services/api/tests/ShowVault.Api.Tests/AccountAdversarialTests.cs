using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ShowVault.Api.Account;
using ShowVault.Api.Contracts;
using ShowVault.Api.Data;
using ShowVault.Api.Security;
using ShowVault.Platform.Organizations;
using Xunit;

namespace ShowVault.Api.Tests;

public sealed class AccountAdversarialTests(TenantApiFactory factory)
    : IClassFixture<TenantApiFactory>
{
    [Theory]
    [InlineData(OrganizationRole.Viewer)]
    [InlineData(OrganizationRole.Technician)]
    [InlineData(OrganizationRole.Manager)]
    [InlineData(OrganizationRole.Administrator)]
    public async Task Every_non_owner_role_is_denied_account_administration(
        OrganizationRole role)
    {
        var owner = Subject("owner");
        var member = Subject(role.ToString());
        var organizationId = await SeedOrganizationAsync(owner);
        await SeedMembershipAsync(organizationId, member, role);
        using var client = Client(member, steppedUp: true);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(
            $"/api/v1/organizations/{organizationId}/account/members")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/account/invitations",
            new { displayLabel = "Denied", role = "viewer" })).StatusCode);
    }

    [Fact]
    public async Task Outsider_missing_subject_and_personal_beta_are_denied()
    {
        var owner = Subject("owner");
        var organizationId = await SeedOrganizationAsync(owner);
        using var outsider = Client(Subject("outsider"), steppedUp: true);
        using var missing = factory.CreateClient();
        using var personal = Client(Subject("personal"), steppedUp: true,
            PersonalBetaAuthenticationHandler.SchemeName);
        var route = $"/api/v1/organizations/{organizationId}/account/invitations";

        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.PostAsJsonAsync(route,
            new { displayLabel = "Denied", role = "viewer" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await missing.PostAsJsonAsync(route,
            new { displayLabel = "Denied", role = "viewer" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await personal.PostAsJsonAsync(route,
            new { displayLabel = "Denied", role = "viewer" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await personal.PostAsJsonAsync(
            "/api/v1/account/invitations/accept",
            new { invitationCode = new string('a', 43) })).StatusCode);
    }

    [Fact]
    public async Task Malformed_invitation_code_is_rejected_before_any_database_query()
    {
        using var client = Client(Subject("malformed-code"));
        _ = await client.GetAsync("/health/ready");
        factory.Commands.Reset();

        var response = await client.PostAsJsonAsync(
            "/api/v1/account/invitations/accept",
            new { invitationCode = "not-a-valid-code" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Commands.ReaderCommands);
    }

    [Fact]
    public async Task Invitation_revocation_and_cross_tenant_ids_are_closed()
    {
        var firstOwner = Subject("first-owner");
        var secondOwner = Subject("second-owner");
        var first = await SeedOrganizationAsync(firstOwner);
        var second = await SeedOrganizationAsync(secondOwner);
        using var firstClient = Client(firstOwner, steppedUp: true);
        using var secondClient = Client(secondOwner, steppedUp: true);
        using var member = Client(Subject("acceptor"));
        var created = await CreateInvitationAsync(firstClient, first, "viewer");

        Assert.Equal(HttpStatusCode.NotFound, (await secondClient.PostAsJsonAsync(
            $"/api/v1/organizations/{second}/account/invitations/{created.Id}/revoke",
            new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await firstClient.PostAsJsonAsync(
            $"/api/v1/organizations/{first}/account/invitations/{created.Id}/revoke",
            new { })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await member.PostAsJsonAsync(
            "/api/v1/account/invitations/accept",
            new { invitationCode = created.InvitationCode })).StatusCode);
    }

    [Fact]
    public async Task Concurrent_same_subject_acceptance_is_idempotent_and_other_winner_is_denied()
    {
        var owner = Subject("owner");
        var organizationId = await SeedOrganizationAsync(owner);
        using var ownerClient = Client(owner, steppedUp: true);
        var sameCode = await CreateInvitationAsync(ownerClient, organizationId, "viewer");
        var winner = Subject("same-winner");
        using var sameA = Client(winner);
        using var sameB = Client(winner);

        var sameResults = await Task.WhenAll(
            sameA.PostAsJsonAsync("/api/v1/account/invitations/accept",
                new { invitationCode = sameCode.InvitationCode }),
            sameB.PostAsJsonAsync("/api/v1/account/invitations/accept",
                new { invitationCode = sameCode.InvitationCode }));
        Assert.All(sameResults, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));

        var racedCode = await CreateInvitationAsync(ownerClient, organizationId, "viewer");
        using var first = Client(Subject("first-racer"));
        using var second = Client(Subject("second-racer"));
        var raceResults = await Task.WhenAll(
            first.PostAsJsonAsync("/api/v1/account/invitations/accept",
                new { invitationCode = racedCode.InvitationCode }),
            second.PostAsJsonAsync("/api/v1/account/invitations/accept",
                new { invitationCode = racedCode.InvitationCode }));
        Assert.Equal(1, raceResults.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, raceResults.Count(response => response.StatusCode == HttpStatusCode.BadRequest));

        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(2, database.Memberships.Count(value =>
            value.OrganizationId == organizationId && value.Role != OrganizationRole.Owner));
        Assert.Equal(2, database.AccountAuditEvents.Count(value =>
            value.OrganizationId == organizationId && value.Action == "invitation_accept"));
    }

    [Theory]
    [InlineData(MembershipState.Active)]
    [InlineData(MembershipState.Suspended)]
    [InlineData(MembershipState.Revoked)]
    public async Task Existing_subject_in_every_state_cannot_accept_another_invitation(
        MembershipState state)
    {
        var owner = Subject("owner");
        var subject = Subject(state.ToString());
        var organizationId = await SeedOrganizationAsync(owner);
        var existing = await SeedMembershipAsync(
            organizationId, subject, OrganizationRole.Viewer);
        await SetStateAsync(existing, state);
        using var ownerClient = Client(owner, steppedUp: true);
        using var memberClient = Client(subject);
        var invitation = await CreateInvitationAsync(ownerClient, organizationId, "manager");

        var response = await memberClient.PostAsJsonAsync(
            "/api/v1/account/invitations/accept",
            new { invitationCode = invitation.InvitationCode });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("invitation_unavailable", await response.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Expiry_is_persisted_and_returns_uniform_unavailable_response()
    {
        var owner = Subject("owner");
        var organizationId = await SeedOrganizationAsync(owner);
        Guid invitationId;
        string code;
        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var tokens = scope.ServiceProvider.GetRequiredService<InvitationTokenService>();
            var issued = tokens.Issue();
            var createdAt = DateTimeOffset.UtcNow.AddDays(-8);
            var invitation = OrganizationInvitation.Create(organizationId, "Expired",
                OrganizationRole.Viewer, issued.Digest, issued.KeyId, owner,
                createdAt, createdAt.AddDays(7));
            database.OrganizationInvitations.Add(invitation);
            await database.SaveChangesAsync();
            invitationId = invitation.Id;
            code = issued.Code;
        }
        using var client = Client(Subject("late-member"));

        var response = await client.PostAsJsonAsync("/api/v1/account/invitations/accept",
            new { invitationCode = code });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var verify = factory.Services.CreateScope();
        var stored = verify.ServiceProvider.GetRequiredService<PlatformDbContext>()
            .OrganizationInvitations.Single(value => value.Id == invitationId);
        Assert.Equal(OrganizationInvitationState.Expired, stored.State);
    }

    [Fact]
    public async Task Role_change_restore_revoke_and_revision_conflict_follow_state_machine()
    {
        var owner = Subject("owner");
        var organizationId = await SeedOrganizationAsync(owner);
        var memberId = await SeedMembershipAsync(
            organizationId, Subject("member"), OrganizationRole.Viewer);
        using var client = Client(owner, steppedUp: true);
        var route = $"/api/v1/organizations/{organizationId}/account/members/{memberId}";

        var changed = await MutateAsync(client, route, "change_role", 1, "manager");
        Assert.Equal("manager", changed.Role);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PatchAsJsonAsync(route,
            new { action = "suspend", expectedRevision = 1 })).StatusCode);
        var suspended = await MutateAsync(client, route, "suspend", changed.Revision);
        Assert.Equal("suspended", suspended.State);
        var restored = await MutateAsync(client, route, "restore", suspended.Revision);
        Assert.Equal("active", restored.State);
        var revoked = await MutateAsync(client, route, "revoke", restored.Revision);
        Assert.Equal("revoked", revoked.State);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PatchAsJsonAsync(route,
            new { action = "restore", expectedRevision = revoked.Revision })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PatchAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/account/members/{Guid.NewGuid()}",
            new { action = "suspend", expectedRevision = 1 })).StatusCode);
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(4, database.AccountAuditEvents.Count(value =>
            value.OrganizationId == organizationId));
    }

    [Fact]
    public async Task Concurrent_member_mutation_has_one_winner_and_one_audit()
    {
        var owner = Subject("mutation-owner");
        var organizationId = await SeedOrganizationAsync(owner);
        var memberId = await SeedMembershipAsync(
            organizationId, Subject("mutation-member"), OrganizationRole.Viewer);
        using var first = Client(owner, steppedUp: true);
        using var second = Client(owner, steppedUp: true);
        var route = $"/api/v1/organizations/{organizationId}/account/members/{memberId}";

        var results = await Task.WhenAll(
            first.PatchAsJsonAsync(route,
                new { action = "suspend", expectedRevision = 1 }),
            second.PatchAsJsonAsync(route,
                new { action = "change_role", expectedRevision = 1, role = "manager" }));

        Assert.Equal(1, results.Count(value => value.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, results.Count(value => value.StatusCode == HttpStatusCode.Conflict));
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Single(database.AccountAuditEvents.Where(value =>
            value.OrganizationId == organizationId));
    }

    [Fact]
    public async Task Sensitive_mutation_rate_limit_rejects_eleventh_request()
    {
        var owner = Subject("rate-owner");
        var organizationId = await SeedOrganizationAsync(owner);
        using var client = Client(owner, steppedUp: true);
        var route = $"/api/v1/organizations/{organizationId}/account/invitations";
        for (var index = 0; index < 10; index++)
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(route,
                new { displayLabel = $"Invite {index}", role = "viewer" })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync(route,
            new { displayLabel = "Invite 11", role = "viewer" })).StatusCode);
    }

    [Fact]
    public async Task Api_accepts_retiring_key_and_denies_its_premature_removal()
    {
        var owner = Subject("rotation-owner");
        var organizationId = await SeedOrganizationAsync(owner);
        AccountInvitationOptions configured;
        List<AccountInvitationKeyOptions> originalKeys;
        var retiringKey = new AccountInvitationKeyOptions
        {
            Id = "fixture-retiring",
            SecretBase64 = Convert.ToBase64String(Enumerable.Range(101, 32)
                .Select(value => (byte)value).ToArray())
        };
        var retiringTokens = new InvitationTokenService(Options.Create(
            new AccountInvitationOptions
            {
                Enabled = true,
                LifetimeHours = 168,
                MaximumCodeBytes = 64,
                ActiveKeyId = retiringKey.Id,
                Keys = [retiringKey]
            }));
        var acceptedToken = retiringTokens.Issue();
        var protectedToken = retiringTokens.Issue();
        Guid protectedInvitationId;
        using (var scope = factory.Services.CreateScope())
        {
            configured = scope.ServiceProvider.GetRequiredService<
                IOptions<AccountInvitationOptions>>().Value;
            originalKeys = configured.Keys!.ToList();
            configured.Keys = [originalKeys[0], retiringKey];
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var now = DateTimeOffset.UtcNow;
            var acceptedInvitation = OrganizationInvitation.Create(
                    organizationId, "Rotating accept",
                    OrganizationRole.Viewer, acceptedToken.Digest, acceptedToken.KeyId,
                    owner, now, now.AddDays(7));
            var protectedInvitation = OrganizationInvitation.Create(
                    organizationId, "Protected pending",
                    OrganizationRole.Viewer, protectedToken.Digest, protectedToken.KeyId,
                    owner, now, now.AddDays(7));
            protectedInvitationId = protectedInvitation.Id;
            database.OrganizationInvitations.AddRange(acceptedInvitation, protectedInvitation);
            await database.SaveChangesAsync();
        }

        try
        {
            using var member = Client(Subject("rotation-member"));
            Assert.Equal(HttpStatusCode.OK, (await member.PostAsJsonAsync(
                "/api/v1/account/invitations/accept",
                new { invitationCode = acceptedToken.Code })).StatusCode);

            configured.Keys = [originalKeys[0]];
            using var ownerClient = Client(owner, steppedUp: true);
            Assert.Equal(HttpStatusCode.ServiceUnavailable,
                (await ownerClient.PostAsJsonAsync(
                    $"/api/v1/organizations/{organizationId}/account/invitations",
                    new { displayLabel = "Denied rotation", role = "viewer" })).StatusCode);
            Assert.Equal(HttpStatusCode.ServiceUnavailable,
                (await member.PostAsJsonAsync("/api/v1/account/invitations/accept",
                    new { invitationCode = protectedToken.Code })).StatusCode);
        }
        finally
        {
            using var cleanup = factory.Services.CreateScope();
            var database = cleanup.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var protectedInvitation = database.OrganizationInvitations.SingleOrDefault(value =>
                value.Id == protectedInvitationId);
            if (protectedInvitation is not null)
            {
                database.OrganizationInvitations.Remove(protectedInvitation);
                await database.SaveChangesAsync();
            }
            configured.Keys = originalKeys;
        }
    }

    private async Task<Guid> SeedOrganizationAsync(string owner)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var organization = Organization.Create("Adversarial fixture", $"adversarial-{Guid.NewGuid():N}");
        database.Add(organization);
        database.Add(Membership.Create(organization.Id, owner, OrganizationRole.Owner,
            DateTimeOffset.UtcNow, "Owner"));
        await database.SaveChangesAsync();
        return organization.Id;
    }

    private async Task<Guid> SeedMembershipAsync(Guid organizationId, string subject,
        OrganizationRole role)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var membership = Membership.Create(organizationId, subject, role,
            DateTimeOffset.UtcNow, "Synthetic member");
        database.Add(membership);
        await database.SaveChangesAsync();
        return membership.Id;
    }

    private async Task SetStateAsync(Guid membershipId, MembershipState state)
    {
        if (state == MembershipState.Active) return;
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var member = database.Memberships.Single(value => value.Id == membershipId);
        member.Suspend(member.Revision, DateTimeOffset.UtcNow.AddSeconds(1));
        if (state == MembershipState.Revoked)
            member.Revoke(member.Revision, DateTimeOffset.UtcNow.AddSeconds(2));
        await database.SaveChangesAsync();
    }

    private static async Task<CreatedAccountInvitation> CreateInvitationAsync(
        HttpClient client, Guid organizationId, string role)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/account/invitations",
            new { displayLabel = "Synthetic invite", role });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ApiResponse<CreatedAccountInvitation>>())!
            .Payload;
    }

    private static async Task<AccountMemberSummary> MutateAsync(HttpClient client,
        string route, string action, long revision, string? role = null)
    {
        var response = await client.PatchAsJsonAsync(route,
            new { action, expectedRevision = revision, role });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ApiResponse<AccountMemberSummary>>())!
            .Payload;
    }

    private HttpClient Client(string subject, bool steppedUp = false,
        string? authenticationType = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Subject", subject);
        if (authenticationType is not null)
            client.DefaultRequestHeaders.Add("X-Test-Authentication-Type", authenticationType);
        if (steppedUp)
        {
            client.DefaultRequestHeaders.Add("X-Test-Scope", "manage:members");
            client.DefaultRequestHeaders.Add("X-Test-Mfa", "mfa");
            client.DefaultRequestHeaders.Add("X-Test-Iat",
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
        }
        return client;
    }

    private static string Subject(string label) => $"auth0|{label}-{Guid.NewGuid():N}";
}
