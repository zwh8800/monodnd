# 敌人AI系统 — 补充设计规范

> **Status**: In Review（v2.0 — `/design-review` 2026-05-10 MAJOR REVISION 修订中）
> **Author**: Sisyphus + user + creative-director
> **Last Updated**: 2026-05-10
> **Implements Pillar**: P2 (战术深度——原汁原味DND)
> **权威关系**: 本文档补充 `04-combat-system.md` §11（敌人AI行为树），提供打分公式、权重向量、协作逻辑和难度机制的详细规范。**核心行为类型映射表和 stat block 的 `behavior` 字段权威定义在 04§11.3.1，本文档不重复定义。**

## Overview

敌人AI系统为所有非玩家战斗单位提供决策能力。它决定了敌人选择攻击谁、使用什么能力、移动到什么位置、何时逃跑——这些决策共同构成了玩家面对的战术挑战。没有AI，敌人就是木桩，战斗退化为纯数值交换——违背 Pillar 2"战术深度"的核心承诺。

系统基于分层决策模型：每回合所有敌人先独立完成态势评估→预决策（目标/行动/位置意向），然后进入协作调整阶段（集火协调、保护重定向），最后执行决策。不同敌人通过**权重向量**（而非共享公式）实现行为差异化——这是"个性"的数学表达。难度级别影响权重精度和噪声水平。MVP 阶段交付 Normal 难度的 6 种行为类型，覆盖所有基础敌人类型。

本 GDD 为 `04-combat-system.md` §11 的补充扩展文档——核心行为类型定义和数据模型权威在 04§11，本文档提供打分机制、难度系统和协作逻辑的详细规范。

## Player Fantasy

> 敌人AI是玩家面对的"看不见的对手"——聪明的AI让每一场战斗都像在对抗一个活生生的敌人而非训练木桩。哥布林从侧翼包抄后撤离、骷髅无视恐惧条件冲向最近者、巨魔优先追打受伤最重的角色——这些行为差异来自**权重向量**的设计：不同敌人类型对距离、HP、威胁和特殊条件的敏感度不同，产生可辨识的"个性"。玩家需要针对不同敌人调整战术：对骷髅不能被距离欺骗（它们只看距离不看威胁），对巨魔不能让脆皮角色暴露在低HP状态。AI不追求打败玩家，而是追求让玩家打败它时感到自己是真的赢了。

## Detailed Design

### Core Rules

#### 行为类型权威源

敌人AI的行为类型映射表、`behavior.type` 枚举值和 `special_tactics` 数组的**权威定义**位于 `04-combat-system.md` §11.3.1。本文档使用以下 6 种行为类型，与 04§11.3.1 的映射关系如下：

| 本文档类型 | 04§11.3.1 标识符 | 典型 enemies |
|:---:|------|------|
| `basic_melee` | `basic_melee` | 基础近战（兽人步兵、民兵） |
| `berserker` | `berserker` | 狂暴近战（巨魔、狂战士） |
| `skirmisher` | `skirmisher` / `hit_and_run` | 游击近战（哥布林、豺狼人） |
| `undead_soldier` | `undead_soldier` | 亡灵战士（骷髅、僵尸） |
| `caster` | `caster` | 施法者（哥布林萨满、邪教徒法师） |
| `assassin` | `assassin` | 刺客（Phase 2 扩展；MVP 回退到 skirmisher） |

#### AI决策流程（两阶段）

