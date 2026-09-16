namespace CircleSpaceCoordinator.Desktop.Windows;

public sealed partial class VenueEditorGame
{
    private const double StatusHintDurationSeconds = 5;
    private static readonly string[] AddressSwapHints =
    [
        "番地スワップ中：Shift＋ドラッグで範囲選択 → 選択範囲をドラッグ",
        "選択中の番地だけ交換。フレーム番号は同数のフレームを会場の上→下・左→右順に対応",
        "ドラッグ中のEsc：取消　ボタンを再度押す：入力モード　Ctrl＋Z：元に戻す",
    ];
    private static readonly string[] BlockNumberHints =
    [
        "Shift＋ドラッグ：セル単位で範囲選択（フレームの一部も選択できます）",
        "Shift を離して選択範囲内をクリック：ブロック番号を一括入力",
        "選択範囲をCtrl＋ドラッグ：番地スワップ　Shift＋ドラッグ：範囲を選び直す",
        "空欄で確定：番号を削除　［色・網掛けの対応］：ブロックの表示設定",
    ];
    private static readonly string[] FrameNumberHints =
    [
        "Shift キーを押しながらマウスドラッグで複数フレーム選択",
        "Shift を離して選択中のフレームをクリック：フレーム番号を一括入力",
        "選択フレームをCtrl＋ドラッグ：番号スワップ　Shift＋ドラッグ：範囲を選び直す",
        "番号入力を空欄で確定：一括削除　Ctrl＋Z：元に戻す　Ctrl＋Y：やり直し",
    ];
    private static readonly string[] FramePlacementHints =
    [
        "［フレーム番号］を選び、Shift キーを押しながらマウスドラッグで複数フレーム選択",
        "［フレーム番号］で複数選択後、Shift を離して選択中のフレームをクリック：一括入力",
        "Ctrl＋Z：元に戻す　Ctrl＋Y：やり直し　Ctrl＋S：保存",
    ];
    private static readonly string[] CellNumberHints =
    [
        "番地入力中：Shift＋ドラッグでセル範囲選択、修飾キーなしのクリックで入力",
        "選択範囲をCtrl＋ドラッグ：番地スワップ　Shift＋ドラッグ：範囲を選び直す",
        "番地入力の隣の矢印ボタン：スワップモード　Ctrl＋Z：元に戻す",
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
        var hints = IsAddressSwapMode ? AddressSwapHints : IsBlockNumberChannelSelected ? BlockNumberHints : IsFrameNumberChannelSelected ? FrameNumberHints : ShowsCellNumberWizardButton ? CellNumberHints : FramePlacementHints;
        var context = IsAddressSwapMode ? "address-swap" : IsBlockNumberChannelSelected ? "block-number" : IsFrameNumberChannelSelected ? "frame-number" : ShowsCellNumberWizardButton ? "cell-number" : "frame-placement";
        if (statusHintContext != context)
        {
            statusHintContext = context;
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
