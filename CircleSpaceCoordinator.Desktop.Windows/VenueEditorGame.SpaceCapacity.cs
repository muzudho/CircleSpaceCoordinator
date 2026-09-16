namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Desktop.Core.Interaction;
using Microsoft.Xna.Framework;
using StationeryUI.Canvas;

public sealed partial class VenueEditorGame
{
    private CircleSpaceProject? capacityProject;
    private SpaceCapacitySummary? capacitySummary;

    private ScreenRectangle SpaceCapacityBounds => new(NextSpaceBounds.X,
        NextSpaceBounds.Y + NextSpaceBounds.Height + 8, NextSpaceBounds.Width, 100);

    private void DrawSpaceCapacity()
    {
        if (workspace is null || SelectedDisplayedLayoutId is not { } layoutId) return;
        var bounds = SpaceCapacityBounds;
        var summary = GetSpaceCapacity();
        var capacity = summary.Layouts[layoutId];
        var difference = capacity - summary.Requested;
        DrawRectangle(bounds, new Color(24, 34, 44));
        void Text(string text, int offset, Color color) => textRenderer?.Draw(text,
            ToRectangle(new ScreenRectangle(bounds.X + 8, bounds.Y + offset, bounds.Width - 16, 26)), color, 17, true);
        Text($"配置可能：{capacity:N0} sp", 8, Color.White);
        Text($"要求：{summary.Requested:N0} sp", 36, Color.White);
        Text(difference == 0 ? "一致" : difference < 0 ? $"不足：{-difference:N0} sp" : $"超過：{difference:N0} sp",
            64, difference == 0 ? Color.LightGreen : Color.Orange);
    }

    private SpaceCapacitySummary GetSpaceCapacity()
    {
        if (capacitySummary is null || !ReferenceEquals(capacityProject, workspace!.Project))
        {
            capacityProject = workspace!.Project;
            capacitySummary = SpaceCapacitySummary.Calculate(capacityProject);
        }
        return capacitySummary;
    }

    private string DescribeSpaceCapacity(string layoutId)
    {
        var summary = GetSpaceCapacity();
        var capacity = summary.Layouts[layoutId];
        var difference = capacity - summary.Requested;
        var comparison = difference == 0 ? "一致" : difference < 0 ? $"不足 {-difference:N0} sp" : $"超過 {difference:N0} sp";
        return $"配置可能：{capacity:N0} sp ／ 申込：{summary.Requested:N0} sp（{comparison}）";
    }

    private void DrawErrorMark(ScreenRectangle mark)
    {
        DrawRectangle(mark, new Color(195, 35, 48));
        DrawOutline(mark, 1, new Color(255, 112, 120));
        DrawRectangle(new ScreenRectangle(mark.X + 9, mark.Y + 4, 2, 8), Color.White);
        DrawRectangle(new ScreenRectangle(mark.X + 9, mark.Y + 14, 2, 2), Color.White);
    }
}
