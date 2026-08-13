namespace ShowVault.Platform.Agents;

public sealed class DesktopCatalogScanCandidate
{
    private DesktopCatalogScanCandidate(
        Guid id,
        Guid scanId,
        Guid venueId,
        string candidateKey,
        string productName,
        string candidateType,
        string evidence,
        DateTimeOffset detectedAt)
    {
        Id = id;
        ScanId = scanId;
        VenueId = venueId;
        CandidateKey = candidateKey;
        ProductName = productName;
        CandidateType = candidateType;
        Evidence = evidence;
        DetectedAt = detectedAt;
    }

    public Guid Id { get; }
    public Guid ScanId { get; }
    public Guid VenueId { get; }
    public string CandidateKey { get; }
    public string ProductName { get; }
    public string CandidateType { get; }
    public string Evidence { get; }
    public DateTimeOffset DetectedAt { get; }

    public static DesktopCatalogScanCandidate Detected(
        Guid scanId,
        Guid venueId,
        string candidateKey,
        string productName,
        string candidateType,
        string evidence,
        DateTimeOffset detectedAt)
    {
        if (scanId == Guid.Empty || venueId == Guid.Empty)
        {
            throw new ArgumentException("Scan and venue IDs must not be empty.");
        }

        foreach (var value in new[] { candidateKey, productName, candidateType, evidence })
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
        }

        if (candidateKey.Length > 120 || productName.Length > 200 ||
            candidateType.Length > 80 || evidence.Length > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(candidateKey), "Candidate metadata exceeds its bound.");
        }

        return new(Guid.NewGuid(), scanId, venueId, candidateKey, productName,
            candidateType, evidence, detectedAt);
    }
}
