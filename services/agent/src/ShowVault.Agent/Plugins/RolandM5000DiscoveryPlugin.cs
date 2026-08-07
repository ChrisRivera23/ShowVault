using Microsoft.Extensions.Options;

namespace ShowVault.Agent.Plugins;

public sealed class RolandM5000DiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : ExactRootFileDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.roland-m5000";

    public override AgentPluginManifest Manifest { get; } = new(
        PluginId,
        "ShowVault Roland M-5000 Project",
        "0.1.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    protected override IReadOnlyList<string> ConfiguredRoots => Options.RolandM5000ProjectRoots;

    protected override string ProductName => "Roland M-5000/M-5000C";

    protected override bool HasExpectedStructure(string rootPath) =>
        ContainsExtension(rootPath, ".m5pj");
}
