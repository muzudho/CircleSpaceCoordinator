namespace CircleSpaceCoordinator.EditorClient;

using CircleSpaceCoordinator.Application.Plans;
using CircleSpaceCoordinator.Application.Queries;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Engine.Contracts.V1;
using CircleSpaceCoordinator.Engine.Model;
using CircleSpaceCoordinator.OptimizationEngine;
using Grpc.Core;

public sealed class RemoteWorkspace : IEditorWorkspace, IDisposable
{
    private readonly EditorConnection connection;
    private WorkspaceState state;
    public WorkspaceView View { get; private set; }
    public string Id => state.WorkspaceId;
    public long Revision => state.Revision;
    public RemoteWorkspace(EditorConnection connection, WorkspaceState state)
    {
        this.connection = connection;
        this.state = state;
        View = WireJson.Read<WorkspaceView>(state.ViewJson);
    }
    public CircleSpaceProject Project => View.Project;
    public Plan SelectedPlan => View.SelectedPlan;
    public string SelectedPlanId => SelectedPlan.Id;
    public string SelectedDeskLayoutId => View.SelectedDeskLayoutId;
    public bool HasSelectedCircleLayout => View.HasSelectedCircleLayout;
    public bool CanRemoveSelectedDeskLayout => View.CanRemoveSelectedDeskLayout;
    public bool CanUndo => state.CanUndo;
    public bool CanRedo => state.CanRedo;
    public IReadOnlyList<RankedPlan> RankPlans() => View.Ranking;
    public PlanSnapshot GetSelectedPlanSnapshot() => View.Snapshot;
    public void Refresh() => Accept(EditorConnection.Invoke(() => connection.Client.Get(new WorkspaceRequest { WorkspaceId = Id }, EditorConnection.Deadline())));
    public void Execute(EditorOperation operation, bool selectedPlanEdit = true) => Accept(EditorConnection.Invoke(() => connection.Client.Execute(
        new OperationRequest { WorkspaceId = Id, ExpectedRevision = Revision, SelectedPlanEdit = selectedPlanEdit, OperationJson = WireJson.Write(operation) }, EditorConnection.Deadline())));
    public void SelectPlan(string id) => Select(new SelectionRequest { PlanId = id });
    public void SelectDeskLayout(string id) => Select(new SelectionRequest { DeskLayoutId = id });
    private void Select(SelectionRequest request)
    {
        request.WorkspaceId = Id; request.ExpectedRevision = Revision;
        Accept(EditorConnection.Invoke(() => connection.Client.Select(request, EditorConnection.Deadline())));
    }
    public void LoadProject(CircleSpaceProject project) => Accept(EditorConnection.Invoke(() => connection.Client.Load(
        new LoadRequest { WorkspaceId = Id, ExpectedRevision = Revision, ProjectJson = connection.Encode(project) }, EditorConnection.Deadline())));
    public CircleSpaceProject Undo() => Edit(new EditRequest { Undo = new Empty() });
    public CircleSpaceProject Redo() => Edit(new EditRequest { Redo = new Empty() });
    private CircleSpaceProject Edit(EditRequest request)
    {
        request.WorkspaceId = Id; request.ExpectedRevision = Revision;
        Accept(EditorConnection.Invoke(() => connection.Client.Edit(request, EditorConnection.Deadline())));
        return Project;
    }
    public void Accept(WorkspaceState next)
    {
        if (next.WorkspaceId != Id || next.Revision < Revision) throw new InvalidOperationException("Unexpected workspace state.");
        var view = WireJson.Read<WorkspaceView>(next.ViewJson);
        state = next; View = view;
    }

    public async Task<JobEvent> OptimizeAsync(int minutes, IProgress<CirclePlacementOptimizationProgress> progress, CancellationToken stop)
    {
        var plan = SelectedPlan;
        var handle = await connection.Client.StartJobAsync(new EditorOptimizeRequest
        {
            WorkspaceId = Id, ExpectedRevision = Revision, SourcePlanId = plan.Id,
            NewPlanId = $"{plan.Id}-optimized-{Guid.NewGuid():N}", NewPlanName = $"{plan.Name} 自動最適化",
            OnlyIfImproved = true,
            Options = new OptimizationOptions { MaximumIterations = int.MaxValue, TimeLimitMs = checked(minutes * 60000) },
        }, EditorConnection.Deadline()).ConfigureAwait(false);
        using var watch = connection.Client.WatchJob(handle, deadline: DateTime.UtcNow.AddMinutes(minutes + 1));
        var stopSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = stop.Register(() => stopSignal.TrySetResult(true));
        var stopping = SendStop();
        try
        {
            await foreach (var item in watch.ResponseStream.ReadAllAsync().ConfigureAwait(false))
            {
                if (item.ProgressJson.Length > 0) progress.Report(WireJson.Read<CirclePlacementOptimizationProgress>(item.ProgressJson));
                if (!item.Completed) continue;
                if (item.ErrorCode.Length > 0) throw new InvalidOperationException($"{item.ErrorCode}: {item.ErrorMessage}");
                return item;
            }
            throw new InvalidOperationException("Optimization ended without a result.");
        }
        finally
        {
            stopSignal.TrySetResult(false);
            await stopping.ConfigureAwait(false);
            try { await connection.Client.ReleaseJobAsync(handle, EditorConnection.Deadline()).ConfigureAwait(false); }
            catch (RpcException) { /* Active or disconnected jobs can be reattached through their ID. */ }
        }
        async Task SendStop()
        {
            if (await stopSignal.Task.ConfigureAwait(false))
                await connection.Client.StopJobAsync(handle, EditorConnection.Deadline()).ConfigureAwait(false);
        }
    }
    public void Dispose()
    {
        try { connection.Client.Close(new WorkspaceRequest { WorkspaceId = Id }, EditorConnection.Deadline()); }
        catch (Grpc.Core.RpcException) { /* Persisted state remains available after a disconnected shutdown. */ }
    }
}
