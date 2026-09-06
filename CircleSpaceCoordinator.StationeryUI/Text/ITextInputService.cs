namespace CircleSpaceCoordinator.StationeryUI.Text;

using CircleSpaceCoordinator.StationeryUI.Canvas;

public readonly record struct TextInputUpdate(string Text, bool IsComposition);

/// <summary>確定文字・IME の未確定文字・クリップボードへのプラットフォーム境界。</summary>
public interface ITextInputService : IDisposable
{
    void Start();
    void Stop();
    void SetInputArea(ScreenRectangle area);
    IReadOnlyList<TextInputUpdate> DrainUpdates();
    string ReadClipboard();
    void WriteClipboard(string text);
}
