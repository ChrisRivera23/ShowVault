using System.Net.Http.Headers;
using System.Net.Http.Json;
using ShowVault.Agent.Identity;
using ShowVault.AgentContracts;

namespace ShowVault.Agent.Communication;

public sealed class AgentCommandClient(HttpClient client)
{
    public async Task<IReadOnlyList<AgentCommandEnvelope>> PollAsync(
        StoredAgentIdentity identity,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, "/api/v1/agent-commands", identity);
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<CommandPollResponse>(
            cancellationToken: cancellationToken);
        return body?.Payload ?? [];
    }

    public async Task AcknowledgeAsync(
        StoredAgentIdentity identity,
        Guid commandId,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(
            HttpMethod.Post,
            $"/api/v1/agent-commands/{commandId}/acknowledge",
            identity);
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        string path,
        StoredAgentIdentity identity)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "ShowVault-Agent",
            identity.Credential);
        return request;
    }

    private sealed record CommandPollResponse(IReadOnlyList<AgentCommandEnvelope>? Payload);
}
