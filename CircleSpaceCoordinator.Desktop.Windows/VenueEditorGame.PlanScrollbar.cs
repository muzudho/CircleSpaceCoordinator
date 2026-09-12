namespace CircleSpaceCoordinator.Desktop.Windows;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StationeryUI.Canvas;

public sealed partial class VenueEditorGame
{
    private bool draggingPlanScrollbar;
    private double planScrollbarGrabOffset;

    private (ScreenRectangle Track, ScreenRectangle Thumb, int Maximum) GetPlanScrollbar()
    {
        var count = GetDisplayedPlans().Count;
        var visible = Math.Min(count, GetVisiblePlanRowCount());
        var maximum = Math.Max(0, count - visible);
        planScroll = Math.Clamp(planScroll, 0, maximum);
        var track = new ScreenRectangle(
            GraphicsDevice.PresentationParameters.BackBufferWidth - 30d,
            ToolbarHeight + 100d, 14d, Math.Max(0, visible * 42d - 4d));
        var thumbHeight = maximum == 0 ? track.Height
            : Math.Min(track.Height, Math.Max(24d, track.Height * visible / count));
        var offset = maximum == 0 ? 0 : (track.Height - thumbHeight) * planScroll / maximum;
        return (track, new ScreenRectangle(track.X, track.Y + offset, track.Width, thumbHeight), maximum);
    }

    private void DrawPlanScrollbar()
    {
        var bar = GetPlanScrollbar();
        if (bar.Track.Height <= 0) return;
        DrawRectangle(bar.Track, new Color(13, 17, 22));
        DrawRectangle(bar.Thumb, bar.Maximum == 0 ? new Color(48, 57, 68)
            : draggingPlanScrollbar ? new Color(129, 235, 202) : new Color(104, 157, 204));
    }

    private bool HandlePlanScrollbarInput(MouseState mouse, ScreenPoint pointer)
    {
        if (workspace is null || editorMode is EditorMode.GenreData or EditorMode.ParticipantData)
        {
            draggingPlanScrollbar = false;
            return false;
        }
        var bar = GetPlanScrollbar();
        if (draggingPlanScrollbar)
        {
            if (mouse.LeftButton == ButtonState.Released)
                draggingPlanScrollbar = false;
            else
            {
                var travel = bar.Track.Height - bar.Thumb.Height;
                var fraction = travel <= 0 ? 0
                    : Math.Clamp((pointer.Y - bar.Track.Y - planScrollbarGrabOffset) / travel, 0, 1);
                planScroll = (int)Math.Round(fraction * bar.Maximum);
            }
            // Consume the entire gesture, including release outside the list.
            return true;
        }
        if (mouse.LeftButton != ButtonState.Pressed || previousMouse.LeftButton != ButtonState.Released ||
            bar.Track.Height <= 0 || !Contains(bar.Track, pointer))
            return false;
        if (bar.Maximum > 0)
        {
            if (Contains(bar.Thumb, pointer))
            {
                draggingPlanScrollbar = true;
                planScrollbarGrabOffset = pointer.Y - bar.Thumb.Y;
            }
            else
            {
                var direction = pointer.Y < bar.Thumb.Y ? -1 : 1;
                planScroll = Math.Clamp(planScroll + direction * Math.Max(1, GetVisiblePlanRowCount()), 0, bar.Maximum);
            }
        }
        return true;
    }
}
