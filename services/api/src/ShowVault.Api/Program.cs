using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.RateLimiting;
using ShowVault.Api.Contracts;
using ShowVault.Api.Data;
using ShowVault.Api.Endpoints;
using ShowVault.Api.HostedSync;
using ShowVault.Api.Security;
using ShowVault.AgentContracts;

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
builder.Services.AddHostedSync(builder.Configuration);
builder.Services.AddDbContext<PlatformDbContext>(options =>
    options.UseNpgsql(platformConnectionString));
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
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var app = builder.Build();
if (args.Contains("--smoke-hosted-sync", StringComparer.Ordinal))
{
    await HostedSyncSmokeCheck.RunAsync(app.Services);
    return;
}
if (args.Contains("--migrate", StringComparer.Ordinal))
{
    await using var migrationScope = app.Services.CreateAsyncScope();
    await migrationScope.ServiceProvider.GetRequiredService<PlatformDbContext>()
        .Database.MigrateAsync();
    return;
}
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
});
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
app.MapRecoveryWorkflowEndpoints();
app.MapRecoveryCandidateEndpoints();
app.MapSubnetProposalEndpoints();
app.MapHostedSyncEndpoints();

app.Run();

public sealed record AuthenticatedIdentity(string Subject, string? Name);

public partial class Program;
