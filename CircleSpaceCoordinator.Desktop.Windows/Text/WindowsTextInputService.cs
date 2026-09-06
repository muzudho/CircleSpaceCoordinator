namespace CircleSpaceCoordinator.Desktop.Windows.Text;

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using CircleSpaceCoordinator.StationeryUI.Canvas;
using CircleSpaceCoordinator.StationeryUI.Text;

/// <summary>Windows DesktopGL の SDL 入力と Windows クリップボードの接続。</summary>
internal sealed class WindowsTextInputService : ITextInputService
{
    private readonly ConcurrentQueue<TextInputUpdate> updates = new();
    private readonly EventWatch watch;
    private readonly uint windowId;
    private bool started;

    public WindowsTextInputService(nint sdlWindow)
    {
        windowId = SDL_GetWindowID(sdlWindow);
        watch = Watch;
    }
    public void Start()
    {
        if (started) return;
        started = true;
        SDL_AddEventWatch(watch, 0);
        SDL_StartTextInput();
    }
    public void Stop()
    {
        if (!started) return;
        SDL_DelEventWatch(watch, 0);
        SDL_StopTextInput();
        started = false;
        while (updates.TryDequeue(out _)) { }
    }
    public void SetInputArea(ScreenRectangle area)
    {
        var rectangle = new SdlRectangle { X = (int)area.X, Y = (int)area.Y, W = (int)area.Width, H = (int)area.Height };
        SDL_SetTextInputRect(ref rectangle);
    }
    public IReadOnlyList<TextInputUpdate> DrainUpdates()
    {
        var result = new List<TextInputUpdate>();
        while (updates.TryDequeue(out var update)) result.Add(update);
        return result;
    }
    private int Watch(nint userData, nint data)
    {
        try
        {
            var type = Marshal.ReadInt32(data);
            if (type is not (0x302 or 0x303) || unchecked((uint)Marshal.ReadInt32(data, 8)) != windowId) return 0;
            var bytes = new byte[32];
            Marshal.Copy(data + 12, bytes, 0, bytes.Length);
            var end = Array.IndexOf(bytes, (byte)0);
            updates.Enqueue(new TextInputUpdate(Encoding.UTF8.GetString(bytes, 0, end < 0 ? bytes.Length : end), type == 0x302));
        }
        catch { /* Native callbacks must not propagate exceptions. */ }
        return 0;
    }
    public string ReadClipboard() => System.Windows.Forms.Clipboard.ContainsText() ? System.Windows.Forms.Clipboard.GetText() : "";
    public void WriteClipboard(string text) { if (text.Length > 0) System.Windows.Forms.Clipboard.SetText(text); }
    public void Dispose() => Stop();

    [StructLayout(LayoutKind.Sequential)] private struct SdlRectangle { public int X, Y, W, H; }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int EventWatch(nint userData, nint data);
    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl)] private static extern uint SDL_GetWindowID(nint window);
    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl)] private static extern void SDL_AddEventWatch(EventWatch filter, nint userData);
    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl)] private static extern void SDL_DelEventWatch(EventWatch filter, nint userData);
    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl)] private static extern void SDL_StartTextInput();
    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl)] private static extern void SDL_StopTextInput();
    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl)] private static extern void SDL_SetTextInputRect(ref SdlRectangle rect);
}
