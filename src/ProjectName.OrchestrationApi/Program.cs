using Microsoft.OpenApi;
using ProjectName.Application;          // For CognitiveOrchestrator
using ProjectName.Core.Entities;        // For AgentIdentity
using ProjectName.Core.Interfaces;      // For IPlanner, ICognitiveTrail
using ProjectName.Infrastructure.Data;
using ProjectName.Infrastructure.MCP;        // For McpClientConfiguration
using ProjectName.Infrastructure.Services;   // For InMemoryCognitiveTrail
using ProjectName.OrchestrationApi.Services; // For GrpcPlannerGateway
using ProjectName.PlannerService;
using ProjectName.ServiceDefaults;
using System.Numerics;

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
// !!! THE FIX: EXTEND TIMEOUT FOR AI OPERATIONS !!!
.AddStandardResilienceHandler(options =>
{
    // Allow 10 minutes total for Cold Start + Inference
    options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(10);

    // Allow 10 minutes for the individual attempt
    options.AttemptTimeout.Timeout = TimeSpan.FromMinutes(10);

    // Configure Circuit Breaker to be patient
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(20);
});
// 2. REGISTER MCP CLIENT (Connects to McpServer)
// This extension method (from Infrastructure) sets up HttpClient for IMcpToolExecutor
builder.Services.AddMcpClient(builder.Configuration);

// 3. REGISTER CORE DEPENDENCIES
// The "Brain" Proxy
builder.Services.AddScoped<IPlanner, GrpcPlannerGateway>();
builder.Services.AddScoped<ICognitiveTrail, PersistentCognitiveTrail>();

// The "Memory"
builder.Services.AddSingleton<ICognitiveTrail, InMemoryCognitiveTrail>();

// The "Ego"
builder.Services.AddSingleton(new AgentIdentity("Orchestrator", "Executive", "PMCR-O"));

// 4. REGISTER THE ORCHESTRATOR
builder.Services.AddScoped<CognitiveOrchestrator>();

// Add Aspire defaults
builder.AddServiceDefaults();

var app = builder.Build();
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
    catch (Npgsql.PostgresException ex) when (ex.SqlState == "42P04") // 42P04 = Database already exists
    {
        logger.LogWarning("⚠️ Database race condition detected. Retrying to ensure Schema...");
        // Retry: Now that the DB exists, this call will skip creation and build the tables.
        await context.Database.EnsureCreatedAsync();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ CRITICAL: Database initialization failed.");
        throw;
    }
}
// ... rest of the pipeline (Swagger, HTTPS, MapControllers, etc.) ...
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