using Microsoft.Extensions.Options;

namespace ShowVault.Agent.Plugins;

public sealed class YamahaDm7DiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : ExactRootFileDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.yamaha-dm7";

    public override AgentPluginManifest Manifest { get; } = new(
        PluginId,
        "ShowVault Yamaha DM7 Settings Export",
        "0.1.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    protected override IReadOnlyList<string> ConfiguredRoots => Options.YamahaDm7ExportRoots;

    protected override string ProductName => "Yamaha DM7";

    protected override bool HasExpectedStructure(string rootPath) =>
        ContainsExtension(rootPath, ".dm7f");

    private static bool ContainsExtension(string rootPath, string extension) =>
        Directory.EnumerateFiles(
            rootPath,
            "*",
            new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = false,
                AttributesToSkip = FileAttributes.ReparsePoint
            })
            .Any(path => string.Equals(
                Path.GetExtension(path),
                extension,
                StringComparison.OrdinalIgnoreCase));
}

public sealed class YamahaRivageDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : ExactRootFileDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.yamaha-rivage";
    private static readonly HashSet<string> SettingsExtensions = new(
        [".RIVAGEPM", ".PM10ALL", ".PM7ALL", ".PM10PART", ".PM7PART"],
        StringComparer.OrdinalIgnoreCase);

    public override AgentPluginManifest Manifest { get; } = new(
        PluginId,
        "ShowVault Yamaha RIVAGE PM Settings Export",
        "0.1.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    protected override IReadOnlyList<string> ConfiguredRoots => Options.YamahaRivageExportRoots;

    protected override string ProductName => "Yamaha RIVAGE PM";

    protected override bool HasExpectedStructure(string rootPath) =>
        Directory.EnumerateFiles(
            rootPath,
            "*",
            new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = false,
                AttributesToSkip = FileAttributes.ReparsePoint
            })
            .Any(path => SettingsExtensions.Contains(Path.GetExtension(path)));
}
