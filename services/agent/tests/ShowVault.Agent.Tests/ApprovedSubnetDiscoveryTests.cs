using System.Net;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class ApprovedSubnetDiscoveryTests
{
    [Fact]
    public async Task Probes_only_the_bounded_host_subset_without_ports_or_payloads()
    {
        var probe = new RecordingProbe();
        var discovery = new ApprovedSubnetDiscovery(probe, TimeProvider.System);

        var result = await discovery.DiscoverAsync(
            new ApprovedSubnet(Guid.NewGuid(), "192.168.10.0", 24), 4, 250, CancellationToken.None);

        Assert.Equal(4, result.AttemptedHostCount);
        Assert.Equal(4, result.RespondingHostCount);
        Assert.Equal(["192.168.10.1", "192.168.10.2", "192.168.10.3", "192.168.10.4"],
            result.RespondingAddresses.Select(address => address.ToString()));
        Assert.Equal(["192.168.10.1", "192.168.10.2", "192.168.10.3", "192.168.10.4"],
            probe.Addresses.Select(address => address.ToString()));
        Assert.All(probe.Timeouts, timeout => Assert.Equal(TimeSpan.FromMilliseconds(250), timeout));
    }

    [Theory]
    [InlineData(0, 250)]
    [InlineData(33, 250)]
    [InlineData(1, 99)]
    [InlineData(1, 501)]
    public async Task Rejects_out_of_bounds_authorizations(int hosts, int timeout)
    {
        var discovery = new ApprovedSubnetDiscovery(new RecordingProbe(), TimeProvider.System);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => discovery.DiscoverAsync(
            new ApprovedSubnet(Guid.NewGuid(), "10.0.0.0", 24), hosts, timeout, CancellationToken.None));
    }

    [Fact]
    public async Task Link_local_discovery_preserves_the_same_host_and_timeout_bounds()
    {
        var probe = new RecordingProbe();
        var result = await new ApprovedSubnetDiscovery(probe, TimeProvider.System).DiscoverAsync(
            new ApprovedSubnet(Guid.NewGuid(), "169.254.73.0", 24), 4, 250, CancellationToken.None);

        Assert.Equal(4, result.AttemptedHostCount);
        Assert.Equal(["169.254.73.1", "169.254.73.2", "169.254.73.3", "169.254.73.4"],
            probe.Addresses.Select(address => address.ToString()));
    }

    private sealed class RecordingProbe : ISubnetReachabilityProbe
    {
        public List<IPAddress> Addresses { get; } = [];
        public List<TimeSpan> Timeouts { get; } = [];
        public Task<bool> IsReachableAsync(IPAddress address, TimeSpan timeout, CancellationToken cancellationToken)
        {
            lock (Addresses) { Addresses.Add(address); Timeouts.Add(timeout); }
            return Task.FromResult(true);
        }
    }
}
