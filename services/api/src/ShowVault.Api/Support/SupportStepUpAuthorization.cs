using System.Globalization;
using System.Security.Claims;
using System.Text.Json;

namespace ShowVault.Api.Support;

public sealed record SupportIdentityResult(bool Authorized, string ReasonCode,
    string? Issuer = null, string? Subject = null)
{
    public static SupportIdentityResult Deny(string reason) => new(false, reason);
    public static SupportIdentityResult Allow(string issuer, string subject) =>
        new(true, "authorized", issuer, subject);
}

public sealed class SupportStepUpAuthorization(TimeProvider timeProvider)
{
    public const string AuthenticationMethodsClaim =
        "https://showvault.app/authentication_methods";
    internal static readonly TimeSpan MaximumAge = TimeSpan.FromMinutes(5);
    internal static readonly TimeSpan FutureSkew = TimeSpan.FromSeconds(30);

    public SupportIdentityResult Evaluate(ClaimsPrincipal user, string expectedIssuer)
    {
        var identity = user.Identities.SingleOrDefault(value => value.IsAuthenticated &&
            value.AuthenticationType == SupportAdminOptions.SchemeName);
        if (identity is null) return SupportIdentityResult.Deny("support_scheme_required");
        var issuer = identity.FindFirst("iss")?.Value;
        var subject = identity.FindFirst("sub")?.Value?.Trim();
        if (!string.Equals(issuer, expectedIssuer, StringComparison.Ordinal))
            return SupportIdentityResult.Deny("issuer_invalid");
        if (string.IsNullOrWhiteSpace(subject) || subject.Length > 255 || subject.Any(char.IsControl))
            return SupportIdentityResult.Deny("subject_missing");
        var scopes = identity.FindAll("scope").SelectMany(claim =>
            claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (!scopes.Contains(SupportAdminOptions.RequiredScope, StringComparer.Ordinal))
            return SupportIdentityResult.Deny("scope_missing");
        if (!HasMfa(identity.FindAll(AuthenticationMethodsClaim).Select(claim => claim.Value)))
            return SupportIdentityResult.Deny("mfa_missing");
        if (!long.TryParse(identity.FindFirst("iat")?.Value, NumberStyles.None,
                CultureInfo.InvariantCulture, out var issuedAtSeconds) || issuedAtSeconds < 0)
            return SupportIdentityResult.Deny("iat_invalid");
        DateTimeOffset issuedAt;
        try { issuedAt = DateTimeOffset.FromUnixTimeSeconds(issuedAtSeconds); }
        catch (ArgumentOutOfRangeException) { return SupportIdentityResult.Deny("iat_invalid"); }
        var now = timeProvider.GetUtcNow();
        if (issuedAt > now + FutureSkew) return SupportIdentityResult.Deny("iat_future");
        if (now - issuedAt > MaximumAge) return SupportIdentityResult.Deny("iat_stale");
        return SupportIdentityResult.Allow(issuer!, subject);
    }

    private static bool HasMfa(IEnumerable<string> values)
    {
        foreach (var value in values)
        {
            if (value == "mfa") return true;
            try
            {
                using var document = JsonDocument.Parse(value);
                if (document.RootElement.ValueKind == JsonValueKind.Array &&
                    document.RootElement.GetArrayLength() <= 16 &&
                    document.RootElement.EnumerateArray().Any(element =>
                        element.ValueKind == JsonValueKind.String && element.GetString() == "mfa"))
                    return true;
            }
            catch (JsonException) { }
        }
        return false;
    }
}
