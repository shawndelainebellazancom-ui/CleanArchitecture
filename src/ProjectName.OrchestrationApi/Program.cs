using Microsoft.Extensions.AI;
using Microsoft.OpenApi;
using ProjectName.Application;
using ProjectName.Core.Entities;
using ProjectName.Core.Interfaces;
using ProjectName.Infrastructure.Data;
using ProjectName.Infrastructure.MCP;
using ProjectName.Infrastructure.Services;
using ProjectName.OrchestrationApi.Services;
using ProjectName.PlannerService;
using ProjectName.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddNpgsqlDbContext<CognitiveDbContext>("cognitivedb");

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PMCR-O Orchestration API",
        Version = "v1"
    });
});

// 1. REGISTER gRPC CLIENT (Connects to PlannerService)
builder.Services.AddGrpcClient<Planner.PlannerClient>(options =>
{
    var plannerUrl = builder.Configuration.GetConnectionString("planner-service")
        ?? "https://localhost:7035";
    options.Address = new Uri(plannerUrl);
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    if (builder.Environment.IsDevelopment())
    {
        handler.ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    }
    return handler;
})
.AddStandardResilienceHandler(options =>
{
    // Allow 10 minutes total for AI operations
    options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(10);
    options.AttemptTimeout.Timeout = TimeSpan.FromMinutes(10);
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(20);
});

// 2. REGISTER MCP CLIENT (Connects to McpServer)
builder.Services.AddMcpClient(builder.Configuration);

// 3. REGISTER CORE DEPENDENCIES
builder.Services.AddScoped<IPlanner, GrpcPlannerGateway>();
builder.Services.AddScoped<ICognitiveTrail, PersistentCognitiveTrail>();

// The "Ego" - Fixed with Voice parameter
builder.Services.AddSingleton(new AgentIdentity(
    Name: "Orchestrator",
    Role: "Executive",
    Philosophy: "PMCR-O",
    Voice: "Analytical"
));

// 4. REGISTER THE ORCHESTRATOR
builder.Services.AddScoped<CognitiveOrchestrator>();

// Add Aspire defaults
builder.AddServiceDefaults();

var app = builder.Build();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var context = services.GetRequiredService<CognitiveDbContext>();

    try
    {
        logger.LogInformation("🧠 Initializing Cognitive Memory (Database)...");
        await context.Database.EnsureCreatedAsync();
    }
    catch (Npgsql.PostgresException ex) when (ex.SqlState == "42P04")
    {
        logger.LogWarning("⚠️ Database race condition detected. Retrying to ensure Schema...");
        await context.Database.EnsureCreatedAsync();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ CRITICAL: Database initialization failed.");
        throw;
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "PMCR-O API v1");
        options.RoutePrefix = string.Empty;
    });
    app.UseCors();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapDefaultEndpoints();

app.Run();