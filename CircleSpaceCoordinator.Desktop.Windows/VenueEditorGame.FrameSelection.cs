namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Desktop.Core;
using Microsoft.Xna.Framework;
using StationeryUI.Canvas;

public sealed partial class VenueEditorGame
{
    private ScreenPoint? frameSelectionStart;
    private ScreenPoint frameSelectionPointer;
    private readonly HashSet<string> selectedFrameIds = [];

    private bool IsFrameNumberChannelSelected => editorMode == EditorMode.DeskPlacement &&
        !IsWeightChannelSelected && selectedNumberChannel == 1;

    private ScreenRectangle FrameSelectionBounds(ScreenPoint start, ScreenPoint end) => new(
        Math.Min(start.X, end.X), Math.Min(start.Y, end.Y),
        Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y));

    private ScreenRectangle FrameHighlightBounds(IEnumerable<GridPosition> cells)
    {
        var occupied = cells.ToArray();
        var bounds = GetCellRegionBounds(
            new GridPosition(occupied.Min(cell => cell.X), occupied.Min(cell => cell.Y)),
            new GridPosition(occupied.Max(cell => cell.X), occupied.Max(cell => cell.Y)));
        return new ScreenRectangle(bounds.X + 2, bounds.Y + 2,
            Math.Max(1, bounds.Width - 4), Math.Max(1, bounds.Height - 4));
    }

    private static bool TouchesFrame(ScreenRectangle selection, ScreenRectangle frame) =>
        selection.X <= frame.X + frame.Width && selection.X + selection.Width >= frame.X &&
        selection.Y <= frame.Y + frame.Height && selection.Y + selection.Height >= frame.Y;

    private void FinishFrameSelection(ScreenPoint pointer)
    {
        selectedFrameIds.Clear();
        if (frameSelectionStart is { } start && workspace is not null)
        {
            var selection = FrameSelectionBounds(start, pointer);
            foreach (var desk in workspace.GetSelectedPlanSnapshot().Desks)
                if (desk.OccupiedCells.Any() && TouchesFrame(selection, FrameHighlightBounds(desk.OccupiedCells)))
                    selectedFrameIds.Add(desk.Id);
        }
        frameSelectionStart = null;
    }

    private void DrawFrameSelection()
    {
        if (workspace is null) return;
        var selection = frameSelectionStart is { } start
            ? FrameSelectionBounds(start, frameSelectionPointer) : (ScreenRectangle?)null;
        foreach (var desk in workspace.GetSelectedPlanSnapshot().Desks)
        {
            if (!desk.OccupiedCells.Any()) continue;
            var bounds = FrameHighlightBounds(desk.OccupiedCells);
            if (selection is { } rectangle ? TouchesFrame(rectangle, bounds) : selectedFrameIds.Contains(desk.Id))
            {
                DrawRectangle(bounds, SelectionHighlightColor);
                DrawOutline(bounds, 2, OperationTargetColor);
            }
        }
        if (selection is not { } area) return;
        var topLeft = new ScreenPoint(area.X, area.Y);
        var topRight = new ScreenPoint(area.X + area.Width, area.Y);
        var bottomLeft = new ScreenPoint(area.X, area.Y + area.Height);
        var bottomRight = new ScreenPoint(area.X + area.Width, area.Y + area.Height);
        DrawSelectionDashes(topLeft, topRight);
        DrawSelectionDashes(topRight, bottomRight);
        DrawSelectionDashes(bottomRight, bottomLeft);
        DrawSelectionDashes(bottomLeft, topLeft);
        DrawCircle(frameSelectionStart!.Value, 5, OperationTargetColor);
        DrawCircle(frameSelectionPointer, 5, OperationTargetColor);
    }

    private void DrawSelectionDashes(ScreenPoint from, ScreenPoint to)
    {
        var length = Math.Sqrt(DistanceSquared(from, to));
        for (var offset = 0d; offset < length; offset += 10)
        {
            var end = Math.Min(offset + 5, length);
            DrawLine(new ScreenPoint(from.X + (to.X - from.X) * offset / length, from.Y + (to.Y - from.Y) * offset / length),
                new ScreenPoint(from.X + (to.X - from.X) * end / length, from.Y + (to.Y - from.Y) * end / length),
                2, OperationTargetColor);
        }
    }
}
