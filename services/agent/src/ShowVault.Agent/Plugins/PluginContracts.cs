namespace ShowVault.Agent.Plugins;

public sealed record AgentPluginManifest(
    string Id,
    string Name,
    string Version,
    IReadOnlySet<AgentPluginCapability> Capabilities,
    IReadOnlySet<AgentPluginPermission> Permissions);

public enum AgentPluginCapability
{
    Discovery
}

public enum AgentPluginPermission
{
    ReadFiles
}

public sealed record DiscoveryRequest(string RootPath, int MaxFiles = 1_000);

public sealed record DiscoveryFile(
    string RelativePath,
    long Size,
    DateTimeOffset LastModifiedAt,
    string Sha256);

public sealed record DiscoveryResult(
    string PluginId,
    string PluginVersion,
    string RootPath,
    DateTimeOffset CompletedAt,
    bool Truncated,
    IReadOnlyList<DiscoveryFile> Files);

public interface IDiscoveryPlugin
{
    AgentPluginManifest Manifest { get; }

    Task<DiscoveryResult> DiscoverAsync(
        DiscoveryRequest request,
        CancellationToken cancellationToken);
}
