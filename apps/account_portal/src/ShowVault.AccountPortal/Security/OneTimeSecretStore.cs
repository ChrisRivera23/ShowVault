using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace ShowVault.AccountPortal.Security;

public sealed class OneTimeSecretStore(TimeProvider timeProvider)
{
    internal const int MaximumEntries = 1024;
    private readonly ConcurrentDictionary<string, Entry> _entries = [];
    private readonly object _gate = new();

    public string Put(string value)
    {
        var handle = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24));
        lock (_gate)
        {
            TrimToCapacity();
            _entries[handle] = new(value, timeProvider.GetUtcNow().AddMinutes(5));
        }
        return handle;
    }

    private void TrimToCapacity()
    {
        var now = timeProvider.GetUtcNow();
        foreach (var item in _entries.Where(item => item.Value.ExpiresAt <= now))
            _entries.TryRemove(item.Key, out _);
        while (_entries.Count >= MaximumEntries)
        {
            var oldest = _entries.MinBy(item => item.Value.ExpiresAt);
            if (oldest.Key is null) break;
            _entries.TryRemove(oldest.Key, out _);
        }
    }

    public string? Take(string handle)
    {
        if (!_entries.TryRemove(handle, out var entry) ||
            entry.ExpiresAt <= timeProvider.GetUtcNow()) return null;
        return entry.Value;
    }

    private sealed record Entry(string Value, DateTimeOffset ExpiresAt);
}
