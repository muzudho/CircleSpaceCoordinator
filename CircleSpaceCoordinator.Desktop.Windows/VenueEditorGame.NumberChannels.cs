namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Desktop.Core;
using CircleSpaceCoordinator.Desktop.Core.Interaction;
using Microsoft.Xna.Framework;
using StationeryUI.Canvas;

public sealed partial class VenueEditorGame
{
    private EditorCommandResult EditNumberCells(Func<GridPosition, bool> includes)
    {
        if (workspace is null || commandController is null || IsWeightChannelSelected || selectedNumberChannel == 1)
            return EditorCommandResult.NoTarget;
        var plan = workspace.SelectedPlan;
        var types = workspace.Project.DeskTypes.ToDictionary(type => type.Id);
        var targets = plan.DeskPlacements.SelectMany(placement =>
            placement.GetSeatCells(types[placement.DeskTypeId]).Where(includes).Select(cell =>
                new SeatLabelTarget(placement.Id,
                    new GridPosition(cell.X - placement.Anchor.X, cell.Y - placement.Anchor.Y)
                        .Rotate((QuarterTurn)((4 - (int)placement.Orientation) % 4)), cell))).ToArray();
        if (targets.Length == 0) return EditorCommandResult.NoTarget;
        var labels = plan.SeatLabels.ToDictionary(label => (label.DeskPlacementId, label.RelativeCell));
        var values = targets.Select(target => labels.TryGetValue((target.DeskPlacementId, target.RelativeCell), out var label)
            ? selectedNumberChannel == 0 ? label.BlockName : label.SeatName : "").Distinct().ToArray();
        if (values.Length > 1 && System.Windows.Forms.MessageBox.Show(
                $"選択範囲には異なる{NumberChannelName}が入っています。まとめて変更しますか？", "番号入力",
                System.Windows.Forms.MessageBoxButtons.YesNo, System.Windows.Forms.MessageBoxIcon.Warning,
                System.Windows.Forms.MessageBoxDefaultButton.Button2) != System.Windows.Forms.DialogResult.Yes)
            return EditorCommandResult.NoTarget;
        var edit = DeskNumberDialog.Show(values.Length == 1 ? values[0] : "", NumberChannelName);
        if (edit is null) return EditorCommandResult.NoTarget;
        foreach (var target in targets)
        {
            var key = (target.DeskPlacementId, target.RelativeCell);
            var previous = labels.GetValueOrDefault(key) ?? new DeskSeatLabel(target.DeskPlacementId, target.RelativeCell, "", "");
            var updated = selectedNumberChannel == 0
                ? previous with { BlockName = edit.DeskNumber ?? "" }
                : previous with { SeatName = edit.DeskNumber ?? "" };
            if (string.IsNullOrWhiteSpace(updated.BlockName) && string.IsNullOrWhiteSpace(updated.SeatName)) labels.Remove(key);
            else labels[key] = updated;
        }
        return commandController.ReplaceSeatLabels(labels.Values.ToArray());
    }

    private void DrawNumberLabels()
    {
        if (workspace is null || editorMode != EditorMode.DeskPlacement) return;
        var plan = workspace.SelectedPlan;
        var types = workspace.Project.DeskTypes.ToDictionary(type => type.Id);
        var labels = plan.SeatLabels.ToDictionary(label => (label.DeskPlacementId, label.RelativeCell));
        var duplicates = plan.SeatLabels.Where(label => !string.IsNullOrWhiteSpace(label.BlockName) && !string.IsNullOrWhiteSpace(label.SeatName))
            .GroupBy(label => (label.BlockName, label.SeatName)).Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet();
        foreach (var placement in plan.DeskPlacements)
        {
            var seats = selectedNumberChannel == 1
                ? placement.GetFrameNumberCells(types[placement.DeskTypeId]) : placement.GetSeatCells(types[placement.DeskTypeId]);
            foreach (var cell in seats)
            {
                var relative = new GridPosition(cell.X - placement.Anchor.X, cell.Y - placement.Anchor.Y)
                    .Rotate((QuarterTurn)((4 - (int)placement.Orientation) % 4));
                var label = labels.GetValueOrDefault((placement.Id, relative));
                var value = selectedNumberChannel switch { 0 => label?.BlockName, 1 => placement.DeskNumber, _ => label?.SeatName };
                // The frame number is one shared value, shown once on a seat of that frame.
                if (selectedNumberChannel == 1 && cell != (seats.Contains(placement.Anchor) ? placement.Anchor : seats.First())) continue;
                if (string.IsNullOrEmpty(value)) continue;
                var bounds = viewport.GetCellBounds(VenueCanvasMapper.ToCanvasCell(cell));
                var duplicate = selectedNumberChannel != 1 && label is not null && duplicates.Contains((label.BlockName, label.SeatName));
                textRenderer?.Draw(value, ToRectangle(bounds, 4), duplicate ? new Color(96, 0, 12) : new Color(24, 20, 14), VenueTextSize(14), true);
            }
        }
    }
}
