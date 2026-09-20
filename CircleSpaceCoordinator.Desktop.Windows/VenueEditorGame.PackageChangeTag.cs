namespace CircleSpaceCoordinator.Desktop.Windows;

using Microsoft.Xna.Framework;
using StationeryUI.Canvas;
using StationeryUI.Controls;

public sealed partial class VenueEditorGame
{
    private ChangeTagEditor? packageChangeTag;
    private readonly ChangeTagEditorView packageTagView = new()
    {
        Placeholder = "パッケージへの変更コメントを入力してください",
    };
    private bool GenreTagsSplit => mappingKnowledgeComments && (GenreGridSplit || packageGenreTable is not null);
    private double ProjectTagActionWidth => mappingKnowledgeComments ? 16 : mappingChangeTag?.HasChanges == true ? 240 : 128;
    private ScreenRectangle ProjectTagBounds => mappingKnowledgeComments ? GenreTagBounds(false)
        : MappingBounds(20, 502, mappingChangeTag?.HasChanges == true ? 936 : 824, 98);
    private ScreenRectangle GenreTagBounds(bool right)
    {
        var vertical = MappingBounds(20, 502, 960, 98);
        var scale = MappingEditorScale;
        var margin = 20 * scale;
        var gap = 16 * scale;
        var total = Math.Max(1, GraphicsDevice.Viewport.Width - 2 * margin);
        var width = GenreTagsSplit ? (total - gap) / 2 : total;
        return new(margin + (right ? width + gap : 0), vertical.Y, width, vertical.Height);
    }
    private ScreenRectangle GenreTagButtonBounds(bool right, bool edit = false)
    {
        var tag = GenreTagBounds(right);
        var scale = MappingEditorScale;
        var width = Math.Min((edit ? 140 : 310) * scale, tag.Width * (edit ? .28 : .68));
        return new(edit ? tag.X + tag.Width - width : tag.X, MappingBounds(20, 644, 960, 30).Y, width, 30 * scale);
    }
    private ScreenRectangle PackageLogBounds => ChangeTagEditorView.GetInputBounds(GenreTagBounds(true), MappingEditorScale, 16);
    private bool MappingCommentsCanClose => mappingChangeTag?.CanClose == true &&
        (!mappingKnowledgeComments || packageChangeTag?.CanClose != false);

    private void OpenPackageChangeComment()
        => OpenGenreChangeComment(project: false);

    private void OpenProjectChangeComment()
        => OpenGenreChangeComment(project: true);

    private void OpenGenreChangeComment(bool project)
    {
        if ((project ? mappingChangeTag : packageChangeTag) is not { HasChanges: true } tag) return;
        SetMappingTextFocus(false);
        OpenUnderlineInput(project ? "プロジェクトへの変更コメント" : "パッケージへの変更コメント", tag.Text, value =>
        {
            tag.Editor.SelectAll();
            tag.Editor.Delete(false);
            tag.Insert(value);
            mappingWidth = -1;
        }, project ? "左側のジャンルコード表に対する変更内容を入力してください。パッケージ側のコメントとは別に保存します。"
            : "右側のジャンルコード表に対する変更内容を入力してください。プロジェクト側のコメントとは別に保存します。", 1000,
            validate: ValidateMappingChangeLog);
    }

    private void DrawPackageChangeTag()
    {
        if (packageGenreTable is null || packageChangeTag is not { } tag) return;
        var size = Math.Max(10, (int)(ChangeTagEditorView.InputFontSize * MappingEditorScale));
        packageTagView.Draw(tag, GenreTagBounds(true), MappingEditorScale, false, false,
            "パッケージへの変更コメント　" + (packageGenreSaveSession?.OpeningTable.Credits?.AttributionText ?? "変更者・日付不明"), "",
            value => textRenderer?.Measure(value, size).X ?? 0,
            (value, area, fontSize, ink) => textRenderer?.Draw(value, ToRectangle(area), MappingInk(ink), Math.Max(10, (int)(fontSize * MappingEditorScale))),
            (area, ink) => DrawRectangle(area, MappingInk(ink)),
            previousLog: packageGenreSaveSession?.OpeningTable.Credits?.ChangeLog, actionAreaWidth: 16);
    }
}
