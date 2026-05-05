using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using DndGame.Core;

namespace DndGame.Scenes;

public class MainMenuScene : Scene
{
    private Texture2D? _background;
    private FontSystem? _fontSystem;
    private DynamicSpriteFont? _font;
    private readonly Color _backgroundColor = new(20, 30, 50);

    public override void Initialize()
    {
        _background = new Texture2D(Game.GraphicsDevice, 1, 1);
        _background.SetData(new[] { Color.White });

        _fontSystem = new FontSystem();
        var fontPath = Path.Combine(AppContext.BaseDirectory, "Content", "Fonts", "NotoSansCJKsc-Regular.ttf");
        if (File.Exists(fontPath))
        {
            _fontSystem.AddFont(File.ReadAllBytes(fontPath));
        }
        _font = _fontSystem.GetFont(32);
    }

    public override void Draw(GameTime gameTime)
    {
        var sb = Game.SpriteBatch;

        sb.Begin(samplerState: SamplerState.PointClamp);
        sb.Draw(_background,
            new Rectangle(0, 0, GameRoot.DESIGN_WIDTH, GameRoot.DESIGN_HEIGHT),
            _backgroundColor);

        if (_font != null)
        {
            var text = "你好酒馆";
            var size = _font.MeasureString(text);
            var position = new Vector2(
                (GameRoot.DESIGN_WIDTH - size.X) / 2,
                (GameRoot.DESIGN_HEIGHT - size.Y) / 2);
            _font.DrawText(sb, text, position, Color.White);
        }

        sb.End();

        base.Draw(gameTime);
    }

    public override void End()
    {
        _background?.Dispose();
        _fontSystem?.Dispose();
        base.End();
    }
}
