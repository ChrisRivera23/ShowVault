namespace ShowVault.Api.Support;

public sealed class SupportAdminOptions
{
    public const string SectionName = "SupportAdmin";

    public bool Enabled { get; set; }
    public string? Authority { get; set; }
    public string? Audience { get; set; }
}
