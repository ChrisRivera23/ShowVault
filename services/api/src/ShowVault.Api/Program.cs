using ShowVault.Api.Contracts;
using ShowVault.AgentContracts;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();

var app = builder.Build();
app.UseExceptionHandler();

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

app.Run();

public partial class Program;
