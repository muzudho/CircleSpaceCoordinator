namespace CircleSpaceCoordinator.Desktop.Windows;

/// <summary>Shows progress while a file operation runs without blocking its animation.</summary>
internal sealed class LoadingSpinnerDialog : System.Windows.Forms.Form
{
    private static readonly string[] Frames = ["◐", "◓", "◑", "◒"];
    private readonly System.Windows.Forms.Label spinner;
    private readonly System.Windows.Forms.Timer animationTimer;
    private int frameIndex;

    private LoadingSpinnerDialog(string message)
    {
        Text = "処理中";
        Width = 360;
        Height = 150;
        FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
        ControlBox = false;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;

        spinner = new System.Windows.Forms.Label
        {
            Text = Frames[0],
            Left = 26,
            Top = 32,
            Width = 50,
            Height = 54,
            TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
            Font = new System.Drawing.Font(System.Drawing.SystemFonts.DefaultFont.FontFamily, 28, System.Drawing.FontStyle.Bold),
        };
        var label = new System.Windows.Forms.Label
        {
            Text = message,
            Left = 88,
            Top = 43,
            Width = 230,
            Height = 34,
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
        };
        Controls.AddRange([spinner, label]);

        animationTimer = new System.Windows.Forms.Timer { Interval = 120 };
        animationTimer.Tick += (_, _) =>
        {
            frameIndex = (frameIndex + 1) % Frames.Length;
            spinner.Text = Frames[frameIndex];
        };
    }

    public static T Run<T>(System.Windows.Forms.IWin32Window? owner, string message, Func<T> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        using var dialog = new LoadingSpinnerDialog(message);
        T result = default!;
        System.Runtime.ExceptionServices.ExceptionDispatchInfo? failure = null;
        dialog.Shown += async (_, _) =>
        {
            dialog.animationTimer.Start();
            try { result = await Task.Run(operation); }
            catch (Exception exception) { failure = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception); }
            finally
            {
                dialog.animationTimer.Stop();
                dialog.Close();
            }
        };
        // Native modal ownership restores the owner before closing the progress window.
        // Closing a modeless window while its owner is disabled can activate another app.
        if (owner is null)
            dialog.ShowDialog();
        else
            dialog.ShowDialog(owner);
        failure?.Throw();
        return result;
    }
}
