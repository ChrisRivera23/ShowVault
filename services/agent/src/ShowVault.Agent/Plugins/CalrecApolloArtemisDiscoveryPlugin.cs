using Microsoft.Extensions.Options;

namespace ShowVault.Agent.Plugins;

public sealed class CalrecApolloArtemisDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : ExactRootFileDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.calrec-apollo-artemis";

    public override AgentPluginManifest Manifest { get; } = new(
        PluginId,
        "ShowVault Calrec Apollo/Artemis Show",
        "0.1.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    protected override IReadOnlyList<string> ConfiguredRoots =>
        Options.CalrecApolloArtemisShowRoots;

    protected override string ProductName => "Calrec Apollo/Artemis";

    protected override bool HasExpectedStructure(string rootPath) =>
        ContainsExtension(rootPath, ".CalrecShow");
}
