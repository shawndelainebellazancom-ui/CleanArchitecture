
// File: ProjectName.Core/Interfaces/IMcpToolExecutor.cs
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ProjectName.Application.Interfaces;

/// <summary>
/// Executes MCP (Model Context Protocol) tools remotely.
/// </summary>
public interface IMcpToolExecutor
{
    /// <summary>
    /// Executes an MCP tool with the specified arguments.
    /// </summary>
    /// <param name="toolName">The name of the tool to execute.</param>
    /// <param name="arguments">The arguments to pass to the tool.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The execution result.</returns>
    Task<McpExecutionResult> ExecuteAsync(
        string toolName,
        Dictionary<string, object> arguments,
        CancellationToken ct = default);
}

/// <summary>
/// Represents the result of an MCP tool execution.
/// </summary>
/// <param name="Success">Whether the execution was successful.</param>
/// <param name="Output">The output from the tool execution.</param>
/// <param name="Error">The error message if execution failed.</param>
public record McpExecutionResult(bool Success, string Output, string? Error);
