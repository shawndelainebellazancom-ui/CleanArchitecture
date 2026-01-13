namespace ProjectName.Core.Interfaces;

/// <summary>
/// Represents a single step in an execution plan
/// </summary>
public sealed record PlanStep(
    int Order,
    string Action,
    string Tool,
    string ArgumentsJson = "{}");

/// <summary>
/// Represents the complete plan with analysis and steps
/// </summary>
public sealed record PlanResult(
    string Goal,
    List<PlanStep> Steps,
    string Analysis);

/// <summary>
/// Planning interface for PMCR-O Phase P (Plan)
/// </summary>
public interface IPlanner
{
    /// <summary>
    /// Creates a structured plan from user intent using LLM reasoning
    /// </summary>
    Task<PlanResult> CreatePlanAsync(string intent, CancellationToken ct = default);
}