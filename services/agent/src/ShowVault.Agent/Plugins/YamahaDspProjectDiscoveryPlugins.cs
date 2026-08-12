using Microsoft.Extensions.Options;

namespace ShowVault.Agent.Plugins;

public sealed class YamahaProVisionaireDesignProjectDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : YamahaSettingsExportDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.yamaha-provisionaire-design-project";

    public override AgentPluginManifest Manifest { get; } = new(
        PluginId,
        "ShowVault Yamaha ProVisionaire Design Project Assisted Recovery",
        "1.0.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    protected override IReadOnlyList<string> ConfiguredRoots =>
        Options.YamahaProVisionaireDesignProjectRoots;
}

public sealed class YamahaMtxMrxProjectDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : YamahaSettingsExportDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.yamaha-mtx-mrx-project";

    public override AgentPluginManifest Manifest { get; } = new(
        PluginId,
        "ShowVault Yamaha MTX/MRX Editor Project Assisted Recovery",
        "1.0.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    protected override IReadOnlyList<string> ConfiguredRoots => Options.YamahaMtxMrxProjectRoots;
}
