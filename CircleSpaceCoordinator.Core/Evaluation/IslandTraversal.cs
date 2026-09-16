namespace CircleSpaceCoordinator.Core.Evaluation;

using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;

public sealed record IslandDistance(int Distance, GridPosition StartCell, GridPosition? ParentCell);

/// <summary>Multi-source breadth-first traversal. Facing rectangles are not graph edges.</summary>
public static class IslandTraversal
{
    public static IReadOnlyDictionary<GridPosition, IslandDistance> Build(Plan plan, VenueTopologyGraph graph)
    {
        var result = new Dictionary<GridPosition, IslandDistance>();
        var directions = new Dictionary<GridPosition, QuarterTurn>();
        var queue = new Queue<GridPosition>();
        var desks = plan.DeskPlacements.ToDictionary(item => item.Id);
        // Stable root order also resolves equal-distance competition between roots.
        var roots = plan.IslandStarts.Where(start => desks.ContainsKey(start.DeskPlacementId))
            .Select(start => (Cell: start.GetCell(desks[start.DeskPlacementId]), Direction: start.GetDirection(desks[start.DeskPlacementId])))
            .OrderBy(start => start.Cell.Y).ThenBy(start => start.Cell.X);
        foreach (var root in roots)
        {
            if (!graph.Neighbors.ContainsKey(root.Cell) || !result.TryAdd(root.Cell, new(0, root.Cell, null))) continue;
            directions[root.Cell] = root.Direction;
            queue.Enqueue(root.Cell);
        }
        while (queue.TryDequeue(out var current))
        {
            var visit = result[current];
            foreach (var next in graph.Neighbors[current]
                .Where(next => !directions.TryGetValue(current, out var entrance) || DirectionTo(current, next) == entrance)
                .OrderBy(next => next.Y).ThenBy(next => next.X))
                if (result.TryAdd(next, new(visit.Distance + 1, visit.StartCell, current))) queue.Enqueue(next);
        }
        return result;
    }

    // A straight connector belongs to its dominant axis; exact diagonals use the horizontal side.
    private static QuarterTurn DirectionTo(GridPosition from, GridPosition to)
    {
        var dx = (long)to.X - from.X;
        var dy = (long)to.Y - from.Y;
        return Math.Abs(dx) >= Math.Abs(dy)
            ? dx >= 0 ? QuarterTurn.East : QuarterTurn.West
            : dy >= 0 ? QuarterTurn.South : QuarterTurn.North;
    }
}
