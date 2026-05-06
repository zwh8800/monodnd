using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using DndGame.Core;

namespace DndGame.Scenes;

/// <summary>
/// 主菜单场景 —— 显示游戏标题，按 Enter 进入酒馆。
/// </summary>
public class MainMenuScene : Scene
{
    private Texture2D? _background;
    private readonly Color _backgroundColor = new(20, 30, 50);
    private bool _enterPressed;

    public override void Initialize()
    {
        _background = new Texture2D(Game.GraphicsDevice, 1, 1);
        _background.SetData(new[] { Color.White });
        base.Initialize();
    }

    public override void Update(GameTime gameTime)
    {
        var state = Keyboard.GetState();

        if (state.IsKeyDown(Keys.Enter) && !_enterPressed)
        {
            _enterPressed = true;
            GameRoot.Instance.StartSceneTransition(new TavernScene());
        }

        if (state.IsKeyUp(Keys.Enter))
            _enterPressed = false;

        base.Update(gameTime);
    }

    public override void Draw(GameTime gameTime)
    {
        var sb = Game.SpriteBatch;
        sb.Begin(samplerState: SamplerState.PointClamp);

        sb.Draw(_background,
            new Rectangle(0, 0, GameRoot.DESIGN_WIDTH, GameRoot.DESIGN_HEIGHT),
            _backgroundColor);

        if (ServiceLocator.TryGet<IFontService>(out var fontService))
        {
            var font = fontService!.GetFont(48);
            var title = "酒馆与命运";
            var titleSize = font.MeasureString(title);
            font.DrawText(sb, title,
                new Vector2((GameRoot.DESIGN_WIDTH - titleSize.X) / 2, 200),
                new Color(255, 215, 0));

            var font24 = fontService.GetFont(24);
            var subtitle = "按 Enter 进入酒馆";
            var subSize = font24.MeasureString(subtitle);
            font24.DrawText(sb, subtitle,
                new Vector2((GameRoot.DESIGN_WIDTH - subSize.X) / 2, 400),
                new Color(200, 200, 200));

            var font16 = fontService.GetFont(16);
            var phase = "Phase 1 MVP — 战斗/角色/地图/酒馆";
            var phaseSize = font16.MeasureString(phase);
            font16.DrawText(sb, phase,
                new Vector2((GameRoot.DESIGN_WIDTH - phaseSize.X) / 2, 500),
                new Color(120, 120, 150));
        }

        sb.End();
        base.Draw(gameTime);
    }

    public override void End()
    {
        _background?.Dispose();
        base.End();
    }
}
