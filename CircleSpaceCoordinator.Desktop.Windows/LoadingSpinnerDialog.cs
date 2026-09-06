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
        var ownerForm = owner as System.Windows.Forms.Form;
        try
        {
            if (ownerForm is not null)
                ownerForm.Enabled = false;
            if (owner is null)
                dialog.Show();
            else
                dialog.Show(owner);
            dialog.animationTimer.Start();

            var task = Task.Run(operation);
            while (!task.Wait(50))
                System.Windows.Forms.Application.DoEvents();
            return task.GetAwaiter().GetResult();
        }
        finally
        {
            dialog.animationTimer.Stop();
            dialog.Close();
            if (ownerForm is not null && !ownerForm.IsDisposed)
                ownerForm.Enabled = true;
        }
    }
}
