using Microsoft.Extensions.Options;

namespace ShowVault.Agent.Plugins;

public sealed class DanteControllerDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : ExactRootFileDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.dante-controller";

    public override AgentPluginManifest Manifest { get; } = new(
        PluginId,
        "ShowVault Dante Controller Presets",
        "0.1.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    protected override IReadOnlyList<string> ConfiguredRoots => Options.DanteControllerPresetRoots;

    protected override string ProductName => "Dante Controller";

    protected override bool HasExpectedStructure(string rootPath) =>
        ContainsExtension(rootPath, ".xml");
}
