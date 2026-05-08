# Systems Index: 《酒馆与命运》

> **Status**: Draft
> **Created**: 2026-05-08
> **Last Updated**: 2026-05-08
> **Source Concept**: design/gdd/game-concept.md

---

## Overview

《酒馆与命运》是一款 Roguelike × DND RPG，核心体验通过三大支柱驱动：**角色驱动的故事**（LLM程序化叙事）、**战术深度——原汁原味DND**（5e SRD回合制战斗 + 6项节奏优化）、**持续演进的世界**（永久后果 + 酒馆成长）。

游戏结构分为双层循环：酒馆元循环（招募→培养→组队→冒险→结算）嵌套冒险局内循环（探索→事件→战斗→选择→后果）。这要求系统覆盖战斗规则引擎、程序化叙事管线、角色构筑深度、元游戏进度管理和像素UI呈现六大领域。

MVP标准：离线可玩的核心循环（LLM为增强体验，离线有模板降级）——招募4人队伍→进入冒险→DND战斗→返回结算→重复10次不腻。

---

## Systems Enumeration

| # | System Name | Category | Priority | Status | Design Doc | Depends On |
|---|-------------|----------|----------|--------|------------|------------|
| 1 | 事件总线 (Event Bus) | Core | MVP | Implemented | `docs/architecture/adr-0003-event-bus.md` | — |
| 2 | 骰子系统 (Dice System) | Core | MVP | Not Started | — | — |
| 3 | 场景管理 (Scene Management) | Core | MVP | Not Started | — | — |
| 4 | 设置选项系统 (Settings/Options) | Meta | MVP | Not Started | — | — |
| 5 | 音频系统 (Audio System) | Audio | MVP | Not Started | — | — |
| 6 | 角色系统 (Character System) | Gameplay | MVP | Not Started | `design/gdd/01-character-system.md`* | 骰子, 事件总线 |
| 7 | 物品装备系统 (Items & Equipment) | Gameplay | MVP | Not Started | `design/gdd/03-items-equipment.md`* | 角色, 事件总线 |
| 8 | 条件效果系统 (Status Effects) | Gameplay | MVP | Not Started | — | 角色, 事件总线 |
| 9 | 世界状态系统 (World State) | Persistence | MVP | Not Started | — | 事件总线 |
| 10 | LLM集成网关 (LLM Gateway) | Narrative | MVP | Not Started | `design/gdd/02-llm-integration.md`* | 事件总线 |
| 11 | 敌人AI系统 (Enemy AI) | Gameplay | MVP | Not Started | — | 角色, 条件效果 |
| 12 | 地图探索系统 (Map & Exploration) | Gameplay | MVP | Not Started | `design/gdd/05-map-exploration.md`* | 骰子, 场景管理 |
| 13 | 战斗系统 (Combat System) | Gameplay | MVP | Not Started | `design/gdd/04-combat-system.md`* | 角色, 物品装备, 条件效果, 骰子, 敌人AI |
| 14 | 冒险生成系统 (Adventure Generation) | Gameplay | MVP | Not Started | `design/gdd/06-adventure-generation.md`* | LLM网关, 世界状态, 地图探索 |
| 15 | 对话系统 (Dialogue System) | Narrative | MVP | Not Started | — | LLM网关, 角色 |
| 16 | 酒馆系统 (Tavern System) | Gameplay | MVP | Not Started | `design/gdd/07-tavern-system.md`* | 角色, 物品装备, 关系(VS), 冒险生成 |
| 17 | 失败与成长系统 (Failure & Growth) | Progression | MVP | Not Started | `design/gdd/08-failure-growth.md`* | 角色, 战斗, 世界状态, LLM网关 |
| 18 | 存档系统 (Save/Load) | Persistence | MVP | Not Started | — | 角色, 世界状态, 酒馆 |
| 19 | UI系统 (UI/UX) | UI | MVP | Not Started | `design/gdd/09-ui-ux-design.md`* | 所有游戏系统(展示层) |
| 20 | 关系系统 (Relationship System) | Narrative | VS | Not Started | — | 角色 |
| 21 | 经验与升级系统 (XP & Leveling) | Progression | VS | Not Started | — | 角色 |
| 22 | 酒馆声望系统 (Tavern Reputation) | Progression | VS | Not Started | — | 角色, 事件总线 |
| 23 | 经济系统 (Economy) | Economy | VS | Not Started | — | 物品装备, 角色 |
| 24 | 场景交互系统 (Scene Interaction) | Gameplay | Alpha | Not Started | — | 角色, 物品装备, 骰子 |
| 25 | 战利品系统 (Loot System) | Economy | Alpha | Not Started | — | 物品装备, 骰子 |
| 26 | 打造系统 (Crafting System) | Economy | Alpha | Not Started | — | 物品装备, 经济(VS) |

