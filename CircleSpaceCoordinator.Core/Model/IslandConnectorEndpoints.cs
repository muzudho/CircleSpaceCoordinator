namespace CircleSpaceCoordinator.Core.Model;

using CircleSpaceCoordinator.Core.Geometry;

public static class IslandConnectorEndpoints
{
    /// <summary>Resolves legacy frame-only connectors to free seat ends; non-seat explicit endpoints are inactive.</summary>
    public static (GridPosition FirstCell, GridPosition SecondCell)? Resolve(
        Plan plan, IReadOnlyDictionary<string, DeskType> deskTypes, IslandConnector connector,
        IReadOnlyList<(GridPosition FirstCell, GridPosition SecondCell)> automaticEdges)
    {
        var first = plan.DeskPlacements.FirstOrDefault(desk => desk.Id == connector.FirstDeskId);
        var second = plan.DeskPlacements.FirstOrDefault(desk => desk.Id == connector.SecondDeskId);
        if (first is null || second is null) return null;
        var firstCells = first.GetSeatCells(deskTypes[first.DeskTypeId]);
        var secondCells = second.GetSeatCells(deskTypes[second.DeskTypeId]);
        if (firstCells.Count == 0 || secondCells.Count == 0 ||
            connector.FirstCell is { } a && !firstCells.Contains(a) ||
            connector.SecondCell is { } b && !secondCells.Contains(b)) return null;
        if (connector.FirstCell is { } explicitFirst && connector.SecondCell is { } explicitSecond)
            return (explicitFirst, explicitSecond);
        var disabled = plan.DisabledIslandConnections.Select(edge => Normalize(edge.FirstCell, edge.SecondCell)).ToHashSet();
        var degree = automaticEdges.Where(edge => !disabled.Contains(edge))
            .SelectMany(edge => new[] { edge.FirstCell, edge.SecondCell }).GroupBy(cell => cell)
            .ToDictionary(group => group.Key, group => group.Count());
        GridPosition FreeEnd(IEnumerable<GridPosition> cells) => cells.OrderBy(cell => degree.GetValueOrDefault(cell))
            .ThenBy(cell => cell.Y).ThenBy(cell => cell.X).First();
        return (connector.FirstCell ?? FreeEnd(firstCells), connector.SecondCell ?? FreeEnd(secondCells));
    }

    private static (GridPosition FirstCell, GridPosition SecondCell) Normalize(GridPosition first, GridPosition second) =>
        first.Y < second.Y || first.Y == second.Y && first.X <= second.X ? (first, second) : (second, first);
}
