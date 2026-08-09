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

public sealed record LocalApplicationVersionDirectory(
    string CandidateType,
    string ParentRelativePath,
    string DirectoryNamePrefix,
    string ChildRelativePath,
    string Evidence);

public sealed record LocalApplicationCatalogEntry(
    string PluginId,
    string ProductName,
    IReadOnlyList<LocalApplicationLocation> MacOsApplicationLocations,
    IReadOnlyList<LocalApplicationLocation> WindowsApplicationLocations,
    IReadOnlyList<LocalApplicationLocation> MacOsUserLocations,
    IReadOnlyList<LocalApplicationLocation> WindowsUserLocations)
{
    public IReadOnlyList<LocalApplicationVersionDirectory> MacOsVersionDirectories { get; init; } = [];
    public IReadOnlyList<LocalApplicationVersionDirectory> WindowsVersionDirectories { get; init; } = [];
    public IReadOnlyList<LocalApplicationVersionDirectory> MacOsUserVersionDirectories { get; init; } = [];
    public IReadOnlyList<LocalApplicationVersionDirectory> WindowsUserVersionDirectories { get; init; } = [];
}

public sealed class LocalApplicationDetectionRegistry
{
    public const string SeratoDjProPluginId = "showvault.serato-dj-pro";
    public const string RekordboxPluginId = "showvault.rekordbox";
    public const string TraktorProPluginId = "showvault.traktor-pro";
    public const string VirtualDjPluginId = "showvault.virtualdj";
    private const int MaximumVersionDirectoryCount = 32;

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
            [new("UserDataRoot", Path.Combine("Music", "_Serato_"), "Catalog standard Serato library location")]),
        new(
            RekordboxPluginId,
            "rekordbox",
            [
                new("InstalledApplication", Path.Combine("rekordbox 5", "rekordbox.app"),
                    "Catalog standard rekordbox 5 macOS application location"),
                new("InstalledApplication", Path.Combine("rekordbox 6", "rekordbox.app"),
                    "Catalog standard rekordbox 6 macOS application location"),
                new("InstalledApplication", Path.Combine("rekordbox 7", "rekordbox.app"),
                    "Catalog standard rekordbox 7 macOS application location")
            ],
            [],
            [new("UserDataRoot", Path.Combine("Library", "Pioneer", "rekordbox"),
                "Catalog standard rekordbox database location")],
            [new("UserDataRoot", Path.Combine("AppData", "Roaming", "Pioneer", "rekordbox"),
                "Catalog standard rekordbox database location")])
        {
            WindowsVersionDirectories =
            [
                new(
                    "InstalledApplication",
                    "Pioneer",
                    "rekordbox 5.",
                    "rekordbox.exe",
                    "Catalog documented versioned rekordbox 5 Windows application location")
            ]
        },
        new(
            TraktorProPluginId,
            "Traktor Pro",
            [
                new("InstalledApplication", Path.Combine("Native Instruments", "Traktor Pro 2"),
                    "Catalog standard Traktor Pro 2 macOS application folder"),
                new("InstalledApplication", Path.Combine("Native Instruments", "Traktor Pro 3"),
                    "Catalog standard Traktor Pro 3 macOS application folder")
            ],
            [
                new("InstalledApplication", Path.Combine("Native Instruments", "Traktor Pro 2"),
                    "Catalog standard Traktor Pro 2 Windows application folder"),
                new("InstalledApplication", Path.Combine("Native Instruments", "Traktor Pro 3"),
                    "Catalog standard Traktor Pro 3 Windows application folder")
            ],
            [new("UserDataRoot", Path.Combine("Music", "Traktor"),
                "Catalog standard Traktor generated-content location")],
            [new("UserDataRoot", Path.Combine("Music", "Traktor"),
                "Catalog standard Traktor generated-content location")])
        {
            MacOsUserVersionDirectories =
            [new("UserDataRoot", Path.Combine("Documents", "Native Instruments"), "Traktor ", "",
                "Catalog standard versioned Traktor root database location")],
            WindowsUserVersionDirectories =
            [new("UserDataRoot", Path.Combine("Documents", "Native Instruments"), "Traktor ", "",
                "Catalog standard versioned Traktor root database location")]
        },
        new(
            VirtualDjPluginId,
            "VirtualDJ",
            [new("InstalledApplication", "VirtualDJ.app",
                "Catalog standard macOS application location")],
            [new("InstalledApplication", Path.Combine("VirtualDJ", "virtualdj.exe"),
                "Catalog standard Windows application location")],
            [
                new("UserDataRoot", Path.Combine("Library", "Application Support", "VirtualDJ"),
                    "Catalog current VirtualDJ home location"),
                new("UserDataRoot", Path.Combine("Documents", "VirtualDJ"),
                    "Catalog legacy VirtualDJ home location")
            ],
            [
                new("UserDataRoot", Path.Combine("AppData", "Local", "VirtualDJ"),
                    "Catalog current VirtualDJ home location"),
                new("UserDataRoot", Path.Combine("Documents", "VirtualDJ"),
                    "Catalog legacy VirtualDJ home location")
            ])
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
            var versionDirectories = platform == LocalApplicationPlatform.MacOs
                ? entry.MacOsVersionDirectories
                : entry.WindowsVersionDirectories;
            AddVersionDirectoryCandidates(candidates, entry, applicationRoots, versionDirectories);
        }

        foreach (var userHome in userHomes)
        {
            foreach (var entry in Entries)
            {
                var locations = platform == LocalApplicationPlatform.MacOs
                    ? entry.MacOsUserLocations
                    : entry.WindowsUserLocations;
                AddCandidates(candidates, entry, [userHome], locations);
                var versionDirectories = platform == LocalApplicationPlatform.MacOs
                    ? entry.MacOsUserVersionDirectories
                    : entry.WindowsUserVersionDirectories;
                AddVersionDirectoryCandidates(candidates, entry, [userHome], versionDirectories);
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

    private static void AddVersionDirectoryCandidates(
        List<StandardLocationCandidate> candidates,
        LocalApplicationCatalogEntry entry,
        IReadOnlyList<string> roots,
        IReadOnlyList<LocalApplicationVersionDirectory> locations)
    {
        foreach (var root in roots)
        {
            foreach (var location in locations)
            {
                try
                {
                    var parent = Path.Combine(root, location.ParentRelativePath);
                    var versionDirectories = Directory.EnumerateDirectories(parent)
                        .Where(path => Path.GetFileName(path).StartsWith(
                            location.DirectoryNamePrefix, StringComparison.OrdinalIgnoreCase))
                        .Order(StringComparer.OrdinalIgnoreCase)
                        .Take(MaximumVersionDirectoryCount)
                        .ToArray();
                    foreach (var versionDirectory in versionDirectories)
                    {
                        candidates.Add(new StandardLocationCandidate(
                            entry.PluginId,
                            entry.ProductName,
                            location.CandidateType,
                            Path.Combine(versionDirectory, location.ChildRelativePath),
                            location.Evidence));
                    }
                }
                catch (IOException)
                {
                    // An inaccessible catalog parent contributes no candidates.
                }
                catch (UnauthorizedAccessException)
                {
                    // An inaccessible catalog parent contributes no candidates.
                }
            }
        }
    }
}
