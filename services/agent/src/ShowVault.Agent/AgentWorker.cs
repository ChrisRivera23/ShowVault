using Microsoft.Extensions.Options;
using ShowVault.Agent.Identity;
using ShowVault.Agent.Communication;
using ShowVault.Agent.Execution;
using ShowVault.Agent.Queue;
using ShowVault.AgentContracts;

namespace ShowVault.Agent;

public sealed class AgentWorker(
    ILogger<AgentWorker> logger,
    IOptions<AgentOptions> options,
    AgentIdentityBootstrapper identityBootstrapper,
    AgentQueueStore queueStore,
    AgentEventDispatcher eventDispatcher,
    AgentCommandPoller commandPoller,
    AgentCommandExecutor commandExecutor,
    TimeProvider timeProvider,
    IHostApplicationLifetime applicationLifetime) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var identity = await identityBootstrapper.GetOrEnrollAsync(stoppingToken);
        if (options.Value.EnrollOnly)
        {
            logger.LogInformation("ShowVault Agent enrollment completed for {AgentId}", identity.AgentId);
            applicationLifetime.StopApplication();
            return;
        }

        await queueStore.InitializeAsync(stoppingToken);
        await queueStore.EnqueueEventAsync(
            AgentEventEnvelope.Create(
                identity.AgentId,
                AgentEventType.AgentConnected,
                Guid.NewGuid().ToString("N"),
                "{}",
                timeProvider.GetUtcNow()),
            stoppingToken);
        logger.LogInformation(
            "ShowVault Agent {AgentId} started with protocol {ProtocolVersion} for {ControlPlaneUri}",
            identity.AgentId,
            AgentProtocol.Version,
            options.Value.ControlPlaneUri);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5), timeProvider);
        do
        {
            await eventDispatcher.DispatchPendingOnceAsync(identity, stoppingToken);
            await commandPoller.PollOnceAsync(identity, stoppingToken);
            await commandExecutor.ExecutePendingOnceAsync(identity, stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
