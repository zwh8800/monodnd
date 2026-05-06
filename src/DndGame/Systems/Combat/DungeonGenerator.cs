using GoRogue;
using GoRogue.MapViews;
using GoRogue.MapGeneration;

namespace DndGame.Systems.Combat;

/// <summary>
/// 地牢生成器，使用 GoRogue 2.6.4 QuickGenerators 生成战斗地图。
/// </summary>
public class DungeonGenerator
{
    /// <summary>
    /// 生成地牢地图，返回 GoRogueMapManager 实例。
    /// </summary>
    public GoRogueMapManager Generate(int width, int height, string theme = "dungeon")
    {
        var map = new GoRogueMapManager(width, height);

        // 使用 GoRogue 生成房间+迷宫布局
        var boolMap = new ArrayMap<bool>(width, height);
        QuickGenerators.GenerateDungeonMazeMap(boolMap, 15, 20, 3, 6);

        // 将 bool map 转换为 TileType
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            if (boolMap[x, y])
                map.SetTile(x, y, TileType.Floor, walkable: true, transparent: true);
            else
                map.SetTile(x, y, TileType.Wall, walkable: false, transparent: false);
        }

        return map;
    }

    /// <summary>
    /// 查找地图中的可通行起始点。
    /// </summary>
    public static Coord? FindStartPosition(GoRogueMapManager map)
    {
        for (int x = 0; x < map.Width; x++)
        for (int y = 0; y < map.Height; y++)
            if (map.IsWalkable(x, y))
                return new Coord(x, y);
        return null;
    }

    /// <summary>
    /// 查找地图中的可通行终点（离起点最远的可通行点）。
    /// </summary>
    public static Coord? FindEndPosition(GoRogueMapManager map, Coord start)
    {
        Coord? farthest = null;
        int maxDist = 0;

        for (int x = 0; x < map.Width; x++)
        for (int y = 0; y < map.Height; y++)
        {
            if (!map.IsWalkable(x, y)) continue;
            var dist = Math.Abs(x - start.X) + Math.Abs(y - start.Y);
            if (dist > maxDist)
            {
                maxDist = dist;
                farthest = new Coord(x, y);
            }
        }

        return farthest;
    }
}
