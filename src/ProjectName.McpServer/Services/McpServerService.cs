using ProjectName.McpServer.Tools;
using System.Text.Json;

namespace ProjectName.McpServer.Services;

/// <summary>
/// MCP Server implementation handling protocol messages and tool routing
/// </summary>
public class McpServerService
{
    private readonly ILogger<McpServerService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, Type> _tools;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public McpServerService(
        ILogger<McpServerService> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _tools = [];

        RegisterTools();
    }

    private void RegisterTools()
    {
        // Register all browser tools
        RegisterTool<BrowserNavigateTool>();
        RegisterTool<BrowserClickTool>();
        RegisterTool<BrowserTypeTool>();
        RegisterTool<BrowserScreenshotTool>();
        RegisterTool<BrowserExtractTool>();
        RegisterTool<BrowserEvaluateTool>();

        _logger.LogInformation("Registered {Count} MCP tools", _tools.Count);
    }

    private void RegisterTool<TTool>() where TTool : class
    {
        var toolInstance = _serviceProvider.GetRequiredService<TTool>();
        var nameProperty = typeof(TTool).GetProperty("Name");

        if (nameProperty != null)
        {
            var name = nameProperty.GetValue(toolInstance) as string;
            if (!string.IsNullOrEmpty(name))
            {
                _tools[name] = typeof(TTool);
                _logger.LogDebug("Registered tool: {ToolName}", name);
            }
        }
    }

    /// <summary>
    /// Handle incoming MCP request
    /// </summary>
    public async Task<McpResponse> HandleRequestAsync(McpRequest request, CancellationToken ct = default)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Handling MCP request: {Method}", request.Method);
        }

        try
        {
            return request.Method switch
            {
                "initialize" => HandleInitialize(request),
                "tools/list" => await HandleListToolsAsync(ct),
                "tools/call" => await HandleToolCallAsync(request, ct),
                _ => McpResponse.CreateError(request.Id, -32601, $"Method not found: {request.Method}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling MCP request");
            return McpResponse.CreateError(request.Id, -32603, ex.Message);
        }
    }

    private static McpResponse HandleInitialize(McpRequest request)
    {
        var result = new
        {
            protocolVersion = "2024-11-05",
            serverInfo = new
            {
                name = "pmcro-mcp-server",
                version = "1.0.0"
            },
            capabilities = new
            {
                tools = new { }
            }
        };

        return McpResponse.CreateSuccess(request.Id, result);
    }

    private async Task<McpResponse> HandleListToolsAsync(CancellationToken ct)
    {
        var tools = new List<object>();

        foreach (var (name, toolType) in _tools)
        {
            var toolInstance = _serviceProvider.GetRequiredService(toolType);

            var nameProperty = toolType.GetProperty("Name");
            var descProperty = toolType.GetProperty("Description");
            var schemaProperty = toolType.GetProperty("InputSchema");

            var toolInfo = new
            {
                name = nameProperty?.GetValue(toolInstance) as string ?? name,
                description = descProperty?.GetValue(toolInstance) as string ?? "",
                inputSchema = schemaProperty?.GetValue(toolInstance) ?? new { }
            };

            tools.Add(toolInfo);
        }

        var result = new { tools };
        return McpResponse.CreateSuccess(null, result);
    }

    private async Task<McpResponse> HandleToolCallAsync(McpRequest request, CancellationToken ct)
    {
        if (request.Params == null)
        {
            return McpResponse.CreateError(request.Id, -32602, "Missing params");
        }

        var paramsElement = (JsonElement)request.Params;

        if (!paramsElement.TryGetProperty("name", out var nameElement))
        {
            return McpResponse.CreateError(request.Id, -32602, "Missing tool name");
        }

        var toolName = nameElement.GetString();

        if (string.IsNullOrEmpty(toolName) || !_tools.TryGetValue(toolName, out var toolType))
        {
            return McpResponse.CreateError(request.Id, -32602, $"Unknown tool: {toolName}");
        }

        var arguments = paramsElement.TryGetProperty("arguments", out var argsElement)
            ? argsElement
            : JsonDocument.Parse("{}").RootElement;

        // Get tool instance
        var tool = _serviceProvider.GetRequiredService(toolType);

        // Find ExecuteAsync method that takes JsonElement
        var executeMethod = toolType.GetMethod("ExecuteAsync", [typeof(JsonElement), typeof(CancellationToken)]);

        if (executeMethod == null)
        {
            return McpResponse.CreateError(request.Id, -32603, "Tool execution method not found");
        }

        // Invoke tool
        var task = executeMethod.Invoke(tool, [arguments, ct]) as Task<McpToolResult>;
        var result = await task!;

        if (!result.IsSuccess)
        {
            return McpResponse.CreateError(request.Id, -32603, result.Error ?? "Tool execution failed");
        }

        var response = new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = JsonSerializer.Serialize(result.Content, JsonOptions)
                }
            }
        };

        return McpResponse.CreateSuccess(request.Id, response);
    }
}

/// <summary>
/// MCP request message
/// </summary>
public record McpRequest(
    string Method,
    string? Id = null,
    object? Params = null)
{
    public string JsonRpc { get; init; } = "2.0";
}

/// <summary>
/// MCP response message
/// </summary>
public record McpResponse(
    string? Id,
    object? Result = null,
    McpError? ErrorInfo = null)
{
    public string JsonRpc { get; init; } = "2.0";

    public static McpResponse CreateSuccess(string? id, object result)
    {
        return new McpResponse(
            Id: id,
            Result: result,
            ErrorInfo: null
        );
    }

    public static McpResponse CreateError(string? id, int code, string message)
    {
        return new McpResponse(
            Id: id,
            Result: null,
            ErrorInfo: new McpError(code, message)
        );
    }
}

/// <summary>
/// MCP error details
/// </summary>
public record McpError(int Code, string Message);