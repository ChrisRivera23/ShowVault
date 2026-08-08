using System.Net;

namespace ShowVault.Agent.Plugins;

public interface IMaLightingProtocolProbe
{
    Task<string?> IdentifyAsync(IPAddress address, TimeSpan timeout, CancellationToken cancellationToken);
}

public sealed class GrandMa3WebRemoteProbe : IMaLightingProtocolProbe
{
    private const int MaximumEvidenceBytes = 65_536;

    public async Task<string?> IdentifyAsync(
        IPAddress address,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        try
        {
            using var client = new HttpClient { Timeout = timeout };
            using var response = await client.GetAsync(
                new UriBuilder(Uri.UriSchemeHttp, address.ToString(), 8080, "/").Uri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var buffer = new byte[MaximumEvidenceBytes];
            var length = await stream.ReadAsync(buffer, cancellationToken);
            var content = System.Text.Encoding.UTF8.GetString(buffer, 0, length);
            return content.Contains("grandMA3", StringComparison.OrdinalIgnoreCase) ? "grandMA3" : null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }
}

public sealed record MaLightingIdentification(IPAddress Address, string ProductFamily);

public sealed record MaLightingIdentificationResult(
    Guid ProposalId,
    Guid DiscoveryCommandId,
    int AttemptedHostCount,
    IReadOnlyList<MaLightingIdentification> Identifications,
    DateTimeOffset CompletedAt);

public sealed class MaLightingNetworkIdentification(
    IMaLightingProtocolProbe probe,
    TimeProvider timeProvider)
{
    public async Task<MaLightingIdentificationResult> IdentifyAsync(
        Guid proposalId,
        Guid discoveryCommandId,
        IReadOnlyList<string> hosts,
        int timeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        if (proposalId == Guid.Empty || discoveryCommandId == Guid.Empty || hosts.Count > 32 ||
            timeoutMilliseconds is < 100 or > 500)
            throw new ArgumentException("MA Lighting identification authorization is invalid.");

        var identifications = new List<MaLightingIdentification>();
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
