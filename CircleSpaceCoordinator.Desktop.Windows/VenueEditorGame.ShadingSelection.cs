namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Desktop.Core.Interaction;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StationeryUI.Canvas;

public sealed partial class VenueEditorGame
{
    private readonly ShadingRowSelection shadingSelection = new();
    private bool MultipleShadingRows => shadingSelection.Multiple;
    private bool ShadingRowSelected(bool right, string key) => shadingSelection.Keys.Count > 0
        ? shadingSelection.Right == right && shadingSelection.Keys.Contains(key)
        : key == (right ? selectedPackageGenreKey : selectedGenreKey);
    private void ClickShadingRow(bool right, string key, KeyboardState keyboard)
    {
        var order = right ? PackageGenreRows().Select(row => row.Key).ToArray() : mappingDraft!.Rows.Select(row => row.Key).ToArray();
        if (!shadingSelection.Click(right, order, key, keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift), IsControlDown(keyboard))) return;
        if (shadingSelection.Active is { } active)
        {
            if (right) SelectPackageGenreTarget(active, preserveSelection: true);
            else SelectGenreTarget(active, preserveSelection: true);
        }
        else { selectedGenreKey = selectedPackageGenreKey = null; mappingWidth = -1; }
    }
    private ScreenRectangle LockedShadingPaneBounds()
    {
        var bounds = MappingGridBounds(20, 62, 960, 386, !shadingSelection.Right);
        return new(bounds.X - 5, bounds.Y, bounds.Width + 23 * MappingEditorScale, bounds.Height);
    }
    private void DrawShadingSelectionLock()
    {
        if (!GenreGridSplit || !MultipleShadingRows) return;
        DrawRectangle(LockedShadingPaneBounds(), new Color(80, 80, 80, 175));
    }
}
