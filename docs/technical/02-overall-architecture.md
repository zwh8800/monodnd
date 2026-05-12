# 酒馆与命运 — Master Architecture

## Document Status

| Field | Value |
|-------|-------|
| **版本** | v3.0（重构版，按 `/create-architecture` 10章节模板） |
| **Last Updated** | 2026-05-11 |
| **引擎** | MonoGame 3.8.x stable（当前 3.8.4.1） (C# 12 / .NET 8) |
| **场景框架** | 自定义轻量 ECS (Scene/Entity/Component/SceneComponent) — ADR-0001 |
| **规则基线** | DND 5e SRD（经Roguelike调整，6项偏离见 §4.2.6） |
| **GDDs Covered** | GDD-v1, 01-character, 02-llm, 03-items, 04-combat, 05-map, 06-adventure, 07-tavern, 08-failure, 09-ui, 10-condition, 11-enemy-ai |
| **ADRs Referenced** | ADR-0000~0008（共9个，全部 Accepted） |
| **语言政策** | 游戏文本统一采用简体中文，技术标识符使用英文 PascalCase/camelCase，JSON键名使用 snake_case |
| **前置文档** | docs/architecture/adr-0000~0008, design/gdd/systems-index.md |
| **Technical Director Sign-Off** | 2026-05-11 — **APPROVED**（TD 4项条件已全部修复） |
| **Lead Programmer Feasibility** | 2026-05-11 — **FEASIBLE**（LP 7项阻塞已全部解决，LP-4 Myra原型验证推迟至 Sprint 1 启动前） |

> **签批条件**：所有 TD + LP 条件项已于 2026-05-11 全部修复。架构文档已达 APPROVED / FEASIBLE 状态，可进入 Sprint 1 MVP 开发。LP-4（Myra 像素风 UI 原型验证）推迟至 Sprint 1 启动前执行。详见 §10.1。

> **v2.0→v3.0 变更要点**：层级架构从旧5层模型调整为4层（移除 LLM集成层 和 数据持久化层 作为独立层，归入CORE和FOUNDATION）；BalancerAgent移除（GDD v1.2决策）；GoRogue版本统一锁定为2.6.4（ADR-0007）；补充ADR审计、缺失ADR清单、架构原则、开放问题等模板要求章节。

---

## Engine Knowledge Gap Summary

引擎：MonoGame 3.8.x stable（当前 3.8.4.1） / C# 12 / .NET 8

LLM 训练覆盖版本：MonoGame API 自 XNA 时代（2006）以来高度稳定，LLM 训练数据充分覆盖。

### LOW RISK 域（训练数据可靠，无需额外验证）

| 域 | 说明 |
|----|------|
| MonoGame 核心 API | SpriteBatch, ContentManager, Game基类, GameTime — 2006年起稳定 |
| C# 基础类型系统 | record, enum, Dictionary, LINQ, async/await — .NET长期稳定 |
| System.Text.Json | .NET Core 3.0 起内置 — 充分覆盖 |
| HttpClient | .NET 标准库 — 充分覆盖 |
| sqlite-net ORM | 社区维护，API 长期稳定 |

### MEDIUM RISK 域（需验证关键 API）

| 域 | 关键变更 | 影响系统 |
|----|---------|---------|
| GoRogue 2.6.4 API | csproj锁定2.6.4，但旧文档引用3.x；Coord→Point为3.x破坏性变更 | MapExploration, CombatEngine, AdventureGeneration |
| Myra UI | 版本号"最新"未精确锁定 — API可能有变更 | UISystem |
| FontStashSharp | 版本号"最新"未精确锁定 | UISystem, 所有文本渲染 |

### HIGH RISK 域（必须在引擎参考库中验证）

| 域 | 说明 |
|----|------|
| 无 | MonoGame 3.8.5+ 核心API全部在训练截止日期前稳定；无post-cutoff高风险域 |

### 涉及 MEDIUM RISK 的系统

| 系统 | 域 | 风险等级 |
|------|----|---------|
| MapExploration | GoRogue 2.6.4 API | MEDIUM — 已锁定版本，已封装坐标转换 |
| CombatEngine | GoRogue FOV/A* | MEDIUM — 已通过GoRogueMapManager封装隔离 |
| UISystem | Myra + FontStashSharp | MEDIUM — 需在Sprint 1锁定版本号 |

**结论**：本项目引擎知识差距整体LOW。唯一MEDIUM风险为GoRogue版本不一致（已通过ADR-0007锁定为2.6.4解决）和第三方UI库版本未锁定（需在实现时处理）。

---

## 1. System Layer Map

### 1.1 核心设计哲学

```
┌─────────────────────────────────────────────────────────────────┐
│                    设计哲学 (Design Philosophy)                    │
├─────────────────────────────────────────────────────────────────┤
│                                                                   │
│  ① LLM = 皮肤层，程序 = 骨骼层                                     │
│     LLM生成叙事文本、对话、描述，不决策任何数值、战斗结果、           │
│     故事走向。所有规则判定由程序完成。（ADR-0004）                    │
│                                                                   │
│  ② 严格的JSON Schema验证                                          │
│     所有Agent输出必须通过Schema验证后才能进入游戏系统。               │
│     不合规输出触发自动重试（最多3次），耗尽后降级到离线模板。          │
│                                                                   │
│  ③ 程序层不依赖LLM，可完全单元测试                                  │
│     所有数值计算、规则判定、分支逻辑在无LLM环境下也可运行。            │
│     LLM是增量体验，不是必需品。                                      │
│                                                                   │
│  ④ 数值由程序控制，LLM只负责叙事文本                                  │
│     伤害数值、检定DC、战利品掉落、世界状态变更——全部程序控制。        │
│     LLM只负责"描述这一切看起来/听起来是什么样"。                     │
│                                                                   │
│  ⑤ 数据驱动设计                                                    │
│     DND 5e的规则（法术、怪物、职业）尽量配置在SQLite或JSON中，        │
│     避免硬编码。（ADR-0005）                                        │
│                                                                   │
│  ⑥ 代码即场景                                                      │
│     所有游戏对象通过C#代码创建，不依赖可视化编辑器。                   │
│     自定义 Entity/Component 组合取代预制体体系。所有场景是Scene子类。     │
│     像素坐标、精灵帧、碰撞体——全部在代码中定义。（ADR-0001）          │
│                                                                   │
└─────────────────────────────────────────────────────────────────┘
```

### 1.2 四层架构总览

> **v3.0 关键修正**：旧5层模型（表现层/场景管理层/游戏逻辑层/LLM集成层/数据持久化层）→ 新4层模型。LLM Gateway 归入 CORE 层（非所有系统必经之路）；Data Persistence 归入 FOUNDATION（无游戏语义的纯基础设施）；Scene Management 归入 FOUNDATION（ECS容器是框架级基础设施）。

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           PRESENTATION 层 (表现层)                          │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │  渲染管线: MonoGame SpriteBatch                                        │   │
│  │  ┌────────────┐ ┌────────────┐ ┌──────────┐ ┌────────┐ ┌────────┐   │   │
│  │  │  Tile渲染   │ │ 角色精灵    │ │ 粒子特效  │ │ 动画    │ │ 迷雾    │   │   │
│  │  └────────────┘ └────────────┘ └──────────┘ └────────┘ └────────┘   │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │  UI层: Myra UI (像素风)                                               │   │
│  │  ┌────────────┐ ┌────────────┐ ┌──────────┐ ┌────────────┐ ┌──────┐ │   │
│  │  │ 酒馆UI     │ │ 战斗UI     │ │ 地图UI   │ │ 角色面板   │ │ 对话  │ │   │
│  │  └────────────┘ └────────────┘ └──────────┘ └────────────┘ └──────┘ │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────┘
                                       │
┌─────────────────────────────────────────────────────────────────────────────┐
│                           FEATURE 层 (玩法层)                               │
│                                                                             │
│  ┌─── L3: 复合系统 (跨多个深度系统的组合) ──────────────────────────────┐  │
│  │  ┌──────────────┐  ┌──────────────────┐  ┌──────────────┐            │  │
│  │  │  酒馆系统      │  │  失败与成长系统    │  │  存档系统      │            │  │
│  │  │  TavernSystem │  │  Settlement      │  │  SaveSystem  │            │  │
│  │  │  · 升级管理    │  │  · XP/战利品计算   │  │  · 序列化     │            │  │
│  │  │  · 招募       │  │  · 伤疤/惩罚      │  │  · 完整性校验   │            │  │
│  │  │  · 商店       │  │  · 世界状态变更    │  │  · 槽位管理    │            │  │
│  │  │  · NPC调度    │  │  · 关系变化       │  │              │            │  │
│  │  │  · 事件       │  │                  │  │              │            │  │
│  │  └──────────────┘  └──────────────────┘  └──────────────┘            │  │
│  └───────────────────────────────────────────────────────────────────────┘  │
│                                                                             │
│  ┌─── L2: 深度系统 (有复杂内部逻辑+多依赖) ────────────────────────────┐  │
│  │  ┌──────────────────┐  ┌──────────────────────────────┐              │  │
│  │  │  战斗引擎          │  │  冒险生成系统                  │              │  │
│  │  │  CombatEngine     │  │  AdventureGeneration         │              │  │
│  │  │  · 14状态 FSM     │  │  · 蓝图解析→实例化14步          │              │  │
│  │  │  · DiceRoller     │  │  · 遭遇生成(CR预算)            │              │  │
│  │  │  · ActionResolver │  │  · GoRogue地牢生成             │              │  │
│  │  │  · ConditionTracker│  │  · 节点图+分支管理             │              │  │
│  │  │  · EnemyAI(内部)  │  │                              │              │  │
│  │  │  · GoRogue FOV/A* │  │                              │              │  │
│  │  └──────────────────┘  └──────────────────────────────┘              │  │
│  └───────────────────────────────────────────────────────────────────────┘  │
│                                                                             │
│  ┌─── L1: 简单系统 (依赖少、内部逻辑简单) ────────────────────────────┐  │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐              │  │
│  │  │  敌人AI       │  │  地图探索      │  │  对话系统      │              │  │
│  │  │  EnemyAI      │  │  MapExplore   │  │  Dialogue     │              │  │
│  │  │  · 5种行为模式 │  │  · FOV/迷雾   │  │  · UI框架     │              │  │
│  │  │  · 目标选择   │  │  · 节点导航   │  │  · 选项追踪    │              │  │
│  │  └──────────────┘  └──────────────┘  └──────────────┘              │  │
│  └───────────────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────────┘
                                       │
┌─────────────────────────────────────────────────────────────────────────────┐
│                           CORE 层 (核心数据层)                              │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │  角色系统 (数据枢纽 — 11个系统依赖)                                    │   │
│  │  ┌──────────────┐ ┌──────────────┐ ┌──────────────┐ ┌──────────┐   │   │
│  │  │ CharacterData │ │ 属性/公式    │ │ 关系双轴      │ │ 叙事层   │   │   │
│  │  │ (record,      │ │ HP/AC/PB    │ │ trust×conflict│ │ LLM生成  │   │   │
│  │  │  FROZEN)      │ │ (FROZEN)    │ │ (FROZEN)     │ │ Schema   │   │   │
│  │  └──────────────┘ └──────────────┘ └──────────────┘ └──────────┘   │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐ ┌─────────┐  │   │
│  │  │ 物品装备系统   │  │ 条件效果系统   │  │ 世界状态管理器 │ │ DiceRoller│ │   │
│  │  │ ItemSystem   │  │ ConditionTracker│ │ WorldState   │ │ (static) │ │   │
│  │  │ · 装备槽位    │  │ · 14条件追踪   │ │ · 区域/势力  │ │ · 纯函数 │ │   │
│  │  └──────────────┘  └──────────────┘  └──────────────┘ └─────────┘  │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │  LLM Gateway (皮肤层服务 — 只有需要叙事的系统才调用)                    │   │
│  │  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐ │   │
│  │  │编剧Agent  │ │ DM Agent │ │文案Agent  │ │Schema验证│ │缓存+降级 │ │   │
│  │  │(冒险前)  │ │(实时叙事)│ │(按需)    │ │(JsonSchema│ │(sqlite- │ │   │
│  │  │          │ │          │ │          │ │ .Net)    │ │ net+模板)│ │   │
│  │  └──────────┘ └──────────┘ └──────────┘ └──────────┘ └──────────┘ │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
│  ┌──────────────┐                                                       │   │
│  │ 音频系统      │                                                       │   │
│  │ AudioManager │                                                       │   │
│  │ · BGM/SFX   │                                                       │   │
│  └──────────────┘                                                       │   │
└─────────────────────────────────────────────────────────────────────────────┘
                                       │
┌─────────────────────────────────────────────────────────────────────────────┐
│                           FOUNDATION 层 (基础设施层)                        │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │  自定义 ECS Core (GameRoot + Scene/Entity/Component/SceneComponent)    │   │
│  │  ┌──────────────┐ ┌────────────────┐ ┌────────────┐ ┌────────────┐  │   │
│  │  │ TavernScene  │ │ AdventureScene │ │CombatScene │ │ Loading    │  │   │
│  │  │ (酒馆)       │ │ (地图探索)     │ │ (战斗)     │ │ Scene     │  │   │
│  │  └──────────────┘ └────────────────┘ └────────────┘ └────────────┘  │   │
│  │  场景切换: GameRoot 场景切换队列 (_nextScene) + 预加载策略               │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │  ServiceLocator (全局服务注册，初始化顺序 0-6)                          │   │
│  │  EventBus(0) → GameStateManager(1) → DataPersistence(2)               │   │
│  │  → LLMGateway(3) → WorldStateManager(4) → AudioManager(5)            │   │
│  │  → ResourceCache(6) → FinalizeRegistration()                          │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │  Data Persistence (JSON配置 + SQLite运行时)                            │   │
│  │  ┌────────────────┐  ┌────────────────┐  ┌──────────────┐           │   │
│  │  │ sqlite-net      │  │ System.Text.Json │  │ LLM缓存      │           │   │
│  │  │ · 角色数据      │  │ · 世界状态快照    │ │ (sqlite-net) │           │   │
│  │  │ · 物品/装备     │  │ · 存档序列化      │ │              │           │   │
│  │  │ · 冒险日志      │  │ · 配置表(JSON)   │ │              │           │   │
│  │  └────────────────┘  └────────────────┘  └──────────────┘           │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
│  ┌──────────────┐ ┌──────────────┐                                        │   │
│  │ 设置选项      │ │ 骰子系统      │                                        │   │
│  │ Settings     │ │ Dice System  │                                        │   │
│  │ · 键位/音量  │ │ (Quick Spec) │                                        │   │
│  └──────────────┘ └──────────────┘                                        │   │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 1.3 模块依赖关系

```
┌──────────────────────────────────────────────────────────────────────────┐
│                     模块依赖关系 (Module Dependencies)                     │
│                                                                          │
│  GameRoot.Instance.StartSceneTransition ──────────────────────────────────┐ │
│     │ 场景切换                              │                            │ │
│     ▼                                      ▼                            │ │
│  CombatEngine ◄── CharacterSystem ──► AdventureSystem                    │ │
│     │  战斗结算     ↑ 提供属性              │  蓝图生成                    │ │
│     │              │                       ▼                            │ │
│     ▼              │                  LLMGateway                         │ │
│  SettlementSystem ─┘  ◄── TavernSystem ──► WorldStateManager             │ │
│    (失败/惩罚)      │    ↑ 触发事件           │                           │ │
│                    │    │                   ▼                            │ │
│                    │    └────── DataPersistence (sqlite-net + JSON)      │ │
│                    └───────────────────────┘                             │ │
│                                                                          │ │
│  GoRogue(2.6.4) ─► CombatEngine (FOV/Pathfinding)                       │ │
│  GoRogue(2.6.4) ─► AdventureSystem (MapGeneration)                      │ │
│  GameRoot ──► 所有Scene子类 (场景管理)                                  │ │
│  Myra ──────► 所有UI面板 (XML布局 + 数据绑定)                              │ │
│  FontStashSharp ──► 所有UI/渲染 (动态字体+中文)                            │ │
└──────────────────────────────────────────────────────────────────────────┘
```

### 1.4 全局服务注册表

所有服务通过 `ServiceLocator` 模式注册为全局服务。初始化顺序为硬性约束（EventBus 必须最先注册）：

| 优先级 | 服务名 | 接口 | 实现类 | 职责 |
|:---:|------|------|--------|------|
| 0 | EventBus | IEventBus | EventBus | 全局事件总线（C# event + Snapshot-then-Invoke） |
| 1 | GameStateManager | IGameStateManager | GameStateManager | 全局状态管理、场景切换 |
| 2 | DataPersistence | IDataPersistence | DataPersistence | sqlite-net + JSON 数据存取 |
| 3 | LLMGateway | ILLMGateway | LLMGateway | LLM 请求唯一入口（皮肤层服务） |
| 4 | WorldStateManager | IWorldStateManager | WorldStateManager | 世界状态追踪 |
| 5 | AudioManager | IAudioManager | AudioManager | BGM/SFX 管理 |
| 6 | ResourceCache | IResourceCache | ResourceCache | 预加载资源管理 |

注册完成后调用 `ServiceLocator.FinalizeRegistration()` 锁定注册表。

---

## 2. Module Ownership

### 2.1 FOUNDATION 层模块归属

| 模块 | Owns（独占） | Exposes（暴露给其他模块） | Consumes（从其他模块读取） | Engine APIs |
|------|-----------|----------------------|-----------------------|------------|
| EventBus | 事件订阅路由、处理器委托链 | IEventBus.Subscribe/Publish/Unsubscribe | — (根服务) | 无 — 纯C#实现 |
| ServiceLocator | 全局服务注册表、初始化顺序 | Register/Get/FinalizeRegistration/Reset | — (根服务) | 无 — 纯C#实现 |
| GameStateManager | SceneId枚举、场景切换队列、TransitionContext | IGameStateManager.Transition/SwitchTo | IEventBus | MonoGame Game基类 |
| DataPersistence | 存档槽管理(1-based)、序列化编排 | IDataPersistence.SaveAsync/LoadAsync/DeleteAsync/GetAvailableSlots | IEventBus | sqlite-net ORM, System.Text.Json |
| Scene/ECS | 实体生命周期、组件管理、场景切换帧边界安全 | CreateEntity/AddSceneComponent/Initialize/Begin/End | ServiceLocator | MonoGame SpriteBatch, GameTime |
| DiceRoller | 随机数生成器、骰子表达式解析器 | static Roll/RollD20/RollWithAdvantage/RollCritDamage | — (纯函数) | 无 — 纯C# Random |
| SettingsOptions | 音量配置(3组滑块)、键位映射表、语言选择、无障碍选项 | ISettingsProvider.GetVolume/GetKeyMap/GetAccessibilityOptions | IDataPersistence(持久化保存) | 无 — 纯数据配置 |

### 2.2 CORE 层模块归属

| 模块 | Owns | Exposes | Consumes | Engine APIs |
|------|------|---------|---------|------------|
| CharacterSystem | CharacterData record(FROZEN)、六维属性+公式(FROZEN)、等级进阶、关系双轴(FROZEN)、叙事层 | ICharacterSystem.Get/Save/LevelUp/GetRelationship | DiceRoller, IEventBus, IDataPersistence | 无 — 纯数据逻辑 |
| ItemSystem | ItemData record、装备槽位模型(9槽)、AC计算公式、附魔效果 | IItemSystem.Get/Equip/Unequip | CharacterSystem, IEventBus, IDataPersistence | 无 — 纯数据逻辑 |
| ConditionTracker | 14种ConditionType枚举、持续时间追踪、堆叠规则、互斥规则 | IConditionTracker.Apply/Remove/Query | CharacterSystem, IEventBus | 无 — 纯数据逻辑 |
| WorldStateManager | 区域安全等级、势力友好度、世界事件、冒险印记 | IWorldStateManager.Get/Set/GetContextForLLM | IEventBus, IDataPersistence | 无 — 纯数据逻辑 |
| LLMGateway | 4Agent调度(编剧/DM/文案)、Schema验证管线、语义缓存(sqlite-net)、离线降级 | ILLMGateway.CallAgent/GetBudgetStatus | IEventBus, IDataPersistence | HttpClient, System.Text.Json, JsonSchema.Net |
| AudioManager | BGM/SFX播放状态、音量配置 | IAudioManager.Play/Stop/SetVolume | IEventBus | MonoGame SoundEffect/Song |

### 2.3 FEATURE 层模块归属

**L1 — 简单系统：**

| 模块 | Owns | Exposes | Consumes | Engine APIs |
|------|------|---------|---------|------------|
| EnemyAI | 5种行为模式决策(BT)、目标选择启发 | CombatAction决策结果(返回给CombatEngine) | CharacterSystem, ConditionTracker | 无 |
| MapExploration | GoRogue ArrayMap/FOV/A*封装、节点导航状态 | IMapSystem.CalculateFOV/FindPath/GenerateDungeon | DiceRoller, GameStateManager | GoRogue 2.6.4 (Coord/ArrayMap/FOV/AStar) |
| DialogueSystem | 对话UI框架、选项追踪、上下文管理 | IDialogueSystem.StartDialogue/SelectOption | LLMGateway, CharacterSystem | Myra UI |

**L2 — 深度系统：**

| 模块 | Owns | Exposes | Consumes | Engine APIs |
|------|------|---------|---------|------------|
| CombatEngine | 14状态FSM、行动经济6种资源、攻击/伤害管线、战斗日志 | CombatEngine(SceneComponent) InitializeCombat/SubmitAction/GetCombatants | CharacterSystem, ItemSystem, ConditionTracker, DiceRoller, EnemyAI(内部) | GoRogue 2.6.4, MonoGame GameTime |
| AdventureGeneration | 蓝图解析器、实例化14步算法、遭遇生成(CR预算)、节点图 | IAdventureSystem.Instantiate/GetCurrentInstance | LLMGateway, WorldStateManager, MapExploration | GoRogue 2.6.4 (MapGeneration) |

**L3 — 复合系统：**

| 模块 | Owns | Exposes | Consumes | Engine APIs |
|------|------|---------|---------|------------|
| TavernSystem | 酒馆升级Lv1-10、招募板、商店(6种)、NPC调度、酒馆事件、短/长休 | ITavernSystem.Recruit/Upgrade/TriggerEvent/Rest | CharacterSystem, ItemSystem, AdventureGeneration, LLMGateway, WorldStateManager | 无 |
| SettlementSystem | XP计算、战利品生成、伤疤判定、惩罚计算、世界状态变更 | ISettlementSystem.CalculateRewards/Penalties | CharacterSystem, IEventBus(CombatEnded等战斗事件), WorldStateManager, LLMGateway | 无 |
| SaveSystem | 存档序列化/反序列化、完整性校验(hash)、槽位管理 | ISaveSystem.Save/Load/Validate/ListSlots | CharacterSystem, WorldStateManager, TavernSystem, IDataPersistence | 无 |

### 2.4 PRESENTATION 层模块归属

| 模块 | Owns | Exposes | Consumes | Engine APIs |
|------|------|---------|---------|------------|
| UISystem | Myra面板管理、数据绑定、像素主题配置、战斗日志双轨 | UI面板渲染 + 输入事件收集 | 所有游戏系统(展示层包裹) | Myra, FontStashSharp, MonoGame SpriteBatch |

---

## 3. Data Flow

### 3.1 核心数据流图

```
                    ┌───────────────────────────────────────┐
                    │          LLM API (云端)                │
                    │  OpenAI / Claude / 国产模型 / Ollama   │
                    └────────────┬──────────────────────────┘
                                 │ HTTP POST (JSON via HttpClient)
                                 ▼
┌──────────┐    ┌───────────────────────────────────────┐
│ 玩家操作  │───▶│           LLM Gateway (CORE层服务)     │
│ (输入)    │    │  · 请求队列 · Schema验证 · 缓存 · 降级 │
└──────────┘    └────────────┬──────────────────────────┘
                             │ AgentResponse (已验证JSON)
                             ▼
                    ┌───────────────────┐
                    │  FEATURE层游戏逻辑  │
                    │  ──────────────   │
                    │  · 冒险系统        │ ← 接收蓝图
                    │  · 战斗引擎        │ ← 接收叙事文本
                    │  · 酒馆系统        │ ← 接收招募/事件文本
                    │  · 结算系统        │ ← 接收惩罚叙事
                    └────────┬──────────┘
                             │
                    ┌────────▼──────────┐
                    │  CORE层数据计算     │
                    │  ──────────────   │
                    │  · 数值计算        │ ← 不依赖LLM
                    │  · 规则判定        │
                    │  · 分支逻辑        │
                    │  · 世界状态变更    │
                    └───────────────────┘
```

### 3.2 LLM与程序层的职责边界

```
LLM只做:
  · 生成NPC说的话（不是NPC做的事）
  · 写战斗描述文字（不是计算伤害数值）
  · 生成选项的风味描述（不是定义选项的后果）
  · 给角色取名字和写背景故事（不是决定角色的属性）
  · 描述伤疤的外观（不是选择伤疤的数值效果）

程序做:
  · 计算所有伤害、治疗、buff数值
  · 决定命中/未命中
  · 控制NPC的行动逻辑（EnemyAI行为树）
  · 执行剧情分支的走向
  · 管理角色升级和属性变化
  · 计算战利品掉落
  · 应用状态效果
  · 变更世界状态
```

### 3.3 EventBus 事件类型清单

所有跨系统通信通过 `IEventBus`（Snapshot-then-Invoke模式，ADR-0003）：

```csharp
// 战斗系统事件
public record CombatStarted(string CombatId, string BlueprintId);
public record CombatEnded(string CombatId, CombatResult Result, List<string> Survivors, List<string> Casualties);
public record DamageDealt(string TargetId, string SourceId, int Amount, DamageType Type, bool IsCritical);
public record CharacterDied(string CharacterId, string KillerId, DamageType FinalBlow, string AdventureId);
public record ConditionApplied(string TargetId, ConditionType Type, int DurationRounds);
public record SpellCast(string CasterId, string SpellId, int SpellLevel);
public record DeathSaveProgressed(string CharacterId, int RoundsWithoutHeal);

// 角色系统事件
public record CharacterCreated(string CharacterId, string RaceId, string ClassId);
public record CharacterLeveledUp(string CharacterId, int OldLevel, int NewLevel);
public record RelationshipChanged(string CharA, string CharB, int Delta, int NewValue);

// 酒馆系统事件
public record TavernEventTriggered(string EventId, MechanicalResult Result);
public record AdventurerRecruited(string CharacterId, int RecruitmentFee);
public record FacilityUpgraded(string FacilityId, int OldLevel, int NewLevel);

// 冒险系统事件
public record AdventureStarted(string AdventureId, string BlueprintId);
public record AdventureCompleted(string AdventureId, AdventureOutcome Outcome);
public record NodeEntered(string NodeId, NodeType Type);

// LLM 事件
public record NarrativeReady(string RequestId, NarrativeText Text);
public record LLMFallbackTriggered(string AgentType, string Reason);
public record BudgetExceeded(string AdventureId, int TokensUsed, int BudgetLimit);
```

### 3.4 关键游戏场景数据流

**路径1: 玩家接任务 → 冒险开始**

```
玩家点击任务板 → TavernSystem.CreateAdventure(tier)
  → await LLMGateway.CallAgent<ScreenwriterAgent, AdventureBlueprint>(input)
    → 编剧Agent生成冒险蓝图
    → JsonSchema.Net Schema验证 + 业务逻辑验证
    → 失败策略：3次重试 → 离线模板
    → 返回 AdventureBlueprint
  → AdventureSystem.Instantiate(blueprint, partyState)
    → 14步实例化算法 (纯C#，无LLM)
    → 返回 AdventureInstance
  → GameRoot.StartSceneTransition(AdventureScene)
```

**路径2: 战斗动作 → 玩家看到结果**

```
玩家选择"攻击" → CombatEngine.SubmitAction(action)
  → DiceRoller.RollAttack(...)           # 纯程序计算
  → ActionResolver.Resolve(...)          # 纯程序计算
  → EventBus.Publish(DamageDealt)        # 事件驱动UI更新
  → UISystem.Update()                    # 更新数值显示
  → await LLMGateway.CallAgent<DMAgent, NarrativeText>(combatContext)  # 异步，不阻塞
    → DM Agent生成叙事文本
    → UISystem.AppendNarration(text)     # 追加到战斗日志
```

**路径3: 冒险结束 → 结算**

```
AdventureSystem.CheckCompletion() → 判定成功/失败
  → SettlementSystem.CalculateRewards()    # 纯程序
    → XP分配、战利品生成、世界状态变更
  → SettlementSystem.CalculatePenalties()   # 纯程序
    → 伤疤判定(预定义池)、资源消耗、关系变化
  → await LLMGateway.CallAgent<DMAgent, NarrativeText>(settlementContext)  # 异步
    → DM Agent生成结算叙事文本
  → GameRoot.StartSceneTransition(SettlementScene)
```

### 3.5 保存/加载路径

```
SaveSystem.Save() → IDataPersistence.SaveAsync(CharacterRecord) + WorldState + TavernState
  → SQLite事务写入 → 完整性校验(hash)
Load → IDataPersistence.LoadAsync → 反序列化 → 恢复场景状态
```

### 3.6 初始化顺序

```
GameRoot.Initialize():
  0: ServiceLocator.Register<IEventBus>(new EventBus())
  1: ServiceLocator.Register<IGameStateManager>(new GameStateManager())
  2: ServiceLocator.Register<IDataPersistence>(new DataPersistence())
  3: ServiceLocator.Register<ILLMGateway>(new LLMGateway())
  4: ServiceLocator.Register<IWorldStateManager>(new WorldStateManager())
  5: ServiceLocator.Register<IAudioManager>(new AudioManager())
  6: ServiceLocator.Register<IResourceCache>(new ResourceCache())
  → ServiceLocator.FinalizeRegistration()  // 锁定，后续Register抛异常
```

---

## 4. Module Detailed Design

### 4.1 场景管理 (Scene Management)

> 详细设计见 ADR-0001（自定义 ECS 架构）和 `src/DndGame/Core/Scene.cs`（146行）。

#### 场景列表

| SceneId | C# 类 | 类型 | 说明 |
|---------|-------|:----:|------|
| MainMenu | MainMenuScene | 独立 | 新游戏/继续/设置 |
| Tavern | TavernScene | 持久 | 元游戏核心空间 |
| AdventureMap | AdventureScene | 按需加载 | 节点图探索 |
| Combat | CombatScene | 按需加载 | 战术地图战斗 |
| Loading | LoadingScene | 叠加 | 过渡动画 |
| Settlement | SettlementScene | 叠加 | 冒险完成/失败结算 |
| Dialogue | DialogueScene | 叠加 | NPC对话弹窗 |

#### 场景状态机

```
┌──────────┐   新游戏/读档    ┌────────┐
│ main_menu │ ──────────────▶ │ tavern │
└──────────┘                  └───┬────┘
                                   │
                           ┌───────┴───────┐
                           │               │
                           ▼               ▼
                     ┌────────────┐  ┌──────────┐
                     │ adventure  │  │  combat  │ ←── 从冒险或酒馆进入
                     │ _map       │  │          │
                     └──────┬─────┘  └────┬─────┘
                            │             │
                            └──────┬──────┘
                                   ▼
                            ┌───────────┐
                            │ settlement│ ──→ tavern (返回酒馆)
                            └───────────┘
```

#### 场景切换机制

- `GameRoot.StartSceneTransition()` — 延迟切换，在帧边界执行（当前帧完整渲染后才切换）
- 切换流程：旧Scene.End() → 新Scene.Initialize() → 新Scene.Begin()
- 预加载策略：ResourceCache在SceneChanging事件时按场景类型预加载资源

#### 预加载规则

| 场景 | 预加载时机 | 常驻资源 |
|------|----------|---------|
| Combat | 冒险中进入战斗节点前 | 地牢Tileset、攻击特效精灵 |
| Dialogue | 常驻内存（复用实例） | — |
| Settlement | 冒险完成时 | 结算UI面板 |
| Tavern | 游戏全程常驻 | 酒馆背景、NPC精灵 |

### 4.2 战斗引擎 (CombatEngine)

> 详细架构见 ADR-0006（战斗引擎架构 — FSM + EventBus + 数据驱动）。

#### 4.2.1 架构定位

战斗引擎是**纯程序系统**，不依赖任何LLM调用。所有数值计算由程序完成，LLM只负责接收战斗日志并生成叙事文本（通过DM Agent）。

#### 4.2.2 核心模块

```
┌──────────────────────────────────────────────────────────────┐
│                     CombatEngine (战斗引擎)                     │
│                                                                │
│  ┌──────────────────┐  ┌──────────────────┐  ┌─────────────┐  │
│  │  CombatFSM        │  │  DiceRoller      │  │  Action     │  │
│  │  · 14状态有限机   │  │  · d20检定       │  │  Resolver   │  │
│  │  · 状态守卫条件   │  │  · 优势/劣势     │  │  · 攻击结算  │  │
│  │  · 同时选择机制   │  │  · 伤害骰        │  │  · 法术结算  │  │
│  └──────────────────┘  └──────────────────┘  │  · 豁免结算  │  │
│                                                └─────────────┘  │
│  ┌──────────────────┐  ┌──────────────────┐  ┌─────────────┐  │
│  │  ConditionTracker │  │  EnemyAI         │  │  Terrain    │  │
│  │  · 14种DND条件   │  │  · 目标选择启发  │  │  Interact   │  │
│  │  · 持续时间追踪   │  │  · 行为树决策    │  │  · 8种标签  │  │
│  │  · 堆叠互斥规则   │  │  · 5种行为模式   │  │  · 环境危害  │  │
│  └──────────────────┘  └──────────────────┘  └─────────────┘  │
│                                                                │
│  ┌──────────────────┐  ┌──────────────────┐                    │
│  │  GoRogue集成      │  │  CombatLog       │                    │
│  │  · FOV视野计算    │  │  · 完整日志记录   │                    │
│  │  · A* Pathfinding │  │  · 格式化输出    │ ←──→ DM Agent     │
│  │  · ArrayMap地图   │  │  · 导出用于LLM  │                    │
│  └──────────────────┘  └──────────────────┘                    │
└──────────────────────────────────────────────────────────────┘
```

#### 4.2.3 14状态FSM定义

> 完整FSM定义和转换守卫条件见 ADR-0006 §"14 状态 FSM 定义"。

关键偏离（相对于标准 DND 5e）：
- ROLL_INITIATIVE: 每轮重新骰先攻（非整场固定）
- ACTION_PHASE: 所有玩家同时选择，按先攻顺序结算
- 暴击: 伤害骰取最大值（非双骰）
- 死亡: 3轮无治疗 = 死亡（非3成功/3失败）
- 疲劳: 3级（非6级）
- DEFEAT: 触发失败结算而非战斗结束

#### 4.2.4 GoRogue集成

> GoRogue版本锁定为2.6.4（ADR-0007）。封装层为 `GoRogueMapManager.cs`（109行），使用2.6.4 API：Coord、ArrayMap<bool>、FOV、AStar、Distance.MANHATTAN。

#### 4.2.5 事件结果分离模型（战斗特化）

```
机械结果（CombatEngine 程序控制）:
  · 攻击命中/未命中 · 伤害数值 · 暴击判定
  · 法术DC和豁免结果 · 死亡豁免轮数
  · 条件施加/移除 · AI目标选择 · Boss阶段切换

叙事结果（LLM DM Agent 生成，通过 IEventBus 异步调用）:
  · 攻击描述文本 · 击杀台词 · 暴击特殊描述
  · Boss阶段转换演出文本 · 环境氛围描写

执行流程:
  1. CombatEngine计算机械结果
  2. EventBus.Publish(DamageDealt等事件)
  3. LLMGateway订阅事件 → DM Agent生成叙事文本
  4. NarrativeReady事件 → UI显示叙事+伤害数字
```

### 4.3 角色系统 (CharacterSystem)

> 详细数据模型和冻结协议见 ADR-0008（角色数据模型冻结协议）。

#### 数据模型分层

```
角色数据 = 数值层 (程序控制，100%可测试) + 叙事层 (LLM生成，Schema约束)

数值层 (FROZEN — ADR-0008):
  · 六维属性 (STR/DEX/CON/INT/WIS/CHA) + 调整值
  · HP/AC/速度/熟练加值/先攻/法术位
  · 职业等级/子职业/已学特性
  · 种族/亚种/种族特性
  · 技能熟练/专精/专长
  · 装备槽（主手/副手/护甲/饰品等9槽位）
  · 条件/伤疤/疲乏等级
  · 关系值 (trust×conflict 双轴模型)

叙事层 (LLM生成，Schema约束):
  · 姓名/性别/年龄 · 性格标签 (6维度)
  · 背景故事 · 外观描述 · 个人目标
  · 冒险记忆 · 伤疤叙事描述
```

#### 角色生成管线

```
Phase 1: 程序化数值层
  输入: race + class + target_level
  → Standard Array + 种族加成 → 六维属性
  → 职业进阶表 → HP/熟练加值/特性
  → 衍生值计算 → AC/技能调整值/法术位
  → 起始装备分配
  输出: CharacterNumericalBlock (纯JSON)

Phase 2: LLM叙事层 (通过文案Agent)
  输入: race + class + numerical_summary
  → Schema约束生成 → name/gender/personality_tags/backstory/appearance/goal
  → 最多3次重试 → 失败使用模板
  输出: CharacterNarrativeBlock

Phase 3: 合并写入
  → 合并Phase 1 + Phase 2 → 写入SQLite → 触发CharacterCreated事件
```

### 4.4 酒馆系统 (TavernSystem)

> 详细设计见 `design/gdd/07-tavern-system.md`。

酒馆系统负责冒险之间的**元游戏层**。所有数值由程序控制，LLM只负责招募文本、事件描述和NPC对话。

#### 酒馆升级路径

| 等级 | 解锁 | 关键功能 |
|:----:|------|----------|
| Lv1 | 大厅 | 基础招募、基础任务板、基础休息 |
| Lv2 | 铁匠铺 | 装备修复/打造 |
| Lv3 | 炼金台 | 药水/卷轴制作 |
| Lv4 | — | 中冒险解锁、角色槽+1 |
| Lv5 | 图书馆 | 法术学习、新职业/专长 |
| Lv6 | — | 关系系统深化、酒馆事件启用 |
| Lv7 | 神殿 | 复活角色、移除诅咒 |
| Lv8 | — | 长冒险解锁、传奇任务线 |
| Lv9 | — | 双任务并发、英雄传记完整 |
| Lv10 | 英雄之壁完整 | 传说装备、全功能巅峰 |

### 4.5 冒险系统 (AdventureGeneration)

> 详细设计见 `design/gdd/06-adventure-generation.md`。

#### 三层生成管线（核心架构）

```
第一层：冒险蓝图生成（冒险开始前）
  编剧Agent + Schema验证 + 业务逻辑验证
  → adventure_blueprint.json (严格Schema约束)
  → 失败策略：3次重试 → 离线模板

第二层：程序化实例化（冒险开始前）
  AdventureInstantiator (纯程序，不依赖LLM，可单元测试)
  14步算法：蓝图解析→节点图→房间模板→GoRogue地牢→遭遇→对话→探索→商人→休息→谜题→Boss→走廊→交互标签→状态机

第三层：DM Agent实时叙事（冒险进行中）
  玩家每个动作后按需调用DM Agent（<2秒响应时间）
  · 进入新房间 → scene_atmosphere
  · NPC对话 → npc_dialogue + options
  · 检定结果 → skill_check_result
  · 战斗动作 → combat_narration
  失败降级：静态模板
```

### 4.6 LLM Gateway

> 详细架构见 ADR-0004（LLM集成架构 — 皮肤层 + 事件结果分离模型）。

#### Agent调度表

| Agent | 职责 | 调用时机 | 阻塞模式 | Token预算 | 模型等级 |
|-------|------|----------|:--------:|:---------:|:--------:|
| 编剧Agent | 生成冒险蓝图 | 冒险开始前 | 异步(加载等待) | 4000/2000 | 高质量 |
| DM Agent | 实时叙事 | 玩家每个动作后 | 异步(不阻塞UI) | 2000/500 | 中等 |
| 文案Agent | 物品/技能描述 | 新物品获得时 | 异步(后台生成) | 1000/300 | 轻量 |

> ⚠️ **v3.0修正**：BalancerAgent（蓝图验证）已从GDD v1.2移除。蓝图验证功能合并到编剧Agent的Schema验证管线中。

#### 请求生命周期

```
1. Pre-Validate (本地校验) → 失败返回GatewayError
2. Cache Lookup (语义Hash查SQLite缓存) → 命中跳到Step 7
3. Rate Check (per-agent配额+全局配额) → 超限降级
4. Send to API (HttpClient → 云端LLM) → 构造请求体+注入system prompt
5. Parse (System.Text.Json提取) → 处理Markdown代码块包裹
6. Validate (JsonSchema.Net + Business Logic) → 失败最多3次重试
7. Cache Write (写入SQLite缓存 via sqlite-net)
8. Post-Process (填充默认值、业务预处理)
9. 返回 AgentResponse 给调用方
```

#### 降级链

```
Primary Model → Secondary Model → Tertiary Model → Cached Response → Static Template

示例 (编剧Agent):
  primary:   OpenAI GPT-4o
  secondary: OpenAI GPT-4o-mini
  tertiary:  Anthropic Claude-3-Haiku
  fallback:  adventure_blueprint_templates.json (内置50+模板)
```

### 4.7 世界状态管理器 (WorldStateManager)

负责追踪冒险成功/失败对游戏世界的**永久影响**。数据存储在SQLite中，在生成下一次冒险时作为LLM的上下文输入。

```
世界状态 = 持久变化的数据，影响后续游戏
  区域状态: 各区域安全/危险等级、已解锁/摧毁的城镇和地标
  势力关系: 各势力对酒馆友好度 (hostile/neutral/friendly/allied)
  世界事件: 正在发生的重大事件 (瘟疫/战争/天灾)、玩家行动触发的连锁反应
  冒险印记: 已完成/失败的冒险记录、未完成的任务线、特殊NPC生死状态
```

### 4.8 UI系统 (UISystem)

> 详细设计见 `design/gdd/09-ui-ux-design.md`。

#### 设计原则

1. 像素风一致: FF6风格窗口框架，深色镶边+深色半透明背景
2. 数据与表现分离: UI只负责展示，逻辑由各系统处理
3. 响应式布局: Myra Grid + StackPanel 适配不同分辨率
4. 骰子可见: 玩家可查看每个骰子的完整计算过程
5. 战斗日志双轨: LLM叙事文本+程序数值日志并行展示
6. 代码构建: 所有UI通过Myra XML布局定义 + C#代码控制逻辑

#### UI子系统划分

| UI模块 | 关联系统 | 核心Myra组件 |
|--------|----------|-------------|
| 酒馆UI | TavernSystem | Grid, ListBox, Button, Label — 招募板、任务板、商店面板 |
| 战斗UI | CombatEngine | Grid, HorizontalStackPanel, ScrollViewer — 先攻条、行动菜单、战斗日志 |
| 地图UI | AdventureSystem | 自定义SpriteBatch渲染 + Myra叠加层 — 节点图、小地图 |
| 角色面板 | CharacterSystem | TabControl, Grid, Label — 属性页、装备页、技能页 |
| 对话UI | LLMGateway | TextBox, ListBox — 对话窗口、选项列表 |
| 结算UI | SettlementSystem | Grid, Label — 成功/失败画面、奖励展示 |
| 主菜单 | GameState | Menu, Button — 新游戏、继续、设置 |

#### 战斗日志双轨机制

```
战斗日志 = 程序日志 + LLM叙事

程序日志 (程序生成，格式固定):
  "[战士] 攻击 [哥布林]: d20(13) + 5 = 18 vs AC 15 → 命中"
  "[战士] 伤害: 1d8(6) + 3 = 9 斩击"

LLM叙事 (DM Agent生成，描述性文本):
  "索林怒吼着挥动长剑，剑气划破空气，
  在哥布林肮脏的皮甲上留下一道深深的伤口。"

两者并行展示在战斗日志面板中。
```

---

### 4.9 结算系统 (SettlementSystem)

> 详细设计见 `design/gdd/08-failure-growth.md`。需在 Settlement Epic 前创建专门 ADR（见 §8 Required ADRs）。

#### 核心职责

结算系统在每次冒险结束后执行，计算玩家奖励/惩罚并更新世界状态。是 FEATURE-L3 复合系统，依赖 CharacterSystem、CombatEngine（通过 IEventBus 事件）、WorldStateManager、LLMGateway。

#### 结算管线

```
冒险结束(成功/失败/撤退)
  → 步骤1: XP计算 — 根据战斗难度、敌人数量、冒险深度计算经验值
  → 步骤2: 等级提升 — 检查角色是否升级，触发等级提升流程
  → 步骤3: 战利品生成 — 根据冒险主题和CR预算生成战利品
  → 步骤4: 伤疤判定 — 角色死亡/重伤时判定伤疤类型与数值影响
  → 步骤5: 惩罚计算 — 撤退/失败的资源惩罚与关系损失
  → 步骤6: 世界状态更新 — 更新区域安全等级、势力关系、冒险印记
  → 步骤7: 叙事生成 — LLM生成冒险结局叙事（可选，离线时用模板）
  → 结算UI展示
```

#### 关键数据结构

- `SettlementResult`: 封装一次结算的完整结果（XP、战利品、伤疤、世界变更）
- `ScarData`: 伤疤类型 + 数值影响（永久属性减益、外观变化、叙事标签）
- `LootTable`: 按CR预算的战利品掉落表（JSON 配置驱动）

#### 接口约定

- 通过 `IEventBus.CombatEnded` 事件触发结算流程，不直接引用 CombatEngine
- XP 计算公式和战利品表定义在 JSON 配置中，不硬编码
- 伤疤判定使用独立随机数种子，确保可复现

---

## 5. API Boundaries

### 5.1 系统间接口规范

```
1. 所有接口参数为 C# record 或 JsonElement
2. 所有接口返回 record 或 void（通过 EventBus 传递结果）
3. LLM相关接口使用 async Task<T> — await LLMGateway.CallAgent()
4. 非LLM接口使用同步调用
5. EventBus 事件数据使用 C# record（轻量、不可变、编译期类型安全）
```

### 5.2 关键接口合同

| 边界 | 接口 | 关键方法 | 调用方 |
|------|------|---------|--------|
| EventBus↔所有系统 | IEventBus | Subscribe/Publish/Unsubscribe | 全系统 |
| ServiceLocator↔所有系统 | ServiceLocator(static) | Register/Get/FinalizeRegistration | 全系统 |
| Combat↔Character | ICharacterCombatData | GetHP/GetAC/GetAbilityModifier/GetSavingThrowModifier | CombatEngine |
| Combat↔Items | IWeaponData / IArmorData | GetDamageDice/GetDamageBonus/GetACBonus/GetEnchantments | CombatEngine |
| Combat↔Conditions | IConditionTracker | Apply/Remove/GetActiveConditions/CheckImmunity | CombatEngine |
| LLM↔冒险 | ILLMGateway.CallAgent | CallAgent<ScreenwriterAgent,AdventureBlueprint> | AdventureSystem |
| LLM↔战斗叙事 | ILLMGateway.CallAgent | CallAgent<DMAgent,NarrativeText> | EventBus订阅→UI |
| Tavern↔Character | ICharacterSystem | GetCharacter/SaveCharacter/LevelUp | TavernSystem |
| Save↔Persistence | IDataPersistence | SaveAsync/LoadAsync/CreateSaveSlot | SaveSystem |
| Tavern↔Items | IItemSystem | GetItem/EquipItem/UnequipItem/GetInventory | TavernSystem |
| Adventure↔World | IWorldStateManager | GetRegionState/GetFactionRelation/GetContextForLLM | AdventureSystem |
| UI↔Audio | IAudioManager | PlayBGM/StopBGM/PlaySFX/SetMasterVolume | UISystem |

### 5.3 关键不变量

- CombatEngine只通过 `ICharacterCombatData` 接口读取角色战斗数据，不直接引用 `CharacterSystem` 类
- LLM输出必须通过Schema验证后才进入游戏系统（3次重试→降级模板）
- EventBus事件使用C# record类型——轻量、不可变、编译期类型安全
- 所有数值由程序层计算，LLM只生成叙事文本
- 角色数据模型FROZEN字段不可删除、重命名、改变类型（ADR-0008）
- 扩展操作仅允许新增（枚举值/可选字段），不修改已有定义

---

## 6. Technology Stack & Project Structure

### 6.1 核心技术栈

| 层级 | 选型 | 版本 | 说明 |
|------|------|:----:|------|
| **游戏框架** | MonoGame | 3.8.5+ | MIT许可证，跨平台，C#/.NET原生 |
| **语言** | C# | 12+ | .NET 8+，record类型、模式匹配 |
| **场景/实体** | 自定义 ECS | — | Scene/Entity/Component/SceneComponent，GameRoot管理生命周期（ADR-0001） |
| **Roguelike工具** | GoRogue | **2.6.4** | FOV视野、A*寻路、ArrayMap — 版本锁定（ADR-0007） |
| **扩展框架** | MonoGame.Extended | **6.0.0** | 扩展功能(含Tiled地图支持)、Content Pipeline | 
| **UI框架** | Myra | 1.5.* | 像素风UI，XML布局+数据绑定 |
| **数据持久化** | sqlite-net | 最新 | 轻量ORM，SQLite原生绑定（ADR-0005） |
| **LLM集成** | HttpClient + System.Text.Json | .NET内置 | HTTP/JSON原生支持 |
| **Schema验证** | JsonSchema.Net | 最新 | JSON Schema Draft 7+验证库 |
| **字体渲染** | FontStashSharp.MonoGame | 1.5.* | 动态字体+简体中文支持 |
| **内容管线** | MGCB (MonoGame Content Builder) | — | 纹理/音效/字体编译 |
| **单元测试** | xUnit + FluentAssertions | 最新 | C#标准测试栈 |

> ⚠️ **v3.0修正**：GoRogue版本从文档中的"3.x"统一锁定为2.6.4（ADR-0007）。MonoGame.Extended 6.0.0 已安装（含 Tiled 地图支持）。Myra/FontStashSharp 锁定为 1.5.* 稳定线。

### 6.2 项目结构

```
src/DndGame/
├── Core/                           # FOUNDATION层 (已实现)
│   ├── ServiceLocator.cs           # 全局服务注册（126行）
│   ├── EventBus.cs                 # 事件总线（136行）
│   ├── GameRoot.cs                 # MonoGame主入口（200行）
│   ├── Scene.cs                    # 场景基类（146行）
│   ├── Entity.cs                   # 实体组合（158行）
│   ├── Component.cs                # 实体组件（55行）
│   ├── SceneComponent.cs           # 场景级组件（56行）
│   └── GameStateManager.cs         # 场景状态管理
├── Systems/                        # CORE + FEATURE层（待实现）
│   ├── Combat/                     # 战斗引擎（ADR-0006蓝图）
│   ├── Character/                  # 角色系统（ADR-0008冻结协议）
│   ├── Items/                      # 物品装备系统
│   ├── Tavern/                     # 酒馆系统
│   ├── Adventure/                  # 冒险生成系统
│   ├── WorldState/                 # 世界状态管理器
│   └── Map/                        # 地图探索系统（GoRogue 2.6.4）
├── Gateway/                        # LLM网关（ADR-0004蓝图）
│   ├── LLMGateway.cs
│   ├── Agents/                     # 编剧/DM/文案Agent
│   ├── Validation/                 # Schema验证器
│   ├── Cache/                      # 语义缓存
│   └── Fallback/                   # 离线降级+模板
├── Scenes/                         # 场景子类
├── Entities/                       # 实体定义
├── UI/                             # Myra面板+自定义Widget
├── Data/                           # 数据层
│   ├── Config/                     # JSON配置表（ADR-0005）
│   ├── Schemas/                    # JSON Schema（7个）
│   └── Database/                   # sqlite-net ORM
└── Content/                        # MGCB资源
    ├── Sprites/                    # 精灵图
    ├── Fonts/                      # FontStashSharp字体
    └── Audio/                      # BGM/SFX

tests/DndGame.Tests/                # 测试
└── Unit/                           # ServiceLocator(5) + EventBus(8) = 13测试

Data/                               # 游戏数据目录
├── config/                         # JSON配置（races/classes/spells/monsters/equipment）
├── schemas/                        # JSON Schema（7个Agent输出约束）
└── templates/                      # 离线降级模板（冒险/叙事/描述）
```

### 6.3 JSON Schema 中心管理

Schema文件集中在 `Data/Schemas/`，LLM Gateway运行时通过 `JsonSchema.Net` 加载使用：

| Schema | Agent | 用途 |
|--------|-------|------|
| adventure_blueprint.schema.json | 编剧Agent | 冒险蓝图输出约束 |
| narrative_text.schema.json | DM Agent | 实时叙事输出约束 |
| npc_dialogue.schema.json | DM Agent | NPC对话输出约束 |
| item_description.schema.json | 文案Agent | 物品描述输出约束 |
| character_narrative.schema.json | 文案Agent | 角色叙事层约束 |
| balance_report.schema.json | 蓝图验证 | 冒险难度验证 |
| penalty_result.schema.json | DM Agent | 结算叙事约束 |

### 6.4 语言政策

| 上下文 | 语言 | 示例 |
|--------|------|------|
| 面向玩家的叙事文本 | 简体中文 | "你推开酒馆的木门..." |
| UI界面文字 | 简体中文 | "招募"、"装备"、"法术" |
| 内部技术标识符 | 英文PascalCase/camelCase | CharacterId, adventureBlueprint |
| JSON Schema字段名 | 英文snake_case | plot_outline, difficulty_profile |
| 枚举值 | 英文PascalCase | Combat, Dialogue, Exploration |
| LLM Agent System Prompt | 中文(要求输出中文叙事) | 见02-llm-integration.md |
| XML文档注释(///) | 简体中文 | 所有public API |

---

## 7. ADR Audit

### 7.1 ADR质量检查

| ADR | Engine Compat | Version | GDD Linkage | 与本架构冲突 | Valid |
|-----|:---:|:---:|:---:|------|:---:|
| ADR-0000: MonoGame选型 | ✅ | ✅ 3.8.5+ | ✅ 8项需求 | 无 | ✅ |
| ADR-0001: 自定义ECS | ✅ | ✅ | ✅ 6项需求 | 无（Scene归入FOUNDATION与ADR一致） | ✅ |
| ADR-0002: ServiceLocator | ✅ | ✅ | ✅ 4项需求 | 无 | ✅ |
| ADR-0003: EventBus | ✅ | ✅ | ✅ 6项需求 | 无 | ✅ |
| ADR-0004: LLM皮肤层 | ✅ | ✅ | ✅ 7项需求 | ⚠️ 层级定位从"独立LLM层"→CORE层服务（原则不变） | ✅ |
| ADR-0005: 数据驱动设计 | ✅ | ✅ | ✅ 6项需求 | 无 | ✅ |
| ADR-0006: CombatEngine架构 | ✅ | ✅ | ✅ 10项需求 | 无 | ✅ |
| ADR-0007: GoRogue版本锁定 | ✅ | ✅ 2.6.4 | ✅ 5项需求 | 无 | ✅ |
| ADR-0008: 角色数据冻结 | ✅ | ✅ | ✅ 10项需求 | 无 | ✅ |

**结果**：9/9 ADR全部Valid。唯一调整是ADR-0004的层级定位描述更新——从"LLM集成层（独立层）"调整为"CORE层服务"。**核心决策（LLM=皮肤层）不变。**

### 7.2 TR 注册表（技术需求基线）

从 12份 GDD + 7份 Quick Spec + 1份核心GDD 中提取的完整技术需求基线，共计 **1,211 条 TR**（1,149基础 + 34 failgrowth补读 + 28 combat补读）。每条 TR 的详细描述见对应 GDD/Quick Spec 原文，此处以索引表形式记录编号范围、数量、域分布和 ADR 覆盖映射。

#### 7.2.1 GDD 子系统 TR

| 来源 | Slug | 编号范围 | 数量 | 架构层 | 主要域 | ADR覆盖 |
|------|------|----------|:----:|--------|--------|---------|
| 01-角色系统 | `character` | 001~136 | 136 | CORE | Core(68), Data(48), Cross(14), Perf(4) | ADR-0008(数据冻结+契约), ADR-0003(EventBus) |
| 02-LLM集成 | `llm` | 001~079 | 79 | CORE+FEATURE | Network(29), Core(30), Data(10), Perf(10) | ADR-0004(皮肤层), ADR-0005(数据驱动) |
| 03-物品装备 | `items` | 001~071 | 71 | FEATURE-L2 | Data(35), Core(30), UI(3), Perf(3) | ADR-0005(数据驱动), ADR-0008(条件追踪) |
| 04-战斗系统 | `combat` | 001~114 | 114 | FEATURE-L2 | Core(72), Data(22), UI(8), Network(4), Cross(4), Perf(4) | ADR-0006(FSM+数据驱动), ADR-0007(GoRogue) — 补读+28: Edge(6∈Core)+Retreat(4∈Core)+Boss(2∈Core)+Balance(3∈Perf)+Tunable(10∈Data)+Mock(1∈Perf)+v2AC(12∈Core) |
| 05-地图探索 | `map` | 001~172 | 172 | FEATURE-L3 | Core(60), Data(50), UI(40), Perf(22) | ADR-0007(GoRogue 2.6.4), ADR-0001(ECS) |
| 06-冒险生成 | `advgen` | 001~084 | 84 | FEATURE-L3 | Core(40), Data(30), Network(10), Perf(4) | ADR-0004(L1蓝图), 本文档(L2/L3实例化) ⚠️ |
| 07-酒馆系统 | `tavern` | 001~093 | 93 | FEATURE-L1+L2 | Core(30), Data(35), UI(15), Network(8), Perf(5) | 无专门ADR（本文档§4.4） ⚠️ |
| 08-失败成长 | `failgrowth` | 001~124 | 124 | FEATURE-L1+L3 | Core(40), Data(30), Network(10), Save(8), UI(5), Perf(2), Economy(6), Settlement(6), Cross-system(3) | 无专门ADR（本文档§4.4） ⚠️ |
| 09-UI/UX | `ui` | 001~134 | 134 | PRESENTATION | UI(95), Perf(25), Phys(14) | 无专门ADR（本文档§4.8） |
| 10-条件效果 | `condition` | 001~120 | 120 | CORE | Core(65), Data(40), UI(15) | ADR-0008(Extension-Only枚举), ADR-0003(EventBus) |
| 11-敌人AI | `ai` | 001~099 | 99 | FEATURE-L2 | Core(65), Data(30), Perf(4) | ADR-0006(行为映射), ADR-0008(条件查询) |

#### 7.2.2 Quick Spec TR

| 来源 | Slug | 编号范围 | 数量 | 架构层 | 主要域 | ADR覆盖 |
|------|------|----------|:----:|--------|--------|---------|
| 骰子系统 | `dice-system` | 001~026 | 26 | FOUNDATION | Core(20), Data(5), Testing(1) | ADR-0006(DiceRoller纯函数) |
| 场景管理 | `scene-management` | 001~013 | 13 | FOUNDATION | Core(9), UI(3), Data(1) | ADR-0001(自定义ECS), ADR-0002(初始化顺序) |
| 对话系统 | `dialogue-system` | 001~023 | 23 | FEATURE-L1 | Core(14), UI(5), Cross(3), Perf(1) | ADR-0003(EventBus), ADR-0004(LLM皮肤层) |
| 设置选项 | `settings-options` | 001~008 | 8 | FOUNDATION | Core(2), UI(5), Save(1) | 无需专门ADR（独立Meta系统） |
| 音频系统 | `audio-system` | 001~011 | 11 | FOUNDATION | Audio(10), Perf(1) | 无需专门ADR（简单IAudioManager） |
| 存档系统 | `save-system` | 001~010 | 10 | FOUNDATION | Save-Load(10) | ADR-0002(初始化顺序), ADR-0005(数据驱动) |
| 世界状态 | `world-state` | 001~044 | 44 | CORE | Data(20), Core(12), Cross(8), Testing(4) | ADR-0003(EventBus), ADR-0005(数据驱动) |

#### 7.2.3 核心GDD TR

| 来源 | Slug | 编号范围 | 数量 | 架构层 | 主要域 | ADR覆盖 |
|------|------|----------|:----:|--------|--------|---------|
| GDD-v1 | `gdd-v1` | 001~148 | 148 | 全层 | Core(54), Data(16), UI(14), Network(16), Perf(3), Save(3) | 全部ADR（综合验证源） |

**注**：gdd-v1 的约40条 TR 与子系统 GDD 重复——核心GDD作为权威验证源，子系统GDD作为实现规格。两端通过 TR 编号交叉引用，不删除重复项。详细交叉引用如下：

| gdd-v1 TR | 子系统 TR | 重复主题 | 关系 |
|-----------|----------|----------|------|
| TR-gdd-v1-003 | TR-llm-001~030 | LLM=皮肤层原则 | gdd-v1定义原则，llm细化实现 |
| TR-gdd-v1-005 | TR-combat-039, TR-failgrowth-037 | 死亡双轨制(MAX_FAILURES=1) | gdd-v1定义规则，combat/failgrowth各实现部分 |
| TR-gdd-v1-006 | TR-character-084~096 | 关系值[-5,+5]+战斗触发 | gdd-v1概要，character细化矩阵 |
| TR-gdd-v1-024~034 | TR-advgen-001~084 | 三层冒险生成管线 | gdd-v1定义管线架构，advgen细化14步实例化 |
| TR-gdd-v1-037 | TR-combat-039, TR-failgrowth-037 | 死亡双轨(3轮无治/3失败) | 同规则两端引用 |
| TR-gdd-v1-038 | TR-condition-032~038 | 疲乏3级制 | gdd-v1定义偏离，condition细化各级效果 |
| TR-gdd-v1-039 | TR-character-016~017 | 槽位负重(10+STR×2) | 同公式两端引用 |
| TR-gdd-v1-040 | TR-combat-007~009 | 先攻每轮重掷 | gdd-v1定义偏离，combat细化FSM |
| TR-gdd-v1-042 | TR-combat-020 | 暴击取最大值 | gdd-v1定义偏离，combat细化伤害管线 |
| TR-gdd-v1-046 | TR-character-039~051 | 3MVP职业完整特性 | gdd-v1定义MVP范围，character细化每级特性 |
| TR-gdd-v1-047 | TR-map-107~138 | 交互标签枚举 | gdd-v1定义标签集，map细化每个标签数据模型 |
| TR-gdd-v1-048 | TR-condition-001~027 | 8种MVP条件 | gdd-v1定义MVP范围，condition细化完整生命周期 |
| TR-gdd-v1-049~055 | TR-failgrowth-001~090 | 结算+伤疤+世界状态 | gdd-v1概要，failgrowth细化全部数据 |
| TR-gdd-v1-057~063 | TR-llm-044~079 | Gateway+Schema+缓存 | gdd-v1概要，llm细化6子组件 |
| TR-gdd-v1-064~069 | TR-ui-001~134 | 像素规格+FF6风格 | gdd-v1定义视觉风格，ui细化每个像素参数 |
| TR-gdd-v1-082~089 | TR-combat-017~086 | 6条战斗AC | gdd-v1验收标准，combat细化全部86条TR |
| TR-gdd-v1-123~129 | TR-character-018~028, TR-combat-007~042 | 6个核心公式 | gdd-v1公式汇总，子系统各自展开实现细节 |

#### 7.2.4 总量统计

| 统计维度 | 数值 |
|----------|------|
| TR 总数 | **1,211** (1,149基础 + 34 failgrowth补读 + 28 combat补读) |
| 来源数 | 19 (11 GDD + 7 QS + 1 核心GDD) |
| ADR 完全覆盖的 TR | ~870 (72%) |
| 架构文档覆盖的 TR | ~200 (17%) — 无专门ADR但本文档§4有详细设计 |
| 待补充ADR的 TR | ~141 (11%) — 冒险实例化/酒馆/结算三个Feature层系统 |

**按架构层分布：**

```
PRESENTATION ─── ui(134) ──────────────────────── 134 (11%)
FEATURE-L3 ── map(172)+advgen(84)+tavern(93)+failgrowth(124) ── 473 (39%)
FEATURE-L2 ── combat(114)+items(71)+ai(99) ────────── 284 (23%)
FEATURE-L1 ── dialogue(23)+tavern(基础)+failgrowth(结算) ─── ~50 (4%)
CORE ──── character(136)+condition(120)+world-state(44)+llm(79) ── 379 (31%)
FOUNDATION ── dice(26)+scene(13)+save(10)+audio(11)+settings(8) ── 68 (6%)
```

> **注**：gdd-v1(148)横跨全部层级，已在各层分布中计入重叠部分而非独立叠加。百分比以去重后的有效TR估算。

**按域分类：**

| 域 | TR 估计数 | 占比 |
|----|----------|------|
| Core | ~570 | 47% |
| Data | ~210 | 17% |
| UI | ~200 | 17% |
| Network | ~80 | 7% |
| Performance | ~40 | 3.3% |
| Save-Load | ~30 | 2.5% |
| Cross-system | ~20 | 1.7% |
| Audio | ~10 | 0.8% |

#### 7.2.5 关键TR→ADR 映射（原17项保留）

| TR ID | 需求 | ADR覆盖 | 状态 |
|-------|------|---------|------|
| TR-character-001 | CharacterData record模型 | ADR-0008 | ✅ |
| TR-character-002 | 六维属性+公式FROZEN | ADR-0008 | ✅ |
| TR-combat-001 | 14状态FSM | ADR-0006 | ✅ |
| TR-combat-002 | DND 5e偏离(6项) | ADR-0006 | ✅ |
| TR-combat-003 | DiceRoller纯函数 | ADR-0006 | ✅ |
| TR-combat-004 | 14种条件追踪 | ADR-0006+ADR-0008 | ✅ |
| TR-llm-001 | LLM=皮肤层原则 | ADR-0004 | ✅ |
| TR-llm-002 | Schema验证管线 | ADR-0004 | ✅ |
| TR-llm-003 | 离线降级路径 | ADR-0004 | ✅ |
| TR-map-001 | GoRogue FOV/A*(2.6.4) | ADR-0007 | ✅ |
| TR-data-001 | JSON+SQLite双层数据 | ADR-0005 | ✅ |
| TR-ecs-001 | 自定义Scene/Entity/Component | ADR-0001 | ✅ |
| TR-service-001 | ServiceLocator初始化顺序 | ADR-0002 | ✅ |
| TR-event-001 | EventBus跨系统解耦 | ADR-0003 | ✅ |
| TR-adventure-001 | 三层生成管线 | ADR-0004(L1) + 本文档(L2/L3) | ⚠️ 部分覆盖 |
| TR-tavern-001 | 酒馆元游戏循环 | 无专门ADR（本文档§4.4） | ⚠️ 依赖架构文档 |
| TR-settlement-001 | 失败/惩罚/伤疤 | 无专门ADR（本文档§4.4） | ⚠️ 依赖架构文档 |

**覆盖率**：1,211 条 TR 中 ~870 条(72%)完全由 ADR 覆盖，~200 条(17%)由本文档详细设计覆盖，~141 条(11%)待在对应 Epic 开始前补充专门 ADR。

---

## 8. Required ADRs

### 必须在编码开始前创建（Foundation & Core决策）

**无** — 所有Foundation和Core层决策已有ADR覆盖（ADR-0000~0008）。

### 应在对应系统构建前创建（Feature层决策）

| 优先级 | ADR标题 | 覆盖TR | 何时创建 |
|:------:|---------|--------|---------|
| HIGH | 冒险实例化14步算法架构 | TR-adventure-001(L2/L3) | Adventure Epic开始前 |
| HIGH | 酒馆系统服务架构 | TR-tavern-001 | Tavern Epic开始前 |
| MEDIUM | 结算/伤疤/传承管线架构 | TR-settlement-001 | Settlement Epic开始前 |

### 可以推迟到实现时创建

| 优先级 | ADR标题 | 说明 |
|:------:|---------|------|
| LOW | UI系统架构(Myra集成) | 本文档§4.8已充分描述，无需专门ADR |
| LOW | 音频系统架构 | 简单IAudioManager服务，无需专门ADR |
| LOW | 存档系统架构 | 依赖IDataPersistence接口，无需专门ADR |
| LOW | 设置选项系统 | 独立系统，无复杂架构决策 |

---

## 9. Architecture Principles

从游戏概念、GDD和9个ADR提炼的5条架构原则：

### 原则1：LLM = 皮肤层，程序 = 骨骼层

LLM只生成叙事文本（对话、描述、氛围）。程序控制所有数值、规则判定和分支逻辑。这一原则确保游戏核心循环可离线运行，LLM是增量体验而非必需品。（来源：ADR-0004、GDD §1.2）

**违反信号**：任何让LLM直接决定数值（伤害、DC、战利品）的代码或设计。

### 原则2：数据驱动设计

游戏数值（种族属性、职业进阶、装备参数、法术伤害、怪物模板）存储在JSON配置和SQLite中，不硬编码在C#中。C#代码只负责规则逻辑和公式计算。（来源：ADR-0005）

**违反信号**：代码中出现魔法数字（`= 10`）而非从配置加载或使用命名常量。

### 原则3：接口优于实现，系统通过IEventBus解耦

系统间禁止直接引用对方的具体类。所有跨系统通信通过IEventBus。新订阅者无需修改发布者代码。（来源：ADR-0003）

**违反信号**：CombatEngine中出现 `using CharacterSystem` 直接引用。

### 原则4：角色数据契约冻结

CharacterData核心字段FROZEN——不可删除、重命名、改变类型。Extension-Only规则允许新增（枚举值/可选字段）。任何Frozen字段的破坏性变更必须走 `/propagate-design-change` 6步审批流程。（来源：ADR-0008）

**违反信号**：PR中直接删除或重命名Frozen字段而未走变更流程。

### 原则5：编译器即验证器

`dotnet build`作为AI代码质量第一道防线。TreatWarningsAsErrors=true。编译器在2-5秒内捕获语法错误、类型错误、空引用风险、switch穷尽性。所有代码必须通过 `dotnet build` + `dotnet test` 才能提交。（来源：ADR-0000、03-vibe-coding-conventions.md）

**违反信号**：代码提交时 `dotnet build` 有错误或警告；使用 `#pragma warning disable`。

---

## 10. Open Questions

| # | 问题 | 影响层级 | 何时必须解决 | 备注 |
|---|------|---------|-------------|------|
| 1 | Game1.cs死代码处理 | FOUNDATION | Sprint 1开始前 | Program.cs创建GameRoot，Game1.cs从未实例化——删除或标记为废弃 |
| 2 | ServiceRegistration补全4个服务注册+FinalizeRegistration() | FOUNDATION | Sprint 1开始前 | 当前只注册3个服务(IEventBus/IGameStateManager/IFontService) |
| 3 | BalancerAgent旧引用清理 | CORE | 本文档已完成 | 已从GDD v1.2移除，architecture.md v3.0已清理 |
| 4 | GoRogue版本文档统一 | FOUNDATION | 本文档已完成 | 已统一锁定为2.6.4(ADR-0007) |
| 5 | MonoGame.Extended.Tiled 使用策略 | FOUNDATION | Map Epic开始前 | MonoGame.Extended 6.0.0 已安装（含 Tiled 支持），需决策地图格式（Tiled vs GoRogue 程序化生成 vs 混合） |
| 6 | Myra 像素风 UI 原型验证 | PRESENTATION | Sprint 1开始前 | Myra/FontStashSharp 已锁定 1.5.*，需验证 134 TR UI 规格可实现性（FF6 风格） |
| 7 | ICharacterCombatData接口契约细化 | CORE | Combat Epic开始前 | CombatEngine如何读取角色战斗数据的具体接口定义 |
| 8 | 冒险实例化14步算法是否需要专门ADR | FEATURE | Adventure Epic开始前 | 建议创建（见§8 Required ADRs） |
| 9 | .editorconfig和Directory.Build.props缺失 | FOUNDATION | Sprint 1开始前 | 代码风格统一和构建属性集中管理 |
| 10 | ConditionTracker从CombatEngine子模块升级为独立系统(ADR-0006讨论) | CORE | Condition Epic开始前 | ADR-0006保留为内部类，实际开发时可独立 |

### 10.1 签批条件追踪（TD + LP 双轨签批，2026-05-11）

**TD-ARCHITECTURE: APPROVED** — 4 项条件已全部修复：

| # | 条件 | 严重度 | 状态 |
|---|------|:------:|:----:|
| TD-1 | §5.2 补全缺失接口合同（IItemSystem/IWorldStateManager/IAudioManager） | ⚠️ | ✅ 已修复 |
| TD-2 | §2.3 SettlementSystem Consumes 列修正：`CombatEngine`→`IEventBus(CombatEnded)` | ⚠️ | ✅ 已修复 |
| TD-3 | §4 补充 SettlementSystem 详细设计子章节（§4.9） | ⚠️ | ✅ 已修复 |
| TD-4 | Myra/FontStashSharp 版本锁定后更新 §6.1 表格 | ⚠️ | ✅ 已修复 |

**LP-FEASIBILITY: FEASIBLE** — 7 项阻塞已全部解决（+ 3 项补充 ADR 不变）：

| # | 阻塞项 | 严重度 | 状态 |
|---|--------|:------:|:----:|
| LP-1 | IDataPersistence 代码接口远简于架构定义（缺少 LoadConfig\<T\>/泛型 CRUD/完整性校验） | 🔴 | ✅ 已修复 — §2.1 行已对齐代码接口 |
| LP-2 | MonoGame.Extended 6.0.0 在 csproj 但架构未记录 — 决策并文档化或移除 | 🔴 | ✅ 已修复 — §6.1 已添加 MonoGame.Extended 行，§10 #5 已更新 |
| LP-3 | Myra 版本锁定（浮动 `1.*` → `1.5.*`） | 🟠 | ✅ 已修复 — csproj 锁定 1.5.* |
| LP-4 | Myra 素风 UI 原型验证（134 TR/零 ADR/FF6风格未经验证） | 🟠 | 🔲 Sprint 1 启动前验证 |
| LP-5 | CharacterData.cs CORE→FEATURE 反向依赖（`using DndGame.Systems.Combat`） | 🟠 | ✅ 已修复 — Condition 枚举移至 Core/ConditionType.cs |
| LP-6 | Entity 构造函数改为 `internal`（强制 scene.CreateEntity 约定） | 🟡 | ✅ 已修复 — Entity.cs 构造函数已改为 internal |
| LP-7 | 开放问题 #5 修正（MonoGame.Extended.Tiled "未安装"→已包含在 Extended 6.0.0） | 🟡 | ✅ 已修复 — §10 #5 已更新 |

**补充 ADR（各 Epic 前创建，非整体阻塞）**：

| 优先级 | ADR | 覆盖 TR | 启动条件 |
|:------:|-----|:------:|---------|
| HIGH | 冒险实例化 14 步算法架构 | TR-adventure-001~084 | Adventure Epic 前 |
| HIGH | 酒馆系统服务架构 | TR-tavern-001~093 | Tavern Epic 前 |
| MEDIUM | 结算/伤疤/传承管线架构 | TR-settlement-001~124 | Settlement Epic 前 |

---

## Appendix: Development Roadmap

### Phase 1 — MVP ("第一次冒险")

**目标**: 玩家能招募4人队伍 → 完成短冒险 → 体验核心循环

| 优先级 | 模块 | C#实现内容 | 估算 |
|:------:|------|-----------|:----:|
| P0 | 战斗引擎 | CombatFSM, DiceRoller, ActionResolver, EnemyAI, GoRogue FOV/寻路 | 6-8周 |
| P0 | 角色系统 | CharacterData(record), NumericalGenerator, 3职业×3种族, Lv1-5 | 3-4周 |
| P0 | 酒馆UI | Myra XML布局, 招募板(9预设角色), 任务板, 基础装备管理 | 2-3周 |
| P0 | 地图/探索 | AdventureScene, GoRogue地牢生成, 节点导航, 3房间模板 | 3-4周 |
| P0 | LLM Gateway | HttpClient + System.Text.Json基础Gateway, DM Agent, 文案Agent | 2-3周 |
| P0 | 美术资源 | 基础Tileset, 3角色精灵, 3敌人精灵, MGCB内容管线 | 持续 |
| P1 | 短冒险模板 | 5个主题的离线模板JSON | 1-2周 |
| P1 | 结算系统 | SettlementSystem, XP/战利品计算 | 2周 |
| P2 | 音效 | 骰子音效, 基础战斗音效 | 1周 |

**MVP不包含**: 酒馆升级、角色关系系统、中/长冒险、编剧Agent（MVP用模板代替）、BGM

### 测试策略

```
Unit Tests (xUnit + FluentAssertions):
  · DiceRoller: 骰子概率分布、优势/劣势、暴击取最大值
  · CombatEngine: FSM状态转换、攻击检定、伤害计算、条件应用
  · CharacterSystem: 属性计算、升级公式、法术位、关系值
  · AdventureInstantiator: 蓝图解析、实例化算法、遭遇生成
  · SettlementSystem: XP计算、战利品生成、伤疤判定
  · GoRogue集成: FOV计算、寻路验证、地图生成
  全部可离线运行，不依赖LLM

Integration Tests:
  · 角色→战斗: 角色属性正确传入战斗引擎
  · 战斗→结算: 战斗胜负正确触发结算
  · 冒险→地图: 蓝图正确实例化为地图节点
  · LLM→冒险: Schema验证通过后正确解析

手动测试:
  · LLM叙事质量评估（不能自动化）
  · 战斗平衡性测试
  · 像素美术视觉验收
```

---

## Risk & Mitigation

### 技术风险

| 风险 | 概率 | 影响 | 应对 |
|:----:|:----:|:----:|------|
| LLM API成本超预算 | 中 | 高 | Token预算控制、缓存策略、离线模板降级、选择性调用 |
| LLM输出不符合Schema | 中 | 中 | 3次重试机制、增强prompt(附带失败原因)、兜底默认值 |
| MonoGame无可视化编辑器 | 确定 | 中 | 代码创建所有对象(代码即场景)，Tiled编辑地图，monogame-mcp辅助 |
| DND 5e规则复杂度超预期 | 中 | 高 | MVP精简规则(3职业+基本规则)、渐进式完善 |
| GoRogue版本兼容 | 低 | 中 | 已锁定2.6.4(ADR-0007)，封装层隔离变更面 |

### 架构安全护栏

```
① LLM输出隔离:
   LLM响应 → JsonSchema.Net验证 → 业务逻辑验证 → 后处理
   不符合Schema的字段被拒绝/使用默认值

② 离线降级保障:
   内置50+冒险模板、100+叙事模板、物品/角色描述模板
   LLM完全不可用时，游戏核心循环不受影响

③ 编译器即验证器:
   dotnet build → dotnet test → 代码审查 → 合并
   TreatWarningsAsErrors=true，禁止#pragma warning disable

④ 服务降级:
   LLM调用超时 → 不阻塞主线程 → 返回默认叙事文本
   战斗引擎完全离线运行，LLM只负责事后叙事渲染

⑤ 数据安全:
   本地SQLite存档 + JSON备份
   存档文件独立于游戏目录
```

---

> **文档版本**: v3.0（重构版）
> **创建日期**: 2026-05-05 (v1.0) → 2026-05-06 (v2.0 MonoGame迁移版) → 2026-05-11 (v3.0 重构版)
> **前置文档**: docs/architecture/adr-0000~0008, design/gdd/systems-index.md
> **下一阶段**: Technical Director签批 → Lead Programmer可行性评审 → Sprint 1 MVP开发