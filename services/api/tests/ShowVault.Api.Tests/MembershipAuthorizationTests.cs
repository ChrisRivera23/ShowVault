using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using ShowVault.Api.Authorization;
using ShowVault.Api.Data;
using ShowVault.Platform.Organizations;
using ShowVault.Platform.Venues;
using Xunit;

namespace ShowVault.Api.Tests;

public sealed class MembershipAuthorizationTests(TenantApiFactory factory)
    : IClassFixture<TenantApiFactory>
{
    [Fact]
    public async Task Active_state_role_and_exact_venue_are_all_required()
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var authorization = scope.ServiceProvider.GetRequiredService<MembershipAuthorizationService>();
        var organization = Organization.Create("Authorization fixture", $"auth-{Guid.NewGuid():N}");
        var otherOrganization = Organization.Create("Other fixture", $"other-{Guid.NewGuid():N}");
        var venue = Venue.Create(organization.Id, "Main", "America/New_York");
        var member = Membership.Create(organization.Id, "auth0|manager",
            OrganizationRole.Manager, DateTimeOffset.UtcNow);
        database.AddRange(organization, otherOrganization, venue, member);
        await database.SaveChangesAsync();

        var principal = Principal("auth0|manager");
        Assert.True(await authorization.HasVenueAccessAsync(
            organization.Id, venue.Id, principal, true, default));
        Assert.False(await authorization.HasVenueAccessAsync(
            otherOrganization.Id, venue.Id, principal, true, default));
        Assert.False(await authorization.IsOwnerAsync(organization.Id, principal, default));
        Assert.False(await authorization.IsActiveAsync(
            organization.Id, new ClaimsPrincipal(), default));

        member.Suspend(member.Revision, DateTimeOffset.UtcNow.AddSeconds(1));
        await database.SaveChangesAsync();
        Assert.False(await authorization.HasVenueAccessAsync(
            organization.Id, venue.Id, principal, false, default));
        Assert.Empty(await authorization.ListActiveOrganizationsAsync(principal, default));

        member.Revoke(member.Revision, DateTimeOffset.UtcNow.AddSeconds(2));
        await database.SaveChangesAsync();
        Assert.False(await authorization.IsActiveAsync(organization.Id, principal, default));
    }

    [Fact]
    public async Task Append_only_account_audit_rejects_modification()
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var organization = Organization.Create("Audit fixture", $"audit-{Guid.NewGuid():N}");
        database.Organizations.Add(organization);
        var audit = AccountAuditEvent.Create(organization.Id, "auth0|owner", "membership",
            Guid.NewGuid(), "membership_suspend", "success", "authorized",
            "correlation-fixture", "account-v1", DateTimeOffset.UtcNow);
        database.AccountAuditEvents.Add(audit);
        await database.SaveChangesAsync();

        database.Entry(audit).Property(value => value.ReasonCode).CurrentValue = "changed";
        await Assert.ThrowsAsync<InvalidOperationException>(() => database.SaveChangesAsync());
    }

    private static ClaimsPrincipal Principal(string subject) => new(
        new ClaimsIdentity([new Claim("sub", subject)], "Test"));
}
