using Microsoft.Extensions.Options;

namespace ShowVault.Agent.Plugins;

public sealed class PeaveyNwareDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : ExactRootFileDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.peavey-nware";

    public override AgentPluginManifest Manifest { get; } = new(
        PluginId,
        "ShowVault Peavey MediaMatrix NWare Project",
        "0.1.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    protected override IReadOnlyList<string> ConfiguredRoots => Options.PeaveyNwareProjectRoots;

    protected override string ProductName => "Peavey MediaMatrix NWare";

    protected override bool HasExpectedStructure(string rootPath) =>
        ContainsExtension(rootPath, ".npa");
}
