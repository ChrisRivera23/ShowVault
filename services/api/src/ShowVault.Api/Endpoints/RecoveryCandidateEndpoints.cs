using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ShowVault.Api.Contracts;
using ShowVault.Api.Data;
using ShowVault.AgentContracts;
using ShowVault.Platform.Agents;

namespace ShowVault.Api.Endpoints;

public static class RecoveryCandidateEndpoints
{
    public static IEndpointRouteBuilder MapRecoveryCandidateEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/v1/organizations/{organizationId:guid}/venues/{venueId:guid}/recovery-candidates",
                ListAsync)
            .RequireAuthorization();
        endpoints.MapPut(
                "/api/v1/organizations/{organizationId:guid}/venues/{venueId:guid}/recovery-candidates/{candidateId:guid}/decision",
                DecideAsync)
            .RequireAuthorization();
        endpoints.MapPost(
                "/api/v1/organizations/{organizationId:guid}/venues/{venueId:guid}/recovery-candidates/{candidateId:guid}/validate",
                ValidateAsync)
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> ValidateAsync(
        Guid organizationId,
        Guid venueId,
        Guid candidateId,
        ValidateRecoveryCandidateRequest request,
        ClaimsPrincipal user,
        HttpContext context,
        PlatformDbContext database,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var subject = user.FindFirstValue("sub");
        if (!await HasVenueAccessAsync(database, organizationId, venueId, subject, true, cancellationToken))
        {
            return Results.Forbid();
        }

        if (request.MaxFiles is < 1 or > 100_000)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.MaxFiles)] = ["File limit must be between 1 and 100000."]
            });
        }

        var candidate = await database.RecoveryCandidates.SingleOrDefaultAsync(
            item => item.Id == candidateId &&
                item.Decision == RecoveryCandidateDecision.Approved &&
                item.CandidateType == "UserDataRoot" &&
                database.VenueAgents.Any(agent =>
                    agent.Id == item.AgentId && agent.VenueId == venueId && agent.RevokedAt == null),
            cancellationToken);
        if (candidate is null)
        {
            return Results.BadRequest();
        }

        var command = AgentCommandEnvelope.Create(
            candidate.AgentId,
            AgentCommandType.ValidateRecoveryCandidate,
            context.TraceIdentifier,
            JsonSerializer.Serialize(new ValidateRecoveryCandidatePayload(candidate.Id, request.MaxFiles)),
            timeProvider.GetUtcNow(),
            TimeSpan.FromHours(1));
        database.IssuedAgentCommands.Add(IssuedAgentCommand.FromEnvelope(command));
        await database.SaveChangesAsync(cancellationToken);
        return Results.Accepted(
            $"/api/v1/agent-commands/{command.CommandId}",
            ApiResponse<AgentCommandEnvelope>.Success(command, context.TraceIdentifier));
    }

    private static async Task<IResult> ListAsync(
        Guid organizationId,
        Guid venueId,
        ClaimsPrincipal user,
        HttpContext context,
        PlatformDbContext database,
        CancellationToken cancellationToken)
    {
        var subject = user.FindFirstValue("sub");
        if (!await HasVenueAccessAsync(database, organizationId, venueId, subject, false, cancellationToken))
        {
            return Results.Forbid();
        }

        var records = await database.RecoveryCandidates
            .Where(candidate => database.VenueAgents.Any(agent =>
                agent.Id == candidate.AgentId && agent.VenueId == venueId && agent.RevokedAt == null))
            .Join(
                database.VenueAgents,
                candidate => candidate.AgentId,
                agent => agent.Id,
                (candidate, agent) => new { Candidate = candidate, AgentName = agent.Name })
            .ToListAsync(cancellationToken);
        var candidates = records.OrderByDescending(record => record.Candidate.DetectedAt)
            .Select(record => new RecoveryCandidateSummary(
            record.Candidate.Id,
            record.Candidate.AgentId,
            record.AgentName,
            record.Candidate.PluginId,
            record.Candidate.ProductName,
            record.Candidate.CandidateType,
            record.Candidate.Evidence,
            record.Candidate.Decision.ToString().ToLowerInvariant(),
            record.Candidate.DetectedAt,
            record.Candidate.DecidedAt)).ToArray();
        return Results.Ok(ApiResponse<IReadOnlyList<RecoveryCandidateSummary>>.Success(
            candidates,
            context.TraceIdentifier));
    }

    private static async Task<IResult> DecideAsync(
        Guid organizationId,
        Guid venueId,
        Guid candidateId,
        DecideRecoveryCandidateRequest request,
        ClaimsPrincipal user,
        HttpContext context,
        PlatformDbContext database,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var subject = user.FindFirstValue("sub");
        if (!await HasVenueAccessAsync(database, organizationId, venueId, subject, true, cancellationToken))
        {
            return Results.Forbid();
        }

        var candidate = await database.RecoveryCandidates.SingleOrDefaultAsync(
            item => item.Id == candidateId && database.VenueAgents.Any(agent =>
                agent.Id == item.AgentId && agent.VenueId == venueId && agent.RevokedAt == null),
            cancellationToken);
        if (candidate is null)
        {
            return Results.NotFound();
        }

        candidate.RecordDecision(
            request.Approved ? RecoveryCandidateDecision.Approved : RecoveryCandidateDecision.Rejected,
            subject!,
            timeProvider.GetUtcNow());
        var command = AgentCommandEnvelope.Create(
            candidate.AgentId,
            AgentCommandType.ApplyRecoveryCandidateDecision,
            context.TraceIdentifier,
            JsonSerializer.Serialize(new ApplyRecoveryCandidateDecisionPayload(
                candidate.Id,
                request.Approved)),
            timeProvider.GetUtcNow(),
            TimeSpan.FromHours(1));
        database.IssuedAgentCommands.Add(IssuedAgentCommand.FromEnvelope(command));
        await database.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static Task<bool> HasVenueAccessAsync(
        PlatformDbContext database,
        Guid organizationId,
        Guid venueId,
        string? subject,
        bool requireManager,
        CancellationToken cancellationToken) =>
        !string.IsNullOrWhiteSpace(subject)
            ? database.Venues
                .Where(venue => venue.Id == venueId && venue.OrganizationId == organizationId)
                .Join(
                    database.Memberships.Where(membership =>
                        membership.IdentitySubject == subject &&
                        (!requireManager ||
                         membership.Role == ShowVault.Platform.Organizations.OrganizationRole.Manager ||
                         membership.Role == ShowVault.Platform.Organizations.OrganizationRole.Administrator ||
                         membership.Role == ShowVault.Platform.Organizations.OrganizationRole.Owner)),
                    venue => venue.OrganizationId,
                    membership => membership.OrganizationId,
                    (_, _) => true)
                .AnyAsync(cancellationToken)
            : Task.FromResult(false);
}
