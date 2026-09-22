// Adapted from StationeryUI samples/StationeryUI.Demo/InspectorGame.cs at
// 5130fff6a5cf18aa2559f33388bcc104b3aa1d05 (MIT; see ThirdParty/StationeryUI-LICENSE.txt).
// The inspector UI itself is the library's StationeryDeveloperView.
namespace CircleSpaceCoordinator.Desktop.Windows;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StationeryUI.Inspection;
using StationeryUI.MonoGame;
using global::StationeryUI.Windows;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

/// <summary>Separate SDL event loop for the existing StationeryUI developer view.</summary>
internal sealed class StationeryInspectorGame : Game
{
    private readonly GraphicsDeviceManager manager;
    private readonly StationeryDeveloperStyle style = StationeryDeveloperStyle.Load();
    private readonly string pipeName;
    private readonly CancellationTokenSource stopping = new();
    private readonly object gate = new();
    private Pending? pending;
    private volatile bool disconnected;
    private Task? connection;
    private WindowsTextInputService? input;
    private StationeryDeveloperView? view;
    private long showSequence = -1;
    private long captureSequence;
    private bool shown = true;
    private bool waitForCloseKeyRelease = true;
    private KeyboardState previous;
    private sealed record Pending(DeveloperInspectionMessage Message, TaskCompletionSource<DeveloperViewState> Response);

    public StationeryInspectorGame(string pipeName)
    {
        this.pipeName = pipeName;
        manager = new(this) { PreferredBackBufferWidth = style.Width, PreferredBackBufferHeight = style.Height };
        Window.Title = "StationeryUI Inspector";
        Window.AllowUserResizing = true;
        IsMouseVisible = true;
    }

    protected override void LoadContent()
    {
        Window.Title = "F12 開発者ウィンドウ — サークル配置コーディネーター";
        input = new(Window.Handle);
        view = new(GraphicsDevice, input, family => new WindowsTextRasterizer(family), style);
        connection = Task.Run(ConnectAsync);
    }

    private async Task ConnectAsync()
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(10000, stopping.Token);
            using var reader = new StreamReader(pipe, Encoding.UTF8, false, 4096, true);
            using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, true) { AutoFlush = true };
            while (!stopping.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(stopping.Token);
                if (line is null) break;
                var message = JsonSerializer.Deserialize<DeveloperInspectionMessage>(line) ?? throw new JsonException("Missing snapshot.");
                var response = new TaskCompletionSource<DeveloperViewState>(TaskCreationOptions.RunContinuationsAsynchronously);
                lock (gate) pending = new(message, response);
                var state = await response.Task.WaitAsync(stopping.Token);
                await writer.WriteLineAsync(JsonSerializer.Serialize(state).AsMemory(), stopping.Token);
            }
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or TimeoutException or JsonException)
        { System.Diagnostics.Trace.WriteLine("Inspector connection: " + ex.Message); }
        finally { disconnected = true; }
    }

    protected override void Update(GameTime gameTime)
    {
        if (disconnected) { Exit(); return; }
        Pending? packet;
        lock (gate) { packet = pending; pending = null; }
        if (packet is not null)
        {
            view!.Refresh(packet.Message.Entries);
            if (showSequence < 0) view.Restore(packet.Message.RestoreState);
            if (packet.Message.CaptureSequence != captureSequence)
            {
                if (packet.Message.CapturePath is { } path) view.SelectCaptured(path);
                captureSequence = packet.Message.CaptureSequence;
            }
            if (packet.Message.ShowSequence != showSequence)
            {
                shown = true;
                waitForCloseKeyRelease = true;
                SDL_ShowWindow(Window.Handle);
                SDL_RaiseWindow(Window.Handle);
                showSequence = packet.Message.ShowSequence;
            }
        }
        var keyboard = Keyboard.GetState();
        UpdateVisibility(keyboard, IsActive);
        view!.Update(gameTime, shown && IsActive, keyboard, Mouse.GetState(), GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        packet?.Response.TrySetResult(view.Capture(shown));
        base.Update(gameTime);
    }

    private void UpdateVisibility(KeyboardState keyboard, bool active)
    {
        if (keyboard.IsKeyUp(Keys.F12) && keyboard.IsKeyUp(Keys.Escape)) waitForCloseKeyRelease = false;
        if (shown && active && !waitForCloseKeyRelease &&
            ((keyboard.IsKeyDown(Keys.F12) && previous.IsKeyUp(Keys.F12)) ||
             (keyboard.IsKeyDown(Keys.Escape) && previous.IsKeyUp(Keys.Escape))))
        {
            shown = false;
            SDL_HideWindow(Window.Handle);
        }
        previous = keyboard;
    }

    protected override void Draw(GameTime gameTime)
    {
        if (shown)
        {
            GraphicsDevice.Clear(StationeryUiHost.Convert(view!.Theme.Background));
            view.Draw();
        }
        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        stopping.Cancel();
        view?.Dispose();
        input?.Dispose();
        base.UnloadContent();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            stopping.Cancel();
            try { connection?.Wait(1000); } catch (AggregateException) { }
        }
        base.Dispose(disposing);
    }

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)] private static extern void SDL_ShowWindow(nint window);
    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)] private static extern void SDL_HideWindow(nint window);
    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)] private static extern void SDL_RaiseWindow(nint window);
}
