using Amazon;
using Amazon.S3;
using Microsoft.Extensions.Options;

namespace ShowVault.Api.HostedSync;

public static class HostedSyncServiceCollectionExtensions
{
    public static IServiceCollection AddHostedSync(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<HostedSyncOptions>()
            .Bind(configuration.GetSection("HostedSync"))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<HostedSyncOptions>,
            HostedSyncOptionsValidator>();
        services.AddSingleton<HostedSyncStore>();
        services.AddSingleton<ObjectHostedSyncStore>();
        services.AddSingleton<DisabledHostedSyncStore>();
        services.AddSingleton<IAmazonS3>(serviceProvider =>
        {
            var options = serviceProvider
                .GetRequiredService<IOptions<HostedSyncOptions>>().Value.S3;
            var config = new AmazonS3Config
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region),
                ForcePathStyle = options.ForcePathStyle
            };
            if (!string.IsNullOrWhiteSpace(options.ServiceUrl))
            {
                config.ServiceURL = options.ServiceUrl;
                config.AuthenticationRegion = options.Region;
            }
            return new AmazonS3Client(config);
        });
        services.AddSingleton<IHostedObjectStore, S3HostedObjectStore>();
        services.AddSingleton<IHostedSyncStore>(serviceProvider =>
        {
            var provider = serviceProvider
                .GetRequiredService<IOptions<HostedSyncOptions>>().Value.Provider;
            return provider switch
            {
                HostedSyncProviders.FileSystem =>
                    serviceProvider.GetRequiredService<HostedSyncStore>(),
                HostedSyncProviders.S3 =>
                    serviceProvider.GetRequiredService<ObjectHostedSyncStore>(),
                _ => serviceProvider.GetRequiredService<DisabledHostedSyncStore>()
            };
        });
        services.AddHealthChecks().AddCheck<HostedSyncHealthCheck>(
            "hosted-sync",
            tags: ["ready"]);
        return services;
    }
}
