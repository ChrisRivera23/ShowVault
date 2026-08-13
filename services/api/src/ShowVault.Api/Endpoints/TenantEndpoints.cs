using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShowVault.Api.Contracts;
using ShowVault.Api.Data;
using ShowVault.Platform.Organizations;
using ShowVault.Platform.Venues;

namespace ShowVault.Api.Endpoints;

public static class TenantEndpoints
{
    public static IEndpointRouteBuilder MapTenantEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var organizations = endpoints.MapGroup("/api/v1/organizations")
            .RequireAuthorization();

        organizations.MapPost("/", CreateOrganizationAsync);
        organizations.MapGet("/", ListOrganizationsAsync);
        organizations.MapPost("/{organizationId:guid}/venues", CreateVenueAsync);
        organizations.MapGet("/{organizationId:guid}/venues", ListVenuesAsync);

        return endpoints;
    }

    private static async Task<IResult> CreateOrganizationAsync(
        CreateOrganizationRequest request,
        ClaimsPrincipal user,
        HttpContext context,
        PlatformDbContext database,
        CancellationToken cancellationToken)
    {
        var subject = user.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(subject))
        {
            return Results.Unauthorized();
        }

        Organization organization;
        try
        {
            organization = Organization.Create(request.Name, request.Slug);
        }
        catch (ArgumentException exception)
        {
            return ValidationProblem(exception, "organization");
        }

        database.Organizations.Add(organization);
        database.Memberships.Add(Membership.Create(
            organization.Id,
            subject,
            OrganizationRole.Owner));

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Results.Conflict(new ProblemDetails
            {
                Title = "An organization with this slug already exists.",
                Status = StatusCodes.Status409Conflict
            });
        }

        var summary = new OrganizationSummary(
            organization.Id,
            organization.Name,
            organization.Slug,
            "owner");
        return Results.Created(
            $"/api/v1/organizations/{organization.Id}",
            ApiResponse<OrganizationSummary>.Success(summary, context.TraceIdentifier));
    }

    private static async Task<IResult> ListOrganizationsAsync(
        ClaimsPrincipal user,
        HttpContext context,
        PlatformDbContext database,
        CancellationToken cancellationToken)
    {
        var subject = user.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(subject))
        {
            return Results.Unauthorized();
        }

        var accessibleOrganizations = await database.Memberships
            .Where(membership => membership.IdentitySubject == subject)
            .Join(
                database.Organizations,
                membership => membership.OrganizationId,
                organization => organization.Id,
                (membership, organization) => new { membership, organization })
            .OrderBy(result => result.organization.Name)
            .Select(result => new OrganizationSummary(
                result.organization.Id,
                result.organization.Name,
                result.organization.Slug,
                result.membership.Role == OrganizationRole.Owner ? "owner" :
                result.membership.Role == OrganizationRole.Administrator ? "administrator" :
                result.membership.Role == OrganizationRole.Manager ? "manager" :
                result.membership.Role == OrganizationRole.Technician ? "technician" : "viewer"))
            .ToListAsync(cancellationToken);

        return Results.Ok(ApiResponse<IReadOnlyList<OrganizationSummary>>.Success(
            accessibleOrganizations,
            context.TraceIdentifier));
    }

    private static async Task<IResult> CreateVenueAsync(
        Guid organizationId,
        CreateVenueRequest request,
        ClaimsPrincipal user,
        HttpContext context,
        PlatformDbContext database,
        CancellationToken cancellationToken)
    {
        var membership = await FindMembershipAsync(
            database,
            organizationId,
            user,
            cancellationToken);
        if (membership is null || !membership.Role.CanManageVenues())
        {
            return Results.Forbid();
        }

        Venue venue;
        try
        {
            venue = Venue.Create(organizationId, request.Name, request.TimeZoneId);
        }
        catch (ArgumentException exception)
        {
            return ValidationProblem(exception, "venue");
        }

        database.Venues.Add(venue);
        await database.SaveChangesAsync(cancellationToken);

        var summary = new VenueSummary(
            venue.Id,
            venue.OrganizationId,
            venue.Name,
            venue.TimeZoneId);
        return Results.Created(
            $"/api/v1/organizations/{organizationId}/venues/{venue.Id}",
            ApiResponse<VenueSummary>.Success(summary, context.TraceIdentifier));
    }

    private static async Task<IResult> ListVenuesAsync(
        Guid organizationId,
        ClaimsPrincipal user,
        HttpContext context,
        PlatformDbContext database,
        CancellationToken cancellationToken)
    {
        var membership = await FindMembershipAsync(
            database,
            organizationId,
            user,
            cancellationToken);
        if (membership is null)
        {
            return Results.Forbid();
        }

        var venues = await database.Venues
            .Where(venue => venue.OrganizationId == organizationId)
            .OrderBy(venue => venue.Name)
            .Select(venue => new VenueSummary(
                venue.Id,
                venue.OrganizationId,
                venue.Name,
                venue.TimeZoneId))
            .ToListAsync(cancellationToken);

        return Results.Ok(ApiResponse<IReadOnlyList<VenueSummary>>.Success(
            venues,
            context.TraceIdentifier));
    }

    private static Task<Membership?> FindMembershipAsync(
        PlatformDbContext database,
        Guid organizationId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var subject = user.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(subject))
        {
            return Task.FromResult<Membership?>(null);
        }

        return database.Memberships.SingleOrDefaultAsync(
            membership => membership.OrganizationId == organizationId &&
                membership.IdentitySubject == subject,
            cancellationToken);
    }

    private static IResult ValidationProblem(ArgumentException exception, string fallbackKey) =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [exception.ParamName ?? fallbackKey] = [exception.Message]
        });
}
