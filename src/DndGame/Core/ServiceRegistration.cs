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
        ServiceLocator.Register<IEventBus>(new EventBus());
        ServiceLocator.Register<IGameStateManager>(new GameStateManager());

        // 字体服务 —— 所有场景共享的 FontStashSharp 字体
        var fontPath = Path.Combine(AppContext.BaseDirectory, "Content", "Fonts", "NotoSansCJKsc-Regular.ttf");
        ServiceLocator.Register<IFontService>(new FontService(fontPath));

        // ---- 以下服务将在后续 Sprint 中注册 ----
        // ILLMGateway, IDataPersistence, IWorldStateManager, IAudioManager, IResourceCache
    }
}
