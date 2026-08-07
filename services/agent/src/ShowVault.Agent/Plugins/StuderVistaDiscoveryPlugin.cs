using Microsoft.Extensions.Options;

namespace ShowVault.Agent.Plugins;

public sealed class StuderVistaDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : ExactRootFileDiscoveryPluginBase(options, timeProvider)
{
    private const string BackupDirectoryPrefix = "BCK_D950_BACKUP";

    public const string PluginId = "showvault.studer-vista";

    public override AgentPluginManifest Manifest { get; } = new(
        PluginId,
        "ShowVault Studer Vista Title Backup",
        "0.1.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    protected override IReadOnlyList<string> ConfiguredRoots =>
        Options.StuderVistaTitleBackupRoots;

    protected override string ProductName => "Studer Vista";

    protected override bool HasExpectedStructure(string rootPath) =>
        Path.GetFileName(rootPath).StartsWith(
            BackupDirectoryPrefix,
            StringComparison.OrdinalIgnoreCase) &&
        Directory.EnumerateFiles(
            rootPath,
            "*",
            new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = false,
                AttributesToSkip = FileAttributes.ReparsePoint
            }).Any();
}
