using Microsoft.AspNetCore.Mvc;
using ProjectName.Application;
using System.ComponentModel.DataAnnotations;

namespace ProjectName.OrchestrationApi.Controllers;

/// <summary>
/// REST API Gateway for PMCR-O Orchestration
/// Acts as the Nervous System trigger, invoking the Cognitive Orchestrator
/// to execute the full Plan-Make-Check-Reflect loop.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class OrchestrationController : ControllerBase
{
    private readonly CognitiveOrchestrator _orchestrator;
    private readonly ILogger<OrchestrationController> _logger;

    public OrchestrationController(
        CognitiveOrchestrator orchestrator,
        ILogger<OrchestrationController> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    /// <summary>
    /// Executes a full PMCR-O cognitive cycle based on user intent.
    /// 1. PLANS using the Planner Service (Brain/LLM).
    /// 2. MAKES using the MCP Server (Body/Tools).
    /// 3. RECORDS the cognitive trail.
    /// </summary>
    /// <param name="request">The seed intent (e.g., "Navigate to google.com...")</param>
    /// <returns>A JSON report containing the Goal, Status, and Execution Log.</returns>
    [HttpPost("execute")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExecuteIntent(
        [FromBody] CreatePlanRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        _logger.LogInformation("Received execution request: {Intent}", request.Intent);

        try
        {
            // Triggers the Full PMCR-O Loop (Plan -> Make -> Check -> Reflect)
            // The Orchestrator handles the distributed coordination between gRPC Planner and HTTP MCP.
            var resultJson = await _orchestrator.ProcessIntent(request.Intent, ct);

            // Return raw JSON because the Orchestrator returns a serialized string 
            // containing dynamic tool outputs.
            return Content(resultJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Orchestration failure");
            return StatusCode(500, new { Error = ex.Message, StackTrace = ex.StackTrace });
        }
    }

    /// <summary>
    /// Simple health check to verify API availability.
    /// </summary>
    [HttpGet("health")]
    public IActionResult HealthCheck()
    {
        return Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow });
    }
}

// DTOs for REST API

public class CreatePlanRequest
{
    [Required]
    [MinLength(1)]
    public string Intent { get; set; } = string.Empty;
}