using Microsoft.Extensions.Options;
using ShowVault.AgentContracts;

namespace ShowVault.Agent;

public sealed class AgentWorker(
    ILogger<AgentWorker> logger,
    IOptions<AgentOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "ShowVault Agent {AgentId} started with protocol {ProtocolVersion} for {ControlPlaneUri}",
            options.Value.AgentId,
            AgentProtocol.Version,
            options.Value.ControlPlaneUri);

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }
}
