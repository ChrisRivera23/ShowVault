using Microsoft.Extensions.Options;

namespace ShowVault.Agent.Plugins;

public sealed class AshlyProteaNeDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : ExactRootFileDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.ashly-protea-ne";

    public override AgentPluginManifest Manifest { get; } = new(
        PluginId,
        "ShowVault Ashly Protea NE Project",
        "0.1.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    protected override IReadOnlyList<string> ConfiguredRoots => Options.AshlyProteaNeProjectRoots;

    protected override string ProductName => "Ashly Protea NE";

    protected override bool HasExpectedStructure(string rootPath) =>
        ContainsExtension(rootPath, ".cpj");
}
