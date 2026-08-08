using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShowVault.AgentContracts;
using ShowVault.Api.Contracts;
using ShowVault.Api.Data;
using ShowVault.Platform.Agents;
using ShowVault.Platform.Organizations;

namespace ShowVault.Api.Endpoints;

public static class SubnetProposalEndpoints
{
    public static IEndpointRouteBuilder MapSubnetProposalEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var path = "/api/v1/organizations/{organizationId:guid}/venues/{venueId:guid}/subnet-proposals";
        endpoints.MapGet(path, ListAsync).RequireAuthorization();
        endpoints.MapPut(path + "/{proposalId:guid}/decision", DecideAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> ListAsync(Guid organizationId, Guid venueId,
        ClaimsPrincipal user, HttpContext context, PlatformDbContext db, CancellationToken ct)
    {
        if (!await HasAccess(db, organizationId, venueId, user.FindFirstValue("sub"), false, ct)) return Results.Forbid();
        var rows = await db.SubnetProposals.Where(p => db.VenueAgents.Any(a =>
                a.Id == p.AgentId && a.VenueId == venueId && a.RevokedAt == null))
            .Join(db.VenueAgents, p => p.AgentId, a => a.Id, (p, a) => new { p, a.Name })
            .ToListAsync(ct);
        return Results.Ok(ApiResponse<IReadOnlyList<SubnetProposalSummary>>.Success(rows
            .OrderByDescending(x => x.p.DetectedAt).Select(x =>
            new SubnetProposalSummary(x.p.Id, x.p.AgentId, x.Name, x.p.Network, x.p.PrefixLength,
                x.p.InterfaceType, x.p.Evidence, x.p.Decision.ToString().ToLowerInvariant(),
                x.p.DetectedAt, x.p.DecidedAt)).ToArray(), context.TraceIdentifier));
    }

    private static async Task<IResult> DecideAsync(Guid organizationId, Guid venueId, Guid proposalId,
        DecideSubnetProposalRequest request, ClaimsPrincipal user, HttpContext context,
        PlatformDbContext db, TimeProvider time, CancellationToken ct)
    {
        var subject = user.FindFirstValue("sub");
        if (!await HasAccess(db, organizationId, venueId, subject, true, ct)) return Results.Forbid();
        var proposal = await db.SubnetProposals.SingleOrDefaultAsync(p => p.Id == proposalId &&
            db.VenueAgents.Any(a => a.Id == p.AgentId && a.VenueId == venueId && a.RevokedAt == null), ct);
        if (proposal is null) return Results.NotFound();
        proposal.RecordDecision(request.Approved ? SubnetProposalDecision.Approved : SubnetProposalDecision.Rejected,
            subject!, time.GetUtcNow());
        var command = AgentCommandEnvelope.Create(proposal.AgentId, AgentCommandType.ApplySubnetProposalDecision,
            context.TraceIdentifier, JsonSerializer.Serialize(new ApplySubnetProposalDecisionPayload(proposal.Id, request.Approved)),
            time.GetUtcNow(), TimeSpan.FromHours(1));
        db.IssuedAgentCommands.Add(IssuedAgentCommand.FromEnvelope(command));
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static Task<bool> HasAccess(PlatformDbContext db, Guid organizationId, Guid venueId,
        string? subject, bool manager, CancellationToken ct) => string.IsNullOrWhiteSpace(subject)
        ? Task.FromResult(false)
        : db.Venues.Where(v => v.Id == venueId && v.OrganizationId == organizationId)
            .Join(db.Memberships.Where(m => m.IdentitySubject == subject && (!manager ||
                m.Role == OrganizationRole.Manager || m.Role == OrganizationRole.Administrator ||
                m.Role == OrganizationRole.Owner)), v => v.OrganizationId, m => m.OrganizationId, (_, _) => true)
            .AnyAsync(ct);
}
