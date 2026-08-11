using System.Text.Json;
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
                if (command is null)
                {
                    logger.LogWarning(
                        "Rejected command with null envelope; the next cycle will continue");
                    continue;
                }

                var now = timeProvider.GetUtcNow();
                if (command.AgentId != identity.AgentId ||
                    !AgentCommandValidation.TryValidate(command, out _) ||
                    command.ExpiresAt <= now)
                {
                    logger.LogWarning(
                        "Rejected command {CommandId} with invalid envelope, identity, or expiry",
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
        catch (HttpRequestException)
        {
            logger.LogWarning(
                "Agent command polling failed with transport-response; the next cycle will retry");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Agent command polling failed with timeout; the next cycle will retry");
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            logger.LogWarning(
                "Agent command polling failed with malformed-response; the next cycle will retry");
        }
    }
}
