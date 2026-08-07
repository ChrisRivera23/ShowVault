using System.Text.Json;

namespace ShowVault.Agent.Identity;

internal static class AgentCredentialSerialization
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize(StoredAgentIdentity identity) =>
        JsonSerializer.Serialize(identity, Options);

    public static StoredAgentIdentity Deserialize(string value) =>
        JsonSerializer.Deserialize<StoredAgentIdentity>(value, Options)
        ?? throw new InvalidOperationException("The stored Agent identity is invalid.");
}
