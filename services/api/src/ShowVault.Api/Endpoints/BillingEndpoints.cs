namespace ShowVault.Api.Endpoints;

using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShowVault.Api.Billing;
using ShowVault.Api.Contracts;
using ShowVault.Api.Data;
using ShowVault.Api.Security;
using ShowVault.Platform.Billing;
using ShowVault.Platform.Organizations;

public static class BillingEndpoints
{
    private static readonly HashSet<string> ReconciliationEvents = new(StringComparer.Ordinal)
    {
        "checkout.session.completed", "checkout.session.async_payment_succeeded",
        "checkout.session.async_payment_failed", "customer.subscription.created",
        "customer.subscription.updated", "customer.subscription.deleted",
        "customer.subscription.paused", "customer.subscription.resumed",
        "invoice.paid", "invoice.payment_failed", "charge.refunded",
        "charge.dispute.created", "charge.dispute.updated", "charge.dispute.closed"
    };

    public static IEndpointRouteBuilder MapBillingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/organizations/{organizationId:guid}/billing")
            .RequireAuthorization();
        group.MapGet("/offering", GetOfferingAsync);
        group.MapPost("/checkout-sessions", CreateCheckoutAsync);
        group.MapPost("/portal-sessions", CreatePortalAsync);
        endpoints.MapPost("/api/v1/provider-webhooks/stripe", ReceiveWebhookAsync)
            .DisableAntiforgery();
        return endpoints;
    }

    private static async Task<IResult> GetOfferingAsync(Guid organizationId,
        ClaimsPrincipal user, HttpContext context, PlatformDbContext database,
        BillingService billing, CancellationToken cancellationToken)
    {
        if (!await IsOwnerAsync(organizationId, user, database, cancellationToken))
            return OwnerFailure(user);
        return Results.Ok(ApiResponse<BillingOfferingSummary?>.Success(
            await billing.CurrentOfferingAsync(organizationId, cancellationToken),
            context.TraceIdentifier));
    }

    private static async Task<IResult> CreateCheckoutAsync(Guid organizationId,
        BillingCheckoutRequest request, ClaimsPrincipal user, HttpContext context,
        PlatformDbContext database, BillingService billing,
        CancellationToken cancellationToken)
    {
        if (IsPersonalBeta(user)) return Results.Forbid();
        if (!await IsOwnerAsync(organizationId, user, database, cancellationToken))
            return OwnerFailure(user);
        if (string.IsNullOrWhiteSpace(request.OfferingCode) || request.OfferingCode.Length > 80)
            return Results.BadRequest();
        var session = await billing.CreateCheckoutAsync(
            organizationId, request.OfferingCode, cancellationToken);
        await RecordOwnerAuditAsync(database, organizationId,
            user.FindFirstValue("sub")!, context.TraceIdentifier,
            "billing_checkout_session", session is null ? "unavailable" : "created",
            session is null ? "provider_disabled" : "hosted_session_created",
            cancellationToken);
        return session is null ? Results.Problem(statusCode: 503,
            title: "Provider billing is unavailable.") :
            Results.Ok(ApiResponse<BillingSessionResponse>.Success(
                new(session.AttemptId, session.Url.AbsoluteUri, session.ExpiresAt,
                    "payment_processing"), context.TraceIdentifier));
    }

    private static async Task<IResult> CreatePortalAsync(Guid organizationId,
        ClaimsPrincipal user, HttpContext context, PlatformDbContext database,
        BillingService billing, CancellationToken cancellationToken)
    {
        if (IsPersonalBeta(user)) return Results.Forbid();
        if (!await IsOwnerAsync(organizationId, user, database, cancellationToken))
            return OwnerFailure(user);
        var session = await billing.CreatePortalAsync(organizationId, cancellationToken);
        await RecordOwnerAuditAsync(database, organizationId,
            user.FindFirstValue("sub")!, context.TraceIdentifier,
            "billing_portal_session", session is null ? "unavailable" : "created",
            session is null ? "binding_or_provider_unavailable" : "hosted_session_created",
            cancellationToken);
        return session is null ? Results.Problem(statusCode: 503,
            title: "Billing management is unavailable.") :
            Results.Ok(ApiResponse<BillingSessionResponse>.Success(
                new(null, session.Url.AbsoluteUri, session.ExpiresAt, "ready"),
                context.TraceIdentifier));
    }

    private static async Task<IResult> ReceiveWebhookAsync(HttpRequest request,
        PlatformDbContext database, IStripeWebhookSignatureVerifier verifier,
        IOptions<StripeWebhookOptions> options,
        IOptions<BillingOptions> billingOptions, TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var configuration = options.Value;
        if (!request.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase)
                ?? true) return Results.BadRequest();
        if (request.ContentLength is > 0 &&
            request.ContentLength > configuration.MaximumBodyBytes) return Results.BadRequest();
        if (!request.Headers.TryGetValue("Stripe-Signature", out var signature))
            return Results.BadRequest();
        await using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        while (true)
        {
            var count = await request.Body.ReadAsync(chunk, cancellationToken);
            if (count == 0) break;
            if (buffer.Length + count > configuration.MaximumBodyBytes) return Results.BadRequest();
            await buffer.WriteAsync(chunk.AsMemory(0, count), cancellationToken);
        }
        var body = buffer.ToArray();
        var now = timeProvider.GetUtcNow();
        if (!verifier.Verify(body, signature.ToString(), now, configuration))
            return Results.BadRequest();

        StripeEvent eventValue;
        try { eventValue = ParseEvent(body); }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        { return Results.BadRequest(); }
        var environment = eventValue.LiveMode ? BillingProviderEnvironment.Live :
            BillingProviderEnvironment.Sandbox;
        if (environment != billingOptions.Value.Environment) return Results.BadRequest();

        var existing = await database.BillingEventReceipts.SingleOrDefaultAsync(value =>
            value.Provider == "stripe" && value.Environment == environment &&
            value.ProviderEventId == eventValue.Id, cancellationToken);
        if (existing is not null) return Results.Ok();
        var receipt = new BillingEventReceipt
        {
            Id = Guid.CreateVersion7(now), Environment = environment,
            ProviderEventId = eventValue.Id, EventType = eventValue.Type,
            ProviderObjectId = eventValue.ObjectId,
            ProviderCreatedAt = DateTimeOffset.FromUnixTimeSeconds(eventValue.Created),
            ApiVersion = eventValue.ApiVersion,
            PayloadSha256 = Convert.ToHexString(SHA256.HashData(body)).ToLowerInvariant(),
            ReceivedAt = now,
            State = ReconciliationEvents.Contains(eventValue.Type)
                ? BillingEventProcessingState.Pending : BillingEventProcessingState.Ignored,
            OutcomeCode = ReconciliationEvents.Contains(eventValue.Type) ? "pending" : "ignored"
        };
        database.BillingEventReceipts.Add(receipt);
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException)
        {
            database.ChangeTracker.Clear();
            if (await database.BillingEventReceipts.AnyAsync(value =>
                value.Provider == "stripe" && value.Environment == environment &&
                value.ProviderEventId == eventValue.Id, cancellationToken)) return Results.Ok();
            throw;
        }
        return Results.Ok();
    }

    private static StripeEvent ParseEvent(byte[] body)
    {
        using var document = JsonDocument.Parse(body, new JsonDocumentOptions
            { MaxDepth = 16, CommentHandling = JsonCommentHandling.Disallow });
        var root = document.RootElement;
        var id = Required(root, "id", 255);
        var type = Required(root, "type", 100);
        var objectId = Required(root.GetProperty("data").GetProperty("object"), "id", 255);
        var created = root.GetProperty("created").GetInt64();
        var liveMode = root.GetProperty("livemode").GetBoolean();
        var apiVersion = root.TryGetProperty("api_version", out var api) &&
            api.ValueKind == JsonValueKind.String ? api.GetString() : null;
        if (created < 0 || apiVersion?.Length > 40) throw new InvalidOperationException();
        return new(id, type, objectId, created, liveMode, apiVersion);
    }

    private static string Required(JsonElement element, string name, int maximum)
    {
        var value = element.GetProperty(name).GetString();
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximum)
            throw new InvalidOperationException();
        return value;
    }

    private static async Task<bool> IsOwnerAsync(Guid organizationId, ClaimsPrincipal user,
        PlatformDbContext database, CancellationToken cancellationToken)
    {
        var subject = user.FindFirstValue("sub");
        return !string.IsNullOrWhiteSpace(subject) &&
            await database.Memberships.AnyAsync(value =>
                value.OrganizationId == organizationId && value.IdentitySubject == subject &&
                value.Role == OrganizationRole.Owner, cancellationToken);
    }

    private static bool IsPersonalBeta(ClaimsPrincipal user) =>
        user.Identities.Any(identity => identity.IsAuthenticated &&
            identity.AuthenticationType == PersonalBetaAuthenticationHandler.SchemeName);
    private static IResult OwnerFailure(ClaimsPrincipal user) =>
        string.IsNullOrWhiteSpace(user.FindFirstValue("sub")) ? Results.Unauthorized() :
            Results.Forbid();

    private static async Task RecordOwnerAuditAsync(PlatformDbContext database,
        Guid organizationId, string actor, string correlationId, string action,
        string outcome, string reason, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        database.CommercialAuditEvents.Add(new()
        {
            Id = Guid.CreateVersion7(now), OrganizationId = organizationId,
            ActorSubject = actor, Action = action, Outcome = outcome,
            ReasonCode = reason, CorrelationId = correlationId,
            PolicyVersion = "billing-1", OccurredAt = now
        });
        await database.SaveChangesAsync(cancellationToken);
    }

    private sealed record StripeEvent(string Id, string Type, string ObjectId,
        long Created, bool LiveMode, string? ApiVersion);
}

public sealed record BillingCheckoutRequest(string OfferingCode);
public sealed record BillingSessionResponse(Guid? AttemptId, string Url,
    DateTimeOffset ExpiresAt, string Status);
