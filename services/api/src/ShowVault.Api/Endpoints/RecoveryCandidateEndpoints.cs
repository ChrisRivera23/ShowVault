using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ShowVault.Api.Contracts;
using ShowVault.Api.Data;
using ShowVault.Platform.Agents;

namespace ShowVault.Api.Endpoints;

public static class RecoveryCandidateEndpoints
{
    private sealed record CatalogCandidate(string ProductName, string CandidateType, string Evidence);

    private static readonly IReadOnlyDictionary<string, CatalogCandidate> DirectCatalog =
        new Dictionary<string, CatalogCandidate>(StringComparer.Ordinal)
        {
            ["macos.resolume-arena.application"] = new("Resolume Arena", "InstalledApplication", "Catalog standard macOS application location"),
            ["macos.resolume-arena.user-data"] = new("Resolume Arena", "UserDataRoot", "Catalog standard Resolume user-data location"),
            ["macos.serato-dj-pro.application"] = new("Serato DJ Pro", "InstalledApplication", "Catalog standard macOS application location"),
            ["macos.serato-dj-pro.user-data"] = new("Serato DJ Pro", "UserDataRoot", "Catalog standard Serato library location"),
            ["windows.resolume-arena.application"] = new("Resolume Arena", "InstalledApplication", "Catalog standard Windows application location"),
            ["windows.resolume-arena.user-data"] = new("Resolume Arena", "UserDataRoot", "Catalog standard Resolume user-data location"),
            ["windows.serato-dj-pro.application"] = new("Serato DJ Pro", "InstalledApplication", "Catalog standard Windows application location"),
            ["windows.serato-dj-pro.user-data"] = new("Serato DJ Pro", "UserDataRoot", "Catalog standard Serato library location")
        };

    public static IEndpointRouteBuilder MapRecoveryCandidateEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var venues = endpoints.MapGroup(
            "/api/v1/organizations/{organizationId:guid}/venues/{venueId:guid}")
            .RequireAuthorization();
        venues.MapPost("/computer-scans", SubmitComputerScanAsync);
        venues.MapGet("/recovery-candidates", ListAsync);
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
        if (!await HasVenueAccessAsync(database, organizationId, venueId, user, true, cancellationToken))
        {
            return Results.Forbid();
        }

        if (request.CandidateKeys is null || request.CandidateKeys.Count > 128 ||
            request.CandidateKeys.Any(key => string.IsNullOrWhiteSpace(key) ||
                key.Length > 120 || !DirectCatalog.ContainsKey(key)))
        {
            return Results.BadRequest();
        }

        var keys = request.CandidateKeys.Distinct(StringComparer.Ordinal).ToArray();
        var now = timeProvider.GetUtcNow();
        var scan = DesktopCatalogScan.Complete(venueId, now);
        database.DesktopCatalogScans.Add(scan);
        foreach (var key in keys)
        {
            var catalogCandidate = DirectCatalog[key];
            database.DesktopCatalogScanCandidates.Add(DesktopCatalogScanCandidate.Detected(
                scan.Id, venueId, key, catalogCandidate.ProductName,
                catalogCandidate.CandidateType, catalogCandidate.Evidence, now));
        }

        await database.SaveChangesAsync(cancellationToken);
        return Results.Created(
            $"/api/v1/organizations/{organizationId}/venues/{venueId}/computer-scans/{scan.Id}",
            ApiResponse<SubmitComputerScanResponse>.Success(
                new(scan.Id, keys.Length, now), context.TraceIdentifier));
    }

    private static async Task<IResult> ListAsync(
        Guid organizationId,
        Guid venueId,
        ClaimsPrincipal user,
        HttpContext context,
        PlatformDbContext database,
        CancellationToken cancellationToken)
    {
        if (!await HasVenueAccessAsync(database, organizationId, venueId, user, false, cancellationToken))
        {
            return Results.Forbid();
        }

        var latestScanId = await database.DesktopCatalogScans
            .Where(scan => scan.VenueId == venueId)
            .OrderByDescending(scan => scan.Id)
            .Select(scan => (Guid?)scan.Id)
            .FirstOrDefaultAsync(cancellationToken);
        DirectRecoveryCandidateSummary[] results = latestScanId is null
            ? []
            : await database.DesktopCatalogScanCandidates
                .Where(candidate => candidate.ScanId == latestScanId.Value && candidate.VenueId == venueId)
                .OrderBy(candidate => candidate.ProductName)
                .ThenBy(candidate => candidate.CandidateType)
                .Select(candidate => new DirectRecoveryCandidateSummary(
                    candidate.Id, candidate.CandidateKey, candidate.ProductName,
                    candidate.CandidateType, candidate.Evidence, "detected",
                    candidate.DetectedAt, true))
                .ToArrayAsync(cancellationToken);
        return Results.Ok(ApiResponse<IReadOnlyList<DirectRecoveryCandidateSummary>>.Success(
            results, context.TraceIdentifier));
    }

    private static async Task<bool> HasVenueAccessAsync(
        PlatformDbContext database,
        Guid organizationId,
        Guid venueId,
        ClaimsPrincipal user,
        bool requireManager,
        CancellationToken cancellationToken)
    {
        var subject = user.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(subject)) return false;
        return await database.Memberships.AnyAsync(membership =>
            membership.OrganizationId == organizationId &&
            membership.IdentitySubject == subject &&
            (!requireManager || membership.Role == ShowVault.Platform.Organizations.OrganizationRole.Manager ||
                membership.Role == ShowVault.Platform.Organizations.OrganizationRole.Administrator ||
                membership.Role == ShowVault.Platform.Organizations.OrganizationRole.Owner) &&
            database.Venues.Any(venue => venue.Id == venueId && venue.OrganizationId == organizationId),
            cancellationToken);
    }
}
