using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShowVault.Api.Contracts;
using ShowVault.Api.Data;
using ShowVault.Api.HostedSync;
using ShowVault.Platform.Organizations;

namespace ShowVault.Api.Endpoints;

public static class HostedSyncEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapHostedSyncEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(
            "/api/v1/organizations/{organizationId:guid}/venues/{venueId:guid}" +
            "/hosted-sync/{recoveryPointId}").RequireAuthorization();
        group.MapPost("/begin", BeginAsync);
        group.MapGet("/sessions/{sessionId:guid}/files", FileStateAsync);
        group.MapPut("/sessions/{sessionId:guid}/files", AppendAsync)
            .DisableAntiforgery();
        group.MapPost("/sessions/{sessionId:guid}/commit", CommitAsync);
        group.MapGet("/receipt", ReceiptAsync);
        return endpoints;
    }

    private static async Task<IResult> BeginAsync(
        Guid organizationId, Guid venueId, string recoveryPointId,
        JsonElement requestBody, ClaimsPrincipal user, HttpContext context,
        PlatformDbContext database, IHostedObjectStore store, TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!await CanWriteAsync(database, organizationId, venueId, user, cancellationToken))
            return Results.Forbid();
        if (!store.IsAvailable) return Results.StatusCode(503);
        HostedSyncBeginRequest request;
        try
        {
            request = requestBody.Deserialize<HostedSyncBeginRequest>(JsonOptions)
                ?? throw new JsonException();
        }
        catch (JsonException) { return Results.BadRequest(); }
        if (!HostedSyncValidator.TryValidate(recoveryPointId, request, out var manifestJson))
            return Results.BadRequest();

        var existing = await database.HostedSyncSessions.SingleOrDefaultAsync(session =>
            session.OrganizationId == organizationId && session.VenueId == venueId &&
            session.RecoveryPointId == recoveryPointId, cancellationToken);
        if (existing is not null)
        {
            if (existing.ManifestDigest != request.ManifestDigest) return Results.Conflict();
            try { await InitializeZeroObjectsAsync(store, existing.Id, request.Manifest, cancellationToken); }
            catch (HostedSyncUnavailableException) { return Results.StatusCode(503); }
            return Results.Ok(ApiResponse<HostedSyncBeginResponse>.Success(
                new(existing.Id.ToString("N"), HostedSyncValidator.MaximumChunkBytes,
                    existing.Status == "completed"), context.TraceIdentifier));
        }

        var now = timeProvider.GetUtcNow();
        var session = new HostedSyncSession
        {
            Id = Guid.CreateVersion7(now),
            OrganizationId = organizationId,
            VenueId = venueId,
            RecoveryPointId = recoveryPointId,
            ManifestDigest = request.ManifestDigest,
            ManifestJson = manifestJson,
            CreatedAt = now,
            UpdatedAt = now
        };
        database.HostedSyncSessions.Add(session);
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException)
        {
            database.ChangeTracker.Clear();
            var winner = await database.HostedSyncSessions.SingleOrDefaultAsync(value =>
                value.OrganizationId == organizationId && value.VenueId == venueId &&
                value.RecoveryPointId == recoveryPointId, cancellationToken);
            if (winner is null || winner.ManifestDigest != request.ManifestDigest)
                return Results.Conflict();
            try { await InitializeZeroObjectsAsync(store, winner.Id, request.Manifest, cancellationToken); }
            catch (HostedSyncUnavailableException) { return Results.StatusCode(503); }
            return Results.Ok(ApiResponse<HostedSyncBeginResponse>.Success(
                new(winner.Id.ToString("N"), HostedSyncValidator.MaximumChunkBytes,
                    winner.Status == "completed"), context.TraceIdentifier));
        }
        try { await InitializeZeroObjectsAsync(store, session.Id, request.Manifest, cancellationToken); }
        catch (HostedSyncUnavailableException) { return Results.StatusCode(503); }
        return Results.Ok(ApiResponse<HostedSyncBeginResponse>.Success(
            new(session.Id.ToString("N"), HostedSyncValidator.MaximumChunkBytes, false),
            context.TraceIdentifier));
    }

    private static async Task<IResult> FileStateAsync(
        Guid organizationId, Guid venueId, string recoveryPointId, Guid sessionId,
        string path, ClaimsPrincipal user, HttpContext context,
        PlatformDbContext database, IHostedObjectStore store,
        CancellationToken cancellationToken)
    {
        var access = await GetSessionAsync(database, organizationId, venueId,
            recoveryPointId, sessionId, user, cancellationToken);
        if (access is null) return Results.Forbid();
        var manifest = ParseManifest(access.ManifestJson);
        if (!manifest.Files.Any(file => file.RelativePath == path)) return Results.BadRequest();
        try
        {
            var length = await store.GetLengthAsync(
                HostedSyncValidator.ObjectKey(sessionId, path), cancellationToken);
            return Results.Ok(ApiResponse<HostedSyncFileStateResponse>.Success(
                new(length), context.TraceIdentifier));
        }
        catch (HostedSyncUnavailableException) { return Results.StatusCode(503); }
    }

    private static async Task<IResult> AppendAsync(
        Guid organizationId, Guid venueId, string recoveryPointId, Guid sessionId,
        string path, long offset, HttpRequest request, ClaimsPrincipal user,
        PlatformDbContext database, IHostedObjectStore store,
        CancellationToken cancellationToken)
    {
        var session = await GetSessionAsync(database, organizationId, venueId,
            recoveryPointId, sessionId, user, cancellationToken);
        if (session is null) return Results.Forbid();
        if (session.Status != "uploading" || offset < 0 ||
            request.ContentLength is null or < 1 or > HostedSyncValidator.MaximumChunkBytes ||
            !request.Headers.TryGetValue("X-ShowVault-Chunk-Sha256", out var expectedDigest))
            return Results.BadRequest();
        var manifest = ParseManifest(session.ManifestJson);
        var file = manifest.Files.SingleOrDefault(file => file.RelativePath == path);
        if (file is null || offset + request.ContentLength > file.Size) return Results.BadRequest();
        var bytes = new byte[checked((int)request.ContentLength.Value)];
        await request.Body.ReadExactlyAsync(bytes, cancellationToken);
        var digest = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (digest != expectedDigest.ToString()) return Results.BadRequest();
        try
        {
            await store.AppendAsync(HostedSyncValidator.ObjectKey(sessionId, path),
                offset, bytes, cancellationToken);
            return Results.NoContent();
        }
        catch (HostedSyncConflictException) { return Results.Conflict(); }
        catch (HostedSyncUnavailableException) { return Results.StatusCode(503); }
    }

    private static async Task<IResult> CommitAsync(
        Guid organizationId, Guid venueId, string recoveryPointId, Guid sessionId,
        ClaimsPrincipal user, HttpContext context, PlatformDbContext database,
        IHostedObjectStore store, TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var session = await GetSessionAsync(database, organizationId, venueId,
            recoveryPointId, sessionId, user, cancellationToken);
        if (session is null) return Results.Forbid();
        if (session.Status == "completed" && session.ReceiptJson is not null)
            return ReceiptResult(session.ReceiptJson, context.TraceIdentifier);
        var manifest = ParseManifest(session.ManifestJson);
        var objects = new List<HostedSyncObjectDigest>();
        try
        {
            var expectedKeys = manifest.Files.Select(file =>
                    HostedSyncValidator.ObjectKey(sessionId, file.RelativePath))
                .Order(StringComparer.Ordinal).ToArray();
            var actualKeys = await store.ListKeysAsync(
                HostedSyncValidator.ObjectPrefix(sessionId), cancellationToken);
            if (!expectedKeys.SequenceEqual(
                    actualKeys.Order(StringComparer.Ordinal), StringComparer.Ordinal))
                return Results.Conflict();
            foreach (var file in manifest.Files)
            {
                var bytes = await store.ReadAsync(
                    HostedSyncValidator.ObjectKey(sessionId, file.RelativePath), cancellationToken);
                var digest = Convert.ToHexStringLower(SHA256.HashData(bytes));
                if (bytes.LongLength != file.Size || digest != file.Sha256)
                    return Results.Conflict();
                objects.Add(new(file.RelativePath, file.Size, digest));
            }
        }
        catch (KeyNotFoundException) { return Results.Conflict(); }
        catch (HostedSyncUnavailableException) { return Results.StatusCode(503); }
        var receipt = new HostedSyncReceipt("1.0", organizationId, venueId,
            recoveryPointId, session.ManifestDigest, manifest.FileCount,
            manifest.TotalBytes, objects, timeProvider.GetUtcNow());
        var receiptJson = JsonSerializer.Serialize(receipt, JsonOptions);
        session.ReceiptJson = receiptJson;
        session.Status = "completed";
        session.UpdatedAt = receipt.CompletedAt;
        session.Revision++;
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            database.ChangeTracker.Clear();
            var winner = await database.HostedSyncSessions.SingleAsync(value =>
                value.Id == sessionId, cancellationToken);
            if (winner.Status != "completed" || winner.ReceiptJson is null)
                return Results.Conflict();
            return ReceiptResult(winner.ReceiptJson, context.TraceIdentifier);
        }
        return Results.Ok(ApiResponse<HostedSyncReceipt>.Success(receipt, context.TraceIdentifier));
    }

    private static async Task<IResult> ReceiptAsync(
        Guid organizationId, Guid venueId, string recoveryPointId,
        ClaimsPrincipal user, HttpContext context, PlatformDbContext database,
        IHostedObjectStore store,
        CancellationToken cancellationToken)
    {
        if (!await CanWriteAsync(database, organizationId, venueId, user, cancellationToken))
            return Results.Forbid();
        if (!store.IsAvailable) return Results.StatusCode(503);
        var receipt = await database.HostedSyncSessions.Where(session =>
                session.OrganizationId == organizationId && session.VenueId == venueId &&
                session.RecoveryPointId == recoveryPointId && session.Status == "completed")
            .Select(session => session.ReceiptJson).SingleOrDefaultAsync(cancellationToken);
        return receipt is null ? Results.NotFound() :
            ReceiptResult(receipt, context.TraceIdentifier);
    }

    private static IResult ReceiptResult(string json, string correlationId)
    {
        var receipt = JsonSerializer.Deserialize<HostedSyncReceipt>(json, JsonOptions)
            ?? throw new JsonException();
        return Results.Ok(ApiResponse<HostedSyncReceipt>.Success(receipt, correlationId));
    }

    private static HostedSyncManifest ParseManifest(string json) =>
        JsonSerializer.Deserialize<HostedSyncManifest>(json, JsonOptions) ?? throw new JsonException();

    private static async Task<HostedSyncSession?> GetSessionAsync(
        PlatformDbContext database, Guid organizationId, Guid venueId,
        string recoveryPointId, Guid sessionId, ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        if (!await CanWriteAsync(database, organizationId, venueId, user, cancellationToken))
            return null;
        return await database.HostedSyncSessions.SingleOrDefaultAsync(session =>
            session.Id == sessionId && session.OrganizationId == organizationId &&
            session.VenueId == venueId && session.RecoveryPointId == recoveryPointId,
            cancellationToken);
    }

    private static async Task<bool> CanWriteAsync(
        PlatformDbContext database, Guid organizationId, Guid venueId,
        ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var subject = user.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(subject)) return false;
        return await database.Memberships.AnyAsync(membership =>
            membership.OrganizationId == organizationId &&
            membership.IdentitySubject == subject &&
            (membership.Role == OrganizationRole.Manager ||
             membership.Role == OrganizationRole.Administrator ||
             membership.Role == OrganizationRole.Owner) &&
            database.Venues.Any(venue => venue.Id == venueId &&
                venue.OrganizationId == organizationId), cancellationToken);
    }

    private static async Task InitializeZeroObjectsAsync(
        IHostedObjectStore store,
        Guid sessionId,
        HostedSyncManifest manifest,
        CancellationToken cancellationToken)
    {
        foreach (var file in manifest.Files.Where(file => file.Size == 0))
            await store.AppendAsync(HostedSyncValidator.ObjectKey(sessionId, file.RelativePath),
                0, ReadOnlyMemory<byte>.Empty, cancellationToken);
    }
}
