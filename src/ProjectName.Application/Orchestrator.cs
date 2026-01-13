using System.Text.Json;
using Microsoft.Extensions.Logging;
using ProjectName.Core.Entities;
using ProjectName.Core.Interfaces;
using ProjectName.Infrastructure.MCP;

namespace ProjectName.Application;

public class CognitiveOrchestrator
{
    private readonly IPlanner _planner;
    private readonly IMcpToolExecutor _mcpExecutor;
    private readonly ICognitiveTrail _trail;
    private readonly AgentIdentity _identity;
    private readonly ILogger<CognitiveOrchestrator> _logger;

    public CognitiveOrchestrator(
        IPlanner planner,
        IMcpToolExecutor mcpExecutor,
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
        _logger.LogInformation("[{Phase}] Seed Intent: {Seed}", "INTAKE", seed);

        // --- PHASE P: PLAN ---
        // The Planner (Brain) uses the Genetic Identity to think about the problem.
        var plan = await _planner.CreatePlanAsync(seed, ct);

        // Record the thought process in the Cognitive Trail
        _trail.Record("Plan", new { Seed = seed, Goal = plan.Goal, Analysis = plan.Analysis, Steps = plan.Steps });

        _logger.LogInformation("[{Phase}] Plan Generated: {Count} steps", "PLAN", plan.Steps.Count);

        var executionResults = new List<object>();

        // --- PHASE M: MAKE (The Execution Loop) ---
        foreach (var step in plan.Steps)
        {
            _logger.LogInformation("[{Phase}] Executing Step {Order}: {Action} via {Tool}", "MAKE", step.Order, step.Action, step.Tool);

            try
            {
                var arguments = JsonSerializer.Deserialize<Dictionary<string, object>>(step.ArgumentsJson)
                                ?? new Dictionary<string, object>();

                // Execute the tool via the MCP Server (The Body)
                var result = await _mcpExecutor.ExecuteToolAsync(step.Tool, arguments, ct);

                var stepRecord = new
                {
                    Step = step.Order,
                    Tool = step.Tool,
                    Success = result.Success,
                    Output = result.Data,
                    Error = result.Error
                };

                executionResults.Add(stepRecord);
                _trail.Record("Make", stepRecord);

                if (!result.Success)
                {
                    _logger.LogWarning("Step {Order} failed: {Error}", step.Order, result.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CRITICAL: Execution failure at step {Order}", step.Order);
                // We continue execution to see if later steps can recover or if Partial Success is possible.
            }
        }

        // --- PHASE C: CHECK ---
        _logger.LogInformation("[{Phase}] Validating outcome...", "CHECK");

        var executionJson = JsonSerializer.Serialize(executionResults);

        // Ask the Brain to Audit the Body's work
        var validation = await _planner.ValidateOutcomeAsync(seed, executionJson, ct);

        _trail.Record("Check", validation);

        // --- PHASE R: REFLECT ---
        // Construct the final report, explicitly including the Cognitive Analysis (The "Soul").
        var finalResult = new
        {
            Goal = plan.Goal,
            ThoughtProcess = plan.Analysis, // <--- EXPOSES THE PERSONA/VOICE
            Status = "Completed",
            Validation = JsonSerializer.Deserialize<object>(validation),
            ExecutionLog = executionResults
        };

        return JsonSerializer.Serialize(finalResult, new JsonSerializerOptions { WriteIndented = true });
    }
}