using Grpc.Net.Client;
using Microsoft.AspNetCore.Mvc;
using ProjectName.PlanerService;
using System.ComponentModel.DataAnnotations;

namespace ProjectName.OrchestrationApi.Controllers;

/// <summary>
/// REST API Gateway for PMCR-O Orchestration
/// Exposes HTTP endpoints that internally call gRPC services
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class OrchestrationController : ControllerBase
{
    private readonly Planner.PlannerClient _plannerClient;
    private readonly ILogger<OrchestrationController> _logger;

    public OrchestrationController(
        Planner.PlannerClient plannerClient,
        ILogger<OrchestrationController> logger)
    {
        _plannerClient = plannerClient;
        _logger = logger;
    }

    /// <summary>
    /// Creates an execution plan from user intent
    /// </summary>
    /// <param name="request">Intent request with user's goal</param>
    /// <returns>Structured plan with steps and analysis</returns>
    [HttpPost("plan")]
    [ProducesResponseType(typeof(PlanResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreatePlan(
        [FromBody] CreatePlanRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ErrorResponse
            {
                Error = "Invalid request",
                Details = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList()
            });
        }

        _logger.LogInformation("Received plan request: {Intent}", request.Intent);

        try
        {
            // Call gRPC Planner service
            var grpcRequest = new PlanRequest
            {
                Intent = request.Intent
            };

            var grpcResponse = await _plannerClient.CreatePlanAsync(
                grpcRequest,
                cancellationToken: ct);

            if (!grpcResponse.Success)
            {
                _logger.LogWarning("Plan creation failed: {Error}", grpcResponse.ErrorMessage);

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new ErrorResponse
                    {
                        Error = "Plan creation failed",
                        Details = new List<string> { grpcResponse.ErrorMessage }
                    });
            }

            // Convert gRPC response to REST DTO
            var response = new PlanResponseDto
            {
                Goal = grpcResponse.Goal,
                Analysis = grpcResponse.Analysis,
                Steps = grpcResponse.Steps.Select(s => new PlanStepDto
                {
                    Order = s.Order,
                    Action = s.Action,
                    Tool = s.Tool,
                    Arguments = ParseArguments(s.ArgumentsJson)
                }).ToList()
            };

            _logger.LogInformation(
                "Plan created successfully - Steps: {Count}",
                response.Steps.Count);

            return Ok(response);
        }
        catch (Grpc.Core.RpcException ex)
        {
            _logger.LogError(ex, "gRPC error while creating plan");

            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new ErrorResponse
                {
                    Error = "Planner service unavailable",
                    Details = new List<string> { ex.Status.Detail }
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating plan");

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new ErrorResponse
                {
                    Error = "Internal server error",
                    Details = new List<string> { ex.Message }
                });
        }
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> HealthCheck(CancellationToken ct)
    {
        try
        {
            var response = await _plannerClient.HealthCheckAsync(
                new HealthCheckRequest(),
                cancellationToken: ct);

            return Ok(new HealthResponse
            {
                Status = response.Status.ToString(),
                Message = response.Message,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");

            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new HealthResponse
                {
                    Status = "Unhealthy",
                    Message = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
        }
    }

    private Dictionary<string, object>? ParseArguments(string argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson) || argumentsJson == "{}")
        {
            return null;
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(argumentsJson);
        }
        catch
        {
            return null;
        }
    }
}

// DTOs for REST API

public class CreatePlanRequest
{
    [Required]
    [MinLength(1)]
    public string Intent { get; set; } = string.Empty;
}

public class PlanResponseDto
{
    public string Goal { get; set; } = string.Empty;
    public string Analysis { get; set; } = string.Empty;
    public List<PlanStepDto> Steps { get; set; } = new();
}

public class PlanStepDto
{
    public int Order { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Tool { get; set; } = string.Empty;
    public Dictionary<string, object>? Arguments { get; set; }
}

public class ErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public List<string> Details { get; set; } = new();
}

public class HealthResponse
{
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}