# 战斗系统 — 技术设计文档

> **项目**: 酒馆与命运 (Tavern & Destiny)
> **规则基线**: DND 5e SRD（经Roguelike调整）
> **语言政策**: 游戏文本统一采用简体中文，技术标识符使用英文snake_case
> **文档版本**: v1.0
> **对应GDD版本**: GDD-v1.0
> **依赖文档**: character-system.md, items-equipment.md

---

## 1. 概述

### 1.1 系统目的

战斗系统是《酒馆与命运》的**核心系统**，是玩家体验"策略构筑"和"永久风险"两大情感支柱的主要载体。本系统负责：

- **回合制战斗的完整生命周期管理**（初始化→先攻→行动→结算→胜利/失败）
- **所有攻击、伤害、法术、豁免的数值计算管线**
- **14种DND 5e条件的追踪与效果应用**
- **敌人AI行为决策**
- **地形交互与环境效果**
- **短休/长休资源恢复**
- **战斗结算（胜利奖励/失败惩罚/角色死亡）**

### 1.2 设计哲学

> **保留DND 5e的灵魂，调整Roguelike的节奏。**

核心原则：
1. **数值确定性**：所有战斗数值由程序计算，LLM仅负责叙事文本（战斗描述、击杀台词等）
2. **规则透明**：玩家可以查看每个骰子结果的完整计算过程
3. **节奏优化**：通过流畅的UI和快速的AI决策减少等待感
4. **死亡有重量**：角色永久死亡是游戏核心体验的一部分

### 1.3 关键修改（相对于标准DND 5e）

| 系统 | DND 5e原版 | 本游戏调整 | 原因 | GDD引用 |
|------|-----------|-----------|------|---------|
| 先攻 | 整场战斗固定顺序 | **整场战斗固定顺序**（标准5e） | 保持策略深度 | §5.4 |
| 暴击 | 自然20=伤害骰翻倍 | **自然20=伤害骰取最大值** | 更爽更快结算 | §5.4 |
| 死亡 | 3次失败=死亡 | **双轨制：3轮无治疗=死亡（主要）+ 死亡失败累积3次=死亡（次要）** | 有层次的风险递进，兼顾紧迫感与救援窗口 | §8.3, §14.3 |
| 疲劳 | 6级渐进 | **3级（正常/疲乏/力竭）** | 减少管理负担 | §5.3 |
| 法术位恢复 | 短休部分恢复 | **短休恢复所有1环位** | Roguelike资源压力 | §5.4 |
| 移动 | 整段移动 | **分段移动**（移动→行动→移动） | 增加战术灵活性 | §5.4 |
| 反应 | 回合结束时检查 | **中断驱动**（随时触发） | 更真实的战斗节奏 | §5.4 |

### 1.4 与其他系统的关系

```
┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│   角色系统    │────▶│  战斗系统     │◀────│  物品装备系统  │
│  (HP/AC/属性) │     │  (核心系统)   │     │  (武器/护甲)  │
└──────────────┘     └──────┬───────┘     └──────────────┘
                            │
              ┌─────────────┼─────────────┐
              ▼             ▼             ▼
        ┌──────────┐ ┌──────────┐ ┌──────────┐
        │ 冒险系统  │ │ LLM网关   │ │ 酒馆系统  │
        │ (遭遇CR) │ │ (战斗叙述)│ │ (短休长休)│
        └──────────┘ └──────────┘ └──────────┘
```

- **角色系统** → 战斗系统：提供HP/AC/属性调整值/法术位/豁免熟练/技能加值
- **物品装备系统** → 战斗系统：提供武器伤害骰/护甲AC/附魔效果
- **冒险系统** → 战斗系统：提供遭遇CR/敌人配置/地形数据
- **LLM网关** ← 战斗系统：接收战斗日志，生成叙事文本
- **酒馆系统** ↔ 战斗系统：短休/长休触发资源恢复

### 1.5 SRD标记说明

本文档中每条规则使用以下标记：

| 标记 | 含义 |
|------|------|
| `[SRD-FULL]` | 完全遵循DND 5e SRD规则，未做修改 |
| `[SRD-MODIFIED]` | 基于SRD规则，但参数/机制有调整（注明修改内容） |
| `[CUSTOM]` | 完全原创规则，不在SRD范围内 |

---

## 2. 回合流程状态机

### 2.1 状态机总览 `[CUSTOM]`

战斗采用有限状态机（FSM）管理整个回合流程。每个状态有明确的进入条件、执行逻辑和退出条件。

**核心设计**：
- **顺序回合制**：每个角色按先攻顺序依次行动（标准DND 5e）
- **分段移动**：移动点数可在回合内分段使用（移动→行动→移动）
- **中断式反应**：反应不占用回合阶段，在满足触发条件时随时中断当前回合执行（见反应中断表）
- **固定先攻**：战斗开始时骰一次先攻，整场战斗固定顺序，不重复骰

```
┌─────────────────────────────────────────────────────────────────┐
│                    战斗状态机 (Combat FSM)                        │
│                                                                   │
│  INITIALIZATION                                                   │
│       │                                                           │
│       ▼                                                           │
│  ROLL_INITIATIVE (战斗开始时仅一次)                               │
│       │                                                           │
│       ▼                                                           │
│  ┌ ROUND_START ◄──────────────────────────────────────┐          │
│  │     │                                              │          │
│  │     ▼                                              │          │
│  │  ┌─ TURN_START ◄──────────────────────────┐       │          │
│  │  │     │                                   │       │          │
│  │  │     ▼                                   │       │          │
│  │  │  MOVEMENT_PHASE_1 (可选)                 │       │          │
│  │  │     │                                   │       │          │
│  │  │     ▼                                   │       │          │
│  │  │  ACTION_PHASE                            │       │          │
│  │  │     │                                   │       │          │
│  │  │     ▼                                   │       │          │
│  │  │  BONUS_ACTION_PHASE (可选)               │       │          │
│  │  │     │                                   │       │          │
│  │  │     ▼                                   │       │          │
│  │  │  MOVEMENT_PHASE_2 (可选)                 │       │          │
│  │  │     │                                   │       │          │
│  │  │     ▼                                   │       │          │
│  │  │  TURN_END ──→ 还有下一个角色? ──YES─────┘       │          │
│  │  │     │                                   NO      │          │
│  │  │     ▼                                   │       │          │
│  │  │  ROUND_END ──→ 战斗结束? ──YES──→ VICTORY/DEFEAT   │      │
│  │  │     │           │     NO                    │          │
│  │  │     │           └──→ 撤退投票通过? ──YES──→ RETREAT_CHECK   │
│  │  │     │                    NO                │          │
│  │  │     └──────────────→ ROUND_START ──────────┘          │
│  │  └───────────────────────────────────────────────────────┘
│  │                                                           │
│  └── REACTION 中断 (随时)：在任何角色的回合内，满足触发条件   │
│       时暂停当前回合 → 执行反应 → 恢复原回合              │
│                                                                   │
│  RETREAT_CHECK                                                    │
│       │                                                           │
│       ├── 成功 ──→ FLEE ──→ 返回冒险地图                          │
│       │                                                           │
│       └── 失败 ──→ 借机攻击 ──→ ROUND_START                      │
└─────────────────────────────────────────────────────────────────┘
```

### 2.2 状态定义与转换表

| 状态 | 进入条件 | 执行逻辑 | 退出条件 | 下一状态 |
|------|----------|----------|----------|----------|
| `INITIALIZATION` | 战斗触发 | 加载地图/敌人/地形；初始化战斗日志；设置参与者列表 | 所有参与者加载完成 | `ROLL_INITIATIVE` |
| `ROLL_INITIATIVE` | 战斗开始（仅一次） | 每个参与者骰d20+DEX调整值；排序；处理突袭 | 所有参与者骰完 | `ROUND_START` |
| `ROUND_START` | 新轮开始 | 触发回合开始效果（如再生、持续伤害）；更新条件持续时间 | 效果处理完成 | `TURN_START` |
| `TURN_START` | 轮到当前角色 | 显示当前角色高亮；检查失能/昏迷状态；重置反应次数；重置移动点数 | 角色可行动或自动跳过 | `MOVEMENT_PHASE_1` |
| `MOVEMENT_PHASE_1` | 当前角色回合开始 | 显示移动范围；等待移动指令（可选，可跳过直接进入行动） | 移动完成或跳过 | `ACTION_PHASE` |
| `ACTION_PHASE` | 移动前半段完成 | 显示行动菜单；等待玩家选择（或AI决策）；执行行动 | 行动执行完成或跳过 | `BONUS_ACTION_PHASE` |
| `BONUS_ACTION_PHASE` | Action完成 | 检查是否有可用附赠动作；显示附赠动作菜单 | 附赠动作执行或跳过 | `MOVEMENT_PHASE_2` |
| `MOVEMENT_PHASE_2` | Bonus Action完成 | 使用剩余移动点数；显示可移动范围 | 移动完成或跳过 | `TURN_END` |
| `TURN_END` | 当前角色回合结束 | 触发回合结束效果；重置回合标记 | 效果处理完成 | 下一角色`TURN_START`或`ROUND_END` |
| `ROUND_END` | 所有角色回合结束 | 检查胜利/失败条件；执行撤退投票检查 | 条件检查完成 | `ROUND_START`或终态或`RETREAT_CHECK` |
| `RETREAT_CHECK` | 撤退投票通过 | 计算撤退DC；执行撤退检定（d20 + DEX_mod + 环境修正） | 检定完成 | `FLEE`（成功）或`ROUND_START`（失败，承受借机攻击后） |
| `FLEE` | 撤退检定成功 | 应用撤退惩罚；触发LLM撤退叙述；标记节点"已撤退" | 惩罚处理完成 | 返回冒险地图 |
| `VICTORY` | 所有敌人死亡/逃跑 | 触发战利品生成；触发LLM叙述；结算XP | 结算完成 | 返回冒险场景 |
| `DEFEAT` | 所有玩家角色死亡 | 触发失败惩罚；触发LLM叙述；检查角色死亡 | 惩罚处理完成 | 返回酒馆 |

**反应（REACTION）中断机制**：

反应不是独立的回合阶段，而是**中断驱动**的系统。在任何角色的回合内，当满足反应触发条件时，当前回合暂停，执行反应，然后恢复原回合。

| 反应类型 | 触发条件 | 执行时机 | 恢复点 |
|----------|----------|----------|--------|
| **借机攻击** | 敌人离开触及范围 | 立即中断 | 敌人移动继续 |
| **Shield** | 被攻击命中时 | 攻击判定后、伤害前 | 伤害计算继续 |
| **Counterspell** | 敌人施法时 | 施法开始后、效果前 | 法术解析继续 |
| **Protection** | 邻接队友被攻击时 | 攻击判定前 | 攻击判定继续 |
| **Uncanny Dodge** | 被可见攻击者命中时 | 伤害计算后 | 伤害应用继续 |

### 2.3 顺序回合制 `[SRD-FULL]`

**核心规则**：每个角色按先攻顺序依次行动，标准DND 5e回合制。

```
顺序回合流程:

  Step 1: ROLL_INITIATIVE (战斗开始时，仅一次)
    - 所有参与者骰 d20 + DEX调整值 + 其他加值
    - 按结果从高到低排序
    - 处理平局（见§3.3）
    - 结果固定，整场战斗不变

  Step 2: ROUND_START (每轮开始)
    - 触发回合开始效果（再生、持续伤害等）
    - 更新条件持续时间

  Step 3: TURN_START (每个角色回合开始)
    - 高亮当前角色
    - 检查失能/昏迷状态
    - 重置反应次数（恢复1次）
    - 重置移动点数（恢复至速度值）

  Step 4: MOVEMENT_PHASE_1 (移动前半段，可选)
    - 使用部分移动点数
    - 可跳过，直接进入行动

  Step 5: ACTION_PHASE (主动作)
    - 显示行动菜单
    - 等待玩家选择（或AI决策）
    - 执行行动

  Step 6: BONUS_ACTION_PHASE (附赠动作，可选)
    - 检查是否有可用附赠动作
    - 执行或跳过

  Step 7: MOVEMENT_PHASE_2 (移动后半段，可选)
    - 使用剩余移动点数
    - 支持分段移动：移动→行动→移动

  Step 8: TURN_END (回合结束)
    - 触发回合结束效果
    - 检查下一个角色

  Step 9: ROUND_END (轮结束)
    - 所有角色回合结束后触发
    - 检查胜利/失败条件
    - 进入下一轮（先攻顺序不变）

  分段移动示例:
    战士速度30尺:
      MOVEMENT_PHASE_1: 移动10尺靠近敌人
      ACTION_PHASE: 攻击敌人
      MOVEMENT_PHASE_2: 移动20尺撤退
      总移动: 30尺（等于速度值）

  设计理由:
    - 标准5e规则，玩家熟悉
    - 分段移动增加战术深度
    - 顺序行动保证策略可预测性
```

### 2.4 状态转换守卫条件

