namespace ShowVault.Api.Contracts;

public sealed record BeginHostedSyncRequest(byte[] RemoteManifest);

public sealed record HostedSyncFileStateRequest(string RelativePath);

public sealed record HostedSyncFileStateResponse(long UploadedLength);

public sealed record AppendHostedSyncChunkRequest(
    string RelativePath,
    long Offset,
    byte[] Bytes);

public sealed record HostedSyncReceiptResponse(
    string PackageId,
    string RemoteManifestSha256,
    DateTimeOffset CompletedAt);
