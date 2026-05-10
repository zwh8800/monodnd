# Quick Design Spec: 世界状态系统 (World State System)

**Type**: New Small System
**Scope**: MVP — 区域状态追踪、势力关系增量、世界事件日志、冒险日志墙。服务 Pillar 3（持续演进的世界）的最小可行表达。
**Date**: 2026-05-10
**Authoritative Data Model**: `design/gdd/08-failure-growth.md` §9.1
**Systems Index Ref**: #9 — Core Layer, depends on EventBus only
**Estimated Implementation**: 3-5 小时（~250 行 + 15-20 测试用例）

---

## 1. 概述

世界状态系统是 Pillar 3（**持续演进的世界**）的运行时数据持有者和变更接口。它维护一个纯数据的世界快照——区域状态、势力关系、NPC 状态、世界事件日志和冒险历史——并通过 `IEventBus` 发布变更事件，让依赖系统（冒险生成、酒馆、失败与成长、存档）读取和响应世界变化。

### 1.1 在游戏循环中的位置

```
冒险前                          冒险中                          冒险后
   │                              │                              │
   ▼                              │                              ▼
┌──────────────────────┐          │          ┌──────────────────────────┐
│ 冒险生成系统读取       │          │          │ 失败与成长系统写入        │
│ - 区域威胁等级→难度    │          │          │ - 更新区域状态            │
│ - 势力关系→敌人/盟友   │          │          │ - 记录势力关系增量        │
│ - 已完成冒险→避免重复  │          │          │ - 追加世界事件            │
└──────────────────────┘          │          │ - 追加冒险日志墙条目      │
                                   │          └──────────┬───────────────┘
                                   │                      │
                                   │                      ▼
                                   │          ┌──────────────────────────┐
                                   │          │ IWorldStateManager        │
                                   │          │ · 内存中持有 WorldState   │
                                   │          │ · 发布 WorldStateChanged   │
                                   │          │    Event 到 IEventBus     │
                                   │          └──────────┬───────────────┘
                                   │                      │
                                   │         ┌────────────┴──────────────┐
                                   │         ▼                           ▼
                                   │  ┌──────────────┐          ┌──────────────┐
                                   │  │ 存档系统      │          │ 酒馆系统      │
                                   │  │ 序列化到磁盘  │          │ 刷新冒险日志墙 │
                                   │  └──────────────┘          └──────────────┘
```

### 1.2 核心约束

> **LLM = 皮肤层**：世界状态的所有数值由程序确定。LLM 仅读取世界状态快照作为叙事上下文（"灰谷镇沦陷了"），不决定世界状态的值。

> **数据模型权威源**：`08-failure-growth.md` §9.1 的 JSON Schema 是世界状态数据模型的**单一权威定义**。本规格仅定义接口契约，不重复定义数据模型。

---

## 2. 数据模型（引用声明）

以下数据结构以 `08-failure-growth.md` §9.1 为准，此处仅列出接口方法使用的关键类型摘要。

### 2.1 区域状态 (RegionState)

```
RegionState {
    region_id:    string              // 唯一标识 (如 "gray_valley")
    name:         string              // 中文显示名 (如 "灰谷镇")
    state:        RegionStateEnum     // safe | threatened | fallen | liberated | destroyed
    threat_level: int [0, 10]        // 0=安全, 10=灾难级威胁
    description:  string              // 文本描述
    key_locations: string[]           // 已知地点列表
    controlled_by: string             // 控制势力 ID
    last_updated:  DateTime
}
```

### 2.2 势力关系 (FactionRelation)

```
FactionRelation {
    faction_id:        string                  // 唯一标识 (如 "undead_legion")
    name:              string                  // 中文名 (如 "亡灵军团")
    disposition:       DispositionEnum         // ally | friendly | neutral | hostile | enemy
    reputation_value:  int [-100, 100]         // -100=死敌, 100=铁杆盟友
    description:       string
    leader:            string
    goals:             string[]
}
```

### 2.3 世界事件 (WorldEvent)

```
WorldEvent {
    event_id:              string
    type:                  EventTypeEnum  // adventure_success | adventure_failure
                                          // | world_event | faction_change | npc_death
    description:           string         // 简体中文摘要
    timestamp:             DateTime
    related_adventure_id:  string?
    impact:                string         // 影响分类: "positive" | "negative" | "neutral"
}
```

### 2.4 冒险日志墙条目 (AdventureLogEntry)

