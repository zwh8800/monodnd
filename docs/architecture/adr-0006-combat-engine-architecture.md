# ADR-0006：战斗引擎架构 — FSM CombatEngine SceneComponent + EventBus + 数据驱动

## Status
Accepted

## Date
2026-05-07

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | MonoGame 3.8.5+ |
| **Domain** | Core（战斗引擎：回合制 FSM、行动经济、攻击/伤害管线、法术/豁免/条件/AI） |
| **Knowledge Risk** | LOW — 核心战斗逻辑为纯 C# 实现，不依赖 MonoGame 渲染 API。DND 5e SRD 规则在 LLM 训练数据中充分覆盖。 |
| **References Consulted** | `design/gdd/GDD-v1.md` §5（战斗系统设计哲学与 DND 5e 偏离规则）、`design/gdd/04-combat-system.md`（完整 FSM/行动/管线/法术/条件/AI）、`design/gdd/01-character-system.md`（HP/AC/属性/熟练/死亡豁免）、`design/gdd/03-items-equipment.md`（武器伤害骰/护甲 AC 公式/附魔效果） |
| **Post-Cutoff APIs Used** | None — 所有逻辑为纯 C#（Random、Dictionary、Action<T> 委托），无 MonoGame 专属 API |
| **Verification Required** | `dotnet build` zero errors/warnings；`dotnet test` 覆盖全部 14 FSM 状态转换、骰子系统、伤害管线、豁免计算、条件应用；战斗可脱离 MonoGame 渲染上下文的纯单元测试 |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0000（MonoGame 引擎）、ADR-0001（ECS — CombatEngine 挂载为 SceneComponent）、ADR-0002（ServiceLocator 访问全局服务）、ADR-0003（IEventBus 发布战斗事件）、ADR-0005（JSON 加载武器/怪物/法术配置） |
| **Enables** | ADR-0004（LLM 通过 CombatEnded/DamageDealt 事件接收战斗日志生成叙事） |
| **Blocks** | Combat Epic 全部 Story、Character Epic（战斗集成部分）、Adventure Epic（遭遇实例化集成） |
| **Ordering Note** | 必须在任何 Combat System Story 开始实现前 Accepted |

## Context

### Problem Statement
战斗系统是《酒馆与命运》最复杂、最核心的子系统。GDD §5 和 `04-combat-system.md` 定义了一套基于 DND 5e SRD 的回合制战斗系统，包含 14 个 FSM 状态、6 种行动资源（Action/Bonus Action/Movement/Reaction/Free Action/Object Interaction）、12 步攻击/伤害计算管线、法术位系统（1-9 环）、14 种条件追踪、5 种敌人 AI 行为模式、4 种 Boss 多阶段机制、地形交互标签系统。当前没有 ADR 定义这些能力的架构组织方式。需要决定战斗引擎的代码结构、与 ECS 的关系、事件发布策略、骰子系统位置、AI 组织方式、条件/伤害类型的数据模型。

### Constraints
- 必须作为 SceneComponent 挂载到 CombatScene（ADR-0001 的架构约束）
- 必须通过 IEventBus 与其他系统通信（ADR-0003 的禁止直接引用约束）
- 所有武器/护甲/怪物/法术数值必须从 JSON 配置加载（ADR-0005 禁止硬编码数值）
- LLM 只能生成战斗叙事文本，不能决策任何机械结果（ADR-0004 的皮肤层约束）
- 核心战斗逻辑（骰子、伤害计算、豁免判定）必须可脱离 MonoGame 渲染上下文进行纯单元测试
- DND 5e 的 6 个关键偏离必须正确实现（每轮重骰先攻、暴击取最大值、3轮无治疗死亡、3级疲劳、短休恢复1环、同时选择→按先攻结算）
- 性能约束：2D 像素游戏，每场战斗 < 20 实体，目标 60fps

