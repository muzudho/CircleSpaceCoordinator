namespace CircleSpaceCoordinator.Core.Model;

using CircleSpaceCoordinator.Core.Geometry;

public sealed record DeskType(
    string Id,
    string Name,
    IReadOnlyList<GridPosition> Footprint)
{
    public SpaceTypeDetails? Space { get; init; }
}

public sealed record SpaceAreaCell(int X, int Y, int Area);
public sealed record SpaceTypeDetails(string DefinitionId, string Kind, int Width, int Height,
    IReadOnlyList<SpaceAreaCell> Cells, IReadOnlyList<string> Edges);

public sealed record DeskPlacement(
    string Id,
    string DeskTypeId,
    GridPosition Anchor,
    QuarterTurn Orientation)
{
    /// <summary>Desk number used instead of a seat number for a two-cell circle placed at this desk.</summary>
    public string? DeskNumber { get; init; }

    public IReadOnlySet<GridPosition> GetOccupiedCells(DeskType deskType) =>
        deskType.Footprint
            .Select(relative => Anchor + relative.Rotate(Orientation))
            .ToHashSet();

    /// <summary>Number-editing targets exclude occupied cells that are not seats.</summary>
    public IReadOnlySet<GridPosition> GetSeatCells(DeskType deskType) =>
        (deskType.Space is { } space
            ? space.Cells.Where(cell => cell.Area > 0).Select(cell => new GridPosition(cell.X, cell.Y))
            : deskType.Footprint)
        .Select(relative => Anchor + relative.Rotate(Orientation))
        .ToHashSet();
}
