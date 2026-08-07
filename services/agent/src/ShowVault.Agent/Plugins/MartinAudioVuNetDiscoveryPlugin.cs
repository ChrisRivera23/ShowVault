using Microsoft.Extensions.Options;

namespace ShowVault.Agent.Plugins;

public sealed class MartinAudioVuNetDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : ExactRootFileDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.martin-audio-vunet";

    public override AgentPluginManifest Manifest { get; } = new(
        PluginId, "ShowVault Martin Audio Vu-Net Project", "0.1.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    protected override IReadOnlyList<string> ConfiguredRoots => Options.MartinAudioVuNetProjectRoots;

    protected override string ProductName => "Martin Audio Vu-Net";

    protected override bool HasExpectedStructure(string rootPath) =>
        Directory.EnumerateFiles(rootPath, "*", SearchOption.TopDirectoryOnly)
            .Any(path => string.Equals(Path.GetExtension(path), ".vun", StringComparison.OrdinalIgnoreCase) &&
                         new FileInfo(path).Length > 0);
}
