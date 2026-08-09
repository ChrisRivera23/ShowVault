using System.Net;
using System.Net.Sockets;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class AllenHeathQuNetworkIdentificationTests
{
    private static readonly byte[] ExactRequest =
        [0xF0, 0x00, 0x00, 0x1A, 0x50, 0x11, 0x01, 0x00, 0x7F, 0x10, 0x00, 0xF7];

    [Theory]
    [InlineData(0x01, "Allen & Heath Qu-16")]
    [InlineData(0x02, "Allen & Heath Qu-24")]
    [InlineData(0x03, "Allen & Heath Qu-32")]
    [InlineData(0x04, "Allen & Heath Qu-Pac")]
    [InlineData(0x05, "Allen & Heath Qu-SB")]
    public async Task MatchesExactDocumentedBoxId(byte boxId, string expected)
    {
        var reply = new byte[]
            { 0xFE, 0xF0, 0x00, 0x00, 0x1A, 0x50, 0x11, 0x01, 0x00, 0x03,
                0x11, boxId, 0x01, 0x09, 0xF7 };
        await using var fixture = await TcpFixture.StartAsync(reply);

        var result = await new AllenHeathQuProtocolProbe(fixture.Port).IdentifyAsync(
            IPAddress.Loopback, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(expected, result);
        Assert.Equal(ExactRequest, fixture.Request);
    }

    [Fact]
    public async Task AcceptsFragmentedReplyAndInterleavedActiveSensing()
    {
        var chunks = new[]
        {
            new byte[] { 0xFE, 0xF0, 0x00, 0x00, 0x1A },
            new byte[] { 0x50, 0x11, 0xFE, 0x01, 0x00, 0x00, 0x11, 0x04 },
            new byte[] { 0x01, 0x09, 0xF7 }
        };
        await using var fixture = await TcpFixture.StartAsync(chunks);

        var result = await new AllenHeathQuProtocolProbe(fixture.Port).IdentifyAsync(
            IPAddress.Loopback, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal("Allen & Heath Qu-Pac", result);
    }

    [Theory]
    [MemberData(nameof(InvalidReplies))]
    public async Task RejectsUnknownOrMalformedReplies(byte[] reply)
    {
        await using var fixture = await TcpFixture.StartAsync(reply);

        var result = await new AllenHeathQuProtocolProbe(fixture.Port).IdentifyAsync(
            IPAddress.Loopback, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Null(result);
    }

    public static TheoryData<byte[]> InvalidReplies => new()
    {
        new byte[] { 0xF0, 0x00, 0x00, 0x1A, 0x50, 0x11, 0x01, 0x00, 0x00,
            0x11, 0x06, 0x01, 0x09, 0xF7 },
        new byte[] { 0xF0, 0x00, 0x00, 0x1A, 0x50, 0x10, 0x01, 0x00, 0x00,
            0x11, 0x01, 0x01, 0x09, 0xF7 },
        new byte[] { 0xF0, 0x00, 0x00, 0x1A, 0x50, 0x11, 0x01, 0x00, 0x10,
            0x11, 0x01, 0x01, 0x09, 0xF7 },
        new byte[] { 0xF0, 0x00, 0x00, 0x1A, 0x50, 0x11, 0x01, 0x00, 0x00,
            0x12, 0x01, 0x01, 0x09, 0xF7 },
        new byte[] { 0xF0, 0x00, 0x00, 0x1A, 0x50, 0x11, 0x01, 0x00, 0x00,
            0x11, 0x01, 0x01, 0x09 },
        new byte[] { 0x90, 0x00, 0x7F }
    };

    [Fact]
    public async Task RejectsReplyBeyondResponseCap()
    {
        var reply = Enumerable.Repeat((byte)0x01, 64).Concat(new byte[]
            { 0xF0, 0x00, 0x00, 0x1A, 0x50, 0x11, 0x01, 0x00, 0x00,
                0x11, 0x01, 0x01, 0x09, 0xF7 }).ToArray();
        await using var fixture = await TcpFixture.StartAsync(reply);

        var result = await new AllenHeathQuProtocolProbe(fixture.Port).IdentifyAsync(
            IPAddress.Loopback, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task NetworkIdentificationKeepsAddressesLocalAndEnforcesBounds()
    {
        var result = await new AllenHeathQuNetworkIdentification(
            new FixedProbe(), TimeProvider.System).IdentifyAsync(
            Guid.NewGuid(), Guid.NewGuid(), ["192.0.2.10"], 100, CancellationToken.None);

        var match = Assert.Single(result.Identifications);
        Assert.Equal(IPAddress.Parse("192.0.2.10"), match.Address);
        Assert.Equal("Allen & Heath Qu-16", match.ProductFamily);
        await Assert.ThrowsAsync<ArgumentException>(() => new AllenHeathQuNetworkIdentification(
            new FixedProbe(), TimeProvider.System).IdentifyAsync(
            Guid.NewGuid(), Guid.NewGuid(), Enumerable.Repeat("192.0.2.10", 33).ToArray(),
            100, CancellationToken.None));
    }

    private sealed class FixedProbe : IAllenHeathQuProtocolProbe
    {
        public Task<string?> IdentifyAsync(
            IPAddress address, TimeSpan timeout, CancellationToken cancellationToken) =>
            Task.FromResult<string?>("Allen & Heath Qu-16");
    }

    private sealed class TcpFixture : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly Task<byte[]> _requestTask;

        private TcpFixture(TcpListener listener, Task<byte[]> requestTask)
        {
            _listener = listener;
            _requestTask = requestTask;
        }

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
        public byte[] Request => _requestTask.GetAwaiter().GetResult();

        public static Task<TcpFixture> StartAsync(params byte[][] chunks)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return Task.FromResult(new TcpFixture(listener, RunAsync(listener, chunks)));
        }

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            await _requestTask.WaitAsync(TimeSpan.FromSeconds(3));
        }

        private static async Task<byte[]> RunAsync(TcpListener listener, byte[][] chunks)
        {
            using var client = await listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();
            var request = new byte[ExactRequest.Length];
            var received = 0;
            while (received < request.Length)
            {
                var count = await stream.ReadAsync(request.AsMemory(received));
                if (count == 0) break;
                received += count;
            }
            foreach (var chunk in chunks)
            {
                await stream.WriteAsync(chunk);
                await stream.FlushAsync();
                await Task.Yield();
            }
            return request[..received];
        }
    }
}
