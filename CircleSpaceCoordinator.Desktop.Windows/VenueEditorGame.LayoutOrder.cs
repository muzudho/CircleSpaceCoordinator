namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Engine.Model;
using StationeryUI.Canvas;

public sealed partial class VenueEditorGame
{
    private bool ShowsLayoutOrder => UsesSeparatedLayouts && ShowsDeskLayouts;

    private ScreenRectangle LayoutOrderButton(int direction) => new(
        GraphicsDevice.Viewport.Width - (direction < 0 ? 88 : 54), ToolbarHeight + 16, 30, 28);

    private bool CanMoveLayout(int direction)
    {
        if (!ShowsLayoutOrder) return false;
        var index = workspace!.Project.DeskLayouts.ToList().FindIndex(item => item.Id == workspace.SelectedDeskLayoutId);
        return index >= 0 && index + direction >= 0 && index + direction < workspace.Project.DeskLayouts.Count;
    }

    private void MoveSelectedLayout(int direction)
    {
        if (!CanMoveLayout(direction)) return;
        workspace!.Execute(new LayoutCatalogServiceMoveDeskLayout(workspace.SelectedDeskLayoutId, direction), selectedPlanEdit: false);
        var index = workspace.Project.DeskLayouts.ToList().FindIndex(item => item.Id == workspace.SelectedDeskLayoutId);
        var visible = GetVisiblePlanRowCount();
        planScroll = Math.Clamp(planScroll, Math.Max(0, index - visible + 1), index);
    }

    private bool HandleLayoutOrderClick(ScreenPoint pointer)
    {
        if (!ShowsLayoutOrder) return false;
        foreach (var direction in new[] { -1, 1 })
        {
            if (!Contains(LayoutOrderButton(direction), pointer)) continue;
            var enabled = CanMoveLayout(direction);
            MoveSelectedLayout(direction);
            LogPointer(direction < 0 ? "layout_move_up" : "layout_move_down", pointer, enabled);
            return true;
        }
        return false;
    }

    private void DrawLayoutOrderButtons()
    {
        if (!ShowsLayoutOrder) return;
        var pointer = new ScreenPoint(previousMouse.X, previousMouse.Y);
        foreach (var direction in new[] { -1, 1 })
            DrawLayoutButton(LayoutOrderButton(direction), direction < 0 ? "↑" : "↓",
                CanShowEditorHover && Contains(LayoutOrderButton(direction), pointer), CanMoveLayout(direction));
    }
}
