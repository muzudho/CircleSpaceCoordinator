namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Desktop.Core;
using CircleSpaceCoordinator.Engine.Model;
using Microsoft.Xna.Framework;
using StationeryUI.Canvas;

public sealed partial class VenueEditorGame
{
    private const int ChannelPanelReservedHeight = 290;
    private string? selectedChannelId;
    private int channelScroll;
    private int planScroll;
    private bool IsWeightChannelSelected => editorMode == EditorMode.DeskPlacement && selectedChannelId is not null &&
        workspace?.Project.Evaluation.Features.Any(item => item.Id == selectedChannelId) == true;

    private ScreenRectangle GetChannelPanelBounds()
    {
        var plans = GetPlanListBounds(Math.Min(GetDisplayedPlans().Count, GetVisiblePlanRowCount()));
        var top = plans.Y + plans.Height + 8;
        return new ScreenRectangle(plans.X, top, plans.Width,
            Math.Max(160, Math.Min(ChannelPanelReservedHeight - 8,
                GraphicsDevice.PresentationParameters.BackBufferHeight - StatusBarHeight - top - 8)));
    }

    private ScreenRectangle ChannelButton(int index)
    {
        var panel = GetChannelPanelBounds();
        return new ScreenRectangle(panel.X + 8 + index * 83, panel.Y + 34, 78, 26);
    }

    private ScreenRectangle ChannelRow(int index)
    {
        var panel = GetChannelPanelBounds();
        return new ScreenRectangle(panel.X + 6, panel.Y + 66 + index * 48, panel.Width - 12, 46);
    }

    private int VisibleChannelRows => Math.Max(1, (int)(GetChannelPanelBounds().Height - 114) / 48);

    private void DrawChannels()
    {
        if (editorMode != EditorMode.DeskPlacement || workspace is null) return;
        if (!IsWeightChannelSelected) selectedChannelId = null;
        var panel = GetChannelPanelBounds();
        var features = workspace.Project.Evaluation.Features;
        channelScroll = Math.Clamp(channelScroll, 0, Math.Max(0, features.Count + 1 - VisibleChannelRows));
        DrawRectangle(panel, new Color(20, 25, 32, 245));
        DrawOutline(panel, 2, new Color(88, 103, 120));
        textRenderer?.Draw("チャンネル", new Rectangle((int)panel.X + 10, (int)panel.Y + 4, 240, 26), Color.White, 20, true);
        DrawLayoutButton(ChannelButton(0), "追加", false);
        DrawLayoutButton(ChannelButton(1), "列・名前", false, IsWeightChannelSelected);
        DrawLayoutButton(ChannelButton(2), "削除", false, IsWeightChannelSelected);
        var evaluation = workspace.GetSelectedPlanSnapshot().Evaluation;
        for (var rowIndex = 0; rowIndex < VisibleChannelRows; rowIndex++)
        {
            var index = channelScroll + rowIndex;
            if (index > features.Count) break;
            var feature = index == 0 ? null : features[index - 1];
            var selected = feature?.Id == selectedChannelId;
            var row = ChannelRow(rowIndex);
            DrawRectangle(row, selected ? new Color(35, 126, 111) : new Color(29, 36, 45));
            var score = evaluation.Features.FirstOrDefault(item => item.FeatureId == feature?.Id)?.WeightedScore ?? 0;
            textRenderer?.Draw(feature is null ? "番地" : $"{feature.Name}  {score:0.###}点",
                new Rectangle((int)row.X + 6, (int)row.Y + 1, 240, 27), Color.White, 15, selected);
            textRenderer?.Draw(feature is null ? "ブロック名・机番地・セル番地（採点なし）" : $"列: {feature.SourceColumn ?? "（対応なし）"}",
                new Rectangle((int)row.X + 6, (int)row.Y + 26, 240, 20), new Color(184, 204, 214), 11);
        }
        textRenderer?.Draw($"サークル配置評価値: {evaluation.TotalScore:0.###}",
            new Rectangle((int)panel.X + 10, (int)(panel.Y + panel.Height) - 44, 245, 21), new Color(244, 208, 111), 15);
        textRenderer?.Draw("セルをクリックで入力 / リストはホイールで移動",
            new Rectangle((int)panel.X + 8, (int)(panel.Y + panel.Height) - 22, 248, 18), Color.LightGray, 11);
    }

    private bool ScrollChannels(ScreenPoint pointer, int delta)
    {
        if (editorMode != EditorMode.DeskPlacement || workspace is null || !Contains(GetChannelPanelBounds(), pointer)) return false;
        channelScroll = Math.Clamp(channelScroll + (delta > 0 ? -1 : 1), 0,
            Math.Max(0, workspace.Project.Evaluation.Features.Count + 1 - VisibleChannelRows));
        return true;
    }

