using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using ShowVault.AccountPortal.Configuration;

namespace ShowVault.AccountPortal.Clients;

public sealed class ShowVaultAccountClient(
    HttpClient client,
    IHttpContextAccessor contextAccessor,
    IOptions<AccountPortalOptions> options)
{
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
    };

    public Task<IReadOnlyList<OrganizationView>> OrganizationsAsync(CancellationToken token) =>
        GetAsync<IReadOnlyList<OrganizationView>>("api/v1/organizations", token);
    public Task<IReadOnlyList<MemberView>> MembersAsync(Guid organizationId, CancellationToken token) =>
        GetAsync<IReadOnlyList<MemberView>>($"api/v1/organizations/{organizationId}/account/members", token);
    public Task<IReadOnlyList<InvitationView>> InvitationsAsync(Guid organizationId, CancellationToken token) =>
        GetAsync<IReadOnlyList<InvitationView>>($"api/v1/organizations/{organizationId}/account/invitations", token);
    public Task<CreatedInvitationView> CreateInvitationAsync(Guid organizationId,
        string label, string role, CancellationToken token) => SendAsync<CreatedInvitationView>(
            HttpMethod.Post, $"api/v1/organizations/{organizationId}/account/invitations",
            new { displayLabel = label, role }, token);
    public Task<AcceptedInvitationView> AcceptAsync(string code, CancellationToken token) =>
        SendAsync<AcceptedInvitationView>(HttpMethod.Post,
            "api/v1/account/invitations/accept", new { invitationCode = code }, token);
    public Task<InvitationView> RevokeInvitationAsync(Guid organizationId,
        Guid invitationId, CancellationToken token) => SendAsync<InvitationView>(
            HttpMethod.Post,
            $"api/v1/organizations/{organizationId}/account/invitations/{invitationId}/revoke",
            new { }, token);
    public Task<MemberView> MutateAsync(Guid organizationId, Guid membershipId,
        string action, long revision, string? role, CancellationToken token) =>
        SendAsync<MemberView>(HttpMethod.Patch,
            $"api/v1/organizations/{organizationId}/account/members/{membershipId}",
            new { action, expectedRevision = revision, role }, token);

    private async Task<T> GetAsync<T>(string path, CancellationToken token) =>
        await SendAsync<T>(HttpMethod.Get, path, null, token);

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body,
        CancellationToken token)
    {
        var context = contextAccessor.HttpContext ?? throw new InvalidOperationException();
        var accessToken = await context.GetTokenAsync("access_token");
        if (string.IsNullOrWhiteSpace(accessToken)) throw new UnauthorizedAccessException();
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null) request.Content = JsonContent.Create(body);
        using var response = await client.SendAsync(request,
            HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        await response.Content.LoadIntoBufferAsync(
            options.Value.MaximumApiResponseBytes, token);
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        var envelope = await JsonSerializer.DeserializeAsync<ApiEnvelope<T>>(
            stream, _json, token) ?? throw new InvalidDataException("API response was invalid.");
        return envelope.Payload;
    }
}
