using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace ShowVault.Api.HostedSync;

public sealed class HostedSyncHealthCheck(
    IOptions<HostedSyncOptions> options,
    IServiceProvider services) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (options.Value.Provider == HostedSyncProviders.Disabled)
            {
                return HealthCheckResult.Unhealthy("Hosted synchronization is disabled.");
            }
            if (options.Value.Provider == HostedSyncProviders.FileSystem)
            {
                var root = Path.GetFullPath(options.Value.RootPath);
                Directory.CreateDirectory(root);
                if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
                {
                    return HealthCheckResult.Unhealthy(
                        "Hosted filesystem storage is linked.");
                }
                return HealthCheckResult.Healthy();
            }
            await services.GetRequiredService<IHostedObjectStore>()
                .CheckAvailabilityAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (
            exception is HostedSyncUnavailableException or IOException or
            UnauthorizedAccessException)
        {
            return HealthCheckResult.Unhealthy(
                "Hosted synchronization storage is unavailable.");
        }
    }
}
