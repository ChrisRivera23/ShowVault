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
    private sealed record CatalogCandidate(
        string PluginId,
        string ProductName,
        string CandidateType,
        string Evidence);

    private static readonly IReadOnlyDictionary<string, CatalogCandidate> DirectCatalog =
        new Dictionary<string, CatalogCandidate>(StringComparer.Ordinal)
        {
            ["macos.resolume-arena.application"] = new(
                "showvault.resolume", "Resolume Arena", "InstalledApplication",
                "Catalog standard macOS application location"),
            ["macos.resolume-arena.user-data"] = new(
                "showvault.resolume", "Resolume Arena", "UserDataRoot",
                "Catalog standard Resolume user-data location"),
            ["macos.serato-dj-pro.application"] = new(
                "showvault.serato-dj-pro", "Serato DJ Pro", "InstalledApplication",
                "Catalog standard macOS application location"),
            ["macos.serato-dj-pro.user-data"] = new(
                "showvault.serato-dj-pro", "Serato DJ Pro", "UserDataRoot",
                "Catalog standard Serato library location"),
            ["windows.resolume-arena.application"] = new(
                "showvault.resolume", "Resolume Arena", "InstalledApplication",
                "Catalog standard Windows application location"),
            ["windows.resolume-arena.user-data"] = new(
                "showvault.resolume", "Resolume Arena", "UserDataRoot",
                "Catalog standard Resolume user-data location"),
            ["windows.serato-dj-pro.application"] = new(
                "showvault.serato-dj-pro", "Serato DJ Pro", "InstalledApplication",
                "Catalog standard Windows application location"),
            ["windows.serato-dj-pro.user-data"] = new(
                "showvault.serato-dj-pro", "Serato DJ Pro", "UserDataRoot",
                "Catalog standard Serato library location")
        };

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
        endpoints.MapPost(
                "/api/v1/organizations/{organizationId:guid}/venues/{venueId:guid}/recovery-candidates/{candidateId:guid}/backup",
                BackupAsync)
            .RequireAuthorization();
        endpoints.MapPost(
                "/api/v1/organizations/{organizationId:guid}/venues/{venueId:guid}/computer-scans",
                SubmitComputerScanAsync)
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> SubmitComputerScanAsync(
        Guid organizationId,
        Guid venueId,
        SubmitComputerScanRequest request,
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

        var candidateKeys = request.CandidateKeys
            .Distinct(StringComparer.Ordinal)
            .Take(129)
            .ToArray();
        if (candidateKeys.Length > 128 || candidateKeys.Any(key => !DirectCatalog.ContainsKey(key)))
        {
            return Results.BadRequest();
        }

        var scanId = Guid.NewGuid();
        var now = timeProvider.GetUtcNow();
        database.DesktopCatalogScans.Add(DesktopCatalogScan.Complete(scanId, venueId, now));
        foreach (var candidateKey in candidateKeys)
        {
            var candidate = DirectCatalog[candidateKey];
            database.DesktopCatalogScanCandidates.Add(DesktopCatalogScanCandidate.Detected(
                scanId,
                venueId,
                candidateKey,
                candidate.PluginId,
                candidate.ProductName,
                candidate.CandidateType,
                candidate.Evidence,
                now));
        }

        await database.SaveChangesAsync(cancellationToken);
        return Results.Created(
            $"/api/v1/organizations/{organizationId}/venues/{venueId}/computer-scans/{scanId}",
            ApiResponse<SubmitComputerScanResponse>.Success(
                new SubmitComputerScanResponse(scanId, candidateKeys.Length, now),
                context.TraceIdentifier));
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
        candidate.StartValidation(command.CommandId);
        await database.SaveChangesAsync(cancellationToken);
        return Results.Accepted(
            $"/api/v1/agent-commands/{command.CommandId}",
            ApiResponse<AgentCommandEnvelope>.Success(command, context.TraceIdentifier));
    }

    private static async Task<IResult> BackupAsync(
        Guid organizationId,
        Guid venueId,
        Guid candidateId,
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
            item => item.Id == candidateId &&
                item.Decision == RecoveryCandidateDecision.Approved &&
                item.ValidationStatus == RecoveryCandidateValidationStatus.Passed &&
                item.ValidationCommandId != null &&
                database.VenueAgents.Any(agent =>
                    agent.Id == item.AgentId && agent.VenueId == venueId && agent.RevokedAt == null),
            cancellationToken);
        if (candidate is null)
        {
            return Results.BadRequest();
        }

        var command = AgentCommandEnvelope.Create(
            candidate.AgentId,
            AgentCommandType.CreateBackup,
            context.TraceIdentifier,
            JsonSerializer.Serialize(new { discoveryCommandId = candidate.ValidationCommandId!.Value }),
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
            record.Candidate.DecidedAt,
            record.Candidate.ValidationCommandId,
            record.Candidate.ValidationStatus == null
                ? null
                : record.Candidate.ValidationStatus.ToString()!.ToLowerInvariant(),
            record.Candidate.ValidationFileCount,
            record.Candidate.ValidationTruncated,
            record.Candidate.ValidationMessage,
            record.Candidate.ValidatedAt)).ToList();
        var desktopScans = await database.DesktopCatalogScans
            .Where(scan => scan.VenueId == venueId)
            .ToListAsync(cancellationToken);
        var latestScanId = desktopScans
            .OrderByDescending(scan => scan.CompletedAt)
            .Select(scan => (Guid?)scan.Id)
            .FirstOrDefault();
        if (latestScanId is { } scanId)
        {
            var desktopCandidates = await database.DesktopCatalogScanCandidates
                .Where(candidate => candidate.VenueId == venueId && candidate.ScanId == scanId)
                .OrderBy(candidate => candidate.ProductName)
                .ThenBy(candidate => candidate.CandidateType)
                .ToArrayAsync(cancellationToken);
            candidates.AddRange(desktopCandidates.Select(candidate => new RecoveryCandidateSummary(
                candidate.Id,
                Guid.Empty,
                "This computer",
                candidate.PluginId,
                candidate.ProductName,
                candidate.CandidateType,
                candidate.Evidence,
                "detected",
                candidate.DetectedAt,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                true,
                candidate.CandidateKey)));
        }
        return Results.Ok(ApiResponse<IReadOnlyList<RecoveryCandidateSummary>>.Success(
            candidates.OrderByDescending(candidate => candidate.DetectedAt).ToArray(),
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
