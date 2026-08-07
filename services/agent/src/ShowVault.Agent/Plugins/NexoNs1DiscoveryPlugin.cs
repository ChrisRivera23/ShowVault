using Microsoft.Extensions.Options;

namespace ShowVault.Agent.Plugins;

public sealed class NexoNs1DiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : ExactRootFileDiscoveryPluginBase(options, timeProvider)
{
    private static readonly HashSet<string> ProjectExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".nexo",
        ".nexo3"
    };

    public const string PluginId = "showvault.nexo-ns1";

    public override AgentPluginManifest Manifest { get; } = new(
        PluginId, "ShowVault NEXO NS-1 Project", "0.1.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    protected override IReadOnlyList<string> ConfiguredRoots => Options.NexoNs1ProjectRoots;

    protected override string ProductName => "NEXO NS-1";

    protected override bool HasExpectedStructure(string rootPath) =>
        Directory.EnumerateFiles(rootPath, "*", SearchOption.TopDirectoryOnly)
            .Any(path => ProjectExtensions.Contains(Path.GetExtension(path)) &&
                         new FileInfo(path).Length > 0);
}
