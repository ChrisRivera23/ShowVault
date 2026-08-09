using System.Net;
using System.Net.NetworkInformation;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class LocalSubnetProposalDiscoveryTests
{
    [Fact]
    public void Proposes_only_bounded_unique_private_directly_connected_subnets()
    {
        var discovery = new LocalSubnetProposalDiscovery(new FixedInterfaceProvider(
        [
            Address("en0", NetworkInterfaceType.Ethernet, "192.168.10.42", "255.255.255.0"),
            Address("en0", NetworkInterfaceType.Ethernet, "192.168.10.43", "255.255.255.0"),
            Address("en1", NetworkInterfaceType.Wireless80211, "10.44.7.9", "255.255.0.0")
        ]));

        var proposals = discovery.Discover();

        Assert.Collection(
            proposals,
            item =>
            {
                Assert.Equal("192.168.10.0", item.Network);
                Assert.Equal(24, item.PrefixLength);
                Assert.True(item.RequiresOperatorApproval);
                Assert.Contains("no hosts were contacted", item.Evidence, StringComparison.Ordinal);
                Assert.DoesNotContain("192.168.10.42", item.Evidence, StringComparison.Ordinal);
            },
            item =>
            {
                Assert.Equal("10.44.7.0", item.Network);
                Assert.Equal(24, item.PrefixLength);
                Assert.Contains("narrowed from the directly assigned /16", item.Evidence, StringComparison.Ordinal);
            });
    }

    [Fact]
    public void Excludes_inactive_loopback_link_local_public_tunnel_virtual_and_point_to_point_addresses()
    {
        var discovery = new LocalSubnetProposalDiscovery(new FixedInterfaceProvider(
        [
            Address("down", NetworkInterfaceType.Ethernet, "192.168.1.2", "255.255.255.0", OperationalStatus.Down),
            Address("lo0", NetworkInterfaceType.Loopback, "127.0.0.1", "255.0.0.0"),
            Address("en0", NetworkInterfaceType.Wireless80211, "169.254.1.2", "255.255.0.0"),
            Address("en1", NetworkInterfaceType.Ethernet, "203.0.113.2", "255.255.255.0"),
            Address("utun4", NetworkInterfaceType.Tunnel, "10.1.1.2", "255.255.255.0"),
            Address("vpn adapter", NetworkInterfaceType.Ethernet, "10.2.2.2", "255.255.255.0"),
            Address("ppp0", NetworkInterfaceType.Ppp, "10.3.3.2", "255.255.255.0"),
            Address("docker0", NetworkInterfaceType.Ethernet, "172.17.0.1", "255.255.0.0"),
            Address("en2", NetworkInterfaceType.Ethernet, "192.168.2.2", "255.255.255.254"),
            Address("en3", NetworkInterfaceType.Ethernet, "192.168.3.2", "255.0.255.0"),
            Address("en4", NetworkInterfaceType.Ethernet, "192.168.4.0", "255.255.255.0"),
            Address("en5", NetworkInterfaceType.Ethernet, "192.168.5.255", "255.255.255.0")
        ]));

        Assert.Empty(discovery.Discover());
    }

    [Fact]
    public void Proposes_one_bounded_link_local_network_for_one_physical_ethernet_interface()
    {
        var discovery = new LocalSubnetProposalDiscovery(new FixedInterfaceProvider(
        [
            Address("en7", NetworkInterfaceType.GigabitEthernet, "169.254.73.42", "255.255.0.0")
        ]));

        var proposal = Assert.Single(discovery.Discover());

        Assert.Equal("169.254.73.0", proposal.Network);
        Assert.Equal(24, proposal.PrefixLength);
        Assert.True(proposal.RequiresOperatorApproval);
        Assert.Contains("One active physical Ethernet", proposal.Evidence, StringComparison.Ordinal);
        Assert.Contains("direct-link review", proposal.Evidence, StringComparison.Ordinal);
        Assert.DoesNotContain("169.254.73.42", proposal.Evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_ambiguous_multiple_link_local_ethernet_interfaces()
    {
        var discovery = new LocalSubnetProposalDiscovery(new FixedInterfaceProvider(
        [
            Address("en7", NetworkInterfaceType.Ethernet, "169.254.73.42", "255.255.0.0"),
            Address("en8", NetworkInterfaceType.Ethernet, "169.254.90.8", "255.255.0.0")
        ]));

        Assert.Empty(discovery.Discover());
    }

    [Fact]
    public void Caps_the_number_of_proposals()
    {
        var addresses = Enumerable.Range(1, 20)
            .Select(index => Address(
                $"en{index}",
                NetworkInterfaceType.Ethernet,
                $"10.0.{index}.2",
                "255.255.255.0"))
            .ToArray();

        var proposals = new LocalSubnetProposalDiscovery(new FixedInterfaceProvider(addresses)).Discover();

        Assert.Equal(LocalSubnetProposalDiscovery.MaximumProposalCount, proposals.Count);
    }

    private static LocalInterfaceAddress Address(
        string name,
        NetworkInterfaceType type,
        string address,
        string mask,
        OperationalStatus status = OperationalStatus.Up) =>
        new(name, name, type, status, IPAddress.Parse(address), IPAddress.Parse(mask));

    private sealed class FixedInterfaceProvider(IReadOnlyList<LocalInterfaceAddress> addresses)
        : ILocalInterfaceProvider
    {
        public IReadOnlyList<LocalInterfaceAddress> GetAddresses() => addresses;
    }
}
