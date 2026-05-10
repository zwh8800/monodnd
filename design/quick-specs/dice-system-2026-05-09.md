# Quick Design Spec: 骰子系统 (Dice System)

**Type**: New Small System
**Scope**: DND 5e SRD d20 核心判定的纯函数库——攻击检定、豁免检定、属性检定、先攻检定、伤害骰。不包含 GM 重骰、骰子动画、音效。
**Date**: 2026-05-09
**Rule Baseline**: DND 5e SRD + 本游戏 3 项骰子相关偏差
**Estimated Implementation**: 4-6 小时（核心逻辑约 200 行 + 测试 15-20 个用例）

---

## Overview

骰子系统是《酒馆与命运》的**核心判定引擎**，实现 DND 5e SRD 的 d20 不确定性机制——所有攻击检定、豁免检定、属性检定、先攻和伤害均通过本系统结算。

系统为**无状态纯函数静态库**：每次调用传入完整参数（加值、优势/劣势标记、骰子表达式），返回结构化结果。系统不持有角色状态、不缓存骰子历史、不触发任何副作用——它是一个"骰子计算器"，调用方负责解释和使用结果。

服务于 **Pillar 2（战术深度——原汁原味DND）**：d20 的不确定性是 DND 体验的物理根基。骰子落地的声音是玩家理解风险和收益的感官锚点——"我投出了 17，加上 +5 就是 22，命中了！"

---

## Core Rules

### 1. 基础掷骰

```
RollDie(int sides) → int ∈ [1, sides]
```

给定面数，返回 [1, sides] 范围内的均匀随机整数。所有 d20 检定和伤害骰均基于此操作。

随机源使用 `System.Random` 的单例实例（单线程游戏中够用）。MVP 阶段不要求种子可控——如需用于测试的可重现随机性，可通过 `IRandomProvider` 接口注入（Phase 2 优化项）。

---

### 2. d20 检定（攻击 / 豁免 / 属性）

DND 5e 的三类 d20 检定共享相同的数学结构：

```
d20_result = d20 + ability_modifier + proficiency_bonus + other_bonuses
```

#### 2.1 核心方法

```
RollD20Check(D20CheckRequest request) → D20CheckResult
```

**输入** `D20CheckRequest`：

| 字段 | 类型 | 必填 | 说明 |
|------|------|:----:|------|
| `Modifier` | int | ✅ | ability_mod + proficiency_bonus + other_bonuses 的总和 |
| `Advantage` | AdvantageState | ✅ | `Normal` / `Advantage` / `Disadvantage` |
| `CheckType` | D20CheckType | ✅ | `Attack` / `SavingThrow` / `AbilityCheck` / `Initiative` |

**优势/劣势规则**（5e RAW）：
- `Advantage`：掷 2d20，取较高值
- `Disadvantage`：掷 2d20，取较低值
- `Normal`：掷 1d20
- 任意数量的优势 + 任意数量的劣势 = 互相抵消 → `Normal`（由调用方在调用前合并，本系统不接受同时为优+劣的输入）

**暴击判定**（仅 `Attack` 类型）：
- 任意一颗原始 d20 结果 = 20 → **暴击**（自动命中，无论 AC 多少）
- 任意一颗原始 d20 结果 = 1 → **大失败**（自动未命中）
- 暴击/大失败判定只看 d20 原始值，不计加值
- **豁免和属性检定不受暴击/大失败影响**（5e RAW：Nat20/Nat1 在豁免和技能检定中无特殊效果）

**输出** `D20CheckResult`：

| 字段 | 类型 | 说明 |
|------|------|------|
| `RawDieValue` | int | 优势/劣势取高/低后的原始 d20 值 |
| `Total` | int | RawDieValue + Modifier |
| `DiceRolled` | int[] | 所有掷出的原始 d20 值（1 或 2 个） |
| `Modifier` | int | 传入的加值 |
| `IsNatural20` | bool | 是否暴击（任意骰子 = 20）— 仅 `Attack` 类型有意义 |
| `IsNatural1` | bool | 是否大失败（任意骰子 = 1）— 仅 `Attack` 类型有意义 |
| `AdvantageUsed` | AdvantageState | 实际使用的优劣势状态 |
| `CheckType` | D20CheckType | 传入的检定类型 |

#### 2.2 便捷方法

