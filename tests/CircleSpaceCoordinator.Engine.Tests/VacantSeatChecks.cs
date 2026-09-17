using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Core.Evaluation;
using CircleSpaceCoordinator.Core.Validation;
using CircleSpaceCoordinator.EditorClient;
using CircleSpaceCoordinator.Engine.Contracts.V1;
using CircleSpaceCoordinator.ThinkingEngine;
using Grpc.Core;

internal static class VacantSeatChecks
{
    public static async Task Run(string address)
    {
        var source = CreateProject();
        Check(ProjectValidator.Validate(source).Count == 0, string.Join("; ", ProjectValidator.Validate(source)));
        var filled = VacantSeatFiller.Fill(source, "p");
        var plan = filled.Plans.Single();
        Check(ProjectValidator.Validate(filled).Count == 0, "valid result");
        Check(plan.Assignments.Count == 7, "all fitting circles placed, oversized circle remains");
        Check(plan.Assignments.Single(a => a.ParticipantId == "fixed") == source.Plans[0].Assignments[0], "existing assignment preserved");
        var pairCells = plan.Assignments.Where(a => a.ParticipantId is "a" or "b").SelectMany(a => a.OccupiedCells).ToHashSet();
        Check(plan.DeskPlacements.Any(d => d.GetSeatCells(source.DeskTypes[0]).IsSupersetOf(pairCells)), "imported partners share frame");
        Check(plan.TemporaryPlacements.Count == 0, "parked pair moved together");
        Check(plan.Assignments.Count(a => a.CombinedSpaceId == "parked") == 2, "manual combined ID retained");
        Check(plan.Assignments.All(a => !a.OccupiedCells.Contains(new(2, 0))), "non-seat excluded");
        Check(ReferenceEquals(VacantSeatFiller.Fill(filled, "p"), filled), "repeat does not change full plan");
        Check(source.Plans[0].Assignments.Count == 1 && source.Plans[0].TemporaryPlacements.Count == 2, "source immutable");

        var partial = source with
        {
            Participants = [Person("a") with { CombinedWithCircleId = "b" }, Person("b")],
            Plans = [new Plan("p", "Partial pair", [source.Plans[0].DeskPlacements[0]],
                [new ParticipantAssignment("a", new HashSet<GridPosition> { new(0, 0) }, new(0, 0))])],
        };
        var partialResult = VacantSeatFiller.Fill(partial, "p").Plans[0];
        Check(partialResult.Assignments.Single(a => a.ParticipantId == "b").OccupiedCells.SetEquals([new(1, 0)]), "partner joins fixed member");
        var impossible = partial with { Participants = [partial.Participants[0], Person("b", 2)] };
        Check(ReferenceEquals(VacantSeatFiller.Fill(impossible, "p"), impossible), "insufficient room preserves incomplete group");
        var noSeats = source with { Plans = [new Plan("p", "Empty", [], [])] };
        Check(ReferenceEquals(VacantSeatFiller.Fill(noSeats, "p"), noSeats), "empty venue");
        var rotated = source with
        {
            Participants = [Person("rotated", 2)],
            Plans = [new Plan("p", "Rotated", [new DeskPlacement("r", "frame", new(5, 5), QuarterTurn.East)], [])],
        };
        Check(VacantSeatFiller.Fill(rotated, "p").Plans[0].Assignments.Single().OccupiedCells.SetEquals(
            rotated.Plans[0].DeskPlacements[0].GetSeatCells(rotated.DeskTypes[0])), "rotated assignable cells");
        var shared = LayoutProjection.MigrateLegacyPlans(source);
        shared = shared with { CircleLayouts = [.. shared.CircleLayouts,
            shared.CircleLayouts[0] with { Id = "sibling", Name = "Sibling" }] };
        shared = shared with { Plans = LayoutProjection.ToPlans(shared) };
        var sharedResult = VacantSeatFiller.Fill(shared, "p");
        Check(sharedResult.Plans.Single(p => p.Id == "sibling").Assignments.Count == 1 &&
            sharedResult.Plans.Single(p => p.Id == "sibling").TemporaryPlacements.Count == 2, "sibling layout unchanged");
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        try { VacantSeatFiller.Fill(source, "p", cancelled.Token); throw new Exception("Expected cancellation"); }
        catch (OperationCanceledException) { }

        using var connection = new EditorConnection(address);
        using var workspace = connection.Open(connection.Encode(source));
        var original = connection.Encode(workspace.Project);
        var revision = workspace.Revision;
        workspace.Accept(await workspace.FillVacantSeatsAsync());
        Check(workspace.SelectedPlan.Assignments.Count == 7 && workspace.CanUndo, "two-hop fill");
        var result = connection.Encode(workspace.Project);
        workspace.Undo();
        Check(connection.Encode(workspace.Project) == original, "one undo restores all stones and temporary positions");
        workspace.Redo();
        Check(connection.Encode(workspace.Project) == result, "redo restores filled layout");
        var fullRevision = workspace.Revision;
        workspace.Accept(await workspace.FillVacantSeatsAsync());
        Check(workspace.Revision == fullRevision, "no-op does not add history");
        try
        {
            await connection.Client.FillVacantSeatsAsync(new FillVacantSeatsRequest { WorkspaceId = workspace.Id, ExpectedRevision = revision });
            throw new Exception("Expected stale revision rejection");
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Aborted) { }
        Console.WriteLine("PASS: vacant seats, combined groups, existing/temporary stones, seat restrictions, cancellation, gRPC and undo/redo.");
    }

    private static Participant Person(string id, int size = 1) => new(id, id, size, new Dictionary<string, double>());

    private static CircleSpaceProject CreateProject()
    {
        var type = new DeskType("frame", "Frame", [new(0, 0), new(1, 0), new(2, 0)])
        { Space = new SpaceTypeDetails("frame", "机", 3, 1,
            [new(0, 0, 1), new(1, 0, 1), new(2, 0, 0)], ["開放", "開放", "開放", "開放"]) };
        return new CircleSpaceProject("1.0", "fill", "Fill", new Venue("v", "Venue", 20, 10, new HashSet<GridPosition>()),
            [type], [Person("fixed"), Person("a") with { CombinedWithCircleId = "b" }, Person("b"),
                Person("large", 2), Person("park1"), Person("park2"), Person("single"), Person("too-large", 3)],
            new EvaluationConfiguration([], []),
            [new Plan("p", "Plan", Enumerable.Range(0, 4).Select(i => new DeskPlacement($"d{i}", "frame", new(i * 4, 0), QuarterTurn.North)).ToArray(),
                [new ParticipantAssignment("fixed", new HashSet<GridPosition> { new(0, 0) }, new(0, 0))])
            { TemporaryPlacements = [
                new ParticipantAssignment("park1", new HashSet<GridPosition> { new(0, 5) }, new(0, 5)) { CombinedSpaceId = "parked" },
                new ParticipantAssignment("park2", new HashSet<GridPosition> { new(1, 5) }, new(1, 5)) { CombinedSpaceId = "parked" }] }]);
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception($"FAIL vacant seats: {name}");
    }
}
