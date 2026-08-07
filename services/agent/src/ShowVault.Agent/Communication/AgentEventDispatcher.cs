using ShowVault.Agent.Identity;
using ShowVault.Agent.Queue;

namespace ShowVault.Agent.Communication;

public sealed class AgentEventDispatcher(
    AgentQueueStore queueStore,
    AgentEventClient eventClient,
    TimeProvider timeProvider,
    ILogger<AgentEventDispatcher> logger)
{
    public async Task DispatchPendingOnceAsync(
        StoredAgentIdentity identity,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var pendingEvents = await queueStore.GetPendingEventsAsync(now, 25, cancellationToken);
        foreach (var queuedEvent in pendingEvents)
        {
            try
            {
                await eventClient.SendAsync(identity, queuedEvent.Envelope, cancellationToken);
                await queueStore.MarkEventDeliveredAsync(
                    queuedEvent.Envelope.EventId,
                    timeProvider.GetUtcNow(),
                    cancellationToken);
            }
            catch (HttpRequestException exception)
            {
                var retryDelay = TimeSpan.FromSeconds(Math.Min(
                    300,
                    Math.Pow(2, Math.Min(queuedEvent.AttemptCount, 8))));
                await queueStore.RecordEventFailureAsync(
                    queuedEvent.Envelope.EventId,
                    timeProvider.GetUtcNow().Add(retryDelay),
                    cancellationToken);
                logger.LogWarning(
                    exception,
                    "Agent event {EventId} delivery failed; retry scheduled in {RetryDelay}",
                    queuedEvent.Envelope.EventId,
                    retryDelay);
            }
        }
    }
}
