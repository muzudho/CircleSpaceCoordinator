namespace CircleSpaceCoordinator.Core.Tests;

using CircleSpaceCoordinator.Core.Evaluation;
using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Core.Validation;

internal static class Program
{
    private static int Main()
    {
        var tests = new (string Name, Action Run)[]
        {
            ("Desk footprint rotates by quarter turns", DeskFootprintRotates),
            ("Hex RGB colors parse in #RRGGBB format", HexRgbColorsParse),
            ("Two-cell participant is scored only at circle-ID cell", TwoCellParticipantUsesScoringPositionOnce),
            ("Plans are ranked by total evaluation score", PlansCanBeCompared),
            ("Scoring position must be an occupied participant cell", InvalidScoringPositionIsRejected),
            ("Participant must receive the required number of cells", InvalidParticipantCellCountIsRejected),
            ("Participant cells must be placed on a desk", ParticipantCellWithoutDeskIsRejected),
            ("Combined-space participants may share one desk", CombinedSpaceOnOneDeskIsAccepted),
            ("Combined-space participants may not be split across desks", CombinedSpaceAcrossDesksIsRejected),
            ("One circle may use one cell of a two-cell desk", OneCircleMayUseHalfDesk),
            ("Desk cells may remain empty", DeskCellsMayRemainEmpty),
            ("General-attendee genre score halves when one genre is split", GenreSplitHalvesGeneralScore),
            ("An empty desk cell keeps same-genre circles connected", EmptyDeskCellKeepsGenreConnected),
            ("A separated approved combined circle zeros both audience scores", SeparatedCombinedCircleZerosScores),
            ("A combined circle shares genre connectivity without increasing its genre count", CombinedCircleSharesGenreConnectivity),
            ("Adjacent desks with the same orientation form an automatic desk run", AdjacentDesksFormRun),
            ("An island connector joins separated genre intervals around a ring", IslandConnectorJoinsGenreIntervals),
            ("An island connector joins the free ends of adjacent desk runs", IslandConnectorUsesFreeRunEnds),
            ("A disabled automatic connection separates physically adjacent desks", DisabledAutomaticConnectionSeparatesDesks),
            ("A disabled automatic connection separates cells within one desk", DisabledAutomaticConnectionSeparatesCellsWithinDesk),
            ("A facing region joins genre groups with the aisle coefficient", FacingRegionUsesAisleCoefficient),
            ("A pillar in an aisle blocks a facing region", PillarBlocksFacingRegion),
            ("A facing region does not match diagonal cells", FacingRegionRejectsDiagonalMatch),
            ("A facing region ignores desks whose island sides do not face the aisle", FacingRegionRequiresFacingOrientations),
        };

        var failed = 0;
        foreach (var test in tests)
        {
            try
            {
                test.Run();
                Console.WriteLine($"PASS: {test.Name}");
            }
            catch (Exception exception)
            {
                failed++;
                Console.Error.WriteLine($"FAIL: {test.Name}");
                Console.Error.WriteLine(exception.Message);
            }
        }

        Console.WriteLine($"{tests.Length - failed}/{tests.Length} tests passed.");
        return failed == 0 ? 0 : 1;
    }

    private static void DeskFootprintRotates()
    {
        var deskType = StandardDesk();
        var placement = new DeskPlacement("desk", deskType.Id, new GridPosition(3, 2), QuarterTurn.East);
        AssertSetEqual(
            new HashSet<GridPosition> { new(3, 2), new(3, 3) },
            placement.GetOccupiedCells(deskType));
    }

    private static void HexRgbColorsParse()
    {
        AssertEqual(true, RgbHexColor.TryParse("#FF6347", out var red, out var green, out var blue));
        AssertEqual((byte)255, red);
        AssertEqual((byte)99, green);
        AssertEqual((byte)71, blue);
        AssertEqual(false, RgbHexColor.TryParse("#FFF", out _, out _, out _));
        AssertEqual(false, RgbHexColor.TryParse("FF6347", out _, out _, out _));
        AssertEqual(false, RgbHexColor.TryParse("#GG6347", out _, out _, out _));
    }

