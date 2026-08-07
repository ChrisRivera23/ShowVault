using Microsoft.Extensions.Options;

namespace ShowVault.Agent.Plugins;

public sealed class SslLiveDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : ExactRootFileDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.ssl-live";

    public override AgentPluginManifest Manifest { get; } = new(
        PluginId,
        "ShowVault SSL Live Showfile",
        "0.1.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    protected override IReadOnlyList<string> ConfiguredRoots => Options.SslLiveShowRoots;

    protected override string ProductName => "SSL Live";

    protected override bool HasExpectedStructure(string rootPath) =>
        ContainsExtension(rootPath, ".show");
}
