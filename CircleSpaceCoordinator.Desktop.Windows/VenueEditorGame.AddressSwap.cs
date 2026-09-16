namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Desktop.Core;
using StationeryUI.Canvas;

public sealed partial class VenueEditorGame
{
    private bool addressSwapEnabled;
    private CircleSpaceProject? addressSwapProject;
    private string? addressSwapPlanId;
    private int addressSwapChannel = -1;
    private string[] addressSwapFrames = [];
    private bool ShowsAddressSwapButton => editorMode == EditorMode.DeskPlacement && selectedChannelId is null;
    private bool IsAddressSwapMode => ShowsAddressSwapButton && addressSwapEnabled && activeCanvasTool == ToolbarAction.EditSeatName;
    private void ToggleAddressSwapMode()
    {
        var enabled = !IsAddressSwapMode;
        CancelInProgressPointerInteraction();
        addressSwapEnabled = enabled;
        activeCanvasTool = ToolbarAction.EditSeatName;
        rangeSwapStatus = enabled ? "番地スワップ：Shift＋ドラッグで選択 → 範囲をドラッグ。Escで解除。" : "番地入力モード";
    }

    private bool CanSwapSelectedAddress(ScreenPoint pointer)
    {
        if (workspace is null || !ShowsAddressSwapButton || activeCanvasTool != ToolbarAction.EditSeatName) return false;
        var cell = VenueCanvasMapper.ToGridPosition(viewport.ScreenToCell(pointer));
        if (selectedNumberChannel == 1)
            return workspace.GetSelectedPlanSnapshot().Desks.Any(frame => selectedFrameIds.Contains(frame.Id) && frame.OccupiedCells.Contains(cell));
        return selectedCellRange is { } range && range.Contains(cell);
    }

    private void BeginAddressSwap(ScreenPoint pointer)
    {
        if (workspace is null) return;
        var cell = VenueCanvasMapper.ToGridPosition(viewport.ScreenToCell(pointer));
        var plan = workspace.SelectedPlan;
        var types = workspace.Project.DeskTypes.ToDictionary(type => type.Id);
        if (selectedNumberChannel == 1)
        {
            var clicked = plan.DeskPlacements.LastOrDefault(frame => frame.GetOccupiedCells(types[frame.DeskTypeId]).Contains(cell));
            if (clicked is null) return;
            if (!selectedFrameIds.Contains(clicked.Id)) { selectedFrameIds.Clear(); selectedFrameIds.Add(clicked.Id); }
            addressSwapFrames = plan.DeskPlacements.Where(frame => selectedFrameIds.Contains(frame.Id)).Select(frame => frame.Id).ToArray();
            var cells = plan.DeskPlacements.Where(frame => selectedFrameIds.Contains(frame.Id))
                .SelectMany(frame => frame.GetOccupiedCells(types[frame.DeskTypeId])).ToArray();
            draggedCellRange = CellRange.From(new(cells.Min(p => p.X), cells.Min(p => p.Y)), new(cells.Max(p => p.X), cells.Max(p => p.Y)));
        }
        else
        {
            if (selectedCellRange is { } selected && selected.Contains(cell)) draggedCellRange = selected;
            else
            {
                if (!plan.DeskPlacements.Any(frame => frame.GetSeatCells(types[frame.DeskTypeId]).Contains(cell))) return;
                draggedCellRange = CellRange.From(cell, cell);
            }
            selectedCellRange = draggedCellRange;
            addressSwapFrames = [];
        }
        var range = draggedCellRange!.Value;
        rangeDragGrabOffset = new(cell.X - range.Left, cell.Y - range.Top);
        participantDragPointer = pointer;
        addressSwapChannel = selectedNumberChannel;
        addressSwapPlanId = workspace.SelectedPlanId;
        addressSwapProject = workspace.Project;
        rangeSwapStatus = "移動先と番地を入れ替えます。Escで取消。";
    }

    private void FinishAddressSwap(ScreenPoint pointer)
    {
        var range = draggedCellRange!.Value;
        var channel = addressSwapChannel;
        var frames = addressSwapFrames;
        var unchanged = workspace is not null && workspace.SelectedPlanId == addressSwapPlanId && ReferenceEquals(workspace.Project, addressSwapProject);
        draggedCellRange = null;
        addressSwapChannel = -1;
        addressSwapProject = null;
        if (!unchanged || !IsPointerInEditorCanvas(pointer)) { rangeSwapStatus = "番地のスワップを取り消しました"; return; }
        var cell = VenueCanvasMapper.ToGridPosition(viewport.ScreenToCell(pointer));
        var destination = new GridPosition(cell.X - rangeDragGrabOffset.X, cell.Y - rangeDragGrabOffset.Y);
        var source = new GridPosition(range.Left, range.Top);
        if (source == destination) { rangeSwapStatus = "番地のスワップ：範囲をドラッグしてください"; return; }
        var result = commandController!.SwapAddresses(channel, source, destination, range.Width, range.Height, frames);
        if (result.Applied)
        {
            rangeSwapStatus = $"{NumberChannelNames[channel]}をスワップしました";
            selectedCellRange = null;
            selectedFrameIds.Clear();
        }
        else
        {
            rangeSwapStatus = "番地をスワップできませんでした";
            ShowInAppMessage("番地のスワップ", string.Join("\n", result.Issues.Select(issue => issue.Message)));
        }
    }

    private void DrawAddressSwapFramePreview()
    {
        if (addressSwapChannel != 1 || draggedCellRange is not { } range) return;
        var cell = VenueCanvasMapper.ToGridPosition(viewport.ScreenToCell(participantDragPointer));
        var first = new GridPosition(cell.X - rangeDragGrabOffset.X, cell.Y - rangeDragGrabOffset.Y);
        DrawOutline(GetCellRegionBounds(first, new(first.X + range.Width - 1, first.Y + range.Height - 1)), 3, OperationTargetColor);
    }
}
