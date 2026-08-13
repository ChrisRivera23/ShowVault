namespace ShowVault.Api.Commercial;

using Microsoft.EntityFrameworkCore;
using ShowVault.Api.Data;
using ShowVault.Api.HostedSync;
using ShowVault.Platform.Commercial;

public enum HostedSyncReservationDecision
{
    Created,
    Existing,
    ManifestConflict,
    CommercialAccessRequired,
    QuotaExceeded
}

public sealed record HostedSyncReservationResult(
    HostedSyncReservationDecision Decision,
    HostedSyncSession? Session,
    string ReasonCode);

public sealed record OrganizationPlanSnapshot(
    string? PlanCode,
    string LicenseStatus,
    string SubscriptionStatus,
    DateTimeOffset? CurrentPeriodEndsAt,
    DateTimeOffset? GraceEndsAt,
    long LogicalStorageLimitBytes,
    long CommittedBytes,
    long ReservedBytes,
    bool Eligible,
    string ReasonCode);

public sealed class CommercialStateService(
    PlatformDbContext database,
    ICommercialPlanPolicyCatalog policies,
    TimeProvider timeProvider)
{
    public async Task<OrganizationPlanSnapshot> GetPlanAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var license = await database.CommercialLicenses.AsNoTracking()
            .SingleOrDefaultAsync(value => value.OrganizationId == organizationId,
                cancellationToken);
        var subscription = await database.ServiceSubscriptions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.OrganizationId == organizationId,
                cancellationToken);
        var usage = await database.OrganizationStorageUsages.AsNoTracking()
            .SingleOrDefaultAsync(value => value.OrganizationId == organizationId,
                cancellationToken);
        var entitlement = CommercialEntitlementEvaluator.Evaluate(
            license, subscription, timeProvider.GetUtcNow(), policies);
        if (await database.BillingAttentions.AsNoTracking().AnyAsync(value =>
                value.OrganizationId == organizationId && value.ResolvedAt == null,
                cancellationToken))
            entitlement = new(false, CommercialReasonCodes.BillingAttention,
                entitlement.Policy);
        return new(
            subscription?.PlanCode,
            LicenseStatus(license?.State),
            SubscriptionStatus(subscription?.State),
            subscription?.CurrentPeriodEndsAt,
            subscription?.GraceEndsAt,
            entitlement.Policy?.LogicalStorageLimitBytes ?? 0,
            usage?.CommittedBytes ?? 0,
            usage?.ReservedBytes ?? 0,
            entitlement.Eligible,
            entitlement.ReasonCode);
    }

    public async Task<HostedSyncReservationResult> TryCreateSessionAsync(
        HostedSyncSession proposed,
        string actorSubject,
        string correlationId,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var existing = await database.HostedSyncSessions.SingleOrDefaultAsync(session =>
                session.OrganizationId == proposed.OrganizationId &&
                session.VenueId == proposed.VenueId &&
                session.RecoveryPointId == proposed.RecoveryPointId, cancellationToken);
            if (existing is not null)
                return existing.ManifestDigest == proposed.ManifestDigest
                    ? new(HostedSyncReservationDecision.Existing, existing,
                        CommercialReasonCodes.Eligible)
                    : new(HostedSyncReservationDecision.ManifestConflict, existing,
                        "manifest_conflict");

            var license = await database.CommercialLicenses.SingleOrDefaultAsync(value =>
                value.OrganizationId == proposed.OrganizationId, cancellationToken);
            var subscription = await database.ServiceSubscriptions.SingleOrDefaultAsync(value =>
                value.OrganizationId == proposed.OrganizationId, cancellationToken);
            var usage = await database.OrganizationStorageUsages.SingleOrDefaultAsync(value =>
                value.OrganizationId == proposed.OrganizationId, cancellationToken);
            var entitlement = CommercialEntitlementEvaluator.Evaluate(
                license, subscription, timeProvider.GetUtcNow(), policies);
            if (await database.BillingAttentions.AsNoTracking().AnyAsync(value =>
                    value.OrganizationId == proposed.OrganizationId &&
                    value.ResolvedAt == null, cancellationToken))
                entitlement = new(false, CommercialReasonCodes.BillingAttention,
                    entitlement.Policy);
            if (!entitlement.Eligible)
            {
                await RecordDecisionAsync(proposed.OrganizationId, actorSubject, correlationId,
                    "denied", entitlement.ReasonCode, proposed.ManifestTotalBytes,
                    usage, entitlement.Policy?.PolicyVersion ?? "commercial-1", cancellationToken);
                return new(HostedSyncReservationDecision.CommercialAccessRequired, null,
                    entitlement.ReasonCode);
            }

            usage ??= new OrganizationStorageUsage { OrganizationId = proposed.OrganizationId };
            if (database.Entry(usage).State == EntityState.Detached)
                database.OrganizationStorageUsages.Add(usage);
            var limit = entitlement.Policy!.LogicalStorageLimitBytes;
            if (usage.CommittedBytes < 0 || usage.ReservedBytes < 0 ||
                usage.CommittedBytes > limit ||
                usage.ReservedBytes > limit - usage.CommittedBytes)
            {
                await RecordDecisionAsync(proposed.OrganizationId, actorSubject, correlationId,
                    "denied", CommercialReasonCodes.StateInconsistent,
                    proposed.ManifestTotalBytes, usage, entitlement.Policy.PolicyVersion,
                    cancellationToken);
                return new(HostedSyncReservationDecision.CommercialAccessRequired, null,
                    CommercialReasonCodes.StateInconsistent);
            }
            if (proposed.ManifestTotalBytes > limit - usage.CommittedBytes - usage.ReservedBytes)
            {
                await RecordDecisionAsync(proposed.OrganizationId, actorSubject, correlationId,
                    "denied", CommercialReasonCodes.QuotaExceeded,
                    proposed.ManifestTotalBytes, usage, entitlement.Policy.PolicyVersion,
                    cancellationToken);
                return new(HostedSyncReservationDecision.QuotaExceeded, null,
                    CommercialReasonCodes.QuotaExceeded);
            }

            var now = timeProvider.GetUtcNow();
            usage.ReservedBytes += proposed.ManifestTotalBytes;
            usage.Revision++;
            database.HostedSyncSessions.Add(proposed);
            database.HostedSyncReservations.Add(new HostedSyncReservation
            {
                HostedSyncSessionId = proposed.Id,
                OrganizationId = proposed.OrganizationId,
                LogicalBytes = proposed.ManifestTotalBytes,
                State = HostedSyncReservationState.Reserved,
                ReservedAt = now
            });
            database.CommercialAuditEvents.Add(Audit(proposed.OrganizationId,
                actorSubject, correlationId, "allowed", CommercialReasonCodes.Eligible,
                proposed.ManifestTotalBytes, usage, entitlement.Policy.PolicyVersion, now));
            try
            {
                await database.SaveChangesAsync(cancellationToken);
                return new(HostedSyncReservationDecision.Created, proposed,
                    CommercialReasonCodes.Eligible);
            }
            catch (DbUpdateConcurrencyException) when (attempt < 3)
            {
                database.ChangeTracker.Clear();
            }
            catch (DbUpdateException) when (attempt < 3)
            {
                database.ChangeTracker.Clear();
            }
        }
        throw new DbUpdateConcurrencyException("Commercial reservation could not be serialized.");
    }

    public async Task CommitReservationAsync(HostedSyncSession session,
        DateTimeOffset committedAt, string actorSubject, string correlationId,
        CancellationToken cancellationToken)
    {
        var reservation = await database.HostedSyncReservations.SingleAsync(value =>
            value.HostedSyncSessionId == session.Id, cancellationToken);
        if (reservation.State == HostedSyncReservationState.Committed) return;
        var usage = await database.OrganizationStorageUsages.SingleAsync(value =>
            value.OrganizationId == session.OrganizationId, cancellationToken);
        if (usage.ReservedBytes < reservation.LogicalBytes)
            throw new InvalidOperationException("Hosted synchronization usage is inconsistent.");
        usage.ReservedBytes -= reservation.LogicalBytes;
        usage.CommittedBytes = checked(usage.CommittedBytes + reservation.LogicalBytes);
        usage.Revision++;
        reservation.State = HostedSyncReservationState.Committed;
        reservation.CommittedAt = committedAt;
        reservation.Revision++;
        var subscription = await database.ServiceSubscriptions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.OrganizationId == session.OrganizationId,
                cancellationToken);
        var policyVersion = subscription is null ? null :
            policies.Find(subscription.PlanCode)?.PolicyVersion;
        database.CommercialAuditEvents.Add(Audit(session.OrganizationId, actorSubject,
            correlationId, "committed", CommercialReasonCodes.Eligible,
            reservation.LogicalBytes, usage, policyVersion ?? "commercial-1", committedAt));
    }

    private async Task RecordDecisionAsync(Guid organizationId, string actorSubject,
        string correlationId, string outcome, string reasonCode, long requestedBytes,
        OrganizationStorageUsage? usage, string policyVersion,
        CancellationToken cancellationToken)
    {
        database.CommercialAuditEvents.Add(Audit(organizationId, actorSubject,
            correlationId, outcome, reasonCode, requestedBytes, usage, policyVersion,
            timeProvider.GetUtcNow()));
        await database.SaveChangesAsync(cancellationToken);
    }

    private static CommercialAuditEvent Audit(Guid organizationId, string actorSubject,
        string correlationId, string outcome, string reasonCode, long requestedBytes,
        OrganizationStorageUsage? usage, string policyVersion, DateTimeOffset occurredAt) => new()
        {
            Id = Guid.CreateVersion7(occurredAt),
            OrganizationId = organizationId,
            ActorSubject = actorSubject,
            Action = outcome == "committed" ? "hosted_sync_commit" : "hosted_sync_begin",
            Outcome = outcome,
            ReasonCode = reasonCode,
            RequestedBytes = requestedBytes,
            ReservedBytes = usage?.ReservedBytes ?? 0,
            CommittedBytes = usage?.CommittedBytes ?? 0,
            CorrelationId = correlationId,
            PolicyVersion = policyVersion,
            OccurredAt = occurredAt
        };

    private static string LicenseStatus(CommercialLicenseState? value) => value switch
    {
        CommercialLicenseState.Pending => "pending",
        CommercialLicenseState.Active => "active",
        CommercialLicenseState.Refunded => "refunded",
        CommercialLicenseState.Revoked => "revoked",
        _ => "missing"
    };

    private static string SubscriptionStatus(ServiceSubscriptionState? value) => value switch
    {
        ServiceSubscriptionState.Trialing => "trialing",
        ServiceSubscriptionState.Incomplete => "incomplete",
        ServiceSubscriptionState.Active => "active",
        ServiceSubscriptionState.PastDue => "past_due",
        ServiceSubscriptionState.Unpaid => "unpaid",
        ServiceSubscriptionState.Paused => "paused",
        ServiceSubscriptionState.Canceled => "canceled",
        _ => "missing"
    };
}