### Requirements
- 必须实现 GDD §5.2-5.6 定义的完整 FSM 14 状态转换
- 必须实现 GDD §5.4 定义的 6 项 DND 5e 偏离规则
- 必须支持 6 种行动资源（Action/Bonus Action/Movement/Reaction/Free Action/Object Interaction）
- 必须支持 14 种条件追踪（目盲/魅惑/耳聋/恐慌/擒抱/失能/隐形/麻痹/石化/中毒/倒地/束缚/震慑/昏迷）
- 必须支持 13 种伤害类型（钝击/斩击/穿刺/酸蚀/寒冷/火焰/力场/闪电/黯蚀/毒素/心灵/光耀/雷鸣）
- 必须支持 5 种敌人 AI 行为模式 + Boss 多阶段
- 必须通过 IEventBus 发布 CombatEnded、DamageDealt、CharacterDied 等事件
- 必须可脱离 MonoGame 渲染上下文进行单元测试

## Decision

**采用 FSM-based CombatEngine 作为 SceneComponent，独立 DiceRoller 工具类，内部行为树 AI，JSON 配置 + C# enum 双定义伤害类型和条件数据模型。**

### 架构示意图

```
┌─────────────────────────────────────────────────────────────────┐
│                      CombatScene                                 │
│                                                                 │
│  ┌───────────────────────────────────────────────────────────┐ │
│  │  CombatEngine : SceneComponent                              │ │
│  │                                                             │ │
│  │  ┌─────────────────────────────────────────────────────┐  │ │
│  │  │  CombatStateMachine (FSM)                             │  │ │
│  │  │                                                       │  │ │
│  │  │  INIT → ROLL_INIT → ROUND_START → TURN_START          │  │ │
│  │  │    → ACTION_PHASE → BONUS_ACTION → MOVEMENT           │  │ │
│  │  │    → REACTION_WINDOW → TURN_END → ROUND_END           │  │ │
│  │  │    → VICTORY / DEFEAT                                  │  │ │
│  │  └─────────────────────────────────────────────────────┘  │ │
│  │                                                             │ │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐    │ │
│  │  │DiceRoller    │  │DamageCalc    │  │Condition     │    │ │
│  │  │(static)      │  │Pipeline      │  │Tracker       │    │ │
│  │  │· Roll()      │  │· 武器+属性   │  │· 14 conditions│    │ │
│  │  │· Advantage()  │  │· 暴击取最大  │  │· 堆叠规则    │    │ │
│  │  │· DiceExpr()   │  │· 抗性/易伤   │  │· 持续时间    │    │ │
│  │  └──────────────┘  └──────────────┘  └──────────────┘    │ │
│  │                                                             │ │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐    │ │
│  │  │EnemyAI       │  │SpellSystem   │  │ActionEconomy │    │ │
│  │  │· BT nodes    │  │· 法术位      │  │· Action      │    │ │
│  │  │· 5 behaviors │  │· 专注        │  │· Bonus Action│    │ │
│  │  │· Boss phases │  │· 豁免DC     │  │· Reaction    │    │ │
│  │  └──────────────┘  └──────────────┘  └──────────────┘    │ │
│  │                                                             │ │
│  │  ┌──────────────────────────────────────────────────────┐  │ │
│  │  │  Config Loader (IDataPersistence)                      │  │ │
│  │  │  · weapons.json   · monsters.json   · spells.json     │  │ │
│  │  │  · conditions.json · terrain.json   · formulas.json   │  │ │
│  │  └──────────────────────────────────────────────────────┘  │ │
│  └───────────────────────────────────────────────────────────┘ │
│                                                                 │
│  ┌───────────────────────────────────────────────────────────┐ │
│  │  CombatScene Entities (ECS)                                │ │
│  │  · 玩家角色 Entity (SpriteComponent + CombatStatComponent) │ │
│  │  · 敌人 Entity (SpriteComponent + CombatStatComponent)     │ │
│  │  · 地形 Entity (TerrainComponent + InteractionTag)        │ │
│  └───────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼ (IEventBus)
┌─────────────────────────────────────────────────────────────────┐
│  战斗事件（通过 IEventBus 发布）                                  │
│                                                                 │
│  CombatSystem 事件:                                              │
│    DamageDealt(string TargetId, int Amount, DamageType Type,     │
│                bool IsCritical)                                  │
│    CharacterDied(string CharacterId, string KillerId,            │
│                  DamageType FinalBlow)                           │
│    CombatEnded(string CombatId, CombatResult Result,             │
│                List<string> Survivors)                           │
│    CombatStarted(string CombatId, string BlueprintId)            │
│    ConditionApplied(string TargetId, ConditionType Type,         │
│                     int DurationRounds)                          │
│    SpellCast(string CasterId, string SpellId, int SpellLevel)    │
│    DeathSaveProgressed(string CharacterId, int RoundsWithoutHeal) │
│                                                                 │
│  消费者:                                                        │
│    · LLM Gateway (生成战斗叙事 — ADR-0004)                       │
│    · Character System (更新角色状态 — 01-character-system)       │
│    · Tavern System (追踪战斗统计 — 07-tavern-system)            │
│    · UI System (战斗日志/HUD 更新 — 09-ui-ux-design)             │
│    · Settlement System (计算 XP 和奖励 — 08-failure-growth)     │
└─────────────────────────────────────────────────────────────────┘
```

