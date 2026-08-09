using System.Net;
using System.Net.Sockets;
using System.Text;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class NewTekTriCasterNetworkIdentificationTests
{
    private const string DocumentedBody = """
        <product_information>
        <product_model>TC1</product_model>
        <product_name>TriCaster TC1</product_name>
        <product_version>7-0</product_version>
        <product_id>NCWL-WFKNJ8YAA-200918</product_id>
        <product_serial_no/>
        <product_build_no>7-0-180920</product_build_no>
        <machine_name>TC1</machine_name>
        <session_name>TC1 Session</session_name>
        </product_information>
        """;

    [Fact]
    public async Task MatchesExactDocumentedTc1FixtureWithReadOnlyVersionRequest()
    {
        await using var fixture = await HttpFixture.StartAsync(HttpStatusCode.OK, DocumentedBody);

        var result = await new NewTekTriCasterProtocolProbe(fixture.Port).IdentifyAsync(
            IPAddress.Loopback, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal("NewTek TriCaster TC1", result);
        Assert.StartsWith("GET /version HTTP/1.1", fixture.Request, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("TC2", "TriCaster TC1")]
    [InlineData("TC1", "TriCaster Mini")]
    public async Task RejectsOtherOrConflictingProducts(string model, string name)
    {
        var body = DocumentedBody
            .Replace("<product_model>TC1</product_model>", $"<product_model>{model}</product_model>", StringComparison.Ordinal)
            .Replace("<product_name>TriCaster TC1</product_name>", $"<product_name>{name}</product_name>", StringComparison.Ordinal);
        await using var fixture = await HttpFixture.StartAsync(HttpStatusCode.OK, body);

        var result = await new NewTekTriCasterProtocolProbe(fixture.Port).IdentifyAsync(
            IPAddress.Loopback, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task RejectsDuplicateIdentityFields()
    {
        var body = DocumentedBody.Replace("<product_model>TC1</product_model>",
            "<product_model>TC1</product_model><product_model>TC2</product_model>", StringComparison.Ordinal);
        await using var fixture = await HttpFixture.StartAsync(HttpStatusCode.OK, body);

        var result = await new NewTekTriCasterProtocolProbe(fixture.Port).IdentifyAsync(
            IPAddress.Loopback, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task TreatsAuthenticationChallengeAsSafeFalseNegative()
    {
        await using var fixture = await HttpFixture.StartAsync(HttpStatusCode.Unauthorized, string.Empty);

        var result = await new NewTekTriCasterProtocolProbe(fixture.Port).IdentifyAsync(
            IPAddress.Loopback, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Null(result);
        Assert.StartsWith("GET /version HTTP/1.1", fixture.Request, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DoesNotFollowRedirects()
    {
        await using var fixture = await HttpFixture.StartAsync(HttpStatusCode.Redirect, DocumentedBody);

        var result = await new NewTekTriCasterProtocolProbe(fixture.Port).IdentifyAsync(
            IPAddress.Loopback, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task RejectsDocumentTypeDeclarations()
    {
        var body = "<!DOCTYPE product_information [<!ENTITY xxe SYSTEM 'file:///etc/passwd'>]>" +
            DocumentedBody.Replace("TC1</product_model>", "&xxe;</product_model>", StringComparison.Ordinal);
        await using var fixture = await HttpFixture.StartAsync(HttpStatusCode.OK, body);

        var result = await new NewTekTriCasterProtocolProbe(fixture.Port).IdentifyAsync(
            IPAddress.Loopback, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task RejectsOversizedOrMalformedResponses()
    {
        await using var fixture = await HttpFixture.StartAsync(
            HttpStatusCode.OK, new string('x', 16_385) + DocumentedBody);

        var result = await new NewTekTriCasterProtocolProbe(fixture.Port).IdentifyAsync(
            IPAddress.Loopback, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Null(result);
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
            var bodyBytes = Encoding.UTF8.GetBytes(body);
            var response = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 {(int)status} {status}\r\nContent-Type: application/xml\r\nContent-Length: {bodyBytes.Length}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(response);
            await stream.WriteAsync(bodyBytes);
            return request;
        }
    }
}
