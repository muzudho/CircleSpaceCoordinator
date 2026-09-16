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
            ("Dialog validation distinguishes clearing numbers from empty names and rejects invalid weights", DialogValidation),
            ("Participant cell selection preserves Unicode and copies reversed multiline ranges", ParticipantCellSelection),
            ("Frame numbers support bulk input and clearing with one undo", BulkFrameNumbers),
            ("Frame layout order survives remote operations, history and save without changing bindings", FrameLayoutOrder),
            ("Space totals sum requests and count each frame layout independently of assignments and history", SpaceTotals),
            ("Number channel gaps distinguish frames from seats and follow independent edits and history", MissingNumberChannels),
            ("Vacancy markers count physical seats and follow placement, parking and history", VacantPhysicalSeats),
            ("A missing circle layout reports the reason and creating one enables placement", MissingCircleLayoutCanBeCreated),
            ("Genre appearance drafts validate colors, preserve unused styles and apply as one undoable edit", GenreAppearanceDrafts),
            ("Shared appearance mappings accept block numbers and preserve isolated edits and unused styles", BlockAppearanceDrafts),
            ("Block mappings collect frame labels and survive remote history and JSON", BlockStylesRoundTrip),
            ("Block view repeats labels every four cells and respects rotated seat footprints", BlockChannelDisplay),
            ("Cell number wizard orders rotated cells, validates lists and applies one undoable edit", CellNumberWizardEdits),
            ("Cell numbering restarts per frame even when venue order interleaves frames", CellNumberFrameRepeat),
            ("Island paths use seat cells while facing rectangles retain physical cells", IslandPathsUseSeats),
            ("Frame-defined connections survive editing, rotation, catalog and event persistence", FrameDefinedConnections),
            ("Address swaps preserve other channels and physical frames through remote history and JSON", AddressSwaps),
            ("Deleting an occupied frame can unassign circles across shared layouts and undo all changes", OccupiedFrameDeletion),
            ("Frame drafts isolate edits, retain hidden cells until save and validate references", FrameDraftEdits),
            ("Returning to events saves or discards and closes the engine workspace; cancel and save failure keep it open", EventProjectCloseTransitions),
            ("Block cell and frame numbers can be edited independently and survive history", IndependentNumberChannels),
            ("Catalog spaces place in all directions and preserve snapshots through history and JSON", CatalogSpacesRoundTrip),
            ("Space definitions persist globally and reject broken references and stale saves", SpaceDefinitionsPersist),
            ("Export requires an explicit decision and preserves it independently of editor selection", ExportPlanDecision),
            ("Export choices rank both scores, retain duplicate names and filter pinned IDs without editing", ExportChoicesAndPins),
            ("Imported table survives remote import, save, undo and redo with ordered duplicate headers", ParticipantTableImportRoundTrip),
            ("Table viewport reaches the last cell of 10000 by 100 without copying values", LargeParticipantTableViewport),
            ("Output table displays destination columns and rows independently of participants", OutputTableView),
            ("New export preserves imported columns and distinguishes source changes from plan changes", NewExportSource),
            ("Export column drafts move bindings, append columns and leave cancelled sources untouched", ExportColumnDrafts),
            ("Space numbers use full-frame occupancy and mark partial multi-cell assignments undefined", FullFrameSpaceNumbers),
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

    private static void FrameLayoutOrder()
    {
        var project = LayoutCatalogService.CreateDeskLayout(CreateProject(), "second", "Second");
        project = LayoutCatalogService.CreateDeskLayout(project, "third", "Third");
        using var remote = CircleSpaceCoordinator.EditorClient.EditorConnection.Current!.Open(ProjectJsonSerializer.Save(project));
        remote.SelectDeskLayout("third");
        var first = remote.Project.DeskLayouts[0].Id;
        remote.Execute(new CircleSpaceCoordinator.Engine.Model.LayoutCatalogServiceMoveDeskLayout("third", -1), selectedPlanEdit: false);
        AssertEqual($"{first},third,second", string.Join(",", remote.Project.DeskLayouts.Select(item => item.Id)));
        AssertEqual("third", remote.SelectedDeskLayoutId);
        remote.Undo();
        AssertEqual($"{first},second,third", string.Join(",", remote.Project.DeskLayouts.Select(item => item.Id)));
        remote.Redo();
        var restored = ProjectJsonSerializer.Load(ProjectJsonSerializer.Save(remote.Project));
        AssertEqual($"{first},third,second", string.Join(",", restored.DeskLayouts.Select(item => item.Id)));
        AssertEqual(first, restored.CircleLayouts.Single().DeskLayoutId);
        AssertEqual(true, restored.CircleLayouts.Single().Assignments.Count > 0);
        var unchanged = LayoutCatalogService.MoveDeskLayout(restored, first, -1);
        AssertEqual(true, ReferenceEquals(restored, unchanged));
        var movedDown = LayoutCatalogService.MoveDeskLayout(restored, "third", 1);
        AssertEqual($"{first},second,third", string.Join(",", movedDown.DeskLayouts.Select(item => item.Id)));
    }

    private static void SpaceTotals()
    {
        var project = CreateAssignmentProject(2);
        var totals = SpaceCapacitySummary.Calculate(project);
        AssertEqual(3L, totals.Requested);
        AssertEqual(4L, totals.Layouts["plan-1"]);
        var type = project.DeskTypes[0];
        project = project with { DeskTypes = [type with
        {
            Space = new("custom", "机", 2, 1, [new(0, 0, 1), new(1, 0, 0)], ["開放", "開放", "開放", "開放"]),
        }] };
        AssertEqual(2L, SpaceCapacitySummary.Calculate(project).Layouts["plan-1"]);
        AssertEqual(2L, SpaceCapacitySummary.Calculate(project with
        {
            Plans = [project.Plans[0] with { Assignments = [] }],
        }).Layouts["plan-1"]);

        var workspace = new ProjectWorkspace(CreateProject());
        var commands = new EditorCommandController(workspace);
        AssertEqual(true, commands.DuplicateSelectedPlan("Another circle layout").Applied);
        totals = SpaceCapacitySummary.Calculate(workspace.Project);
        AssertEqual(1, totals.Layouts.Count);
        AssertEqual(2L, totals.Layouts.Values.Single());
        AssertEqual(true, commands.AddDeskAt(new(2, 0)).Applied);
        AssertEqual(4L, SpaceCapacitySummary.Calculate(workspace.Project).Layouts.Values.Single());
        AssertEqual(true, commands.Undo());
        AssertEqual(2L, SpaceCapacitySummary.Calculate(workspace.Project).Layouts.Values.Single());
        var expanded = LayoutCatalogService.CreateDeskLayout(workspace.Project, "empty", "Empty");
        totals = SpaceCapacitySummary.Calculate(expanded);
        AssertEqual(2, totals.Layouts.Count);
        AssertEqual(0L, totals.Layouts["empty"]);
        AssertEqual(2L, totals.Requested);
        AssertEqual(0L, SpaceCapacitySummary.Calculate(expanded with { Participants = [] }).Requested);
    }

    private static void MissingNumberChannels()
    {
        var project = CreateProject();
        var type = new DeskType("custom", "Custom", [new(0, 0), new(1, 0), new(2, 0)])
        {
            Space = new("custom", "机", 3, 1, [new(0, 0, 1), new(1, 0, 0), new(2, 0, 1)],
                ["開放", "開放", "開放", "開放"]),
        };
        project = project with { DeskTypes = [type] };
        var plan = new Plan("test", "Test", [new("desk", type.Id, new(2, 1), QuarterTurn.East)], [])
        {
            SeatLabels = [new("desk", new(0, 0), "A", " "), new("desk", new(2, 0), "", "2")],
        };
        AssertEqual(new NumberChannelGaps(1, 1, 1), NumberChannelGaps.Find(project, plan));
        AssertEqual(new NumberChannelGaps(0, 0, 0), NumberChannelGaps.Find(project, plan with
        {
            DeskPlacements = [plan.DeskPlacements[0] with { DeskNumber = "1" }],
            SeatLabels = [new("desk", new(0, 0), "A", "1"), new("desk", new(2, 0), "A", "2")],
        }));
        var noSeats = project with { DeskTypes = [type with { Space = type.Space! with
        {
            Cells = type.Space.Cells.Select(cell => cell with { Area = 0 }).ToArray(),
        } }] };
        AssertEqual(new NumberChannelGaps(0, 1, 0), NumberChannelGaps.Find(noSeats, plan));
        AssertEqual(new NumberChannelGaps(0, 0, 0), NumberChannelGaps.Find(project, plan with { DeskPlacements = [] }));

        var workspace = new ProjectWorkspace(CreateProject());
        NumberChannelGaps Gaps() => NumberChannelGaps.Find(workspace.Project, workspace.SelectedPlan);
        AssertEqual(new NumberChannelGaps(2, 1, 2), Gaps());
        var commands = new EditorCommandController(workspace);
        AssertEqual(true, commands.SetDeskNumber("desk-1", "10").Applied);
        AssertEqual(new NumberChannelGaps(2, 0, 2), Gaps());
        AssertEqual(true, commands.ReplaceSeatLabels([new("desk-1", new(0, 0), "A", "1"), new("desk-1", new(1, 0), "A", "2")]).Applied);
        AssertEqual(new NumberChannelGaps(0, 0, 0), Gaps());
        AssertEqual(true, commands.Undo());
        AssertEqual(new NumberChannelGaps(2, 0, 2), Gaps());
        AssertEqual(true, commands.Redo());
        AssertEqual(new NumberChannelGaps(0, 0, 0), Gaps());
    }

    private static void VacantPhysicalSeats()
    {
        var project = CreateProject();
        var type = new DeskType("custom", "Custom", [new(0, 0), new(1, 0), new(2, 0)])
        {
            Space = new("custom", "机", 3, 1,
                [new(0, 0, 1), new(1, 0, 0), new(2, 0, 1)], ["開放", "開放", "開放", "開放"]),
        };
        var desk = new DeskPlacement("custom", type.Id, new(2, 1), QuarterTurn.East);
        var plan = new Plan("vacant", "Vacant", [desk], []);
        project = project with { DeskTypes = [type] };
        var expected = new HashSet<GridPosition>
        {
            desk.Anchor, desk.Anchor + new GridPosition(2, 0).Rotate(QuarterTurn.East),
        };
        AssertEqual(true, CircleLayoutVacancies.Find(project, plan).SetEquals(expected));
        var restricted = project with { Venue = project.Venue with { BlockedCells = new HashSet<GridPosition> { desk.Anchor } } };
        AssertEqual(1, CircleLayoutVacancies.Find(restricted, plan).Count);
        AssertEqual(0, CircleLayoutVacancies.Find(project with { Venue = project.Venue with { Width = 1 } }, plan).Count);
        AssertEqual(0, CircleLayoutVacancies.Find(project, plan with
        {
            Assignments = [new ParticipantAssignment("fictional-circle-001", expected, desk.Anchor)],
        }).Count);

        var workspace = new ProjectWorkspace(CreateAssignmentProject(2));
        int Count() => CircleLayoutVacancies.Find(workspace.Project, workspace.SelectedPlan).Count;
        AssertEqual(3, Count());
        var controller = new ParticipantPlacementController(workspace);
        AssertEqual(true, controller.PlaceParticipantAt("fictional-circle-002", new(2, 0)).Applied);
        AssertEqual(1, Count());
        AssertEqual(true, controller.PlaceParticipantAt("fictional-circle-001", new(1, 0)).Applied);
        AssertEqual(true, CircleLayoutVacancies.Find(workspace.Project, workspace.SelectedPlan).SetEquals([new(0, 0)]));
        AssertEqual(true, controller.ParkParticipantAt("fictional-circle-002", [new(2, 0), new(3, 0)], new(2, 0), new(2, 2)).Applied);
        AssertEqual(3, Count());
        var commands = new EditorCommandController(workspace);
        AssertEqual(true, commands.Undo());
        AssertEqual(1, Count());
        AssertEqual(true, commands.Redo());
        AssertEqual(3, Count());
        var filled = CreateProject();
        AssertEqual(0, CircleLayoutVacancies.Find(filled, filled.Plans[0]).Count);
        AssertEqual(2, CircleLayoutVacancies.Find(filled, filled.Plans[0] with { Assignments = [] }).Count);
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
            Detail: "circleName=fictional-circle-private;circleId=private-001;path=C:\\Users\\private-user\\event.json;message=private-error",
            FailureCode: UiOperationFailure.CircleLayoutRequired));

        AssertEqual(true, json.Contains("\"action\":\"pointer_left_down\"", StringComparison.Ordinal));
        AssertEqual(true, json.Contains("\"screenX\":123", StringComparison.Ordinal));
        AssertEqual(true, json.Contains("\"gridY\":1", StringComparison.Ordinal));
        AssertEqual(false, json.Contains("fictional-circle", StringComparison.Ordinal));
        AssertEqual(false, json.Contains("private-", StringComparison.Ordinal));
        AssertEqual(true, json.Contains("\"detail\":null", StringComparison.Ordinal));
        AssertEqual(true, json.Contains("\"failureCode\":\"CircleLayoutRequired\"", StringComparison.Ordinal));
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

    private static void BlockAppearanceDrafts()
    {
        StyleMappingEntry[] configured = [new("東A", "blue", "white", "checker"), new("未使用", "red", "yellow", "solid")];
        var draft = new StyleMappingDraft(["東B", "東A", "東A", "", " "], configured);
        AssertEqual(2, draft.Rows.Count);
        AssertEqual("東A", draft.Rows[0].Key);
        AssertEqual("thick-grid", draft.Rows[0].Pattern);
        var liveRows = draft.Rows;
        draft.SetColor(0, true, " #12abef ");
        draft.SetPattern(0, "dots");
        AssertEqual("#12ABEF", liveRows[0].PrimaryColor);
        AssertEqual("blue", configured[0].PrimaryColor);
        AssertEqual("checker", configured[0].Pattern);
        var snapshot = draft.Build();
        AssertEqual(configured[1], snapshot[2]);
        draft.SetPattern(0, "solid");
        AssertEqual("dots", snapshot[0].Pattern);
        var rejected = false;
        try { draft.SetPattern(0, "unknown"); } catch (ArgumentException) { rejected = true; }
        AssertEqual(true, rejected);
        AssertEqual("solid", draft.Rows[0].Pattern);
        var empty = new StyleMappingDraft([], configured);
        AssertEqual(0, empty.Rows.Count);
        AssertEqual(true, configured.SequenceEqual(empty.Build()));
        var invalid = new StyleMappingDraft(["東A"], [configured[0] with { PrimaryColor = "invalid" }]);
        rejected = false;
        try { invalid.Build(); } catch (InvalidDataException ex) { rejected = ex.Message.Contains("東A"); }
        AssertEqual(true, rejected);
    }

    private static void CellNumberWizardEdits()
    {
        var original = CreateProject();
        var placement = original.Plans[0].DeskPlacements[0] with { Anchor = new(1, 0), Orientation = QuarterTurn.South, DeskNumber = "frame" };
        var plan = original.Plans[0] with
        {
            DeskPlacements = [placement],
            Assignments = [],
            SeatLabels = [new(placement.Id, new(0, 0), "A", "old1"), new(placement.Id, new(1, 0), "A", "old2")],
        };
        var project = original with { Plans = [plan] };
        var draft = new CellNumberWizard(project, plan);
        AssertEqual(2, draft.Count);
        AssertEqual("1, 2", draft.DefaultNumbers);
        AssertEqual(new GridPosition(0, 0), draft.Targets(CellNumberOrder.VenueTopLeft)[0].Cell);
        AssertEqual(new GridPosition(1, 0), draft.Targets(CellNumberOrder.FrameTopLeft)[0].Cell);
        var updated = draft.Build(CellNumberOrder.FrameTopLeft, " 01, 02 ");
        AssertEqual("01", updated.Single(label => label.RelativeCell == new GridPosition(0, 0)).SeatName);
        AssertEqual(true, updated.All(label => label.BlockName == "A"));
        AssertEqual("old1", plan.SeatLabels[0].SeatName);
        foreach (var input in new[] { "1", "1,2,3", "1, ", "1," + new string('x', 81) })
        {
            var rejected = false;
            try { draft.Build(CellNumberOrder.VenueTopLeft, input); } catch (ArgumentException) { rejected = true; }
            AssertEqual(true, rejected);
        }
        var partial = new CellNumberWizard(project, plan, cell => cell.X == 0);
        var partialLabels = partial.Build(CellNumberOrder.FrameTopLeft, "X");
        AssertEqual("old1", partialLabels.Single(label => label.RelativeCell == new GridPosition(0, 0)).SeatName);
        AssertEqual("X", partialLabels.Single(label => label.RelativeCell == new GridPosition(1, 0)).SeatName);
        var workspace = new ProjectWorkspace(project);
        var commands = new EditorCommandController(workspace);
        AssertEqual(true, commands.ReplaceSeatLabels(updated).Applied);
        AssertEqual("frame", workspace.SelectedPlan.DeskPlacements[0].DeskNumber);
        AssertEqual(true, commands.Undo());
        AssertEqual("old1", workspace.SelectedPlan.SeatLabels[0].SeatName);
        AssertEqual(true, commands.Redo());
        AssertEqual("01", workspace.SelectedPlan.SeatLabels.Single(label => label.RelativeCell == new GridPosition(0, 0)).SeatName);
        var reloaded = ProjectJsonSerializer.Load(ProjectJsonSerializer.Save(workspace.Project));
        AssertEqual("01", reloaded.Plans[0].SeatLabels.Single(label => label.RelativeCell == new GridPosition(0, 0)).SeatName);
    }

    private static void CellNumberFrameRepeat()
    {
        var project = CreateProject();
        var source = project.Plans[0];
        var frames = Enumerable.Range(0, 3).Select(index => source.DeskPlacements[0] with
        {
            Id = $"frame-{index}", Anchor = new(index * 2, 0), Orientation = QuarterTurn.East,
        }).ToArray();
        var plan = source with { DeskPlacements = frames, Assignments = [], SeatLabels = [] };
        var draft = new CellNumberWizard(project, plan);
        AssertEqual(6, draft.Count);
        AssertEqual("1, 2", draft.DefaultFrameNumbers);
        foreach (var order in Enum.GetValues<CellNumberOrder>())
        {
            var built = draft.Build(order, "01, 02", repeatPerFrame: true);
            foreach (var frame in frames)
            {
                AssertEqual("01", built.Single(label => label.DeskPlacementId == frame.Id && label.RelativeCell == new GridPosition(0, 0)).SeatName);
                AssertEqual("02", built.Single(label => label.DeskPlacementId == frame.Id && label.RelativeCell == new GridPosition(1, 0)).SeatName);
            }
            var assigned = draft.AssignedNumbers(order, "01, 02", true);
            var targets = draft.Targets(order);
            for (var i = 0; i < targets.Count; i++)
                AssertEqual(assigned[i], built.Single(label => label.DeskPlacementId == targets[i].FrameId && label.RelativeCell == targets[i].RelativeCell).SeatName);
        }
        var partial = new CellNumberWizard(project, plan, cell => cell.X == 0 || cell.Y == 0);
        AssertEqual(4, partial.Count);
        AssertEqual(2, partial.NumbersPerFrame);
        AssertEqual("01", partial.Build(CellNumberOrder.VenueTopLeft, "01,02", true).Single(label => label.DeskPlacementId == "frame-1").SeatName);
        foreach (var text in new[] { "1", "1,2,3", "1," })
        {
            var rejected = false;
            try { draft.Build(CellNumberOrder.FrameTopLeft, text, true); } catch (ArgumentException) { rejected = true; }
            AssertEqual(true, rejected);
        }
        AssertEqual(0, plan.SeatLabels.Count);
    }

    private static void IslandPathsUseSeats()
    {
        var type = new DeskType("seats", "Seats", [new(0, 0), new(1, 0), new(2, 0)])
        {
            Space = new("seats", "ブース", 3, 1, [new(0, 0, 1), new(1, 0, 0), new(2, 0, 1)], ["開放", "開放", "開放", "開放"]),
        };
        var empty = type with { Id = "empty", Space = type.Space with { Cells = [new(0, 0, 0), new(1, 0, 0), new(2, 0, 0)] } };
        var plan = new Plan("plan", "Plan", [new("a", type.Id, new(0, 0), QuarterTurn.North),
            new("b", type.Id, new(3, 0), QuarterTurn.North), new("empty", empty.Id, new(6, 0), QuarterTurn.North)], []);
        var project = CreateProject() with { DeskTypes = [type, empty], Plans = [plan],
            Venue = new("venue", "Venue", 10, 10, new HashSet<GridPosition>()) };
        var types = project.DeskTypes.ToDictionary(item => item.Id);
        var graph = VenueTopologyAnalyzer.Build(project, plan);
        AssertEqual(4, graph.Neighbors.Count);
        AssertEqual(false, graph.Neighbors.ContainsKey(new(1, 0)));
        foreach (var edge in VenueTopologyAnalyzer.GetAutomaticCellEdges(plan, types))
        {
            AssertEqual(true, graph.Neighbors.ContainsKey(edge.FirstCell));
            AssertEqual(true, graph.Neighbors.ContainsKey(edge.SecondCell));
        }
        AssertEqual(project, VenueTopologyEditor.AddConnector(project, plan.Id, "a", "b", new(1, 0), new(3, 0)));
        AssertEqual(project, VenueTopologyEditor.AddConnector(project, plan.Id, "a", "empty"));
        var linked = VenueTopologyEditor.AddConnector(project, plan.Id, "a", "b", new(0, 0), new(5, 0));
        AssertEqual(true, VenueTopologyAnalyzer.Build(linked, linked.Plans[0]).Neighbors[new(0, 0)].Contains(new(5, 0)));
        var legacy = plan with { IslandConnectors = [new("old", "a", "b", new(1, 0), new(4, 0)), new("no-seat", "a", "empty")] };
        AssertEqual(4, VenueTopologyAnalyzer.Build(project, legacy).Neighbors.Count);
        AssertEqual(true, VenueTopologyAnalyzer.ResolveConnectorCells(plan, types, legacy.IslandConnectors[0]) is null);
        var facing = plan with
        {
            DeskPlacements = [new("a", type.Id, new(0, 0), QuarterTurn.East), new("b", type.Id, new(4, 2), QuarterTurn.West)],
            FacingRegions = [new("facing", new(0, 0), new(4, 2))],
        };
        var facingGraph = VenueTopologyAnalyzer.Build(project, facing);
        AssertEqual(false, facingGraph.Neighbors.ContainsKey(new(0, 1)));
        AssertEqual(true, facingGraph.FacingCellPairs.Contains((new GridPosition(0, 1), new GridPosition(4, 1))));
    }

    private static void FrameDefinedConnections()
    {
        var source = SpaceDefinitionCatalog.CreateDefault().Types.Single(type => type.Id == "desk-3-ends");
        var draft = new SpaceDefinitionDraft(source);
        var first = new GridPosition(0, 0);
        var last = new GridPosition(2, 0);
        AssertEqual(0, draft.Connections.Count);
        draft.ToggleConnection(first, last);
        AssertEqual(1, draft.Connections.Count);
        AssertEqual(true, source.Connections is null);
        draft.ToggleConnection(last, first);
        AssertEqual(0, draft.Connections.Count);
        draft.ToggleConnection(first, last);
        draft.Resize(2, 1);
        AssertEqual(0, draft.BuildType().Connections!.Count);
        draft.Resize(3, 1);
        AssertEqual(1, draft.Connections.Count);
        var definition = draft.BuildType();
        draft.Paint(2, 0, 0);
        AssertEqual(0, draft.Connections.Count);
        AssertEqual(1, definition.Connections!.Count);
        var invalid = false;
        try { draft.ToggleConnection(first, last); } catch (ArgumentException) { invalid = true; }
        AssertEqual(true, invalid);
        var type = SpaceTypeFactory.Create(definition);
        AssertEqual(false, type.Id == SpaceTypeFactory.Create(source).Id);
        foreach (var orientation in Enum.GetValues<QuarterTurn>())
        {
            var frame = new DeskPlacement("frame", type.Id, new(5, 5), orientation);
            var plan = new Plan("plan", "Plan", [frame], []);
            var project = CreateProject() with { DeskTypes = [type], Plans = [plan],
                Venue = new("venue", "Venue", 12, 12, new HashSet<GridPosition>()) };
            var a = frame.Anchor + first.Rotate(orientation);
            var b = frame.Anchor + last.Rotate(orientation);
            AssertEqual(true, VenueTopologyAnalyzer.Build(project, plan).Neighbors[a].Contains(b));
            var disabled = VenueTopologyEditor.ToggleAutomaticConnection(project, plan.Id, a, b);
            AssertEqual(false, VenueTopologyAnalyzer.Build(disabled, disabled.Plans[0]).Neighbors[a].Contains(b));
            var restored = ProjectJsonSerializer.Load(ProjectJsonSerializer.Save(project));
            AssertEqual(definition.Connections[0], restored.DeskTypes[0].Space!.Connections![0]);
            AssertEqual(true, VenueTopologyAnalyzer.Build(restored, restored.Plans[0]).Neighbors[a].Contains(b));
        }
        var adjacent = new SpaceDefinitionDraft(SpaceDefinitionCatalog.CreateDefault().Types[0]);
        AssertEqual(1, adjacent.Connections.Count);
        adjacent.ToggleConnection(new(0, 0), new(1, 0));
        AssertEqual(0, adjacent.BuildType().Connections!.Count);
        adjacent.ResetAdjacentConnections();
        AssertEqual(true, adjacent.BuildType().Connections is null);
        AssertEqual(1, adjacent.Connections.Count);
        foreach (var links in new FrameCellConnection[][]
        {
            [new(first, first)], [new(first, new(1, 0))], [new(first, last), new(last, first)],
        })
        {
            invalid = false;
            try { new SpaceDefinitionCatalog([definition with { Connections = links }], []).Validate(); }
            catch (InvalidDataException) { invalid = true; }
            AssertEqual(true, invalid);
        }
        var directory = Path.Combine(Path.GetTempPath(), $"frame-links-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "definitions.json");
            new SpaceDefinitionStore(path).Save(new([definition], []));
            var loaded = new SpaceDefinitionStore(path).Current.Types[0];
            AssertEqual(definition.Connections[0], loaded.Connections![0]);
            AssertEqual(type.Id, SpaceTypeFactory.Create(loaded).Id);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static void AddressSwaps()
    {
        var original = CreateAssignmentProject(1);
        var originalPlan = original.Plans[0];
        var plan = originalPlan with
        {
            DeskPlacements = [originalPlan.DeskPlacements[0] with { DeskNumber = "F1" },
                originalPlan.DeskPlacements[1] with { DeskNumber = "F2", Anchor = new(3, 0), Orientation = QuarterTurn.South }],
            SeatLabels = [new("desk-1", new(0, 0), "A", "1"), new("desk-1", new(1, 0), "A", "2"),
                new("desk-2", new(0, 0), "B", "7"), new("desk-2", new(1, 0), "B", "8")],
        };
        var project = original with { Plans = [plan] };
        var directory = Path.Combine(Path.GetTempPath(), $"address-swaps-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            foreach (var channel in new[] { 0, 1, 2 })
            {
                var path = Path.Combine(directory, $"{channel}.json");
                ProjectFileService.Save(path, project);
                using var remote = DesktopApplication.LoadWorkspace(path);
                var commands = new EditorCommandController(remote);
                var before = ProjectJsonSerializer.Save(remote.Project);
                AssertEqual(true, commands.SwapAddresses(channel, new(0, 0), new(2, 0), 2, 1, ["desk-1"]).Applied);
                var edited = remote.SelectedPlan;
                var left = edited.SeatLabels.Single(label => label.DeskPlacementId == "desk-1" && label.RelativeCell == new GridPosition(0, 0));
                AssertEqual(channel == 0 ? "B" : "A", left.BlockName);
                AssertEqual(channel == 2 ? "8" : "1", left.SeatName);
                AssertEqual(channel == 1 ? "F2" : "F1", edited.DeskPlacements[0].DeskNumber);
                AssertEqual(true, plan.DeskPlacements.Select(frame => (frame.Id, frame.Anchor, frame.Orientation))
                    .SequenceEqual(edited.DeskPlacements.Select(frame => (frame.Id, frame.Anchor, frame.Orientation))));
                AssertEqual(true, edited.Assignments[0].OccupiedCells.SetEquals(plan.Assignments[0].OccupiedCells));
                var after = ProjectJsonSerializer.Save(remote.Project);
                remote.Undo();
                AssertEqual(before, ProjectJsonSerializer.Save(remote.Project));
                remote.Redo();
                AssertEqual(after, ProjectJsonSerializer.Save(remote.Project));
                ProjectFileService.Save(path, remote.Project);
                AssertEqual(after, ProjectJsonSerializer.Save(ProjectFileService.Load(path)));
                AssertEqual(false, commands.SwapAddresses(channel, new(0, 0), new(3, 0), 2, 1, ["desk-1"]).Applied);
                AssertEqual(after, ProjectJsonSerializer.Save(remote.Project));
            }
        }
        finally { Directory.Delete(directory, recursive: true); }
        var emptyPlan = plan with { SeatLabels = [new("desk-1", new(0, 0), "A", "")] };
        var moved = AddressSwapEditor.Swap(project with { Plans = [emptyPlan] }, plan.Id, 0, new(0, 0), new(2, 0), 1, 1, []);
        AssertEqual(1, moved.Plans[0].SeatLabels.Count);
        AssertEqual("desk-2", moved.Plans[0].SeatLabels[0].DeskPlacementId);
        AssertEqual(new GridPosition(1, 0), moved.Plans[0].SeatLabels[0].RelativeCell);
        var four = plan with
        {
            DeskPlacements = Enumerable.Range(0, 4).Select(i => new DeskPlacement($"d{i}", plan.DeskPlacements[0].DeskTypeId,
                new(i * 2, 0), QuarterTurn.North) { DeskNumber = $"N{i}" }).ToArray(),
            SeatLabels = [], Assignments = [],
        };
        var large = project with { Plans = [four], Venue = project.Venue with { Width = 8 } };
        var swapped = AddressSwapEditor.Swap(large, four.Id, 1, new(0, 0), new(4, 0), 4, 1, ["d1", "d0"]);
        AssertEqual("N2,N3,N0,N1", string.Join(",", swapped.Plans[0].DeskPlacements.Select(frame => frame.DeskNumber)));
        foreach (var channel in new[] { 0, 1, 2 })
        {
            var rejected = false;
            try { AddressSwapEditor.Swap(project, plan.Id, channel, new(0, 0), new(1, 0), 2, 1, ["desk-1"]); }
            catch (CircleSpaceCoordinator.Core.Validation.ProjectValidationException) { rejected = true; }
            AssertEqual(true, rejected);
        }
    }

    private static void OccupiedFrameDeletion()
    {
        var source = CreateAssignmentProject(1);
        var project = source with
        {
            Plans = [source.Plans[0] with
            {
                SeatLabels = [new("desk-1", new(0, 0), "A", "1"), new("desk-2", new(0, 0), "B", "2")],
                IslandConnectors = [new("link", "desk-1", "desk-2", new(1, 0), new(2, 0))],
                DisabledIslandConnections = [new(new(0, 0), new(1, 0))],
            }],
        };
        var directory = Path.Combine(Path.GetTempPath(), $"frame-deletion-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "event.json");
            ProjectFileService.Save(path, project);
            using var remote = DesktopApplication.LoadWorkspace(path);
            var commands = new EditorCommandController(remote);
            AssertEqual(true, commands.DuplicateSelectedPlan().Applied);
            var before = ProjectJsonSerializer.Save(remote.Project);
            AssertEqual(false, commands.RemoveDeskAt(new(0, 0)).Applied);
            AssertEqual(before, ProjectJsonSerializer.Save(remote.Project));
            AssertEqual(true, commands.RemoveDeskAt(new(0, 0), unassignParticipants: true).Applied);
            AssertEqual(2, remote.Project.Plans.Count);
            foreach (var plan in remote.Project.Plans)
            {
                AssertEqual("desk-2", plan.DeskPlacements.Single().Id);
                AssertEqual(0, plan.Assignments.Count);
                AssertEqual("desk-2", plan.SeatLabels.Single().DeskPlacementId);
                AssertEqual(0, plan.IslandConnectors.Count);
                AssertEqual(0, plan.DisabledIslandConnections.Count);
            }
            AssertEqual(source.Participants.Count, remote.Project.Participants.Count);
            var after = ProjectJsonSerializer.Save(remote.Project);
            remote.Undo();
            AssertEqual(before, ProjectJsonSerializer.Save(remote.Project));
            remote.Redo();
            AssertEqual(after, ProjectJsonSerializer.Save(remote.Project));
            ProjectFileService.Save(path, remote.Project);
            AssertEqual(after, ProjectJsonSerializer.Save(ProjectFileService.Load(path)));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static void BlockChannelDisplay()
    {
        var cells = Enumerable.Range(0, 8).SelectMany(y => Enumerable.Range(0, 8).Select(x => new GridPosition(x, y))).ToArray();
        var type = new DeskType("large", "Large", cells);
        var placement = new DeskPlacement("frame", type.Id, new(0, 0), QuarterTurn.North);
        var plan = new Plan("plan", "Plan", [placement], [])
        {
            SeatLabels = cells.Select(cell => new DeskSeatLabel(placement.Id, cell, "A", "")).ToArray(),
        };
        var project = CreateProject() with { DeskTypes = [type], Plans = [plan] };
        var view = BlockChannelView.Create(project, plan);
        AssertEqual(64, view.Cells.Count);
        AssertEqual(4, view.Labels.Count);
        AssertEqual(true, view.Labels.Keys.ToHashSet().SetEquals([new(1, 1), new(5, 1), new(1, 5), new(5, 5)]));
        var mixed = plan with { SeatLabels = plan.SeatLabels.Select(label => label.RelativeCell == new GridPosition(0, 0)
            ? label with { BlockName = "tiny" } : label).ToArray() };
        AssertEqual("tiny", BlockChannelView.Create(project, mixed).Labels[new(0, 0)]);
        AssertEqual(0, BlockChannelView.Create(project, plan with { SeatLabels = [] }).Labels.Count);
        var seatType = type with { Space = new("large", "booth", 8, 8,
            [new(0, 0, 1), new(1, 0, 0), new(2, 0, 1)], ["", "", "", ""]) };
        foreach (var orientation in Enum.GetValues<QuarterTurn>())
        {
            var rotated = placement with { Anchor = new(10, 10), Orientation = orientation };
            var rotatedPlan = plan with { DeskPlacements = [rotated] };
            var rotatedView = BlockChannelView.Create(project with { DeskTypes = [seatType] }, rotatedPlan);
            AssertEqual(2, rotatedView.Cells.Count);
            AssertEqual(true, rotatedView.Cells.Keys.ToHashSet().SetEquals(rotated.GetSeatCells(seatType)));
            AssertEqual(true, rotatedView.Cells.Values.All(value => value == "A"));
        }
    }

    private static void BlockStylesRoundTrip()
    {
        var original = CreateProject();
        var plan = original.Plans[0];
        var labels = new DeskSeatLabel[] { new(plan.DeskPlacements[0].Id, new(0, 0), "A", "1") };
        var project = original with
        {
            Plans = [plan with { SeatLabels = labels }],
            BlockStyles = [new("A", "blue", "white", "solid"), new("unused", "red", "white", "dots")],
        };
        AssertEqual("A", new BlockStyleDraft(project).Mapping.Rows.Single().Key);
        var separated = project with
        {
            DeskLayouts = [new("frames", "Frames", plan.DeskPlacements) { SeatLabels = [labels[0] with { BlockName = "B" }] }],
        };
        AssertEqual("B", new BlockStyleDraft(separated).Mapping.Rows.Single().Key);
        AssertEqual(0, new BlockStyleDraft(original).Mapping.Rows.Count);
        var directory = Path.Combine(Path.GetTempPath(), $"block-style-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "event.json");
            ProjectFileService.Save(path, project);
            using var remote = DesktopApplication.LoadWorkspace(path);
            var draft = new BlockStyleDraft(remote.Project).Mapping;
            draft.SetPattern(0, "checkerboard");
            AssertEqual("solid", remote.Project.BlockStyles[0].Pattern);
            remote.Execute(new CircleSpaceCoordinator.Engine.Model.SetBlockStyles(draft.Build().Select(style =>
                new BlockStyleDefinition(style.Key, style.PrimaryColor, style.SecondaryColor, style.Pattern)).ToArray()), selectedPlanEdit: false);
            AssertEqual("checkerboard", remote.Project.BlockStyles[0].Pattern);
            remote.Undo();
            AssertEqual("solid", remote.Project.BlockStyles[0].Pattern);
            remote.Redo();
            AssertEqual("checkerboard", remote.Project.BlockStyles[0].Pattern);
            AssertEqual(project.BlockStyles[1], remote.Project.BlockStyles[1]);
            ProjectFileService.Save(path, remote.Project);
            var saved = ProjectFileService.Load(path);
            AssertEqual(true, saved.BlockStyles.SequenceEqual(remote.Project.BlockStyles));
            AssertEqual(true, saved.GenreStyles.SequenceEqual(project.GenreStyles));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static void GenreAppearanceDrafts()
    {
        var original = CreateProject();
        var project = original with
        {
            Participants = original.Participants.Select(item => item with { GenreId = "G01" }).ToArray(),
            GenreStyles = [new("G01", "red", "white", "checker"), new("unused", "blue", "white", "solid")],
        };
        var draft = new GenreStyleDraft(project);
        AssertEqual(1, draft.Rows.Count);
        AssertEqual("thick-grid", draft.Rows[0].Pattern);
        draft.SetColor(0, true, "#12abef");
        AssertEqual("#12ABEF", draft.Rows[0].PrimaryColor);
        AssertEqual("red", project.GenreStyles[0].PrimaryColor);
        var invalid = false;
        try { draft.SetColor(0, true, "#NOPE"); } catch (ArgumentException) { invalid = true; }
        AssertEqual(true, invalid);
        AssertEqual("#12ABEF", draft.Rows[0].PrimaryColor);
        draft.SetColor(0, false, "yellow");
        draft.SetPattern(0, "solid");
        invalid = false;
        try { draft.SetColor(0, false, "black"); } catch (InvalidOperationException) { invalid = true; }
        AssertEqual(true, invalid);
        draft.SetPattern(0, "thick-horizontal");
        AssertEqual("uniform-horizontal", draft.Rows[0].Pattern);
        AssertEqual("yellow", draft.Rows[0].SecondaryColor);
        var snapshot = draft.Build();
        AssertEqual(project.GenreStyles[1], snapshot[1]);
        draft.SetPattern(0, "dots");
        AssertEqual("uniform-horizontal", snapshot[0].Pattern);
        var defaults = new GenreStyleDraft(project with
        {
            GenreStyles = [],
            Participants = Enumerable.Range(0, 20).Select(i => new Participant($"p{i}", $"P{i}", 1, new Dictionary<string, double>()) { GenreId = $"G{i:D2}" }).ToArray(),
        });
        AssertEqual(20, defaults.Rows.Count);
        AssertEqual("solid", defaults.Rows[11].Pattern);
        AssertEqual("horizontal", defaults.Rows[12].Pattern);
        var directory = Path.Combine(Path.GetTempPath(), $"genre-style-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "event.json");
            ProjectFileService.Save(path, project);
            using var remote = DesktopApplication.LoadWorkspace(path);
            remote.Execute(new CircleSpaceCoordinator.Engine.Model.SetGenreStyles(draft.Build()), selectedPlanEdit: false);
            AssertEqual("dots", remote.Project.GenreStyles[0].Pattern);
            remote.Undo();
            AssertEqual("checker", remote.Project.GenreStyles[0].Pattern);
            remote.Redo();
            AssertEqual("#12ABEF", remote.Project.GenreStyles[0].PrimaryColor);
            ProjectFileService.Save(path, remote.Project);
            AssertEqual("dots", ProjectFileService.Load(path).GenreStyles[0].Pattern);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static void FrameDraftEdits()
    {
        var catalog = SpaceDefinitionCatalog.CreateDefault();
        var source = catalog.Types[0];
        var draft = new SpaceDefinitionDraft(source);
        draft.Name = "  編集した型  ";
        draft.Paint(0, 0, 0);
        draft.Edges[0] = "壁";
        AssertEqual(1, source.Cells[0].Area);
        AssertEqual("正面", source.Edges[0]);
        draft.Resize(1, 1);
        AssertEqual(1, draft.BuildType().Cells.Count);
        draft.Resize(2, 1);
        AssertEqual(2, draft.AreaAt(1, 0));
        var built = draft.BuildType();
        AssertEqual("編集した型", built.Name);
        AssertEqual(0, built.Cells[0].Area);
        draft.Paint(1, 0, null);
        AssertEqual(2, built.Cells.Count);
        var invalid = false;
        try { (catalog with { Types = catalog.Types.Select(type => type.Id == source.Id ? draft.BuildType() : type).ToArray() }).Validate(); }
        catch (InvalidDataException) { invalid = true; }
        AssertEqual(true, invalid);
        draft.Paint(1, 0, 1);
        invalid = false;
        try { (catalog with { Types = catalog.Types.Select(type => type.Id == source.Id ? draft.BuildType() : type).ToArray() }).Validate(); }
        catch (InvalidDataException) { invalid = true; }
        AssertEqual(true, invalid); // Existing request still refers to area 2.
        draft.Paint(0, 0, 1);
        draft.Paint(1, 0, 2);
        (catalog with { Types = catalog.Types.Select(type => type.Id == source.Id ? draft.BuildType() : type).ToArray() }).Validate();
        var requestSource = catalog.Requests[0];
        var request = new SpaceDefinitionDraft(requestSource);
        request.Name = "別の申込値";
        request.Targets.Clear();
        AssertEqual(2, requestSource.Targets.Count);
        request.Targets.Add(new SpaceTarget(catalog.Types[1].Id, 1));
        var savedRequest = request.BuildRequest();
        request.Targets.Clear();
        AssertEqual(1, savedRequest.Targets.Count);
        (catalog with { Requests = [savedRequest, catalog.Requests[1]] }).Validate();
        foreach (var size in new[] { 0, 13 })
        {
            invalid = false;
            try { draft.Resize(size, 1); } catch (ArgumentOutOfRangeException) { invalid = true; }
            AssertEqual(true, invalid);
        }
    }

    private static void EventProjectCloseTransitions()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"circle-space-close-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            foreach (var decision in new[] { ProjectCloseDecision.Cancel, ProjectCloseDecision.Save, ProjectCloseDecision.Discard })
            foreach (var saveSucceeds in new[] { false, true })
            {
                var path = Path.Combine(directory, "event.json");
                ProjectFileService.Save(path, CreateProject() with { Name = "Saved name" });
                using var remote = DesktopApplication.LoadWorkspace(path);
                remote.LoadProject(remote.Project with { Name = "Edited name" });
                var saveCalls = 0;
                var closed = false;
                var result = ProjectCloseWorkflow.TryClose(decision, () =>
                {
                    saveCalls++;
                    if (!saveSucceeds) return false;
                    ProjectFileService.Save(path, remote.Project);
                    return true;
                }, () => { remote.Dispose(); closed = true; });
                var shouldClose = decision == ProjectCloseDecision.Discard || (decision == ProjectCloseDecision.Save && saveSucceeds);
                AssertEqual(shouldClose, result);
                AssertEqual(shouldClose, closed);
                AssertEqual(decision == ProjectCloseDecision.Save ? 1 : 0, saveCalls);
                AssertEqual(decision == ProjectCloseDecision.Save && saveSucceeds ? "Edited name" : "Saved name", ProjectFileService.Load(path).Name);
                if (!closed)
                {
                    remote.Refresh();
                    AssertEqual("Edited name", remote.Project.Name);
                }
                else
                {
                    var rejected = false;
                    try { remote.Refresh(); }
                    catch (InvalidOperationException) { rejected = true; }
                    AssertEqual(true, rejected);
                    using var reopened = DesktopApplication.LoadWorkspace(path);
                    AssertEqual(ProjectFileService.Load(path).Name, reopened.Project.Name);
                    AssertEqual(false, reopened.CanUndo);
                }
            }
            var closeCalled = false;
            try
            {
                ProjectCloseWorkflow.TryClose(ProjectCloseDecision.Save, () => throw new IOException("save failed"), () => closeCalled = true);
                throw new Exception("Expected save exception");
            }
            catch (IOException) { AssertEqual(false, closeCalled); }
        }
        finally { Directory.Delete(directory, recursive: true); }
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
                new Dictionary<string, bool> { ["evaluationAnalysis"] = true }, "unused-desk", ["plan-2", "plan-1"]));

            var restored = new ApplicationSettingsService(settingsPath).GetWorkingState(projectPath)!;
            AssertEqual("plan-2", restored.SelectedPlanId);
            AssertEqual("unused-desk", restored.SelectedDeskLayoutId!);
            AssertEqual("GenrePlacement", restored.EditorMode);
            AssertEqual(1.75d, restored.Zoom);
            AssertEqual(true, restored.Switches!["evaluationAnalysis"]);
            AssertEqual("plan-2,plan-1", string.Join(",", restored.PinnedExportPlanIds!));
            var otherPath = Path.Combine(directory, "other.json");
            settings.SaveWorkingState(new ProjectWorkingState(otherPath, "plan-1", "CirclePlacement", 1, 0, 0));
            var reloaded = new ApplicationSettingsService(settingsPath);
            AssertEqual<IReadOnlyList<string>?>(null, reloaded.GetWorkingState(otherPath)!.PinnedExportPlanIds);
            AssertEqual(2, reloaded.GetWorkingState(projectPath)!.PinnedExportPlanIds!.Count);
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

    private static void IndependentNumberChannels()
    {
        var workspace = new ProjectWorkspace(CreateProject());
        var commands = new EditorCommandController(workspace);
        var relative = new GridPosition(0, 0);
        AssertEqual(true, commands.ReplaceSeatLabels([new DeskSeatLabel("desk-1", relative, "A", "")]).Applied);
        AssertEqual(true, commands.ReplaceSeatLabels([workspace.SelectedPlan.SeatLabels.Single() with { SeatName = "1" }]).Applied);
        AssertEqual(true, commands.SetDeskNumber("desk-1", "10").Applied);
        var before = ProjectJsonSerializer.Save(workspace.Project);
        AssertEqual(true, commands.ReplaceSeatLabels([workspace.SelectedPlan.SeatLabels.Single() with { BlockName = "" }]).Applied);
        AssertEqual("1", workspace.SelectedPlan.SeatLabels.Single().SeatName);
        AssertEqual("10", workspace.SelectedPlan.DeskPlacements.Single().DeskNumber);
        var cleared = ProjectJsonSerializer.Save(workspace.Project);
        AssertEqual(true, commands.Undo());
        AssertEqual(before, ProjectJsonSerializer.Save(workspace.Project));
        AssertEqual(true, commands.Redo());
        var loaded = ProjectJsonSerializer.Load(cleared);
        AssertEqual("", loaded.Plans.Single().SeatLabels.Single().BlockName);
        AssertEqual("1", loaded.Plans.Single().SeatLabels.Single().SeatName);
        AssertEqual(cleared, ProjectJsonSerializer.Save(workspace.Project));
        AssertEqual(true, commands.ReplaceSeatLabels([]).Applied);
        AssertEqual("10", workspace.SelectedPlan.DeskPlacements.Single().DeskNumber);
    }

    private static void CatalogSpacesRoundTrip()
    {
        var defaults = SpaceDefinitionCatalog.CreateDefault().Types;
        var booth = defaults.Single(type => type.Id == "booth");
        var frameOnly = booth with { Id = "representative-booth", Cells = booth.Cells.Select(cell => cell with { Area = cell.X == 2 && cell.Y == 1 ? 1 : 0 }).ToArray() };
        foreach (var definition in defaults.Append(frameOnly))
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
            var seats = placed.GetSeatCells(type);
            AssertEqual(true, placed.GetFrameNumberCells(type).SetEquals(seats.Count == 0 ? placed.GetOccupiedCells(type) : seats));
            foreach (var cell in definition.Cells)
                AssertEqual(cell.Area > 0, seats.Contains(placed.Anchor + new GridPosition(cell.X, cell.Y).Rotate(orientation)));
            var legacyType = type with { Space = null };
            AssertEqual(true, placed.GetSeatCells(legacyType).SetEquals(placed.GetOccupiedCells(legacyType)));
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
            var frameOnlyCatalog = new SpaceDefinitionCatalog([emptyType], []);
            try { first.Save(frameOnlyCatalog); throw new Exception("Expected representative cell rejection."); }
            catch (InvalidDataException ex) { AssertEqual("ブロックを入力するために、フレームを代表するセルが１つは必要です", ex.Message); }
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(frameOnlyCatalog, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase }));
            AssertEqual(0, new SpaceDefinitionStore(path).Current.Types.Single().Cells.Single().Area);
            try { new SpaceDefinitionCatalog([emptyType with { Cells = [] }], []).Validate(); throw new Exception("Expected empty footprint rejection."); }
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

    private static void DialogValidation()
    {
        AssertEqual(true, EditorDialogValidation.Name("  ") is not null);
        AssertEqual<string?>(null, EditorDialogValidation.Name("  ", allowEmpty: true));
        AssertEqual(true, EditorDialogValidation.ChannelName(" 番地 ") is not null);
        AssertEqual<string?>(null, EditorDialogValidation.ChannelName("優先度"));
        foreach (var value in new[] { "", " ", "0", "1", 0.001m.ToString(), 0.123m.ToString(), 1.000m.ToString() }) AssertEqual<string?>(null, EditorDialogValidation.Weight(value));
        foreach (var value in new[] { "NaN", "-1", "2", "1e-3", "文字", 0.1234m.ToString(), 1.001m.ToString(), 0.0001m.ToString() }) AssertEqual(true, EditorDialogValidation.Weight(value) is not null);
        AssertEqual(true, EditorDialogValidation.TryParseWeight("  ", out var emptyWeight));
        AssertEqual(0m, emptyWeight);
        AssertEqual(true, EditorDialogValidation.TryParseWeight(0.375m.ToString(), out var preciseWeight));
        AssertEqual(0.375m, preciseWeight);
        AssertEqual<string?>(null, EditorDialogValidation.CircleIdPattern(""));
        AssertEqual<string?>(null, EditorDialogValidation.CircleIdPattern("^(.*)$"));
        AssertEqual(true, EditorDialogValidation.CircleIdPattern("[") is not null);
    }

    private static void ParticipantCellSelection()
    {
        var content = new ParticipantCellText("A😀e\u0301\r\n日本語\r\n");
        AssertEqual(3, content.Lines.Count);
        AssertEqual((0, 1), content.ClampPosition((0, 2)));
        AssertEqual((0, 3), content.ClampPosition((0, 4)));
        AssertEqual("😀e\u0301" + Environment.NewLine + "日本", content.Select((1, 2), (0, 1)));
        AssertEqual("", content.Select((0, 0), (0, 0)));
        AssertEqual("A😀e\u0301" + Environment.NewLine + "日本語" + Environment.NewLine, content.Select((-1, 0), (99, 99)));
        var empty = new ParticipantCellText("");
        AssertEqual("", empty.Select((0, 0), (10, 10)));
        var longLine = new ParticipantCellText(new string('a', 100000) + "終");
        AssertEqual("終", longLine.Select((0, 100000), (0, 100001)));
    }

    private static void BulkFrameNumbers()
    {
        var workspace = new ProjectWorkspace(CreateAssignmentProject(2));
        var commands = new EditorCommandController(workspace);
        AssertEqual(true, commands.SetDeskNumber("desk-1", "A").Applied);
        AssertEqual(true, commands.SetDeskNumber("desk-2", "B").Applied);
        AssertEqual(true, commands.SetDeskNumbers(["desk-1", "desk-2"], " 10 ").Applied);
        AssertEqual(true, workspace.SelectedPlan.DeskPlacements.All(desk => desk.DeskNumber == "10"));
        AssertEqual(true, commands.Undo());
        AssertEqual("A", workspace.SelectedPlan.DeskPlacements.Single(desk => desk.Id == "desk-1").DeskNumber);
        AssertEqual("B", workspace.SelectedPlan.DeskPlacements.Single(desk => desk.Id == "desk-2").DeskNumber);
        AssertEqual(true, commands.Redo());
        AssertEqual(true, workspace.SelectedPlan.DeskPlacements.All(desk => desk.DeskNumber == "10"));
        AssertEqual(true, commands.SetDeskNumbers(["desk-1", "desk-2"], " ").Applied);
        AssertEqual(true, workspace.SelectedPlan.DeskPlacements.All(desk => desk.DeskNumber is null));
        AssertEqual(true, commands.Undo());
        AssertEqual(true, commands.SetDeskNumbers(["desk-1"], "20").Applied);
        AssertEqual("10", workspace.SelectedPlan.DeskPlacements.Single(desk => desk.Id == "desk-2").DeskNumber);
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
            catch (InvalidOperationException exception) when (exception.Message.Contains("フレーム番号"))
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

    private static void MissingCircleLayoutCanBeCreated()
    {
        var workspace = new ProjectWorkspace(LayoutCatalogService.CreateDeskLayout(CreateProject(), "unused", "Unused"));
        workspace.SelectDeskLayout("unused");
        AssertEqual(true, new EditorCommandController(workspace).AddDeskAt(new GridPosition(0, 0)).Applied);
        var placement = new ParticipantPlacementController(workspace);
        var id = workspace.Project.Participants[0].Id;
        var before = placement.PlaceParticipantAt(id, new GridPosition(0, 0));
        AssertEqual("assignment.circleLayout.required", before.Issues.Single().Code);
        workspace.Execute(new CircleSpaceCoordinator.Engine.Model.LayoutCatalogServiceCreateCircleLayout("new-circle", "New circle", "unused"), selectedPlanEdit: false);
        workspace.SelectPlan("new-circle");
        AssertEqual(true, placement.PlaceParticipantAt(id, new GridPosition(0, 0)).Applied);
        AssertEqual(1, workspace.SelectedPlan.Assignments.Count);
        workspace.Undo();
        AssertEqual(0, workspace.SelectedPlan.Assignments.Count);
        workspace.Undo();
        AssertEqual(false, workspace.Project.CircleLayouts.Any(layout => layout.Id == "new-circle"));
        workspace.SelectDeskLayout("unused");
        AssertEqual(false, workspace.HasSelectedCircleLayout);
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
        var rejected = placement.PlaceParticipantAt(workspace.Project.Participants[0].Id, new GridPosition(0, 0));
        AssertEqual(false, rejected.Applied);
        AssertEqual("assignment.circleLayout.required", rejected.Issues.Single().Code);
        var parkRejected = placement.ParkParticipantAt(workspace.Project.Participants[0].Id, [new(0, 0)], new(0, 0), new(0, 2));
        AssertEqual("assignment.circleLayout.required", parkRejected.Issues.Single().Code);
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

    private static void ExportChoicesAndPins()
    {
        var project = CreateProject();
        var template = project.Plans[0];
        project = project with { Plans = [template with { Id = "a", Name = "同名" }, template with { Id = "b", Name = "同名" },
            template with { Id = "c" }, template with { Id = "d" }], ExportPlanId = "a" };
        CircleSpaceCoordinator.Application.Plans.RankedPlan Score(string id, double general, double circle) =>
            new(99, id, "unused", -100, general, circle, true, []);
        var choices = ExportPlanChoices.Build(project, [Score("a", 10, 2), Score("c", 9, 100), Score("d", 10, 8), Score("b", 10, 8)]);
        AssertEqual("b,d,a,c", string.Join(",", choices.Select(item => item.Plan.Id)));
        AssertEqual("同名", choices[0].Plan.Name);
        var pins = new HashSet<string> { "a", "b", "deleted" };
        AssertEqual("b,a", string.Join(",", ExportPlanChoices.Filter(choices, pins, true).Select(item => item.Plan.Id)));
        pins.Remove("b");
        AssertEqual("a", ExportPlanChoices.Filter(choices, pins, true).Single().Plan.Id);
        AssertEqual(0, ExportPlanChoices.Filter(choices, new HashSet<string>(), true).Count);
        AssertEqual(4, ExportPlanChoices.Filter(choices, pins, false).Count);
        AssertEqual("a", project.ExportPlanId!);
        AssertEqual("a,b,c,d", string.Join(",", project.Plans.Select(item => item.Id)));
        AssertEqual(0, ExportPlanChoices.Build(project with { Plans = [] }, []).Count);
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

    private static void ExportColumnDrafts()
    {
        var source = new CircleSpaceCoordinator.Infrastructure.Tabular.ParticipantTableSheet("入力", ["ID", "同名", "同名"], [new[] { "001", "残す1", "残す2" }]);
        var draft = new ExportColumnDraft(source, [-1, -1, 0]);
        draft.Assign(0, 1);
        draft.Assign(1, 1);
        AssertEqual(-1, draft.Columns[0]);
        AssertEqual(1, draft.Columns[1]);
        draft.Assign(0, 2);
        AssertEqual(true, draft.IsComplete);
        draft.Assign(0, -1);
        AssertEqual(false, draft.IsComplete);
        draft.AddNumberColumn(0);
        draft.AddNumberColumn(1);
        draft.AddNumberColumn(0); // The unused first addition is discarded on apply.
        var built = draft.Build();
        AssertEqual(5, built.Sheet.Headers.Count);
        AssertEqual("セル番号", built.Sheet.Headers[3]);
        AssertEqual("ブロック番号 (2)", built.Sheet.Headers[4]);
        AssertEqual("4,3,0", string.Join(",", built.Columns));
        AssertEqual("残す2", built.Sheet.Rows[0][2]);
        AssertEqual("", built.Sheet.Rows[0][3]);
        AssertEqual(3, source.Headers.Count);
        AssertEqual(3, source.Rows[0].Count);
    }

    private static void FullFrameSpaceNumbers()
    {
        var project = CreateProject();
        var cells = new HashSet<GridPosition> { new(0, 0), new(1, 0), new(2, 0) };
        var type = new DeskType("three", "３セル", cells.ToArray());
        var desk = new DeskPlacement("frame", "three", new(0, 0), QuarterTurn.North) { DeskNumber = "F03" };
        var circle = new Participant("a", "A", 3, new Dictionary<string, double>());
        var plan = new Plan("test", "test", [desk], [new("a", cells, new(0, 0))])
        {
            SeatLabels = cells.Select(cell => new DeskSeatLabel("frame", cell, "B", $"S{cell.X + 1}")).ToArray(),
        };
        project = project with { DeskTypes = [type], Participants = [circle], Plans = [plan] };
        AssertEqual("F03", CircleSeatExportBuilder.Build(project, plan).Single().SeatName);
        plan = plan with { Assignments = [new("a", new HashSet<GridPosition> { new(0, 0), new(1, 0) }, new(0, 0)), new("b", new HashSet<GridPosition> { new(2, 0) }, new(2, 0))] };
        project = project with { Participants = [circle with { RequiredCellCount = 2 }, circle with { Id = "b", CircleId = "b", RequiredCellCount = 1 }], Plans = [plan] };
        var rows = CircleSeatExportBuilder.Build(project, plan);
        AssertEqual(CircleSeatExportBuilder.UndefinedSpaceNumber, rows[0].SeatName);
        AssertEqual("S3", rows[1].SeatName);
        var one = type with { Footprint = [new(0, 0)] };
        plan = plan with { Assignments = [new("b", new HashSet<GridPosition> { new(0, 0) }, new(0, 0))] };
        AssertEqual("F03", CircleSeatExportBuilder.Build(project with { DeskTypes = [one] }, plan).Single().SeatName);
        plan = plan with { SeatLabels = [] };
        AssertEqual("#MISSING_BLOCK_NUMBER", CircleSeatExportBuilder.Build(project with { DeskTypes = [one] }, plan).Single().BlockName);
    }

    private static void NewExportSource()
    {
        var project = CreateProject();
        project = project with
        {
            ParticipantTableSource = new("input.csv", "入力", ["受付", "", "メモ", "メモ"], ["id", "blank", "memo1", "memo2"]),
            Participants = project.Participants.Select((participant, index) => participant with
            {
                CircleId = $"00{index}",
                SourceValues = new Dictionary<string, string> { ["id"] = $"00{index}", ["blank"] = "空見出しの値", ["memo1"] = "元の値", ["memo2"] = "別の値" },
            }).ToArray(),
        };
        var result = CircleSeatSourceTable.Build(project);
        AssertEqual(0, result.Circle);
        AssertEqual(4, result.Block);
        AssertEqual(5, result.Seat);
        AssertEqual("", result.Sheet.Headers[1]);
        AssertEqual("メモ", result.Sheet.Headers[3]);
        AssertEqual("別の値", result.Sheet.Rows[0][3]);
        AssertEqual("", result.Sheet.Rows[0][result.Block]);
        AssertEqual(4, project.ParticipantTableSource.Headers.Count);
        var clone = project with { Participants = project.Participants.Select(item => item with { SourceValues = item.SourceValues.ToDictionary(pair => pair.Key, pair => pair.Value) }).ToArray(), ExportPlanId = project.Plans[0].Id };
        AssertEqual(true, CircleSeatSourceTable.HasSameValues(project, clone));
        clone = clone with { Participants = clone.Participants.Select(item => item with { SourceValues = new Dictionary<string, string> { ["memo1"] = "変更" } }).ToArray() };
        AssertEqual(false, CircleSeatSourceTable.HasSameValues(project, clone));
        var noId = project with { ParticipantTableSource = new("input.csv", "入力", ["メモ"], ["memo1"]) };
        var withId = CircleSeatSourceTable.Build(noId);
        AssertEqual(1, withId.Circle);
        AssertEqual(project.Participants[0].CircleId, withId.Sheet.Rows[0][withId.Circle]);
    }

    private static void OutputTableView()
    {
        var table = new ParticipantTableView(["ID", "", "席", "席"],
            [new[] { "destination-only", "保持", "ア", "10左" }, new[] { "short-row" }], "output.csv");
        AssertEqual(2, table.RowCount);
        AssertEqual(4, table.ColumnCount);
        AssertEqual("", table.Headers[1]);
        AssertEqual("destination-only", table.GetValue(0, 0));
        AssertEqual("10左", table.GetValue(0, 3));
        AssertEqual("", table.GetValue(1, 3));
        AssertEqual("output.csv", table.SourceDescription);
        var empty = new ParticipantTableView([], [], "未選択");
        AssertEqual(0, empty.RowCount);
        AssertEqual(0, empty.ColumnCount);
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
