using System.Net.Http.Headers;
using System.Net.Http.Json;
using ShowVault.Agent.Identity;
using ShowVault.AgentContracts;

namespace ShowVault.Agent.Communication;

public sealed class AgentEventClient(HttpClient client)
{
    public async Task<AgentEventDeliveryResult> SendAsync(
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
        if (response.IsSuccessStatusCode)
        {
            return AgentEventDeliveryResult.Delivered;
        }

        var statusCode = (int)response.StatusCode;
        return response.StatusCode is System.Net.HttpStatusCode.Unauthorized or
                System.Net.HttpStatusCode.Forbidden or
                System.Net.HttpStatusCode.RequestTimeout or
                System.Net.HttpStatusCode.TooManyRequests ||
            statusCode == 425 ||
            statusCode >= 500
            ? AgentEventDeliveryResult.RetryableFailure
            : AgentEventDeliveryResult.PermanentFailure;
    }
}

public enum AgentEventDeliveryResult
{
    Delivered,
    RetryableFailure,
    PermanentFailure
}
