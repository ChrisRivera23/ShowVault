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

    public static DesktopCatalogScan Complete(
        Guid id,
        Guid venueId,
        DateTimeOffset completedAt)
    {
        if (id == Guid.Empty || venueId == Guid.Empty)
        {
            throw new ArgumentException("Scan and venue IDs must not be empty.");
        }

        return new(id, venueId, completedAt);
    }
}
