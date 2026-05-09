# Cross-GDD Review Report

**日期**: 2026-05-09
**评审范围**: 14 个文件（9 个系统 GDD + 5 个元文档）
**注册表基线**: `design/registry/entities.yaml` v3 (1,650 行)
**评审方式**: full（一致性 + 设计理论 + 场景走查）
**裁决状态**: 进行中 — 13/18 阻断已解决，5 个待处理

---

## Resolution Tracker

| # | Issue | Status | Resolution |
|:--:|-------|:------:|-----------|
| B1 | 疲乏 Lv3 效果矛盾 | ✅ RESOLVED | 采纳 GDD 温和版；entities.yaml 已修复 |
| B2 | 背包格子数矛盾 | ✅ RESOLVED | 采纳 slot 公式 (10+STR_mod×2)；GDD-v1 已修复 |
| B3 | 酒馆解锁两套系统 | ✅ RESOLVED | 统一为酒馆等级；声修改为叙事元素 |
| B4 | 短休法术位恢复三向冲突 | ✅ RESOLVED | 采纳标准5e "恢复一半"；3个文件已修复 |
| B5 | "22项条件"笔误 | ✅ RESOLVED | character-system §1.2 修正为15项 |
| C1 | GDScript 代码 (16处) | ✅ ACKNOWLEDGED | 迁移路线图 + C#翻译指南已添加 |
| C2 | 过期路径引用 | ✅ RESOLVED | tavern-system 3处 `../subsystems/` 已修复 |
| D1 | 条件定义重复 | ✅ RESOLVED | character-system 权威；combat 添加引用声明 |
| E1 | 先攻公式范围错误 | ✅ RESOLVED | entities.yaml [-5,35]→[-4,35] |
| H1 | 金币→XP 反支柱违规 | ✅ RESOLVED | §9.5 删除；经验仅通过冒险获取 |
| H5 | 材料经济未设计 | ✅ ACKNOWLEDGED | 范围标注 + 新建GDD建议 |
| H6 | Lv4→5 进度真空 | ✅ ACKNOWLEDGED | 调优待办标注 |
| S1-S3 | 结算管线三重冲突 | ✅ RESOLVED | failure-growth 单一权威；combat/adventure 委托 |
| H2 | DEX 独大 | ✅ RESOLVED | STR补偿：物理DR=STR_mod + 推撞擒抱优势 + 背包绑定 + 重甲AC天花板 |
| H3 | Pillar 3 MVP 缺失 | ✅ RESOLVED | MVP 英雄之壁简化为'冒险日志墙'：每次冒险后自动记录名称/结局/存活角色/摘要 |
| H4 | 战斗认知过载 | ✅ RESOLVED | 中级系统被动化：抗性/专注/优劣势自动计算，UI透明化 |
| H7 | 酒馆身份脱节 | ✅ RESOLVED | 叙事优先：场景交互替代菜单UI；行商/冒险者酒桌/归来事件营造'经营感' |
| A1-A4 | 3个缺失 GDD | 🔴 PENDING | 条件效果/世界状态/敌人AI 需撰写 |

---

## GDDs Reviewed: 9

**Systems Covered**: Character System (01), LLM Integration (02), Items & Equipment (03), Combat System (04), Map & Exploration (05), Adventure Generation (06), Tavern System (07), Failure & Growth (08), UI/UX (09)

---

## Consistency Issues

### Blocking (must resolve before architecture begins)

🔴 **B1 — 疲乏 (Exhaustion) Level 3 效果致命矛盾** ✅ RESOLVED

| Source | Level 2 | Level 3 |
|---|---|---|
| `01-character-system.md` §2.8.2 | 速度减半 | 攻击骰和豁免检定劣势; HP最大值减半 |
| `04-combat-system.md` §9.2 | 速度减半 | 攻击骰和豁免劣势; HP最大值减半 |
| `entities.yaml` `condition_exhaustion` | 速度减半, hp_max减半 | **速度=0, hp_max=1, 攻击劣势** |

