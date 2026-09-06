namespace CircleSpaceCoordinator.Desktop.Windows.Screenshots;

using Microsoft.Xna.Framework;

internal static class ScreenshotEffect
{
    public const double DurationSeconds = 0.42d;

    public static void Draw(float progress, int width, int height, Action<Rectangle, Color> fillRectangle)
    {
        progress = Math.Clamp(progress, 0f, 1f);
        var flashOpacity = progress < 0.30f ? (byte)(210f * (1f - progress / 0.30f)) : (byte)0;
        if (flashOpacity > 0)
            fillRectangle(new Rectangle(0, 0, width, height), new Color(255, 255, 245, (int)flashOpacity));

        var shutterPhase = progress < 0.42f ? progress / 0.42f : 1f - (progress - 0.42f) / 0.58f;
        shutterPhase = Math.Clamp(shutterPhase, 0f, 1f);
        var shutterHeight = (int)(height * 0.075f * shutterPhase);
        if (shutterHeight > 0)
        {
            var shutterColor = new Color(8, 10, 14, (int)(215f * shutterPhase));
            fillRectangle(new Rectangle(0, 0, width, shutterHeight), shutterColor);
            fillRectangle(new Rectangle(0, height - shutterHeight, width, shutterHeight), shutterColor);
        }

        var lineOpacity = (byte)(255f * (1f - progress));
        if (lineOpacity > 0)
            fillRectangle(new Rectangle(0, height / 2 - 2, width, 4), new Color(255, 255, 255, (int)lineOpacity));
    }
}
