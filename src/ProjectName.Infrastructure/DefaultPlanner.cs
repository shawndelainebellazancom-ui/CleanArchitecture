using ProjectName.Application.Interfaces;
using System.Text.Json;

namespace ProjectName.Infrastructure;

public class DefaultPlanner : IPlanner
{
    public Task<PlanResult> CreatePlanAsync(string intent, CancellationToken ct = default)
    {
        // Simple fallback plan using required init syntax
        var steps = new List<PlanStep>
        {
            new() { Order = 1, Action = "Analyze Input", Tool = "manual_intervention", ArgumentsJson = "{}" },
            new() { Order = 2, Action = "Reflect", Tool = "manual_intervention", ArgumentsJson = "{}" }
        };

        var result = new PlanResult
        {
            Goal = intent,
            Steps = steps,
            Analysis = "Default planner execution"
        };

        return Task.FromResult(result);
    }

    public Task<string> ValidateOutcomeAsync(string intent, string executionLog, CancellationToken ct = default)
    {
        // Default validation always passes
        var validation = new
        {
            success = true,
            reasoning = "Default planner assumes success without AI validation.",
            correction = (string?)null
        };

        return Task.FromResult(JsonSerializer.Serialize(validation));
    }
}