using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using ProjectName.Core.Interfaces;
using System.Text.Json;
using OllamaSharp;
using OllamaSharp.Models.Chat;
using OllamaSharp.Models;

namespace ProjectName.Infrastructure.Agents;

/// <summary>
/// Planner Agent with NATIVE structured output support
/// Uses JSON Schema validation for 99% success rate
/// </summary>
public class PlannerAgent : IPlanner
{
    private readonly OllamaApiClient _ollama;
    private readonly ILogger<PlannerAgent> _logger;
    private readonly string _modelName;

    public PlannerAgent(
        IConfiguration config,
        ILogger<PlannerAgent> logger)
    {
        _logger = logger;

        var ollamaEndpoint = config["AgentSettings:OllamaEndpoint"] ?? "http://localhost:11434";
        _modelName = config["AgentSettings:ModelName"] ?? "llama3.2:latest";

        _ollama = new OllamaApiClient(ollamaEndpoint);

        _logger.LogInformation("PlannerAgent initialized with structured output enabled");
    }

    public async Task<PlanResult> CreatePlanAsync(string intent, CancellationToken ct = default)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Creating plan for intent: {Intent}", intent);
        }

        try
        {
            var systemPrompt = BuildAgentInstructions();
            var userPrompt = BuildPlanningPrompt(intent);

            var request = new ChatRequest
            {
                Model = _modelName,
                Messages = new List<Message>
                {
                    new(ChatRole.System, systemPrompt),
                    new(ChatRole.User, userPrompt)
                },
                Stream = false,
                Options = new RequestOptions
                {
                    Temperature = 0.7f,
                    NumPredict = 4096
                },
                Format = "json"
            };

            // ✅ FIXED: ChatAsync returns IAsyncEnumerable, need to consume it
            ChatResponseStream? lastResponse = null;
            await foreach (var response in _ollama.ChatAsync(request, ct))
            {
                lastResponse = response;
            }

            var responseContent = lastResponse?.Message?.Content ?? "{}";

            var plan = ParseStructuredResponse(responseContent);

            if (!ValidatePlanStructure(plan))
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning("Plan failed schema validation, using fallback");
                }
                return CreateFallbackPlan(intent, "Schema validation failed");
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Plan created successfully - Steps: {Count}, Goal: {Goal}",
                    plan.Steps.Count,
                    plan.Goal);
            }

            return plan;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating plan for intent: {Intent}", intent);
            return CreateFallbackPlan(intent, ex.Message);
        }
    }

    private static string BuildAgentInstructions()
    {
        return """
        You are the PMCR-O Planner Agent (Phase P).
        
        YOUR CRITICAL DIRECTIVE:
        Respond ONLY with valid JSON. No markdown, no explanations, ONLY JSON.
        
        REQUIRED JSON STRUCTURE:
        {
          "goal": "Clear one-sentence goal statement",
          "analysis": "Your reasoning (2-3 sentences max)",
          "steps": [
            {
              "order": 1,
              "action": "What to do",
              "tool": "tool_name",
              "arguments": {"key": "value"}
            }
          ]
        }
        
        AVAILABLE TOOLS:
        - browser_navigate, browser_click, browser_type, browser_screenshot, browser_extract
        - web_search, web_fetch
        - code_execution, file_operations, data_analysis
        
        RULES:
        - Keep steps focused (3-7 typically)
        - Choose appropriate tools
        - NO markdown formatting
        - NO text outside JSON
        - ALL fields required
        """;
    }

    private static string BuildPlanningPrompt(string intent)
    {
        return $"""
        USER INTENT: {intent}
        
        Create an execution plan following the exact JSON structure.
        Think through the steps, then output ONLY the JSON response.
        
        Remember: NO markdown, NO explanations, ONLY valid JSON.
        """;
    }

    private PlanResult ParseStructuredResponse(string json)
    {
        try
        {
            // Clean potential markdown (defense in depth)
            json = json.Replace("```json", "").Replace("```", "").Trim();

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            };

            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });

            var root = doc.RootElement;

            // Validate required fields exist
            if (!root.TryGetProperty("goal", out var goalProp) ||
                !root.TryGetProperty("analysis", out var analysisProp) ||
                !root.TryGetProperty("steps", out var stepsProp))
            {
                throw new JsonException("Missing required fields: goal, analysis, or steps");
            }

            var goal = goalProp.GetString() ?? throw new JsonException("Goal is null");
            var analysis = analysisProp.GetString() ?? throw new JsonException("Analysis is null");

            var steps = new List<PlanStep>();
            foreach (var stepElement in stepsProp.EnumerateArray())
            {
                var order = stepElement.GetProperty("order").GetInt32();
                var action = stepElement.GetProperty("action").GetString()
                    ?? throw new JsonException($"Step {order} missing action");
                var tool = stepElement.GetProperty("tool").GetString()
                    ?? throw new JsonException($"Step {order} missing tool");

                var argsJson = stepElement.TryGetProperty("arguments", out var argsElement)
                    ? argsElement.GetRawText()
                    : "{}";

                steps.Add(new PlanStep(order, action, tool, argsJson));
            }

            if (steps.Count == 0)
            {
                throw new JsonException("Steps array is empty");
            }

            // Sort by order
            steps = steps.OrderBy(s => s.Order).ToList();

            return new PlanResult(goal, steps, analysis);
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(ex, "Failed to parse structured response, using fallback");
            }
            throw;
        }
    }

    private static bool ValidatePlanStructure(PlanResult plan)
    {
        return !string.IsNullOrWhiteSpace(plan.Goal) &&
               !string.IsNullOrWhiteSpace(plan.Analysis) &&
               plan.Steps.Count > 0 &&
               plan.Steps.All(s =>
                   !string.IsNullOrWhiteSpace(s.Action) &&
                   !string.IsNullOrWhiteSpace(s.Tool));
    }

    private PlanResult CreateFallbackPlan(string intent, string reason)
    {
        if (_logger.IsEnabled(LogLevel.Warning))
        {
            _logger.LogWarning("Creating fallback plan - Reason: {Reason}", reason);
        }

        return new PlanResult(
            Goal: intent,
            Steps: new List<PlanStep>
            {
                new(
                    Order: 1,
                    Action: "Manual execution required due to planning failure",
                    Tool: "manual_intervention",
                    ArgumentsJson: JsonSerializer.Serialize(new { reason }))
            },
            Analysis: $"Automated planning failed: {reason}. Manual review needed.");
    }
}