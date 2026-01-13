using System.Text.Json;

namespace ProjectName.McpServer.Tools;

/// <summary>
/// Base class for all MCP tools with structured input/output
/// Provides JSON schema generation and execution framework
/// </summary>
public abstract class McpToolBase<TInput, TOutput>
{
    protected ILogger Logger { get; }

    protected McpToolBase(ILogger logger)
    {
        Logger = logger;
    }

    /// <summary>
    /// Tool identifier (e.g., "browser_navigate")
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Human-readable description of what this tool does
    /// </summary>
    public abstract string Description { get; }

    /// <summary>
    /// JSON schema for input validation
    /// </summary>
    public virtual object InputSchema => GenerateInputSchema();

    /// <summary>
    /// Execute the tool with typed input
    /// </summary>
    public abstract Task<TOutput> ExecuteAsync(TInput input, CancellationToken ct = default);

    /// <summary>
    /// Execute the tool with raw JSON input (MCP protocol)
    /// </summary>
    public async Task<McpToolResult> ExecuteAsync(JsonElement arguments, CancellationToken ct = default)
    {
        try
        {
            // Deserialize to typed input
            var input = JsonSerializer.Deserialize<TInput>(arguments.GetRawText(), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (input == null)
            {
                return McpToolResult.Failure(Name, "Failed to deserialize input arguments");
            }

            // Execute
            var output = await ExecuteAsync(input, ct);

            // Serialize result
            return McpToolResult.Success(Name, output);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Tool execution failed: {Tool}", Name);
            return McpToolResult.Failure(Name, ex.Message);
        }
    }

    /// <summary>
    /// Generate JSON schema from TInput type
    /// </summary>
    protected virtual object GenerateInputSchema()
    {
        var type = typeof(TInput);
        var properties = new Dictionary<string, object>();
        var required = new List<string>();

        foreach (var prop in type.GetProperties())
        {
            var propName = char.ToLowerInvariant(prop.Name[0]) + prop.Name[1..];

            properties[propName] = new
            {
                type = GetJsonType(prop.PropertyType),
                description = GetPropertyDescription(prop)
            };

            // Required if not nullable
            if (!IsNullable(prop.PropertyType))
            {
                required.Add(propName);
            }
        }

        return new
        {
            type = "object",
            properties,
            required = required.Count > 0 ? required : null
        };
    }

    private static string GetJsonType(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        if (underlyingType == typeof(string))
            return "string";
        if (underlyingType == typeof(int) || underlyingType == typeof(long))
            return "integer";
        if (underlyingType == typeof(bool))
            return "boolean";
        if (underlyingType == typeof(double) || underlyingType == typeof(float) || underlyingType == typeof(decimal))
            return "number";
        if (underlyingType.IsArray || typeof(System.Collections.IEnumerable).IsAssignableFrom(underlyingType))
            return "array";

        return "object";
    }

    private static bool IsNullable(Type type)
    {
        return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
    }

    private static string GetPropertyDescription(System.Reflection.PropertyInfo prop)
    {
        // Try to get description from XML comments or attributes
        // For now, return a default description
        return $"{prop.Name} parameter";
    }
}

/// <summary>
/// MCP tool execution result
/// </summary>
public record McpToolResult
{
    public bool IsSuccess { get; init; }
    public string ToolName { get; init; } = "";
    public object? Content { get; init; }
    public string? Error { get; init; }

    public static McpToolResult Success(string toolName, object content)
    {
        return new McpToolResult
        {
            IsSuccess = true,
            ToolName = toolName,
            Content = content,
            Error = null
        };
    }

    public static McpToolResult Failure(string toolName, string error)
    {
        return new McpToolResult
        {
            IsSuccess = false,
            ToolName = toolName,
            Content = null,
            Error = error
        };
    }
}