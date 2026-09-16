namespace CircleSpaceCoordinator.Desktop.Windows;

using StationeryUI.Canvas;
using StationeryUI.Controls;

/// <summary>Selection uses a green fill; only an operation target has a cyan outline.</summary>
internal static class OperationButtonRenderer
{
    public static void Draw(IconButtonModel button,
        Action<ScreenRectangle, ButtonColor> fill,
        Action<ScreenRectangle, double, ButtonColor> outline,
        Action<ScreenRectangle, ButtonColor> content)
    {
        fill(button.Bounds, !button.IsEnabled ? new(35, 40, 46, 255)
            : button.IsSelected ? new(35, 126, 111, 255) : new(35, 43, 54, 255));
        // Retain the library's content layout and disabled foreground, without its decorative borders/shadow.
        StationeryButtonRenderer.Draw(button, (_, _) => { }, (_, _, _) => { }, content);
        if (button.IsEnabled && button.IsPointerOver)
            outline(button.Bounds, 2, new(115, 231, 255, 255));
    }
}
