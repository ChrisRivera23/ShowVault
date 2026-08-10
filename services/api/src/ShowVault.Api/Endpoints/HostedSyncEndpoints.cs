using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShowVault.Api.Contracts;
using ShowVault.Api.Data;
using ShowVault.Api.HostedSync;
using ShowVault.Platform.Organizations;

namespace ShowVault.Api.Endpoints;

public static class HostedSyncEndpoints
{
    private const string BasePath =
        "/api/v1/organizations/{organizationId:guid}/venues/{venueId:guid}/hosted-sync/{packageId}";

    public static IEndpointRouteBuilder MapHostedSyncEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(BasePath).RequireAuthorization();
        group.MapGet("/receipt", GetReceiptAsync);
        group.MapPost("/begin", BeginAsync)
            .WithMetadata(new RequestSizeLimitAttribute(3 * 1024 * 1024));
        group.MapPost("/file-state", GetFileStateAsync);
        group.MapPost("/chunks", AppendChunkAsync)
            .WithMetadata(new RequestSizeLimitAttribute(400 * 1024));
        group.MapPost("/commit", CommitAsync)
            .WithMetadata(new RequestSizeLimitAttribute(3 * 1024 * 1024));
        return endpoints;
    }

    private static async Task<IResult> GetReceiptAsync(
        Guid organizationId, Guid venueId, string packageId,
        ClaimsPrincipal user, HttpContext context, PlatformDbContext database,
        IHostedSyncStore store, CancellationToken cancellationToken)
    {
        if (!await CanSynchronizeAsync(database, organizationId, venueId, user, cancellationToken))
            return Results.Forbid();
        return await ExecuteAsync(async () =>
        {
            var receipt = await store.GetReceiptAsync(
                organizationId, venueId, packageId, cancellationToken);
            return receipt is null
                ? Results.NotFound()
                : Results.Ok(ApiResponse<HostedSyncReceiptResponse>.Success(
                    ToResponse(receipt), context.TraceIdentifier));
        });
    }

    private static async Task<IResult> BeginAsync(
        Guid organizationId, Guid venueId, string packageId,
        BeginHostedSyncRequest request, ClaimsPrincipal user, HttpContext context,
        PlatformDbContext database, IHostedSyncStore store,
        CancellationToken cancellationToken)
    {
        if (!await CanSynchronizeAsync(database, organizationId, venueId, user, cancellationToken))
            return Results.Forbid();
        return await ExecuteAsync(async () =>
        {
            if (request.RemoteManifest is null)
                throw new HostedSyncValidationException("A hosted manifest is required.");
            var receipt = await store.BeginAsync(
                organizationId, venueId, packageId, request.RemoteManifest, cancellationToken);
            return receipt is null
                ? Results.NoContent()
                : Results.Ok(ApiResponse<HostedSyncReceiptResponse>.Success(
                    ToResponse(receipt), context.TraceIdentifier));
        });
    }

    private static async Task<IResult> GetFileStateAsync(
        Guid organizationId, Guid venueId, string packageId,
        HostedSyncFileStateRequest request, ClaimsPrincipal user, HttpContext context,
        PlatformDbContext database, IHostedSyncStore store,
        CancellationToken cancellationToken)
    {
        if (!await CanSynchronizeAsync(database, organizationId, venueId, user, cancellationToken))
            return Results.Forbid();
        return await ExecuteAsync(async () =>
        {
            var length = await store.UploadedLengthAsync(
                organizationId, venueId, packageId, request.RelativePath, cancellationToken);
            return Results.Ok(ApiResponse<HostedSyncFileStateResponse>.Success(
                new HostedSyncFileStateResponse(length), context.TraceIdentifier));
        });
    }

    private static async Task<IResult> AppendChunkAsync(
        Guid organizationId, Guid venueId, string packageId,
        AppendHostedSyncChunkRequest request, ClaimsPrincipal user,
        PlatformDbContext database, IHostedSyncStore store,
        CancellationToken cancellationToken)
    {
        if (!await CanSynchronizeAsync(database, organizationId, venueId, user, cancellationToken))
            return Results.Forbid();
        return await ExecuteAsync(async () =>
        {
            if (request.Bytes is null)
                throw new HostedSyncValidationException("Hosted chunk bytes are required.");
            await store.AppendChunkAsync(
                organizationId, venueId, packageId, request.RelativePath,
                request.Offset, request.Bytes, cancellationToken);
            return Results.NoContent();
        });
    }

    private static async Task<IResult> CommitAsync(
        Guid organizationId, Guid venueId, string packageId,
        BeginHostedSyncRequest request, ClaimsPrincipal user, HttpContext context,
        PlatformDbContext database, IHostedSyncStore store,
        CancellationToken cancellationToken)
    {
        if (!await CanSynchronizeAsync(database, organizationId, venueId, user, cancellationToken))
            return Results.Forbid();
        return await ExecuteAsync(async () =>
        {
            if (request.RemoteManifest is null)
                throw new HostedSyncValidationException("A hosted manifest is required.");
            var receipt = await store.VerifyAndCommitAsync(
                organizationId, venueId, packageId, request.RemoteManifest, cancellationToken);
            return Results.Ok(ApiResponse<HostedSyncReceiptResponse>.Success(
                ToResponse(receipt), context.TraceIdentifier));
        });
    }

    private static async Task<bool> CanSynchronizeAsync(
        PlatformDbContext database, Guid organizationId, Guid venueId,
        ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var subject = user.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(subject)) return false;
        return await database.Venues
            .Where(venue => venue.Id == venueId && venue.OrganizationId == organizationId)
            .Join(
                database.Memberships.Where(membership =>
                    membership.OrganizationId == organizationId &&
                    membership.IdentitySubject == subject),
                venue => venue.OrganizationId,
                membership => membership.OrganizationId,
                (_, membership) => membership.Role)
            .AnyAsync(role => role == OrganizationRole.Manager ||
                role == OrganizationRole.Administrator ||
                role == OrganizationRole.Owner,
                cancellationToken);
    }

    private static async Task<IResult> ExecuteAsync(Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (HostedSyncValidationException error)
        {
            return Results.BadRequest(new { error = error.Message });
        }
        catch (HostedSyncConflictException error)
        {
            return Results.Conflict(new { error = error.Message });
        }
        catch (HostedSyncUnavailableException)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
        catch (IOException)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
        catch (UnauthorizedAccessException)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static HostedSyncReceiptResponse ToResponse(HostedSyncReceipt receipt) =>
        new(receipt.PackageId, receipt.RemoteManifestSha256, receipt.CompletedAt);
}
