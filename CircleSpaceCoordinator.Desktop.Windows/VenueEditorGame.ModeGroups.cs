namespace CircleSpaceCoordinator.Desktop.Windows;

using Microsoft.Xna.Framework;
using StationeryUI.Canvas;
using StationeryUI.Controls;

public sealed partial class VenueEditorGame
{
    private bool frameModesCollapsed;
    private bool circleModesCollapsed;

    private void DrawModeGroupToggle(ToolbarButton button)
    {
        OperationButtonRenderer.Draw(button.Model,
            (bounds, color) => FillModeToggle(bounds, ToButtonColor(color)),
            (bounds, thickness, color) =>
            {
                FillModeToggle(bounds, ToButtonColor(color), thickness);
            },
            (bounds, color) =>
            {
                var x = bounds.X + bounds.Width / 2;
                var y = bounds.Y + bounds.Height / 2;
                var foreground = ToButtonColor(color);
                DrawLine(new ScreenPoint(x - 5, y), new ScreenPoint(x + 5, y), 2, foreground);
                if (button.Action == ToolbarAction.ToggleFrameModes ? frameModesCollapsed : circleModesCollapsed)
                    DrawLine(new ScreenPoint(x, y - 5), new ScreenPoint(x, y + 5), 2, foreground);
            });
    }

    private void FillModeToggle(ScreenRectangle bounds, Color color, double borderThickness = 0)
    {
        const double radius = 5;
        for (var row = 0; row < (int)bounds.Height; row++)
        {
            var edgeDistance = Math.Min(row + 0.5, bounds.Height - row - 0.5);
            var inset = edgeDistance < radius
                ? radius - Math.Sqrt(radius * radius - Math.Pow(radius - edgeDistance, 2)) : 0;
            if (borderThickness <= 0 || edgeDistance <= borderThickness)
                DrawRectangle(new ScreenRectangle(bounds.X + inset, bounds.Y + row, bounds.Width - inset * 2, 1), color);
            else
            {
                var innerRadius = Math.Max(0, radius - borderThickness);
                var innerDistance = edgeDistance - borderThickness;
                var innerInset = borderThickness + (innerDistance < innerRadius
                    ? innerRadius - Math.Sqrt(innerRadius * innerRadius - Math.Pow(innerRadius - innerDistance, 2)) : 0);
                DrawRectangle(new ScreenRectangle(bounds.X + inset, bounds.Y + row, innerInset - inset, 1), color);
                DrawRectangle(new ScreenRectangle(bounds.X + bounds.Width - innerInset, bounds.Y + row, innerInset - inset, 1), color);
            }
        }
    }
}
