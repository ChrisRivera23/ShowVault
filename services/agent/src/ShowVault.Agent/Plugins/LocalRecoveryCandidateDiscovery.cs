namespace ShowVault.Agent.Plugins;

public sealed record StandardLocationCandidate(
    string PluginId,
    string ProductName,
    string CandidateType,
    string Path,
    string Evidence);

public sealed record LocalRecoveryCandidate(
    Guid CandidateId,
    string PluginId,
    string ProductName,
    string CandidateType,
    string Path,
    string Evidence,
    bool RequiresOperatorApproval);

public interface IHostStandardLocationProvider
{
    IReadOnlyList<StandardLocationCandidate> GetCandidates();
}

public sealed class HostStandardLocationProvider(LocalApplicationDetectionRegistry registry)
    : IHostStandardLocationProvider
{
    private const int MaximumUserHomeCount = 64;
    private const int MaximumMountedVolumeCount = 64;

    public IReadOnlyList<StandardLocationCandidate> GetCandidates()
    {
        if (OperatingSystem.IsMacOS())
        {
            return registry.GetCandidates(
                LocalApplicationPlatform.MacOs,
                ["/Applications"],
                EnumerateUserHomes("/Users").ToArray(),
                EnumerateMountedVolumeRoots(LocalApplicationPlatform.MacOs));
        }

        if (OperatingSystem.IsWindows())
        {
            var programFilesRoot = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var applicationRoots = new[]
            {
                programFilesRoot,
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            }.Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var usersRoot = Path.GetFullPath(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".."));
            var systemRoot = Path.GetPathRoot(
                Environment.GetFolderPath(Environment.SpecialFolder.System));
            var systemRoots = string.IsNullOrWhiteSpace(systemRoot)
                ? []
                : new[] { systemRoot };
            return registry.GetCandidates(
                LocalApplicationPlatform.Windows,
                applicationRoots,
                EnumerateUserHomes(usersRoot).ToArray(),
                EnumerateMountedVolumeRoots(LocalApplicationPlatform.Windows),
                systemRoots,
                string.IsNullOrWhiteSpace(programFilesRoot) ? [] : [programFilesRoot]);
        }

        return [];
    }

    private static IEnumerable<string> EnumerateUserHomes(string usersRoot)
    {
        try
        {
            return Directory.EnumerateDirectories(usersRoot)
                .Where(path => !Path.GetFileName(path).StartsWith(".", StringComparison.Ordinal) &&
                               !string.Equals(Path.GetFileName(path), "Shared", StringComparison.OrdinalIgnoreCase))
                .Take(MaximumUserHomeCount)
                .ToArray();
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static IReadOnlyList<string> EnumerateMountedVolumeRoots(LocalApplicationPlatform platform)
    {
        try
        {
            var comparison = platform == LocalApplicationPlatform.Windows
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
            return DriveInfo.GetDrives()
                .Select(drive => GetEligibleMountedVolumeRoot(drive, platform))
                .Where(path => path is not null)
                .Cast<string>()
                .Distinct(comparison)
                .Take(MaximumMountedVolumeCount)
                .ToArray();
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static string? GetEligibleMountedVolumeRoot(
        DriveInfo drive,
        LocalApplicationPlatform platform)
    {
        try
        {
            if (!drive.IsReady)
                return null;

            var root = drive.RootDirectory.FullName;
            if (platform == LocalApplicationPlatform.MacOs)
            {
                return root.StartsWith("/Volumes/", StringComparison.Ordinal)
                    ? root
                    : null;
            }

            if (drive.DriveType is not (DriveType.Fixed or DriveType.Removable))
                return null;

            var systemRoot = Path.GetPathRoot(
                Environment.GetFolderPath(Environment.SpecialFolder.System));
            return string.Equals(root, systemRoot, StringComparison.OrdinalIgnoreCase)
                ? null
                : root;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}

public sealed class LocalRecoveryCandidateDiscovery(IHostStandardLocationProvider locationProvider)
{
    private const int MaximumCandidateCount = 128;

    public IReadOnlyList<LocalRecoveryCandidate> Discover()
    {
        var results = new List<LocalRecoveryCandidate>();
        foreach (var candidate in locationProvider.GetCandidates())
        {
            try
            {
                if (!Directory.Exists(candidate.Path) && !File.Exists(candidate.Path))
                {
                    continue;
                }

                results.Add(new LocalRecoveryCandidate(
                    Guid.NewGuid(),
                    candidate.PluginId,
                    candidate.ProductName,
                    candidate.CandidateType,
                    Path.GetFullPath(candidate.Path),
                    candidate.Evidence,
                    true));
                if (results.Count == MaximumCandidateCount)
                {
                    break;
                }
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                // An inaccessible standard location is not a discovered candidate.
            }
        }

        return results;
    }
}