    private static void TwoCellParticipantUsesScoringPositionOnce()
    {
        var project = CreateProject();
        var result = ProjectEvaluator.Evaluate(project).Single(item => item.PlanId == "plan-main");
        var feature = result.Features.Single();

        // The participant occupies (1,1) and (2,1). Only (1,1), where the former
        // Excel circle ID is stored, contributes: 10 * 0.5 = 5.
        AssertEqual(5d, feature.RawSum);
        AssertEqual(12d, feature.AdjustedScore); // 5 * scale 2 + offset 2
        AssertEqual(18d, feature.WeightedScore); // 12 * overall weight 1.5
        AssertEqual(18d, result.TotalScore);
    }

    private static void PlansCanBeCompared()
    {
        var project = CreateProject(includeSecondPlan: true);
        var results = ProjectEvaluator.Evaluate(project).OrderByDescending(item => item.TotalScore).ToArray();

        AssertEqual("plan-other", results[0].PlanId);
        AssertEqual(2, results.Length);
    }

    private static void GenreSplitHalvesGeneralScore()
    {
        var deskType = new DeskType("single", "1セル机", [new GridPosition(0, 0)]);
        var participants = new[]
        {
            new Participant("a", "A", 1, new Dictionary<string, double>()) { GenreId = "game" },
            new Participant("b", "B", 1, new Dictionary<string, double>()) { GenreId = "game" },
        };
        var desks = new[]
        {
            new DeskPlacement("d0", deskType.Id, new GridPosition(0, 0), QuarterTurn.North),
            new DeskPlacement("d1", deskType.Id, new GridPosition(1, 0), QuarterTurn.North),
            new DeskPlacement("d3", deskType.Id, new GridPosition(3, 0), QuarterTurn.North),
        };
        var together = new Plan("together", "連続", desks,
        [
            new ParticipantAssignment("a", new HashSet<GridPosition> { new(0, 0) }, new GridPosition(0, 0)),
            new ParticipantAssignment("b", new HashSet<GridPosition> { new(1, 0) }, new GridPosition(1, 0)),
        ]);
        var split = together with
        {
            Id = "split",
            Name = "分断",
            Assignments =
            [
                together.Assignments[0],
                new ParticipantAssignment("b", new HashSet<GridPosition> { new(3, 0) }, new GridPosition(3, 0)),
            ],
        };
        var project = new CircleSpaceProject("1.0", "genre-project", "ジャンル評価",
            new Venue("venue", "会場", 4, 1, new HashSet<GridPosition>()), [deskType], participants,
            new EvaluationConfiguration([], []), [together, split]);

        var results = GeneralAttendeeEvaluator.Evaluate(project).ToDictionary(item => item.PlanId);
        AssertEqual(2d, results["together"].GeneralAttendeeScore);
        AssertEqual(1d, results["split"].GeneralAttendeeScore);
    }

    private static void EmptyDeskCellKeepsGenreConnected()
    {
        var deskType = new DeskType("single", "1 cell desk", [new GridPosition(0, 0)]);
        var participants = new[]
        {
            new Participant("left", "Left", 1, new Dictionary<string, double>()) { GenreId = "game" },
            new Participant("right", "Right", 1, new Dictionary<string, double>()) { GenreId = "game" },
        };
        var plan = new Plan("empty-gap", "Empty gap", [
            new DeskPlacement("left-desk", deskType.Id, new GridPosition(0, 0), QuarterTurn.North),
            new DeskPlacement("empty-desk", deskType.Id, new GridPosition(1, 0), QuarterTurn.North),
            new DeskPlacement("right-desk", deskType.Id, new GridPosition(2, 0), QuarterTurn.North),
        ], [
            new ParticipantAssignment("left", new HashSet<GridPosition> { new(0, 0) }, new GridPosition(0, 0)),
            new ParticipantAssignment("right", new HashSet<GridPosition> { new(2, 0) }, new GridPosition(2, 0)),
        ]);
        var project = new CircleSpaceProject("1.0", "empty-gap", "Empty gap",
            new Venue("venue", "Venue", 3, 1, new HashSet<GridPosition>()), [deskType], participants,
            new EvaluationConfiguration([], []), [plan]);

        var game = GeneralAttendeeEvaluator.Evaluate(project).Single().Genres.Single();

        AssertEqual(1, game.ContinuousGroupCount);
        AssertEqual(2d, game.Score);
    }

