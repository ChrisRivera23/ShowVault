using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace ShowVault.AccountPortal.Security;

public sealed class ServerSideTicketStore(TimeProvider timeProvider) : ITicketStore
{
    private readonly ConcurrentDictionary<string, StoredTicket> _tickets = [];

    public Task<string> StoreAsync(AuthenticationTicket ticket) =>
        StoreAsync(ticket, default);

    public Task<string> StoreAsync(AuthenticationTicket ticket,
        CancellationToken cancellationToken)
    {
        var key = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        _tickets[key] = new StoredTicket(ticket, Expires(ticket));
        return Task.FromResult(key);
    }

    public Task RenewAsync(string key, AuthenticationTicket ticket) =>
        RenewAsync(key, ticket, default);

    public Task RenewAsync(string key, AuthenticationTicket ticket,
        CancellationToken cancellationToken)
    {
        _tickets[key] = new StoredTicket(ticket, Expires(ticket));
        return Task.CompletedTask;
    }

    public Task<AuthenticationTicket?> RetrieveAsync(string key) =>
        RetrieveAsync(key, default);

    public Task<AuthenticationTicket?> RetrieveAsync(string key,
        CancellationToken cancellationToken)
    {
        if (!_tickets.TryGetValue(key, out var stored)) return Task.FromResult<AuthenticationTicket?>(null);
        if (stored.ExpiresAt > timeProvider.GetUtcNow())
            return Task.FromResult<AuthenticationTicket?>(stored.Ticket);
        _tickets.TryRemove(key, out _);
        return Task.FromResult<AuthenticationTicket?>(null);
    }

    public Task RemoveAsync(string key) => RemoveAsync(key, default);

    public Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        _tickets.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    private DateTimeOffset Expires(AuthenticationTicket ticket) =>
        ticket.Properties.ExpiresUtc ?? timeProvider.GetUtcNow().AddMinutes(30);

    private sealed record StoredTicket(AuthenticationTicket Ticket, DateTimeOffset ExpiresAt);
}
