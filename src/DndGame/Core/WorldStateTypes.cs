namespace DndGame.Core;

// ── 枚举定义 ──

/// <summary>区域状态枚举。</summary>
public enum RegionStateEnum
{
    /// <summary>安全 — 区域无威胁。</summary>
    Safe,
    /// <summary>受威胁 — 区域存在已知危险。</summary>
    Threatened,
    /// <summary>沦陷 — 区域已被敌对势力控制。</summary>
    Fallen,
    /// <summary>解放 — 区域已被玩家收复。</summary>
    Liberated,
    /// <summary>毁灭 — 区域已不可恢复。</summary>
    Destroyed
}

/// <summary>势力好感度枚举。</summary>
public enum DispositionEnum
{
    /// <summary>铁杆盟友。</summary>
    Ally,
    /// <summary>友好。</summary>
    Friendly,
    /// <summary>中立。</summary>
    Neutral,
    /// <summary>敌对。</summary>
    Hostile,
    /// <summary>死敌。</summary>
    Enemy
}

/// <summary>世界事件类型枚举。</summary>
public enum EventTypeEnum
{
    /// <summary>冒险成功。</summary>
    AdventureSuccess,
    /// <summary>冒险失败。</summary>
    AdventureFailure,
    /// <summary>世界事件（非冒险触发）。</summary>
    WorldEvent,
    /// <summary>势力关系变更。</summary>
    FactionChange,
    /// <summary>NPC 死亡。</summary>
    NpcDeath
}

/// <summary>冒险结果枚举。</summary>
public enum ResultEnum
{
    /// <summary>冒险成功。</summary>
    Success,
    /// <summary>冒险失败。</summary>
    Failure,
    /// <summary>部分成功。</summary>
    Partial
}

/// <summary>NPC 状态枚举。</summary>
public enum NpcStatusEnum
{
    /// <summary>存活。</summary>
    Alive,
    /// <summary>死亡。</summary>
    Dead,
    /// <summary>失踪。</summary>
    Missing,
    /// <summary>盟友。</summary>
    Allied,
    /// <summary>敌对。</summary>
    Hostile,
    /// <summary>中立。</summary>
    Neutral
}

// ── 数据记录（record，不可变） ──

/// <summary>区域状态快照。</summary>
public record RegionState
{
    /// <summary>区域唯一标识。</summary>
    public string RegionId { get; init; } = "";

    /// <summary>中文显示名称。</summary>
    public string Name { get; init; } = "";

    /// <summary>区域当前状态。</summary>
    public RegionStateEnum State { get; init; } = RegionStateEnum.Safe;

    /// <summary>威胁等级 [0, 10]。</summary>
    public int ThreatLevel { get; init; }

    /// <summary>区域文本描述。</summary>
    public string Description { get; init; } = "";

    /// <summary>已知地点列表。</summary>
    public IReadOnlyList<string> KeyLocations { get; init; } = [];

    /// <summary>控制势力 ID。</summary>
    public string ControlledBy { get; init; } = "";

    /// <summary>最后更新时间。</summary>
    public DateTime LastUpdated { get; init; }
}

/// <summary>势力关系快照。</summary>
public record FactionRelation
{
    /// <summary>势力唯一标识。</summary>
    public string FactionId { get; init; } = "";

    /// <summary>势力中文名称。</summary>
    public string Name { get; init; } = "";

    /// <summary>势力好感度。</summary>
    public DispositionEnum Disposition { get; init; } = DispositionEnum.Neutral;

    /// <summary>声望值 [-100, 100]。</summary>
    public int ReputationValue { get; init; }

    /// <summary>势力描述。</summary>
    public string Description { get; init; } = "";

    /// <summary>势力领袖名称。</summary>
    public string Leader { get; init; } = "";

    /// <summary>势力目标列表。</summary>
    public IReadOnlyList<string> Goals { get; init; } = [];
}

