# 条件效果系统 — 技术设计文档

> **Status**: In Review (v1.1 — 8阻断已修复)
> **Author**: Sisyphus + user
> **Last Updated**: 2026-05-10
> **Implements Pillar**: P2 (战术深度——原汁原味DND)
> **ADR Ref**: ADR-0006 (ConditionTracker 子模块 → 独立系统)
> **Review**: `/design-review` 2026-05-10 — NEEDS REVISION → 修订完成，8 阻断 + 6 建议已修复

## Overview

条件效果系统是 DND 5e 14种标准状态条件（目盲/魅惑/恐慌/麻痹/中毒等）及 3 级疲乏的统一管理引擎。它定义了条件的施加、持续追踪、同名/异名堆叠、交互优先级和到期自动移除逻辑，同时驱动玩家可见的状态图标与角色精灵视觉反馈（变色/动画/粒子特效）。

作为 character-system 和 combat-system 之间共享的核心服务，条件效果系统为战术决策的多样性提供基础——**没有条件系统，DND 战斗将退化为纯数值对撞**（违背 Pillar 2）。系统通过 IEventBus 发布 `condition_applied` / `condition_removed` / `condition_expired` 事件，角色系统、战斗系统、UI 系统和敌人 AI 通过订阅这些事件响应条件变化。

本系统从 ADR-0006 中 CombatEngine 内部 `ConditionTracker` 子模块**升级为独立系统**，以解决 character-system 和 combat-system 中条件定义的重复问题（参见 `/review-all-gdds` 2026-05-09 裁决 D1）。升级后，本 GDD 成为条件的**单一数据权威源**——character-system §2.8 和 combat-system §9 改为引用本系统。

MVP 阶段实现 8 种核心条件（昏迷/失能/目盲/魅惑/恐慌/麻痹/中毒/倒地），其余 6 种在 Phase 2 增量添加。

## Player Fantasy

> **"我操控战场节奏"** — 玩家不是通过蛮力碾压取胜，而是通过精准的状态控制瘫痪关键敌人、保护濒死队友、在绝境中拆解威胁。每一轮，玩家扫描敌方和己方的条件面板——"Boss 被恐慌了，这轮攻击劣势""我的法师被麻痹了，必须在他被暴击前解除"——并做出改变了战斗走向的决策。
>
> 条件的施加与解除像是战场上的"开关"：目盲关掉了敌人的攻击窗口，中毒关掉了队友的安全边际，倒地关掉了 Boss 的移动自由。掌握了这些开关的人，才是真正的战术大师。
>
> 在 SFC 像素画面上，每个条件都是一个可读的视觉信号——昏迷的角色倒地不动，恐慌的角色身上冒出冷汗粒子，目盲的敌人眼前蒙上黑雾——让玩家在一瞥之间理解战场状态，做出亚秒级的战术判断。

**服务于 Pillar 2（战术深度——原汁原味DND）**：完整保留 DND 5e 的 14 种条件，确保"战斗决策有真实后果"的体验通过条件系统得到具体化——没有条件系统，DND 战斗只剩下数值对撞。

## Detailed Design

### Core Rules

#### 条件施加与生命周期

```
施加条件流程:
  1. 来源系统发出施加请求（法术/攻击/环境/能力）
  2. 检查目标是否对该条件免疫 → 是则拒绝（记录战斗日志："[目标] 免疫 [条件]！"）
  3. 检查目标是否已存在同名条件 → 是则刷新持续时间（始终取较长者），来源更新为最新施加者
  4. 写入条件数据（类型、来源ID、剩余回合数、dc、save_ability）
  5. 应用即时机械效果
  6. 发布 ConditionApplied 事件
  7. UI/视觉系统响应

条件到期流程:
  1. 每回合结束 (OnTurnEnd) 检查该目标所有活跃条件
  2. remaining_rounds>0: 递减; remaining_rounds=0: 跳过（永久条件）
  3. remaining_rounds 递减后归零 → 发布 ConditionExpired 事件
  4. 移除条件 + 逆转机械效果
  5. 若条件有 dc 且 save_ability: 执行豁免检定，成功则到期（见 §Formulas）

条件移除流程:
  1. 来源系统发出移除请求（法术驱散/药水/长休）
  2. 发布 ConditionRemoved 事件
  3. 移除条件 + 逆转机械效果
```

