using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using ShowVault.AccountPortal.Clients;
using ShowVault.AccountPortal.Configuration;
using ShowVault.AccountPortal.Security;

var builder = WebApplication.CreateBuilder(args);
var portal = builder.Configuration.GetSection(AccountPortalOptions.SectionName)
    .Get<AccountPortalOptions>() ?? new AccountPortalOptions();
if (portal.Enabled && !portal.IsComplete(builder.Environment.IsDevelopment()))
    throw new InvalidOperationException("Enabled account portal configuration is incomplete.");
if (portal.Enabled)
    builder.Configuration["AllowedHosts"] = new Uri(portal.Origin!).Host;

builder.Services.Configure<AccountPortalOptions>(
    builder.Configuration.GetSection(AccountPortalOptions.SectionName));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ServerSideTicketStore>();
builder.Services.AddSingleton<OneTimeSecretStore>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddRazorPages(options =>
    options.Conventions.AuthorizeFolder("/Organizations"));
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "__Host-showvault-account-csrf";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.Path = "/";
});
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
}).AddCookie(options =>
{
    options.Cookie.Name = "__Host-showvault-account";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.Path = "/";
    options.SlidingExpiration = false;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
}).AddOpenIdConnect(options =>
{
    options.Authority = portal.Auth0Authority ?? "https://disabled.invalid/";
    options.ClientId = portal.Auth0ClientId ?? "disabled";
    options.ClientSecret = portal.Auth0ClientSecret ?? "disabled";
    options.ResponseType = OpenIdConnectResponseType.Code;
    options.UsePkce = true;
    options.SaveTokens = true;
    options.MapInboundClaims = false;
    options.CallbackPath = "/signin-oidc";
    options.SignedOutCallbackPath = "/signout-callback-oidc";
    options.Scope.Clear();
    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.TokenValidationParameters = new TokenValidationParameters
    {
        NameClaimType = "name"
    };
    options.Events.OnRedirectToIdentityProvider = context =>
    {
        if (!string.IsNullOrWhiteSpace(portal.Auth0Audience))
            context.ProtocolMessage.SetParameter("audience", portal.Auth0Audience);
        if (context.Properties.Items.ContainsKey("showvault_step_up"))
        {
            context.ProtocolMessage.Scope = "openid profile manage:members";
            context.ProtocolMessage.AcrValues =
                "http://schemas.openid.net/pape/policies/2007/06/multi-factor";
            context.ProtocolMessage.MaxAge = "0";
        }
        return Task.CompletedTask;
    };
});
builder.Services.AddOptions<CookieAuthenticationOptions>(
        CookieAuthenticationDefaults.AuthenticationScheme)
    .Configure<ServerSideTicketStore>((options, tickets) =>
        options.SessionStore = tickets);
builder.Services.AddHttpClient<ShowVaultAccountClient>(client =>
{
    client.BaseAddress = new Uri(portal.ApiBaseUri ?? "https://disabled.invalid/");
    client.Timeout = TimeSpan.FromSeconds(portal.ApiTimeoutSeconds);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("ShowVault-AccountPortal/1.0");
}).RemoveAllLoggers();

var app = builder.Build();
app.UseExceptionHandler(error => error.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await Results.Problem(statusCode: StatusCodes.Status500InternalServerError,
        title: "The account portal could not complete the request.",
        extensions: new Dictionary<string, object?>
        {
            ["correlationId"] = context.TraceIdentifier
        }).ExecuteAsync(context);
}));
app.UseMiddleware<PortalSecurityHeadersMiddleware>();
if (portal.Enabled)
    app.UseMiddleware<PortalOriginMiddleware>(new Uri(portal.Origin!));
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
if (!portal.Enabled)
{
    app.MapGet("/", () => Results.Problem(statusCode: 503,
        title: "The account portal is disabled."));
}
else
{
    app.MapRazorPages();
}
app.Run();

public partial class Program;
