namespace CircleSpaceCoordinator.Desktop.Windows;

internal sealed record BulkSeatNameEdit(string? BlockName, string? SeatName);

internal static class BulkSeatNameDialog
{
    public static BulkSeatNameEdit? Show(string? initialBlockName, string? initialSeatName, int targetCount)
    {
        using var form = new System.Windows.Forms.Form
        {
            Text = "席名をまとめて変更",
            Width = 500,
            Height = 260,
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen,
            ShowInTaskbar = false,
        };
        using var explanation = new System.Windows.Forms.Label
        {
            Text = $"選択したフレームのセル {targetCount} 個を変更します。空欄の項目は変更しません。",
            Left = 14,
            Top = 14,
            Width = 452,
            Height = 22,
        };
        using var blockLabel = new System.Windows.Forms.Label { Text = "ブロック名（上書き）", Left = 14, Top = 48, AutoSize = true };
        using var block = new System.Windows.Forms.TextBox { Text = initialBlockName ?? "", Left = 14, Top = 70, Width = 452, MaxLength = 40 };
        using var seatLabel = new System.Windows.Forms.Label { Text = "席名（上書き）", Left = 14, Top = 104, AutoSize = true };
        using var seat = new System.Windows.Forms.TextBox { Text = initialSeatName ?? "", Left = 14, Top = 126, Width = 452, MaxLength = 80 };
        using var note = new System.Windows.Forms.Label
        {
            Text = "ブロック名だけ、または席名だけでも入力できます。空欄の項目は変更しません。",
            Left = 14,
            Top = 160,
            Width = 452,
            Height = 22,
        };
        using var ok = new System.Windows.Forms.Button { Text = "上書き", Left = 310, Top = 190, Width = 75 };
        using var cancel = new System.Windows.Forms.Button { Text = "キャンセル", DialogResult = System.Windows.Forms.DialogResult.Cancel, Left = 391, Top = 190, Width = 75 };
        BulkSeatNameEdit? result = null;
        ok.Click += (_, _) =>
        {
            var blockName = string.IsNullOrWhiteSpace(block.Text) ? null : block.Text.Trim();
            var seatName = string.IsNullOrWhiteSpace(seat.Text) ? null : seat.Text.Trim();
            if (blockName is null && seatName is null)
            {
                System.Windows.Forms.MessageBox.Show(form, "ブロック名または席名を入力してください。", "席名をまとめて変更",
                    System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                return;
            }
            result = new BulkSeatNameEdit(blockName, seatName);
            form.DialogResult = System.Windows.Forms.DialogResult.OK;
            form.Close();
        };
        form.Controls.AddRange([explanation, blockLabel, block, seatLabel, seat, note, ok, cancel]);
        form.CancelButton = cancel;
        form.Shown += (_, _) => block.Focus();
        return form.ShowDialog() == System.Windows.Forms.DialogResult.OK ? result : null;
    }
}
