using System.Runtime.Versioning;
using Meziantou.Framework.Win32;

namespace ShowVault.Agent.Identity;

[SupportedOSPlatform("windows5.1.2600")]
public sealed class WindowsCredentialStore : IAgentCredentialStore
{
    private const string Target = "ShowVault/VenueAgent";
    private const string UserName = "VenueAgent";

    public ValueTask<StoredAgentState?> LoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var credential = CredentialManager.ReadCredential(Target);
        return ValueTask.FromResult(credential?.Password is not { } value
            ? null
            : AgentCredentialSerialization.Deserialize(value));
    }

    public ValueTask SaveAsync(
        StoredAgentState state,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CredentialManager.WriteCredential(
            Target,
            UserName,
            AgentCredentialSerialization.Serialize(state),
            CredentialPersistence.LocalMachine);
        return ValueTask.CompletedTask;
    }

    public ValueTask DeleteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CredentialManager.DeleteCredential(Target);
        return ValueTask.CompletedTask;
    }
}