#### 条件堆叠规则

| # | 规则 | 说明 |
|:--:|------|------|
| 1 | **同名取长** | 两次施加同名条件→持续时间为较长者（无论来源是否相同）。来源 ID 更新为最近施加者。 |
| 2 | **异名共存** | 角色可同时目盲+倒地+中毒 |
| 3 | **优劣势互斥** | 攻击者任意数量优势 + 任意数量劣势 = 平骰（见 §Formulas 优劣势合并判定） |
| 4 | **免疫优先** | 免疫某条件→拒绝所有施加尝试，记录战斗日志："[目标] 免疫 [条件]！"，返回 false |
| 5 | **传递关系** | Paralyzed/Stunned/Unconscious 自动包含 Incapacitated。Unconscious 自动包含 Prone。详见 §传递条件 |
| 6 | **永久条件 (duration=0)** | 不会被每回合 TickRound 递减，仅通过主动移除结束

#### MVP 条件（8种）

| 条件ID | 名称 | 机械效果 | 典型来源 | MVP |
|:---:|------|----------|----------|:---:|
| Blinded | 目盲 | 攻击劣势；被攻击优势；视觉技能自动失败 | Blindness 法术 | ✅ |
| Charmed | 魅惑 | 不能攻击魅惑者；魅惑者社交优势 | Charm Person | ✅ |
| Deafened | 耳聋 | 听觉技能自动失败 | — | Phase 2 |
| Frightened | 恐慌 | 恐慌源在视线内时攻击/技能劣势；不能主动靠近 | 龙威、Cause Fear | ✅ |
| Grappled | 擒抱 | 速度=0；擒抱者失能时结束 | — | Phase 2 |
| Incapacitated | 失能 | 不能执行动作或反应 | 被包含于其他条件 | ✅ |
| Invisible | 隐形 | 重度遮蔽；攻击优势；对其攻击劣势 | — | Phase 2 |
| Paralyzed | 麻痹 | 失能+不能移动/说话；STR/DEX豁免自动失败；5尺内攻击自动暴击 | Hold Person | ✅ |
| Petrified | 石化 | 麻痹+重量×10+免疫伤害+停止老化 | — | Phase 2 |
| Poisoned | 中毒 | 攻击和技能检定劣势 | 毒药、毒素攻击 | ✅ |
| Prone | 倒地 | 只能爬行移动；攻击劣势；5尺内对其攻击优势；站立消耗半速 | 推倒、Trip Attack | ✅ |
| Restrained | 束缚 | 速度=0；攻击劣势；对其攻击优势；DEX豁免劣势 | — | Phase 2 |
| Stunned | 震慑 | 失能+不能移动；STR/DEX豁免自动失败；对其攻击优势 | — | Phase 2 |
| Unconscious | 昏迷 | 失能+倒地+不能感知；STR/DEX豁免自动失败；5尺内攻击自动暴击 | HP降至0 | ✅ |

#### 传递条件 (Transitive Conditions)

DND 5e 中，部分条件自动包含其他条件的效果。本系统采用**顶层存储 + 查询时动态计算**的策略：

**存储规则**:
- 仅存储顶层条件（如 Paralyzed）到角色条件列表
- 不重复存储其传递包含的子条件（如 Incapacitated）

**查询规则**:
- `HasCondition(target, Incapacitated)` → 检查目标是否拥有 Incapacitated **或** 任何传递包含 Incapacitated 的顶层条件（Paralyzed/Stunned/Unconscious）
- `GetEffectiveConditions(target)` → 返回所有顶层条件 + 其传递包含子条件的完整展开列表

**移除规则**:
- 移除顶层条件（如 Paralyzed）→ 其传递子条件（Incapacitated）自动随之移除，**除非**子条件同时被另一个顶层条件传递包含（如角色同时有 Paralyzed 和 Stunned，移除 Paralyzed 后 Incapacitated 仍由 Stunned 维持）
- 独立来源的子条件：若 Prone 既被 Unconscious 传递包含，又被独立的 Trip Attack 施加，移除 Unconscious 后 Prone 保留（来自 Trip Attack 的独立来源）

