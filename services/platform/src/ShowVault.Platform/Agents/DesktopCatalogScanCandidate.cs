namespace ShowVault.Platform.Agents;

public sealed class DesktopCatalogScanCandidate
{
    private DesktopCatalogScanCandidate(
        Guid id,
        Guid scanId,
        Guid venueId,
        string candidateKey,
        string pluginId,
        string productName,
        string candidateType,
        string evidence,
        DateTimeOffset detectedAt)
    {
        Id = id;
        ScanId = scanId;
        VenueId = venueId;
        CandidateKey = candidateKey;
        PluginId = pluginId;
        ProductName = productName;
        CandidateType = candidateType;
        Evidence = evidence;
        DetectedAt = detectedAt;
    }

    public Guid Id { get; }
    public Guid ScanId { get; }
    public Guid VenueId { get; }
    public string CandidateKey { get; }
    public string PluginId { get; }
    public string ProductName { get; }
    public string CandidateType { get; }
    public string Evidence { get; }
    public DateTimeOffset DetectedAt { get; }

    public static DesktopCatalogScanCandidate Detected(
        Guid scanId,
        Guid venueId,
        string candidateKey,
        string pluginId,
        string productName,
        string candidateType,
        string evidence,
        DateTimeOffset detectedAt)
    {
        if (scanId == Guid.Empty || venueId == Guid.Empty)
        {
            throw new ArgumentException("Scan and venue IDs must not be empty.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(candidateKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(productName);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateType);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidence);
        return new(
            Guid.NewGuid(), scanId, venueId, candidateKey, pluginId,
            productName, candidateType, evidence, detectedAt);
    }
}
