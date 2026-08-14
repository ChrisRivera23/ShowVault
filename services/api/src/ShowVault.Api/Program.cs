using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.RateLimiting;
using ShowVault.Api.Contracts;
using ShowVault.Api.Data;
using ShowVault.Api.Endpoints;
using ShowVault.Api.Security;
using ShowVault.AgentContracts;
using ShowVault.Api.HostedSync;
using ShowVault.Api.Commercial;
using ShowVault.Platform.Commercial;
using ShowVault.Api.Billing;
using ShowVault.Api.Authorization;
using ShowVault.Api.Account;
using ShowVault.Api.Support;

var builder = WebApplication.CreateBuilder(args);
var auth0Domain = builder.Configuration["Auth0:Domain"]
    ?? throw new InvalidOperationException("Auth0:Domain configuration is required.");
var auth0Audience = builder.Configuration["Auth0:Audience"]
    ?? throw new InvalidOperationException("Auth0:Audience configuration is required.");
var platformConnectionString = builder.Configuration.GetConnectionString("Platform")
    ?? throw new InvalidOperationException("The Platform connection string is required.");

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<CommercialStateService>();
builder.Services.AddScoped<MembershipAuthorizationService>();
builder.Services.Configure<AccountInvitationOptions>(
    builder.Configuration.GetSection(AccountInvitationOptions.SectionName));
builder.Services.AddSingleton<InvitationTokenService>();
builder.Services.AddSingleton<MembershipStepUpAuthorization>();
builder.Services.AddScoped<AccountAdministrationService>();
builder.Services.Configure<SupportAdminOptions>(
    builder.Configuration.GetSection(SupportAdminOptions.SectionName));
builder.Services.Configure<BillingOptions>(builder.Configuration.GetSection(BillingOptions.SectionName));
builder.Services.Configure<StripeApiOptions>(builder.Configuration.GetSection(StripeApiOptions.SectionName));
builder.Services.Configure<BillingOfferingOptions>(builder.Configuration.GetSection(BillingOfferingOptions.SectionName));
builder.Services.Configure<StripeWebhookOptions>(builder.Configuration.GetSection(StripeWebhookOptions.SectionName));
builder.Services.AddHttpClient<StripeBillingProvider>().RemoveAllLoggers();
builder.Services.AddTransient<IBillingProvider>(services =>
    services.GetRequiredService<StripeBillingProvider>());
builder.Services.AddSingleton<IBillingOfferingCatalog, ConfiguredBillingOfferingCatalog>();
builder.Services.AddSingleton<IStripeWebhookSignatureVerifier, StripeWebhookSignatureVerifier>();
builder.Services.AddScoped<BillingService>();
builder.Services.AddScoped<BillingReconciliationService>();
builder.Services.AddHostedService<BillingReconciliationWorker>();
builder.Services.AddDbContext<PlatformDbContext>(options =>
    options.UseNpgsql(platformConnectionString));
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IHostedObjectStore, SyntheticHostedObjectStore>();
    builder.Services.AddSingleton<ICommercialPlanPolicyCatalog,
        SyntheticCommercialPlanPolicyCatalog>();
}
else
{
    builder.Services.AddSingleton<IHostedObjectStore, DisabledHostedObjectStore>();
    builder.Services.AddSingleton<ICommercialPlanPolicyCatalog,
        DisabledCommercialPlanPolicyCatalog>();
}
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = "ShowVault-User";
        options.DefaultChallengeScheme = "ShowVault-User";
    })
    .AddPolicyScheme("ShowVault-User", "ShowVault user authentication", options =>
    {
        options.ForwardDefaultSelector = context =>
            PersonalBetaAuthenticationHandler.IsPersonalBetaRequest(context.Request)
                ? PersonalBetaAuthenticationHandler.SchemeName
                : JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.Authority = $"https://{auth0Domain.TrimEnd('/')}";
        options.Audience = auth0Audience;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "name",
            RoleClaimType = "roles"
        };
    })
    .AddScheme<AuthenticationSchemeOptions, PersonalBetaAuthenticationHandler>(
        PersonalBetaAuthenticationHandler.SchemeName,
        _ => { });
builder.Services.AddAuthentication()
    .AddScheme<AuthenticationSchemeOptions, AgentAuthenticationHandler>(
        AgentAuthenticationHandler.SchemeName,
        _ => { });
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("agent-enrollment", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.AddPolicy("invitation-accept", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            $"{context.User.FindFirstValue("sub") ?? "unknown"}|" +
            $"{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.AddPolicy("account-mutation", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            $"{context.User.FindFirstValue("sub") ?? "unknown"}|" +
            $"{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var app = builder.Build();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health");
app.MapGet("/api/v1/agent-protocol", (HttpContext context) =>
    Results.Ok(ApiResponse<AgentProtocolDescription>.Success(
        AgentProtocolDescription.Current,
        context.TraceIdentifier)));
app.MapGet("/api/v1/platform/status", (HttpContext context) =>
{
    var correlationId = context.TraceIdentifier;
    return Results.Ok(ApiResponse<PlatformStatus>.Success(
        new PlatformStatus("ShowVault API", "foundation"),
        correlationId));
});
app.MapGet("/api/v1/identity", (ClaimsPrincipal user, HttpContext context) =>
{
    var subject = user.FindFirstValue("sub");
    if (string.IsNullOrWhiteSpace(subject))
    {
        return Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "The access token does not contain a subject.");
    }

    return Results.Ok(ApiResponse<AuthenticatedIdentity>.Success(
        new AuthenticatedIdentity(subject, user.FindFirstValue("name")),
        context.TraceIdentifier));
}).RequireAuthorization();

app.MapTenantEndpoints();
app.MapAgentEnrollmentEndpoints();
app.MapAgentCommunicationEndpoints();
app.MapRecoveryHistoryEndpoints();
app.MapRecoveryCandidateEndpoints();
app.MapHostedSyncEndpoints();
app.MapCommercialEndpoints();
app.MapBillingEndpoints();
app.MapAccountEndpoints();

app.Run();

public sealed record AuthenticatedIdentity(string Subject, string? Name);

public partial class Program;
