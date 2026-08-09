using System.Net;
using System.Net.Sockets;
using System.Text;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class BehringerWingNetworkIdentificationTests
{
    private static readonly byte[] ExactRequest = "WING?"u8.ToArray();

    [Fact]
    public async Task MatchesExactDocumentedStandardWingReply()
    {
        await using var fixture = await UdpFixture.StartAsync(
            "WING,192.168.1.62,FOH,ngc-full,12345678,2.0.1");

        var result = await new BehringerWingProtocolProbe(fixture.Port).IdentifyAsync(
            IPAddress.Loopback, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal("Behringer WING", result);
        Assert.Equal(ExactRequest, fixture.Request);
    }

    [Theory]
    [InlineData("WING,192.168.1.62,FOH,ngc-compact,12345678,2.0.1")]
    [InlineData("WING,192.168.1.62,FOH,ngc-rack,12345678,2.0.1")]
    [InlineData("X32,192.168.1.62,FOH,ngc-full,12345678,2.0.1")]
    [InlineData("WING,not-an-ip,FOH,ngc-full,12345678,2.0.1")]
    [InlineData("WING,192.168.1.62,,ngc-full,12345678,2.0.1")]
    [InlineData("WING,192.168.1.62,FOH,ngc-full,,2.0.1")]
    [InlineData("WING,192.168.1.62,FOH,ngc-full,12345678,")]
    [InlineData("WING,192.168.1.62,FOH,ngc-full,12345678,2.0.1,extra")]
    [InlineData("WING,192.168.1.62,FOH,NGC-FULL,12345678,2.0.1")]
    [InlineData("generic UDP response")]
    public async Task RejectsUnknownOrMalformedReplies(string reply)
    {
        await using var fixture = await UdpFixture.StartAsync(reply);

        var result = await new BehringerWingProtocolProbe(fixture.Port).IdentifyAsync(
            IPAddress.Loopback, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task RejectsNonAsciiAndOversizedReplies()
    {
        await using var nonAscii = await UdpFixture.StartAsync(
            [.. Encoding.ASCII.GetBytes("WING,192.168.1.62,FOH,ngc-full,12345678,2.0.1"), 0x00]);
        Assert.Null(await new BehringerWingProtocolProbe(nonAscii.Port).IdentifyAsync(
            IPAddress.Loopback, TimeSpan.FromMilliseconds(500), CancellationToken.None));

        await using var oversized = await UdpFixture.StartAsync(Enumerable.Repeat((byte)'A', 257).ToArray());
        Assert.Null(await new BehringerWingProtocolProbe(oversized.Port).IdentifyAsync(
            IPAddress.Loopback, TimeSpan.FromMilliseconds(500), CancellationToken.None));
    }

    [Fact]
    public async Task NetworkIdentificationKeepsAddressesLocalAndEnforcesBounds()
    {
        var result = await new BehringerWingNetworkIdentification(
            new FixedProbe(), TimeProvider.System).IdentifyAsync(
            Guid.NewGuid(), Guid.NewGuid(), ["192.0.2.10"], 100, CancellationToken.None);

        var match = Assert.Single(result.Identifications);
        Assert.Equal(IPAddress.Parse("192.0.2.10"), match.Address);
        Assert.Equal("Behringer WING", match.ProductFamily);
        await Assert.ThrowsAsync<ArgumentException>(() => new BehringerWingNetworkIdentification(
            new FixedProbe(), TimeProvider.System).IdentifyAsync(
            Guid.NewGuid(), Guid.NewGuid(), Enumerable.Repeat("192.0.2.10", 33).ToArray(),
            100, CancellationToken.None));
    }

    private sealed class FixedProbe : IBehringerWingProtocolProbe
    {
        public Task<string?> IdentifyAsync(
            IPAddress address, TimeSpan timeout, CancellationToken cancellationToken) =>
            Task.FromResult<string?>("Behringer WING");
    }

    private sealed class UdpFixture : IAsyncDisposable
    {
        private readonly UdpClient _server;
        private readonly Task<byte[]> _requestTask;

        private UdpFixture(UdpClient server, Task<byte[]> requestTask)
        {
            _server = server;
            _requestTask = requestTask;
        }

        public int Port => ((IPEndPoint)_server.Client.LocalEndPoint!).Port;
        public byte[] Request => _requestTask.GetAwaiter().GetResult();

        public static Task<UdpFixture> StartAsync(string reply) =>
            StartAsync(Encoding.ASCII.GetBytes(reply));

        public static Task<UdpFixture> StartAsync(byte[] reply)
        {
            var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            return Task.FromResult(new UdpFixture(server, RunAsync(server, reply)));
        }

        public async ValueTask DisposeAsync()
        {
            _server.Dispose();
            await _requestTask.WaitAsync(TimeSpan.FromSeconds(3));
        }

        private static async Task<byte[]> RunAsync(UdpClient server, byte[] reply)
        {
            var request = await server.ReceiveAsync();
            await server.SendAsync(reply, request.RemoteEndPoint);
            return request.Buffer;
        }
    }
}
