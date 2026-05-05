using DndGame.Core;
using DndGame.Scenes;

namespace DndGame;

/// <summary>
/// 应用程序入口点。
/// 包含 Main 方法，负责启动游戏实例并进入主循环。
/// </summary>
public static class Program
{
    /// <summary>
    /// 应用程序的主入口点。
    ///
    /// [STAThread] 特性标记此线程为单线程单元（Single-Threaded Apartment）。
    /// 这是 Windows  Forms / WPF / 老旧 COM 互操作的要求：
    /// MonoGame 内部调用某些 Win32 API（如对话框、剪贴板）时依赖 STA 模型，
    /// 缺少此特性可能导致运行时崩溃或剪贴板访问失败。
    ///
    /// 方法内部创建 GameRoot 实例并启动游戏主循环，
    /// 直到游戏窗口关闭后 Main 方法才返回。
    /// </summary>
    [STAThread]
    public static void Main()
    {
        // 创建 GameRoot 实例（继承自 MonoGame 的 Game 类）
        // using 语句确保 game 对象在退出作用域时自动调用 Dispose 释放资源
        using var game = new GameRoot();

        // 设置初始场景为主菜单，启动后立即切换到 MainMenuScene
        // StartSceneTransition 在下一个 Update 周期执行实际切换
        game.StartSceneTransition(new MainMenuScene());

        // 进入游戏主循环：每秒约 60 次调用 Update 和 Draw
        // 此方法会阻塞直到游戏窗口关闭
        game.Run();
    }
}
