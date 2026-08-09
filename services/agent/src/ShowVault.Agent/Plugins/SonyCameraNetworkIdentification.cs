using System.Net;
using System.Text;

namespace ShowVault.Agent.Plugins;

public interface ISonyCameraProtocolProbe
{
    Task<string?> IdentifyAsync(IPAddress address, TimeSpan timeout, CancellationToken cancellationToken);
}

public sealed class SonyCameraProtocolProbe(int port = 80) : ISonyCameraProtocolProbe
{
    private const int MaximumResponseBytes = 16_384;
    private static readonly IReadOnlyDictionary<string, string> ProductFamilies =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["BRC-X400"] = "Sony BRC-X400",
            ["BRC-X401"] = "Sony BRC-X401",
            ["SRG-X400"] = "Sony SRG-X400",
            ["SRG-X402"] = "Sony SRG-X402",
            ["SRG-201M2"] = "Sony SRG-201M2",
            ["SRG-X120"] = "Sony SRG-X120",
            ["SRG-HD1M2"] = "Sony SRG-HD1M2",
            ["SRG-A40"] = "Sony SRG-A40",
            ["SRG-A12"] = "Sony SRG-A12"
        };

    public async Task<string?> IdentifyAsync(
        IPAddress address, TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);
            using var handler = new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.None,
                ConnectTimeout = timeout,
                MaxResponseHeadersLength = 8,
                UseProxy = false,
                UseCookies = false
            };
            using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
            var origin = new UriBuilder(Uri.UriSchemeHttp, address.ToString(), port).Uri;
            var uri = new UriBuilder(Uri.UriSchemeHttp, address.ToString(), port,
                "/command/inquiry.cgi")
            {
                Query = "inq=system"
            }.Uri;
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Referrer = origin;
            using var response = await client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, timeoutSource.Token);
            if (response.StatusCode != HttpStatusCode.OK ||
                response.Content.Headers.ContentLength is > MaximumResponseBytes)
                return null;
            await using var stream = await response.Content.ReadAsStreamAsync(timeoutSource.Token);
            using var bounded = new MemoryStream();
            var buffer = new byte[MaximumResponseBytes + 1];
            while (bounded.Length <= MaximumResponseBytes)
            {
                var remaining = MaximumResponseBytes + 1 - (int)bounded.Length;
                var count = await stream.ReadAsync(buffer.AsMemory(0, remaining), timeoutSource.Token);
                if (count == 0) break;
                await bounded.WriteAsync(buffer.AsMemory(0, count), timeoutSource.Token);
            }
            if (bounded.Length > MaximumResponseBytes) return null;
            var body = Encoding.ASCII.GetString(bounded.GetBuffer(), 0, (int)bounded.Length);
            if (body.EndsWith("\r\n", StringComparison.Ordinal)) body = body[..^2];
            else if (body.EndsWith('\n')) body = body[..^1];
            var values = body.Split('&', StringSplitOptions.None)
                .Where(field => field.StartsWith("ModelName=", StringComparison.Ordinal))
                .Select(field => field["ModelName=".Length..])
                .ToArray();
            return values.Length == 1 && ProductFamilies.TryGetValue(values[0], out var family) ?
                family : null;
        }
        catch (HttpRequestException) { return null; }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return null; }
        catch (IOException) { return null; }
    }
}

public sealed record SonyCameraIdentification(IPAddress Address, string ProductFamily);
public sealed record SonyCameraIdentificationResult(
    Guid ProposalId, Guid DiscoveryCommandId, int AttemptedHostCount,
    IReadOnlyList<SonyCameraIdentification> Identifications, DateTimeOffset CompletedAt);

public sealed class SonyCameraNetworkIdentification(ISonyCameraProtocolProbe probe, TimeProvider timeProvider)
{
    public async Task<SonyCameraIdentificationResult> IdentifyAsync(
        Guid proposalId, Guid discoveryCommandId, IReadOnlyList<string> hosts,
        int timeoutMilliseconds, CancellationToken cancellationToken)
    {
        if (proposalId == Guid.Empty || discoveryCommandId == Guid.Empty || hosts.Count > 32 ||
            timeoutMilliseconds is < 100 or > 500)
            throw new ArgumentException("Sony camera identification authorization is invalid.");
        var identifications = new List<SonyCameraIdentification>();
        foreach (var host in hosts)
        {
            var address = IPAddress.Parse(host);
            var family = await probe.IdentifyAsync(address,
                TimeSpan.FromMilliseconds(timeoutMilliseconds), cancellationToken);
            if (family is not null) identifications.Add(new(address, family));
        }
        return new(proposalId, discoveryCommandId, hosts.Count, identifications, timeProvider.GetUtcNow());
    }
}
