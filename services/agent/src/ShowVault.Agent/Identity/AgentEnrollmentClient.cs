using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using ShowVault.AgentContracts;

namespace ShowVault.Agent.Identity;

public sealed class AgentEnrollmentClient(HttpClient client)
{
    public async Task<StoredAgentIdentity> EnrollAsync(
        PendingAgentEnrollment pending,
        CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/agents/enroll",
            new EnrollAgentRequest(
                pending.EnrollmentCode,
                pending.AgentName,
                pending.RequestId,
                pending.CredentialSecret),
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<EnrollAgentResponse>>(
            cancellationToken);
        if (envelope is null)
        {
            throw new InvalidOperationException("The enrollment response was empty.");
        }

        var identity = new StoredAgentIdentity(
            envelope.Payload.AgentId,
            envelope.Payload.VenueId,
            envelope.Payload.Credential);
        if (!string.Equals(
                identity.Credential,
                $"{identity.AgentId}.{pending.CredentialSecret}",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The enrollment response did not match the pending credential.");
        }

        return identity;
    }

    public async Task<StoredAgentIdentity> RotateCredentialAsync(
        PendingAgentRotation pending,
        CancellationToken cancellationToken)
    {
        var replacementIdentity = CreateReplacementIdentity(pending);
        var response = await TryRotateCredentialAsync(
            pending.PreviousIdentity,
            pending,
            cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            response = await TryRotateCredentialAsync(
                replacementIdentity,
                pending,
                cancellationToken);
        }

        using (response)
        {
            response.EnsureSuccessStatusCode();

            var envelope = await response.Content.ReadFromJsonAsync<
                ApiEnvelope<RotateAgentCredentialResponse>>(cancellationToken);
            if (envelope is null)
            {
                throw new InvalidOperationException("The credential rotation response was empty.");
            }

            if (!string.Equals(
                    envelope.Payload.Credential,
                    replacementIdentity.Credential,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The credential rotation response did not match the pending credential.");
            }

            return replacementIdentity;
        }
    }

    public static string GenerateCredentialSecret() =>
        $"sva_{Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant()}";

    private async Task<HttpResponseMessage> TryRotateCredentialAsync(
        StoredAgentIdentity authenticationIdentity,
        PendingAgentRotation pending,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/v1/agents/rotate-credential");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "ShowVault-Agent",
            authenticationIdentity.Credential);
        request.Content = JsonContent.Create(new RotateAgentCredentialRequest(
            pending.RequestId,
            pending.CredentialSecret));
        return await client.SendAsync(request, cancellationToken);
    }

    private static StoredAgentIdentity CreateReplacementIdentity(PendingAgentRotation pending) =>
        pending.PreviousIdentity with
        {
            Credential = $"{pending.PreviousIdentity.AgentId}.{pending.CredentialSecret}"
        };

    private sealed record ApiEnvelope<T>(T Payload);
}
