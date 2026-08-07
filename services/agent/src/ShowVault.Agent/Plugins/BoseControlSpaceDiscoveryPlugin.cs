using Microsoft.Extensions.Options;

namespace ShowVault.Agent.Plugins;

public sealed class BoseControlSpaceDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : ExactRootFileDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.bose-controlspace";

    public override AgentPluginManifest Manifest { get; } = new(
        PluginId,
        "ShowVault Bose ControlSpace Designer Project",
        "0.1.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    protected override IReadOnlyList<string> ConfiguredRoots =>
        Options.BoseControlSpaceProjectRoots;

    protected override string ProductName => "Bose Professional ControlSpace Designer";

    protected override bool HasExpectedStructure(string rootPath) =>
        ContainsExtension(rootPath, ".csp");
}
