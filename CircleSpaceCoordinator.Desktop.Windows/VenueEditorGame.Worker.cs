namespace CircleSpaceCoordinator.Desktop.Windows;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StationeryUI.Canvas;
using Forms = System.Windows.Forms;

public sealed partial class VenueEditorGame
{
    private const int WorkerBarHeight = 32;
    private string Handle => settings?.Current.Handle ?? "";
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
        form.ShowDialog();
        modalInputDrain = true;
    }

    private bool UpdateWorkerBar(MouseState mouse)
    {
        if (!CanShowEditorHover || frameDraft is not null || mappingDraft is not null || optimizationTask is not null || backgroundOperation is not null)
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
                if (workspace is not null && mouse.X >= Window.ClientBounds.Width - 150) ManageChannelKnowledge();
                else EditHandle();
            }
        }
        previousMouse = mouse;
        return true;
    }

    private void DrawWorkerBar()
    {
        if (frameDraft is not null || mappingDraft is not null) return;
        var width = GraphicsDevice.Viewport.Width;
        DrawRectangle(new ScreenRectangle(0, 0, width, WorkerBarHeight), new Color(27, 42, 52));
        textRenderer?.Draw($"作業者：{(Handle.Length == 0 ? "未設定" : Handle)}  ［変更］",
            new Rectangle(12, 3, Math.Max(1, width - 178), 26), Color.White, 16);
        if (workspace is not null)
            textRenderer?.Draw("知見ライブラリー", new Rectangle(Math.Max(0, width - 150), 3, 145, 26), Color.LightCyan, 15, true);
    }
}