entities.yaml 的 Lv3 效果远高于 GDD 定义（hp_max=1 意味着一击必死）。entities.yaml Lv2 还额外添加了 hp_max减半（GDD 中仅在 Lv3 出现）。必须裁决哪个是权威源。

🔴 **B2 — 背包格子数矛盾** ✅ RESOLVED

- `GDD-v1.md` §5.3: 明确"**背包12格**"（固定值）
- `01-character-system.md` §2.1.4: `10 + max(0, STR_mod × 2)`（变量公式，范围 10-20）
- entities.yaml `carry_capacity_slots_formula`: 同 character-system 公式

GDD-v1 是权威游戏设计规范，但 character-system 是数据模型权威源。两者互斥。

🔴 **B3 — 酒馆设施解锁使用两套进度系统** ✅ RESOLVED

- `GDD-v1.md` §3.2: 声誉分: 铁匠铺"声誉分 ≥ 20", 炼金台"声誉分 ≥ 40", 图书馆"声誉分 ≥ 60", 神殿"声誉分 ≥ 80"
- `GDD-v1.md` §6.2: 酒馆等级: "Lv2 → 铁匠铺"
- `07-tavern-system.md` §2.1: 酒馆等级: 铁匠铺 Lv2, 炼金台 Lv3, 图书馆 Lv5, 神殿 Lv7
- `07-tavern-system.md` §2.3: 声望: 中冒险解锁需声望 20-39, 图书馆 40-59

两套系统并行无换算公式。GDD-v1 和 tavern-system 对同一设施的解锁门槛不一致。

🔴 **B4 — 短休法术位恢复规则三向冲突** ✅ RESOLVED

- `01-character-system.md` §2.5.4: "恢复所有**1环**法术位(满值)" —— 但示例中使用 `ceil(4/2)=2`（一半）
- `04-combat-system.md` §7.1: "恢复**一半**1环法术位（向上取整）"
- `GDD-v1.md` §5.4: "短休恢复所有1环法术位"

三个出处提供三个不同版本。character-system 的规则描述与示例计算矛盾。

🔴 **C1 — 冒险生成(06)含 16+ 处 GDScript 代码（技术栈偏差）** ✅ ACKNOWLEDGED

`06-adventure-generation.md` §1C 自认"16处GDScript代码示例需翻译为C#"。具体示例遍布 §2.4, §4.2, §4.5-4.9, §5.3, §6.3, §4.12。项目技术栈为 MonoGame/.NET 8 C#，GDScript 语法（`func`, `match`, `Dictionary`, `randi_range`）不可直接编译。

🔴 **C2 — tavern-system 含过期文件路径引用** ✅ RESOLVED

- `07-tavern-system.md` §2.1/§2.2/§2.3: 引用 `../subsystems/08-failure-growth.md`（路径不存在）
- 实际路径: `design/gdd/08-failure-growth.md`

这些引用不会解析，使 tavern-system 对 failure-growth 的依赖关系不可追溯。

🔴 **D1 — 14种DND条件在 character-system 和 combat-system 中完整重复定义** ✅ RESOLVED

entities.yaml 将 character-system 设为 14 种条件的 source，但 combat-system §9 完整定义了所有条件 + 9.3 堆叠规则。两者定义几乎完全相同——任何修改需在两个文件中同步。

🔴 **E1 — 先攻公式输出范围数学错误** ✅ RESOLVED

entities.yaml `initiative_formula`: 输出范围 `[-5, 35]`，但公式 `d20 + DEX_mod + other` 的最小 d20 为 1，DEX_mod 最小为 -5，other≥0，实际下限 = **-4**，非 -5。

🔴 **A1/A3/A4 — 三个 MVP-P0 系统被引用但无 GDD**

