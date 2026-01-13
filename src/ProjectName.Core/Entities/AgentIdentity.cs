namespace ProjectName.Core.Entities;

/// <summary>
/// I AM the Genetic Identity of the Agent.
/// I define the boundaries of the self within the cognitive architecture.
/// </summary>
public record AgentIdentity(
    string Name,
    string Role,
    string Philosophy,
    string Voice);