namespace CircleSpaceCoordinator.Application.Tests;

using CircleSpaceCoordinator.Application.Editing;
using CircleSpaceCoordinator.Application.Layouts;
using CircleSpaceCoordinator.Application.Plans;
using CircleSpaceCoordinator.Application.Participants;
using CircleSpaceCoordinator.Application.Queries;
using CircleSpaceCoordinator.Application.Workspace;
using CircleSpaceCoordinator.Core.Evaluation;
using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Core.Validation;
using CircleSpaceCoordinator.OptimizationEngine;

internal static class Program
{
    private static int Main()
    {
        var tests = new (string Name, Action Run)[]
        {
            ("Channels map imported values, score, validate and undo through editor operations", ChannelChecks.Run),
            ("Moving a desk moves its circle assignment", MovingDeskMovesAssignment),
            ("Rotating a desk rotates its circle assignment", RotatingDeskMovesAssignment),
            ("Invalid desk edits are rejected", InvalidEditIsRejected),
            ("A plan can be duplicated without changing its source", PlanCanBeDuplicated),
            ("A seat label follows its desk and is removed with it", SeatLabelFollowsDesk),
            ("A desk number follows its desk", DeskNumberFollowsDesk),
            ("Seat labels can be replaced together and temporary duplicate names are retained", SeatLabelsCanBeReplacedTogether),
            ("A desk layout can be copied without copying assignments", DeskLayoutCanBeCopied),
            ("A duplicate plan ID is rejected", DuplicatePlanIdIsRejected),
            ("Plans are ranked by score with stable ties", PlansAreRanked),
            ("A desk edit can be undone and redone", DeskEditCanBeUndoneAndRedone),
            ("A new edit clears the redo history", NewEditClearsRedoHistory),
            ("A rejected edit is not added to history", RejectedEditIsNotRecorded),
            ("A participant can be assigned and unassigned", ParticipantCanBeAssignedAndUnassigned),
            ("A participant assignment can be moved", ParticipantCanBeReassigned),
            ("Rectangular cell ranges swap assignments", RectangularCellRangesSwapAssignments),
            ("Cell ranges may include empty aisles", CellRangesMayIncludeEmptyAisles),
            ("Overlapping cell ranges cannot be swapped", OverlappingCellRangesCannotBeSwapped),
            ("An overlapping assignment is rejected", OverlappingAssignmentIsRejected),
            ("An unassigned desk can be added and removed", DeskCanBeAddedAndRemoved),
            ("A desk with an assignment cannot be removed", AssignedDeskCannotBeRemoved),
            ("An invalid new desk is rejected", InvalidNewDeskIsRejected),
            ("A plan can be renamed and undone", PlanCanBeRenamedAndUndone),
            ("A plan can be removed", PlanCanBeRemoved),
            ("The last plan cannot be removed", LastPlanCannotBeRemoved),
            ("An empty plan can be created and edited", EmptyPlanCanBeCreatedAndEdited),
            ("A new plan ID must be unique", NewPlanIdMustBeUnique),
            ("A workspace runs the selected-plan editing flow", WorkspaceRunsEditingFlow),
            ("A workspace reconciles selection after plan removal", WorkspaceReconcilesSelection),
            ("A plan snapshot contains desks assignments and unassigned participants", PlanSnapshotContainsViewData),
            ("A workspace exposes its selected plan snapshot", WorkspaceExposesSelectedSnapshot),
            ("A venue can be expanded", VenueCanBeExpanded),
            ("A venue cannot shrink across an existing desk", VenueCannotShrinkAcrossDesk),
            ("A pillar blocks and frees a venue cell", PillarBlocksAndFreesVenueCell),
            ("Two-cell desks fill available venue cells", DesksFillAvailableCells),
            ("Participant import preserves matching internal IDs and removes stale assignments", ParticipantImportReconcilesCatalog),
            ("Optimization engine improves a swappable plan without changing its source", OptimizationEngineImprovesCopy),
            ("Optimization engine swaps two-cell circles", OptimizationEngineSwapsTwoCellCircles),
            ("Optimization engine swaps combined circles as groups", OptimizationEngineSwapsCombinedCircles),
            ("A desk layout cannot be removed while a circle layout refers to it", ReferencedDeskLayoutCannotBeRemoved),
            ("A circle layout can be linked to another desk layout", CircleLayoutCanBeRelinked),
            ("Desk and circle layouts can be renamed independently", LayoutsCanBeRenamed),
            ("Unused desk selection supports editing removal and undo without creating circles", UnusedDeskSelection),
            ("Relinking the last circle leaves its desk independently selectable", RelinkedDeskSelection),
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

    private static void MovingDeskMovesAssignment()
    {
        var original = CreateProject();
        var edited = PlanDeskEditor.MoveDesk(original, "plan-1", "desk-1", new GridPosition(2, 2));
        var assignment = edited.Plans.Single().Assignments.Single();

        AssertSetEqual(
            new HashSet<GridPosition> { new(2, 2), new(3, 2) },
            assignment.OccupiedCells);
        AssertEqual(new GridPosition(2, 2), assignment.ScoringPosition);
        AssertEqual(new GridPosition(0, 0), original.Plans.Single().DeskPlacements.Single().Anchor);
    }

    private static void RotatingDeskMovesAssignment()
    {
        var edited = PlanDeskEditor.RotateDesk(CreateProject(), "plan-1", "desk-1", QuarterTurn.East);
        var assignment = edited.Plans.Single().Assignments.Single();

        AssertSetEqual(
            new HashSet<GridPosition> { new(0, 0), new(0, 1) },
            assignment.OccupiedCells);
        AssertEqual(new GridPosition(0, 0), assignment.ScoringPosition);
    }

    private static void InvalidEditIsRejected()
    {
        try
        {
            PlanDeskEditor.MoveDesk(CreateProject(), "plan-1", "desk-1", new GridPosition(4, 3));
            throw new InvalidOperationException("Expected the out-of-bounds edit to fail.");
        }
        catch (ProjectValidationException exception)
        {
            if (exception.Issues.All(issue => issue.Code != "deskPlacement.outOfBounds"))
                throw;
        }
    }

    private static void PlanCanBeDuplicated()
    {
        var original = CreateProject();
        var edited = PlanCatalogService.DuplicatePlan(original, "plan-1", "plan-2", "架空配置案２");

        AssertEqual(1, original.Plans.Count);
        AssertEqual(2, edited.Plans.Count);
        AssertEqual("plan-2", edited.Plans[1].Id);
        AssertEqual("架空配置案２", edited.Plans[1].Name);
        AssertSetEqual(edited.Plans[0].Assignments[0].OccupiedCells, edited.Plans[1].Assignments[0].OccupiedCells);
    }

    private static void DuplicatePlanIdIsRejected()
    {
        try
        {
            PlanCatalogService.DuplicatePlan(CreateProject(), "plan-1", "plan-1", "重複案");
            throw new InvalidOperationException("Expected the duplicate plan ID to fail.");
        }
        catch (InvalidOperationException exception) when (exception.Message.Contains("already exists", StringComparison.Ordinal))
        {
        }
    }

    private static void SeatLabelFollowsDesk()
    {
        var source = CreateProject();
        source = source with { Plans = [source.Plans.Single() with { Assignments = [] }] };
        var labeled = DeskSeatLabelEditor.SetLabel(
            source, "plan-1", "desk-1", new GridPosition(1, 0), "ア", "10左");
        var moved = PlanDeskEditor.MoveDesk(labeled, "plan-1", "desk-1", new GridPosition(2, 1));
        var rotated = PlanDeskEditor.RotateDesk(moved, "plan-1", "desk-1", QuarterTurn.East);

        var label = rotated.Plans.Single().SeatLabels.Single();
        AssertEqual("ア", label.BlockName);
        AssertEqual("10左", label.SeatName);
        AssertEqual(new GridPosition(1, 0), label.RelativeCell);

        var removed = PlanDeskEditor.RemoveDesk(rotated, "plan-1", "desk-1");
        AssertEqual(0, removed.Plans.Single().SeatLabels.Count);
    }

    private static void DeskNumberFollowsDesk()
    {
        var numbered = PlanDeskEditor.SetDeskNumber(CreateProject(), "plan-1", "desk-1", "A-10");
        var moved = PlanDeskEditor.MoveDesk(numbered, "plan-1", "desk-1", new GridPosition(2, 1));
        var rotated = PlanDeskEditor.RotateDesk(moved, "plan-1", "desk-1", QuarterTurn.East);

        if (rotated.Plans.Single().DeskPlacements.Single().DeskNumber != "A-10")
            throw new InvalidOperationException("Desk number did not follow the desk.");

        var cleared = PlanDeskEditor.SetDeskNumber(rotated, "plan-1", "desk-1", " ");
        if (cleared.Plans.Single().DeskPlacements.Single().DeskNumber is not null)
            throw new InvalidOperationException("Blank desk number was not removed.");
    }

    private static void OptimizationEngineImprovesCopy()
    {
        var deskType = new DeskType("single", "1セル机", [new GridPosition(0, 0)]);
        var participants = new[]
        {
            new Participant("low", "低", 1, new Dictionary<string, double> { ["score"] = 1d }),
            new Participant("high", "高", 1, new Dictionary<string, double> { ["score"] = 10d }),
        };
        var feature = new EvaluationFeature("score", "評価", 1d, 0d, 1d);
        var project = new CircleSpaceProject("1.0", "optimizer-test", "最適化テスト",
            new Venue("venue", "会場", 2, 1, new HashSet<GridPosition>()),
            [deskType], participants,
            new EvaluationConfiguration([feature], [new WeightMap("score", 0d, new Dictionary<GridPosition, double> { [new(0, 0)] = 0d, [new(1, 0)] = 1d })]),
            [new Plan("plan", "配置案", [
                new DeskPlacement("left", deskType.Id, new(0, 0), QuarterTurn.North),
                new DeskPlacement("right", deskType.Id, new(1, 0), QuarterTurn.North)], [
                new ParticipantAssignment("low", new HashSet<GridPosition> { new(1, 0) }, new(1, 0)),
                new ParticipantAssignment("high", new HashSet<GridPosition> { new(0, 0) }, new(0, 0))])]);

        var result = new CirclePlacementOptimizationEngine().Optimize(project, "plan", new(1_000, RandomSeed: 7));
        if (result.BestScore.CompareTo(result.InitialScore) <= 0)
            throw new InvalidOperationException("Optimization did not improve the plan.");
        AssertEqual(new GridPosition(1, 0), project.Plans.Single().Assignments.Single(item => item.ParticipantId == "low").ScoringPosition);
        AssertEqual(new GridPosition(0, 0), result.BestPlan.Assignments.Single(item => item.ParticipantId == "low").ScoringPosition);
    }

    private static void OptimizationEngineSwapsTwoCellCircles()
    {
        var deskType = new DeskType("two", "2セル机", [new GridPosition(0, 0), new GridPosition(1, 0)]);
        var participants = new[]
        {
            new Participant("low", "低", 2, new Dictionary<string, double> { ["score"] = 1d }),
            new Participant("high", "高", 2, new Dictionary<string, double> { ["score"] = 10d }),
        };
        var project = CreateOptimizationProject(deskType, participants,
            [new ParticipantAssignment("low", new HashSet<GridPosition> { new(2, 0), new(3, 0) }, new(2, 0)), new ParticipantAssignment("high", new HashSet<GridPosition> { new(0, 0), new(1, 0) }, new(0, 0))]);
        var result = new CirclePlacementOptimizationEngine().Optimize(project, "plan", new(1_000, RandomSeed: 7));

        if (result.BestScore.CompareTo(result.InitialScore) <= 0)
            throw new InvalidOperationException("Two-cell circles were not optimized.");
        AssertEqual(new GridPosition(0, 0), result.BestPlan.Assignments.Single(item => item.ParticipantId == "low").ScoringPosition);
    }

    private static void OptimizationEngineSwapsCombinedCircles()
    {
        var deskType = new DeskType("two", "2セル机", [new GridPosition(0, 0), new GridPosition(1, 0)]);
        var participants = new[]
        {
            new Participant("low-a", "低A", 1, new Dictionary<string, double> { ["score"] = 1d }) { CircleId = "low-a", CombinedWithCircleId = "low-b" },
            new Participant("low-b", "低B", 1, new Dictionary<string, double> { ["score"] = 1d }) { CircleId = "low-b", CombinedWithCircleId = "low-a" },
            new Participant("high-a", "高A", 1, new Dictionary<string, double> { ["score"] = 10d }) { CircleId = "high-a", CombinedWithCircleId = "high-b" },
            new Participant("high-b", "高B", 1, new Dictionary<string, double> { ["score"] = 10d }) { CircleId = "high-b", CombinedWithCircleId = "high-a" },
        };
        var project = CreateOptimizationProject(deskType, participants,
        [
            new ParticipantAssignment("low-a", new HashSet<GridPosition> { new(2, 0) }, new(2, 0)),
            new ParticipantAssignment("low-b", new HashSet<GridPosition> { new(3, 0) }, new(3, 0)),
            new ParticipantAssignment("high-a", new HashSet<GridPosition> { new(0, 0) }, new(0, 0)),
            new ParticipantAssignment("high-b", new HashSet<GridPosition> { new(1, 0) }, new(1, 0)),
        ]);
        var result = new CirclePlacementOptimizationEngine().Optimize(project, "plan", new(1_000, RandomSeed: 7));

        if (result.BestScore.CompareTo(result.InitialScore) <= 0)
            throw new InvalidOperationException("Combined circles were not optimized.");
        var lowCells = result.BestPlan.Assignments.Where(item => item.ParticipantId.StartsWith("low", StringComparison.Ordinal)).SelectMany(item => item.OccupiedCells).ToHashSet();
        AssertSetEqual(new HashSet<GridPosition> { new(0, 0), new(1, 0) }, lowCells);
    }

    private static CircleSpaceProject CreateOptimizationProject(DeskType deskType, IReadOnlyList<Participant> participants, IReadOnlyList<ParticipantAssignment> assignments)
    {
        var feature = new EvaluationFeature("score", "評価", 1d, 0d, 1d);
        return new CircleSpaceProject("1.0", "optimizer-test", "最適化テスト",
            new Venue("venue", "会場", 4, 1, new HashSet<GridPosition>()), [deskType], participants,
            new EvaluationConfiguration([feature], [new WeightMap("score", 0d, new Dictionary<GridPosition, double> { [new(0, 0)] = 0d, [new(1, 0)] = 0d, [new(2, 0)] = 1d, [new(3, 0)] = 1d })]),
            [new Plan("plan", "配置案", [new DeskPlacement("left", deskType.Id, new(0, 0), QuarterTurn.North), new DeskPlacement("right", deskType.Id, new(2, 0), QuarterTurn.North)], assignments)]);
    }

    private static void DeskLayoutCanBeCopied()
    {
        var source = DeskSeatLabelEditor.SetLabel(
            CreateProject(), "plan-1", "desk-1", new GridPosition(0, 0), "ア", "10左");
        var withDestination = PlanCatalogService.CreatePlan(source, "plan-2", "配置案２");
        var copied = PlanCatalogService.CopyDeskLayout(withDestination, "plan-1", "plan-2");

        var destination = copied.Plans.Single(plan => plan.Id == "plan-2");
        AssertEqual(1, destination.DeskPlacements.Count);
        AssertEqual("desk-1", destination.DeskPlacements.Single().Id);
        AssertEqual("ア", destination.SeatLabels.Single().BlockName);
        AssertEqual(0, destination.Assignments.Count);
    }

    private static void SeatLabelsCanBeReplacedTogether()
    {
        var source = CreateProject();
        source = source with { Plans = [source.Plans.Single() with { Assignments = [] }] };
        var labels = new[]
        {
            new DeskSeatLabel("desk-1", new GridPosition(0, 0), "ア", "10左"),
            new DeskSeatLabel("desk-1", new GridPosition(1, 0), "ア", "10右"),
        };
        var edited = DeskSeatLabelEditor.ReplaceLabels(source, "plan-1", labels);
        AssertEqual(2, edited.Plans.Single().SeatLabels.Count);

        var blockOnly = DeskSeatLabelEditor.ReplaceLabels(source, "plan-1",
        [
            new DeskSeatLabel("desk-1", new GridPosition(0, 0), "ア", ""),
            new DeskSeatLabel("desk-1", new GridPosition(1, 0), "ア", ""),
        ]);
        AssertEqual(2, blockOnly.Plans.Single().SeatLabels.Count);

        var duplicateNames = DeskSeatLabelEditor.ReplaceLabels(source, "plan-1",
        [
            labels[0],
            labels[1] with { SeatName = "10左" },
        ]);
        AssertEqual(2, duplicateNames.Plans.Single().SeatLabels.Count);
    }

    private static void PlansAreRanked()
    {
        var project = CreateScoredProject();
        var ranking = PlanCatalogService.RankPlans(project);

        AssertEqual("plan-high", ranking[0].PlanId);
        AssertEqual(1, ranking[0].Rank);
        AssertEqual(10d, ranking[0].TotalScore);
        AssertEqual("plan-low-a", ranking[1].PlanId);
        AssertEqual("plan-low-b", ranking[2].PlanId);
    }

    private static void DeskEditCanBeUndoneAndRedone()
    {
        var history = new ProjectEditHistory(CreateProject());
        history.Apply(project =>
            PlanDeskEditor.MoveDesk(project, "plan-1", "desk-1", new GridPosition(2, 2)));

        AssertEqual(new GridPosition(2, 2), CurrentDeskAnchor(history));
        AssertEqual(true, history.CanUndo);
        AssertEqual(false, history.CanRedo);

        history.Undo();
        AssertEqual(new GridPosition(0, 0), CurrentDeskAnchor(history));
        AssertEqual(true, history.CanRedo);

        history.Redo();
        AssertEqual(new GridPosition(2, 2), CurrentDeskAnchor(history));
    }

    private static void NewEditClearsRedoHistory()
    {
        var history = new ProjectEditHistory(CreateProject());
        history.Apply(project =>
            PlanDeskEditor.MoveDesk(project, "plan-1", "desk-1", new GridPosition(1, 1)));
        history.Undo();
        history.Apply(project =>
            PlanDeskEditor.MoveDesk(project, "plan-1", "desk-1", new GridPosition(2, 1)));

        AssertEqual(false, history.CanRedo);
        AssertEqual(new GridPosition(2, 1), CurrentDeskAnchor(history));
    }

    private static void RejectedEditIsNotRecorded()
    {
        var history = new ProjectEditHistory(CreateProject());
        try
        {
            history.Apply(project =>
                PlanDeskEditor.MoveDesk(project, "plan-1", "desk-1", new GridPosition(4, 3)));
            throw new InvalidOperationException("Expected the invalid edit to fail.");
        }
        catch (ProjectValidationException)
        {
        }

        AssertEqual(false, history.CanUndo);
        AssertEqual(new GridPosition(0, 0), CurrentDeskAnchor(history));
    }

    private static GridPosition CurrentDeskAnchor(ProjectEditHistory history) =>
        history.Current.Plans.Single().DeskPlacements.Single().Anchor;

    private static void ParticipantCanBeAssignedAndUnassigned()
    {
        var project = CreateTwoParticipantProject(assignSecondParticipant: false);
        var assigned = ParticipantAssignmentEditor.Assign(
            project,
            "plan-1",
            "fictional-circle-002",
            new HashSet<GridPosition> { new(3, 0) },
            new GridPosition(3, 0));

        AssertEqual(2, assigned.Plans.Single().Assignments.Count);
        var unassigned = ParticipantAssignmentEditor.Unassign(assigned, "plan-1", "fictional-circle-002");
        AssertEqual(1, unassigned.Plans.Single().Assignments.Count);
    }

    private static void ParticipantCanBeReassigned()
    {
        var project = CreateTwoParticipantProject(assignSecondParticipant: true);
        var reassigned = ParticipantAssignmentEditor.Reassign(
            project,
            "plan-1",
            "fictional-circle-002",
            new HashSet<GridPosition> { new(2, 0) },
            new GridPosition(2, 0));
        var assignment = reassigned.Plans.Single().Assignments.Single(item => item.ParticipantId == "fictional-circle-002");

        AssertSetEqual(new HashSet<GridPosition> { new(2, 0) }, assignment.OccupiedCells);
        AssertEqual(new GridPosition(2, 0), assignment.ScoringPosition);
    }

    private static void RectangularCellRangesSwapAssignments()
    {
        var swapped = ParticipantAssignmentEditor.SwapCellRegions(
            CreateTwoParticipantProject(assignSecondParticipant: true), "plan-1",
            new GridPosition(0, 0), new GridPosition(2, 0), 2, 1);
        var assignments = swapped.Plans.Single().Assignments.ToDictionary(item => item.ParticipantId);

        AssertSetEqual(new HashSet<GridPosition> { new(2, 0) }, assignments["fictional-circle-001"].OccupiedCells);
        AssertSetEqual(new HashSet<GridPosition> { new(1, 0) }, assignments["fictional-circle-002"].OccupiedCells);
    }

    private static void CellRangesMayIncludeEmptyAisles()
    {
        var deskType = new DeskType("single", "Single", [new GridPosition(0, 0)]);
        var participants = new[]
        {
            new Participant("left", "Left", 1, new Dictionary<string, double>()),
            new Participant("right", "Right", 1, new Dictionary<string, double>()),
        };
        var project = new CircleSpaceProject("1.0", "aisle", "Aisle",
            new Venue("venue", "Venue", 4, 1, new HashSet<GridPosition>()), [deskType], participants,
            new EvaluationConfiguration([], []), [new Plan("plan", "Plan", [
                new DeskPlacement("left-desk", deskType.Id, new GridPosition(0, 0), QuarterTurn.North),
                new DeskPlacement("right-desk", deskType.Id, new GridPosition(2, 0), QuarterTurn.North),
            ], [
                new ParticipantAssignment("left", new HashSet<GridPosition> { new(0, 0) }, new(0, 0)),
                new ParticipantAssignment("right", new HashSet<GridPosition> { new(2, 0) }, new(2, 0)),
            ])]);

        var swapped = ParticipantAssignmentEditor.SwapCellRegions(
            project, "plan", new GridPosition(0, 0), new GridPosition(2, 0), 2, 1);
        var assignments = swapped.Plans.Single().Assignments.ToDictionary(item => item.ParticipantId);

        AssertSetEqual(new HashSet<GridPosition> { new(2, 0) }, assignments["left"].OccupiedCells);
        AssertSetEqual(new HashSet<GridPosition> { new(0, 0) }, assignments["right"].OccupiedCells);
    }

    private static void OverlappingCellRangesCannotBeSwapped()
    {
        try
        {
            ParticipantAssignmentEditor.SwapCellRegions(
                CreateTwoParticipantProject(assignSecondParticipant: true), "plan-1",
                new GridPosition(0, 0), new GridPosition(1, 0), 2, 1);
            throw new InvalidOperationException("Expected overlapping ranges to be rejected.");
        }
        catch (ProjectValidationException exception)
        {
            if (exception.Issues.All(issue => issue.Code != "rangeSwap.overlap"))
                throw;
        }
    }

    private static void OverlappingAssignmentIsRejected()
    {
        var project = CreateTwoParticipantProject(assignSecondParticipant: false);
        try
        {
            ParticipantAssignmentEditor.Assign(
                project,
                "plan-1",
                "fictional-circle-002",
                new HashSet<GridPosition> { new(0, 0) },
                new GridPosition(0, 0));
            throw new InvalidOperationException("Expected the overlapping assignment to fail.");
        }
        catch (ProjectValidationException exception)
        {
            if (exception.Issues.All(issue => issue.Code != "assignment.cell.duplicate"))
                throw;
        }
    }

    private static void DeskCanBeAddedAndRemoved()
    {
        var project = CreateProject();
        var added = PlanDeskEditor.AddDesk(
            project,
            "plan-1",
            new DeskPlacement("desk-2", "standard-desk", new GridPosition(2, 2), QuarterTurn.North));
        AssertEqual(2, added.Plans.Single().DeskPlacements.Count);

        var removed = PlanDeskEditor.RemoveDesk(added, "plan-1", "desk-2");
        AssertEqual(1, removed.Plans.Single().DeskPlacements.Count);
        AssertEqual("desk-1", removed.Plans.Single().DeskPlacements.Single().Id);
    }

    private static void AssignedDeskCannotBeRemoved()
    {
        try
        {
            PlanDeskEditor.RemoveDesk(CreateProject(), "plan-1", "desk-1");
            throw new InvalidOperationException("Expected removal of an assigned desk to fail.");
        }
        catch (ProjectValidationException exception)
        {
            if (exception.Issues.All(issue => issue.Code != "assignment.cell.withoutDesk"))
                throw;
        }
    }

    private static void InvalidNewDeskIsRejected()
    {
        try
        {
            PlanDeskEditor.AddDesk(
                CreateProject(),
                "plan-1",
                new DeskPlacement("desk-outside", "standard-desk", new GridPosition(4, 3), QuarterTurn.North));
            throw new InvalidOperationException("Expected the out-of-bounds desk to fail.");
        }
        catch (ProjectValidationException exception)
        {
            if (exception.Issues.All(issue => issue.Code != "deskPlacement.outOfBounds"))
                throw;
        }
    }

    private static void PlanCanBeRenamedAndUndone()
    {
        var history = new ProjectEditHistory(CreateProject());
        history.Apply(project => PlanCatalogService.RenamePlan(project, "plan-1", "架空配置案・改"));
        AssertEqual("架空配置案・改", history.Current.Plans.Single().Name);

        history.Undo();
        AssertEqual("架空配置案１", history.Current.Plans.Single().Name);
    }

    private static void PlanCanBeRemoved()
    {
        var project = CreateScoredProject();
        var edited = PlanCatalogService.RemovePlan(project, "plan-high");

        AssertEqual(2, edited.Plans.Count);
        AssertEqual(false, edited.Plans.Any(plan => plan.Id == "plan-high"));
        AssertEqual(3, project.Plans.Count);
    }

    private static void LastPlanCannotBeRemoved()
    {
        try
        {
            PlanCatalogService.RemovePlan(CreateProject(), "plan-1");
            throw new InvalidOperationException("Expected removal of the last plan to fail.");
        }
        catch (InvalidOperationException exception) when (exception.Message.Contains("last plan", StringComparison.Ordinal))
        {
        }
    }

    private static void EmptyPlanCanBeCreatedAndEdited()
    {
        var project = PlanCatalogService.CreatePlan(
            CreateProject(),
            "plan-2",
            "架空配置案２",
            "空の状態から作る架空案");
        var created = project.Plans.Single(plan => plan.Id == "plan-2");
        AssertEqual(0, created.DeskPlacements.Count);
        AssertEqual(0, created.Assignments.Count);

        project = PlanDeskEditor.AddDesk(
            project,
            "plan-2",
            new DeskPlacement("plan-2-desk-1", "standard-desk", new GridPosition(2, 1), QuarterTurn.North));
        project = ParticipantAssignmentEditor.Assign(
            project,
            "plan-2",
            "fictional-circle-001",
            new HashSet<GridPosition> { new(2, 1), new(3, 1) },
            new GridPosition(2, 1));

        AssertEqual(1, project.Plans.Single(plan => plan.Id == "plan-2").DeskPlacements.Count);
        AssertEqual(1, project.Plans.Single(plan => plan.Id == "plan-2").Assignments.Count);
    }

    private static void NewPlanIdMustBeUnique()
    {
        try
        {
            PlanCatalogService.CreatePlan(CreateProject(), "plan-1", "重複する架空案");
            throw new InvalidOperationException("Expected the duplicate plan ID to fail.");
        }
        catch (InvalidOperationException exception) when (exception.Message.Contains("already exists", StringComparison.Ordinal))
        {
        }
    }

    private static void WorkspaceRunsEditingFlow()
    {
        var workspace = new ProjectWorkspace(CreateProject());
        workspace.ApplyProjectEdit(project =>
            PlanCatalogService.CreatePlan(project, "plan-2", "架空配置案２"));
        workspace.SelectPlan("plan-2");
        workspace.Apply((project, planId) =>
            PlanDeskEditor.AddDesk(
                project,
                planId,
                new DeskPlacement("plan-2-desk-1", "standard-desk", new GridPosition(2, 1), QuarterTurn.North)));
        workspace.Apply((project, planId) =>
            ParticipantAssignmentEditor.Assign(
                project,
                planId,
                "fictional-circle-001",
                new HashSet<GridPosition> { new(2, 1), new(3, 1) },
                new GridPosition(2, 1)));

        AssertEqual("plan-2", workspace.SelectedPlanId);
        AssertEqual(1, workspace.SelectedPlan.DeskPlacements.Count);
        AssertEqual(1, workspace.SelectedPlan.Assignments.Count);
        AssertEqual(2, workspace.RankPlans().Count);

        workspace.Undo();
        AssertEqual(0, workspace.SelectedPlan.Assignments.Count);
        workspace.Redo();
        AssertEqual(1, workspace.SelectedPlan.Assignments.Count);
    }

    private static void WorkspaceReconcilesSelection()
    {
        var workspace = new ProjectWorkspace(CreateScoredProject(), "plan-high");
        workspace.ApplyProjectEdit(project => PlanCatalogService.RemovePlan(project, "plan-high"));

        AssertEqual("plan-low-a", workspace.SelectedPlanId);
        workspace.Undo();
        workspace.SelectPlan("plan-high");
        AssertEqual("plan-high", workspace.SelectedPlanId);
    }

    private static void PlanSnapshotContainsViewData()
    {
        var snapshot = PlanViewService.Build(
            CreateTwoParticipantProject(assignSecondParticipant: false),
            "plan-1");

        AssertEqual(2, snapshot.Desks.Count);
        AssertEqual(1, snapshot.Assignments.Count);
        AssertEqual("架空サークル１", snapshot.Assignments[0].ParticipantName);
        AssertEqual(new GridPosition(0, 0), snapshot.Assignments[0].ScoringPosition);
        AssertEqual(1, snapshot.UnassignedParticipants.Count);
        AssertEqual("fictional-circle-002", snapshot.UnassignedParticipants[0].ParticipantId);
        AssertEqual(0d, snapshot.Evaluation.TotalScore);
    }

    private static void WorkspaceExposesSelectedSnapshot()
    {
        var workspace = new ProjectWorkspace(CreateScoredProject(), "plan-high");
        var snapshot = workspace.GetSelectedPlanSnapshot();

        AssertEqual("plan-high", snapshot.PlanId);
        AssertEqual(10d, snapshot.Evaluation.TotalScore);
        AssertEqual(0, snapshot.UnassignedParticipants.Count);
    }

    private static void VenueCanBeExpanded()
    {
        var edited = VenueEditor.Resize(CreateProject(), 8, 6);
        AssertEqual(8, edited.Venue.Width);
        AssertEqual(6, edited.Venue.Height);
    }

    private static void VenueCannotShrinkAcrossDesk()
    {
        try
        {
            VenueEditor.Resize(CreateProject(), 1, 1);
            throw new InvalidOperationException("Expected venue shrink to fail.");
        }
        catch (ProjectValidationException exception)
        {
            if (exception.Issues.All(issue => issue.Code != "deskPlacement.outOfBounds"))
                throw;
        }
    }

    private static void PillarBlocksAndFreesVenueCell()
    {
        var cell = new GridPosition(4, 3);
        var withPillar = VenueEditor.AddPillar(CreateProject(), cell);

        AssertEqual(true, withPillar.Venue.BlockedCells.Contains(cell));
        AssertEqual(false, withPillar.Venue.CanPlaceAt(cell));

        var withoutPillar = VenueEditor.RemovePillar(withPillar, cell);
        AssertEqual(false, withoutPillar.Venue.BlockedCells.Contains(cell));
        AssertEqual(true, withoutPillar.Venue.CanPlaceAt(cell));
    }

    private static void DesksFillAvailableCells()
    {
        var project = CreateProject() with
        {
            Participants = [],
            Plans = [CreateProject().Plans[0] with { DeskPlacements = [], Assignments = [] }],
            Venue = CreateProject().Venue with { Width = 5, Height = 2 },
        };
        var edited = DeskLayoutService.FillAvailableCells(project, "plan-1", "standard-desk");

        AssertEqual(4, edited.Plans.Single().DeskPlacements.Count);
        var occupied = edited.Plans.Single().DeskPlacements
            .SelectMany(placement => placement.GetOccupiedCells(edited.DeskTypes.Single()))
            .ToHashSet();
        AssertEqual(8, occupied.Count);
        AssertEqual(false, occupied.Contains(new GridPosition(4, 0)));
        AssertEqual(false, occupied.Contains(new GridPosition(4, 1)));
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
        var plan = new Plan(
            "plan-1",
            "架空配置案１",
            [new DeskPlacement("desk-1", deskType.Id, new GridPosition(0, 0), QuarterTurn.North)],
            [new ParticipantAssignment(
                participant.Id,
                new HashSet<GridPosition> { new(0, 0), new(1, 0) },
                new GridPosition(0, 0))]);
        return new CircleSpaceProject(
            "1.0",
            "fictional-project",
            "架空プロジェクト",
            new Venue("fictional-venue", "架空会場", 5, 4, new HashSet<GridPosition>()),
            [deskType],
            [participant],
            new EvaluationConfiguration([], []),
            [plan]);
    }

    private static CircleSpaceProject CreateScoredProject()
    {
        var deskType = new DeskType("single-desk", "架空の1セル机", [new GridPosition(0, 0)]);
        var participant = new Participant(
            "fictional-circle-001",
            "架空サークル１",
            1,
            new Dictionary<string, double> { ["test-feature"] = 10d });
        var feature = new EvaluationFeature("test-feature", "架空評価項目", 1d, 0d, 1d);
        var weightMap = new WeightMap(
            feature.Id,
            0d,
            new Dictionary<GridPosition, double>
            {
                [new GridPosition(0, 0)] = 0.5d,
                [new GridPosition(1, 0)] = 1d,
                [new GridPosition(2, 0)] = 0.5d,
            });
        Plan MakePlan(string id, GridPosition position) => new(
            id,
            id,
            [new DeskPlacement($"{id}-desk", deskType.Id, position, QuarterTurn.North)],
            [new ParticipantAssignment(participant.Id, new HashSet<GridPosition> { position }, position)]);

        return new CircleSpaceProject(
            "1.0",
            "fictional-scored-project",
            "架空の比較プロジェクト",
            new Venue("fictional-venue", "架空会場", 3, 1, new HashSet<GridPosition>()),
            [deskType],
            [participant],
            new EvaluationConfiguration([feature], [weightMap]),
            [
                MakePlan("plan-low-a", new GridPosition(0, 0)),
                MakePlan("plan-high", new GridPosition(1, 0)),
                MakePlan("plan-low-b", new GridPosition(2, 0)),
            ]);
    }

    private static CircleSpaceProject CreateTwoParticipantProject(bool assignSecondParticipant)
    {
        var deskType = new DeskType(
            "standard-desk",
            "架空の標準机",
            [new GridPosition(0, 0), new GridPosition(1, 0)]);
        var participants = new[]
        {
            new Participant("fictional-circle-001", "架空サークル１", 1, new Dictionary<string, double>()),
            new Participant("fictional-circle-002", "架空サークル２", 1, new Dictionary<string, double>()),
        };
        var assignments = new List<ParticipantAssignment>
        {
            new(participants[0].Id, new HashSet<GridPosition> { new(0, 0) }, new GridPosition(0, 0)),
        };
        if (assignSecondParticipant)
        {
            assignments.Add(new ParticipantAssignment(
                participants[1].Id,
                new HashSet<GridPosition> { new(3, 0) },
                new GridPosition(3, 0)));
        }

        return new CircleSpaceProject(
            "1.0",
            "fictional-assignment-project",
            "架空の割当プロジェクト",
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
                assignments)]);
    }