### 关键接口

```csharp
// ── CombatEngine 主入口（SceneComponent）──
public class CombatEngine : SceneComponent
{
    // 初始化战斗 — 加载遭遇配置、生成敌人实体、开始 FSM
    public void InitializeCombat(EncounterConfig encounter, 
                                  List<CharacterStatBlock> party,
                                  CombatTerrainData terrain);

    // FSM 推进 — 每帧 Update() 调用
    public override void Update(GameTime gameTime);

    // 玩家输入 — 从 CombatScene 的 InputComponent 调用
    public void SubmitAction(string characterId, CombatAction action);

    // 查询当前状态
    public CombatState CurrentState { get; }
    public int CurrentRound { get; }
    public int CurrentTurnIndex { get; }
    public List<CombatantState> GetCombatants();
    public CombatLogEntry[] GetRecentLog(int count);
}

// ── DiceRoller 独立工具类（static，纯函数）──
public static class DiceRoller
{
    // 基础骰子
    public static int Roll(string diceExpr);              // "2d6+3" → 结果
    public static int RollD20();                          // d20 单独方法
    public static int RollWithAdvantage(string diceExpr); // 优势骰
    public static int RollWithDisadvantage(string diceExpr); // 劣势骰

    // 暴击与大失败判定
    public static bool IsCriticalHit(int d20Result);      // 自然20
    public static bool IsCriticalMiss(int d20Result);     // 自然1

    // 暴击伤害（取最大值，本游戏规则）
    public static int RollCritDamage(string diceExpr);

    // 骰子表达式解析
    // "1d8+3" → 1 次 d8 + 3
    // "2d6+1d6_fire+4" → 2d6 基础 + 1d6 火焰 + 4
    public static DiceExpression Parse(string expr);
}

// ── 伤害类型与条件类型（C# enum + JSON 参数）──
public enum DamageType
{
    Bludgeoning, Piercing, Slashing,  // 物理三类型
    Acid, Cold, Fire, Force, Lightning,
    Necrotic, Poison, Psychic, Radiant, Thunder
}

public enum ConditionType
{
    Blinded, Charmed, Deafened, Frightened,
    Grappled, Incapacitated, Invisible, Paralyzed,
    Petrified, Poisoned, Prone, Restrained,
    Stunned, Unconscious
}

// ── 战斗事件 Record（通过 IEventBus 发布）──
public record DamageDealt(
    string TargetId, string SourceId,
    int Amount, DamageType Type, bool IsCritical
);

public record CharacterDied(
    string CharacterId, string KillerId,
    DamageType FinalBlow, string AdventureId
);

public record CombatEnded(
    string CombatId, CombatResult Result,
    List<string> Survivors, List<string> Casualties,
    int TotalRounds, Dictionary<string, CombatStats> PerCharacterStats
);

public enum CombatResult { Victory, Defeat, Retreat }
```