> \* 现有 `docs/subsystems/` 中的设计文档为详细子系统规格说明，但位置和格式不符合 `design/gdd/` 的8段GDD标准。这些文档可作为设计参考，但在正式设计时需按GDD模板重新整理至 `design/gdd/` 目录。

---

## Categories

| Category | Description | Systems |
|----------|-------------|---------|
| **Core** | 所有系统依赖的基础设施 | 事件总线, 骰子, 场景管理 |
| **Gameplay** | 让游戏好玩的核心玩法系统 | 角色, 物品装备, 条件效果, 敌人AI, 地图探索, 战斗, 冒险生成, 酒馆, 场景交互 |
| **Progression** | 玩家和世界如何随时间成长 | 失败与成长(含知识传承), 经验与升级, 酒馆声望 |
| **Economy** | 资源的创建和消耗 | 经济, 战利品, 打造 |
| **Narrative** | 故事和对话交付 | LLM网关, 对话, 关系 |
| **Persistence** | 存档和持续性 | 世界状态, 存档 |
| **UI** | 面向玩家的信息展示 | UI系统 |
| **Audio** | 声音和音乐 | 音频系统 |
| **Meta** | 核心游戏循环之外的系统 | 设置选项 |

---

## Priority Tiers

| Tier | Definition | Target Milestone | Design Urgency |
|------|------------|------------------|----------------|
| **MVP** | 离线可玩核心循环（LLM为增强体验，本地模板降级保底）。招募→冒险→战斗→结算→重复10次不腻。 | 第一个可玩原型 | 优先设计 |
| **Vertical Slice** | 一个完整冒险体现LLM增强叙事+关系+成长。"这个故事是我的"体验上线。 | 垂直切片/Demo | 第二批设计 |
| **Alpha** | 所有特性到位（高级交互、完整战利品、打造）。内容可占位。 | Alpha里程碑 | 第三批设计 |
| **Full Vision** | 所有系统的内容完善、平衡调优、视觉打磨。 | Beta/发布 | 按需设计 |

### MVP Priority Rationale

| # | System | Why MVP |
|---|--------|---------|
| 1 | 事件总线 | 已实现。所有系统通过它通信——无EventBus则系统间全耦合。 |
| 2 | 骰子系统 | 每一个判定（攻击/豁免/检定/先攻/伤害）依赖d20骰子。DND体验的物理根基——骰子声就是DND。 |
| 3 | 场景管理 | 酒馆→地图→战斗→返回——场景切换是玩家在"家"和"冒险"之间来回的物理体验。 |
| 4 | 音频系统 | 骰子音效和基础战斗音效最低实现。骰子落地的声音强化"我在玩桌游DND"的感觉。 |
| 5 | 设置选项 | 玩家需要调整音量/键位。独立系统，无依赖风险。 |
| 6 | 角色系统 | MVP的招募/属性/HP/AC/法术位/3种族×3职业全部依赖它。🔴 **最大瓶颈——11个系统依赖**，必须MVP首批稳定。 |
| 7 | 物品装备 | 装备槽位制/武器/护甲。"我的战士拿到了+1长剑"是最直接的成长反馈——玩家需要穿装备才能进冒险。 |
| 8 | 条件效果 | DND 5e战斗中的14种条件（盲目/恐惧/中毒等）。没有条件效果，战斗变成纯数值对撞——失去DND最核心的策略维度（Pillar 2）。 |
| 9 | 世界状态 | 冒险结果的永久记录（城镇安全/势力关系）。Pillar 3（持续演进的世界）的最低可行实现——没有它就实现不了"上次冒险改变了什么"。 |
| 10 | LLM网关 | 🔑 核心差异化体验：DM Agent + 文案Agent让每次冒险叙事独一无二——"这个故事是我的"。离线时自动降级到本地模板。 |
| 11 | 敌人AI | 战斗中的敌人决策。即使只有1种敌人类型，没有AI的敌人就是木桩——DND战斗变成打沙包（违背Pillar 2）。 |
| 12 | 地图探索 | 线性地牢+3种房间模板。玩家需要走路、发现房间、触发事件——从酒馆"踏入冒险"的物理锚点。 |
| 13 | 战斗系统 | Pillar 2（战术深度——原汁原味DND）的主要载体——DND 5e简化版、3职业、回合制。没有战斗，"DND RPG"失去了一半的意义。 |
| 14 | 冒险生成 | MVP用LLM生成模板 + 手工模板双轨（在线/离线）。模板解析→地图实例化→事件触发这条管线必须存在——没有它就没有冒险。 |
| 15 | 对话系统 | LLM生成对话需要一个展示框架——对话UI + 选项追踪。LLM生成"兽人说了什么"，对话系统让玩家选择和看到它。 |
| 16 | 酒馆系统 | Pillar 1和3的物理容器——它是"家"。MVP基础版：招募板+装备管理+任务板。 |
| 17 | 失败与成长 | Pillar 2的重量感来源——赢了有回报，输了有代价。MVP简单结算（成功/失败判定+金币变化+基础伤疤）。 |
| 18 | 存档 | "下次继续"是玩家回到酒馆的心理契约。短冒险一次性+中冒险可存档。 |
| 19 | UI系统 | 所有MVP系统的视觉外壳：酒馆UI/战斗UI/地图UI/角色面板。像素DND的入口——字体、骰子动画、角色头像。 |

