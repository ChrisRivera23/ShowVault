namespace ShowVault.Platform.Agents;

public sealed class DesktopCatalogScan
{
    private DesktopCatalogScan(Guid id, Guid venueId, DateTimeOffset completedAt)
    {
        Id = id;
        VenueId = venueId;
        CompletedAt = completedAt;
    }

    public Guid Id { get; }
    public Guid VenueId { get; }
    public DateTimeOffset CompletedAt { get; }

    public static DesktopCatalogScan Complete(Guid venueId, DateTimeOffset completedAt)
    {
        if (venueId == Guid.Empty)
        {
            throw new ArgumentException("Venue ID must not be empty.", nameof(venueId));
        }

        return new(Guid.CreateVersion7(completedAt), venueId, completedAt);
    }
}
