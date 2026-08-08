using System.Net;
using System.Net.NetworkInformation;

namespace ShowVault.Agent.Plugins;

public interface ISubnetReachabilityProbe
{
    Task<bool> IsReachableAsync(IPAddress address, TimeSpan timeout, CancellationToken cancellationToken);
}

public sealed class IcmpSubnetReachabilityProbe : ISubnetReachabilityProbe
{
    public async Task<bool> IsReachableAsync(
        IPAddress address,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var ping = new Ping();
        try
        {
            var reply = await ping.SendPingAsync(address, (int)timeout.TotalMilliseconds)
                .WaitAsync(cancellationToken);
            return reply.Status == IPStatus.Success;
        }
        catch (PingException)
        {
            return false;
        }
    }
}

public sealed record ApprovedSubnetDiscoveryResult(
    Guid ProposalId,
    int AttemptedHostCount,
    int RespondingHostCount,
    IReadOnlyList<IPAddress> RespondingAddresses,
    DateTimeOffset CompletedAt);

public sealed class ApprovedSubnetDiscovery(
    ISubnetReachabilityProbe probe,
    TimeProvider timeProvider)
{
    public const int MaximumHostCount = 32;
    public const int MaximumConcurrency = 8;
    public const int MinimumTimeoutMilliseconds = 100;
    public const int MaximumTimeoutMilliseconds = 500;

    public async Task<ApprovedSubnetDiscoveryResult> DiscoverAsync(
        ApprovedSubnet subnet,
        int maxHosts,
        int timeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        if (maxHosts is < 1 or > MaximumHostCount ||
            timeoutMilliseconds is < MinimumTimeoutMilliseconds or > MaximumTimeoutMilliseconds)
        {
            throw new ArgumentOutOfRangeException(nameof(maxHosts), "Subnet discovery bounds are invalid.");
        }

        var addresses = EnumerateHosts(subnet.Network, subnet.PrefixLength).Take(maxHosts).ToArray();
        using var concurrency = new SemaphoreSlim(MaximumConcurrency);
        var timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);
        var results = await Task.WhenAll(addresses.Select(async address =>
        {
            await concurrency.WaitAsync(cancellationToken);
            try
            {
                return await probe.IsReachableAsync(address, timeout, cancellationToken);
            }
            finally
            {
                concurrency.Release();
            }
        }));
        var respondingAddresses = addresses
            .Where((_, index) => results[index])
            .ToArray();
        return new(subnet.ProposalId, addresses.Length, respondingAddresses.Length,
            respondingAddresses, timeProvider.GetUtcNow());
    }

    private static IEnumerable<IPAddress> EnumerateHosts(string network, int prefixLength)
    {
        var bytes = IPAddress.Parse(network).GetAddressBytes();
        var start = ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
        var hostCount = (1u << (32 - prefixLength)) - 2;
        for (uint offset = 1; offset <= hostCount; offset++)
        {
            var value = start + offset;
            yield return new IPAddress([
                (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value
            ]);
        }
    }
}

public sealed record ApprovedSubnet(Guid ProposalId, string Network, int PrefixLength);
