using System.Net;
using System.Net.Sockets;
using System.Text;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class BlackmagicVideohubNetworkIdentificationTests
{
    private const string DocumentedResponse = """
        PROTOCOL PREAMBLE:
        Version: 2.3

        VIDEOHUB DEVICE:
        Device present: true
        Model name: Blackmagic Smart Videohub
        Video inputs: 16
        Video processing units: 0
        Video outputs: 16
        Video monitoring outputs: 0
        Serial ports: 0


        """;

    [Fact]
    public async Task MatchesExactDocumentedSmartVideohubFixtureWithoutSendingData()
    {
        await using var fixture = VideohubFixture.Start(DocumentedResponse, maximumWriteBytes: 5);

        var result = await new BlackmagicVideohubProtocolProbe().IdentifyAsync(
            IPAddress.Loopback, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal("Blackmagic Smart Videohub 16x16", result);
        Assert.Equal(0, await fixture.ClientBytesReceivedAsync);
    }

    [Theory]
    [InlineData("Version: 2.4", "Model name: Blackmagic Smart Videohub", "Video outputs: 16")]
    [InlineData("Version: 2.3", "Model name: Blackmagic Universal Videohub", "Video outputs: 16")]
    [InlineData("Version: 2.3", "Model name: Blackmagic Smart Videohub", "Video outputs: 20")]
    public async Task RejectsUndocumentedOrIncompleteSignatures(
        string version, string model, string outputs)
    {
        var response = DocumentedResponse
            .Replace("Version: 2.3", version, StringComparison.Ordinal)
            .Replace("Model name: Blackmagic Smart Videohub", model, StringComparison.Ordinal)
            .Replace("Video outputs: 16", outputs, StringComparison.Ordinal);
        await using var fixture = VideohubFixture.Start(response);

        var result = await new BlackmagicVideohubProtocolProbe().IdentifyAsync(
            IPAddress.Loopback, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, await fixture.ClientBytesReceivedAsync);
    }

    [Fact]
    public async Task SilentServiceTimesOutWithoutSendingData()
    {
        await using var fixture = VideohubFixture.Start(string.Empty, completeResponse: false);

        var result = await new BlackmagicVideohubProtocolProbe().IdentifyAsync(
            IPAddress.Loopback, TimeSpan.FromMilliseconds(100), CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, await fixture.ClientBytesReceivedAsync);
    }

    [Fact]
    public async Task RejectsConflictingDuplicateIdentityFieldsWithoutSendingData()
    {
        var response = DocumentedResponse.Replace(
            "Model name: Blackmagic Smart Videohub",
            "Model name: Blackmagic Smart Videohub\nModel name: Blackmagic Universal Videohub",
            StringComparison.Ordinal);
        await using var fixture = VideohubFixture.Start(response);

        var result = await new BlackmagicVideohubProtocolProbe().IdentifyAsync(
            IPAddress.Loopback, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, await fixture.ClientBytesReceivedAsync);
    }

    [Fact]
    public async Task IgnoresSignatureBeyondResponseCapWithoutSendingData()
    {
        var response = new string('x', 4_096) + DocumentedResponse;
        await using var fixture = VideohubFixture.Start(response);

        var result = await new BlackmagicVideohubProtocolProbe().IdentifyAsync(
            IPAddress.Loopback, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, await fixture.ClientBytesReceivedAsync);
    }

    private sealed class VideohubFixture : IAsyncDisposable
    {
        private readonly TcpListener _listener;

        private VideohubFixture(string response, int maximumWriteBytes, bool completeResponse)
        {
            _listener = new TcpListener(IPAddress.Loopback, 9_990);
            _listener.Start();
            ClientBytesReceivedAsync = RunAsync(
                Encoding.ASCII.GetBytes(response), maximumWriteBytes, completeResponse);
        }

        public Task<int> ClientBytesReceivedAsync { get; }

        public static VideohubFixture Start(
            string response, int maximumWriteBytes = int.MaxValue, bool completeResponse = true) =>
            new(response, maximumWriteBytes, completeResponse);

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            await ClientBytesReceivedAsync.WaitAsync(TimeSpan.FromSeconds(3));
        }

        private async Task<int> RunAsync(byte[] response, int maximumWriteBytes, bool completeResponse)
        {
            using var client = await _listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();
            for (var offset = 0; offset < response.Length; offset += maximumWriteBytes)
            {
                var count = Math.Min(maximumWriteBytes, response.Length - offset);
                await stream.WriteAsync(response.AsMemory(offset, count));
            }
            if (completeResponse) client.Client.Shutdown(SocketShutdown.Send);
            var received = new byte[1];
            try { return await stream.ReadAsync(received); }
            catch (IOException) { return 0; }
        }
    }
}
