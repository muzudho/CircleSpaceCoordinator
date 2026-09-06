namespace CircleSpaceCoordinator.StationeryUI.Controls;

using CircleSpaceCoordinator.StationeryUI.Canvas;

public sealed class IconButtonModel(ScreenRectangle bounds, string accessibleName)
{
    public ScreenRectangle Bounds { get; } = bounds;

    public string AccessibleName { get; } = string.IsNullOrWhiteSpace(accessibleName)
        ? throw new ArgumentException("An accessible name is required.", nameof(accessibleName))
        : accessibleName;

    public bool IsEnabled { get; set; } = true;

    public bool IsSelected { get; set; }

    public bool IsPointerOver { get; private set; }

    public bool IsPressed { get; private set; }

    public bool Contains(ScreenPoint point) =>
        point.X >= Bounds.X && point.X < Bounds.X + Bounds.Width &&
        point.Y >= Bounds.Y && point.Y < Bounds.Y + Bounds.Height;

    public bool IsHit(ScreenPoint point) => IsEnabled && Contains(point);

    public void UpdatePointer(ScreenPoint point)
    {
        IsPointerOver = IsHit(point);
        if (!IsEnabled)
            IsPressed = false;
    }

    public bool Press(ScreenPoint point)
    {
        UpdatePointer(point);
        IsPressed = IsPointerOver;
        return IsPressed;
    }

    public bool Release(ScreenPoint point)
    {
        var clicked = IsPressed && IsHit(point);
        IsPressed = false;
        UpdatePointer(point);
        return clicked;
    }

    public void CancelPress() => IsPressed = false;
}
