namespace DndGame.Core;

/// <summary>
/// 服务注册管线 —— 按依赖顺序注册所有全局服务到 ServiceLocator。
/// 注册顺序至关重要：被依赖的服务必须先于依赖方注册。
/// 最终在所有服务就绪后调用 FinalizeRegistration() 冻结注册表。
/// </summary>
public static class ServiceRegistration
{
    /// <summary>
    /// 注册所有核心服务。按以下顺序执行：
    /// 0. IEventBus —— 系统间通信总线（所有其他服务的依赖）
    /// 1. IGameStateManager —— 全局状态与场景切换
    /// 后续 Sprint 将逐步追加更多服务（见下方注释）。
    /// </summary>
    public static void RegisterAll()
    {
        // 第 0 步：事件总线 —— 所有系统间解耦通信的基础
        ServiceLocator.Register<IEventBus>(new EventBus());

        // 第 1 步：游戏状态管理器 —— 场景标识与跨场景数据传递
        ServiceLocator.Register<IGameStateManager>(new GameStateManager());

        // ---- 以下服务将在后续 Sprint 中注册 ----
        // 第 2 步：IDataPersistence —— SQLite + JSON 数据持久化
        // 第 3 步：ILLMGateway —— LLM 请求入口
        // 第 4 步：IWorldStateManager —— 世界状态追踪
        // 第 5 步：IAudioManager —— BGM/SFX 管理
        // 第 6 步：IResourceCache —— 预加载资源缓存

        // 注意：FinalizeRegistration() 暂不调用，等待所有服务就绪后在 Sprint 7 冻结
    }
}
