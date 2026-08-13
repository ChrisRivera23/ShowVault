namespace ShowVault.LocalEngine;

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
public sealed record HostedSyncFile(
    string RelativePath,
    long Size,
    string Sha256);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HostedSyncBeginRequest(
    HostedSyncManifest Manifest,
    string ManifestDigest);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HostedSyncBeginResponse(
    string SessionId,
    int MaximumChunkBytes,
    bool Completed);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HostedSyncFileStateResponse(long NextOffset);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
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

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HostedSyncObjectDigest(
    string RelativePath,
    long Size,
    string Sha256);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record HostedSyncEnvelope<T>(
    string Status,
    string Message,
    string CorrelationId,
    string Version,
    DateTimeOffset Timestamp,
    T Payload);