```csharp
/// <summary>
/// 战斗状态机转换守卫条件
/// </summary>
public static class CombatTransitionGuards
{
    /// <summary>
    /// 检查是否允许从一个状态转换到另一个状态
    /// </summary>
    public static bool CanTransition(CombatState from, CombatState to, CombatContext context)
    {
        return (from, to) switch
        {
            (CombatState.Initialization, CombatState.RollInitiative)
                => context.AllParticipantsLoaded(),

            (CombatState.RollInitiative, CombatState.RoundStart)
                => context.InitiativeRollsComplete(),

            (CombatState.RoundStart, CombatState.TurnStart)
                => context.RoundStartEffectsResolved(),

            (CombatState.TurnStart, CombatState.MovementPhase1)
                => context.CurrentCombatant.CanAct(),  // 非失能/昏迷

            (CombatState.MovementPhase1, CombatState.ActionPhase)
                => context.MovementPhase1CompleteOrSkipped(),

            (CombatState.ActionPhase, CombatState.BonusActionPhase)
                => context.ActionUsedOrSkipped(),

            (CombatState.BonusActionPhase, CombatState.MovementPhase2)
                => context.BonusActionUsedOrSkipped(),

            (CombatState.MovementPhase2, CombatState.TurnEnd)
                => context.MovementPhase2CompleteOrSkipped(),

            (CombatState.TurnEnd, CombatState.TurnStart)
                => context.HasNextCombatant(),

            (CombatState.TurnEnd, CombatState.RoundEnd)
                => context.AllCombatantsActed(),

            (CombatState.RoundEnd, CombatState.RoundStart)
                => !context.IsCombatOver(),

            (CombatState.RoundEnd, CombatState.Victory)
                => context.AllEnemiesDeadOrFled(),

            (CombatState.RoundEnd, CombatState.Defeat)
                => context.AllPlayersDead(),

            // 撤退相关转换
            (CombatState.RoundEnd, CombatState.RetreatCheck)
                => context.RetreatVotePassed(),

            (CombatState.RetreatCheck, CombatState.Flee)
                => context.RetreatCheckSucceeded(),

            (CombatState.RetreatCheck, CombatState.RoundStart)
                => !context.RetreatCheckSucceeded(),  // 撤退失败→借机攻击→进入下一轮

            (CombatState.Flee, CombatState.Victory)
                => false,  // Flee是终态，已处理完毕

            _ => false
        };
    }
}

/// <summary>
/// 战斗状态枚举
/// </summary>
public enum CombatState
{
    Initialization,
    RollInitiative,
    RoundStart,
    TurnStart,
    MovementPhase1,
    ActionPhase,
    BonusActionPhase,
    MovementPhase2,
    TurnEnd,
    RoundEnd,
    Victory,
    Defeat,
    RetreatCheck,
    Flee
}

/// <summary>
/// 战斗上下文，提供守卫条件查询
/// </summary>
public class CombatContext
{
    public Combatant CurrentCombatant { get; set; }
    public List<Combatant> AllCombatants { get; set; }
    public int CurrentCombatantIndex { get; set; }

    public bool AllParticipantsLoaded()
        => AllCombatants.All(c => c.IsLoaded);

    public bool InitiativeRollsComplete()
        => AllCombatants.All(c => c.InitiativeRolled);

    public bool RoundStartEffectsResolved()
        => true; // 由效果系统确认

    public bool MovementPhase1CompleteOrSkipped()
        => true; // 玩家可随时跳过移动

    public bool ActionUsedOrSkipped()
        => CurrentCombatant.ActionUsed || CurrentCombatant.ActionSkipped;

    public bool BonusActionUsedOrSkipped()
        => CurrentCombatant.BonusActionUsed || CurrentCombatant.BonusActionSkipped;

    public bool MovementPhase2CompleteOrSkipped()
        => true; // 玩家可随时跳过移动

    public bool HasNextCombatant()
        => CurrentCombatantIndex < AllCombatants.Count - 1;

    public bool AllCombatatantsActed()
        => CurrentCombatantIndex >= AllCombatants.Count - 1;

    public bool IsCombatOver()
        => AllEnemiesDeadOrFled() || AllPlayersDead();

    public bool AllEnemiesDeadOrFled()
        => AllCombatants
            .Where(c => c.Team == Team.Enemy)
            .All(c => c.IsDead || c.HasFled);

    public bool AllPlayersDead()
        => AllCombatants
            .Where(c => c.Team == Team.Player)
            .All(c => c.IsDead);

    /// <summary>
    /// 撤退投票是否通过（全员同意或多数+领导检定成功）
    /// </summary>
    public bool RetreatVotePassed()
        => RetreatVoteResult == RetreatVote.Agreed;

    /// <summary>
    /// 撤退检定是否成功（d20 + DEX_mod + 环境修正 vs DC）
    /// </summary>
    public bool RetreatCheckSucceeded()
        => RetreatCheckResult.IsSuccess;

    public RetreatVote RetreatVoteResult { get; set; }
    public RetreatCheckOutcome RetreatCheckResult { get; set; }
}
```

---

## 3. 先攻系统 `[SRD-FULL]`

### 3.1 先攻检定公式

```
先攻检定 = d20 + DEX调整值 + 其他加值

其中:
  - d20: 在战斗开始时骰一次，整场战斗固定
  - DEX调整值: floor((DEX - 10) / 2)
  - 其他加值:
    - Alert专长: +5
    - 某些法术/效果: 如Guidance (+1d4)
    - 某些装备附魔: 如"疾风之" (+3)
```

### 3.2 固定先攻规则 `[SRD-FULL]`

**规则**：采用标准DND 5e固定先攻——战斗开始时所有参与者骰一次先攻，整场战斗保持此顺序不变。

```
固定先攻流程:

  1. INITIALIZATION 完成后进入 ROLL_INITIATIVE
  2. 所有参与者骰 d20 + DEX_mod + bonuses
  3. 按结果从高到低排序
  4. 平局处理（见§3.3）
  5. 确定先攻顺序，整场战斗固定不变
  6. 进入 ROUND_START

  设计理由:
    - 策略可预测性：玩家知道行动顺序后可据此制定战术
    - 减少等待时间：无需每轮重新排序
    - 符合标准5e规则，玩家无需额外学习
    - 便于DEX优化构筑发挥价值
```

### 3.3 平局处理规则 `[CUSTOM]`

```
当两个或多个角色先攻检定结果相同时:

  1. 比较 DEX 调整值（高者优先）
  2. 若仍平局，比较 DEX 属性值（高者优先）
  3. 若仍平局，比较角色等级（高者优先）
  4. 若仍平局，随机决定（d2，1=先手）

  示例:
    战士先攻=14 (d20=12, DEX_mod=+2)
    盗贼先攻=14 (d20=10, DEX_mod=+4)
    → 盗贼先手（DEX_mod +4 > +2）
```

### 3.4 突袭轮 `[SRD-FULL]`

```
突袭条件:
  - 一方完全未被察觉（成功隐匿 vs 对方被动察觉）
  - 突袭方在第一轮获得"突袭"状态

突袭效果:
  - 被突袭方在第一轮: 失能（不能行动/反应）
  - 突袭方: 正常行动
  - 第二轮开始: 正常流程

突袭检定:
  - 隐匿方: d20 + Stealth vs 被动察觉 (10 + WIS_mod + proficiency)
  - 多人: 每人独立检定，任一人失败则该人被发现
```

### 3.5 先攻UI显示

```
先攻顺序条 (Initiative Bar):

┌────────────────────────────────────────────────────────────┐
│  Round 3                                                    │
│  ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐  │
│  │ 盗贼 │ │ 法师 │ │哥布林│ │ 战士 │ │哥布林│ │哥布林│  │
│  │  18  │ │  15  │ │  13  │ │  11  │ │  9  │ │  7  │  │
│  │  ▲   │ │      │ │      │ │      │ │      │ │      │  │
│  └──────┘ └──────┘ └──────┘ └──────┘ └──────┘ └──────┘  │
│  ▲ = 当前行动角色                                          │
│  灰色 = 已完成回合                                         │
│  红色边框 = 敌方                                           │
│  蓝色边框 = 友方                                           │
└────────────────────────────────────────────────────────────┘
```

---

## 4. 行动经济

### 4.1 行动类型总览 `[SRD-FULL]`

每个角色在自己的回合内可以使用以下行动资源：

| 行动类型 | 数量/回合 | 说明 |
|----------|:---------:|------|
| **Action（主动作）** | 1 | 主要攻击/施法/使用物品/冲刺/撤离/躲避 |
| **Bonus Action（附赠动作）** | 0-1 | 特定能力/法术允许的附赠动作 |
| **Movement（移动）** | 等于速度值 | 可在行动前后分段使用 |
| **Reaction（反应）** | 0-1 | 在他人回合触发（借机攻击/防护/法术） |
| **Free Action（自由动作）** | 不限 | 说话/丢弃物品/简单互动 |
| **Object Interaction（物品互动）** | 1 | 开门/拔武器/拉开关 |

### 4.2 主动作（Action）可选列表

| 动作 | 效果 | SRD标记 |
|------|------|:-------:|
| **Attack（攻击）** | 进行一次武器攻击（Lv5+战士可多次） | FULL |
| **Cast a Spell（施法）** | 施放施法时间为1动作的法术 | FULL |
| **Dash（冲刺）** | 本回合移动距离翻倍 | FULL |
| **Disengage（撤离）** | 本回合移动不触发借机攻击 | FULL |
| **Dodge（躲避）** | 对你的攻击有劣势，DEX豁免有优势 | FULL |
| **Help（援助）** | 给予队友下次检定优势，或分散敌人注意力 | FULL |
| **Hide（躲藏）** | 隐匿检定，成功后获得隐形状态 | FULL |
| **Ready（准备）** | 设定触发条件，满足时用反应执行 | FULL |
| **Search（搜索）** | 察觉或调查检定 | FULL |
| **Use an Object（使用物品）** | 使用物品（药水/卷轴/工具） | FULL |
| **Use a Class Feature（使用职业特性）** | Second Wind / Action Surge等 | FULL |

### 4.3 附赠动作（Bonus Action）来源

| 来源 | 附赠动作 | 条件 |
|------|----------|------|
| **Two-Weapon Fighting** | 副手武器攻击 | 主手和副手都是light武器 |
| **Cunning Action (Rogue Lv2)** | Dash/Disengage/Hide | 盗贼职业特性 |
| **Second Wind (Fighter)** | 恢复1d10+Fighter Lv HP | 每短休1次 |
| **某些法术** | 如 Healing Word, Misty Step | 法术描述指定 |
| **某些职业特性** | 如 Bardic Inspiration | 法术/特性描述指定 |

**关键规则**：如果没有明确来源，角色**没有**附赠动作可用。

### 4.4 反应（Reaction）来源

| 来源 | 反应 | 触发条件 |
|------|------|----------|
| **Opportunity Attack（借机攻击）** | 一次近战攻击 | 敌人离开你的触及范围 |
| **Protection Fighting Style** | 赋予攻击者劣势 | 5尺内队友被攻击 |
| **Uncanny Dodge (Rogue Lv5)** | 受到伤害减半 | 被可见攻击者命中 |
| **Shield (法术)** | AC+5直到下回合开始 | 被攻击命中时 |
| **Counterspell (法术)** | 反制法术 | 60尺内敌人施法 |
| **War Caster** | 施放单目标法术 | 借机攻击触发时 |

**反应恢复**：每个角色在自己回合开始时恢复1个反应使用次数。

### 4.5 物品互动（Object Interaction）示例

| 互动 | 示例 |
|------|------|
| 拔出武器 | 从鞘中拔出长剑 |
| 收起武器 | 将短剑收回鞘中 |
| 开门 | 打开一扇未锁的门 |
| 拉开关 | 拉下墙上的拉杆 |
| 取出物品 | 从背包取出药水 |
| 丢弃物品 | 将火把丢在地上 |

**规则**：每回合1次免费物品互动，第2次起消耗主动作。

---

## 5. 攻击检定管线

### 5.1 攻击检定公式 `[SRD-FULL]`

```
攻击检定 = d20 + 属性调整值 + 熟练加值 + 其他加值

其中:
  属性调整值:
    - 近战武器: STR调整值（finesse武器可选DEX）
    - 远程武器: DEX调整值
    - 法术攻击: 施法属性调整值（INT/WIS/CHA）

  熟练加值:
    - 角色熟练的武器: +proficiency_bonus
    - 角色不熟练的武器: +0

  其他加值:
    - 魔法武器加值: +1/+2/+3
    - 战斗风格(Archery): +2（仅远程）
    - 法术加值: 如Bless (+1d4)
    - 优势/劣势: 骰2次取高/低（见5.3）
    - 掩体: 半掩体+2 AC, 3/4掩体+5 AC（见5.5）
```

### 5.2 命中判定

```
命中条件:
  攻击检定结果 ≥ 目标AC

  特殊情况:
    - 自然20: 必定命中（无论AC多少）→ 暴击
    - 自然1: 必定未命中（无论加值多少）
```

### 5.3 优势与劣势 `[SRD-FULL]`

```
优势 (Advantage):
  - 骰2次d20，取较高值
  - 来源: 隐匿攻击、目标倒地(5尺内)、目标束缚、Help动作等

劣势 (Disadvantage):
  - 骰2次d20，取较低值
  - 来源: 目标隐形、远程攻击5尺内有敌人、中毒、恐慌等

互斥规则 [SRD-FULL]:
  - 任意数量优势 + 任意数量劣势 = 平骰（1次d20）
  - 优势和劣势不累加，只看是否存在
  - 3源优势 + 1源劣势 = 平骰
  - 1源优势 + 0源劣势 = 优势

示例:
  盗贼从隐匿状态攻击倒地的敌人:
    - 隐匿 → 优势
    - 目标倒地(5尺内) → 优势
    - 总计: 优势（2源优势，骰2次取高）

  中毒的战士攻击隐形的敌人:
    - 中毒 → 攻击劣势
    - 目标隐形 → 对其攻击劣势
    - 总计: 劣势（2源劣势，骰2次取低）
```

### 5.4 暴击与大失败 `[SRD-MODIFIED]`