    private static void ParticipantImportReconcilesCatalog()
    {
        var project = CreateTwoParticipantProject(assignSecondParticipant: true);
        var result = ParticipantCatalogService.ReplaceParticipants(project,
        [
            new ParticipantImportRow("fictional-circle-001", "更新したサークル", 1, "ABC1234"),
            new ParticipantImportRow("ABC1234", "ぐれーすけーる", 1, "fictional-circle-001"),
        ]);

        AssertEqual(2, result.Participants.Count);
        AssertEqual("fictional-circle-001", result.Participants[0].Id);
        AssertEqual("fictional-circle-001", result.Participants[0].CircleId);
        AssertEqual("更新したサークル", result.Participants[0].DisplayName);
        AssertEqual("ABC1234", result.Participants[1].CircleId);
        AssertEqual("ABC1234", result.Participants[0].CombinedWithCircleId!);
        AssertEqual(1, result.Plans[0].Assignments.Count);
        AssertEqual("fictional-circle-001", result.Plans[0].Assignments[0].ParticipantId);
    }

    private static void ReferencedDeskLayoutCannotBeRemoved()
    {
        var project = CreateProject();
        var migrated = LayoutProjection.MigrateLegacyPlans(project);
        var deskLayoutId = migrated.DeskLayouts.Single().Id;
        var circleLayoutId = migrated.CircleLayouts.Single().Id;
        var rejected = false;
        try
        {
            LayoutCatalogService.RemoveDeskLayout(migrated, deskLayoutId);
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        if (!rejected)
            throw new InvalidOperationException("A referenced desk layout must not be removable.");

        var withoutCircle = LayoutCatalogService.RemoveCircleLayout(migrated, circleLayoutId);
        var withoutDesk = LayoutCatalogService.RemoveDeskLayout(withoutCircle, deskLayoutId);
        AssertEqual(0, withoutDesk.DeskLayouts.Count);
    }

    private static void CircleLayoutCanBeRelinked()
    {
        var project = LayoutProjection.MigrateLegacyPlans(CreateProject());
        var added = LayoutCatalogService.CreateDeskLayout(project, "desk-layout-extra", "Extra desk layout");
        var originalDeskId = added.CircleLayouts.Single().DeskLayoutId;
        var withEmptyCircle = LayoutCatalogService.CreateCircleLayout(added, "circle-layout-extra", "Extra circle layout", originalDeskId);
        var relinked = LayoutCatalogService.ReassignCircleLayout(withEmptyCircle, "circle-layout-extra", "desk-layout-extra");
        AssertEqual("desk-layout-extra", relinked.CircleLayouts.Single(item => item.Id == "circle-layout-extra").DeskLayoutId);
        AssertEqual("desk-layout-extra", relinked.DeskLayouts.Single(item => item.Id == "desk-layout-extra").Id);
    }

    private static void LayoutsCanBeRenamed()
    {
        var project = LayoutProjection.MigrateLegacyPlans(CreateProject());
        var deskId = project.DeskLayouts.Single().Id;
        var circleId = project.CircleLayouts.Single().Id;
        var renamedDesk = LayoutCatalogService.RenameDeskLayout(project, deskId, "Renamed desk");
        var renamedBoth = LayoutCatalogService.RenameCircleLayout(renamedDesk, circleId, "Renamed circle");
        AssertEqual("Renamed desk", renamedBoth.DeskLayouts.Single().Name);
        AssertEqual("Renamed circle", renamedBoth.CircleLayouts.Single().Name);
    }

    private static void UnusedDeskSelection()
    {
        var workspace = new ProjectWorkspace(LayoutCatalogService.CreateDeskLayout(CreateProject(), "unused", "Unused"));
        var originalPlanId = workspace.SelectedPlanId;
        workspace.SelectDeskLayout("unused");
        AssertEqual("unused", workspace.SelectedDeskLayoutId);
        AssertEqual(false, workspace.HasSelectedCircleLayout);
        AssertEqual(true, workspace.CanRemoveSelectedDeskLayout);
        AssertEqual(0, workspace.GetSelectedPlanSnapshot().Desks.Count);
        var circleCount = workspace.Project.CircleLayouts.Count;
        var deskTypeId = workspace.Project.DeskTypes[0].Id;
        workspace.Apply((project, id) => PlanDeskEditor.AddDesk(project, id,
            new DeskPlacement("new-desk", deskTypeId, new GridPosition(0, 0), QuarterTurn.North)));
        AssertEqual(1, workspace.GetSelectedPlanSnapshot().Desks.Count);
        AssertEqual(1, workspace.Project.DeskLayouts.Single(desk => desk.Id == "unused").DeskPlacements.Count);
        AssertEqual(circleCount, workspace.Project.CircleLayouts.Count);
        AssertEqual(false, workspace.Project.Plans.Any(plan => plan.Id == workspace.SelectedPlanId));
        workspace.Undo();
        AssertEqual(0, workspace.GetSelectedPlanSnapshot().Desks.Count);
        workspace.Redo();
        AssertEqual(1, workspace.GetSelectedPlanSnapshot().Desks.Count);
        workspace.ApplyProjectEdit(project => LayoutCatalogService.RemoveDeskLayout(project, "unused"));
        AssertEqual(originalPlanId, workspace.SelectedPlanId);
        AssertEqual(false, workspace.CanRemoveSelectedDeskLayout);
        workspace.Undo();
        workspace.SelectDeskLayout("unused");
        AssertEqual(1, workspace.GetSelectedPlanSnapshot().Desks.Count);
        workspace.ApplyProjectEdit(project => LayoutCatalogService.CreateCircleLayout(project, "child", "Child", "unused"));
        AssertEqual(true, workspace.HasSelectedCircleLayout);
        AssertEqual("child", workspace.SelectedPlanId);
        AssertEqual(false, workspace.CanRemoveSelectedDeskLayout);
    }

    private static void RelinkedDeskSelection()
    {
        var project = LayoutProjection.MigrateLegacyPlans(CreateProject());
        var oldDesk = project.DeskLayouts[0];
        project = project with { DeskLayouts = [.. project.DeskLayouts, oldDesk with { Id = "target" }] };
        var workspace = new ProjectWorkspace(project);
        var circleId = workspace.SelectedPlanId;
        workspace.ApplyProjectEdit(value => LayoutCatalogService.ReassignCircleLayout(value, circleId, "target"));
        workspace.SelectDeskLayout(oldDesk.Id);
        AssertEqual(oldDesk.Id, workspace.SelectedDeskLayoutId);
        AssertEqual(true, workspace.CanRemoveSelectedDeskLayout);
        AssertEqual(false, workspace.HasSelectedCircleLayout);
        AssertEqual(oldDesk.DeskPlacements.Count, workspace.GetSelectedPlanSnapshot().Desks.Count);
        workspace.SelectDeskLayout("target");
        AssertEqual("target", workspace.SelectedDeskLayoutId);
        AssertEqual(circleId, workspace.SelectedPlanId);
        AssertEqual(false, workspace.CanRemoveSelectedDeskLayout);
    }

    private static void AssertSetEqual<T>(IReadOnlySet<T> expected, IReadOnlySet<T> actual)
    {
        if (!expected.SetEquals(actual))
            throw new InvalidOperationException($"Expected [{string.Join(", ", expected)}], actual [{string.Join(", ", actual)}].");
    }

    private static void AssertEqual<T>(T expected, T actual)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected '{expected}', actual '{actual}'.");
    }
}