```
每回合所有敌人分两阶段决策:

Phase 1 — 独立预决策（所有敌人并行，互不影响）:
  Step 1: 评估战场态势
    - 统计双方存活人数
    - 遍历所有玩家计算 estimated_dpr（缓存，见 Formulas §estimated_dpr）
    - 评估自身HP、可用行动（法术位/冷却/每场限制）和条件状态

  Step 2: 选择意向目标
    - 以权重向量 + 有界打分制评估每个存活玩家
    - 公式: target_score = Σ(component_i × weight_i)（见 Formulas §目标选择）
    - 取最高分目标作为意向目标
    - 治疗者使用独立的 ally_heal_score 公式（见 Formulas §治疗者目标选择）

  Step 3: 选择意向行动
    - 基于行为类型的 special_tactics + 行动可用性
    - 优先顺序: CD技能 → 最高伤害技能 → 基础攻击
    - 条件影响: 目盲→不选视觉技能; 恐慌→不靠近恐慌源; 自身失能→跳过回合

  Step 4: 选择意向位置
    - 对移动范围内的可达 tile 进行 position_score 评分
    - 公式: position_score = Σ(factor_i × factor_weight)（见 Formulas §位置选择）
    - 取最高分可达 tile

Phase 2 — 协作调整（所有敌人意向确定后）:
  - 集火协调: 如果 ≥2 敌人意向目标相同 → 所有集火者攻击检定+2
  - 保护响应: 如果治疗者被 ≥2 敌人意向锁定 → 1 个最近近战敌人重定向目标为保护治疗者（攻击治疗者的敌人）
  - 最终决策: 将 Phase 1 意向 ± Phase 2 调整 → 提交 CombatAction
```

#### 行为类型权重向量（个性的数学表达）

不同行为类型使用**不同权重向量**作用于统一的打分公式，这是差异化"个性"的核心机制：

| 行为类型 | 距离权重 | HP权重 | 威胁权重 | 特殊权重 | 撤退阈值 | 设计意图 |
|:---|:---:|:---:|:---:|:---:|:---:|------|
| `basic_melee` | 1.0 | 0.5 | 0.3 | 0.5 | 20% | 均衡近战——偏重距离，适度考虑HP |
| `berserker` | 0.3 | 1.0 | 0.2 | 0.3 | 15% | 嗜血——追最弱目标（HP权重主导），不关心自身安全 |
| `skirmisher` | 0.8 | 0.8 | 0.4 | 0.6 | 30% | 游击——攻击近处弱者，HP<30% 即撤离 |
| `undead_soldier` | 1.2 | 0.0 | 0.0 | 0.0 | 无 | 亡灵无畏——只看距离冲锋，无视HP/威胁/条件，永不撤退 |
| `caster` | 0.2 | 0.6 | 0.8 | 1.0 | 25% | 战术施法者——远程优先高威胁+特殊目标（专注/治疗者） |
| `assassin` | 0.6 | 1.2 | 0.1 | 1.5 | 50% | 刺客——偷袭低HP高价值目标，HP<50% 立即Disengage撤离 |

> **核心原则**: `undead_soldier` 的 threat=0 → 骷髅"只看距离冲锋"（无视威胁/HP）；`berserker` 的 hp=1.0 → 巨魔"追最弱目标"；`assassin` 的 hp=1.2+special=1.5 → 刺客"偷袭低HP施法者然后跑"。权重不同，行为不同——玩家可观察。

#### 协作逻辑（两阶段）

- **Phase 2 集火**: 如果 ≥2 敌人 Phase 1 意向目标相同，所有集火者的攻击检定获得 **+2 命中**（非叠加，固定+2）
- **Phase 2 保护**: 如果治疗者（或标记 `protect_target` 的队友）被 ≥2 敌人意向锁定，距离该队友最近的近战敌人重定向目标为"攻击治疗者的优先级最高敌人"。重定向后该敌人本轮不触发集火加成
- **位置保护**: 近战敌人位置评分中包含 `adjacency_to_ally × protect_weight`——鼓励站在远程/施法者队友前方形成物理阻隔

#### 难度级别

| 难度 | 目标选择 | 行动选择 | 位置策略 | 美学目标 | 阶段 |
|:---:|----------|----------|----------|----------|:---:|
| Easy | 权重向量 × 0.5 + 随机噪声 ±5 | 模板驱动（偶尔犯错） | 模板驱动（偶尔浪费移动） | Submission（放松） | MVP |
| Normal | 权重向量 × 1.0 | 模板驱动 + special_tactics | 模板驱动 | Challenge（战术挑战） | **MVP 标准** |
| Hard | 权重向量 × 1.0 + 精确 estimated_dpr | 最优选择 | 最优位置 | Challenge（高难度） | Phase 2 |
| Tactical | 全局协调 + 异步计算 | 配合队友 | 利用一切地形 | (Boss专用) | **推迟至 Phase 2** |

