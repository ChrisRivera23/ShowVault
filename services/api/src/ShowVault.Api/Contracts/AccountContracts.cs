namespace ShowVault.Api.Contracts;

public sealed record AccountMemberSummary(
    Guid Id,
    string? DisplayLabel,
    string Role,
    string State,
    bool IsCurrentUser,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    long Revision);

public sealed record AccountInvitationSummary(
    Guid Id,
    string DisplayLabel,
    string Role,
    string State,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset ExpiresAt,
    long Revision);

public sealed record CreatedAccountInvitation(
    Guid Id,
    string DisplayLabel,
    string Role,
    string State,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset ExpiresAt,
    long Revision,
    string InvitationCode);

public sealed record AcceptedAccountInvitation(AccountMemberSummary Membership);