```csharp
// 攻击检定
RollAttack(int attackBonus, AdvantageState advantage) → D20CheckResult
// 等效于 RollD20Check(new(attackBonus, advantage, D20CheckType.Attack))

// 豁免检定
RollSavingThrow(int saveBonus, AdvantageState advantage) → D20CheckResult
// 等效于 RollD20Check(new(saveBonus, advantage, D20CheckType.SavingThrow))

// 属性检定（技能检定）
RollAbilityCheck(int checkBonus, AdvantageState advantage) → D20CheckResult
// 等效于 RollD20Check(new(checkBonus, advantage, D20CheckType.AbilityCheck))
```

---

### 3. 先攻检定（DND 偏差）

```
RollInitiative(int dexterityModifier, int otherBonuses = 0) → D20CheckResult
```

**规则**：
- 公式：`d20 + DEX_modifier + other_bonuses`
- **无优势/劣势**——先攻骰不受优劣势影响（5e RAW：先攻是"敏捷检定"，可受 Enhance Ability 等优势效果；但在本游戏中简化为始终 `Normal`）
- **偏差（GDD-v1 §5.4）**：每轮开始时**重新掷先攻**，非标准 5e 的整场战斗固定顺序
  - 本系统只提供掷骰计算——"每轮重掷"由 CombatFSM 在 `ROUND_START` 状态触发

**内部调用**：
```csharp
RollInitiative(int dexMod, int otherBonuses = 0) →
    RollD20Check(new(dexMod + otherBonuses, Normal, D20CheckType.Initiative))
```

---

### 4. 伤害骰

```
RollDamage(DamageRollRequest request) → DamageRollResult
```

**输入** `DamageRollRequest`：

| 字段 | 类型 | 必填 | 说明 |
|------|------|:----:|------|
| `DiceExpression` | string | ✅ | 骰子表达式，如 `"2d6+3"`、`"1d8"`、`"4d10-2"` |
| `FlatBonus` | int | 否 | 额外加值（如属性加值、附魔加值） |
| `IsCritical` | bool | 否 | 是否暴击 |

**暴击伤害规则（DND 偏差 — GDD-v1 §5.4）**：

| 规则 | DND 5e 标准 | 本游戏 |
|------|-----------|--------|
| 暴击掷骰 | **双倍骰子数量**：`2d6+3` → 掷 `4d6+3` | **伤害骰取最大值**：`2d6+3` → `6+6+3 = 15` |
| 判定依据 | Nat20 自动命中即为暴击 | 同标准 |
| 暴击范围扩展 | Champion Fighter Lv3：19-20 | 同标准（19-20 也视为暴击） |

**设计理由**（来自 GDD-v1）：取最大值比双倍骰子更快结算、更爽、避免"双骰取小"的挫败感。"剑圣一刀砍出满伤"的体验比"再掷一次，啊，掷了 2"更满足。

**正常掷伤害**：
```
dice_expression = "2d6+3"
→ 掷 2 个 d6：4, 3
→ total = 4 + 3 + 3(表达式自带) + flatBonus
```

**暴击掷伤害**：
```
dice_expression = "2d6+3"
→ 每个骰子取最大值：6, 6
→ total = 6 + 6 + 3(表达式自带) + flatBonus
→ 不额外掷骰子
```

**输出** `DamageRollResult`：

| 字段 | 类型 | 说明 |
|------|------|------|
| `Rolls` | int[] | 每个伤害骰的结果（暴击时全为最大值） |
| `ExpressionBonus` | int | 表达式中自带的加值（如 `+3`） |
| `FlatBonus` | int | 额外加值 |
| `Total` | int | Rolls.Sum() + ExpressionBonus + FlatBonus |
| `IsCritical` | bool | 是否暴击 |
| `DiceCount` | int | 骰子数量（如 `2d6` = 2） |
| `DiceSides` | int | 骰子面数（如 `2d6` = 6） |

---

### 5. 骰子表达式解析

```
ParseDiceExpression(string expression) → DiceExpression
```

**输入格式**：`XdY±Z`
- `X`：骰子数量（正整数）
- `Y`：骰子面数（正整数，常见：4, 6, 8, 10, 12, 20）
- `±Z`：固定加值（可选，整数）
- 大小写不敏感：`d` 和 `D` 均接受

**输出** `DiceExpression`（record）：

| 字段 | 类型 | 说明 |
|------|------|------|
| `Count` | int | 骰子数量 |
| `Sides` | int | 骰子面数 |
| `Bonus` | int | 固定加值 |

