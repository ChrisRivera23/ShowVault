using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using ShowVault.AgentContracts;
using Xunit;

namespace ShowVault.Api.Tests;

public sealed class HealthTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HealthTests(WebApplicationFactory<Program> factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Health_endpoint_is_available()
    {
        var response = await _client.GetAsync("/health");
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Agent_protocol_endpoint_describes_current_contract()
    {
        var response = await _client.GetFromJsonAsync<ProtocolResponse>(
            "/api/v1/agent-protocol");

        Assert.NotNull(response);
        Assert.Equal(AgentProtocol.Version, response.Payload.Version);
        Assert.Contains(AgentCommandType.CreateBackup, response.Payload.Commands);
    }

    private sealed record ProtocolResponse(AgentProtocolDescription Payload);
}
