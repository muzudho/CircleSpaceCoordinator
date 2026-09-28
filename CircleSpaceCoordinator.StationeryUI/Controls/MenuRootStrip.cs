namespace StationeryUI.Controls;

using StationeryUI.Canvas;

/// <summary>One-line menu heading with a root-shaped line grouping its child items.</summary>
public sealed class MenuRootStrip
{
    private MenuRootStrip(ScreenRectangle toggleBounds, ScreenRectangle headingBounds,
        IReadOnlyList<ScreenRectangle> itemBounds)
    {
        ToggleBounds = toggleBounds;
        HeadingBounds = headingBounds;
        ItemBounds = itemBounds;
    }

    public ScreenRectangle ToggleBounds { get; }
    public ScreenRectangle HeadingBounds { get; }
    public IReadOnlyList<ScreenRectangle> ItemBounds { get; }

    public static MenuRootStrip Create(ScreenRectangle bounds, IReadOnlyList<double> itemWeights,
        double toggleWidth = 24d, double toggleGap = 6d, double headingWidth = 72d,
        double rootWidth = 14d, double itemGap = 3d)
    {
        ArgumentNullException.ThrowIfNull(itemWeights);
        if (itemWeights.Count == 0 || itemWeights.Any(weight => !double.IsFinite(weight) || weight <= 0d))
            throw new ArgumentException("At least one positive item weight is required.", nameof(itemWeights));
        if (!double.IsFinite(bounds.Width) || !double.IsFinite(bounds.Height) || bounds.Height <= 0d ||
            !double.IsFinite(toggleWidth) || toggleWidth <= 0d || !double.IsFinite(toggleGap) || toggleGap < 0d ||
            !double.IsFinite(headingWidth) || headingWidth <= 0d || !double.IsFinite(rootWidth) || rootWidth < 13d ||
            !double.IsFinite(itemGap) || itemGap < 0d)
            throw new ArgumentOutOfRangeException(nameof(bounds), "Menu root strip dimensions must be finite and positive.");

        var itemWidth = bounds.Width - toggleWidth - toggleGap - headingWidth - rootWidth - itemGap * (itemWeights.Count - 1);
        var totalWeight = itemWeights.Sum();
        if (itemWidth <= 0d || !double.IsFinite(totalWeight))
            throw new ArgumentOutOfRangeException(nameof(bounds), "Menu root strip is too narrow for its items.");

        var toggle = new ScreenRectangle(bounds.X, bounds.Y + (bounds.Height - toggleWidth) / 2d, toggleWidth, toggleWidth);
        var heading = new ScreenRectangle(bounds.X + toggleWidth + toggleGap, bounds.Y, headingWidth, bounds.Height);
        var items = new ScreenRectangle[itemWeights.Count];
        var x = heading.X + heading.Width + rootWidth;
        var right = bounds.X + bounds.Width;
        for (var index = 0; index < items.Length; index++)
        {
            var width = index == items.Length - 1 ? right - x : itemWidth * itemWeights[index] / totalWeight;
            items[index] = new ScreenRectangle(x, bounds.Y, width, bounds.Height);
            x += width + itemGap;
        }
        return new MenuRootStrip(toggle, heading, items);
    }

    public void Draw(string heading, Action<string, ScreenRectangle> drawHeading,
        Action<ScreenPoint, ScreenPoint, double> drawLine, double lineThickness = 2d)
    {
        ArgumentNullException.ThrowIfNull(drawHeading);
        ArgumentNullException.ThrowIfNull(drawLine);
        drawHeading(heading, HeadingBounds);
        var x = HeadingBounds.X + HeadingBounds.Width;
        var top = HeadingBounds.Y + 2d;
        drawLine(new ScreenPoint(x, top + 18d), new ScreenPoint(x + 4d, top + 23d), lineThickness);
        drawLine(new ScreenPoint(x + 4d, top + 23d), new ScreenPoint(x + 13d, top), lineThickness);
        var last = ItemBounds[^1];
        drawLine(new ScreenPoint(x + 13d, top), new ScreenPoint(last.X + last.Width, top), lineThickness);
    }
}
