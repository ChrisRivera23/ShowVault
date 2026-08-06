using ShowVault.Agent;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddOptions<AgentOptions>()
    .Bind(builder.Configuration.GetSection(AgentOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => options.AgentId != Guid.Empty, "Agent ID must not be empty.")
    .ValidateOnStart();

builder.Services.AddHostedService<AgentWorker>();

await builder.Build().RunAsync();
