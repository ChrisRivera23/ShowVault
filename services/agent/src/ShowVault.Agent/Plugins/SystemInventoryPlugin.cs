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

public sealed record SystemInventoryHostFacts(
    string MachineName,
    string OperatingSystem,
    string OsArchitecture,
    string ProcessArchitecture,
    int LogicalProcessorCount);

public interface ISystemInventorySource
{
    SystemInventoryHostFacts ReadHostFacts();

    IEnumerable<SystemVolume> EnumerateVolumes();
}

public sealed class PlatformSystemInventorySource : ISystemInventorySource
{
    public SystemInventoryHostFacts ReadHostFacts() => new(
        Environment.MachineName,
        RuntimeInformation.OSDescription,
        RuntimeInformation.OSArchitecture.ToString(),
        RuntimeInformation.ProcessArchitecture.ToString(),
        Environment.ProcessorCount);

    public IEnumerable<SystemVolume> EnumerateVolumes() =>
        DriveInfo.GetDrives().Select(ReadVolume);

    private static SystemVolume ReadVolume(DriveInfo drive)
    {
        var name = drive.Name;
        var driveType = drive.DriveType.ToString();
        try
        {
            return new SystemVolume(
                name,
                driveType,
                drive.IsReady ? drive.TotalSize : null,
                drive.IsReady ? drive.AvailableFreeSpace : null);
        }
        catch (IOException)
        {
            return new SystemVolume(name, driveType, null, null);
        }
        catch (UnauthorizedAccessException)
        {
            return new SystemVolume(name, driveType, null, null);
        }
    }
}

public sealed class SystemInventoryPlugin(
    TimeProvider timeProvider,
    ISystemInventorySource inventorySource)
{
    public const string PluginId = "showvault.system-inventory";
    private const int MaximumVolumeCount = 64;
    private const int MaximumMachineNameLength = 255;
    private const int MaximumOperatingSystemLength = 512;
    private const int MaximumArchitectureLength = 64;
    private const int MaximumVolumeNameLength = 1_024;
    private const int MaximumDriveTypeLength = 64;
    private const int MaximumLogicalProcessorCount = 1_048_576;

    public AgentPluginManifest Manifest { get; } = new(
        PluginId,
        "ShowVault System Inventory",
        "0.1.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.SystemInventory },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadSystemInformation });

    public Task<SystemInventoryResult> CollectAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var host = inventorySource.ReadHostFacts();
        ValidateHostFacts(host);
        var volumes = new List<SystemVolume>(MaximumVolumeCount);
        using var enumerator = inventorySource.EnumerateVolumes().GetEnumerator();
        while (volumes.Count < MaximumVolumeCount)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!enumerator.MoveNext())
            {
                break;
            }

            cancellationToken.ThrowIfCancellationRequested();
            ValidateVolume(enumerator.Current);
            volumes.Add(enumerator.Current);
        }

        return Task.FromResult(new SystemInventoryResult(
            Manifest.Id,
            Manifest.Version,
            timeProvider.GetUtcNow(),
            host.MachineName,
            host.OperatingSystem,
            host.OsArchitecture,
            host.ProcessArchitecture,
            host.LogicalProcessorCount,
            volumes));
    }

    private static void ValidateHostFacts(SystemInventoryHostFacts host)
    {
        RequireBounded(host.MachineName, nameof(host.MachineName), MaximumMachineNameLength);
        RequireBounded(
            host.OperatingSystem,
            nameof(host.OperatingSystem),
            MaximumOperatingSystemLength);
        RequireBounded(
            host.OsArchitecture,
            nameof(host.OsArchitecture),
            MaximumArchitectureLength);
        RequireBounded(
            host.ProcessArchitecture,
            nameof(host.ProcessArchitecture),
            MaximumArchitectureLength);
        if (host.LogicalProcessorCount is < 1 or > MaximumLogicalProcessorCount)
        {
            throw new InvalidOperationException("Logical processor count is outside the allowed range.");
        }
    }

    private static void ValidateVolume(SystemVolume volume)
    {
        RequireBounded(volume.Name, nameof(volume.Name), MaximumVolumeNameLength);
        RequireBounded(volume.DriveType, nameof(volume.DriveType), MaximumDriveTypeLength);
        if (volume.TotalBytes < 0 || volume.AvailableBytes < 0 ||
            volume.TotalBytes is null && volume.AvailableBytes is not null ||
            volume.TotalBytes is not null && volume.AvailableBytes > volume.TotalBytes)
        {
            throw new InvalidOperationException("Volume capacity is outside the allowed range.");
        }
    }

    private static void RequireBounded(string value, string fieldName, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength)
        {
            throw new InvalidOperationException($"{fieldName} is outside the allowed range.");
        }
    }
}
