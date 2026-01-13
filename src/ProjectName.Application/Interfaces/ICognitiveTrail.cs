// File: ProjectName.Core/Interfaces/ICognitiveTrail.cs
namespace ProjectName.Application.Interfaces;

/// <summary>
/// I AM the memory of the Strange Loop.
/// I record every phase transition and thought artifact.
/// </summary>
public interface ICognitiveTrail
{
    /// <summary>
    /// Records a cognitive event or thought during a specific phase.
    /// </summary>
    /// <param name="phase">The phase name (e.g., "Plan", "Make", "Check").</param>
    /// <param name="data">The data or artifact to record.</param>
    void Record(string phase, object data);

    /// <summary>
    /// Retrieves the complete history of cognitive events.
    /// </summary>
    /// <returns>A JSON string representing the cognitive trail history.</returns>
    string GetHistory();

    /// <summary>
    /// Clears all recorded cognitive history.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ClearAsync();
}