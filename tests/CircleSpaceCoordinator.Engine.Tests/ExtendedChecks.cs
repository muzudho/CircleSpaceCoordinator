using CircleSpaceCoordinator.Application.Workspace;
using CircleSpaceCoordinator.Core.Evaluation;
using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Desktop.Core.Interaction;
using CircleSpaceCoordinator.EditorClient;
using CircleSpaceCoordinator.EditorEngine;
using CircleSpaceCoordinator.Engine.Contracts.V1;
using CircleSpaceCoordinator.Engine.Model;
using CircleSpaceCoordinator.OptimizationEngine;
using Grpc.Core;
using StationeryUI.Canvas;

internal static class ExtendedChecks
{
    public static async Task Run(string address, string json)
    {
        using var connection = new EditorConnection(address);
        using var workspace = connection.Open(connection.Encode(new CircleSpaceProject("1.0", "remote", "Remote editing",
            new Venue("v", "Venue", 10, 10, new HashSet<GridPosition>()),
            [new DeskType("desk", "Desk", [new(0, 0), new(1, 0)])],
            [new Participant("a", "A", 1, new Dictionary<string, double>()), new Participant("b", "B", 1, new Dictionary<string, double>())],
            new EvaluationConfiguration([], []), [new Plan("p", "Plan", [], [])])));
        var commands = new EditorCommandController(workspace);
        var participants = new ParticipantPlacementController(workspace);
        Check(commands.AddDeskAt(new(0, 0)).Applied, "remote add desk");
        Check(commands.AddDeskAt(new(3, 0)).Applied, "remote add second desk");
        Check(participants.PlaceParticipantAt("a", new(0, 0)).Applied, "remote assign");
        Check(participants.PlaceParticipantAt("b", new(3, 0)).Applied, "remote assign second");
        Check(participants.SwapParticipants("a", "b").Applied, "remote swap");
        var before = workspace.Project;
        var desk = workspace.SelectedPlan.DeskPlacements[0];
        workspace.Execute(new PlanDeskEditorMoveDesk(workspace.SelectedPlanId, desk.Id, new(0, 2)));
        Check(workspace.SelectedPlan.DeskPlacements[0].Anchor == new GridPosition(0, 2), "remote move");
        workspace.Undo();
        Check(WireJson.Write(workspace.Project) == WireJson.Write(before), "remote history");
        Check(!commands.AddDeskAt(new(0, 0)).Applied, "validation trailers reach GUI controller");
        workspace.Execute(new LayoutCatalogServiceCreateDeskLayout("unused", "Unused"), false);
        workspace.SelectDeskLayout("unused");
        Check(!workspace.HasSelectedCircleLayout, "unused desk selection");
        Check(commands.AddDeskAt(new(1, 1)).Applied, "unused desk edit");
        Check(workspace.Project.DeskLayouts.Single(d => d.Id == "unused").DeskPlacements.Count == 1, "unused desk remains canonical");
        workspace.Execute(new LayoutCatalogServiceCreateCircleLayout("new-circle", "New circle", "unused"), false);
        workspace.SelectPlan("new-circle");
        Check(commands.DuplicateSelectedPlan("Duplicate").Applied, "remote duplicate");
        workspace.Execute(new PlanCatalogServiceCopyDeskLayout("p", workspace.SelectedPlanId));
        Check(workspace.SelectedPlan.DeskPlacements.Count == 2, "copy commits the destination desk layout");
        workspace.Execute(new PlanDeskEditorMoveDesk("p", desk.Id, new(0, 3)), selectedPlanEdit: false);
        Check(workspace.Project.Plans.Single(plan => plan.Id == "p").DeskPlacements[0].Anchor == new GridPosition(0, 3), "explicit target edits persist without changing selection");
        workspace.Execute(new SetGenreStyles([new("test", "red", "blue", "solid")]), false);
        Check(workspace.Project.GenreStyles.Count == 1, "remote styles");
        workspace.SelectPlan("p");
        workspace.Execute(new ParticipantCatalogServiceReplaceParticipants(workspace.Project.Participants.Select(item =>
            new CircleSpaceCoordinator.Application.Participants.ParticipantImportRow(item.CircleId, item.DisplayName, item.RequiredCellCount)
            { SourceValues = new Dictionary<string, string> { ["Books"] = "1" } }).ToArray()), false);
        workspace.Execute(new UpsertChannel("books", "Books", "Books"), false);
        var scoringCell = workspace.SelectedPlan.Assignments[0].ScoringPosition;
        workspace.Execute(new SetChannelWeights("p", "books", [scoringCell], 0.75));
        Check(workspace.GetSelectedPlanSnapshot().Evaluation.TotalScore == 0.75, "remote channel score");
        workspace.Undo();
        Check(workspace.GetSelectedPlanSnapshot().Evaluation.TotalScore == 0, "remote channel undo");
        workspace.Redo();
        Check(workspace.GetSelectedPlanSnapshot().Evaluation.TotalScore == 0.75, "remote channel redo");
        var channelProject = connection.Decode(connection.Encode(workspace.Project));
        Check(channelProject.Evaluation.Features[0].SourceColumn == "Books" && channelProject.Participants[0].SourceValues["Books"] == "1",
            "remote channel persistence");
        workspace.LoadProject(connection.Decode(json));
        Check(!workspace.CanUndo, "load resets history");

        var started = await connection.Client.StartJobAsync(new EditorOptimizeRequest
        {
            WorkspaceId = workspace.Id, ExpectedRevision = workspace.Revision, SourcePlanId = workspace.SelectedPlanId,
            NewPlanId = "stopped", NewPlanName = "Stopped result",
            Options = new OptimizationOptions { MaximumIterations = int.MaxValue, TimeLimitMs = 60000, RandomSeed = 42 },
        });
        using var watch = connection.Client.WatchJob(started, deadline: DateTime.UtcNow.AddSeconds(15));
        await connection.Client.StopJobAsync(started, deadline: DateTime.UtcNow.AddSeconds(5));
        JobEvent? completed = null;
        await foreach (var item in watch.ResponseStream.ReadAllAsync()) if (item.Completed) completed = item;
        Check(completed is { ErrorCode.Length: 0, State: not null }, "stop returns a result");
        var result = WireJson.Read<CirclePlacementOptimizationResult>(completed!.ResultJson);
        Check(result.WasStopped, "stop preserves best-so-far result");
        workspace.Accept(completed.State!);
        Check(workspace.SelectedPlanId == "stopped", "job commits and selects result");
        await connection.Client.ReleaseJobAsync(started);
        await CheckPersistence(json);
        Console.WriteLine("PASS: remote GUI controllers, snapshots, stopped jobs and restart recovery.");
    }

