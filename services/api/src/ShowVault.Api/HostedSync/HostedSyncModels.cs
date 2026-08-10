namespace ShowVault.Api.HostedSync;

public sealed record HostedSyncDescriptor(string RelativePath, long Size, string Sha256);

public sealed record ValidatedHostedManifest(
    string PackageId,
    string RemoteManifestSha256,
    byte[] Bytes,
    IReadOnlyDictionary<string, HostedSyncDescriptor> Files);

public sealed record HostedSyncReceipt(
    string PackageId,
    string RemoteManifestSha256,
    DateTimeOffset CompletedAt);
