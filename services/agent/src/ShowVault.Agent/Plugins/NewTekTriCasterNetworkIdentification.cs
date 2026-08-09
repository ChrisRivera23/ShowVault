using System.Net;
using System.Net.Http.Headers;
using System.Xml;
using System.Xml.Linq;

namespace ShowVault.Agent.Plugins;

public interface INewTekTriCasterProtocolProbe
{
    Task<string?> IdentifyAsync(IPAddress address, TimeSpan timeout, CancellationToken cancellationToken);
}

public sealed class NewTekTriCasterProtocolProbe(int port = 80) : INewTekTriCasterProtocolProbe
{
    private const int MaximumResponseBytes = 16_384;
    private const string ProductFamily = "NewTek TriCaster TC1";

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
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
            using var response = await client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, timeoutSource.Token);
            if (response.StatusCode != HttpStatusCode.OK ||
                response.Content.Headers.ContentLength is > MaximumResponseBytes)
                return null;
            await using var stream = await response.Content.ReadAsStreamAsync(timeoutSource.Token);
            using var bounded = new MemoryStream();
            var buffer = new byte[2_048];
            while (bounded.Length <= MaximumResponseBytes)
            {
                var remaining = MaximumResponseBytes + 1 - (int)bounded.Length;
                var count = await stream.ReadAsync(
                    buffer.AsMemory(0, Math.Min(buffer.Length, remaining)), timeoutSource.Token);
                if (count == 0) break;
                await bounded.WriteAsync(buffer.AsMemory(0, count), timeoutSource.Token);
            }
            if (bounded.Length > MaximumResponseBytes) return null;
            bounded.Position = 0;
            return MatchesDocumentedFixture(bounded) ? ProductFamily : null;
        }
        catch (HttpRequestException) { return null; }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return null; }
        catch (IOException) { return null; }
        catch (XmlException) { return null; }
    }

    private static bool MatchesDocumentedFixture(Stream response)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaximumResponseBytes
        };
        using var reader = XmlReader.Create(response, settings);
        var document = XDocument.Load(reader, LoadOptions.None);
        if (document.Root?.Name != "product_information") return false;
        var models = document.Root.Elements("product_model").Select(item => item.Value).ToArray();
        var names = document.Root.Elements("product_name").Select(item => item.Value).ToArray();
        return models is ["TC1"] && names is ["TriCaster TC1"];
    }
}

public sealed record NewTekTriCasterIdentification(IPAddress Address, string ProductFamily);
public sealed record NewTekTriCasterIdentificationResult(
    Guid ProposalId, Guid DiscoveryCommandId, int AttemptedHostCount,
    IReadOnlyList<NewTekTriCasterIdentification> Identifications, DateTimeOffset CompletedAt);

public sealed class NewTekTriCasterNetworkIdentification(
    INewTekTriCasterProtocolProbe probe, TimeProvider timeProvider)
{
    public async Task<NewTekTriCasterIdentificationResult> IdentifyAsync(
        Guid proposalId, Guid discoveryCommandId, IReadOnlyList<string> hosts,
        int timeoutMilliseconds, CancellationToken cancellationToken)
    {
        if (proposalId == Guid.Empty || discoveryCommandId == Guid.Empty || hosts.Count > 32 ||
            timeoutMilliseconds is < 100 or > 500)
            throw new ArgumentException("NewTek TriCaster identification authorization is invalid.");
        var identifications = new List<NewTekTriCasterIdentification>();
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
