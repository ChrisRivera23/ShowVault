namespace ShowVault.Api.Billing;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShowVault.Api.Data;
using ShowVault.Platform.Billing;
using ShowVault.Platform.Commercial;

public sealed record BillingOfferingSummary(string Code, string DisplayName,
    bool HasBillingAccount);
public sealed record BillingSessionResult(Guid? AttemptId, Uri Url, DateTimeOffset ExpiresAt);

public sealed class BillingService(
    PlatformDbContext database,
    IBillingProvider provider,
    IBillingOfferingCatalog offerings,
    IOptions<BillingOptions> options,
    TimeProvider timeProvider)
{
    public async Task<BillingOfferingSummary?> CurrentOfferingAsync(
        Guid organizationId, CancellationToken cancellationToken)
    {
        var offering = offerings.Current;
        var available = provider.IsAvailable && options.Value.TryGetReturnOrigin(out _) &&
            offering is not null;
        if (!available) return null;
        var hasAccount = await database.BillingAccountBindings.AsNoTracking().AnyAsync(value =>
            value.OrganizationId == organizationId && value.Provider == "stripe" &&
            value.Environment == options.Value.Environment, cancellationToken);
        return offering is not null
            ? new(offering.Code, offering.DisplayName, hasAccount)
            : null;
    }

    public async Task<BillingSessionResult?> CreateCheckoutAsync(
        Guid organizationId, string offeringCode, CancellationToken cancellationToken)
    {
        var configuration = options.Value;
        var offering = offerings.Find(offeringCode);
        if (!provider.IsAvailable || offering is null ||
            !configuration.TryGetReturnOrigin(out var origin)) return null;

        var now = timeProvider.GetUtcNow();
        var attempt = await database.BillingPurchaseAttempts.SingleOrDefaultAsync(value =>
            value.OrganizationId == organizationId && value.ActiveSlot == "checkout",
            cancellationToken);
        if (attempt is not null && attempt.ExpiresAt <= now)
        {
            attempt.State = BillingPurchaseAttemptState.Expired;
            attempt.ActiveSlot = null;
            attempt.UpdatedAt = now;
            attempt.Revision++;
            await database.SaveChangesAsync(cancellationToken);
            attempt = null;
        }
        if (attempt is not null && attempt.OfferingCode != offering.Code)
            throw new InvalidOperationException("An organization already has an open purchase attempt.");

        if (attempt is null)
        {
            attempt = new BillingPurchaseAttempt
            {
                Id = Guid.CreateVersion7(now),
                OrganizationId = organizationId,
                Environment = configuration.Environment,
                OfferingCode = offering.Code,
                State = BillingPurchaseAttemptState.Creating,
                ActiveSlot = "checkout",
                CreatedAt = now,
                UpdatedAt = now,
                ExpiresAt = now.AddMinutes(configuration.CheckoutLifetimeMinutes)
            };
            database.BillingPurchaseAttempts.Add(attempt);
            try { await database.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateException)
            {
                database.ChangeTracker.Clear();
                attempt = await database.BillingPurchaseAttempts.SingleAsync(value =>
                    value.OrganizationId == organizationId && value.ActiveSlot == "checkout",
                    cancellationToken);
            }
        }

        var command = new BillingCheckoutCommand(
            organizationId, attempt.Id, configuration.Environment, offering,
            new Uri(origin, $"billing/checkout/return?attempt={attempt.Id:D}"),
            new Uri(origin, "billing/checkout/canceled"));
        try
        {
            var session = await provider.CreateCheckoutAsync(command,
                attempt.Id.ToString("N"), cancellationToken);
            ValidateHostedSession(session, now, TimeSpan.FromMinutes(60));
            attempt.ProviderSessionId = session.Id;
            attempt.State = BillingPurchaseAttemptState.Open;
            attempt.ExpiresAt = session.ExpiresAt;
            attempt.UpdatedAt = timeProvider.GetUtcNow();
            attempt.Revision++;
            try { await database.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateConcurrencyException)
            {
                database.ChangeTracker.Clear();
                var winner = await database.BillingPurchaseAttempts.AsNoTracking()
                    .SingleAsync(value => value.Id == attempt.Id, cancellationToken);
                if (winner.State != BillingPurchaseAttemptState.Open ||
                    winner.ProviderSessionId != session.Id)
                    throw new InvalidOperationException(
                        "Concurrent purchase attempt state is inconsistent.");
            }
            return new(attempt.Id, session.Url, session.ExpiresAt);
        }
        catch
        {
            attempt.State = BillingPurchaseAttemptState.Failed;
            attempt.ActiveSlot = null;
            attempt.UpdatedAt = timeProvider.GetUtcNow();
            attempt.Revision++;
            await database.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<BillingSessionResult?> CreatePortalAsync(
        Guid organizationId, CancellationToken cancellationToken)
    {
        var configuration = options.Value;
        if (!provider.IsAvailable || !configuration.TryGetReturnOrigin(out var origin)) return null;
        var binding = await database.BillingAccountBindings.AsNoTracking()
            .SingleOrDefaultAsync(value => value.OrganizationId == organizationId &&
                value.Provider == "stripe" && value.Environment == configuration.Environment,
                cancellationToken);
        if (binding is null) return null;
        var session = await provider.CreatePortalAsync(binding.ProviderCustomerId,
            new Uri(origin, "billing/return"), cancellationToken);
        ValidateHostedSession(session, timeProvider.GetUtcNow(), TimeSpan.FromMinutes(15));
        return new(null, session.Url, session.ExpiresAt);
    }

    private static void ValidateHostedSession(BillingHostedSession session,
        DateTimeOffset now, TimeSpan maximumLifetime)
    {
        if (string.IsNullOrWhiteSpace(session.Id) || session.Id.Length > 255 ||
            session.Url.Scheme != Uri.UriSchemeHttps || !session.Url.IsAbsoluteUri ||
            !string.IsNullOrEmpty(session.Url.UserInfo) || session.ExpiresAt <= now ||
            session.ExpiresAt > now.Add(maximumLifetime))
            throw new InvalidOperationException("The provider returned an invalid hosted session.");
    }
}

public sealed class BillingReconciliationService(
    PlatformDbContext database,
    IBillingProvider provider,
    IBillingOfferingCatalog offerings,
    TimeProvider timeProvider)
{
    public async Task ProcessPendingAsync(int limit, CancellationToken cancellationToken)
    {
        if (!provider.IsAvailable) return;
        var ids = await database.BillingEventReceipts.AsNoTracking()
            .Where(value => value.State == BillingEventProcessingState.Pending)
            .OrderBy(value => value.Id).Select(value => value.Id)
            .Take(Math.Clamp(limit, 1, 25)).ToListAsync(cancellationToken);
        foreach (var id in ids) await ProcessAsync(id, cancellationToken);
    }

    public async Task ProcessAsync(Guid receiptId, CancellationToken cancellationToken)
    {
        var receipt = await database.BillingEventReceipts.SingleOrDefaultAsync(
            value => value.Id == receiptId, cancellationToken);
        if (receipt is null || receipt.State != BillingEventProcessingState.Pending ||
            !provider.IsAvailable) return;
        var snapshot = await provider.RetrieveCurrentStateAsync(
            receipt.EventType, receipt.ProviderObjectId, cancellationToken);
        if (snapshot is null)
        {
            Complete(receipt, BillingEventProcessingState.Attention, "state_unavailable");
            await database.SaveChangesAsync(cancellationToken);
            return;
        }
        if (!ValidSnapshot(snapshot))
        {
            Complete(receipt, BillingEventProcessingState.Attention, "provider_state_invalid");
            await database.SaveChangesAsync(cancellationToken);
            return;
        }
        receipt.OrganizationId = snapshot.OrganizationId;
        var offering = offerings.Find(snapshot.OfferingCode);
        var attempt = await database.BillingPurchaseAttempts.SingleOrDefaultAsync(value =>
            value.OrganizationId == snapshot.OrganizationId &&
            value.ProviderSessionId == snapshot.CheckoutSessionId &&
            value.Environment == receipt.Environment, cancellationToken);
        if (offering is null || attempt is null ||
            offering.RecurringPriceId != snapshot.RecurringPriceId ||
            offering.LicensePriceId != snapshot.LicensePriceId)
        {
            await AttentionAsync(snapshot.OrganizationId, "provider_object_mismatch",
                cancellationToken);
            Complete(receipt, BillingEventProcessingState.Attention, "provider_object_mismatch");
            await database.SaveChangesAsync(cancellationToken);
            return;
        }

        var binding = await database.BillingAccountBindings.SingleOrDefaultAsync(value =>
            value.OrganizationId == snapshot.OrganizationId, cancellationToken);
        if (binding is not null && (binding.Environment != receipt.Environment ||
            binding.ProviderCustomerId != snapshot.CustomerId))
        {
            await AttentionAsync(snapshot.OrganizationId, "provider_binding_conflict",
                cancellationToken);
            Complete(receipt, BillingEventProcessingState.Attention, "provider_binding_conflict");
            await database.SaveChangesAsync(cancellationToken);
            return;
        }
        if (string.Equals(binding?.ProviderRevision, snapshot.ProviderRevision,
                StringComparison.Ordinal) ||
            binding?.ProviderModifiedAt > snapshot.ProviderModifiedAt)
        {
            Complete(receipt, BillingEventProcessingState.Processed, "stale_noop");
            await database.SaveChangesAsync(cancellationToken);
            return;
        }
        binding ??= new BillingAccountBinding
        {
            OrganizationId = snapshot.OrganizationId,
            Environment = receipt.Environment,
            ProviderCustomerId = snapshot.CustomerId
        };
        if (database.Entry(binding).State == EntityState.Detached)
            database.BillingAccountBindings.Add(binding);
        binding.ProviderSubscriptionId = snapshot.SubscriptionId;
        binding.InitialInvoiceId = snapshot.InitialInvoiceId;
        binding.OfferingCode = offering.Code;
        binding.ProviderModifiedAt = snapshot.ProviderModifiedAt;
        binding.ProviderRevision = snapshot.ProviderRevision;
        binding.UpdatedAt = timeProvider.GetUtcNow();
        binding.Revision++;

        var now = timeProvider.GetUtcNow();
        var license = await database.CommercialLicenses.SingleOrDefaultAsync(value =>
            value.OrganizationId == snapshot.OrganizationId, cancellationToken);
        license ??= new CommercialLicense
        {
            Id = Guid.CreateVersion7(now),
            OrganizationId = snapshot.OrganizationId,
            LicenseTypeCode = offering.LicenseTypeCode
        };
        if (database.Entry(license).State == EntityState.Detached)
            database.CommercialLicenses.Add(license);
        license.State = snapshot.LicensePaymentState switch
        {
            BillingLicensePaymentState.Paid => CommercialLicenseState.Active,
            BillingLicensePaymentState.FullyRefunded => CommercialLicenseState.Refunded,
            BillingLicensePaymentState.PartialOrAmbiguous or
                BillingLicensePaymentState.Disputed => CommercialLicenseState.Revoked,
            _ => CommercialLicenseState.Pending
        };
        license.EffectiveAt = license.State == CommercialLicenseState.Active
            ? license.EffectiveAt ?? now : null;
        license.UpdatedAt = now;
        license.Revision++;

        var subscription = await database.ServiceSubscriptions.SingleOrDefaultAsync(value =>
            value.OrganizationId == snapshot.OrganizationId, cancellationToken);
        subscription ??= new ServiceSubscription
        {
            Id = Guid.CreateVersion7(now),
            OrganizationId = snapshot.OrganizationId,
            PlanCode = offering.PlanCode
        };
        if (database.Entry(subscription).State == EntityState.Detached)
            database.ServiceSubscriptions.Add(subscription);
        var mapped = MapSubscription(snapshot.SubscriptionStatus);
        subscription.State = mapped ?? ServiceSubscriptionState.Paused;
        subscription.CurrentPeriodEndsAt = snapshot.CurrentPeriodEndsAt;
        subscription.GraceEndsAt = null;
        subscription.UpdatedAt = now;
        subscription.Revision++;

        var attentionReason = snapshot.LicensePaymentState switch
        {
            BillingLicensePaymentState.PartialOrAmbiguous => "license_refund_ambiguous",
            BillingLicensePaymentState.Disputed => "license_payment_disputed",
            _ when mapped is null => "subscription_status_unsupported",
            _ => null
        };
        if (attentionReason is not null)
            await AttentionAsync(snapshot.OrganizationId, attentionReason, cancellationToken);
        else
        {
            var open = await database.BillingAttentions.Where(value =>
                value.OrganizationId == snapshot.OrganizationId && value.ResolvedAt == null)
                .ToListAsync(cancellationToken);
            foreach (var attention in open)
            {
                attention.ResolvedAt = now;
                attention.Revision++;
            }
        }
        attempt.State = BillingPurchaseAttemptState.Completed;
        attempt.ActiveSlot = null;
        attempt.UpdatedAt = now;
        attempt.Revision++;
        Complete(receipt, attentionReason is null ? BillingEventProcessingState.Processed :
            BillingEventProcessingState.Attention, attentionReason ?? "projection_updated");
        database.CommercialAuditEvents.Add(new CommercialAuditEvent
        {
            Id = Guid.CreateVersion7(now),
            OrganizationId = snapshot.OrganizationId,
            Action = "billing_projection_reconciled",
            Outcome = attentionReason is null ? "updated" : "attention",
            ReasonCode = attentionReason ?? "provider_state_current",
            CorrelationId = receipt.Id.ToString("N"),
            PolicyVersion = offering.PolicyVersion,
            OccurredAt = now
        });
        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task AttentionAsync(Guid organizationId, string reason,
        CancellationToken cancellationToken)
    {
        if (await database.BillingAttentions.AnyAsync(value =>
            value.OrganizationId == organizationId && value.ResolvedAt == null &&
            value.ReasonCode == reason, cancellationToken)) return;
        var now = timeProvider.GetUtcNow();
        database.BillingAttentions.Add(new BillingAttention
        {
            Id = Guid.CreateVersion7(now),
            OrganizationId = organizationId,
            ReasonCode = reason,
            OpenedAt = now
        });
    }

    private void Complete(BillingEventReceipt receipt, BillingEventProcessingState state,
        string outcome)
    {
        receipt.State = state;
        receipt.OutcomeCode = outcome;
        receipt.ProcessedAt = timeProvider.GetUtcNow();
        receipt.Revision++;
    }

    private static ServiceSubscriptionState? MapSubscription(string value) => value switch
    {
        "incomplete" => ServiceSubscriptionState.Incomplete,
        "trialing" => ServiceSubscriptionState.Trialing,
        "active" => ServiceSubscriptionState.Active,
        "past_due" => ServiceSubscriptionState.PastDue,
        "unpaid" => ServiceSubscriptionState.Unpaid,
        "paused" => ServiceSubscriptionState.Paused,
        "canceled" => ServiceSubscriptionState.Canceled,
        _ => null
    };

    private static bool ValidSnapshot(BillingProviderSnapshot value) =>
        value.OrganizationId != Guid.Empty &&
        Bounded(value.OfferingCode, 80) && Bounded(value.CheckoutSessionId, 255) &&
        Bounded(value.CustomerId, 255) && Bounded(value.SubscriptionId, 255) &&
        Bounded(value.InitialInvoiceId, 255) && Bounded(value.RecurringPriceId, 255) &&
        Bounded(value.LicensePriceId, 255) && Bounded(value.SubscriptionStatus, 40) &&
        Bounded(value.ProviderRevision, 120);

    private static bool Bounded(string value, int maximum) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maximum;
}

public sealed class BillingReconciliationWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<BillingReconciliationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<BillingReconciliationService>()
                    .ProcessPendingAsync(10, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Bounded billing reconciliation pass failed.");
            }
            if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
        }
    }
}
