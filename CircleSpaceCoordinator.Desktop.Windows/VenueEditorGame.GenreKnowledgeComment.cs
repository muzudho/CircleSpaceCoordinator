namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Core.Model;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StationeryUI.Canvas;
using StationeryUI.MonoGame.Controls.ActionBadge;

public sealed partial class VenueEditorGame
{
    private ScreenRectangle MappingTableNameBounds => TableHeaderBounds().Name;
    private ScreenRectangle MappingOverallCommentBounds => TableHeaderBounds().Comment;

    private (ScreenRectangle NameLabel, ScreenRectangle Name, ScreenRectangle CommentLabel, ScreenRectangle Comment)
        TableHeaderBounds(bool right = false)
    {
        var row = MappingGridBounds(20, 62, 960, 36, right);
        var size = Math.Max(10, (int)(14 * MappingEditorScale));
        var gap = 12 * MappingEditorScale;
        var half = (row.Width - gap) / 2;
        var nameLabelWidth = (textRenderer?.Measure("表名：", size).X ?? size * 3) + 6;
        var commentLabelWidth = (textRenderer?.Measure("コメント：", size).X ?? size * 5) + 6;
        var commentX = row.X + half + gap;
        return (new(row.X, row.Y, nameLabelWidth, row.Height),
            new(row.X + nameLabelWidth, row.Y, Math.Max(1, half - nameLabelWidth), row.Height),
            new(commentX, row.Y, commentLabelWidth, row.Height),
            new(commentX + commentLabelWidth, row.Y, Math.Max(1, half - commentLabelWidth), row.Height));
    }

    private void DrawTableHeader(string name, string? comment, bool right = false, bool editable = true)
    {
        var bounds = TableHeaderBounds(right);
        var size = Math.Max(10, (int)(14 * MappingEditorScale));
        void Label(string text, ScreenRectangle area) => textRenderer?.Draw(text,
            ToRectangle(new(area.X, area.Y + 3, area.Width, area.Height - 9)), Color.White, size);
        Label("表名：", bounds.NameLabel);
        DrawGenreKnowledgeComment(name, bounds.Name, "表名", editable);
        Label("コメント：", bounds.CommentLabel);
        DrawGenreKnowledgeComment(comment, bounds.Comment, "コメント", editable);
    }

    private void OpenMappingOverallComment()
    {
        if (mappingDraft is not { } draft) return;
        SetMappingTextFocus(false);
        OpenUnderlineInput("コメント", draft.OverallComment ?? "", value =>
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

    private void DrawGenreKnowledgeComment(string? comment, ScreenRectangle bounds, string placeholder = "コメントを入力", bool editable = true)
    {
        var size = Math.Max(10, (int)(14 * MappingEditorScale));
        var badge = ActionBadgeComponent.Create("EDIT", new Rectangle(0, 0,
            (int)(bounds.Width / MappingEditorScale), (int)(bounds.Height / MappingEditorScale)));
        var mouse = Mouse.GetState();
        var hovered = editable && mappingPickerColumn == 0 && CanShowEditorHover && Contains(bounds, new(mouse.X, mouse.Y));
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
