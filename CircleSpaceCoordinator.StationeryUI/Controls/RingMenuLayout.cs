namespace CircleSpaceCoordinator.ReusableControls;

using StationeryUI.Canvas;

/// <summary>Renderer-independent circular menu geometry, staged for migration to StationeryUI.</summary>
public sealed record RingMenuLayout(ScreenPoint Center, double Radius, IReadOnlyList<ScreenRectangle> Buttons)
{
    public static RingMenuLayout Create(ScreenRectangle anchor, double width, double height, int count)
    {
        if (!double.IsFinite(width) || width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (!double.IsFinite(height) || height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
        var radius = Math.Max(0d, Math.Min(72d, Math.Min((width - 60d) / 2d, (height - 60d) / 2d)));
        var halfSize = Math.Min(22d, Math.Min(width, height) / 2d);
        var extent = radius + halfSize;
        var center = new ScreenPoint(
            Math.Clamp(anchor.X + anchor.Width / 2d, extent, Math.Max(extent, width - extent)),
            Math.Clamp(anchor.Y + anchor.Height / 2d, extent, Math.Max(extent, height - extent)));
        var buttons = new ScreenRectangle[count];
        for (var index = 0; index < count; index++)
        {
            var angle = -Math.PI / 2d + index * Math.Tau / count;
            buttons[index] = new ScreenRectangle(
                center.X + Math.Cos(angle) * radius - halfSize,
                center.Y + Math.Sin(angle) * radius - halfSize, halfSize * 2d, halfSize * 2d);
        }
        return new RingMenuLayout(center, radius, Array.AsReadOnly(buttons));
    }
}
