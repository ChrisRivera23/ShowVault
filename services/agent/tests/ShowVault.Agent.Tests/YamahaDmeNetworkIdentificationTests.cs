using System.Net;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class YamahaDmeNetworkIdentificationTests
{
    [Fact]
    public async Task Publishes_only_exact_DME7_protocol_matches()
    {
        var service = new YamahaDmeNetworkIdentification(new FixedProbe(), TimeProvider.System);
        var result = await service.IdentifyAsync(Guid.NewGuid(), Guid.NewGuid(),
            ["192.168.1.2", "192.168.1.3"], 250, CancellationToken.None);

        var match = Assert.Single(result.Identifications);
        Assert.Equal("192.168.1.2", match.Address.ToString());
        Assert.Equal("Yamaha DME7", match.ProductFamily);
        Assert.Equal(2, result.AttemptedHostCount);
    }

    private sealed class FixedProbe : IYamahaDmeProtocolProbe
    {
        public Task<string?> IdentifyAsync(IPAddress address, TimeSpan timeout,
            CancellationToken cancellationToken) => Task.FromResult<string?>(
                address.ToString().EndsWith(".2", StringComparison.Ordinal) ? "Yamaha DME7" : null);
    }
}
