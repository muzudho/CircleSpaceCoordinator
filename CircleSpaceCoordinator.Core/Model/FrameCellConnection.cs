namespace CircleSpaceCoordinator.Core.Model;

using CircleSpaceCoordinator.Core.Geometry;

/// <summary>An undirected connection between two seat cells in unrotated frame coordinates.</summary>
public sealed record FrameCellConnection(GridPosition FirstCell, GridPosition SecondCell)
{
    public FrameCellConnection Normalize() => FirstCell.Y < SecondCell.Y ||
        FirstCell.Y == SecondCell.Y && FirstCell.X <= SecondCell.X ? this : new(SecondCell, FirstCell);

    public static bool AreValid(IReadOnlyList<FrameCellConnection>? connections, IReadOnlySet<GridPosition> seats)
    {
        if (connections is null) return true; // Legacy: connect adjacent seats.
        var seen = new HashSet<FrameCellConnection>();
        return connections.All(link => link is not null && link.FirstCell != link.SecondCell &&
            seats.Contains(link.FirstCell) && seats.Contains(link.SecondCell) && seen.Add(link.Normalize()));
    }

    public static FrameCellConnection[] Adjacent(IReadOnlySet<GridPosition> seats) => seats
        .SelectMany(cell => new[] { new GridPosition(cell.X + 1, cell.Y), new GridPosition(cell.X, cell.Y + 1) }
            .Where(seats.Contains).Select(next => new FrameCellConnection(cell, next)))
        .OrderBy(link => link.FirstCell.Y).ThenBy(link => link.FirstCell.X).ToArray();
}
