using System.Collections.Concurrent;

namespace ShowVault.Api.Support;

public sealed class SupportRequestRateLimiter(TimeProvider timeProvider)
{
    internal const int PermitLimit = 10;
    internal const int MaximumPartitions = 4096;
    internal static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
    internal static readonly TimeSpan Retention = TimeSpan.FromMinutes(2);
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly object _partitionGate = new();
    internal int PartitionCount
    {
        get { lock (_partitionGate) return _entries.Count; }
    }

    public bool TryAcquire(string issuer, string subject, string source)
    {
        var now = timeProvider.GetUtcNow();
        var key = $"{issuer.Length}:{issuer}{subject.Length}:{subject}{source}";
        lock (_partitionGate)
        {
            if (!_entries.TryGetValue(key, out var entry))
            {
                if (_entries.Count >= MaximumPartitions)
                {
                    Prune(now);
                    if (_entries.Count >= MaximumPartitions) return false;
                }
                entry = _entries.GetOrAdd(key, _ => new(now));
            }
            if (now - entry.WindowStartedAt >= Window)
            {
                entry.WindowStartedAt = now;
                entry.Count = 0;
            }
            entry.LastSeenAt = now;
            if (entry.Count >= PermitLimit) return false;
            entry.Count++;
            return true;
        }
    }

    private void Prune(DateTimeOffset now)
    {
        foreach (var pair in _entries)
            if (now - pair.Value.LastSeenAt >= Retention)
                _entries.TryRemove(pair.Key, out _);
    }

    private sealed class Entry(DateTimeOffset now)
    {
        public DateTimeOffset WindowStartedAt { get; set; } = now;
        public DateTimeOffset LastSeenAt { get; set; } = now;
        public int Count { get; set; }
    }
}
