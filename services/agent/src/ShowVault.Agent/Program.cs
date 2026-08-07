using ShowVault.Agent;
using ShowVault.Agent.Identity;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddOptions<AgentOptions>()
    .Bind(builder.Configuration.GetSection(AgentOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(
        options => options.ControlPlaneUri.Scheme == Uri.UriSchemeHttps ||
            (options.ControlPlaneUri.Scheme == Uri.UriSchemeHttp && options.ControlPlaneUri.IsLoopback),
        "The control plane URI must use HTTPS except for loopback development.")
    .ValidateOnStart();

builder.Services.AddHttpClient<AgentEnrollmentClient>((services, client) =>
{
    var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<AgentOptions>>();
    client.BaseAddress = options.Value.ControlPlaneUri;
});
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
