namespace CircleSpaceCoordinator.ThinkingEngine;

using System.Text.Json;
using CircleSpaceCoordinator.Core.Validation;
using CircleSpaceCoordinator.Engine.Contracts.V1;
using CircleSpaceCoordinator.Infrastructure.Json;
using Grpc.Core;

public sealed partial class ThinkingService
{
    public override async Task<ThinkingResult> FillVacantSeats(ThinkingRequest request, ServerCallContext context)
    {
        if (!await slots.WaitAsync(0, context.CancellationToken))
            throw new RpcException(new Status(StatusCode.ResourceExhausted, "Thinking engine is busy."));
        try
        {
            var project = ProjectJsonSerializer.Load(request.ProjectJson);
            if (!project.Plans.Any(p => p.Id == request.PlanId))
                throw new RpcException(new Status(StatusCode.NotFound, "Plan does not exist."));
            var result = await Task.Run(() => VacantSeatFiller.Fill(project, request.PlanId, context.CancellationToken), context.CancellationToken);
            context.CancellationToken.ThrowIfCancellationRequested();
            return new ThinkingResult { ProjectJson = ProjectJsonSerializer.Save(result) };
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException or ProjectValidationException)
        { throw new RpcException(new Status(StatusCode.InvalidArgument, exception.Message)); }
        catch (InvalidOperationException exception)
        { throw new RpcException(new Status(StatusCode.FailedPrecondition, exception.Message)); }
        finally { slots.Release(); }
    }
}