```
暴击 (Critical Hit):
  触发: 自然20（d20原始结果=20）
  效果:
    1. 必定命中
    2. 伤害骰取最大值（非标准5e的双骰）[SRD-MODIFIED]
    3. 所有伤害骰（基础+附魔+偷袭）都最大化

  示例:
    战士Lv3, STR 16, 长剑(1d8), 炽焰附魔(+1d6 fire)
    暴击伤害 = 8 (max 1d8) + 3 (STR) + 6 (max 1d6 fire) = 17

  Champion子职业:
    Lv3: 暴击范围扩展为19-20
    Lv15: 暴击范围扩展为18-20

大失败 (Critical Miss):
  触发: 自然1（d20原始结果=1）
  效果:
    1. 必定未命中
    2. （可选）触发fumble表 [CUSTOM]
```

### 5.5 掩体规则 `[SRD-FULL]`

| 掩体类型 | AC加成 | 条件 |
|----------|:------:|------|
| **半掩体 (Half Cover)** | +2 | 目标后方有低矮障碍物/其他生物 |
| **3/4掩体 (Three-Quarters Cover)** | +5 | 目标后方有大量遮挡 |
| **全掩体 (Total Cover)** | 无法瞄准 | 目标完全被遮挡 |

```
掩体判定:
  - 从攻击者到目标画一条线
  - 线上经过的障碍物决定掩体类型
  - 其他生物提供半掩体
  - 墙壁/柱子根据遮挡比例判定

  Sharpshooter专长: 无视半掩体和3/4掩体
  Spell Sniper专长: 无视半掩体和3/4掩体
```

### 5.6 远程攻击修正 `[SRD-FULL]`

```
射程规则:
  - 普通射程 (range_normal): 正常攻击
  - 长射程 (range_long): 攻击有劣势
  - 超出长射程: 无法攻击

  5尺内近战:
    - 使用远程武器时，5尺内有敌人 → 攻击有劣势
    - Crossbow Expert专长: 无视此规则

  示例:
    短弓: 射程 80/320
    - 80尺内: 正常
    - 81-320尺: 劣势
    - 320尺+: 无法攻击
```

### 5.7 攻击检定完整示例

```
示例1: 基础近战攻击
  角色: Lv3 Fighter, STR 16(+3), 熟练长剑, PB=2
  武器: 长剑 (1d8 slashing)
  目标: AC 14 的哥布林

  攻击检定 = d20 + 3(STR) + 2(PB) = d20 + 5
  需要: d20 + 5 ≥ 14 → d20 ≥ 9
  命中概率: 60% (12/20)

示例2: 优势远程攻击
  角色: Lv5 Rogue, DEX 18(+4), 熟练短弓, PB=3
  武器: 短弓 (1d6 piercing), 射程 80/320
  目标: AC 13 的骷髅, 距离30尺, 盗贼从隐匿状态攻击

  攻击检定 = d20 + 4(DEX) + 3(PB) = d20 + 7 (优势骰2次取高)
  需要: d20 + 7 ≥ 13 → d20 ≥ 6
  命中概率(优势): 1 - (5/20)² = 93.75%

示例3: 掩体攻击
  角色: Lv3 Wizard, INT 16(+3), PB=2
  法术: Fire Bolt (法术攻击)
  目标: AC 15 的敌人, 有3/4掩体

  法术攻击 = d20 + 3(INT) + 2(PB) = d20 + 5
  目标有效AC = 15 + 5(3/4掩体) = 20
  需要: d20 + 5 ≥ 20 → d20 ≥ 15
  命中概率: 30% (6/20)
```

---

## 6. 伤害计算管线

### 6.1 伤害公式 `[SRD-FULL]`

```
基础伤害 = 武器伤害骰 + 属性调整值 + 其他加值

其中:
  武器伤害骰:
    - 近战: 武器基础骰 (如长剑 1d8)
    - 远程: 武器基础骰 (如短弓 1d6)
    - Versatile双手: 使用更高骰 (如长剑双手 1d10)

  属性调整值:
    - 近战: STR（finesse可选DEX）
    - 远程: DEX
    - 法术: 施法属性（通常不加到伤害，除非有特殊特性）

  其他加值:
    - 魔法武器: +1/+2/+3
    - 战斗风格(Dueling): +2（单手无副手武器时）
    - 附魔伤害骰: 如炽焰 +1d6 fire
    - 偷袭: +Xd6（见职业特性）
```

### 6.2 暴击伤害 `[SRD-MODIFIED]`

```
暴击伤害计算:
  本游戏规则: 所有伤害骰取最大值（非标准5e的双骰）

  暴击伤害 = max(武器骰) + max(附魔骰) + max(偷袭骰) + 属性调整值 + 其他固定加值

  示例:
    Lv5 Rogue, DEX 18(+4), 细剑(1d8), 偷袭3d6
    暴击伤害 = 8(max 1d8) + 4(DEX) + 18(max 3d6) = 30 piercing

    Lv3 Fighter, STR 16(+3), 炽焰长剑(1d8 + 1d6 fire)
    暴击伤害 = 8(max 1d8) + 3(STR) + 6(max 1d6 fire) = 17 (8 slashing + 6 fire + 3)
```

### 6.3 13种伤害类型 `[SRD-FULL]`

| 类型 | 英文 | 来源示例 |
|------|------|----------|
| 钝击 | Bludgeoning | 钉头锤、巨锤、徒手 |
| 斩击 | Slashing | 长剑、巨剑、手斧 |
| 穿刺 | Piercing | 匕首、细剑、短弓 |
| 酸蚀 | Acid | 强酸瓶、Melf's Acid Arrow |
| 寒冷 | Cold | 霜痕附魔、Cone of Cold |
| 火焰 | Fire | 炽焰附魔、Fireball |
| 力场 | Force | Magic Missile、Eldritch Blast |
| 闪电 | Lightning | 震击附魔、Lightning Bolt |
| 黯蚀 | Necrotic | 黯蚀附魔、Vampiric Touch |
| 毒素 | Poison | 毒牙附魔、Poison Spray |
| 心灵 | Psychic | Mind Spike、Vicious Mockery |
| 光耀 | Radiant | 光耀附魔、Sacred Flame |
| 雷鸣 | Thunder | Thunderwave、Shatter |

### 6.4 抗性/免疫/易伤 `[SRD-FULL]`

```
伤害修正规则:

  抗性 (Resistance):
    - 该类型伤害减半（向下取整）
    - 示例: 受到15点火焰伤害，有火焰抗性 → 受7点

  免疫 (Immunity):
    - 该类型伤害降为0
    - 示例: 受到20点毒素伤害，有毒素免疫 → 受0点

  易伤 (Vulnerability):
    - 该类型伤害翻倍
    - 示例: 受到12点寒冷伤害，有寒冷易伤 → 受24点

  堆叠规则:
    - 多个同类型抗性不叠加（只减半一次）
    - 同时有抗性和易伤 → 互相抵消（正常伤害）
    - 不同类型独立计算

  计算顺序:
    1. 基础伤害 + 加值 = 总伤害
    2. 应用抗性/免疫/易伤
    3. 减去临时HP
    4. 减去实际HP
```

### 6.5 临时HP规则 `[SRD-FULL]`

```
临时HP (Temporary Hit Points):

  获取来源:
    - 法术: 如 False Life (1d4+4)
    - 药水: 某些药水提供临时HP
    - 职业特性: 如某些子职业特性

  规则:
    1. 临时HP不叠加: 新的临时HP替换旧的（取较高值）
    2. 临时HP先于真实HP被扣除
    3. 临时HP不能被治疗恢复
    4. 临时HP在长休时消失

  示例:
    角色HP: 20/25, 临时HP: 5
    受到12点伤害:
      - 先扣临时HP: 5 → 0
      - 剩余伤害: 12 - 5 = 7
      - 扣真实HP: 20 → 13
      - 最终: HP 13/25, 临时HP 0
```

### 6.6 专注检定 `[SRD-FULL]`

```
专注 (Concentration) 规则:

  触发条件:
    - 施法者正在维持一个需要专注的法术
    - 受到伤害时

  检定公式:
    DC = max(10, 伤害值 / 2)
    检定: d20 + CON调整值 (+ 熟练, 如果CON豁免熟练)

  示例:
    法师正在专注维持Haste法术
    受到15点伤害:
      DC = max(10, 15/2) = max(10, 7) = 10
      法师CON 14(+2): d20 + 2 vs DC 10
      需要: d20 ≥ 8 → 65%成功率

  失败后果:
    - 专注法术立即结束
    - 法术效果消失

  War Caster专长:
    - 专注豁免有优势
```

### 6.7 伤害计算完整示例

```
示例1: 基础武器伤害
  角色: Lv3 Fighter, STR 16(+3)
  武器: 长剑 (1d8 slashing, 单手)
  命中: 正常

  伤害 = roll(1d8) + 3
  若 roll(1d8) = 5: 伤害 = 5 + 3 = 8 slashing

示例2: 附魔武器暴击
  角色: Lv3 Fighter, STR 16(+3)
  武器: 炽焰长剑 (1d8 slashing + 1d6 fire)
  命中: 暴击(自然20)

  伤害 = 8(max 1d8) + 3(STR) + 6(max 1d6 fire) = 17
  分解: 11 slashing + 6 fire

示例3: 偷袭+抗性
  角色: Lv5 Rogue, DEX 18(+4)
  武器: 细剑 (1d8 piercing, finesse)
  偷袭: 3d6
  目标: 有穿刺抗性的骷髅

  命中伤害 = roll(1d8) + 4 + roll(3d6)
  若 roll(1d8)=6, roll(3d6)=4+5+3=12:
    基础伤害 = 6 + 4 + 12 = 22 piercing
    应用抗性: 22 / 2 = 11 piercing

示例4: 临时HP吸收
  角色: Lv3 Wizard, HP 18/20, 临时HP 8
  受到: 15点火焰伤害

  计算:
    1. 临时HP吸收: 8 → 0 (吸收8点)
    2. 剩余伤害: 15 - 8 = 7
    3. 真实HP: 18 → 11
    4. 最终: HP 11/20, 临时HP 0
```

---

## 7. 法术系统

### 7.1 法术位消耗与恢复 `[SRD-MODIFIED]`

```
法术位消耗:
  - 施放法术消耗对应环位的法术位
  - 戏法(Cantrip): 不消耗法术位
  - 高环施放: 可用更高环位施放低环法术（通常增强效果）

法术位恢复 [SRD-MODIFIED]:

  短休 (Short Rest):
    - 恢复一半1环法术位（向上取整）
    - 邪术师(Warlock): 恢复全部Pact Magic法术位
    - 法师(Wizard): 额外使用Arcane Recovery恢复ceil(Lv/2)总环位

  长休 (Long Rest):
    - 恢复全部法术位至最大值

  示例:
    Lv5 Wizard (1st=4, 2nd=3, 3rd=2):
      短休前: 1st=1, 2环=0, 3环=0
      短休后: 1st=3, 2环=0, 3环=0 (恢复ceil(4/2)=2个1环)
      Arcane Recovery: +3环 (可选1环+2环, 或3个1环)
      最终: 1st=4, 2nd=2, 3rd=0 (假设选1个1环+2个2环)
```

### 7.2 法术解析管线

```
法术解析流程:

  Step 1: CAST (施放)
    - 检查法术位是否足够
    - 检查施法时间（1动作/1附赠/1反应/仪式）
    - 检查成分（言语/姿势/材料）
    - 检查射程和目标
    - 消耗法术位

  Step 2: CONCENTRATION_CHECK (专注检定, 如果需要)
    - 如果施法者正在专注另一个法术
    - 必须放弃旧专注或进行专注检定
    - 新法术自动替换旧专注

  Step 3: SAVE_OR_ATTACK (豁免或攻击)
    - 攻击法术: 进行法术攻击检定 (d20 + spell_attack_mod vs AC)
    - 豁免法术: 目标进行豁免检定 (d20 + save_mod vs spell_dc)
    - 自动命中法术: 如Magic Missile (无需检定)

  Step 4: EFFECT (效果)
    - 计算伤害/治疗/效果
    - 应用到目标
    - 处理AOE范围

  Step 5: DURATION (持续)
    - 瞬发: 立即生效，无需追踪
    - 持续: 记录持续时间（轮/分钟/小时）
    - 专注: 标记为专注法术
```

### 7.3 专注追踪状态机 `[CUSTOM]`

```
专注状态机:

  ┌─────────┐
  │  IDLE   │ ◄── 无专注法术
  └────┬────┘
       │ 施放专注法术
       ▼
  ┌─────────────┐
  │CONCENTRATING│ ◄── 正在专注
  └──────┬──────┘
         │ 受到伤害
         ▼
  ┌─────────────┐
  │  CHECKING   │ ◄── 进行专注检定
  └──────┬──────┘
         │
    ┌────┴────┐
    ▼         ▼
 成功        失败
  │          │
  ▼          ▼
回到        法术结束
CONCENTRATING  → IDLE

其他退出条件:
  - 施放新的专注法术 → 旧法术结束，开始新专注
  - 施法者失能/昏迷 → 专注结束
  - 施法者主动放弃 → 专注结束
  - 法术持续时间到期 → 专注结束
```

### 7.4 法术DC与攻击加值 `[SRD-FULL]`

```
法术豁免DC:
  spell_save_dc = 8 + 熟练加值 + 施法属性调整值

  示例:
    Lv5 Wizard, INT 16(+3), PB=3
    spell_save_dc = 8 + 3 + 3 = 14

法术攻击加值:
  spell_attack_mod = 熟练加值 + 施法属性调整值

  示例:
    Lv5 Wizard, INT 16(+3), PB=3
    spell_attack_mod = 3 + 3 = +6
```

### 7.5 AOE范围规则（5尺网格）`[SRD-FULL]`

