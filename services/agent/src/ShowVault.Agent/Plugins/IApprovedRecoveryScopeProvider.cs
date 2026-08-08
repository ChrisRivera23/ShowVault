namespace ShowVault.Agent.Plugins;

public interface IApprovedRecoveryScopeProvider
{
    Task<bool> IsApprovedExactScopeAsync(
        string pluginId,
        string localPath,
        CancellationToken cancellationToken);
}
