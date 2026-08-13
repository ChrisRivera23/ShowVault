using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using ShowVault.Api.Authorization;
using ShowVault.Api.Contracts;
using ShowVault.Api.Data;
using ShowVault.Api.HostedSync;
using ShowVault.Api.Commercial;

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
        CommercialStateService commercial, MembershipAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        if (!await authorization.HasVenueAccessAsync(
                organizationId, venueId, user, true, cancellationToken))
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

        var now = timeProvider.GetUtcNow();
        var session = new HostedSyncSession
        {
            Id = Guid.CreateVersion7(now),
            OrganizationId = organizationId,
            VenueId = venueId,
            RecoveryPointId = recoveryPointId,
            ManifestDigest = request.ManifestDigest,
            ManifestJson = manifestJson,
            ManifestTotalBytes = request.Manifest.TotalBytes,
            CreatedAt = now,
            UpdatedAt = now
        };
        var subject = user.FindFirstValue("sub")!;
        HostedSyncReservationResult reservation;
        try
        {
            reservation = await commercial.TryCreateSessionAsync(session, subject,
                context.TraceIdentifier, cancellationToken);
        }
        catch (DbUpdateConcurrencyException) { return Results.Conflict(); }
        if (reservation.Decision == HostedSyncReservationDecision.ManifestConflict)
            return Results.Conflict();
        if (reservation.Decision == HostedSyncReservationDecision.CommercialAccessRequired)
            return CommercialDenied("commercial_access_required");
        if (reservation.Decision == HostedSyncReservationDecision.QuotaExceeded)
            return CommercialDenied("quota_exceeded");
        var accepted = reservation.Session!;
        try { await InitializeZeroObjectsAsync(store, accepted.Id, request.Manifest, cancellationToken); }
        catch (HostedSyncUnavailableException) { return Results.StatusCode(503); }
        return Results.Ok(ApiResponse<HostedSyncBeginResponse>.Success(
            new(accepted.Id.ToString("N"), HostedSyncValidator.MaximumChunkBytes,
                accepted.Status == "completed"),
            context.TraceIdentifier));
    }

    private static async Task<IResult> FileStateAsync(
        Guid organizationId, Guid venueId, string recoveryPointId, Guid sessionId,
        string path, ClaimsPrincipal user, HttpContext context,
        PlatformDbContext database, IHostedObjectStore store,
        MembershipAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        var access = await GetSessionAsync(database, organizationId, venueId,
            recoveryPointId, sessionId, user, authorization, cancellationToken);
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
        MembershipAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        var session = await GetSessionAsync(database, organizationId, venueId,
            recoveryPointId, sessionId, user, authorization, cancellationToken);
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
        IHostedObjectStore store, TimeProvider timeProvider, CommercialStateService commercial,
        MembershipAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        var session = await GetSessionAsync(database, organizationId, venueId,
            recoveryPointId, sessionId, user, authorization, cancellationToken);
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
        await commercial.CommitReservationAsync(session, receipt.CompletedAt,
            user.FindFirstValue("sub")!, context.TraceIdentifier, cancellationToken);
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (Exception exception) when (exception is DbUpdateConcurrencyException or
            DbUpdateException or DbException)
        {
            database.ChangeTracker.Clear();
            for (var attempt = 0; attempt < 10; attempt++)
            {
                var winner = await database.HostedSyncSessions.AsNoTracking()
                    .SingleAsync(value => value.Id == sessionId, cancellationToken);
                if (winner.Status == "completed" && winner.ReceiptJson is not null)
                    return ReceiptResult(winner.ReceiptJson, context.TraceIdentifier);
                await Task.Delay(10, cancellationToken);
            }
            return Results.Conflict();
        }
        return Results.Ok(ApiResponse<HostedSyncReceipt>.Success(receipt, context.TraceIdentifier));
    }

    private static async Task<IResult> ReceiptAsync(
        Guid organizationId, Guid venueId, string recoveryPointId,
        ClaimsPrincipal user, HttpContext context, PlatformDbContext database,
        IHostedObjectStore store,
        MembershipAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        if (!await authorization.HasVenueAccessAsync(
                organizationId, venueId, user, true, cancellationToken))
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

    private static IResult CommercialDenied(string code) => Results.Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "Hosted synchronization cannot start.",
        extensions: new Dictionary<string, object?> { ["code"] = code });

    private static HostedSyncManifest ParseManifest(string json) =>
        JsonSerializer.Deserialize<HostedSyncManifest>(json, JsonOptions) ?? throw new JsonException();

    private static async Task<HostedSyncSession?> GetSessionAsync(
        PlatformDbContext database, Guid organizationId, Guid venueId,
        string recoveryPointId, Guid sessionId, ClaimsPrincipal user,
        MembershipAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        if (!await authorization.HasVenueAccessAsync(
                organizationId, venueId, user, true, cancellationToken))
            return null;
        return await database.HostedSyncSessions.SingleOrDefaultAsync(session =>
            session.Id == sessionId && session.OrganizationId == organizationId &&
            session.VenueId == venueId && session.RecoveryPointId == recoveryPointId,
            cancellationToken);
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