### 14 状态 FSM 定义

```
FSM 状态转换（与 04-combat-system.md §2.1 完全对齐）:

INITIALIZATION
  → ROLL_INITIATIVE (所有参与者加载完成)
    → ROUND_START (先攻排序完成)
      → TURN_START (轮到当前角色，检查失能/昏迷)
        → ACTION_PHASE (行动执行或跳过)
          → BONUS_ACTION_PHASE (附赠动作执行或跳过)
            → MOVEMENT_PHASE (移动完成或跳过)
              → REACTION_WINDOW (反应处理完成)
                → TURN_END (效果处理完成)
                  → TURN_START (还有下一个角色)
                  → ROUND_END (所有角色已行动)
                    → ROLL_INITIATIVE (战斗未结束，重新骰先攻)
                    → VICTORY (所有敌人死亡/逃跑)
                    → DEFEAT (所有玩家角色死亡)

关键偏离（相对于标准 DND 5e）:
  · ROLL_INITIATIVE: 每轮重新骰先攻（非整场固定）
  · ACTION_PHASE: 所有玩家同时选择，按先攻顺序结算
  · 暴击: 伤害骰取最大值（非双骰）
  · 死亡: 3轮无治疗 = 死亡（非3成功/3失败）
  · DEFEAT: 触发失败结算而非战斗结束
```

### AI 行为树结构

```csharp
// AI 行为树节点（CombatEngine 内部类）
public abstract class BTNode
{
    public abstract BTStatus Execute(CombatContext ctx);
}

public enum BTStatus { Success, Failure, Running }

// 5 种行为模式:
//   Skirmisher:   Selector(攻击最近 → 撤离躲藏 → 远程射击)
//   UndeadSoldier: Sequence(评估目标 → 前进攻击 → 不撤退)
//   BasicMelee:   Selector(攻击最近 → 撤退(寡不敌众) → 投降(低HP))
//   Caster:       Selector(施法最高威胁 → 撤退远离前线)
//   Berserker:    Selector(多击最近 → 优先低HP → 半血撤退)
```

### 事件结果分离模型（战斗特化）

```
┌─────────────────────────────────────────────────────────────────┐
│  机械结果（CombatEngine 程序控制）:                                │
│    · 攻击命中/未命中 · 伤害数值 · 暴击判定                          │
│    · 法术 DC 和豁免结果 · 死亡豁免轮数                              │
│    · 条件施加/移除 · AI 目标选择 · Boss 阶段切换                    │
│    · 战利品掉落判定 · XP 计算                                      │
│                                                                 │
│  叙事结果（LLM DM Agent 生成，通过 IEventBus 异步调用）:             │
│    · 攻击描述文本（"哥布林挥动弯刀，骰出12！未能穿透你的锁子甲。"）    │
│    · 击杀台词 · 暴击特殊描述 · Boss 阶段转换演出文本                 │
│    · 环境氛围描写 · 战斗开场/收尾叙事                                │
│                                                                 │
│  执行流程:                                                        │
│    1. CombatEngine 计算机械结果                                    │
│    2. 通过 IEventBus 发布 DamageDealt 等事件                      │
│    3. LLM Gateway 订阅事件，生成叙事文本                            │
│    4. NarrativeReady 事件 → UI 显示叙事 + 伤害数字                   │
└─────────────────────────────────────────────────────────────────┘
```

## Alternatives Considered

