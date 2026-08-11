using Microsoft.Extensions.Options;
using ShowVault.Agent.Identity;
using ShowVault.AgentContracts;

namespace ShowVault.Agent;

public sealed class AgentWorker(
    ILogger<AgentWorker> logger,
    IOptions<AgentOptions> options,
    AgentIdentityBootstrapper identityBootstrapper) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var identity = await identityBootstrapper.GetOrEnrollAsync(stoppingToken);
        logger.LogInformation(
            "ShowVault Agent {AgentId} started with protocol {ProtocolVersion} for {ControlPlaneUri}",
            identity.AgentId,
            AgentProtocol.Version,
            options.Value.ControlPlaneUri);

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }
}
