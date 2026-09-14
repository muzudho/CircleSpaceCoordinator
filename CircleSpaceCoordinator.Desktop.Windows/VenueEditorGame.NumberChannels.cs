namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Desktop.Core;
using CircleSpaceCoordinator.Desktop.Core.Interaction;
using Microsoft.Xna.Framework;
using StationeryUI.Canvas;
using StationeryUI.Controls;

public sealed partial class VenueEditorGame
{
    private CircleSpaceProject? numberGapsProject;
    private string? numberGapsPlanId;
    private NumberChannelGaps? numberGaps;

    private void OpenNumberInput(string field, string? initial, bool confirmOverwrite, Action<string> accepted)
    {
        void Edit() => OpenUnderlineInput(field + "を変更", initial ?? "", accepted,
            "番号を入力してください（80 文字まで）。\n空欄で確定すると削除します。キャンセルでは変更しません。", 80, allowEmpty: true);
        if (!confirmOverwrite) { Edit(); return; }
        OpenModal(new ModalDialogModel(ModalDialogKind.Confirmation, "番号入力",
            $"選択範囲には異なる{field}が入っています。まとめて変更しますか？"), action =>
            { if (action == ModalDialogAction.Accept) Edit(); },
            [("キャンセル", ModalDialogAction.Cancel), ("変更する", ModalDialogAction.Accept)]);
    }

    private NumberChannelGaps GetNumberChannelGaps()
    {
        var project = workspace!.Project;
        var plan = workspace.SelectedPlan;
        if (numberGaps is null || !ReferenceEquals(project, numberGapsProject) || numberGapsPlanId != plan.Id)
        {
            numberGapsProject = project;
            numberGapsPlanId = plan.Id;
            numberGaps = NumberChannelGaps.Find(project, plan);
        }
        return numberGaps;
    }

    private string? GetNumberChannelHoverError(ScreenPoint pointer)
    {
        if (workspace is null || editorMode != EditorMode.DeskPlacement) return null;
        for (var row = 0; row < VisibleChannelRows; row++)
        {
            var index = channelScroll + row;
            if (index >= 3) break;
            if (!Contains(ChannelRow(row), pointer)) continue;
            var count = GetNumberChannelGaps()[index];
            if (count > 0)
                return $"エラー：{NumberChannelNames[index]}が未設定の{(index == 1 ? "フレーム" : "スペース")}が{count}件あります。番号を設定してください。";
        }
        return null;
    }

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
        var channel = selectedNumberChannel;
        var editingWorkspace = workspace;
        var planId = workspace.SelectedPlanId;
        OpenNumberInput(NumberChannelName, values.Length == 1 ? values[0] : "", values.Length > 1, value =>
        {
            if (workspace != editingWorkspace || workspace.SelectedPlanId != planId)
                throw new InvalidOperationException("編集対象が変わりました。選び直してください。");
            foreach (var target in targets)
            {
                var key = (target.DeskPlacementId, target.RelativeCell);
                var previous = labels.GetValueOrDefault(key) ?? new DeskSeatLabel(target.DeskPlacementId, target.RelativeCell, "", "");
                var updated = channel == 0
                    ? previous with { BlockName = value }
                    : previous with { SeatName = value };
                if (string.IsNullOrWhiteSpace(updated.BlockName) && string.IsNullOrWhiteSpace(updated.SeatName)) labels.Remove(key);
                else labels[key] = updated;
            }
            var result = commandController.ReplaceSeatLabels(labels.Values.ToArray());
            Log("number_edit", result.Applied);
            if (!result.Applied) ShowInAppMessage("番号入力", "番号を変更できませんでした。");
        });
        return EditorCommandResult.Success;
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
                var bounds = viewport.GetCellBounds(VenueCanvasMapper.ToCanvasCell(cell));
                if (string.IsNullOrWhiteSpace(value))
                {
                    // Missing frame numbers are drawn once across the entire frame.
                    if (selectedNumberChannel != 1)
                        DrawMissingNumber(new ScreenRectangle(bounds.X + 2, bounds.Y + 2,
                            Math.Max(1, bounds.Width - 4), Math.Max(1, bounds.Height - 4)));
                    continue;
                }
                var duplicate = selectedNumberChannel != 1 && label is not null && duplicates.Contains((label.BlockName, label.SeatName));
                textRenderer?.Draw(value, ToRectangle(bounds, 4), duplicate ? new Color(96, 0, 12) : new Color(24, 20, 14), VenueTextSize(14), true);
            }
        }
    }

    private void DrawMissingNumber(ScreenRectangle bounds)
    {
        DrawRectangle(bounds, new Color(204, 38, 48, 92));
        DrawOutline(bounds, 2, new Color(255, 72, 80));
        textRenderer?.Draw("？", ToRectangle(bounds), new Color(255, 235, 235), VenueTextSize(14), true);
    }
}
