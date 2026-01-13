using ProjectName.Core.Interfaces;
using ProjectName.Infrastructure.Agents;
using ProjectName.Infrastructure.MCP;
using ProjectName.PlanerService.Services;
using ProjectName.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// Add gRPC services
builder.Services.AddGrpc(options =>
{
    options.MaxReceiveMessageSize = 16 * 1024 * 1024; // 16 MB
    options.MaxSendMessageSize = 16 * 1024 * 1024;
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});

// Add MCP Client
builder.Services.AddMcpClient(builder.Configuration);

// Register PlannerAgent as IPlanner
builder.Services.AddSingleton<IPlanner, PlannerAgent>();

// Add logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
    if (builder.Environment.IsDevelopment())
    {
        logging.SetMinimumLevel(LogLevel.Debug);
    }
});

// Add Aspire service defaults (telemetry, health checks, service discovery)
builder.AddServiceDefaults();

var app = builder.Build();

// Configure gRPC endpoints
app.MapGrpcService<PlannerGrpcService>();

// Health check endpoint
app.MapGet("/", () =>
    "PMCR-O Planner gRPC Service. " +
    "Communication must be made through a gRPC client. " +
    "See https://go.microsoft.com/fwlink/?linkid=2086909");

// Map default endpoints (health checks from Aspire)
app.MapDefaultEndpoints();

app.Run();