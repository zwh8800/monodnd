# ADR-0008：角色系统数据模型冻结协议

## Status
Accepted

## Date
2026-05-10

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | MonoGame 3.8.5+ |
| **Domain** | Data — 角色数据模型是 11 个下游系统的共享契约；冻结后可安全并行开发 |
| **Knowledge Risk** | LOW — 角色数据模型基于 DND 5e SRD（六属性、HP/AC/法术位），规则高度固化；LLM 训练数据充分覆盖 |
| **References Consulted** | `design/gdd/01-character-system.md`（角色完整 Schema §2.1、种族/职业定义 §2.2-2.3、衍生值公式 §2.1.2）、`src/DndGame/Systems/Character/CharacterData.cs`（当前实现）、`src/DndGame/Systems/Character/CharacterEnums.cs`（当前枚举）、`src/DndGame/Systems/Character/CharacterGenerator.cs`（生成器实现） |
| **Post-Cutoff APIs Used** | None — 纯 C# `record` / `enum` 定义，无引擎 API 依赖 |
| **Verification Required** | `dotnet build` zero errors/warnings；`dotnet test` 在角色系统及全部 11 个下游系统的测试通过 |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0001（ECS — 角色 Entity 持有 CharacterData 组件）、ADR-0005（数据驱动设计 — 种族/职业配置从 JSON 加载） |
| **Enables** | 全部 11 个下游系统：Combat（HP/AC/属性/法术位）、Items（装备槽位/属性加成）、Conditions（14 种条件追踪）、EnemyAI（目标优先级）、LLMGateway（叙事上下文）、Map（角色位置/状态）、Adventure（CR 计算/生成参数）、Tavern（招募/关系值）、Failure（伤疤/传承）、Dialogue（关系+性格影响对话）、UI（角色面板/装备/关系界面） |
| **Blocks** | 全部 11 个下游 Epic 的 Story — 角色数据模型不稳定将导致所有并行实现工作产生级联返工 |
| **Ordering Note** | 必须在任何下游 Story 大规模展开前 Accepted。此为"先冻结数据契约，再并行实现"的安全锁 |

## Context

### Problem Statement
角色系统是项目依赖面最广的模块——11 个系统直接依赖其数据模型（Combat/Items/Conditions/EnemyAI/LLMGateway/Map/Adventure/Tavern/Failure/Dialogue/UI）。当前 `01-character-system.md` GDD 已完整定义了角色 Schema（§2.1，351行 JSON 示例），但仅部分在代码中实现（`CharacterData`、`CharacterStats`、`CharacterNarrative`、`Ability`/`Skill`/`DamageType` enum）。随着 Sprint 1 即将开工，11 个团队（或 AI Agent）将并行实现各自依赖角色数据的 Story。如果数据模型在并行开发期间发生结构性变更（如重命名属性、删除字段、改变类型），将导致级联返工——修改 1 行定义意味着 11 个系统需要同步修改和重新测试。

需要建立明确的冻结规则：哪些已 Frozen、哪些可以 Extension、变更的正确流程是什么。

### Constraints
- 角色系统有 **11 个下游依赖系统**——项目最多
- `01-character-system.md` GDD 为数据模型的权威定义
- 当前代码实现（`CharacterData.cs`、`CharacterEnums.cs`）为数据模型的 C# 表达
- ADR-0005 要求种族/职业配置从 JSON 加载（`RaceConfig`/`ClassConfig` record）
- ADR-0001 要求角色 Entity 持有 CharacterData 作为 ECS 组件
- 冻结期间不影响 LLM 生成叙事文本（叙事层数据如 `backstory`、`personality` 属于扩展字段，可安全添加）

### Requirements
- 必须明确列出 Frozen 字段（不可删除、不可重命名、不可改变类型）
- 必须明确 Extension-Only 规则（允许新增枚举值、新增字段、新增可选属性）
- 必须定义变更流程（任何 Frozen 字段的变更需要经过哪些步骤）
- 必须确保冻结规则与 GDD 对齐（GDD 定义的字段 = Frozen；GDD 未定义但代码已有的 = 按此 ADR 规则处理）

## Decision

**核心角色数据模型字段 FROZEN。仅允许 Extension（新增枚举值、新增字段）。任何删除、重命名或结构修改必须通过 `/propagate-design-change` 影响评估流程。**

### Frozen Fields（冻结字段清单）

