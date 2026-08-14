namespace ShowVault.SupportAdmin.Configuration;

public sealed class SupportAdminPortalOptions
{
    public const string SectionName = "SupportAdminPortal";
    public const string CookieScheme = "ShowVault-SupportAdmin-Cookie";
    public const string OidcScheme = "ShowVault-SupportAdmin-Oidc";
    public const string RequiredScope = "support:organizations:read";

    public bool Enabled { get; set; }
    public string? Origin { get; set; }
    public string? ApiBaseUri { get; set; }
    public string? OidcAuthority { get; set; }
    public string? OidcAudience { get; set; }
    public string? OidcClientId { get; set; }
    public string? OidcClientSecret { get; set; }
    public int SessionLifetimeMinutes { get; set; } = 5;
    public int ApiTimeoutSeconds { get; set; } = 15;
    public int MaximumApiResponseBytes { get; set; } = 262_144;

    public bool IsComplete(bool development)
    {
        if (!Enabled || !development || !HttpsRoot(Origin) || !HttpsRoot(ApiBaseUri) ||
            !HttpsRoot(OidcAuthority) || !Bounded(OidcAudience) || !Bounded(OidcClientId) ||
            !Bounded(OidcClientSecret) || SessionLifetimeMinutes != 5 ||
            ApiTimeoutSeconds is < 1 or > 30 ||
            MaximumApiResponseBytes is < 4096 or > 262_144)
            return false;
        return !string.Equals(Origin, ApiBaseUri, StringComparison.OrdinalIgnoreCase);
    }

    public static bool HttpsRoot(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps && !string.IsNullOrEmpty(uri.Host) &&
        string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) &&
        string.IsNullOrEmpty(uri.Fragment) && uri.AbsolutePath == "/";

    private static bool Bounded(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 255 &&
        !value.Any(char.IsControl);
}
