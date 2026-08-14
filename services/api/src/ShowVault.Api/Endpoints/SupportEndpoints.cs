using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Net.Http.Headers;
using ShowVault.Api.Contracts;
using ShowVault.Api.Support;

namespace ShowVault.Api.Endpoints;

public static class SupportEndpoints
{
    public static IEndpointRouteBuilder MapSupportEndpoints(this IEndpointRouteBuilder endpoints,
        string expectedIssuer)
    {
        endpoints.MapPost("/api/v1/support/organization-overview",
                (HttpRequest request, ClaimsPrincipal user, HttpContext context,
                 SupportAuthorizationService authorization,
                 SupportOrganizationOverviewService overview, CancellationToken cancellationToken) =>
                    HandleAsync(request, user, context, authorization, overview,
                        expectedIssuer, cancellationToken))
            .RequireAuthorization(new AuthorizeAttribute
            {
                AuthenticationSchemes = SupportAdminOptions.SchemeName
            });
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(HttpRequest request, ClaimsPrincipal user,
        HttpContext context, SupportAuthorizationService authorization,
        SupportOrganizationOverviewService overview,
        string expectedIssuer, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        var source = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var authority = authorization.Evaluate(user, expectedIssuer, source);
        if (authority.Kind == SupportRequestAuthorizationKind.Forbidden) return Results.Forbid();
        if (authority.Kind == SupportRequestAuthorizationKind.RateLimited)
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);
        var body = await ParseAsync(request, cancellationToken);
        if (body is null || body.OrganizationId == Guid.Empty) return Results.BadRequest();
        var result = await overview.GetAsync(body.OrganizationId, authority.Issuer!,
            authority.Subject!, context.TraceIdentifier, cancellationToken);
        return result.Kind switch
        {
            SupportOverviewResultKind.Success => Results.Ok(result.Value),
            SupportOverviewResultKind.StaffUnavailable => Results.Forbid(),
            SupportOverviewResultKind.TargetUnavailable => Results.NotFound(new
            { code = "support_target_unavailable" }),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError,
                title: "Support request failed.")
        };
    }

    internal static async Task<SupportOrganizationOverviewRequest?> ParseAsync(HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var contentType) ||
            !string.Equals(contentType.MediaType.Value, "application/json",
                StringComparison.OrdinalIgnoreCase) || request.ContentLength is > 4096) return null;
        await using var buffer = new MemoryStream();
        var chunk = new byte[1024];
        while (true)
        {
            var count = await request.Body.ReadAsync(chunk, cancellationToken);
            if (count == 0) break;
            if (buffer.Length + count > 4096) return null;
            await buffer.WriteAsync(chunk.AsMemory(0, count), cancellationToken);
        }
        try
        {
            using var document = JsonDocument.Parse(buffer.ToArray());
            if (document.RootElement.ValueKind != JsonValueKind.Object) return null;
            var properties = document.RootElement.EnumerateObject().ToArray();
            if (properties.Length != 1 || properties[0].Name != "organizationId" ||
                properties[0].Value.ValueKind != JsonValueKind.String ||
                !Guid.TryParseExact(properties[0].Value.GetString(), "D", out var organizationId) ||
                organizationId == Guid.Empty)
                return null;
            return new(organizationId);
        }
        catch (JsonException) { return null; }
    }
}
