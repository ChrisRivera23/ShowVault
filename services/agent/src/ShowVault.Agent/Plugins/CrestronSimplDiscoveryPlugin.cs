using Microsoft.Extensions.Options;

namespace ShowVault.Agent.Plugins;

public sealed class CrestronSimplDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : ExactRootFileDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.crestron-simpl";

    public override AgentPluginManifest Manifest { get; } = new(
        PluginId,
        "ShowVault Crestron SIMPL Windows Project",
        "0.1.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    protected override IReadOnlyList<string> ConfiguredRoots => Options.CrestronSimplProjectRoots;

    protected override string ProductName => "Crestron SIMPL Windows";

    protected override bool HasExpectedStructure(string rootPath) =>
        ContainsExtension(rootPath, ".smw");
}
