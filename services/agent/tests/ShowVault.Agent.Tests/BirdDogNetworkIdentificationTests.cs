using System.Net;
using System.Net.Sockets;
using System.Text;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class BirdDogNetworkIdentificationTests
{
    private const string DocumentedIdentifier = "BirdDog P200A4_A5";

    [Theory]
    [InlineData(DocumentedIdentifier)]
    [InlineData(DocumentedIdentifier + "\n")]
    [InlineData(DocumentedIdentifier + "\r\n")]
    public async Task MatchesExactDocumentedP200FixtureWithReadOnlyVersionRequest(string body)
    {
        await using var fixture = await HttpFixture.StartAsync(HttpStatusCode.OK, body);

        var result = await new BirdDogProtocolProbe(fixture.Port).IdentifyAsync(
            IPAddress.Loopback, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal("BirdDog P200 (A4/A5)", result);
        Assert.StartsWith("GET /version HTTP/1.1", fixture.Request, StringComparison.Ordinal);
        Assert.Contains("Accept: text", fixture.Request, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("BirdDog P200_A4_A5")]
    [InlineData("BirdDog P200A2_A3")]
    [InlineData("BirdDog P240")]
    [InlineData("P200A4_A5")]
    [InlineData("BirdDog P200A4_A5 extra")]
    public async Task RejectsOtherOrLookalikeIdentifiers(string body)
    {
        await using var fixture = await HttpFixture.StartAsync(HttpStatusCode.OK, body);

        var result = await new BirdDogProtocolProbe(fixture.Port).IdentifyAsync(
            IPAddress.Loopback, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Null(result);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Redirect)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task RejectsNonSuccessfulResponses(HttpStatusCode status)
    {
        await using var fixture = await HttpFixture.StartAsync(status, DocumentedIdentifier);

        var result = await new BirdDogProtocolProbe(fixture.Port).IdentifyAsync(
            IPAddress.Loopback, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task RejectsOversizedResponse()
    {
        await using var fixture = await HttpFixture.StartAsync(
            HttpStatusCode.OK, DocumentedIdentifier + new string('x', 65));

        var result = await new BirdDogProtocolProbe(fixture.Port).IdentifyAsync(
            IPAddress.Loopback, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task NetworkIdentificationKeepsAddressesLocalAndEnforcesBounds()
    {
        var result = await new BirdDogNetworkIdentification(
            new FixedProbe(), TimeProvider.System).IdentifyAsync(
            Guid.NewGuid(), Guid.NewGuid(), ["192.0.2.10"], 100, CancellationToken.None);

        var match = Assert.Single(result.Identifications);
        Assert.Equal(IPAddress.Parse("192.0.2.10"), match.Address);
        Assert.Equal("BirdDog P200 (A4/A5)", match.ProductFamily);
        await Assert.ThrowsAsync<ArgumentException>(() => new BirdDogNetworkIdentification(
            new FixedProbe(), TimeProvider.System).IdentifyAsync(
            Guid.NewGuid(), Guid.NewGuid(), Enumerable.Repeat("192.0.2.10", 33).ToArray(),
            100, CancellationToken.None));
    }

    private sealed class FixedProbe : IBirdDogProtocolProbe
    {
        public Task<string?> IdentifyAsync(
            IPAddress address, TimeSpan timeout, CancellationToken cancellationToken) =>
            Task.FromResult<string?>("BirdDog P200 (A4/A5)");
    }

    private sealed class HttpFixture : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly Task<string> _requestTask;

        private HttpFixture(TcpListener listener, Task<string> requestTask)
        {
            _listener = listener;
            _requestTask = requestTask;
        }

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
        public string Request => _requestTask.GetAwaiter().GetResult();

        public static Task<HttpFixture> StartAsync(HttpStatusCode status, string body)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return Task.FromResult(new HttpFixture(listener, RunAsync(listener, status, body)));
        }

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            await _requestTask.WaitAsync(TimeSpan.FromSeconds(3));
        }

        private static async Task<string> RunAsync(TcpListener listener, HttpStatusCode status, string body)
        {
            using var client = await listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();
            var requestBuffer = new byte[4_096];
            var received = await stream.ReadAsync(requestBuffer);
            var request = Encoding.ASCII.GetString(requestBuffer, 0, received);
            var bodyBytes = Encoding.ASCII.GetBytes(body);
            var response = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 {(int)status} {status}\r\nContent-Type: text/plain\r\nContent-Length: {bodyBytes.Length}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(response);
            await stream.WriteAsync(bodyBytes);
            return request;
        }
    }
}