### Alternative A：多状态机拆分（每个行动类型独立 FSM）
- **Description**：不采用单一 CombatEngine FSM，而是按行动类型实现独立的状态机（AttackFsm、SpellFsm、MovementFsm），通过组合协调
- **Pros**：更灵活的扩展，每个行动类型可独立演进
- **Cons**：跨状态机协调复杂（行动→附赠→移动→反应需要严格的先后顺序）；调试困难（14 个独立 FSM 之间的状态一致性难以追踪）
- **Rejection Reason**：回合制战斗有严格的顺序约束（行动→附赠→移动→反应），独立 FSM 的灵活性在此场景中不产生价值，反而增加协调复杂度。04-combat-system.md GDD 已明确定义了完整的 14 状态顺序 FSM，单一 FSM 与设计文档完全对齐

### Alternative B：完全数据驱动的规则引擎
- **Description**：所有战斗规则（攻击公式、豁免计算、条件效果）定义为 JSON 规则表，运行时由通用规则解释器执行
- **Pros**：理论上修改 JSON 即可改变战斗规则，不重新编译
- **Cons**：运行时规则解释增加 CPU 开销（每个动作都需 JSON 查找和动态执行）；失去编译期类型安全；调试困难（规则引擎的调用栈不可追踪）；AI-First 范式下，AI 修改 JSON 规则文件可能引入隐蔽的逻辑错误
- **Rejection Reason**：DND 5e 战斗规则高度固化且逻辑复杂（12 步伤害管线、14 种条件交互、专注/豁免/抗性堆叠规则），JSON 规则表在此场景中的表达能力不足。数据驱动适用于数值参数（武器伤害骰、怪物 HP），但规则逻辑应由类型安全的 C# 代码实现

### Alternative C：AI 行为树作为独立系统
- **Description**：敌人 AI 作为独立子系统，通过 IEventBus 接收战斗状态，返回 AI 决策结果
- **Pros**：AI 可独立开发和测试；不同敌人类型可动态注册 AI 策略
- **Cons**：AI 决策与回合流程高度耦合（AI 在 ACTION_PHASE 中需同步决策）；EventBus 发布/订阅的异步性引入额外的时序复杂性
- **Rejection Reason**：AI 决策在回合制战斗中严格同步——必须在 ACTION_PHASE 内立即返回结果。通过 EventBus 包装只是增加了无意义的通信开销。AI 行为树作为 CombatEngine 内部类，测试时可通过注入 CombatContext 进行隔离测试

## Consequences

### Positive
- 与 GDD 设计文档完全对齐——04-combat-system.md 的 FSM 状态、行动经济、伤害管线可直接映射到代码结构
- SceneComponent 架构与 ADR-0001 的自定义 ECS 一致——CombatEngine 通过 `CombatScene.AddSceneComponent()` 注册，生命周期由场景管理
- IEventBus 解耦——CombatEngine 不需要知道 CharacterSystem、TavernSystem、LLMGateway 的存在；新订阅者无需修改 CombatEngine
- DiceRoller 独立纯函数——可脱离 MonoGame 渲染上下文进行 100% 单元测试覆盖（攻击检定、伤害计算、豁免判定均不需要 GraphicsDevice）
- 数据驱动配置——武器参数、怪物 stat block、法术数据存储在 JSON，AI 可安全修改而不破坏 C# 逻辑
- AI 行为树在 CombatEngine 内部——决策延迟为 0（同步调用），不通过 EventBus 延迟
- 编译期类型安全——DamageType 和 ConditionType 为 C# enum，编译器保证 switch 穷尽性

### Negative
- CombatEngine 承担多重职责（FSM、伤害计算、条件追踪、AI、法术系统）——需通过内部子模块分层解决（DiceRoller、ConditionTracker、EnemyAI 为独立内部类）
- 单一 FSM 的状态爆炸风险——14 状态 × N 种行动 = 大量状态转换分支，需要通过守卫条件函数集中管理转换逻辑
- SceneComponent 生命周期限制——CombatEngine 在 CombatScene 销毁时释放，不能跨场景共享（战斗叙述和结算需通过 EventBus 传递）
- 行为树内部类耦合——修改 AI 策略需要重新编译 CombatEngine（但 AI 行为模式数据（目标偏好权重、行为参数）可从 JSON 加载）

