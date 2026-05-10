namespace DndGame.Core;

/// <summary>
/// 服务注册管线 —— 按依赖顺序注册所有全局服务到 ServiceLocator。
/// 注册顺序至关重要：被依赖的服务必须先于依赖方注册。
/// 最终在所有服务就绪后调用 FinalizeRegistration() 冻结注册表。
///
/// 完整注册顺序（共 7 个核心服务 + 1 个辅助服务）：
/// 0. IEventBus           — 系统间通信总线（所有其他服务的依赖）
/// 1. IGameStateManager   — 全局状态与场景切换
///     IFontService       — 字体服务（辅助，非核心初始化顺序）
/// 2. IDataPersistence    — 存档持久化
/// 3. ILLMGateway         — LLM 集成入口
/// 4. IWorldStateManager  — 世界状态追踪
/// 5. IAudioManager       — 音频管理
/// 6. IResourceCache      — 资源缓存
/// </summary>
public static class ServiceRegistration
{
    /// <summary>
    /// 注册全部 7 个核心服务及其桩实现，然后冻结注册表。
    /// 桩实现将在后续 Sprint 中被完整实现替换。
    /// </summary>
    public static void RegisterAll()
    {
        // 0. 事件总线 —— 必须最先注册，所有其他服务依赖事件总线进行解耦通信
        ServiceLocator.Register<IEventBus>(new EventBus());

        // 1. 游戏状态管理器 —— 场景切换和全局状态追踪
        ServiceLocator.Register<IGameStateManager>(new GameStateManager());

        // 字体服务 —— 所有场景共享的 FontStashSharp 字体（辅助服务，独立于核心初始化顺序）
        var fontPath = Path.Combine(AppContext.BaseDirectory, "Content", "Fonts", "NotoSansCJKsc-Regular.ttf");
        ServiceLocator.Register<IFontService>(new FontService(fontPath));

        // 2. 数据持久化 —— 存档保存/加载/删除
        ServiceLocator.Register<IDataPersistence>(new StubDataPersistence());

        // 3. LLM 集成网关 —— Agent 调用、缓存和降级（离线模式桩）
        ServiceLocator.Register<ILLMGateway>(new StubLLMGateway());

        // 4. 世界状态管理器 —— 区域/势力/NPC/事件/冒险日志数据持有者
        ServiceLocator.Register<IWorldStateManager>(new StubWorldStateManager());

        // 5. 音频管理器 —— BGM/SFX 播放与控制
        ServiceLocator.Register<IAudioManager>(new StubAudioManager());

        // 6. 资源缓存 —— 纹理/字体/音频预加载与缓存
        ServiceLocator.Register<IResourceCache>(new StubResourceCache());

        // 冻结注册表，禁止后续注册
        ServiceLocator.FinalizeRegistration();
    }
}
