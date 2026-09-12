using global::StationeryUI.Windows;
namespace CircleSpaceCoordinator.Desktop.Windows;

using System.Globalization;
using CircleSpaceCoordinator.Desktop.Windows.Text;
using StationeryUI.Canvas;
using StationeryUI.Controls;
using StationeryUI.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

public sealed partial class VenueEditorGame
{
    private const int TextInputFocus = -1;
    private const int NoModalFocus = -2;
    private ITextInputService? textInputService;
    private UnderlineTextEditor? underlineEditor;
    private string compositionText = "";
    private bool suppressTextConfirmation;
    private bool selectingUnderlineText;

    private void OpenUnderlineInput(string title, string initial, Action<string> accepted)
    {
        var editor = new UnderlineTextEditor(initial);
        OpenModal(new ModalDialogModel(ModalDialogKind.Text, title,
            "選択した配置の名前を変更します。\n新しい名前を入力し、［確定］を選んでください（100 文字まで）。\n［キャンセル］を選ぶと、元の名前を残します。"), action =>
        {
            if (action != ModalDialogAction.Accept) return;
            try { accepted(editor.Text.Trim()); }
            catch (Exception exception) { ShowInAppMessage(title, exception.Message); }
        });
        underlineEditor = editor;
        compositionText = "";
        suppressTextConfirmation = false;
        textInputService ??= new WindowsTextInputService(Window.Handle);
        textInputService.Start();
    }

    private ScreenRectangle UnderlineBounds()
    {
        var bounds = ModalBounds();
        return new ScreenRectangle(bounds.X + 20, bounds.Y + bounds.Height - 136, bounds.Width - 40, 46);
    }

    private const int TextInputHelpHeight = 112;

    private void DrawTextInputHelp()
    {
        if (modalDialog?.Kind != ModalDialogKind.Text) return;
        var width = GraphicsDevice.Viewport.Width;
        var top = Math.Max(0, GraphicsDevice.Viewport.Height - TextInputHelpHeight);
        // A modal overlay, independent of the ordinary status message underneath.
        DrawRectangle(new ScreenRectangle(0, top, width, TextInputHelpHeight), new Color(20, 32, 42));
        DrawLine(new ScreenPoint(0, top), new ScreenPoint(width, top), 2, new Color(99, 223, 185));
        string[] lines =
        [
            "キーボード操作　Ctrl+A：全選択　Ctrl+C：コピー　Ctrl+V：貼り付け　Ctrl+X：切り取り",
            "Ctrl+Z：元に戻す　Ctrl+Y：やり直し　←／→・Home／End：移動　Shift 併用：範囲選択",
            "Tab：入力欄・ボタンを移動　Enter：確定／選択中のボタンを実行　Esc：キャンセル（IME 変換中を除く）",
            "ドラッグ：範囲選択　Ctrl+P：画面を撮影　余白をクリック：入力を離れる（未確定の変換は取消）",
        ];
        for (var index = 0; index < lines.Length; index++)
            textRenderer?.Draw(lines[index], new Rectangle(16, top + 8 + index * 25, Math.Max(1, width - 32), 23),
                new Color(220, 233, 239), 16);
    }

