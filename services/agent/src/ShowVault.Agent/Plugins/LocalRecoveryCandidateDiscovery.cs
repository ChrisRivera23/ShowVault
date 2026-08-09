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

    public IReadOnlyList<StandardLocationCandidate> GetCandidates()
    {
        if (OperatingSystem.IsMacOS())
        {
            return registry.GetCandidates(
                LocalApplicationPlatform.MacOs,
                ["/Applications"],
                EnumerateUserHomes("/Users").ToArray());
        }

        if (OperatingSystem.IsWindows())
        {
            var applicationRoots = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            }.Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var usersRoot = Path.GetFullPath(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".."));
            return registry.GetCandidates(
                LocalApplicationPlatform.Windows,
                applicationRoots,
                EnumerateUserHomes(usersRoot).ToArray());
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
