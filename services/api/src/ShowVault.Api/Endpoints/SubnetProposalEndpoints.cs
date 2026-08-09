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
        endpoints.MapPost(path + "/{proposalId:guid}/discover", DiscoverAsync).RequireAuthorization();
        endpoints.MapPost(path + "/{proposalId:guid}/identify-ma-lighting", IdentifyMaLightingAsync).RequireAuthorization();
        endpoints.MapPost(path + "/{proposalId:guid}/identify-yamaha-dme", IdentifyYamahaDmeAsync).RequireAuthorization();
        endpoints.MapPost(path + "/{proposalId:guid}/identify-grandma2", IdentifyGrandMa2Async).RequireAuthorization();
        endpoints.MapPost(path + "/{proposalId:guid}/identify-projectors", IdentifyProjectorsAsync).RequireAuthorization();
        endpoints.MapPost(path + "/{proposalId:guid}/identify-blackmagic-videohub",
            IdentifyBlackmagicVideohubAsync).RequireAuthorization();
        endpoints.MapPost(path + "/{proposalId:guid}/identify-newtek-tricaster",
            IdentifyNewTekTriCasterAsync).RequireAuthorization();
        endpoints.MapPost(path + "/{proposalId:guid}/identify-birddog",
            IdentifyBirdDogAsync).RequireAuthorization();
        endpoints.MapPost(path + "/{proposalId:guid}/identify-panasonic-camera",
            IdentifyPanasonicCameraAsync).RequireAuthorization();
        endpoints.MapPost(path + "/{proposalId:guid}/identify-sony-camera",
            IdentifySonyCameraAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> IdentifySonyCameraAsync(
        Guid organizationId, Guid venueId, Guid proposalId,
        IdentifySonyCameraRequest request, ClaimsPrincipal user, HttpContext context,
        PlatformDbContext db, TimeProvider time, CancellationToken ct)
    {
        var subject = user.FindFirstValue("sub");
        if (!await HasAccess(db, organizationId, venueId, subject, true, ct)) return Results.Forbid();
        if (request.TimeoutMilliseconds is < 100 or > 500) return Results.BadRequest();
        var proposal = await db.SubnetProposals.SingleOrDefaultAsync(p => p.Id == proposalId &&
            p.Decision == SubnetProposalDecision.Approved &&
            p.DiscoveryStatus == SubnetDiscoveryStatus.Completed && p.RespondingHostCount > 0 &&
            db.VenueAgents.Any(a => a.Id == p.AgentId && a.VenueId == venueId && a.RevokedAt == null), ct);
        if (proposal?.DiscoveryCommandId is not Guid discoveryCommandId) return Results.BadRequest();
        var command = AgentCommandEnvelope.Create(
            proposal.AgentId, AgentCommandType.IdentifySonyCamera,
            context.TraceIdentifier, JsonSerializer.Serialize(new IdentifySonyCameraPayload(
                proposal.Id, discoveryCommandId, request.TimeoutMilliseconds)),
            time.GetUtcNow(), TimeSpan.FromMinutes(10));
        db.IssuedAgentCommands.Add(IssuedAgentCommand.FromEnvelope(command));
        proposal.StartSonyCameraIdentification(command.CommandId);
        await db.SaveChangesAsync(ct);
        return Results.Accepted($"/api/v1/agent-commands/{command.CommandId}",
            ApiResponse<AgentCommandEnvelope>.Success(command, context.TraceIdentifier));
    }

    private static async Task<IResult> IdentifyPanasonicCameraAsync(
        Guid organizationId, Guid venueId, Guid proposalId,
        IdentifyPanasonicCameraRequest request, ClaimsPrincipal user, HttpContext context,
        PlatformDbContext db, TimeProvider time, CancellationToken ct)
    {
        var subject = user.FindFirstValue("sub");
        if (!await HasAccess(db, organizationId, venueId, subject, true, ct)) return Results.Forbid();
        if (request.TimeoutMilliseconds is < 100 or > 500) return Results.BadRequest();
        var proposal = await db.SubnetProposals.SingleOrDefaultAsync(p => p.Id == proposalId &&
            p.Decision == SubnetProposalDecision.Approved &&
            p.DiscoveryStatus == SubnetDiscoveryStatus.Completed && p.RespondingHostCount > 0 &&
            db.VenueAgents.Any(a => a.Id == p.AgentId && a.VenueId == venueId && a.RevokedAt == null), ct);
        if (proposal?.DiscoveryCommandId is not Guid discoveryCommandId) return Results.BadRequest();
        var command = AgentCommandEnvelope.Create(
            proposal.AgentId, AgentCommandType.IdentifyPanasonicCamera,
            context.TraceIdentifier, JsonSerializer.Serialize(new IdentifyPanasonicCameraPayload(
                proposal.Id, discoveryCommandId, request.TimeoutMilliseconds)),
            time.GetUtcNow(), TimeSpan.FromMinutes(10));
        db.IssuedAgentCommands.Add(IssuedAgentCommand.FromEnvelope(command));
        proposal.StartPanasonicCameraIdentification(command.CommandId);
        await db.SaveChangesAsync(ct);
        return Results.Accepted($"/api/v1/agent-commands/{command.CommandId}",
            ApiResponse<AgentCommandEnvelope>.Success(command, context.TraceIdentifier));
    }

    private static async Task<IResult> IdentifyBirdDogAsync(
        Guid organizationId, Guid venueId, Guid proposalId,
        IdentifyBirdDogRequest request, ClaimsPrincipal user, HttpContext context,
        PlatformDbContext db, TimeProvider time, CancellationToken ct)
    {
        var subject = user.FindFirstValue("sub");
        if (!await HasAccess(db, organizationId, venueId, subject, true, ct)) return Results.Forbid();
        if (request.TimeoutMilliseconds is < 100 or > 500) return Results.BadRequest();
        var proposal = await db.SubnetProposals.SingleOrDefaultAsync(p => p.Id == proposalId &&
            p.Decision == SubnetProposalDecision.Approved &&
            p.DiscoveryStatus == SubnetDiscoveryStatus.Completed && p.RespondingHostCount > 0 &&
            db.VenueAgents.Any(a => a.Id == p.AgentId && a.VenueId == venueId && a.RevokedAt == null), ct);
        if (proposal?.DiscoveryCommandId is not Guid discoveryCommandId) return Results.BadRequest();
        var command = AgentCommandEnvelope.Create(
            proposal.AgentId, AgentCommandType.IdentifyBirdDog,
            context.TraceIdentifier, JsonSerializer.Serialize(new IdentifyBirdDogPayload(
                proposal.Id, discoveryCommandId, request.TimeoutMilliseconds)),
            time.GetUtcNow(), TimeSpan.FromMinutes(10));
        db.IssuedAgentCommands.Add(IssuedAgentCommand.FromEnvelope(command));
        proposal.StartBirdDogIdentification(command.CommandId);
        await db.SaveChangesAsync(ct);
        return Results.Accepted($"/api/v1/agent-commands/{command.CommandId}",
            ApiResponse<AgentCommandEnvelope>.Success(command, context.TraceIdentifier));
    }

    private static async Task<IResult> IdentifyNewTekTriCasterAsync(
        Guid organizationId, Guid venueId, Guid proposalId,
        IdentifyNewTekTriCasterRequest request, ClaimsPrincipal user, HttpContext context,
        PlatformDbContext db, TimeProvider time, CancellationToken ct)
    {
        var subject = user.FindFirstValue("sub");
        if (!await HasAccess(db, organizationId, venueId, subject, true, ct)) return Results.Forbid();
        if (request.TimeoutMilliseconds is < 100 or > 500) return Results.BadRequest();
        var proposal = await db.SubnetProposals.SingleOrDefaultAsync(p => p.Id == proposalId &&
            p.Decision == SubnetProposalDecision.Approved &&
            p.DiscoveryStatus == SubnetDiscoveryStatus.Completed && p.RespondingHostCount > 0 &&
            db.VenueAgents.Any(a => a.Id == p.AgentId && a.VenueId == venueId && a.RevokedAt == null), ct);
        if (proposal?.DiscoveryCommandId is not Guid discoveryCommandId) return Results.BadRequest();
        var command = AgentCommandEnvelope.Create(
            proposal.AgentId, AgentCommandType.IdentifyNewTekTriCaster,
            context.TraceIdentifier, JsonSerializer.Serialize(new IdentifyNewTekTriCasterPayload(
                proposal.Id, discoveryCommandId, request.TimeoutMilliseconds)),
            time.GetUtcNow(), TimeSpan.FromMinutes(10));
        db.IssuedAgentCommands.Add(IssuedAgentCommand.FromEnvelope(command));
        proposal.StartNewTekTriCasterIdentification(command.CommandId);
        await db.SaveChangesAsync(ct);
        return Results.Accepted($"/api/v1/agent-commands/{command.CommandId}",
            ApiResponse<AgentCommandEnvelope>.Success(command, context.TraceIdentifier));
    }

    private static async Task<IResult> IdentifyBlackmagicVideohubAsync(
        Guid organizationId, Guid venueId, Guid proposalId,
        IdentifyBlackmagicVideohubRequest request, ClaimsPrincipal user, HttpContext context,
        PlatformDbContext db, TimeProvider time, CancellationToken ct)
    {
        var subject = user.FindFirstValue("sub");
        if (!await HasAccess(db, organizationId, venueId, subject, true, ct)) return Results.Forbid();
        if (request.TimeoutMilliseconds is < 100 or > 500) return Results.BadRequest();
        var proposal = await db.SubnetProposals.SingleOrDefaultAsync(p => p.Id == proposalId &&
            p.Decision == SubnetProposalDecision.Approved &&
            p.DiscoveryStatus == SubnetDiscoveryStatus.Completed && p.RespondingHostCount > 0 &&
            db.VenueAgents.Any(a => a.Id == p.AgentId && a.VenueId == venueId && a.RevokedAt == null), ct);
        if (proposal?.DiscoveryCommandId is not Guid discoveryCommandId) return Results.BadRequest();
        var command = AgentCommandEnvelope.Create(
            proposal.AgentId, AgentCommandType.IdentifyBlackmagicVideohub,
            context.TraceIdentifier, JsonSerializer.Serialize(new IdentifyBlackmagicVideohubPayload(
                proposal.Id, discoveryCommandId, request.TimeoutMilliseconds)),
            time.GetUtcNow(), TimeSpan.FromMinutes(10));
        db.IssuedAgentCommands.Add(IssuedAgentCommand.FromEnvelope(command));
        proposal.StartBlackmagicVideohubIdentification(command.CommandId);
        await db.SaveChangesAsync(ct);
        return Results.Accepted($"/api/v1/agent-commands/{command.CommandId}",
            ApiResponse<AgentCommandEnvelope>.Success(command, context.TraceIdentifier));
    }

    private static async Task<IResult> IdentifyProjectorsAsync(Guid organizationId, Guid venueId, Guid proposalId,
        IdentifyProjectorsRequest request, ClaimsPrincipal user, HttpContext context,
        PlatformDbContext db, TimeProvider time, CancellationToken ct)
    {
        var subject = user.FindFirstValue("sub");
        if (!await HasAccess(db, organizationId, venueId, subject, true, ct)) return Results.Forbid();
        if (request.TimeoutMilliseconds is < 100 or > 500) return Results.BadRequest();
        var proposal = await db.SubnetProposals.SingleOrDefaultAsync(p => p.Id == proposalId &&
            p.Decision == SubnetProposalDecision.Approved &&
            p.DiscoveryStatus == SubnetDiscoveryStatus.Completed && p.RespondingHostCount > 0 &&
            db.VenueAgents.Any(a => a.Id == p.AgentId && a.VenueId == venueId && a.RevokedAt == null), ct);
        if (proposal?.DiscoveryCommandId is not Guid discoveryCommandId) return Results.BadRequest();
        var command = AgentCommandEnvelope.Create(proposal.AgentId, AgentCommandType.IdentifyProjectors,
            context.TraceIdentifier, JsonSerializer.Serialize(new IdentifyProjectorsPayload(
                proposal.Id, discoveryCommandId, request.TimeoutMilliseconds)),
            time.GetUtcNow(), TimeSpan.FromMinutes(10));
        db.IssuedAgentCommands.Add(IssuedAgentCommand.FromEnvelope(command));
        await db.SaveChangesAsync(ct);
        return Results.Accepted($"/api/v1/agent-commands/{command.CommandId}",
            ApiResponse<AgentCommandEnvelope>.Success(command, context.TraceIdentifier));
    }

    private static async Task<IResult> IdentifyGrandMa2Async(Guid organizationId, Guid venueId, Guid proposalId,
        IdentifyGrandMa2Request request, ClaimsPrincipal user, HttpContext context,
        PlatformDbContext db, TimeProvider time, CancellationToken ct)
    {
        var subject = user.FindFirstValue("sub");
        if (!await HasAccess(db, organizationId, venueId, subject, true, ct)) return Results.Forbid();
        if (request.TimeoutMilliseconds is < 100 or > 500) return Results.BadRequest();
        var proposal = await db.SubnetProposals.SingleOrDefaultAsync(p => p.Id == proposalId &&
            p.Decision == SubnetProposalDecision.Approved &&
            p.DiscoveryStatus == SubnetDiscoveryStatus.Completed && p.RespondingHostCount > 0 &&
            db.VenueAgents.Any(a => a.Id == p.AgentId && a.VenueId == venueId && a.RevokedAt == null), ct);
        if (proposal?.DiscoveryCommandId is not Guid discoveryCommandId) return Results.BadRequest();
        var command = AgentCommandEnvelope.Create(proposal.AgentId, AgentCommandType.IdentifyGrandMa2,
            context.TraceIdentifier, JsonSerializer.Serialize(new IdentifyGrandMa2Payload(
                proposal.Id, discoveryCommandId, request.TimeoutMilliseconds)),
            time.GetUtcNow(), TimeSpan.FromMinutes(10));
        db.IssuedAgentCommands.Add(IssuedAgentCommand.FromEnvelope(command));
        proposal.StartGrandMa2Identification(command.CommandId);
        await db.SaveChangesAsync(ct);
        return Results.Accepted($"/api/v1/agent-commands/{command.CommandId}",
            ApiResponse<AgentCommandEnvelope>.Success(command, context.TraceIdentifier));
    }

    private static async Task<IResult> IdentifyYamahaDmeAsync(Guid organizationId, Guid venueId, Guid proposalId,
        IdentifyYamahaDmeRequest request, ClaimsPrincipal user, HttpContext context,
        PlatformDbContext db, TimeProvider time, CancellationToken ct)
    {
        var subject = user.FindFirstValue("sub");
        if (!await HasAccess(db, organizationId, venueId, subject, true, ct)) return Results.Forbid();
        if (request.TimeoutMilliseconds is < 100 or > 500) return Results.BadRequest();
        var proposal = await db.SubnetProposals.SingleOrDefaultAsync(p => p.Id == proposalId &&
            p.Decision == SubnetProposalDecision.Approved &&
            p.DiscoveryStatus == SubnetDiscoveryStatus.Completed && p.RespondingHostCount > 0 &&
            db.VenueAgents.Any(a => a.Id == p.AgentId && a.VenueId == venueId && a.RevokedAt == null), ct);
        if (proposal?.DiscoveryCommandId is not Guid discoveryCommandId) return Results.BadRequest();
        var command = AgentCommandEnvelope.Create(proposal.AgentId, AgentCommandType.IdentifyYamahaDme,
            context.TraceIdentifier, JsonSerializer.Serialize(new IdentifyYamahaDmePayload(
                proposal.Id, discoveryCommandId, request.TimeoutMilliseconds)),
            time.GetUtcNow(), TimeSpan.FromMinutes(10));
        db.IssuedAgentCommands.Add(IssuedAgentCommand.FromEnvelope(command));
        proposal.StartYamahaIdentification(command.CommandId);
        await db.SaveChangesAsync(ct);
        return Results.Accepted($"/api/v1/agent-commands/{command.CommandId}",
            ApiResponse<AgentCommandEnvelope>.Success(command, context.TraceIdentifier));
    }

    private static async Task<IResult> IdentifyMaLightingAsync(Guid organizationId, Guid venueId, Guid proposalId,
        IdentifyMaLightingRequest request, ClaimsPrincipal user, HttpContext context,
        PlatformDbContext db, TimeProvider time, CancellationToken ct)
    {
        var subject = user.FindFirstValue("sub");
        if (!await HasAccess(db, organizationId, venueId, subject, true, ct)) return Results.Forbid();
        if (request.TimeoutMilliseconds is < 100 or > 500) return Results.BadRequest();
        var proposal = await db.SubnetProposals.SingleOrDefaultAsync(p => p.Id == proposalId &&
            p.Decision == SubnetProposalDecision.Approved &&
            p.DiscoveryStatus == SubnetDiscoveryStatus.Completed && p.RespondingHostCount > 0 &&
            db.VenueAgents.Any(a => a.Id == p.AgentId && a.VenueId == venueId && a.RevokedAt == null), ct);
        if (proposal?.DiscoveryCommandId is not Guid discoveryCommandId) return Results.BadRequest();
        var command = AgentCommandEnvelope.Create(proposal.AgentId, AgentCommandType.IdentifyMaLighting,
            context.TraceIdentifier, JsonSerializer.Serialize(new IdentifyMaLightingPayload(
                proposal.Id, discoveryCommandId, request.TimeoutMilliseconds)),
            time.GetUtcNow(), TimeSpan.FromMinutes(10));
        db.IssuedAgentCommands.Add(IssuedAgentCommand.FromEnvelope(command));
        proposal.StartIdentification(command.CommandId);
        await db.SaveChangesAsync(ct);
        return Results.Accepted($"/api/v1/agent-commands/{command.CommandId}",
            ApiResponse<AgentCommandEnvelope>.Success(command, context.TraceIdentifier));
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
                x.p.DetectedAt, x.p.DecidedAt, x.p.DiscoveryCommandId,
                x.p.DiscoveryStatus?.ToString().ToLowerInvariant(), x.p.AttemptedHostCount,
                x.p.RespondingHostCount, x.p.PassiveCandidateCount, x.p.FallbackTargetCount,
                x.p.DiscoveryMessage, x.p.DiscoveredAt,
                x.p.IdentificationCommandId,
                x.p.IdentificationStatus?.ToString().ToLowerInvariant(),
                x.p.IdentificationAttemptedHostCount, x.p.IdentifiedHostCount,
                x.p.IdentifiedProductFamilies, x.p.IdentificationMessage, x.p.IdentifiedAt,
                x.p.YamahaIdentificationCommandId,
                x.p.YamahaIdentificationStatus?.ToString().ToLowerInvariant(),
                x.p.YamahaIdentificationAttemptedHostCount, x.p.YamahaIdentifiedHostCount,
                x.p.YamahaIdentifiedProductFamilies, x.p.YamahaIdentificationMessage,
                x.p.YamahaIdentifiedAt,
                x.p.GrandMa2IdentificationCommandId,
                x.p.GrandMa2IdentificationStatus?.ToString().ToLowerInvariant(),
                x.p.GrandMa2IdentificationAttemptedHostCount, x.p.GrandMa2IdentifiedHostCount,
                x.p.GrandMa2IdentifiedProductFamilies, x.p.GrandMa2IdentificationMessage,
                x.p.GrandMa2IdentifiedAt,
                x.p.BlackmagicVideohubIdentificationCommandId,
                x.p.BlackmagicVideohubIdentificationStatus?.ToString().ToLowerInvariant(),
                x.p.BlackmagicVideohubIdentificationAttemptedHostCount,
                x.p.BlackmagicVideohubIdentifiedHostCount,
                x.p.BlackmagicVideohubIdentifiedProductFamilies,
                x.p.BlackmagicVideohubIdentificationMessage,
                x.p.BlackmagicVideohubIdentifiedAt,
                x.p.NewTekTriCasterIdentificationCommandId,
                x.p.NewTekTriCasterIdentificationStatus?.ToString().ToLowerInvariant(),
                x.p.NewTekTriCasterIdentificationAttemptedHostCount,
                x.p.NewTekTriCasterIdentifiedHostCount,
                x.p.NewTekTriCasterIdentifiedProductFamilies,
                x.p.NewTekTriCasterIdentificationMessage,
                x.p.NewTekTriCasterIdentifiedAt,
                x.p.BirdDogIdentificationCommandId,
                x.p.BirdDogIdentificationStatus?.ToString().ToLowerInvariant(),
                x.p.BirdDogIdentificationAttemptedHostCount,
                x.p.BirdDogIdentifiedHostCount,
                x.p.BirdDogIdentifiedProductFamilies,
                x.p.BirdDogIdentificationMessage,
                x.p.BirdDogIdentifiedAt,
                x.p.PanasonicCameraIdentificationCommandId,
                x.p.PanasonicCameraIdentificationStatus?.ToString().ToLowerInvariant(),
                x.p.PanasonicCameraIdentificationAttemptedHostCount,
                x.p.PanasonicCameraIdentifiedHostCount,
                x.p.PanasonicCameraIdentifiedProductFamilies,
                x.p.PanasonicCameraIdentificationMessage,
                x.p.PanasonicCameraIdentifiedAt,
                x.p.SonyCameraIdentificationCommandId,
                x.p.SonyCameraIdentificationStatus?.ToString().ToLowerInvariant(),
                x.p.SonyCameraIdentificationAttemptedHostCount,
                x.p.SonyCameraIdentifiedHostCount,
                x.p.SonyCameraIdentifiedProductFamilies,
                x.p.SonyCameraIdentificationMessage,
                x.p.SonyCameraIdentifiedAt)).ToArray(), context.TraceIdentifier));
    }

    private static async Task<IResult> DiscoverAsync(Guid organizationId, Guid venueId, Guid proposalId,
        DiscoverSubnetRequest request, ClaimsPrincipal user, HttpContext context,
        PlatformDbContext db, TimeProvider time, CancellationToken ct)
    {
        var subject = user.FindFirstValue("sub");
        if (!await HasAccess(db, organizationId, venueId, subject, true, ct)) return Results.Forbid();
        if (request.MaxHosts is < 1 or > 32 || request.TimeoutMilliseconds is < 100 or > 500)
            return Results.BadRequest();
        var proposal = await db.SubnetProposals.SingleOrDefaultAsync(p => p.Id == proposalId &&
            p.Decision == SubnetProposalDecision.Approved && db.VenueAgents.Any(a =>
                a.Id == p.AgentId && a.VenueId == venueId && a.RevokedAt == null), ct);
        if (proposal is null) return Results.BadRequest();
        var command = AgentCommandEnvelope.Create(proposal.AgentId, AgentCommandType.DiscoverApprovedSubnet,
            context.TraceIdentifier, JsonSerializer.Serialize(new DiscoverApprovedSubnetPayload(
                proposal.Id, request.MaxHosts, request.TimeoutMilliseconds)), time.GetUtcNow(), TimeSpan.FromMinutes(10));
        db.IssuedAgentCommands.Add(IssuedAgentCommand.FromEnvelope(command));
        proposal.StartDiscovery(command.CommandId);
        await db.SaveChangesAsync(ct);
        return Results.Accepted($"/api/v1/agent-commands/{command.CommandId}",
            ApiResponse<AgentCommandEnvelope>.Success(command, context.TraceIdentifier));
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
