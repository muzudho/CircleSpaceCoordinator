namespace CircleSpaceCoordinator.EditorEngine;

using System.Text.Json;
using CircleSpaceCoordinator.Application.Plans;
using CircleSpaceCoordinator.Application.Workspace;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Core.Validation;
using CircleSpaceCoordinator.Engine.Contracts.V1;
using CircleSpaceCoordinator.Infrastructure.Json;
using Grpc.Core;
using CircleSpaceCoordinator.Engine.Model;
using CircleSpaceCoordinator.Core.Evaluation;

public sealed partial class EditorService(Thinking.ThinkingClient thinking, SessionRepository repository) : Editor.EditorBase
{
    public override Task<EngineInfo> Describe(Empty request, ServerCallContext context) =>
        Task.FromResult(new EngineInfo { ApiMajor = 1, Engine = "editor" });

    public override Task<DocumentReply> ConvertDocument(DocumentRequest request, ServerCallContext context) => Run(() =>
    {
        var project = request.InputCase switch
        {
            DocumentRequest.InputOneofCase.ProjectJson => ProjectJsonSerializer.Load(request.ProjectJson),
            DocumentRequest.InputOneofCase.ModelJson => WireJson.Read<CircleSpaceProject>(request.ModelJson),
            _ => throw new ArgumentException("A document is required."),
        };
        return new DocumentReply { ProjectJson = ProjectJsonSerializer.Save(project, ProjectEvaluator.Evaluate(project)), ModelJson = WireJson.Write(project) };
    });

    public override Task<WorkspaceState> Execute(OperationRequest request, ServerCallContext context) => Run(() =>
    {
        var operation = WireJson.Read<EditorOperation>(request.OperationJson);
        lock (gate)
        {
            var session = Clone(Find(request.WorkspaceId));
            CheckRevision(session, request.ExpectedRevision);
            session.Workspace.Execute(operation, request.SelectedPlanEdit);
            session.Revision++;
            return Snapshot(request.WorkspaceId, session);
        }
    });

    public override Task<WorkspaceState> Select(SelectionRequest request, ServerCallContext context) => Run(() =>
    {
        lock (gate)
        {
            var session = Clone(Find(request.WorkspaceId));
            CheckRevision(session, request.ExpectedRevision);
            if (request.TargetCase == SelectionRequest.TargetOneofCase.PlanId) session.Workspace.SelectPlan(request.PlanId);
            else if (request.TargetCase == SelectionRequest.TargetOneofCase.DeskLayoutId) session.Workspace.SelectDeskLayout(request.DeskLayoutId);
            else throw new ArgumentException("A selection is required.");
            session.Revision++;
            return Snapshot(request.WorkspaceId, session);
        }
    });

    public override Task<WorkspaceState> Load(LoadRequest request, ServerCallContext context) => Run(() =>
    {
        var project = ProjectJsonSerializer.Load(request.ProjectJson);
        lock (gate)
        {
            var session = Clone(Find(request.WorkspaceId));
            CheckRevision(session, request.ExpectedRevision);
            session.Workspace.LoadProject(project);
            session.Revision++;
            return Snapshot(request.WorkspaceId, session);
        }
    });
    // Every workspace operation is atomic; expensive thinking runs outside this lock.
    private readonly object gate = new();
    private readonly Dictionary<string, Session> sessions = new();

    private sealed class Session(ProjectWorkspace workspace)
    {
        public ProjectWorkspace Workspace { get; } = workspace;
        public long Revision { get; set; } = 1;
        public long PersistedRevision { get; set; }
        public DateTime LastAccess { get; set; } = DateTime.UtcNow;
    }

    public override Task<WorkspaceState> Open(OpenRequest request, ServerCallContext context) => Run(() =>
    {
        var session = new Session(new ProjectWorkspace(ProjectJsonSerializer.Load(request.ProjectJson)));
        var id = Guid.NewGuid().ToString("N");
        lock (gate)
        {
            MakeRoom();
            var result = Snapshot(id, session);

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
            var session = Clone(Find(request.WorkspaceId));
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
                var session = Clone(Find(request.WorkspaceId));
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
                var session = Clone(Find(request.WorkspaceId));
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
            if (jobs.Values.Any(job => job.Request.WorkspaceId == request.WorkspaceId && !job.Latest.Completed))
                throw new InvalidOperationException("Stop and finish optimization before closing the workspace.");
            repository.Delete(request.WorkspaceId);
            sessions.Remove(request.WorkspaceId);
            return new Empty();
        }
    });

