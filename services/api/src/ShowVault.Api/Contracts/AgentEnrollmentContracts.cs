namespace ShowVault.Api.Contracts;

public sealed record CreateAgentEnrollmentResponse(
    Guid EnrollmentId,
    string EnrollmentCode,
    DateTimeOffset ExpiresAt);

public sealed record EnrollAgentRequest(string EnrollmentCode, string Name);

public sealed record EnrollAgentResponse(
    Guid AgentId,
    Guid VenueId,
    string Credential);

public sealed record AgentIdentityResponse(Guid AgentId, Guid VenueId);
