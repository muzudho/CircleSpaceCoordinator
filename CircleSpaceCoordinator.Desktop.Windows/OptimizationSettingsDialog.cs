namespace CircleSpaceCoordinator.Desktop.Windows;

internal static class OptimizationSettingsDialog
{
    public static TimeSpan? Show()
    {
        using var form = new System.Windows.Forms.Form
        {
            Text = "自動最適化の設定",
            Width = 410,
            Height = 180,
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen,
            ShowInTaskbar = false,
        };
        using var description = new System.Windows.Forms.Label { Text = "最適化を続ける時間を指定してください。\n途中で［ストップ］を押せば、その時点の最高案を採用します。", Left = 14, Top = 14, Width = 360, Height = 40 };
        using var label = new System.Windows.Forms.Label { Text = "最適化時間（分）", Left = 14, Top = 68, AutoSize = true };
        using var minutes = new System.Windows.Forms.NumericUpDown { Left = 130, Top = 64, Width = 85, Minimum = 1, Maximum = 120, Value = 10 };
        using var start = new System.Windows.Forms.Button { Text = "開始", Left = 218, Top = 105, Width = 75 };
        using var cancel = new System.Windows.Forms.Button { Text = "キャンセル", DialogResult = System.Windows.Forms.DialogResult.Cancel, Left = 299, Top = 105, Width = 75 };
        TimeSpan? result = null;
        start.Click += (_, _) => { result = TimeSpan.FromMinutes((double)minutes.Value); form.DialogResult = System.Windows.Forms.DialogResult.OK; form.Close(); };
        form.Controls.AddRange([description, label, minutes, start, cancel]);
        form.AcceptButton = start;
        form.CancelButton = cancel;
        return form.ShowDialog() == System.Windows.Forms.DialogResult.OK ? result : null;
    }
}