**事件规则**:
- 仅顶层条件发布 `condition_applied` 和 `condition_removed` 事件
- 事件 payload 中标注 `implicit_conditions` 列表——下游系统（UI/AI）可据此获知隐含条件
- 隐含条件不独立发布事件

**传递关系表**:

| 顶层条件 | 自动包含 | 备注 |
|----------|---------|------|
| Paralyzed | Incapacitated | 目标不能行动但 Incapacitated 不独立显示 |
| Stunned (Phase 2) | Incapacitated | 同上 |
| Unconscious | Incapacitated + Prone | HP=0 时自动施加；治愈 HP>0 后自动移除二者 |

**Frightened 视线判定** `[S4]`:
- 每回合开始时检查：恐慌源是否在目标视线内（非隐形、非完全掩体、在感知范围内）
- 恐慌源死亡 → Frightened 的"不能主动靠近"限制解除，"攻击/技能劣势"保留直到条件到期或移除
- 恐慌源离开视线（隐形/掩体）→"攻击/技能劣势"暂时解除（源不可见时无法触发），"不能靠近"保留

**同时施加排序** `[S5]`:
- 在同时选择→按先攻结算的系统中，两个来源同时声明施加同名条件时，按先攻顺序决定"先到达"的请求
- 若完全同时（同先攻值），按自然顺序（1→N）排序

#### 疲乏系统（3级）

| 等级 | 效果 | 获取途径 | 恢复方式 |
|:---:|------|----------|----------|
| Lv1 | 属性检定劣势 | 战斗败退/特殊效果 | 长休-1级（归零） |
| Lv2 | 速度减半 + 攻击劣势 | 叠加 | 长休-1级 |
| Lv3 | 豁免劣势 + HP最大值减半 | 叠加 | 长休-1级 |

> **设计原则**: 疲乏 Lv3 为上限——不再累加。死亡仅在疲乏等级从 Lv2 升至 Lv3 时触发（见 §Formulas 疲乏死亡判定），而非每次获得疲乏都死。Lv3 角色再获得疲乏→忽略（已封顶）。与标准 5e 的区别：简化为 3 级（标准 5e 为 6 级），采用渐进曲线而非悬崖曲线。

### States and Transitions

每个条件有 3 种状态：

```
                 ┌─────────────────────┐
    施加请求      │     INACTIVE        │
  ──────────────▶│  (条件未激活)        │◀──────────────
                 └────────┬────────────┘               │
                          │ 免疫检查通过               │ 持续时间归零
                          │ 写入条件数据               │ 或主动移除
                          ▼                            │
                 ┌─────────────────────┐               │
                 │     ACTIVE          │───────────────┘
                 │  (条件生效中)        │
                 └────────┬────────────┘
                           │ 持续时间=0（永久条件）
                           │ 持续时间>0（回合递减）
                           │ 同名再施加→取较长者
                          ▼
                 ┌─────────────────────┐
                 │   EXPIRING          │
                 │  (到期中: 持续=0)    │
                 └────────┬────────────┘
                          │ 发布 ConditionExpired
                          ▼
                    ┌──────────┐
                    │  REMOVED │
                    └──────────┘
```

### Interactions with Other Systems

**上游（本系统依赖）**:

| 系统 | 交互 | 性质 |
|------|------|:--:|
| 角色系统 (01) | 读取角色属性（HP/豁免/免疫列表）；写入条件效果 | 硬依赖 |
| 事件总线 | 发布 ConditionApplied/Removed/Expired 事件 | 硬依赖 |

**下游（依赖本系统）**:

| 系统 | 交互 | 性质 |
|------|------|:--:|
| 战斗系统 (04) | 查询条件状态以计算攻防骰修正、行动限制 | 硬依赖 |
| 敌人AI (11) | 查询目标条件以决策（优先攻击被麻痹的目标） | 硬依赖 |
| UI系统 (09) | 订阅条件事件以渲染状态图标/粒子/动画 | 软依赖 |
| 冒险生成 (06) | 读取条件状态生成难度适配 | 软依赖 |
| 失败与成长 (08) | 读取HP=0后读取昏迷/死亡条件状态 | 软依赖 |

