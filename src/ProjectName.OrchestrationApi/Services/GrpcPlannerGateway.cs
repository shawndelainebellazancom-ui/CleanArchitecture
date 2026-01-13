using ProjectName.Core.Interfaces;
using ProjectName.PlannerService;
using System.Numerics;

namespace ProjectName.OrchestrationApi.Services;

public class GrpcPlannerGateway : IPlanner
{
    private readonly Planner.PlannerClient _grpcClient;
    private readonly ILogger<GrpcPlannerGateway> _logger;

    public GrpcPlannerGateway(Planner.PlannerClient grpcClient, ILogger<GrpcPlannerGateway> logger)
    {
        _grpcClient = grpcClient;
        _logger = logger;
    }

    public async Task<PlanResult> CreatePlanAsync(string intent, CancellationToken ct = default)
    {
        _logger.LogInformation("Forwarding plan request to gRPC service...");

        var request = new PlanRequest { Intent = intent };

        var response = await _grpcClient.CreatePlanAsync(request, cancellationToken: ct);

        if (!response.Success)
        {
            throw new Exception($"Planner service failed: {response.ErrorMessage}");
        }

        var steps = response.Steps.Select(s => new PlanStep
        {
            Order = s.Order,
            Action = s.Action,
            Tool = s.Tool,
            ArgumentsJson = s.ArgumentsJson
        }).ToList();

        return new PlanResult
        {
            Goal = response.Goal,
            Steps = steps,
            Analysis = response.Analysis
        };
    }

    public async Task<string> ValidateOutcomeAsync(string intent, string executionLog, CancellationToken ct = default)
    {
        _logger.LogInformation("Forwarding validation request to gRPC service...");

        var request = new ValidationRequest
        {
            Intent = intent,
            ExecutionLog = executionLog
        };

        var response = await _grpcClient.ValidateOutcomeAsync(request, cancellationToken: ct);

        if (!response.Success)
        {
            throw new Exception($"Validation failed: {response.ErrorMessage}");
        }

        return response.ValidationJson;
    }
}