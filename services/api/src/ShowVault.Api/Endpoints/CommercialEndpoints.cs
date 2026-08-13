namespace ShowVault.Api.Endpoints;

using System.Security.Claims;
using ShowVault.Api.Authorization;
using ShowVault.Api.Commercial;
using ShowVault.Api.Contracts;

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
        CommercialStateService commercial,
        MembershipAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        if (HumanIdentity.Subject(user) is null) return Results.Unauthorized();
        var owner = await authorization.IsOwnerAsync(
            organizationId, user, cancellationToken);
        if (!owner) return Results.Forbid();
        var plan = await commercial.GetPlanAsync(organizationId, cancellationToken);
        return Results.Ok(ApiResponse<OrganizationPlanSnapshot>.Success(
            plan, context.TraceIdentifier));
    }
}