### Risks
| Risk | Severity | Mitigation |
|------|----------|------------|
| FSM 状态转换遗漏（AI 生成代码未覆盖边缘情况） | Medium | FSM 状态转换表以数据驱动方式定义（枚举映射），编译器检查穷尽性。xUnit 测试覆盖全部 14 状态的所有可能的守卫条件分支 |
| DiceRoller 随机性导致测试不稳定 | Low | DiceRoller 接受可选的 `Random` 实例注入；测试使用固定种子 `new Random(42)` 确保可重现 |
| 战斗事件类型爆炸（系统增多后事件膨胀） | Low | 事件使用 C# `record` 类型（轻量），按子系统前缀分组（CombatXxx、CharacterXxx）。TypeScript 风格的 discriminated union 通过接口 + pattern matching 实现 |
| Boss 多阶段逻辑硬编码 | Medium | Boss 阶段数据（HP 阈值、技能列表、行为变化）从 JSON 加载；阶段转换逻辑为通用模板，参数化而非 per-Boss 硬编码 |
| AI 行为树在复杂场景中决策质量不足 | Low | MVP 阶段仅 5 种基础行为模式 + 3 个 Boss（Troll/Goblin/Skeleton），复杂度可控。Phase 2+ 可引入优先队列和效用函数 |

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| GDD-v1.md §5.2 | 完整保留 DND 5e 核心规则（属性、检定、AC、豁免、行动经济、法术位、职业特性、种族特性） | 6 维属性、d20+调整值 vs AC/DC、6 种行动资源、1-9 环法术位表均在 CombatEngine 中作为类型安全的 C# 代码实现 |
| GDD-v1.md §5.3 | 适度调整：短休/长休简化、死亡豁免修改、疲劳简化、负重槽位制、专注 UI 醒目提示 | 3 轮无治疗死亡、3 级疲劳在 ConditionTracker 中实现；槽位制负重由 ItemSystem 独立处理 |
| GDD-v1.md §5.4 | Roguelike 特化：每轮重骰先攻、同时选择→按先攻结算、暴击取最大值 | ROLL_INITIATIVE 每轮重骰、ACTION_PHASE 同时选择→先攻顺序结算、DiceRoller.RollCritDamage() 取最大值 |
| GDD-v1.md §5.6 | 场景交互标签（Pushable/Flammable/Climbable 等 8 个） | TerrainComponent 持有 InteractableTag 枚举，CombatEngine 在 MovementPhase 检查标签效果 |
| 04-combat-system.md §2 | 14 状态 FSM 完整定义 | CombatStateMachine 实现全部 14 状态 + 转换守卫条件 |
| 04-combat-system.md §5-6 | 攻击检定管线 + 伤害计算管线 | AttackPipeline 和 DamagePipeline 作为 CombatEngine 内部子模块，实现优势/劣势、掩体、抗性/免疫/易伤、临时HP 规则 |
| 04-combat-system.md §7 | 法术系统（法术位、专注、AOE 网格覆盖、升环、仪式） | SpellSystem 子模块管理法术位消耗/恢复、专注状态机、AOE 网格判定 |
| 04-combat-system.md §9 | 条件追踪（14 种条件 + 堆叠规则） | ConditionTracker 子模块，ConditionType enum + JSON 持续时间/SaveDC 参数 |
| 04-combat-system.md §11 | 敌人 AI 行为树 | EnemyAI 子模块实现 5 种行为模式 + Boss 多阶段 |
| 01-character-system.md §2.1 | 角色战斗中读取 HP/AC/属性/豁免熟练/法术位 | CombatEngine 通过 ICharacterCombatData 接口读取，不直接引用 CharacterSystem |
| 03-items-equipment.md §2 | 武器伤害骰/护甲 AC 公式/附魔效果 | ConfigLoader 从 weapons.json/armor.json 加载，AttackPipeline 消费 |

