using Microsoft.Extensions.Options;

namespace ShowVault.Agent.Plugins;

public sealed class YamahaProVisionaireControlDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : ExactRootFileDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.yamaha-provisionaire-control";

    public override AgentPluginManifest Manifest { get; } = new(
        PluginId,
        "ShowVault Yamaha ProVisionaire Control PLUS Project",
        "0.1.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    protected override IReadOnlyList<string> ConfiguredRoots =>
        Options.YamahaProVisionaireControlProjectRoots;

    protected override string ProductName => "Yamaha ProVisionaire Control PLUS";

    protected override bool HasExpectedStructure(string rootPath) =>
        ContainsExtension(rootPath, ".pvcppj");
}