以下字段一旦此 ADR Accepted，**禁止删除、重命名、改变类型或改变语义**：

#### A. 六维属性模型

| 字段 | 当前定义 | 冻结范围 |
|------|---------|---------|
| `Ability` enum | `Str, Dex, Con, Int, Wis, Cha` | **枚举成员不可删除、不可重命名**。顺序不可改变（现有代码依赖 `(int)ability` 转换） |
| `AbilityScore` record | `Score: int` + 计算属性 `Modifier` | **record 结构不可改变**。`Modifier` 公式 `floor((Score-10)/2.0)` 不可改变 |
| `CharacterStats.Abilities` | `Dictionary<Ability, AbilityScore>` | **键类型必须为 `Ability` enum，值类型必须为 `AbilityScore`** |

**对应 GDD**：`01-character-system.md` §2.1.1 `stats.abilities`（JSON Schema 第54-61行）、§2.1.2 衍生值计算公式

**对应代码**：`CharacterEnums.cs` 第6行 `enum Ability`、`CharacterData.cs` 第8-16行 `record AbilityScore`

#### B. 种族与职业标识

| 字段 | 当前定义 | 冻结范围 |
|------|---------|---------|
| 种族标识 | `RaceConfig.RaceId: string`（如 `"human"`, `"elf"`, `"dwarf"`） | **标识键为 `string` 类型，不可改为 enum**。MVP 种族 ID 不可删除（`human`/`elf`/`dwarf`）；Post-MVP 种族 ID 可通过 Extension 添加 |
| 职业标识 | `ClassConfig.ClassId: string`（如 `"fighter"`, `"wizard"`, `"rogue"`） | **标识键为 `string` 类型，不可改为 enum**。MVP 职业 ID 不可删除 |
| `RaceConfig` record | `RaceId, Name, Speed, AbilityIncreases, Traits` | **record 结构不可改变**（不可删除字段、不可改变字段类型） |
| `ClassConfig` record | `ClassId, Name, HitDie, PrimaryAbility, SavingThrowProficiencies, SkillChoices, FeaturesPerLevel` | **record 结构不可改变** |

**对应 GDD**：`01-character-system.md` §2.2（种族定义，9种族）、§2.3（职业定义，12职业）

**对应代码**：`CharacterGenerator.cs` 第9-52行 `RaceConfig` / `ClassConfig` record

#### C. 核心数值公式

| 字段 | 公式 | 冻结范围 |
|------|------|---------|
| HP 最大值计算 | Lv1: HitDie最大值 + CON_mod<br>Lv2+: 上一级HP + max(1, HitDie期望值 + CON_mod) | **公式逻辑不可改变**。`NumericalGenerator.CalculateHP()` 的实现冻结 |
| AC 计算 | 基于护甲类型（无甲/轻甲/中甲/重甲）+ DEX_mod（上限按护甲类型）+ 盾牌+2 | **AC 公式及其护甲类型规则不可改变**。`NumericalGenerator.CalculateAC()` 的实现冻结 |
| 熟练加值 | `floor((level - 1) / 4) + 2` | **公式不可改变**。`NumericalGenerator.GetProficiencyBonus()` 的实现冻结 |
| 属性调整值 | `floor((Score - 10) / 2.0)` | **公式不可改变**。`AbilityScore.Modifier` 的计算属性实现冻结 |

**对应 GDD**：`01-character-system.md` §2.1.2 衍生值计算公式表、§2.1.3 AC 计算公式

**对应代码**：`NumericalGenerator.cs`（CalculateHP/CalculateAC/GetProficiencyBonus）、`CharacterData.cs` 第15行 Modifier 计算

#### D. 法术位结构

| 字段 | 定义 | 冻结范围 |
|------|------|---------|
| 法术位模型 | 1-9 环法术位，每环有 `max` 和 `current` 值 | **环位数量（1-9）不可改变**。`max`/`current` 的语义（最大可用/当前剩余）不可改变 |
| Full Caster 法术位表 | 按 GDD §2.5.1 完整表（Wizard/Cleric/Druid/Bard/Sorcerer） | **表格数据不可改变**（与 DND 5e SRD 对齐，内容固化） |
| Half Caster 法术位表 | 按 GDD §2.5.2（Paladin/Ranger） | **表格数据不可改变** |
| Warlock Pact Magic | 按 GDD §2.5.3（独立法术位系统） | **表格数据不可改变** |

**对应 GDD**：`01-character-system.md` §2.5 法术位表（§2.5.1-2.5.3）

