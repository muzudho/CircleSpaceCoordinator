namespace CircleSpaceCoordinator.Desktop.Tests;

using CircleSpaceCoordinator.Desktop.Core;
using CircleSpaceCoordinator.Application.Workspace;
using CircleSpaceCoordinator.Application.Editing;
using CircleSpaceCoordinator.Application.Layouts;
using CircleSpaceCoordinator.Core.Evaluation;
using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Desktop.Core.Interaction;
using CircleSpaceCoordinator.Desktop.Core.Logging;
using CircleSpaceCoordinator.Desktop.Core.Persistence;
using CircleSpaceCoordinator.Infrastructure.Json;
using StationeryUI.Canvas;
using CircleSpaceCoordinator.OptimizationEngine;

internal static class Program
{
    private static async Task<int> Main()
    {
        await using var engine = CircleSpaceCoordinator.EditorEngine.EditorEngineHost.Build(["--port", "0", "--Logging:LogLevel:Default", "Warning"]);
        await engine.StartAsync();
        using var connection = new CircleSpaceCoordinator.EditorClient.EditorConnection(engine.Urls.Single());
        CircleSpaceCoordinator.EditorClient.EditorConnection.Current = connection;
        var tests = new (string Name, Action Run)[]
        {
            ("Catalog spaces place in all directions and preserve snapshots through history and JSON", CatalogSpacesRoundTrip),
            ("Space definitions persist globally and reject broken references and stale saves", SpaceDefinitionsPersist),
            ("Export requires an explicit decision and preserves it independently of editor selection", ExportPlanDecision),
            ("Imported table survives remote import, save, undo and redo with ordered duplicate headers", ParticipantTableImportRoundTrip),
            ("Table viewport reaches the last cell of 10000 by 100 without copying values", LargeParticipantTableViewport),
            ("Dragging previews then commits one desk move", DragPreviewThenCommit),
            ("An invalid desk drop leaves the project unchanged", InvalidDropIsRejected),
            ("Cancelling a drag leaves the project unchanged", CancelLeavesProjectUnchanged),
            ("Plan selection cycles in both directions", PlanSelectionCycles),
            ("A desk under a cell can be rotated and undone", DeskCanBeRotatedAndUndone),
            ("Rotating empty space does nothing", RotatingEmptySpaceDoesNothing),
            ("Operation logs serialize click coordinates without business data", OperationLogIsStructured),
            ("An unassigned participant can be selected and assigned", ParticipantCanBePlaced),
            ("A two-cell participant occupies one physical desk", TwoCellParticipantUsesDesk),
            ("An assigned participant can be removed from a cell", ParticipantCanBeRemoved),
            ("An assigned participant can be dragged to another desk", ParticipantCanBeReassignedByPlacementController),
            ("Two assigned participants can be swapped in one operation", ParticipantsCanBeSwapped),
            ("Two one-cell participants on the same desk can be swapped", SameDeskParticipantsCanBeSwapped),
            ("A combined pair swaps both cells with another desk", CombinedPairSwapsWholeDesk),
            ("Members of a combined pair can swap cells on their desk", CombinedPairMembersCanSwapCells),
            ("A two-cell participant swaps with both participants on another desk", TwoCellParticipantSwapsWholeDesk),
            ("Desktop startup uses the fictional example by default", StartupUsesFictionalExample),
            ("Desktop startup honors an explicit project path", StartupUsesExplicitPath),
            ("Application settings remember the last project beside the executable", SettingsRememberLastProject),
            ("Application settings remember separate import and export directories", SettingsRememberExcelDirectories),
            ("Application settings preserve the shared circle label display", SettingsPreserveCircleLabelDisplay),
            ("Circle labels show channel coefficients and preserve ID regex behavior", CircleLabelsShowChannelValues),
            ("Legacy application settings migrate the last project into the catalog", LegacySettingsMigrateProject),
            ("An empty project catalog recovers valid JSON files from its project directory", EmptyCatalogRecoversProjects),
            ("Application settings preserve project order and remove only registrations", SettingsManageProjectCatalog),
            ("Application settings preserve per-project working state", SettingsPreserveWorkingState),
            ("Event catalog creates a valid empty project", EventCatalogCreatesProject),
            ("Event catalog can mark a project confidential without an unmark operation", EventCatalogMarksProjectConfidential),
            ("Event catalog duplicates with a new event identity", EventCatalogDuplicatesProject),
            ("A desk can be added and removed through editor commands", DeskCanBeAddedAndRemovedByCommand),
            ("A desk can be added using the remembered orientation", DeskCanBeAddedWithOrientation),
            ("Editor commands can fill desks and expand the venue", CommandsFillDesksAndResizeVenue),
            ("A project is saved through a temporary file and can be reloaded", ProjectCanBeSavedAndReloaded),
            ("The selected plan can be duplicated as a revision", SelectedPlanCanBeDuplicated),
            ("Edited circles survive duplication and layout operations", EditedCirclesSurviveDuplication),
            ("Desk edits through a duplicate are shared and persisted", DuplicateDeskEditsAreShared),
            ("All desks need numbers before export, including empty desks", AllDesksNeedNumbersForExport),
            ("Optimized circle layouts keep their desk binding after selection and saving", OptimizedPlanKeepsDeskBinding),
            ("An unused desk can be edited and saved without creating a circle layout", UnusedDeskCanBeEditedAndSaved),
            ("Temporary positions survive undo save and duplication", TemporaryPositionsSurviveHistoryAndSave),
            ("Temporary and assigned circles can swap", TemporaryCirclesCanSwap),
            ("Ranges can be parked and returned from aisles", RangesCanUseAisles),
            ("Combined identities survive parking one member", CombinedIdentitySurvivesParking),
            ("Invalid temporary positions are rejected atomically", TemporaryCollisionsAreRejected),
            ("Offscreen indicators point to each edge and ignore visible tokens", OffscreenIndicatorsFollowViewport),
        };
        var failures = 0;
        foreach (var test in tests)
        {
            try
            {
                test.Run();
                Console.WriteLine($"PASS: {test.Name}");
            }
            catch (Exception exception)
            {
                failures++;
                Console.Error.WriteLine($"FAIL: {test.Name}");
                Console.Error.WriteLine(exception.Message);
            }
        }

        Console.WriteLine($"{tests.Length - failures}/{tests.Length} tests passed.");
        return failures == 0 ? 0 : 1;
    }

    private static void TemporaryPositionsSurviveHistoryAndSave()
    {
        var workspace = new ProjectWorkspace(CreateProject());
        var commands = new EditorCommandController(workspace);
        AssertEqual(true, commands.RotateDeskAt(new(0, 0), true).Applied);
        var controller = new ParticipantPlacementController(workspace);
        var source = workspace.SelectedPlan.Assignments.Single();
        AssertEqual(true, controller.ParkParticipantAt(source.ParticipantId, source.OccupiedCells.ToArray(),
            source.ScoringPosition, new(-2, 3)).Applied);
        AssertEqual(0, workspace.SelectedPlan.Assignments.Count);
        AssertEqual(true, workspace.SelectedPlan.TemporaryPlacements.Single().OccupiedCells.SetEquals([new(-2, 3), new(-2, 4)]));
        AssertEqual(true, commands.Undo());
        AssertEqual(1, workspace.SelectedPlan.Assignments.Count);
        AssertEqual(0, workspace.SelectedPlan.TemporaryPlacements.Count);
        AssertEqual(true, commands.Redo());
        AssertEqual(true, commands.DuplicateSelectedPlan("Parked copy").Applied);
        var restored = new ProjectWorkspace(ProjectJsonSerializer.Load(ProjectJsonSerializer.Save(workspace.Project)));
        foreach (var plan in restored.Project.Plans)
            AssertEqual(new GridPosition(-2, 3), plan.TemporaryPlacements.Single().ScoringPosition);
        try
        {
            CircleSeatExportBuilder.Build(restored.Project, restored.SelectedPlan);
            throw new InvalidOperationException("Export accepted a temporary circle.");
        }
        catch (InvalidOperationException exception) when (exception.Message.Contains("仮置き")) { }
        var restoredController = new ParticipantPlacementController(restored);
        AssertEqual(true, restoredController.PlaceParticipantAt(source.ParticipantId, new(0, 0)).Applied);
        AssertEqual(0, restored.SelectedPlan.TemporaryPlacements.Count);
        AssertEqual(1, restored.SelectedPlan.Assignments.Count);
        AssertEqual(1, restored.Project.Plans.Single(plan => plan.Id != restored.SelectedPlanId).TemporaryPlacements.Count);
    }