## System Interface `[C# API]`

条件效果系统通过 `IConditionSystem` 接口暴露服务，注册于 `ServiceLocator`（初始化顺序 #4，在 IGameStateManager 之后、ILLMGateway 之前）。

```csharp
/// <summary>
/// 条件效果系统——DND 5e 14种条件的统一管理引擎。
/// 通过 IEventBus 发布 condition_applied/condition_removed/condition_expired 事件。
/// </summary>
public interface IConditionSystem
{
    // ═══ 施加与移除 ═══

    /// <summary>尝试对目标施加条件。返回施加结果（成功/免疫/拒绝）。</summary>
    ConditionApplyResult ApplyCondition(
        string targetId, ConditionType type, string sourceId,
        int durationRounds, int? dc = null, string? saveAbility = null);

    /// <summary>主动移除目标的一个条件。返回是否成功移除。</summary>
    bool RemoveCondition(string targetId, ConditionType type, string reason);

    // ═══ 查询 ═══

    /// <summary>检查目标是否拥有某条件（含传递条件）。</summary>
    bool HasCondition(string targetId, ConditionType type);

    /// <summary>获取目标所有活跃条件（含传递展开后的完整列表）。</summary>
    IReadOnlyList<ConditionInstance> GetEffectiveConditions(string targetId);

    /// <summary>获取目标顶层条件（不含传递展开）。</summary>
    IReadOnlyList<ConditionInstance> GetRootConditions(string targetId);

    // ═══ 战术查询（供战斗系统/AI/UI） ═══

    /// <summary>目标是否可以执行动作和反应？</summary>
    bool CanAct(string targetId);

    /// <summary>目标是否可以移动？</summary>
    bool CanMove(string targetId);

    /// <summary>获取攻击者对目标的攻击优劣势状态。</summary>
    AdvantageState GetAttackAdvantage(string attackerId, string targetId);

    /// <summary>是否对目标自动暴击（5尺内麻痹/昏迷）。</summary>
    bool IsAutoCrit(string attackerId, string targetId, int distanceFeet);

    /// <summary>获取目标的豁免检定修正状态。</summary>
    SavingThrowState GetSavingThrowState(string targetId, AbilityType ability);

    // ═══ 疲乏 ═══

    /// <summary>获取目标的疲乏等级 (0-3)。</summary>
    int GetExhaustionLevel(string targetId);

    /// <summary>增加目标的疲乏等级（触发死亡判定）。</summary>
    void AddExhaustion(string targetId, string reason);

    /// <summary>移除目标的疲乏等级（长休/复活调用）。</summary>
    void ReduceExhaustion(string targetId, int levels, string reason);

    // ═══ 生命周期 ═══

    /// <summary>目标回合结束时调用——递减条件持续时间，判定到期。</summary>
    void OnTurnEnd(string targetId);

    /// <summary>战斗结束时调用——冻结所有目标条件计时器。</summary>
    void OnCombatEnd();

    /// <summary>战斗开始时调用——恢复所有目标条件计时器。</summary>
    void OnCombatStart();
}

/// <summary>条件施加结果</summary>
public enum ConditionApplyResult
{
    Applied,      // 成功施加
    Refreshed,    // 同名条件已存在，刷新持续时间
    Immune,       // 目标免疫此条件
    Rejected      // 请求被拒绝（duration=0 等无效参数）
}

/// <summary>优劣势状态</summary>
public enum AdvantageState { Normal, Advantage, Disadvantage }

/// <summary>豁免检定修正状态</summary>
public enum SavingThrowState { Normal, AutoFail, AutoSuccess }
```

## Event Payload Structures

所有条件事件通过 `IEventBus` 发布——**不使用** .NET 原生 event/`Action`。事件记录定义如下：

