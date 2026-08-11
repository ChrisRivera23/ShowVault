namespace ShowVault.Agent.Identity;

public sealed record StoredAgentIdentity(Guid AgentId, Guid VenueId, string Credential);

public abstract record StoredAgentState;

public sealed record ActiveAgentState(StoredAgentIdentity Identity) : StoredAgentState;

public sealed record PendingAgentEnrollment(
    Guid RequestId,
    string EnrollmentCode,
    string AgentName,
    string CredentialSecret) : StoredAgentState;

public sealed record PendingAgentRotation(
    Guid RequestId,
    StoredAgentIdentity PreviousIdentity,
    string CredentialSecret) : StoredAgentState;
