using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using ProjectName.Core.Interfaces;
using ProjectName.Core.Attributes;
using System.Reflection;
using System.Text.Json;
using OllamaSharp;
using OllamaSharp.Models.Chat;
using OllamaSharp.Models;

namespace ProjectName.Infrastructure.Agents;

/// <summary>
/// The Cognitive Planner (Phase P).
/// Reads its Identity from Assembly Metadata and uses strict Block-Based Prompting.
/// </summary>
public class PlannerAgent : IPlanner
{
    private readonly OllamaApiClient _ollama;
    private readonly ILogger<PlannerAgent> _logger;
    private readonly string _modelName;

    // IDENTITY FIELDS (DNA)
    private readonly string _role;
    private readonly string _expertise;
    private readonly string _voice;

    public PlannerAgent(
        IConfiguration config,
        ILogger<PlannerAgent> logger)
    {
        _logger = logger;

        // 1. HARDWARE CONFIG
        var ollamaEndpoint = config["AgentSettings:OllamaEndpoint"] ?? "http://localhost:11434";
        _modelName = config["AgentSettings:ModelName"] ?? "qwen2.5-coder:latest";

        _ollama = new OllamaApiClient(ollamaEndpoint);

        // 2. READ GENETIC MEMORY (Assembly Metadata)
        // This reads the attributes injected by MSBuild in the .csproj
        var persona = typeof(PlannerAgent).Assembly.GetCustomAttribute<AgentPersonaAttribute>();

        if (persona != null)
        {
            _role = persona.Role;
            _expertise = persona.Expertise;
            _voice = persona.Voice;
            _logger.LogInformation("🧬 AGENT DNA ACTIVATED: {Role} | {Voice}", _role, _voice);
        }
        else
        {
            // Fallback for development/testing if attributes aren't present
            _role = "PMCR-O Planner";
            _expertise = "General Orchestration";
            _voice = "Robotic";
            _logger.LogWarning("⚠️ AGENT DNA MISSING. Using defaults.");
        }
    }

    public async Task<PlanResult> CreatePlanAsync(string intent, CancellationToken ct = default)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Creating plan for intent: {Intent}", intent);
        }

        try
        {
            // 3. CONSTRUCT THE COGNITIVE BLOCK
            var systemPrompt = BuildCognitiveArchitecture();
            var userPrompt = BuildIntentBlock(intent);

            var request = new ChatRequest
            {
                Model = _modelName,
                Messages = new List<Message>
                {
                    new(ChatRole.System, systemPrompt),
                    new(ChatRole.User, userPrompt)
                },
                Format = "json",
                Stream = false,
                Options = new RequestOptions
                {
                    Temperature = 0.2f, // Low temperature for strict adherence
                    NumPredict = 4096
                }
            };

            ChatResponseStream? lastResponse = null;
            await foreach (var response in _ollama.ChatAsync(request, ct))
            {
                lastResponse = response;
            }

            var responseContent = lastResponse?.Message?.Content ?? "{}";

            // 4. PARSE & RETURN
            return ParseStructuredResponse(responseContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating plan");
            return CreateFallbackPlan(intent, ex.Message);
        }
    }

    /// <summary>
    /// Builds the System Prompt using the Genetic Identity and BIP Structure.
    /// </summary>
    private string BuildCognitiveArchitecture()
    {
        // $$""" allows interpolation {{var}} while preserving JSON braces { }
        return $$"""
        @persona {
            "role": "{{_role}}",
            "expertise": "{{_expertise}}",
            "voice": "{{_voice}}"
        }

        @context {
            "system": "PMCR-O (Plan-Make-Check-Reflect-Orchestrate)",
            "environment": ".NET 10 / Aspire / MCP",
            "available_tools": [
                "browser_navigate(url)",
                "browser_click(selector)",
                "browser_type(selector, text)",
                "browser_screenshot(path, fullPage)",
                "browser_extract(selector, format)",
                "browser_evaluate(script)"
            ]
        }

        @constraints {
            "output_format": "JSON_ONLY",
            "no_markdown": true,
            "no_hallucinations": "Use only provided tools",
            "thinking": "Required in '@thought' field"
        }

        @format {
            "root_object": {
                "@thought": "Step-by-step reasoning explaining WHY these steps were chosen.",
                "goal": "Refined single-sentence goal",
                "steps": [
                    {
                        "order": 1,
                        "action": "Description of action",
                        "tool": "tool_name",
                        "arguments": { "arg": "value" }
                    }
                ]
            }
        }
        """;
    }

    private static string BuildIntentBlock(string intent)
    {
        return $$"""
        @intent {
            "{{intent}}"
        }

        @instruction {
            "Analyze the @intent using your @expertise.",
            "Formulate a plan using the available tools.",
            "Output ONLY valid JSON matching the @format."
        }
        """;
    }

    private static PlanResult ParseStructuredResponse(string json)
    {
        try
        {
            // Clean markdown blocks if the LLM ignores instructions
            json = json.Replace("```json", "").Replace("```", "").Trim();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Map "@thought" to the Analysis property
            // Check multiple possible keys in case the LLM hallucinates casing
            var thought = root.TryGetProperty("@thought", out var t) ? t.GetString() :
                          root.TryGetProperty("thought", out t) ? t.GetString() :
                          root.TryGetProperty("analysis", out var a) ? a.GetString() :
                          "No thought trace provided.";

            var goal = root.GetProperty("goal").GetString() ?? "No goal defined";

            var steps = new List<PlanStep>();
            if (root.TryGetProperty("steps", out var stepsProp))
            {
                foreach (var stepElement in stepsProp.EnumerateArray())
                {
                    var order = stepElement.GetProperty("order").GetInt32();
                    var action = stepElement.GetProperty("action").GetString() ?? "";
                    var tool = stepElement.GetProperty("tool").GetString() ?? "";

                    var argsJson = stepElement.TryGetProperty("arguments", out var argsElement)
                        ? argsElement.GetRawText()
                        : "{}";

                    steps.Add(new PlanStep(order, action, tool, argsJson));
                }
            }

            return new PlanResult(goal, steps, thought ?? "");
        }
        catch (Exception ex)
        {
            throw new JsonException($"Failed to parse Agent output: {ex.Message}. Raw JSON: {json}");
        }
    }

    private PlanResult CreateFallbackPlan(string intent, string reason)
    {
        return new PlanResult(
            Goal: intent,
            Steps: [],
            Analysis: $"Planning Failed: {reason}. Manual Intervention Required."
        );
    }

    public async Task<string> ValidateOutcomeAsync(string intent, string executionLog, CancellationToken ct = default)
    {
        // Simple validator that adopts the Auditor persona
        // In a full implementation, this would also use a @block prompt

        var prompt = $$"""
        @persona { "role": "Auditor", "expertise": "Compliance and Verification" }
        @context { "task": "Check execution against intent" }
        @intent { "{{intent}}" }
        @data { {{executionLog}} }
        
        Respond with JSON: { "success": true/false, "reasoning": "..." }
        """;

        var request = new ChatRequest
        {
            Model = _modelName,
            Messages = new List<Message> { new(ChatRole.User, prompt) },
            Format = "json",
            Stream = false
        };

        var responseContent = "";
        await foreach (var chunk in _ollama.ChatAsync(request, ct))
        {
            responseContent = chunk.Message.Content;
        }

        return responseContent;
    }
}