    private static void SeparatedCombinedCircleZerosScores()
    {
        var deskType = StandardDesk();
        var participants = new[]
        {
            new Participant("a", "合体A", 1, new Dictionary<string, double>())
                { CircleId = "A", CombinedWithCircleId = "B", GenreId = "game" },
            new Participant("b", "合体B", 1, new Dictionary<string, double>())
                { CircleId = "B", CombinedWithCircleId = "A", GenreId = "game" },
        };
        var plan = new Plan("split", "泣き別れ",
        [
            new DeskPlacement("desk-a", deskType.Id, new GridPosition(0, 0), QuarterTurn.North),
            new DeskPlacement("desk-b", deskType.Id, new GridPosition(3, 0), QuarterTurn.North),
        ],
        [
            new ParticipantAssignment("a", new HashSet<GridPosition> { new(0, 0) }, new GridPosition(0, 0)),
            new ParticipantAssignment("b", new HashSet<GridPosition> { new(3, 0) }, new GridPosition(3, 0)),
        ]);
        var project = new CircleSpaceProject("1.0", "combined-required", "合体契約",
            new Venue("venue", "会場", 5, 1, new HashSet<GridPosition>()), [deskType], participants,
            new EvaluationConfiguration([], []), [plan]);

        var result = GeneralAttendeeEvaluator.Evaluate(project).Single();
        AssertEqual(false, result.CombinedSpaceRequirementsSatisfied);
        AssertEqual(0d, result.GeneralAttendeeScore);
        AssertEqual(0d, result.CircleParticipantScore);
    }

    private static void CombinedCircleSharesGenreConnectivity()
    {
        var deskType = StandardDesk();
        var participants = new[]
        {
            new Participant("game-left", "ゲーム左", 1, new Dictionary<string, double>()) { GenreId = "game" },
            new Participant("partner", "合体相手", 1, new Dictionary<string, double>()) { GenreId = "other" },
            new Participant("game-right", "ゲーム右", 1, new Dictionary<string, double>()) { GenreId = "game" },
        };
        var plan = new Plan("combined-bridge", "合体接続", [
            new DeskPlacement("combined-desk", deskType.Id, new GridPosition(0, 0), QuarterTurn.North),
            new DeskPlacement("next-desk", deskType.Id, new GridPosition(2, 0), QuarterTurn.North),
        ], [
            new ParticipantAssignment("game-left", new HashSet<GridPosition> { new(0, 0) }, new GridPosition(0, 0)) { CombinedSpaceId = "combined-1" },
            new ParticipantAssignment("partner", new HashSet<GridPosition> { new(1, 0) }, new GridPosition(1, 0)) { CombinedSpaceId = "combined-1" },
            new ParticipantAssignment("game-right", new HashSet<GridPosition> { new(2, 0) }, new GridPosition(2, 0)),
        ]);
        var project = new CircleSpaceProject("1.0", "combined-bridge", "合体接続評価",
            new Venue("venue", "会場", 4, 1, new HashSet<GridPosition>()), [deskType], participants,
            new EvaluationConfiguration([], []), [plan]);

        var game = GeneralAttendeeEvaluator.Evaluate(project).Single().Genres.Single(item => item.GenreId == "game");
        AssertEqual(2, game.AssignedCircleCount);
        AssertEqual(1, game.ContinuousGroupCount);
        AssertEqual(2d, game.Score);
    }

    private static void InvalidScoringPositionIsRejected()
    {
        var project = CreateProject();
        var assignment = project.Plans[0].Assignments[0] with { ScoringPosition = new GridPosition(3, 1) };
        var invalid = project with
        {
            Plans = [project.Plans[0] with { Assignments = [assignment] }],
        };

        AssertContainsIssue(ProjectValidator.Validate(invalid), "assignment.scoringPosition");
    }

    private static void InvalidParticipantCellCountIsRejected()
    {
        var project = CreateProject();
        var assignment = project.Plans[0].Assignments[0] with
        {
            OccupiedCells = new HashSet<GridPosition> { new(1, 1) },
        };
        var invalid = project with
        {
            Plans = [project.Plans[0] with { Assignments = [assignment] }],
        };

        AssertContainsIssue(ProjectValidator.Validate(invalid), "assignment.cellCount");
    }

    private static void ParticipantCellWithoutDeskIsRejected()
    {
        var project = CreateProject();
        var assignment = project.Plans[0].Assignments[0] with
        {
            OccupiedCells = new HashSet<GridPosition> { new(1, 1), new(5, 3) },
        };
        var invalid = project with
        {
            Plans = [project.Plans[0] with { Assignments = [assignment] }],
        };

        AssertContainsIssue(ProjectValidator.Validate(invalid), "assignment.cell.withoutDesk");
    }

