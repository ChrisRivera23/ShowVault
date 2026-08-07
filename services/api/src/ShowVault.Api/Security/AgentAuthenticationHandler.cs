using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShowVault.Api.Data;

namespace ShowVault.Api.Security;

public sealed class AgentAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    PlatformDbContext database)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ShowVaultAgent";
    private const string AuthorizationPrefix = "ShowVault-Agent ";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith(AuthorizationPrefix, StringComparison.Ordinal))
        {
            return AuthenticateResult.NoResult();
        }

        var credential = authorization[AuthorizationPrefix.Length..];
        var separatorIndex = credential.IndexOf('.');
        if (separatorIndex <= 0 ||
            !Guid.TryParse(credential[..separatorIndex], out var agentId) ||
            separatorIndex == credential.Length - 1)
        {
            return AuthenticateResult.Fail("The Agent credential is malformed.");
        }

        var agent = await database.VenueAgents
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == agentId, Context.RequestAborted);
        if (agent is null ||
            agent.RevokedAt is not null ||
            !AgentSecrets.Verify(credential[(separatorIndex + 1)..], agent.CredentialHash))
        {
            return AuthenticateResult.Fail("The Agent credential is invalid.");
        }

        var identity = new ClaimsIdentity(
            [
                new Claim("agent_id", agent.Id.ToString()),
                new Claim("venue_id", agent.VenueId.ToString())
            ],
            SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return AuthenticateResult.Success(ticket);
    }
}
