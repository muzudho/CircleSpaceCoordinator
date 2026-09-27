namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Desktop.Core;
using Microsoft.Xna.Framework;
using StationeryUI.Canvas;

public sealed partial class VenueEditorGame
{
    private string? GetRedMarkResolutionHint(ScreenPoint pointer)
    {
        if (!CanShowEditorHover || workspace is null || editorMode != EditorMode.DeskPlacement) return null;

        if (hoveredPlanId is { } planId)
        {
            var capacity = GetSpaceCapacity();
            var difference = capacity.Layouts[planId] - capacity.Requested;
            if (difference != 0)
                return $"赤い印：配置可能数が申込数より{Math.Abs(difference):N0} sp{(difference < 0 ? "不足" : "超過")}。この配置案を選び、フレーム配置か申込数を調整して一致させてください。";
        }

        for (var row = 0; row < VisibleChannelRows; row++)
        {
            var index = channelScroll + row;
            if (index >= 3) break;
            if (!Contains(ChannelRow(row), pointer)) continue;
            var count = GetNumberChannelGaps()[index];
            if (count > 0)
                return NumberMarkResolutionHint(index, count);
        }

        if (!IsPointerInEditorCanvas(pointer) || IsWeightChannelSelected || ShowCircleHeatmap) return null;
        var cell = VenueCanvasMapper.ToGridPosition(viewport.ScreenToCell(pointer));
        var plan = workspace.SelectedPlan;
        if (selectedNumberChannel == 0)
            return GetBlockChannelView().Cells.TryGetValue(cell, out var number) && string.IsNullOrWhiteSpace(number)
                ? NumberMarkResolutionHint(0, 1) : null;

        var types = workspace.Project.DeskTypes.ToDictionary(type => type.Id);
        foreach (var placement in plan.DeskPlacements)
        {
            var type = types[placement.DeskTypeId];
            if (selectedNumberChannel == 1)
            {
                if (string.IsNullOrWhiteSpace(placement.DeskNumber) && placement.GetOccupiedCells(type).Contains(cell))
                    return NumberMarkResolutionHint(1, 1);
                continue;
            }

            if (!placement.GetSeatCells(type).Contains(cell)) continue;
            var relative = new GridPosition(cell.X - placement.Anchor.X, cell.Y - placement.Anchor.Y)
                .Rotate((QuarterTurn)((4 - (int)placement.Orientation) % 4));
            var label = plan.SeatLabels.FirstOrDefault(item => item.DeskPlacementId == placement.Id && item.RelativeCell == relative);
            return string.IsNullOrWhiteSpace(label?.SeatName) ? NumberMarkResolutionHint(2, 1) : null;
        }
        return null;
    }

    private static string NumberMarkResolutionHint(int channel, int count)
    {
        var name = NumberChannelNames[channel];
        var target = channel == 1 ? "フレーム" : "セル";
        return $"赤い印：{name}が{count:N0}件未設定。右の［{name}］を選び、赤い{target}をクリックして番号を入力してください。";
    }
}
