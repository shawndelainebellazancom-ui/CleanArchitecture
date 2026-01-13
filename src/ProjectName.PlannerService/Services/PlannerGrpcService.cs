using Grpc.Core;
using ProjectName.Application.Interfaces;
using System.Diagnostics;

namespace ProjectName.PlannerService.Services;

public class PlannerGrpcService : Planner.PlannerBase
{
    private readonly IPlanner _planner;
    private readonly ILogger<PlannerGrpcService> _logger;

    public PlannerGrpcService(IPlanner planner, ILogger<PlannerGrpcService> logger)
    {
        _planner = planner;
        _logger = logger;
    }

    public override async Task<PlanResponse> CreatePlan(
        PlanRequest request,
        ServerCallContext context)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("CreatePlan called - Intent: {Intent}", request.Intent);

        try
        {
            if (string.IsNullOrWhiteSpace(request.Intent))
            {
                return new PlanResponse { Success = false, ErrorMessage = "Intent cannot be empty" };
            }

            var plan = await _planner.CreatePlanAsync(request.Intent, context.CancellationToken);
            sw.Stop();

            var response = new PlanResponse
            {
                Success = true,
                Goal = plan.Goal,
                Analysis = plan.Analysis
            };

            foreach (var step in plan.Steps)
            {
                response.Steps.Add(new PlanStepMessage
                {
                    Order = step.Order,
                    Action = step.Action,
                    Tool = step.Tool,
                    ArgumentsJson = step.ArgumentsJson
                });
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating plan");
            return new PlanResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    public override async Task<ValidationResponse> ValidateOutcome(
        ValidationRequest request,
        ServerCallContext context)
    {
        try
        {
            var result = await _planner.ValidateOutcomeAsync(request.Intent, request.ExecutionLog, context.CancellationToken);

            return new ValidationResponse
            {
                Success = true,
                ValidationJson = result
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating outcome");
            return new ValidationResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    public override Task<HealthCheckResponse> HealthCheck(
        HealthCheckRequest request,
        ServerCallContext context)
    {
        return Task.FromResult(new HealthCheckResponse
        {
            Status = HealthCheckResponse.Types.ServingStatus.Serving,
            Message = "Planner service is healthy"
        });
    }
}