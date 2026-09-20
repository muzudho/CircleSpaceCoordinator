namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Desktop.Core.Interaction;
using Microsoft.Xna.Framework;
using StationeryUI.Canvas;

public sealed partial class VenueEditorGame
{
    private void DrawShadingRows(IReadOnlyList<StyleMappingEntry> rows, int scroll, bool right)
    {
        var order = right ? packageGenreTable?.Metadata().RowOrder.ToArray() ?? [] : mappingGenreCodeOrder;
        void Text(string text, ScreenRectangle bounds, int size = 17, Color? color = null) =>
            textRenderer?.Draw(text, ToRectangle(bounds, 3), color ?? Color.White, Math.Max(10, (int)(size * MappingEditorScale)), true);
        for (var row = 0; row < MappingVisibleRows && scroll + row < rows.Count; row++)
        {
            var style = rows[scroll + row];
            for (var column = 0; column < (mappingKnowledgeComments ? 7 : 5); column++)
            {
                var bounds = MappingCell(row, column, right);
                if (mappingKnowledgeComments && column == 3 && style.Pattern == "solid")
                    continue;
                var plainCell = mappingKnowledgeComments && column is 1 or 6;
                if (!plainCell) DrawRectangle(bounds, new Color(35, 43, 54));
                if (column == 0)
                {
                    var orderIndex = Array.IndexOf(order, style.Key);
                    var value = (orderIndex >= 0 ? orderIndex + 1 : scroll + row + 1).ToString();
                    var size = Math.Max(10, (int)(17 * MappingEditorScale));
                    var measured = textRenderer?.Measure(value, size).X ?? 0;
                    textRenderer?.Draw(value, ToRectangle(new(bounds.X + bounds.Width - measured - 8, bounds.Y, measured + 4, bounds.Height), 3), Color.White, size, true);
                }
                else if (column == 1) Text(style.Key, bounds);
                else if (column == 6) Text(style.KnowledgeComment ?? "コメントを入力", bounds, 14);
                else if (column is 2 or 3)
                {
                    if (column == 3 && style.Pattern == "solid")
                        continue;
                    else
                    {
                        var id = column == 2 ? style.PrimaryColor : style.SecondaryColor;
                        var color = GenreColorFromId(id, Color.Gray);
                        DrawRectangle(bounds, color);
                        Text(StyleMappingDraft.Colors.FirstOrDefault(choice => choice.Id == id).Label ?? id, bounds, 17, MappingColorText(color));
                    }
                }
                else
                {
                    var swatch = new ScreenRectangle(bounds.X + 4, bounds.Y + 3, bounds.Width - 8,
                        column == 4 && !mappingKnowledgeComments ? bounds.Height * 0.54 : bounds.Height - 6);
                    DrawRectangle(swatch, column == 4 ? Color.Black : GenreColorFromId(style.PrimaryColor, Color.Gray));
                    DrawGenrePattern(swatch, GenrePatternFromId(style.Pattern), column == 4 ? Color.White : GenreColorFromId(style.SecondaryColor, Color.White), 255);
                    if (column == 4 && !mappingKnowledgeComments) Text(StyleMappingDraft.Patterns.FirstOrDefault(choice => choice.Id == style.Pattern).Label ?? style.Pattern,
                        new(bounds.X, bounds.Y + bounds.Height * 0.58, bounds.Width, bounds.Height * 0.4), 12);
                }
                if (!plainCell) DrawOutline(bounds, 1, new Color(100, 119, 130));
                if (!GenreGridVisible && mappingPickerColumn == 0 && mappingFocus < 0 && selectedGenreKey is null && selectedPackageGenreKey is null && mappingRow == scroll + row && mappingColumn == column)
                {
                    if (plainCell)
                        DrawLine(new(bounds.X + 6, bounds.Y + bounds.Height - 7), new(bounds.X + bounds.Width - 6, bounds.Y + bounds.Height - 7), 2, OperationTargetColor);
                    else DrawOutline(bounds, 2, OperationTargetColor);
                }
            }
            if (mappingKnowledgeComments && ShadingRowSelected(right, style.Key))
            {
                DrawOutline(GenreTargetRowBounds(row, right), 2 * MappingEditorScale, OperationTargetColor);
                DrawGenreCellHover(row, style.Pattern, right);
            }
        }
    }
}
