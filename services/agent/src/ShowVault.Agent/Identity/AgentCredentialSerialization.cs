using System.Text.Json;

namespace ShowVault.Agent.Identity;

internal static class AgentCredentialSerialization
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize(StoredAgentState state) =>
        JsonSerializer.Serialize(ToDocument(state), Options);

    public static StoredAgentState Deserialize(string value)
    {
        var document = JsonSerializer.Deserialize<CredentialStateDocument>(value, Options)
            ?? throw new InvalidOperationException("The stored Agent identity is invalid.");
        return document.Kind switch
        {
            "active" when document.Identity is not null =>
                new ActiveAgentState(document.Identity),
            "pending-enrollment" when document.RequestId is not null &&
                document.EnrollmentCode is not null &&
                document.AgentName is not null &&
                document.CredentialSecret is not null =>
                new PendingAgentEnrollment(
                    document.RequestId.Value,
                    document.EnrollmentCode,
                    document.AgentName,
                    document.CredentialSecret),
            "pending-rotation" when document.RequestId is not null &&
                document.Identity is not null &&
                document.CredentialSecret is not null =>
                new PendingAgentRotation(
                    document.RequestId.Value,
                    document.Identity,
                    document.CredentialSecret),
            _ => throw new InvalidOperationException("The stored Agent identity state is invalid.")
        };
    }

    private static CredentialStateDocument ToDocument(StoredAgentState state) => state switch
    {
        ActiveAgentState active => new("active", active.Identity, null, null, null, null),
        PendingAgentEnrollment pending => new(
            "pending-enrollment",
            null,
            pending.RequestId,
            pending.EnrollmentCode,
            pending.AgentName,
            pending.CredentialSecret),
        PendingAgentRotation pending => new(
            "pending-rotation",
            pending.PreviousIdentity,
            pending.RequestId,
            null,
            null,
            pending.CredentialSecret),
        _ => throw new InvalidOperationException("The Agent credential state is unsupported.")
    };

    private sealed record CredentialStateDocument(
        string Kind,
        StoredAgentIdentity? Identity,
        Guid? RequestId,
        string? EnrollmentCode,
        string? AgentName,
        string? CredentialSecret);
}
