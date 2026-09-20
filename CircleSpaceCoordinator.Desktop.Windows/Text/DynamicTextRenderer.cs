namespace CircleSpaceCoordinator.Desktop.Windows.Text;

using Microsoft.Xna.Framework.Graphics;
using CircleSpaceCoordinator.Desktop.Core.Logging;
using StationeryUI.Platform;

internal sealed class DynamicTextRenderer(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, PerformanceRecorder? performance = null)
    : global::StationeryUI.MonoGame.RasterTextRenderer(graphicsDevice, spriteBatch,
        new MeasuredTextRasterizer(performance))
{
}

internal sealed class MeasuredTextRasterizer(PerformanceRecorder? performance) : ITextRasterizer
{
    private readonly global::StationeryUI.Windows.WindowsTextRasterizer inner = new();
    public byte[] RasterizePng(string text, int pixelHeight, bool bold)
    {
        using var timing = performance?.Measure("text_rasterize_cache_miss");
        return inner.RasterizePng(text, pixelHeight, bold);
    }
    public float MeasureTextWidth(string text, int pixelHeight, bool bold) => inner.MeasureTextWidth(text, pixelHeight, bold);
    public int MeasureLineHeight(int pixelHeight, int extraLineSpacing) => inner.MeasureLineHeight(pixelHeight, extraLineSpacing);
    public int MeasureBaselineOffset(int pixelHeight) => inner.MeasureBaselineOffset(pixelHeight);
    public int GetWrappedPageCount(string text, int width, int height, int pixelHeight, int extraLineSpacing)
        => inner.GetWrappedPageCount(text, width, height, pixelHeight, extraLineSpacing);
    public byte[] RasterizeWrappedPagePng(string text, int width, int height, int pixelHeight, int extraLineSpacing, int requestedPage)
        => inner.RasterizeWrappedPagePng(text, width, height, pixelHeight, extraLineSpacing, requestedPage);
}
