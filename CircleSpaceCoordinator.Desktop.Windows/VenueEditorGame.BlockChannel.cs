namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Desktop.Core;
using CircleSpaceCoordinator.Desktop.Core.Interaction;
using Microsoft.Xna.Framework;
using StationeryUI.Canvas;

public sealed partial class VenueEditorGame
{
    private bool IsBlockNumberChannelSelected => editorMode == EditorMode.DeskPlacement &&
        !IsWeightChannelSelected && selectedNumberChannel == 0;
    private CircleSpaceProject? blockViewProject;
    private Plan? blockViewPlan;
    private BlockChannelView? blockView;
    private Dictionary<string, GenreVisualStyle> blockVisualStyles = [];

    private BlockChannelView GetBlockChannelView()
    {
        var project = workspace!.Project;
        var plan = workspace.SelectedPlan;
        if (blockView is null || !ReferenceEquals(project, blockViewProject) || !ReferenceEquals(plan, blockViewPlan))
        {
            blockViewProject = project;
            blockViewPlan = plan;
            blockView = BlockChannelView.Create(project, plan);
            blockVisualStyles = new BlockStyleDraft(project).Mapping.Rows.ToDictionary(style => style.Key,
                style => new GenreVisualStyle(GenreColorFromId(style.PrimaryColor, Color.Gray),
                    GenreColorFromId(style.SecondaryColor, Color.White), GenrePatternFromId(style.Pattern)));
        }
        return blockView;
    }

    private void DrawBlockBackgrounds()
    {
        if (!IsBlockNumberChannelSelected || workspace is null) return;
        foreach (var (cell, number) in GetBlockChannelView().Cells)
        {
            var bounds = viewport.GetCellBounds(VenueCanvasMapper.ToCanvasCell(cell));
            if (string.IsNullOrWhiteSpace(number))
                DrawMissingNumber(new ScreenRectangle(bounds.X + 2, bounds.Y + 2,
                    Math.Max(1, bounds.Width - 4), Math.Max(1, bounds.Height - 4)));
            else if (blockVisualStyles.TryGetValue(number, out var style))
            {
                DrawRectangle(bounds, style.Primary);
                DrawGenrePattern(bounds, style.Pattern, style.Secondary, 255);
                DrawOutline(bounds, 1, CanvasGridColor);
            }
        }
    }

    private void DrawBlockNumberLabels()
    {
        if (workspace is null) return;
        foreach (var (cell, number) in GetBlockChannelView().Labels)
        {
            var bounds = viewport.GetCellBounds(VenueCanvasMapper.ToCanvasCell(cell));
            // A neutral backing keeps text readable on every two-colour pattern.
            var labelBounds = new ScreenRectangle(bounds.X + 3, bounds.Y + bounds.Height * 0.25,
                Math.Max(1, bounds.Width - 6), bounds.Height * 0.5);
            DrawRectangle(labelBounds, new Color(20, 25, 32, 220));
            textRenderer?.Draw(number, ToRectangle(labelBounds, 1), Color.White, VenueTextSize(14), true);
        }
    }
}
