namespace DndGame;

/// <summary>
/// 游戏入口包装类。
/// 由 MonoGame DesktopGL 模板自动生成，作为项目启动对象。
/// 实际的游戏逻辑由 <see cref="Core.GameRoot"/> 管理，此处仅负责触发其初始化。
/// </summary>
public class Game1 : Microsoft.Xna.Framework.Game
{
    /// <summary>
    /// 初始化 Game1 实例。
    /// 构造时立即创建 <see cref="Core.GameRoot"/> 单例，
    /// GameRoot 负责后续的图形设备管理、场景系统和游戏主循环。
    /// </summary>
    public Game1()
    {
        // 触发 GameRoot 的构造函数，完成全局状态初始化
        // GameRoot 构造时会注册自身为全局单例、设置图形设备参数和窗口标题
        new Core.GameRoot();
    }
}
