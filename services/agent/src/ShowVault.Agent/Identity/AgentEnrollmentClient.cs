using System.Net.Http.Json;
using System.Net.Http.Headers;
using ShowVault.AgentContracts;

namespace ShowVault.Agent.Identity;

public sealed class AgentEnrollmentClient(HttpClient client)
{
    public async Task<StoredAgentIdentity> EnrollAsync(
        string enrollmentCode,
        string name,
        CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/agents/enroll",
            new EnrollAgentRequest(enrollmentCode, name),
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<EnrollAgentResponse>>(
            cancellationToken);
        if (envelope is null)
        {
            throw new InvalidOperationException("The enrollment response was empty.");
        }

        return new StoredAgentIdentity(
            envelope.Payload.AgentId,
            envelope.Payload.VenueId,
            envelope.Payload.Credential);
    }

    public async Task<StoredAgentIdentity> RotateCredentialAsync(
        StoredAgentIdentity identity,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/v1/agents/rotate-credential");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "ShowVault-Agent",
            identity.Credential);
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<
            ApiEnvelope<RotateAgentCredentialResponse>>(cancellationToken);
        if (envelope is null)
        {
            throw new InvalidOperationException("The credential rotation response was empty.");
        }

        return identity with { Credential = envelope.Payload.Credential };
    }

    private sealed record ApiEnvelope<T>(T Payload);
}
