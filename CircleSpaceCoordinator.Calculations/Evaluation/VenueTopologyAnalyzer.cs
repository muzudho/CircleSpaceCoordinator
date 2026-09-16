namespace CircleSpaceCoordinator.Core.Evaluation;

using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;

public static class VenueTopologyAnalyzer
{
    public static VenueTopologyGraph Build(CircleSpaceProject project, Plan plan)
    {
        var deskTypes = project.DeskTypes.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var deskCells = plan.DeskPlacements.ToDictionary(
            item => item.Id,
            item => item.GetSeatCells(deskTypes[item.DeskTypeId]),
            StringComparer.Ordinal);
        var deskIdByCell = plan.DeskPlacements.SelectMany(desk => desk.GetOccupiedCells(deskTypes[desk.DeskTypeId]).Select(cell => (Cell: cell, DeskId: desk.Id)))
            .ToDictionary(item => item.Cell, item => item.DeskId);
        var orientationByDeskId = plan.DeskPlacements.ToDictionary(item => item.Id, item => item.Orientation, StringComparer.Ordinal);
        var neighbors = deskCells.Values.SelectMany(item => item)
            .Distinct().ToDictionary(cell => cell, _ => new HashSet<GridPosition>());

        var disabledConnections = plan.DisabledIslandConnections
            .Select(item => Normalize(item.FirstCell, item.SecondCell))
            .ToHashSet();
        foreach (var (first, second) in GetAutomaticCellEdges(plan, deskTypes, deskCells))
            if (!disabledConnections.Contains(Normalize(first, second)))
                AddEdge(first, second);

        foreach (var connector in plan.IslandConnectors)
            if (ResolveConnectorCells(plan, deskTypes, connector) is { } endpoints)
                AddEdge(endpoints.FirstCell, endpoints.SecondCell);
        var facingPairs = new HashSet<(GridPosition, GridPosition)>();
        foreach (var region in plan.FacingRegions)
        {
            var left = Math.Min(region.FirstCorner.X, region.SecondCorner.X);
            var right = Math.Max(region.FirstCorner.X, region.SecondCorner.X);
            var top = Math.Min(region.FirstCorner.Y, region.SecondCorner.Y);
            var bottom = Math.Max(region.FirstCorner.Y, region.SecondCorner.Y);
            for (var y = top; y <= bottom; y++)
                AddFacingPair(new GridPosition(left, y), QuarterTurn.East,
                    new GridPosition(right, y), QuarterTurn.West);
            for (var x = left; x <= right; x++)
                AddFacingPair(new GridPosition(x, top), QuarterTurn.South,
                    new GridPosition(x, bottom), QuarterTurn.North);
        }

        var islandByCell = new Dictionary<GridPosition, int>();
        var islandNumber = 0;
        foreach (var cell in neighbors.Keys.Where(cell => !islandByCell.ContainsKey(cell)))
        {
            var queue = new Queue<GridPosition>();
            islandByCell[cell] = islandNumber;
            queue.Enqueue(cell);
            while (queue.TryDequeue(out var current))
                foreach (var next in neighbors[current])
                    if (islandByCell.TryAdd(next, islandNumber)) queue.Enqueue(next);
            islandNumber++;
        }
        var crossIslandFacingPairs = facingPairs
            .Where(pair => !islandByCell.TryGetValue(pair.Item1, out var firstIsland) ||
                !islandByCell.TryGetValue(pair.Item2, out var secondIsland) || firstIsland != secondIsland)
            .Select(pair => (pair.Item1, pair.Item2)).ToArray();

        return new VenueTopologyGraph(
            neighbors.ToDictionary(pair => pair.Key, pair => (IReadOnlySet<GridPosition>)pair.Value),
            crossIslandFacingPairs,
            deskIdByCell);

        void AddFacingPair(
            GridPosition first,
            QuarterTurn requiredFirstOrientation,
            GridPosition second,
            QuarterTurn requiredSecondOrientation)
        {
            if (!deskIdByCell.TryGetValue(first, out var firstDeskId) ||
                !deskIdByCell.TryGetValue(second, out var secondDeskId) ||
                firstDeskId == secondDeskId ||
                orientationByDeskId[firstDeskId] != requiredFirstOrientation ||
                orientationByDeskId[secondDeskId] != requiredSecondOrientation ||
                HasBlockedCellBetween(first, second))
                return;
            facingPairs.Add(Compare(first, second) <= 0 ? (first, second) : (second, first));
        }

        bool HasBlockedCellBetween(GridPosition first, GridPosition second)
        {
            if (first.X == second.X)
            {
                var top = Math.Min(first.Y, second.Y);
                var bottom = Math.Max(first.Y, second.Y);
                return Enumerable.Range(top + 1, bottom - top - 1)
                    .Any(y => project.Venue.BlockedCells.Contains(new GridPosition(first.X, y)));
            }
            var left = Math.Min(first.X, second.X);
            var right = Math.Max(first.X, second.X);
            return Enumerable.Range(left + 1, right - left - 1)
                .Any(x => project.Venue.BlockedCells.Contains(new GridPosition(x, first.Y)));
        }

        void AddEdge(GridPosition first, GridPosition second)
        {
            neighbors[first].Add(second);
            neighbors[second].Add(first);
        }
    }