    private static void CombinedSpaceOnOneDeskIsAccepted()
    {
        var project = CreateCombinedSpaceProject(splitAcrossDesks: false);
        var issues = ProjectValidator.Validate(project);
        if (issues.Count != 0)
            throw new InvalidOperationException($"Expected a valid combined space, but got: {string.Join(", ", issues.Select(issue => issue.Code))}");
    }

    private static void CombinedSpaceAcrossDesksIsRejected()
    {
        var project = CreateCombinedSpaceProject(splitAcrossDesks: true);
        AssertContainsIssue(ProjectValidator.Validate(project), "combinedSpace.splitAcrossDesks");
    }

    private static void OneCircleMayUseHalfDesk()
    {
        var project = CreateCombinedSpaceProject(splitAcrossDesks: false);
        var oneCircle = project with
        {
            Participants = [project.Participants[0]],
            Plans = [project.Plans[0] with { Assignments = [project.Plans[0].Assignments[0] with { CombinedSpaceId = null }] }],
        };
        AssertEqual(0, ProjectValidator.Validate(oneCircle).Count);
    }

    private static void DeskCellsMayRemainEmpty()
    {
        var project = CreateCombinedSpaceProject(splitAcrossDesks: false);
        var empty = project with
        {
            Participants = [],
            Plans = [project.Plans[0] with { Assignments = [] }],
        };
        AssertEqual(0, ProjectValidator.Validate(empty).Count);
    }

    private static void AdjacentDesksFormRun()
    {
        var deskType = StandardDesk();
        var plan = new Plan("plan", "机列", [
            new DeskPlacement("a", deskType.Id, new GridPosition(0, 0), QuarterTurn.North),
            new DeskPlacement("b", deskType.Id, new GridPosition(2, 0), QuarterTurn.North),
            new DeskPlacement("c", deskType.Id, new GridPosition(0, 2), QuarterTurn.South),
        ], []);

        var runs = DeskRunDetector.Detect(plan, new Dictionary<string, DeskType> { [deskType.Id] = deskType });

        AssertEqual(2, runs.Count);
        AssertEqual(2, runs.Single(run => run.Orientation == QuarterTurn.North).DeskIds.Count);
        AssertEqual("a", runs.Single(run => run.Orientation == QuarterTurn.North).DeskIds[0]);
        AssertEqual("b", runs.Single(run => run.Orientation == QuarterTurn.North).DeskIds[1]);
    }

    private static void IslandConnectorJoinsGenreIntervals()
    {
        var project = CreateTopologyEvaluationProject();
        var open = GeneralAttendeeEvaluator.Evaluate(project).Single().Genres.Single(item => item.GenreId == "game");
        var closedPlan = project.Plans[0] with
        {
            IslandConnectors = [new IslandConnector("close-ring", "d0", "d2")],
        };
        var closed = GeneralAttendeeEvaluator.Evaluate(project with { Plans = [closedPlan] })
            .Single().Genres.Single(item => item.GenreId == "game");

        AssertEqual(2, open.ContinuousGroupCount);
        AssertEqual(1d, open.Score);
        AssertEqual(1, closed.ContinuousGroupCount);
        AssertEqual(2d, closed.Score);
    }

    private static void IslandConnectorUsesFreeRunEnds()
    {
        var deskType = StandardDesk();
        var plan = new Plan("parallel-runs", "並行机列", [
            new DeskPlacement("left-top", deskType.Id, new GridPosition(0, 0), QuarterTurn.East),
            new DeskPlacement("left-bottom", deskType.Id, new GridPosition(0, 2), QuarterTurn.East),
            new DeskPlacement("right-top", deskType.Id, new GridPosition(1, 0), QuarterTurn.East),
            new DeskPlacement("right-bottom", deskType.Id, new GridPosition(1, 2), QuarterTurn.East),
        ], [])
        {
            IslandConnectors = [new IslandConnector("bottom-link", "left-bottom", "right-bottom")],
        };
        var project = new CircleSpaceProject("1.0", "parallel-runs", "自由端判定",
            new Venue("venue", "会場", 2, 4, new HashSet<GridPosition>()), [deskType], [],
            new EvaluationConfiguration([], []), [plan]);

        var topology = VenueTopologyAnalyzer.Build(project, plan);
        AssertEqual(true, topology.Neighbors[new GridPosition(0, 3)].Contains(new GridPosition(1, 3)));
        AssertEqual(false, topology.Neighbors[new GridPosition(0, 2)].Contains(new GridPosition(1, 2)));
    }