```csharp
/// <summary>条件施加事件负载</summary>
public record ConditionAppliedEvent(
    string TargetId,                // 被施加条件的角色 ID
    ConditionType Type,             // 条件类型
    string SourceId,                // 施加者 ID（法术来源/环境来源）
    int DurationRounds,             // 持续回合数（0=永久）
    int? Dc,                        // 豁免 DC（若有自主到期）
    string? SaveAbility,            // 豁免属性（如 "con"）
    IReadOnlyList<ConditionType> ImplicitConditions // 传递包含的子条件
);

/// <summary>条件移除事件负载</summary>
public record ConditionRemovedEvent(
    string TargetId,
    ConditionType Type,
    string Reason,                  // 移除原因: "expired" / "dispelled" / "healed" / "long_rest"
    IReadOnlyList<ConditionType> RemovedImplicitConditions
);

/// <summary>条件自然到期事件负载</summary>
public record ConditionExpiredEvent(
    string TargetId,
    ConditionType Type,
    int ElapsedRounds               // 已持续的回合数
);
```

## Formulas

### 持续时间追踪

```
每回合递减逻辑 (TickRound):
  if remaining_rounds > 0:
      remaining_rounds -= 1    // 递减 1 回合
  if remaining_rounds == 0:
      // 永久条件——不递减，不受回合数影响
      // 仅通过主动移除（法术驱散/药水/长休/死亡）结束

持续值约定:
  - remaining_rounds = 0 → 永久条件 (∞)，不被动到期，需要主动移除
  - remaining_rounds > 0 → 有限回合，每回合 TickRound 递减直至归零
  - 施加时传入 duration_rounds=0 → 拒绝施加（无效请求），见 Edge Cases §7

重新施加刷新规则 (同名条件):
  existing_remaining = max(existing_remaining, new_duration)
  // 始终取较长者——无论来源是否相同
  // 来源 ID 同步更新为最近施加者
```

### 豁免判定（每回合结束自主到期）

条件施加时存储 `dc` 和 `save_ability` 字段。每回合结束时，若条件支持自主豁免到期：

```
save_success = (d20 + character_save_mod(ability)) >= dc

若成功: 条件到期（触发 ConditionExpired），移除条件
若失败: 条件保持，下回合继续检定
```

> DC 和 save_ability 由来源系统在施加时提供并写入条件数据。条件系统不定义 DC 值——仅执行判定。若施加时未提供 DC，则该条件不支持自主豁免到期。

### 疲乏死亡判定

```
if exhaustion_level >= 3 AND exhaustion_level_increases:
    character_dies()
    // 疲乏 Lv3 为上限——不再累积
    // 死亡仅在疲乏等级从 <3 升至 ≥3 时触发（见疲乏系统）
```

### 优劣势合并判定

条件系统需要为攻击者/防御者分别计算净状态：

```
// 扫描目标的所有活跃条件（含传递条件），计算攻击者视角的优劣势来源
advantage_count = count(条件赋予攻击者优势) 
disadvantage_count = count(条件赋予攻击者劣势)

if advantage_count > 0 AND disadvantage_count > 0:
    result = NORMAL    // 互斥：任意数量优势 + 任意数量劣势 = 平骰
elif advantage_count > 0:
    result = ADVANTAGE
elif disadvantage_count > 0:
    result = DISADVANTAGE
else:
    result = NORMAL
```

## Edge Cases

