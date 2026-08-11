namespace ShowVault.Agent.Identity;

public interface IAgentCredentialStore
{
    ValueTask<StoredAgentState?> LoadAsync(CancellationToken cancellationToken);
    ValueTask SaveAsync(StoredAgentState state, CancellationToken cancellationToken);
    ValueTask DeleteAsync(CancellationToken cancellationToken);
}
