//using ProjectName.Core.Interfaces;
//using ProjectName.PlanerService.Protos;
//using System.Numerics;

//namespace ProjectName.PlanerService.Services;

///// <summary>
///// gRPC service implementing the Planner interface
///// This is the infrastructure node that handles planning logic
///// </summary>
//public class PlannerGrpcService : Planner.PlannerBase
//{
//    private readonly IPlanner _planner;
//    private readonly ILogger<PlannerGrpcService> _logger;

//    public PlannerGrpcService(IPlanner planner, ILogger<PlannerGrpcService> logger)
//    {
//        _planner = planner;
//        _logger = logger;
//    }

//    public override async Task<PlanResponse> CreatePlan(PlanRequest request, ServerCallContext context)
//    {
//        _logger.LogInformation("CreatePlan called with intent: {Intent}", request.Intent);

//        try
//        {
//            var plan = await _planner.CreatePlanAsync(request.Intent, context.CancellationToken);

//            var response = new PlanResponse
//            {
//                Goal = plan.Goal,
//                Analysis = plan.Analysis,
//                Success = true
//            };

//            foreach (var step in plan.Steps)
//            {
//                response.Steps.Add(new PlanStepMessage
//                {
//                    Order = step.Order,
//                    Action = step.Action,
//                    Tool = step.Tool,
//                    ArgumentsJson = step.ArgumentsJson
//                });
//            }

//            return response;
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Error creating plan");
//            return new PlanResponse
//            {
//                Success = false,
//                ErrorMessage = ex.Message
//            };
//        }
//    }
//}