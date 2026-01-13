using System;

namespace ProjectName.Core.Entities;

/// <summary>
/// A single snapshot of the Agent's consciousness at a specific point in the loop.
/// </summary>
public record Thought(
    string Phase,
    string Content,
    DateTime Timestamp,
    string CorrelationId);