namespace ShowVault.Platform.Organizations;

public enum OrganizationRole
{
    Viewer,
    Technician,
    Manager,
    Administrator,
    Owner
}

public static class OrganizationRoleExtensions
{
    public static bool CanManageVenues(this OrganizationRole role) => role is
        OrganizationRole.Manager or
        OrganizationRole.Administrator or
        OrganizationRole.Owner;
}

public sealed record Membership(
    Guid Id,
    Guid OrganizationId,
    string IdentitySubject,
    OrganizationRole Role,
    DateTimeOffset CreatedAt)
{
    public static Membership Create(
        Guid organizationId,
        string identitySubject,
        OrganizationRole role)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization ID must not be empty.", nameof(organizationId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(identitySubject);

        return new Membership(
            Guid.NewGuid(),
            organizationId,
            identitySubject.Trim(),
            role,
            DateTimeOffset.UtcNow);
    }
}
