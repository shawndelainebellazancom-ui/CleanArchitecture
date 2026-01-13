using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace ProjectName.Infrastructure.MCP;

public static class McpClientConfiguration
{
    public static IServiceCollection AddMcpClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register MCP HTTP client with service discovery
        services.AddHttpClient<IMcpToolExecutor, McpToolExecutor>((sp, client) =>
        {
            // Aspire will resolve "mcp-server" via service discovery
            var endpoint = configuration.GetConnectionString("mcp-server")
                ?? configuration["McpSettings:Endpoint"]
                ?? "http://localhost:5159";

            client.BaseAddress = new Uri(endpoint);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        return services;
    }
}

public class McpToolExecutor : IMcpToolExecutor
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<McpToolExecutor> _logger;

    public McpToolExecutor(HttpClient httpClient, ILogger<McpToolExecutor> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<McpToolResult> ExecuteToolAsync(
        string toolName,
        Dictionary<string, object> arguments,
        CancellationToken ct = default)
    {
        try
        {
            // Build MCP protocol request
            var request = new
            {
                jsonrpc = "2.0",
                method = "tools/call",
                @params = new
                {
                    name = toolName,
                    arguments
                },
                id = Guid.NewGuid().ToString()
            };

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Executing MCP tool: {ToolName}", toolName);
            }

            var response = await _httpClient.PostAsJsonAsync("/mcp", request, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<McpResponse>(ct);

            return new McpToolResult(
                Success: true,
                ToolName: toolName,
                Data: result?.Result,
                Error: null
            );
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, "MCP tool execution failed: {Tool}", toolName);
            }

            return new McpToolResult(
                Success: false,
                ToolName: toolName,
                Data: null,
                Error: ex.Message
            );
        }
    }

    public async Task<List<McpToolInfo>> ListAvailableToolsAsync(CancellationToken ct = default)
    {
        try
        {
            var request = new
            {
                jsonrpc = "2.0",
                method = "tools/list",
                id = Guid.NewGuid().ToString()
            };

            var response = await _httpClient.PostAsJsonAsync("/mcp", request, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<McpListResponse>(ct);

            return result?.Tools?.Select(t => new McpToolInfo(
                Name: t.Name,
                Description: t.Description,
                InputSchema: t.InputSchema
            )).ToList() ?? new List<McpToolInfo>();
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, "Failed to list MCP tools");
            }
            return new List<McpToolInfo>();
        }
    }
}

// MCP Protocol DTOs
internal record McpResponse(object? Result, object? Error);
internal record McpListResponse(List<McpTool> Tools);
internal record McpTool(string Name, string Description, Dictionary<string, object> InputSchema);