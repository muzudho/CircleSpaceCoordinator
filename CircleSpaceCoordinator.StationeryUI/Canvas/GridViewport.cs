namespace CircleSpaceCoordinator.StationeryUI.Canvas;

public readonly record struct GridCellAddress(int Column, int Row);

public readonly record struct ScreenPoint(double X, double Y);

public readonly record struct ScreenRectangle(double X, double Y, double Width, double Height);

public sealed class GridViewport
{
    public const double MinimumZoom = 0.25d;
    public const double MaximumZoom = 4d;

    public GridViewport(double baseCellSize)
    {
        if (!double.IsFinite(baseCellSize) || baseCellSize <= 0d)
            throw new ArgumentOutOfRangeException(nameof(baseCellSize), "Cell size must be a positive finite number.");
        BaseCellSize = baseCellSize;
    }

    public double BaseCellSize { get; }

    public double Zoom { get; private set; } = 1d;

    public ScreenPoint Origin { get; private set; }

    public double CellSize => BaseCellSize * Zoom;

    public GridCellAddress ScreenToCell(ScreenPoint point) => new(
        (int)Math.Floor((point.X - Origin.X) / CellSize),
        (int)Math.Floor((point.Y - Origin.Y) / CellSize));

    public ScreenRectangle GetCellBounds(GridCellAddress cell) => new(
        Origin.X + cell.Column * CellSize,
        Origin.Y + cell.Row * CellSize,
        CellSize,
        CellSize);

    public void PanBy(double deltaX, double deltaY)
    {
        if (!double.IsFinite(deltaX) || !double.IsFinite(deltaY))
            throw new ArgumentOutOfRangeException(nameof(deltaX), "Pan distance must be finite.");
        Origin = new ScreenPoint(Origin.X + deltaX, Origin.Y + deltaY);
    }

    public void SetView(double zoom, ScreenPoint origin)
    {
        if (!double.IsFinite(zoom) || zoom is < MinimumZoom or > MaximumZoom)
            throw new ArgumentOutOfRangeException(nameof(zoom), $"Zoom must be between {MinimumZoom} and {MaximumZoom}.");
        if (!double.IsFinite(origin.X) || !double.IsFinite(origin.Y))
            throw new ArgumentOutOfRangeException(nameof(origin), "Origin coordinates must be finite.");

        Zoom = zoom;
        Origin = origin;
    }

    public void ZoomAt(ScreenPoint anchor, double zoom)
    {
        if (!double.IsFinite(zoom) || zoom is < MinimumZoom or > MaximumZoom)
            throw new ArgumentOutOfRangeException(nameof(zoom), $"Zoom must be between {MinimumZoom} and {MaximumZoom}.");

        var worldX = (anchor.X - Origin.X) / CellSize;
        var worldY = (anchor.Y - Origin.Y) / CellSize;
        Zoom = zoom;
        Origin = new ScreenPoint(
            anchor.X - worldX * CellSize,
            anchor.Y - worldY * CellSize);
    }
}
