using System.Net;
using System.Net.Sockets;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class NecProjectorProbeTests
{
    [Theory]
    [InlineData(0xFF, 0x30, 0x00, 0x10, "NEC NP-PH3501QL")]
    [InlineData(0xFF, 0x30, 0x01, 0x10, "NEC NP-PH2601QL")]
    [InlineData(0xFF, 0x35, 0x00, 0x10, "NEC NP-PX2000UL")]
    [InlineData(0xFF, 0x35, 0x00, 0x11, "NEC NP-PX2201UL")]
    [InlineData(0xFF, 0x35, 0x01, 0x10, null)]
    public async Task MatchesOnlyDocumentedBaseModelTypeResponses(
        byte data01, byte data02, byte data12, byte data13, string? expected)
    {
        await using var fixture = NecFixture.Start(data01, data02, data12, data13);

        var result = await new NecProjectorProbe().IdentifyAsync(
            IPAddress.Loopback, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(expected, result);
        Assert.Equal(new byte[] { 0x00, 0xBF, 0x00, 0x00, 0x01, 0x00, 0xC0 },
            await fixture.QueryReceivedAsync);
    }

    [Fact]
    public async Task RejectsResponseWithInvalidChecksum()
    {
        await using var fixture = NecFixture.Start(0xFF, 0x30, 0x00, 0x10, corruptChecksum: true);

        var result = await new NecProjectorProbe().IdentifyAsync(
            IPAddress.Loopback, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task RejectsMalformedResponseHeader()
    {
        await using var fixture = NecFixture.Start(0xFF, 0x30, 0x00, 0x10, responseCode: 0xA0);

        var result = await new NecProjectorProbe().IdentifyAsync(
            IPAddress.Loopback, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Null(result);
    }

    private sealed class NecFixture : IAsyncDisposable
    {
        private readonly TcpListener _listener;

        private NecFixture(
            byte data01, byte data02, byte data12, byte data13,
            bool corruptChecksum, byte responseCode)
        {
            _listener = new TcpListener(IPAddress.Loopback, 7_142);
            _listener.Start();
            QueryReceivedAsync = RunAsync(
                data01, data02, data12, data13, corruptChecksum, responseCode);
        }

        public Task<byte[]> QueryReceivedAsync { get; }

        public static NecFixture Start(
            byte data01, byte data02, byte data12, byte data13,
            bool corruptChecksum = false, byte responseCode = 0x20) =>
            new(data01, data02, data12, data13, corruptChecksum, responseCode);

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            await QueryReceivedAsync.WaitAsync(TimeSpan.FromSeconds(3));
        }

        private async Task<byte[]> RunAsync(
            byte data01, byte data02, byte data12, byte data13,
            bool corruptChecksum, byte responseCode)
        {
            using var client = await _listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();
            var query = new byte[7];
            await stream.ReadExactlyAsync(query);
            var response = new byte[22];
            response[0] = responseCode;
            response[1] = 0xBF;
            response[4] = 0x10;
            response[5] = 0x00;
            response[6] = data01;
            response[7] = data02;
            response[17] = data12;
            response[18] = data13;
            response[^1] = (byte)(response[..^1].Aggregate(0, (sum, value) => sum + value) % 256);
            if (corruptChecksum) response[^1]++;
            await stream.WriteAsync(response);
            return query;
        }
    }
}
