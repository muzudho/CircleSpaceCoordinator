namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Desktop.Core.Interaction;

public sealed partial class VenueEditorGame
{
    private void OpenCircleRegexSettings()
    {
        var initial = CurrentCircleDisplay;
        var pattern = initial.CircleIdPattern ?? "";
        var replacement = initial.CircleIdReplacement ?? "";
        void ShowDraft() => OpenSelection("サークルIDの正規表現",
            [$"正規表現：{(pattern.Length == 0 ? "（元のIDを表示）" : pattern)}", $"置換文字列：{replacement}", "保存"], 0, index =>
        {
            if (index == 0)
            {
                OpenUnderlineInput("正規表現", pattern, value => { pattern = value; ShowDraft(); },
                "空欄の場合は元のサークルIDを表示します。", 32767, allowEmpty: true, validate: EditorDialogValidation.CircleIdPattern, cancelled: ShowDraft); return;
            }
            if (index == 1)
            {
                OpenUnderlineInput("置換文字列", replacement, value => { replacement = value; ShowDraft(); },
                "例：$1 で最初のグループを参照します。空欄も使用できます。", 32767, allowEmpty: true, cancelled: ShowDraft, trim: false); return;
            }
            SaveCircleDisplay(initial with
            {
                CircleIdPattern = pattern.Length == 0 ? null : pattern,
                CircleIdReplacement = string.IsNullOrWhiteSpace(replacement) ? null : replacement
            });
        });
        ShowDraft();
    }
}
