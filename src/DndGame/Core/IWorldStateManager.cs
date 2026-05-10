namespace DndGame.Core;

/// <summary>
/// 世界状态管理器接口 — 持有 world state 内存快照，提供查询与写入接口。
/// 仅依赖 IEventBus 发布 WorldStateChangedEvent。
/// MVP 阶段数据全量载入内存（数据量小，无需分页/懒加载）。
/// </summary>
public interface IWorldStateManager
{
    // ── 区域状态查询与更新 ──

    /// <summary>查询指定区域的状态快照。regionId 不存在时返回 null。</summary>
    RegionState? QueryRegionState(string regionId);

    /// <summary>获取所有区域的只读列表。</summary>
    IReadOnlyList<RegionState> GetAllRegions();

    /// <summary>
    /// 更新区域状态。状态或威胁等级任一变更即视为有效更新，
    /// 自动更新 last_updated 时间戳并发布 WorldStateChangedEvent。
    /// </summary>
    void UpdateRegionState(string regionId, RegionStateEnum? newState, int? threatLevelDelta);

    // ── 势力关系 ──

    /// <summary>查询指定势力的关系快照。factionId 不存在时返回 null。</summary>
    FactionRelation? QueryFactionRelation(string factionId);

    /// <summary>
    /// 应用势力关系增量。叠加到 reputation_value（钳制到 [-100, 100]），
    /// 若增量导致 disposition 跨越阈值则自动更新 disposition。
    /// 若 factionId 不存在则创建新条目（初始 reputation_value = delta, 钳制）。
    /// </summary>
    void ApplyFactionDelta(string factionId, string factionName, int reputationDelta);

    // ── 世界事件日志 ──

    /// <summary>追加一条世界事件到日志。自动生成 event_id 和 timestamp。</summary>
    void RecordWorldEvent(EventTypeEnum type, string description,
        string? relatedAdventureId = null, string impact = "neutral");

    /// <summary>获取世界事件日志的只读列表（按时间倒序）。</summary>
    IReadOnlyList<WorldEvent> GetWorldEvents(int maxCount = 50);

    // ── 冒险日志墙 ──

    /// <summary>
    /// 追加一条冒险记录到冒险日志墙。
    /// MVP 仅记录核心字段：名称、结局、存活角色、摘要。
    /// </summary>
    void RecordAdventureLog(AdventureLogEntry entry);

    /// <summary>获取冒险日志墙的只读列表（按时间倒序）。</summary>
    IReadOnlyList<AdventureLogEntry> GetAdventureLog(int maxCount = 20);

    // ── NPC 状态 ──

    /// <summary>查询指定 NPC 的状态。npcId 不存在时返回 null。</summary>
    NpcState? QueryNpcState(string npcId);

    /// <summary>更新 NPC 状态。</summary>
    void UpdateNpcState(string npcId, NpcStatusEnum newStatus, string? location = null);

    // ── 全局快照 ──

    /// <summary>
    /// 获取完整世界状态快照（只读副本）。
    /// 用于存档序列化、冒险生成上下文注入。
    /// </summary>
    WorldStateSnapshot GetSnapshot();

    /// <summary>
    /// 从快照恢复世界状态（存档加载时调用）。
    /// 不触发 WorldStateChangedEvent（避免加载时副作用风暴）。
    /// </summary>
    void RestoreFromSnapshot(WorldStateSnapshot snapshot);
}