> **Tactical 推迟理由**: "全局最优"搜索的估算计算量为 8-35ms（15敌人+大型网格），超过 16.67ms 帧预算。Phase 2 需配合异步 AI 架构重新设计。MVP 仅交付 Easy + Normal。

### Interactions

| 系统 | 交互 | 说明 |
|------|------|------|
| 角色系统 (01) | 读取敌人/玩家属性用于打分和目标评估 | 硬依赖 |
| 条件效果 (10) | 查询目标/自身条件以影响决策（如恐慌→不靠近、麻痹→优先攻击） | 硬依赖 |
| 战斗系统 (04) | 提供回合控制、移动/行动API；AI决策驱动敌人行动 | 硬依赖 |
| 地图探索 (05) | 读取地形数据用于位置选择（掩体/高地/困难地形） | 软依赖 |

## Formulas

### 目标选择打分（有界化 + 权重向量）

```
target_score = (distance_score × w_distance) + (hp_score × w_hp) + (threat_score × w_threat) + (special_score × w_special)

分项定义（全部有界）:
  distance_score = max(0, 20 - distance_in_feet)
    → 取值范围: 0 ~ 20 (distance_in_feet 为寻路距离, 非欧氏距离)

  hp_score = (1 - target.current_hp / target.max_hp) × 15
    → 取值范围: 0 ~ 15
    → 过滤: target.current_hp ≤ 0 的目标直接跳过（已死亡/濒死）

  threat_score = min(target.estimated_dpr × 1.0, 20)
    → 取值范围: 0 ~ 20 (硬上限, 防止威胁分主导)
    → estimated_dpr 计算方法见下方 §estimated_dpr

  special_score =
    +10 if target.is_concentrating
    +8  if target.is_healer
    -5  if target.has_condition("poisoned")
    +5  if target.has_condition("paralyzed") or target.has_condition("unconscious")
    → 取值范围: -5 ~ +18

权重向量 w_distance/w_hp/w_threat/w_special = 行为类型对应的权重 (见 Detailed Design §行为类型权重向量表)
```

### estimated_dpr（威胁估算）

```
estimated_dpr = Σ(attack.hit_chance × attack.avg_damage)  // 遍历目标所有攻击

hit_chance = clamp((attack_bonus - target_ac + 10.5) / 20, 0.05, 0.95)
  → attack_bonus: 从角色系统 StatBlock.attacks[].attack_bonus 读取
  → target_ac: 目标当前 AC
  → 约束: 命中率区间 [5%, 95%]（自然1必失/自然20必中）

avg_damage = Σ(dice_max × count / 2 + 0.5 × count) + flat_bonus
  → 示例: 1d6+2 → (6×1/2 + 0.5) + 2 = 3.5 + 2 = 5.5
  → 暴击不单独计算（MVP 简化——暴击最大化规则在 Hard 难度加入）

数据源: ICombatant.StatBlock.attacks[] 数组
缓存策略: 战斗开始时预计算一次, 存入 ThreatCache (Dictionary<combatant_id, float>)
更新时机: 目标 AC 变化或攻击列表变化时重算（换武器/附魔变更）；HP 变化不重算 DPR
复杂度: O(M) 每场战斗, 非 O(N×M) 每轮
```

### 治疗者目标选择（友方打分）

治疗者不使用上述 target_score 公式——使用独立的友方评分：

```
ally_heal_score = hp_urgency_score + role_priority_score + positioning_score

hp_urgency_score = (1 - ally.current_hp / ally.max_hp) × 20
  → 取值范围: 0 ~ 20 (HP 越低分数越高)
  → 过滤: ally.current_hp ≤ 0 → 不可治疗（已死亡）

role_priority_score:
  +8 if ally.role == "tank"        // 优先保护前排
  +6 if ally.role == "dpr_dealer"  // 保护输出
  +4 if ally.role == "support"     // 保护辅助
  +0 if ally.role == "other"

positioning_score:
  +3 if ally.is_adjacent_to_self           // 邻接→容易治疗
  -5 if ally.is_behind_enemy_lines         // 被包围→难接近
  +0 if ally.is_reachable_this_turn        // 可到达
  -999 if ally.is_unreachable_this_turn    // 不可到达→跳过

自保覆写:
  if healer.current_hp / healer.max_hp < 0.3:
    self_heal_priority = 999  // 强制自保, 覆盖其他一切
```

