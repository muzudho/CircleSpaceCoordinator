namespace CircleSpaceCoordinator.Desktop.Windows.Text;

using Microsoft.Xna.Framework.Graphics;

internal sealed class DynamicTextRenderer(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
    : global::StationeryUI.MonoGame.RasterTextRenderer(graphicsDevice, spriteBatch,
        new global::StationeryUI.Windows.WindowsTextRasterizer())
{
}