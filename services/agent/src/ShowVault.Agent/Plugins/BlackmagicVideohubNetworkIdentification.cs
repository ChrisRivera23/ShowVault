using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ShowVault.Agent.Plugins;

public interface IBlackmagicVideohubProtocolProbe
{
    Task<string?> IdentifyAsync(IPAddress address, TimeSpan timeout, CancellationToken cancellationToken);
}

public sealed class BlackmagicVideohubProtocolProbe : IBlackmagicVideohubProtocolProbe
{
    private const int Port = 9_990;
    private const int MaximumResponseBytes = 4_096;
    private const string ProductFamily = "Blackmagic Smart Videohub 16x16";

    public async Task<string?> IdentifyAsync(
        IPAddress address, TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);
            using var client = new TcpClient(address.AddressFamily);
            await client.ConnectAsync(address, Port, timeoutSource.Token);
            await using var stream = client.GetStream();
            var buffer = new byte[MaximumResponseBytes];
            var received = 0;
            while (received < buffer.Length)
            {
                var count = await stream.ReadAsync(buffer.AsMemory(received), timeoutSource.Token);
                if (count == 0) break;
                received += count;
                var response = NormalizeLineEndings(Encoding.ASCII.GetString(buffer, 0, received));
                if (MatchesDocumentedFixture(response)) return ProductFamily;
            }
            return null;
        }
        catch (SocketException) { return null; }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return null; }
        catch (IOException) { return null; }
    }

    private static bool MatchesDocumentedFixture(string response)
    {
        var blocks = response.Split("\n\n", StringSplitOptions.None);
        if (blocks.Length < 3) return false;
        var preamble = blocks[0].Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (preamble.Length != 2 || preamble[0] != "PROTOCOL PREAMBLE:" ||
            preamble[1] != "Version: 2.3") return false;
        var device = blocks[1].Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (device.Length < 7 || device[0] != "VIDEOHUB DEVICE:") return false;
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in device.Skip(1))
        {
            var separator = line.IndexOf(':');
            if (separator <= 0 || !fields.TryAdd(line[..separator], line[(separator + 1)..].TrimStart()))
                return false;
        }
        return fields.TryGetValue("Device present", out var present) && present == "true" &&
            fields.TryGetValue("Model name", out var model) && model == "Blackmagic Smart Videohub" &&
            fields.TryGetValue("Video inputs", out var inputs) && inputs == "16" &&
            fields.TryGetValue("Video processing units", out var processing) && processing == "0" &&
            fields.TryGetValue("Video outputs", out var outputs) && outputs == "16" &&
            fields.TryGetValue("Video monitoring outputs", out var monitoring) && monitoring == "0" &&
            fields.TryGetValue("Serial ports", out var serial) && serial == "0";
    }

    private static string NormalizeLineEndings(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
}

public sealed record BlackmagicVideohubIdentification(IPAddress Address, string ProductFamily);
public sealed record BlackmagicVideohubIdentificationResult(
    Guid ProposalId, Guid DiscoveryCommandId, int AttemptedHostCount,
    IReadOnlyList<BlackmagicVideohubIdentification> Identifications, DateTimeOffset CompletedAt);

public sealed class BlackmagicVideohubNetworkIdentification(
    IBlackmagicVideohubProtocolProbe probe, TimeProvider timeProvider)
{
    public async Task<BlackmagicVideohubIdentificationResult> IdentifyAsync(
        Guid proposalId, Guid discoveryCommandId, IReadOnlyList<string> hosts,
        int timeoutMilliseconds, CancellationToken cancellationToken)
    {
        if (proposalId == Guid.Empty || discoveryCommandId == Guid.Empty || hosts.Count > 32 ||
            timeoutMilliseconds is < 100 or > 500)
            throw new ArgumentException("Blackmagic Videohub identification authorization is invalid.");
        var identifications = new List<BlackmagicVideohubIdentification>();
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