### 位置选择（Tile 评分）

```
position_score = Σ(factor_i × importance_weight)  // 对移动范围内每个可达 tile 评分

因子定义:
  proximity_to_target: max(0, speed - distance_to_target) × 2.0
    → 奖励靠近意向目标的 tile

  flank_angle: flank_bonus × 3.0
    → flank_bonus = 1.0 如果 tile 与意向目标的对角线夹角 ≤ 45°; 否则 0
    → 策略: "侧翼位置"

  cover_available: 1.0 → 半掩体; 2.0 → 3/4掩体; 3.0 → 全掩体
    → 从地图系统 terrain_features[] 读取掩体数据
    → 策略: "利用掩体"

  terrain_penalty: -2 × 困难地形格数; -5 × 不可通行格
    → 策略: "避免困难地形"

  adjacency_to_ally: +2 如果在 ally_melee 或 ally_ranged 邻接格
    → 策略: "保护后排"——近战站在队友前方

  threat_zone_avoidance: -10 如果 tile 在多个玩家触及范围内
    → 策略: "避免被包围"

评价策略: 遍历移动范围内的所有可达 tile, 取最高分 tile
性能约束: 移动范围 ≤ 6 格 (30ft) 时评估 ≤ 113 个 tile; >6 格时采样 (每方向取 Top-5)
Fallback: 如果无可达 tile → 原地待命, 跳过移动阶段
```

### 撤退判定

```
should_retreat = (enemy.current_hp / enemy.max_hp) < retreat_threshold
  - 不同行为类型有不同的 retreat_threshold (见权重向量表)
  - 撤退触发时机: 每回合 Step 1 态势评估阶段检查
  - 被标记 no_retreat 的敌人（如 undead_soldier）忽略此判定
  - 撤退条件: enemy_team_survivors ≥ 2（至少还有1个友方在场间接应）
    → 仅剩1个敌人时：死战不退（戏剧性效果）
  - 撤退执行: 消耗动作使用 Disengage, 在移动阶段向最近战场边界移动（消耗全部速度）
    → 如果 Disengage 不可用（已消耗动作）→ 赌移动触发借机攻击
  - 撤退后: 标记 combatant.has_fled = true, 战斗系统从参战列表中移除

edge case: 如果 HP 从 >threshold 被一击打到 0 → 无撤退机会（已死亡）
edge case: 如果撤退路径被完全阻挡 → 放弃撤退, 本回合选择攻击行动
```

## Edge Cases

- **若所有目标均不可达（被地形阻断）**: 选择最近可达位置移动，本回合跳过攻击
- **若所有目标 current_hp ≤ 0**: 所有存活目标均已死亡/濒死——跳过目标选择，进入"胜利庆祝"状态
- **若敌人被恐慌且恐慌源是唯一可选目标**: 攻击劣势 + 不能移动靠近（恐慌规则优先于位置评分）
- **若多个目标 score 完全相同**: 取先攻值最高的目标（打破平局）；若先攻也相同 → 取 distance 最近的目标
- **若敌人同时满足多个行为类型（如 Boss 阶段切换）**: Boss 阶段定义的 `behavior.type` 优先于基础类型——切换时重新加载权重向量（见 Open Questions §Boss 多阶段 AI）
- **若治疗者自身 HP<30% 且存在受伤队友**: ally_heal_score 自保覆写触发（self_heal_priority=999），强制自保
- **若治疗者自身 HP<30% 且无其他受伤队友**: 治疗自己（唯一选择）——不攻击
- **若所有近战敌人死亡，仅剩远程**: 远程敌人改变位置策略 → 边退边打（距离权重临时 × 1.5），不再固守后方
- **若玩家全灭**: 敌人停止攻击，播放胜利动画
- **若撤退路径被完全阻挡且 Disengage 不可用**: 放弃撤退，本回合选择攻击行动（死战）
- **若 enemy_team_survivors = 1（最后一个敌人）**: 无论 HP 多少，不触发撤退——死战到底（戏剧性效果）
- **若意向 tile 被其他单位占据**: 取 position_score 第二高分的 tile；若所有可达 tile 被占据 → 原地待命
- **若 HP 从 >threshold 被一击打到 0**: 无撤退机会（已阵亡）——撤退检查在回合开始时触发

