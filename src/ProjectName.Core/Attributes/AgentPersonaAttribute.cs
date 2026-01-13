using System;

namespace ProjectName.Core.Attributes;

/// <summary>
/// Defines the Genetic Identity of the Agent.
/// This is injected at build time via MSBuild and read at runtime via Reflection.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, Inherited = false)]
public sealed class AgentPersonaAttribute : Attribute
{
    public string Role { get; }
    public string Expertise { get; }
    public string Voice { get; }

    public AgentPersonaAttribute(string role, string expertise, string voice)
    {
        Role = role;
        Expertise = expertise;
        Voice = voice;
    }
}