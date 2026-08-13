using System.Security.Claims;
using System.Text.Json;
using ShowVault.Api.Account;
using ShowVault.Api.Contracts;
using ShowVault.Platform.Organizations;

namespace ShowVault.Api.Endpoints;

public static class AccountEndpoints
{
    private static readonly JsonSerializerOptions StrictJson = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
    };

    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var organizations = endpoints.MapGroup(
            "/api/v1/organizations/{organizationId:guid}/account").RequireAuthorization();
        organizations.MapGet("/members", ListMembersAsync);
        organizations.MapGet("/invitations", ListInvitationsAsync);
        organizations.MapPost("/invitations", CreateInvitationAsync)
            .RequireRateLimiting("account-mutation");
        organizations.MapPost("/invitations/{invitationId:guid}/revoke", RevokeInvitationAsync)
            .RequireRateLimiting("account-mutation");
        organizations.MapMethods("/members/{membershipId:guid}", ["PATCH"], MutateMemberAsync)
            .RequireRateLimiting("account-mutation");
        endpoints.MapPost("/api/v1/account/invitations/accept", AcceptInvitationAsync)
            .RequireAuthorization().RequireRateLimiting("invitation-accept");
        return endpoints;
    }

    private static async Task<IResult> ListMembersAsync(Guid organizationId,
        ClaimsPrincipal user, HttpContext context, AccountAdministrationService accounts,
        CancellationToken cancellationToken) => Result(
            await accounts.ListMembersAsync(organizationId, user, cancellationToken), context);

    private static async Task<IResult> ListInvitationsAsync(Guid organizationId,
        ClaimsPrincipal user, HttpContext context, AccountAdministrationService accounts,
        CancellationToken cancellationToken) => Result(
            await accounts.ListInvitationsAsync(organizationId, user, cancellationToken), context);

    private static async Task<IResult> CreateInvitationAsync(Guid organizationId,
        HttpRequest request, ClaimsPrincipal user, HttpContext context,
        AccountAdministrationService accounts, CancellationToken cancellationToken)
    {
        var body = await ParseAsync<CreateInvitationBody>(request, cancellationToken);
        if (body is null || !TryRole(body.Role, out var role)) return Results.BadRequest();
        var result = await accounts.CreateInvitationAsync(organizationId,
            body.DisplayLabel ?? "", role, user, context.TraceIdentifier, cancellationToken);
        if (result.Kind == AccountResultKind.Success)
        {
            context.Response.Headers.CacheControl = "no-store";
            return Results.Created(
                $"/api/v1/organizations/{organizationId}/account/invitations/{result.Value!.Id}",
                ApiResponse<CreatedAccountInvitation>.Success(
                    result.Value, context.TraceIdentifier));
        }
        return Failure(result.Kind);
    }

    private static async Task<IResult> RevokeInvitationAsync(Guid organizationId,
        Guid invitationId, ClaimsPrincipal user, HttpContext context,
        AccountAdministrationService accounts, CancellationToken cancellationToken) => Result(
            await accounts.RevokeInvitationAsync(organizationId, invitationId, user,
                context.TraceIdentifier, cancellationToken), context);

    private static async Task<IResult> AcceptInvitationAsync(HttpRequest request,
        ClaimsPrincipal user, HttpContext context, AccountAdministrationService accounts,
        CancellationToken cancellationToken)
    {
        var body = await ParseAsync<AcceptInvitationBody>(request, cancellationToken);
        if (body?.InvitationCode is null) return Results.BadRequest();
        return Result(await accounts.AcceptInvitationAsync(body.InvitationCode, user,
            context.TraceIdentifier, cancellationToken), context);
    }

    private static async Task<IResult> MutateMemberAsync(Guid organizationId,
        Guid membershipId, HttpRequest request, ClaimsPrincipal user, HttpContext context,
        AccountAdministrationService accounts, CancellationToken cancellationToken)
    {
        var body = await ParseAsync<MutateMemberBody>(request, cancellationToken);
        if (body is null || body.ExpectedRevision < 1 ||
            body.Role is not null && !TryRole(body.Role, out _) ||
            !ValidMutation(body.Action, body.Role)) return Results.BadRequest();
        OrganizationRole? role = body.Role is null ? null : ParseRole(body.Role);
        return Result(await accounts.MutateMemberAsync(organizationId, membershipId,
            body.Action ?? "", body.ExpectedRevision, role, user, context.TraceIdentifier,
            cancellationToken), context);
    }

    private static async Task<T?> ParseAsync<T>(HttpRequest request,
        CancellationToken cancellationToken) where T : class
    {
        if (!request.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase)
                ?? true || request.ContentLength is > 4096) return null;
        await using var buffer = new MemoryStream();
        var chunk = new byte[1024];
        while (true)
        {
            var count = await request.Body.ReadAsync(chunk, cancellationToken);
            if (count == 0) break;
            if (buffer.Length + count > 4096) return null;
            await buffer.WriteAsync(chunk.AsMemory(0, count), cancellationToken);
        }
        buffer.Position = 0;
        try { return await JsonSerializer.DeserializeAsync<T>(buffer, StrictJson, cancellationToken); }
        catch (JsonException) { return null; }
    }

    private static IResult Result<T>(AccountResult<T> result, HttpContext context) =>
        result.Kind == AccountResultKind.Success
            ? Results.Ok(ApiResponse<T>.Success(result.Value!, context.TraceIdentifier))
            : Failure(result.Kind);

    private static IResult Failure(AccountResultKind kind) => kind switch
    {
        AccountResultKind.Unauthorized => Results.Unauthorized(),
        AccountResultKind.Forbidden => Results.Forbid(),
        AccountResultKind.NotFound => Results.NotFound(),
        AccountResultKind.Conflict => Results.Conflict(),
        AccountResultKind.BadRequest => Results.BadRequest(),
        AccountResultKind.FeatureUnavailable => Results.Problem(statusCode: 503,
            title: "Account invitations are unavailable."),
        AccountResultKind.InvitationUnavailable => Results.BadRequest(new
        {
            code = "invitation_unavailable"
        }),
        _ => Results.StatusCode(500)
    };

    private static bool TryRole(string? value, out OrganizationRole role)
    {
        role = value switch
        {
            "viewer" => OrganizationRole.Viewer,
            "technician" => OrganizationRole.Technician,
            "manager" => OrganizationRole.Manager,
            "administrator" => OrganizationRole.Administrator,
            _ => OrganizationRole.Owner
        };
        return role.IsNonOwner();
    }

    private static OrganizationRole ParseRole(string value)
    {
        _ = TryRole(value, out var role);
        return role;
    }

    private static bool ValidMutation(string? action, string? role) => action switch
    {
        "change_role" => role is not null,
        "suspend" or "restore" or "revoke" => role is null,
        _ => false
    };

    private sealed record CreateInvitationBody(string? DisplayLabel, string? Role);
    private sealed record AcceptInvitationBody(string? InvitationCode);
    private sealed record MutateMemberBody(string? Action, long ExpectedRevision, string? Role);
}
