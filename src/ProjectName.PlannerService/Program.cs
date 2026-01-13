using ProjectName.Core.Interfaces;
using ProjectName.Infrastructure.Agents; // Namespace of PlannerAgent
using ProjectName.Infrastructure.MCP;
using ProjectName.PlannerService.Services;
using ProjectName.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// Add gRPC services
builder.Services.AddGrpc(options =>
{
    options.MaxReceiveMessageSize = 16 * 1024 * 1024;
    options.MaxSendMessageSize = 16 * 1024 * 1024;
});

// Add MCP Client
builder.Services.AddMcpClient(builder.Configuration);

// REGISTER THE AGENT (The Brain)
// This will trigger the Constructor which reads the DNA attributes
builder.Services.AddSingleton<IPlanner, PlannerAgent>();

builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
});

builder.AddServiceDefaults();

var app = builder.Build();

app.MapGrpcService<PlannerGrpcService>();

app.MapGet("/", () => "PMCR-O Planner gRPC Service is Active.");

app.MapDefaultEndpoints();

app.Run();