| AOE形状 | 描述 | 网格计算 |
|---------|------|----------|
| **锥形 (Cone)** | 从施法者向外扩展的三角形 | 长度X尺的锥形，宽度在末端=X尺 |
| **立方 (Cube)** | 边长X尺的立方体 | X/5 个网格边长 |
| **圆柱 (Cylinder)** | 半径X尺，高Y尺的圆柱 | 圆形覆盖网格 |
| **线形 (Line)** | 宽5尺，长X尺的直线 | X/5 个网格长，1格宽 |
| **球形 (Sphere)** | 半径X尺的球体 | 圆形覆盖网格 |

```
网格覆盖判定:
  - 网格中心点在AOE范围内 → 该网格被覆盖
  - 网格中心点在AOE范围外 → 该网格不被覆盖
  - 边界情况: 中心点恰好在边界上 → 被覆盖

  示例: Fireball (半径20尺球形)
    - 以目标点为中心
    - 覆盖所有中心点距目标点≤20尺的网格
    - 大约覆盖约50个网格（十字形+角）
```

### 7.6 仪式施放 `[SRD-FULL]`

```
仪式施放 (Ritual Casting):
  - 标记为"仪式"的法术可以不消耗法术位施放
  - 额外施法时间: +10分钟
  - 法师: 可仪式施放法术书中的任何仪式法术（无需准备）
  - 其他职业: 需要已准备/已知该法术

  示例:
    Detect Magic (仪式):
      - 正常施放: 1动作, 消耗1环位
      - 仪式施放: 10分钟+1动作, 不消耗法术位
```

### 7.7 升环施放 `[SRD-FULL]`

```
升环施放 (Upcasting):
  - 用更高环位施放法术
  - 某些法术在更高环位有增强效果

  示例:
    Magic Missile (1环):
      - 1环: 3颗飞弹 (3×(1d4+1))
      - 2环: 4颗飞弹
      - 3环: 5颗飞弹
      - 每升1环+1颗飞弹

    Cure Wounds (1环):
      - 1环: 1d8+施法mod
      - 2环: 2d8+施法mod
      - 每升1环+1d8
```

### 7.8 MVP法术列表

#### Fighter (Battle Master) — 战技 `[SRD-FULL]`

| 战技 | 消耗 | 效果 | 豁免 |
|------|------|------|------|
| **Precision Attack** | 1骰 | 攻击检定+d8 | 无 |
| **Trip Attack** | 1骰 | 武器伤害+d8, 目标倒地 | STR DC 8+PB+STR_mod |
| **Riposte** | 1骰 | 反应攻击, 武器伤害+d8 | 无 |
| **Menacing Attack** | 1骰 | 武器伤害+d8, 目标恐慌 | WIS DC 8+PB+STR_mod |
| **Pushing Attack** | 1骰 | 武器伤害+d8, 推15尺 | STR DC 8+PB+STR_mod |

#### Wizard (Evocation) — 塑能法术 `[SRD-FULL]`

| 法术 | 环位 | 施法时间 | 射程 | 效果 | 豁免 |
|------|:----:|----------|------|------|------|
| **Fire Bolt** | 戏法 | 1动作 | 120尺 | 1d10 fire | 法术攻击 |
| **Magic Missile** | 1环 | 1动作 | 120尺 | 3×(1d4+1) force | 自动命中 |
| **Shield** | 1环 | 1反应 | 自身 | AC+5至下回合开始 | 无 |
| **Thunderwave** | 1环 | 1动作 | 15尺立方 | 2d8 thunder + 推10尺 | CON半伤 |
| **Scorching Ray** | 2环 | 1动作 | 120尺 | 3×(2d6 fire) | 法术攻击×3 |
| **Fireball** | 3环 | 1动作 | 150尺 | 8d6 fire (半径20尺) | DEX半伤 |
| **Counterspell** | 3环 | 1反应 | 60尺 | 反制法术 | 无(或检定) |

#### Rogue (Arcane Trickster) — 奥术诡计 `[SRD-FULL]`

| 法术 | 环位 | 效果 |
|------|:----:|------|
| **Mage Hand** | 戏法 | 幽灵手, 30尺操控 |
| **Minor Illusion** | 戏法 | 创造声音或图像 |
| **Disguise Self** | 1环 | 改变外观 |
| **Silent Image** | 1环 | 创造视觉幻象 |
| **Invisibility** | 2环 | 隐形（攻击/施法结束） |

### 7.9 法术专注规则完整 `[SRD-FULL]`

```
专注法术列表 (MVP相关):

  1环:
    - Hex (邪术师): 额外1d6黯蚀伤害
    - Hunter's Mark (游侠): 额外1d6伤害
    - Bless (牧师): 攻击/豁免+1d4
    - Faerie Fire: 区域内敌人发光(攻击优势)

  2环:
    - Invisibility: 隐形
    - Hold Person: 麻痹(每轮豁免)
    - Spiritual Weapon: 浮动武器攻击(不需专注)
    - Web: 束缚区域

  3环:
    - Haste: 目标速度翻倍+AC+2+额外攻击
    - Slow: 区域内敌人速度减半+AC-2
    - Hypnotic Pattern: 区域内魅惑
    - Spirit Guardians: 区域持续伤害

专注维持规则:
  - 同时只能专注1个法术
  - 施放新专注法术 → 旧法术自动结束
  - 受到伤害 → 专注检定 (DC = max(10, 伤害/2))
  - 失能/昏迷 → 专注自动结束
  - 施法者死亡 → 专注自动结束
```

---

## 8. 豁免系统

### 8.1 六维豁免公式 `[SRD-FULL]`

```
豁免检定 = d20 + 属性调整值 + (熟练则+PB)

六维豁免:
  STR豁免: d20 + STR_mod + (STR豁免熟练? PB : 0)
  DEX豁免: d20 + DEX_mod + (DEX豁免熟练? PB : 0)
  CON豁免: d20 + CON_mod + (CON豁免熟练? PB : 0)
  INT豁免: d20 + INT_mod + (INT豁免熟练? PB : 0)
  WIS豁免: d20 + WIS_mod + (WIS豁免熟练? PB : 0)
  CHA豁免: d20 + CHA_mod + (CHA豁免熟练? PB : 0)
```

### 8.2 职业豁免熟练 `[SRD-FULL]`

| 职业 | 豁免熟练 |
|------|----------|
| Fighter | STR, CON |
| Wizard | INT, WIS |
| Rogue | DEX, INT |
| Cleric | WIS, CHA |
| Ranger | STR, DEX |
| Paladin | WIS, CHA |
| Sorcerer | CON, CHA |
| Bard | DEX, CHA |
| Druid | INT, WIS |
| Monk | STR, DEX |
| Warlock | WIS, CHA |
| Barbarian | STR, CON |

### 8.3 死亡豁免与死亡失败 `[SRD-MODIFIED + CUSTOM]`

```
死亡系统规则 [SRD-MODIFIED + CUSTOM]:

  本游戏采用双轨死亡机制:
    - 主要机制: 3轮无治疗计时器（紧迫感）
    - 次要机制: 死亡失败计数（受伤累积风险）

  ─── 触发条件 ───
    - 角色HP降至0
    - 角色进入昏迷(Unconscious) + 倒地(Prone)状态

  ─── 主要机制: 3轮死亡计时器 ───
    1. HP降至0 → rounds_without_healing = 0, death_failures = 0
    2. 每轮开始: rounds_without_healing += 1
    3. 如果3轮内无人治疗且未稳定 → 角色永久死亡
    4. 如果有人治疗(HP>0) → 角色恢复，所有计数重置

  ─── 次要机制: 死亡失败计数 [CUSTOM] ───
    1. 角色HP=0时受到任何伤害 → death_failures += 2
    2. death_failures >= 3 → 角色永久死亡（无需等待3轮）
    3. 死亡失败不会因稳定化重置（但HP恢复>0时全部重置）

    设计理由:
      - 标准5e的"0HP受伤=直接死亡"过于残酷
      - 改为累积失败制，给队友更多救援时间
      - 但仍保持高压: 一次受伤=2失败，两次受伤=死亡

  ─── 稳定化 (Stabilize) ───
    检定: d20 + Medicine技能 vs DC 10
    成功:
      - 角色从"濒死"变为"稳定"
      - HP仍为0，仍处于昏迷状态
      - 停止计数死亡轮次（rounds_without_healing不再增加）
      - 停止计数死亡失败（death_failures不再增加）
      - 仍需治疗才能恢复行动
    失败: 无效果，继续计数
    自动成功: Spare the Dying (戏法) 等特定效果

  ─── 治疗定义 ───
    - 任何恢复HP至>0的效果（法术/药水/能力）
    - 治疗后: rounds_without_healing = 0, death_failures = 0
    - 临时HP不算治疗
    - 稳定化不算治疗（只是停止计数）

  ─── 状态追踪 ───
    每个HP=0的角色追踪两个计数器:
      - rounds_without_healing: 0-3（每轮+1）
      - death_failures: 0-3（受伤+2）
    任一计数器达到3 → 角色永久死亡

  ─── 示例 ───
    示例1: 标准3轮死亡
      回合1: 战士HP降至0, rounds=0, failures=0
      回合2: 无人治疗, rounds=1
      回合3: 无人治疗, rounds=2
      回合4: 无人治疗, rounds=3 → 永久死亡

    示例2: 受伤加速死亡
      回合1: 战士HP降至0, rounds=0, failures=0
      回合2: 受到5点伤害, rounds=1, failures=2
      回合3: 受到3点伤害, rounds=2, failures=4(≥3) → 永久死亡

    示例3: 稳定化保命
      回合1: 战士HP降至0, rounds=0, failures=0
      回合2: 受到伤害, rounds=1, failures=2
      回合3: 盗贼DC 10 Medicine成功 → 稳定化
        → rounds停止增加, failures停止增加
        → 战士仍昏迷但不再有死亡风险
      回合5: 法师施放Healing Word → 战士恢复

    示例4: 治疗重置一切
      回合1: 战士HP降至0, rounds=0, failures=0
      回合2: 受到伤害, rounds=1, failures=2
      回合3: 牧师施放Cure Wounds → HP恢复>0
        → rounds=0, failures=0, 战士恢复行动
```

### 8.4 豁免DC来源

| 来源 | DC公式 | 示例 |
|------|--------|------|
| 法术 | 8 + PB + 施法属性mod | Lv5 Wizard INT 16: DC 14 |
| 陷阱 | 由陷阱数据定义 | 通常DC 10-15 |
| 环境效果 | 由地形数据定义 | 通常DC 10-13 |
| 怪物能力 | 由怪物数据定义 | 通常DC 11-15 |
| 战技 | 8 + PB + STR或DEX mod | Lv3 Fighter STR 16: DC 13 |

---

## 9. 条件追踪系统

### 9.1 完整条件列表 `[SRD-FULL + SRD-MODIFIED]`

| # | ID | 名称 | 机械效果 | 结束条件 |
|:--:|----|------|----------|----------|
| 1 | Blinded | 目盲 | 攻击劣势; 对其攻击优势; 依赖视觉技能自动失败 | 效果结束 |
| 2 | Charmed | 魅惑 | 不能攻击魅惑者; 魅惑者社交优势 | 效果结束 |
| 3 | Deafened | 耳聋 | 依赖听觉技能自动失败 | 效果结束 |
| 4 | Frightened | 恐慌 | 恐慌源在视线内时攻击/技能劣势; 不能主动接近 | 效果结束或脱离视线 |
| 5 | Grappled | 擒抱 | 速度=0; 擒抱者失能时结束 | 脱离擒抱(运动/特技 vs 运动) |
| 6 | Incapacitated | 失能 | 不能采取动作或反应 | 效果结束 |
| 7 | Invisible | 隐形 | 重度遮蔽; 攻击优势; 对其攻击劣势 | 攻击或施法结束 |
| 8 | Paralyzed | 麻痹 | 失能+不能移动; STR/DEX豁免自动失败; 5尺内攻击自动暴击 | 效果结束 |
| 9 | Petrified | 石化 | 麻痹+重量×10+免疫伤害+停止老化 | 特殊效果解除 |
| 10 | Poisoned | 中毒 | 攻击和技能检定劣势 | 效果结束或豁免成功 |
| 11 | Prone | 倒地 | 只能爬行; 攻击劣势; 5尺内对其攻击优势; 站立消耗半速 | 消耗半速站立 |
| 12 | Restrained | 束缚 | 速度=0; 攻击劣势; 对其攻击优势; DEX豁免劣势 | 效果结束或挣脱 |
| 13 | Stunned | 震慑 | 失能+不能移动; STR/DEX豁免自动失败; 对其攻击优势 | 效果结束 |
| 14 | Unconscious | 昏迷 | 失能+不能移动+掉落手中物+倒地; 5尺内攻击自动暴击; STR/DEX豁免自动失败 | HP恢复>0或治疗 |

### 9.2 疲乏系统 `[SRD-MODIFIED: 简化为3级]`

| 等级 | 名称 | 效果 | 恢复方式 |
|:----:|------|------|----------|
| 0 | 正常 | 无 | - |
| 1 | 疲乏 | 技能检定劣势 | 长休消除1级 |
| 2 | 疲乏 | 速度减半 | 长休消除1级 |
| 3 | 力竭 | 攻击骰和豁免劣势; HP最大值减半 | 长休消除1级 |

```
疲乏叠加规则:
  - 每次获得疲乏 → 等级+1
  - 等级3时再获得疲乏 → 直接死亡
  - 长休: 消除1级疲乏
  - 特殊效果: 某些药水/法术可消除疲乏

  与标准5e区别:
    - 标准5e有6级，本游戏简化为3级
    - 减少管理负担，保持紧迫感
```

### 9.3 条件堆叠与交互规则

