namespace ProjectName.Infrastructure.MCP;

/// <summary>
/// Interface for executing MCP tools
/// </summary>
public interface IMcpToolExecutor
{
    /// <summary>
    /// Execute a specific MCP tool with arguments
    /// </summary>
    Task<McpToolResult> ExecuteToolAsync(
        string toolName,
        Dictionary<string, object> arguments,
        CancellationToken ct = default);

    /// <summary>
    /// List all available MCP tools from the server
    /// </summary>
    Task<List<McpToolInfo>> ListAvailableToolsAsync(CancellationToken ct = default);
}

/// <summary>
/// Result of an MCP tool execution
/// </summary>
public record McpToolResult(
    bool Success,
    string ToolName,
    object? Data,
    string? Error
);

/// <summary>
/// Information about an available MCP tool
/// </summary>
public record McpToolInfo(
    string Name,
    string Description,
    Dictionary<string, object> InputSchema
);