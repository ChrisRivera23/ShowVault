using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ShowVault.Api.Contracts;
using ShowVault.Api.Data;
using ShowVault.Api.Recovery;
using ShowVault.AgentContracts;

namespace ShowVault.Api.Endpoints;

public static class RecoveryHistoryEndpoints
{
    public static IEndpointRouteBuilder MapRecoveryHistoryEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/v1/organizations/{organizationId:guid}/venues/{venueId:guid}/recovery-runs",
                ListRecoveryRunsAsync)
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> ListRecoveryRunsAsync(
        Guid organizationId,
        Guid venueId,
        ClaimsPrincipal user,
        HttpContext context,
        PlatformDbContext database,
        CancellationToken cancellationToken)
    {
        var subject = user.FindFirstValue("sub");
        var authorized = !string.IsNullOrWhiteSpace(subject) &&
            await database.Venues
                .Where(venue => venue.Id == venueId && venue.OrganizationId == organizationId)
                .Join(
                    database.Memberships.Where(membership =>
                        membership.OrganizationId == organizationId &&
                        membership.IdentitySubject == subject),
                    venue => venue.OrganizationId,
                    membership => membership.OrganizationId,
                    (_, _) => true)
                .AnyAsync(cancellationToken);
        if (!authorized)
        {
            return Results.Forbid();
        }

        var agents = await database.VenueAgents
            .Where(agent => agent.VenueId == venueId)
            .Select(agent => new { agent.Id, agent.Name })
            .ToListAsync(cancellationToken);
        var agentIds = agents.Select(agent => agent.Id).ToList();
        var commands = await database.IssuedAgentCommands
            .Where(command => agentIds.Contains(command.AgentId) &&
                (command.Type == AgentCommandType.StartDiscovery ||
                 command.Type == AgentCommandType.CreateBackup ||
                 command.Type == AgentCommandType.VerifyBackup ||
                 command.Type == AgentCommandType.StartRestore))
            .Select(command => new RecoveryHistoryCommand(
                command.CommandId,
                command.AgentId,
                command.Type,
                command.IssuedAt,
                command.Status,
                command.AcknowledgedAt,
                command.Payload))
            .ToListAsync(cancellationToken);
        var commandIds = commands.Select(command => command.CommandId).ToList();
        var outcomes = await database.ReceivedAgentEvents
            .Where(agentEvent => commandIds.Contains(agentEvent.EventId) &&
                (agentEvent.Type == AgentEventType.JobCompleted ||
                 agentEvent.Type == AgentEventType.JobFailed))
            .Select(agentEvent => new
            {
                agentEvent.EventId,
                agentEvent.AgentId,
                agentEvent.Type,
                agentEvent.OccurredAt
            })
            .ToListAsync(cancellationToken);
        var runs = RecoveryHistoryBuilder.Build(
            agents.ToDictionary(agent => agent.Id, agent => agent.Name),
            commands,
            outcomes.ToDictionary(
                outcome => outcome.EventId,
                outcome => new RecoveryHistoryOutcome(
                    outcome.AgentId,
                    outcome.Type,
                    outcome.OccurredAt)));
        return Results.Ok(ApiResponse<IReadOnlyList<RecoveryRunSummary>>.Success(
            runs,
            context.TraceIdentifier));
    }
}
