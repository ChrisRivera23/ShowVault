using System.Security.Claims;
using ShowVault.Api.Security;
using Xunit;

namespace ShowVault.Api.Tests;

public sealed class MembershipStepUpTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-13T12:00:00Z");
    private readonly MembershipStepUpAuthorization _authorization =
        new(new FixedTimeProvider(Now));

    [Fact]
    public void Exact_scope_mfa_and_fresh_iat_allow()
    {
        var result = _authorization.Evaluate(Principal(
            scope: "openid manage:members", mfa: "[\"pwd\",\"mfa\"]", issuedAt: Now));
        Assert.True(result.Authorized);
    }

    [Theory]
    [InlineData(null, "mfa", 0, "scope_missing")]
    [InlineData("manage:members", "pwd", 0, "mfa_missing")]
    [InlineData("manage:members", "mfa", -301, "iat_stale")]
    [InlineData("manage:members", "mfa", 31, "iat_future")]
    public void Missing_or_stale_evidence_denies(
        string? scope, string mfa, int seconds, string reason)
    {
        var result = _authorization.Evaluate(Principal(scope, mfa, Now.AddSeconds(seconds)));
        Assert.False(result.Authorized);
        Assert.Equal(reason, result.ReasonCode);
    }

    [Fact]
    public void Personal_beta_denies_even_with_claims()
    {
        var result = _authorization.Evaluate(Principal("manage:members", "mfa", Now,
            ShowVault.Api.Security.PersonalBetaAuthenticationHandler.SchemeName));
        Assert.False(result.Authorized);
        Assert.Equal("personal_beta_denied", result.ReasonCode);
    }

    private static ClaimsPrincipal Principal(string? scope, string mfa,
        DateTimeOffset issuedAt, string authenticationType = "Test")
    {
        var claims = new List<Claim>
        {
            new("sub", "auth0|owner"),
            new(MembershipStepUpAuthorization.AuthenticationMethodsClaim, mfa),
            new("iat", issuedAt.ToUnixTimeSeconds().ToString(
                System.Globalization.CultureInfo.InvariantCulture))
        };
        if (scope is not null) claims.Add(new Claim("scope", scope));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
