var builder = DistributedApplication.CreateBuilder(args);

// ============================================================================
// INFRASTRUCTURE RESOURCES
// ============================================================================

// Redis for caching and session state
var redis = builder.AddRedis("redis");

// PostgreSQL for cognitive trail persistence
var postgres = builder.AddPostgres("postgres");
var cognitiveDb = postgres.AddDatabase("cognitivedb");

// ============================================================================
// MCP SERVER (.NET)
// ============================================================================

// .NET MCP Server with Playwright browser automation
var mcpServer = builder.AddProject<Projects.ProjectName_McpServer>("mcp-server");

// ============================================================================
// MICROSERVICES
// ============================================================================

// gRPC Planner Service (hosts PlannerAgent)
var plannerService = builder.AddProject<Projects.ProjectName_PlannerService>("planner-service")
    .WithReference(redis)
    .WithReference(mcpServer);  // ✅ Service discovery for MCP

// REST API Gateway (OrchestrationApi)
var orchestrationApi = builder.AddProject<Projects.ProjectName_OrchestrationApi>("orchestration-api")
    .WithReference(plannerService)
    .WithReference(redis)
    .WithReference(cognitiveDb);

// ============================================================================
// BUILD AND RUN
// ============================================================================

var app = builder.Build();

app.Run();