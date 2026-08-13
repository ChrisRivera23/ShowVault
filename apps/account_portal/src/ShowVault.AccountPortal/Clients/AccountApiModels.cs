using System.Text.Json.Serialization;

namespace ShowVault.AccountPortal.Clients;

public sealed record ApiEnvelope<T>([property: JsonPropertyName("payload")] T Payload);
public sealed record OrganizationView(Guid Id, string Name, string Slug, string Role);
public sealed record MemberView(Guid Id, string? DisplayLabel, string Role, string State,
    bool IsCurrentUser, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, long Revision);
public sealed record InvitationView(Guid Id, string DisplayLabel, string Role, string State,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset ExpiresAt, long Revision);
public sealed record CreatedInvitationView(Guid Id, string DisplayLabel, string Role, string State,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset ExpiresAt, long Revision,
    string InvitationCode);
public sealed record AcceptedInvitationView(MemberView Membership);
