using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ProjectName.Application.Interfaces;
using ProjectName.Core.Attributes;
using System.Reflection;
using System.Text.Json;

namespace ProjectName.Infrastructure.Agents;

/// <summary>
/// Production-ready Cognitive Planner using IChatClient (Microsoft.Extensions.AI)
/// Works with OllamaSharp implementation
/// </summary>
public class PlannerAgent : IPlanner
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<PlannerAgent> _logger;
    private readonly string _role;
    private readonly string _expertise;
    private readonly string _voice;

    public PlannerAgent(
        IChatClient chatClient,
        ILogger<PlannerAgent> logger)
    {
        _chatClient = chatClient;
        _logger = logger;

        // Extract persona from assembly attributes
        var persona = GetPersonaAttribute();
        _role = persona?.Role ?? "PMCR-O Planner";
        _expertise = persona?.Expertise ?? "General Orchestration";
        _voice = persona?.Voice ?? "Analytical and Precise";
    }

    private static AgentPersonaAttribute? GetPersonaAttribute()
    {
        return typeof(PlannerAgent).Assembly.GetCustomAttribute<AgentPersonaAttribute>();
    }

    public async Task<PlanResult> CreatePlanAsync(string intent, CancellationToken ct = default)
    {
        _logger.LogInformation("🧠 Creating plan for intent: {Intent}", intent);

        try
        {
            // Build chat history with system and user messages
            var chatHistory = new List<ChatMessage>
            {
                new(ChatRole.System, BuildCognitiveInstructions()),
                new(ChatRole.User, BuildIntentPrompt(intent))
            };

            // Call the LLM with JSON format
            var response = await _chatClient.GetResponseAsync(
                chatHistory,
                new ChatOptions
                {
                    ResponseFormat = ChatResponseFormat.Json
                },
                ct
            );

            var content = response.Messages[^1].Text ?? "{}";

            _logger.LogDebug("📝 Raw LLM Response: {Response}", content);

            return ParseStructuredResponse(content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error creating plan");
            return CreateFallbackPlan(intent, ex.Message);
        }
    }

    private string BuildCognitiveInstructions()
    {
        return $$"""
        You are {{_role}}, an AI agent specialized in {{_expertise}}.
        Your communication style is: {{_voice}}.

        SYSTEM CONTEXT:
        - Framework: PMCR-O (Plan-Make-Check-Reflect-Orchestrate)
        - Environment: .NET 10 / Aspire / MCP
        - Available MCP Tools:
          * browser_navigate(url)
          * browser_click(selector)
          * browser_type(selector, text)
          * browser_screenshot(path, fullPage)
          * browser_extract(selector, format)
          * browser_evaluate(script)

        OUTPUT REQUIREMENTS:
        1. Respond ONLY with valid JSON (no markdown, no preamble)
        2. Include a "@thought" field with your reasoning
        3. Break down tasks into atomic, executable steps
        4. Use only the MCP tools listed above

        JSON FORMAT:
        {
          "@thought": "Your step-by-step reasoning in {{_voice}} style",
          "goal": "Single-sentence refined goal",
          "steps": [
            {
              "order": 1,
              "action": "Description of what to do",
              "tool": "tool_name",
              "arguments": { "key": "value" }
            }
          ]
        }

        CRITICAL: Output MUST be valid JSON. No other text allowed.
        """;
    }

    private string BuildIntentPrompt(string intent)
    {
        return $$"""
        USER INTENT: {{intent}}

        TASK: Analyze this intent and create an execution plan.
        - Think step-by-step in your {{_voice}} voice
        - Output ONLY the JSON object (no explanation outside JSON)
        - Ensure each step uses an available MCP tool
        - Be specific with tool arguments (actual URLs, selectors, etc.)
        """;
    }

    private PlanResult ParseStructuredResponse(string json)
    {
        try
        {
            // Clean any markdown artifacts
            json = json.Replace("```json", "").Replace("```", "").Trim();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Extract thought/analysis
            var thought = root.TryGetProperty("@thought", out var t) ? t.GetString() :
                          root.TryGetProperty("thought", out t) ? t.GetString() :
                          root.TryGetProperty("analysis", out var a) ? a.GetString() :
                          "No reasoning provided.";

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

                    steps.Add(new PlanStep
                    {
                        Order = order,
                        Action = action,
                        Tool = tool,
                        ArgumentsJson = argsJson
                    });
                }
            }

            _logger.LogInformation("✅ Parsed {StepCount} execution steps", steps.Count);

            return new PlanResult
            {
                Goal = goal,
                Steps = steps,
                Analysis = thought ?? ""
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse LLM output. Raw JSON: {Json}", json);
            throw new JsonException($"Failed to parse Agent output: {ex.Message}. Raw JSON: {json}");
        }
    }

    private static PlanResult CreateFallbackPlan(string intent, string reason)
    {
        return new PlanResult
        {
            Goal = intent,
            Steps = [],
            Analysis = $"⚠️ Planning Failed: {reason}. Manual Intervention Required."
        };
    }

    public async Task<string> ValidateOutcomeAsync(string intent, string executionLog, CancellationToken ct = default)
    {
        var prompt = $$"""
        ROLE: Auditor and Compliance Verifier
        TASK: Validate execution results against original intent
        
        ORIGINAL INTENT: {{intent}}
        
        EXECUTION LOG:
        {{executionLog}}
        
        OUTPUT FORMAT (JSON only):
        {
          "success": true/false,
          "reasoning": "Detailed explanation of validation decision"
        }
        """;

        var chatHistory = new List<ChatMessage>
        {
            new(ChatRole.User, prompt)
        };

        var response = await _chatClient.GetResponseAsync(
            chatHistory,
            new ChatOptions
            {
                ResponseFormat = ChatResponseFormat.Json
            },
            ct
        );

        return response.Messages[^1].Text ?? "{}";
    }
}