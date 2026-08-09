using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class LocalRecoveryCandidateDiscoveryTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "showvault-candidate-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Finds_existing_standard_locations_and_requires_operator_approval()
    {
        var application = Path.Combine(_root, "Resolume Arena.app");
        var dataRoot = Path.Combine(_root, "Documents", "Resolume Arena");
        Directory.CreateDirectory(application);
        Directory.CreateDirectory(Path.Combine(dataRoot, "Compositions"));
        var discovery = new LocalRecoveryCandidateDiscovery(new FixedLocationProvider(
        [
            new(ResolumeDiscoveryPlugin.PluginId, "Resolume Arena", "InstalledApplication", application, "standard app"),
            new(ResolumeDiscoveryPlugin.PluginId, "Resolume Arena", "UserDataRoot", dataRoot, "standard data")
        ]));

        var candidates = discovery.Discover();

        Assert.Collection(
            candidates,
            candidate =>
            {
                Assert.NotEqual(Guid.Empty, candidate.CandidateId);
                Assert.Equal("InstalledApplication", candidate.CandidateType);
                Assert.True(candidate.RequiresOperatorApproval);
            },
            candidate =>
            {
                Assert.NotEqual(Guid.Empty, candidate.CandidateId);
                Assert.Equal("UserDataRoot", candidate.CandidateType);
                Assert.Equal(ResolumeDiscoveryPlugin.PluginId, candidate.PluginId);
                Assert.True(candidate.RequiresOperatorApproval);
            });
    }

    [Fact]
    public void Ignores_missing_standard_locations()
    {
        var discovery = new LocalRecoveryCandidateDiscovery(new FixedLocationProvider(
        [
            new(ResolumeDiscoveryPlugin.PluginId, "Resolume Arena", "UserDataRoot", Path.Combine(_root, "missing"), "standard data")
        ]));

        Assert.Empty(discovery.Discover());
    }

    [Theory]
    [InlineData(LocalApplicationPlatform.MacOs)]
    [InlineData(LocalApplicationPlatform.Windows)]
    public void Catalog_registry_finds_fixture_backed_resolume_and_serato_candidates(
        LocalApplicationPlatform platform)
    {
        var applicationRoot = Path.Combine(_root, platform.ToString(), "Applications");
        var userHome = Path.Combine(_root, platform.ToString(), "Users", "operator");
        var registry = new LocalApplicationDetectionRegistry();
        var standardLocations = registry.GetCandidates(platform, [applicationRoot], [userHome]);
        var expectedSeratoApplication = platform == LocalApplicationPlatform.MacOs
            ? Path.Combine(applicationRoot, "Serato DJ Pro.app")
            : Path.Combine(applicationRoot, "Serato", "Serato DJ Pro", "Serato DJ Pro.exe");
        var expectedSeratoData = Path.Combine(userHome, "Music", "_Serato_");
        Assert.Contains(standardLocations, location =>
            location.PluginId == LocalApplicationDetectionRegistry.SeratoDjProPluginId &&
            location.CandidateType == "InstalledApplication" &&
            location.Path == expectedSeratoApplication);
        Assert.Contains(standardLocations, location =>
            location.PluginId == LocalApplicationDetectionRegistry.SeratoDjProPluginId &&
            location.CandidateType == "UserDataRoot" &&
            location.Path == expectedSeratoData);

        foreach (var location in standardLocations)
        {
            if (location.CandidateType == "InstalledApplication" && Path.HasExtension(location.Path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(location.Path)!);
                File.WriteAllText(location.Path, "fixture");
            }
            else
            {
                Directory.CreateDirectory(location.Path);
            }
        }

        var candidates = new LocalRecoveryCandidateDiscovery(
            new FixedLocationProvider(standardLocations)).Discover();

        Assert.Equal(6, candidates.Count);
        Assert.Equal(2, candidates.Count(candidate =>
            candidate.PluginId == ResolumeDiscoveryPlugin.PluginId &&
            candidate.CandidateType == "InstalledApplication"));
        Assert.Equal(2, candidates.Count(candidate =>
            candidate.PluginId == ResolumeDiscoveryPlugin.PluginId &&
            candidate.CandidateType == "UserDataRoot"));
        Assert.Single(candidates, candidate =>
            candidate.PluginId == LocalApplicationDetectionRegistry.SeratoDjProPluginId &&
            candidate.CandidateType == "InstalledApplication");
        Assert.Single(candidates, candidate =>
            candidate.PluginId == LocalApplicationDetectionRegistry.SeratoDjProPluginId &&
            candidate.CandidateType == "UserDataRoot" &&
            Path.GetFileName(candidate.Path) == "_Serato_");
        Assert.All(candidates, candidate => Assert.True(candidate.RequiresOperatorApproval));
    }

    [Fact]
    public void Missing_locations_do_not_consume_the_discovered_candidate_limit()
    {
        var existing = Path.Combine(_root, "existing", "_Serato_");
        Directory.CreateDirectory(existing);
        var locations = Enumerable.Range(0, 128)
            .Select(index => new StandardLocationCandidate(
                ResolumeDiscoveryPlugin.PluginId,
                "Resolume Arena",
                "UserDataRoot",
                Path.Combine(_root, "missing", index.ToString()),
                "standard data"))
            .Append(new StandardLocationCandidate(
                LocalApplicationDetectionRegistry.SeratoDjProPluginId,
                "Serato DJ Pro",
                "UserDataRoot",
                existing,
                "standard data"))
            .ToArray();

        var candidate = Assert.Single(new LocalRecoveryCandidateDiscovery(
            new FixedLocationProvider(locations)).Discover());

        Assert.Equal(LocalApplicationDetectionRegistry.SeratoDjProPluginId, candidate.PluginId);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private sealed class FixedLocationProvider(
        IReadOnlyList<StandardLocationCandidate> candidates) : IHostStandardLocationProvider
    {
        public IReadOnlyList<StandardLocationCandidate> GetCandidates() => candidates;
    }
}
