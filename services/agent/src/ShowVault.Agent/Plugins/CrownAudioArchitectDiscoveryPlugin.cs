using Microsoft.Extensions.Options;

namespace ShowVault.Agent.Plugins;

public sealed class CrownAudioArchitectDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : ExactRootFileDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.crown-audio-architect";
    public override AgentPluginManifest Manifest { get; } = new(
        PluginId, "ShowVault Crown Audio Architect Venue", "0.1.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });
    protected override IReadOnlyList<string> ConfiguredRoots => Options.CrownAudioArchitectVenueRoots;
    protected override string ProductName => "Crown HiQnet Audio Architect";
    protected override bool HasExpectedStructure(string rootPath) => ContainsExtension(rootPath, ".audioarchitect");
}
