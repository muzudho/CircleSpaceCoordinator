namespace CircleSpaceCoordinator.EditorEngine;

using CircleSpaceCoordinator.Application.Plans;
using CircleSpaceCoordinator.Engine.Contracts.V1;
using CircleSpaceCoordinator.Engine.Model;
using CircleSpaceCoordinator.Infrastructure.Json;
using CircleSpaceCoordinator.OptimizationEngine;
using Grpc.Core;

public sealed partial class EditorService
{
    private readonly Dictionary<string, EditorJob> jobs = new();
    private sealed class EditorJob(EditorOptimizeRequest request)
    {
        public EditorOptimizeRequest Request { get; } = request;
        public JobEvent Latest { get; set; } = new();
        public long Sequence { get; set; }
        public bool StopRequested { get; set; }
        public DateTime LastAccess { get; set; } = DateTime.UtcNow;
    }

    public override Task<JobHandle> StartJob(EditorOptimizeRequest request, ServerCallContext context) => Run(() =>
    {
        if (request.Options is not { MaximumIterations: > 0, TimeLimitMs: > 0 and <= 7200000 })
            throw new ArgumentException("Positive iterations and time_limit_ms in 1..7200000 are required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(request.NewPlanId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.NewPlanName);
        lock (gate)
        {
            foreach (var old in jobs.Where(pair => pair.Value.Latest.Completed && pair.Value.LastAccess < DateTime.UtcNow.AddMinutes(-10)).Select(pair => pair.Key).ToArray())
                jobs.Remove(old);
            if (jobs.Count >= 128 || jobs.Values.Any(job => !job.Latest.Completed))
                throw new RpcException(new Status(StatusCode.ResourceExhausted, "An optimization is already running or the job limit was reached."));
            var session = Find(request.WorkspaceId);
            CheckRevision(session, request.ExpectedRevision);
            if (!session.Workspace.Project.Plans.Any(plan => plan.Id == request.SourcePlanId))
                throw new KeyNotFoundException("Source plan does not exist.");
            if (session.Workspace.Project.Plans.Any(plan => plan.Id == request.NewPlanId))
                throw new ArgumentException("New plan ID already exists.");
            var id = Guid.NewGuid().ToString("N");
            var job = new EditorJob(request.Clone()) { Latest = new JobEvent { JobId = id } };
            var input = new ThinkingJobRequest { JobId = id, Input = new ThinkingRequest
            { ProjectJson = ProjectJsonSerializer.Save(session.Workspace.Project), PlanId = request.SourcePlanId, Options = request.Options } };
            jobs.Add(id, job);
            _ = Task.Run(() => RunJob(id, job, input));
            return new JobHandle { JobId = id };
        }
    });

    private async Task RunJob(string id, EditorJob job, ThinkingJobRequest input)
    {
        try
        {
            using var stream = thinking.Run(input, deadline: DateTime.UtcNow.AddMilliseconds(input.Input.Options.TimeLimitMs + 30000));
            await foreach (var item in stream.ResponseStream.ReadAllAsync())
            {
                bool stop;
                lock (gate) stop = job.StopRequested;
                // Re-send until a first progress event confirms that the thinking job exists.
                if (stop && !item.Completed) await thinking.StopAsync(new JobHandle { JobId = id });
                lock (gate)
                {
                    if (item.Completed)
                    {
                        var result = WireJson.Read<CirclePlacementOptimizationResult>(item.ResultJson);
                        var session = Clone(Find(job.Request.WorkspaceId));
                        CheckRevision(session, job.Request.ExpectedRevision);
                        if (!job.Request.OnlyIfImproved || result.BestScore.CompareTo(result.InitialScore) > 0)
                        {
                            session.Workspace.ApplyProjectEdit(project => PlanCatalogService.AddOptimizedPlan(project,
                                result.BestPlan, job.Request.NewPlanId, job.Request.NewPlanName));
                            session.Workspace.SelectPlan(job.Request.NewPlanId);
                            session.Revision++;
                        }
                        item.State = Snapshot(job.Request.WorkspaceId, session);
                    }
                    job.Latest = item;
                    job.Sequence++;
                }
            }
            lock (gate)
                if (!job.Latest.Completed) throw new InvalidOperationException("Thinking stream ended without a result.");
        }
        catch (Exception exception)
        {
            lock (gate)
            {
                job.Latest = new JobEvent { JobId = id, Completed = true,
                    ErrorCode = exception is RpcException rpc ? rpc.StatusCode.ToString() : "FailedPrecondition",
                    ErrorMessage = exception is RpcException remote ? remote.Status.Detail : exception.Message };
                job.Sequence++;
            }
        }
    }

    public override async Task WatchJob(JobHandle request, IServerStreamWriter<JobEvent> response, ServerCallContext context)
    {
        long seen = -1;
        while (true)
        {
            JobEvent item;
            long sequence;
            lock (gate)
            {
                var job = FindJob(request.JobId);
                job.LastAccess = DateTime.UtcNow;
                item = job.Latest.Clone(); sequence = job.Sequence;
            }
            if (sequence != seen) { await response.WriteAsync(item); seen = sequence; }
            if (item.Completed) return;
            await Task.Delay(100, context.CancellationToken);
        }
    }

    public override async Task<Empty> StopJob(JobHandle request, ServerCallContext context)
    {
        lock (gate) FindJob(request.JobId).StopRequested = true;
        await thinking.StopAsync(request, deadline: DateTime.UtcNow.AddSeconds(5), cancellationToken: context.CancellationToken);
        return new Empty();
    }
    public override Task<Empty> ReleaseJob(JobHandle request, ServerCallContext context) => Run(() =>
    {
        lock (gate)
        {
            if (!FindJob(request.JobId).Latest.Completed) throw new InvalidOperationException("Stop and finish the job before releasing it.");
            jobs.Remove(request.JobId);
            return new Empty();
        }
    });
    private EditorJob FindJob(string id) => jobs.TryGetValue(id, out var job) ? job
        : throw new RpcException(new Status(StatusCode.NotFound, "Job does not exist or has expired."));
}