    private Session Find(string id)
    {
        if (sessions.TryGetValue(id, out var session))
        {
            if (repository.Enabled)
            {
                var disk = repository.Load(id);
                if (disk is null) { sessions.Remove(id); throw new KeyNotFoundException("Workspace was closed."); }
                if (disk.Revision != session.Revision)
                    sessions[id] = session = new Session(ProjectWorkspace.Restore(disk.Workspace))
                        { Revision = disk.Revision, PersistedRevision = disk.Revision };
            }
            session.LastAccess = DateTime.UtcNow;
            return session;
        }
        var saved = repository.Load(id) ?? throw new KeyNotFoundException("Workspace does not exist or was closed.");
        MakeRoom();
        session = new Session(ProjectWorkspace.Restore(saved.Workspace)) { Revision = saved.Revision, PersistedRevision = saved.Revision };
        sessions.Add(id, session);
        return session;
    }
    private void MakeRoom()
    {
        foreach (var old in sessions.Where(pair => pair.Value.LastAccess < DateTime.UtcNow.AddMinutes(-30)
            && !jobs.Values.Any(job => job.Request.WorkspaceId == pair.Key && !job.Latest.Completed)).Select(pair => pair.Key).ToArray())
            sessions.Remove(old);
        if (sessions.Count >= 128) throw new RpcException(new Status(StatusCode.ResourceExhausted, "Close unused workspaces before opening another."));
    }
    private static Session Clone(Session session) => new(ProjectWorkspace.Restore(session.Workspace.Capture()))
        { Revision = session.Revision, PersistedRevision = session.PersistedRevision };


    private static void CheckRevision(Session session, long expected)
    {
        if (session.Revision != expected)
            throw new RpcException(new Status(StatusCode.Aborted, "Workspace changed. Fetch its current revision and retry."));
    }

    private WorkspaceState Snapshot(string id, Session session)
    {
        var snapshot = new WorkspaceState
        {
        WorkspaceId = id,
        Revision = session.Revision,
        ProjectJson = ProjectJsonSerializer.Save(session.Workspace.Project),
        CanUndo = session.Workspace.CanUndo,
        CanRedo = session.Workspace.CanRedo,
        ViewJson = WireJson.Write(BuildView(session.Workspace)),
        };
        if (snapshot.CalculateSize() > 32 * 1024 * 1024)
            throw new RpcException(new Status(StatusCode.ResourceExhausted, "Workspace snapshot exceeds the 32 MiB message limit."));
        repository.Save(id, new SavedSession(session.Revision, session.Workspace.Capture()), session.PersistedRevision);
        session.PersistedRevision = session.Revision;
        sessions[id] = session;
        return snapshot;
    }

    private static WorkspaceView BuildView(ProjectWorkspace workspace) => new(workspace.Project,
        workspace.SelectedPlan, workspace.SelectedDeskLayoutId, workspace.HasSelectedCircleLayout,
        workspace.CanRemoveSelectedDeskLayout, workspace.GetSelectedPlanSnapshot(), workspace.RankPlans(),
        VenueTopologyAnalyzer.Build(workspace.Project, workspace.SelectedPlan),
        VenueTopologyAnalyzer.GetAutomaticCellEdges(workspace.SelectedPlan, workspace.Project.DeskTypes.ToDictionary(item => item.Id)),
        GeneralAttendeeEvaluator.BuildCombinedPartners(workspace.Project, workspace.SelectedPlan));

    private static Task<T> Run<T>(Func<T> action)
    {
        try { return Task.FromResult(action()); }
        catch (KeyNotFoundException) { throw new RpcException(new Status(StatusCode.NotFound, "Workspace or plan does not exist.")); }
        catch (ProjectValidationException exception)
        { throw new RpcException(new Status(StatusCode.InvalidArgument, exception.Message), new Metadata { { "validation-json", WireJson.Write(exception.Issues) } }); }
        catch (Exception exception) when (exception is ArgumentException or JsonException)
        { throw new RpcException(new Status(StatusCode.InvalidArgument, exception.Message)); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { throw new RpcException(new Status(StatusCode.Unavailable, exception.Message)); }
        catch (InvalidOperationException exception)
        { throw new RpcException(new Status(StatusCode.FailedPrecondition, exception.Message)); }
    }
}
