namespace ShowVault.Platform.Venues;

public sealed record Venue(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string TimeZoneId,
    DateTimeOffset CreatedAt)
{
    public static Venue Create(Guid organizationId, string name, string timeZoneId)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization ID must not be empty.", nameof(organizationId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);

        var normalizedName = name.Trim();
        if (normalizedName.Length > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(name), "Name cannot exceed 200 characters.");
        }

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId.Trim(), out _))
        {
            throw new ArgumentException("Time zone ID is not recognized.", nameof(timeZoneId));
        }

        return new Venue(
            Guid.NewGuid(),
            organizationId,
            normalizedName,
            timeZoneId.Trim(),
            DateTimeOffset.UtcNow);
    }
}
