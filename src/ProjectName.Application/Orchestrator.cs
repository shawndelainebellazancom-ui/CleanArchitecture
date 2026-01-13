using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProjectName.Application.Interfaces; // Using the interface from Core
using ProjectName.Core.Entities;

namespace ProjectName.Application;

/// <summary>
/// I AM the Cognitive Orchestrator.
/// I drive the Strange Loop by transforming Seed Intent into True Intent through recursive execution.
/// </summary>
public class CognitiveOrchestrator
{
    private readonly IPlanner _planner;
    private readonly IMcpToolExecutor _mcpExecutor; // Added: The "Hands"
    private readonly ICognitiveTrail _trail;
    private readonly AgentIdentity _identity;
    private readonly ILogger<CognitiveOrchestrator> _logger;

    public CognitiveOrchestrator(
        IPlanner planner,
        IMcpToolExecutor mcpExecutor, // Injected here
        ICognitiveTrail trail,
        AgentIdentity identity,
        ILogger<CognitiveOrchestrator> logger)
    {
        _planner = planner;
        _mcpExecutor = mcpExecutor;
        _trail = trail;
        _identity = identity;
        _logger = logger;
    }

    public async Task<string> ProcessIntent(string seed, CancellationToken ct = default)
    {
        _logger.LogInformation("🧠 [IDENTITY: {Name}] Ingesting Seed Intent: {Seed}", _identity.Name, seed);

        // --- PHASE P: PLAN ---
        // The Brain (IPlanner) uses the Genetic Identity to decompose the goal.
        var plan = await _planner.CreatePlanAsync(seed, ct);

        _trail.Record("Plan", new
        {
            Goal = plan.Goal,
            Analysis = plan.Analysis,
            StepCount = plan.Steps.Count
        });

        _logger.LogInformation("🧭 Plan Converged: {Analysis}", plan.Analysis);

        var executionResults = new List<object>();

        // --- PHASE M: MAKE (Execution) ---
        foreach (var step in plan.Steps)
        {
            _logger.LogInformation("🔨 Executing Step {Order}: {Action} [Tool: {Tool}]", step.Order, step.Action, step.Tool);

            McpExecutionResult result;

            try
            {
                // Deserialize the arguments from the JSON plan
                var args = JsonSerializer.Deserialize<Dictionary<string, object>>(step.ArgumentsJson)
                           ?? new Dictionary<string, object>();

                // ACTUALLY EXECUTE THE TOOL via the MCP Client
                result = await _mcpExecutor.ExecuteAsync(step.Tool, args, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute step {Order}", step.Order);
                result = new McpExecutionResult(false, string.Empty, ex.Message);
            }

            // Create a record of what happened
            var executionRecord = new
            {
                Step = step.Order,
                Tool = step.Tool,
                Status = result.Success ? "Success" : "Failed",
                Output = result.Output, // IMPORTANT: The Validator reads this to confirm the file exists!
                Error = result.Error,
                Timestamp = DateTime.UtcNow
            };

            executionResults.Add(executionRecord);
            _trail.Record("Make", executionRecord);

            if (!result.Success)
            {
                _logger.LogWarning("⚠️ Step {Order} failed: {Error}", step.Order, result.Error);
                // Optional: Break loop on critical failure? 
                // For now, we continue and let the Check phase decide.
            }
            else
            {
                _logger.LogInformation("✅ Step {Order} Output: {Output}", step.Order, result.Output);
            }
        }

        // --- PHASE C: CHECK ---
        _logger.LogInformation("🔍 Validating outcome...");

        // We send the full execution log (including tool outputs) back to the brain
        var executionLog = JsonSerializer.Serialize(executionResults);
        var validation = await _planner.ValidateOutcomeAsync(seed, executionLog, ct);

        _trail.Record("Check", new { Validation = validation });

        // --- PHASE R: REFLECT ---
        _logger.LogInformation("🪞 Reflecting on cognitive performance...");
        var finalReport = new
        {
            Agent = _identity.Name,
            Philosophy = _identity.Philosophy,
            Summary = "Cycle Complete",
            PlanAnalysis = plan.Analysis,
            ValidationResults = validation, // This will now likely contain "success": true
            History = _trail.GetHistory()
        };

        return JsonSerializer.Serialize(finalReport, new JsonSerializerOptions { WriteIndented = true });
    }
}