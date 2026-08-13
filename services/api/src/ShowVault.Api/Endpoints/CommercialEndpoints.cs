namespace ShowVault.Api.Endpoints;

using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ShowVault.Api.Commercial;
using ShowVault.Api.Contracts;
using ShowVault.Api.Data;
using ShowVault.Platform.Organizations;

public static class CommercialEndpoints
{
    public static IEndpointRouteBuilder MapCommercialEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/organizations/{organizationId:guid}/plan", GetPlanAsync)
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> GetPlanAsync(
        Guid organizationId,
        ClaimsPrincipal user,
        HttpContext context,
        PlatformDbContext database,
        CommercialStateService commercial,
        CancellationToken cancellationToken)
    {
        var subject = user.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(subject)) return Results.Unauthorized();
        var owner = await database.Memberships.AnyAsync(membership =>
            membership.OrganizationId == organizationId &&
            membership.IdentitySubject == subject &&
            membership.Role == OrganizationRole.Owner, cancellationToken);
        if (!owner) return Results.Forbid();
        var plan = await commercial.GetPlanAsync(organizationId, cancellationToken);
        return Results.Ok(ApiResponse<OrganizationPlanSnapshot>.Success(
            plan, context.TraceIdentifier));
    }
}