    private static int Manhattan(GridPosition first, GridPosition second) =>
        Math.Abs(first.X - second.X) + Math.Abs(first.Y - second.Y);

    public static (GridPosition FirstCell, GridPosition SecondCell)? ResolveConnectorCells(
        Plan plan, IReadOnlyDictionary<string, DeskType> deskTypes, IslandConnector connector) =>
        IslandConnectorEndpoints.Resolve(plan, deskTypes, connector, GetAutomaticCellEdges(plan, deskTypes));

    /// <summary>Returns the physical links automatically inferred between adjacent desks.</summary>
    public static IReadOnlyList<(GridPosition FirstCell, GridPosition SecondCell)> GetAutomaticIslandEdges(
        Plan plan,
        IReadOnlyDictionary<string, DeskType> deskTypes)
    {
        var deskCells = plan.DeskPlacements.ToDictionary(
            item => item.Id,
            item => item.GetSeatCells(deskTypes[item.DeskTypeId]),
            StringComparer.Ordinal);
        return GetAutomaticIslandEdges(plan, deskTypes, deskCells);
    }

    /// <summary>Returns every visible physical cell link, including links within a desk.</summary>
    public static IReadOnlyList<(GridPosition FirstCell, GridPosition SecondCell)> GetAutomaticCellEdges(
        Plan plan,
        IReadOnlyDictionary<string, DeskType> deskTypes)
    {
        var deskCells = plan.DeskPlacements.ToDictionary(
            item => item.Id,
            item => item.GetSeatCells(deskTypes[item.DeskTypeId]),
            StringComparer.Ordinal);
        return GetAutomaticCellEdges(plan, deskTypes, deskCells);
    }

    private static IReadOnlyList<(GridPosition FirstCell, GridPosition SecondCell)> GetAutomaticCellEdges(
        Plan plan,
        IReadOnlyDictionary<string, DeskType> deskTypes,
        IReadOnlyDictionary<string, IReadOnlySet<GridPosition>> deskCells)
    {
        var edges = new HashSet<(GridPosition FirstCell, GridPosition SecondCell)>();
        foreach (var cells in deskCells.Values)
        foreach (var first in cells)
        foreach (var second in cells.Where(second => Manhattan(first, second) == 1))
            edges.Add(Normalize(first, second));
        edges.UnionWith(GetAutomaticIslandEdges(plan, deskTypes, deskCells));
        return edges.OrderBy(item => item.FirstCell.Y).ThenBy(item => item.FirstCell.X)
            .ThenBy(item => item.SecondCell.Y).ThenBy(item => item.SecondCell.X).ToArray();
    }

    private static IReadOnlyList<(GridPosition FirstCell, GridPosition SecondCell)> GetAutomaticIslandEdges(
        Plan plan,
        IReadOnlyDictionary<string, DeskType> deskTypes,
        IReadOnlyDictionary<string, IReadOnlySet<GridPosition>> deskCells)
    {
        var edges = new HashSet<(GridPosition FirstCell, GridPosition SecondCell)>();
        foreach (var run in DeskRunDetector.Detect(plan, deskTypes))
        for (var index = 1; index < run.DeskIds.Count; index++)
            if (deskCells[run.DeskIds[index - 1]].Count > 0 && deskCells[run.DeskIds[index]].Count > 0)
                edges.Add(ClosestPair(deskCells[run.DeskIds[index - 1]], deskCells[run.DeskIds[index]]));

        return edges.OrderBy(item => item.FirstCell.Y).ThenBy(item => item.FirstCell.X)
            .ThenBy(item => item.SecondCell.Y).ThenBy(item => item.SecondCell.X).ToArray();
    }

    private static (GridPosition FirstCell, GridPosition SecondCell) ClosestPair(
        IEnumerable<GridPosition> first, IEnumerable<GridPosition> second)
    {
        var pair = first.SelectMany(a => second.Select(b => (A: a, B: b, Distance: Manhattan(a, b))))
            .OrderBy(item => item.Distance).ThenBy(item => item.A.Y).ThenBy(item => item.A.X)
            .ThenBy(item => item.B.Y).ThenBy(item => item.B.X).First();
        return Normalize(pair.A, pair.B);
    }

    private static (GridPosition FirstCell, GridPosition SecondCell) Normalize(GridPosition first, GridPosition second) =>
        Compare(first, second) <= 0 ? (first, second) : (second, first);

    private static int Compare(GridPosition first, GridPosition second) =>
        first.Y != second.Y ? first.Y.CompareTo(second.Y) : first.X.CompareTo(second.X);
}
