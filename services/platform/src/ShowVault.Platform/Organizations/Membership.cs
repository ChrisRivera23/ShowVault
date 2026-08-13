namespace ShowVault.Platform.Organizations;

public enum OrganizationRole
{
    Viewer,
    Technician,
    Manager,
    Administrator,
    Owner
}

public enum MembershipState
{
    Active,
    Suspended,
    Revoked
}

public static class OrganizationRoleExtensions
{
    public static bool CanManageVenues(this OrganizationRole role) => role is
        OrganizationRole.Manager or
        OrganizationRole.Administrator or
        OrganizationRole.Owner;

    public static bool IsNonOwner(this OrganizationRole role) => role is
        OrganizationRole.Viewer or
        OrganizationRole.Technician or
        OrganizationRole.Manager or
        OrganizationRole.Administrator;
}

public sealed class Membership
{
    private Membership()
    {
    }

    private Membership(
        Guid id,
        Guid organizationId,
        string identitySubject,
        string? displayLabel,
        OrganizationRole role,
        MembershipState state,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        long revision)
    {
        Id = id;
        OrganizationId = organizationId;
        IdentitySubject = identitySubject;
        DisplayLabel = displayLabel;
        Role = role;
        State = state;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        Revision = revision;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string IdentitySubject { get; private set; } = "";
    public string? DisplayLabel { get; private set; }
    public OrganizationRole Role { get; private set; }
    public MembershipState State { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long Revision { get; private set; }

    public static Membership Create(
        Guid organizationId,
        string identitySubject,
        OrganizationRole role,
        DateTimeOffset createdAt,
        string? displayLabel = null)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException("Organization ID must not be empty.", nameof(organizationId));
        ArgumentException.ThrowIfNullOrWhiteSpace(identitySubject);
        var normalizedSubject = identitySubject.Trim();
        if (normalizedSubject.Length > 255)
            throw new ArgumentException("Identity subject must not exceed 255 characters.", nameof(identitySubject));

        return new Membership(
            Guid.NewGuid(),
            organizationId,
            normalizedSubject,
            NormalizeLabel(displayLabel),
            role,
            MembershipState.Active,
            createdAt,
            createdAt,
            1);
    }

    public void ChangeRole(OrganizationRole role, long expectedRevision, DateTimeOffset updatedAt)
    {
        EnsureMutable(expectedRevision, updatedAt);
        if (Role == OrganizationRole.Owner || !role.IsNonOwner())
            throw new InvalidOperationException("Owner membership cannot be changed in this milestone.");
        if (Role == role)
            throw new InvalidOperationException("The membership already has that role.");
        Role = role;
        Advance(updatedAt);
    }

    public void Suspend(long expectedRevision, DateTimeOffset updatedAt)
    {
        EnsureMutable(expectedRevision, updatedAt);
        EnsureNonOwner();
        if (State != MembershipState.Active)
            throw new InvalidOperationException("Only an active membership can be suspended.");
        State = MembershipState.Suspended;
        Advance(updatedAt);
    }

    public void Restore(long expectedRevision, DateTimeOffset updatedAt)
    {
        EnsureMutable(expectedRevision, updatedAt);
        EnsureNonOwner();
        if (State != MembershipState.Suspended)
            throw new InvalidOperationException("Only a suspended membership can be restored.");
        State = MembershipState.Active;
        Advance(updatedAt);
    }

    public void Revoke(long expectedRevision, DateTimeOffset updatedAt)
    {
        EnsureMutable(expectedRevision, updatedAt);
        EnsureNonOwner();
        if (State is not (MembershipState.Active or MembershipState.Suspended))
            throw new InvalidOperationException("Only an active or suspended membership can be revoked.");
        State = MembershipState.Revoked;
        Advance(updatedAt);
    }

    private void EnsureMutable(long expectedRevision, DateTimeOffset updatedAt)
    {
        if (expectedRevision != Revision)
            throw new InvalidOperationException("The membership revision is stale.");
        if (updatedAt < UpdatedAt)
            throw new ArgumentOutOfRangeException(nameof(updatedAt), "Updated time cannot move backwards.");
        if (State == MembershipState.Revoked)
            throw new InvalidOperationException("A revoked membership is terminal.");
    }

    private void EnsureNonOwner()
    {
        if (Role == OrganizationRole.Owner)
            throw new InvalidOperationException("Owner membership cannot be changed in this milestone.");
    }

    private void Advance(DateTimeOffset updatedAt)
    {
        UpdatedAt = updatedAt;
        Revision++;
    }

    private static string? NormalizeLabel(string? displayLabel)
    {
        if (displayLabel is null)
            return null;
        var normalized = displayLabel.Trim();
        if (normalized.Length is < 1 or > 80)
            throw new ArgumentException("Display label must contain 1 to 80 characters.", nameof(displayLabel));
        return normalized;
    }
}
