using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;
using ProjectName.Application.Interfaces; // ✅ Now using the correct Core interface

namespace ProjectName.Infrastructure.MCP;

public static class McpClientConfiguration
{
    public static IServiceCollection AddMcpClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register the Core Interface implementation
        services.AddHttpClient<IMcpToolExecutor, McpToolExecutor>((sp, client) =>
        {
            // Aspire resolves "mcp-server" automatically
            var endpoint = configuration.GetConnectionString("mcp-server")
                ?? configuration["McpSettings:Endpoint"]
                ?? "http://localhost:5159";

            client.BaseAddress = new Uri(endpoint);
            client.Timeout = TimeSpan.FromMinutes(5);
        });

        return services;
    }
}

public class McpToolExecutor : IMcpToolExecutor
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<McpToolExecutor> _logger;
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public McpToolExecutor(HttpClient httpClient, ILogger<McpToolExecutor> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<McpExecutionResult> ExecuteAsync(
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

            var result = await response.Content.ReadFromJsonAsync<McpResponse>(_jsonOptions, ct);

            if (result?.ErrorInfo != null)
            {
                return new McpExecutionResult(false, string.Empty, result.ErrorInfo.ToString());
            }

            // Serialize the result object to a string so the Planner/Orchestrator can log it
            string output = result?.Result != null
                ? JsonSerializer.Serialize(result.Result, _jsonOptions)
                : "{}";

            return new McpExecutionResult(true, output, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MCP tool execution failed: {Tool}", toolName);
            return new McpExecutionResult(false, string.Empty, ex.Message);
        }
    }
}

// Internal DTOs for MCP Protocol
internal record McpResponse(object? Result, object? ErrorInfo);