    private static void TemporaryCirclesCanSwap()
    {
        var workspace = new ProjectWorkspace(CreateAssignmentProject(1));
        var controller = new ParticipantPlacementController(workspace);
        AssertEqual(true, controller.PlaceParticipantAt("fictional-circle-002", new(2, 0)).Applied);
        AssertEqual(true, controller.ParkParticipantAt("fictional-circle-001", [new(0, 0)], new(0, 0), new(0, 2)).Applied);
        AssertEqual(true, controller.SwapParticipants("fictional-circle-001", "fictional-circle-002").Applied);
        AssertEqual("fictional-circle-001", workspace.SelectedPlan.Assignments.Single().ParticipantId);
        AssertEqual(new GridPosition(2, 0), workspace.SelectedPlan.Assignments.Single().ScoringPosition);
        AssertEqual("fictional-circle-002", workspace.SelectedPlan.TemporaryPlacements.Single().ParticipantId);
        AssertEqual(new GridPosition(0, 2), workspace.SelectedPlan.TemporaryPlacements.Single().ScoringPosition);
        workspace.Undo();
        AssertEqual("fictional-circle-001", workspace.SelectedPlan.TemporaryPlacements.Single().ParticipantId);
    }

    private static void RangesCanUseAisles()
    {
        var workspace = new ProjectWorkspace(CreateAssignmentProject(1));
        var controller = new ParticipantPlacementController(workspace);
        AssertEqual(true, controller.PlaceParticipantAt("fictional-circle-002", new(2, 0)).Applied);
        AssertEqual(true, controller.SwapCellRegions(new(0, 0), new(0, 2), 4, 1).Applied);
        AssertEqual(0, workspace.SelectedPlan.Assignments.Count);
        AssertEqual(2, workspace.SelectedPlan.TemporaryPlacements.Count);
        AssertEqual(true, controller.SwapCellRegions(new(0, 2), new(0, 0), 4, 1).Applied);
        AssertEqual(2, workspace.SelectedPlan.Assignments.Count);
        AssertEqual(0, workspace.SelectedPlan.TemporaryPlacements.Count);
    }

    private static void CombinedIdentitySurvivesParking()
    {
        var source = CreateAssignmentProject(1);
        var plan = source.Plans[0] with
        {
            Assignments =
            [
                new ParticipantAssignment("fictional-circle-001", new HashSet<GridPosition> { new(0, 0) }, new(0, 0)) { CombinedSpaceId = "pair" },
                new ParticipantAssignment("fictional-circle-002", new HashSet<GridPosition> { new(1, 0) }, new(1, 0)) { CombinedSpaceId = "pair" },
            ],
        };
        var workspace = new ProjectWorkspace(source with { Plans = [plan] });
        var controller = new ParticipantPlacementController(workspace);
        AssertEqual(true, controller.ParkParticipantAt("fictional-circle-001", [new(0, 0)], new(0, 0), new(0, 2)).Applied);
        AssertEqual("pair", workspace.SelectedPlan.TemporaryPlacements.Single().CombinedSpaceId);
        AssertEqual("pair", workspace.SelectedPlan.Assignments.Single().CombinedSpaceId);
        var restored = new ProjectWorkspace(ProjectJsonSerializer.Load(ProjectJsonSerializer.Save(workspace.Project)));
        AssertEqual(true, new ParticipantPlacementController(restored).PlaceParticipantAt("fictional-circle-001", new(0, 0)).Applied);
        AssertEqual(true, restored.SelectedPlan.Assignments.All(item => item.CombinedSpaceId == "pair"));
    }

    private static void TemporaryCollisionsAreRejected()
    {
        var workspace = new ProjectWorkspace(CreateAssignmentProject(2));
        var controller = new ParticipantPlacementController(workspace);
        AssertEqual(true, controller.ParkParticipantAt("fictional-circle-001", [new(0, 0)], new(0, 0), new(0, 2)).Applied);
        var before = workspace.Project;
        AssertEqual(false, controller.ParkParticipantAt("fictional-circle-002", [new(0, 0), new(1, 0)], new(0, 0), new(0, 2)).Applied);
        AssertEqual(true, ReferenceEquals(before, workspace.Project));
        AssertEqual(false, controller.ParkParticipantAt("fictional-circle-002", [new(0, 0), new(1, 0)], new(0, 0), new(3, 0)).Applied);
        AssertEqual(true, ReferenceEquals(before, workspace.Project));
    }

    private static void OffscreenIndicatorsFollowViewport()
    {
        var viewport = new ScreenRectangle(0, 100, 800, 500);
        AssertEqual<ScreenEdge?>(ScreenEdge.Left, OffscreenParticipantIndicator.FindEdge(new(-50, 200, 20, 20), viewport));
        AssertEqual<ScreenEdge?>(ScreenEdge.Right, OffscreenParticipantIndicator.FindEdge(new(820, 200, 20, 20), viewport));
        AssertEqual<ScreenEdge?>(ScreenEdge.Top, OffscreenParticipantIndicator.FindEdge(new(390, 50, 20, 20), viewport));
        AssertEqual<ScreenEdge?>(ScreenEdge.Bottom, OffscreenParticipantIndicator.FindEdge(new(390, 610, 20, 20), viewport));
        AssertEqual<ScreenEdge?>(null, OffscreenParticipantIndicator.FindEdge(new(390, 200, 20, 20), viewport));
        AssertEqual<ScreenEdge?>(null, OffscreenParticipantIndicator.FindEdge(new(-10, 200, 20, 20), viewport));
        AssertEqual<ScreenEdge?>(ScreenEdge.Left, OffscreenParticipantIndicator.FindEdge(new(-900, -20, 20, 20), viewport));
    }

    private static void DragPreviewThenCommit()
    {
        var workspace = new ProjectWorkspace(CreateProject());
        var controller = new DeskDragController(workspace, new GridViewport(20d));

        AssertEqual(true, controller.BeginDrag(new ScreenPoint(25d, 5d)));
        AssertEqual(true, controller.UpdateDrag(new ScreenPoint(65d, 45d)));
        AssertEqual(new GridPosition(2, 2), controller.PreviewAnchor!.Value);
        AssertEqual(new GridPosition(0, 0), CurrentAnchor(workspace));

        var result = controller.Drop();
        AssertEqual(true, result.Applied);
        AssertEqual(QuarterTurn.North, result.AffectedOrientation);
        AssertEqual(new GridPosition(2, 2), CurrentAnchor(workspace));
        AssertEqual(true, workspace.CanUndo);
    }

    private static void InvalidDropIsRejected()
    {
        var workspace = new ProjectWorkspace(CreateProject());
        var controller = new DeskDragController(workspace, new GridViewport(20d));
        controller.BeginDrag(new ScreenPoint(5d, 5d));
        controller.UpdateDrag(new ScreenPoint(-15d, -15d));

        var result = controller.Drop();
        AssertEqual(false, result.Applied);
        AssertEqual(true, result.Issues.Any(issue => issue.Code == "deskPlacement.outOfBounds"));
        AssertEqual(new GridPosition(0, 0), CurrentAnchor(workspace));
        AssertEqual(false, workspace.CanUndo);
    }

    private static void CancelLeavesProjectUnchanged()
    {
        var workspace = new ProjectWorkspace(CreateProject());
        var controller = new DeskDragController(workspace, new GridViewport(20d));
        controller.BeginDrag(new ScreenPoint(5d, 5d));
        controller.UpdateDrag(new ScreenPoint(45d, 45d));
        controller.Cancel();

        AssertEqual(false, controller.IsDragging);
        AssertEqual(null, controller.PreviewAnchor);
        AssertEqual(new GridPosition(0, 0), CurrentAnchor(workspace));
    }

    private static void PlanSelectionCycles()
    {
        var project = CreateProject();
        project = CircleSpaceCoordinator.Application.Plans.PlanCatalogService.DuplicatePlan(
            project,
            "plan-1",
            "plan-2",
            "架空配置案２");
        var workspace = new ProjectWorkspace(project);
        var commands = new EditorCommandController(workspace);

        commands.CyclePlan(1);
        AssertEqual("plan-2", workspace.SelectedPlanId);
        commands.CyclePlan(1);
        AssertEqual("plan-1", workspace.SelectedPlanId);
        commands.CyclePlan(-1);
        AssertEqual("plan-2", workspace.SelectedPlanId);
    }