    private static void DisabledAutomaticConnectionSeparatesDesks()
    {
        var deskType = new DeskType("single", "1 cell desk", [new GridPosition(0, 0)]);
        var plan = new Plan("separate", "Separate", [
            new DeskPlacement("left", deskType.Id, new GridPosition(0, 0), QuarterTurn.North),
            new DeskPlacement("right", deskType.Id, new GridPosition(1, 0), QuarterTurn.North),
        ], [])
        {
            DisabledIslandConnections = [new DisabledIslandConnection(new(0, 0), new(1, 0))],
        };
        var project = new CircleSpaceProject("1.0", "separate", "Separate",
            new Venue("venue", "Venue", 2, 1, new HashSet<GridPosition>()), [deskType], [],
            new EvaluationConfiguration([], []), [plan]);

        var topology = VenueTopologyAnalyzer.Build(project, plan);

        AssertEqual(false, topology.Neighbors[new GridPosition(0, 0)].Contains(new GridPosition(1, 0)));
    }

    private static void DisabledAutomaticConnectionSeparatesCellsWithinDesk()
    {
        var deskType = new DeskType("double", "2 cell desk", [new GridPosition(0, 0), new GridPosition(1, 0)]);
        var plan = new Plan("separate-cells", "Separate cells", [
            new DeskPlacement("desk", deskType.Id, new GridPosition(0, 0), QuarterTurn.North),
        ], [])
        {
            DisabledIslandConnections = [new DisabledIslandConnection(new(0, 0), new(1, 0))],
        };
        var project = new CircleSpaceProject("1.0", "separate-cells", "Separate cells",
            new Venue("venue", "Venue", 2, 1, new HashSet<GridPosition>()), [deskType], [],
            new EvaluationConfiguration([], []), [plan]);

        var topology = VenueTopologyAnalyzer.Build(project, plan);

        AssertEqual(false, topology.Neighbors[new GridPosition(0, 0)].Contains(new GridPosition(1, 0)));
    }

    private static void FacingRegionUsesAisleCoefficient()
    {
        var deskType = new DeskType("single", "1セル机", [new GridPosition(0, 0)]);
        var participants = new[]
        {
            new Participant("a", "A", 1, new Dictionary<string, double>()) { GenreId = "game" },
            new Participant("b", "B", 1, new Dictionary<string, double>()) { GenreId = "game" },
        };
        var plan = new Plan("facing", "向かい合わせ", [
            new DeskPlacement("left", deskType.Id, new GridPosition(0, 0), QuarterTurn.East),
            new DeskPlacement("right", deskType.Id, new GridPosition(2, 0), QuarterTurn.West),
        ], [
            new ParticipantAssignment("a", new HashSet<GridPosition> { new(0, 0) }, new GridPosition(0, 0)),
            new ParticipantAssignment("b", new HashSet<GridPosition> { new(2, 0) }, new GridPosition(2, 0)),
        ]) { FacingRegions = [new FacingRegion("aisle", new(0, 0), new(2, 0))] };
        var project = new CircleSpaceProject("1.0", "facing", "向かい合わせ評価",
            new Venue("venue", "会場", 3, 1, new HashSet<GridPosition>()), [deskType], participants,
            new EvaluationConfiguration([], []), [plan]);

        var result = GeneralAttendeeEvaluator.Evaluate(project).Single().Genres.Single();
        AssertEqual(1, result.ContinuousGroupCount);
        AssertEqual(true, Math.Abs(result.Score - 1.4d) < 0.000001d);
    }

