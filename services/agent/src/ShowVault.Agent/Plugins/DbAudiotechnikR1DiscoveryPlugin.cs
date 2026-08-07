using Microsoft.Extensions.Options;

namespace ShowVault.Agent.Plugins;

public sealed class DbAudiotechnikR1DiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : ExactRootFileDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.db-audiotechnik-r1";

    public override AgentPluginManifest Manifest { get; } = new(
        PluginId, "ShowVault d&b audiotechnik R1 Project", "0.1.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    protected override IReadOnlyList<string> ConfiguredRoots =>
        Options.DbAudiotechnikR1ProjectRoots;

    protected override string ProductName => "d&b audiotechnik R1";

    protected override bool HasExpectedStructure(string rootPath) =>
        Directory.EnumerateFiles(rootPath, "*", SearchOption.TopDirectoryOnly)
            .Any(path =>
                string.Equals(Path.GetExtension(path), ".dbpr", StringComparison.OrdinalIgnoreCase) &&
                new FileInfo(path).Length > 0);
}
