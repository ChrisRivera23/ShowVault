using Microsoft.Extensions.Options;

namespace ShowVault.Agent.Plugins;

public sealed class LabGruppenLakeDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : ExactRootFileDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.lab-gruppen-lake";
    public override AgentPluginManifest Manifest { get; } = new(
        PluginId, "ShowVault Lab Gruppen Lake System", "0.1.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });
    protected override IReadOnlyList<string> ConfiguredRoots => Options.LabGruppenLakeSystemRoots;
    protected override string ProductName => "Lab Gruppen Lake Controller";
    protected override bool HasExpectedStructure(string rootPath) => ContainsExtension(rootPath, ".csc");
}