    private static void DeskCanBeRotatedAndUndone()
    {
        var workspace = new ProjectWorkspace(CreateProject());
        var commands = new EditorCommandController(workspace);
        var result = commands.RotateDeskAt(new GridPosition(1, 0), clockwise: true);

        AssertEqual(true, result.Applied);
        AssertEqual(QuarterTurn.East, result.AffectedOrientation);
        AssertEqual(QuarterTurn.East, workspace.SelectedPlan.DeskPlacements.Single().Orientation);
        AssertEqual(true, commands.Undo());
        AssertEqual(QuarterTurn.North, workspace.SelectedPlan.DeskPlacements.Single().Orientation);
        AssertEqual(true, commands.Redo());
        AssertEqual(QuarterTurn.East, workspace.SelectedPlan.DeskPlacements.Single().Orientation);
    }

    private static void RotatingEmptySpaceDoesNothing()
    {
        var workspace = new ProjectWorkspace(CreateProject());
        var result = new EditorCommandController(workspace).RotateDeskAt(new GridPosition(4, 3), clockwise: true);

        AssertEqual(false, result.Applied);
        AssertEqual(0, result.Issues.Count);
        AssertEqual(false, workspace.CanUndo);
    }

    private static void OperationLogIsStructured()
    {
        var json = UiOperationLogFormatter.Serialize(new UiOperationLogEntry(
            new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero),
            "pointer_left_down",
            ScreenX: 123,
            ScreenY: 45,
            GridX: 3,
            GridY: 1,
            Success: true,
            Detail: "circleName=fictional-circle-private;circleId=private-001;path=C:\\Users\\private-user\\event.json;message=private-error"));

