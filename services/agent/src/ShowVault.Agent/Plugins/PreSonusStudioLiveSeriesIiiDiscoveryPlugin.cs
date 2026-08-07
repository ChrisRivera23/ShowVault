using Microsoft.Extensions.Options;

namespace ShowVault.Agent.Plugins;

public sealed class PreSonusStudioLiveSeriesIiiDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : ExactRootFileDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.presonus-studiolive-series-iii";

    public override AgentPluginManifest Manifest { get; } = new(
        PluginId,
        "ShowVault PreSonus StudioLive Series III Backup",
        "0.1.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    protected override IReadOnlyList<string> ConfiguredRoots =>
        Options.PreSonusStudioLiveSeriesIiiBackupRoots;

    protected override string ProductName => "PreSonus StudioLive Series III";

    protected override bool HasExpectedStructure(string rootPath) =>
        ContainsExtension(rootPath, ".bak");
}