**异常**：
- 空字符串 / null → `ArgumentException`
- 缺少 `d` 分隔符 → `FormatException`
- `X` 或 `Y` 不是正整数 → `FormatException`

**示例**：

| 输入 | Count | Sides | Bonus |
|------|:-----:|:-----:|:-----:|
| `"2d6+3"` | 2 | 6 | 3 |
| `"1d20"` | 1 | 20 | 0 |
| `"4d10-2"` | 4 | 10 | -2 |
| `"1D8"` | 1 | 8 | 0 |

---

### 6. 本系统不负责的事项

以下功能不属于骰子系统，由其他系统处理：

| 事项 | 所属系统 | 说明 |
|------|---------|------|
| 优势/劣势的来源判定 | 条件效果系统 / 战斗系统 | 骰子系统只接受"是否有优势"的枚举入参 |
| 攻击命中/未命中的判定 | ActionResolver | 骰子系统输出 d20 结果，ActionResolver 对比 AC 判断命中 |
| 豁免成功/失败的判定 | ActionResolver | 骰子系统输出 d20 结果，ActionResolver 对比 DC |
| 伤害类型（穿刺/火焰等） | 战斗系统 | 骰子系统只计算伤害数值 |
| 死亡豁免 | 战斗系统 §8.3 | **本游戏不使用 d20 死亡豁免**——使用确定性计数（3 轮无治疗 = 死亡） |
| 骰子动画/音效 | UI 系统 / 音频系统 | 骰子系统输出数值，UI/音频根据数值播放效果 |
| 伽马重骰 / 命运点 | LLM 网关 / UI 系统 | 手动重骰是 GM 模式功能，非核心骰子系统 |

---

## Data Types

```csharp
/// <summary>优劣势状态</summary>
public enum AdvantageState
{
    Normal,
    Advantage,
    Disadvantage
}

/// <summary>d20 检定类型——影响暴击判定行为</summary>
public enum D20CheckType
{
    Attack,        // Nat20 = 暴击, Nat1 = 大失败
    SavingThrow,   // Nat20/Nat1 无特殊效果
    AbilityCheck,  // Nat20/Nat1 无特殊效果
    Initiative     // Nat20/Nat1 无特殊效果
}

/// <summary>d20 检定输入</summary>
public record D20CheckRequest
{
    public int Modifier { get; init; }
    public AdvantageState Advantage { get; init; } = AdvantageState.Normal;
    public D20CheckType CheckType { get; init; }
}

/// <summary>d20 检定结果</summary>
public record D20CheckResult
{
    public int RawDieValue { get; init; }
    public int Total { get; init; }
    public int[] DiceRolled { get; init; } = [];
    public int Modifier { get; init; }
    public bool IsNatural20 { get; init; }
    public bool IsNatural1 { get; init; }
    public AdvantageState AdvantageUsed { get; init; }
    public D20CheckType CheckType { get; init; }
}

/// <summary>伤害骰输入</summary>
public record DamageRollRequest
{
    public string DiceExpression { get; init; } = "";
    public int FlatBonus { get; init; }
    public bool IsCritical { get; init; }
}

/// <summary>伤害骰结果</summary>
public record DamageRollResult
{
    public int[] Rolls { get; init; } = [];
    public int ExpressionBonus { get; init; }
    public int FlatBonus { get; init; }
    public int Total { get; init; }
    public bool IsCritical { get; init; }
    public int DiceCount { get; init; }
    public int DiceSides { get; init; }
}

/// <summary>解析后的骰子表达式</summary>
public record DiceExpression
{
    public int Count { get; init; }
    public int Sides { get; init; }
    public int Bonus { get; init; }
}
```

---

## Tuning Knobs

| Knob | Default | 说明 | 调优方式 |
|------|---------|------|---------|
| 暴击伤害策略 | `Maximize`（取最大） | DND 标准是 `DoubleDice`（双倍骰子），本游戏使用 `Maximize` | 硬编码在 `RollDamage` 方法中。如需回退标准 5e，改为 `DoubleDice` 分支。MVP 保持当前值 |

---

## Acceptance Criteria

### 基础掷骰
- [ ] **AC-DICE.01**：`RollDie(20)` 1000 次结果全部在 [1, 20]，分布无明显偏差

