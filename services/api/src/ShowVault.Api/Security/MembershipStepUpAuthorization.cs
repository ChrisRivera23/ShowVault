using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using ShowVault.Api.Authorization;

namespace ShowVault.Api.Security;

public sealed record MembershipStepUpResult(bool Authorized, string ReasonCode)
{
    public static MembershipStepUpResult Allow() => new(true, "authorized");
    public static MembershipStepUpResult Deny(string reason) => new(false, reason);
}

public sealed class MembershipStepUpAuthorization(TimeProvider timeProvider)
{
    public const string RequiredScope = "manage:members";
    public const string AuthenticationMethodsClaim =
        "https://showvault.app/authentication_methods";
    public static readonly TimeSpan MaximumAge = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan FutureSkew = TimeSpan.FromSeconds(30);

    public MembershipStepUpResult Evaluate(ClaimsPrincipal user)
    {
        if (HumanIdentity.Subject(user) is null)
            return MembershipStepUpResult.Deny("subject_missing");
        if (HumanIdentity.IsPersonalBeta(user))
            return MembershipStepUpResult.Deny("personal_beta_denied");
        var scopes = user.FindAll("scope").SelectMany(claim =>
            claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (!scopes.Contains(RequiredScope, StringComparer.Ordinal))
            return MembershipStepUpResult.Deny("scope_missing");
        if (!HasMfa(user.FindAll(AuthenticationMethodsClaim).Select(claim => claim.Value)))
            return MembershipStepUpResult.Deny("mfa_missing");
        var issuedAtValue = user.FindFirstValue("iat");
        if (!long.TryParse(issuedAtValue, NumberStyles.None, CultureInfo.InvariantCulture,
                out var issuedAtSeconds))
            return MembershipStepUpResult.Deny("iat_missing");
        DateTimeOffset issuedAt;
        try { issuedAt = DateTimeOffset.FromUnixTimeSeconds(issuedAtSeconds); }
        catch (ArgumentOutOfRangeException) { return MembershipStepUpResult.Deny("iat_invalid"); }
        var now = timeProvider.GetUtcNow();
        if (issuedAt > now + FutureSkew)
            return MembershipStepUpResult.Deny("iat_future");
        if (now - issuedAt > MaximumAge)
            return MembershipStepUpResult.Deny("iat_stale");
        return MembershipStepUpResult.Allow();
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
                    document.RootElement.EnumerateArray().Any(element =>
                        element.ValueKind == JsonValueKind.String && element.GetString() == "mfa"))
                    return true;
            }
            catch (JsonException)
            {
                // A malformed claim is denied unless another exact value proves MFA.
            }
        }
        return false;
    }
}
