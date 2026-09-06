namespace CircleSpaceCoordinator.Core.Model;

using CircleSpaceCoordinator.Core.Geometry;

public sealed record DeskType(
    string Id,
    string Name,
    IReadOnlyList<GridPosition> Footprint);

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
}
