using Microsoft.Extensions.Options;

namespace ShowVault.Agent.Plugins;

public sealed class JblVenueSynthesisDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : ExactRootFileDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.jbl-venue-synthesis";

    public override AgentPluginManifest Manifest { get; } = new(
        PluginId, "ShowVault JBL Venue Synthesis Project", "0.1.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    protected override IReadOnlyList<string> ConfiguredRoots => Options.JblVenueSynthesisProjectRoots;

    protected override string ProductName => "JBL Venue Synthesis";

    protected override bool HasExpectedStructure(string rootPath) =>
        Directory.EnumerateFiles(rootPath, "*", SearchOption.TopDirectoryOnly)
            .Any(path => string.Equals(Path.GetExtension(path), ".vysn", StringComparison.OrdinalIgnoreCase) &&
                         new FileInfo(path).Length > 0);
}
