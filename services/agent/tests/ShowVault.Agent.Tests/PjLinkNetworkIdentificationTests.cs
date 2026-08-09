using System.Net;
using System.Net.Sockets;
using System.Text;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class PjLinkNetworkIdentificationTests
{
    [Theory]
    [InlineData("CHRISTIE", "LX41", "Christie LX41")]
    [InlineData("CHRISTIE", "LW41", "Christie LW41")]
    [InlineData("Panasonic", "DZ770", "Panasonic PT-DZ770")]
    [InlineData("Panasonic", "VW431DEA", "Panasonic PT-VW431DEA")]
    [InlineData("Panasonic", "RZ470", "Panasonic PT-RZ470")]
    [InlineData("Panasonic", "RW430", "Panasonic PT-RW430")]
    [InlineData("EPSON", "EPSON QB1000B", "Epson QB1000B")]
    [InlineData("EPSON", "EPSON QB1000W", "Epson QB1000W")]
    [InlineData("CHRISTIE", "Other", null)]
    [InlineData("Panasonic", "Other", null)]
    [InlineData("EPSON", "Other", null)]
    [InlineData("Epson", "EPSON QB1000B", null)]
    [InlineData("PANASONIC", "DZ770", null)]
    [InlineData("OTHER", "LX41", null)]
    public async Task MatchesOnlyDocumentedManufacturerAndModelPairs(
        string manufacturer, string model, string? expected)
    {
        await using var fixture = PjLinkFixture.Start("PJLINK 0", manufacturer, model);

        var result = await new PjLinkProjectorProbe().IdentifyAsync(
            IPAddress.Loopback, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(expected, result);
        var expectedQueries = manufacturer is "CHRISTIE" or "Panasonic" or "EPSON"
            ? new[] { "%1INF1 ?", "%1INF2 ?" }
            : ["%1INF1 ?"];
        Assert.Equal(expectedQueries, await fixture.QueriesReceivedAsync);
    }

    [Fact]
    public async Task AuthenticationEnabledGreetingIsSafeFalseNegativeWithoutSendingACommand()
    {
        await using var fixture = PjLinkFixture.Start("PJLINK 1 01234567", "CHRISTIE", "LX41");

        var result = await new PjLinkProjectorProbe().IdentifyAsync(
            IPAddress.Loopback, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Null(result);
        Assert.Empty(await fixture.QueriesReceivedAsync);
    }

    [Fact]
    public async Task PublishesOnlyExactFixtureBackedMatches()
    {
        var service = new PjLinkNetworkIdentification(new FixedProbe(), TimeProvider.System);
        var result = await service.IdentifyAsync(Guid.NewGuid(), Guid.NewGuid(),
            ["192.168.1.2", "192.168.1.3"], 250, CancellationToken.None);

        var match = Assert.Single(result.Identifications);
        Assert.Equal("192.168.1.2", match.Address.ToString());
        Assert.Equal("Christie LX41", match.ProductFamily);
        Assert.Equal(2, result.AttemptedHostCount);
    }

    [Fact]
    public async Task CompositeProbeReturnsARecognizedFamilyFromTheBoundedProtocolSet()
    {
        var pjLink = new FixedFamilyProbe(null);
        var nec = new FixedFamilyProbe("NEC NP-PH3501QL");
        var probe = new ProjectorProtocolProbe([pjLink, nec]);

        var result = await probe.IdentifyAsync(
            IPAddress.Loopback, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal("NEC NP-PH3501QL", result);
        Assert.True(pjLink.WasCalled);
        Assert.True(nec.WasCalled);
    }

    private sealed class FixedProbe : IProjectorProtocolProbe
    {
        public Task<string?> IdentifyAsync(IPAddress address, TimeSpan timeout,
            CancellationToken cancellationToken) => Task.FromResult<string?>(
                address.ToString().EndsWith(".2", StringComparison.Ordinal) ? "Christie LX41" : null);
    }

    private sealed class FixedFamilyProbe(string? family) : IProjectorProtocolProbe
    {
        public bool WasCalled { get; private set; }

        public Task<string?> IdentifyAsync(IPAddress address, TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult(family);
        }
    }

    private sealed class PjLinkFixture : IAsyncDisposable
    {
        private readonly TcpListener _listener;

        private PjLinkFixture(string greeting, string manufacturer, string model)
        {
            _listener = new TcpListener(IPAddress.Loopback, 4_352);
            _listener.Start();
            QueriesReceivedAsync = RunAsync(greeting, manufacturer, model);
        }

        public Task<IReadOnlyList<string>> QueriesReceivedAsync { get; }

        public static PjLinkFixture Start(string greeting, string manufacturer, string model) =>
            new(greeting, manufacturer, model);

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            await QueriesReceivedAsync.WaitAsync(TimeSpan.FromSeconds(3));
        }

        private async Task<IReadOnlyList<string>> RunAsync(string greeting, string manufacturer, string model)
        {
            using var client = await _listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();
            await stream.WriteAsync(Encoding.ASCII.GetBytes(greeting + "\r"));
            var queries = new List<string>();
            if (greeting != "PJLINK 0") return queries;

            queries.Add(await ReadQueryAsync(stream));
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"%1INF1={manufacturer}\r"));
            if (manufacturer is not ("CHRISTIE" or "Panasonic" or "EPSON")) return queries;

            queries.Add(await ReadQueryAsync(stream));
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"%1INF2={model}\r"));
            return queries;
        }

        private static async Task<string> ReadQueryAsync(Stream stream)
        {
            var bytes = new List<byte>();
            while (true)
            {
                var value = new byte[1];
                if (await stream.ReadAsync(value) == 0 || value[0] == '\r') break;
                bytes.Add(value[0]);
            }
            return Encoding.ASCII.GetString(bytes.ToArray());
        }
    }
}
