namespace CircleSpaceCoordinator.Desktop.Windows.Text;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

internal sealed class DynamicTextRenderer(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch) : IDisposable
{
    private readonly Dictionary<(string Text, int Height, bool Bold), Texture2D> textures = [];

    public void Draw(string text, Rectangle bounds, Color color, int pixelHeight = 18, bool bold = false)
    {
        if (string.IsNullOrEmpty(text) || bounds.Width <= 0 || bounds.Height <= 0)
            return;
        var texture = GetTexture(text, pixelHeight, bold);
        spriteBatch.Draw(texture, GetDrawBounds(text, bounds, pixelHeight, bold), color);
    }

    public Point Measure(string text, int pixelHeight = 18, bool bold = false) =>
        string.IsNullOrEmpty(text) ? Point.Zero : GetTexture(text, pixelHeight, bold).Bounds.Size;

    public Rectangle GetDrawBounds(string text, Rectangle bounds, int pixelHeight = 18, bool bold = false)
    {
        var measured = Measure(text, pixelHeight, bold);
        var scale = MathF.Min(1f, MathF.Min(bounds.Width / (float)Math.Max(1, measured.X), bounds.Height / (float)Math.Max(1, measured.Y)));
        var width = Math.Max(1, (int)MathF.Round(measured.X * scale));
        var height = Math.Max(1, (int)MathF.Round(measured.Y * scale));
        return new Rectangle(bounds.X, bounds.Y + (bounds.Height - height) / 2, width, height);
    }

    private Texture2D GetTexture(string text, int pixelHeight, bool bold)
    {
        var key = (text, pixelHeight, bold);
        if (!textures.TryGetValue(key, out var texture))
        {
            using var stream = new MemoryStream(WindowsTextRasterizer.RasterizePng(text, pixelHeight, bold), writable: false);
            texture = Texture2D.FromStream(graphicsDevice, stream);
            textures[key] = texture;
        }

        return texture;
    }

    public void Dispose()
    {
        foreach (var texture in textures.Values)
            texture.Dispose();
        textures.Clear();
    }
}
