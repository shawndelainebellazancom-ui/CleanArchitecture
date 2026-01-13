
// File: ProjectName.Core/Attributes/AgentPersonaAttribute.cs
using System;

namespace ProjectName.Core.Attributes;

/// <summary>
/// Defines the Genetic Identity of the Agent.
/// This is injected at build time via MSBuild and read at runtime via Reflection.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, Inherited = false)]
public sealed class AgentPersonaAttribute : Attribute
{
    /// <summary>
    /// Gets the agent's role or job title.
    /// </summary>
    public string Role { get; }

    /// <summary>
    /// Gets the agent's domain expertise or specialization.
    /// </summary>
    public string Expertise { get; }

    /// <summary>
    /// Gets the agent's communication voice or personality style.
    /// </summary>
    public string Voice { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentPersonaAttribute"/> class.
    /// </summary>
    /// <param name="role">The agent's role.</param>
    /// <param name="expertise">The agent's expertise.</param>
    /// <param name="voice">The agent's voice.</param>
    public AgentPersonaAttribute(string role, string expertise, string voice)
    {
        Role = role;
        Expertise = expertise;
        Voice = voice;
    }
}
