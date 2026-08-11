using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ShowVault.Api.Data;
using ShowVault.Api.Security;
using ShowVault.AgentContracts;
using ShowVault.Platform.Agents;

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
}