**对应代码**：已在 GDD JSON Schema `§2.1.1 spellcasting.slots` 中定义为 `{max, current}` 结构，代码侧法术位系统尚未完全实现

#### E. 性格六维模型

| 字段 | 定义 | 冻结范围 |
|------|------|---------|
| 性格维度 | `attribute_hint`（核心特质）、`class_archetype`（职业原型）、`race_stereotype`（种族刻板印象）、`life_experience`（人生经历）、`fear_weakness`（恐惧/弱点）、`speech_style`（说话风格） | **六个维度的名称和语义不可删除、不可改变**。可新增维度但不可删除现有维度 |

**对应 GDD**：`01-character-system.md` §2.1.1 `narrative.personality`（JSON Schema 第27-32行）

**对应代码**：当前 `CharacterNarrative.PersonalityTags` 为 `List<string>`（简化为标签列表），完整六维模型尚未在代码中实现。冻结后实现时必须包含全部六个维度

#### F. 关系双轴模型

| 字段 | 定义 | 冻结范围 |
|------|------|---------|
| 关系轴 | `trust: int`（信任度） + `conflict: int`（冲突度） | **双轴模型不可改变为单轴**。`trust` 和 `conflict` 为独立的两个语义维度 |
| 信任衰减率 | 1点/30天 | **不可改变**（如需调整，通过 Tuning Knobs 而非结构变更） |
| 冲突衰减率 | 1点/15天 | **不可改变** |
| 关系标签 | `labels: [{type, name}]`（如"战友"、"情敌"） | **`type`+`name` 结构不可改变** |

**对应 GDD**：`01-character-system.md` §2.1.1 `relationships`（JSON Schema 第71-74行）、§1.7 Tuning Knobs（衰减率参数）

**对应代码**：当前尚未实现关系系统代码，但 GDD Schema 已固化

### Extension-Only（仅允许扩展）

以下操作在冻结期内 **允许且无需变更流程审批**：

| 扩展类型 | 示例 | 约束 |
|---------|------|------|
| 新增种族 enum 值 | 添加 `"gnome"` 种族 | 不可删除已有的 `human`/`elf`/`dwarf`；新种族需有完整的 GDD §2.2 数据 |
| 新增职业 enum 值 | 添加 `"cleric"` 职业 | 不可删除已有的 `fighter`/`wizard`/`rogue`；新职业需有完整的 GDD §2.3 数据 |
| 新增条件类型 | 添加新的 `ConditionType` 枚举值 | 不可删除现有的 14 种条件（Blinded-Prone）；新条件需在 `conditions.json` 中定义参数 |
| 新增装备槽位 | 添加新的槽位到 `equipment.slots` | 不可删除现有的 9 个槽位（main_hand 到 amulet）；新槽位需同步更新 UI 和装备系统 |
| 新增可选字段到 record | 添加 `CharacterNarrative.Title: string?` | 新字段必须为可选（`= null` 或 `= default`），不影响现有代码 |
| 新增 personality 维度 | 添加 `moral_alignment` 维度 | 不可删除现有的 6 个维度 |
| 新增关系标签类型 | 添加 `"rival"` 标签 | 不可改变 `{type, name}` 的结构 |

### Change Process（变更流程）

任何 Frozen 字段的删除、重命名、类型变更或语义变更必须经过以下流程：

```
Step 1: 提出变更
  · 在 PR 或 ADR Proposal 中明确描述：变什么、为什么、影响面估算

Step 2: 运行 /propagate-design-change
  · 自动扫描 11 个下游 GDD + 所有代码文件
  · 生成影响报告：哪些文件需要修改、哪些测试可能回归
  · 估算工作量（文件数 × 变更复杂度）

Step 3: 影响报告 → 显式接受
  · Technical Director 和 Game Designer 审阅影响报告
  · 显式 Accept（不接受隐式通过）

Step 4: 更新 GDD
  · 首先更新 01-character-system.md 对应章节
  · 然后更新所有 11 个下游 GDD 中受影响的交叉引用

Step 5: 更新代码
  · 更新 CharacterData.cs / CharacterEnums.cs 等核心定义
  · 逐一更新 11 个下游系统的代码
  · 每个下游系统独立提交（不混在同一个 commit）

Step 6: 验证
  · dotnet build zero errors/warnings
  · dotnet test 全部通过（包括角色系统 + 11 个下游系统的测试）
  · 如有测试失败，修复后再重新验证
```

