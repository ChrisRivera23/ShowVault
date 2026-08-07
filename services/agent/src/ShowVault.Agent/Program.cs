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
builder.Services.AddSingleton<DiscoveryPluginRegistry>();
builder.Services.AddSingleton<RecoveryPackageWriter>();
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
