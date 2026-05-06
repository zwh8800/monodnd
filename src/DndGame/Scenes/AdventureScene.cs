using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using GoRogue;
using DndGame.Core;
using DndGame.Systems.Combat;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace DndGame.Scenes;

/// <summary>
/// 冒险地图场景 —— 地牢探索、FOV、WASD移动。
/// </summary>
public class AdventureScene : Scene
{
    private GoRogueMapManager? _map;
    private DungeonGenerator? _generator;
    private Coord _playerPosition;
    private Texture2D? _pixelTexture;
    private DynamicSpriteFont? _font;
    private DynamicSpriteFont? _fontSmall;
    private KeyboardState _prevKeyState;
    private int _moveCount;

    public override void Initialize()
    {
        _generator = new DungeonGenerator();
        _map = _generator.Generate(40, 30);

        var start = DungeonGenerator.FindStartPosition(_map);
        _playerPosition = start ?? new Coord(5, 5);

        _pixelTexture = new Texture2D(Game.GraphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });

        if (ServiceLocator.TryGet<IFontService>(out var fontService))
        {
            _font = fontService!.GetFont(20);
            _fontSmall = fontService.GetFont(14);
        }

        UpdateFOV();
        base.Initialize();
    }

    public override void Update(GameTime gameTime)
    {
        var state = Keyboard.GetState();
        var dx = 0;
        var dy = 0;

        if (state.IsKeyDown(Keys.W) && _prevKeyState.IsKeyUp(Keys.W)) dy = -1;
        if (state.IsKeyDown(Keys.S) && _prevKeyState.IsKeyUp(Keys.S)) dy = 1;
        if (state.IsKeyDown(Keys.A) && _prevKeyState.IsKeyUp(Keys.A)) dx = -1;
        if (state.IsKeyDown(Keys.D) && _prevKeyState.IsKeyUp(Keys.D)) dx = 1;

        if ((dx != 0 || dy != 0) && _map != null)
        {
            var newX = _playerPosition.X + dx;
            var newY = _playerPosition.Y + dy;
            if (_map.IsWalkable(newX, newY))
            {
                _playerPosition = new Coord(newX, newY);
                UpdateFOV();
                _moveCount++;
            }
        }

        if (state.IsKeyDown(Keys.T) && _prevKeyState.IsKeyUp(Keys.T))
            GameRoot.Instance.StartSceneTransition(new TavernScene());

        if (state.IsKeyDown(Keys.C) && _prevKeyState.IsKeyUp(Keys.C))
            GameRoot.Instance.StartSceneTransition(new CombatScene());

        _prevKeyState = state;
        base.Update(gameTime);
    }

    public override void Draw(GameTime gameTime)
    {
        if (_map == null || _pixelTexture == null || _font == null) return;

        var sb = Game.SpriteBatch;
        sb.Begin(samplerState: SamplerState.PointClamp);

        const int tileSize = 16;

        for (int x = 0; x < _map.Width; x++)
        for (int y = 0; y < _map.Height; y++)
        {
            var rect = new Rectangle(x * tileSize, y * tileSize, tileSize, tileSize);
            var color = GetTileColor(x, y);
            sb.Draw(_pixelTexture, rect, color);
        }

        var playerRect = new Rectangle(
            _playerPosition.X * tileSize,
            _playerPosition.Y * tileSize,
            tileSize, tileSize);
        sb.Draw(_pixelTexture, playerRect, Color.Cyan);

        _font.DrawText(sb, "冒险地图", new Vector2(660, 10), new Color(255, 215, 0));
        _fontSmall!.DrawText(sb, "WASD 移动 | C 进入战斗 | T 返回酒馆", new Vector2(660, 40), Color.White);
        _fontSmall.DrawText(sb, $"位置: ({_playerPosition.X}, {_playerPosition.Y})", new Vector2(660, 70), Color.LightGray);
        _fontSmall.DrawText(sb, $"移动次数: {_moveCount}", new Vector2(660, 90), Color.LightGray);

        sb.End();
        base.Draw(gameTime);
    }

    public override void End()
    {
        _pixelTexture?.Dispose();
        base.End();
    }

    private void UpdateFOV()
    {
        if (_map == null) return;
        _map.CalculateFOV(_playerPosition.X, _playerPosition.Y, 8);
    }

    private Color GetTileColor(int x, int y)
    {
        if (_map == null) return Color.Black;

        if (_map.IsVisible(x, y))
        {
            return _map.GetTileType(x, y) switch
            {
                TileType.Floor => new Color(60, 60, 80),
                TileType.Wall => new Color(30, 30, 50),
                TileType.Door => new Color(100, 80, 60),
                _ => new Color(40, 40, 60)
            };
        }

        if (_map.IsExplored(x, y))
        {
            return _map.GetTileType(x, y) == TileType.Wall
                ? new Color(15, 15, 25)
                : new Color(30, 30, 40);
        }

        return Color.Black;
    }
}
