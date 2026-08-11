using Microsoft.Extensions.Options;

namespace ShowVault.Agent.Identity;

public sealed class AgentIdentityBootstrapper(
    IAgentCredentialStore credentialStore,
    AgentEnrollmentClient enrollmentClient,
    IOptions<AgentOptions> options)
{
    public async Task<StoredAgentIdentity> GetOrEnrollAsync(CancellationToken cancellationToken)
    {
        var storedState = await credentialStore.LoadAsync(cancellationToken);
        if (storedState is ActiveAgentState active)
        {
            return active.Identity;
        }

        if (storedState is PendingAgentEnrollment pendingEnrollment)
        {
            return await CompleteEnrollmentAsync(pendingEnrollment, cancellationToken);
        }

        if (storedState is PendingAgentRotation pendingRotation)
        {
            return await CompleteRotationAsync(pendingRotation, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(options.Value.EnrollmentCode))
        {
            throw new InvalidOperationException(
                "The Agent is not enrolled. Supply Agent:EnrollmentCode for the first start only.");
        }

        var pending = new PendingAgentEnrollment(
            Guid.NewGuid(),
            options.Value.EnrollmentCode,
            options.Value.Name,
            AgentEnrollmentClient.GenerateCredentialSecret());
        await credentialStore.SaveAsync(pending, cancellationToken);
        return await CompleteEnrollmentAsync(pending, cancellationToken);
    }

    public async Task<StoredAgentIdentity> RotateCredentialAsync(
        StoredAgentIdentity identity,
        CancellationToken cancellationToken)
    {
        var pending = new PendingAgentRotation(
            Guid.NewGuid(),
            identity,
            AgentEnrollmentClient.GenerateCredentialSecret());
        await credentialStore.SaveAsync(pending, cancellationToken);
        return await CompleteRotationAsync(pending, cancellationToken);
    }

    private async Task<StoredAgentIdentity> CompleteEnrollmentAsync(
        PendingAgentEnrollment pending,
        CancellationToken cancellationToken)
    {
        var identity = await enrollmentClient.EnrollAsync(pending, cancellationToken);
        await credentialStore.SaveAsync(new ActiveAgentState(identity), cancellationToken);
        return identity;
    }

    private async Task<StoredAgentIdentity> CompleteRotationAsync(
        PendingAgentRotation pending,
        CancellationToken cancellationToken)
    {
        var identity = await enrollmentClient.RotateCredentialAsync(pending, cancellationToken);
        await credentialStore.SaveAsync(new ActiveAgentState(identity), cancellationToken);
        return identity;
    }
}
