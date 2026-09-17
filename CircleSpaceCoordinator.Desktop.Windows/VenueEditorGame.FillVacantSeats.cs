namespace CircleSpaceCoordinator.Desktop.Windows;

public sealed partial class VenueEditorGame
{
    private void FillVacantSeats()
    {
        if (workspace is not { HasSelectedCircleLayout: true } owner || optimizationTask is not null) return;
        var before = owner.SelectedPlan.Assignments.Count;
        RunBackground("サークル石を一括配置", () => owner.FillVacantSeatsAsync().GetAwaiter().GetResult(), state =>
        {
            owner.Accept(state);
            var placed = owner.SelectedPlan.Assignments.Count - before;
            var remaining = owner.Project.Participants.Count - owner.SelectedPlan.Assignments.Count;
            ShowInAppMessage("サークル石を一括配置", $"{placed} サークルを配置しました。\n" +
                (remaining == 0 ? "すべてのサークルを配置済みです。" :
                    $"未配置・仮置きは残り {remaining} サークルです。\n必要な空きセルや合体条件を満たせない石は、そのまま残しています。") +
                (placed > 0 ? "\n１回の Undo で一括配置を元に戻せます。" : ""));
        });
    }
}
