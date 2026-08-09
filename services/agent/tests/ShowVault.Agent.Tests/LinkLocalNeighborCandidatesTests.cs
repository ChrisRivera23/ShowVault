using System.Net;
using System.Net.NetworkInformation;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class LinkLocalNeighborCandidatesTests
{
    [Fact]
    public async Task Reads_only_complete_neighbors_on_the_exact_unix_interface()
    {
        var provider = CreateProvider("""
            ? (169.254.220.9) at aa:bb:cc:dd:ee:ff on en7 ifscope [ethernet]
            ? (169.254.220.10) at (incomplete) on en7 ifscope [ethernet]
            ? (169.254.44.8) at aa:bb:cc:dd:ee:00 on en8 ifscope [ethernet]
            ? (192.168.1.2) at aa:bb:cc:dd:ee:11 on en7 ifscope [ethernet]
            """);

        var candidates = await provider.GetCandidatesAsync(
            new ApprovedSubnet(Guid.NewGuid(), "169.254.0.0", 16), CancellationToken.None);

        Assert.Equal(["169.254.220.9"], candidates.Select(item => item.ToString()));
    }

    [Fact]
    public async Task Reads_only_the_windows_section_for_the_exact_local_address()
    {
        var provider = CreateProvider("""
            Interface: 169.254.73.42 --- 0x6
              Internet Address      Physical Address      Type
              169.254.200.10        aa-bb-cc-dd-ee-ff     dynamic
            Interface: 192.168.1.4 --- 0x9
              Internet Address      Physical Address      Type
              169.254.99.2          aa-bb-cc-dd-ee-00     dynamic
            """);

        var candidates = await provider.GetCandidatesAsync(
            new ApprovedSubnet(Guid.NewGuid(), "169.254.0.0", 16), CancellationToken.None);

        Assert.Equal(["169.254.200.10"], candidates.Select(item => item.ToString()));
    }

    [Fact]
    public async Task Rejects_neighbor_cache_when_multiple_physical_link_local_interfaces_are_active()
    {
        var interfaces = new FixedInterfaceProvider(
        [
            Address("en7", "169.254.73.42"),
            Address("en8", "169.254.90.8")
        ]);
        var reader = new FixedArpTableReader("? (169.254.220.9) at aa:bb:cc:dd:ee:ff on en7");
        var candidates = await new ArpLinkLocalNeighborProvider(interfaces, reader).GetCandidatesAsync(
            new ApprovedSubnet(Guid.NewGuid(), "169.254.0.0", 16), CancellationToken.None);

        Assert.Empty(candidates);
        Assert.Equal(0, reader.ReadCount);
    }

    private static ArpLinkLocalNeighborProvider CreateProvider(string output) =>
        new(new FixedInterfaceProvider([Address("en7", "169.254.73.42")]),
            new FixedArpTableReader(output));

    private static LocalInterfaceAddress Address(string name, string address) =>
        new(name, name, NetworkInterfaceType.GigabitEthernet, OperationalStatus.Up,
            IPAddress.Parse(address), IPAddress.Parse("255.255.0.0"));

    private sealed class FixedInterfaceProvider(IReadOnlyList<LocalInterfaceAddress> addresses)
        : ILocalInterfaceProvider
    {
        public IReadOnlyList<LocalInterfaceAddress> GetAddresses() => addresses;
    }

    private sealed class FixedArpTableReader(string output) : IArpTableReader
    {
        public int ReadCount { get; private set; }
        public Task<string> ReadAsync(CancellationToken cancellationToken)
        {
            ReadCount++;
            return Task.FromResult(output);
        }
    }
}
