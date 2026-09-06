namespace CircleSpaceCoordinator.StationeryUI.Controls;

using CircleSpaceCoordinator.StationeryUI.Canvas;

public readonly record struct ButtonColor(byte R, byte G, byte B, byte A = 255);

/// <summary>文房具ボタンの外観を描き、ラベルや既存アイコンの描画はホストへ委譲します。</summary>
public static class StationeryButtonRenderer
{
    public static void Draw(
        IconButtonModel model,
        Action<ScreenRectangle, ButtonColor> fillRectangle,
        Action<ScreenRectangle, double, ButtonColor> drawOutline,
        Action<ScreenRectangle, ButtonColor> drawContent)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(fillRectangle);
        ArgumentNullException.ThrowIfNull(drawOutline);
        ArgumentNullException.ThrowIfNull(drawContent);

        var offset = model.IsPressed && model.IsEnabled ? 2d : 0d;
        var bounds = model.Bounds with { X = model.Bounds.X + offset, Y = model.Bounds.Y + offset };
        var fill = !model.IsEnabled ? new ButtonColor(24, 27, 31)
            : model.IsSelected ? new ButtonColor(31, 151, 112)
            : model.IsPointerOver ? new ButtonColor(58, 82, 94) : new ButtonColor(36, 48, 58);
        var border = !model.IsEnabled ? new ButtonColor(43, 50, 56)
            : model.IsSelected ? new ButtonColor(151, 255, 215)
            : model.IsPointerOver ? new ButtonColor(178, 219, 226) : new ButtonColor(126, 150, 164);

        fillRectangle(bounds with { X = bounds.X + 4d, Y = bounds.Y + 5d },
            new ButtonColor(0, 0, 0, model.IsEnabled ? (byte)95 : (byte)28));
        fillRectangle(bounds, fill);
        drawOutline(bounds, 2d, border);
        if (model.IsEnabled && bounds.Width > 4d && bounds.Height > 4d)
            drawOutline(new ScreenRectangle(bounds.X + 2d, bounds.Y + 2d, bounds.Width - 4d, bounds.Height - 4d), 1d,
                model.IsSelected ? new ButtonColor(215, 255, 238, 95)
                    : new ButtonColor(255, 255, 255, model.IsPointerOver ? (byte)70 : (byte)36));

        drawContent(bounds, model.IsEnabled ? new ButtonColor(255, 255, 255) : new ButtonColor(91, 100, 106));
    }
}
