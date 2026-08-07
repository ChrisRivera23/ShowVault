using Microsoft.Extensions.Options;

namespace ShowVault.Agent.Plugins;

public sealed class QsysDesignerDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : ExactRootFileDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.qsys-designer";

    public override AgentPluginManifest Manifest { get; } = new(
        PluginId,
        "ShowVault Q-SYS Designer Project",
        "0.1.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    protected override IReadOnlyList<string> ConfiguredRoots => Options.QsysDesignerProjectRoots;

    protected override string ProductName => "Q-SYS Designer";

    protected override bool HasExpectedStructure(string rootPath) =>
        ContainsExtension(rootPath, ".qsys");
}
