using Microsoft.Extensions.Options;

namespace ShowVault.Agent.Plugins;

public sealed class MeyerSoundMapp3dDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : ExactRootFileDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.meyer-sound-mapp-3d";

    public override AgentPluginManifest Manifest { get; } = new(
        PluginId, "ShowVault Meyer Sound MAPP 3D Project", "0.1.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    protected override IReadOnlyList<string> ConfiguredRoots => Options.MeyerSoundMapp3dProjectRoots;

    protected override string ProductName => "Meyer Sound MAPP 3D";

    protected override bool HasExpectedStructure(string rootPath) =>
        Directory.EnumerateFiles(rootPath, "*", SearchOption.TopDirectoryOnly)
            .Any(path => string.Equals(Path.GetExtension(path), ".mapp", StringComparison.OrdinalIgnoreCase) &&
                         new FileInfo(path).Length > 0);
}
