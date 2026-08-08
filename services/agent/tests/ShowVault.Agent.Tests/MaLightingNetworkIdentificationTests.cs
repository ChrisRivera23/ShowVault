using System.Net;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class MaLightingNetworkIdentificationTests
{
    [Fact]
    public async Task Publishes_only_primary_signature_matches()
    {
        var probe = new FixedProbe();
        var service = new MaLightingNetworkIdentification(probe, TimeProvider.System);
        var result = await service.IdentifyAsync(Guid.NewGuid(), Guid.NewGuid(),
            ["192.168.1.2", "192.168.1.3"], 250, CancellationToken.None);

        var match = Assert.Single(result.Identifications);
        Assert.Equal("192.168.1.2", match.Address.ToString());
        Assert.Equal("grandMA3", match.ProductFamily);
        Assert.Equal(2, result.AttemptedHostCount);
    }

    private sealed class FixedProbe : IMaLightingProtocolProbe
    {
        public Task<string?> IdentifyAsync(IPAddress address, TimeSpan timeout,
            CancellationToken cancellationToken) => Task.FromResult<string?>(
                address.ToString().EndsWith(".2", StringComparison.Ordinal) ? "grandMA3" : null);
    }
}
