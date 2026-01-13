using Grpc.Core;
using ProjectName.Core.Interfaces;
using System.Diagnostics;

namespace ProjectName.PlanerService.Services;

/// <summary>
/// gRPC service implementing the Planner interface
/// Hosts the PlannerAgent and exposes it via gRPC
/// </summary>
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

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("CreatePlan called - Intent: {Intent}", request.Intent);
        }

        try
        {
            // Validate request
            if (string.IsNullOrWhiteSpace(request.Intent))
            {
                return new PlanResponse
                {
                    Success = false,
                    ErrorMessage = "Intent cannot be empty"
                };
            }

            // Call the PlannerAgent
            var plan = await _planner.CreatePlanAsync(
                request.Intent,
                context.CancellationToken);

            sw.Stop();

            // Build successful response
            var response = new PlanResponse
            {
                Success = true,
                Goal = plan.Goal,
                Analysis = plan.Analysis
            };

            // Convert PlanSteps to gRPC messages
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

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Plan created successfully - Steps: {StepCount}, Duration: {Duration}ms",
                    response.Steps.Count,
                    sw.ElapsedMilliseconds);
            }

            return response;
        }
        catch (RpcException)
        {
            // Re-throw gRPC exceptions
            throw;
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, "Error creating plan for intent: {Intent}", request.Intent);
            }

            return new PlanResponse
            {
                Success = false,
                ErrorMessage = $"Internal error: {ex.Message}"
            };
        }
    }

    public override Task<HealthCheckResponse> HealthCheck(
        HealthCheckRequest request,
        ServerCallContext context)
    {
        _logger.LogDebug("Health check requested");

        try
        {
            return Task.FromResult(new HealthCheckResponse
            {
                Status = HealthCheckResponse.Types.ServingStatus.Serving,
                Message = "Planner service is healthy"
            });
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, "Health check failed");
            }

            return Task.FromResult(new HealthCheckResponse
            {
                Status = HealthCheckResponse.Types.ServingStatus.NotServing,
                Message = $"Service unhealthy: {ex.Message}"
            });
        }
    }
}