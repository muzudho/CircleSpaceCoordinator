namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Core.Model;

internal sealed record ExportPlanChoice(string? Id, string Label)
{
    public override string ToString() => Label;
}

internal static class ExportPlanDialog
{
    // Null result means cancelled; an item with null ID explicitly clears the decision.
    public static ExportPlanChoice? Show(CircleSpaceProject project)
    {
        using var form = new System.Windows.Forms.Form
        {
            Text = "配置決定案を選択する", ClientSize = new System.Drawing.Size(520, 170),
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false,
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen,
        };
        using var note = new System.Windows.Forms.Label
        {
            Text = "Excel に席番地を書き出す配置案を選択してください。\n作業中の配置案を切り替えても、この選択は変わりません。",
            Left = 16, Top = 16, Width = 488, Height = 42,
        };
        using var choices = new System.Windows.Forms.ComboBox
        {
            Left = 16, Top = 65, Width = 488, DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList,
        };
        choices.Items.Add(new ExportPlanChoice(null, "配置案　未決定"));
        foreach (var plan in project.Plans)
            choices.Items.Add(new ExportPlanChoice(plan.Id, $"{plan.Name}（{plan.Id}）"));
        choices.SelectedIndex = Math.Max(0, choices.Items.Cast<ExportPlanChoice>().ToList().FindIndex(item => item.Id == project.ExportPlanId));
        using var ok = new System.Windows.Forms.Button
        {
            Text = "決定", Left = 304, Top = 120, Width = 90, DialogResult = System.Windows.Forms.DialogResult.OK,
        };
        using var cancel = new System.Windows.Forms.Button
        {
            Text = "キャンセル", Left = 404, Top = 120, Width = 100, DialogResult = System.Windows.Forms.DialogResult.Cancel,
        };
        form.Controls.AddRange([note, choices, ok, cancel]);
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        return form.ShowDialog() == System.Windows.Forms.DialogResult.OK ? (ExportPlanChoice?)choices.SelectedItem : null;
    }
}
