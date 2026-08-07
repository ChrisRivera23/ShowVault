using Microsoft.Extensions.Options;

namespace ShowVault.Agent.Plugins;

public sealed class TascamModelMtrDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : ExactRootFileDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.tascam-model-mtr";

    public override AgentPluginManifest Manifest { get; } = new(
        PluginId,
        "ShowVault Tascam Model-Series MTR Song",
        "0.1.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    protected override IReadOnlyList<string> ConfiguredRoots => Options.TascamModelMtrSongRoots;

    protected override string ProductName => "Tascam Model-series MTR";

    protected override bool HasExpectedStructure(string rootPath) =>
        string.Equals(
            Path.GetFileName(Path.GetDirectoryName(rootPath)),
            "MTR",
            StringComparison.OrdinalIgnoreCase) &&
        ContainsExtension(rootPath, ".wav");
}
