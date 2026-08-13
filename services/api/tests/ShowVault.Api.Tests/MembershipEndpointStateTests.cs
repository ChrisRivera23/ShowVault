using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShowVault.Api.Data;
using ShowVault.Platform.Organizations;
using ShowVault.Platform.Venues;
using Xunit;

namespace ShowVault.Api.Tests;

public sealed class MembershipEndpointStateTests(TenantApiFactory factory)
    : IClassFixture<TenantApiFactory>
{
    public enum Surface
    {
        Tenant,
        RecoveryCandidates,
        RecoveryHistory,
        AgentEnrollment,
        CommercialPlan,
        Billing,
        HostedSync,
        AccountAdministration
    }

    [Theory]
    [InlineData(Surface.Tenant, MembershipState.Suspended)]
    [InlineData(Surface.Tenant, MembershipState.Revoked)]
    [InlineData(Surface.RecoveryCandidates, MembershipState.Suspended)]
    [InlineData(Surface.RecoveryCandidates, MembershipState.Revoked)]
    [InlineData(Surface.RecoveryHistory, MembershipState.Suspended)]
    [InlineData(Surface.RecoveryHistory, MembershipState.Revoked)]
    [InlineData(Surface.AgentEnrollment, MembershipState.Suspended)]
    [InlineData(Surface.AgentEnrollment, MembershipState.Revoked)]
    [InlineData(Surface.CommercialPlan, MembershipState.Suspended)]
    [InlineData(Surface.CommercialPlan, MembershipState.Revoked)]
    [InlineData(Surface.Billing, MembershipState.Suspended)]
    [InlineData(Surface.Billing, MembershipState.Revoked)]
    [InlineData(Surface.HostedSync, MembershipState.Suspended)]
    [InlineData(Surface.HostedSync, MembershipState.Revoked)]
    [InlineData(Surface.AccountAdministration, MembershipState.Suspended)]
    [InlineData(Surface.AccountAdministration, MembershipState.Revoked)]
    public async Task Every_endpoint_module_requires_active_membership(
        Surface surface, MembershipState deniedState)
    {
        var subject = $"auth0|surface-{surface}-{deniedState}-{Guid.NewGuid():N}";
        var role = surface is Surface.CommercialPlan or Surface.Billing or
            Surface.AccountAdministration ? OrganizationRole.Owner : OrganizationRole.Manager;
        var fixture = await SeedAsync(subject, role);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Subject", subject);

        var active = await SendAsync(client, surface, fixture);
        Assert.NotEqual(HttpStatusCode.Forbidden, active.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, active.StatusCode);

        await ChangeStateAsync(fixture.MembershipId, deniedState);
        var denied = await SendAsync(client, surface, fixture);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task Restoration_reenables_only_role_permitted_surface()
    {
        var subject = $"auth0|restored-viewer-{Guid.NewGuid():N}";
        var fixture = await SeedAsync(subject, OrganizationRole.Viewer);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Subject", subject);
        await ChangeStateAsync(fixture.MembershipId, MembershipState.Suspended);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendAsync(client, Surface.RecoveryHistory, fixture)).StatusCode);
        await RestoreAsync(fixture.MembershipId);

        Assert.Equal(HttpStatusCode.OK,
            (await SendAsync(client, Surface.RecoveryHistory, fixture)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendAsync(client, Surface.AgentEnrollment, fixture)).StatusCode);
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, Surface surface,
        Fixture fixture) => surface switch
    {
        Surface.Tenant => client.GetAsync(
            $"/api/v1/organizations/{fixture.OrganizationId}/venues"),
        Surface.RecoveryCandidates => client.GetAsync(
            $"/api/v1/organizations/{fixture.OrganizationId}/venues/{fixture.VenueId}" +
            "/recovery-candidates"),
        Surface.RecoveryHistory => client.GetAsync(
            $"/api/v1/organizations/{fixture.OrganizationId}/venues/{fixture.VenueId}" +
            "/recovery-runs"),
        Surface.AgentEnrollment => client.PostAsync(
            $"/api/v1/organizations/{fixture.OrganizationId}/venues/{fixture.VenueId}" +
            "/agent-enrollments", null),
        Surface.CommercialPlan => client.GetAsync(
            $"/api/v1/organizations/{fixture.OrganizationId}/plan"),
        Surface.Billing => client.GetAsync(
            $"/api/v1/organizations/{fixture.OrganizationId}/billing/offering"),
        Surface.HostedSync => client.GetAsync(
            $"/api/v1/organizations/{fixture.OrganizationId}/venues/{fixture.VenueId}" +
            "/hosted-sync/" + new string('a', 64) + "/receipt"),
        Surface.AccountAdministration => client.GetAsync(
            $"/api/v1/organizations/{fixture.OrganizationId}/account/members"),
        _ => throw new ArgumentOutOfRangeException(nameof(surface))
    };

    private async Task<Fixture> SeedAsync(string subject, OrganizationRole role)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var organization = Organization.Create("Surface fixture", $"surface-{Guid.NewGuid():N}");
        var venue = Venue.Create(organization.Id, "Synthetic venue", "UTC");
        var membership = Membership.Create(organization.Id, subject, role,
            DateTimeOffset.UtcNow, "Synthetic member");
        database.AddRange(organization, venue, membership);
        await database.SaveChangesAsync();
        return new(organization.Id, venue.Id, membership.Id);
    }

    private async Task ChangeStateAsync(Guid membershipId, MembershipState state)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        // Owner transitions are deliberately unavailable through the domain. A
        // direct fixture update proves every endpoint still fails closed if a
        // legacy/imported row ever carries a non-active Owner state.
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE memberships SET State = {state.ToString()}, Revision = Revision + 1 WHERE Id = {membershipId}");
    }

    private async Task RestoreAsync(Guid membershipId)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var membership = database.Memberships.Single(value => value.Id == membershipId);
        membership.Restore(membership.Revision, DateTimeOffset.UtcNow.AddSeconds(2));
        await database.SaveChangesAsync();
    }

    private sealed record Fixture(Guid OrganizationId, Guid VenueId, Guid MembershipId);
}