- **若同名条件被两个来源同时施加**: 取持续时间较长者。若持续时间相同，取先到达的请求（堆叠规则 #1）。
- **若角色被施加与其免疫冲突的条件**: 免疫优先——施加请求被静默拒绝。免疫列表由角色系统管理（种族/职业/魔法物品提供）。
- **若角色 HP>0 但仍处于 Unconscious**: 不可能发生——Unconscious 的移除条件之一为 HP 恢复>0。一旦 HP>0，Unconscious 自动移除。
- **若角色同时处于多种条件（极限复合）**: 全部生效（异名共存）。优劣势按互斥规则合并：多源优势+多源劣势=平骰。示例：攻击者若被恐慌（攻击劣势），同时攻击被 Blinded+Prone(5尺内) 的目标（2优势）→ 2优势+1劣势=平骰。若攻击者无自身劣势，纯攻击优势来源（如3优势0劣势）→ 优势。
- **若疲乏 Lv3 角色受到长休**: 疲乏降至 Lv2，HP 恢复至原始最大值。Lv3 效果（攻击/豁免劣势、HP减半）移除。
- **若战斗结束但角色仍有持续条件**: 战斗结束不清除条件。持续时间在战斗外**冻结**（不再递减），保留剩余回合数直至下一场战斗恢复追踪。永久条件 (duration=0) 同样冻结，仅通过主动移除（神殿/药水/长休）结束。
- **战斗外条件持续规则**: 条件系统订阅 `CombatEnded` 事件——战斗结束时冻结所有角色条件计时器；订阅 `CombatStarted` 事件——战斗开始时恢复计时器。酒馆场景中不允许条件自然到期——条件需通过神殿、药水或长休移除。
- **若角色死亡后通过复活术复活**: 所有死亡前的条件清除。疲乏降至死亡前等级-1（最低0）。
- **若条件持续时间=0 的施加请求**: 视为无效请求，拒绝施加。不发布事件。
- **若条件移除时角色已不存在（死亡/退役）**: 静默跳过，不发布事件。

## Dependencies

### 硬依赖（系统不能没有这些）

| 依赖系统 | 接口 | 说明 |
|----------|------|------|
| 角色系统 (01) | 读取: 属性值、豁免调整值、免疫列表、HP；写入: 条件效果修改的属性/状态 | 条件施加前须检查免疫和豁免；条件效果须修改角色战斗属性 |
| 事件总线 | 发布: `condition_applied` / `condition_removed` / `condition_expired` | 所有条件变更通过事件通知下游系统 |

### 软依赖（增强但非必需）

| 依赖系统 | 接口 | 说明 |
|----------|------|------|
| UI系统 (09) | 订阅条件事件 → 渲染状态图标/粒子/动画 | 无UI仍可玩（纯数据）；有UI则显示视觉反馈 |
| 战斗系统 (04) | 提供回合开始/结束钩子（用于持续时间递减） | 条件在战斗外也追踪，但持续时间递减依赖回合通知 |
| 冒险生成 (06) | 消耗品/药水施加和移除条件 | 非战斗条件下的施加（如饮用解毒剂） |

### 被依赖

| 系统 | 依赖内容 |
|------|----------|
| 战斗系统 (04) | 查询条件→计算攻击骰修正/伤害调整/行动限制 |
| 敌人AI (11) | 查询目标条件→决策优先级（优先攻击麻痹/昏迷目标） |
| UI系统 (09) | 订阅条件事件→渲染状态UI/角色精灵动画 |
| 酒馆系统 (07) | 神殿移除诅咒/疾病条件；药水移除中毒条件 |
| 失败与成长 (08) | 伤疤可能附加永久条件；死亡检查依赖昏迷状态 |

## Tuning Knobs

| 参数 | 默认值 | 安全范围 | 说明 | 影响面 |
|------|:-----:|:-------:|------|--------|
| 默认条件持续回合(标准法术) | 10 | 5-20 | 大多数法术施加条件的基准持续 | 战术节奏 |
| 默认条件持续回合(永久) | 0 | 0 | 中毒等持续到解毒——不被动到期 | 资源压力 |
| 同名条件刷新策略 | 取长 | 取长（锁定） | 无论来源是否相同，始终取较长者 | 条件防御价值 |
| 疲乏每长休消除等级 | 1 | 1-3 | 每次长休消除的疲乏级数 | 难度曲线 |
| 疲乏等级上限 | 3 | 3-6 | Lv3封顶（Lv2→Lv3死亡，Lv3不再累加） | 死亡惩罚 |
| 驱散成功率 | 自动成功 | 自动成功/DC 13 | 法术驱散是否需要豁免 | 驱散法术价值 |
| 死亡时条件清除 | 全部清除 | 全部清除/部分保留 | 复活后是否保留死亡前条件 | 复活成本 |
| MVP条件数 | 8 | 8-14 | MVP阶段实现的条件数量 | 实现范围 |
| 战斗外条件行为 | 冻结 | 冻结/实时追踪 | 战斗结束时持续时间是否继续递减 | 战术节奏/挫败感 |
| 每角色视觉层预算 | 2 | 1-3 | 最多同时呈现的精灵动画视觉层数 | 性能/视觉清晰度 |

