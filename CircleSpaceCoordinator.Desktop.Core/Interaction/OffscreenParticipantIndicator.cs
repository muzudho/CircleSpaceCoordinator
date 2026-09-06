namespace CircleSpaceCoordinator.Desktop.Core.Interaction;

using CircleSpaceCoordinator.StationeryUI.Canvas;

public enum ScreenEdge { Left, Right, Top, Bottom }

public static class OffscreenParticipantIndicator
{
    // A partly visible token needs no arrow. Use the dominant direction for
    // diagonal targets so each participant is counted only once.
    public static ScreenEdge? FindEdge(ScreenRectangle token, ScreenRectangle viewport)
    {
        if (token.X < viewport.X + viewport.Width && token.X + token.Width > viewport.X &&
            token.Y < viewport.Y + viewport.Height && token.Y + token.Height > viewport.Y)
            return null;
        var dx = (token.X + token.Width / 2d - viewport.X - viewport.Width / 2d) / (viewport.Width / 2d);
        var dy = (token.Y + token.Height / 2d - viewport.Y - viewport.Height / 2d) / (viewport.Height / 2d);
        return Math.Abs(dx) > Math.Abs(dy)
            ? dx < 0 ? ScreenEdge.Left : ScreenEdge.Right
            : dy < 0 ? ScreenEdge.Top : ScreenEdge.Bottom;
    }
}