```
堆叠规则:

  1. 同名条件不堆叠:
     两次中毒 = 一次中毒, 持续时间取较长者

  2. 不同条件可共存:
     角色可同时: 目盲 + 倒地 + 中毒

  3. 来源刷新:
     同一来源重新施加相同条件 → 刷新持续时间

  4. 优势/劣势互斥:
     多源优势 + 任一源劣势 = 平骰
     3源优势 + 1源劣势 = 平骰
     1源优势 + 0源劣势 = 优势

  5. 免疫优先:
     角色对某条件免疫 → 忽略所有施加尝试

  6. 失能传递:
     Paralyzed/Stunned/Unconscious → 自动包含 Incapacitated

  7. 倒地交互:
     昏迷角色自动倒地
     0HP角色默认倒地+昏迷

  8. 0HP状态:
     HP降至0 = Unconscious + Prone + 不可行动
```

### 9.4 条件持续时间追踪

```json
{
  "condition_id": "cond_001",
  "character_id": "char_7a3f2b1c",
  "condition_type": "poisoned",
  "source_id": "spell_poison_spray",
  "source_name": "毒雾术",
  "applied_at_round": 3,
  "duration": {
    "type": "rounds",
    "remaining": 3,
    "max": 3
  },
  "save_ends": {
    "ability": "con",
    "dc": 13,
    "can_retry_each_turn": true,
    "retry_action": "end_of_turn"
  },
  "mechanical_effects": {
    "attack_disadvantage": true,
    "ability_check_disadvantage": true
  }
}
```

---

## 10. 地形交互规则

### 10.1 交互标签系统 `[CUSTOM]`

每个场景物体有**交互标签**，决定其在战斗中的行为：

| 标签 | 交互方式 | 战斗应用 | 伤害/效果 |
|------|----------|----------|-----------|
| **Pushable** | 角色可推倒 | 推倒后有掩体/堵路 | 无直接伤害 |
| **Flammable** | 可点燃 | 火焰法术点燃→范围伤害 | 2d6 fire, 半径5尺 |
| **Climbable** | 可攀爬 | 获得高处优势 | 无直接伤害 |
| **Breakable** | 可破坏 | 破坏桥梁/箱子 | 无直接伤害 |
| **Readable** | 可阅读 | 获得线索/法术卷轴 | 无直接伤害 |
| **Flammable_Liquid** | 油桶等 | 点燃后大范围持续燃烧 | 3d6 fire/轮, 半径15尺 |
| **Electrical** | 导电 | 闪电链传导 | 传导至邻接目标 |
| **Hideable** | 可躲藏 | 潜行攻击优势 | 无直接伤害 |

### 10.2 环境危害

| 危害类型 | 效果 | 豁免 | DC |
|----------|------|------|:--:|
| **火坑** | 3d6 fire/轮 | DEX半伤 | 13 |
| **酸液池** | 2d6 acid/轮 | DEX半伤 | 12 |
| **冰面** | 移动需DC 10 DEX检定, 失败倒地 | DEX | 10 |
| **毒气** | 2d6 poison + 中毒 | CON半伤 | 13 |
| **尖刺陷阱** | 2d10 piercing | DEX全避 | 15 |
| **落石** | 4d6 bludgeoning | DEX半伤 | 14 |

### 10.3 高度优势/劣势 `[CUSTOM]`

```
高度规则:

  高处攻击低处:
    - 近战: 无特殊效果（5尺网格内）
    - 远程: 攻击优势（从高处向下射击）

  低处攻击高处:
    - 近战: 无特殊效果（5尺网格内）
    - 远程: 攻击劣势（从低处向上射击）

  高度差判定:
    - 高度差≥10尺: 触发优势/劣势
    - 高度差<10尺: 无效果

  攀爬:
    - 消耗双倍移动速度
    - Athletics检定 (DC由地形决定)
    - 失败: 原地不动或坠落
```

### 10.4 地形掩体

| 地形元素 | 掩体类型 | 说明 |
|----------|----------|------|
| 矮墙/栏杆 | 半掩体 | +2 AC |
| 树木/柱子 | 半掩体或3/4掩体 | 取决于遮挡比例 |
| 岩石/大箱子 | 3/4掩体 | +5 AC |
| 墙壁/门 | 全掩体 | 无法瞄准 |
| 其他生物 | 半掩体 | +2 AC |

---

## 11. 敌人AI行为树

### 11.1 AI决策流程 `[CUSTOM]`

```
AI决策流程 (每回合):

  Step 1: 评估战场态势
    - 统计双方存活人数
    - 识别高威胁目标（高伤害/低HP）
    - 评估自身HP/资源状态

  Step 2: 选择目标
    - 优先级: 最近的敌人 > 最低HP敌人 > 最高威胁敌人
    - 特殊: 某些敌人类型有特定目标偏好

  Step 3: 选择行动
    - 基于敌人类型和当前状态
    - 优先使用最有效的行动

  Step 4: 选择位置
    - 近战敌人: 移向目标
    - 远程敌人: 保持距离
    - 施法者: 远离前线
```

### 11.2 目标选择启发式

```
目标选择算法:

  function select_target(enemy, party_members):
    candidates = []

    for member in party_members:
      if member.is_dead():
        continue

      score = 0

      # 距离因素 (越近越高)
      distance = get_distance(enemy, member)
      score += max(0, 20 - distance)

      # HP因素 (越低越高)
      hp_ratio = member.hp_current / member.hp_max
      score += (1 - hp_ratio) * 15

      # 威胁因素 (伤害输出越高越高)
      threat = estimate_damage_per_round(member)
      score += threat * 2

      # 特殊因素
      if member.is_concentrating():
        score += 10  # 优先打断专注
      if member.is_healer():
        score += 8   # 优先击杀治疗者
      if member.has_condition("poisoned"):
        score -= 5   # 已中毒，降低优先级

      candidates.append({member, score})

    return max(candidates, key=score)
```

### 11.3 敌人类型行为模式

| 敌人类型 | 行为模式 | 目标偏好 | 位置策略 |
|----------|----------|----------|----------|
| **近战战士** | 冲锋→攻击 | 最近敌人 | 前线 |
| **远程射手** | 射击→后退 | 最低HP敌人 | 后方 |
| **施法者** | 施法→撤退 | 高威胁敌人 | 远离前线 |
| **治疗者** | 治疗→辅助 | 治疗队友 | 中后方 |
| **刺客** | 潜行→偷袭 | 最高威胁敌人 | 侧翼 |

### 11.4 MVP敌人完整数据

#### Goblin (哥布林) `[SRD-FULL]`

```json
{
  "id": "monster_goblin",
  "name": "哥布林",
  "name_en": "Goblin",
  "type": "humanoid",
  "size": "small",
  "cr": 0.25,
  "xp": 50,

  "abilities": {
    "str": 8, "dex": 14, "con": 10,
    "int": 10, "wis": 8, "cha": 8
  },

  "hp": { "max": 7, "current": 7 },
  "ac": 15,
  "ac_formula": "15 (leather armor + shield)",
  "speed": 30,

  "proficiency_bonus": 2,
  "senses": { "darkvision": 60 },
  "languages": ["common", "goblin"],

  "skills": {
    "stealth": 6
  },

  "attacks": [
    {
      "name": "弯刀",
      "type": "melee",
      "weapon": "scimitar",
      "attack_bonus": 4,
      "damage": "1d6+2 slashing",
      "reach": 5
    },
    {
      "name": "短弓",
      "type": "ranged",
      "weapon": "shortbow",
      "attack_bonus": 4,
      "damage": "1d6+2 piercing",
      "range": "80/320"
    }
  ],

  "traits": [
    {
      "id": "nimble_escape",
      "name": "灵巧逃脱",
      "description": "哥布林可以用附赠动作执行撤离或躲藏动作"
    }
  ],

  "behavior": {
    "type": "skirmisher",
    "target_preference": "nearest",
    "position_strategy": "hit_and_run",
    "special_tactics": [
      "use_nimble_escape_to_disengage",
      "hide_after_attack_if_possible",
      "flee_if_hp_below_3"
    ]
  }
}
```

#### Skeleton (骷髅) `[SRD-FULL]`

```json
{
  "id": "monster_skeleton",
  "name": "骷髅",
  "name_en": "Skeleton",
  "type": "undead",
  "size": "medium",
  "cr": 0.25,
  "xp": 50,

  "abilities": {
    "str": 10, "dex": 14, "con": 15,
    "int": 6, "wis": 8, "cha": 5
  },

  "hp": { "max": 13, "current": 13 },
  "ac": 13,
  "ac_formula": "13 (armor scraps)",
  "speed": 30,

  "proficiency_bonus": 2,
  "senses": { "darkvision": 60 },
  "languages": ["understands_common_but_cant_speak"],

  "damage_vulnerabilities": ["bludgeoning"],
  "damage_immunities": ["poison"],
  "condition_immunities": ["exhaustion", "poisoned"],

  "attacks": [
    {
      "name": "短剑",
      "type": "melee",
      "weapon": "shortsword",
      "attack_bonus": 4,
      "damage": "1d6+2 piercing",
      "reach": 5
    },
    {
      "name": "短弓",
      "type": "ranged",
      "weapon": "shortbow",
      "attack_bonus": 4,
      "damage": "1d6+2 piercing",
      "range": "80/320"
    }
  ],

  "behavior": {
    "type": "undead_soldier",
    "target_preference": "nearest_living",
    "position_strategy": "advance_and_attack",
    "special_tactics": [
      "no_retreat_undead_fearless",
      "attack_nearest_target",
      "no_self_preservation"
    ]
  }
}
```

#### Bandit (强盗) `[SRD-FULL]`

```json
{
  "id": "monster_bandit",
  "name": "强盗",
  "name_en": "Bandit",
  "type": "humanoid",
  "size": "medium",
  "cr": 0.125,
  "xp": 25,

  "abilities": {
    "str": 11, "dex": 12, "con": 12,
    "int": 10, "wis": 10, "cha": 10
  },

  "hp": { "max": 11, "current": 11 },
  "ac": 12,
  "ac_formula": "12 (leather armor)",
  "speed": 30,

  "proficiency_bonus": 2,
  "languages": ["common"],

  "attacks": [
    {
      "name": "弯刀",
      "type": "melee",
      "weapon": "scimitar",
      "attack_bonus": 3,
      "damage": "1d6+1 slashing",
      "reach": 5
    },
    {
      "name": "轻弩",
      "type": "ranged",
      "weapon": "light_crossbow",
      "attack_bonus": 3,
      "damage": "1d8+1 piercing",
      "range": "80/320"
    }
  ],

  "behavior": {
    "type": "basic_melee",
    "target_preference": "nearest",
    "position_strategy": "advance_and_attack",
    "special_tactics": [
      "flee_if_outnumbered",
      "surrender_if_hp_below_3"
    ]
  }
}
```

### 11.5 MVP Boss完整数据

#### Troll (巨魔) `[SRD-FULL]`

```json
{
  "id": "monster_troll",
  "name": "巨魔",
  "name_en": "Troll",
  "type": "giant",
  "size": "large",
  "cr": 5,
  "xp": 1800,

  "abilities": {
    "str": 18, "dex": 13, "con": 20,
    "int": 7, "wis": 9, "cha": 7
  },

  "hp": { "max": 84, "current": 84 },
  "ac": 15,
  "ac_formula": "15 (natural armor)",
  "speed": 30,

  "proficiency_bonus": 3,
  "senses": { "darkvision": 60 },
  "languages": ["giant"],

  "skills": {
    "perception": 2
  },

  "attacks": [
    {
      "name": "多击",
      "type": "multiattack",
      "description": "巨魔进行两次爪击",
      "attacks": [
        {
          "name": "爪击",
          "type": "melee",
          "attack_bonus": 7,
          "damage": "1d6+4 slashing",
          "reach": 5
        },
        {
          "name": "爪击",
          "type": "melee",
          "attack_bonus": 7,
          "damage": "1d6+4 slashing",
          "reach": 5
        }
      ]
    },
    {
      "name": "咬击",
      "type": "melee",
      "attack_bonus": 7,
      "damage": "1d6+4 piercing",
      "reach": 5
    }
  ],

  "traits": [
    {
      "id": "regeneration",
      "name": "再生",
      "description": "巨魔在回合开始时恢复10点HP。如果巨魔受到火焰或酸蚀伤害，则在下一回合开始前不会再生。",
      "mechanical": {
        "heal_per_turn": 10,
        "suppressed_by": ["fire", "acid"],
        "suppression_duration": "until_next_turn_start"
      }
    },
    {
      "id": "keen_smell",
      "name": "敏锐嗅觉",
      "description": "巨魔基于嗅觉的感知检定有优势",
      "mechanical": {
        "perception_advantage": "smell_based"
      }
    }
  ],

  "behavior": {
    "type": "berserker",
    "target_preference": "nearest",
    "position_strategy": "charge_and_attack",
    "special_tactics": [
      "always_multiattack",
      "prioritize_weakest_target",
      "no_retreat_until_half_hp",
      "at_half_hp_consider_fleeing"
    ],
    "boss_mechanics": {
      "phase_1": {
        "hp_threshold": "100%",
        "behavior": "aggressive_melee",
        "description": "巨魔疯狂攻击最近的目标"
      },
      "phase_2": {
        "hp_threshold": "50%",
        "behavior": "defensive_heal",
        "description": "巨魔开始尝试拉开距离等待再生",
        "trigger": "hp_below_42"
      }
    }
  },

  "legendary_actions": [],

  "loot_table": {
    "guaranteed": [
      { "item_id": "item_troll_hide", "rarity": "uncommon", "description": "巨魔皮 (制作材料)" }
    ],
    "random": [
      { "rarity": "rare", "count": 1, "type": "weapon_or_armor" },
      { "rarity": "uncommon", "count": "1d2", "type": "any" }
    ],
    "gold": "4d10 × 10 gp"
  }
}
```

