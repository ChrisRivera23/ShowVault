using Microsoft.Extensions.Options;

namespace ShowVault.Agent.Plugins;

public sealed class AllenHeathSqShowDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : ExactRootFileDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.allen-heath-sq";

    public override AgentPluginManifest Manifest { get; } = new(
        PluginId,
        "ShowVault Allen & Heath SQ Show",
        "0.1.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    protected override IReadOnlyList<string> ConfiguredRoots => Options.AllenHeathSqShowRoots;

    protected override string ProductName => "Allen & Heath SQ";

    protected override bool HasExpectedStructure(string rootPath)
    {
        var directoryName = Path.GetFileName(rootPath);
        if (directoryName.Length != 8 ||
            !directoryName.StartsWith("SHOW", StringComparison.OrdinalIgnoreCase) ||
            !directoryName[4..].All(char.IsAsciiDigit) ||
            !string.Equals(
                Path.GetFileName(Path.GetDirectoryName(rootPath)),
                "SHOWS",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Directory.EnumerateFiles(rootPath, "*", SearchOption.TopDirectoryOnly)
            .Any(path => string.Equals(
                Path.GetFileName(path),
                "SHOW.DAT",
                StringComparison.OrdinalIgnoreCase));
    }
}
