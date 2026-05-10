namespace DndGame.Core;

/// <summary>
/// 数据持久化服务桩实现。当前不支持实际持久化操作，所有方法均抛出异常。
/// </summary>
internal sealed class StubDataPersistence : IDataPersistence
{
    public Task SaveAsync(int slot, object gameState, CancellationToken ct = default)
        => throw new NotImplementedException("数据持久化服务尚未实现——SaveAsync 不可调用");

    public Task<object?> LoadAsync(int slot, CancellationToken ct = default)
        => throw new NotImplementedException("数据持久化服务尚未实现——LoadAsync 不可调用");

    public Task DeleteAsync(int slot, CancellationToken ct = default)
        => throw new NotImplementedException("数据持久化服务尚未实现——DeleteAsync 不可调用");

    public IReadOnlyList<int> GetAvailableSlots()
        => throw new NotImplementedException("数据持久化服务尚未实现——GetAvailableSlots 不可调用");
}

/// <summary>
/// LLM 网关服务桩实现。离线模式下所有 Agent 不可用。
/// </summary>
internal sealed class StubLLMGateway : ILLMGateway
{
    public Task<T?> CallAgentAsync<T>(string agentId, object context, CancellationToken ct = default) where T : class
        => Task.FromResult<T?>(null);

    public bool IsAgentReady(string agentId) => false;

    public IReadOnlyList<string> GetRegisteredAgentIds() => [];
}

/// <summary>
/// 世界状态管理器桩实现。返回空/默认值，确保下游系统不会因调用而崩溃。
/// 5 个下游系统（冒险生成、失败与成长、酒馆、存档、LLM 网关）依赖此桩的安全默认值。
/// </summary>
internal sealed class StubWorldStateManager : IWorldStateManager
{
    // ── 区域状态 ──
    public RegionState? QueryRegionState(string regionId) => null;
    public IReadOnlyList<RegionState> GetAllRegions() => [];
    public void UpdateRegionState(string regionId, RegionStateEnum? newState, int? threatLevelDelta) { }

    // ── 势力关系 ──
    public FactionRelation? QueryFactionRelation(string factionId) => null;
    public void ApplyFactionDelta(string factionId, string factionName, int reputationDelta) { }

    // ── 世界事件日志 ──
    public void RecordWorldEvent(EventTypeEnum type, string description,
        string? relatedAdventureId = null, string impact = "neutral") { }
    public IReadOnlyList<WorldEvent> GetWorldEvents(int maxCount = 50) => [];

    // ── 冒险日志墙 ──
    public void RecordAdventureLog(AdventureLogEntry entry) { }
    public IReadOnlyList<AdventureLogEntry> GetAdventureLog(int maxCount = 20) => [];

    // ── NPC 状态 ──
    public NpcState? QueryNpcState(string npcId) => null;
    public void UpdateNpcState(string npcId, NpcStatusEnum newStatus, string? location = null) { }

    // ── 全局快照 ──
    public WorldStateSnapshot GetSnapshot() => new();
    public void RestoreFromSnapshot(WorldStateSnapshot snapshot) { }
}

/// <summary>
/// 音频管理器桩实现。当前不支持音频播放操作。
/// </summary>
internal sealed class StubAudioManager : IAudioManager
{
    public void PlayBGM(string name, float volume = 1.0f)
        => throw new NotImplementedException("音频管理器尚未实现——PlayBGM 不可调用");

    public void StopBGM(int fadeDurationMs = 500)
        => throw new NotImplementedException("音频管理器尚未实现——StopBGM 不可调用");

    public void PlaySFX(string name, float volume = 1.0f)
        => throw new NotImplementedException("音频管理器尚未实现——PlaySFX 不可调用");

    public void SetMasterVolume(float volume)
        => throw new NotImplementedException("音频管理器尚未实现——SetMasterVolume 不可调用");

    public float GetMasterVolume()
        => throw new NotImplementedException("音频管理器尚未实现——GetMasterVolume 不可调用");
}

/// <summary>
/// 资源缓存桩实现。当前不支持资源预加载和缓存操作。
/// </summary>
internal sealed class StubResourceCache : IResourceCache
{
    public Task PreloadAsync(string category, CancellationToken ct = default)
        => throw new NotImplementedException("资源缓存服务尚未实现——PreloadAsync 不可调用");

    public T? Get<T>(string key) where T : class
        => throw new NotImplementedException("资源缓存服务尚未实现——Get 不可调用");

    public void Evict(string key)
        => throw new NotImplementedException("资源缓存服务尚未实现——Evict 不可调用");

    public void Clear()
        => throw new NotImplementedException("资源缓存服务尚未实现——Clear 不可调用");

    public long GetCacheSizeBytes()
        => throw new NotImplementedException("资源缓存服务尚未实现——GetCacheSizeBytes 不可调用");
}