### 11.6 AI难度级别 `[CUSTOM]`

| 难度 | 行为特征 | 目标选择 | 资源使用 |
|------|----------|----------|----------|
| **Easy** | 随机攻击，不使用战术 | 随机 | 不使用消耗品 |
| **Normal** | 基本战术，攻击最近/最低HP | 启发式 | 偶尔使用 |
| **Hard** | 高级战术，优先击杀治疗者/施法者 | 精确计算 | 合理使用 |
| **Tactical** | 完美战术，利用地形和配合 | 最优选择 | 最优使用 |

---

## 12. 战斗UI交互流程

### 12.1 战斗屏幕布局 (参考GDD §5.5)

```
┌──────────────────────────────────────────────────────────────┐
│  [角色状态栏]  [回合顺序条]  [小地图]                          │
│  ┌────────┐  ┌────────┐  ┌────────┐                        │
│  │战士 Lv3│  │法师 Lv3│  │盗贼 Lv3│                        │
│  │ HP: 28 │  │ HP: 18 │  │ HP: 22 │                        │
│  │ AC: 16 │  │ AC: 12 │  │ AC: 15 │                        │
│  └────────┘  └────────┘  └────────┘                        │
│                                                              │
│  ┌────────────────────────────────────────────┐              │
│  │                                            │              │
│  │          2D 战术地图 (像素风)               │              │
│  │     每个5尺格约32x32像素                    │              │
│  │     显示：地形/角色/效果/可交互物体          │              │
│  │                                            │              │
│  └────────────────────────────────────────────┘              │
│                                                              │
│  ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐                      │
│  │攻击  │ │法术  │ │物品  │ │技能  │   [结束回合]           │
│  └──┬───┘ └──┬───┘ └──┬───┘ └──┬───┘                      │
│     ↓        ↓        ↓        ↓                            │
│  子菜单展开，显示可用选项+骰子概率预览                        │
│                                                              │
│  ┌────────────────────────────────────────────┐              │
│  │ 战斗日志（DM叙述）                          │              │
│  │ "哥布林游荡者挥动弯刀，骰出12！              │              │
│  │  未能穿透你的锁子甲。"                       │              │
│  └────────────────────────────────────────────┘              │
└──────────────────────────────────────────────────────────────┘
```

### 12.2 回合流程UI

```
玩家回合UI流程:

  1. 高亮当前角色
     - 角色精灵闪烁/边框高亮
     - 先攻条上当前角色标记

  2. 显示可用行动
     ┌─────────────────────────────────────┐
     │  [攻击] [法术] [物品] [技能] [待机]  │
     │                                     │
     │  移动点数: 30/30                     │
     │  主动作: 可用                        │
     │  附赠动作: 可用 (Second Wind)        │
     │  反应: 可用                          │
     └─────────────────────────────────────┘

  3. 选择行动后
     - 攻击: 显示目标选择（高亮可攻击目标）
     - 法术: 显示法术列表（按环位分组）
     - 物品: 显示背包物品（仅消耗品）
     - 技能: 显示可用技能（Hide/Help等）

  4. 确认执行
     - 显示预计伤害/命中率
     - 确认按钮
     - 执行动画

  5. 结果显示
     - 骰子动画
     - 伤害数字弹出
     - 战斗日志更新
```

### 12.3 行动菜单层级

```
行动菜单结构:

  [攻击]
    ├─ 主手武器 (长剑 1d8+3)
    ├─ 副手武器 (短剑 1d6, 附赠动作)
    └─ 徒手攻击 (1+STR_mod)

  [法术]
    ├─ 戏法
    │   ├─ Fire Bolt (1d10 fire)
    │   └─ Minor Illusion
    ├─ 1环 (剩余: 3/4)
    │   ├─ Magic Missile (3×1d4+1)
    │   ├─ Shield (反应)
    │   └─ Thunderwave (2d8 thunder)
    ├─ 2环 (剩余: 2/3)
    │   └─ Scorching Ray (3×2d6 fire)
    └─ 3环 (剩余: 1/2)
        └─ Fireball (8d6 fire)

  [物品]
    ├─ 治疗药水 (2d4+2 HP)
    ├─ 解毒剂
    └─ 火把

  [技能]
    ├─ Hide (隐匿检定)
    ├─ Help (援助队友)
    ├─ Dodge (躲避)
    └─ Dash (冲刺)

  [待机]
    └─ 结束回合，不执行任何行动
```

### 12.4 移动模式

```
移动模式UI:

  1. 点击移动按钮或按M键
  2. 地图高亮可移动范围（绿色网格）
     - 绿色: 可到达
     - 黄色: 需要冲刺才能到达
     - 红色: 困难地形（消耗双倍移动）
  3. 点击目标网格
  4. 角色沿路径移动（自动寻路）
  5. 移动过程中可随时点击停止

  移动点数显示:
    ┌────────────────────┐
    │ 移动: 20/30 尺     │
    │ ████████░░░░ 67%   │
    └────────────────────┘
```

### 12.5 反应提示

```
反应触发提示:

  当满足反应条件时，弹出提示:

  ┌─────────────────────────────────────────┐
  │  ⚡ 反应机会                             │
  │                                         │
  │  哥布林正在离开你的触及范围              │
  │                                         │
  │  [借机攻击]  [允许离开]                  │
  │                                         │
  │  借机攻击: d20+5 vs AC 15               │
  │  预计伤害: 1d8+3 = 7.5                   │
  └─────────────────────────────────────────┘

  其他反应触发:
    - Shield: 被攻击命中时
    - Counterspell: 敌人施法时
    - Uncanny Dodge: 被攻击命中时
    - Protection: 邻接队友被攻击时
```

### 12.6 专注指示器

```
专注法术指示器:

  ┌────────────────────────────┐
  │  🔮 专注中: Haste          │
  │  持续: 8/10 轮             │
  │  目标: 战士                │
  │                            │
  │  ⚠️ 受到伤害需专注检定     │
  │  DC = max(10, 伤害/2)      │
  └────────────────────────────┘

  位置: 角色状态栏下方或法术图标旁
  颜色: 蓝色边框表示专注中
```

### 12.7 条件指示器

```
条件图标显示:

  角色头顶显示条件图标:
    🌀 中毒 (Poisoned)
    😨 恐慌 (Frightened)
    💀 中断 (Concentration broken)
    🛡️ 掩体 (Half/Three-quarters cover)
    ⚡ 加速 (Haste)
    🔥 燃烧 (Taking fire damage each turn)

  鼠标悬停显示详细信息:
    ┌────────────────────────────┐
    │ 中毒 (Poisoned)            │
    │ 来源: 哥布林毒镖           │
    │ 剩余: 2轮                  │
    │ 效果: 攻击和技能检定劣势   │
    │ 结束: 每轮结束CON豁免DC13  │
    └────────────────────────────┘
```

### 12.8 骰子动画

```
骰子动画流程:

  1. 攻击检定
     - d20骰子滚动动画 (0.5秒)
     - 显示原始值 (高亮自然1/20)
     - 显示加值 (+5)
     - 显示最终值 vs AC
     - 命中: 绿色特效 + 命中音效
     - 未命中: 红色特效 + 未命中音效
     - 暴击: 金色特效 + 暴击音效

  2. 伤害骰
     - 伤害骰滚动动画 (0.3秒)
     - 显示每个骰子结果
     - 显示总伤害
     - 伤害数字从目标头顶弹出

  3. 豁免检定
     - d20骰子滚动动画
     - 显示加值
     - 显示DC
     - 成功/失败指示
```

---

## 13. 短休/长休机制 `[SRD-MODIFIED]`

### 13.1 短休规则

```
短休 (Short Rest):
  触发时机: 场景之间（战斗结束后，进入下一场景前）
  持续时间: 约1分钟（游戏内时间，非现实时间）

  恢复内容:
    1. 法术位恢复 [SRD-MODIFIED]:
       - 恢复一半1环法术位（向上取整）
       - 邪术师: 恢复全部Pact Magic法术位
       - 法师: 额外使用Arcane Recovery

    2. Hit Dice治疗:
       - 消耗Hit Dice恢复HP
       - 每个Hit Dice: roll(Hit Die) + CON_mod
       - Hit Dice数量: 等级数（长休恢复一半）

    3. 职业特性恢复:
       - Fighter: Action Surge恢复
       - Fighter: Second Wind恢复
       - Rogue: (无短休恢复资源)

    4. 同调:
       - 可进行物品同调

  示例:
    Lv3 Fighter (Hit Die d10, CON 14):
      短休前: HP 15/28, Hit Dice 2/3
      消耗1个Hit Dice: roll(1d10) + 2 = 7 + 2 = 9
      短休后: HP 24/28, Hit Dice 1/3
```

### 13.2 长休规则

```
长休 (Long Rest):
  触发时机: 冒险节点（安全区域，如营地/城镇）
  持续时间: 约8小时（游戏内时间）

  恢复内容:
    1. HP完全恢复:
       - HP恢复至最大值

    2. 法术位完全恢复:
       - 所有法术位恢复至最大值

    3. Hit Dice恢复:
       - 恢复一半的Hit Dice（向上取整）

    4. 职业特性完全恢复:
       - 所有短休/长休恢复的特性

    5. 疲乏消除:
       - 消除1级疲乏

    6. 临时HP消失:
       - 所有临时HP清零

    7. 条件清除:
       - 某些持续性条件在长休后结束

  示例:
    Lv5 Wizard (1st=4, 2nd=3, 3rd=2):
      长休前: HP 20/32, 1st=1, 2nd=0, 3rd=0, Hit Dice 2/5
      长休后: HP 32/32, 1st=4, 2nd=3, 3rd=2, Hit Dice 4/5
```

### 13.3 冒险类型限制

| 冒险类型 | 短休次数 | 长休次数 | 说明 |
|----------|:--------:|:--------:|------|
| 短冒险 | 不限 | 0 | 无安全区域 |
| 中冒险 | 不限 | 1-2 | 特定节点可长休 |
| 长冒险 | 不限 | 3-5 | 多个安全区域 |

---

## 14. 战斗结算与死亡

### 14.1 胜利条件

```
胜利条件:
  - 所有敌方单位死亡或逃跑
  - Boss被击败
  - 特殊目标完成（如存活N回合/保护NPC）

胜利结算流程:
  1. 战斗动画结束
  2. 显示胜利画面
  3. 触发LLM战斗叙述生成
  4. 分配经验值
  5. 生成战利品
  6. 显示战利品分配界面
  7. 返回冒险场景
```

### 14.2 失败条件

```
失败条件:
  - 所有玩家角色死亡（HP=0且rounds_without_healing ≥ 3或death_failures ≥ 3）
  - 特殊失败条件（如NPC被杀/回合耗尽）

撤退条件:
  - 队伍通过撤退投票并成功通过撤退检定 → 见§14.8撤退机制
  - 撤退不是失败，但有独立惩罚

失败结算流程:
  1. 战斗动画结束
  2. 显示失败画面
  3. 触发LLM失败叙述生成
  4. 应用失败惩罚（见§14.5）
  5. 检查角色永久死亡
  6. 返回酒馆
```

### 14.3 死亡机制回顾 `[SRD-MODIFIED + CUSTOM]`

```
死亡流程（双轨机制）:

  ─── 阶段1: HP降至0 ───
     - 角色进入昏迷(Unconscious) + 倒地(Prone)
     - rounds_without_healing = 0
     - death_failures = 0

  ─── 阶段2: 每轮开始检查 ───
     - rounds_without_healing += 1
     - 如果 rounds >= 3 → 角色永久死亡

  ─── 阶段3: 受到额外伤害 ───
     - 角色HP=0时受到任何伤害 → death_failures += 2
     - 如果 death_failures >= 3 → 角色永久死亡
     - 注意: 不再是"0HP受伤=直接死亡"，改为累积失败制

  ─── 阶段4: 治疗 ───
     - 任何恢复HP至>0的效果 → 角色恢复
     - rounds_without_healing = 0
     - death_failures = 0

  ─── 阶段5: 稳定化 ───
     - DC 10 Medicine检定
     - 成功: 角色稳定
       · HP仍为0，仍昏迷
       · 停止计数死亡轮次
       · 停止计数死亡失败
       · 仍需治疗才能行动
     - 失败: 无效果，继续计数

  ─── 死亡判定优先级 ───
     1. death_failures >= 3 → 立即永久死亡
     2. rounds_without_healing >= 3 → 永久死亡
     3. 两个计数器独立追踪，任一触发即死亡
```

### 14.4 稳定化规则

```
稳定化 (Stabilize):
  检定: d20 + Medicine技能 vs DC 10
  成功: 角色从"濒死"变为"稳定"
    - HP仍为0
    - 不再计数死亡轮次
    - 仍处于昏迷状态
    - 需要治疗才能恢复行动

  失败: 无效果，继续计数

  自动成功:
    - 某些法术/药水可自动稳定
    - 如 Spare the Dying (戏法)
```

### 14.5 失败惩罚 `[CUSTOM]`

> 失败惩罚分为两大类：**战斗败退惩罚**（队伍被击败）和**主动撤退惩罚**（战略性撤退）。撤退的代价低于败退，鼓励玩家在不利局面下做出明智判断。

#### 14.5.1 战斗败退惩罚表

| 惩罚等级 | 触发条件 | 效果 |
|----------|----------|------|
| **轻微** | 部分队员倒地但最终胜利 | 无额外惩罚（倒地队员通过治疗恢复后无长期影响） |
| **中等** | 全队败退（被击败）但无人永久死亡 | 随机装备损坏(1-2件退化25%) + 金币损失(10-20%) |
| **严重** | 有角色永久死亡（双轨死亡机制触发：3轮无治疗 或 死亡失败>=3） | 角色永久死亡 + 全队疲乏+1 + 装备损坏(1件退化至broken) |
| **灾难性** | 全灭（全员永久死亡） | 所有角色死亡 + 所有装备退化至broken + 金币损失50% |

