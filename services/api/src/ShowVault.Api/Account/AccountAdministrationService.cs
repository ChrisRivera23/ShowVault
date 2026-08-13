using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShowVault.Api.Authorization;
using ShowVault.Api.Contracts;
using ShowVault.Api.Data;
using ShowVault.Api.Security;
using ShowVault.Platform.Organizations;

namespace ShowVault.Api.Account;

public enum AccountResultKind
{
    Success,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
    BadRequest,
    InvitationUnavailable,
    FeatureUnavailable
}

public sealed record AccountResult<T>(AccountResultKind Kind, T? Value = default)
{
    public static AccountResult<T> Success(T value) => new(AccountResultKind.Success, value);
    public static AccountResult<T> Failure(AccountResultKind kind) => new(kind);
}

internal readonly record struct AcceptedInvitationObservation(
    bool IsConclusive,
    AccountResult<AcceptedAccountInvitation> Result);

public sealed class AccountAdministrationService(
    PlatformDbContext database,
    MembershipAuthorizationService authorization,
    MembershipStepUpAuthorization stepUp,
    InvitationTokenService tokens,
    IOptions<AccountInvitationOptions> invitationOptions,
    TimeProvider timeProvider)
{
    private const string PolicyVersion = "account-v1";
    private static readonly TimeSpan[] AcceptedInvitationResumeDelays =
    [
        TimeSpan.FromMilliseconds(10),
        TimeSpan.FromMilliseconds(20),
        TimeSpan.FromMilliseconds(40),
        TimeSpan.FromMilliseconds(80),
        TimeSpan.FromMilliseconds(160)
    ];

    public async Task<AccountResult<IReadOnlyList<AccountMemberSummary>>> ListMembersAsync(
        Guid organizationId, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var access = await OwnerAccessAsync(organizationId, user, false, cancellationToken);
        if (access != AccountResultKind.Success)
            return AccountResult<IReadOnlyList<AccountMemberSummary>>.Failure(access);
        var subject = HumanIdentity.Subject(user)!;
        var members = await database.Memberships.AsNoTracking()
            .Where(member => member.OrganizationId == organizationId)
            .OrderBy(member => member.DisplayLabel)
            .ThenBy(member => member.CreatedAt)
            .ToListAsync(cancellationToken);
        return AccountResult<IReadOnlyList<AccountMemberSummary>>.Success(
            members.Select(member => Summary(member, subject)).ToArray());
    }

    public async Task<AccountResult<IReadOnlyList<AccountInvitationSummary>>> ListInvitationsAsync(
        Guid organizationId, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var access = await OwnerAccessAsync(organizationId, user, false, cancellationToken);
        if (access != AccountResultKind.Success)
            return AccountResult<IReadOnlyList<AccountInvitationSummary>>.Failure(access);
        var invitations = await database.OrganizationInvitations
            .Where(value => value.OrganizationId == organizationId)
            .OrderByDescending(value => value.CreatedAt)
            .ToListAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var changed = false;
        foreach (var invitation in invitations)
        {
            var revision = invitation.Revision;
            invitation.ObserveExpiry(now);
            changed |= revision != invitation.Revision;
        }
        if (changed)
        {
            try { await database.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateConcurrencyException)
            {
                database.ChangeTracker.Clear();
                invitations = await database.OrganizationInvitations.AsNoTracking()
                    .Where(value => value.OrganizationId == organizationId)
                    .OrderByDescending(value => value.CreatedAt)
                    .ToListAsync(cancellationToken);
            }
        }
        return AccountResult<IReadOnlyList<AccountInvitationSummary>>.Success(
            invitations.Select(Summary).ToArray());
    }

    public async Task<AccountResult<CreatedAccountInvitation>> CreateInvitationAsync(
        Guid organizationId, string displayLabel, OrganizationRole role,
        ClaimsPrincipal user, string correlationId, CancellationToken cancellationToken)
    {
        var access = await OwnerAccessAsync(organizationId, user, true, cancellationToken);
        if (access != AccountResultKind.Success)
            return AccountResult<CreatedAccountInvitation>.Failure(access);
        if (!await HasCompleteKeyRingAsync(cancellationToken))
            return AccountResult<CreatedAccountInvitation>.Failure(AccountResultKind.FeatureUnavailable);

        var now = timeProvider.GetUtcNow();
        var issued = tokens.Issue();
        OrganizationInvitation invitation;
        try
        {
            invitation = OrganizationInvitation.Create(
                organizationId, displayLabel, role, issued.Digest, issued.KeyId,
                HumanIdentity.Subject(user)!, now,
                now.AddHours(invitationOptions.Value.LifetimeHours));
        }
        catch (ArgumentException)
        {
            return AccountResult<CreatedAccountInvitation>.Failure(AccountResultKind.BadRequest);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        database.OrganizationInvitations.Add(invitation);
        database.AccountAuditEvents.Add(Audit(invitation.OrganizationId,
            HumanIdentity.Subject(user)!, "invitation", invitation.Id, "invitation_create",
            correlationId, now));
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return AccountResult<CreatedAccountInvitation>.Failure(AccountResultKind.Conflict);
        }
        var summary = Summary(invitation);
        return AccountResult<CreatedAccountInvitation>.Success(new(
            summary.Id, summary.DisplayLabel, summary.Role, summary.State,
            summary.CreatedAt, summary.UpdatedAt, summary.ExpiresAt,
            summary.Revision, issued.Code));
    }

    public async Task<AccountResult<AccountInvitationSummary>> RevokeInvitationAsync(
        Guid organizationId, Guid invitationId, ClaimsPrincipal user,
        string correlationId, CancellationToken cancellationToken)
    {
        var access = await OwnerAccessAsync(organizationId, user, true, cancellationToken);
        if (access != AccountResultKind.Success)
            return AccountResult<AccountInvitationSummary>.Failure(access);
        var invitation = await database.OrganizationInvitations.SingleOrDefaultAsync(value =>
            value.Id == invitationId && value.OrganizationId == organizationId,
            cancellationToken);
        if (invitation is null)
            return AccountResult<AccountInvitationSummary>.Failure(AccountResultKind.NotFound);
        var now = timeProvider.GetUtcNow();
        invitation.ObserveExpiry(now);
        if (invitation.State == OrganizationInvitationState.Expired)
        {
            try { await database.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateConcurrencyException) { database.ChangeTracker.Clear(); }
            return AccountResult<AccountInvitationSummary>.Failure(AccountResultKind.Conflict);
        }
        try { invitation.Revoke(invitation.Revision, now); }
        catch (InvalidOperationException)
        {
            return AccountResult<AccountInvitationSummary>.Failure(AccountResultKind.Conflict);
        }
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        database.AccountAuditEvents.Add(Audit(organizationId, HumanIdentity.Subject(user)!,
            "invitation", invitation.Id, "invitation_revoke", correlationId, now));
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return AccountResult<AccountInvitationSummary>.Failure(AccountResultKind.Conflict);
        }
        return AccountResult<AccountInvitationSummary>.Success(Summary(invitation));
    }

    public async Task<AccountResult<AcceptedAccountInvitation>> AcceptInvitationAsync(
        string code, ClaimsPrincipal user, string correlationId,
        CancellationToken cancellationToken)
    {
        var subject = HumanIdentity.Subject(user);
        if (subject is null)
            return AccountResult<AcceptedAccountInvitation>.Failure(AccountResultKind.Unauthorized);
        if (HumanIdentity.IsPersonalBeta(user))
            return AccountResult<AcceptedAccountInvitation>.Failure(AccountResultKind.Forbidden);
        var candidates = tokens.CandidateDigests(code);
        if (candidates.Count == 0)
            return AccountResult<AcceptedAccountInvitation>.Failure(
                AccountResultKind.InvitationUnavailable);
        if (!await HasCompleteKeyRingAsync(cancellationToken))
            return AccountResult<AcceptedAccountInvitation>.Failure(AccountResultKind.FeatureUnavailable);

        OrganizationInvitation? invitation = null;
        foreach (var candidate in candidates)
        {
            invitation = await database.OrganizationInvitations.SingleOrDefaultAsync(value =>
                value.TokenKeyId == candidate.KeyId &&
                value.TokenDigest.SequenceEqual(candidate.Digest), cancellationToken);
            if (invitation is not null) break;
        }
        if (invitation is null)
            return AccountResult<AcceptedAccountInvitation>.Failure(
                AccountResultKind.InvitationUnavailable);

        var existing = await database.Memberships.SingleOrDefaultAsync(value =>
            value.OrganizationId == invitation.OrganizationId &&
            value.IdentitySubject == subject, cancellationToken);
        if (invitation.State == OrganizationInvitationState.Accepted || existing is not null)
        {
            // The winning transaction can commit between the invitation and membership
            // queries. Re-observe both records together instead of pairing a stale pending
            // invitation with the winner's newly visible membership and denying the retry.
            database.ChangeTracker.Clear();
            return await ResumeAcceptedInvitationAsync(
                invitation.Id, subject, cancellationToken);
        }

        var now = timeProvider.GetUtcNow();
        invitation.ObserveExpiry(now);
        if (invitation.State != OrganizationInvitationState.Pending)
        {
            if (invitation.State == OrganizationInvitationState.Expired)
            {
                try { await database.SaveChangesAsync(cancellationToken); }
                catch (DbUpdateConcurrencyException) { database.ChangeTracker.Clear(); }
            }
            return AccountResult<AcceptedAccountInvitation>.Failure(
                AccountResultKind.InvitationUnavailable);
        }
        var membership = Membership.Create(invitation.OrganizationId, subject,
            invitation.Role, now, invitation.DisplayLabel);
        try { invitation.Accept(membership.Id, subject, invitation.Revision, now); }
        catch (InvalidOperationException)
        {
            return AccountResult<AcceptedAccountInvitation>.Failure(
                AccountResultKind.InvitationUnavailable);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        database.Memberships.Add(membership);
        database.AccountAuditEvents.Add(Audit(invitation.OrganizationId, subject,
            "invitation", invitation.Id, "invitation_accept", correlationId, now));
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            database.ChangeTracker.Clear();
            return await ResumeAcceptedInvitationAsync(
                invitation.Id, subject, cancellationToken);
        }
        return AccountResult<AcceptedAccountInvitation>.Success(
            new AcceptedAccountInvitation(Summary(membership, subject)));
    }

    public async Task<AccountResult<AccountMemberSummary>> MutateMemberAsync(
        Guid organizationId, Guid membershipId, string action, long expectedRevision,
        OrganizationRole? role, ClaimsPrincipal user, string correlationId,
        CancellationToken cancellationToken)
    {
        var access = await OwnerAccessAsync(organizationId, user, true, cancellationToken);
        if (access != AccountResultKind.Success)
            return AccountResult<AccountMemberSummary>.Failure(access);
        var membership = await database.Memberships.SingleOrDefaultAsync(value =>
            value.Id == membershipId && value.OrganizationId == organizationId,
            cancellationToken);
        if (membership is null)
            return AccountResult<AccountMemberSummary>.Failure(AccountResultKind.NotFound);
        var now = timeProvider.GetUtcNow();
        try
        {
            switch (action)
            {
                case "change_role" when role is not null:
                    membership.ChangeRole(role.Value, expectedRevision, now);
                    break;
                case "suspend" when role is null:
                    membership.Suspend(expectedRevision, now);
                    break;
                case "restore" when role is null:
                    membership.Restore(expectedRevision, now);
                    break;
                case "revoke" when role is null:
                    membership.Revoke(expectedRevision, now);
                    break;
                default:
                    return AccountResult<AccountMemberSummary>.Failure(AccountResultKind.Conflict);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or
            ArgumentOutOfRangeException)
        {
            return AccountResult<AccountMemberSummary>.Failure(AccountResultKind.Conflict);
        }
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        database.AccountAuditEvents.Add(Audit(organizationId, HumanIdentity.Subject(user)!,
            "membership", membership.Id, $"membership_{action}", correlationId, now));
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return AccountResult<AccountMemberSummary>.Failure(AccountResultKind.Conflict);
        }
        return AccountResult<AccountMemberSummary>.Success(
            Summary(membership, HumanIdentity.Subject(user)!));
    }

    private async Task<AccountResultKind> OwnerAccessAsync(
        Guid organizationId, ClaimsPrincipal user, bool sensitive,
        CancellationToken cancellationToken)
    {
        if (HumanIdentity.Subject(user) is null) return AccountResultKind.Unauthorized;
        if (HumanIdentity.IsPersonalBeta(user)) return AccountResultKind.Forbidden;
        if (!await authorization.IsOwnerAsync(organizationId, user, cancellationToken))
            return AccountResultKind.Forbidden;
        if (sensitive && !stepUp.Evaluate(user).Authorized)
            return AccountResultKind.Forbidden;
        return AccountResultKind.Success;
    }

    private async Task<bool> HasCompleteKeyRingAsync(CancellationToken cancellationToken)
    {
        if (!tokens.IsAvailable) return false;
        var keyIds = tokens.ConfiguredKeyIds;
        var now = timeProvider.GetUtcNow();
        // SQLite stores DateTimeOffset values as text and cannot translate ordering
        // comparisons for them. Keep the state filter server-side, then apply the
        // expiry check to the small set of pending invitation key references.
        var pendingKeys = await database.OrganizationInvitations.AsNoTracking()
            .Where(invitation => invitation.State == OrganizationInvitationState.Pending)
            .Select(invitation => new { invitation.TokenKeyId, invitation.ExpiresAt })
            .ToListAsync(cancellationToken);
        var requiredKeyIds = pendingKeys
            .Where(invitation => invitation.ExpiresAt > now)
            .Select(invitation => invitation.TokenKeyId)
            .Distinct(StringComparer.Ordinal);
        return requiredKeyIds.All(keyIds.Contains);
    }

    private async Task<AccountResult<AcceptedAccountInvitation>> ResumeAcceptedInvitationAsync(
        Guid invitationId, string subject, CancellationToken cancellationToken)
    {
        return await RetryAcceptedInvitationObservationAsync(async token =>
        {
            var winner = await database.OrganizationInvitations.AsNoTracking()
                .SingleOrDefaultAsync(value => value.Id == invitationId, token);
            if (winner is null || winner.State == OrganizationInvitationState.Pending)
                return InconclusiveInvitationObservation();
            if (winner.State != OrganizationInvitationState.Accepted ||
                winner.AcceptedBySubject != subject ||
                winner.AcceptedMembershipId is not { } membershipId)
                return ConclusiveInvitationUnavailable();
            var membership = await database.Memberships.AsNoTracking()
                .SingleOrDefaultAsync(value =>
                    value.Id == membershipId && value.OrganizationId == winner.OrganizationId &&
                    value.IdentitySubject == subject, token);
            return membership is null
                ? InconclusiveInvitationObservation()
                : new AcceptedInvitationObservation(true,
                    AccountResult<AcceptedAccountInvitation>.Success(
                        new AcceptedAccountInvitation(Summary(membership, subject))));
        }, timeProvider, AcceptedInvitationResumeDelays, cancellationToken);
    }

    internal static async Task<AccountResult<AcceptedAccountInvitation>>
        RetryAcceptedInvitationObservationAsync(
            Func<CancellationToken, Task<AcceptedInvitationObservation>> observe,
            TimeProvider timeProvider,
            IReadOnlyList<TimeSpan> retryDelays,
            CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            var observation = await observe(cancellationToken);
            if (observation.IsConclusive || attempt == retryDelays.Count)
                return observation.Result;
            await Task.Delay(retryDelays[attempt], timeProvider, cancellationToken);
        }
    }

    private static AcceptedInvitationObservation InconclusiveInvitationObservation() =>
        new(false, AccountResult<AcceptedAccountInvitation>.Failure(
            AccountResultKind.InvitationUnavailable));

    private static AcceptedInvitationObservation ConclusiveInvitationUnavailable() =>
        new(true, AccountResult<AcceptedAccountInvitation>.Failure(
            AccountResultKind.InvitationUnavailable));

    private static AccountMemberSummary Summary(Membership member, string currentSubject) => new(
        member.Id, member.DisplayLabel, Lower(member.Role), Lower(member.State),
        member.IdentitySubject == currentSubject, member.CreatedAt, member.UpdatedAt,
        member.Revision);

    private static AccountInvitationSummary Summary(OrganizationInvitation invitation) => new(
        invitation.Id, invitation.DisplayLabel, Lower(invitation.Role), Lower(invitation.State),
        invitation.CreatedAt, invitation.UpdatedAt, invitation.ExpiresAt, invitation.Revision);

    private static string Lower<T>(T value) where T : struct, Enum =>
        value.ToString().ToLowerInvariant();

    private static AccountAuditEvent Audit(Guid organizationId, string actor,
        string targetType, Guid targetId, string action, string correlationId,
        DateTimeOffset occurredAt) => AccountAuditEvent.Create(
            organizationId, actor, targetType, targetId, action, "success", "authorized",
            correlationId, PolicyVersion, occurredAt);
}