### Frozen vs Extension 决策树

```
问题: 我想修改角色数据模型中的某个字段...

├─ 是新增一个值？（新种族/新职业/新条件/新槽位）
│  └─ ✅ 允许（Extension-Only）—— 直接添加，无需审批
│
├─ 是新增一个可选字段？（添加新属性到 record，默认值 = null/default）
│  └─ ✅ 允许（Extension-Only）—— 直接添加，注意设为可选
│
├─ 是删除/重命名/改变类型？
│  └─ ❌ 禁止 —— 必须走 Change Process（6 步）
│
├─ 是改变核心公式（HP/AC/PB/Modifier）？
│  └─ ❌ 禁止 —— 必须走 Change Process + Tuning Knobs 安全范围验证
│
└─ 不确定？
   └─ ⚠️ 先问 Technical Director —— 永远不要假设
```

## Alternatives Considered

### Alternative A：不冻结，自由演化（Agile）

- **Description**：不对角色数据模型施加任何冻结限制，允许任何 Story 自由修改模型字段，由 CI 和代码审查捕获破坏性变更
- **Pros**：最大灵活性——可以快速响应设计变更；与 Agile 哲学一致
- **Cons**：11 个并行开发的下游系统中任何一个修改核心字段都会导致级联返工；调试成本指数级增长（"谁改了这个字段？为什么 6 个测试突然失败？"）；AI Agent 缺乏全局意识，可能孤立地"优化"字段而不知道下游影响
- **Rejection Reason**：11 个下游依赖 + AI Agent 并行开发 = 灾难性组合。不冻结意味着每个 Sprint 都可能出现未知的级联回归。冻结是安全并行开发的必要条件

### Alternative B：完全冻结，零变更

- **Description**：角色数据模型完全锁定——不允许任何新增、不允许任何修改，直到 v2.0
- **Pros**：最大稳定性——零意外变更
- **Cons**：过度刚性——MVP 阶段可能发现需要新增种族/条件/槽位，完全冻结会阻塞合理的扩展需求；与 Agile 开发现实不符
- **Rejection Reason**：Extension-Only 提供了合理的安全中间地带——允许无痛添加新内容（新种族/新职业/新条件），同时保护现有结构不受破坏

### Alternative C：Extension-Only + Change Process（Accepted）

- **Description**：核心字段冻结 + 允许扩展 + 破坏性变更走审批流程
- **Pros**：平衡安全性和灵活性——11 个下游系统可以安全并行开发；合理的扩展需求不被阻塞（新增种族/职业/条件）；破坏性变更不会意外发生（必须走审批流程）；变更影响可预测（通过 `/propagate-design-change`）
- **Cons**：增加流程开销（但值得——与级联返工的成本相比微不足道）
- **Selection Reason**：这是唯一满足"安全并行开发"+"允许合理扩展"+"防止意外破坏"三个需求的方案

## Consequences

### Positive
- 11 个下游系统可以安全地并行开发——每个系统读取的角色数据结构保证稳定
- 任何对 Frozen 字段的修改必须经过显式审批——消除"某人改了 CharacterData，6 个系统突然编译失败"的风险
- Extension-Only 规则允许 MVP 期间合理添加新种族/职业/条件——不阻塞内容扩展
- AI Agent 获得明确边界——"你可以添加新值，但不能改变现有结构"
- 变更影响可追踪——通过 `/propagate-design-change` 生成的报告

### Negative
- 增加了流程开销——破坏性变更需要走 6 步审批流程
- 如果 Frozen 字段的定义在 MVP 期间被证明有误，修正成本高于不冻结的情况（需要走完整流程）
- 需要所有开发者（人类和 AI Agent）理解和遵守冻结规则——需要文档和 onboarding

### Risks

