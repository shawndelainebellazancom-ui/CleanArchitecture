using System.Text.Json;
using ProjectName.Core.Interfaces;

namespace ProjectName.Infrastructure;

public class DefaultPlanner : IPlanner
{
    public Task<PlanResult> CreatePlanAsync(string intent, CancellationToken ct = default)
    {
        // Simple fallback plan
        var steps = new List<PlanStep>
        {
            new(1, "Analyze Input", "manual_intervention", "{}"),
            new(2, "Reflect", "manual_intervention", "{}")
        };

        return Task.FromResult(new PlanResult(intent, steps, "Default planner execution"));
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

    // Legacy method kept if needed, or can be removed if not part of interface
    public static Task<string> PlanBareMinimum(string seedIntent)
    {
        var plan = new
        {
            Steps = new[] { "Analyze Input", "Generate Artifact", "Reflect" },
            Target = seedIntent
        };
        return Task.FromResult(JsonSerializer.Serialize(plan));
    }
}