using Microsoft.Extensions.AI;
using OllamaSharp;
using ProjectName.Application.Interfaces;
using ProjectName.Infrastructure.Agents;
using ProjectName.PlannerService.Services;
using ProjectName.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddGrpc();

// Get configuration
var ollamaEndpoint = builder.Configuration["AgentSettings:OllamaEndpoint"] ?? "http://localhost:11434";
var modelName = builder.Configuration["AgentSettings:ModelName"] ?? "qwen2.5-coder:latest";

// Register OllamaSharp as IChatClient (implements Microsoft.Extensions.AI)
builder.Services.AddSingleton<IChatClient>(sp =>
{
    var ollamaClient = new OllamaApiClient(
        new Uri(ollamaEndpoint),
        modelName
    );

    // OllamaApiClient implements IChatClient directly
    return ollamaClient;
});

// Register PlannerAgent as the Brain implementation
builder.Services.AddSingleton<IPlanner, PlannerAgent>();

var app = builder.Build();

app.MapGrpcService<PlannerGrpcService>();
app.MapDefaultEndpoints();

app.Run();