### d20 检定核心
- [ ] **AC-DICE.02**：`RollD20Check(new(5, Normal, Attack))` → `RawDieValue` ∈ [1, 20]，`Total = RawDieValue + 5`，`DiceRolled.Length = 1`
- [ ] **AC-DICE.03**：`RollD20Check(new(5, Advantage, Attack))` → `DiceRolled.Length = 2`，`RawDieValue = Max(DiceRolled)`，`Total = Max + 5`
- [ ] **AC-DICE.04**：`RollD20Check(new(5, Disadvantage, Attack))` → `DiceRolled.Length = 2`，`RawDieValue = Min(DiceRolled)`，`Total = Min + 5`
- [ ] **AC-DICE.05**：自然 20 → `IsNatural20 = true`，`IsNatural1 = false`
- [ ] **AC-DICE.06**：自然 1 → `IsNatural1 = true`，`IsNatural20 = false`
- [ ] **AC-DICE.07**：暴击判定**仅**在 `D20CheckType.Attack` 时设置 `IsNatural20`/`IsNatural1`——`SavingThrow` 和 `AbilityCheck` 中 Nat20 不设置

### 便捷方法
- [ ] **AC-DICE.08**：`RollAttack(5, Advantage)` 返回的类型与 `RollD20Check` 一致，`CheckType = Attack`
- [ ] **AC-DICE.09**：`RollSavingThrow(3, Disadvantage)` → `CheckType = SavingThrow`，劣势取低
- [ ] **AC-DICE.10**：`RollAbilityCheck(4, Normal)` → `CheckType = AbilityCheck`

### 先攻
- [ ] **AC-DICE.11**：`RollInitiative(2, 3)` → `Total = d20 + 2 + 3`，范围 [6, 25]

### 伤害骰
- [ ] **AC-DICE.12**：`RollDamage(new("2d6+3"))`（正常）→ `Total` ∈ [5, 15]，`Rolls.Length = 2`，`ExpressionBonus = 3`
- [ ] **AC-DICE.13**：`RollDamage(new("2d6+3", IsCritical: true))`（暴击）→ `Total = 15`，`Rolls = [6, 6]`，不掷骰
- [ ] **AC-DICE.14**：`RollDamage(new("1d8", FlatBonus: 2))` → `Total` ∈ [3, 10]，`Rolls.Length = 1`

### 表达式解析
- [ ] **AC-DICE.15**：`ParseDiceExpression("2d6+3")` → `(Count: 2, Sides: 6, Bonus: 3)`
- [ ] **AC-DICE.16**：`ParseDiceExpression("1d20")` → `(1, 20, 0)`
- [ ] **AC-DICE.17**：`ParseDiceExpression("4d10-2")` → `(4, 10, -2)`
- [ ] **AC-DICE.18**：`ParseDiceExpression("")` → 抛出 `ArgumentException`
- [ ] **AC-DICE.19**：`ParseDiceExpression("invalid")` → 抛出 `FormatException`

### 回归保护
- [ ] **AC-DICE.20**：系统为无状态纯函数——相同输入始终产生相同结构的结果，不持有内部状态

---

## Systems Index

此系统已在 `design/gdd/systems-index.md` 中列为 **#2 骰子系统 (Dice System)**：
- **层级**：Foundation（无游戏系统依赖，所有 Core 层系统依赖它）
- **优先级**：MVP
- **设计顺序**：#2（事件总线之后，角色系统之前）
- **当前状态**：Not Started → **本 spec 完成后更新为 "Designed"**

---

## GDD Cross-References

本 spec 记录的规则引用自以下 GDD，如出现矛盾以本 spec 为准（作为骰子系统的**单一权威源**）：

| 规则 | 引用源 | 裁决 |
|------|--------|------|
| 攻击检定公式 | `04-combat-system.md` §5.1 | 一致 |
| 优势/劣势抵消规则 | `04-combat-system.md` §5.3 | 一致（5e RAW） |
| 暴击取最大值（非双倍骰） | `GDD-v1.md` §5.4；`04-combat-system.md` §1.3 | ✅ 采纳——本 spec §4 |
| 先攻每轮重骰 | `GDD-v1.md` §5.4；`04-combat-system.md` §3.2 | ✅ 采纳——重骰由 CombatFSM 触发，本系统提供计算 |
| 无 d20 死亡豁免 | `GDD-v1.md` §5.4；`01-character-system.md` §2.5.3 | ✅ 采纳——骰子系统不处理死亡豁免 |
| 豁免/属性检定 Nat20 无特殊 | 5e SRD RAW | ✅ 标准规则 |
| 先攻无优劣势影响 | 5e SRD（先攻视为敏捷检定）| 📌 简化为 `Normal`，如需恢复 5e 语义可扩展 |
