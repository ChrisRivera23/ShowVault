using Microsoft.Extensions.Options;

namespace ShowVault.Agent.Plugins;

public sealed class DynacordSonicueDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : ExactRootFileDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.dynacord-sonicue";
    public override AgentPluginManifest Manifest { get; } = new(
        PluginId, "ShowVault Dynacord SONICUE Project", "0.1.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });
    protected override IReadOnlyList<string> ConfiguredRoots => Options.DynacordSonicueProjectRoots;
    protected override string ProductName => "Dynacord SONICUE";
    protected override bool HasExpectedStructure(string rootPath) => ContainsExtension(rootPath, ".snc");
}