> 所有持续时间值由来源系统在施加时指定（如法术数据中定义"Blindness: duration=10"），本系统仅定义默认值和边界行为。

## Visual/Audio Requirements

条件效果是玩家在战斗中**最直接的视觉反馈来源**。每个条件需有独特的 SFC 像素视觉标识：

| 条件 | 视觉反馈 (SFC像素) | 音效 |
|------|-------------------|------|
| Blinded 目盲 | 角色眼部蒙上黑雾像素层（5×5 像素遮罩） | — |
| Charmed 魅惑 | 角色头顶飘浮粉色心形粒子（2×2 px，每帧上移 1px） | 轻快铃声 |
| Frightened 恐慌 | 角色身上冒出蓝色冷汗粒子（3滴），sprite 微颤（±1px 水平抖动） | 低沉嗡声 |
| Incapacitated 失能 | 无独立视觉——由 Paralyzed/Stunned/Unconscious 包含 | — |
| Paralyzed 麻痹 | 角色变成灰色调（去饱和 100%）+ sprite 停止在定格帧 | 碎裂声 |
| Poisoned 中毒 | 角色肤色变绿色调 + 头顶冒绿色气泡粒子（2×2 px） | 气泡声 |
| Prone 倒地 | 角色水平翻转 sprite（躺倒姿态） | 倒地声 |
| Unconscious 昏迷 | 角色闭眼（眼部像素变黑）+ 躺倒 + 头上旋转星星粒子 | — |
| 疲乏 Lv1 | 角色 sprite 饱和度 90% | — |
| 疲乏 Lv2 | 饱和度 70% + sprite 轻微变暗 | — |
| 疲乏 Lv3 | 饱和度 50% + sprite 明显变暗 + 间歇闪烁 | — |

> **美术风格约束**: 所有视觉反馈必须符合 SFC 黄金时代像素艺术规范（16-bit，限制色板）。粒子效果不超过 3 帧动画循环。性能预算: 每角色 ≤ 2 个活跃视觉层。

> **视觉优先级规则** `[S3]`: 当角色同时拥有的活跃条件数超过 2 层视觉预算时，按以下威胁等级排序选择显示的视觉效果：
> 1. Paralyzed / Unconscious（硬控——最高优先级）
> 2. Blinded / Frightened（攻防惩罚）
> 3. Poisoned / Prone（中优先级）
> 4. Charmed（低优先级——不直接造成生命威胁）
> 5. 疲乏（与条件视觉层独立，使用独立的饱和度/闪烁管线）
>
> 低优先级条件仅显示图标（16×16 px），不占用精灵视觉层预算。颜色叠加冲突时，按优先级层级覆盖（高优先级覆盖低优先级），不混合。

## UI Requirements

- **条件图标栏**: 战斗界面角色面板上方水平排列条件图标（16×16 px），每个图标右上角显示剩余持续回合数（4px 白色数字）
- **条件详情 tooltip**: 鼠标悬停/长按显示：条件名称、完整机械效果、来源（如"目盲 — Blindness法术 持续3回合"）
- **疲乏进度条**: 3 段式水平进度条，位于角色面板 HP 区域下方。Lv1=黄色, Lv2=橙色, Lv3=红色
- **战斗日志**: 条件施加/移除时追加一行日志（"哥布林萨满 施加了 目盲 → 战士"）

## Acceptance Criteria

### 条件施加
- **GIVEN** 一个活跃角色，**WHEN** 被施加 Blinded（目盲，来源="blindness_spell", 持续=3），**THEN** 角色攻击骰显示劣势；对该角色的攻击骰显示优势；角色眼部出现黑雾遮罩
- **GIVEN** 一个已有 "Poisoned"（持续=5, 来源="trap"）的角色，**WHEN** 不同来源再次施加 "Poisoned"（持续=3, 来源="snake"），**THEN** 持续时间保持 5（取较长者），来源更新为 "snake"
- **GIVEN** 一个已有 "Poisoned"（持续=3）的角色，**WHEN** 同源再次施加 "Poisoned"（持续=10），**THEN** 持续时间更新为 10（取较长者）
- **GIVEN** 免疫 Poisoned 的角色，**WHEN** 被施加 Poisoned，**THEN** 施加被拒绝，返回 Immune，战斗日志显示 "免疫 Poisoned！"

