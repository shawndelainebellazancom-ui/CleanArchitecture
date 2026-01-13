using System.Text.Json;
using ProjectName.Core.Interfaces;

namespace ProjectName.Infrastructure;

public class DefaultPlanner : IPlanner
{
    public Task<PlanResult> CreatePlanAsync(string intent, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<string> PlanBareMinimum(string seedIntent)
    {
        var plan = new
        {
            Steps = new[] { "Analyze Input", "Generate Artifact", "Reflect" },
            Target = seedIntent
        };
        return Task.FromResult(JsonSerializer.Serialize(plan));
    }
}