    private static void FacingRegionRejectsDiagonalMatch()
    {
        var deskType = new DeskType("single", "1セル机", [new GridPosition(0, 0)]);
        var participants = new[]
        {
            new Participant("a", "A", 1, new Dictionary<string, double>()) { GenreId = "game" },
            new Participant("b", "B", 1, new Dictionary<string, double>()) { GenreId = "game" },
        };
        var plan = new Plan("diagonal", "斜め", [
            new DeskPlacement("left", deskType.Id, new GridPosition(0, 0), QuarterTurn.East),
            new DeskPlacement("right", deskType.Id, new GridPosition(2, 1), QuarterTurn.West),
        ], [
            new ParticipantAssignment("a", new HashSet<GridPosition> { new(0, 0) }, new GridPosition(0, 0)),
            new ParticipantAssignment("b", new HashSet<GridPosition> { new(2, 1) }, new GridPosition(2, 1)),
        ]) { FacingRegions = [new FacingRegion("aisle", new(0, 0), new(2, 1))] };
        var project = new CircleSpaceProject("1.0", "diagonal", "斜め除外",
            new Venue("venue", "会場", 3, 2, new HashSet<GridPosition>()), [deskType], participants,
            new EvaluationConfiguration([], []), [plan]);

        var result = GeneralAttendeeEvaluator.Evaluate(project).Single().Genres.Single();
        AssertEqual(2, result.ContinuousGroupCount);
        AssertEqual(1d, result.Score);
    }

    private static void PillarBlocksFacingRegion()
    {
        var deskType = new DeskType("single", "1 cell desk", [new GridPosition(0, 0)]);
        var plan = new Plan("pillar", "Pillar", [
            new DeskPlacement("left", deskType.Id, new GridPosition(0, 0), QuarterTurn.East),
            new DeskPlacement("right", deskType.Id, new GridPosition(2, 0), QuarterTurn.West),
        ], []) { FacingRegions = [new FacingRegion("aisle", new(0, 0), new(2, 0))] };
        var project = new CircleSpaceProject("1.0", "pillar", "Pillar",
            new Venue("venue", "Venue", 3, 1, new HashSet<GridPosition> { new(1, 0) }), [deskType], [],
            new EvaluationConfiguration([], []), [plan]);

        var topology = VenueTopologyAnalyzer.Build(project, plan);

        AssertEqual(0, topology.FacingCellPairs.Count);
    }

    private static void FacingRegionRequiresFacingOrientations()
    {
        var deskType = new DeskType("single", "1セル机", [new GridPosition(0, 0)]);
        var participants = new[]
        {
            new Participant("a", "A", 1, new Dictionary<string, double>()) { GenreId = "game" },
            new Participant("b", "B", 1, new Dictionary<string, double>()) { GenreId = "game" },
        };
        var plan = new Plan("wrong-way", "背中合わせ", [
            new DeskPlacement("left", deskType.Id, new GridPosition(0, 0), QuarterTurn.West),
            new DeskPlacement("right", deskType.Id, new GridPosition(2, 0), QuarterTurn.East),
        ], [
            new ParticipantAssignment("a", new HashSet<GridPosition> { new(0, 0) }, new GridPosition(0, 0)),
            new ParticipantAssignment("b", new HashSet<GridPosition> { new(2, 0) }, new GridPosition(2, 0)),
        ]) { FacingRegions = [new FacingRegion("aisle", new(0, 0), new(2, 0))] };
        var project = new CircleSpaceProject("1.0", "wrong-way", "方向判定",
            new Venue("venue", "会場", 3, 1, new HashSet<GridPosition>()), [deskType], participants,
            new EvaluationConfiguration([], []), [plan]);

        var result = GeneralAttendeeEvaluator.Evaluate(project).Single().Genres.Single();
        AssertEqual(2, result.ContinuousGroupCount);
        AssertEqual(1d, result.Score);
    }

    private static CircleSpaceProject CreateTopologyEvaluationProject()
    {
        var deskType = new DeskType("single", "1セル机", [new GridPosition(0, 0)]);
        var participants = new[]
        {
            new Participant("a", "A", 1, new Dictionary<string, double>()) { GenreId = "game" },
            new Participant("middle", "M", 1, new Dictionary<string, double>()) { GenreId = "other" },
            new Participant("b", "B", 1, new Dictionary<string, double>()) { GenreId = "game" },
        };
        var plan = new Plan("ring", "環状候補", [
            new DeskPlacement("d0", deskType.Id, new GridPosition(0, 0), QuarterTurn.North),
            new DeskPlacement("d1", deskType.Id, new GridPosition(1, 0), QuarterTurn.North),
            new DeskPlacement("d2", deskType.Id, new GridPosition(2, 0), QuarterTurn.North),
        ], [
            new ParticipantAssignment("a", new HashSet<GridPosition> { new(0, 0) }, new GridPosition(0, 0)),
            new ParticipantAssignment("middle", new HashSet<GridPosition> { new(1, 0) }, new GridPosition(1, 0)),
            new ParticipantAssignment("b", new HashSet<GridPosition> { new(2, 0) }, new GridPosition(2, 0)),
        ]);
        return new CircleSpaceProject("1.0", "ring", "環状評価",
            new Venue("venue", "会場", 3, 1, new HashSet<GridPosition>()), [deskType], participants,
            new EvaluationConfiguration([], []), [plan]);
    }

