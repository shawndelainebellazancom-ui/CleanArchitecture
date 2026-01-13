using Microsoft.OpenApi;
using ProjectName.McpServer.Services;
using ProjectName.McpServer.Tools;
using ProjectName.ServiceDefaults;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PMCR-O MCP Server",
        Version = "v1",
        Description = "Model Context Protocol server for browser automation and tool execution"
    });
});

// Register PlaywrightManager as singleton (browser lifecycle)
builder.Services.AddSingleton<PlaywrightManager>();

// Register MCP Server Service
builder.Services.AddSingleton<McpServerService>();

// Register all MCP tools
builder.Services.AddScoped<BrowserNavigateTool>();
builder.Services.AddScoped<BrowserClickTool>();
builder.Services.AddScoped<BrowserTypeTool>();
builder.Services.AddScoped<BrowserScreenshotTool>();
builder.Services.AddScoped<BrowserExtractTool>();
builder.Services.AddScoped<BrowserEvaluateTool>();

// Add Aspire service defaults (telemetry, health checks)
builder.AddServiceDefaults();

var app = builder.Build();

// Initialize Playwright on startup
var playwright = app.Services.GetRequiredService<PlaywrightManager>();
await playwright.InitializeAsync();

// Cached JSON options
var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "MCP Server API v1");
    });
}

app.UseHttpsRedirection();

// MCP Protocol Endpoint
app.MapPost("/mcp", async (
    HttpContext context,
    McpServerService mcpServer) =>
{
    try
    {
        // Read request
        var request = await JsonSerializer.DeserializeAsync<McpRequest>(
            context.Request.Body,
            jsonOptions);

        if (request == null)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid request" });
            return;
        }

        // Handle request
        var response = await mcpServer.HandleRequestAsync(request, context.RequestAborted);

        // Write response
        await context.Response.WriteAsJsonAsync(response);
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = 500;
        await context.Response.WriteAsJsonAsync(new
        {
            jsonrpc = "2.0",
            error = new
            {
                code = -32603,
                message = ex.Message
            }
        });
    }
})
.WithName("McpProtocol");

// Health check endpoint
app.MapGet("/health", (PlaywrightManager playwright) =>
{
    var isHealthy = playwright.Browser != null && playwright.Browser.IsConnected;

    return Results.Json(new
    {
        status = isHealthy ? "healthy" : "unhealthy",
        browser = isHealthy ? "connected" : "disconnected",
        timestamp = DateTime.UtcNow
    });
})
.WithName("HealthCheck");

// List available tools endpoint (for debugging)
app.MapGet("/tools", async (McpServerService mcpServer) =>
{
    var request = new McpRequest("tools/list");
    var response = await mcpServer.HandleRequestAsync(request);
    return Results.Json(response.Result);
})
.WithName("ListTools");

// Map default Aspire endpoints
app.MapDefaultEndpoints();

app.Run();