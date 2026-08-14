using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ShowVault.Api.Contracts;
using ShowVault.Api.Data;
using ShowVault.Platform.Commercial;
using ShowVault.Platform.Organizations;
using ShowVault.Platform.Support;

namespace ShowVault.Api.Support;

public enum SupportOverviewResultKind { Success, StaffUnavailable, TargetUnavailable, Failure }
public sealed record SupportOverviewResult(
    SupportOverviewResultKind Kind, SupportOrganizationOverview? Value = null,
    string ReasonCode = "support_request_failed");

public sealed class SupportOrganizationOverviewService(
    PlatformDbContext database,
    ICommercialPlanPolicyCatalog policies,
    TimeProvider timeProvider)
{
    private const string PolicyVersion = "support-v1";
    private const int MaximumAggregateRows = 10_000;

    public async Task<SupportOverviewResult> GetAsync(Guid organizationId,
        string issuer, string subject, string correlationId,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            await using var transaction = await database.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            try
            {
                var assignment = await database.SupportStaffAssignments.SingleOrDefaultAsync(value =>
                    value.IdentityIssuer == issuer && value.IdentitySubject == subject &&
                    value.Role == SupportStaffRole.SupportReader &&
                    value.State == SupportStaffAssignmentState.Active, cancellationToken);
                if (assignment is null)
                    return new(SupportOverviewResultKind.StaffUnavailable,
                        ReasonCode: "support_staff_unavailable");

                var target = await database.SupportOrganizationGrants
                    .Where(grant => grant.StaffAssignmentId == assignment.Id &&
                        grant.OrganizationId == organizationId &&
                        grant.State == SupportOrganizationGrantState.Active)
                    .Join(database.Organizations,
                        grant => grant.OrganizationId,
                        organization => organization.Id,
                        (_, organization) => new { organization.Id, organization.Name })
                    .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
                if (target is null)
                {
                    database.SupportAuditEvents.Add(SupportAuditEvent.Create(null, issuer, subject,
                        "support_overview_read", "denied", "support_target_unavailable",
                        correlationId, PolicyVersion, timeProvider.GetUtcNow()));
                    await database.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return new(SupportOverviewResultKind.TargetUnavailable,
                        ReasonCode: "support_target_unavailable");
                }

                var overview = await ProjectAsync(target.Id, target.Name, cancellationToken);
                database.SupportAuditEvents.Add(SupportAuditEvent.Create(target.Id, issuer, subject,
                    "support_overview_read", "allowed", "authorized", correlationId,
                    PolicyVersion, timeProvider.GetUtcNow()));
                await database.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new(SupportOverviewResultKind.Success, overview, "authorized");
            }
            catch (OperationCanceledException) { throw; }
            catch (DbUpdateConcurrencyException) when (attempt < 2)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                database.ChangeTracker.Clear();
            }
            catch (DbUpdateException) when (attempt < 2)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                database.ChangeTracker.Clear();
            }
            catch (PostgresException exception) when (
                exception.SqlState == PostgresErrorCodes.SerializationFailure && attempt < 2)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                database.ChangeTracker.Clear();
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return new(SupportOverviewResultKind.Failure);
            }
        }
        return new(SupportOverviewResultKind.Failure, ReasonCode: "serialization_exhausted");
    }

    private async Task<SupportOrganizationOverview> ProjectAsync(Guid organizationId,
        string displayName, CancellationToken cancellationToken)
    {
        if (displayName.Length is < 1 or > 200) throw new InvalidOperationException();
        var groupedMembers = await database.Memberships.AsNoTracking()
            .Where(value => value.OrganizationId == organizationId)
            .GroupBy(value => new { value.Role, value.State })
            .Select(group => new { group.Key.Role, group.Key.State, Count = group.LongCount() })
            .ToListAsync(cancellationToken);
        if (groupedMembers.Any(value => !Enum.IsDefined(value.Role) || !Enum.IsDefined(value.State)))
            throw new InvalidOperationException();
        var members = Enum.GetValues<OrganizationRole>().SelectMany(role =>
            Enum.GetValues<MembershipState>().Select(state => new SupportMemberCount(
                Role(role), MembershipStateName(state),
                groupedMembers.SingleOrDefault(value => value.Role == role && value.State == state)?.Count ?? 0)))
            .ToArray();

        var license = await database.CommercialLicenses.AsNoTracking()
            .SingleOrDefaultAsync(value => value.OrganizationId == organizationId, cancellationToken);
        var subscription = await database.ServiceSubscriptions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.OrganizationId == organizationId, cancellationToken);
        if (license is not null && !Enum.IsDefined(license.State) ||
            subscription is not null && !Enum.IsDefined(subscription.State))
            throw new InvalidOperationException();
        var usage = await database.OrganizationStorageUsages.AsNoTracking()
            .SingleOrDefaultAsync(value => value.OrganizationId == organizationId, cancellationToken);
        var entitlement = CommercialEntitlementEvaluator.Evaluate(
            license, subscription, timeProvider.GetUtcNow(), policies);

        var attentionRows = await database.BillingAttentions.AsNoTracking()
            .Where(value => value.OrganizationId == organizationId && value.ResolvedAt == null)
            .Select(value => new { value.ReasonCode, value.OpenedAt })
            .Take(MaximumAggregateRows + 1).ToListAsync(cancellationToken);
        if (attentionRows.Count > MaximumAggregateRows) throw new InvalidOperationException();
        var reasons = attentionRows.Select(value => value.ReasonCode).Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToArray();
        if (reasons.Length > 8 || reasons.Any(value => string.IsNullOrWhiteSpace(value) ||
            value.Length > 80 || value.Any(char.IsControl))) throw new InvalidOperationException();
        if (attentionRows.Count > 0)
            entitlement = new(false, CommercialReasonCodes.BillingAttention, entitlement.Policy);

        var committed = usage?.CommittedBytes ?? 0;
        var reserved = usage?.ReservedBytes ?? 0;
        var limit = entitlement.Policy?.LogicalStorageLimitBytes ?? 0;
        if (committed < 0 || reserved < 0 || limit < 0 ||
            checked(committed + reserved) > limit) throw new InvalidOperationException();

        var syncRows = await database.HostedSyncSessions.AsNoTracking()
            .Where(value => value.OrganizationId == organizationId)
            .Select(value => new { value.Status, value.UpdatedAt })
            .Take(MaximumAggregateRows + 1).ToListAsync(cancellationToken);
        if (syncRows.Count > MaximumAggregateRows) throw new InvalidOperationException();
        if (syncRows.Any(value => value.Status is not ("uploading" or "completed")))
            throw new InvalidOperationException();
        var syncCounts = new[] { "uploading", "completed" }.Select(status =>
            new SupportHostedSyncCount(status, checked((long)syncRows.Count(value =>
                value.Status == status)))).ToArray();
        DateTimeOffset? latestSync = syncRows.Count == 0 ? null :
            syncRows.Max(value => value.UpdatedAt).ToUniversalTime();
        var accountTimes = await database.AccountAuditEvents.AsNoTracking()
            .Where(value => value.OrganizationId == organizationId)
            .Select(value => value.OccurredAt).Take(MaximumAggregateRows + 1)
            .ToListAsync(cancellationToken);
        var commercialTimes = await database.CommercialAuditEvents.AsNoTracking()
            .Where(value => value.OrganizationId == organizationId)
            .Select(value => value.OccurredAt).Take(MaximumAggregateRows + 1)
            .ToListAsync(cancellationToken);
        if (accountTimes.Count > MaximumAggregateRows ||
            commercialTimes.Count > MaximumAggregateRows) throw new InvalidOperationException();
        DateTimeOffset? lastAccount = accountTimes.Count == 0 ? null : accountTimes.Max();
        DateTimeOffset? lastCommercial = commercialTimes.Count == 0 ? null : commercialTimes.Max();

        return new(organizationId, displayName, members,
            new(subscription?.PlanCode, LicenseState(license?.State),
                SubscriptionState(subscription?.State), Utc(subscription?.CurrentPeriodEndsAt),
                Utc(subscription?.GraceEndsAt), entitlement.Eligible, entitlement.ReasonCode,
                committed, reserved, limit),
            new(checked((long)attentionRows.Count), reasons,
                Utc(attentionRows.Count == 0 ? null : attentionRows.Min(value => value.OpenedAt))),
            new(syncCounts, latestSync),
            new(Utc(lastAccount), Utc(lastCommercial)));
    }

    private static DateTimeOffset? Utc(DateTimeOffset? value) => value?.ToUniversalTime();
    private static string Role(OrganizationRole value) => value.ToString().ToLowerInvariant();
    private static string MembershipStateName(MembershipState value) => value.ToString().ToLowerInvariant();
    private static string LicenseState(CommercialLicenseState? value) => value switch
    {
        null => "missing",
        CommercialLicenseState.Pending => "pending",
        CommercialLicenseState.Active => "active",
        CommercialLicenseState.Refunded => "refunded",
        CommercialLicenseState.Revoked => "revoked",
        _ => throw new InvalidOperationException()
    };
    private static string SubscriptionState(ServiceSubscriptionState? value) => value switch
    {
        null => "missing",
        ServiceSubscriptionState.Incomplete => "incomplete",
        ServiceSubscriptionState.Trialing => "trialing",
        ServiceSubscriptionState.Active => "active",
        ServiceSubscriptionState.PastDue => "past_due",
        ServiceSubscriptionState.Unpaid => "unpaid",
        ServiceSubscriptionState.Paused => "paused",
        ServiceSubscriptionState.Canceled => "canceled",
        _ => throw new InvalidOperationException()
    };
}