> **死亡触发说明**：角色的永久死亡通过双轨机制判定（详见§8.3），任一轨道触发即死亡：
> - **主要轨道**：HP=0后3轮无治疗 → 永久死亡
> - **次要轨道**：HP=0期间death_failures累积达到3次 → 永久死亡
> 
> 惩罚等级取触发的最严重级别，不叠加。示例：有人死亡+全灭 → 按"灾难性"处理。

#### 14.5.2 主动撤退惩罚表

| 惩罚项 | 效果 | 说明 |
|--------|------|------|
| **疲乏** | 全队获得1级疲乏 | 撤退的体力消耗 |
| **金币损失** | 丢失10-20%当前金币 | 撤退中遗落 |
| **装备损坏** | 1件随机装备退化25% | 匆忙中受损 |
| **冒险进度** | 当前节点标记"已撤退" | 重新进入需再次触发遭遇（可能更难） |

> 以上所有惩罚**同时生效**（非选择其一）。撤退惩罚独立于败退惩罚——撤退不会同时触发角色死亡。

#### 14.5.3 装备损坏与金币损失规则

```
惩罚叠加规则:
  - 败退惩罚按最高级生效，不叠加（严重 vs 灾难性→按灾难性）
  - 撤退惩罚独立于败退惩罚

装备损坏规则:
  - 退化25%: 装备耐久度降低，属性暂时减弱
  - 退化至broken: 装备完全失效，需要修理
  - 修理费用: broken→正常 = 装备价值的50%
  - 退化25%→正常 = 装备价值的15%

金币损失:
  - 从队伍总金币中扣除
  - 不足时扣除至0，不产生负数
  - 丢失的金币在冒险结束时结算
```

### 14.6 战利品与XP分配

```
XP分配规则:
  - 每个击杀: 使用DND 5e SRD标准XP值（按CR查表）
  - 完成冒险: 基于冒险长度的完成奖励（见failure-growth.md §2.2）
  - 多人满额获取: 每个角色独立获取全额XP（不分摊）

  DND 5e CR→XP对照表（部分）:
    CR 0: 10 XP
    CR 1/8: 25 XP
    CR 1/4: 50 XP
    CR 1/2: 100 XP
    CR 1: 200 XP
    CR 2: 450 XP
    CR 3: 700 XP
    CR 5: 1,800 XP
    CR 10: 5,900 XP

  注意: XP获取与角色等级无关——高等级角色杀低CR怪物获得的XP不变。
  这鼓励玩家挑战更高CR的遭遇，而非反复刷低级怪物。

战利品分配:
  - 战利品在战斗结束后生成
  - 玩家可选择分配给特定角色
  - 灵魂绑定物品自动绑定
  - 超出背包容量的物品提示丢弃
```

### 14.7 LLM战斗叙述触发点

```
LLM叙述触发时机:

  1. 战斗开始
     - 生成战斗开场描述
     - 描述敌人外观和地形

  2. 暴击/大失败
     - 生成戏剧性的攻击描述
     - "战士挥出致命一击，长剑贯穿哥布林的胸膛！"

  3. Boss战阶段转换
     - 描述Boss的变化
     - "巨魔发出愤怒的咆哮，伤口开始急速愈合！"

  4. 角色倒地
     - 生成倒地描述
     - "法师被毒镖击中，踉跄倒地..."

  5. 战斗胜利
     - 生成胜利描述
     - "随着最后一只哥布林倒下，洞穴恢复了寂静。"

  6. 战斗失败
     - 生成失败描述
     - "黑暗吞噬了冒险者的意识..."
```

### 14.8 撤退机制 `[CUSTOM]`

```
撤退 (Retreat/Flee) 设计哲学:
  - 在永久死亡游戏中，撤退必须是可行选项
  - 撤退应有代价但不致命（保留"战略性撤退"的空间）
  - 撤退流程必须简单快速，不打断战斗节奏
```

#### 14.8.1 撤退投票

```
撤退发起条件:
  - 任何玩家角色可在自己回合选择"提议撤退"
  - 提议撤退消耗该角色的主动作
  - 提议后进入投票阶段（不中断当前回合）

投票规则:
  - 所有存活玩家角色参与投票
  - 全员同意 → 立即进入撤退检定
  - 多数同意 + 领导者检定成功 → 进入撤退检定
    · 领导者检定: d20 + CHA(Leadership) vs DC 12
    · 任一角色可担任提议者
  - 未通过 → 战斗继续，该回合已消耗的主动作不返还

投票UI:
  ┌─────────────────────────────────────────┐
  │  🏳️ 撤退提议                             │
  │                                         │
  │  战士提议撤退！                          │
  │                                         │
  │  [同意撤退]  [继续战斗]                  │
  │                                         │
  │  队伍状态:                               │
  │  战士: HP 12/28 ⚠️                      │
  │  法师: HP 3/18 💀                       │
  │  盗贼: HP 22/22 ✓                       │
  └─────────────────────────────────────────┘
```

#### 14.8.2 撤退检定

```
撤退检定公式:
  撤退检定 = d20 + 队伍最高DEX调整值 + 环境修正

  环境修正:
    - 每个存活敌人: -1（敌人越多越难逃脱）
    - 有明确出口: +2（地图有标记的出口）
    - 困难地形: -2（撤退路径有困难地形）
    - 已消灭半数以上敌人: +3（敌人士气低落）
    - Boss战: -5（Boss不会放走猎物）

  DC基准:
    ┌────────────────────────────────────────────┐
    │  遭遇类型        │  基础DC  │  说明         │
    ├──────────────────┼──────────┼───────────────┤
    │  普通遭遇        │  10      │  标准难度     │
    │  精英遭遇        │  13      │  敌人更执着   │
    │  Boss战          │  18      │  极难逃脱     │
    │  伏击/陷阱       │  15      │  被包围       │
    └──────────────────┴──────────┴───────────────┘

  最终DC = 基础DC + 环境修正（最低DC 5，最高DC 25）

  示例:
    普通遭遇(3哥布林), 已消灭1个, 有出口:
      DC = 10 + (-2存活敌人) + (+2出口) + (+3已消灭半数) = 13
      检定: d20 + DEX_mod(最高) vs DC 13

    Boss战(巨魔), 无出口:
      DC = 18 + (-1存活敌人) + (-5 Boss) = 24
      检定: d20 + DEX_mod vs DC 24（几乎不可能）
```

#### 14.8.3 撤退结果

```
撤退成功:
  - 所有玩家角色脱离战斗
  - 进入撤退惩罚（详见§14.5.2撤退惩罚表）
  - 返回冒险地图（当前节点标记为"已探索"）
  - 未拾取的战利品丢失

撤退失败:
  - 所有玩家角色受到一次借机攻击（来自最近的敌人）
  - 借机攻击使用敌人的标准攻击检定
  - 战斗继续（不消耗额外回合）
  - 该轮不能再提议撤退
```

#### 14.8.4 撤退限制

```
不可撤退的情况:
  - Boss战中Boss HP > 50%: DC极高(18+)，但技术上仍可尝试
  - 被包围（所有出口被敌人占据）: DC+5
  - 特殊剧情战斗: 标记为"不可撤退"的遭遇

撤退冷却:
  - 撤退失败后，该遭遇不能再提议撤退
  - 撤退成功后，重新进入该节点会触发新遭遇（可能更难）

AI敌人对撤退的反应:
  - 普通敌人: 不追击（撤退成功）
  - 精英敌人: 50%概率追击（额外1轮战斗）
  - Boss: 必定追击（但撤退检定已考虑此因素）
```

#### 14.8.5 撤退与FSM集成

```
撤退在FSM中的位置:

  ROUND_END状态:
    - 检查胜利/失败条件之前
    - 检查是否有撤退投票通过
    - 如果通过 → 进入RETREAT_CHECK状态

  RETREAT_CHECK状态:
    - 执行撤退检定
    - 成功 → FLEE状态
    - 失败 → 借机攻击 → 回到ROUND_START

  FLEE状态:
    - 应用撤退惩罚
    - 触发LLM撤退叙述
    - 返回冒险地图
```

---

## 15. 测试规格

### 15.1 单元测试

#### Test Suite: 攻击检定

```
TEST 1: 基础攻击检定
  Given: Fighter Lv3, STR 16(+3), PB=2, 长剑
  Attack bonus = 3 + 2 = +5
  vs AC 14: 需要 d20 ≥ 9 → 60%命中率

TEST 2: 暴击判定
  Given: d20 = 20
  Expected: 必定命中, 暴击

TEST 3: 大失败判定
  Given: d20 = 1
  Expected: 必定未命中

TEST 4: 优势骰
  Given: 优势状态
  Expected: 骰2次d20, 取较高值

TEST 5: 劣势骰
  Given: 劣势状态
  Expected: 骰2次d20, 取较低值

TEST 6: 优势劣势互斥
  Given: 2源优势 + 1源劣势
  Expected: 平骰(1次d20)

TEST 7: 掩体AC加成
  Given: 目标有半掩体
  Expected: AC +2

TEST 8: Finesse武器
  Given: Rogue, DEX 18(+4), STR 8(-1), 细剑
  Expected: 使用DEX (+4) 而非STR (-1)
```

#### Test Suite: 伤害计算

```
TEST 9: 基础武器伤害
  Given: 长剑(1d8), STR 16(+3)
  Expected: roll(1d8) + 3

TEST 10: 暴击伤害最大化
  Given: 长剑(1d8), STR 16(+3), 暴击
  Expected: 8(max) + 3 = 11

TEST 11: 附魔伤害
  Given: 炽焰长剑(1d8 + 1d6 fire), STR 16(+3)
  Expected: roll(1d8) + 3 + roll(1d6)

TEST 12: 偷袭伤害
  Given: Rogue Lv5, 细剑(1d8), 偷袭3d6
  Expected: roll(1d8) + 4 + roll(3d6)

TEST 13: 抗性减半
  Given: 15点穿刺伤害, 目标有穿刺抗性
  Expected: 7点伤害 (15/2=7.5, 向下取整)

TEST 14: 免疫降零
  Given: 20点毒素伤害, 目标有毒素免疫
  Expected: 0点伤害

TEST 15: 易伤翻倍
  Given: 12点寒冷伤害, 目标有寒冷易伤
  Expected: 24点伤害

TEST 16: 临时HP吸收
  Given: HP 20/25, 临时HP 8, 受到12点伤害
  Expected: 临时HP 0, HP 16/25

TEST 17: 副手伤害无属性加值
  Given: 副手短剑(1d6), DEX 16(+3), 无双持战斗风格
  Expected: roll(1d6) + 0 (不加DEX)
```

#### Test Suite: 法术系统

```
TEST 18: 法术DC计算
  Given: Wizard Lv5, INT 16(+3), PB=3
  Expected: 8 + 3 + 3 = 14

TEST 19: 法术攻击加值
  Given: Wizard Lv5, INT 16(+3), PB=3
  Expected: 3 + 3 = +6

TEST 20: 法术位消耗
  Given: Wizard有4个1环位, 施放Magic Missile
  Expected: 1环位剩余3

TEST 21: 升环施放
  Given: Magic Missile用2环施放
  Expected: 4颗飞弹 (基础3 + 升环1)

TEST 22: 短休恢复1环位
  Given: Wizard Lv5, 1环=1/4, 2环=0/3, 3环=0/2
  短休后: 1环=4/4, 2环=0/3, 3环=0/2

TEST 23: 专注检定
  Given: 专注中, 受到15点伤害
  DC = max(10, 15/2) = 10
  CON 14(+2): d20+2 vs 10 → 需要d20≥8

TEST 24: 专注替换
  Given: 正在专注Haste, 施放新的专注法术Slow
  Expected: Haste自动结束, 开始专注Slow
```

#### Test Suite: 豁免系统

```
TEST 25: 基础豁免
  Given: Fighter, STR 16(+3), STR豁免熟练, PB=2
  Expected: d20 + 3 + 2 = d20 + 5

TEST 26: 非熟练豁免
  Given: Fighter, INT 8(-1), INT豁免不熟练
  Expected: d20 + (-1) = d20 - 1

TEST 27: 死亡豁免计数
  Given: 角色HP=0, 3轮无治疗
  Expected: 第3轮结束时死亡

TEST 28: 死亡豁免治疗重置
  Given: 角色HP=0, 已2轮无治疗, 受到治疗
  Expected: HP恢复, rounds_without_healing重置为0

TEST 29: 0HP额外伤害（双轨死亡 — 次要机制）
  Given: 角色HP=0, 受到任何伤害
  Expected: death_failures += 2
  death_failures >= 3 → 永久死亡
  （注：不再是"0HP受伤=直接死亡"，改为累积失败制，详见§8.3）
```

#### Test Suite: 条件系统

```
TEST 30: 同名条件不堆叠
  Apply: Poisoned (3轮) + Poisoned (5轮)
  Expected: 1个Poisoned, duration=5

TEST 31: 不同条件共存
  Apply: Blinded + Prone + Poisoned
  Expected: 3个独立条件同时生效

TEST 32: 条件结束
  Given: Poisoned, duration=2轮
  2轮后: 条件自动移除

TEST 33: 豁免结束条件
  Given: Poisoned, 每轮结束CON豁免DC13
  豁免成功: 条件移除
```

### 15.2 集成测试