| Risk | Severity | Mitigation |
|------|----------|------------|
| Frozen 字段在实现中被发现设计缺陷 | Medium | Frozen 字段均基于 DND 5e SRD 固化规则——经过 50 年验证，设计缺陷概率极低。如果确实发现问题，Change Process 6 步可安全修正 |
| 开发者无视冻结规则直接修改 | High | 代码审查强制检查；CI 中通过自动化工具检测 Frozen 字段的变更（可选增强）；AI Agent 在 System Prompt 中注入冻结规则 |
| Extension 泛滥（太多新种族/职业/条件导致平衡失控） | Low | 新增枚举值仍受 GDD 约束（新种族必须有完整数据）；Balance Check 可检测新内容的数值异常 |
| Frozen 规则与实际 GDD 不一致 | Medium | 本文档的 Frozen Fields 清单与 `01-character-system.md` 交叉引用——任何 GDD 更新需同步更新此 ADR |

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| 01-character-system.md §2.1 | 完整角色属性 Schema（351行 JSON） | Frozen Fields 清单中 A-F 组与 Schema 的六个核心块一一对应 |
| 01-character-system.md §2.1.2 | 衍生值计算公式（HP/AC/PB/Modifier） | 核心数值公式（C 组）冻结——所有下游系统依赖这些公式的一致性 |
| 01-character-system.md §2.2 | 9 种族定义（MVP: Human/Elf/Dwarf） | MVP 种族 ID 不可删除；新种族通过 Extension 添加（§2.2.3 完整版种族定义） |
| 01-character-system.md §2.3 | 12 职业定义（MVP: Fighter/Wizard/Rogue） | MVP 职业 ID 不可删除；新职业通过 Extension 添加（§2.3.5 完整版职业定义） |
| 01-character-system.md §2.1.1 | 性格六维模型（personality） | E 组冻结——六个维度不可删除 |
| 01-character-system.md §2.1.1 | 关系双轴模型（trust×conflict） | F 组冻结——双轴结构不可改为单轴 |
| 01-character-system.md §2.5 | 法术位表（Full/Half Caster + Warlock） | D 组冻结——法术位结构与 DND 5e SRD 对齐 |
| 01-character-system.md §1.4 | STR-DEX 平衡 House Rule（物理伤害减免=STR_mod 等） | House Rule 参数在 C 组公式中冻结——不可在并行开发期间改变 |
| 03-items-equipment.md | 装备槽位模型（10 槽位） | 现有 9 个槽位不可删除；新槽位通过 Extension 添加 |
| 10-condition-effects-system.md | 14 种 DND 5e 条件 | 现有 ConditionType 枚举值不可删除；新条件通过 Extension 添加 |

## Performance Implications
- **CPU**：N/A — 冻结协议是流程约束，不影响运行时性能
- **Memory**：N/A
- **Load Time**：N/A

## Validation Criteria
- `dotnet build` zero errors, zero warnings（在角色系统 + 全部 11 个下游系统上）
- `dotnet test` 全部通过，零回归
- 任何新提交的角色数据模型变更必须符合冻结规则：
  - Frozen 字段无删除/重命名/类型变更
  - Extension 操作仅新增（枚举值/可选字段），不修改已有定义
- `/propagate-design-change` 对任何破坏性变更生成完整影响报告
- AI Agent System Prompt 中包含冻结规则摘要（确保 AI 遵守）

## Related Decisions
- ADR-0001 — ECS 架构（角色 Entity 持有 CharacterData 作为 Component）
- ADR-0005 — 数据驱动设计（种族/职业配置从 JSON 加载，RaceConfig/ClassConfig 结构冻结）
- ADR-0006 — 战斗引擎架构（Combat 读取角色 HP/AC/属性——依赖冻结的 C 组公式）
- `design/gdd/01-character-system.md` — 角色系统完整 GDD（冻结字段的权威定义来源）
- `design/gdd/04-combat-system.md` — 战斗系统（最大角色数据消费者）
- `design/gdd/03-items-equipment.md` — 物品装备（装备槽位模型）
- `design/gdd/10-condition-effects-system.md` — 条件效果（ConditionType 枚举）
- `src/DndGame/Systems/Character/CharacterData.cs` — 当前 C# 实现（68行）
- `src/DndGame/Systems/Character/CharacterEnums.cs` — 当前枚举定义（33行）
- `src/DndGame/Systems/Character/CharacterGenerator.cs` — 当前生成器实现（164行）

## Architecture Alignment
- **GDD State**: `01-character-system.md` 完整定义（1167+ 行，包含所有 Frozen 字段的权威定义）✅
- **Code State**: `CharacterData.cs`（68行）+ `CharacterEnums.cs`（33行）+ `CharacterGenerator.cs`（164行）— 部分实现，核心结构已建立 ✅
- **Downstream Systems**: 11 个系统 GDD 已编写——冻结确保它们可以安全开始实现 ✅
- **Resolution**: This ADR defines the freeze protocol. Future changes to Frozen fields MUST follow the 6-step Change Process.
