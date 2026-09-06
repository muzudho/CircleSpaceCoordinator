namespace CircleSpaceCoordinator.Desktop.Windows;

internal sealed record SeatNameEdit(string BlockName, string SeatName, bool Remove);

internal static class SeatNameDialog
{
    public static SeatNameEdit? Show(string? initialBlockName, string? initialSeatName)
    {
        using var form = new System.Windows.Forms.Form
        {
            Text = "席名を変更",
            Width = 460,
            Height = 225,
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen,
            ShowInTaskbar = false,
        };
        using var blockLabel = new System.Windows.Forms.Label { Text = "ブロック名", Left = 14, Top = 15, AutoSize = true };
        using var block = new System.Windows.Forms.TextBox { Text = initialBlockName ?? "", Left = 14, Top = 38, Width = 416, MaxLength = 40 };
        using var seatLabel = new System.Windows.Forms.Label { Text = "席名", Left = 14, Top = 71, AutoSize = true };
        using var seat = new System.Windows.Forms.TextBox { Text = initialSeatName ?? "", Left = 14, Top = 94, Width = 416, MaxLength = 80 };
        using var remove = new System.Windows.Forms.Button { Text = "席名を削除", Left = 14, Top = 137, Width = 94, Enabled = initialBlockName is not null };
        using var ok = new System.Windows.Forms.Button { Text = "OK", Left = 274, Top = 137, Width = 75 };
        using var cancel = new System.Windows.Forms.Button { Text = "キャンセル", DialogResult = System.Windows.Forms.DialogResult.Cancel, Left = 355, Top = 137, Width = 75 };
        SeatNameEdit? result = null;

        ok.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(block.Text) || string.IsNullOrWhiteSpace(seat.Text))
            {
                System.Windows.Forms.MessageBox.Show(form, "ブロック名と席名を両方入力してください。", "席名を変更",
                    System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                return;
            }
            result = new SeatNameEdit(block.Text.Trim(), seat.Text.Trim(), Remove: false);
            form.DialogResult = System.Windows.Forms.DialogResult.OK;
            form.Close();
        };
        remove.Click += (_, _) =>
        {
            result = new SeatNameEdit("", "", Remove: true);
            form.DialogResult = System.Windows.Forms.DialogResult.OK;
            form.Close();
        };
        form.Controls.AddRange([blockLabel, block, seatLabel, seat, remove, ok, cancel]);
        form.CancelButton = cancel;
        form.Shown += (_, _) =>
        {
            block.Focus();
            block.SelectAll();
        };
        return form.ShowDialog() == System.Windows.Forms.DialogResult.OK ? result : null;
    }
}
