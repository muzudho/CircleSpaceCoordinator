namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Core.Model;

internal sealed record PlanCopyRequest(string SourcePlanId, string DestinationPlanId);

internal static class PlanCopyDialog
{
    public static PlanCopyRequest? Show(IReadOnlyList<Plan> plans)
    {
        if (plans.Count < 2)
            return null;

        using var form = new System.Windows.Forms.Form
        {
            Text = "色んなコピー",
            Width = 520,
            Height = 265,
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen,
            ShowInTaskbar = false,
        };
        var items = plans.Select(plan => new PlanItem(plan.Id, plan.Name)).ToArray();
        using var sourceLabel = new System.Windows.Forms.Label { Text = "コピー元", Left = 14, Top = 16, AutoSize = true };
        using var source = new System.Windows.Forms.ComboBox { Left = 14, Top = 38, Width = 472, DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList };
        using var destinationLabel = new System.Windows.Forms.Label { Text = "コピー先", Left = 14, Top = 74, AutoSize = true };
        using var destination = new System.Windows.Forms.ComboBox { Left = 14, Top = 96, Width = 472, DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList };
        source.Items.AddRange(items);
        destination.Items.AddRange(items);
        source.SelectedIndex = 0;
        destination.SelectedIndex = 1;
        using var methodLabel = new System.Windows.Forms.Label { Text = "コピー方法", Left = 14, Top = 132, AutoSize = true };
        using var method = new System.Windows.Forms.ComboBox { Left = 14, Top = 154, Width = 220, DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList };
        method.Items.Add("スペース配置だけコピー");
        method.SelectedIndex = 0;
        using var note = new System.Windows.Forms.Label
        {
            Text = "スペース・席名・島定義をコピーします。コピー先のサークル配置は維持します。\n新しいスペース配置に収まらない場合は、コピーせずに中止します。",
            Left = 14,
            Top = 185,
            Width = 472,
            Height = 38,
        };
        using var ok = new System.Windows.Forms.Button { Text = "コピー", Left = 330, Top = 185, Width = 75 };
        using var cancel = new System.Windows.Forms.Button { Text = "キャンセル", DialogResult = System.Windows.Forms.DialogResult.Cancel, Left = 411, Top = 185, Width = 75 };
        PlanCopyRequest? result = null;
        ok.Click += (_, _) =>
        {
            var sourceItem = source.SelectedItem as PlanItem;
            var destinationItem = destination.SelectedItem as PlanItem;
            if (sourceItem is null || destinationItem is null)
                return;
            if (sourceItem.Id == destinationItem.Id)
            {
                System.Windows.Forms.MessageBox.Show(form, "コピー元とコピー先には別の配置案を選んでください。", "色んなコピー",
                    System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                return;
            }
            result = new PlanCopyRequest(sourceItem.Id, destinationItem.Id);
            form.DialogResult = System.Windows.Forms.DialogResult.OK;
            form.Close();
        };
        form.Controls.AddRange([sourceLabel, source, destinationLabel, destination, methodLabel, method, note, ok, cancel]);
        form.CancelButton = cancel;
        return form.ShowDialog() == System.Windows.Forms.DialogResult.OK ? result : null;
    }

    private sealed record PlanItem(string Id, string Name)
    {
        public override string ToString() => Name;
    }
}