## Dependencies

| 系统 | 方向 | 说明 |
|------|:--:|------|
| 角色系统 (01) | ← | 读取属性、HP、StatBlock.attacks[]（estimated_dpr 数据源）、法术位 |
| 条件效果 (10) | ← | 查询目标/自身条件影响决策（恐慌/麻痹/目盲等 14 种条件） |
| 战斗系统 (04) | → | AI决策驱动敌人行动执行；04§11.3.1 为行为类型映射权威源 |
| 地图探索 (05) | ← | 读取 terrain_features[] 掩体/困难地形数据辅助位置选择；⚠️ 双向引用待 05 更新 |

> **注意**: `05-map-exploration.md` 当前未包含敌人AI引用。需要在 `05` 的 Dependencies 节添加本系统的双向引用，并在 `combat` 房间节点定义中暴露 `terrain_features` 查询接口。

## Tuning Knobs

### 全局参数

| 参数 | 默认值 | 安全范围 | 说明 |
|------|:-----:|:--------:|------|
| 威胁分硬上限 | 20 | 15-30 | threat_score = min(estimated_dpr × 1.0, cap) |
| 集火命中加成 | +2 | +1 ~ +3 | 协作集火的命中奖励（非叠加，固定+2） |
| 默认难度 | Normal | Easy/Normal | MVP 阶段的 AI 难度 |
| Easy 噪声幅度 | ±5 | ±3 ~ ±8 | Easy 模式打分公式的随机噪声范围 |
| Easy 权重缩放 | 0.5 | 0.3-0.7 | Easy 模式权重向量缩放系数 |
| 位置评估 tile 数上限 | 113 | 50-200 | 移动范围 ≤ 6 格时全评估；>6 格时采样 |

### 行为类型权重向量（每类型独立可调）

| 行为类型 | 距离 | HP | 威胁 | 特殊 | 撤退阈值 |
|:---|:---:|:---:|:---:|:---:|:---:|
| `basic_melee` | 1.0 | 0.5 | 0.3 | 0.5 | 20% |
| `berserker` | 0.3 | 1.0 | 0.2 | 0.3 | 15% |
| `skirmisher` | 0.8 | 0.8 | 0.4 | 0.6 | 30% |
| `undead_soldier` | 1.2 | 0.0 | 0.0 | 0.0 | 无 |
| `caster` | 0.2 | 0.6 | 0.8 | 1.0 | 25% |
| `assassin` | 0.6 | 1.2 | 0.1 | 1.5 | 50% |

### 特殊分权重

| 参数 | 默认值 | 安全范围 | 说明 |
|------|:-----:|:--------:|------|
| 专注打断加分 | +10 | +5 ~ +15 | 攻击专注中目标的特殊分 |
| 治疗者识别加分 | +8 | +5 ~ +12 | 识别治疗者角色的特殊分 |
| 中毒降分 | -5 | -3 ~ -8 | 已中毒目标降低优先级 |
| 麻痹/昏迷加分 | +5 | +3 ~ +8 | 优先攻击麻痹/昏迷目标（自动暴击） |

### 位置选择权重

| 参数 | 默认值 | 说明 |
|------|:-----:|------|
| 接近目标权重 | 2.0 | proximity_to_target 因子 |
| 侧翼权重 | 3.0 | flank_angle 因子 |
| 保护队友权重 | 2.0 | adjacency_to_ally 因子 |
| 威胁区惩罚 | -10 | threat_zone_avoidance 因子 |

