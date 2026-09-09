namespace CircleSpaceCoordinator.ThinkingEngine;

using System.Text.Json;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Core.Validation;
using CircleSpaceCoordinator.Engine.Contracts.V1;
using CircleSpaceCoordinator.Infrastructure.Json;
using CircleSpaceCoordinator.OptimizationEngine;
using Grpc.Core;

public sealed class ThinkingService : Thinking.ThinkingBase
{
    public override async Task<ThinkingResult> Optimize(ThinkingRequest request, ServerCallContext context)
    {
        try
        {
            if (request.Options is not { MaximumIterations: > 0, TimeLimitMs: > 0 and <= 600000 })
                throw new ArgumentException("Positive iterations and time_limit_ms in 1..600000 are required.");
            var project = ProjectJsonSerializer.Load(request.ProjectJson);
            if (!project.Plans.Any(plan => plan.Id == request.PlanId))
                throw new RpcException(new Status(StatusCode.NotFound, "Plan does not exist."));
            var options = new CirclePlacementOptimizationOptions(request.Options.MaximumIterations,
                request.Options.HasRandomSeed ? request.Options.RandomSeed : null)
            {
                TimeLimit = TimeSpan.FromMilliseconds(request.Options.TimeLimitMs),
            };
            var result = await Task.Run(() => new CirclePlacementOptimizationEngine().Optimize(
                project, request.PlanId, options, cancellationToken: context.CancellationToken), context.CancellationToken);
            context.CancellationToken.ThrowIfCancellationRequested();
            var optimized = LayoutProjection.CommitLegacyPlanEdits(project with
            {
                Plans = project.Plans.Select(plan => plan.Id == request.PlanId ? result.BestPlan : plan).ToArray(),
            }, request.PlanId);
            return new ThinkingResult
            {
                ProjectJson = ProjectJsonSerializer.Save(optimized),
                IterationCount = result.IterationCount,
                GeneralAttendeeScore = result.BestScore.GeneralAttendeeScore,
                CircleParticipantScore = result.BestScore.CircleParticipantScore,
            };
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException or ProjectValidationException)
        { throw new RpcException(new Status(StatusCode.InvalidArgument, exception.Message)); }
        catch (InvalidOperationException exception)
        { throw new RpcException(new Status(StatusCode.FailedPrecondition, exception.Message)); }
    }
}