### 条件到期
- **GIVEN** 角色 char_A 拥有 Poisoned（持续=2），**WHEN** 经过 2 次 OnTurnEnd，**THEN** Poisoned 被移除，发布 ConditionExpired 事件
- **GIVEN** 角色 char_A 拥有 Poisoned（持续=0, 永久），**WHEN** 执行 10 次 OnTurnEnd，**THEN** Poisoned 仍然存在，不发布任何到期事件

### 传递条件
- **GIVEN** 角色 char_A，**WHEN** 被施加 Paralyzed，**THEN** HasCondition(char_A, Incapacitated) 返回 true（由 Paralyzed 传递包含）
- **GIVEN** 角色 char_A 同时拥有 Paralyzed 和 Stunned（Phase 2），**WHEN** Paralyzed 被移除，**THEN** HasCondition(char_A, Incapacitated) 仍为 true（由 Stunned 维持）
- **GIVEN** 角色 Unconscious+Prone（HP=0），**WHEN** 被治疗至 HP>0，**THEN** Unconscious 和 Prone 同时自动移除，角色站立

### 优劣势合并
- **GIVEN** 攻击者被 Frightened（攻击劣势），目标被 Blinded+Prone(5尺内)（攻击者对目标 2 优势），**WHEN** 攻击者攻击目标，**THEN** 攻击骰为平骰（2优势+1劣势互斥）
- **GIVEN** 攻击者攻击 Blinded+Prone(5尺内) 目标（3优势，0劣势），**WHEN** 攻击者攻击目标，**THEN** 攻击骰为优势

### 疲乏
- **GIVEN** 疲乏 Lv2 的角色，**WHEN** 完成长休，**THEN** 疲乏降至 Lv1
- **GIVEN** 疲乏 Lv3 的角色，**WHEN** 再次获得疲乏，**THEN** 疲乏保持 Lv3（封顶），不死亡（死亡仅在 Lv2→Lv3 时触发）
- **GIVEN** 疲乏 Lv1 的角色，**WHEN** 完成长休，**THEN** 疲乏降至 Lv0（无疲乏）

### 战斗外持续
- **GIVEN** 角色在战斗结束时剩余 Poisoned（持续=3），**WHEN** 战斗结束（OnCombatEnd 被调用），**THEN** Poisoned 持续时间冻结在 3，不会在战斗外自然到期
- **GIVEN** 死亡角色，**WHEN** 被复活术复活，**THEN** 所有死亡前条件清除，疲乏=max(0, 死亡前Lv-1)

### 防御
- **GIVEN** 角色 char_A，**WHEN** 尝试施加 Poisoned（持续=0），**THEN** 施加被拒绝，返回 Rejected，不发布事件

## Open Questions

- **疲乏与伤疤混合效果**: 角色同时 HP 减半(疲乏 Lv3) + HP 上限降低(伤疤)时，叠加顺序？（建议: 先应用伤疤固定值，再应用疲乏百分比减半——`floor((max_hp - scar_penalty) / 2)`）
- **Phase 2 新增条件的顺序**: 6 种延后条件中，哪些优先加入？（建议顺序: Petrified→Stunned→Restrained→Grappled→Invisible→Deafened）
- **免疫上限**: 若角色通过魔法物品获得大量条件免疫，是否需要限制同时生效的免疫数量？（建议: 不设上限，依赖装备槽位和同调系统自然限制）

> **已解决 (v1.1)**:
> - ✅ 同名条件刷新策略 → 统一为"始终取较长者"（无论同源或异源）
> - ✅ 战斗外持续时间追踪 → 冻结策略（战斗结束时冻结，战斗开始时恢复）
> - ✅ 疲乏 Lv3 超额处理 → Lv3 封顶，死亡仅在 Lv2→Lv3 时触发一次
