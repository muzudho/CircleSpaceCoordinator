namespace CircleSpaceCoordinator.Desktop.Windows;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StationeryUI.Inspection;
using global::StationeryUI.Windows;

public sealed partial class VenueEditorGame
{
    private readonly StationeryDeveloperWindow developerWindow = new();
    private bool previousDeveloperKey;
    private double inspectionElapsed;
    private string? reportedDeveloperError;

    private IReadOnlyList<StationeryInspectionEntry> InspectStationery() => EventLayout.Inspect(
        workspace is null && mappingDraft is null && frameDraft is null);

    private void UpdateDeveloperWindow(GameTime gameTime, KeyboardState keyboard, bool active)
    {
        var down = keyboard.IsKeyDown(Keys.F12);
        if (active && down && !previousDeveloperKey)
            developerWindow.Show(InspectStationery());
        previousDeveloperKey = down;
        inspectionElapsed += gameTime.ElapsedGameTime.TotalSeconds;
        if (developerWindow.IsOpen && inspectionElapsed >= .25)
        {
            developerWindow.Update(InspectStationery());
            inspectionElapsed = 0;
        }
        if (developerWindow.LastError != reportedDeveloperError)
        {
            reportedDeveloperError = developerWindow.LastError;
            if (reportedDeveloperError is not null)
                // The upstream launcher also reports a broken pipe on a normal child-window close.
                // Keep diagnostics without interrupting the user's main window with a modal.
                Log("developer_window_connection", false, detail: reportedDeveloperError);
        }
    }
}
