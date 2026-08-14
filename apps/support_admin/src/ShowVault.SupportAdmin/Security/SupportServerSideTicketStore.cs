using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace ShowVault.SupportAdmin.Security;

public sealed class SupportServerSideTicketStore(TimeProvider timeProvider) : ITicketStore
{
    internal const int MaximumEntries = 4096;
    private readonly Dictionary<string, StoredTicket> _tickets = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public Task<string> StoreAsync(AuthenticationTicket ticket) => StoreAsync(ticket, default);

    public Task<string> StoreAsync(AuthenticationTicket ticket, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        lock (_gate)
        {
            TrimToCapacity();
            _tickets.Add(key, new(ticket, Expires(ticket)));
        }
        return Task.FromResult(key);
    }

    public Task RenewAsync(string key, AuthenticationTicket ticket) =>
        RenewAsync(key, ticket, default);

    public Task RenewAsync(string key, AuthenticationTicket ticket,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_tickets.ContainsKey(key)) TrimToCapacity();
            _tickets[key] = new(ticket, Expires(ticket));
        }
        return Task.CompletedTask;
    }

    public Task<AuthenticationTicket?> RetrieveAsync(string key) =>
        RetrieveAsync(key, default);

    public Task<AuthenticationTicket?> RetrieveAsync(string key,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_tickets.TryGetValue(key, out var stored))
                return Task.FromResult<AuthenticationTicket?>(null);
            if (stored.ExpiresAt > timeProvider.GetUtcNow())
                return Task.FromResult<AuthenticationTicket?>(stored.Ticket);
            _tickets.Remove(key);
            return Task.FromResult<AuthenticationTicket?>(null);
        }
    }

    public Task RemoveAsync(string key) => RemoveAsync(key, default);

    public Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate) _tickets.Remove(key);
        return Task.CompletedTask;
    }

    private DateTimeOffset Expires(AuthenticationTicket ticket) =>
        ticket.Properties.ExpiresUtc ?? timeProvider.GetUtcNow().AddMinutes(5);

    private void TrimToCapacity()
    {
        var now = timeProvider.GetUtcNow();
        foreach (var key in _tickets.Where(item => item.Value.ExpiresAt <= now)
                     .Select(item => item.Key).ToArray())
            _tickets.Remove(key);
        while (_tickets.Count >= MaximumEntries)
        {
            var oldest = _tickets.MinBy(item => item.Value.ExpiresAt);
            if (oldest.Key is null) break;
            _tickets.Remove(oldest.Key);
        }
    }

    private sealed record StoredTicket(AuthenticationTicket Ticket, DateTimeOffset ExpiresAt);
}
