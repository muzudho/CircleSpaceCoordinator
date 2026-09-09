namespace CircleSpaceCoordinator.EditorEngine;

using System.Text.Json;
using CircleSpaceCoordinator.Application.Plans;
using CircleSpaceCoordinator.Application.Workspace;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Core.Validation;
using CircleSpaceCoordinator.Engine.Contracts.V1;
using CircleSpaceCoordinator.Infrastructure.Json;
using Grpc.Core;

public sealed class EditorService(Thinking.ThinkingClient thinking) : Editor.EditorBase
{
    // Every workspace operation is atomic; expensive thinking runs outside this lock.
    private readonly object gate = new();
    private readonly Dictionary<string, Session> sessions = new();

    private sealed class Session(ProjectWorkspace workspace)
    {
        public ProjectWorkspace Workspace { get; } = workspace;
        public long Revision { get; set; } = 1;
    }

    public override Task<WorkspaceState> Open(OpenRequest request, ServerCallContext context) => Run(() =>
    {
        var session = new Session(new ProjectWorkspace(ProjectJsonSerializer.Load(request.ProjectJson)));
        var id = Guid.NewGuid().ToString("N");
        lock (gate)
        {
            var result = Snapshot(id, session);
            sessions.Add(id, session);
            return result;
        }
    });

    public override Task<WorkspaceState> Get(WorkspaceRequest request, ServerCallContext context) => Run(() =>
    {
        lock (gate) return Snapshot(request.WorkspaceId, Find(request.WorkspaceId));
    });

    public override Task<WorkspaceState> Edit(EditRequest request, ServerCallContext context) => Run(() =>
    {
        lock (gate)
        {
            var session = Find(request.WorkspaceId);
            CheckRevision(session, request.ExpectedRevision);
            switch (request.CommandCase)
            {
                case EditRequest.CommandOneofCase.RenamePlan:
                    session.Workspace.ApplyProjectEdit(project => LayoutProjection.CommitLegacyPlanEdits(
                        PlanCatalogService.RenamePlan(project, request.RenamePlan.PlanId, request.RenamePlan.Name),
                        request.RenamePlan.PlanId));
                    break;
                case EditRequest.CommandOneofCase.Undo: session.Workspace.Undo(); break;
                case EditRequest.CommandOneofCase.Redo: session.Workspace.Redo(); break;
                default: throw new ArgumentException("An edit command is required.");
            }
            session.Revision++;
            return Snapshot(request.WorkspaceId, session);
        }
    });

    public override async Task<WorkspaceState> Optimize(EditorOptimizeRequest request, ServerCallContext context)
    {
        var input = await Run(() =>
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(request.NewPlanId);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.NewPlanName);
            lock (gate)
            {
                var session = Find(request.WorkspaceId);
                CheckRevision(session, request.ExpectedRevision);
                if (!session.Workspace.Project.Plans.Any(plan => plan.Id == request.SourcePlanId))
                    throw new KeyNotFoundException("Source plan does not exist.");
                if (session.Workspace.Project.Plans.Any(plan => plan.Id == request.NewPlanId))
                    throw new ArgumentException("New plan ID already exists.");
                return new ThinkingRequest
                {
                    ProjectJson = ProjectJsonSerializer.Save(session.Workspace.Project),
                    PlanId = request.SourcePlanId,
                    Options = request.Options,
                };
            }
        });
        var result = await thinking.OptimizeAsync(input, deadline: context.Deadline,
            cancellationToken: context.CancellationToken);
        return await Run(() =>
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var project = ProjectJsonSerializer.Load(result.ProjectJson);
            var bestPlan = project.Plans.Single(plan => plan.Id == request.SourcePlanId);
            lock (gate)
            {
                var session = Find(request.WorkspaceId);
                CheckRevision(session, request.ExpectedRevision);
                session.Workspace.ApplyProjectEdit(current => PlanCatalogService.AddOptimizedPlan(
                    current, bestPlan, request.NewPlanId, request.NewPlanName));
                session.Revision++;
                return Snapshot(request.WorkspaceId, session);
            }
        });
    }

    public override Task<Empty> Close(WorkspaceRequest request, ServerCallContext context) => Run(() =>
    {
        lock (gate)
        {
            Find(request.WorkspaceId);
            sessions.Remove(request.WorkspaceId);
            return new Empty();
        }
    });

    private Session Find(string id) => sessions.TryGetValue(id, out var session) ? session
        : throw new KeyNotFoundException("Workspace does not exist or was closed.");

    private static void CheckRevision(Session session, long expected)
    {
        if (session.Revision != expected)
            throw new RpcException(new Status(StatusCode.Aborted, "Workspace changed. Fetch its current revision and retry."));
    }

    private static WorkspaceState Snapshot(string id, Session session) => new()
    {
        WorkspaceId = id,
        Revision = session.Revision,
        ProjectJson = ProjectJsonSerializer.Save(session.Workspace.Project),
        CanUndo = session.Workspace.CanUndo,
        CanRedo = session.Workspace.CanRedo,
    };

    private static Task<T> Run<T>(Func<T> action)
    {
        try { return Task.FromResult(action()); }
        catch (KeyNotFoundException) { throw new RpcException(new Status(StatusCode.NotFound, "Workspace or plan does not exist.")); }
        catch (Exception exception) when (exception is ArgumentException or JsonException or ProjectValidationException)
        { throw new RpcException(new Status(StatusCode.InvalidArgument, exception.Message)); }
        catch (InvalidOperationException exception)
        { throw new RpcException(new Status(StatusCode.FailedPrecondition, exception.Message)); }
    }
}
