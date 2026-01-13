// File: ProjectName.Core/PlanModels.cs
using System;

namespace ProjectName.Core;

/// <summary>
/// Represents a persistent cognitive trail entry in the database.
/// </summary>
public class ThoughtEntry
{
    /// <summary>
    /// Gets or sets the unique identifier for this thought.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the timestamp when this thought was recorded.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the original intent or query.
    /// </summary>
    public string Intent { get; set; } = default!;

    /// <summary>
    /// Gets or sets the analysis or reasoning.
    /// </summary>
    public string Analysis { get; set; } = default!;

    /// <summary>
    /// Gets or sets the plan as a JSON string.
    /// </summary>
    public string PlanJson { get; set; } = default!;
}