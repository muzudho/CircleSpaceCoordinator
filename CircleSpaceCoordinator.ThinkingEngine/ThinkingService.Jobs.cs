namespace CircleSpaceCoordinator.ThinkingEngine;

using System.Collections.Concurrent;
using System.Threading.Channels;
using CircleSpaceCoordinator.Engine.Contracts.V1;
using CircleSpaceCoordinator.Engine.Model;
using CircleSpaceCoordinator.Infrastructure.Json;
using CircleSpaceCoordinator.OptimizationEngine;
using Grpc.Core;

public sealed partial class ThinkingService
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> running = new();
    private readonly SemaphoreSlim slots = new(1, 1);

    public override Task<EngineInfo> Describe(Empty request, ServerCallContext context) =>
        Task.FromResult(new EngineInfo { ApiMajor = 1, Engine = "thinking" });

    public override Task<Empty> Stop(JobHandle request, ServerCallContext context)
    {
        if (running.TryGetValue(request.JobId, out var cancellation))
        {
            try { cancellation.Cancel(); } catch (ObjectDisposedException) { }
        }
        return Task.FromResult(new Empty());
    }

    public override async Task Run(ThinkingJobRequest request, IServerStreamWriter<JobEvent> response, ServerCallContext context)
    {
        if (!Guid.TryParseExact(request.JobId, "N", out _) || request.Input?.Options is not
            { MaximumIterations: > 0, TimeLimitMs: > 0 and <= 7200000 })
            throw new RpcException(new Status(StatusCode.InvalidArgument, "A job ID and valid optimization options are required."));
        if (!await slots.WaitAsync(0, context.CancellationToken))
            throw new RpcException(new Status(StatusCode.ResourceExhausted, "Thinking engine is busy."));
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
        if (!running.TryAdd(request.JobId, stop))
        {
            slots.Release();
            throw new RpcException(new Status(StatusCode.AlreadyExists, "Job ID is already running."));
        }
        var events = Channel.CreateBounded<JobEvent>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });
        events.Writer.TryWrite(new JobEvent { JobId = request.JobId });
        var producer = Task.Run(() =>
        {
            try
            {
                var input = request.Input;
                var project = ProjectJsonSerializer.Load(input.ProjectJson);
                if (!project.Plans.Any(plan => plan.Id == input.PlanId))
                    throw new RpcException(new Status(StatusCode.NotFound, "Source plan does not exist."));
                var options = new CirclePlacementOptimizationOptions(input.Options.MaximumIterations,
                    input.Options.HasRandomSeed ? input.Options.RandomSeed : null)
                { TimeLimit = TimeSpan.FromMilliseconds(input.Options.TimeLimitMs) };
                var progress = new InlineProgress(value => events.Writer.TryWrite(new JobEvent
                { JobId = request.JobId, ProgressJson = WireJson.Write(value) }));
                var result = new CirclePlacementOptimizationEngine().Optimize(project, input.PlanId, options, progress, stop.Token);
                context.CancellationToken.ThrowIfCancellationRequested();
                events.Writer.TryWrite(new JobEvent { JobId = request.JobId, ResultJson = WireJson.Write(result), Completed = true });
                events.Writer.TryComplete();
            }
            catch (Exception exception) { events.Writer.TryComplete(exception); }
        });
        try
        {
            await foreach (var item in events.Reader.ReadAllAsync(context.CancellationToken)) await response.WriteAsync(item);
            await producer;
        }
        finally
        {
            stop.Cancel();
            await producer;
            running.TryRemove(request.JobId, out _);
            slots.Release();
        }
    }

    private sealed class InlineProgress(Action<CirclePlacementOptimizationProgress> report) : IProgress<CirclePlacementOptimizationProgress>
    {
        public void Report(CirclePlacementOptimizationProgress value) => report(value);
    }
}
