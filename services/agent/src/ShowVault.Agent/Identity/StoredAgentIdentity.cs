namespace ShowVault.Agent.Identity;

public sealed record StoredAgentIdentity(Guid AgentId, Guid VenueId, string Credential);
