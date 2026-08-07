namespace ShowVault.Platform.Agents;

public sealed class VenueAgent
{
    private VenueAgent(
        Guid id,
        Guid venueId,
        string name,
        byte[] credentialHash,
        DateTimeOffset createdAt)
    {
        Id = id;
        VenueId = venueId;
        Name = name;
        CredentialHash = credentialHash;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }
    public Guid VenueId { get; }
    public string Name { get; }
    public byte[] CredentialHash { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public static VenueAgent Create(
        Guid venueId,
        string name,
        byte[] credentialHash,
        DateTimeOffset now)
    {
        if (venueId == Guid.Empty)
        {
            throw new ArgumentException("Venue ID must not be empty.", nameof(venueId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(credentialHash);
        var normalizedName = name.Trim();
        if (normalizedName.Length > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(name), "Name cannot exceed 200 characters.");
        }

        if (credentialHash.Length != 32)
        {
            throw new ArgumentException("Credential hash must be 32 bytes.", nameof(credentialHash));
        }

        return new VenueAgent(
            Guid.NewGuid(),
            venueId,
            normalizedName,
            credentialHash.ToArray(),
            now);
    }

    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;
}
