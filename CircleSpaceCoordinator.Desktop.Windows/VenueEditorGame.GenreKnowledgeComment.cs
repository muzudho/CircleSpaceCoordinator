namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Core.Model;
using Microsoft.Xna.Framework;
using StationeryUI.Canvas;

public sealed partial class VenueEditorGame
{
    private void OpenGenreKnowledgeComment(int row)
    {
        if (mappingDraft is not { } draft || row < 0 || row >= draft.Rows.Count) return;
        mappingRow = row;
        mappingColumn = 5;
        mappingFocus = -1;
        SetMappingTextFocus(false);
        var style = draft.Rows[row];
        OpenUnderlineInput($"{style.Key} の知見コメント", style.KnowledgeComment ?? "", value =>
        {
            draft.SetKnowledgeComment(row, value);
            mappingWidth = -1;
        }, "ジャンルの範囲や補足を1000文字以内で入力してください。例：アクションRPGを含む\n空欄で確定するとコメントを削除します。キャンセルすると元のコメントを残します。",
            maxLength: int.MaxValue, allowEmpty: true, validate: value =>
            {
                try { GenreStyleDefinition.NormalizeKnowledgeComment(value); return null; }
                catch (ArgumentException ex) { return ex.Message; }
            });
    }

    private void DrawGenreKnowledgeComment(string? comment, ScreenRectangle bounds)
    {
        var size = Math.Max(10, (int)(14 * MappingEditorScale));
        const string suffix = "［］";
        var value = string.IsNullOrEmpty(comment) ? "コメントを入力" : comment;
        var available = Math.Max(1, bounds.Width - 12);
        // Truncate the preview, never the stored text or a Unicode text element.
        var boundaries = System.Globalization.StringInfo.ParseCombiningCharacters(value).Append(value.Length).ToArray();
        var lo = 0;
        var hi = boundaries.Length - 1;
        while (lo < hi)
        {
            var mid = (lo + hi + 1) / 2;
            if ((textRenderer?.Measure(value[..boundaries[mid]] + suffix, size).X ?? 0) <= available) lo = mid;
            else hi = mid - 1;
        }
        textRenderer?.Draw(value[..boundaries[lo]] + suffix, ToRectangle(new(bounds.X + 6, bounds.Y + 3, available, bounds.Height - 9)),
            string.IsNullOrEmpty(comment) ? Color.LightGray : Color.White, size);
        DrawLine(new(bounds.X + 6, bounds.Y + bounds.Height - 7), new(bounds.X + bounds.Width - 6, bounds.Y + bounds.Height - 7),
            1, new Color(99, 223, 185));
    }
}
