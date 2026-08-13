using System.Collections.Concurrent;

namespace ShowVault.Api.HostedSync;

public interface IHostedObjectStore
{
    bool IsAvailable { get; }
    Task<long> GetLengthAsync(string key, CancellationToken cancellationToken);
    Task AppendAsync(string key, long offset, ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken);
    Task<byte[]> ReadAsync(string key, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> ListKeysAsync(
        string prefix, CancellationToken cancellationToken);
}

public sealed class SyntheticHostedObjectStore : IHostedObjectStore
{
    private readonly ConcurrentDictionary<string, byte[]> _objects = new(StringComparer.Ordinal);
    public bool IsAvailable => true;

    public Task<long> GetLengthAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_objects.TryGetValue(key, out var value) ? (long)value.Length : 0);
    }

    public Task AppendAsync(string key, long offset, ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _objects.AddOrUpdate(key,
            _ => offset == 0 ? bytes.ToArray() : throw new HostedSyncConflictException(),
            (_, current) => Append(current, offset, bytes.Span));
        return Task.CompletedTask;
    }

    public Task<byte[]> ReadAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_objects.TryGetValue(key, out var value)) throw new KeyNotFoundException();
        return Task.FromResult(value.ToArray());
    }

    public Task<IReadOnlyList<string>> ListKeysAsync(
        string prefix, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<string> keys = _objects.Keys
            .Where(key => key.StartsWith(prefix, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal).ToArray();
        return Task.FromResult(keys);
    }

    private static byte[] Append(byte[] current, long offset, ReadOnlySpan<byte> bytes)
    {
        if (offset < 0 || offset > current.Length) throw new HostedSyncConflictException();
        if (offset < current.Length)
        {
            if (offset + bytes.Length > current.Length ||
                !current.AsSpan((int)offset, bytes.Length).SequenceEqual(bytes))
                throw new HostedSyncConflictException();
            return current;
        }
        var result = new byte[checked(current.Length + bytes.Length)];
        current.CopyTo(result, 0);
        bytes.CopyTo(result.AsSpan(current.Length));
        return result;
    }
}

public sealed class DisabledHostedObjectStore : IHostedObjectStore
{
    public bool IsAvailable => false;
    public Task<long> GetLengthAsync(string key, CancellationToken cancellationToken) =>
        throw new HostedSyncUnavailableException();
    public Task AppendAsync(string key, long offset, ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken) => throw new HostedSyncUnavailableException();
    public Task<byte[]> ReadAsync(string key, CancellationToken cancellationToken) =>
        throw new HostedSyncUnavailableException();
    public Task<IReadOnlyList<string>> ListKeysAsync(
        string prefix, CancellationToken cancellationToken) =>
        throw new HostedSyncUnavailableException();
}

public sealed class HostedSyncConflictException : Exception;
public sealed class HostedSyncUnavailableException : Exception;
