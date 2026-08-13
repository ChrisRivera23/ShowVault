using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ShowVault.Api.Data;
using ShowVault.Api.Security;
using ShowVault.Platform.Organizations;

namespace ShowVault.Api.Authorization;

public sealed record OrganizationAccess(
    Guid Id,
    string Name,
    string Slug,
    OrganizationRole Role);

public static class HumanIdentity
{
    public static string? Subject(ClaimsPrincipal user)
    {
        var subject = user.FindFirstValue("sub")?.Trim();
        return string.IsNullOrWhiteSpace(subject) ? null : subject;
    }

    public static bool IsPersonalBeta(ClaimsPrincipal user) =>
        user.Identities.Any(identity => identity.IsAuthenticated &&
            identity.AuthenticationType == PersonalBetaAuthenticationHandler.SchemeName);
}

public sealed class MembershipAuthorizationService(PlatformDbContext database)
{
    public Task<Membership?> FindActiveAsync(
        Guid organizationId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var subject = HumanIdentity.Subject(user);
        return subject is null
            ? Task.FromResult<Membership?>(null)
            : database.Memberships.SingleOrDefaultAsync(membership =>
                membership.OrganizationId == organizationId &&
                membership.IdentitySubject == subject &&
                membership.State == MembershipState.Active,
                cancellationToken);
    }

    public async Task<bool> IsActiveAsync(
        Guid organizationId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken) =>
        await FindActiveAsync(organizationId, user, cancellationToken) is not null;

    public async Task<bool> CanManageAsync(
        Guid organizationId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var membership = await FindActiveAsync(organizationId, user, cancellationToken);
        return membership?.Role.CanManageVenues() == true;
    }

    public async Task<bool> IsOwnerAsync(
        Guid organizationId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var membership = await FindActiveAsync(organizationId, user, cancellationToken);
        return membership?.Role == OrganizationRole.Owner;
    }

    public async Task<bool> HasVenueAccessAsync(
        Guid organizationId,
        Guid venueId,
        ClaimsPrincipal user,
        bool requireManager,
        CancellationToken cancellationToken)
    {
        var membership = await FindActiveAsync(organizationId, user, cancellationToken);
        if (membership is null || requireManager && !membership.Role.CanManageVenues())
            return false;
        return await database.Venues.AnyAsync(venue =>
            venue.Id == venueId && venue.OrganizationId == organizationId,
            cancellationToken);
    }

    public async Task<bool> CanManageAgentAsync(
        Guid organizationId,
        Guid venueId,
        Guid agentId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken) =>
        await HasVenueAccessAsync(organizationId, venueId, user, true, cancellationToken) &&
        await database.VenueAgents.AnyAsync(agent =>
            agent.Id == agentId && agent.VenueId == venueId && agent.RevokedAt == null,
            cancellationToken);

    public Task<List<OrganizationAccess>> ListActiveOrganizationsAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var subject = HumanIdentity.Subject(user);
        if (subject is null)
            return Task.FromResult(new List<OrganizationAccess>());
        return database.Memberships
            .Where(membership => membership.IdentitySubject == subject &&
                membership.State == MembershipState.Active)
            .Join(database.Organizations,
                membership => membership.OrganizationId,
                organization => organization.Id,
                (membership, organization) => new { membership, organization })
            .OrderBy(result => result.organization.Name)
            .Select(result => new OrganizationAccess(
                result.organization.Id,
                result.organization.Name,
                result.organization.Slug,
                result.membership.Role))
            .ToListAsync(cancellationToken);
    }
}
