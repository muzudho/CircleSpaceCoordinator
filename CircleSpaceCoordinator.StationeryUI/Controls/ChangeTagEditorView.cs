namespace StationeryUI.Controls;

using StationeryUI.Canvas;

public enum ChangeTagInk { Text, Muted, Placeholder, Selection, Underline, Badge, BadgeText, Error, Composition, Paper, Shadow }

/// <summary>Reusable underline/badge presentation. Drawing and font measurement belong to the host.</summary>
public sealed class ChangeTagEditorView
{
    public string Placeholder { get; init; } = "変更内容を１０００文字で入力してください";
    public string Label { get; init; } = "１行チェンジログ";
    public string ActionLabel { get; init; } = "(change)";
    public int MaximumLength { get; init; } = 1000;
    public (int Start, int End) VisibleRange { get; private set; }
    public ScreenRectangle InputBounds { get; private set; }
    public ScreenRectangle BadgeBounds { get; private set; }
    public ScreenRectangle CaretBounds { get; private set; }
    public ScreenRectangle CompositionBounds { get; private set; }
    public const int InputFontSize = 14;

    public void Draw(ChangeTagEditor model, ScreenRectangle bounds, double scale, bool focused, bool hovered,
        string attribution, string previousLog, string composition,
        Func<string, double> measure,
        Action<string, ScreenRectangle, int, ChangeTagInk> text,
        Action<ScreenRectangle, ChangeTagInk> fill,
        Action<ScreenRectangle>? badge = null)
    {
        ScreenRectangle Area(double x, double y, double width, double height) =>
            new(bounds.X + x * scale, bounds.Y + y * scale, width * scale, height * scale);
        var width = bounds.Width / scale;
        fill(new(bounds.X + 5 * scale, bounds.Y + 5 * scale, bounds.Width, bounds.Height), ChangeTagInk.Shadow);
        fill(bounds, ChangeTagInk.Paper);
        // About forty full-width characters at 14px, plus the existing action badge.
        var inputWidth = Math.Max(1, width - 144);
        InputBounds = Area(16, 62, inputWidth, 36);
        BadgeBounds = Area(16 + inputWidth - 108, 70, 100, 26);
        text(attribution, Area(16, 8, width - 32, 22), 14, ChangeTagInk.Text);
        text(previousLog, Area(16, 34, width - 32, 22), 13, ChangeTagInk.Muted);
        if (!model.HasChanges)
        {
            text("変更なし — チェンジログの入力は不要です", InputBounds, 16, ChangeTagInk.Muted);
            return;
        }
        var editor = model.Editor;
        var insertion = composition.Length > 0 ? editor.SelectionStart : editor.Caret;
        var display = composition.Length > 0
            ? editor.Text.Remove(editor.SelectionStart, editor.SelectionLength).Insert(insertion, composition)
            : editor.Text;
        var textWidth = Math.Max(1, BadgeBounds.X - InputBounds.X - 12 * scale);
        VisibleRange = model.VisibleRange(display, insertion + composition.Length, textWidth - 3, measure);
        var (start, end) = VisibleRange;
        double Offset(int index) => measure(display[start..Math.Clamp(index, start, end)]);
        if (focused && editor.SelectionLength > 0 && composition.Length == 0)
            fill(new(InputBounds.X + Offset(editor.SelectionStart), InputBounds.Y,
                Offset(editor.SelectionStart + editor.SelectionLength) - Offset(editor.SelectionStart), InputBounds.Height), ChangeTagInk.Selection);
        text(display.Length == 0 ? Placeholder : display[start..end],
            new(InputBounds.X, InputBounds.Y, textWidth, InputBounds.Height), InputFontSize,
            display.Length == 0 ? ChangeTagInk.Placeholder : ChangeTagInk.Text);
        fill(new(InputBounds.X, InputBounds.Y + InputBounds.Height, InputBounds.Width, focused ? 3 : 1), ChangeTagInk.Underline);
        CaretBounds = new(InputBounds.X + Offset(insertion), InputBounds.Y + 3, 2, InputBounds.Height - 6);
        CompositionBounds = new(InputBounds.X + Offset(insertion), InputBounds.Y + InputBounds.Height - 3,
            Offset(insertion + composition.Length) - Offset(insertion), 2);
        if (composition.Length > 0) fill(CompositionBounds, ChangeTagInk.Composition);
        if (focused) fill(CaretBounds, ChangeTagInk.Underline);
        if (focused || hovered)
        {
            if (badge is not null) badge(InputBounds);
            else
            {
                fill(BadgeBounds, ChangeTagInk.Badge);
                text(ActionLabel, BadgeBounds, 16, ChangeTagInk.BadgeText);
            }
        }
        text($"{Label}　{model.CharacterCount} / {MaximumLength}", Area(16, 102, width - 32, 23), 13, ChangeTagInk.Text);
        var error = model.SaveError ?? model.ValidationError;
        text(error ?? "閉じる・Esc：変更タグと内容を自動保存",
            Area(16, 126, width - 32, 23), 12, error is null ? ChangeTagInk.Muted : ChangeTagInk.Error);
    }
}