---

## Dependency Map

Systems sorted by dependency order — design and build from top to bottom.

### Foundation Layer (no game-system dependencies)

1. **事件总线** — 已实现（Core层IEventBus）。所有系统间通信的唯一通道。
2. **骰子系统** — d20+修正值是所有检定/攻击/豁免/伤害的基础。无游戏系统依赖。
3. **场景管理** — 酒馆/地图/战斗三场景切换。框架级基础设施。
4. **设置选项系统** — 键位/音量/语言配置。独立系统，不依赖游戏逻辑。
5. **音频系统** — BGM/SFX管理服务。独立于游戏逻辑运行。

### Core Layer (depends on Foundation)

6. **角色系统** — depends on: 骰子, 事件总线
7. **物品装备系统** — depends on: 角色, 事件总线
8. **条件效果系统** — depends on: 角色, 事件总线
9. **世界状态系统** — depends on: 事件总线
10. **LLM集成网关** — depends on: 事件总线

### Feature Layer 1 — Simple Features (depends on Core)

11. **敌人AI系统** — depends on: 角色, 条件效果
12. **地图探索系统** — depends on: 骰子, 场景管理
13. **对话系统** — depends on: LLM网关, 角色
14. **经验与升级系统** (VS) — depends on: 角色
15. **关系系统** (VS) — depends on: 角色
16. **酒馆声望系统** (VS) — depends on: 角色, 事件总线
17. **经济系统** (VS) — depends on: 物品装备, 角色

### Feature Layer 2 — Deep Features (depends on Feature Layer 1)

18. **战斗系统** — depends on: 角色, 物品装备, 条件效果, 骰子, 敌人AI
19. **冒险生成系统** — depends on: LLM网关, 世界状态, 地图探索

### Feature Layer 3 — Composite Systems (depends on multiple deep features)

20. **酒馆系统** — depends on: 角色, 物品装备, 关系(VS), 酒馆声望(VS), 冒险生成
21. **失败与成长系统** — depends on: 角色, 战斗, 世界状态, LLM网关
22. **存档系统** — depends on: 角色, 世界状态, 酒馆
23. **场景交互系统** (Alpha) — depends on: 角色, 物品装备, 骰子
24. **战利品系统** (Alpha) — depends on: 物品装备, 骰子
25. **打造系统** (Alpha) — depends on: 物品装备, 经济(VS)

### Presentation Layer (depends on features)

26. **UI系统** — depends on: all gameplay systems (展示层包裹)

---

## Recommended Design Order

| Order | System | Priority | Layer | Est. Effort |
|-------|--------|----------|-------|-------------|
| 1 | 事件总线 | MVP | Foundation | — (已实现) |
| 2 | 骰子系统 | MVP | Foundation | S |
| 3 | 场景管理 | MVP | Foundation | S |
| 4 | 设置选项系统 | MVP | Foundation | S |
| 5 | 音频系统 | MVP | Foundation | S |
| 6 | **角色系统** | MVP | Core | **L** |
| 7 | 物品装备系统 | MVP | Core | M |
| 8 | 条件效果系统 | MVP | Core | M |
| 9 | 世界状态系统 | MVP | Core | S |
| 10 | LLM集成网关 | MVP | Core | L |
| 11 | 敌人AI系统 | MVP | Feature | M |
| 12 | 地图探索系统 | MVP | Feature | M |
| 13 | 战斗系统 | MVP | Feature | L |
| 14 | 冒险生成系统 | MVP | Feature | L |
| 15 | 对话系统 | MVP | Feature | M |
| 16 | 酒馆系统 | MVP | Feature | L |
| 17 | 失败与成长系统 | MVP | Feature | L |
| 18 | 存档系统 | MVP | Feature | M |
| 19 | UI系统 | MVP | Presentation | L |
| 20 | 关系系统 | VS | Feature | M |
| 21 | 经验与升级系统 | VS | Feature | M |
| 22 | 酒馆声望系统 | VS | Feature | S |
| 23 | 经济系统 | VS | Feature | M |
| 24 | 场景交互系统 | Alpha | Feature | M |
| 25 | 战利品系统 | Alpha | Feature | M |
| 26 | 打造系统 | Alpha | Feature | M |

