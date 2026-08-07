using Microsoft.Extensions.Options;

namespace ShowVault.Agent.Plugins;

public sealed class SoundcraftViDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : ExactRootFileDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.soundcraft-vi";

    public override AgentPluginManifest Manifest { get; } = new(
        PluginId,
        "ShowVault Soundcraft Vi Showfolder",
        "0.1.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    protected override IReadOnlyList<string> ConfiguredRoots => Options.SoundcraftViShowRoots;

    protected override string ProductName => "Soundcraft Vi";

    protected override bool HasExpectedStructure(string rootPath) =>
        HasDirectory(rootPath, "Snapshots") &&
        ContainsExtension(Path.Combine(rootPath, "Snapshots"), ".snp");
}
