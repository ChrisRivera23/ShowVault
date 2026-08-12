using ShowVault.Agent;
using ShowVault.Agent.Identity;
using ShowVault.Agent.Communication;
using ShowVault.Agent.Execution;
using ShowVault.Agent.Plugins;
using ShowVault.Agent.Recovery;
using ShowVault.Agent.Queue;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddOptions<AgentOptions>()
    .Bind(builder.Configuration.GetSection(AgentOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(
        options => options.ControlPlaneUri.Scheme == Uri.UriSchemeHttps ||
            (options.ControlPlaneUri.Scheme == Uri.UriSchemeHttp && options.ControlPlaneUri.IsLoopback),
        "The control plane URI must use HTTPS except for loopback development.")
    .Validate(
        options => options.DiscoveryRoots.All(Path.IsPathFullyQualified),
        "Every discovery root must be an absolute path.")
    .Validate(
        options => options.ResolumeDiscoveryRoots.Count <=
                ResolumeDiscoveryPlugin.MaximumConfiguredRootCount &&
            options.ResolumeDiscoveryRoots.All(Path.IsPathFullyQualified) &&
            options.ResolumeDiscoveryRoots
                .Select(ResolumeDiscoveryPlugin.NormalizeRoot)
                .Distinct(OperatingSystem.IsWindows()
                    ? StringComparer.OrdinalIgnoreCase
                    : StringComparer.Ordinal)
                .Count() == options.ResolumeDiscoveryRoots.Count,
        "Resolume recovery requires at most 32 unique absolute bundle roots.")
    .Validate(
        options => options.ResolumeUserDataRoots.Count <=
                ResolumeUserDataDiscoveryPlugin.MaximumConfiguredRootCount &&
            options.ResolumeUserDataRoots.All(Path.IsPathFullyQualified) &&
            options.ResolumeUserDataRoots
                .Select(ResolumeDiscoveryPlugin.NormalizeRoot)
                .Distinct(OperatingSystem.IsWindows()
                    ? StringComparer.OrdinalIgnoreCase
                    : StringComparer.Ordinal)
                .Count() == options.ResolumeUserDataRoots.Count,
        "Resolume user-data recovery requires at most 32 unique absolute roots.")
    .Validate(
        options => !options.ResolumeDiscoveryRoots
            .Select(ResolumeDiscoveryPlugin.NormalizeRoot)
            .Intersect(
                options.ResolumeUserDataRoots.Select(ResolumeDiscoveryPlugin.NormalizeRoot),
                OperatingSystem.IsWindows()
                    ? StringComparer.OrdinalIgnoreCase
                    : StringComparer.Ordinal)
            .Any(),
        "A Resolume root cannot be configured as both a portable bundle and user data.")
    .Validate(
        options => options.GrandMa2ShowExportRoots.Count <=
                MaLightingShowExportDiscoveryPluginBase.MaximumConfiguredRootCount &&
            options.GrandMa2ShowExportRoots.All(Path.IsPathFullyQualified) &&
            options.GrandMa2ShowExportRoots
                .Select(MaLightingShowExportDiscoveryPluginBase.NormalizeRoot)
                .Distinct(OperatingSystem.IsWindows()
                    ? StringComparer.OrdinalIgnoreCase
                    : StringComparer.Ordinal)
                .Count() == options.GrandMa2ShowExportRoots.Count,
        "grandMA2 Assisted recovery requires at most 32 unique absolute show-export roots.")
    .Validate(
        options => options.GrandMa3ShowExportRoots.Count <=
                MaLightingShowExportDiscoveryPluginBase.MaximumConfiguredRootCount &&
            options.GrandMa3ShowExportRoots.All(Path.IsPathFullyQualified) &&
            options.GrandMa3ShowExportRoots
                .Select(MaLightingShowExportDiscoveryPluginBase.NormalizeRoot)
                .Distinct(OperatingSystem.IsWindows()
                    ? StringComparer.OrdinalIgnoreCase
                    : StringComparer.Ordinal)
                .Count() == options.GrandMa3ShowExportRoots.Count,
        "grandMA3 Assisted recovery requires at most 32 unique absolute show-export roots.")
    .Validate(
        options => !options.GrandMa2ShowExportRoots
            .Select(MaLightingShowExportDiscoveryPluginBase.NormalizeRoot)
            .Intersect(
                options.GrandMa3ShowExportRoots.Select(
                    MaLightingShowExportDiscoveryPluginBase.NormalizeRoot),
                OperatingSystem.IsWindows()
                    ? StringComparer.OrdinalIgnoreCase
                    : StringComparer.Ordinal)
            .Any(),
        "A grandMA export root cannot be configured for both product profiles.")
    .Validate(
        options => YamahaSettingsExportDiscoveryPluginBase.AreConfiguredRootsValid(
            options.YamahaDm7SettingsExportRoots),
        "Yamaha DM7 Assisted recovery requires at most 32 unique absolute export roots.")
    .Validate(
        options => YamahaSettingsExportDiscoveryPluginBase.AreConfiguredRootsValid(
            options.YamahaRivageSettingsExportRoots),
        "Yamaha RIVAGE Assisted recovery requires at most 32 unique absolute export roots.")
    .Validate(
        options => YamahaSettingsExportDiscoveryPluginBase.AreConfiguredRootsValid(
            options.YamahaClQlSettingsExportRoots),
        "Yamaha CL/QL Assisted recovery requires at most 32 unique absolute export roots.")
    .Validate(
        options => YamahaSettingsExportDiscoveryPluginBase.AreConfiguredRootsValid(
            options.YamahaTfSettingsExportRoots),
        "Yamaha TF Assisted recovery requires at most 32 unique absolute export roots.")
    .Validate(
        options => YamahaSettingsExportDiscoveryPluginBase.AreConfiguredRootsValid(
            options.YamahaDm3SettingsExportRoots),
        "Yamaha DM3 Assisted recovery requires at most 32 unique absolute export roots.")
    .Validate(
        options => YamahaSettingsExportDiscoveryPluginBase.AreConfiguredRootsValid(
            options.YamahaProVisionaireDesignProjectRoots),
        "Yamaha ProVisionaire Design Assisted recovery requires at most 32 unique absolute project roots.")
    .Validate(
        options => YamahaSettingsExportDiscoveryPluginBase.AreConfiguredRootsValid(
            options.YamahaMtxMrxProjectRoots),
        "Yamaha MTX/MRX Assisted recovery requires at most 32 unique absolute project roots.")
    .Validate(
        options => YamahaSettingsExportDiscoveryPluginBase.HaveNoOverlap(
            options.YamahaDm7SettingsExportRoots,
            options.YamahaRivageSettingsExportRoots,
            options.YamahaClQlSettingsExportRoots,
            options.YamahaTfSettingsExportRoots,
            options.YamahaDm3SettingsExportRoots,
            options.YamahaProVisionaireDesignProjectRoots,
            options.YamahaMtxMrxProjectRoots),
        "A Yamaha export root cannot overlap another Yamaha product profile.")
    .Validate(
        options => options.RestoreRoots.All(Path.IsPathFullyQualified),
        "Every restore root must be an absolute path.")
    .Validate(
        options => string.IsNullOrWhiteSpace(options.PackageDirectory) ||
            Path.IsPathFullyQualified(options.PackageDirectory),
        "The package directory must be an absolute path when configured.")
    .ValidateOnStart();

builder.Services.AddHttpClient<AgentEnrollmentClient>((services, client) =>
{
    var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<AgentOptions>>();
    client.BaseAddress = options.Value.ControlPlaneUri;
});
builder.Services.AddHttpClient<AgentEventClient>((services, client) =>
{
    var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<AgentOptions>>();
    client.BaseAddress = options.Value.ControlPlaneUri;
});
builder.Services.AddHttpClient<AgentCommandClient>((services, client) =>
{
    var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<AgentOptions>>();
    client.BaseAddress = options.Value.ControlPlaneUri;
});
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<AgentQueueStore>();
builder.Services.AddSingleton<AgentEventDispatcher>();
builder.Services.AddSingleton<AgentCommandPoller>();
builder.Services.AddSingleton<IDiscoveryPlugin, FileSystemDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, ResolumeDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, ResolumeUserDataDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, GrandMa2ShowExportDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, GrandMa3ShowExportDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, YamahaDm7SettingsExportDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, YamahaRivageSettingsExportDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, YamahaClQlSettingsExportDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, YamahaTfSettingsExportDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, YamahaDm3SettingsExportDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, YamahaProVisionaireDesignProjectDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, YamahaMtxMrxProjectDiscoveryPlugin>();
builder.Services.AddSingleton<DiscoveryPluginRegistry>();
builder.Services.AddSingleton<ISystemInventorySource, PlatformSystemInventorySource>();
builder.Services.AddSingleton<SystemInventoryPlugin>();
builder.Services.AddSingleton<RecoveryPackageWriter>();
builder.Services.AddSingleton<RecoveryPackageVerifier>();
builder.Services.AddSingleton<RecoveryPackageRestorer>();
builder.Services.AddSingleton<AgentCommandExecutor>();
builder.Services.AddSingleton<AgentIdentityBootstrapper>();
if (OperatingSystem.IsWindowsVersionAtLeast(5, 1, 2600))
{
    builder.Services.AddSingleton<IAgentCredentialStore, WindowsCredentialStore>();
}
else if (OperatingSystem.IsMacOS())
{
    builder.Services.AddSingleton<IAgentCredentialStore, MacOsKeychainCredentialStore>();
}
else
{
    throw new PlatformNotSupportedException(
        "ShowVault Agent credential storage currently supports Windows and macOS.");
}

builder.Services.AddHostedService<AgentWorker>();

await builder.Build().RunAsync();