Effort estimates: S = 1 session, M = 2-3 sessions, L = 4+ sessions. One "session" = one focused design conversation producing a complete GDD.

Design order principle: Combine dependency sort + priority tier. Independent systems at the same layer can be designed in parallel (e.g., settings options and audio system; relationship and XP/leveling).

---

## Circular Dependencies

- **None found.** All dependencies flow unidirectionally from Foundation → Core → Feature → Presentation.

---

## High-Risk Systems

| System | Risk Type | Risk Description | Mitigation |
|--------|-----------|-----------------|------------|
| **角色系统** | Design / Dependency | 🔴 11个系统依赖——数据模型不稳定将导致下游11个系统全部返工。DND 5e属性/职业/种族/法术位覆盖面广，设计复杂度高。 | 最优先设计，最稳锁定。参考现有 `docs/subsystems/01-character-system.md` 但需按GDD模板重写。设计冻结后3轮review再允许下游系统开工。 |
| **LLM集成网关** | Technical | 四Agent管线+Schema验证+Token预算+离线降级——技术链路最长，涉及外部API集成和Schema验证不稳定性。 | 参考 `docs/subsystems/02-llm-integration.md`（4213行详细设计）。早期原型验证Schema验证重试机制和离线降级。 |
| **冒险生成系统** | Technical / Scope | 三层管线（LLM→程序化→实时叙事）是项目最核心的技术设计，涉及LLM输出的不确定性转化为可玩地图。 | 分层设计，每层独立单元测试。先实现手工模板管线（无LLM依赖），再接入LLM。 |
| **战斗系统** | Design / Complexity | DND 5e规则体系庞大（14种条件、FSM状态机、6项节奏优化偏差），设计容易失控。 | 参考 `docs/subsystems/04-combat-system.md`（2400行）。MVP限定3职业+1敌人类型+简化条件池，逐层扩展。 |

---

## Progress Tracker

| Metric | Count |
|--------|-------|
| Total systems identified | 26 |
| Design docs started | 9 (existing `docs/subsystems/` — to be migrated to GDD format) |
| Design docs reviewed | 0 |
| Design docs approved | 0 |
| MVP systems designed | 0 / 19 |
| Vertical Slice systems designed | 0 / 4 |
| Alpha systems designed | 0 / 3 |
| Implemented systems | 1 (EventBus — Core infrastructure, Phase 0) |

---

## Next Steps

- [x] Review and approve this systems enumeration
- [x] Review and approve dependency mapping
- [x] Review and approve priority assignments
- [ ] Design MVP-tier systems first — start with `骰子系统` (design order #2)
- [ ] Run `/design-review` on each completed GDD
- [ ] Migrate existing `docs/subsystems/*.md` to `design/gdd/*.md` in GDD format during design
- [ ] Run `/gate-check pre-production` when all MVP GDDs are authored and reviewed

---

## Existing Design Assets (Reference)

The following existing documents contain detailed subsystem designs. They will be used as reference during GDD authoring but will eventually be superseded by new `design/gdd/` GDDs following the 8-section format:

| Existing Doc | Lines | Covers System(s) | Action |
|-------------|-------|-----------------|--------|
| `docs/subsystems/01-character-system.md` | 2461 | #6 角色系统 | Reference during GDD authoring |
| `docs/subsystems/02-llm-integration.md` | 4213 | #10 LLM网关 | Reference during GDD authoring |
| `docs/subsystems/03-items-equipment.md` | 2362 | #7 物品装备系统 | Reference during GDD authoring |
| `docs/subsystems/04-combat-system.md` | 2400 | #13 战斗系统 | Reference during GDD authoring |
| `docs/subsystems/05-map-exploration.md` | 2465 | #12 地图探索系统 | Reference during GDD authoring |
| `docs/subsystems/06-adventure-generation.md` | 2766 | #14 冒险生成系统 | Reference during GDD authoring |
| `docs/subsystems/07-tavern-system.md` | 2190 | #16 酒馆系统 | Reference during GDD authoring |
| `docs/subsystems/08-failure-growth.md` | 2284 | #17 失败与成长系统 | Reference during GDD authoring |
| `docs/subsystems/09-ui-ux-design.md` | 1927 | #19 UI系统 | Reference during GDD authoring |
| `docs/GDD-v1.md` | 1029 | Game design canon | Authoritative reference |
