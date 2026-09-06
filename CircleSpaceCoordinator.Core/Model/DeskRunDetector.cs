namespace CircleSpaceCoordinator.Core.Model;

using CircleSpaceCoordinator.Core.Geometry;

public sealed record DeskRun(QuarterTurn Orientation, IReadOnlyList<string> DeskIds);

public static class DeskRunDetector
{
    public static IReadOnlyList<DeskRun> Detect(Plan plan, IReadOnlyDictionary<string, DeskType> deskTypes)
    {
        var remaining = plan.DeskPlacements.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var runs = new List<DeskRun>();
        while (remaining.Count > 0)
        {
            var seed = remaining.Values.First();
            remaining.Remove(seed.Id);
            var members = new List<DeskPlacement> { seed };
            var queue = new Queue<DeskPlacement>();
            queue.Enqueue(seed);
            while (queue.TryDequeue(out var current))
            {
                foreach (var candidate in remaining.Values.Where(item => IsAdjacent(current, item, deskTypes)).ToArray())
                {
                    remaining.Remove(candidate.Id);
                    members.Add(candidate);
                    queue.Enqueue(candidate);
                }
            }
            var horizontal = seed.Orientation is QuarterTurn.North or QuarterTurn.South;
            runs.Add(new DeskRun(seed.Orientation, members
                .OrderBy(item => horizontal ? item.Anchor.X : item.Anchor.Y)
                .ThenBy(item => horizontal ? item.Anchor.Y : item.Anchor.X)
                .Select(item => item.Id).ToArray()));
        }
        return runs;
    }

    private static bool IsAdjacent(DeskPlacement first, DeskPlacement second, IReadOnlyDictionary<string, DeskType> deskTypes)
    {
        if (first.Orientation != second.Orientation ||
            !deskTypes.TryGetValue(first.DeskTypeId, out var firstType) ||
            !deskTypes.TryGetValue(second.DeskTypeId, out var secondType))
            return false;
        var a = Bounds(first.GetOccupiedCells(firstType));
        var b = Bounds(second.GetOccupiedCells(secondType));
        return first.Orientation is QuarterTurn.North or QuarterTurn.South
            ? a.Top == b.Top && a.Bottom == b.Bottom && (a.Right == b.Left || b.Right == a.Left)
            : a.Left == b.Left && a.Right == b.Right && (a.Bottom == b.Top || b.Bottom == a.Top);
    }

    private static (int Left, int Top, int Right, int Bottom) Bounds(IEnumerable<GridPosition> cells)
    {
        var value = cells.ToArray();
        return (value.Min(cell => cell.X), value.Min(cell => cell.Y),
            value.Max(cell => cell.X) + 1, value.Max(cell => cell.Y) + 1);
    }
}
