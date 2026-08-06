using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ShowVault.Api.Contracts;
using ShowVault.AgentContracts;

var builder = WebApplication.CreateBuilder(args);
var auth0Domain = builder.Configuration["Auth0:Domain"]
    ?? throw new InvalidOperationException("Auth0:Domain configuration is required.");
var auth0Audience = builder.Configuration["Auth0:Audience"]
    ?? throw new InvalidOperationException("Auth0:Audience configuration is required.");

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = $"https://{auth0Domain.TrimEnd('/')}";
        options.Audience = auth0Audience;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "name",
            RoleClaimType = "roles"
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

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

app.Run();

public sealed record AuthenticatedIdentity(string Subject, string? Name);

public partial class Program;
