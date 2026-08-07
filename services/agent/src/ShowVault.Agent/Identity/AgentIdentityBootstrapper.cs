using Microsoft.Extensions.Options;

namespace ShowVault.Agent.Identity;

public sealed class AgentIdentityBootstrapper(
    IAgentCredentialStore credentialStore,
    AgentEnrollmentClient enrollmentClient,
    IOptions<AgentOptions> options)
{
    public async Task<StoredAgentIdentity> GetOrEnrollAsync(CancellationToken cancellationToken)
    {
        var storedIdentity = await credentialStore.LoadAsync(cancellationToken);
        if (storedIdentity is not null)
        {
            return storedIdentity;
        }

        if (string.IsNullOrWhiteSpace(options.Value.EnrollmentCode))
        {
            throw new InvalidOperationException(
                "The Agent is not enrolled. Supply Agent:EnrollmentCode for the first start only.");
        }

        var identity = await enrollmentClient.EnrollAsync(
            options.Value.EnrollmentCode,
            options.Value.Name,
            cancellationToken);
        await credentialStore.SaveAsync(identity, cancellationToken);
        return identity;
    }

    public async Task<StoredAgentIdentity> RotateCredentialAsync(
        StoredAgentIdentity identity,
        CancellationToken cancellationToken)
    {
        var rotatedIdentity = await enrollmentClient.RotateCredentialAsync(
            identity,
            cancellationToken);
        await credentialStore.SaveAsync(rotatedIdentity, cancellationToken);
        return rotatedIdentity;
    }
}