    private static CircleSpaceProject CreateCombinedSpaceProject(bool splitAcrossDesks)
    {
        var deskType = StandardDesk();
        var participants = new[]
        {
            new Participant("circle-a", "合同A", 1, new Dictionary<string, double>()),
            new Participant("circle-b", "合同B", 1, new Dictionary<string, double>()),
        };
        var desks = splitAcrossDesks
            ? new[]
            {
                new DeskPlacement("desk-a", deskType.Id, new GridPosition(1, 1), QuarterTurn.North),
                new DeskPlacement("desk-b", deskType.Id, new GridPosition(3, 1), QuarterTurn.North),
            }
            : new[]
            {
                new DeskPlacement("desk-shared", deskType.Id, new GridPosition(1, 1), QuarterTurn.North),
            };
        var secondCell = splitAcrossDesks ? new GridPosition(3, 1) : new GridPosition(2, 1);
        var assignments = new[]
        {
            new ParticipantAssignment("circle-a", new HashSet<GridPosition> { new(1, 1) }, new GridPosition(1, 1))
                { CombinedSpaceId = "combined-01" },
            new ParticipantAssignment("circle-b", new HashSet<GridPosition> { secondCell }, secondCell)
                { CombinedSpaceId = "combined-01" },
        };

        return new CircleSpaceProject(
            "1.0",
            "combined-project",
            "合体スペース検証",
            new Venue("venue", "架空会場", 8, 4, new HashSet<GridPosition>()),
            [deskType],
            participants,
            new EvaluationConfiguration([], []),
            [new Plan("plan", "合同配置", desks, assignments)]);
    }

    private static CircleSpaceProject CreateProject(bool includeSecondPlan = false)
    {
        var deskType = StandardDesk();
        var participant = new Participant(
            "participant-1",
            "架空参加者",
            RequiredCellCount: 2,
            new Dictionary<string, double> { ["shop-items"] = 10d });
        var feature = new EvaluationFeature(
            "shop-items",
            "頒布物数",
            Scale: 2d,
            Offset: 2d,
            OverallWeight: 1.5d);
        var weights = new WeightMap(
            feature.Id,
            DefaultWeight: 0d,
            new Dictionary<GridPosition, double>
            {
                [new GridPosition(1, 1)] = 0.5d,
                [new GridPosition(2, 1)] = 100d,
                [new GridPosition(1, 2)] = 1d,
                [new GridPosition(2, 2)] = 100d,
            });
        var mainPlan = new Plan(
            "plan-main",
            "主セルの重みが0.5の案",
            [new DeskPlacement("desk-main", deskType.Id, new GridPosition(1, 1), QuarterTurn.North)],
            [new ParticipantAssignment(
                participant.Id,
                new HashSet<GridPosition> { new(1, 1), new(2, 1) },
                new GridPosition(1, 1))]);
        var plans = new List<Plan> { mainPlan };
        if (includeSecondPlan)
        {
            plans.Add(new Plan(
                "plan-other",
                "主セルの重みが1.0の案",
                [new DeskPlacement("desk-other", deskType.Id, new GridPosition(1, 2), QuarterTurn.North)],
                [new ParticipantAssignment(
                    participant.Id,
                    new HashSet<GridPosition> { new(1, 2), new(2, 2) },
                    new GridPosition(1, 2))]));
        }

        return new CircleSpaceProject(
            "1.0",
            "project",
            "架空プロジェクト",
            new Venue("venue", "架空会場", 6, 4, new HashSet<GridPosition>()),
            [deskType],
            [participant],
            new EvaluationConfiguration([feature], [weights]),
            plans);
    }

    private static DeskType StandardDesk() => new(
        "standard-desk",
        "2セル机",
        [new GridPosition(0, 0), new GridPosition(1, 0)]);

    private static void AssertContainsIssue(IReadOnlyList<ValidationIssue> issues, string code)
    {
        if (issues.All(issue => issue.Code != code))
            throw new InvalidOperationException($"Expected validation issue '{code}', but got: {string.Join(", ", issues.Select(issue => issue.Code))}");
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
