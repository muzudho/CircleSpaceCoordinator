namespace CircleSpaceCoordinator.Desktop.Windows;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StationeryUI.Canvas;
using StationeryUI.Text;
using StationeryUI.MonoGame.Controls.ActionBadge;
using global::StationeryUI.Windows;
using System.Globalization;
using Forms = System.Windows.Forms;

public sealed partial class VenueEditorGame
{
    private const int WorkerBarHeight = 32;
    private string Handle => settings?.Current.Handle ?? "";
    private DateOnly WorkDate => DateOnly.FromDateTime(DateTime.Now);
    private bool workerBarPressed;
    private UnderlineTextEditor? workerEditor;
    private string workerComposition = "";
    private string? workerError;
    private bool workerSuppressConfirmation;
    private bool workerSelecting;
    private bool ShowWorkerLibrary => workspace is not null && frameDraft is null && mappingDraft is null;
    private ScreenRectangle WorkerInputBounds
    {
        get
        {
            var right = GraphicsDevice.Viewport.Width - (ShowWorkerLibrary ? 322 : 162);
            var width = Math.Clamp(right - 80, 1, 340);
            return new(right - width, 2, width, 28);
        }
    }
    private Rectangle WorkerTextBounds => ToRectangle(new ScreenRectangle(WorkerInputBounds.X + 4, 3,
        Math.Max(1, WorkerInputBounds.Width - 116), 23));

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
        if (settings is null || workerEditor is not null) return;
        if (mappingDraft is not null) SetMappingTextFocus(false);
        if (genreOrderCommentEditing) CommitGenreOrderCommentEdit();
        CancelInProgressPointerInteraction();
        workerEditor = new UnderlineTextEditor(Handle, int.MaxValue);
        workerEditor.SelectAll();
        workerComposition = "";
        workerError = null;
        workerSuppressConfirmation = true;
        workerSelecting = false;
        ResetUnderlineKeyRepeat();
        textInputService ??= new WindowsTextInputService(Window.Handle);
        textInputService.Start();
    }

    private bool FinishWorkerEdit(bool save)
    {
        if (workerEditor is null) return true;
        if (save)
        {
            try { settings!.SaveHandle(workerEditor.Text); }
            catch (Exception ex) { workerError = ex.Message; return false; }
        }
        workerEditor = null;
        workerComposition = "";
        workerError = null;
        workerSelecting = false;
        textInputService?.Stop();
        ResetUnderlineKeyRepeat();
        return true;
    }

    private bool UpdateWorkerBar(KeyboardState keyboard, MouseState mouse)
    {
        if (workerEditor is not null)
        {
            UpdateWorkerInput(keyboard, mouse);
            previousMouse = mouse;
            return true;
        }
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
                    if (ShowWorkerLibrary && mouse.X >= Window.ClientBounds.Width - 310) ManageChannelKnowledge();
                    else if (Contains(WorkerInputBounds, new(mouse.X, mouse.Y))) EditHandle();
                }
            }
        }
        previousMouse = mouse;
        return true;
    }

    private void UpdateWorkerInput(KeyboardState keyboard, MouseState mouse)
    {
        if (workerEditor is not { } editor || textInputService is null) return;
        var hadComposition = workerComposition.Length > 0;
        var updates = textInputService.DrainUpdates();
        foreach (var update in updates)
        {
            workerSelecting = false;
            if (update.IsComposition) workerComposition = update.Text;
            else { editor.Insert(update.Text); workerComposition = ""; workerError = null; }
        }
        if (hadComposition || updates.Count > 0 || workerComposition.Length > 0) workerSuppressConfirmation = true;
        else if (keyboard.IsKeyUp(Keys.Enter) && keyboard.IsKeyUp(Keys.Escape)) workerSuppressConfirmation = false;
        var enabled = !hadComposition && workerComposition.Length == 0 && !updates.Any(update => update.IsComposition);
        var left = underlineLeftRepeat.Update(keyboard.IsKeyDown(Keys.Left), IsPressed(keyboard, Keys.Left), statusHintTime, enabled);
        var right = underlineRightRepeat.Update(keyboard.IsKeyDown(Keys.Right), IsPressed(keyboard, Keys.Right), statusHintTime, enabled);
        var back = underlineBackRepeat.Update(keyboard.IsKeyDown(Keys.Back), IsPressed(keyboard, Keys.Back), statusHintTime, enabled);
        var delete = underlineDeleteRepeat.Update(keyboard.IsKeyDown(Keys.Delete), IsPressed(keyboard, Keys.Delete), statusHintTime, enabled);
        if (IsControlDown(keyboard) && IsPressed(keyboard, Keys.P)) RequestScreenshot();
        if (!workerSuppressConfirmation && IsPressed(keyboard, Keys.Escape)) { FinishWorkerEdit(false); return; }
        if ((!workerSuppressConfirmation && IsPressed(keyboard, Keys.Enter)) || (enabled && IsPressed(keyboard, Keys.Tab)))
        { FinishWorkerEdit(true); return; }
        var pointer = new ScreenPoint(mouse.X, mouse.Y);
        if (mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released && !Contains(WorkerInputBounds, pointer))
        {
            // Consume the click so it cannot also edit the project below the input.
            textInputService.Stop();
            workerComposition = "";
            if (!FinishWorkerEdit(true)) textInputService.Start();
            return;
        }
        if (!enabled) return;
        var shift = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
        if (left) editor.Move(-1, shift);
        if (right) editor.Move(1, shift);
        if (back) editor.Delete(true);
        if (delete) editor.Delete(false);
        if (IsPressed(keyboard, Keys.Home)) editor.MoveTo(0, shift);
        if (IsPressed(keyboard, Keys.End)) editor.MoveTo(editor.Text.Length, shift);
        if (IsControlDown(keyboard))
        {
            if (IsPressed(keyboard, Keys.A)) editor.SelectAll();
            if (IsPressed(keyboard, Keys.Z)) editor.Undo();
            if (IsPressed(keyboard, Keys.Y)) editor.Redo();
            try
            {
                if (editor.SelectionLength > 0 && (IsPressed(keyboard, Keys.C) || IsPressed(keyboard, Keys.X)))
                {
                    textInputService.WriteClipboard(editor.SelectedText);
                    if (IsPressed(keyboard, Keys.X)) editor.Delete(false);
                }
                if (IsPressed(keyboard, Keys.V)) editor.Insert(textInputService.ReadClipboard());
            }
            catch (System.Runtime.InteropServices.ExternalException) { workerError = "クリップボードを使用できませんでした。"; }
        }
        if (mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released && mouse.X < WorkerTextBounds.Right)
        {
            editor.MoveTo(WorkerCaretIndex(editor.Text, mouse.X), shift);
            workerSelecting = true;
        }
        if (workerSelecting && previousMouse.LeftButton == ButtonState.Pressed)
        {
            if (mouse.X != previousMouse.X || mouse.Y != previousMouse.Y) editor.MoveTo(WorkerCaretIndex(editor.Text, mouse.X), true);
            if (mouse.LeftButton == ButtonState.Released) workerSelecting = false;
        }
    }

    private float WorkerTextScale(string text) =>
        (textRenderer?.GetDrawBounds(string.IsNullOrEmpty(text) ? " " : text, WorkerTextBounds, 16).Width ?? 1)
        / (float)Math.Max(1, textRenderer?.Measure(string.IsNullOrEmpty(text) ? " " : text, 16).X ?? 1);

    private int WorkerCaretIndex(string text, int pointerX) => StringInfo.ParseCombiningCharacters(text).Append(text.Length)
        .MinBy(index => Math.Abs(WorkerTextBounds.X + (textRenderer?.Measure(text[..index], 16).X ?? 0) * WorkerTextScale(text) - pointerX));

    private void DrawWorkerBar()
    {
        var width = GraphicsDevice.Viewport.Width;
        var showLibrary = ShowWorkerLibrary;
        DrawRectangle(new ScreenRectangle(0, 0, width, WorkerBarHeight), new Color(27, 42, 52));
        var bounds = WorkerInputBounds;
        textRenderer?.Draw("作業者", new Rectangle(Math.Max(0, (int)bounds.X - 64), 3, 60, 26), Color.White, 16);
        var editor = workerEditor;
        var insertion = editor is null ? 0 : workerComposition.Length > 0 ? editor.SelectionStart : editor.Caret;
        var value = editor?.Text ?? Handle;
        var display = editor is not null && workerComposition.Length > 0
            ? value.Remove(editor.SelectionStart, editor.SelectionLength).Insert(insertion, workerComposition) : value;
        var scale = WorkerTextScale(display);
        float X(int index) => WorkerTextBounds.X + (textRenderer?.Measure(display[..index], 16).X ?? 0) * scale;
        if (editor is not null && editor.SelectionLength > 0 && workerComposition.Length == 0)
            DrawRectangle(new(X(editor.SelectionStart), 4, Math.Max(1, X(editor.SelectionStart + editor.SelectionLength) - X(editor.SelectionStart)), 21), new Color(45, 95, 125));
        textRenderer?.Draw(display.Length == 0 && editor is null ? "未設定" : display, WorkerTextBounds, Color.White, 16);
        DrawLine(new(bounds.X, 29), new(bounds.X + bounds.Width, 29), editor is null ? 1 : 2, new Color(99, 223, 185));
        var badge = ActionBadgeComponent.Create("Edit", new Rectangle(0, 0, (int)bounds.Width, (int)bounds.Height));
        var mouse = Mouse.GetState();
        if (!eventStartupLoading && CanShowEditorHover && Contains(bounds, new ScreenPoint(mouse.X, mouse.Y))) badge.Show();
        DrawMappingBadge(badge, bounds, 1);
        if (editor is not null)
        {
            DrawRectangle(new(X(insertion), 4, 2, 21), new Color(147, 244, 200));
            if (workerComposition.Length > 0) DrawLine(new(X(insertion), 26), new(X(insertion + workerComposition.Length), 26), 2, Color.Gold);
            textInputService?.SetInputArea(new(X(insertion), 3, Math.Max(1, WorkerTextBounds.Right - X(insertion)), 26));
            var help = workerError ?? "16文字以内／Enter：確定／Esc：取消";
            var helpBounds = new ScreenRectangle(Math.Max(0, bounds.X - 64), 32, bounds.Width + 64, 26);
            DrawRectangle(helpBounds, new Color(27, 42, 52));
            textRenderer?.Draw(help, ToRectangle(helpBounds), workerError is null ? Color.LightGray : Color.LightSalmon, 13);
        }
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