```
AdventureLogEntry {
    adventure_id:   string
    title:          string              // 冒险标题 (简体中文)
    result:         ResultEnum          // success | failure | partial
    severity:       string?             // minor | moderate | severe | catastrophic (失败时)
    theme:          string              // 冒险主题
    timestamp:      DateTime
    party_members:  string[]            // 参战角色 ID 列表
    survivors:      string[]            // 存活角色 ID 列表（MVP 冒险日志墙核心字段）
    summary:        string              // 1-2 句摘要 (简体中文，LLM 生成或模板降级)
}
```

### 2.5 NPC 状态 (NpcState)

```
NpcState {
    npc_id:               string
    name:                 string
    status:               NpcStatusEnum  // alive | dead | missing | allied | hostile | neutral
    role:                 string
    location:             string
    disposition_to_player: string
    last_seen:            DateTime?
    notes:                string
}
```

---

## 3. 接口契约

### 3.1 IWorldStateManager

```csharp
/// <summary>
/// 世界状态管理器 — 持有 WorldState 内存快照，提供查询/写入接口。
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
```

### 3.2 WorldStateSnapshot（序列化 DTO）

```csharp
/// <summary>世界状态快照 — 纯数据 DTO，用于序列化和跨系统传输。</summary>
public record WorldStateSnapshot
{
    public string Version { get; init; } = "1.0";
    public Dictionary<string, RegionState> Regions { get; init; } = new();
    public Dictionary<string, FactionRelation> Factions { get; init; } = new();
    public Dictionary<string, NpcState> Npcs { get; init; } = new();
    public List<WorldEvent> Events { get; init; } = new();
    public List<AdventureLogEntry> AdventureHistory { get; init; } = new();
}
```

### 3.3 WorldStateChangedEvent

```csharp
/// <summary>世界状态变更事件 — 通过 IEventBus 发布。</summary>
public record WorldStateChangedEvent
{
    public string ChangeType { get; init; }         // "region" | "faction" | "event" | "adventure_log" | "npc"
    public string EntityId { get; init; }           // 变更的实体 ID（regionId/factionId/npcId/adventureId）
    public string Description { get; init; }        // 简体中文变更描述
    public DateTime Timestamp { get; init; }
}
```

---

## 4. 势力关系阈值规则

势力 disposition 由 `reputation_value` 自动计算，阈值规则如下：

| reputation_value 范围 | disposition | 说明 |
|:---------------------:|-------------|------|
| 80 ~ 100 | ally | 铁杆盟友 |
| 30 ~ 79 | friendly | 友好 |
| -29 ~ 29 | neutral | 中立 |
| -79 ~ -30 | hostile | 敌对 |
| -100 ~ -80 | enemy | 死敌 |

**增量规则**：
- `ApplyFactionDelta` 的 `reputationDelta` 叠加到当前值后钳制到 [-100, 100]
- disposition 在叠加后根据新 reputation_value 自动重算
- disposition 跨越阈值时发布 `WorldStateChangedEvent`（如 neutral→hostile）

---

## 5. 依赖关系

### 5.1 上游依赖（世界状态系统依赖谁）

| 系统 | 依赖内容 | 状态 |
|------|----------|:----:|
| **事件总线** (IEventBus) | 发布 `WorldStateChangedEvent` | ✅ 已实现 |

### 5.2 下游依赖（谁依赖世界状态系统）

