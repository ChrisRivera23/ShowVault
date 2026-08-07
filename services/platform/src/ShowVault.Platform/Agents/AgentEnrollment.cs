namespace ShowVault.Platform.Agents;

public sealed class AgentEnrollment
{
    private AgentEnrollment(
        Guid id,
        Guid venueId,
        byte[] secretHash,
        string createdBySubject,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        Id = id;
        VenueId = venueId;
        SecretHash = secretHash;
        CreatedBySubject = createdBySubject;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; }
    public Guid VenueId { get; }
    public byte[] SecretHash { get; }
    public string CreatedBySubject { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset ExpiresAt { get; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public static AgentEnrollment Create(
        Guid venueId,
        byte[] secretHash,
        string createdBySubject,
        DateTimeOffset now,
        TimeSpan lifetime)
    {
        if (venueId == Guid.Empty)
        {
            throw new ArgumentException("Venue ID must not be empty.", nameof(venueId));
        }

        ArgumentNullException.ThrowIfNull(secretHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBySubject);
        if (secretHash.Length != 32)
        {
            throw new ArgumentException("Secret hash must be 32 bytes.", nameof(secretHash));
        }

        if (lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime));
        }

        return new AgentEnrollment(
            Guid.NewGuid(),
            venueId,
            secretHash.ToArray(),
            createdBySubject.Trim(),
            now,
            now.Add(lifetime));
    }

    public bool CanBeConsumed(DateTimeOffset now) =>
        ConsumedAt is null && RevokedAt is null && now < ExpiresAt;

    public void Consume(DateTimeOffset now)
    {
        if (!CanBeConsumed(now))
        {
            throw new InvalidOperationException("Enrollment is no longer valid.");
        }

        ConsumedAt = now;
    }

    public void Revoke(DateTimeOffset now)
    {
        if (ConsumedAt is not null)
        {
            throw new InvalidOperationException("A consumed enrollment cannot be revoked.");
        }

        RevokedAt ??= now;
    }
}
