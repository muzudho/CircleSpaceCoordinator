namespace CircleSpaceCoordinator.Desktop.Windows;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StationeryUI.Canvas;
using Forms = System.Windows.Forms;

public sealed partial class VenueEditorGame
{
    private const int WorkerBarHeight = 32;
    private string Handle => settings?.Current.Handle ?? "";
    private DateOnly WorkDate => DateOnly.FromDateTime(DateTime.Now);
    private bool workerBarPressed;

    private bool EnsureHandle()
    {
        bool Valid()
        {
            try { return CircleSpaceCoordinator.Core.Model.PersonCredits.NormalizeHandle(Handle).Length > 0; }
            catch (ArgumentException) { return false; }
        }
        if (!Valid()) EditHandle();
        return Valid();
    }

    private void EditHandle()
    {
        using var form = PortableForm("作業者のHandle（Unicode 16文字以内）");
        var name = PortableText(form, "作業者", 24, Handle);
        var status = PortableStatus(form);
        var save = new Forms.Button { Left = 440, Top = 558, Width = 130, Text = "保存" };
        var cancel = new Forms.Button { Left = 580, Top = 558, Width = 135, Text = "キャンセル", DialogResult = Forms.DialogResult.Cancel };
        save.Click += (_, _) =>
        {
            try
            {
                settings!.SaveHandle(name.Text);
                form.DialogResult = Forms.DialogResult.OK;
            }
            catch (Exception ex) { status.Text = ex.Message; }
        };
        form.Controls.AddRange([save, cancel]);
        form.AcceptButton = save;
        form.CancelButton = cancel;
        ShowEditorDialog(form);
        modalInputDrain = true;
    }

    private bool UpdateWorkerBar(MouseState mouse)
    {
        if (!CanShowEditorHover || mappingPickerColumn > 0 || optimizationTask is not null || backgroundOperation is not null)
        {
            workerBarPressed = false;
            return false;
        }
        var inside = mouse.Y >= 0 && mouse.Y < WorkerBarHeight;
        if (inside && mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released)
        {
            CancelInProgressPointerInteraction();
            workerBarPressed = true;
        }
        if (!workerBarPressed) return false;
        if (mouse.LeftButton == ButtonState.Released)
        {
            workerBarPressed = false;
            if (inside)
            {
                if (mouse.X < Window.ClientBounds.Width - 160)
                {
                    if (workspace is not null && frameDraft is null && mappingDraft is null && mouse.X >= Window.ClientBounds.Width - 310) ManageChannelKnowledge();
                    else { if (mappingDraft is not null) SetMappingTextFocus(false); EditHandle(); }
                }
            }
        }
        previousMouse = mouse;
        return true;
    }

    private void DrawWorkerBar()
    {
        var width = GraphicsDevice.Viewport.Width;
        var showLibrary = workspace is not null && frameDraft is null && mappingDraft is null;
        DrawRectangle(new ScreenRectangle(0, 0, width, WorkerBarHeight), new Color(27, 42, 52));
        textRenderer?.Draw($"作業者：{(Handle.Length == 0 ? "未設定" : Handle)}  ［変更］",
            new Rectangle(12, 3, Math.Max(1, width - (showLibrary ? 328 : 178)), 26), Color.White, 16);
        if (showLibrary)
            textRenderer?.Draw("知見ライブラリー", new Rectangle(Math.Max(0, width - 310), 3, 145, 26), Color.LightCyan, 15, true);
        textRenderer?.Draw(WorkDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            new Rectangle(Math.Max(0, width - 150), 3, 138, 26), Color.White, 16, true);
    }

    // App-owned native dialogs share the same header as the canvas screens.
    private Forms.DialogResult ShowEditorDialog(Forms.Form form, Forms.IWin32Window? owner = null)
    {
        form.SuspendLayout();
        var controls = form.Controls.Cast<Forms.Control>().Select(control => (Control: control, Bounds: control.Bounds)).ToArray();
        form.ClientSize = new System.Drawing.Size(form.ClientSize.Width, form.ClientSize.Height + WorkerBarHeight);
        foreach (var item in controls)
            item.Control.Bounds = new System.Drawing.Rectangle(item.Bounds.X, item.Bounds.Y + WorkerBarHeight, item.Bounds.Width, item.Bounds.Height);
        var header = new Forms.Panel { Left = 0, Top = 0, Width = form.ClientSize.Width, Height = WorkerBarHeight,
            Anchor = Forms.AnchorStyles.Top | Forms.AnchorStyles.Left | Forms.AnchorStyles.Right,
            BackColor = System.Drawing.Color.FromArgb(27, 42, 52) };
        var date = new Forms.Label { Dock = Forms.DockStyle.Right, Width = 135, TextAlign = System.Drawing.ContentAlignment.MiddleRight,
            Padding = new Forms.Padding(0, 0, 8, 0), ForeColor = System.Drawing.Color.White };
        var worker = new Forms.Label { Dock = Forms.DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
            Padding = new Forms.Padding(8, 0, 0, 0), AutoEllipsis = true, ForeColor = System.Drawing.Color.White };
        void RefreshHeader()
        {
            worker.Text = $"作業者：{(Handle.Length == 0 ? "未設定" : Handle)}";
            date.Text = WorkDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        }
        header.Controls.Add(worker);
        header.Controls.Add(date);
        form.Controls.Add(header);
        header.BringToFront();
        form.ResumeLayout();
        RefreshHeader();
        using var timer = new Forms.Timer { Interval = 1000 };
        timer.Tick += (_, _) => RefreshHeader();
        timer.Start();
        return owner is null ? form.ShowDialog() : form.ShowDialog(owner);
    }
}