    private bool UpdateUnderlineInput(KeyboardState keyboard, MouseState mouse)
    {
        if (underlineEditor is not { } editor || textInputService is null) return true;
        var hadComposition = compositionText.Length > 0;
        var updates = textInputService.DrainUpdates();
        foreach (var update in updates)
        {
            selectingUnderlineText = false;
            if (update.IsComposition) compositionText = update.Text;
            else
            {
                if (modalFocus == TextInputFocus) editor.Insert(update.Text);
                compositionText = "";
            }
        }
        if (hadComposition || updates.Count > 0 || compositionText.Length > 0) suppressTextConfirmation = true;
        else if (keyboard.IsKeyUp(Keys.Enter) && keyboard.IsKeyUp(Keys.Escape)) suppressTextConfirmation = false;
        var pointer = new ScreenPoint(mouse.X, mouse.Y);
        if (mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released &&
            !Contains(UnderlineBounds(), pointer) && !modalButtons.Any(item => item.Button.Contains(pointer)))
        {
            // Leave the modal open, but stop editing. Uncommitted IME text is cancelled.
            modalFocus = NoModalFocus;
            selectingUnderlineText = false;
            textInputService.Stop();
            compositionText = "";
            pressedModalButton?.CancelPress();
            pressedModalButton = null;
            return true;
        }
        if (compositionText.Length > 0) return true;
        if (!suppressTextConfirmation && IsPressed(keyboard, Keys.Escape))
        {
            ApplyModalAction(ModalDialogAction.Cancel);
            return true;
        }
        if (IsPressed(keyboard, Keys.Tab))
        {
            selectingUnderlineText = false;
            modalFocus = modalFocus >= modalButtons.Count - 1 ? -1 : modalFocus + 1;
            if (modalFocus == TextInputFocus) textInputService.Start(); else textInputService.Stop();
        }
        if (!suppressTextConfirmation && modalFocus != NoModalFocus &&
            (IsPressed(keyboard, Keys.Enter) || (modalFocus >= 0 && IsPressed(keyboard, Keys.Space))))
        {
            ConfirmUnderlineInput(modalFocus == TextInputFocus ? ModalDialogAction.Accept : modalButtons[modalFocus].Action);
            return true;
        }
        if (modalFocus == TextInputFocus)
        {
            var control = IsControlDown(keyboard);
            var shift = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
            if (IsPressed(keyboard, Keys.Left)) editor.Move(-1, shift);
            if (IsPressed(keyboard, Keys.Right)) editor.Move(1, shift);
            if (IsPressed(keyboard, Keys.Home)) editor.MoveTo(0, shift);
            if (IsPressed(keyboard, Keys.End)) editor.MoveTo(editor.Text.Length, shift);
            if (IsPressed(keyboard, Keys.Back)) editor.Delete(true);
            if (IsPressed(keyboard, Keys.Delete)) editor.Delete(false);
            if (control)
            {
                if (IsPressed(keyboard, Keys.A)) editor.SelectAll();
                if (IsPressed(keyboard, Keys.Z)) editor.Undo();
                if (IsPressed(keyboard, Keys.Y)) editor.Redo();
                try
                {
                    if (editor.SelectionLength > 0 && (IsPressed(keyboard, Keys.C) || IsPressed(keyboard, Keys.X)))
                    {
                        textInputService.WriteClipboard(editor.SelectedText);
                        if (IsPressed(keyboard, Keys.X) && editor.SelectionLength > 0) editor.Delete(false);
                    }
                    if (IsPressed(keyboard, Keys.V)) editor.Insert(textInputService.ReadClipboard());
                }
                catch (System.Runtime.InteropServices.ExternalException)
                {
                    modalDialog!.Message = "クリップボードを使用できませんでした。もう一度操作してください。";
                }
            }
        }
        foreach (var (button, _) in modalButtons) button.UpdatePointer(pointer);
        if (mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released)
        {
            if (Contains(UnderlineBounds(), pointer))
            {
                modalFocus = TextInputFocus;
                textInputService.Start();
                editor.MoveTo(UnderlineCaretIndex(editor.Text, mouse.X),
                    keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift));
                selectingUnderlineText = true;
            }
            else
            {
                pressedModalButton = modalButtons.Select(item => item.Button).FirstOrDefault(button => button.Press(pointer));
                if (pressedModalButton is not null)
                {
                    modalFocus = modalButtons.FindIndex(item => item.Button == pressedModalButton);
                    textInputService.Stop();
                }
            }
        }
        // Keep the initial anchor while dragging, including outside the field and on release.
        // MoveTo belongs to StationeryUI and preserves complete Unicode text elements.
        if (selectingUnderlineText && previousMouse.LeftButton == ButtonState.Pressed)
        {
            if (mouse.X != previousMouse.X || mouse.Y != previousMouse.Y)
                editor.MoveTo(UnderlineCaretIndex(editor.Text, mouse.X), extend: true);
            if (mouse.LeftButton == ButtonState.Released) selectingUnderlineText = false;
        }
        if (mouse.LeftButton == ButtonState.Released && previousMouse.LeftButton == ButtonState.Pressed)
        {
            var pressed = pressedModalButton;
            pressedModalButton = null;
            if (pressed?.Release(pointer) == true)
                ConfirmUnderlineInput(modalButtons.Single(item => item.Button == pressed).Action);
        }
        return true;
    }

    private void ConfirmUnderlineInput(ModalDialogAction action)
    {
        if (action == ModalDialogAction.Accept && string.IsNullOrWhiteSpace(underlineEditor?.Text))
        {
            modalDialog!.Message = "名前を入力してください。空白だけの名前は使用できません。";
            modalFocus = TextInputFocus;
            textInputService?.Start();
            return;
        }
        ApplyModalAction(action);
    }

    private int MeasureInput(string text) => textRenderer?.Measure(text, 22).X ?? 0;

    private int UnderlineCaretIndex(string text, int pointerX)
    {
        var boundaries = StringInfo.ParseCombiningCharacters(text).Append(text.Length);
        var scale = UnderlineScale(text);
        return boundaries.MinBy(index => Math.Abs(UnderlineBounds().X + MeasureInput(text[..index]) * scale - pointerX));
    }

    private Rectangle UnderlineTextBounds()
    {
        var bounds = UnderlineBounds();
        return ToRectangle(new ScreenRectangle(bounds.X, bounds.Y, bounds.Width - 8, bounds.Height));
    }

    private Rectangle UnderlineDrawBounds(string text) =>
        textRenderer?.GetDrawBounds(string.IsNullOrEmpty(text) ? " " : text, UnderlineTextBounds(), 22) ?? UnderlineTextBounds();

    // Use the actual rounded destination width, including both width and height fitting.
    private float UnderlineScale(string text) => UnderlineDrawBounds(text).Width / (float)Math.Max(1, MeasureInput(text));

    private void DrawUnderlineInput()
    {
        if (underlineEditor is not { } editor) return;
        var bounds = UnderlineBounds();
        var insertion = compositionText.Length > 0 ? editor.SelectionStart : editor.Caret;
        var display = compositionText.Length > 0
            ? editor.Text.Remove(editor.SelectionStart, editor.SelectionLength).Insert(insertion, compositionText)
            : editor.Text;
        var scale = UnderlineScale(display);
        var destination = UnderlineDrawBounds(display);
        var textHeight = destination.Height;
        var y = destination.Y;
        if (modalFocus == TextInputFocus && editor.SelectionLength > 0 && compositionText.Length == 0)
            DrawRectangle(new ScreenRectangle(bounds.X + MeasureInput(editor.Text[..editor.SelectionStart]) * scale, y,
                Math.Max(1, (MeasureInput(editor.Text[..(editor.SelectionStart + editor.SelectionLength)]) - MeasureInput(editor.Text[..editor.SelectionStart])) * scale), textHeight), new Color(45, 95, 125));
        textRenderer?.Draw(display, UnderlineTextBounds(), Color.White, 22);
        DrawLine(new ScreenPoint(bounds.X, bounds.Y + bounds.Height), new ScreenPoint(bounds.X + bounds.Width, bounds.Y + bounds.Height),
            modalFocus == TextInputFocus ? 3 : 1, new Color(99, 223, 185));
        var caretX = bounds.X + MeasureInput(editor.Text[..insertion]) * scale;
        if (compositionText.Length > 0)
            DrawLine(new ScreenPoint(caretX, y + textHeight), new ScreenPoint(bounds.X + MeasureInput(display[..(insertion + compositionText.Length)]) * scale, y + textHeight), 2, new Color(255, 225, 128));
        if (modalFocus == TextInputFocus) DrawRectangle(new ScreenRectangle(caretX, y, 2, textHeight), new Color(147, 244, 200));
        textInputService?.SetInputArea(new ScreenRectangle(caretX, bounds.Y, Math.Max(1, bounds.Width - (caretX - bounds.X)), bounds.Height));
    }
}