    private bool ScrollPlanList(ScreenPoint pointer, int delta)
    {
        if (workspace is null || editorMode == EditorMode.GenreData) return false;
        var count = GetDisplayedPlans().Count;
        if (!Contains(GetPlanListBounds(Math.Min(count, GetVisiblePlanRowCount())), pointer)) return false;
        planScroll = Math.Clamp(planScroll + (delta > 0 ? -1 : 1), 0, Math.Max(0, count - GetVisiblePlanRowCount()));
        return true;
    }

    private bool HandleChannelPanelClick(ScreenPoint pointer)
    {
        if (editorMode != EditorMode.DeskPlacement || workspace is null || !Contains(GetChannelPanelBounds(), pointer)) return false;
        try
        {
            if (Contains(ChannelButton(0), pointer)) EditChannelDefinition(create: true);
            else if (Contains(ChannelButton(1), pointer) && IsWeightChannelSelected) EditChannelDefinition(create: false);
            else if (Contains(ChannelButton(2), pointer) && IsWeightChannelSelected)
            {
                if (System.Windows.Forms.MessageBox.Show("チャンネルとその重み・列の対応を削除しますか？", "チャンネルの削除",
                    System.Windows.Forms.MessageBoxButtons.OKCancel) == System.Windows.Forms.DialogResult.OK)
                {
                    workspace.Execute(new RemoveChannel(selectedChannelId!), selectedPlanEdit: false);
                    selectedChannelId = null;
                }
            }
            else
            {
                for (var row = 0; row < VisibleChannelRows; row++)
                {
                    var index = row + channelScroll;
                    if (index > workspace.Project.Evaluation.Features.Count || !Contains(ChannelRow(row), pointer)) continue;
                    selectedChannelId = index == 0 ? null : workspace.Project.Evaluation.Features[index - 1].Id;
                    activeCanvasTool = ToolbarAction.EditSeatName;
                    selectedCellRange = null;
                }
            }
        }
        catch (Exception exception) { ShowChannelError(exception); }
        return true;
    }

    private void EditChannelDefinition(bool create)
    {
        if (workspace is null) return;
        var feature = create ? null : workspace.Project.Evaluation.Features.Single(item => item.Id == selectedChannelId);
        var columns = workspace.Project.Participants.SelectMany(item => item.SourceValues.Keys).Distinct(StringComparer.Ordinal).ToArray();
        var result = ChannelDialog.Show(feature?.Name ?? "", feature?.SourceColumn, columns);
        if (result is null) return;
        var id = feature?.Id ?? $"channel-{Guid.NewGuid():N}";
        workspace.Execute(new UpsertChannel(id, result.Value.Name, result.Value.Column), selectedPlanEdit: false);
        selectedChannelId = id;
        channelScroll = Math.Max(0, workspace.Project.Evaluation.Features.Count + 1 - VisibleChannelRows);
        activeCanvasTool = ToolbarAction.EditSeatName;
    }

    private void EditChannelWeight(GridPosition cell)
    {
        if (workspace is null || !IsWeightChannelSelected) return;
        var deskCells = workspace.GetSelectedPlanSnapshot().Desks.SelectMany(item => item.OccupiedCells).ToHashSet();
        if (!deskCells.Contains(cell)) return;
        var cells = selectedCellRange is { } range && range.Contains(cell)
            ? deskCells.Where(range.Contains).ToArray() : [cell];
        var map = workspace.Project.Evaluation.WeightMaps.Single(item => item.FeatureId == selectedChannelId);
        var value = ChannelDialog.ShowWeight(map.GetWeight(cell));
        if (value is null) return;
        try
        {
            workspace.Execute(new SetChannelWeights(workspace.SelectedPlanId, selectedChannelId!, cells, value.Value));
            rangeSwapStatus = $"{cells.Length} セルの重みを変更しました";
            Log("channel_weight_edit", success: true, detail: $"cells={cells.Length}");
        }
        catch (Exception exception) { ShowChannelError(exception); }
    }

    private void DrawChannelWeights()
    {
        if (workspace is null) return;
        var map = workspace.Project.Evaluation.WeightMaps.Single(item => item.FeatureId == selectedChannelId);
        foreach (var cell in workspace.GetSelectedPlanSnapshot().Desks.SelectMany(item => item.OccupiedCells))
        {
            var bounds = viewport.GetCellBounds(VenueCanvasMapper.ToCanvasCell(cell));
            var weight = map.GetWeight(cell);
            DrawRectangle(bounds, Color.Lerp(new Color(35, 40, 54), new Color(35, 166, 134), (float)Math.Clamp(weight, 0, 1)));
            DrawOutline(bounds, 1, new Color(106, 129, 145));
            textRenderer?.Draw($"{weight:0.######}", ToRectangle(bounds, 2), Color.White, VenueTextSize(12), true);
        }
    }

    private static void ShowChannelError(Exception exception) => System.Windows.Forms.MessageBox.Show(exception.Message,
        "チャンネルの編集", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
}
