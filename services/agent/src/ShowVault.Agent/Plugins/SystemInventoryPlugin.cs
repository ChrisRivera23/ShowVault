using System.Runtime.InteropServices;

namespace ShowVault.Agent.Plugins;

public sealed record SystemVolume(
    string Name,
    string DriveType,
    long? TotalBytes,
    long? AvailableBytes);

public sealed record SystemInventoryResult(
    string PluginId,
    string PluginVersion,
    DateTimeOffset CollectedAt,
    string MachineName,
    string OperatingSystem,
    string OsArchitecture,
    string ProcessArchitecture,
    int LogicalProcessorCount,
    IReadOnlyList<SystemVolume> Volumes);

public sealed class SystemInventoryPlugin(TimeProvider timeProvider)
{
    public const string PluginId = "showvault.system-inventory";
    private const int MaximumVolumeCount = 64;

    public AgentPluginManifest Manifest { get; } = new(
        PluginId,
        "ShowVault System Inventory",
        "0.1.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.SystemInventory },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadSystemInformation });

    public Task<SystemInventoryResult> CollectAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var volumes = DriveInfo.GetDrives()
            .Take(MaximumVolumeCount)
            .Select(ReadVolume)
            .ToArray();

        return Task.FromResult(new SystemInventoryResult(
            Manifest.Id,
            Manifest.Version,
            timeProvider.GetUtcNow(),
            Environment.MachineName,
            RuntimeInformation.OSDescription,
            RuntimeInformation.OSArchitecture.ToString(),
            RuntimeInformation.ProcessArchitecture.ToString(),
            Environment.ProcessorCount,
            volumes));
    }

    private static SystemVolume ReadVolume(DriveInfo drive)
    {
        try
        {
            return new SystemVolume(
                drive.Name,
                drive.DriveType.ToString(),
                drive.IsReady ? drive.TotalSize : null,
                drive.IsReady ? drive.AvailableFreeSpace : null);
        }
        catch (IOException)
        {
            return new SystemVolume(drive.Name, drive.DriveType.ToString(), null, null);
        }
        catch (UnauthorizedAccessException)
        {
            return new SystemVolume(drive.Name, drive.DriveType.ToString(), null, null);
        }
    }
}
