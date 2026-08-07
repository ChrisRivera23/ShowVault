namespace ShowVault.Agent.Identity;

public interface IAgentCredentialStore
{
    ValueTask<StoredAgentIdentity?> LoadAsync(CancellationToken cancellationToken);
    ValueTask SaveAsync(StoredAgentIdentity identity, CancellationToken cancellationToken);
    ValueTask DeleteAsync(CancellationToken cancellationToken);
}
