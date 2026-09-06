namespace CircleSpaceCoordinator.Desktop;

using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;

internal static class SdlWindowIcon
{
    public static void TrySet(GameWindow window, string imagePath)
    {
        if (!OperatingSystem.IsWindows() || !File.Exists(imagePath))
            return;

        try
        {
            using var source = new Bitmap(imagePath);
            using var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(bitmap))
                graphics.DrawImageUnscaled(source, 0, 0);

            var bounds = new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var data = bitmap.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                var surface = SdlCreateRgbSurfaceFrom(
                    data.Scan0,
                    bitmap.Width,
                    bitmap.Height,
                    32,
                    data.Stride,
                    0x00ff0000,
                    0x0000ff00,
                    0x000000ff,
                    0xff000000);
                if (surface == IntPtr.Zero)
                    return;

                try
                {
                    SdlSetWindowIcon(window.Handle, surface);
                }
                finally
                {
                    SdlFreeSurface(surface);
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }
        catch (Exception exception) when (
            exception is ExternalException or DllNotFoundException or EntryPointNotFoundException)
        {
            Debug.WriteLine($"Could not set the SDL window icon: {exception.Message}");
        }
    }

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_CreateRGBSurfaceFrom")]
    private static extern IntPtr SdlCreateRgbSurfaceFrom(
        IntPtr pixels,
        int width,
        int height,
        int depth,
        int pitch,
        uint redMask,
        uint greenMask,
        uint blueMask,
        uint alphaMask);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_SetWindowIcon")]
    private static extern void SdlSetWindowIcon(IntPtr window, IntPtr icon);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_FreeSurface")]
    private static extern void SdlFreeSurface(IntPtr surface);
}
