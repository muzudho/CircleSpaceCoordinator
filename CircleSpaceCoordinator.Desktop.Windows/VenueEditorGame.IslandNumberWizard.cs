namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Core.Evaluation;
using StationeryUI.Controls;

public sealed partial class VenueEditorGame
{
    private void OpenIslandFrameNumberWizard()
    {
        if (workspace is not { } owner || commandController is not { } commands) return;
        var source = owner.Project;
        var plan = owner.SelectedPlan;
        var graph = owner.View.Topology;
        var frameIds = selectedFrameIds.Count > 0 ? selectedFrameIds.ToHashSet(StringComparer.Ordinal) :
            selectedCellRange is { } range ? graph.DeskIdByCell.Where(pair => range.Contains(pair.Key))
                .Select(pair => pair.Value).ToHashSet(StringComparer.Ordinal) : null;
        IReadOnlyList<string> frames;
        try
        {
            frames = IslandNumbering.OrderedFrames(plan, graph, frameIds);
            if (frames.Count == 0) throw new InvalidOperationException("対象のフレームがありません。");
        }
        catch (InvalidOperationException exception) { ShowInAppMessage("自動連番できません", exception.Message); return; }
        var first = "1";
        void Edit() => OpenUnderlineInput("旗からフレーム番号を自動連番：開始番号", first, text =>
        {
            if (!int.TryParse(text, out var number) || number < 0 || (long)number + frames.Count - 1 > int.MaxValue)
                throw new ArgumentException("0以上で、最後の番号が2147483647以下になる整数を入力してください。");
            first = text;
            var byId = plan.DeskPlacements.ToDictionary(frame => frame.Id);
            var lines = frames.Take(8).Select((id, index) =>
                $"フレーム ({byId[id].Anchor.X}, {byId[id].Anchor.Y})：{byId[id].DeskNumber ?? "未入力"} → {number + index}");
            OpenModal(new ModalDialogModel(ModalDialogKind.Confirmation, "フレーム番号の自動連番：確認",
                $"対象：{frames.Count}フレーム。旗からの順番で {number}～{number + frames.Count - 1} を入力します。\n" +
                "1フレームに1番号を付けます。複数の旗は会場の上→左の順に処理し、通し番号にします。\n" +
                "対象の既存フレーム番号を置き換えます。対象外・ブロック番号・セル番号は保持します。\n\n" +
                string.Join("\n", lines) + (frames.Count > 8 ? $"\nほか{frames.Count - 8}フレーム" : "") + "\n\n1回のUndoで戻せます。"), action =>
            {
                if (action != ModalDialogAction.Accept) { Edit(); return; }
                try
                {
                    if (workspace != owner || owner.SelectedPlanId != plan.Id || !ReferenceEquals(owner.Project, source))
                        throw new InvalidOperationException("編集対象が変わりました。ウィザードを開き直してください。");
                    var result = commands.NumberFramesFromIslands(number, frameIds);
                    if (!result.Applied) throw new InvalidOperationException("番号を変更できませんでした。\n" + FormatIssues(result.Issues));
                    rangeSwapStatus = $"{frames.Count}フレームに旗からの順番で連番を入力しました";
                }
                catch (Exception exception) { ShowInAppMessage("自動連番できません", exception.Message); }
            }, [("戻る", ModalDialogAction.Cancel), ("適用", ModalDialogAction.Accept)]);
        }, "選択があればそのフレーム、なければ旗から到達する全フレームが対象です。\n旗のセルから入口方向へたどり、最初に現れるフレーム順に番号を付けます。", 10);
        Edit();
    }
}
