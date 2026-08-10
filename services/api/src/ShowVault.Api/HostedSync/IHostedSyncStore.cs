namespace ShowVault.Api.HostedSync;

public interface IHostedSyncStore
{
    Task<HostedSyncReceipt?> GetReceiptAsync(
        Guid organizationId,
        Guid venueId,
        string packageId,
        CancellationToken cancellationToken);

    Task<HostedSyncReceipt?> BeginAsync(
        Guid organizationId,
        Guid venueId,
        string packageId,
        byte[] manifestBytes,
        CancellationToken cancellationToken);

    Task<long> UploadedLengthAsync(
        Guid organizationId,
        Guid venueId,
        string packageId,
        string relativePath,
        CancellationToken cancellationToken);

    Task AppendChunkAsync(
        Guid organizationId,
        Guid venueId,
        string packageId,
        string relativePath,
        long offset,
        byte[] bytes,
        CancellationToken cancellationToken);

    Task<HostedSyncReceipt> VerifyAndCommitAsync(
        Guid organizationId,
        Guid venueId,
        string packageId,
        byte[] manifestBytes,
        CancellationToken cancellationToken);
}