/// <summary>世界事件记录。</summary>
public record WorldEvent
{
    /// <summary>事件唯一标识。</summary>
    public string EventId { get; init; } = "";

    /// <summary>事件类型。</summary>
    public EventTypeEnum Type { get; init; }

    /// <summary>简体中文事件摘要。</summary>
    public string Description { get; init; } = "";

    /// <summary>事件发生时间。</summary>
    public DateTime Timestamp { get; init; }

    /// <summary>关联冒险 ID（可为 null）。</summary>
    public string? RelatedAdventureId { get; init; }

    /// <summary>影响分类：positive / negative / neutral。</summary>
    public string Impact { get; init; } = "neutral";
}

/// <summary>冒险日志墙条目。</summary>
public record AdventureLogEntry
{
    /// <summary>冒险唯一标识。</summary>
    public string AdventureId { get; init; } = "";

    /// <summary>冒险标题（简体中文）。</summary>
    public string Title { get; init; } = "";

    /// <summary>冒险结局。</summary>
    public ResultEnum Result { get; init; }

    /// <summary>失败时严重程度。</summary>
    public string? Severity { get; init; }

    /// <summary>冒险主题。</summary>
    public string Theme { get; init; } = "";

    /// <summary>冒险时间戳。</summary>
    public DateTime Timestamp { get; init; }

    /// <summary>参战角色 ID 列表。</summary>
    public IReadOnlyList<string> PartyMembers { get; init; } = [];

    /// <summary>存活角色 ID 列表。</summary>
    public IReadOnlyList<string> Survivors { get; init; } = [];

    /// <summary>冒险摘要（1-2 句简体中文）。</summary>
    public string Summary { get; init; } = "";
}

/// <summary>NPC 状态快照。</summary>
public record NpcState
{
    /// <summary>NPC 唯一标识。</summary>
    public string NpcId { get; init; } = "";

    /// <summary>NPC 名称。</summary>
    public string Name { get; init; } = "";

    /// <summary>NPC 当前状态。</summary>
    public NpcStatusEnum Status { get; init; }

    /// <summary>NPC 角色定位。</summary>
    public string Role { get; init; } = "";

    /// <summary>NPC 当前位置。</summary>
    public string Location { get; init; } = "";

    /// <summary>NPC 对玩家的态度。</summary>
    public string DispositionToPlayer { get; init; } = "";

    /// <summary>最后目击时间。</summary>
    public DateTime? LastSeen { get; init; }

    /// <summary>备注信息。</summary>
    public string Notes { get; init; } = "";
}

/// <summary>世界状态快照 — 纯数据 DTO，用于序列化和跨系统传输。</summary>
public record WorldStateSnapshot
{
    /// <summary>快照版本号。</summary>
    public string Version { get; init; } = "1.0";

    /// <summary>所有区域状态（键为 region_id）。</summary>
    public Dictionary<string, RegionState> Regions { get; init; } = new();

    /// <summary>所有势力关系（键为 faction_id）。</summary>
    public Dictionary<string, FactionRelation> Factions { get; init; } = new();

    /// <summary>所有 NPC 状态（键为 npc_id）。</summary>
    public Dictionary<string, NpcState> Npcs { get; init; } = new();

    /// <summary>世界事件日志（按时间排序）。</summary>
    public List<WorldEvent> Events { get; init; } = new();

    /// <summary>冒险日志墙历史。</summary>
    public List<AdventureLogEntry> AdventureHistory { get; init; } = new();
}

/// <summary>世界状态变更事件 — 通过 IEventBus 发布。</summary>
public record WorldStateChangedEvent
{
    /// <summary>变更类型：region / faction / event / adventure_log / npc。</summary>
    public string ChangeType { get; init; } = "";

    /// <summary>变更的实体 ID。</summary>
    public string EntityId { get; init; } = "";

    /// <summary>简体中文变更描述。</summary>
    public string Description { get; init; } = "";

    /// <summary>变更发生时间。</summary>
    public DateTime Timestamp { get; init; }
}
