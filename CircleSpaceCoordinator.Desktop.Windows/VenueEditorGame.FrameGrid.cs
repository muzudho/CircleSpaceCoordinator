namespace CircleSpaceCoordinator.Desktop.Windows;

using Microsoft.Xna.Framework;
using StationeryUI.Canvas;

public sealed partial class VenueEditorGame
{
    private const double FrameGridBorderPadding = 8d;

    private ScreenRectangle FramePlacementCanvasBounds
    {
        get
        {
            const double margin = 12d;
            var left = NextSpaceBounds.X + NextSpaceBounds.Width + margin;
            var right = GraphicsDevice.Viewport.Width - 276d - margin;
            var top = ToolbarHeight + margin;
            var bottom = GraphicsDevice.Viewport.Height - StatusBarHeight - margin;
            return new ScreenRectangle(left, top, Math.Max(1d, right - left), Math.Max(1d, bottom - top));
        }
    }

    private void KeepFrameGridInCanvas()
    {
        if (editorMode != EditorMode.DeskPlacement || workspace is null) return;
        var area = FramePlacementCanvasBounds;
        var cellSize = viewport.BaseCellSize * viewport.Zoom;
        var venue = workspace.Project.Venue;
        var width = venue.Width * cellSize + FrameGridBorderPadding * 2d;
        var height = venue.Height * cellSize + FrameGridBorderPadding * 2d;
        static double Constrain(double origin, double size, double start, double available) =>
            size <= available
                ? Math.Clamp(origin, start, start + available - size)
                : Math.Clamp(origin, start + available - size, start);
        var x = Constrain(viewport.Origin.X - FrameGridBorderPadding, width, area.X, area.Width) + FrameGridBorderPadding;
        var y = Constrain(viewport.Origin.Y - FrameGridBorderPadding, height, area.Y, area.Height) + FrameGridBorderPadding;
        if (x != viewport.Origin.X || y != viewport.Origin.Y)
            viewport.SetView(viewport.Zoom, new ScreenPoint(x, y));
    }

    private void DrawFrameGridOutline(int width, int height)
    {
        if (editorMode != EditorMode.DeskPlacement) return;
        var first = viewport.GetCellBounds(new GridCellAddress(0, 0));
        var last = viewport.GetCellBounds(new GridCellAddress(width - 1, height - 1));
        DrawOutline(new ScreenRectangle(first.X - FrameGridBorderPadding, first.Y - FrameGridBorderPadding,
            last.X + last.Width - first.X + FrameGridBorderPadding * 2d,
            last.Y + last.Height - first.Y + FrameGridBorderPadding * 2d),
            2d, new Color(100, 151, 162));
    }
}
