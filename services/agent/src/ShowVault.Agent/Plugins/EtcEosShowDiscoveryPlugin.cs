using Microsoft.Extensions.Options;

namespace ShowVault.Agent.Plugins;

public sealed class EtcEosShowDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : ExactRootFileDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.etc-eos";

    public override AgentPluginManifest Manifest { get; } = new(
        PluginId,
        "ShowVault ETC Eos Show Archive",
        "0.1.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    protected override IReadOnlyList<string> ConfiguredRoots => Options.EtcEosShowArchiveRoots;

    protected override string ProductName => "ETC Eos";

    protected override bool HasExpectedStructure(string rootPath) =>
        ContainsExtension(rootPath, ".esf3d") ||
        ContainsExtension(rootPath, ".esf2") ||
        ContainsExtension(rootPath, ".esf");
}
