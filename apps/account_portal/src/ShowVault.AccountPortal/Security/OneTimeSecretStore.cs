using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace ShowVault.AccountPortal.Security;

public sealed class OneTimeSecretStore(TimeProvider timeProvider)
{
    private readonly ConcurrentDictionary<string, Entry> _entries = [];

    public string Put(string value)
    {
        var handle = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24));
        _entries[handle] = new(value, timeProvider.GetUtcNow().AddMinutes(5));
        return handle;
    }

    public string? Take(string handle)
    {
        if (!_entries.TryRemove(handle, out var entry) ||
            entry.ExpiresAt <= timeProvider.GetUtcNow()) return null;
        return entry.Value;
    }

    private sealed record Entry(string Value, DateTimeOffset ExpiresAt);
}