## Acceptance Criteria

> **测试原则**: 所有 AC 验证**行为**（玩家可观察的选择），不验证内部打分。需要 CombatContextBuilder 注入可控的战场状态。

- **AC-1 (skirmisher 个性)**: **GIVEN** 一个哥布林（`skirmisher`）、战士（距离 2 格, HP 100%）和法师（距离 4 格, HP 30%），**WHEN** 哥布林选择目标，**THEN** 哥布林选择低 HP 的法师而非更近的战士（skirmisher 的 hp_weight=0.8 > distance_weight 的效果）
- **AC-2 (undead_soldier 个性)**: **GIVEN** 一个骷髅（`undead_soldier`）、战士（距离 3 格, HP 100%）和法师（距离 5 格, HP 20%, DPR 高），**WHEN** 骷髅选择目标，**THEN** 骷髅选择最近的战士而非低 HP 的法师（threat=0, hp=0 → 只看距离）
- **AC-3 (berserker 个性)**: **GIVEN** 一个巨魔（`berserker`）、战士（距离 2 格, HP 80%）和治疗者（距离 3 格, HP 40%），**WHEN** 巨魔选择目标，**THEN** 巨魔优先选择低 HP 的治疗者（hp_weight=1.0 主导）
- **AC-4 (集火协作)**: **GIVEN** 2 个哥布林 + 同一法师目标（两者 Phase 1 均意向该法师），**WHEN** Phase 2 集火协调生效，**THEN** 第二个哥布林的攻击检定获得 +2 命中加成
- **AC-5 (no_retreat)**: **GIVEN** 一个 HP<15% 的骷髅（`undead_soldier`, retreat_threshold=无），**WHEN** 骷髅的回合开始时，**THEN** 骷髅执行攻击行动（不触发撤退），与同 HP 的哥布林（应撤退）形成对比
- **AC-6 (恐慌不靠近)**: **GIVEN** 一个被恐慌的敌人位于 (0,0)，恐慌源在 (5,0)，存在远离恐慌源的可达 tile (0,2)，**WHEN** 敌人选择位置，**THEN** 敌人选择 (0,2) 而非靠近恐慌源的任何 tile
- **AC-7 (healer 自保覆写)**: **GIVEN** 一个治疗者（自身 HP 20%）和一个受伤坦克队友（HP 40%），**WHEN** 治疗者选择目标，**THEN** 治疗者选择治疗自己（self_heal_priority=999 覆写）而非队友

## Open Questions

- **Boss 多阶段 AI**: Boss 阶段切换时使用全新行为类型和权重向量——每个阶段有独立配置（如巨魔 P1=`berserker`, P2=`caster`）。Boss stat block 的 `phases[]` 字段定义各阶段的 `behavior.type` 和触发条件。**裁决 (2026-05-10 CD)**: 采用独立权重向量方案，Boss 阶段切换时重新加载。
- **AI 作弊边界**: Hard/Tactical 难度下 AI 是否可"预知"玩家行动？**裁决 (2026-05-10 CD)**: 不可——AI 不应访问玩家计划中的行动。Hard/Tactical 的区别仅在于决策精度（全精确 estimated_dpr 计算 vs 简化估算），而非信息不对称。
- **Tactical 难度异步化**: Tactical 推迟至 Phase 2，需配合异步 AI 架构（状态快照 + 后台计算）。Phase 2 开始前需制定 Tactical 的复杂度预算和降级策略。
- **条件覆盖完整性**: 当前仅明确定义 5 种条件的 AI 交互（恐慌/目盲/中毒/麻痹/昏迷）。剩余 9 种 DND 条件的交互规则需在实现前补全（Charm→不攻击魅惑源; Incapacitated/Stunned/Petrified→跳过AI; Invisible→不可瞄准; Grappled/Restrained→跳过位置选择; Prone→近战加分/远程减分; Deafened→不影响AI）。**建议**: 实现时通过条件→AI行为映射表驱动，而非硬编码。
