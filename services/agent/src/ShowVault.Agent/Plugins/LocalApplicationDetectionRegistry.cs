namespace ShowVault.Agent.Plugins;

public enum LocalApplicationPlatform
{
    MacOs,
    Windows
}

public sealed record LocalApplicationLocation(
    string CandidateType,
    string RelativePath,
    string Evidence);

public sealed record LocalApplicationCatalogEntry(
    string PluginId,
    string ProductName,
    IReadOnlyList<LocalApplicationLocation> MacOsApplicationLocations,
    IReadOnlyList<LocalApplicationLocation> WindowsApplicationLocations,
    IReadOnlyList<LocalApplicationLocation> MacOsUserLocations,
    IReadOnlyList<LocalApplicationLocation> WindowsUserLocations);

public sealed class LocalApplicationDetectionRegistry
{
    public const string SeratoDjProPluginId = "showvault.serato-dj-pro";

    public IReadOnlyList<LocalApplicationCatalogEntry> Entries { get; } =
    [
        new(
            ResolumeDiscoveryPlugin.PluginId,
            "Resolume Arena",
            [new("InstalledApplication", "Resolume Arena.app", "Catalog standard macOS application location")],
            [new("InstalledApplication", "Resolume Arena", "Catalog standard Windows application location")],
            [new("UserDataRoot", Path.Combine("Documents", "Resolume Arena"), "Catalog standard Resolume user-data location")],
            [new("UserDataRoot", Path.Combine("Documents", "Resolume Arena"), "Catalog standard Resolume user-data location")]),
        new(
            ResolumeDiscoveryPlugin.PluginId,
            "Resolume Avenue",
            [new("InstalledApplication", "Resolume Avenue.app", "Catalog standard macOS application location")],
            [new("InstalledApplication", "Resolume Avenue", "Catalog standard Windows application location")],
            [new("UserDataRoot", Path.Combine("Documents", "Resolume Avenue"), "Catalog standard Resolume user-data location")],
            [new("UserDataRoot", Path.Combine("Documents", "Resolume Avenue"), "Catalog standard Resolume user-data location")]),
        new(
            SeratoDjProPluginId,
            "Serato DJ Pro",
            [new("InstalledApplication", "Serato DJ Pro.app", "Catalog standard macOS application location")],
            [new(
                "InstalledApplication",
                Path.Combine("Serato", "Serato DJ Pro", "Serato DJ Pro.exe"),
                "Catalog standard Windows application location")],
            [new("UserDataRoot", Path.Combine("Music", "_Serato_"), "Catalog standard Serato library location")],
            [new("UserDataRoot", Path.Combine("Music", "_Serato_"), "Catalog standard Serato library location")])
    ];

    public IReadOnlyList<StandardLocationCandidate> GetCandidates(
        LocalApplicationPlatform platform,
        IReadOnlyList<string> applicationRoots,
        IReadOnlyList<string> userHomes)
    {
        var candidates = new List<StandardLocationCandidate>();
        foreach (var entry in Entries)
        {
            var locations = platform == LocalApplicationPlatform.MacOs
                ? entry.MacOsApplicationLocations
                : entry.WindowsApplicationLocations;
            AddCandidates(candidates, entry, applicationRoots, locations);
        }

        foreach (var userHome in userHomes)
        {
            foreach (var entry in Entries)
            {
                var locations = platform == LocalApplicationPlatform.MacOs
                    ? entry.MacOsUserLocations
                    : entry.WindowsUserLocations;
                AddCandidates(candidates, entry, [userHome], locations);
            }
        }

        return candidates;
    }

    private static void AddCandidates(
        List<StandardLocationCandidate> candidates,
        LocalApplicationCatalogEntry entry,
        IReadOnlyList<string> roots,
        IReadOnlyList<LocalApplicationLocation> locations)
    {
        foreach (var root in roots)
        {
            foreach (var location in locations)
            {
                candidates.Add(new StandardLocationCandidate(
                    entry.PluginId,
                    entry.ProductName,
                    location.CandidateType,
                    Path.Combine(root, location.RelativePath),
                    location.Evidence));
            }
        }
    }
}
