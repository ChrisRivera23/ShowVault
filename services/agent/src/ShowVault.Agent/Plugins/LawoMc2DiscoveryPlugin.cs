using Microsoft.Extensions.Options;

namespace ShowVault.Agent.Plugins;

public sealed class LawoMc2DiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : ExactRootFileDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.lawo-mc2";

    public override AgentPluginManifest Manifest { get; } = new(
        PluginId,
        "ShowVault Lawo mc² Production",
        "0.1.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    protected override IReadOnlyList<string> ConfiguredRoots => Options.LawoMc2ProductionRoots;

    protected override string ProductName => "Lawo mc²";

    protected override bool HasExpectedStructure(string rootPath) =>
        ContainsExtension(rootPath, ".lpn");
}