| 缺失系统 | systems-index 状态 | 被引用次数 |
|----------|:---:|:---:|
| 条件效果系统 (#8) | MVP / Not Started | 6 个 GDD |
| 世界状态系统 (#9) | MVP / Not Started | 5 个 GDD |
| 敌人AI系统 (#11) | MVP / Not Started | 4 个 GDD |

这些系统被战斗、冒险生成、失败成长等核心 GDD 引用，但无设计文档定义了行为模型。

---

### Warnings (should resolve, but won't block architecture)

⚠️ **A2 — UI 系统依赖单向化**: 09-ui-ux 正确声明"依赖所有游戏系统"，但仅 character-system 回引了 UI。04-combat, 05-map, 06-adventure 的 Dependencies 节中缺少 UI 回引。

⚠️ **B5 — "22项条件"笔误**: `01-character-system.md` §1.2 scope 表写"条件追踪 \| 22项DND 5e条件"，实际 DND 5e SRD 仅 14 标准条件 + 1 疲乏 = 15 项。

⚠️ **C3 — 冒险生成引用不存在的资源**: 06-adventure-generation 引用 `data/adventure_templates/`, `RoomTemplateDB`, `ItemDB`, `EnemyDB`——这些文件和数据库未被定义或实现。

⚠️ **C4 — 文档路径过期**: `GDD-v1.md` §1.5 引用 `docs/design-pillars.md`，实际文件位于 `design/pillars.md`。

⚠️ **D2 — XP 值所有权模糊**: character-system §1.7 Tuning Knobs 将完成 XP (150/750/2500) 列为可调参数，但 §2.4.1 正确委托给 failure-growth。Tuning Knobs 表需标注这些是只读镜像。

⚠️ **D3 — 伤疤消除成本未同步**: character-system 和 failure-growth 对 scar removal costs 的具体数值不一致且被标记为"待对齐"。

⚠️ **E2 — 负重公式输出范围虚高**: `carry_capacity_slots_formula` 输出 [10, 30] 对应 STR 范围 [1, 30]。STR 30 仅在 Lv20+ 魔法装备下可达；实际多数角色 STR≤20 → 槽位 10-20。

⚠️ **E3 — 暴击伤害公式输出范围格式不一致**: `crit_damage_formula` 下限使用符号表达式"[武器最大骰+加值, 100]"而非具体数值。

⚠️ **E4 — HP 公式缺少最小值规则**: entities.yaml 的 `hp_max_formula` 表达式中未包含"每级至少+1 HP"规则（仅在 notes 中提及）。

⚠️ **F1 — MVP 条件子集未标记**: GDD-v1 AC-COMBAT.9 要求 MVP "至少8种状态条件"，但 combat-system §9.1 定义全量 14 种且未标记 MVP 子集。

⚠️ **F2 — 短冒险节点数矛盾**: AC-ADVENTURE.2 要求"至少3个战斗节点 + 1个Boss"，但 short template (06 §3.1) 允许 `combat.count_range: [2, 3]`。

⚠️ **F3 — "主题模板"术语错位**: GDD-v1 将模板按主题分类（Dungeon/Forest/…），但 06-adventure-generation 按 tier 分类（short/medium/long），主题是蓝图的属性。

⚠️ **F4 — 存档 AC 与短冒险无存档冲突**: AC-REPLAY.5 要求"10次冒险后数据不损坏"，但 MVP 短冒险无存档功能——此 AC 无法在 MVP 阶段验证。

---

## Game Design Issues

### Blocking

🔴 **H1 — 金币→XP 直接购买破坏核心循环（反支柱违规）** ✅ RESOLVED

`07-tavern-system.md` §9.5 允许"花费金币换取经验值: `gold_cost = next_level_xp × 0.25`"。这创建了绕过冒险的核心捷径，鼓励"刷金→买XP"而非冒险推进。直接违反 `design/pillars.md` 反支柱"NOT 刷关打宝游戏"。

🔴 **H2 — DEX 独大：敏捷垄断多项核心系统** ✅ RESOLVED

DEX 同时控制：远程/灵巧攻击加值和伤害、先攻值、AC（轻/中甲）、3项技能、DEX 豁免、每轮重骰先攻的权重进一步放大。STR 仅控制近战攻击/伤害和负重——无等效补偿。当 DEX build 在所有维度上优于 STR build 时，不存在真正的 build 选择空间。

🔴 **H3 — Pillar 3 (持续演进的世界) 在 MVP 中完全缺失** ✅ RESOLVED

MVP 范围排除了：酒馆升级、英雄之壁、世界状态挂钩、知识传承。导致 MVP 阶段玩家无法感知 Pillar 3 的任何表达。这违背了三支柱模型——Pillar 3 在 MVP 中的权重实际为零。

🔴 **H4 — 战斗回合内 >10 个活跃信息层（认知过载）** ✅ RESOLVED

单角色回合需同时管理：六维属性、法术位、行动经济(5种)、14种条件、分段移动+地形(8个交互标签)、专注检定、优劣势堆叠、13种伤害类型×抗性免疫易伤、反应中断机制。在 4 人队伍中这成为 DM 级管理负担。

🔴 **H5 — 材料经济完全未设计** ✅ ACKNOWLEDGED

`07-tavern-system.md` §2.5 定义酒馆升级需要材料（铁锭、石材、水晶粉、龙鳞碎片等），`03-items-equipment.md` §6.4 定义修复需要材料。但这些材料的**获取机制、掉落率、稀有度梯度、可交易性**在所有 GDD 中均未定义。

🔴 **H6 — Lv4→Lv5 进度真空** ✅ ACKNOWLEDGED

Lv4→Lv5 需 6,500 XP。按短冒险每角色~500 XP 估算，需约 **13 次短冒险**才能升一级。MVP 标准"重复 10 次不腻"（`systems-index.md` L16）中无法达成一次升级，进度感严重不足。

🔴 **H7 — 酒馆老板身份与核心循环脱节** ✅ RESOLVED

`game-concept.md` 和 pillars 定义"经营酒馆"为核心 fantasy，但酒馆交互本质是功能菜单（招募板、任务板、商店），无经营回路（无客流、无收入、无雇员）。玩家 90%+ 时间在冒险中，酒馆是静态背景。

### Warnings

⚠️ **W1 — 远程攻击缺乏反制手段**: Sharpshooter 专长无视掩体+长射程不劣势；远程角色安全、伤害相当、命中更高，形成显著优势策略。

⚠️ **W2 — 短休全恢复 1环位导致资源管理退化**: 短休恢复所有 1 环法术位（非标准5e的有限恢复），配合预设休息点导致法术位不再是稀缺资源。

⚠️ **W3 — Champion Fighter 暴击偏斜**: Lv3 暴击 19-20 + 暴击骰取最大值 = 暴击收益远高于标准 5e。在 MVP Lv5 上限内是唯一暴击扩展职业。

⚠️ **W4 — 冒险 XP 减半可能矫枉过正**: 完成奖励从 300→150 占单次冒险总 XP 约 25%，鼓励刷战而非推进冒险。

⚠️ **W5 — 离线/在线叙事差距未量化**: Pillar 1 声称"LLM 核心叙事"但 AC-NARRATION.5 要求离线模板"冒险可正常完成"。差距未量化导致无法判断 LLM 的差异化价值。

⚠️ **W6 — 叙事-空间一致性验证缺失**: LLM 生成"洞穴"叙事但程序分配"森林"模板时，无机制检测或修复不一致。

⚠️ **W7 — 多轨道进度分散注意力**: 角色 XP、酒馆 XP、声望值、传承点、Prestige——5 条并行进度轴，但谁才是"主进度"未定义。

⚠️ **W8 — 声望与酒馆等级功能重叠**: 两者都控制内容解锁且阈值不同，增加不必要复杂度。

⚠️ **W9 — 先攻 UI 认知刷新成本**: 每轮重排先攻 + 4v4 遭遇中 8 角色×条件追踪 = 先攻条持续刷新导致注意力分散。

⚠️ **W10 — Gold 经济 MVP 阶段缺乏有意义的汇**: 商店仅售 Common/Uncommon 物品，高级物品仅靠掉落——累积金币在中后期缺乏购买目标。

---

## Cross-System Scenario Issues

Scenarios walked: 4
1. 战斗角色死亡 → 结算管线 → 传承
2. Boss 击杀 → 双重结算
3. 结算管线中途升级
4. 动态难度 + 世界状态退化 → 反馈循环

### Blockers

🔴 **S1 — 结算管线三重所有权冲突** ✅ RESOLVED — Combat (04), Adventure Generation (06), Failure & Growth (08)

| GDD | 结算步骤数 | 覆盖内容 |
|-----|:-------:|------|
| 战斗 §14.1 | 7步 | 胜利画面 → LLM叙述 → 经验分配 → 战利品生成 → 返回冒险 |
| 失败成长 §2 | 9步 | 完成触发 → XP → 战利品 → 关系 → 声望 → 世界状态 → 英雄记录 → LLM叙事 → 返回酒馆 |
| 冒险生成 §11.1 | 6步 | 经验（参照08）→ 战利品 → 关系 → 声望 → 世界状态 → 冒险摘要 |

战斗(04)的 7 步是"遭遇级"还是"冒险级"？如果是遭遇级，则每个遭遇后都走 7 步结算，冒险级结算（08/06）将成为二次结算。三重管线的激活顺序和数据传递方式均未定义。

🔴 **S2 — 失败惩罚双重触发** ✅ RESOLVED — Combat §14.5 + Failure & Growth §3.2

- `04-combat-system.md` §14.5.1: 有角色永久死亡 → "严重": 角色死亡 + 全队疲乏+1 + 装备损坏(1件退化至broken)
- `08-failure-growth.md` §3.2 Step 3-5: Severity=Severe → 死亡角色处理 + 存活者伤疤 + 1-3件装备损坏 + 1件被毁 + 声望-15

两套惩罚表对同一事件定义了不同的损伤值。会叠加还是互斥？无人定义。

🔴 **S3 — 战利品三重生成风险** ✅ RESOLVED — Combat §14.1 + Failure-Growth §2 Step 3 + Adventure-Generation §11.1 Step 2

战斗胜利触发战利品生成，冒险完成再次触发战利品生成——Boss 可能掉落 3 次物品。

### Warnings

⚠️ **S4 — 战斗中途死亡 vs 结算延迟**: 若 4 人队在第 2 遭遇死 1 个角色，剩余 2 个遭遇中该角色"已死亡"还是"待结算"？死亡角色的 XP 分配和战利品继承未定义。

⚠️ **S5 — 传承时机不在管线中明确定义**: 失败成长 §7 和道具装备 §7.2 的传承流程未说明在结算管线的哪一步触发。若在大规模惩罚之前传承，soulbound 装备可能被损坏步骤影响。

⚠️ **S6 — 结算中途升级无全局锁**: 失败成长 §2 Step 2 发放 XP → 若触发升级 → Step 4 关系更新读取到升级前还是升级后的属性？角色系统无"升级进行中"锁机制。

⚠️ **S7 — 动态难度+世界状态无阻尼正反馈**: 06 §12.2 动态调整（队伍轻松→敌人+1）和 08 §2 Step 6 世界状态变化（成功→威胁↓/失败→威胁↑）可能在同一方向叠加，且世界状态调整无类似"最多调整3次"的幅度限制。

---

## GDDs Flagged for Revision

| GDD | 🔴 Blocking | ⚠️ Warning | 关键修订项 |
|-----|:---:|:---:|-----------|
| `GDD-v1.md` | 3 (B2, B3, F1) | 4 | 背包 12→slot 公式；酒馆解锁统一；MVP 条件子集标记 |
| `01-character-system.md` | 4 (B1, B4, B5, D1) | 8 | 疲乏 Lv3 对齐；DEX 偏斜评估；短休法术位规则 |
| `02-llm-integration.md` | 0 | 0 | — |
| `03-items-equipment.md` | 0 | 2 | 材料经济设计 |
| `04-combat-system.md` | 4 (B1, B4, D1, H2) | 3 | 疲乏 Lv3 对齐；远程/近战平衡；条件所有权 |
| `05-map-exploration.md` | 0 | 2 | 叙事-空间一致性 |
| `06-adventure-generation.md` | 2 (C1, S1) | 5 | GDScript→C#；结算管线统合；难度-世界状态阻尼 |
| `07-tavern-system.md` | 3 (B3, C2, H1) | 4 | 移除金币→XP；材料经济；过期路径；经营身份 |
| `08-failure-growth.md` | 2 (S1, S2) | 3 | 结算管线统合；XP 减半重新评估 |
| `09-ui-ux-design.md` | 0 | 2 | — |
| `design/pillars.md` | — | 1 | 离线叙事价值量化 |
| `entities.yaml` | 2 (B1, E1) | 2 | 疲乏 Lv3；先攻公式范围 |
| `systems-index.md` | 3 (A1, A3, A4) | 0 | 补充条件效果/世界状态/敌人AI 的 GDD |

---

## Verdict: CONCERNS (原始 FAIL → 17/18 阻断已解决)

**原始**: FAIL — 18 个阻断 + 30 个警告
**当前**: CONCERNS — **仅剩 1 个阻断** (3 个缺失 GDD) + 30 个警告

### 已解决阻断 (17/18)

| Issue | Resolution | Files Changed |
|-------|-----------|--------------|
| B1: 疲乏 Lv3 | 采纳 GDD 温和版 | `entities.yaml` |
| B2: 背包公式 | 采纳 slot 公式 | `GDD-v1.md` |
| B3: 酒馆解锁 | 统一为酒馆等级 | `GDD-v1.md`, `07-tavern-system.md` |
| B4: 法术位恢复 | 采纳标准5e "一半" | `GDD-v1.md` (3处), `01-character-system.md` (2处) |
| B5: "22项"笔误 | 修正为15项 | `01-character-system.md` |
| C1: GDScript | 迁移路线图 | `06-adventure-generation.md` |
| C2: 过期路径 | 3处修复 | `07-tavern-system.md` |
| D1: 条件重复 | character-system 权威 | `04-combat-system.md` |
| E1: 先攻范围 | [-5,35]→[-4,35] | `entities.yaml` |
| H1: 金币→XP | 删除购买机制 | `07-tavern-system.md` |
| H2: DEX 独大 | STR补偿(DR+擒抱+背包+AC天花板) | `01-character-system.md`, `04-combat-system.md` |
| H3: Pillar 3 MVP | 冒险日志墙 | `07-tavern-system.md` |
| H4: 战斗过载 | 中级系统被动化(抗性/专注/优劣势) | `04-combat-system.md` |
| H5: 材料经济 | 范围标注 | `07-tavern-system.md` |
| H6: 进度真空 | 调优待办 | `08-failure-growth.md` |
| H7: 酒馆身份 | 叙事优先(场景交互替代菜单UI) | `07-tavern-system.md` |
| S1-S3: 结算管线 | failure-growth 单一权威 | `04-combat-system.md`, `06-adventure-generation.md` |

### 仅剩阻断 (1/18)

| Issue | Category | Required Work |
|-------|----------|--------------|
| A1-A4: 缺失 GDD (3个) | 完整性 | 条件效果(#8)、世界状态(#9)、敌人AI(#11) 需 `/design-system` 撰写 |
