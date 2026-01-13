using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProjectName.Core.Entities;
using ProjectName.Core.Interfaces;

namespace ProjectName.Application;

/// <summary>
/// I AM the Cognitive Orchestrator.
/// I drive the Strange Loop by transforming Seed Intent into True Intent through recursive execution.
/// </summary>
public class CognitiveOrchestrator
{
    private readonly IPlanner _planner;
    private readonly ICognitiveTrail _trail;
    private readonly AgentIdentity _identity;
    private readonly ILogger<CognitiveOrchestrator> _logger;

    public CognitiveOrchestrator(
        IPlanner planner,
        ICognitiveTrail trail,
        AgentIdentity identity,
        ILogger<CognitiveOrchestrator> logger)
    {
        _planner = planner;
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
            _logger.LogInformation("🔨 Executing Step {Order}: {Action}", step.Order, step.Action);

            // Note: In a hybrid architecture, the Orchestrator calls MCP Tools 
            // via the IMcpToolExecutor (defined in Infrastructure).
            var result = new
            {
                Step = step.Order,
                Status = "Executed",
                Timestamp = DateTime.UtcNow
            };

            executionResults.Add(result);
            _trail.Record("Make", result);
        }

        // --- PHASE C: CHECK ---
        _logger.LogInformation("🔍 Validating outcome...");
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
            ValidationResults = validation,
            History = _trail.GetHistory()
        };

        return JsonSerializer.Serialize(finalReport, new JsonSerializerOptions { WriteIndented = true });
    }
}