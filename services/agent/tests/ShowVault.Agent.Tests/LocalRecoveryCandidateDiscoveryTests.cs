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
        var expectedResolumeArenaApplication = platform == LocalApplicationPlatform.MacOs
            ? Path.Combine(applicationRoot, "Resolume Arena", "Arena.app")
            : Path.Combine(applicationRoot, "Resolume Arena");
        var expectedResolumeAvenueApplication = platform == LocalApplicationPlatform.MacOs
            ? Path.Combine(applicationRoot, "Resolume Avenue", "Avenue.app")
            : Path.Combine(applicationRoot, "Resolume Avenue");
        Assert.Contains(standardLocations, location =>
            location.PluginId == ResolumeDiscoveryPlugin.PluginId &&
            location.ProductName == "Resolume Arena" &&
            location.CandidateType == "InstalledApplication" &&
            location.Path == expectedResolumeArenaApplication);
        Assert.Contains(standardLocations, location =>
            location.PluginId == ResolumeDiscoveryPlugin.PluginId &&
            location.ProductName == "Resolume Avenue" &&
            location.CandidateType == "InstalledApplication" &&
            location.Path == expectedResolumeAvenueApplication);
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
                     location.PluginId != LocalApplicationDetectionRegistry.ObsStudioPluginId &&
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

        Assert.Equal(platform == LocalApplicationPlatform.MacOs ? 27 : 26, candidates.Count);
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
    public void Catalog_registry_finds_only_documented_versioned_pandoras_box_installation()
    {
        var programFilesRoot = Path.Combine(_root, "Windows", "ProgramFiles");
        var programFilesX86Root = Path.Combine(_root, "Windows", "ProgramFilesX86");
        var userHome = Path.Combine(_root, "Windows", "Users", "operator");
        var installationRoot = Path.Combine(
            programFilesRoot, "Christie", "Pandoras Box 8.3.0");
        var executablePath = Path.Combine(installationRoot, "PandorasBox.exe");
        var undocumentedX86Root = Path.Combine(
            programFilesX86Root, "Christie", "Pandoras Box 8.3.0");
        var customInstallRoot = Path.Combine(_root, "Windows", "Custom", "Pandoras Box 8.3.0");
        var guessedProjectRoot = Path.Combine(userHome, "Documents", "Pandoras Box Projects");
        Directory.CreateDirectory(installationRoot);
        File.WriteAllText(executablePath, "synthetic executable fixture");
        Directory.CreateDirectory(undocumentedX86Root);
        File.WriteAllText(
            Path.Combine(undocumentedX86Root, "PandorasBox.exe"),
            "synthetic executable fixture");
        Directory.CreateDirectory(customInstallRoot);
        File.WriteAllText(
            Path.Combine(customInstallRoot, "PandorasBox.exe"),
            "synthetic executable fixture");
        Directory.CreateDirectory(guessedProjectRoot);
        File.WriteAllText(
            Path.Combine(guessedProjectRoot, "show.pbb"),
            "synthetic project fixture");
        var registry = new LocalApplicationDetectionRegistry();

        var windowsLocations = registry.GetCandidates(
                LocalApplicationPlatform.Windows,
                [programFilesX86Root, customInstallRoot],
                [userHome],
                windowsProgramFilesRoots: [programFilesRoot])
            .Where(location =>
                location.PluginId == LocalApplicationDetectionRegistry.ChristiePandorasBoxPluginId)
            .ToArray();
        var macOsLocations = registry.GetCandidates(
                LocalApplicationPlatform.MacOs,
                [programFilesRoot],
                [userHome])
            .Where(location =>
                location.PluginId == LocalApplicationDetectionRegistry.ChristiePandorasBoxPluginId)
            .ToArray();

        var location = Assert.Single(windowsLocations);
        Assert.Equal("Christie Pandoras Box", location.ProductName);
        Assert.Equal("InstalledApplication", location.CandidateType);
        Assert.Equal(executablePath, location.Path);
        Assert.Equal(
            "Catalog documented versioned Pandoras Box Windows installation location",
            location.Evidence);
        Assert.Empty(macOsLocations);
        Assert.DoesNotContain(windowsLocations, candidate =>
            candidate.CandidateType == "UserDataRoot");
        Assert.DoesNotContain(windowsLocations, candidate =>
            candidate.Path.StartsWith(undocumentedX86Root, StringComparison.Ordinal) ||
            candidate.Path.StartsWith(customInstallRoot, StringComparison.Ordinal) ||
            candidate.Path.StartsWith(guessedProjectRoot, StringComparison.Ordinal));

        var candidate = Assert.Single(new LocalRecoveryCandidateDiscovery(
            new FixedLocationProvider(windowsLocations)).Discover());
        Assert.Equal(LocalApplicationDetectionRegistry.ChristiePandorasBoxPluginId, candidate.PluginId);
        Assert.Equal(executablePath, candidate.Path);
        Assert.True(candidate.RequiresOperatorApproval);
    }

    [Fact]
    public void Catalog_registry_finds_only_documented_touchdesigner_installations()
    {
        var macApplicationsRoot = Path.Combine(_root, "macOS", "Applications");
        var macApplication = Path.Combine(macApplicationsRoot, "TouchDesigner.app");
        var renamedMacApplication = Path.Combine(macApplicationsRoot, "TouchDesigner Experimental.app");
        var programFilesRoot = Path.Combine(_root, "Windows", "ProgramFiles");
        var programFilesX86Root = Path.Combine(_root, "Windows", "ProgramFilesX86");
        var windowsExecutable = Path.Combine(
            programFilesRoot, "Derivative", "TouchDesigner.2023.12370", "bin", "TouchDesigner.exe");
        var x86Executable = Path.Combine(
            programFilesX86Root, "Derivative", "TouchDesigner.2023.12370", "bin", "TouchDesigner.exe");
        var userHome = Path.Combine(_root, "Windows", "Users", "operator");
        var guessedProjectRoot = Path.Combine(userHome, "Desktop", "TouchDesigner Projects");
        Directory.CreateDirectory(macApplication);
        Directory.CreateDirectory(renamedMacApplication);
        Directory.CreateDirectory(Path.GetDirectoryName(windowsExecutable)!);
        File.WriteAllText(windowsExecutable, "synthetic executable fixture");
        Directory.CreateDirectory(Path.GetDirectoryName(x86Executable)!);
        File.WriteAllText(x86Executable, "synthetic executable fixture");
        Directory.CreateDirectory(guessedProjectRoot);
        File.WriteAllText(Path.Combine(guessedProjectRoot, "show.toe"), "synthetic project fixture");
        var registry = new LocalApplicationDetectionRegistry();

        var macOsLocations = registry.GetCandidates(
                LocalApplicationPlatform.MacOs,
                [macApplicationsRoot],
                [userHome])
            .Where(location => location.PluginId == LocalApplicationDetectionRegistry.TouchDesignerPluginId)
            .ToArray();
        var windowsLocations = registry.GetCandidates(
                LocalApplicationPlatform.Windows,
                [programFilesX86Root],
                [userHome],
                windowsProgramFilesRoots: [programFilesRoot])
            .Where(location => location.PluginId == LocalApplicationDetectionRegistry.TouchDesignerPluginId)
            .ToArray();

        var macLocation = Assert.Single(macOsLocations);
        Assert.Equal(macApplication, macLocation.Path);
        Assert.Equal("InstalledApplication", macLocation.CandidateType);
        Assert.Equal("Derivative TouchDesigner", macLocation.ProductName);
        var windowsLocation = Assert.Single(windowsLocations);
        Assert.Equal(windowsExecutable, windowsLocation.Path);
        Assert.Equal("InstalledApplication", windowsLocation.CandidateType);
        Assert.Equal("Derivative TouchDesigner", windowsLocation.ProductName);
        Assert.DoesNotContain(macOsLocations, candidate => candidate.Path == renamedMacApplication);
        Assert.DoesNotContain(windowsLocations, candidate =>
            candidate.Path == x86Executable || candidate.Path.StartsWith(guessedProjectRoot, StringComparison.Ordinal));
        Assert.All(macOsLocations.Concat(windowsLocations), location =>
            Assert.Equal("InstalledApplication", location.CandidateType));

        var candidates = new LocalRecoveryCandidateDiscovery(
            new FixedLocationProvider(macOsLocations.Concat(windowsLocations).ToArray())).Discover();
        Assert.Equal(2, candidates.Count);
        Assert.All(candidates, candidate =>
        {
            Assert.Equal(LocalApplicationDetectionRegistry.TouchDesignerPluginId, candidate.PluginId);
            Assert.True(candidate.RequiresOperatorApproval);
        });
    }

    [Fact]
    public void Catalog_registry_finds_only_documented_madmapper_6_applications_and_workspaces()
    {
        var macApplicationsRoot = Path.Combine(_root, "macOS", "Applications");
        var macExecutable = Path.Combine(
            macApplicationsRoot, "MadMapper 6.1.0.app", "Contents", "MacOS", "MadMapper");
        var unversionedMacExecutable = Path.Combine(
            macApplicationsRoot, "MadMapper.app", "Contents", "MacOS", "MadMapper");
        var programFilesRoot = Path.Combine(_root, "Windows", "ProgramFiles");
        var windowsExecutable = Path.Combine(
            programFilesRoot, "MadMapper 6.1.0", "MadMapper.exe");
        var programFilesX86Root = Path.Combine(_root, "Windows", "ProgramFilesX86");
        var x86Executable = Path.Combine(
            programFilesX86Root, "MadMapper 6.1.0", "MadMapper.exe");
        var userHome = Path.Combine(_root, "Users", "operator");
        var projectRoot = Path.Combine(
            userHome, "Documents", "MadMapper", "Venue Show.madproject");
        var legacyProjectFile = Path.Combine(
            userHome, "Documents", "MadMapper", "Legacy Show.mad");
        var customProjectRoot = Path.Combine(
            userHome, "Desktop", "Custom Show.madproject");
        Directory.CreateDirectory(Path.GetDirectoryName(macExecutable)!);
        File.WriteAllText(macExecutable, "synthetic executable fixture");
        Directory.CreateDirectory(Path.GetDirectoryName(unversionedMacExecutable)!);
        File.WriteAllText(unversionedMacExecutable, "synthetic executable fixture");
        Directory.CreateDirectory(Path.GetDirectoryName(windowsExecutable)!);
        File.WriteAllText(windowsExecutable, "synthetic executable fixture");
        Directory.CreateDirectory(Path.GetDirectoryName(x86Executable)!);
        File.WriteAllText(x86Executable, "synthetic executable fixture");
        Directory.CreateDirectory(projectRoot);
        File.WriteAllText(legacyProjectFile, "synthetic legacy project fixture");
        Directory.CreateDirectory(customProjectRoot);
        var registry = new LocalApplicationDetectionRegistry();

        var macOsLocations = registry.GetCandidates(
                LocalApplicationPlatform.MacOs,
                [macApplicationsRoot],
                [userHome])
            .Where(location => location.PluginId == LocalApplicationDetectionRegistry.MadMapperPluginId)
            .ToArray();
        var windowsLocations = registry.GetCandidates(
                LocalApplicationPlatform.Windows,
                [programFilesX86Root],
                [userHome],
                windowsProgramFilesRoots: [programFilesRoot])
            .Where(location => location.PluginId == LocalApplicationDetectionRegistry.MadMapperPluginId)
            .ToArray();

        Assert.Collection(
            macOsLocations.OrderBy(location => location.CandidateType),
            location =>
            {
                Assert.Equal("InstalledApplication", location.CandidateType);
                Assert.Equal(macExecutable, location.Path);
                Assert.Equal("MadMapper 6", location.ProductName);
            },
            location =>
            {
                Assert.Equal("ProjectRoot", location.CandidateType);
                Assert.Equal(projectRoot, location.Path);
            });
        Assert.Collection(
            windowsLocations.OrderBy(location => location.CandidateType),
            location =>
            {
                Assert.Equal("InstalledApplication", location.CandidateType);
                Assert.Equal(windowsExecutable, location.Path);
                Assert.Equal("MadMapper 6", location.ProductName);
            },
            location =>
            {
                Assert.Equal("ProjectRoot", location.CandidateType);
                Assert.Equal(projectRoot, location.Path);
            });
        Assert.DoesNotContain(macOsLocations, location => location.Path == unversionedMacExecutable);
        Assert.DoesNotContain(windowsLocations, location => location.Path == x86Executable);
        Assert.DoesNotContain(macOsLocations.Concat(windowsLocations), location =>
            location.Path == legacyProjectFile || location.Path == customProjectRoot);
        Assert.All(macOsLocations.Concat(windowsLocations), location =>
            Assert.StartsWith("Catalog documented", location.Evidence, StringComparison.Ordinal));

        var candidates = new LocalRecoveryCandidateDiscovery(
            new FixedLocationProvider(macOsLocations.Concat(windowsLocations).ToArray())).Discover();
        Assert.Equal(4, candidates.Count);
        Assert.All(candidates, candidate =>
        {
            Assert.Equal(LocalApplicationDetectionRegistry.MadMapperPluginId, candidate.PluginId);
            Assert.True(candidate.RequiresOperatorApproval);
        });
    }

    [Fact]
    public void Catalog_registry_finds_only_documented_isadora_4_applications()
    {
        var macApplicationsRoot = Path.Combine(_root, "macOS", "Applications");
        var macApplication = Path.Combine(macApplicationsRoot, "Isadora 4", "Isadora.app");
        var renamedMacApplication = Path.Combine(macApplicationsRoot, "Isadora 4.1", "Isadora.app");
        var programFilesRoot = Path.Combine(_root, "Windows", "ProgramFiles");
        var windowsApplication = Path.Combine(programFilesRoot, "Isadora 4");
        var programFilesX86Root = Path.Combine(_root, "Windows", "ProgramFilesX86");
        var x86Application = Path.Combine(programFilesX86Root, "Isadora 4");
        var userHome = Path.Combine(_root, "Users", "operator");
        var customProject = Path.Combine(userHome, "Desktop", "Venue Show.izz");
        Directory.CreateDirectory(macApplication);
        Directory.CreateDirectory(renamedMacApplication);
        Directory.CreateDirectory(windowsApplication);
        Directory.CreateDirectory(x86Application);
        Directory.CreateDirectory(Path.GetDirectoryName(customProject)!);
        File.WriteAllText(customProject, "synthetic project fixture");
        var registry = new LocalApplicationDetectionRegistry();

        var macOsLocations = registry.GetCandidates(
                LocalApplicationPlatform.MacOs,
                [macApplicationsRoot],
                [userHome])
            .Where(location => location.PluginId == LocalApplicationDetectionRegistry.IsadoraPluginId)
            .ToArray();
        var windowsLocations = registry.GetCandidates(
                LocalApplicationPlatform.Windows,
                [programFilesX86Root],
                [userHome],
                windowsProgramFilesRoots: [programFilesRoot])
            .Where(location => location.PluginId == LocalApplicationDetectionRegistry.IsadoraPluginId)
            .ToArray();

        var macLocation = Assert.Single(macOsLocations);
        Assert.Equal(macApplication, macLocation.Path);
        Assert.Equal("InstalledApplication", macLocation.CandidateType);
        Assert.Equal("TroikaTronix Isadora 4", macLocation.ProductName);
        var windowsLocation = Assert.Single(windowsLocations);
        Assert.Equal(windowsApplication, windowsLocation.Path);
        Assert.Equal("InstalledApplication", windowsLocation.CandidateType);
        Assert.Equal("TroikaTronix Isadora 4", windowsLocation.ProductName);
        Assert.DoesNotContain(macOsLocations, location => location.Path == renamedMacApplication);
        Assert.DoesNotContain(windowsLocations, location => location.Path == x86Application);
        Assert.DoesNotContain(macOsLocations.Concat(windowsLocations), location =>
            location.Path == customProject || location.CandidateType == "ProjectRoot");
        Assert.All(macOsLocations.Concat(windowsLocations), location =>
            Assert.StartsWith("Catalog documented usual", location.Evidence, StringComparison.Ordinal));

        var candidates = new LocalRecoveryCandidateDiscovery(
            new FixedLocationProvider(macOsLocations.Concat(windowsLocations).ToArray())).Discover();
        Assert.Equal(2, candidates.Count);
        Assert.All(candidates, candidate =>
        {
            Assert.Equal(LocalApplicationDetectionRegistry.IsadoraPluginId, candidate.PluginId);
            Assert.True(candidate.RequiresOperatorApproval);
        });
    }

    [Theory]
    [InlineData(LocalApplicationPlatform.MacOs)]
    [InlineData(LocalApplicationPlatform.Windows)]
    public void Catalog_registry_finds_only_standard_obs_application_profiles_and_scenes(
        LocalApplicationPlatform platform)
    {
        var applicationRoot = Path.Combine(_root, platform.ToString(), "Applications");
        var userHome = Path.Combine(_root, platform.ToString(), "Users", "operator");
        var expectedApplication = platform == LocalApplicationPlatform.MacOs
            ? Path.Combine(applicationRoot, "OBS.app")
            : Path.Combine(applicationRoot, "obs-studio", "bin", "64bit", "obs64.exe");
        var configRoot = platform == LocalApplicationPlatform.MacOs
            ? Path.Combine(userHome, "Library", "Application Support", "obs-studio", "basic")
            : Path.Combine(userHome, "AppData", "Roaming", "obs-studio", "basic");
        var expectedProfiles = Path.Combine(configRoot, "profiles");
        var expectedScenes = Path.Combine(configRoot, "scenes");
        var customApplication = Path.Combine(_root, "PortableOBS", "bin", "64bit", "obs64.exe");
        var customScenes = Path.Combine(_root, "PortableOBS", "config", "obs-studio", "basic", "scenes");
        Directory.CreateDirectory(Path.GetDirectoryName(expectedApplication)!);
        File.WriteAllText(expectedApplication, "synthetic application fixture");
        Directory.CreateDirectory(expectedProfiles);
        Directory.CreateDirectory(expectedScenes);
        Directory.CreateDirectory(Path.GetDirectoryName(customApplication)!);
        File.WriteAllText(customApplication, "synthetic portable application fixture");
        Directory.CreateDirectory(customScenes);

        var locations = new LocalApplicationDetectionRegistry().GetCandidates(
                platform,
                [applicationRoot],
                [userHome],
                windowsProgramFilesRoots: [applicationRoot])
            .Where(location => location.PluginId == LocalApplicationDetectionRegistry.ObsStudioPluginId)
            .ToArray();

        Assert.Collection(
            locations.OrderBy(location => location.CandidateType),
            location =>
            {
                Assert.Equal("InstalledApplication", location.CandidateType);
                Assert.Equal(expectedApplication, location.Path);
            },
            location =>
            {
                Assert.Equal("ProfileRoot", location.CandidateType);
                Assert.Equal(expectedProfiles, location.Path);
            },
            location =>
            {
                Assert.Equal("SceneCollectionRoot", location.CandidateType);
                Assert.Equal(expectedScenes, location.Path);
            });
        Assert.DoesNotContain(locations, location =>
            location.Path == customApplication || location.Path == customScenes);
        Assert.All(locations, location =>
        {
            Assert.Equal("OBS Studio", location.ProductName);
            Assert.StartsWith("Catalog documented standard OBS Studio", location.Evidence,
                StringComparison.Ordinal);
        });

        var candidates = new LocalRecoveryCandidateDiscovery(
            new FixedLocationProvider(locations)).Discover();
        Assert.Equal(3, candidates.Count);
        Assert.All(candidates, candidate =>
        {
            Assert.Equal(LocalApplicationDetectionRegistry.ObsStudioPluginId, candidate.PluginId);
            Assert.True(candidate.RequiresOperatorApproval);
        });
    }

    [Theory]
    [InlineData(LocalApplicationPlatform.MacOs)]
    [InlineData(LocalApplicationPlatform.Windows)]
    public void Catalog_registry_finds_only_standard_propresenter_application_and_recovery_data(
        LocalApplicationPlatform platform)
    {
        var applicationRoot = Path.Combine(_root, platform.ToString(), "Applications");
        var userHome = Path.Combine(_root, platform.ToString(), "Users", "operator");
        var expectedApplication = platform == LocalApplicationPlatform.MacOs
            ? Path.Combine(applicationRoot, "ProPresenter.app")
            : Path.Combine(applicationRoot, "Renewed Vision", "ProPresenter");
        var expectedData = Path.Combine(userHome, "Documents", "ProPresenter");
        var customApplication = Path.Combine(_root, "Custom", "ProPresenter.app");
        var customData = Path.Combine(_root, "Custom", "ProPresenter Data");
        Directory.CreateDirectory(expectedApplication);
        Directory.CreateDirectory(expectedData);
        Directory.CreateDirectory(customApplication);
        Directory.CreateDirectory(customData);

        var locations = new LocalApplicationDetectionRegistry().GetCandidates(
                platform,
                [applicationRoot],
                [userHome],
                windowsProgramFilesRoots: [applicationRoot])
            .Where(location => location.PluginId == LocalApplicationDetectionRegistry.ProPresenterPluginId)
            .ToArray();

        Assert.Collection(
            locations.OrderBy(location => location.CandidateType),
            location =>
            {
                Assert.Equal("InstalledApplication", location.CandidateType);
                Assert.Equal(expectedApplication, location.Path);
            },
            location =>
            {
                Assert.Equal("UserDataRoot", location.CandidateType);
                Assert.Equal(expectedData, location.Path);
            });
        Assert.DoesNotContain(locations, location =>
            location.Path == customApplication || location.Path == customData);
        Assert.All(locations, location =>
        {
            Assert.Equal("ProPresenter", location.ProductName);
            Assert.StartsWith("Catalog documented", location.Evidence, StringComparison.Ordinal);
        });

        var candidates = new LocalRecoveryCandidateDiscovery(
            new FixedLocationProvider(locations)).Discover();
        Assert.Equal(2, candidates.Count);
        Assert.All(candidates, candidate =>
        {
            Assert.Equal(LocalApplicationDetectionRegistry.ProPresenterPluginId, candidate.PluginId);
            Assert.True(candidate.RequiresOperatorApproval);
        });
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
