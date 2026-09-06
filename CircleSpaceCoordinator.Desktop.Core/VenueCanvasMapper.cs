namespace CircleSpaceCoordinator.Desktop.Core;

using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.StationeryUI.Canvas;

public static class VenueCanvasMapper
{
    public static GridCellAddress ToCanvasCell(GridPosition position) => new(position.X, position.Y);

    public static GridPosition ToGridPosition(GridCellAddress cell) => new(cell.Column, cell.Row);
}