    private static async Task CheckPersistence(string json)
    {
        var directory = Path.Combine(Path.GetTempPath(), "circle-space-session-test-" + Guid.NewGuid().ToString("N"));
        string id;
        long revision;
        var arguments = new[] { "--port", "0", "--state-directory", directory, "--Logging:LogLevel:Default", "Warning" };
        await using (var host = EditorEngineHost.Build(arguments))
        {
            await host.StartAsync();
            using var connection = new EditorConnection(host.Urls.Single());
            var workspace = connection.Open(json);
            workspace.Execute(new PlanCatalogServiceRenamePlan(workspace.SelectedPlanId, "Persisted"));
            id = workspace.Id; revision = workspace.Revision;
            await host.StopAsync(); // Simulate stopping the engine without Close.
        }
        await using (var host = EditorEngineHost.Build(arguments))
        {
            await host.StartAsync();
            using var connection = new EditorConnection(host.Urls.Single());
            var state = await connection.Client.GetAsync(new WorkspaceRequest { WorkspaceId = id });
            using var workspace = new RemoteWorkspace(connection, state);
            Check(workspace.Revision == revision && workspace.SelectedPlan.Name == "Persisted", "restart restores state");
            Check(workspace.CanUndo, "restart restores undo stack");
            workspace.Undo();
            Check(workspace.SelectedPlan.Name != "Persisted", "undo after restart");
            var beforeFailure = WireJson.Write(workspace.Project);
            var beforeRevision = workspace.Revision;
            using (var blockedSave = new FileStream(Path.Combine(directory, id + ".json.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                try
                {
                    workspace.Execute(new PlanCatalogServiceRenamePlan(workspace.SelectedPlanId, "Must not commit"));
                    throw new Exception("Expected persistence failure.");
                }
                catch (InvalidOperationException exception) when (exception.InnerException is RpcException { StatusCode: StatusCode.Unavailable }) { }
            }
            workspace.Refresh();
            Check(workspace.Revision == beforeRevision && WireJson.Write(workspace.Project) == beforeFailure, "failed save rolls back editing state");
        }
    }
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL: " + name);
    }
}
