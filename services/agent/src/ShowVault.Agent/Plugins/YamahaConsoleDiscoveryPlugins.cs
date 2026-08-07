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

public sealed class YamahaClQlDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : ExactRootFileDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.yamaha-cl-ql";

    public override AgentPluginManifest Manifest { get; } = new(
        PluginId,
        "ShowVault Yamaha CL/QL Settings Export",
        "0.1.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    protected override IReadOnlyList<string> ConfiguredRoots => Options.YamahaClQlExportRoots;

    protected override string ProductName => "Yamaha CL/QL";

    protected override bool HasExpectedStructure(string rootPath) =>
        ContainsExtension(rootPath, ".clf");
}

public sealed class YamahaTfDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : ExactRootFileDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.yamaha-tf";

    public override AgentPluginManifest Manifest { get; } = new(
        PluginId,
        "ShowVault Yamaha TF Settings Export",
        "0.1.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    protected override IReadOnlyList<string> ConfiguredRoots => Options.YamahaTfExportRoots;

    protected override string ProductName => "Yamaha TF";

    protected override bool HasExpectedStructure(string rootPath) =>
        ContainsExtension(rootPath, ".tff");
}
