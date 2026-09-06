namespace CircleSpaceCoordinator.Desktop.Text;

internal static class WindowsTextRasterizer
{
    public static byte[] RasterizePng(string text, int pixelHeight, bool bold)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (pixelHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(pixelHeight));

        using var font = new System.Drawing.Font(
            "Meiryo", pixelHeight,
            bold ? System.Drawing.FontStyle.Bold : System.Drawing.FontStyle.Regular,
            System.Drawing.GraphicsUnit.Pixel);
        var flags = System.Windows.Forms.TextFormatFlags.NoPadding |
                    System.Windows.Forms.TextFormatFlags.NoPrefix;
        var measured = System.Windows.Forms.TextRenderer.MeasureText(
            text, font, new System.Drawing.Size(int.MaxValue, int.MaxValue), flags);
        using var bitmap = new System.Drawing.Bitmap(
            Math.Max(1, measured.Width), Math.Max(1, measured.Height),
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
        {
            graphics.Clear(System.Drawing.Color.Transparent);
            System.Windows.Forms.TextRenderer.DrawText(
                graphics, text, font, new System.Drawing.Point(0, 0), System.Drawing.Color.White, flags);
        }
        using var stream = new MemoryStream();
        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        return stream.ToArray();
    }
}
