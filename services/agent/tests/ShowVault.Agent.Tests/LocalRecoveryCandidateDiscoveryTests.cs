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
    public void Catalog_registry_finds_fixture_backed_dj_candidates(
        LocalApplicationPlatform platform)
    {
        var applicationRoot = Path.Combine(_root, platform.ToString(), "Applications");
        var userHome = Path.Combine(_root, platform.ToString(), "Users", "operator");
        var expectedRekordboxApplication = platform == LocalApplicationPlatform.MacOs
            ? Path.Combine(applicationRoot, "rekordbox 7", "rekordbox.app")
            : Path.Combine(applicationRoot, "Pioneer", "rekordbox 5.8.7", "rekordbox.exe");
        var expectedRekordboxData = platform == LocalApplicationPlatform.MacOs
            ? Path.Combine(userHome, "Library", "Pioneer", "rekordbox")
            : Path.Combine(userHome, "AppData", "Roaming", "Pioneer", "rekordbox");
        Directory.CreateDirectory(Path.GetDirectoryName(expectedRekordboxApplication)!);
        if (Path.HasExtension(expectedRekordboxApplication))
            File.WriteAllText(expectedRekordboxApplication, "fixture");
        else
            Directory.CreateDirectory(expectedRekordboxApplication);
        Directory.CreateDirectory(expectedRekordboxData);
        var expectedTraktorApplication = Path.Combine(
            applicationRoot, "Native Instruments", "Traktor Pro 3");
        var expectedTraktorDatabase = Path.Combine(
            userHome, "Documents", "Native Instruments", "Traktor 3.11.1");
        var expectedTraktorContent = Path.Combine(userHome, "Music", "Traktor");
        Directory.CreateDirectory(expectedTraktorApplication);
        Directory.CreateDirectory(expectedTraktorDatabase);
        Directory.CreateDirectory(expectedTraktorContent);
        var expectedVirtualDjApplication = platform == LocalApplicationPlatform.MacOs
            ? Path.Combine(applicationRoot, "VirtualDJ.app")
            : Path.Combine(applicationRoot, "VirtualDJ", "virtualdj.exe");
        var expectedVirtualDjCurrentData = platform == LocalApplicationPlatform.MacOs
            ? Path.Combine(userHome, "Library", "Application Support", "VirtualDJ")
            : Path.Combine(userHome, "AppData", "Local", "VirtualDJ");
        var expectedVirtualDjLegacyData = Path.Combine(userHome, "Documents", "VirtualDJ");
        Directory.CreateDirectory(Path.GetDirectoryName(expectedVirtualDjApplication)!);
        File.WriteAllText(expectedVirtualDjApplication, "fixture");
        Directory.CreateDirectory(expectedVirtualDjCurrentData);
        Directory.CreateDirectory(expectedVirtualDjLegacyData);
        var expectedEngineDjApplication = platform == LocalApplicationPlatform.MacOs
            ? Path.Combine(applicationRoot, "Engine DJ.app")
            : Path.Combine(applicationRoot, "Engine DJ", "Engine DJ.exe");
        var expectedEngineDjLibrary = Path.Combine(userHome, "Music", "Engine Library");
        Directory.CreateDirectory(Path.GetDirectoryName(expectedEngineDjApplication)!);
        File.WriteAllText(expectedEngineDjApplication, "fixture");
        Directory.CreateDirectory(expectedEngineDjLibrary);
        var expectedDjayApplication = platform == LocalApplicationPlatform.MacOs
            ? Path.Combine(applicationRoot, "djay.app")
            : Path.Combine(userHome, "AppData", "Local", "Packages", "59BEBC1A.djay_e3tqh12mt5rj6");
        var expectedDjayData = Path.Combine(userHome, "Music", "djay");
        var expectedDjayAnalysis = platform == LocalApplicationPlatform.MacOs
            ? Path.Combine(userHome, "Library", "Group Containers", "VJXTL73S8G.com.algoriddim.userdata",
                "Library", "Application Support", "Algoriddim")
            : Path.Combine(expectedDjayApplication, "LocalCache", "Local", "Algoriddim", "djay");
        Directory.CreateDirectory(expectedDjayApplication);
        Directory.CreateDirectory(expectedDjayData);
        Directory.CreateDirectory(expectedDjayAnalysis);
        var expectedMixxxApplication = platform == LocalApplicationPlatform.MacOs
            ? Path.Combine(applicationRoot, "Mixxx.app")
            : Path.Combine(applicationRoot, "Mixxx", "Mixxx.exe");
        var expectedMixxxCurrentData = platform == LocalApplicationPlatform.MacOs
            ? Path.Combine(userHome, "Library", "Containers", "org.mixxx.mixxx", "Data",
                "Library", "Application Support", "Mixxx")
            : Path.Combine(userHome, "AppData", "Local", "Mixxx");
        var expectedMixxxLegacyData = Path.Combine(userHome, "Library", "Application Support", "Mixxx");
        Directory.CreateDirectory(Path.GetDirectoryName(expectedMixxxApplication)!);
        File.WriteAllText(expectedMixxxApplication, "fixture");
        Directory.CreateDirectory(expectedMixxxCurrentData);
        if (platform == LocalApplicationPlatform.MacOs)
            Directory.CreateDirectory(expectedMixxxLegacyData);
        var mountedVolumeRoot = Path.Combine(
            _root,
            platform.ToString(),
            platform == LocalApplicationPlatform.MacOs ? "Volumes" : "Drives",
            "ENGINE_USB");
        var expectedEngineOsLibrary = Path.Combine(mountedVolumeRoot, "Engine Library");
        Directory.CreateDirectory(expectedEngineOsLibrary);

        var registry = new LocalApplicationDetectionRegistry();
        var standardLocations = registry.GetCandidates(
            platform,
            [applicationRoot],
            [userHome],
            [mountedVolumeRoot],
            windowsProgramFilesRoots: [applicationRoot]);
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
        Assert.Contains(standardLocations, location =>
            location.PluginId == LocalApplicationDetectionRegistry.RekordboxPluginId &&
            location.CandidateType == "InstalledApplication" &&
            location.Path == expectedRekordboxApplication);
        Assert.Contains(standardLocations, location =>
            location.PluginId == LocalApplicationDetectionRegistry.RekordboxPluginId &&
            location.CandidateType == "UserDataRoot" &&
            location.Path == expectedRekordboxData);
        Assert.Contains(standardLocations, location =>
            location.PluginId == LocalApplicationDetectionRegistry.TraktorProPluginId &&
            location.CandidateType == "InstalledApplication" &&
            location.Path == expectedTraktorApplication);
        Assert.Contains(standardLocations, location =>
            location.PluginId == LocalApplicationDetectionRegistry.TraktorProPluginId &&
            location.CandidateType == "UserDataRoot" &&
            location.Path == expectedTraktorDatabase);
        Assert.Contains(standardLocations, location =>
            location.PluginId == LocalApplicationDetectionRegistry.TraktorProPluginId &&
            location.CandidateType == "UserDataRoot" &&
            location.Path == expectedTraktorContent);
        Assert.Contains(standardLocations, location =>
            location.PluginId == LocalApplicationDetectionRegistry.VirtualDjPluginId &&
            location.CandidateType == "InstalledApplication" &&
            location.Path == expectedVirtualDjApplication);
        Assert.Contains(standardLocations, location =>
            location.PluginId == LocalApplicationDetectionRegistry.VirtualDjPluginId &&
            location.CandidateType == "UserDataRoot" &&
            location.Path == expectedVirtualDjCurrentData);
        Assert.Contains(standardLocations, location =>
            location.PluginId == LocalApplicationDetectionRegistry.VirtualDjPluginId &&
            location.CandidateType == "UserDataRoot" &&
            location.Path == expectedVirtualDjLegacyData);
        Assert.Contains(standardLocations, location =>
            location.PluginId == LocalApplicationDetectionRegistry.EngineDjPluginId &&
            location.CandidateType == "InstalledApplication" &&
            location.Path == expectedEngineDjApplication);
        Assert.Contains(standardLocations, location =>
            location.PluginId == LocalApplicationDetectionRegistry.EngineDjPluginId &&
            location.CandidateType == "UserDataRoot" &&
            location.Path == expectedEngineDjLibrary);
        Assert.Contains(standardLocations, location =>
            location.PluginId == LocalApplicationDetectionRegistry.DjayProPluginId &&
            location.CandidateType == "InstalledApplication" &&
            location.Path == expectedDjayApplication);
        Assert.Contains(standardLocations, location =>
            location.PluginId == LocalApplicationDetectionRegistry.DjayProPluginId &&
            location.CandidateType == "UserDataRoot" &&
            location.Path == expectedDjayData);
        Assert.Contains(standardLocations, location =>
            location.PluginId == LocalApplicationDetectionRegistry.DjayProPluginId &&
            location.CandidateType == "UserDataRoot" &&
            location.Path == expectedDjayAnalysis);
        Assert.Contains(standardLocations, location =>
            location.PluginId == LocalApplicationDetectionRegistry.MixxxPluginId &&
            location.CandidateType == "InstalledApplication" &&
            location.Path == expectedMixxxApplication);
        Assert.Contains(standardLocations, location =>
            location.PluginId == LocalApplicationDetectionRegistry.MixxxPluginId &&
            location.CandidateType == "UserDataRoot" &&
            location.Path == expectedMixxxCurrentData);
        if (platform == LocalApplicationPlatform.MacOs)
        {
            Assert.Contains(standardLocations, location =>
                location.PluginId == LocalApplicationDetectionRegistry.MixxxPluginId &&
                location.CandidateType == "UserDataRoot" &&
                location.Path == expectedMixxxLegacyData);
        }
        Assert.Contains(standardLocations, location =>
            location.PluginId == LocalApplicationDetectionRegistry.EngineOsPluginId &&
            location.CandidateType == "RemovableDataRoot" &&
            location.Path == expectedEngineOsLibrary);

        foreach (var location in standardLocations.Where(location =>
                     location.PluginId != LocalApplicationDetectionRegistry.RekordboxPluginId &&
                     location.PluginId != LocalApplicationDetectionRegistry.TraktorProPluginId &&
                     location.PluginId != LocalApplicationDetectionRegistry.VirtualDjPluginId &&
                     location.PluginId != LocalApplicationDetectionRegistry.EngineDjPluginId &&
                     location.PluginId != LocalApplicationDetectionRegistry.EngineOsPluginId &&
                     location.PluginId != LocalApplicationDetectionRegistry.DjayProPluginId &&
                     location.PluginId != LocalApplicationDetectionRegistry.MixxxPluginId &&
                     location.PluginId != LocalApplicationDetectionRegistry.DisguiseDesignerPluginId))
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

        Assert.Equal(23, candidates.Count);
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
        Assert.Single(candidates, candidate =>
            candidate.PluginId == LocalApplicationDetectionRegistry.RekordboxPluginId &&
            candidate.CandidateType == "InstalledApplication" &&
            candidate.Path == expectedRekordboxApplication);
        Assert.Single(candidates, candidate =>
            candidate.PluginId == LocalApplicationDetectionRegistry.RekordboxPluginId &&
            candidate.CandidateType == "UserDataRoot" &&
            candidate.Path == expectedRekordboxData);
        Assert.Single(candidates, candidate =>
            candidate.PluginId == LocalApplicationDetectionRegistry.TraktorProPluginId &&
            candidate.CandidateType == "InstalledApplication" &&
            candidate.Path == expectedTraktorApplication);
        Assert.Equal(2, candidates.Count(candidate =>
            candidate.PluginId == LocalApplicationDetectionRegistry.TraktorProPluginId &&
            candidate.CandidateType == "UserDataRoot"));
        Assert.Single(candidates, candidate =>
            candidate.PluginId == LocalApplicationDetectionRegistry.VirtualDjPluginId &&
            candidate.CandidateType == "InstalledApplication" &&
            candidate.Path == expectedVirtualDjApplication);
        Assert.Equal(2, candidates.Count(candidate =>
            candidate.PluginId == LocalApplicationDetectionRegistry.VirtualDjPluginId &&
            candidate.CandidateType == "UserDataRoot"));
        Assert.Single(candidates, candidate =>
            candidate.PluginId == LocalApplicationDetectionRegistry.EngineDjPluginId &&
            candidate.CandidateType == "InstalledApplication" &&
            candidate.Path == expectedEngineDjApplication);
        Assert.Single(candidates, candidate =>
            candidate.PluginId == LocalApplicationDetectionRegistry.EngineDjPluginId &&
            candidate.CandidateType == "UserDataRoot" &&
            candidate.Path == expectedEngineDjLibrary);
        Assert.Single(candidates, candidate =>
            candidate.PluginId == LocalApplicationDetectionRegistry.EngineOsPluginId &&
            candidate.CandidateType == "RemovableDataRoot" &&
            candidate.Path == expectedEngineOsLibrary);
        Assert.Single(candidates, candidate =>
            candidate.PluginId == LocalApplicationDetectionRegistry.DjayProPluginId &&
            candidate.CandidateType == "InstalledApplication" &&
            candidate.Path == expectedDjayApplication);
        Assert.Equal(2, candidates.Count(candidate =>
            candidate.PluginId == LocalApplicationDetectionRegistry.DjayProPluginId &&
            candidate.CandidateType == "UserDataRoot"));
        Assert.Single(candidates, candidate =>
            candidate.PluginId == LocalApplicationDetectionRegistry.MixxxPluginId &&
            candidate.CandidateType == "InstalledApplication" &&
            candidate.Path == expectedMixxxApplication);
        Assert.Equal(platform == LocalApplicationPlatform.MacOs ? 2 : 1, candidates.Count(candidate =>
            candidate.PluginId == LocalApplicationDetectionRegistry.MixxxPluginId &&
            candidate.CandidateType == "UserDataRoot"));
        Assert.All(candidates, candidate => Assert.True(candidate.RequiresOperatorApproval));
    }

    [Fact]
    public void Catalog_registry_finds_only_documented_default_disguise_designer_project_root()
    {
        var applicationRoot = Path.Combine(_root, "Windows", "Applications");
        var userHome = Path.Combine(_root, "Windows", "Users", "operator");
        var projectRoot = Path.Combine(userHome, "Documents", "d3 Projects");
        Directory.CreateDirectory(projectRoot);
        var registry = new LocalApplicationDetectionRegistry();

        var windowsLocations = registry.GetCandidates(
                LocalApplicationPlatform.Windows,
                [applicationRoot],
                [userHome])
            .Where(location =>
                location.PluginId == LocalApplicationDetectionRegistry.DisguiseDesignerPluginId)
            .ToArray();
        var macOsLocations = registry.GetCandidates(
                LocalApplicationPlatform.MacOs,
                [applicationRoot],
                [userHome])
            .Where(location =>
                location.PluginId == LocalApplicationDetectionRegistry.DisguiseDesignerPluginId)
            .ToArray();

        var location = Assert.Single(windowsLocations);
        Assert.Equal("disguise Designer", location.ProductName);
        Assert.Equal("UserDataRoot", location.CandidateType);
        Assert.Equal(projectRoot, location.Path);
        Assert.Equal(
            "Catalog documented default disguise Designer project-root location",
            location.Evidence);
        Assert.Empty(macOsLocations);
        Assert.DoesNotContain(windowsLocations, candidate =>
            candidate.CandidateType == "InstalledApplication");

        var candidate = Assert.Single(new LocalRecoveryCandidateDiscovery(
            new FixedLocationProvider(windowsLocations)).Discover());
        Assert.Equal(LocalApplicationDetectionRegistry.DisguiseDesignerPluginId, candidate.PluginId);
        Assert.Equal(projectRoot, candidate.Path);
        Assert.True(candidate.RequiresOperatorApproval);
    }

    [Fact]
    public void Catalog_registry_finds_only_documented_default_watchout_installation()
    {
        var systemRoot = Path.Combine(_root, "Windows", "SystemDrive");
        var customInstallRoot = Path.Combine(_root, "Windows", "CustomWATCHOUT");
        var defaultInstallRoot = Path.Combine(systemRoot, "WATCHOUT7");
        Directory.CreateDirectory(defaultInstallRoot);
        Directory.CreateDirectory(customInstallRoot);
        var registry = new LocalApplicationDetectionRegistry();

        var windowsLocations = registry.GetCandidates(
                LocalApplicationPlatform.Windows,
                [customInstallRoot],
                [],
                null,
                [systemRoot])
            .Where(location =>
                location.PluginId == LocalApplicationDetectionRegistry.WatchoutPluginId)
            .ToArray();
        var macOsLocations = registry.GetCandidates(
                LocalApplicationPlatform.MacOs,
                [],
                [],
                null,
                [systemRoot])
            .Where(location =>
                location.PluginId == LocalApplicationDetectionRegistry.WatchoutPluginId)
            .ToArray();

        var location = Assert.Single(windowsLocations);
        Assert.Equal("Dataton WATCHOUT 7", location.ProductName);
        Assert.Equal("InstalledApplication", location.CandidateType);
        Assert.Equal(defaultInstallRoot, location.Path);
        Assert.Equal(
            "Catalog documented default WATCHOUT 7 installation location",
            location.Evidence);
        Assert.Empty(macOsLocations);
        Assert.DoesNotContain(windowsLocations, candidate =>
            candidate.CandidateType == "UserDataRoot");
        Assert.DoesNotContain(windowsLocations, candidate =>
            candidate.Path == customInstallRoot);

        var candidate = Assert.Single(new LocalRecoveryCandidateDiscovery(
            new FixedLocationProvider(windowsLocations)).Discover());
        Assert.Equal(LocalApplicationDetectionRegistry.WatchoutPluginId, candidate.PluginId);
        Assert.Equal(defaultInstallRoot, candidate.Path);
        Assert.True(candidate.RequiresOperatorApproval);
    }

    [Fact]
    public void Catalog_registry_finds_only_documented_hippotizer_v4_installation()
    {
        var applicationRoot = Path.Combine(_root, "Windows", "ProgramFiles");
        var programFilesX86Root = Path.Combine(_root, "Windows", "ProgramFilesX86");
        var userHome = Path.Combine(_root, "Windows", "Users", "operator");
        var installationRoot = Path.Combine(applicationRoot, "GreenHippo", "HippotizerV4");
        var undocumentedX86Root = Path.Combine(programFilesX86Root, "GreenHippo", "HippotizerV4");
        var userDefinedShowRoot = Path.Combine(userHome, "Documents", "Hippotizer Shows");
        var configurableStrataRoot = Path.Combine(_root, "Windows", "Media", "STRATA");
        Directory.CreateDirectory(installationRoot);
        Directory.CreateDirectory(undocumentedX86Root);
        Directory.CreateDirectory(userDefinedShowRoot);
        Directory.CreateDirectory(configurableStrataRoot);
        var registry = new LocalApplicationDetectionRegistry();

        var windowsLocations = registry.GetCandidates(
                LocalApplicationPlatform.Windows,
                [programFilesX86Root],
                [userHome],
                windowsProgramFilesRoots: [applicationRoot])
            .Where(location =>
                location.PluginId == LocalApplicationDetectionRegistry.HippotizerPluginId)
            .ToArray();
        var macOsLocations = registry.GetCandidates(
                LocalApplicationPlatform.MacOs,
                [applicationRoot],
                [userHome])
            .Where(location =>
                location.PluginId == LocalApplicationDetectionRegistry.HippotizerPluginId)
            .ToArray();

        var location = Assert.Single(windowsLocations);
        Assert.Equal("Green Hippo Hippotizer V4", location.ProductName);
        Assert.Equal("InstalledApplication", location.CandidateType);
        Assert.Equal(installationRoot, location.Path);
        Assert.Equal(
            "Catalog documented Hippotizer V4 Windows installation location",
            location.Evidence);
        Assert.Empty(macOsLocations);
        Assert.DoesNotContain(windowsLocations, candidate =>
            candidate.CandidateType == "UserDataRoot");
        Assert.DoesNotContain(windowsLocations, candidate =>
            candidate.Path == undocumentedX86Root ||
            candidate.Path == userDefinedShowRoot ||
            candidate.Path == configurableStrataRoot);

        var candidate = Assert.Single(new LocalRecoveryCandidateDiscovery(
            new FixedLocationProvider(windowsLocations)).Discover());
        Assert.Equal(LocalApplicationDetectionRegistry.HippotizerPluginId, candidate.PluginId);
        Assert.Equal(installationRoot, candidate.Path);
        Assert.True(candidate.RequiresOperatorApproval);
    }

    [Fact]
    public void Catalog_registry_finds_only_documented_versioned_pixera_installation()
    {
        var programFilesRoot = Path.Combine(_root, "Windows", "ProgramFiles");
        var programFilesX86Root = Path.Combine(_root, "Windows", "ProgramFilesX86");
        var userHome = Path.Combine(_root, "Windows", "Users", "operator");
        var installationRoot = Path.Combine(
            programFilesRoot, "AV Stumpfl", "Pixera", "build_2-0-172", "presence");
        var undocumentedX86Root = Path.Combine(
            programFilesX86Root, "AV Stumpfl", "Pixera", "build_2-0-172", "presence");
        var nonBuildRoot = Path.Combine(
            programFilesRoot, "AV Stumpfl", "Pixera", "release_2-0-172", "presence");
        var guessedProjectRoot = Path.Combine(userHome, "Documents", "PIXERA Projects");
        Directory.CreateDirectory(installationRoot);
        Directory.CreateDirectory(undocumentedX86Root);
        Directory.CreateDirectory(nonBuildRoot);
        Directory.CreateDirectory(guessedProjectRoot);
        var registry = new LocalApplicationDetectionRegistry();

        var windowsLocations = registry.GetCandidates(
                LocalApplicationPlatform.Windows,
                [programFilesX86Root],
                [userHome],
                windowsProgramFilesRoots: [programFilesRoot])
            .Where(location =>
                location.PluginId == LocalApplicationDetectionRegistry.PixeraPluginId)
            .ToArray();
        var macOsLocations = registry.GetCandidates(
                LocalApplicationPlatform.MacOs,
                [programFilesRoot],
                [userHome])
            .Where(location =>
                location.PluginId == LocalApplicationDetectionRegistry.PixeraPluginId)
            .ToArray();

        var location = Assert.Single(windowsLocations);
        Assert.Equal("AV Stumpfl PIXERA", location.ProductName);
        Assert.Equal("InstalledApplication", location.CandidateType);
        Assert.Equal(installationRoot, location.Path);
        Assert.Equal(
            "Catalog documented versioned PIXERA Windows installation location",
            location.Evidence);
        Assert.Empty(macOsLocations);
        Assert.DoesNotContain(windowsLocations, candidate =>
            candidate.CandidateType == "UserDataRoot");
        Assert.DoesNotContain(windowsLocations, candidate =>
            candidate.Path == undocumentedX86Root ||
            candidate.Path == nonBuildRoot ||
            candidate.Path == guessedProjectRoot);

        var candidate = Assert.Single(new LocalRecoveryCandidateDiscovery(
            new FixedLocationProvider(windowsLocations)).Discover());
        Assert.Equal(LocalApplicationDetectionRegistry.PixeraPluginId, candidate.PluginId);
        Assert.Equal(installationRoot, candidate.Path);
        Assert.True(candidate.RequiresOperatorApproval);
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

    [Fact]
    public void Versioned_application_expansion_is_prefix_scoped_and_bounded()
    {
        var applicationRoot = Path.Combine(_root, "Windows", "Applications");
        var pioneerRoot = Path.Combine(applicationRoot, "Pioneer");
        foreach (var index in Enumerable.Range(0, 33))
            Directory.CreateDirectory(Path.Combine(pioneerRoot, $"rekordbox 5.{index}"));
        Directory.CreateDirectory(Path.Combine(pioneerRoot, "rekordbox 6.0.0"));

        var candidates = new LocalApplicationDetectionRegistry()
            .GetCandidates(LocalApplicationPlatform.Windows, [applicationRoot], [])
            .Where(candidate =>
                candidate.PluginId == LocalApplicationDetectionRegistry.RekordboxPluginId &&
                candidate.CandidateType == "InstalledApplication")
            .ToArray();

        Assert.Equal(32, candidates.Length);
        Assert.All(candidates, candidate =>
            Assert.StartsWith(pioneerRoot, candidate.Path, StringComparison.Ordinal));
        Assert.DoesNotContain(candidates, candidate => candidate.Path.Contains(
            "rekordbox 6", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Mounted_volume_expansion_is_bounded()
    {
        var mountedVolumeRoots = Enumerable.Range(0, 65)
            .Select(index => Path.Combine(_root, "Volumes", index.ToString()))
            .ToArray();

        var candidates = new LocalApplicationDetectionRegistry()
            .GetCandidates(LocalApplicationPlatform.MacOs, [], [], mountedVolumeRoots)
            .Where(candidate =>
                candidate.PluginId == LocalApplicationDetectionRegistry.EngineOsPluginId &&
                candidate.CandidateType == "RemovableDataRoot")
            .ToArray();

        Assert.Equal(64, candidates.Length);
        Assert.DoesNotContain(candidates, candidate => candidate.Path.StartsWith(
            mountedVolumeRoots[64], StringComparison.Ordinal));
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
