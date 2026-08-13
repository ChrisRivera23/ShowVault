namespace ShowVault.Api.HostedSync;

using System.Text.Json.Serialization;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HostedSyncManifest(
    string FormatVersion,
    string RecoveryPointId,
    string ManifestSha256,
    string CandidateKey,
    string PluginId,
    DateTimeOffset CreatedAt,
    int FileCount,
    long TotalBytes,
    IReadOnlyList<HostedSyncFile> Files);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HostedSyncFile(string RelativePath, long Size, string Sha256);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HostedSyncBeginRequest(
    HostedSyncManifest Manifest,
    string ManifestDigest);

public sealed record HostedSyncBeginResponse(
    string SessionId,
    int MaximumChunkBytes,
    bool Completed);

public sealed record HostedSyncFileStateResponse(long NextOffset);

public sealed record HostedSyncReceipt(
    string FormatVersion,
    Guid OrganizationId,
    Guid VenueId,
    string RecoveryPointId,
    string ManifestDigest,
    int FileCount,
    long TotalBytes,
    IReadOnlyList<HostedSyncObjectDigest> Objects,
    DateTimeOffset CompletedAt);

public sealed record HostedSyncObjectDigest(
    string RelativePath,
    long Size,
    string Sha256);

public sealed class HostedSyncSession
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid VenueId { get; set; }
    public string RecoveryPointId { get; set; } = "";
    public string ManifestDigest { get; set; } = "";
    public string ManifestJson { get; set; } = "";
    public string Status { get; set; } = "uploading";
    public string? ReceiptJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public long Revision { get; set; }
}