        AssertEqual(true, json.Contains("\"action\":\"pointer_left_down\"", StringComparison.Ordinal));
        AssertEqual(true, json.Contains("\"screenX\":123", StringComparison.Ordinal));
        AssertEqual(true, json.Contains("\"gridY\":1", StringComparison.Ordinal));
        AssertEqual(false, json.Contains("fictional-circle", StringComparison.Ordinal));
        AssertEqual(false, json.Contains("private-", StringComparison.Ordinal));
        AssertEqual(true, json.Contains("\"detail\":null", StringComparison.Ordinal));
    }

    private static void ParticipantCanBePlaced()
    {
        var workspace = new ProjectWorkspace(CreateAssignmentProject(requiredCellCount: 1));
        var controller = new ParticipantPlacementController(workspace);

        AssertEqual("fictional-circle-002", controller.SelectedParticipantId);
        var result = controller.AssignSelectedAt(new GridPosition(2, 0));
        AssertEqual(true, result.Applied);
        AssertEqual(2, workspace.SelectedPlan.Assignments.Count);
        AssertEqual(null, controller.SelectedParticipantId);
    }

    private static void TwoCellParticipantUsesDesk()
    {
        var workspace = new ProjectWorkspace(CreateAssignmentProject(requiredCellCount: 2));
        var controller = new ParticipantPlacementController(workspace);
        var result = controller.AssignSelectedAt(new GridPosition(3, 0));
        var assignment = workspace.SelectedPlan.Assignments.Single(item => item.ParticipantId == "fictional-circle-002");

        AssertEqual(true, result.Applied);
        AssertEqual(2, assignment.OccupiedCells.Count);
        AssertEqual(true, assignment.OccupiedCells.Contains(new GridPosition(2, 0)));
        AssertEqual(true, assignment.OccupiedCells.Contains(new GridPosition(3, 0)));
        AssertEqual(new GridPosition(3, 0), assignment.ScoringPosition);
    }

    private static void ParticipantCanBeRemoved()
    {
        var workspace = new ProjectWorkspace(CreateAssignmentProject(requiredCellCount: 1));
        var controller = new ParticipantPlacementController(workspace);
        controller.AssignSelectedAt(new GridPosition(2, 0));
        var result = controller.UnassignAt(new GridPosition(2, 0));

        AssertEqual(true, result.Applied);
        AssertEqual(1, workspace.SelectedPlan.Assignments.Count);
        AssertEqual("fictional-circle-002", controller.SelectedParticipantId);
    }

    private static void ParticipantCanBeReassignedByPlacementController()
    {
        var workspace = new ProjectWorkspace(CreateAssignmentProject(requiredCellCount: 1));
        var controller = new ParticipantPlacementController(workspace);

        var result = controller.PlaceParticipantAt("fictional-circle-001", new GridPosition(3, 0));
        var assignment = workspace.SelectedPlan.Assignments.Single(item => item.ParticipantId == "fictional-circle-001");

        AssertEqual(true, result.Applied);
        AssertEqual(new GridPosition(3, 0), assignment.ScoringPosition);
        AssertEqual(true, assignment.OccupiedCells.Contains(new GridPosition(3, 0)));
    }

    private static void ParticipantsCanBeSwapped()
    {
        var workspace = new ProjectWorkspace(CreateAssignmentProject(requiredCellCount: 1));
        var controller = new ParticipantPlacementController(workspace);
        AssertEqual(true, controller.AssignSelectedAt(new GridPosition(3, 0)).Applied);

        var result = controller.SwapParticipants("fictional-circle-001", "fictional-circle-002");
        var assignments = workspace.SelectedPlan.Assignments.ToDictionary(item => item.ParticipantId);

        AssertEqual(true, result.Applied);
        AssertEqual(new GridPosition(3, 0), assignments["fictional-circle-001"].ScoringPosition);
        AssertEqual(new GridPosition(0, 0), assignments["fictional-circle-002"].ScoringPosition);
        AssertEqual(true, workspace.CanUndo);
        workspace.Undo();
        AssertEqual(new GridPosition(0, 0), workspace.SelectedPlan.Assignments
            .Single(item => item.ParticipantId == "fictional-circle-001").ScoringPosition);
    }

    private static void SameDeskParticipantsCanBeSwapped()
    {
        var workspace = new ProjectWorkspace(CreateAssignmentProject(requiredCellCount: 1));
        var controller = new ParticipantPlacementController(workspace);
        AssertEqual(true, controller.AssignSelectedAt(new GridPosition(1, 0)).Applied);

        var result = controller.SwapParticipants("fictional-circle-001", "fictional-circle-002");
        var assignments = workspace.SelectedPlan.Assignments.ToDictionary(item => item.ParticipantId);

        AssertEqual(true, result.Applied);
        AssertEqual(new GridPosition(1, 0), assignments["fictional-circle-001"].ScoringPosition);
        AssertEqual(new GridPosition(0, 0), assignments["fictional-circle-002"].ScoringPosition);
        AssertEqual(true, workspace.CanUndo);
    }

    private static void CombinedPairSwapsWholeDesk()
    {
        var source = CreateAssignmentProject(requiredCellCount: 1);
        var participants = Enumerable.Range(1, 4)
            .Select(index => new Participant($"circle-{index}", $"サークル{index}", 1, new Dictionary<string, double>()))
            .ToArray();
        var assignments = new[]
        {
            new ParticipantAssignment(participants[0].Id, new HashSet<GridPosition> { new(0, 0) }, new GridPosition(0, 0)) { CombinedSpaceId = "combined-1" },
            new ParticipantAssignment(participants[1].Id, new HashSet<GridPosition> { new(1, 0) }, new GridPosition(1, 0)) { CombinedSpaceId = "combined-1" },
            new ParticipantAssignment(participants[2].Id, new HashSet<GridPosition> { new(2, 0) }, new GridPosition(2, 0)),
            new ParticipantAssignment(participants[3].Id, new HashSet<GridPosition> { new(3, 0) }, new GridPosition(3, 0)),
        };
        var project = source with
        {
            Participants = participants,
            Plans = [source.Plans[0] with { Assignments = assignments }],
        };
        var workspace = new ProjectWorkspace(project);
        var result = new ParticipantPlacementController(workspace).SwapParticipants("circle-1", "circle-3");
        var byId = workspace.SelectedPlan.Assignments.ToDictionary(item => item.ParticipantId);

        AssertEqual(true, result.Applied);
        AssertEqual(true, byId["circle-1"].OccupiedCells.Contains(new GridPosition(2, 0)));
        AssertEqual(true, byId["circle-2"].OccupiedCells.Contains(new GridPosition(3, 0)));
        AssertEqual(true, byId["circle-3"].OccupiedCells.Contains(new GridPosition(0, 0)));
        AssertEqual(true, byId["circle-4"].OccupiedCells.Contains(new GridPosition(1, 0)));
        AssertEqual("combined-1", byId["circle-1"].CombinedSpaceId);
        AssertEqual("combined-1", byId["circle-2"].CombinedSpaceId);
        AssertEqual(true, workspace.CanUndo);
    }

    private static void CombinedPairMembersCanSwapCells()
    {
        var source = CreateAssignmentProject(requiredCellCount: 1);
        var participants = source.Participants.Take(2).ToArray();
        var plan = source.Plans[0] with
        {
            Assignments =
            [
                new ParticipantAssignment(participants[0].Id, new HashSet<GridPosition> { new(0, 0) }, new GridPosition(0, 0)) { CombinedSpaceId = "combined-1" },
                new ParticipantAssignment(participants[1].Id, new HashSet<GridPosition> { new(1, 0) }, new GridPosition(1, 0)) { CombinedSpaceId = "combined-1" },
            ],
        };
        var workspace = new ProjectWorkspace(source with { Participants = participants, Plans = [plan] });

        var result = new ParticipantPlacementController(workspace)
            .SwapParticipants(participants[0].Id, participants[1].Id);
        var byId = workspace.SelectedPlan.Assignments.ToDictionary(item => item.ParticipantId);

        AssertEqual(true, result.Applied);
        AssertEqual(new GridPosition(1, 0), byId[participants[0].Id].ScoringPosition);
        AssertEqual(new GridPosition(0, 0), byId[participants[1].Id].ScoringPosition);
        AssertEqual("combined-1", byId[participants[0].Id].CombinedSpaceId);
        AssertEqual("combined-1", byId[participants[1].Id].CombinedSpaceId);
    }

    private static void TwoCellParticipantSwapsWholeDesk()
    {
        var source = CreateAssignmentProject(requiredCellCount: 1);
        var participants = new[]
        {
            new Participant("two-space", "2スペサークル", 2, new Dictionary<string, double>()),
            new Participant("single-1", "1スペサークル1", 1, new Dictionary<string, double>()),
            new Participant("single-2", "1スペサークル2", 1, new Dictionary<string, double>()),
        };
        var assignments = new[]
        {
            new ParticipantAssignment(participants[0].Id, new HashSet<GridPosition> { new(0, 0), new(1, 0) }, new GridPosition(0, 0)),
            new ParticipantAssignment(participants[1].Id, new HashSet<GridPosition> { new(2, 0) }, new GridPosition(2, 0)),
            new ParticipantAssignment(participants[2].Id, new HashSet<GridPosition> { new(3, 0) }, new GridPosition(3, 0)),
        };
        var project = source with
        {
            Participants = participants,
            Plans = [source.Plans[0] with { Assignments = assignments }],
        };
        var workspace = new ProjectWorkspace(project);

        var result = new ParticipantPlacementController(workspace).SwapParticipants("two-space", "single-1");
        var byId = workspace.SelectedPlan.Assignments.ToDictionary(item => item.ParticipantId);

        AssertEqual(true, result.Applied);
        AssertEqual(true, byId["two-space"].OccupiedCells.SetEquals([new GridPosition(2, 0), new GridPosition(3, 0)]));
        AssertEqual(true, byId["single-1"].OccupiedCells.Contains(new GridPosition(0, 0)));
        AssertEqual(true, byId["single-2"].OccupiedCells.Contains(new GridPosition(1, 0)));
        AssertEqual(true, workspace.CanUndo);
    }

    private static void StartupUsesFictionalExample()
    {
        var path = DesktopStartup.ResolveProjectPath([], "C:\\fictional-app");
        AssertEqual(
            Path.GetFullPath("C:\\fictional-app\\examples\\circle-space-project-v1.example.json"),
            path);
    }

    private static void StartupUsesExplicitPath()
    {
        var path = DesktopStartup.ResolveProjectPath(["fictional-project.json"], "C:\\fictional-app");
        AssertEqual(Path.GetFullPath("fictional-project.json"), path);
    }

    private static void SettingsRememberLastProject()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"circle-space-settings-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var settingsPath = Path.Combine(directory, "application-settings.json");
        var projectPath = Path.Combine(directory, "会場.json");
        try
        {
            File.WriteAllText(projectPath, "{}");
            var settings = new ApplicationSettingsService(settingsPath);
            settings.RememberProject(projectPath);

            var reloaded = new ApplicationSettingsService(settingsPath);
            AssertEqual("1.1", reloaded.Current.SchemaVersion);
            AssertEqual(Path.GetFullPath(projectPath), reloaded.Current.LastProjectPath);
            AssertEqual(Path.GetFullPath(directory), reloaded.Current.ProjectsDirectory);
            AssertEqual(1, reloaded.Current.EventProjects!.Count);
            AssertEqual(Path.GetFullPath(projectPath), DesktopStartup.ResolveProjectPath([], directory, reloaded.Current.LastProjectPath));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static void SettingsRememberExcelDirectories()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"circle-space-excel-directory-test-{Guid.NewGuid():N}");
        var importDirectory = Path.Combine(directory, "import");
        var exportDirectory = Path.Combine(directory, "export");
        Directory.CreateDirectory(importDirectory);
        Directory.CreateDirectory(exportDirectory);
        try
        {
            var settingsPath = Path.Combine(directory, "application-settings.json");
            var settings = new ApplicationSettingsService(settingsPath);
            settings.RememberParticipantImportPath(Path.Combine(importDirectory, "circles.xlsx"));
            settings.RememberCircleSeatExportPath(Path.Combine(exportDirectory, "result.xlsx"));

            var reloaded = new ApplicationSettingsService(settingsPath);
            AssertEqual(Path.GetFullPath(importDirectory), reloaded.Current.ParticipantImportDirectory!);
            AssertEqual(Path.GetFullPath(exportDirectory), reloaded.Current.CircleSeatExportDirectory!);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static void SettingsPreserveCircleLabelDisplay()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"circle-space-label-display-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var settingsPath = Path.Combine(directory, "application-settings.json");
            var settings = new ApplicationSettingsService(settingsPath);
            settings.SaveCircleLabelDisplay(new CircleLabelDisplaySettings("circleId", "\\w+\\d+-(\\d+)", "$1"));

            var display = new ApplicationSettingsService(settingsPath).Current.CircleLabelDisplay!;
            AssertEqual("circleId", display.DisplayField);
            AssertEqual("\\w+\\d+-(\\d+)", display.CircleIdPattern!);
            AssertEqual("$1", display.CircleIdReplacement!);
            settings.SaveCircleLabelDisplay(display with { DisplayField = "channel", ChannelId = "books" });
            var channel = new ApplicationSettingsService(settingsPath).Current.CircleLabelDisplay!;
            AssertEqual("channel", channel.DisplayField);
            AssertEqual("books", channel.ChannelId!);
            AssertEqual(display.CircleIdPattern!, channel.CircleIdPattern!);
            AssertEqual("$1", channel.CircleIdReplacement!);
            settings.SaveCircleLabelDisplay(channel with { DisplayField = "circleId", ChannelId = null });
            AssertEqual(display, new ApplicationSettingsService(settingsPath).Current.CircleLabelDisplay!);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static void CircleLabelsShowChannelValues()
    {
        var participant = new Participant("internal-a", "abcdefghijklmnop", 1,
            new Dictionary<string, double> { ["books"] = 1, ["fraction"] = 0.125, ["zero"] = 0 }) { CircleId = "event12-003" };
        var display = new CircleLabelDisplaySettings("circleId", @"^\w+\d+-(\d+)$", "$1");
        AssertEqual("003", CircleLabelFormatter.Format(participant, 7, display));
        AssertEqual("7", CircleLabelFormatter.Format(participant, 7, display with { DisplayField = "internalId" }));
        AssertEqual("abcd\r\nefgh\r\nijk…", CircleLabelFormatter.Format(participant, 7, display with { DisplayField = "displayName" }));
        AssertEqual("1", CircleLabelFormatter.Format(participant, 7, display with { DisplayField = "channel", ChannelId = "books" }));
        AssertEqual("0", CircleLabelFormatter.Format(participant, 7, display with { DisplayField = "channel", ChannelId = "zero" }));
        AssertEqual("0", CircleLabelFormatter.Format(participant, 7, display with { DisplayField = "channel", ChannelId = "unset" }));
        var previousCulture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("fr-FR");
            AssertEqual("0.125", CircleLabelFormatter.Format(participant, 7, display with { DisplayField = "channel", ChannelId = "fraction" }));
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = previousCulture; }
        AssertEqual("event12-003", CircleLabelFormatter.Format(participant, 7, display with { CircleIdPattern = "[" }));
        AssertEqual("event12-003", CircleLabelFormatter.Format(participant, 7, display with { CircleIdPattern = null }));
    }

    private static void SettingsManageProjectCatalog()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"circle-space-catalog-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var first = Path.Combine(directory, "first.json");
        var second = Path.Combine(directory, "second.json");
        try
        {
            File.WriteAllText(first, "{}");
            File.WriteAllText(second, "{}");
            var settings = new ApplicationSettingsService(Path.Combine(directory, "settings.json"));
            settings.RememberProject(first);
            settings.RememberProject(second);
            settings.RememberProject(first);
            AssertEqual(2, settings.Current.EventProjects!.Count);
            AssertEqual(true, settings.MoveProject(second, -1));
            AssertEqual(Path.GetFullPath(second), settings.Current.EventProjects![0].Path);
            settings.RemoveProject(second);
            AssertEqual(1, settings.Current.EventProjects!.Count);
            AssertEqual(true, File.Exists(second));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static void SettingsPreserveWorkingState()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"circle-space-working-state-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var settingsPath = Path.Combine(directory, "settings.json");
        var projectPath = Path.Combine(directory, "event.json");
        try
        {
            var settings = new ApplicationSettingsService(settingsPath);
            settings.SaveWorkingState(new ProjectWorkingState(
                projectPath, "plan-2", "GenrePlacement", 1.75d, -42d, 88d,
                new Dictionary<string, bool> { ["evaluationAnalysis"] = true }, "unused-desk"));

            var restored = new ApplicationSettingsService(settingsPath).GetWorkingState(projectPath)!;
            AssertEqual("plan-2", restored.SelectedPlanId);
            AssertEqual("unused-desk", restored.SelectedDeskLayoutId!);
            AssertEqual("GenrePlacement", restored.EditorMode);
            AssertEqual(1.75d, restored.Zoom);
            AssertEqual(true, restored.Switches!["evaluationAnalysis"]);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static void LegacySettingsMigrateProject()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"circle-space-legacy-settings-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var projectPath = Path.Combine(directory, "legacy-event.json");
        try
        {
            ProjectFileService.Save(projectPath, CreateProject() with { Name = "旧設定イベント" });
            var settingsPath = Path.Combine(directory, "settings.json");
            File.WriteAllText(settingsPath, $$"""
                {
                  "projectsDirectory": {{System.Text.Json.JsonSerializer.Serialize(directory)}},
                  "lastProjectPath": {{System.Text.Json.JsonSerializer.Serialize(projectPath)}}
                }
                """);
            var settings = new ApplicationSettingsService(settingsPath);
            AssertEqual("1.1", settings.Current.SchemaVersion);
            using (var savedSettings = System.Text.Json.JsonDocument.Parse(File.ReadAllText(settingsPath)))
                AssertEqual("1.1", savedSettings.RootElement.GetProperty("schemaVersion").GetString()!);
            AssertEqual(1, settings.Current.EventProjects!.Count);
            AssertEqual("旧設定イベント", settings.Current.EventProjects[0].DisplayName);
            AssertEqual(Path.GetFullPath(projectPath), settings.Current.EventProjects[0].Path);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static void EmptyCatalogRecoversProjects()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"circle-space-recovery-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var projectPath = Path.Combine(directory, "recoverable-event.json");
            ProjectFileService.Save(projectPath, CreateProject() with { Name = "復旧対象イベント" });
            var settingsPath = Path.Combine(directory, "application-settings.json");
            File.WriteAllText(settingsPath, $$"""
                {
                  "schemaVersion": "1.0",
                  "projectsDirectory": {{System.Text.Json.JsonSerializer.Serialize(directory)}},
                  "lastProjectPath": null,
                  "eventProjects": []
                }
                """);

            var settings = new ApplicationSettingsService(settingsPath);

            AssertEqual(1, settings.Current.EventProjects!.Count);
            AssertEqual("復旧対象イベント", settings.Current.EventProjects[0].DisplayName);
            AssertEqual(Path.GetFullPath(projectPath), settings.Current.EventProjects[0].Path);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static void EventCatalogCreatesProject()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"circle-space-create-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "event.json");
            var settings = new ApplicationSettingsService(Path.Combine(directory, "settings.json"));
            var created = new EventProjectCatalogService(settings).Create(path, "架空イベント");
            var project = ProjectFileService.Load(path);
            AssertEqual("架空イベント", created.DisplayName);
            AssertEqual("架空イベント", project.Name);
            AssertEqual(1, project.Plans.Count);
            AssertEqual(1, settings.Current.EventProjects!.Count);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static void EventCatalogMarksProjectConfidential()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"circle-space-confidential-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "secret-event.json");
            var settings = new ApplicationSettingsService(Path.Combine(directory, "settings.json"));
            var catalog = new EventProjectCatalogService(settings);
            catalog.Create(path, "架空の非公開イベント");
            AssertEqual(false, ProjectFileService.Load(path).IsConfidential);

            catalog.MarkConfidential(path);

            AssertEqual(true, ProjectFileService.Load(path).IsConfidential);
            AssertEqual(true, catalog.IsConfidential(path));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static void EventCatalogDuplicatesProject()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"circle-space-duplicate-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var sourcePath = Path.Combine(directory, "source.json");
            var copyPath = Path.Combine(directory, "copy.json");
            ProjectFileService.Save(sourcePath, CreateProject());
            var settings = new ApplicationSettingsService(Path.Combine(directory, "settings.json"));
            var catalog = new EventProjectCatalogService(settings);
            catalog.Register(sourcePath);
            catalog.Duplicate(sourcePath, copyPath, "複製イベント");
            var source = ProjectFileService.Load(sourcePath);
            var copy = ProjectFileService.Load(copyPath);
            AssertEqual(false, source.Id == copy.Id);
            AssertEqual("複製イベント", copy.Name);
            AssertEqual(source.Plans.Count, copy.Plans.Count);
            AssertEqual(2, catalog.Projects.Count);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static void DeskCanBeAddedAndRemovedByCommand()
    {
        var workspace = new ProjectWorkspace(CreateProject());
        var commands = new EditorCommandController(workspace);
        var added = commands.AddDeskAt(new GridPosition(2, 2));
        AssertEqual(true, added.Applied);
        AssertEqual(2, workspace.SelectedPlan.DeskPlacements.Count);

        var removed = commands.RemoveDeskAt(new GridPosition(2, 2));
        AssertEqual(true, removed.Applied);
        AssertEqual(1, workspace.SelectedPlan.DeskPlacements.Count);
    }

    private static void DeskCanBeAddedWithOrientation()
    {
        var workspace = new ProjectWorkspace(CreateProject());
        var commands = new EditorCommandController(workspace);

        var added = commands.AddDeskAt(new GridPosition(3, 1), QuarterTurn.East);

        AssertEqual(true, added.Applied);
        AssertEqual(QuarterTurn.East, added.AffectedOrientation);
        AssertEqual(
            QuarterTurn.East,
            workspace.SelectedPlan.DeskPlacements.Single(item => item.Anchor == new GridPosition(3, 1)).Orientation);
    }

    private static void CatalogSpacesRoundTrip()
    {
        foreach (var definition in SpaceDefinitionCatalog.CreateDefault().Types)
        foreach (var orientation in Enum.GetValues<QuarterTurn>())
        {
            var workspace = new ProjectWorkspace(CreateProject() with
            {
                Venue = new Venue("venue", "Venue", 24, 24, new HashSet<GridPosition>()),
            });
            var commands = new EditorCommandController(workspace);
            var type = SpaceTypeFactory.Create(definition);
            var before = ProjectJsonSerializer.Save(workspace.Project);
            AssertEqual(true, commands.AddDeskAt(new GridPosition(10, 10), orientation, type).Applied);
            var placed = workspace.SelectedPlan.DeskPlacements.Single(p => p.DeskTypeId == type.Id);
            AssertEqual(true, placed.GetOccupiedCells(type).SetEquals(definition.Cells.Select(c => new GridPosition(10, 10) + new GridPosition(c.X, c.Y).Rotate(orientation))));
            var saved = ProjectJsonSerializer.Save(workspace.Project);
            var loaded = ProjectJsonSerializer.Load(saved);
            var details = loaded.DeskTypes.Single(t => t.Id == type.Id).Space!;
            AssertEqual(definition.Id, details.DefinitionId);
            AssertEqual(true, details.Cells.SequenceEqual(type.Space!.Cells));
            AssertEqual(true, details.Edges.SequenceEqual(definition.Edges));
            AssertEqual(true, commands.Undo());
            AssertEqual(before, ProjectJsonSerializer.Save(workspace.Project));
            AssertEqual(true, commands.Redo());
            AssertEqual(saved, ProjectJsonSerializer.Save(workspace.Project));
            var changed = SpaceTypeFactory.Create(definition with { Name = "Changed" });
            AssertEqual(false, changed.Id == type.Id);
            AssertEqual(false, commands.AddDeskAt(placed.Anchor, orientation, changed).Applied);
            AssertEqual(saved, ProjectJsonSerializer.Save(workspace.Project));
            AssertEqual(true, commands.AddDeskAt(new GridPosition(18, 18), orientation, changed).Applied);
            AssertEqual(definition.Name, workspace.Project.DeskTypes.Single(t => t.Id == type.Id).Name);
            var malformed = type with { Space = type.Space! with { Edges = [] } };
            AssertEqual(true, CircleSpaceCoordinator.Core.Validation.ProjectValidator.Validate(loaded with
            {
                DeskTypes = loaded.DeskTypes.Select(t => t.Id == type.Id ? malformed : t).ToArray(),
            }).Any(issue => issue.Code == "deskType.space.invalid"));
        }
    }

    private static void SpaceDefinitionsPersist()
    {
        var directory = Path.Combine(Path.GetTempPath(), "space-definitions-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "space-definitions.json");
            var first = new SpaceDefinitionStore(path);
            AssertEqual(6, first.Current.Types.Count);
            AssertEqual(2, first.Current.Requests.Count);
            AssertEqual(0, first.Current.Types.Single(t => t.Id == "desk-3-ends").Cells.Single(c => c.X == 1).Area);
            AssertEqual(1, first.Current.Types.Single(t => t.Id == "booth").Cells.Select(c => c.Area).Distinct().Count());
            first.Save(first.Current);
            var second = new SpaceDefinitionStore(path);
            var changed = first.Current with { Types = first.Current.Types.Select(t => t.Id == "desk-2-seats" ? t with { Name = "共通の机" } : t).ToArray() };
            first.Save(changed);
            AssertEqual("共通の机", new SpaceDefinitionStore(path).Current.Types[0].Name);
            try { second.Save(second.Current); throw new Exception("Expected stale save rejection."); }
            catch (IOException) { }
            var persisted = File.ReadAllText(path);
            try
            {
                first.Save(changed with { Types = changed.Types.Where(t => t.Id != "desk-2-seats").ToArray() });
                throw new Exception("Expected reference validation.");
            }
            catch (InvalidDataException) { }
            AssertEqual(persisted, File.ReadAllText(path));
            try
            {
                first.Save(changed with { Requests = [.. changed.Requests, changed.Requests[0] with { Id = "duplicate-value" }] });
                throw new Exception("Expected duplicate input validation.");
            }
            catch (InvalidDataException) { }
            var emptyType = changed.Types[0] with { Cells = [new(0, 0, 0)] };
            try { (changed with { Types = [emptyType] }).Validate(); throw new Exception("Expected missing area rejection."); }
            catch (InvalidDataException) { }
            File.WriteAllText(path, "broken");
            try { _ = new SpaceDefinitionStore(path); throw new Exception("Expected corrupt file rejection."); }
            catch (System.Text.Json.JsonException) { }
            AssertEqual("broken", File.ReadAllText(path));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static void CommandsFillDesksAndResizeVenue()
    {
        var workspace = new ProjectWorkspace(CreateProject());
        var commands = new EditorCommandController(workspace);
        AssertEqual(true, commands.ResizeVenue(1, 1).Applied);
        AssertEqual(6, workspace.Project.Venue.Width);
        AssertEqual(5, workspace.Project.Venue.Height);

        var anchor = workspace.SelectedPlan.DeskPlacements[0].Anchor;
        AssertEqual(true, commands.ResizeVenue(1, 0, 1, 0).Applied);
        AssertEqual(anchor + new GridPosition(1, 0), workspace.SelectedPlan.DeskPlacements[0].Anchor);
        AssertEqual(true, commands.Undo());
        AssertEqual(anchor, workspace.SelectedPlan.DeskPlacements[0].Anchor);
        AssertEqual(true, commands.Redo());
        AssertEqual(anchor + new GridPosition(1, 0), workspace.SelectedPlan.DeskPlacements[0].Anchor);
        AssertEqual(true, commands.ResizeVenue(-1, 0, -1, 0).Applied);
        AssertEqual(anchor, workspace.SelectedPlan.DeskPlacements[0].Anchor);

        var before = workspace.SelectedPlan.DeskPlacements.Count;
        AssertEqual(true, commands.FillDesks().Applied);
        AssertEqual(true, workspace.SelectedPlan.DeskPlacements.Count > before);
    }

    private static void ProjectCanBeSavedAndReloaded()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"circle-space-save-test-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "fictional-project.json");
        try
        {
            var project = CreateProject();
            ProjectFileService.Save(path, project);
            var loaded = ProjectJsonSerializer.Load(File.ReadAllText(path));
            AssertEqual(project.Id, loaded.Id);
            AssertEqual(false, File.Exists(path + ".tmp"));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
            if (Directory.Exists(directory))
                Directory.Delete(directory);
        }
    }

    private static void EditedCirclesSurviveDuplication()
    {
        var workspace = new ProjectWorkspace(CreateAssignmentProject(2));
        var commands = new EditorCommandController(workspace);
        workspace.Apply((project, planId) => ParticipantAssignmentEditor.Reassign(
            project, planId, "fictional-circle-001", new HashSet<GridPosition> { new(2, 0) }, new(2, 0)));
        // Renaming rebuilds the combined plan from the separated layout records.
        workspace.ApplyProjectEdit(project => LayoutCatalogService.RenameCircleLayout(project, "plan-1", "Edited"));
        AssertEqual(new GridPosition(2, 0), workspace.SelectedPlan.Assignments.Single().ScoringPosition);
        AssertEqual(true, commands.DuplicateSelectedPlan("Copy").Applied);
        var copyId = workspace.SelectedPlanId;
        foreach (var plan in workspace.Project.Plans)
            AssertEqual(new GridPosition(2, 0), plan.Assignments.Single().ScoringPosition);
        AssertEqual(true, commands.Undo());
        AssertEqual(new GridPosition(2, 0), workspace.SelectedPlan.Assignments.Single().ScoringPosition);
        AssertEqual(true, commands.Redo());
        var restored = new ProjectWorkspace(ProjectJsonSerializer.Load(ProjectJsonSerializer.Save(workspace.Project)));
        restored.SelectPlan(copyId);
        AssertEqual(new GridPosition(2, 0), restored.SelectedPlan.Assignments.Single().ScoringPosition);
        restored.SelectPlan("plan-1");
        AssertEqual(new GridPosition(2, 0), restored.SelectedPlan.Assignments.Single().ScoringPosition);
    }

    private static void DuplicateDeskEditsAreShared()
    {
        var workspace = new ProjectWorkspace(CreateProject());
        var commands = new EditorCommandController(workspace);
        AssertEqual(true, commands.DuplicateSelectedPlan("Copy").Applied);
        AssertEqual(true, commands.SetDeskNumber("desk-1", "10").Applied);
        foreach (var plan in workspace.Project.Plans)
            AssertEqual("10", plan.DeskPlacements.Single().DeskNumber);
        AssertEqual(true, commands.Undo());
        AssertEqual<string?>(null, workspace.Project.DeskLayouts.Single().DeskPlacements.Single().DeskNumber);
        AssertEqual(true, commands.Redo());
        var restored = ProjectJsonSerializer.Load(ProjectJsonSerializer.Save(workspace.Project));
        foreach (var plan in restored.Plans)
            AssertEqual("10", plan.DeskPlacements.Single().DeskNumber);
    }

    private static void AllDesksNeedNumbersForExport()
    {
        var workspace = new ProjectWorkspace(CreateAssignmentProject(2));
        var commands = new EditorCommandController(workspace);
        AssertEqual(true, commands.SetDeskNumber("desk-1", "10").Applied);
        AssertEqual("desk-2", CircleSeatExportBuilder.FindMissingDeskNumbers(workspace.SelectedPlan).Single().Id);
        AssertExportRejected();
        workspace.Apply((project, planId) => ParticipantAssignmentEditor.Unassign(project, planId, "fictional-circle-001"));
        workspace.Apply((project, planId) => ParticipantAssignmentEditor.Assign(
            project, planId, "fictional-circle-002", new HashSet<GridPosition> { new(0, 0), new(1, 0) }, new(0, 0)));
        workspace.Apply((project, planId) => ParticipantAssignmentEditor.Reassign(
            project, planId, "fictional-circle-002", new HashSet<GridPosition> { new(2, 0), new(3, 0) }, new(2, 0)));
        workspace.Apply((project, planId) => DeskSeatLabelEditor.SetLabel(project, planId, "desk-2", new(0, 0), "A", "11a"));
        AssertExportRejected();
        AssertEqual(true, commands.SetDeskNumber("desk-2", "11").Applied);
        var row = CircleSeatExportBuilder.Build(workspace.Project, workspace.SelectedPlan).Single();
        AssertEqual("A", row.BlockName);
        AssertEqual("11", row.SeatName);
        AssertEqual(true, commands.SetDeskNumber("desk-2", " ").Applied);
        AssertExportRejected();

        void AssertExportRejected()
        {
            try
            {
                CircleSeatExportBuilder.Build(workspace.Project, workspace.SelectedPlan);
            }
            catch (InvalidOperationException exception) when (exception.Message.Contains("スペース番号"))
            {
                return;
            }
            throw new InvalidOperationException("Export must reject missing desk numbers.");
        }
    }

    private static void SelectedPlanCanBeDuplicated()
    {
        var workspace = new ProjectWorkspace(CreateProject());
        var commands = new EditorCommandController(workspace);
        var source = workspace.SelectedPlan;
        var result = commands.DuplicateSelectedPlan("案1a");

        AssertEqual(true, result.Applied);
        AssertEqual(workspace.Project.DeskLayouts.Single().Id, workspace.SelectedDeskLayoutId);
        AssertEqual(2, workspace.Project.CircleLayouts.Count);
        AssertEqual(2, workspace.Project.Plans.Count);
        AssertEqual(false, source.Id == workspace.SelectedPlanId);
        AssertEqual("案1a", workspace.SelectedPlan.Name);
        AssertEqual(source.DeskPlacements.Count, workspace.SelectedPlan.DeskPlacements.Count);
        AssertEqual(source.Assignments.Count, workspace.SelectedPlan.Assignments.Count);
        var duplicateId = workspace.SelectedPlanId;
        var deskId = workspace.SelectedDeskLayoutId;
        AssertEqual(duplicateId, workspace.GetSelectedPlanSnapshot().PlanId);
        AssertEqual(true, workspace.RankPlans().Any(plan => plan.PlanId == duplicateId));
        AssertEqual(false, ReferenceEquals(source.Assignments[0].OccupiedCells, workspace.SelectedPlan.Assignments[0].OccupiedCells));
        AssertEqual(true, commands.Undo());
        AssertEqual(source.Id, workspace.SelectedPlanId);
        AssertEqual(1, workspace.Project.CircleLayouts.Count);
        AssertEqual(true, commands.Redo());
        workspace.SelectPlan(duplicateId);
        AssertEqual(deskId, workspace.SelectedDeskLayoutId);

        var renameResult = commands.RenameSelectedPlan("案1a2");
        AssertEqual(true, renameResult.Applied);
        AssertEqual("案1a2", workspace.SelectedPlan.Name);
        var restored = new ProjectWorkspace(ProjectJsonSerializer.Load(ProjectJsonSerializer.Save(workspace.Project)));
        restored.SelectPlan(duplicateId);
        AssertEqual(deskId, restored.SelectedDeskLayoutId);
        AssertEqual("案1a2", restored.GetSelectedPlanSnapshot().PlanName);
        AssertEqual(2, restored.Project.CircleLayouts.Count);
        AssertEqual(1, restored.Project.DeskLayouts.Count);
        AssertEqual(source.Assignments.Count, restored.SelectedPlan.Assignments.Count);
    }

    private static void OptimizedPlanKeepsDeskBinding()
    {
        var workspace = new ProjectWorkspace(CreateProject());
        var commands = new EditorCommandController(workspace);
        var deskId = workspace.SelectedDeskLayoutId;
        var source = workspace.SelectedPlan;
        var score = new CirclePlacementOptimizationScore(0, 0);
        var result = new CirclePlacementOptimizationResult(source with { Assignments = [] }, score, score, 0, 0, false);
        AssertEqual(true, commands.AddOptimizedPlan(result).Applied);
        AssertEqual(deskId, workspace.SelectedDeskLayoutId);
        AssertEqual(2, workspace.Project.CircleLayouts.Count);
        AssertEqual(0, workspace.GetSelectedPlanSnapshot().Assignments.Count);
        AssertEqual(source.Assignments.Count, workspace.Project.Plans.Single(plan => plan.Id == source.Id).Assignments.Count);
        var restored = new ProjectWorkspace(ProjectJsonSerializer.Load(ProjectJsonSerializer.Save(workspace.Project)), workspace.SelectedPlanId);
        AssertEqual(deskId, restored.SelectedDeskLayoutId);
        AssertEqual(0, restored.GetSelectedPlanSnapshot().Assignments.Count);
        AssertEqual(1, restored.Project.DeskLayouts.Count);
    }

    private static void UnusedDeskCanBeEditedAndSaved()
    {
        var workspace = new ProjectWorkspace(LayoutCatalogService.CreateDeskLayout(CreateProject(), "unused", "Unused"));
        workspace.SelectDeskLayout("unused");
        var commands = new EditorCommandController(workspace);
        AssertEqual(true, commands.AddDeskAt(new GridPosition(0, 0)).Applied);
        AssertEqual(false, commands.AddDeskAt(new GridPosition(0, 0)).Applied);
        AssertEqual(1, workspace.GetSelectedPlanSnapshot().Desks.Count);
        var placement = new ParticipantPlacementController(workspace);
        AssertEqual(false, placement.PlaceParticipantAt(workspace.Project.Participants[0].Id, new GridPosition(0, 0)).Applied);
        var viewport = new GridViewport(32d);
        var drag = new DeskDragController(workspace, viewport);
        AssertEqual(true, drag.BeginDrag(new ScreenPoint(8, 8)));
        drag.UpdateDrag(new ScreenPoint(8, 40));
        AssertEqual(true, drag.Drop().Applied);
        AssertEqual(new GridPosition(0, 1), workspace.GetSelectedPlanSnapshot().Desks[0].Anchor);
        var path = Path.Combine(Path.GetTempPath(), $"unused-desk-{Guid.NewGuid():N}.json");
        try
        {
            ProjectFileService.Save(path, workspace.Project);
            var loaded = new ProjectWorkspace(ProjectFileService.Load(path));
            loaded.SelectDeskLayout("unused");
            AssertEqual(1, loaded.Project.CircleLayouts.Count);
            AssertEqual(1, loaded.Project.Plans.Count);
            AssertEqual(true, loaded.CanRemoveSelectedDeskLayout);
            AssertEqual(new GridPosition(0, 1), loaded.GetSelectedPlanSnapshot().Desks[0].Anchor);
            loaded.ApplyProjectEdit(project => LayoutCatalogService.RemoveDeskLayout(project, "unused"));
            AssertEqual(1, loaded.Project.DeskLayouts.Count);
            AssertEqual(true, loaded.HasSelectedCircleLayout);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static void ExportPlanDecision()
    {
        var project = CreateProject();
        var first = project.Plans[0] with
        {
            DeskPlacements = [project.Plans[0].DeskPlacements[0] with { DeskNumber = "01" }],
            SeatLabels = [new DeskSeatLabel("desk-1", new(0, 0), "ア", "01左")],
        };
        var second = first with { Id = "plan-2", Name = "決定候補", SeatLabels = [new DeskSeatLabel("desk-1", new(0, 0), "イ", "02左")],
            DeskPlacements = [first.DeskPlacements[0] with { DeskNumber = "02" }] };
        project = project with { Plans = [first, second] };
        AssertEqual<Plan?>(null, CircleSeatExportBuilder.GetExportPlan(project));
        var rejected = false;
        try { CircleSeatExportBuilder.BuildDecided(project); } catch (InvalidOperationException) { rejected = true; }
        AssertEqual(true, rejected);
        var connection = CircleSpaceCoordinator.EditorClient.EditorConnection.Current!;
        var remote = connection.Open(ProjectJsonSerializer.Save(project));
        remote.Execute(new CircleSpaceCoordinator.Engine.Model.SetExportPlan("plan-2"), false);
        remote.SelectPlan("plan-1");
        AssertEqual("plan-1", remote.SelectedPlan.Id);
        AssertEqual("plan-2", remote.Project.ExportPlanId);
        var rows = CircleSeatExportBuilder.BuildDecided(remote.Project);
        AssertEqual("イ", rows.Single().BlockName);
        AssertEqual("02", rows.Single().SeatName);
        var restored = ProjectJsonSerializer.Load(connection.Encode(remote.Project));
        AssertEqual("plan-2", restored.ExportPlanId);
        remote.Undo();
        AssertEqual<string?>(null, remote.Project.ExportPlanId);
        remote.Redo();
        AssertEqual("plan-2", remote.Project.ExportPlanId);
        remote.Execute(new CircleSpaceCoordinator.Engine.Model.LayoutCatalogServiceRemoveCircleLayout("plan-2"), false);
        AssertEqual<string?>(null, remote.Project.ExportPlanId);
        remote.Undo();
        AssertEqual("plan-2", remote.Project.ExportPlanId);
        remote.Execute(new CircleSpaceCoordinator.Engine.Model.SetExportPlan(null), false);
        AssertEqual<Plan?>(null, CircleSeatExportBuilder.GetExportPlan(remote.Project));
        remote.Undo();
        remote.Execute(new CircleSpaceCoordinator.Engine.Model.PlanCatalogServiceRemovePlan("plan-2"), false);
        AssertEqual<string?>(null, remote.Project.ExportPlanId);
        AssertEqual<Plan?>(null, CircleSeatExportBuilder.GetExportPlan(project with { ExportPlanId = "missing" }));
    }

    private static void ParticipantTableImportRoundTrip()
    {
        var project = CreateProject();
        var legacy = new ParticipantTableView(project);
        AssertEqual(project.Participants[0].DisplayName, legacy.GetValue(0, 1));
        var connection = CircleSpaceCoordinator.EditorClient.EditorConnection.Current!;
        var remote = connection.Open(ProjectJsonSerializer.Save(project));
        var source = new ParticipantTableSource("架空.csv", "架空", ["ID", "名前", "", "重み", "重み"],
            ["ID", "名前", "[3] ", "[4] 重み", "[5] 重み"]);
        var values = new Dictionary<string, string>
        {
            ["ID"] = "001", ["名前"] = "架空サークル", ["[3] "] = "複数行\nの値",
            ["[4] 重み"] = "0.125", ["[5] 重み"] = "0.75",
        };
        remote.Execute(new CircleSpaceCoordinator.Engine.Model.ParticipantCatalogServiceReplaceParticipants(
            [new CircleSpaceCoordinator.Application.Participants.ParticipantImportRow("001", "架空サークル", 1) { SourceValues = values }], source), false);
        var saved = ProjectJsonSerializer.Load(connection.Encode(remote.Project));
        var table = new ParticipantTableView(saved);
        AssertEqual(5, table.ColumnCount);
        AssertEqual("", table.Headers[2]);
        AssertEqual("0.125", table.GetValue(0, 3));
        AssertEqual("0.75", table.GetValue(0, 4));
        AssertEqual("複数行\nの値", table.GetValue(0, 2));
        AssertEqual("架空.csv", saved.ParticipantTableSource!.FileName);
        remote.Undo();
        AssertEqual<ParticipantTableSource?>(null, remote.Project.ParticipantTableSource);
        AssertEqual(project.Participants[0].DisplayName, remote.Project.Participants[0].DisplayName);
        remote.Redo();
        AssertEqual("0.75", new ParticipantTableView(remote.Project).GetValue(0, 4));
        // An import without metadata must clear stale origin information.
        remote.Execute(new CircleSpaceCoordinator.Engine.Model.ParticipantCatalogServiceReplaceParticipants(
            [new CircleSpaceCoordinator.Application.Participants.ParticipantImportRow("002", "別の架空サークル", 1)]), false);
        AssertEqual<ParticipantTableSource?>(null, remote.Project.ParticipantTableSource);
        var empty = new ParticipantTableView(project with { Participants = [] });
        AssertEqual(0, empty.RowCount);
        var oldColumns = new ParticipantTableView(saved with { ParticipantTableSource = null });
        AssertEqual(5, oldColumns.ColumnCount);
    }

    private static void LargeParticipantTableViewport()
    {
        var keys = Enumerable.Range(0, 100).Select(i => $"列{i}").ToArray();
        var participants = Enumerable.Range(0, 10000).Select(row =>
            new Participant($"p{row}", $"架空{row}", 1, new Dictionary<string, double>())
            {
                SourceValues = keys.Select((key, column) => (key, value: $"{row}:{column}"))
                    .ToDictionary(item => item.key, item => item.value),
            }).ToArray();
        var project = CreateProject() with { Participants = participants,
            ParticipantTableSource = new ParticipantTableSource("性能確認.csv", "確認", keys, keys) };
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var table = new ParticipantTableView(project);
        var scroll = new TableScrollPosition();
        scroll.MoveTo(int.MaxValue, int.MaxValue, table.RowCount, table.ColumnCount, 30, 10);
        AssertEqual(9970, scroll.Row);
        AssertEqual(90, scroll.Column);
        AssertEqual("9999:99", table.GetValue(scroll.Row + 29, scroll.Column + 9));
        var checksum = 0;
        for (var page = 0; page < 10000; page += 30)
        {
            scroll.MoveTo(page, 90, table.RowCount, table.ColumnCount, 30, 10);
            for (var r = 0; r < 30; r++)
                for (var c = 0; c < 10; c++) checksum += table.GetValue(scroll.Row + r, scroll.Column + c).Length;
        }
        if (checksum == 0) throw new InvalidOperationException("No visible values read.");
        timer.Stop();
        var bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        if (bytes > 1_000_000) throw new InvalidOperationException($"Table view copied too much data: {bytes} bytes");
        Console.WriteLine($"Table view only, 10000 x 100: {timer.ElapsedMilliseconds} ms; {bytes} allocated bytes (excludes data creation and GPU drawing)");
        scroll.MoveTo(-1, -1, 0, 0, 30, 10);
        AssertEqual(0, scroll.Row);
        AssertEqual(0, scroll.Column);
        scroll.MoveTo(9999, 99, 10000, 100, 20000, 200);
        AssertEqual(0, scroll.Row);
        AssertEqual(0, scroll.Column);
    }

    private static CircleSpaceProject CreateProject()
    {
        var deskType = new DeskType(
            "standard-desk",
            "架空の標準机",
            [new GridPosition(0, 0), new GridPosition(1, 0)]);
        var participant = new Participant(
            "fictional-circle-001",
            "架空サークル１",
            2,
            new Dictionary<string, double>());
        return new CircleSpaceProject(
            "1.0",
            "fictional-project",
            "架空プロジェクト",
            new Venue("fictional-venue", "架空会場", 5, 4, new HashSet<GridPosition>()),
            [deskType],
            [participant],
            new EvaluationConfiguration([], []),
            [new Plan(
                "plan-1",
                "架空配置案１",
                [new DeskPlacement("desk-1", deskType.Id, new GridPosition(0, 0), QuarterTurn.North)],
                [new ParticipantAssignment(
                    participant.Id,
                    new HashSet<GridPosition> { new(0, 0), new(1, 0) },
                    new GridPosition(0, 0))])]);
    }

    private static CircleSpaceProject CreateAssignmentProject(int requiredCellCount)
    {
        var deskType = new DeskType(
            "standard-desk",
            "架空の標準机",
            [new GridPosition(0, 0), new GridPosition(1, 0)]);
        var participants = new[]
        {
            new Participant("fictional-circle-001", "架空サークル１", 1, new Dictionary<string, double>()),
            new Participant("fictional-circle-002", "架空サークル２", requiredCellCount, new Dictionary<string, double>()),
        };
        return new CircleSpaceProject(
            "1.0",
            "fictional-assignment-project",
            "架空割当プロジェクト",
            new Venue("fictional-venue", "架空会場", 4, 1, new HashSet<GridPosition>()),
            [deskType],
            participants,
            new EvaluationConfiguration([], []),
            [new Plan(
                "plan-1",
                "架空配置案１",
                [
                    new DeskPlacement("desk-1", deskType.Id, new GridPosition(0, 0), QuarterTurn.North),
                    new DeskPlacement("desk-2", deskType.Id, new GridPosition(2, 0), QuarterTurn.North),
                ],
                [new ParticipantAssignment(
                    participants[0].Id,
                    new HashSet<GridPosition> { new(0, 0) },
                    new GridPosition(0, 0))])]);
    }

    private static GridPosition CurrentAnchor(ProjectWorkspace workspace) =>
        workspace.SelectedPlan.DeskPlacements.Single().Anchor;

    private static void AssertEqual<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected '{expected}', actual '{actual}'.");
    }
}