```
TEST 34: 完整战斗轮模拟
  Given: 2玩家 vs 3哥布林
  流程:
    1. 初始化战斗
    2. 骰先攻
    3. 按顺序执行每个角色的回合
    4. 验证HP变化、条件应用、伤害计算
    5. 验证胜利/失败条件

TEST 35: 顺序回合制验证
  Given: 3玩家，按先攻顺序为盗贼(18)→法师(15)→战士(11)
  流程:
    1. 骰先攻确定固定顺序
    2. 验证盗贼先行动，然后法师，最后战士
    3. 验证顺序整场战斗不变

TEST 36: 分段移动验证
  Given: 战士速度30尺
  流程:
    1. MOVEMENT_PHASE_1: 移动10尺
    2. ACTION_PHASE: 攻击
    3. MOVEMENT_PHASE_2: 移动20尺
    4. 验证总移动=30尺（不超过速度值）

TEST 37: Boss战完整流程
  Given: 3玩家 vs 巨魔Boss
  流程:
    1. 验证巨魔再生特性
    2. 验证火焰/酸蚀抑制再生
    3. 验证Boss阶段转换
    4. 验证战利品生成
```

### 15.3 边界情况测试

```
TEST 38: 死亡豁免时机
  - 角色在轮开始时HP=0 → 检查死亡计数
  - 角色在轮中间HP降至0 → 不立即计数，下轮开始计数

TEST 39: 专注中断时机
  - 专注法术施放时受到伤害 → 专注检定
  - 专注法术持续中受到伤害 → 专注检定
  - 施法者失能 → 专注自动中断

TEST 40: 反应时机
  - 借机攻击: 敌人离开触及范围时
  - Shield: 被攻击命中时
  - Counterspell: 敌人施法时

TEST 41: 优势劣势取消
  - 3源优势 + 1源劣势 = 平骰
  - 1源优势 + 1源劣势 = 平骰
  - 0源优势 + 2源劣势 = 劣势

TEST 42: 掩体叠加
  - 半掩体 + 半掩体 = 3/4掩体? (否, 取最高)
  - 3/4掩体 + 半掩体 = 3/4掩体

TEST 43: 临时HP不叠加
  - 已有5临时HP, 获得8临时HP → 8临时HP (取高)
  - 已有8临时HP, 获得5临时HP → 8临时HP (保持)

TEST 43.5: 撤退投票
  - 全员同意→撤退检定触发
  - 多数+领导检定成功(d20+CHA_mod vs DC 12)→撤退检定触发
  - 少数同意→战斗继续

TEST 43.6: 撤退检定DC计算
  - 普通遭遇(3哥布林), 已消灭1个, 有出口:
    DC = 10 + (-2存活) + (+2出口) + (+3消灭半数) = 13
  - Boss战(巨魔), 无出口:
    DC = 18 + (-1存活) + (-5Boss) = 24
  - 被包围(无出口):
    DC = 10 + (-3存活×3) + (-5无出口) + 0 = 闭锁（无法撤退）

TEST 43.7: 撤退成功惩罚
  - 全队疲乏+1级
  - 金币损失10-20%
  - 1件随机装备退化25%
  - 当前节点标记"已撤退"

TEST 43.8: 撤退失败惩罚
  - 全员承受一次借机攻击（来自最近敌人）
  - 战斗继续
  - 该遭遇不能再提议撤退
```

### 15.4 平衡验证

```
TEST 44: DPR (每轮伤害) 计算
  Lv1-5各职业的预期DPR:

  Lv1 Fighter (STR 16, 长剑):
    命中率: 60% (d20+5 vs AC 14)
    DPR = 0.6 × (4.5+3) = 4.5

  Lv5 Fighter (STR 18, 长剑, Extra Attack):
    命中率: 65% (d20+7 vs AC 14)
    DPR = 2 × 0.65 × (4.5+4) = 11.05

  Lv5 Rogue (DEX 18, 细剑, 偷袭3d6):
    命中率: 65% (d20+7 vs AC 14)
    DPR = 0.65 × (4.5+4+10.5) = 12.35

  Lv5 Wizard (Fire Bolt):
    命中率: 60% (d20+6 vs AC 14)
    DPR = 0.6 × 5.5 = 3.3 (戏法)
    Fireball: 8d6 = 28平均 (1次/战斗)

TEST 45: 遭遇CR预算
  4人Lv3队伍:
    Easy: CR 1-2
    Medium: CR 3-4
    Hard: CR 5-6
    Deadly: CR 7+

  验证: 使用DMG encounter building guidelines
```

---

## 附录A: 状态机转换图（完整）

```
┌─────────────────────────────────────────────────────────────────────┐
│                        COMBAT FSM (完整)                             │
│                                                                       │
│  ┌─────────────────┐                                                 │
│  │ INITIALIZATION  │                                                 │
│  └────────┬────────┘                                                 │
│           │ all_loaded                                                │
│           ▼                                                           │
│  ┌─────────────────┐                                                 │
│  │ ROLL_INITIATIVE │ (战斗开始时仅一次)                               │
│  └────────┬────────┘                                                 │
│           │ initiative_complete                                       │
│           ▼                                                           │
│  ┌─────────────────┐ ◄───────────────────────────────────┐          │
│  │  ROUND_START    │                                      │          │
│  └────────┬────────┘                                      │          │
│           │ effects_resolved                              │          │
│           ▼                                               │          │
│  ┌─────────────────┐    can_act=false                     │          │
│  │   TURN_START    │──────────────────┐                   │          │
│  └────────┬────────┘                  │                   │          │
│           │ can_act=true              ▼                   │          │
│           ▼                  ┌─────────────────┐          │          │
│  ┌─────────────────┐        │    TURN_END     │          │          │
│  │MOVEMENT_PHASE_1 │        └────────┬────────┘          │          │
│  │    (可选)       │                 │                   │          │
│  └────────┬────────┘                 │                   │          │
│           │ move1_done               │                   │          │
│           ▼                          │                   │          │
│  ┌─────────────────┐                 │                   │          │
│  │  ACTION_PHASE   │                 │                   │          │
│  └────────┬────────┘                 │                   │          │
│           │ action_done              │                   │          │
│           ▼                          │                   │          │
│  ┌─────────────────┐                 │                   │          │
│  │BONUS_ACTION_PHASE│                │                   │          │
│  │    (可选)       │                 │                   │          │
│  └────────┬────────┘                 │                   │          │
│           │ bonus_done               │                   │          │
│           ▼                          │                   │          │
│  ┌─────────────────┐                 │                   │          │
│  │MOVEMENT_PHASE_2 │                 │                   │          │
│  │    (可选)       │                 │                   │          │
│  └────────┬────────┘                 │                   │          │
│           │ move2_done               │                   │          │
│           ▼                          │                   │          │
│  ┌─────────────────┐                 │                   │          │
│  │    TURN_END     │─────────────────┘                   │          │
│  └────────┬────────┘  has_next                           │          │
│           │ no_next                                      │          │
│           ▼                                              │          │
│  ┌─────────────────┐                                     │          │
│  │   ROUND_END     │                                     │          │
│  └────────┬────────┘                                     │          │
│           │                                              │          │
│     ┌─────┼─────┬─────────┬───────────┐                  │          │
│     ▼     ▼     ▼         ▼           ▼                  │          │
│  ┌─────┐┌─────┐┌─────┐┌──────────┐┌──────┐              │          │
│  │VICTORY│DEFEAT│NEXT ││  RETREAT ││ FLEE │              │          │
│  └─────┘└─────┘│ROUND││  _CHECK  │└──────┘              │          │
│                 └──┬──┘└────┬─────┘    ▲                  │          │
│                    │        │ 失败     │ 成功             │          │
│                    │        │ 借机攻击  │                  │          │
│                    │        └──────┐   │                  │          │
│                    │               ▼   │                  │          │
│                    │        ┌──────────┐                  │          │
│                    │        │借机攻击后 │                  │          │
│                    │        │回ROUND_  │                  │          │
│                    │        │  START ◄─┘                  │          │
│                    │        └──────────┘                  │          │
│                    └────────────┬─────────────────────────┘          │
│                                 │                                    │
│                                 │                                    │
│  REACTION 中断（任意回合内）:                                        │
│    当前回合进行中 ──触发条件满足──→ 暂停→执行反应→恢复原回合         │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 附录B: 常用公式速查表

| 公式 | 表达式 | 示例 |
|------|--------|------|
| 属性调整值 | floor((score - 10) / 2) | STR 16 → +3 |
| 熟练加值 | floor((level - 1) / 4) + 2 | Lv5 → +3 |
| AC (无护甲) | 10 + DEX_mod | DEX 14 → 12 |
| AC (轻甲) | armor_base + DEX_mod | 皮甲+DEX16 → 14 |
| AC (中甲) | armor_base + min(DEX_mod, 2) | 鳞甲+DEX16 → 16 |
| AC (重甲) | armor_base | 链甲 → 16 |
| 攻击检定 | d20 + ability_mod + prof + other | d20+5 |
| 法术DC | 8 + prof + spell_ability_mod | 8+3+3=14 |
| 法术攻击 | prof + spell_ability_mod | 3+3=+6 |
| 先攻 | d20 + DEX_mod + other | d20+2 |
| 死亡豁免DC | max(10, damage/2) | 15伤害→DC10 |
| 偷袭骰数 | ceil(rogue_level / 2) | Lv5→3d6 |

---

## 附录C: 参考文献

| 来源 | 用途 |
|------|------|
| DND 5e SRD | 基础规则参考 |
| GDD-v1.0 | 游戏设计约束 |
| character-system.md | 角色数据模型 |
| items-equipment.md | 物品装备数据 |

---

## 16. 依赖关系 (Dependencies)

### 16.1 上游依赖（本系统依赖）

| 依赖系统 | 依赖内容 | 状态 | 风险 |
|----------|----------|:----:|:----:|
| **角色系统** | HP/AC/属性调整值/法术位/豁免熟练/技能加值 | ✅ 已审查 | 低 |
| **物品装备系统** | 武器伤害骰/护甲AC/附魔效果/耐久度 | ✅ 已审查 | 中 — 需集成耐久度退化事件 |
| **冒险生成系统** | 遭遇CR/敌人配置/地形数据 | ✅ 已设计 | 低 |
| **LLM集成网关** | 战斗叙述生成 | ✅ 已审查 | 低 |
| **酒馆系统** | 短休/长休触发 | ✅ 已设计 | 低 |
| **失败与成长系统** | XP计算/失败惩罚/撤退机制 | ✅ 已审查 | 中 — 需对齐XP公式 |

### 16.2 下游依赖（依赖本系统的系统）

| 依赖系统 | 依赖内容 | GDD状态 |
|----------|----------|:-------:|
| **冒险生成系统** | 战斗结果影响世界状态 | ✅ |
| **失败与成长系统** | 角色死亡/伤疤生成 | ✅ |
| **LLM集成网关** | 战斗日志作为叙事输入 | ✅ |
| **酒馆系统** | 短休/长休资源恢复 | ✅ |

---

## 17. 可调参数 (Tuning Knobs)

| 参数 | 当前值 | 安全范围 | 影响面 |
|------|:------:|:--------:|--------|
| **先攻重骰** | 每场战斗1次 | 每轮/每场 | 战术可预测性 |
| **暴击规则** | 伤害骰最大值 | 最大值/双骰 | 暴击爽感 |
| **死亡豁免轮数** | 3轮 | 2-4轮 | 死亡紧迫感 |
| **0HP+伤害** | 2次死亡失败 | 1-3次 | 死亡残酷度 |
| **短休1环恢复** | 一半(向上取整) | 全部/一半/无 | 施法者资源优势 |
| **Arcane Recovery** | ceil(Lv/2)环位 | 保持/减半 | 法师短休收益 |
| **Hit Dice恢复** | 长休恢复一半 | 保持/全部 | 长冒险资源压力 |
| **撤退DC** | 基于情况 | 10-20 | 撤退可行性 |
| **Boss传奇动作** | 无 | 0-3次/轮 | Boss战难度 |
| **AI难度默认** | Normal | Easy-Hard | 敌人战术水平 |
| **同时选择倒计时** | 已移除 | N/A | N/A |
| **每轮重骰先攻** | 已移除 | N/A | N/A |

---

## 18. 验收标准 (Acceptance Criteria)

| # | 验收标准 | 测试方法 | 通过条件 |
|---|----------|----------|----------|
| AC-1 | FSM状态转换正确 | 单元测试 | 12个状态+17条守卫全部通过 |
| AC-2 | 顺序回合制正常工作 | 集成测试 | 按先攻顺序依次执行 |
| AC-3 | 反应中断机制正确 | 集成测试 | 借机攻击/Shield/Counterspell在正确时机触发 |
| AC-4 | 分段移动支持 | 单元测试 | move→action→move流程正确 |
| AC-5 | 攻击检定管线完整 | 单元测试 | 优势/劣势/掩体/finesse全部正确 |
| AC-6 | 伤害计算管线完整 | 单元测试 | 抗性/免疫/易伤/临时HP全部正确 |
| AC-7 | 法术位消耗/恢复正确 | 单元测试 | 短休恢复一半1环，长休全恢复 |
| AC-8 | 死亡豁免机制正确 | 单元测试 | 3轮无治疗=死亡，0HP+伤害=2次失败 |
| AC-9 | 撤退机制正常工作 | 集成测试 | 检定成功/失败正确处理 |
| AC-10 | 条件系统14种条件全部正确 | 单元测试 | 每种条件的机械效果验证 |
| AC-11 | AI目标选择正确 | 单元测试 | 评分公式输出合理 |
| AC-12 | Boss阶段转换正确 | 集成测试 | HP阈值触发行为变化 |

---

*文档版本: v1.1*
*创建日期: 2026-05-04*
*最后更新: 2026-05-09*
*状态: 设计评审修订完成，待复审*
