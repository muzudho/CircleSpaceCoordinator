namespace StationeryUI.Controls;

using System.Text;
using StationeryUI.Text;

/// <summary>A domain-independent change-tag input. The host supplies validation, persistence and exit routing.</summary>
public sealed class ChangeTagEditor(Func<string, string?> validate)
{
    public UnderlineTextEditor Editor { get; } = new("", int.MaxValue);
    public bool HasChanges { get; set; }
    public bool IsSaving { get; private set; }
    public string? InputError { get; private set; }
    public string? SaveError { get; private set; }
    public string Text => Editor.Text.Trim();
    public int CharacterCount => Text.EnumerateRunes().Count();
    public string? ValidationError => InputError ?? validate(Text);
    public bool CanClose => !IsSaving && (!HasChanges || ValidationError is null);
    private int viewStart;

    /// <summary>Scrolls by complete text elements; callers measure with their own font.</summary>
    public (int Start, int End) VisibleRange(double width, Func<string, double> measure)
        => VisibleRange(Editor.Text, Editor.Caret, width, measure);

    /// <summary>Scroll a temporary IME preview without committing it to the editor.</summary>
    public (int Start, int End) VisibleRange(string text, int caret, double width, Func<string, double> measure)
    {
        var boundaries = System.Globalization.StringInfo.ParseCombiningCharacters(text).Append(text.Length).ToArray();
        caret = boundaries.Last(index => index <= Math.Clamp(caret, 0, text.Length));
        viewStart = boundaries.Last(index => index <= Math.Min(viewStart, caret));
        var lo = Array.IndexOf(boundaries, viewStart);
        var hi = Array.IndexOf(boundaries, caret);
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (measure(text[boundaries[mid]..caret]) > width) lo = mid + 1; else hi = mid;
        }
        viewStart = boundaries[lo];
        hi = boundaries.Length - 1;
        while (lo < hi)
        {
            var mid = (lo + hi + 1) / 2;
            if (measure(text[viewStart..boundaries[mid]]) <= width) lo = mid; else hi = mid - 1;
        }
        return (viewStart, boundaries[lo]);
    }

    // Reject invalid paste as a whole: UnderlineTextEditor would silently remove controls.
    public bool Insert(string value)
    {
        var remaining = value.AsSpan();
        while (!remaining.IsEmpty)
        {
            if (Rune.DecodeFromUtf16(remaining, out var rune, out var used) != System.Buffers.OperationStatus.Done ||
                Rune.IsControl(rune) || rune.Value is 0x2028 or 0x2029)
            {
                InputError = "改行・制御文字を含まない１行で入力してください。";
                return false;
            }
            remaining = remaining[used..];
        }
        Editor.Insert(value);
        Edited();
        return true;
    }

    public void Edited() { InputError = null; SaveError = null; }
    public bool TryBeginSave(out string? changeLog)
    {
        changeLog = null;
        if (!CanClose) return false;
        changeLog = HasChanges ? Text : null;
        IsSaving = true;
        SaveError = null;
        return true;
    }
    public void SaveFailed(string message) { IsSaving = false; SaveError = message; }
    public void SaveSucceeded() { IsSaving = false; SaveError = null; }
}
