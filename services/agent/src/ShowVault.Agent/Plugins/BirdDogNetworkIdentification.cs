using System.Net;
using System.Text;

namespace ShowVault.Agent.Plugins;

public interface IBirdDogProtocolProbe
{
    Task<string?> IdentifyAsync(IPAddress address, TimeSpan timeout, CancellationToken cancellationToken);
}

public sealed class BirdDogProtocolProbe(int port = 8_080) : IBirdDogProtocolProbe
{
    private const int MaximumResponseBytes = 64;
    private const string DocumentedIdentifier = "BirdDog P200A4_A5";
    private const string ProductFamily = "BirdDog P200 (A4/A5)";

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
            using var request = new HttpRequestMessage(HttpMethod.Get,
                new UriBuilder(Uri.UriSchemeHttp, address.ToString(), port, "/version").Uri);
            request.Headers.TryAddWithoutValidation("Accept", "text");
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
            return body is DocumentedIdentifier or DocumentedIdentifier + "\n" or
                DocumentedIdentifier + "\r\n" ? ProductFamily : null;
        }
        catch (HttpRequestException) { return null; }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return null; }
        catch (IOException) { return null; }
    }
}

public sealed record BirdDogIdentification(IPAddress Address, string ProductFamily);
public sealed record BirdDogIdentificationResult(
    Guid ProposalId, Guid DiscoveryCommandId, int AttemptedHostCount,
    IReadOnlyList<BirdDogIdentification> Identifications, DateTimeOffset CompletedAt);

public sealed class BirdDogNetworkIdentification(IBirdDogProtocolProbe probe, TimeProvider timeProvider)
{
    public async Task<BirdDogIdentificationResult> IdentifyAsync(
        Guid proposalId, Guid discoveryCommandId, IReadOnlyList<string> hosts,
        int timeoutMilliseconds, CancellationToken cancellationToken)
    {
        if (proposalId == Guid.Empty || discoveryCommandId == Guid.Empty || hosts.Count > 32 ||
            timeoutMilliseconds is < 100 or > 500)
            throw new ArgumentException("BirdDog identification authorization is invalid.");
        var identifications = new List<BirdDogIdentification>();
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
