namespace ShowVault.Platform.Organizations;

public enum OrganizationInvitationState
{
    Pending,
    Accepted,
    Revoked,
    Expired
}

public sealed class OrganizationInvitation
{
    private OrganizationInvitation()
    {
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string DisplayLabel { get; private set; } = "";
    public OrganizationRole Role { get; private set; }
    public byte[] TokenDigest { get; private set; } = [];
    public string TokenKeyId { get; private set; } = "";
    public OrganizationInvitationState State { get; private set; }
    public string CreatedBySubject { get; private set; } = "";
    public Guid? AcceptedMembershipId { get; private set; }
    public string? AcceptedBySubject { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? TerminalAt { get; private set; }
    public long Revision { get; private set; }

    public static OrganizationInvitation Create(
        Guid organizationId,
        string displayLabel,
        OrganizationRole role,
        byte[] tokenDigest,
        string tokenKeyId,
        string createdBySubject,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException("Organization ID must not be empty.", nameof(organizationId));
        var normalizedLabel = displayLabel?.Trim() ?? "";
        if (normalizedLabel.Length is < 1 or > 80)
            throw new ArgumentException("Display label must contain 1 to 80 characters.", nameof(displayLabel));
        if (!role.IsNonOwner())
            throw new ArgumentException("Invitation role must be non-Owner.", nameof(role));
        if (tokenDigest is null || tokenDigest.Length != 32)
            throw new ArgumentException("Token digest must contain exactly 32 bytes.", nameof(tokenDigest));
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenKeyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBySubject);
        if (expiresAt <= createdAt)
            throw new ArgumentOutOfRangeException(nameof(expiresAt), "Expiry must be after creation.");

        return new OrganizationInvitation
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            DisplayLabel = normalizedLabel,
            Role = role,
            TokenDigest = tokenDigest.ToArray(),
            TokenKeyId = tokenKeyId.Trim(),
            State = OrganizationInvitationState.Pending,
            CreatedBySubject = createdBySubject.Trim(),
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            ExpiresAt = expiresAt,
            Revision = 1
        };
    }

    public void ObserveExpiry(DateTimeOffset now)
    {
        if (State == OrganizationInvitationState.Pending && now >= ExpiresAt)
            Terminate(OrganizationInvitationState.Expired, now);
    }

    public void Accept(Guid membershipId, string subject, long expectedRevision, DateTimeOffset now)
    {
        EnsurePending(expectedRevision, now);
        if (membershipId == Guid.Empty)
            throw new ArgumentException("Membership ID must not be empty.", nameof(membershipId));
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        AcceptedMembershipId = membershipId;
        AcceptedBySubject = subject.Trim();
        Terminate(OrganizationInvitationState.Accepted, now);
    }

    public void Revoke(long expectedRevision, DateTimeOffset now)
    {
        EnsurePending(expectedRevision, now);
        Terminate(OrganizationInvitationState.Revoked, now);
    }

    private void EnsurePending(long expectedRevision, DateTimeOffset now)
    {
        if (expectedRevision != Revision)
            throw new InvalidOperationException("The invitation revision is stale.");
        if (now < UpdatedAt)
            throw new ArgumentOutOfRangeException(nameof(now), "Updated time cannot move backwards.");
        ObserveExpiry(now);
        if (State != OrganizationInvitationState.Pending)
            throw new InvalidOperationException("The invitation is no longer pending.");
    }

    private void Terminate(OrganizationInvitationState state, DateTimeOffset now)
    {
        State = state;
        UpdatedAt = now;
        TerminalAt = now;
        Revision++;
    }
}