## Performance Implications
- **CPU**：2D 像素游戏 < 20 战斗实体，每帧 Update() 仅推进 FSM 状态（非每实体遍历）。DiceRoller 开销 < 1μs/次。总体 < 1ms/frame（60fps 预算 16.6ms）
- **Memory**：CombatEngine 实例 ~5KB（FSM 状态 + 参战者引用）。条件追踪 Dictionary < 2KB。战斗事件 record 为短暂对象（GC 回收）
- **Load Time**：JSON 配置加载（weapons.json ~50KB, monsters.json ~100KB, spells.json ~80KB）< 50ms（懒加载，仅首次战斗加载）
- **Network**：N/A（本地回合制战斗，无网络需求）

## Migration Plan
N/A — 战斗系统尚未实现（Phase 0 仅完成了 Core 基础设施）。此 ADR 定义了战斗引擎的完整架构蓝图。

实现步骤：
1. 创建 `src/DndGame/Systems/Combat/CombatEngine.cs`（SceneComponent 主入口）
2. 创建 `src/DndGame/Systems/Combat/DiceRoller.cs`（静态工具类）
3. 创建 `src/DndGame/Systems/Combat/CombatStateMachine.cs`（14 状态 FSM）
4. 创建 `src/DndGame/Systems/Combat/AttackPipeline.cs` + `DamagePipeline.cs`
5. 创建 `src/DndGame/Systems/Combat/SpellSystem.cs` + `ConditionTracker.cs`
6. 创建 `src/DndGame/Systems/Combat/EnemyAI.cs`（行为树节点）
7. 创建 `Data/config/weapons.json`、`monsters.json`、`spells.json`、`conditions.json`
8. 注册 CombatEngine 到 ServiceLocator（第 7 优先级）
9. 编写 xUnit 测试覆盖：FSM 状态转换、DiceRoller、伤害计算、豁免、条件追踪

## Validation Criteria
- `dotnet build` zero errors, zero warnings
- `dotnet test` — 战斗引擎单元测试覆盖：
  - DiceRoller: 基础骰子、优势/劣势、暴击伤害取最大、骰子表达式解析
  - FSM: 全部 14 状态转换 + 守卫条件覆盖率 100%
  - AttackPipeline: 命中判定（普通/优势/劣势/掩体）、自然1/20
  - DamagePipeline: 伤害计算、暴击取最大、抗性/免疫/易伤、临时HP
  - ConditionTracker: 14 种条件施加/移除、持续时间、堆叠规则
  - EnemyAI: 5 种行为模式的目标选择和行动决策
- 6 项 DND 5e 偏离规则均有独立测试用例
- 战斗可脱离 MonoGame 渲染上下文运行（纯逻辑测试，无 GraphicsDevice 依赖）

## Related Decisions
- ADR-0000 — MonoGame 引擎选型（C# .NET 运行时）
- ADR-0001 — ECS 架构（CombatEngine 作为 SceneComponent 挂载）
- ADR-0002 — ServiceLocator（CombatEngine 通过 ServiceLocator.Get 访问 IEventBus/IDataPersistence/ILLMGateway）
- ADR-0003 — IEventBus（CombatEnded/DamageDealt/CharacterDied 事件发布）
- ADR-0004 — LLM 皮肤层（战斗叙事通过 NarrativeReady 事件）
- ADR-0005 — 数据驱动设计（武器/怪物/法术 JSON 配置加载）
- `design/gdd/04-combat-system.md` — 完整战斗系统技术设计（2400+ 行）
- `design/gdd/01-character-system.md` — 角色数据模型（战斗引擎读取接口）
- `design/gdd/03-items-equipment.md` — 武器/护甲数据模型
