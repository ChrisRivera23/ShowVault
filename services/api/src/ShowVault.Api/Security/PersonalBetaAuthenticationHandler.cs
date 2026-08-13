using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ShowVault.Api.Security;

public sealed class PersonalBetaAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IWebHostEnvironment environment,
    IConfiguration configuration)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ShowVault-Personal-Beta";
    private const string AuthorizationValue = "Bearer showvault-personal-beta-loopback";

    public static bool IsPersonalBetaRequest(HttpRequest request) =>
        string.Equals(request.Headers.Authorization.ToString(), AuthorizationValue,
            StringComparison.Ordinal);

    public static bool TryGetIdentitySubject(
        IWebHostEnvironment environment,
        IConfiguration configuration,
        IPAddress? remoteAddress,
        out string subject)
    {
        subject = configuration["PersonalBeta:IdentitySubject"] ?? string.Empty;
        return environment.IsDevelopment() &&
            configuration.GetValue<bool>("PersonalBeta:BypassAuthentication") &&
            remoteAddress is not null && IPAddress.IsLoopback(remoteAddress) &&
            !string.IsNullOrWhiteSpace(subject) && subject.Length <= 255;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!TryGetIdentitySubject(environment, configuration,
                Context.Connection.RemoteIpAddress, out var subject))
        {
            return Task.FromResult(AuthenticateResult.Fail(
                "Personal beta authentication is unavailable."));
        }

        var identity = new ClaimsIdentity(
            [new Claim("sub", subject), new Claim("name", "ShowVault personal beta")],
            SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
            new ClaimsPrincipal(identity), SchemeName)));
    }
}
