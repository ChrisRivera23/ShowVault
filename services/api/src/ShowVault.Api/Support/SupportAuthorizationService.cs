using System.Security.Claims;

namespace ShowVault.Api.Support;

public enum SupportRequestAuthorizationKind { Authorized, Forbidden, RateLimited }
public sealed record SupportRequestAuthorization(
    SupportRequestAuthorizationKind Kind, string? Issuer = null, string? Subject = null);

public sealed class SupportAuthorizationService(
    SupportStepUpAuthorization stepUp,
    SupportRequestRateLimiter limiter)
{
    public SupportRequestAuthorization Evaluate(ClaimsPrincipal user, string expectedIssuer,
        string directPeerSource)
    {
        var identity = stepUp.Evaluate(user, expectedIssuer);
        if (!identity.Authorized)
            return new(SupportRequestAuthorizationKind.Forbidden);
        return limiter.TryAcquire(identity.Issuer!, identity.Subject!, directPeerSource)
            ? new(SupportRequestAuthorizationKind.Authorized, identity.Issuer, identity.Subject)
            : new(SupportRequestAuthorizationKind.RateLimited);
    }
}
