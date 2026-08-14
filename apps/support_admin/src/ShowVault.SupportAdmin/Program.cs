using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using ShowVault.SupportAdmin.Clients;
using ShowVault.SupportAdmin.Configuration;
using ShowVault.SupportAdmin.Security;

var builder = WebApplication.CreateBuilder(args);
var portal = builder.Configuration.GetSection(SupportAdminPortalOptions.SectionName)
    .Get<SupportAdminPortalOptions>() ?? new SupportAdminPortalOptions();
if (portal.Enabled && !portal.IsComplete(builder.Environment.IsDevelopment()))
    throw new InvalidOperationException("Enabled Support portal configuration is incomplete.");
if (portal.Enabled) builder.Configuration["AllowedHosts"] = new Uri(portal.Origin!).Host;

builder.Services.Configure<SupportAdminPortalOptions>(
    builder.Configuration.GetSection(SupportAdminPortalOptions.SectionName));
if (portal.Enabled)
{
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddSingleton<SupportServerSideTicketStore>();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddRazorPages(options => options.Conventions.AuthorizeFolder("/"));
    builder.Services.AddAntiforgery(options =>
    {
        options.Cookie.Name = "__Host-showvault-support-csrf";
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.Path = "/";
    });
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = SupportAdminPortalOptions.CookieScheme;
        options.DefaultChallengeScheme = SupportAdminPortalOptions.OidcScheme;
    }).AddCookie(SupportAdminPortalOptions.CookieScheme, options =>
    {
        options.Cookie.Name = "__Host-showvault-support";
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.Path = "/";
        options.SlidingExpiration = false;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(portal.SessionLifetimeMinutes);
    }).AddOpenIdConnect(SupportAdminPortalOptions.OidcScheme, options =>
    {
        options.Authority = portal.OidcAuthority!;
        options.ClientId = portal.OidcClientId!;
        options.ClientSecret = portal.OidcClientSecret!;
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.UsePkce = true;
        options.SaveTokens = true;
        options.MapInboundClaims = false;
        options.GetClaimsFromUserInfoEndpoint = false;
        options.CallbackPath = "/support/signin-oidc";
        options.SignedOutCallbackPath = "/support/signout-callback-oidc";
        options.NonceCookie.Name = "__Host-showvault-support-nonce";
        options.NonceCookie.SecurePolicy = CookieSecurePolicy.Always;
        options.NonceCookie.HttpOnly = true;
        options.NonceCookie.SameSite = SameSiteMode.None;
        options.NonceCookie.Path = "/";
        options.CorrelationCookie.Name = "__Host-showvault-support-correlation";
        options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
        options.CorrelationCookie.HttpOnly = true;
        options.CorrelationCookie.SameSite = SameSiteMode.None;
        options.CorrelationCookie.Path = "/";
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add(SupportAdminPortalOptions.RequiredScope);
        options.Events.OnRedirectToIdentityProvider = context =>
        {
            context.ProtocolMessage.SetParameter("audience", portal.OidcAudience);
            context.ProtocolMessage.AcrValues =
                "http://schemas.openid.net/pape/policies/2007/06/multi-factor";
            context.ProtocolMessage.MaxAge = "0";
            return Task.CompletedTask;
        };
    });
    builder.Services.AddOptions<CookieAuthenticationOptions>(
            SupportAdminPortalOptions.CookieScheme)
        .Configure<SupportServerSideTicketStore>((options, tickets) =>
            options.SessionStore = tickets);
    builder.Services.AddHttpClient<ShowVaultSupportClient>(client =>
    {
        client.BaseAddress = new Uri(portal.ApiBaseUri!);
        client.Timeout = TimeSpan.FromSeconds(portal.ApiTimeoutSeconds);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ShowVault-SupportAdmin/1.0");
    }).RemoveAllLoggers();
}

var app = builder.Build();
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    ExceptionHandler = async context =>
    {
        context.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("ShowVault.SupportAdmin")
            .LogInformation("Support portal request {Outcome}; correlation {CorrelationId}",
                "unexpected_failure", context.TraceIdentifier);
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await Results.Problem(statusCode: StatusCodes.Status500InternalServerError,
            title: "The Support portal could not complete the request.").ExecuteAsync(context);
    },
    SuppressDiagnosticsCallback = _ => true
});
app.UseMiddleware<SupportSecurityHeadersMiddleware>();
if (!portal.Enabled)
{
    app.MapFallback(() => Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
        title: "Support administration is disabled."));
}
else
{
    app.UseMiddleware<SupportOriginMiddleware>(new Uri(portal.Origin!));
    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapRazorPages();
}
app.Run();

public partial class Program;
