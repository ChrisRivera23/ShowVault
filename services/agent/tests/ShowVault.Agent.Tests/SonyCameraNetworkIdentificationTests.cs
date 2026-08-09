using System.Net;
using System.Net.Sockets;
using System.Text;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class SonyCameraNetworkIdentificationTests
{
    [Theory]
    [InlineData("BRC-X400")]
    [InlineData("BRC-X401")]
    [InlineData("SRG-X400")]
    [InlineData("SRG-X402")]
    [InlineData("SRG-201M2")]
    [InlineData("SRG-X120")]
    [InlineData("SRG-HD1M2")]
    [InlineData("SRG-A40")]
    [InlineData("SRG-A12")]
    public async Task MatchesExactDocumentedModelInReadOnlySystemInquiry(string model)
    {
        await using var fixture = await HttpFixture.StartAsync(
            HttpStatusCode.OK, $"BuildNumber=1&ModelName={model}&Serial=12345678\r\n");

        var result = await new SonyCameraProtocolProbe(fixture.Port).IdentifyAsync(
            IPAddress.Loopback, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal($"Sony {model}", result);
        Assert.StartsWith(
            "GET /command/inquiry.cgi?inq=system HTTP/1.1", fixture.Request,
            StringComparison.Ordinal);
        Assert.Contains("Referer: http://127.0.0.1:", fixture.Request,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Authorization:", fixture.Request, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("ModelName=BRC-X1000")]
    [InlineData("ModelName=SRG-X400-extra")]
    [InlineData("modelName=SRG-X400")]
    [InlineData("ModelName=srg-x400")]
    [InlineData("CameraName=SRG-X400")]
    [InlineData("ModelName=SRG-X400&ModelName=SRG-X400")]
    [InlineData("ModelName=SRG-X400&ModelName=SRG-A40")]
    public async Task RejectsUnknownLookalikeOrDuplicateModelFields(string body)
    {
        await using var fixture = await HttpFixture.StartAsync(HttpStatusCode.OK, body);

        var result = await new SonyCameraProtocolProbe(fixture.Port).IdentifyAsync(
            IPAddress.Loopback, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Null(result);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Redirect)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task RejectsAuthenticationChallengesRedirectsAndMissingEndpoints(HttpStatusCode status)
    {
        await using var fixture = await HttpFixture.StartAsync(status, "ModelName=SRG-A40");

        var result = await new SonyCameraProtocolProbe(fixture.Port).IdentifyAsync(
            IPAddress.Loopback, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task RejectsOversizedPrivacyBearingSystemResponse()
    {
        await using var fixture = await HttpFixture.StartAsync(
            HttpStatusCode.OK, "ModelName=SRG-A40&Serial=" + new string('1', 16_384));

        var result = await new SonyCameraProtocolProbe(fixture.Port).IdentifyAsync(
            IPAddress.Loopback, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task NetworkIdentificationKeepsAddressesLocalAndEnforcesBounds()
    {
        var result = await new SonyCameraNetworkIdentification(
            new FixedProbe(), TimeProvider.System).IdentifyAsync(
            Guid.NewGuid(), Guid.NewGuid(), ["192.0.2.10"], 100, CancellationToken.None);

        var match = Assert.Single(result.Identifications);
        Assert.Equal(IPAddress.Parse("192.0.2.10"), match.Address);
        Assert.Equal("Sony SRG-A40", match.ProductFamily);
        await Assert.ThrowsAsync<ArgumentException>(() => new SonyCameraNetworkIdentification(
            new FixedProbe(), TimeProvider.System).IdentifyAsync(
            Guid.NewGuid(), Guid.NewGuid(), Enumerable.Repeat("192.0.2.10", 33).ToArray(),
            100, CancellationToken.None));
    }

    private sealed class FixedProbe : ISonyCameraProtocolProbe
    {
        public Task<string?> IdentifyAsync(
            IPAddress address, TimeSpan timeout, CancellationToken cancellationToken) =>
            Task.FromResult<string?>("Sony SRG-A40");
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
