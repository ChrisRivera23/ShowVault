namespace ShowVault.Api.Account;

public sealed class AccountInvitationOptions
{
    public const string SectionName = "AccountInvitations";

    public bool Enabled { get; set; }
    public int LifetimeHours { get; set; } = 168;
    public string? ActiveKeyId { get; set; }
    public List<AccountInvitationKeyOptions> Keys { get; set; } = [];
    public int MaximumCodeBytes { get; set; } = 64;
}

public sealed class AccountInvitationKeyOptions
{
    public string Id { get; set; } = "";
    public string SecretBase64 { get; set; } = "";
}
