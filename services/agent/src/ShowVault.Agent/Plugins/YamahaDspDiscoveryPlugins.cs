using Microsoft.Extensions.Options;

namespace ShowVault.Agent.Plugins;

public sealed class YamahaDme7DiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : ExactRootFileDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.yamaha-dme7";

    public override AgentPluginManifest Manifest { get; } = new(
        PluginId,
        "ShowVault Yamaha DME7 ProVisionaire Design Project",
        "0.1.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    protected override IReadOnlyList<string> ConfiguredRoots => Options.YamahaDme7ProjectRoots;

    protected override string ProductName => "Yamaha DME7";

    protected override bool HasExpectedStructure(string rootPath) =>
        ContainsExtension(rootPath, ".pvd");
}

public sealed class YamahaMtxMrxDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : ExactRootFileDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.yamaha-mtx-mrx";

    public override AgentPluginManifest Manifest { get; } = new(
        PluginId,
        "ShowVault Yamaha MTX/MRX Editor Project",
        "0.1.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    protected override IReadOnlyList<string> ConfiguredRoots => Options.YamahaMtxMrxProjectRoots;

    protected override string ProductName => "Yamaha MTX/MRX";

    protected override bool HasExpectedStructure(string rootPath) =>
        ContainsExtension(rootPath, ".mtx");
}
