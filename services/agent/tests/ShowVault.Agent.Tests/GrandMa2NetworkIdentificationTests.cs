using System.Net;
using System.Net.Sockets;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class GrandMa2NetworkIdentificationTests
{
    public static TheoryData<string, string?> TelnetRemoteFixtures => new()
    {
        { "enabled-console.txt", "grandMA2" },
        { "enabled-onpc.txt", "grandMA2" },
        { "partial-guest-only.txt", null },
        { "partial-login-only.txt", null },
        { "generic-telnet.txt", null },
        { "grandma3-banner.txt", null }
    };

    [Theory]
    [MemberData(nameof(TelnetRemoteFixtures))]
    public async Task MatchesOnlyCompleteDocumentedGreetingWithoutSendingData(
        string fixtureName,
        string? expectedProductFamily)
    {
        var response = await File.ReadAllBytesAsync(FixturePath(fixtureName));
        await using var fixture = TelnetRemoteFixture.Start(response, maximumWriteBytes: 7);

        var result = await new GrandMa2TelnetBannerProbe().IdentifyAsync(
            IPAddress.Loopback, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(expectedProductFamily, result);
        Assert.Equal(0, await fixture.ClientBytesReceivedAsync);
    }

    [Fact]
    public async Task DisabledTelnetRemoteIsSafeFalseNegative()
    {
        var result = await new GrandMa2TelnetBannerProbe().IdentifyAsync(
            IPAddress.Loopback, TimeSpan.FromMilliseconds(100), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task SilentServiceTimesOutWithoutSendingData()
    {
        await using var fixture = TelnetRemoteFixture.Start([], completeResponse: false);

        var result = await new GrandMa2TelnetBannerProbe().IdentifyAsync(
            IPAddress.Loopback, TimeSpan.FromMilliseconds(100), CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, await fixture.ClientBytesReceivedAsync);
    }

    [Fact]
    public async Task IgnoresDocumentedGreetingBeyondResponseCapWithoutSendingData()
    {
        var signature = await File.ReadAllBytesAsync(FixturePath("enabled-console.txt"));
        var response = Enumerable.Repeat((byte)'x', 4_096).Concat(signature).ToArray();
        await using var fixture = TelnetRemoteFixture.Start(response);

        var result = await new GrandMa2TelnetBannerProbe().IdentifyAsync(
            IPAddress.Loopback, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, await fixture.ClientBytesReceivedAsync);
    }

    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "GrandMa2TelnetRemote", name);

    private sealed class TelnetRemoteFixture : IAsyncDisposable
    {
        private const int Port = 30_000;
        private readonly TcpListener _listener;

        private TelnetRemoteFixture(
            byte[] response,
            int maximumWriteBytes,
            bool completeResponse)
        {
            _listener = new TcpListener(IPAddress.Loopback, Port);
            _listener.Start();
            ClientBytesReceivedAsync = RunAsync(response, maximumWriteBytes, completeResponse);
        }

        public Task<int> ClientBytesReceivedAsync { get; }

        public static TelnetRemoteFixture Start(
            byte[] response,
            int maximumWriteBytes = int.MaxValue,
            bool completeResponse = true) =>
            new(response, maximumWriteBytes, completeResponse);

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            await ClientBytesReceivedAsync.WaitAsync(TimeSpan.FromSeconds(3));
        }

        private async Task<int> RunAsync(
            byte[] response,
            int maximumWriteBytes,
            bool completeResponse)
        {
            using var client = await _listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();
            for (var offset = 0; offset < response.Length; offset += maximumWriteBytes)
            {
                var count = Math.Min(maximumWriteBytes, response.Length - offset);
                await stream.WriteAsync(response.AsMemory(offset, count));
            }

            if (completeResponse)
                client.Client.Shutdown(SocketShutdown.Send);

            var received = new byte[1];
            try
            {
                return await stream.ReadAsync(received);
            }
            catch (IOException)
            {
                // A client that deliberately stops at the response cap can reset the
                // connection while unread fixture bytes remain. It still sent no data.
                return 0;
            }
        }
    }
}
