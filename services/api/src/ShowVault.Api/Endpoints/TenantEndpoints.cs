using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShowVault.Api.Authorization;
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
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var subject = HumanIdentity.Subject(user);
        if (subject is null)
        {
            return Results.Unauthorized();
        }
        if (HumanIdentity.IsPersonalBeta(user))
        {
            return Results.Forbid();
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
            OrganizationRole.Owner,
            timeProvider.GetUtcNow()));

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
        MembershipAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        if (HumanIdentity.Subject(user) is null)
        {
            return Results.Unauthorized();
        }

        var activeOrganizations = await authorization.ListActiveOrganizationsAsync(
            user, cancellationToken);
        var accessibleOrganizations = activeOrganizations.Select(result => new OrganizationSummary(
            result.Id,
            result.Name,
            result.Slug,
            result.Role == OrganizationRole.Owner ? "owner" :
            result.Role == OrganizationRole.Administrator ? "administrator" :
            result.Role == OrganizationRole.Manager ? "manager" :
            result.Role == OrganizationRole.Technician ? "technician" : "viewer")).ToList();

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
        MembershipAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        var membership = await authorization.FindActiveAsync(
            organizationId, user, cancellationToken);
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
        MembershipAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        var membership = await authorization.FindActiveAsync(
            organizationId, user, cancellationToken);
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

    private static IResult ValidationProblem(ArgumentException exception, string fallbackKey) =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [exception.ParamName ?? fallbackKey] = [exception.Message]
        });
}
