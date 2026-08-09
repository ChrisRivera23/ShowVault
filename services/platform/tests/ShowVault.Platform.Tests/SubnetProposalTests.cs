using ShowVault.Platform.Agents;
using Xunit;

namespace ShowVault.Platform.Tests;

public sealed class SubnetProposalTests
{
    [Fact]
    public void Accepts_aligned_bounded_ipv4_link_local_network()
    {
        var proposal = SubnetProposal.Detected(
            Guid.NewGuid(), Guid.NewGuid(), "169.254.73.0", 24,
            "GigabitEthernet", "Bounded direct link", DateTimeOffset.UtcNow);

        Assert.Equal("169.254.73.0", proposal.Network);
        Assert.Equal(24, proposal.PrefixLength);
    }

    [Theory]
    [InlineData("169.253.73.0", 24)]
    [InlineData("169.254.73.1", 24)]
    [InlineData("169.254.0.0", 16)]
    public void Rejects_non_link_local_unaligned_or_overbroad_networks(string network, int prefix)
    {
        Assert.Throws<ArgumentException>(() => SubnetProposal.Detected(
            Guid.NewGuid(), Guid.NewGuid(), network, prefix,
            "Ethernet", "Invalid direct link", DateTimeOffset.UtcNow));
    }
}
