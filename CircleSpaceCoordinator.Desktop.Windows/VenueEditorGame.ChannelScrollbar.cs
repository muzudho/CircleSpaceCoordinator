namespace CircleSpaceCoordinator.Desktop.Windows;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StationeryUI.Canvas;

public sealed partial class VenueEditorGame
{
    private bool capturingChannelScrollbar;
    private bool draggingChannelScrollbar;
    private double channelScrollbarGrabOffset;

    private int ChannelScrollPosition
    {
        get => editorMode == EditorMode.CirclePlacement ? circleChannelScroll : channelScroll;
        set { if (editorMode == EditorMode.CirclePlacement) circleChannelScroll = value; else channelScroll = value; }
    }

    private (ScreenRectangle Track, ScreenRectangle Thumb, int Maximum) GetChannelScrollbar()
    {
        var count = editorMode == EditorMode.CirclePlacement ? GetCircleDisplayChannels().Count : (workspace?.Project.Evaluation.Features.Count ?? 0) + 3;
        var visible = VisibleChannelRows;
        var maximum = Math.Max(0, count - visible);
        ChannelScrollPosition = Math.Clamp(ChannelScrollPosition, 0, maximum);
        var panel = GetChannelPanelBounds();
        var track = new ScreenRectangle(panel.X + panel.Width - 20, panel.Y + 66, 14,
            visible * (editorMode == EditorMode.CirclePlacement ? 38 : 48) - 2);
        var height = maximum == 0 ? track.Height : Math.Min(track.Height, Math.Max(24, track.Height * visible / count));
        var offset = maximum == 0 ? 0 : (track.Height - height) * ChannelScrollPosition / maximum;
        return (track, new ScreenRectangle(track.X, track.Y + offset, track.Width, height), maximum);
    }

    private void DrawChannelScrollbar()
    {
        var bar = GetChannelScrollbar();
        DrawRectangle(bar.Track, new Color(13, 17, 22));
        DrawRectangle(bar.Thumb, bar.Maximum == 0 ? new Color(48, 57, 68)
            : draggingChannelScrollbar ? new Color(129, 235, 202) : new Color(104, 157, 204));
    }

    private bool HandleChannelScrollbarInput(MouseState mouse, ScreenPoint pointer)
    {
        if (!ShowsChannels || workspace is null)
        {
            capturingChannelScrollbar = draggingChannelScrollbar = false;
            return false;
        }
        var bar = GetChannelScrollbar();
        if (capturingChannelScrollbar)
        {
            if (mouse.LeftButton == ButtonState.Released)
                capturingChannelScrollbar = draggingChannelScrollbar = false;
            else if (draggingChannelScrollbar)
            {
                var travel = bar.Track.Height - bar.Thumb.Height;
                var fraction = travel <= 0 ? 0 : Math.Clamp((pointer.Y - bar.Track.Y - channelScrollbarGrabOffset) / travel, 0, 1);
                ChannelScrollPosition = (int)Math.Round(fraction * bar.Maximum);
            }
            // Keep the full gesture inside the list, including release over the canvas.
            return true;
        }
        if (mouse.LeftButton != ButtonState.Pressed || previousMouse.LeftButton != ButtonState.Released || !Contains(bar.Track, pointer)) return false;
        capturingChannelScrollbar = true;
        if (bar.Maximum > 0)
        {
            if (Contains(bar.Thumb, pointer))
            {
                draggingChannelScrollbar = true;
                channelScrollbarGrabOffset = pointer.Y - bar.Thumb.Y;
            }
            else ChannelScrollPosition = Math.Clamp(ChannelScrollPosition + (pointer.Y < bar.Thumb.Y ? -1 : 1) * VisibleChannelRows, 0, bar.Maximum);
        }
        return true;
    }
}
