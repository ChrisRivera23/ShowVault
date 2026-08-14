namespace ShowVault.Api.Support;

public sealed class SupportAdminOptions
{
    public const string SectionName = "SupportAdmin";
    public const string SchemeName = "ShowVault-Support";
    public const string RequiredScope = "support:organizations:read";

    public bool Enabled { get; set; }
    public string? Authority { get; set; }
    public string? Audience { get; set; }

    public (string Authority, string Audience) RequireValid(string customerAudience)
    {
        if (!Enabled)
            throw new InvalidOperationException("Support administration is disabled.");
        if (!Uri.TryCreate(Authority, UriKind.Absolute, out var authority) ||
            authority.Scheme != Uri.UriSchemeHttps || string.IsNullOrEmpty(authority.Host) ||
            !string.IsNullOrEmpty(authority.UserInfo) || !string.IsNullOrEmpty(authority.Query) ||
            !string.IsNullOrEmpty(authority.Fragment) || authority.AbsolutePath != "/")
            throw new InvalidOperationException("Enabled Support authority must be an HTTPS origin.");
        var audience = Audience?.Trim();
        if (string.IsNullOrEmpty(audience) || audience.Length > 255 ||
            string.Equals(audience, customerAudience, StringComparison.Ordinal))
            throw new InvalidOperationException("Enabled Support audience must be non-empty and distinct from the customer audience.");
        return (authority.AbsoluteUri, audience);
    }
}
