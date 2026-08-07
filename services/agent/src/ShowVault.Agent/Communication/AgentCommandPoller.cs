using ShowVault.Agent.Identity;
using ShowVault.Agent.Queue;
using ShowVault.AgentContracts;

namespace ShowVault.Agent.Communication;

public sealed class AgentCommandPoller(
    AgentQueueStore queueStore,
    AgentCommandClient commandClient,
    TimeProvider timeProvider,
    ILogger<AgentCommandPoller> logger)
{
    public async Task PollOnceAsync(
        StoredAgentIdentity identity,
        CancellationToken cancellationToken)
    {
        try
        {
            var commands = await commandClient.PollAsync(identity, cancellationToken);
            foreach (var command in commands)
            {
                var now = timeProvider.GetUtcNow();
                if (command.AgentId != identity.AgentId ||
                    command.ProtocolVersion != AgentProtocol.Version ||
                    command.ExpiresAt <= now)
                {
                    logger.LogWarning(
                        "Rejected command {CommandId} with invalid identity, protocol, or expiry",
                        command.CommandId);
                    continue;
                }

                await queueStore.EnqueueCommandAsync(command, now, cancellationToken);
                await commandClient.AcknowledgeAsync(
                    identity,
                    command.CommandId,
                    cancellationToken);
            }
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Agent command polling failed; the next cycle will retry");
        }
    }
}
