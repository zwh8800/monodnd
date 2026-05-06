using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using GoRogue;
using DndGame.Core;
using DndGame.Systems.Combat;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace DndGame.Scenes;

/// <summary>
/// 冒险地图场景，负责地牢探索、角色移动、视野渲染和战斗触发。
/// </summary>
public class AdventureScene : Scene
{
    private GoRogueMapManager? _map;
    private DungeonGenerator? _generator;
    private Coord _playerPosition;
    private HashSet<Coord> _explored = new();
    private Texture2D? _pixelTexture;

    /// <summary>
    /// 初始化冒险场景：生成地牢、放置玩家。
    /// </summary>
    public override void Initialize()
    {
        _generator = new DungeonGenerator();
        _map = _generator.Generate(40, 30);

        // 找到起始位置
        var start = DungeonGenerator.FindStartPosition(_map);
        _playerPosition = start ?? new Coord(5, 5);

        // 创建单像素纹理用于绘制
        _pixelTexture = new Texture2D(Game.GraphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });

        // 计算初始视野
        UpdateFOV();

        base.Initialize();
    }

    /// <summary>
    /// 绘制地图：可见区域全亮、已探索区域灰色、未探索区域黑色。
    /// </summary>
    public override void Draw(GameTime gameTime)
    {
        if (_map == null || _pixelTexture == null) return;

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

        // 绘制玩家
        var playerRect = new Rectangle(
            _playerPosition.X * tileSize,
            _playerPosition.Y * tileSize,
            tileSize, tileSize);
        sb.Draw(_pixelTexture, playerRect, Color.Cyan);

        sb.End();

        base.Draw(gameTime);
    }

    /// <summary>
    /// 处理玩家移动输入。
    /// </summary>
    public override void Update(GameTime gameTime)
    {
        var state = Microsoft.Xna.Framework.Input.Keyboard.GetState();
        var dx = 0;
        var dy = 0;

        if (state.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.W)) dy = -1;
        if (state.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.S)) dy = 1;
        if (state.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.A)) dx = -1;
        if (state.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.D)) dx = 1;

        if ((dx != 0 || dy != 0) && _map != null)
        {
            var newX = _playerPosition.X + dx;
            var newY = _playerPosition.Y + dy;
            if (_map.IsWalkable(newX, newY))
            {
                _playerPosition = new Coord(newX, newY);
                UpdateFOV();
            }
        }

        base.Update(gameTime);
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
