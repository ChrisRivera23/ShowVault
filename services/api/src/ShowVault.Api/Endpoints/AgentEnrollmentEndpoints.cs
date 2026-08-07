using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ShowVault.Api.Contracts;
using ShowVault.Api.Data;
using ShowVault.Api.Security;
using ShowVault.AgentContracts;
using ShowVault.Platform.Agents;
using ShowVault.Platform.Organizations;

namespace ShowVault.Api.Endpoints;

public static class AgentEnrollmentEndpoints
{
    private static readonly TimeSpan EnrollmentLifetime = TimeSpan.FromMinutes(15);

    public static IEndpointRouteBuilder MapAgentEnrollmentEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var organizations = endpoints.MapGroup("/api/v1/organizations")
            .RequireAuthorization();
        organizations.MapPost(
            "/{organizationId:guid}/venues/{venueId:guid}/agent-enrollments",
            CreateEnrollmentAsync);
        organizations.MapDelete(
            "/{organizationId:guid}/venues/{venueId:guid}/agents/{agentId:guid}",
            RevokeAgentAsync);

        endpoints.MapPost("/api/v1/agents/enroll", EnrollAgentAsync)
            .RequireRateLimiting("agent-enrollment");
        endpoints.MapGet("/api/v1/agent-identity", GetAgentIdentity)
            .RequireAuthorization(new AuthorizeAttribute
            {
                AuthenticationSchemes = AgentAuthenticationHandler.SchemeName
            });
        endpoints.MapPost("/api/v1/agents/rotate-credential", RotateCredentialAsync)
            .RequireAuthorization(new AuthorizeAttribute
            {
                AuthenticationSchemes = AgentAuthenticationHandler.SchemeName
            });

        return endpoints;
    }

    private static async Task<IResult> CreateEnrollmentAsync(
        Guid organizationId,
        Guid venueId,
        ClaimsPrincipal user,
        HttpContext context,
        PlatformDbContext database,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var subject = user.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(subject) ||
            !await CanManageVenueAsync(
                database,
                organizationId,
                venueId,
                subject,
                cancellationToken))
        {
            return Results.Forbid();
        }

        var code = AgentSecrets.Generate("sve_");
        var now = timeProvider.GetUtcNow();
        var enrollment = AgentEnrollment.Create(
            venueId,
            AgentSecrets.Hash(code),
            subject,
            now,
            EnrollmentLifetime);
        database.AgentEnrollments.Add(enrollment);
        await database.SaveChangesAsync(cancellationToken);
        context.Response.Headers.CacheControl = "no-store";

        return Results.Created(
            $"/api/v1/organizations/{organizationId}/venues/{venueId}/agent-enrollments/{enrollment.Id}",
            ApiResponse<CreateAgentEnrollmentResponse>.Success(
                new CreateAgentEnrollmentResponse(
                    enrollment.Id,
                    code,
                    enrollment.ExpiresAt),
                context.TraceIdentifier));
    }

    private static async Task<IResult> EnrollAgentAsync(
        EnrollAgentRequest request,
        HttpContext context,
        PlatformDbContext database,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.EnrollmentCode))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.EnrollmentCode)] = ["Enrollment code is required."]
            });
        }

        var codeHash = AgentSecrets.Hash(request.EnrollmentCode);
        var enrollment = await database.AgentEnrollments.SingleOrDefaultAsync(
            candidate => candidate.SecretHash.SequenceEqual(codeHash),
            cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (enrollment is null || !enrollment.CanBeConsumed(now))
        {
            return Results.Unauthorized();
        }

        VenueAgent agent;
        var secret = AgentSecrets.Generate("sva_");
        try
        {
            agent = VenueAgent.Create(
                enrollment.VenueId,
                request.Name,
                AgentSecrets.Hash(secret),
                now);
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [exception.ParamName ?? "agent"] = [exception.Message]
            });
        }

        enrollment.Consume(now);
        database.VenueAgents.Add(agent);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Unauthorized();
        }

        var credential = $"{agent.Id}.{secret}";
        context.Response.Headers.CacheControl = "no-store";
        return Results.Ok(ApiResponse<EnrollAgentResponse>.Success(
            new EnrollAgentResponse(agent.Id, agent.VenueId, credential),
            context.TraceIdentifier));
    }

    private static IResult GetAgentIdentity(ClaimsPrincipal user, HttpContext context)
    {
        if (!Guid.TryParse(user.FindFirstValue("agent_id"), out var agentId) ||
            !Guid.TryParse(user.FindFirstValue("venue_id"), out var venueId))
        {
            return Results.Unauthorized();
        }

        return Results.Ok(ApiResponse<AgentIdentityResponse>.Success(
            new AgentIdentityResponse(agentId, venueId),
            context.TraceIdentifier));
    }

    private static async Task<IResult> RotateCredentialAsync(
        ClaimsPrincipal user,
        HttpContext context,
        PlatformDbContext database,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(user.FindFirstValue("agent_id"), out var agentId))
        {
            return Results.Unauthorized();
        }

        var agent = await database.VenueAgents.SingleOrDefaultAsync(
            candidate => candidate.Id == agentId,
            cancellationToken);
        if (agent is null || agent.RevokedAt is not null)
        {
            return Results.Unauthorized();
        }

        var secret = AgentSecrets.Generate("sva_");
        var rotatedAt = timeProvider.GetUtcNow();
        agent.RotateCredential(AgentSecrets.Hash(secret), rotatedAt);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict();
        }

        context.Response.Headers.CacheControl = "no-store";
        return Results.Ok(ApiResponse<RotateAgentCredentialResponse>.Success(
            new RotateAgentCredentialResponse($"{agent.Id}.{secret}", rotatedAt),
            context.TraceIdentifier));
    }

    private static async Task<IResult> RevokeAgentAsync(
        Guid organizationId,
        Guid venueId,
        Guid agentId,
        ClaimsPrincipal user,
        PlatformDbContext database,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var subject = user.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(subject) ||
            !await CanManageVenueAsync(
                database,
                organizationId,
                venueId,
                subject,
                cancellationToken))
        {
            return Results.Forbid();
        }

        var agent = await database.VenueAgents.SingleOrDefaultAsync(
            candidate => candidate.Id == agentId && candidate.VenueId == venueId,
            cancellationToken);
        if (agent is null)
        {
            return Results.NotFound();
        }

        agent.Revoke(timeProvider.GetUtcNow());
        await database.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static Task<bool> CanManageVenueAsync(
        PlatformDbContext database,
        Guid organizationId,
        Guid venueId,
        string subject,
        CancellationToken cancellationToken) =>
        database.Venues
            .Where(venue => venue.Id == venueId && venue.OrganizationId == organizationId)
            .Join(
                database.Memberships.Where(membership =>
                    membership.OrganizationId == organizationId &&
                    membership.IdentitySubject == subject),
                venue => venue.OrganizationId,
                membership => membership.OrganizationId,
                (_, membership) => membership.Role)
            .AnyAsync(role => role == OrganizationRole.Manager ||
                role == OrganizationRole.Administrator ||
                role == OrganizationRole.Owner,
                cancellationToken);
}
