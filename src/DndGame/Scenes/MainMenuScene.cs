using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using DndGame.Core;

namespace DndGame.Scenes;

/// <summary>
/// Placeholder main menu scene for Phase 0 verification.
/// Displays a solid color background to confirm the rendering pipeline works.
/// </summary>
public class MainMenuScene : Scene
{
    private Texture2D? _testTexture;
    private readonly Color _backgroundColor = new(20, 30, 50);

    public override void Initialize()
    {
        _testTexture = new Texture2D(Game.GraphicsDevice, 1, 1);
        _testTexture.SetData(new[] { Color.White });
    }

    public override void Draw(GameTime gameTime)
    {
        Game.SpriteBatch.Begin(samplerState: SamplerState.PointClamp);
        Game.SpriteBatch.Draw(_testTexture,
            new Rectangle(0, 0, GameRoot.DESIGN_WIDTH, GameRoot.DESIGN_HEIGHT),
            _backgroundColor);
        Game.SpriteBatch.End();

        base.Draw(gameTime);
    }

    public override void End()
    {
        _testTexture?.Dispose();
        _testTexture = null;
        base.End();
    }
}
