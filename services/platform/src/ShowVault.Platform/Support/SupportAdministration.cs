namespace ShowVault.Platform.Support;

public enum SupportStaffRole
{
    SupportReader
}

public enum SupportStaffAssignmentState
{
    Active,
    Suspended,
    Revoked
}

public enum SupportOrganizationGrantState
{
    Active,
    Revoked
}

public sealed class SupportStaffAssignment
{
    private SupportStaffAssignment()
    {
    }

    public Guid Id { get; private set; }
    public string IdentityIssuer { get; private set; } = "";
    public string IdentitySubject { get; private set; } = "";
    public SupportStaffRole Role { get; private set; }
    public SupportStaffAssignmentState State { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long Revision { get; private set; }

    public static SupportStaffAssignment Create(
        string identityIssuer,
        string identitySubject,
        DateTimeOffset createdAt) => new()
        {
            Id = Guid.NewGuid(),
            IdentityIssuer = SupportIdentity.NormalizeIssuer(identityIssuer),
            IdentitySubject = SupportIdentity.NormalizeSubject(identitySubject),
            Role = SupportStaffRole.SupportReader,
            State = SupportStaffAssignmentState.Active,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            Revision = 1
        };

    public void Suspend(long expectedRevision, DateTimeOffset updatedAt)
    {
        EnsureMutable(expectedRevision, updatedAt);
        if (State != SupportStaffAssignmentState.Active)
            throw new InvalidOperationException("Only an active staff assignment can be suspended.");
        State = SupportStaffAssignmentState.Suspended;
        Advance(updatedAt);
    }

    public void Restore(long expectedRevision, DateTimeOffset updatedAt)
    {
        EnsureMutable(expectedRevision, updatedAt);
        if (State != SupportStaffAssignmentState.Suspended)
            throw new InvalidOperationException("Only a suspended staff assignment can be restored.");
        State = SupportStaffAssignmentState.Active;
        Advance(updatedAt);
    }

    public void Revoke(long expectedRevision, DateTimeOffset updatedAt)
    {
        EnsureMutable(expectedRevision, updatedAt);
        if (State is not (SupportStaffAssignmentState.Active or
            SupportStaffAssignmentState.Suspended))
            throw new InvalidOperationException("Only an active or suspended staff assignment can be revoked.");
        State = SupportStaffAssignmentState.Revoked;
        Advance(updatedAt);
    }

    private void EnsureMutable(long expectedRevision, DateTimeOffset updatedAt)
    {
        if (expectedRevision != Revision)
            throw new InvalidOperationException("The staff assignment revision is stale.");
        if (updatedAt < UpdatedAt)
            throw new ArgumentOutOfRangeException(nameof(updatedAt), "Updated time cannot move backwards.");
        if (State == SupportStaffAssignmentState.Revoked)
            throw new InvalidOperationException("A revoked staff assignment is terminal.");
    }

    private void Advance(DateTimeOffset updatedAt)
    {
        UpdatedAt = updatedAt;
        Revision++;
    }
}

public sealed class SupportOrganizationGrant
{
    private SupportOrganizationGrant()
    {
    }

    public Guid Id { get; private set; }
    public Guid StaffAssignmentId { get; private set; }
    public Guid OrganizationId { get; private set; }
    public SupportOrganizationGrantState State { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long Revision { get; private set; }

    public static SupportOrganizationGrant Create(
        Guid staffAssignmentId,
        Guid organizationId,
        DateTimeOffset createdAt)
    {
        if (staffAssignmentId == Guid.Empty)
            throw new ArgumentException("Staff assignment ID must not be empty.", nameof(staffAssignmentId));
        if (organizationId == Guid.Empty)
            throw new ArgumentException("Organization ID must not be empty.", nameof(organizationId));
        return new()
        {
            Id = Guid.NewGuid(),
            StaffAssignmentId = staffAssignmentId,
            OrganizationId = organizationId,
            State = SupportOrganizationGrantState.Active,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            Revision = 1
        };
    }

    public void Revoke(long expectedRevision, DateTimeOffset updatedAt)
    {
        if (expectedRevision != Revision)
            throw new InvalidOperationException("The organization grant revision is stale.");
        if (updatedAt < UpdatedAt)
            throw new ArgumentOutOfRangeException(nameof(updatedAt), "Updated time cannot move backwards.");
        if (State == SupportOrganizationGrantState.Revoked)
            throw new InvalidOperationException("A revoked organization grant is terminal.");
        State = SupportOrganizationGrantState.Revoked;
        UpdatedAt = updatedAt;
        Revision++;
    }
}

public sealed class SupportAuditEvent
{
    private SupportAuditEvent()
    {
    }

    public Guid Id { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public string ActorIssuer { get; private set; } = "";
    public string ActorSubject { get; private set; } = "";
    public string Action { get; private set; } = "";
    public string Outcome { get; private set; } = "";
    public string ReasonCode { get; private set; } = "";
    public string CorrelationId { get; private set; } = "";
    public string PolicyVersion { get; private set; } = "";
    public DateTimeOffset OccurredAt { get; private set; }

    public static SupportAuditEvent Create(
        Guid? organizationId,
        string actorIssuer,
        string actorSubject,
        string action,
        string outcome,
        string reasonCode,
        string correlationId,
        string policyVersion,
        DateTimeOffset occurredAt)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException("Organization ID must be null or non-empty.", nameof(organizationId));
        return new()
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            ActorIssuer = SupportIdentity.NormalizeIssuer(actorIssuer),
            ActorSubject = SupportIdentity.NormalizeSubject(actorSubject),
            Action = SupportIdentity.Bounded(action, 80, nameof(action)),
            Outcome = SupportIdentity.Bounded(outcome, 32, nameof(outcome)),
            ReasonCode = SupportIdentity.Bounded(reasonCode, 80, nameof(reasonCode)),
            CorrelationId = SupportIdentity.Bounded(correlationId, 100, nameof(correlationId)),
            PolicyVersion = SupportIdentity.Bounded(policyVersion, 80, nameof(policyVersion)),
            OccurredAt = occurredAt
        };
    }
}

internal static class SupportIdentity
{
    public static string NormalizeIssuer(string value)
    {
        var normalized = Bounded(value, 255, nameof(value));
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var issuer) ||
            issuer.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(issuer.UserInfo) ||
            !string.IsNullOrEmpty(issuer.Query) ||
            !string.IsNullOrEmpty(issuer.Fragment))
            throw new ArgumentException("Identity issuer must be an absolute HTTPS URI without user info, query, or fragment.", nameof(value));
        var canonical = issuer.AbsoluteUri;
        if (canonical.Length > 255)
            throw new ArgumentException("Identity issuer must not exceed 255 characters.", nameof(value));
        return canonical;
    }

    public static string NormalizeSubject(string value) =>
        Bounded(value, 255, nameof(value));

    public static string Bounded(string value, int maximum, string parameter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameter);
        var normalized = value.Trim();
        if (normalized.Length > maximum || normalized.Any(char.IsControl))
            throw new ArgumentException($"Value must contain at most {maximum} non-control characters.", parameter);
        return normalized;
    }
}
