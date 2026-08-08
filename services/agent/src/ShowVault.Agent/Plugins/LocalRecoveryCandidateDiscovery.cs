namespace ShowVault.Agent.Plugins;

public sealed record StandardLocationCandidate(
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

public sealed class HostStandardLocationProvider : IHostStandardLocationProvider
{
    private const int MaximumUserHomeCount = 64;

    public IReadOnlyList<StandardLocationCandidate> GetCandidates()
    {
        var candidates = new List<StandardLocationCandidate>();
        if (OperatingSystem.IsMacOS())
        {
            AddMacOsCandidates(candidates);
        }
        else if (OperatingSystem.IsWindows())
        {
            AddWindowsCandidates(candidates);
        }

        return candidates;
    }

    private static void AddMacOsCandidates(List<StandardLocationCandidate> candidates)
    {
        candidates.Add(new(
            "Resolume Arena",
            "InstalledApplication",
            "/Applications/Resolume Arena.app",
            "Standard macOS application location"));
        candidates.Add(new(
            "Resolume Avenue",
            "InstalledApplication",
            "/Applications/Resolume Avenue.app",
            "Standard macOS application location"));

        foreach (var home in EnumerateUserHomes("/Users"))
        {
            AddResolumeDataCandidates(candidates, Path.Combine(home, "Documents"));
        }
    }

    private static void AddWindowsCandidates(List<StandardLocationCandidate> candidates)
    {
        foreach (var programFiles in new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        }.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            candidates.Add(new(
                "Resolume Arena",
                "InstalledApplication",
                Path.Combine(programFiles, "Resolume Arena"),
                "Standard Windows application location"));
            candidates.Add(new(
                "Resolume Avenue",
                "InstalledApplication",
                Path.Combine(programFiles, "Resolume Avenue"),
                "Standard Windows application location"));
        }

        var usersRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "..");
        foreach (var home in EnumerateUserHomes(Path.GetFullPath(usersRoot)))
        {
            AddResolumeDataCandidates(candidates, Path.Combine(home, "Documents"));
        }
    }

    private static void AddResolumeDataCandidates(
        List<StandardLocationCandidate> candidates,
        string documentsPath)
    {
        candidates.Add(new(
            "Resolume Arena",
            "UserDataRoot",
            Path.Combine(documentsPath, "Resolume Arena"),
            "Standard Resolume user-data location"));
        candidates.Add(new(
            "Resolume Avenue",
            "UserDataRoot",
            Path.Combine(documentsPath, "Resolume Avenue"),
            "Standard Resolume user-data location"));
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
        foreach (var candidate in locationProvider.GetCandidates().Take(MaximumCandidateCount))
        {
            try
            {
                if (!Directory.Exists(candidate.Path) && !File.Exists(candidate.Path))
                {
                    continue;
                }

                results.Add(new LocalRecoveryCandidate(
                    Guid.NewGuid(),
                    ResolumeDiscoveryPlugin.PluginId,
                    candidate.ProductName,
                    candidate.CandidateType,
                    Path.GetFullPath(candidate.Path),
                    candidate.Evidence,
                    true));
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
