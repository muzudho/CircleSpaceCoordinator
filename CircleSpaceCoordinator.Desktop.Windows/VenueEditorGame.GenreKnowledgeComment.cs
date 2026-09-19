namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Core.Model;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StationeryUI.Canvas;
using StationeryUI.MonoGame.Controls.ActionBadge;

public sealed partial class VenueEditorGame
{
    private ScreenRectangle MappingTableNameBounds => MappingBounds(20, 62, 460, 36);
    private ScreenRectangle MappingOverallCommentBounds => MappingBounds(492, 62, 488, 36);

    private void OpenMappingOverallComment()
    {
        if (mappingDraft is not { } draft) return;
        SetMappingTextFocus(false);
        OpenUnderlineInput("全体コメント", draft.OverallComment ?? "", value =>
        {
            draft.SetOverallComment(value);
            mappingWidth = -1;
        }, "対応表全体の傾向や補足を1000文字以内で入力してください。\n空欄で確定すると削除します。キャンセルすると元のコメントを残します。",
            maxLength: int.MaxValue, allowEmpty: true, validate: ValidateGenreComment);
    }

    private static string? ValidateGenreComment(string value)
    {
        try { GenreStyleDefinition.NormalizeKnowledgeComment(value); return null; }
        catch (ArgumentException ex) { return ex.Message; }
    }
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
            maxLength: int.MaxValue, allowEmpty: true, validate: ValidateGenreComment);
    }

    private void DrawGenreKnowledgeComment(string? comment, ScreenRectangle bounds, string placeholder = "コメントを入力")
    {
        var size = Math.Max(10, (int)(14 * MappingEditorScale));
        var badge = ActionBadgeComponent.Create("EDIT", new Rectangle(0, 0,
            (int)(bounds.Width / MappingEditorScale), (int)(bounds.Height / MappingEditorScale)));
        var mouse = Mouse.GetState();
        var hovered = mappingPickerColumn == 0 && CanShowEditorHover && Contains(bounds, new(mouse.X, mouse.Y));
        if (hovered) badge.Show();
        var value = string.IsNullOrEmpty(comment) ? placeholder : comment;
        var available = Math.Max(1, badge.Bounds.X * MappingEditorScale - 12);
        var suffix = (textRenderer?.Measure(value, size).X ?? 0) > available ? "［］" : "";
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
            hovered ? 2 : 1, new Color(99, 223, 185));
        DrawMappingBadge(badge, bounds);
    }
}
