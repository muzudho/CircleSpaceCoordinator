namespace CircleSpaceCoordinator.Desktop.Text;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

internal sealed class DynamicTextRenderer(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch) : IDisposable
{
    private readonly Dictionary<(string Text, int Height, bool Bold), Texture2D> textures = [];

    public void Draw(string text, Rectangle bounds, Color color, int pixelHeight = 18, bool bold = false)
    {
        if (string.IsNullOrEmpty(text) || bounds.Width <= 0 || bounds.Height <= 0)
            return;
        var key = (text, pixelHeight, bold);
        if (!textures.TryGetValue(key, out var texture))
        {
            using var stream = new MemoryStream(WindowsTextRasterizer.RasterizePng(text, pixelHeight, bold), writable: false);
            texture = Texture2D.FromStream(graphicsDevice, stream);
            textures[key] = texture;
        }

        var scale = MathF.Min(1f, MathF.Min(bounds.Width / (float)texture.Width, bounds.Height / (float)texture.Height));
        var width = Math.Max(1, (int)MathF.Round(texture.Width * scale));
        var height = Math.Max(1, (int)MathF.Round(texture.Height * scale));
        spriteBatch.Draw(texture, new Rectangle(bounds.X, bounds.Y + (bounds.Height - height) / 2, width, height), color);
    }

    public void Dispose()
    {
        foreach (var texture in textures.Values)
            texture.Dispose();
        textures.Clear();
    }
}
