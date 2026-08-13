using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ShowVault.Api.Authorization;
using ShowVault.Api.Contracts;
using ShowVault.Api.Data;
using ShowVault.Api.Security;
using ShowVault.AgentContracts;
using ShowVault.Platform.Agents;

namespace ShowVault.Api.Endpoints;

public static class AgentCommunicationEndpoints
{
    private const int MaxCommandsPerPoll = 25;
    private const int MaxCommandCandidatesPerPoll = 50;

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
        MembershipAuthorizationService authorization,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var authorized = await authorization.CanManageAgentAsync(
            organizationId, venueId, agentId, user, cancellationToken);
        if (!authorized)
        {
            return Results.Forbid();
        }

        if (request.ValidForSeconds is < 1 or > 3600)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.ValidForSeconds)] =
                    ["Command validity must be between 1 and 3600 seconds."]
            });
        }

        var issuedAt = timeProvider.GetUtcNow();
        var envelope = new AgentCommandEnvelope(
            Guid.NewGuid(),
            agentId,
            request.Type,
            AgentProtocol.Version,
            issuedAt,
            issuedAt.AddSeconds(request.ValidForSeconds),
            context.TraceIdentifier,
            request.Payload);
        if (!AgentCommandValidation.TryValidate(envelope, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["command"] = [validationError]
            });
        }

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
        var candidates = await database.IssuedAgentCommands
            .Where(command => command.AgentId == agentId &&
                command.Status == IssuedAgentCommandStatus.Pending)
            .OrderBy(command => command.CommandId)
            .Take(MaxCommandCandidatesPerPoll)
            .ToListAsync(cancellationToken);
        var expiredCommands = candidates
            .Where(command => command.ExpiresAt <= now)
            .ToList();
        foreach (var expiredCommand in expiredCommands)
        {
            expiredCommand.Expire();
        }

        if (expiredCommands.Count > 0)
        {
            try
            {
                await database.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                database.ChangeTracker.Clear();
            }
        }

        var commands = candidates
            .Where(command => command.Status == IssuedAgentCommandStatus.Pending &&
                command.ExpiresAt > now)
            .OrderBy(command => command.IssuedAt)
            .Take(MaxCommandsPerPoll)
            .Select(command => new AgentCommandEnvelope(
                command.CommandId,
                command.AgentId,
                command.Type,
                command.ProtocolVersion,
                command.IssuedAt,
                command.ExpiresAt,
                command.CorrelationId,
                command.Payload))
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

        if (!command.Acknowledge(timeProvider.GetUtcNow()))
        {
            return Results.Conflict();
        }
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
