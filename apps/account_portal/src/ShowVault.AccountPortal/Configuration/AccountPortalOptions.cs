namespace ShowVault.AccountPortal.Configuration;

public sealed class AccountPortalOptions
{
    public const string SectionName = "AccountPortal";
    public bool Enabled { get; set; }
    public string? Origin { get; set; }
    public string? ApiBaseUri { get; set; }
    public string? Auth0Authority { get; set; }
    public string? Auth0Audience { get; set; }
    public string? Auth0ClientId { get; set; }
    public string? Auth0ClientSecret { get; set; }
    public int SessionLifetimeMinutes { get; set; } = 30;
    public int ApiTimeoutSeconds { get; set; } = 15;
    public int MaximumApiResponseBytes { get; set; } = 1_048_576;

    public bool IsComplete(bool development)
    {
        if (!Enabled) return false;
        if (!HttpsRoot(Origin) || !HttpsRoot(ApiBaseUri) || !HttpsRoot(Auth0Authority) ||
            string.IsNullOrWhiteSpace(Auth0Audience) ||
            string.IsNullOrWhiteSpace(Auth0ClientId) ||
            string.IsNullOrWhiteSpace(Auth0ClientSecret) ||
            SessionLifetimeMinutes != 30 || ApiTimeoutSeconds is < 1 or > 30 ||
            MaximumApiResponseBytes is < 4096 or > 1_048_576)
            return false;
        // This milestone deliberately provides only an in-memory Development/test store.
        // Production cannot start until a real durable encrypted implementation is added.
        return development;
    }

    public static bool HttpsRoot(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo) &&
        string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment) &&
        uri.AbsolutePath == "/";
}
