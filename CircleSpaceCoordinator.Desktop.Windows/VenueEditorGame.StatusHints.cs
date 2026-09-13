namespace CircleSpaceCoordinator.Desktop.Windows;

public sealed partial class VenueEditorGame
{
    private const double StatusHintDurationSeconds = 5;
    private static readonly string[] FrameNumberHints =
    [
        "Ctrl キーを押しながらマウスドラッグで複数フレーム選択",
        "Ctrl を離して選択中のフレームをクリック：フレーム番号を一括入力",
        "番号入力を空欄で確定：一括削除　Ctrl＋Z：元に戻す　Ctrl＋Y：やり直し",
    ];
    private static readonly string[] FramePlacementHints =
    [
        "［フレーム番号］を選び、Ctrl キーを押しながらマウスドラッグで複数フレーム選択",
        "［フレーム番号］で複数選択後、Ctrl を離して選択中のフレームをクリック：一括入力",
        "Ctrl＋Z：元に戻す　Ctrl＋Y：やり直し　Ctrl＋S：保存",
    ];
    private bool ShowsStatusHintTimer => workspace is not null && editorMode == EditorMode.DeskPlacement;
    private double statusHintTime;
    private double statusHintStartedAt;
    private string? statusHintContext;
    private string[] statusHintPages = [];
    private int statusHintPageIndex;
    private double StatusHintProgress => Math.Clamp(
        (statusHintTime - statusHintStartedAt) / StatusHintDurationSeconds, 0, 1);

    private string GetRotatingFrameNumberHint(IReadOnlyList<string> details)
    {
        var hints = IsFrameNumberChannelSelected ? FrameNumberHints : FramePlacementHints;
        if (statusHintContext is null)
        {
            statusHintContext = "frame-number";
            statusHintStartedAt = statusHintTime;
            statusHintPages = hints.Concat(details).ToArray();
            statusHintPageIndex = 0;
        }
        else if (statusHintTime - statusHintStartedAt >= StatusHintDurationSeconds)
        {
            // Refresh changing hover/status messages only at the page boundary,
            // so moving the pointer cannot continually restart the countdown.
            var previousMessage = statusHintPages[statusHintPageIndex];
            statusHintPages = hints.Concat(details).ToArray();
            var previousIndex = Array.IndexOf(statusHintPages, previousMessage);
            statusHintPageIndex = (previousIndex + 1) % statusHintPages.Length;
            statusHintStartedAt = statusHintTime;
        }
        return $"({statusHintPageIndex + 1}/{statusHintPages.Length})　{statusHintPages[statusHintPageIndex]}";
    }
}
