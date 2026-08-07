using Microsoft.Extensions.Options;

namespace ShowVault.Agent.Plugins;

public sealed class PowersoftArmoniaPlusDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : ExactRootFileDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.powersoft-armoniaplus";
    public override AgentPluginManifest Manifest { get; } = new(
        PluginId, "ShowVault Powersoft ArmoniaPlus Project", "0.1.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });
    protected override IReadOnlyList<string> ConfiguredRoots => Options.PowersoftArmoniaPlusProjectRoots;
    protected override string ProductName => "Powersoft ArmoniaPlus";
    protected override bool HasExpectedStructure(string rootPath) => ContainsExtension(rootPath, ".paw4");
}
