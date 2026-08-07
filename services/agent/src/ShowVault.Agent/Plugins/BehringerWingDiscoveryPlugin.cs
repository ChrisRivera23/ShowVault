using Microsoft.Extensions.Options;

namespace ShowVault.Agent.Plugins;

public sealed class BehringerWingDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : ExactRootFileDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.behringer-wing";

    public override AgentPluginManifest Manifest { get; } = new(
        PluginId,
        "ShowVault Behringer WING Show Folder",
        "0.1.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    protected override IReadOnlyList<string> ConfiguredRoots => Options.BehringerWingShowRoots;

    protected override string ProductName => "Behringer WING";

    protected override bool HasExpectedStructure(string rootPath) =>
        ContainsExtension(rootPath, ".show");
}