| 系统 | 依赖内容 | 状态 |
|------|----------|:----:|
| **冒险生成系统** (#14) | 读取区域威胁等级、势力关系、已完成冒险列表 → 影响难度/敌人/主题选择 | ✅ GDD Approved |
| **失败与成长系统** (#17) | 写入区域状态变更、势力关系增量、世界事件、冒险日志墙条目 | ✅ GDD Approved |
| **酒馆系统** (#16) | 读取冒险日志墙 → 渲染英雄之壁 UI | ✅ GDD Approved |
| **存档系统** (#18) | 通过 `GetSnapshot()` / `RestoreFromSnapshot()` 持久化/恢复 | ✅ Quick Spec Done |
| **LLM 集成网关** (#10) | 读取世界状态快照 → 注入编剧 Agent/DM Agent 上下文 | ✅ GDD Approved |

---

## 6. MVP 范围

### 6.1 MVP 包含

| 功能 | 规模 | 说明 |
|------|:----:|------|
| 区域追踪 | 5-10 个区域 | 硬编码初始区域（灰谷镇、暗影森林、亡灵荒原等），5 种状态轮转 |
| 势力关系 | 3-5 个势力 | 简单增量（+/- N），disposition 自动阈值判定，无复杂行为逻辑 |
| 世界事件日志 | 每冒险 1-3 条 | 追加型日志，仅存储不消费（Phase 2+ 消费事件链） |
| 冒险日志墙 | 每冒险 1 条 | 记录名称/结局/存活角色/摘要。MVP 下所有冒险均为短冒险 |
| NPC 状态 | 按需记录 | 仅记录 key_npcs 中的关键 NPC（由冒险蓝图定义） |

### 6.2 MVP 初始世界状态（硬编码种子数据）

```json
{
  "regions": {
    "gray_valley":       { "state": "threatened", "threat_level": 5 },
    "shadow_forest":     { "state": "threatened", "threat_level": 7 },
    "undead_wasteland":  { "state": "fallen",     "threat_level": 9 },
    "iron_mountains":    { "state": "safe",       "threat_level": 2 },
    "river_trade_route": { "state": "safe",       "threat_level": 3 }
  },
  "factions": {
    "undead_legion":  { "disposition": "enemy",  "reputation_value": -80 },
    "merchants_guild": { "disposition": "neutral", "reputation_value": 10 },
    "forest_elves":    { "disposition": "neutral", "reputation_value": 0 }
  },
  "npcs": {},
  "events": [],
  "adventure_history": []
}
```

### 6.3 MVP 明确排除

| 功能 | 排除原因 |
|------|----------|
| 完整势力模拟（AI 决策、扩张、战争） | 属于 "full faction simulation" — 不在 MVP 范围 |
| 动态经济（物价随区域状态波动） | 属于 "dynamic economy" — 不在 MVP 范围 |
| 领土控制（势力占领区域可视化地图） | 属于 "territory control" — 不在 MVP 范围 |
| 复杂事件链（事件 A 触发事件 B 触发事件 C） | 属于 "complex event chains" — 不在 MVP 范围 |
| 世界状态退化/正反馈阻尼 | MVP 允许简单叠加，不检查反馈循环 |
| NPC 日常模拟（NPC 独立于玩家行动） | NPC 状态仅在冒险结果中被动更新 |

---

## 7. 实现规范

### 7.1 类结构

```
src/DndGame/Systems/WorldState/
├── IWorldStateManager.cs        // 接口定义
├── WorldStateManager.cs         // 实现类（内存中持有 WorldStateSnapshot）
├── WorldStateSnapshot.cs        // 快照 record（纯数据 DTO）
├── WorldStateChangedEvent.cs    // 事件 record
├── RegionState.cs               // 区域状态 record
├── FactionRelation.cs           // 势力关系 record
├── WorldEvent.cs                // 世界事件 record
├── AdventureLogEntry.cs         // 冒险日志墙条目 record
├── NpcState.cs                  // NPC 状态 record
└── Enums.cs                     // RegionStateEnum, DispositionEnum, EventTypeEnum 等
```

### 7.2 关键实现约束

- **纯数据系统**：`WorldStateManager` 不包含任何 MonoGame/渲染逻辑，可在无图形设备的测试环境中完全测试
- **线程安全**：MVP 单线程游戏，不加锁。若未来引入后台存档线程，需对 `WorldStateSnapshot` 做写入时复制
- **事件发布粒度**：每次 `UpdateRegionState` / `ApplyFactionDelta` / `RecordWorldEvent` / `RecordAdventureLog` 调用发布一条独立的 `WorldStateChangedEvent`。不批量发布
- **初始化**：`WorldStateManager` 在 `ServiceLocator` 初始化阶段（第 4 位，在 ILLMGateway 之前）创建，加载硬编码种子数据
- **存档恢复**：`RestoreFromSnapshot` 用于存档加载——完全替换内存状态，不触发事件
- **错误处理**：`Query*` 方法对不存在的 ID 返回 `null`（不抛异常）。`Update*` 方法对不存在的 ID 静默创建（如 `UpdateRegionState` 对未知 region 创建新条目）

---

## 8. 测试规格

所有逻辑为纯数据操作，**无需 MonoGame 即可完全单元测试**。

### 8.1 单元测试用例

```
TEST 1: QueryRegionState 存在区域 → 返回正确 RegionState
  给定: 内存中有 gray_valley (state=threatened, threat_level=5)
  当:   QueryRegionState("gray_valley")
  则:   返回 RegionState { state=threatened, threat_level=5 }

TEST 2: QueryRegionState 不存在区域 → 返回 null
  给定: 内存中无 "atlantis"
  当:   QueryRegionState("atlantis")
  则:   返回 null

TEST 3: UpdateRegionState 变更状态 → 发布 WorldStateChangedEvent
  给定: gray_valley state=threatened
  当:   UpdateRegionState("gray_valley", newState: safe, threatLevelDelta: -3)
  则:   state 变为 safe, threat_level 变为 2
  且:   IEventBus 收到 WorldStateChangedEvent { ChangeType="region", EntityId="gray_valley" }

TEST 4: UpdateRegionState 仅变更 threat_level → 发布事件
  给定: gray_valley threat_level=5
  当:   UpdateRegionState("gray_valley", newState: null, threatLevelDelta: +2)
  则:   state 不变, threat_level 变为 7, 事件发布

TEST 5: ApplyFactionDelta 正增量 → reputation 增加
  给定: merchants_guild reputation=10 (neutral)
  当:   ApplyFactionDelta("merchants_guild", "商人公会", +25)
  则:   reputation=35, disposition=friendly, 事件发布

TEST 6: ApplyFactionDelta 导致 disposition 跨越阈值
  给定: merchants_guild reputation=-30 (hostile)
  当:   ApplyFactionDelta("merchants_guild", "商人公会", +5)
  则:   reputation=-25, disposition=neutral (跨越 -29→-30 阈值)

TEST 7: ApplyFactionDelta 钳制到 [-100, 100]
  给定: merchants_guild reputation=95
  当:   ApplyFactionDelta("merchants_guild", "商人公会", +20)
  则:   reputation=100 (钳制), 非 115

TEST 8: ApplyFactionDelta 不存在的势力 → 自动创建
  给定: 内存中无 "dragon_cult"
  当:   ApplyFactionDelta("dragon_cult", "龙教", -40)
  则:   创建 FactionRelation { faction_id="dragon_cult", reputation=-40, disposition=hostile }

TEST 9: RecordWorldEvent 追加事件
  给定: 空事件日志
  当:   RecordWorldEvent(adventure_success, "灰谷镇被成功收复", adv_001, "positive")
  则:   events 列表包含 1 条事件, event_id 自动生成, timestamp 为当前时间

TEST 10: RecordAdventureLog 追加冒险日志
  给定: 空冒险历史
  当:   RecordAdventureLog(new AdventureLogEntry { title="兽人突袭", result=success, survivors=[...], ... })
  则:   adventure_history 列表包含 1 条记录

TEST 11: GetAdventureLog 返回倒序
  给定: 3 条冒险历史 (adv_001→adv_002→adv_003)
  当:   GetAdventureLog(maxCount=3)
  则:   返回顺序为 adv_003, adv_002, adv_001

TEST 12: GetSnapshot 返回深拷贝
  给定: WorldStateManager 内存中有 gray_valley
  当:   var snap = GetSnapshot(); snap.Regions["gray_valley"].state = destroyed
  则:   WorldStateManager 内存中的 gray_valley.state 仍为原始值 (快照是副本)

TEST 13: RestoreFromSnapshot 完全替换状态
  给定: 内存中有 5 个区域
  当:   RestoreFromSnapshot(new_snapshot { Regions={ "atlantis": ... } })
  则:   内存中仅有 atlantis 1 个区域

TEST 14: RestoreFromSnapshot 不触发事件
  给定: 任意内存状态
  当:   RestoreFromSnapshot(loaded_snapshot)
  则:   IEventBus 上未收到任何 WorldStateChangedEvent
```

---

## 9. 连接点：世界状态如何被上游系统消费

| 上游系统 | 消费方法 | 使用场景 |
|----------|----------|----------|
| 冒险生成 (#14) | `GetSnapshot()` | 将世界状态快照注入编剧 Agent 上下文（区域威胁等级→难度、势力关系→敌人/盟友选择） |
| 失败与成长 (#17) | `UpdateRegionState()`, `ApplyFactionDelta()`, `RecordWorldEvent()`, `RecordAdventureLog()` | 冒险结算管线 Step 6（世界状态正向/负向变化） |
| 酒馆 (#16) | `GetAdventureLog()` | 渲染英雄之壁 UI（冒险日志墙） |
| 存档 (#18) | `GetSnapshot()`, `RestoreFromSnapshot()` | 序列化/反序列化世界状态到磁盘 |
| LLM 网关 (#10) | `GetSnapshot()` | DM Agent 叙事上下文（"亡灵势力已经扩张到灰谷镇…"） |

---

## 10. 与现有系统的一致性声明

- 数据模型以 `08-failure-growth.md` §9.1 为**单一权威源**。若本规格与 failure-growth §9.1 冲突，以 failure-growth 为准
- 结算管线由 failure-growth 拥有——世界状态系统只提供写入接口，不拥有结算逻辑
- 冒险日志墙 MVP 简化范围（名称/结局/存活角色/摘要）与 `gdd-cross-review-2026-05-09.md` H3 裁决一致
- `adventure_blueprint.json` 中的 `world_state_hooks` 由冒险生成系统解析，调用本系统的写入接口执行变更

---

*创建日期: 2026-05-10*
*状态: Draft — 待审批*
*下一步: 审阅后 → 实现 IWorldStateManager + 14 个单元测试*
