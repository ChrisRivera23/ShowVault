using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ShowVault.Api.Contracts;
using ShowVault.Api.Data;
using ShowVault.Api.Security;
using ShowVault.AgentContracts;
using ShowVault.Platform.Agents;
using ShowVault.Platform.Organizations;

namespace ShowVault.Api.Endpoints;

public static class AgentCommunicationEndpoints
{
    public static IEndpointRouteBuilder MapAgentCommunicationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/agent-events", ReceiveEventAsync)
            .RequireAuthorization(new AuthorizeAttribute
            {
                AuthenticationSchemes = AgentAuthenticationHandler.SchemeName
            });
        endpoints.MapGet("/api/v1/agent-commands", PollCommandsAsync)
            .RequireAuthorization(new AuthorizeAttribute
            {
                AuthenticationSchemes = AgentAuthenticationHandler.SchemeName
            });
        endpoints.MapPost(
                "/api/v1/agent-commands/{commandId:guid}/acknowledge",
                AcknowledgeCommandAsync)
            .RequireAuthorization(new AuthorizeAttribute
            {
                AuthenticationSchemes = AgentAuthenticationHandler.SchemeName
            });
        endpoints.MapPost(
                "/api/v1/organizations/{organizationId:guid}/venues/{venueId:guid}/agents/{agentId:guid}/commands",
                IssueCommandAsync)
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> ReceiveEventAsync(
        AgentEventEnvelope envelope,
        ClaimsPrincipal user,
        PlatformDbContext database,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(user.FindFirstValue("agent_id"), out var authenticatedAgentId) ||
            envelope.AgentId != authenticatedAgentId)
        {
            return Results.Forbid();
        }

        if (!AgentEventValidation.TryValidate(envelope, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["event"] = [validationError]
            });
        }

        if (await database.ReceivedAgentEvents.AnyAsync(
            agentEvent => agentEvent.EventId == envelope.EventId,
            cancellationToken))
        {
            return Results.Accepted();
        }

        database.ReceivedAgentEvents.Add(ReceivedAgentEvent.FromEnvelope(
            envelope,
            timeProvider.GetUtcNow()));
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            database.ChangeTracker.Clear();
            if (!await database.ReceivedAgentEvents.AnyAsync(
                agentEvent => agentEvent.EventId == envelope.EventId,
                cancellationToken))
            {
                throw;
            }
        }

        return Results.Accepted();
    }

    private static async Task<IResult> IssueCommandAsync(
        Guid organizationId,
        Guid venueId,
        Guid agentId,
        IssueAgentCommandRequest request,
        ClaimsPrincipal user,
        HttpContext context,
        PlatformDbContext database,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var subject = user.FindFirstValue("sub");
        var authorized = !string.IsNullOrWhiteSpace(subject) &&
            await database.VenueAgents
                .Where(agent => agent.Id == agentId &&
                    agent.VenueId == venueId &&
                    agent.RevokedAt == null)
                .Join(
                    database.Venues.Where(venue =>
                        venue.Id == venueId && venue.OrganizationId == organizationId),
                    agent => agent.VenueId,
                    venue => venue.Id,
                    (_, venue) => venue.OrganizationId)
                .Join(
                    database.Memberships.Where(membership =>
                        membership.IdentitySubject == subject &&
                        (membership.Role == OrganizationRole.Manager ||
                         membership.Role == OrganizationRole.Administrator ||
                         membership.Role == OrganizationRole.Owner)),
                    candidateOrganizationId => candidateOrganizationId,
                    membership => membership.OrganizationId,
                    (_, _) => true)
                .AnyAsync(cancellationToken);
        if (!authorized)
        {
            return Results.Forbid();
        }

        if (request.ValidForSeconds is < 1 or > 3600 || request.Payload.Length > 262_144)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.ValidForSeconds)] =
                    ["Command validity must be between 1 and 3600 seconds and payloads at most 256 KiB."]
            });
        }

        try
        {
            using var _ = JsonDocument.Parse(request.Payload);
        }
        catch (JsonException)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Payload)] = ["Command payload must be valid JSON."]
            });
        }

        var envelope = AgentCommandEnvelope.Create(
            agentId,
            request.Type,
            context.TraceIdentifier,
            request.Payload,
            timeProvider.GetUtcNow(),
            TimeSpan.FromSeconds(request.ValidForSeconds));
        database.IssuedAgentCommands.Add(IssuedAgentCommand.FromEnvelope(envelope));
        await database.SaveChangesAsync(cancellationToken);
        return Results.Accepted(
            $"/api/v1/agent-commands/{envelope.CommandId}",
            ApiResponse<AgentCommandEnvelope>.Success(envelope, context.TraceIdentifier));
    }

    private static async Task<IResult> PollCommandsAsync(
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

        var now = timeProvider.GetUtcNow();
        var pendingCommands = await database.IssuedAgentCommands
            .Where(command => command.AgentId == agentId &&
                command.Status == IssuedAgentCommandStatus.Pending)
            .Select(command => new AgentCommandEnvelope(
                command.CommandId,
                command.AgentId,
                command.Type,
                command.ProtocolVersion,
                command.IssuedAt,
                command.ExpiresAt,
                command.CorrelationId,
                command.Payload))
            .ToListAsync(cancellationToken);
        var commands = pendingCommands
            .Where(command => command.ExpiresAt > now)
            .OrderBy(command => command.IssuedAt)
            .Take(25)
            .ToList();
        return Results.Ok(ApiResponse<IReadOnlyList<AgentCommandEnvelope>>.Success(
            commands,
            context.TraceIdentifier));
    }

    private static async Task<IResult> AcknowledgeCommandAsync(
        Guid commandId,
        ClaimsPrincipal user,
        PlatformDbContext database,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(user.FindFirstValue("agent_id"), out var agentId))
        {
            return Results.Unauthorized();
        }

        var command = await database.IssuedAgentCommands.SingleOrDefaultAsync(
            candidate => candidate.CommandId == commandId && candidate.AgentId == agentId,
            cancellationToken);
        if (command is null)
        {
            return Results.NotFound();
        }

        command.Acknowledge(timeProvider.GetUtcNow());
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            database.ChangeTracker.Clear();
            var acknowledged = await database.IssuedAgentCommands
                .AsNoTracking()
                .AnyAsync(
                    candidate => candidate.CommandId == commandId &&
                        candidate.AgentId == agentId &&
                        candidate.Status == IssuedAgentCommandStatus.Acknowledged,
                    cancellationToken);
            if (!acknowledged)
            {
                return Results.Conflict();
            }
        }

        return Results.NoContent();
    }
}
