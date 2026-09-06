namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.OptimizationEngine;

internal static class OptimizationProgressDialog
{
    public static CirclePlacementOptimizationResult? Show(CircleSpaceProject project, string planId, TimeSpan timeLimit)
    {
        using var cancellation = new CancellationTokenSource();
        using var form = new System.Windows.Forms.Form
        {
            Text = "サークル配置を自動最適化",
            Width = 460,
            Height = 200,
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog,
            ControlBox = false,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen,
            ShowInTaskbar = false,
        };
        using var spinner = new System.Windows.Forms.Label { Text = "◐", Left = 24, Top = 39, Width = 45, Height = 44, TextAlign = System.Drawing.ContentAlignment.MiddleCenter, Font = new System.Drawing.Font(System.Drawing.SystemFonts.DefaultFont.FontFamily, 24, System.Drawing.FontStyle.Bold) };
        using var status = new System.Windows.Forms.Label { Text = "配置案を最適化しています…", Left = 82, Top = 26, Width = 340, Height = 24 };
        using var progressLabel = new System.Windows.Forms.Label { Text = "試行 0 回", Left = 82, Top = 54, Width = 340, Height = 24 };
        using var stop = new System.Windows.Forms.Button { Text = "ストップ", Left = 322, Top = 105, Width = 100, Height = 32 };
        using var timer = new System.Windows.Forms.Timer { Interval = 120 };
        var frames = new[] { "◐", "◓", "◑", "◒" };
        var frame = 0;
        CirclePlacementOptimizationResult? result = null;
        timer.Tick += (_, _) => { frame = (frame + 1) % frames.Length; spinner.Text = frames[frame]; };
        stop.Click += (_, _) => { stop.Enabled = false; status.Text = "停止しています。最高の配置案を確定中です…"; cancellation.Cancel(); };
        form.Controls.AddRange([spinner, status, progressLabel, stop]);
        form.Shown += async (_, _) =>
        {
            timer.Start();
            var progress = new Progress<CirclePlacementOptimizationProgress>(item => progressLabel.Text = $"試行 {item.Iteration:N0} 回　経過 {item.Elapsed:mm\\:ss}　一般 {item.BestScore.GeneralAttendeeScore:F2}　サークル {item.BestScore.CircleParticipantScore:F2}");
            try
            {
                result = await Task.Run(() => new CirclePlacementOptimizationEngine().Optimize(project, planId,
                    new CirclePlacementOptimizationOptions { TimeLimit = timeLimit }, progress, cancellation.Token));
                form.DialogResult = System.Windows.Forms.DialogResult.OK;
            }
            catch (Exception exception)
            {
                System.Windows.Forms.MessageBox.Show(form, exception.Message, "自動最適化エラー", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
            finally
            {
                timer.Stop();
                form.Close();
            }
        };
        return form.ShowDialog() == System.Windows.Forms.DialogResult.OK ? result : null;
    }
}
