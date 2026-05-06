using GoRogue;
using GoRogue.MapViews;
using GoRogue.Pathing;

namespace DndGame.Systems.Combat;

/// <summary>
/// GoRogue 2.6.4 地图管理器，封装 ArrayMap 用于战斗地图数据。
/// 处理通行性、透明性（用于 FOV）、地形类型、视野计算和 A* 寻路。
/// </summary>
public class GoRogueMapManager
{
    private readonly ArrayMap<bool> _walkabilityMap;
    private readonly ArrayMap<bool> _transparencyMap;
    private readonly TileType[,] _tileTypes;
    private FOV? _fov;
    private AStar? _pathfinder;
    private readonly HashSet<Coord> _explored = new();

    public int Width { get; }
    public int Height { get; }

    public GoRogueMapManager(int width, int height)
    {
        Width = width;
        Height = height;
        _walkabilityMap = new ArrayMap<bool>(width, height);
        _transparencyMap = new ArrayMap<bool>(width, height);
        _tileTypes = new TileType[width, height];

        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            _walkabilityMap[x, y] = true;
            _transparencyMap[x, y] = true;
            _tileTypes[x, y] = TileType.Floor;
        }
    }

    public bool IsWalkable(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return false;
        return _walkabilityMap[x, y];
    }

    public bool IsTransparent(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return false;
        return _transparencyMap[x, y];
    }

    public TileType GetTileType(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return TileType.Wall;
        return _tileTypes[x, y];
    }

    public void SetTile(int x, int y, TileType type, bool walkable, bool transparent)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return;
        _tileTypes[x, y] = type;
        _walkabilityMap[x, y] = walkable;
        _transparencyMap[x, y] = transparent;
    }

    /// <summary>
    /// 计算视野范围，返回当前可见坐标。已探索区域会被记录。
    /// </summary>
    public IEnumerable<Coord> CalculateFOV(int originX, int originY, int radius)
    {
        _fov ??= new FOV(_transparencyMap);
        _fov.Calculate(originX, originY, radius);
        foreach (var coord in _fov.CurrentFOV)
            _explored.Add(coord);
        return _fov.CurrentFOV;
    }

    public bool IsVisible(int x, int y)
    {
        if (_fov == null || x < 0 || x >= Width || y < 0 || y >= Height) return false;
        return _fov.BooleanFOV[x, y];
    }

    public bool IsExplored(int x, int y) => _explored.Contains(new Coord(x, y));

    public IReadOnlySet<Coord> GetExplored() => _explored;

    /// <summary>
    /// A* 寻路，返回路径坐标（不含起点）。不可达时返回空列表。
    /// </summary>
    public IEnumerable<Coord> FindPath(Coord start, Coord end)
    {
        _pathfinder ??= new AStar(_walkabilityMap, Distance.MANHATTAN);
        var path = _pathfinder.ShortestPath(start, end);
        return path?.Steps ?? Enumerable.Empty<Coord>();
    }

    public bool IsReachable(Coord start, Coord end)
    {
        _pathfinder ??= new AStar(_walkabilityMap, Distance.MANHATTAN);
        return _pathfinder.ShortestPath(start, end) != null;
    }

    public IMapView<bool> GetWalkabilityMap() => _walkabilityMap;
    public IMapView<bool> GetTransparencyMap() => _transparencyMap;

    public static Coord ToCoord(int x, int y) => new Coord(x, y);
    public static (int X, int Y) FromCoord(Coord c) => (c.X, c.Y);
}
