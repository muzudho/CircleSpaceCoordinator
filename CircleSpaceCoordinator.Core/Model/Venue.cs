namespace CircleSpaceCoordinator.Core.Model;

using CircleSpaceCoordinator.Core.Geometry;

public sealed record Venue(
    string Id,
    string Name,
    int Width,
    int Height,
    IReadOnlySet<GridPosition> BlockedCells)
{
    public IReadOnlyList<VenueZone> Zones { get; init; } = [];

    public bool Contains(GridPosition position) =>
        0 <= position.X && position.X < Width &&
        0 <= position.Y && position.Y < Height;

    public bool CanPlaceAt(GridPosition position) =>
        Contains(position) && !BlockedCells.Contains(position);
}

public sealed record VenueZone(
    string Id,
    string Name,
    IReadOnlySet<GridPosition> Cells);
