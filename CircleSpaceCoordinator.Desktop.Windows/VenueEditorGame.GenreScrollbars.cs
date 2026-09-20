namespace CircleSpaceCoordinator.Desktop.Windows;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StationeryUI.Canvas;

public sealed partial class VenueEditorGame
{
    private int genreScrollbarDrag = -1;
    private double genreScrollbarGrab;

    private int GenreScrollCount(bool right) => right ? packageGenreTable?.GenreStyles?.Length ?? 0 : mappingDraft?.Rows.Count ?? 0;
    private ScreenRectangle GenreScrollArea(bool right)
    {
        var grid = MappingGridBounds(20, 102, 960, 390, right);
        return new(grid.X, grid.Y, grid.Width + 18 * MappingEditorScale, grid.Height);
    }
    private ScreenRectangle GenreScrollTrack(bool right)
    {
        var grid = MappingGridBounds(20, 142, 960, 306, right);
        return new(grid.X + grid.Width + 3 * MappingEditorScale, grid.Y, 12 * MappingEditorScale, grid.Height);
    }
    private ScreenRectangle GenreScrollThumb(bool right)
    {
        var track = GenreScrollTrack(right);
        var count = GenreScrollCount(right);
        var maximum = Math.Max(0, count - MappingVisibleRows);
        var height = Math.Min(track.Height, Math.Max(20 * MappingEditorScale, track.Height * MappingVisibleRows / Math.Max(MappingVisibleRows, count)));
        var offset = Math.Clamp(right ? packageGenreScroll : mappingScroll, 0, maximum);
        return new(track.X, track.Y + (maximum == 0 ? 0 : (track.Height - height) * offset / maximum), track.Width, height);
    }
    private void SetGenreScroll(bool right, int offset)
    {
        var next = Math.Clamp(offset, 0, Math.Max(0, GenreScrollCount(right) - MappingVisibleRows));
        if (right) packageGenreScroll = next;
        else
        {
            mappingScroll = next;
            mappingRow = Math.Clamp(mappingRow, next, Math.Max(next, Math.Min(GenreScrollCount(false) - 1, next + MappingVisibleRows - 1)));
        }
        mappingWidth = -1;
    }
    private bool UpdateGenreScrollbars(MouseState mouse)
    {
        if (!GenreGridVisible || mappingPickerColumn != 0) { genreScrollbarDrag = -1; return false; }
        var pointer = new ScreenPoint(mouse.X, mouse.Y);
        if (mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released)
            for (var side = 0; side < (GenreGridSplit ? 2 : 1); side++)
            {
                var right = side == 1;
                if (!Contains(GenreScrollTrack(right), pointer)) continue;
                if (GenreScrollCount(right) <= MappingVisibleRows) return true;
                var thumb = GenreScrollThumb(right);
                genreScrollbarDrag = side;
                genreScrollbarGrab = Contains(thumb, pointer) ? pointer.Y - thumb.Y : thumb.Height / 2;
                SetMappingTextFocus(false);
                pressedMappingButton = null;
                break;
            }
        if (genreScrollbarDrag < 0) return false;
        if (mouse.LeftButton == ButtonState.Released) { genreScrollbarDrag = -1; return true; }
        var dragRight = genreScrollbarDrag == 1;
        var track = GenreScrollTrack(dragRight);
        var height = GenreScrollThumb(dragRight).Height;
        var fraction = Math.Clamp((pointer.Y - track.Y - genreScrollbarGrab) / Math.Max(1, track.Height - height), 0, 1);
        SetGenreScroll(dragRight, (int)Math.Round(fraction * Math.Max(0, GenreScrollCount(dragRight) - MappingVisibleRows)));
        return true;
    }
    private void DrawGenreScrollbars()
    {
        for (var side = 0; side < (GenreGridSplit ? 2 : 1); side++)
        {
            var right = side == 1;
            DrawRectangle(GenreScrollTrack(right), new Color(35, 43, 54));
            DrawRectangle(GenreScrollThumb(right), GenreScrollCount(right) <= MappingVisibleRows
                ? new Color(60, 72, 80) : genreScrollbarDrag == side ? OperationTargetColor : new Color(120, 153, 165));
        }
    }

    private static int EnsureGenreRowVisible(int scroll, int row, int count)
        => Math.Clamp(row < scroll ? row : row >= scroll + MappingVisibleRows ? row - MappingVisibleRows + 1 : scroll,
            0, Math.Max(0, count - MappingVisibleRows));
}
