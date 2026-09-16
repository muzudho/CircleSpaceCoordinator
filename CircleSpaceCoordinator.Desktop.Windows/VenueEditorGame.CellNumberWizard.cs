namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Desktop.Core.Interaction;
using Microsoft.Xna.Framework;
using StationeryUI.Canvas;
using StationeryUI.Controls;

public sealed partial class VenueEditorGame
{
    private bool ShowsCellNumberWizardButton => editorMode == EditorMode.DeskPlacement &&
        selectedChannelId is null && selectedNumberChannel == 2;
    private bool ShowsChannelFooterButton => ShowsBlockStyleButton || ShowsCellNumberWizardButton;

    private void DrawCellNumberWizardButton()
    {
        var button = new IconButtonModel(BlockStyleButton(), "セル番号入力ウィザード");
        button.UpdatePointer(CanShowEditorHover ? new ScreenPoint(previousMouse.X, previousMouse.Y) : new(-1, -1));
        StationeryButtonRenderer.Draw(button,
            (area, color) => DrawRectangle(area, ToButtonColor(color)),
            (area, width, color) => DrawOutline(area, width, ToButtonColor(color)),
            (area, color) =>
            {
                var x = area.X + area.Width / 2;
                var y = area.Y + area.Height / 2;
                DrawLine(new(x - 12, y + 12), new(x + 6, y - 6), 4, ToButtonColor(color));
                var gold = new Color(255, 215, 90);
                DrawLine(new(x + 8, y - 15), new(x + 8, y - 3), 2, gold);
                DrawLine(new(x + 2, y - 9), new(x + 14, y - 9), 2, gold);
                DrawLine(new(x - 9, y - 13), new(x - 9, y - 7), 2, gold);
                DrawLine(new(x - 12, y - 10), new(x - 6, y - 10), 2, gold);
            });
    }

    private void OpenCellNumberWizard()
    {
        if (workspace is not { } owner || commandController is not { } commands) return;
        var plan = owner.SelectedPlan;
        var sourceProject = owner.Project;
        var range = selectedCellRange;
        var draft = new CellNumberWizard(sourceProject, plan, range is { } selected ? selected.Contains : null);
        if (draft.Count == 0) { ShowInAppMessage("セル番号入力ウィザード", "対象の配置可能セルがありません。"); return; }
        var numbers = draft.DefaultNumbers;
        var order = CellNumberOrder.VenueTopLeft;
        var scope = range is null ? "現在のフレーム配置全体" : "選択したセル範囲";
        void ChooseOrder() => OpenSelection($"セル番号入力ウィザード 1/3：並び順（{draft.Count}セル）",
            ["会場の左上を先頭とする（左→右、上→下）", "セルが上向きのときの左上を先頭とする（フレームごと）"],
            (int)order, index => { order = (CellNumberOrder)index; EditNumbers(); });
        void EditNumbers() => OpenUnderlineInput("セル番号入力ウィザード 2/3：番号リスト", numbers, value =>
        {
            draft.ParseNumbers(value);
            numbers = value;
            Preview();
        }, $"対象：{scope}、{draft.Count}セル。カンマ区切りで{draft.Count}個指定します。\n例：1, 2, 3 ／ 01, 02, 03 ／ A, B, C\nフレーム順は会場の上→下・左→右。番号は途中でリセットしません。",
            Math.Max(4096, draft.Count * 82), cancelled: ChooseOrder);
        void Preview()
        {
            var values = draft.ParseNumbers(numbers);
            var targets = draft.Targets(order);
            var lines = targets.Take(6).Select((target, index) =>
                $"セル ({target.Cell.X}, {target.Cell.Y}) → {values[index]}");
            var message = $"対象：{scope}、{draft.Count}セル\n並び順：{(order == CellNumberOrder.VenueTopLeft ? "会場の左上" : "フレーム内の左上（上向き基準）")}\n既存のセル番号を置き換えます。ブロック番号・フレーム番号は保持します。\n\n" +
                string.Join("\n", lines) + (draft.Count > 6 ? $"\nほか{draft.Count - 6}セル" : "") + "\n\n適用後は1回のUndoで戻せます。";
            OpenModal(new ModalDialogModel(ModalDialogKind.Confirmation, "セル番号入力ウィザード 3/3：確認", message), action =>
            {
                if (action != ModalDialogAction.Accept) { EditNumbers(); return; }
                try
                {
                    if (workspace != owner || owner.SelectedPlanId != plan.Id || !ReferenceEquals(owner.Project, sourceProject))
                        throw new InvalidOperationException("編集対象が変わりました。ウィザードを開き直してください。");
                    var result = commands.ReplaceSeatLabels(draft.Build(order, numbers));
                    if (!result.Applied)
                        throw new InvalidOperationException("セル番号を変更できませんでした。\n" + FormatIssues(result.Issues));
                    rangeSwapStatus = $"{draft.Count}セルの番号を一括入力しました";
                }
                catch (Exception exception) { ShowNotice("セル番号入力ウィザード", exception.Message, EditNumbers); }
            }, [("戻る", ModalDialogAction.Cancel), ("適用", ModalDialogAction.Accept)]);
        }
        ChooseOrder();
    }
}
