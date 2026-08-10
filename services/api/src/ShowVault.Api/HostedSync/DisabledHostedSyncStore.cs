namespace ShowVault.Api.HostedSync;

public sealed class DisabledHostedSyncStore : IHostedSyncStore
{
    private static HostedSyncUnavailableException Unavailable() => new(
        "Hosted synchronization storage is disabled.");

    public Task<HostedSyncReceipt?> GetReceiptAsync(
        Guid organizationId, Guid venueId, string packageId,
        CancellationToken cancellationToken) => throw Unavailable();

    public Task<HostedSyncReceipt?> BeginAsync(
        Guid organizationId, Guid venueId, string packageId, byte[] manifestBytes,
        CancellationToken cancellationToken) => throw Unavailable();

    public Task<long> UploadedLengthAsync(
        Guid organizationId, Guid venueId, string packageId, string relativePath,
        CancellationToken cancellationToken) => throw Unavailable();

    public Task AppendChunkAsync(
        Guid organizationId, Guid venueId, string packageId, string relativePath,
        long offset, byte[] bytes, CancellationToken cancellationToken) => throw Unavailable();

    public Task<HostedSyncReceipt> VerifyAndCommitAsync(
        Guid organizationId, Guid venueId, string packageId, byte[] manifestBytes,
        CancellationToken cancellationToken) => throw Unavailable();
}
