using System.Net;
using System.Net.Sockets;
using System.Text;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class PanasonicCameraNetworkIdentificationTests
{
    [Theory]
    [InlineData("OID:AW-UE150A", "Panasonic AW-UE150A")]
    [InlineData("OID:AW-UE150A\n", "Panasonic AW-UE150A")]
    [InlineData("OID:AW-UE150A\r\n", "Panasonic AW-UE150A")]
    [InlineData("OID:AW-UE100", "Panasonic AW-UE100")]
    [InlineData("OID:AW-UE100\n", "Panasonic AW-UE100")]
    [InlineData("OID:AW-UE100\r\n", "Panasonic AW-UE100")]
    public async Task MatchesExactDocumentedModelFixtureWithReadOnlyQidRequest(
        string body, string expected)
    {
        await using var fixture = await HttpFixture.StartAsync(HttpStatusCode.OK, body);

        var result = await new PanasonicCameraProtocolProbe(fixture.Port).IdentifyAsync(
            IPAddress.Loopback, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(expected, result);
        Assert.StartsWith(
            "GET /cgi-bin/aw_cam?cmd=QID&res=1 HTTP/1.1", fixture.Request,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Authorization:", fixture.Request, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("OID:AW-UE160")]
    [InlineData("OID:AW-UE150")]
    [InlineData("OID:AW-UE150A extra")]
    [InlineData("oid:AW-UE100")]
    [InlineData("AW-UE100")]
    public async Task RejectsOtherOrLookalikeIdentifiers(string body)
    {
        await using var fixture = await HttpFixture.StartAsync(HttpStatusCode.OK, body);

        var result = await new PanasonicCameraProtocolProbe(fixture.Port).IdentifyAsync(
            IPAddress.Loopback, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Null(result);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Redirect)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task RejectsNonSuccessfulResponses(HttpStatusCode status)
    {
        await using var fixture = await HttpFixture.StartAsync(status, "OID:AW-UE100");

        var result = await new PanasonicCameraProtocolProbe(fixture.Port).IdentifyAsync(
            IPAddress.Loopback, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task RejectsOversizedResponse()
    {
        await using var fixture = await HttpFixture.StartAsync(
            HttpStatusCode.OK, "OID:AW-UE100" + new string('x', 65));

        var result = await new PanasonicCameraProtocolProbe(fixture.Port).IdentifyAsync(
            IPAddress.Loopback, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task NetworkIdentificationKeepsAddressesLocalAndEnforcesBounds()
    {
        var result = await new PanasonicCameraNetworkIdentification(
            new FixedProbe(), TimeProvider.System).IdentifyAsync(
            Guid.NewGuid(), Guid.NewGuid(), ["192.0.2.10"], 100, CancellationToken.None);

        var match = Assert.Single(result.Identifications);
        Assert.Equal(IPAddress.Parse("192.0.2.10"), match.Address);
        Assert.Equal("Panasonic AW-UE100", match.ProductFamily);
        await Assert.ThrowsAsync<ArgumentException>(() => new PanasonicCameraNetworkIdentification(
            new FixedProbe(), TimeProvider.System).IdentifyAsync(
            Guid.NewGuid(), Guid.NewGuid(), Enumerable.Repeat("192.0.2.10", 33).ToArray(),
            100, CancellationToken.None));
    }

    private sealed class FixedProbe : IPanasonicCameraProtocolProbe
    {
        public Task<string?> IdentifyAsync(
            IPAddress address, TimeSpan timeout, CancellationToken cancellationToken) =>
            Task.FromResult<string?>("Panasonic AW-UE100");
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
