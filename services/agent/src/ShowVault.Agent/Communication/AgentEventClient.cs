using System.Net.Http.Headers;
using System.Net.Http.Json;
using ShowVault.Agent.Identity;
using ShowVault.AgentContracts;

namespace ShowVault.Agent.Communication;

public sealed class AgentEventClient(HttpClient client)
{
    public async Task SendAsync(
        StoredAgentIdentity identity,
        AgentEventEnvelope envelope,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agent-events")
        {
            Content = JsonContent.Create(envelope)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "ShowVault-Agent",
            identity.Credential);
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
