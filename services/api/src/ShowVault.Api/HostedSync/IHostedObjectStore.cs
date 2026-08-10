namespace ShowVault.Api.HostedSync;

public interface IHostedObjectStore
{
    Task<byte[]?> ReadAsync(
        string key,
        int maximumBytes,
        CancellationToken cancellationToken);

    Task<bool> PutIfAbsentAsync(
        string key,
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<HostedObjectInfo>> ListAsync(
        string prefix,
        CancellationToken cancellationToken);

    Task CheckAvailabilityAsync(CancellationToken cancellationToken);
}

public sealed record HostedObjectInfo(string Key, long Size);
