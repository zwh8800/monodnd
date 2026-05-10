# 失败与成长系统 — 技术设计文档

> **Subsystem**: Failure & Growth System  
> **Game**: 《酒馆与命运》(Tavern & Destiny)  
> **Rules Reference**: DND 5e SRD (经 Roguelike 调整)
> **Language Policy**: 游戏文本统一采用简体中文，技术标识符使用英文snake_case  
> **Version**: 1.3  
> **Status**: 已修订 (MAJOR REVISION — 5项创意决策执行)  
> **对应GDD版本**: GDD-v1.0 §6

---

## 1. 概述

### 1.1 系统目的

失败与成长系统是《酒馆与命运》的**核心情感引擎**，也是游戏的**关键差异化因素**。本系统负责：

- **将失败转化为叙事内容**：每次失败都生成有意义的故事、永久后果和角色成长机会
- **建立情感重量**：通过伤疤、死亡、装备损失等永久后果，让每个决策都有真实代价
- **驱动元进度循环**：失败产生的知识传承、英雄传记和世界状态变化，为下一次冒险提供动力
- **维持长期动机**：即使全灭，玩家也能从失败中获得可量化的成长（传承点、情报标签）

### 1.2 核心设计哲学

> **"Failure is content, not punishment."**  
> **"失败是内容，不是惩罚。"**

| 设计原则 | 说明 |
|----------|------|
| **失败产生故事** | 每次失败都通过 LLM 生成独特的叙事，让玩家记住"发生了什么"而非"失去了什么" |
| **永久后果创造重量** | 伤疤、死亡、装备损失是永久的——这让胜利更甜美，决策更慎重 |
| **失败提供成长** | 知识传承、传承点、英雄传记确保失败不是"白费"，而是为未来铺路 |
| **严重程度分级** | 从轻微（金币损失）到灾难性（全灭），不同失败有不同的情感冲击和恢复路径 |
| **程序控制数值，LLM 控制叙事** | 所有机械效果由程序确定性计算，LLM 只负责生成故事文本 |

### 1.3 与其他系统的关系

```
┌─────────────────────────────────────────────────────────────┐
│                    失败与成长系统 (本系统)                     │
│                                                               │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐    │
│  │ 成功结算  │  │ 失败结算  │  │ 伤疤系统  │  │ 死亡系统  │    │
│  └────┬─────┘  └────┬─────┘  └────┬─────┘  └────┬─────┘    │
│       │              │              │              │          │
│  ┌────┴─────┐  ┌────┴─────┐  ┌────┴─────┐  ┌────┴─────┐    │
│  │ 知识传承  │  │ 英雄传记  │  │ 世界演进  │  │ 元进度    │    │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘    │
└─────────────────────────────────────────────────────────────┘
        │              │              │              │
        ▼              ▼              ▼              ▼
┌──────────────┐ ┌──────────────┐ ┌──────────────┐ ┌──────────────┐
│  Character   │ │  Tavern      │ │  Adventure   │ │  LLM         │
│  System      │ │  System      │ │  Generation  │ │  Gateway     │
│  ─────────── │ │  ─────────── │ │  ─────────── │ │  ─────────── │
│ · 伤疤写入   │ │ · 声望变化   │ │ · 世界状态   │ │ · 叙事生成   │
│ · 死亡标记   │ │ · 设施解锁   │ │   作为上下文 │ │ · 传记生成   │
│ · 关系变化   │ │ · 金币经济   │ │ · 难度调整   │ │ · 惩罚描述   │
│ · 传承数据   │ │ · 英雄之壁   │ │              │ │              │
└──────────────┘ └──────────────┘ └──────────────┘ └──────────────┘
```

**上游依赖**：
- **Character System**：读取角色 HP、装备、关系数据；写入伤疤、死亡状态、传承数据
- **Adventure Generation**：读取冒险主题、难度、完成度；提供世界状态变化作为未来冒险上下文
- **LLM Gateway**：调用 DM Agent 生成失败叙事、伤疤描述、英雄传记

**下游影响**：
- **Tavern System**：更新声望、金币、设施解锁状态
- **Combat System**：触发死亡豁免判定、伤疤效果应用

---

## 1A. 玩家体验幻想

- **"失败不会被遗忘"** — 每次失败都有代价（伤疤/死亡/装备损坏），但每次失败也有传承（知识/遗产）。
- **"死亡有重量"** — 角色永久死亡不是惩罚，而是故事的一部分。传承点让死亡成为新的开始。
- **"世界在演进"** — 冒险成败永久改变世界状态，酒馆反映这些变化。

---

## 1B. 可调参数

| 参数 | 当前值 | 安全范围 | 影响面 |
|------|:------:|:--------:|--------|
| 伤疤生成概率(重度) | 5% | 2-10% | 伤疤频率 |
| 传承点公式 | level + 5 | level + 3~7 | 遗产价值 |
| 世界状态挂钩数 | 1-2 | 1-4 | 世界演进深度 |
| 超时惩罚倍率 | ×0.8 | ×0.6~0.9 | 冒险节奏压力 |
| 复活成本(gp) | 2,000 | 1,500~3,000 | 死亡可逆性 |

---

## 1C. 依赖关系

| 依赖系统 | 内容 | 状态 |
|----------|------|:---:|
| 角色系统 | 伤疤/死亡/传承 | ✅ |
| 战斗系统 | 战斗结果 | ✅ |
| LLM网关 | 结算叙事 | ✅ |
| 酒馆系统 | 声望/元进度 | ✅ |

---

## 2. 成功结算流程

### 2.1 成功结算管线

```
冒险成功结算流程 (Adventure Success Settlement Pipeline)
==========================================================

Step 1: 冒险完成触发
  ┌─────────────────────────────────────────────────┐
  │ 触发条件:                                        │
  │   - Boss 被击败 AND                              │
  │   - 最终目标达成 (如: 宝箱获取/NPC 救出/仪式阻止) │
  │                                                   │
  │ 验证: adventure.completion_status = "success"     │
  │ 发出信号: adventure_completed(adventure_id, true) │
  └───────────────────────┬─────────────────────────┘
                          ▼
Step 2: XP 计算与分配
  ┌─────────────────────────────────────────────────┐
  │ 1. 计算每个遭遇的基础 XP (见 §2.2)               │
  │ 2. 应用参与度乘数 (见 §2.3)                      │
  │ 3. 应用目标奖励 (见 §2.4)                        │
  │ 4. 应用速度奖励 (见 §2.5)                        │
  │ 5. 分配给存活角色 (见 §2.6)                      │
  │                                                   │
  │ 输出: 每个角色获得的 XP 数值                      │
  │ 发出信号: xp_distributed(character_id, xp_amount) │
  └───────────────────────┬─────────────────────────┘
                          ▼
Step 3: 战利品生成与鉴定
  ┌─────────────────────────────────────────────────┐
  │ 1. 根据 loot_tier 从战利品池抽取物品              │
  │ 2. 程序生成物品数值属性                           │
  │ 3. 调用 Copywriter Agent 生成物品描述             │
  │ 4. 鉴定流程 (未鉴定物品需消耗行动识别)            │
  │                                                   │
  │ 输出: 物品列表 + 金币奖励                         │
  │ 发出信号: loot_generated(items_array)             │
  └───────────────────────┬─────────────────────────┘
                          ▼
Step 4: 角色关系更新
  ┌─────────────────────────────────────────────────┐
  │ 1. 统计冒险中的关系触发事件                       │
  │ 2. 计算关系值变化                                 │
  │ 3. 检查关系阈值跨越                               │
  │ 4. 更新关系标签和效果                             │
  │                                                   │
  │ 输出: 关系变化列表                                │
  │ 发出信号: relationship_changed(...)               │
  └───────────────────────┬─────────────────────────┘
                          ▼
Step 5: 酒馆声望提升
  ┌─────────────────────────────────────────────────┐
  │ 声望增加 = 基础值 × 难度系数                     │
  │   短冒险: +5 声望                                │
  │   中冒险: +10 声望                               │
  │   长冒险: +15 声望                               │
  │   难度系数: Easy ×0.8 / Normal ×1.0 / Hard ×1.3 │
  │                                                   │
  │ 输出: 新声望值                                    │
  │ 发出信号: reputation_changed(old, new)            │
  └───────────────────────┬─────────────────────────┘
                          ▼
Step 6: 世界状态正向变化
  ┌─────────────────────────────────────────────────┐
  │ 1. 读取冒险蓝图中的 world_state_hooks             │
  │ 2. 执行 on_success 钩子                          │
  │ 3. 更新区域状态 (如: 威胁→安全)                  │
  │ 4. 更新 NPC 状态 (如: 新 NPC 出现)              │
  │ 5. 更新势力关系 (如: 友好度提升)                 │
  │                                                   │
  │ 输出: 世界状态变更列表                            │
  │ 发出信号: world_state_mutated(changes)            │
  └───────────────────────┬─────────────────────────┘
                          ▼
Step 7: 英雄成就记录
  ┌─────────────────────────────────────────────────┐
  │ 1. 更新角色 adventure_log                        │
  │ 2. 记录关键事件到 memorable_events               │
  │ 3. 更新战斗统计 (击杀/伤害/暴击)                 │
  │ 4. 检查知识标签获取条件                           │
  │                                                   │
  │ 输出: 更新后的角色数据                            │
  └───────────────────────┬─────────────────────────┘
                          ▼
Step 8: LLM 胜利叙事生成
  ┌─────────────────────────────────────────────────┐
  │ 调用 DM Agent 生成胜利叙事                       │
  │ 输入: 冒险摘要 + 关键战斗 + 角色表现             │
  │ 输出: 2-3 段胜利叙事文本                         │
  │                                                   │
  │ 显示: 冒险结算界面的叙事区域                      │
  └───────────────────────┬─────────────────────────┘
                          ▼
Step 9: 返回酒馆
  ┌─────────────────────────────────────────────────┐
  │ 1. 切换到酒馆场景                                │
  │ 2. 显示结算界面 (XP/战利品/关系/声望)            │
  │ 3. 播放胜利 BGM                                  │
  │ 4. 触发酒馆事件 (可选)                           │
  └─────────────────────────────────────────────────┘
```

### 2.2 XP 计算公式

#### 2.2.1 基础遭遇 XP 表

每个遭遇的基础 XP 由遭遇难度和队伍平均等级决定：

| 遭遇难度 | 每角色基础 XP | 说明 |
|----------|:------------:|------|
| Easy | 50 × avg_level | 轻松战斗，几乎无风险 |
| Medium | 100 × avg_level | 标准战斗，需要策略 |
| Hard | 200 × avg_level | 困难战斗，可能有人倒地 |
| Deadly | 400 × avg_level | 致命战斗，死亡风险高 |

**完整 XP 表（按队伍平均等级 × 遭遇难度）**：

| 平均等级 | Easy | Medium | Hard | Deadly |
|:--------:|:----:|:------:|:----:|:------:|
| 1 | 50 | 100 | 200 | 400 |
| 2 | 100 | 200 | 400 | 800 |
| 3 | 150 | 300 | 600 | 1,200 |
| 4 | 200 | 400 | 800 | 1,600 |
| 5 | 250 | 500 | 1,000 | 2,000 |
| 6 | 300 | 600 | 1,200 | 2,400 |
| 7 | 350 | 700 | 1,400 | 2,800 |
| 8 | 400 | 800 | 1,600 | 3,200 |
| 9 | 450 | 900 | 1,800 | 3,600 |
| 10 | 500 | 1,000 | 2,000 | 4,000 |

#### 2.2.2 参与度乘数

```
participation_multiplier = base_multiplier + team_bonus_multiplier

base_multiplier:
  - 存活至冒险结束: 1.0
  - 在最终 Boss 战中倒地但被救回: 0.9
  - 在最终 Boss 战前倒地且未被救回: 0.7

team_bonus_multiplier (可叠加, 奖励团队协作与叙事参与):
  - 存活且从未倒地: +0.1
  - 救回倒地队友次数最多者: +0.1
  - 完成最多非战斗技能检定者: +0.05
```

> **设计说明 (v1.3)**: 移除了"最高伤害输出者"和"最高治疗输出者"的bonus（v1.2曾有此设计），因为它们在合作体验中引入零和竞争。改为"救回队友"鼓励互助——这与 Pillar 1（"角色驱动的故事"、"这些人我舍不得"）对齐。控制/辅助角色（如施放 Hold Person 或 Bless）不再被系统性惩罚。

#### 2.2.3 目标奖励

```
objective_bonus = 0

side_objectives_completed:
  - 每个完成的次要目标: +10% 基础 XP
  - 最多叠加 3 个次要目标 (+30%)

example:
  基础 XP = 300 (Medium, Lv3)
  完成 2 个次要目标: 300 × (1 + 0.2) = 360 XP
```

#### 2.2.4 时间惩罚

> **设计说明 (v1.3)**: 移除了速度奖励（v1.2 的 ×1.2/×1.5 加速倍率），因为它鼓励跳过叙事内容直奔 Boss，与 Pillar 1（叙事优先）冲突。改为仅保留超时惩罚——时间是压力源而非奖励源。

```
time_penalty_multiplier = 1.0

if adventure_completed_in > expected_duration * 1.5:
  time_penalty_multiplier = 0.8  # 严重超时 -20%
elif adventure_completed_in > expected_duration * 1.2:
  time_penalty_multiplier = 0.9  # 轻度超时 -10%

# 正常时间内完成: 无惩罚 (×1.0)
# 快速完成: 无额外奖励 (鼓励探索而非竞速)
```

#### 2.2.5 总 XP 分配算法

```gdscript
func calculate_xp_for_character(
    character: CharacterResource,
    encounters: Array[Dictionary],
    adventure_meta: Dictionary,
    party_stats: Dictionary
) -> int:
    var total_xp = 0
    
    # Step 1: 基础遭遇 XP
    for encounter in encounters:
        var base_xp = get_base_xp(encounter.difficulty, party_stats.avg_level)
        total_xp += base_xp
    
    # Step 2: 参与度乘数
    var participation = calculate_participation_multiplier(character, party_stats)
    total_xp = int(total_xp * participation)
    
    # Step 3: 目标奖励
    var objective_bonus = adventure_meta.side_objectives_completed * 0.10
    total_xp = int(total_xp * (1.0 + min(objective_bonus, 0.30)))
    
    # Step 4: 时间惩罚
    var time_penalty = get_time_penalty(
        adventure_meta.actual_duration,
        adventure_meta.expected_duration
    )
    total_xp = int(total_xp * time_penalty)
    
    # Step 5: 冒险完成基础奖励
    var completion_xp = get_completion_xp(adventure_meta.tier)
    total_xp += completion_xp
    
    return total_xp

func get_completion_xp(tier: String) -> int:
    match tier:
        "short": return 150
        "medium": return 750
        "long": return 2500
        _: return 0
```

> **设计说明 (v1.1)**: 完成奖励已减半，以确保遭遇战XP是主要经验来源，而非"rush到Boss完成冒险"。原始值(300/1500/5000)导致长冒险完成奖励覆盖Lv4→5差距的77%，使遭遇战XP变得微不足道。
>
> **⚠️ 调优待办 (2026-05-09, `/review-all-gdds`)**: Lv4→Lv5需6,500 XP，按短冒险每角色~500 XP/次估算需约13次冒险。在MVP仅提供短冒险（中冒险需酒馆Lv4解锁）的前提下，Lv4后的进度真空违反MVP标准"重复10次不腻"。建议在MVP测试中监控实际升级速率，必要时调高完成奖励或降低Lv4→5阈值。

### 2.3 成功结算示例

**场景**：4 人队伍（平均等级 3）完成一个中等难度的短冒险

```
遭遇统计:
  - 3 个 Medium 遭遇: 3 × 300 = 900 XP 基础
  - 1 个 Hard 遭遇: 600 XP 基础
  - 总基础 XP: 1,500

角色 A (战士, 存活, 从未倒地):
  参与度: 1.0 + 0.1 = 1.1
  目标: 完成 1 个次要目标 (+10%)
  时间: 正常 (×1.0)
  冒险完成: +150
  总 XP: (1500 × 1.1 × 1.1) + 150 = 1,815 + 150 = 1,965

角色 B (法师, 存活, 救回队友次数最多):
  参与度: 1.0 + 0.1 = 1.1
  总 XP: (1500 × 1.1 × 1.1) + 150 = 1,965

角色 C (盗贼, 最终战倒地被救回):
  参与度: 0.9
  总 XP: (1500 × 0.9 × 1.1) + 150 = 1,485 + 150 = 1,635

角色 D (牧师, 存活, 从未倒地, 完成最多技能检定):
  参与度: 1.0 + 0.1 + 0.05 = 1.15
  总 XP: (1500 × 1.15 × 1.1) + 150 = 1,897.5 → 1,898
```

---

## 3. 失败结算流程

### 3.1 失败严重程度判定算法

#### 3.1.1 输入参数

```json
{
  "failure_context": {
    "party_state": {
      "total_members": 4,
      "alive_count": 1,
      "dead_count": 2,
      "unconscious_count": 1,
      "retreated": false
    },
    "adventure_progress": {
      "total_encounters": 8,
      "completed_encounters": 5,
      "completion_percentage": 0.625,
      "boss_reached": false,
      "side_objectives_completed": 1
    },
    "failure_cause": "all_unconscious",
    "damage_taken_total": 245,
    "party_hp_max_total": 100
  }
}
```

#### 3.1.2 严重程度判定决策树

```
失败严重程度判定 (Failure Severity Determination)
=================================================

输入: failure_context

Step 1: 检查灾难性 (Catastrophic)
  IF dead_count == total_members:
    severity = "catastrophic"
    → 全灭，所有角色死亡
  ELSE IF dead_count >= total_members * 0.75 AND alive_count <= 1:
    severity = "catastrophic"
    → 大部分死亡，仅剩 1 人

Step 2: 检查严重 (Severe)
   IF dead_count >= 1 AND dead_count < total_members * 0.75:
     severity = "severe"
     → 有角色永久死亡
   ELSE IF completion_percentage < 0.25 AND retreated == true:
     severity = "severe"
     → 早期撤退，几乎未完成任何内容
   ELSE IF completion_percentage < 0.25 AND retreated == false:
     severity = "severe"
     → 早期被击败（非主动撤退），几乎未完成任何内容

Step 3: 检查中等 (Moderate)
  IF dead_count == 0 AND unconscious_count >= total_members * 0.5:
    severity = "moderate"
    → 无人死亡但大部分人倒地
  ELSE IF completion_percentage >= 0.25 AND completion_percentage < 0.75:
    severity = "moderate"
    → 完成部分但未达成主要目标
  ELSE IF retreated == true AND completion_percentage >= 0.25:
    severity = "moderate"
    → 主动撤退但完成了一些内容

Step 4: 默认轻微 (Minor)
  severity = "minor"
  → 小挫折，大部分内容已完成
```

#### 3.1.3 严重程度等级定义

| 严重程度 | 触发条件 | 情感冲击 | 恢复难度 |
|----------|----------|----------|----------|
| **Minor (轻微)** | 部分队员倒地但最终逃脱；完成 >75% 但未达成主要目标 | 低 — "下次会更好" | 自动恢复 |
| **Moderate (中等)** | 全队败退但无人死亡；完成 25%-75% | 中 — "我们损失了一些东西" | 需要酒馆修复/任务恢复 |
| **Severe (严重)** | 有角色永久死亡；完成 <25% 且撤退 | 高 — "我们失去了同伴" | 需要招募新角色+传承 |
| **Catastrophic (灾难性)** | 全灭（所有角色死亡） | 极高 — "一切都没了" | 需要全新开始+面对后果 |

#### 3.1.4 示例场景

**Minor 示例**：
```
场景: 4 人队伍在短冒险中，3 人倒地但被牧师救回，最终逃脱
输入: alive=4, dead=0, completion=80%, retreated=true
判定: completion >= 75% → Minor
结果: 损失 30% 金币，消耗品消耗，轻微叙事惩罚
```

**Moderate 示例**：
```
场景: 4 人队伍在中冒险中，被 Boss 击败后全队撤退
输入: alive=4, dead=0, completion=50%, retreated=true
判定: completion 25%-75% → Moderate
结果: 随机装备损坏，角色获得伤疤，世界状态负面变化
```

**Severe 示例**：
```
场景: 4 人队伍在长冒险中，战士和盗贼死亡，法师和牧师撤退
输入: alive=2, dead=2, completion=40%, retreated=true
判定: dead >= 1 → Severe
结果: 2 角色永久死亡，重要装备丢失，世界状态剧变
```

**Catastrophic 示例**：
```
场景: 4 人队伍在 Boss 战中全灭
输入: alive=0, dead=4, completion=90%, retreated=false
判定: dead == total → Catastrophic
结果: 全队死亡，所有非绑定装备丢失，世界状态灾难性变化
```

### 3.2 失败结算管线

```
失败结算流程 (Adventure Failure Settlement Pipeline)
====================================================

Step 1: 失败检测
  ┌─────────────────────────────────────────────────┐
  │ 触发条件 (任一):                                 │
  │   - 所有队伍成员 HP = 0 且 rounds_without_healing │
  │     >= 3 (死亡)                                  │
  │   - 玩家主动触发"撤退"命令                       │
  │   - 特殊失败条件达成 (如: 时限到达/关键 NPC 死亡) │
  │                                                   │
  │ 验证: adventure.completion_status = "failure"     │
  │ 发出信号: adventure_failed(adventure_id, cause)   │
  └───────────────────────┬─────────────────────────┘
                          ▼
Step 2: 确定失败严重程度
  ┌─────────────────────────────────────────────────┐
  │ 调用 determine_failure_severity(failure_context) │
  │ 输出: "minor" / "moderate" / "severe" /          │
  │       "catastrophic"                             │
  │                                                   │
  │ 发出信号: failure_severity_determined(severity)   │
  └───────────────────────┬─────────────────────────┘
                          ▼
Step 3: 生成惩罚 (程序选择 + LLM 叙事)
  ┌─────────────────────────────────────────────────┐
  │ 1. 程序根据严重程度从惩罚池中选择惩罚项          │
  │ 2. 调用 LLM 生成惩罚的叙事描述                   │
  │ 3. 合并程序数值 + LLM 文本                       │
  │                                                   │
  │ 输出: penalties 数组 (含数值效果 + 叙事文本)      │
  └───────────────────────┬─────────────────────────┘
                          ▼
Step 4: 应用角色后果
   ┌─────────────────────────────────────────────────┐
   │ 根据严重程度:                                    │
   │   Minor: 无角色后果 (无伤疤, 无死亡)            │
   │   Moderate: 1-2 角色获得伤疤 (见 §4)             │
   │   Severe: 死亡角色处理 (见 §5) + 存活者伤疤      │
   │   Catastrophic: 所有角色标记死亡 (见 §5)          │
   │                                                   │
   │ 注意: Minor 失败不会触发伤疤生成——§4.1 Step 2    │
   │ 的伤疤严重程度判定仅适用于 Moderate+ 失败。      │
   │                                                   │
   │ 发出信号: scar_acquired / character_died          │
  └───────────────────────┬─────────────────────────┘
                          ▼
Step 5: 应用装备后果
  ┌─────────────────────────────────────────────────┐
  │ 根据严重程度 (见 §6):                            │
  │   Minor: 无装备损失                              │
  │   Moderate: 1-2 件装备损坏 (condition -1)        │
  │   Severe: 1-3 件装备损坏 + 1 件可能被毁          │
  │   Catastrophic: 所有非绑定装备丢失                │
  │                                                   │
  │ 发出信号: equipment_damaged / equipment_destroyed │
  └───────────────────────┬─────────────────────────┘
                          ▼
Step 6: 应用世界状态负面变化
  ┌─────────────────────────────────────────────────┐
  │ 1. 读取冒险蓝图中的 world_state_hooks             │
  │ 2. 执行 on_failure 钩子                          │
  │ 3. 更新区域状态 (如: 威胁→沦陷)                  │
  │ 4. 更新 NPC 状态 (如: NPC 死亡)                 │
  │ 5. 更新势力关系 (如: 敌对化)                     │
  │                                                   │
  │ 发出信号: world_state_mutated(negative_changes)   │
  └───────────────────────┬─────────────────────────┘
                          ▼
Step 7: 更新酒馆声望
  ┌─────────────────────────────────────────────────┐
  │ 声望减少 = 基础值 × 严重程度系数                 │
  │   Minor: -5 声望                                 │
  │   Moderate: -10 声望                             │
  │   Severe: -15 声望                               │
  │   Catastrophic: -20 声望                         │
  │                                                   │
  │ 声望下限: 0 (不会变为负数)                       │
  │ 发出信号: reputation_changed(old, new)            │
  └───────────────────────┬─────────────────────────┘
                          ▼
Step 8: LLM 失败叙事生成
  ┌─────────────────────────────────────────────────┐
  │ 调用 DM Agent 生成失败叙事 (见 §12)              │
  │ 输入: 冒险摘要 + 失败原因 + 角色状态 + 惩罚列表  │
  │ 输出: 2-3 段失败叙事文本                         │
  │                                                   │
  │ 显示: 冒险结算界面的叙事区域                      │
  └───────────────────────┬─────────────────────────┘
                          ▼
Step 9: 返回酒馆并展示后果
  ┌─────────────────────────────────────────────────┐
  │ 1. 切换到酒馆场景                                │
  │ 2. 显示失败结算界面                              │
  │    - 严重程度标识                                │
  │    - 角色伤疤/死亡通知                           │
  │    - 装备损失列表                                │
  │    - 世界状态变化摘要                            │
  │    - 声望变化                                    │
  │ 3. 播放失败 BGM (低沉/悲伤)                     │
  │ 4. 触发酒馆悲伤事件 (可选)                       │
  │ 5. 如果有角色死亡: 触发传承流程 (见 §7)          │
  └─────────────────────────────────────────────────┘
```

### 3.3 部分成功处理

当玩家完成了部分次要目标但未达成主要目标时，使用**混合结算**：

```
部分成功结算规则:
  completion_percentage >= 75%:
    - 使用 Minor 失败惩罚 (减轻版)
    - 保留已完成次要目标的 XP 奖励
    - 世界状态: 轻微负面变化

  completion_percentage >= 50%:
    - 使用 Moderate 失败惩罚 (减轻版)
    - 保留 50% 已完成次要目标的 XP 奖励
    - 世界状态: 中等负面变化

  completion_percentage >= 25%:
    - 使用 Moderate 失败惩罚
    - 保留 25% 已完成次要目标的 XP 奖励
    - 世界状态: 显著负面变化

  completion_percentage < 25%:
    - 使用 Severe 失败惩罚
    - 无 XP 奖励
    - 世界状态: 严重负面变化
```

---

## 4. 伤疤系统详解

### 4.1 伤疤生成算法

```
Scar Generation Algorithm
=========================

输入:
  - character: CharacterResource
  - combat_log: Array[Dictionary] (最近 5 轮战斗记录)
  - damage_summary: Dictionary (各伤害类型累计值)
  - failure_severity: String

Step 1: 确定主要伤害类型
  primary_damage_type = max(damage_summary, key=累计伤害)
  
  如果伤害类型不在预定义池中:
    primary_damage_type = "physical"  # 默认物理

Step 2: 确定伤疤严重程度
   # 注意: Minor 失败不生成伤疤 (§3.2 Step 4), 此算法仅在 Moderate+ 失败时调用
   match failure_severity:
     "moderate":
       scar_severity = weighted_random({"light": 60, "moderate": 35, "severe": 5})
     "severe":
       scar_severity = weighted_random({"light": 30, "moderate": 50, "severe": 20})
     "catastrophic":
       scar_severity = weighted_random({"light": 10, "moderate": 40, "severe": 50})

Step 3: 从对应伤害类型池中筛选候选伤疤
   candidates = scar_pool[primary_damage_type]
   candidates = filter(candidates, severity <= scar_severity)
   candidates = filter(candidates, id NOT IN character.scar_ids)  # 排除已存在

   # Fallback: 如果候选列表为空 (伤害类型池枯竭或全部已拥有)
   if candidates is empty:
     if primary_damage_type != "generic":
       candidates = scar_pool["generic"]  # 降级到通用伤疤池
     if candidates is still empty:
       return null  # 不生成伤疤 (所有同类型伤疤已拥有且通用池也枯竭)

Step 4: 加权随机选择
  weights = {
    "light": 70,
    "moderate": 25,
    "severe": 5
  }
  # 如果 scar_severity 为 moderate 或 severe，提升对应权重
  if scar_severity == "moderate":
    weights = {"light": 40, "moderate": 50, "severe": 10}
  elif scar_severity == "severe":
    weights = {"light": 20, "moderate": 40, "severe": 40}
  
  selected_scar = weighted_random_select(candidates, weights)

Step 5: 生成 LLM 叙事
  narrative = call_llm_scar_narrative(character, selected_scar, combat_log)

Step 6: 构建完整伤疤数据
  scar = {
    "scar_id": generate_unique_id(),
    "character_id": character.character_id,
    "acquired_at": {...},
    "name": selected_scar.name,
    "narrative": narrative,
    "scar_category": selected_scar.category,
    "severity": scar_severity,
    "mechanical_effects": selected_scar.effects,
    "cosmetic": selected_scar.cosmetic
  }

Step 7: 应用到角色
  character.scar_ids.append(scar.scar_id)
  apply_mechanical_effects(character, scar.mechanical_effects)
  emit_signal("scar_acquired", character.character_id, scar.scar_id)

返回: scar
```

### 4.2 完整伤疤效果表

> **设计说明 (v1.3)**: 伤疤补偿已从"可量化的战术优势"降级为"被迫适应的微薄改善"，与 character-system v1.2 对齐。原则：
> - 伤害抵抗 → 改为豁免优势（非全抵抗）
> - 黑暗视觉 60尺 → 微光感知 10尺（非全黑暗视觉）
> - 永久属性增益 → 移除或改为情境性小加成
> - 补偿不应让权力游戏者"追求伤疤"——它们是叙事性的适应，不是可收集的升级。

#### 4.2.1 火焰伤害伤疤 (FIRE)

| ID | 名称 | 严重程度 | 惩罚 | 补偿 | 外观标记 | 移除方式 |
|----|------|:--------:|------|------|----------|----------|
| scar_fire_trauma | 惧焰 | Light | 火焰伤害 ×1.5 | 对抗火焰的 DEX 豁免优势 | 背部烧伤疤痕 | 神殿/时间 |
| scar_branded | 烙印 | Moderate | CHA -2 (非威吓社交检定) | 威吓 +3 | 面部烙印 | 特殊任务 |
| scar_seared_lungs | 炙肺 | Moderate | HP 上限永久 -5 | 对抗吸入性毒素豁免优势 | 呼吸伴随嘶哑声 | 特殊任务 |
| scar_fire_hair | 焦发 | Light | 第一次被敌人注意时劣势 | 火焰戏法免费 (如 Produce Flame) | 永久烧焦的发梢 | 神殿/时间 |
| scar_blistered_hands | 灼手 | Light | 巧手 -2, 远程武器攻击 -1 | 对抗火焰的豁免优势 | 手掌水疱 | 神殿/时间 |

#### 4.2.2 寒冷伤害伤疤 (COLD)

| ID | 名称 | 严重程度 | 惩罚 | 补偿 | 外观标记 | 移除方式 |
|----|------|:--------:|------|------|----------|----------|
| scar_frostbite | 冻伤指节 | Light | 巧手 -2 | 对抗寒冷的 CON 豁免优势 | 手指发蓝 | 神殿/时间 |
| scar_chill_bones | 寒骨 | Moderate | 速度 -5 尺 | 察觉 +2 (振动感知增强) | 关节僵硬 | 特殊任务 |
| scar_cold_veins | 寒血 | Severe | 死亡豁免窗口 -1 轮 (2 轮不治即死) | 对抗寒冷的豁免优势 | 血管呈蓝色可见 | 特殊任务 |
| scar_frozen_memory | 冰封记忆 | Light | 历史 -2 | 寒冷环境天候豁免优势 | 银色发丝 | 神殿/时间 |

#### 4.2.3 闪电伤害伤疤 (LIGHTNING)

| ID | 名称 | 严重程度 | 惩罚 | 补偿 | 外观标记 | 移除方式 |
|----|------|:--------:|------|------|----------|----------|
| scar_nerve_damage | 神经损伤 | Moderate | 先攻 -2 | 闪电伤害不打断专注 | 手部震颤 | 特殊任务 |
| scar_static_touch | 静电之触 | Light | 徒手接触 NPC 有 10% 几率使其敌意 | 对抗电击的豁免优势 | 头发偶尔竖立 | 神殿/时间 |
| scar_lightning_twitch | 闪电抽动 | Light | DEX 豁免 -1 | 先攻 +1 (仅在雷暴/闪电环境中) | 肌肉偶尔抽搐 | 神殿/时间 |
| scar_heart_arrythmia | 心脉紊乱 | Moderate | CON 豁免 -1 | 感知心电图 (察觉 +2 对抗伏击) | 颈部血管闪蓝光 | 特殊任务 |

#### 4.2.4 黯蚀伤害伤疤 (NECROTIC)

| ID | 名称 | 严重程度 | 惩罚 | 补偿 | 外观标记 | 移除方式 |
|----|------|:--------:|------|------|----------|----------|
| scar_withered_limb | 枯肢 | Severe | STR -2 (永久) | 获得 10 尺微光感知 | 手臂萎缩/灰白 | 特殊任务 |
| scar_soul_scar | 魂痕 | Moderate | 第 1 次死亡豁免自动失败 | 对亡灵 +2 AC | 胸口灰白斑痕 | 特殊任务 |
| scar_life_drain_echo | 生命回响 | Moderate | HP 上限永久 -10 | 每次击杀获得 +1 临时 HP | 眼下黑圈 | 特殊任务 |
| scar_spectral_mark | 灵痕 | Light | 对驱散/放逐豁免劣势 | 可感知 60 尺内亡灵存在 | 皮肤上飘忽的灰色符文 | 神殿/时间 |

#### 4.2.5 心灵伤害伤疤 (PSYCHIC)

| ID | 名称 | 严重程度 | 惩罚 | 补偿 | 外观标记 | 移除方式 |
|----|------|:--------:|------|------|----------|----------|
| scar_paranoia | 偏执狂 | Moderate | 短休获益有 50% 几率失败 | 对抗突袭免疫 | 时刻环顾四周 | 特殊任务 |
| scar_broken_mind | 破碎心智 | Severe | INT -2 (永久) | 异怪语获得, 对抗心灵的豁免优势 | 神情恍惚 | 特殊任务 |
| scar_nightmare_plagued | 梦魇缠身 | Moderate | 长休 50% 不消除疲乏 | 短休额外恢复 1 个 Hit Die | 严重黑眼圈 | 特殊任务 |
| scar_psychic_scar | 心灵裂痕 | Light | WIS 豁免 -1 | 对魅惑/恐慌豁免优势 | 眉间深深皱纹 | 神殿/时间 |

#### 4.2.6 物理伤害伤疤 (PHYSICAL)

| ID | 名称 | 严重程度 | 惩罚 | 补偿 | 外观标记 | 移除方式 |
|----|------|:--------:|------|------|----------|----------|
| scar_battle_hardened | 老兵之手 | Light | 主手武器攻击 -1 | 近战威吓 +2 | 手臂布满伤疤 | 神殿/时间 |
| scar_lost_eye | 独眼 | Moderate | 察觉 -2, 远程攻击劣势 (>30 尺) | 获得 10 尺微光感知 | 独眼/眼罩 | 特殊任务 |
| scar_limp | 跛行 | Moderate | 速度 -10 尺 | 倒地后站立仅需 5 尺 | 用手杖/明显跛行 | 特殊任务 |
| scar_missing_finger | 断指 | Light | 巧手 -2 | 获得徒手攻击熟练项 | 断一指 | 神殿/时间 |
| scar_broken_ribs | 旧伤肋骨 | Light | CON -1 | 受到暴击时伤害 -3 | 呼吸伴疼痛/偶尔捂胸 | 神殿/时间 |
| scar_shattered_jaw | 碎颚 | Moderate | 说服/欺瞒 -2 | 威吓 +2, 可吃非食物物质 | 下颌畸形 | 特殊任务 |
| scar_severed_ear | 缺耳 | Light | 察觉 -1 | 对抗雷鸣的豁免优势 | 缺一只耳朵 | 神殿/时间 |
| scar_spinal_twist | 脊弯 | Light | 跳跃距离减半 | 不会被推倒 | 姿势微驼 | 神殿/时间 |

#### 4.2.7 毒/酸伤害伤疤 (POISON/ACID)

| ID | 名称 | 严重程度 | 惩罚 | 补偿 | 外观标记 | 移除方式 |
|----|------|:--------:|------|------|----------|----------|
| scar_acid_scarred | 酸蚀 | Moderate | CHA -2 | 对抗酸的豁免优势 | 面部/皮肤腐蚀痕迹 | 特殊任务 |
| scar_toxin_tolerance | 毒抗 | Light | 治疗药水效果 -50% | 对抗中毒的豁免优势 | 皮肤微绿 | 神殿/时间 |
| scar_chemical_burns | 化学灼伤 | Light | 巧手 -1, 表演 -1 | 酸/毒豁免优势 | 手部漂白斑痕 | 神殿/时间 |

#### 4.2.8 通用伤疤 (GENERIC)

| ID | 名称 | 严重程度 | 惩罚 | 补偿 | 外观标记 | 移除方式 |
|----|------|:--------:|------|------|----------|----------|
| scar_survivor_spirit | 幸存者之魂 | Light | 无 | 下次冒险开始时 +5 临时 HP | 眼神中的坚毅 | 自动消失 (1 次冒险后) |
| scar_debt_to_death | 死亡之债 | Moderate | 每次冒险必须完成至少 1 个危险目标 | 对致命伤害的感知 +2 | 额头的灰色印记 | 特殊任务 |
| scar_hollow_stare | 空洞凝视 | Light | 说服 -1 | 洞悉 +2 | 眼神空洞 | 神殿/时间 |

### 4.3 伤疤消除与缓解

| 方法 | 效果 | 获取方式 | 成本 | 限制 |
|------|------|----------|------|------|
| **神殿祈祷** | 移除 1 个 Light 伤疤 | 酒馆声望 Lv7 解锁神殿 | 500 gp | 机械效果移除，但视觉疤痕保留为淡淡痕迹 |
| **传奇药水** | 移除 1 个 Moderate 或 Light 伤疤 | 稀有掉落/炼金台制作 | 300 gp 材料 | 10% 几率失败 (药水浪费) |
| **灵魂换约** | 移除 1 个 Severe 伤疤 | 邪魔/旧日支配者特殊事件 | -2 任一属性永久 | 每角色限 1 次 |
| **时间治愈** | Light 伤疤 5 次冒险后自动转为仅外观 | 自然流逝 | 无 | 仅 Light 严重程度 |
| **替代伤疤** | 新同类型伤疤替换旧伤疤 | 新的重度伤害事件 | 无 | 可选择保留哪个外观 |

### 4.4 伤疤堆叠规则

```
伤疤堆叠规则:
  1. 同一角色不能拥有同一 ID 的伤疤
  2. 同一伤害类型可以拥有多个不同 ID 的伤疤
     例: 角色可以同时拥有 scar_fire_trauma 和 scar_branded
  3. 同一伤害类型的伤疤最多 3 个
     超过 3 个时: 新伤疤替换最旧的同类型伤疤
  4. 不同伤害类型的伤疤无上限
     例: 角色可以同时拥有火焰、寒冷、物理伤疤各 3 个
  5. 伤疤效果全部叠加
     例: 2 个火焰伤疤的惩罚和补偿同时生效
```

### 4.5 伤疤视觉表示

```
伤疤视觉系统:
  - 每个伤疤有 body_part 字段指定身体部位
  - 角色精灵根据伤疤叠加 sprite_overlay
  - 头像根据伤疤显示对应标记
  - Light 伤疤: 细微线条/淡淡痕迹
  - Moderate 伤疤: 明显疤痕/变色
  - Severe 伤疤: 严重变形/缺失部位

  实现方式:
    character_sprite.add_overlay(scar.cosmetic.sprite_overlay)
    character_portrait.add_scar_mark(scar.cosmetic.body_part, scar.severity)
```

---

## 5. 角色死亡系统

### 5.1 死亡流程

```
角色死亡流程 (Character Death Flow)
====================================

Step 1: 角色 HP 降至 0
  ┌─────────────────────────────────────────────────┐
  │ 触发: hp_current <= 0                           │
  │ 状态: Unconscious + Prone                        │
  │ 开始: death_save_rounds_without_healing 计数     │
  │                                                   │
  │ 发出信号: hp_changed(character_id, old_hp, 0,    │
  │                      max_hp)                      │
  │ 发出信号: character_unconscious(character_id)     │
  └───────────────────────┬─────────────────────────┘
                          ▼
Step 2: 死亡豁免倒计时 (双轨制)
  ┌─────────────────────────────────────────────────┐
  │ 每轮开始时:                                      │
  │   death_save_rounds_without_healing += 1         │
  │                                                   │
  │ 如果有治疗 (HP > 0):                             │
  │   death_save_rounds_without_healing = 0          │
  │   death_failures = 0                             │
  │   状态恢复: 移除 Unconscious                      │
  │                                                   │
  │ 如果角色HP=0时受到任何伤害:                       │
  │   death_failures += 2                            │
  │   (标准5e的"0HP受伤=直接死亡"改为累积失败制)       │
  │                                                   │
  │ 如果DC 10医疗检定成功 (WIS/Medicine):            │
  │   角色稳定 (Stabilized)                          │
  │   death_save_rounds_without_healing 停止增加      │
  │   death_failures 停止增加                        │
  │   (但角色仍保持Unconscious直到被治疗)             │
  │                                                   │
  │ 发出信号: death_save_progressed(character_id,     │
  │                                  rounds, failures)│
  └───────────────────────┬─────────────────────────┘
                          ▼
Step 3: 判定死亡
  ┌─────────────────────────────────────────────────┐
  │ IF death_failures >= 3:                          │
  │   角色立即永久死亡 (无需等待3轮)                  │
  │   status = "dead"                                │
  │   cause = "death_failures_maxed"                 │
  │                                                   │
  │ ELSE IF death_save_rounds_without_healing >= 3:  │
  │   角色永久死亡                                   │
  │   status = "dead"                                │
  │   cause = "rounds_without_healing"               │
  │                                                   │
  │   发出信号: character_died(character_id,          │
  │                            cause, adventure_id)   │
  └───────────────────────┬─────────────────────────┘
                          ▼
Step 4: 死亡处理
  ┌─────────────────────────────────────────────────┐
  │ 1. 从活跃队伍中移除角色                          │
  │ 2. 处理装备 (见 §6)                              │
  │ 3. 触发知识传承 (见 §7)                          │
  │ 4. 生成英雄传记 (见 §8)                          │
  │ 5. 更新关系 (存活者对死者的关系变化)             │
  │ 6. 添加到英雄之壁                                │
  └─────────────────────────────────────────────────┘
```

### 5.2 永久死亡后果

| 后果类型 | 详细说明 |
|----------|----------|
| **角色移除** | 角色从活跃队伍移除，状态变为 "dead"，不可再参与冒险 |
| **装备处理** | 非绑定装备进入队伍战利品或丢失；绑定装备进入传承池 |
| **知识传承触发** | 自动触发传承流程 (见 §7) |
| **关系后果** | 存活队友对死者的关系变化: +2 (悲伤/怀念) 或 -1 (怨恨/指责) |
| **英雄传记** | LLM 生成英雄传记，存入英雄之壁 |
| **酒馆事件** | 可能触发酒馆悲伤事件 (如: 追悼会/争吵) |

### 5.3 复活机制 (神殿, Lv7)

```
复活规则:
  前置条件: 酒馆声望 >= Lv7 (解锁神殿)
  
  成本:
    - 2,000 gp
    - 极稀有材料组件 (如: 凤凰羽毛/龙鳞/精灵泪 — 需通过传奇级冒险获取)
  
  时间:
    - 角色在 2 次冒险后返回 (期间不可用)
  
  副作用:
    - 返回时获得永久 debility: CON -1, STR -1
    - 仅在死亡后 2 次冒险内有效 (超过则无法复活)
  
  限制:
    - 每个角色只能被复活 1 次
    - 灾难性失败 (全灭) 中死亡的角色无法复活
    - 同一存档中最多复活 2 个角色
```

---

## 6. 装备损失与损坏

### 6.1 装备损坏规则

| 严重程度 | 装备后果 |
|----------|----------|
| **Minor** | 无装备损失 |
| **Moderate** | 1-2 件随机装备受到状态损坏 (condition 降 1 级) |
| **Severe** | 1-3 件随机装备受到状态损坏 + 1 件随机非绑定装备可能被毁 |
| **Catastrophic** | 所有非绑定装备丢失；绑定装备损坏至 Worn 状态 |

### 6.2 物品状态等级

```
物品状态等级 (Item Condition Levels):
   Pristine (完好) → Good (良好) → Worn (磨损) → Damaged (损坏) → Broken (破碎) → Destroyed (销毁)

   Pristine: 100% 效果
   Good: 95% 效果, 轻微使用痕迹
   Worn: 90% 效果, 视觉轻微磨损
   Damaged: 75% 效果, 视觉明显损坏
   Broken: 50% 效果, 需要修复才能使用
   Destroyed: 0% 效果, 物品消失
```

### 6.3 物品销毁概率

当 Severe 或 Catastrophic 失败导致装备可能被毁时，销毁概率基于物品稀有度：

| 稀有度 | 销毁概率 |
|--------|:--------:|
| Common (普通) | 50% |
| Uncommon (优秀) | 30% |
| Rare (稀有) | 15% |
| Very Rare (极稀有) | 5% |
| Legendary (传说) | 0% (永不被毁) |
| Artifact (神器) | 0% (永不被毁) |

### 6.4 装备恢复机制

```
装备恢复选项:
  1. 铁匠铺修复 (酒馆 Lv2 解锁)
     - 修复费用: 10-50 gp per condition level
     - 修复时间: 1 次冒险 (期间装备不可用)
     - 限制: 无法修复 Destroyed 物品

  2. 冒险中找回
     - 失败后 3 次冒险内，有 20% 几率在类似主题冒险中找到损坏装备
     - 找回时状态: 比丢失时低 1 级 (如丢失时 Worn → 找回时 Damaged)

  3. 炼金台修复 (酒馆 Lv3 解锁)
     - 使用修复药水: 50 gp 材料
     - 恢复 1 个 condition level
     - 限制: 每件装备每次冒险只能使用 1 次

  4. 拆解回收
     - Destroyed 物品可拆解为 crafting_materials
     - 回收量: 基础材料的 25%
```

---

## 7. 知识传承系统

### 7.1 继承触发条件

```
继承触发条件:
  1. 角色状态变为 "dead" (死亡)
  2. 角色状态变为 "retired" (退役)
  
  触发时机:
    - 死亡: 立即触发
    - 退役: 玩家主动选择退役时触发
  
  发出信号: character_inheritance_triggered(from_id, to_id)
```

### 7.2 可继承内容

| 继承内容 | 条件 | 效果 |
|----------|------|------|
| **1 个已知技能** | 自动 | 新角色获得该技能熟练 (若同职业可选专精) |
| **部分 XP** | 自动 | 转化为"传承点" (Heritage Points): 每 500 XP = 1 HP |
| **1 件绑定装备** | 消耗 3 传承点 | 选 1 件同调中的魔法装备传递 |
| **冒险日志情报** | 基于冒险日志中的关键事件 | 提供知识标签 (见 §7.4) |

### 7.3 继承数据模型

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "InheritanceData",
  "type": "object",
  "required": ["from_character", "to_character", "inherited_skill", "heritage_points", "knowledge_tags"],
  "properties": {
    "from_character": {
      "type": "object",
      "required": ["character_id", "name", "class", "level", "cause"],
      "properties": {
        "character_id": { "type": "string", "pattern": "^char_[a-f0-9]{8}$" },
        "name": { "type": "string" },
        "class": { "type": "string" },
        "level": { "type": "integer", "minimum": 1, "maximum": 20 },
        "cause": { "type": "string", "enum": ["death_in_combat", "death_by_trap", "death_by_poison", "retired"] },
        "final_adventure_id": { "type": "string" }
      }
    },
    "to_character": {
      "type": "string",
      "description": "新角色 ID (创建时填入)"
    },
    "inherited_skill": {
      "type": "object",
      "required": ["skill_name", "transfer_type"],
      "properties": {
        "skill_name": {
          "type": "string",
          "enum": [
            "acrobatics", "animal_handling", "arcana", "athletics", "deception",
            "history", "insight", "intimidation", "investigation", "medicine",
            "nature", "perception", "performance", "persuasion", "religion",
            "sleight_of_hand", "stealth", "survival"
          ]
        },
        "transfer_type": {
          "type": "string",
          "enum": ["proficiency", "expertise_if_same_class"],
          "description": "proficiency = 获得熟练; expertise_if_same_class = 同职业时获得专精"
        }
      }
    },
    "heritage_points": {
      "type": "object",
      "required": ["total", "spent", "available"],
      "properties": {
        "total": { "type": "integer", "minimum": 0 },
        "spent": { "type": "integer", "minimum": 0 },
        "available": { "type": "integer", "minimum": 0 },
        "calculation": {
          "type": "object",
          "properties": {
            "from_character_level": { "type": "integer" },
            "xp_converted": { "type": "integer" },
            "points_from_xp": { "type": "integer" },
            "points_from_level": { "type": "integer" }
          }
        }
      }
    },
    "knowledge_tags": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["tag", "description", "mechanical_effect"],
        "properties": {
          "tag": { "type": "string" },
          "description": { "type": "string" },
          "mechanical_effect": { "type": "string" },
          "adventure_theme": { "type": "string" }
        }
      }
    },
    "inherited_equipment": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "item_id": { "type": "string" },
          "item_name": { "type": "string" },
          "condition": { "type": "string", "enum": ["pristine", "worn", "damaged"] }
        }
      }
    },
    "biography": {
      "type": "string",
      "description": "LLM 生成的英雄传记"
    }
  }
}
```

### 7.4 知识标签系统

知识标签是继承系统的核心——它们将前人的经验转化为可量化的游戏优势。

#### 7.4.1 知识标签获取规则

```
知识标签获取条件:
  1. 角色在特定主题冒险中完成关键事件
  2. 角色死亡或退役时，系统检查 adventure_log.memorable_events
  3. 匹配预定义的知识标签获取条件
  4. 满足条件的标签自动添加到继承数据

  示例:
    - 完成 1 次龙类 Boss 冒险 → dragon_lair_known
    - 在陷阱中倒地过 → trapped_hallway_marked
    - 对抗亡灵 Boss 战斗记录 → undead_weakness_known
```

#### 7.4.2 完整知识标签表

| 标签 ID | 名称 | 获取条件 | 机械效果 | 适用主题 |
|---------|------|----------|----------|----------|
| dragon_lair_known | 龙穴知识 | 完成 1 次龙类 Boss 冒险 | 龙穴陷阱察觉 +2 | Dragon |
| undead_weakness_known | 亡灵弱点 | 对抗亡灵 Boss 战斗记录 | 对亡灵第一次攻击优势 | Undead |
| goblin_tactics_memorized | 哥布林战术 | 完成 3 次哥布林主题冒险 | 对哥布林类敌人先攻 +2 | Goblin |
| trapped_hallway_marked | 陷阱走廊 | 在陷阱中倒地过 | 地城走廊感知被动 +1 | Dungeon |
| secret_passage_sense | 密道感知 | 发现过 5 个以上隐藏路径 | 察觉隐藏门 +3 | Dungeon |
| poison_antidote_recipe | 解毒配方 | 被中毒 KO 过 | 辨识毒物优势 | Poison |
| forest_survivor | 森林幸存者 | 完成 3 次森林主题冒险 | 森林地形移动不减速 | Forest |
| urban_informant | 城市线人 | 完成 3 次城市主题冒险 | 城市中获取信息检定 +2 | Urban |
| mountain_climber | 登山者 | 完成 2 次山地主题冒险 | 攀爬检定 +2 | Mountain |
| cave_explorer | 洞穴探索者 | 完成 3 次洞穴主题冒险 | 洞穴中黑暗视觉 +30 尺 | Cave |
| cultist_tracker | 邪教追踪者 | 对抗邪教组织 2 次 | 辨识邪教符号优势 | Cult |
| beast_slayer | 怪物猎人 | 击杀 20 只以上野兽 | 对野兽攻击 +1 伤害 | Beast |

#### 7.4.3 知识标签效果实现

```gdscript
# 知识标签效果应用
func apply_knowledge_tag_bonus(
    character: CharacterResource,
    tag: Dictionary,
    context: Dictionary
) -> Dictionary:
    var bonus = {"applied": false, "description": ""}
    
    # 检查主题匹配
    if context.adventure_theme == tag.adventure_theme:
        match tag.tag:
            "dragon_lair_known":
                if context.check_type == "perception" and context.location == "trap":
                    bonus = {"applied": true, "modifier": 2, "description": "龙穴知识: 察觉 +2"}
            "undead_weakness_known":
                if context.encounter_type == "undead" and context.is_first_attack:
                    bonus = {"applied": true, "advantage": true, "description": "亡灵弱点: 第一次攻击优势"}
            "trapped_hallway_marked":
                if context.location == "dungeon_corridor":
                    bonus = {"applied": true, "passive_bonus": 1, "description": "陷阱走廊: 被动感知 +1"}
    
    return bonus
```

### 7.5 传承点 (Heritage Points) 消费

> **设计说明 (v1.3)**: 传承点总量已从 `floor(xp/500) + points_from_level`（Lv5≈21HP）改为 `level + 5`（Lv5=10HP），以兑现"微薄遗物"承诺。消费成本相应重算——高价值选项（feat）需要更多传承点，让玩家在有限预算内做有意义的选择。

| 传承点成本 | 效果 | 持续时间 |
|:----------:|------|----------|
| 1 HP | 下次冒险开始时 +5 临时 HP | 1 次冒险 |
| 1 HP | 下次冒险开始时 +30 起始金币 | 永久 |
| 2 HP | 下次冒险获得 1 项额外技能熟练 | 1 次冒险 |
| 4 HP | 下次冒险起始等级 +1 (但 XP 获取 -20%) | 永久 |
| 4 HP | 额外 1 个 1 环法术位 (仅限施法者) | 1 次冒险 |
| 5 HP | 继承 1 件绑定装备 | 永久 |
| 7 HP | 获得 1 个 minor feat (如: Lucky, Tough) | 永久 |

### 7.6 传承点计算公式

> **设计说明 (v1.3)**: 传承点公式已从 `floor(xp/500) + points_from_level` 改为 `level + 5`，与 character-system v1.2 统一。这确保传承始终是"微薄遗物"而非"高价值遗产"——"死亡有重量"意味着死亡的代价远大于传承的收获。

```
heritage_points = level + 5

示例:
  Lv5 Fighter: 5 + 5 = 10 HP
  Lv10 Wizard: 10 + 5 = 15 HP
  Lv15 Cleric: 15 + 5 = 20 HP
  Lv20 Barbarian: 20 + 5 = 25 HP
```

---

## 8. 英雄传记系统

### 8.1 传记生成触发

```
传记生成触发条件:
  1. 角色状态变为 "dead" (死亡)
  2. 角色状态变为 "retired" (退役)
  
  触发时机: 继承流程完成后自动生成
```

### 8.2 传记生成输入

```json
{
  "biography_input": {
    "character": {
      "name": "索林·铁锤",
      "race": "dwarf",
      "class": "fighter",
      "level": 5,
      "personality_tags": ["固执", "忠诚", "嗜酒"]
    },
    "adventure_log": {
      "adventures_completed": 7,
      "total_kills": 47,
      "total_damage_dealt": 1250,
      "total_damage_taken": 890,
      "critical_hits": 8,
      "critical_fails": 3,
      "memorable_events": [
        {
          "event": "在被遗忘的回廊中以一敌三击败食人魔",
          "adventure_id": "adv_005",
          "type": "combat_heroic"
        },
        {
          "event": "为掩护队友撤退独自面对亡灵大军",
          "adventure_id": "adv_007",
          "type": "sacrifice"
        }
      ]
    },
    "relationships": {
      "char_elara": {"value": 5, "type": "comrade", "name": "艾拉拉·月歌"},
      "char_player": {"value": -2, "type": "hostile", "name": "亚瑟·晨星"}
    },
    "death_context": {
      "adventure_name": "亡灵军团的崛起",
      "cause": "为掩护队友撤退，独自面对亡灵大军",
      "last_words": "快走！我来挡住它们！"
    }
  }
}
```

### 8.3 LLM 传记生成 Prompt

```
System: 你是酒馆说书人，为逝去或退休的冒险者撰写英雄传记。
传记将被刻在酒馆的英雄之壁上，供后来的冒险者瞻仰。

User:
=== 角色信息 ===
姓名: {character.name}
种族: {character.race}
职业: {character.class}
等级: {character.level}
性格: {personality_tags}

=== 冒险记录 ===
完成冒险: {adventures_completed} 次
总击杀: {total_kills} 名敌人
总伤害输出: {total_damage_dealt}
总伤害承受: {total_damage_taken}
暴击次数: {critical_hits}
关键事件:
{for event in memorable_events:
  - {event.event} ({event.type})
}

=== 关系记录 ===
{for rel in relationships:
  - {rel.name}: {rel.type} (关系值 {rel.value})
}

=== 死亡/退役场景 ===
冒险名称: {death_context.adventure_name}
死亡原因: {death_context.cause}
遗言: {death_context.last_words}

=== 生成要求 ===
1. 传记长度: 2-3 段 (每段 3-5 句)
2. 第一段: 角色生平概述 (种族/职业/性格/主要成就)
3. 第二段: 关键冒险经历 (引用 memorable_events 中的事件)
4. 第三段: 最后的战斗/退役场景 + 对酒馆的影响
5. 语气: 庄重但温暖，像酒馆里的说书人在讲述
6. 包含具体数字 (击杀数/冒险次数) 增加真实感
7. 提及 1-2 个关系角色 (如果有)
8. 结尾暗示角色的精神遗产

输出格式 (纯文本, 不要 JSON):
[传记正文]
```

### 8.4 传记输出示例

```
索林·铁锤，丘陵矮人战士，酒馆中最固执也最忠诚的灵魂。
他完成了 7 次冒险，击杀 47 名敌人，用他那把战锤在无数
敌人的铠甲上留下了印记。他的嗜酒在酒馆中是出了名的，
但他的战斗技艺更是无人能及。

在被遗忘的回廊中，索林以一敌三击败了三只食人魔，那次
战斗让他成为了酒馆中的传奇。他与精灵法师艾拉拉·月歌
建立了深厚的战友情谊，两人在战场上配合默契，如同一体。

在最后的冒险中，面对亡灵大军的围攻，索林选择了留下。
"快走！我来挡住它们！"——这是他最后的话语。他的战锤
至今还挂在酒馆壁炉旁的墙上，每当新冒险者问起，老人们
都会说："那是索林的锤子，它比我们所有人都勇敢。"
```

### 8.5 传记作为世界状态

英雄传记不仅是展示内容，还会被注入未来冒险的 LLM 上下文：

```
传记世界状态注入:
  - 编剧 Agent 生成新冒险时，会读取英雄之壁上的传记
  - 新冒险中可能提及已故英雄:
    - NPC 对话: "你就是那个索林·铁锤的同伴吗？"
    - 环境描写: "墙上刻着一个矮人战士的名字..."
    - 隐藏选项: "使用索林教你的战术" (需要知识标签)
  - 这让死亡的角色在游戏世界中"永生"
```

---

## 9. 世界演进系统

### 9.1 世界状态数据模型

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "WorldState",
  "type": "object",
  "required": ["version", "regions", "factions", "npcs", "events", "adventure_history"],
  "properties": {
    "version": {
      "type": "string",
      "description": "世界状态版本号，用于存档兼容性"
    },
    "regions": {
      "type": "object",
      "additionalProperties": {
        "type": "object",
        "required": ["name", "state", "threat_level"],
        "properties": {
          "name": { "type": "string" },
          "state": {
            "type": "string",
            "enum": ["safe", "threatened", "fallen", "liberated", "destroyed"]
          },
          "threat_level": { "type": "integer", "minimum": 0, "maximum": 10 },
          "description": { "type": "string" },
          "key_locations": { "type": "array", "items": { "type": "string" } },
          "controlled_by": { "type": "string" },
          "last_updated": { "type": "string", "format": "date-time" }
        }
      }
    },
    "factions": {
      "type": "object",
      "additionalProperties": {
        "type": "object",
        "required": ["name", "disposition"],
        "properties": {
          "name": { "type": "string" },
          "disposition": {
            "type": "string",
            "enum": ["ally", "friendly", "neutral", "hostile", "enemy"]
          },
          "reputation_value": { "type": "integer", "minimum": -100, "maximum": 100 },
          "description": { "type": "string" },
          "leader": { "type": "string" },
          "goals": { "type": "array", "items": { "type": "string" } }
        }
      }
    },
    "npcs": {
      "type": "object",
      "additionalProperties": {
        "type": "object",
        "required": ["name", "status"],
        "properties": {
          "name": { "type": "string" },
          "status": {
            "type": "string",
            "enum": ["alive", "dead", "missing", "allied", "hostile", "neutral"]
          },
          "role": { "type": "string" },
          "location": { "type": "string" },
          "disposition_to_player": { "type": "string" },
          "last_seen": { "type": "string", "format": "date-time" },
          "notes": { "type": "string" }
        }
      }
    },
    "events": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["event_id", "type", "description", "timestamp"],
        "properties": {
          "event_id": { "type": "string" },
          "type": {
            "type": "string",
            "enum": ["adventure_success", "adventure_failure", "world_event", "faction_change", "npc_death"]
          },
          "description": { "type": "string" },
          "timestamp": { "type": "string", "format": "date-time" },
          "related_adventure_id": { "type": "string" },
          "impact": { "type": "string" }
        }
      }
    },
    "adventure_history": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["adventure_id", "title", "result", "timestamp"],
        "properties": {
          "adventure_id": { "type": "string" },
          "title": { "type": "string" },
          "result": { "type": "string", "enum": ["success", "failure", "partial"] },
          "severity": { "type": "string" },
          "theme": { "type": "string" },
          "timestamp": { "type": "string", "format": "date-time" },
          "party_members": { "type": "array", "items": { "type": "string" } },
          "key_choices": { "type": "array", "items": { "type": "string" } }
        }
      }
    }
  }
}
```

### 9.2 世界状态变异规则

#### 9.2.1 成功时的正向变异

| 冒险主题 | 成功后果 | 世界状态变化 |
|----------|----------|--------------|
| 拯救城镇 | 城镇安全 | region.state: threatened → safe; region.threat_level -= 3 |
| 击败 Boss | 区域安全 | region.state: threatened → safe; new NPC 出现 |
| 获得神器 | 神器入库 | faction.disposition: neutral → friendly |
| 惹怒势力 | 势力敌对 | faction.disposition: hostile → enemy |
| 发现秘密 | 新任务线 | new event 添加; faction.reputation += 10 |
| 保护商队 | 贸易恢复 | region.threat_level -= 2; 商人物品池更新 |
| 清剿地牢 | 地城安全 | region.key_locations 添加 "cleared_dungeon" |
| 解救俘虏 | NPC 加入 | npc.status: missing → allied |
| 阻止仪式 | 灾难避免 | region.state: threatened → safe; event 添加 |
| 揭露阴谋 | 真相大白 | faction.disposition: hostile → neutral; new quest |

#### 9.2.2 失败时的负向变异

| 冒险主题 | 失败后果 | 世界状态变化 |
|----------|----------|--------------|
| 拯救城镇 | 城镇沦陷 | region.state: threatened → fallen; region.threat_level += 5 |
| 击败 Boss | Boss 势力扩张 | region.state: safe → threatened; region.controlled_by = boss |
| 获得神器 | 神器落入敌手 | faction.disposition: friendly → hostile; boss 获得神器 |
| 惹怒势力 | 势力追杀 | faction.disposition: hostile → enemy; 随机遭遇增加 |
| 发现秘密 | 秘密泄露 | faction.reputation -= 20; 竞争任务出现 |
| 保护商队 | 商队被劫 | region.threat_level += 3; 商人物品池减少 |
| 清剿地牢 | 地城更危险 | region.threat_level += 2; 怪物等级提升 |
| 解救俘虏 | 俘虏死亡 | npc.status: missing → dead; 关系角色悲伤 |
| 阻止仪式 | 仪式成功 | region.state: safe → threatened; 灾难性事件触发 |
| 揭露阴谋 | 阴谋得逞 | faction.disposition: neutral → hostile; 新敌人出现 |

### 9.3 世界状态持久化

```
世界状态存储:
   文件路径: {ApplicationData}/world_state.json (MonoGame: Environment.SpecialFolder.ApplicationData)
   格式: JSON (见 §9.1 Schema)
  
  加载时机:
    - 游戏启动时加载
    - 每次冒险开始前加载
  
  保存时机:
    - 每次冒险完成后保存
    - 酒馆升级后保存
    - 手动保存时保存
  
  版本控制:
    - version 字段用于存档兼容性
    - 旧版本存档自动迁移
```

### 9.4 世界状态注入冒险生成

```
世界状态作为冒险生成上下文:

  编剧 Agent 生成冒险蓝图时，读取世界状态作为输入:

  输入:
    - regions: 当前区域状态 (哪些安全/哪些危险)
    - factions: 势力关系 (哪些友好/哪些敌对)
    - npcs: NPC 状态 (哪些可用/哪些已死)
    - adventure_history: 历史冒险记录 (避免重复主题)

  影响:
    - 优先选择 threatened 区域作为冒险地点
    - 敌对势力可能成为冒险中的敌人
    - 已死 NPC 不会再次出现 (除非特殊复活剧情)
    - 历史冒险中的选择会影响新冒险的 NPC 态度

  示例:
    世界状态: 灰谷镇 fallen, 亡灵势力 expanded
    → 新冒险主题: "收复灰谷镇" 或 "寻找亡灵势力的弱点"
    → NPC 态度: 灰谷镇幸存者对玩家友好 (如果玩家之前帮助过)
```

---

## 10. 酒馆声望与元进度

### 10.1 酒馆声望系统

#### 10.1.1 声望获取与损失

```
声望变化规则:
  获取:
    - 短冒险完成: +5 声望
    - 中冒险完成: +10 声望
    - 长冒险完成: +15 声望
    - 难度系数: Easy ×0.8 / Normal ×1.0 / Hard ×1.3 / Deadly ×1.6
  
  损失:
    - Minor 失败: -5 声望
    - Moderate 失败: -10 声望
    - Severe 失败: -15 声望
    - Catastrophic 失败: -20 声望
  
  特殊:
    - 完成传奇任务: +25 声望
    - 全灭 (Catastrophic): 额外 -10 声望 (总计 -30)
    - 声望下限: 0 (不会变为负数)
    - 声望上限: 100
```

#### 10.1.2 声望阈值与奖励

| 声望范围 | 等级 | 解锁内容 |
|:--------:|------|----------|
| 0-19 | Novice (新手) | 基础招募、短冒险 |
| 20-39 | Known (知名) | 解锁中冒险、铁匠铺 (Lv2) |
| 40-59 | Respected (受尊敬) | 解锁图书馆、更好的商人 |
| 60-79 | Famous (著名) | 解锁长冒险、更好的战利品表 |
| 80-100 | Legendary (传奇) | 解锁传奇任务、传说装备 |

### 10.2 酒馆等级系统

#### 10.2.1 酒馆 XP 来源

```
酒馆 XP 来源:
  - 冒险完成: 
    - 短冒险: 100 Tavern XP
    - 中冒险: 300 Tavern XP
    - 长冒险: 600 Tavern XP
  
  - 金币投资:
    - 每 100 gp 投资到酒馆升级: 50 Tavern XP
  
  - 特殊成就:
    - 首次完成某主题冒险: 200 Tavern XP
    - 角色达到 Lv5: 150 Tavern XP
    - 角色达到 Lv10: 300 Tavern XP
```

#### 10.2.2 酒馆等级进阶表

| 酒馆等级 | 所需 Tavern XP | 累计 XP | 解锁内容 |
|:--------:|:--------------:|:-------:|----------|
| 1 | 0 | 0 | 基础大厅、招募板、任务板 |
| 2 | 500 | 500 | 铁匠铺 (装备修复/简单打造) |
| 3 | 1,200 | 1,700 | 炼金台 (药水制作) |
| 4 | 2,500 | 4,200 | 中冒险解锁 |
| 5 | 4,500 | 8,700 | 图书馆 (新职业/专长学习) |
| 6 | 7,500 | 16,200 | 角色关系系统深化 |
| 7 | 12,000 | 28,200 | 神殿 (复活角色/移除诅咒) |
| 8 | 18,000 | 46,200 | 长冒险解锁 |
| 9 | 26,000 | 72,200 | 传奇任务线 |
| 10 | 36,000 | 108,200 | 英雄之壁完整功能、传说装备 |

### 10.3 元进度持久化

```
元进度持久化规则:

  跨角色死亡持久化的内容:
    ✓ 酒馆等级 (Tavern Level)
    ✓ 酒馆声望 (Tavern Reputation)
    ✓ 世界状态 (World State)
    ✓ 传承点 (Heritage Points) - 未消费的
    ✓ 已解锁的职业/专长
    ✓ 英雄之壁上的传记
    ✓ 知识标签 (已继承的)

  不持久化的内容 (随角色死亡丢失):
    ✗ 个体角色属性/等级
    ✗ 角色关系值
    ✗ 角色装备 (除非通过继承)
    ✗ 角色金币
    ✗ 角色冒险日志 (除非转化为知识标签)
```

---

## 11. 金币经济模型

### 11.1 金币收入来源

| 收入来源 | 金额范围 | 说明 |
|----------|:--------:|------|
| 冒险完成奖励 | 50-500 gp | 基于冒险等级和难度 |
| 敌人掉落 | 5-50 gp/敌人 | 基于敌人 CR |
| 物品出售 | 基础价值 × 30% × 稀有度乘数 × 状态调整 | 受声望影响 (见下) |
| 任务奖励 | 100-1,000 gp | 特殊任务/支线任务 |
| 制作出售 | 成本 ×150% | 炼金台/铁匠铺制作 |

**物品出售价格修正**:
```
sell_price = base_value × rarity_multiplier × 0.3 × condition_adjuster × reputation_multiplier

rarity_multiplier (与 items-equipment.md §12.2 统一):
  common:     ×1
  uncommon:   ×3
  rare:       ×10
  very_rare:  ×30
  legendary:  ×100
  artifact:   ×500

condition_adjuster (与 items-equipment.md §12.2 统一):
  pristine: ×1.0
  good:     ×0.9
  worn:     ×0.65
  damaged:  ×0.35
  broken:   ×0.05

reputation_multiplier (本系统特有):
  Novice (0-19):   ×0.8
  Known (20-39):   ×1.0
  Respected (40-59): ×1.1
  Famous (60-79):  ×1.2
  Legendary (80-100): ×1.3
```

### 11.2 金币支出项目

| 支出项目 | 金额范围 | 说明 |
|----------|:--------:|------|
| 招募新角色 | 50-200 gp | 基于角色等级和稀有度 |
| 装备购买 | 10-5,000 gp | 基于物品稀有度 |
| 装备修复 | 10-50 gp/级 | 每个 condition level |
| 药水/卷轴购买 | 25-500 gp | 基于物品等级 |
| 训练费用 | 100-1,000 gp | 学习新技能/专长 |
| 酒馆升级 | 500-10,000 gp | 每级递增 |
| 复活角色 | 1,000 gp + 材料 | 神殿 Lv7 |
| 伤疤移除 | 500 gp | 神殿祈祷 |

### 11.3 金币经济平衡表

| 冒险等级 | 平均收入/冒险 | 平均支出/酒馆访问 | 净收入 | 说明 |
|:--------:|:------------:|:----------------:|:------:|------|
| Lv1-2 | 150 gp | 100 gp | +50 gp | 早期积累 |
| Lv3-4 | 350 gp | 250 gp | +100 gp | 稳定增长 |
| Lv5-6 | 600 gp | 450 gp | +150 gp | 中期平衡 |
| Lv7-8 | 1,000 gp | 800 gp | +200 gp | 高级消费 |
| Lv9-10 | 1,500 gp | 1,200 gp | +300 gp | 顶级经济 |

### 11.4 通胀控制机制

```
通胀控制:
  1. 物品出售递减收益
     - 同一物品类型连续出售: 价格每次 -10% (最低 50%)
     - 每 5 次冒险重置
  
  2. 修复成本递增
     - 物品等级越高，修复成本越高
     - 修复成本 = base_cost × (1 + item_level × 0.1)
  
  3. 冒险金币上限
     - 每次冒险有金币获取上限 (防止刷金)
     - 上限 = 100 × adventure_tier × party_level
  
  4. 稀有物品供应限制
     - 商人物品池有限，每次冒险后刷新
     - 高稀有度物品出现概率低
```

---

## 12. LLM 惩罚生成系统

### 12.1 失败惩罚生成 Prompt

```
System: 你是《酒馆与命运》的地下城主。根据以下冒险失败场景，
生成符合叙事逻辑的惩罚。惩罚必须与失败原因直接相关，让玩家
感受到"这是故事的一部分"而非"这是随机惩罚"。

User:
=== 冒险概述 ===
冒险名称: {adventure_name}
冒险主题: {adventure_theme}
冒险摘要: {adventure_summary}

=== 失败场景 ===
失败原因: {failure_cause}
失败严重程度: {failure_severity}
完成百分比: {completion_percentage}%

=== 参战角色 ===
{for character in party:
  - {character.name} ({character.race} {character.class} Lv{character.level})
    性格: {character.personality_tags}
    当前状态: {character.status} (HP: {character.hp_current}/{character.hp_max})
}

=== 战斗日志 (最近 5 轮) ===
{combat_log_summary}

=== 程序已确定的惩罚 ===
{for penalty in program_penalties:
  - {penalty.type}: {penalty.description} (数值: {penalty.value})
}

=== 生成要求 ===
1. 为每个程序惩罚生成 1-2 句叙事描述
2. 将惩罚自然地融入故事，让玩家理解"为什么会这样"
3. 语气: 庄重但不绝望，暗示"还有希望"
4. 如果有角色死亡，描述死亡的瞬间和队友的反应
5. 如果有装备损坏，描述损坏的原因

输出格式 (严格 JSON):
{
  "narrative_penalties": [
    {
      "penalty_id": "program_penalty_id",
      "narrative": "1-2 句叙事描述"
    }
  ],
  "overall_narrative": "2-3 段整体失败叙事，描述冒险的结局"
}
```

### 12.2 伤疤叙事生成 Prompt

```
System: 你是《酒馆与命运》的伤疤叙事生成器。根据战斗数据和
程序选中的伤疤效果，生成生动的伤疤故事。

User:
=== 战斗数据 ===
角色名: {character.name}
种族: {character.race}
职业: {character.class}
冒险名称: {adventure_name}
伤害来源: {source_name} ({source_type})
总受到伤害: {total_damage}
主要伤害类型: {primary_damage_type}
曾 HP 降至 0: {was_knocked_out}
最后的敌人: {final_enemy}

=== 程序选中的伤疤效果 ===
伤疤名称: {scar_name}
伤疤严重程度: {scar_severity}
数值惩罚: {penalties_summary}
数值补偿: {bonuses_summary}
外观标记: {cosmetic_description}

=== 生成要求 ===
1. narrative: 2-3 句叙事描述
   - 第一句: 描述受伤的瞬间 (具体、生动)
   - 第二句: 描述现在的后遗症 (日常影响)
   - 第三句: 暗示一点正面效果 (伤疤带来的成长)
2. 使用 {narrative_template} 作为基础模板
3. 语气: 像老冒险者在酒馆里展示伤疤时的口吻

输出格式 (JSON):
{"narrative": "..."}
```

### 12.3 英雄传记生成 Prompt

(见 §8.3)

### 12.4 LLM 叙事与程序数值的合并

```
合并流程:
  1. 程序确定所有数值效果 (惩罚/补偿/概率)
  2. 调用 LLM 生成叙事文本
  3. 验证 LLM 输出格式 (JSON Schema)
  4. 合并: 程序数值 + LLM 叙事 = 完整惩罚数据
  5. 应用到游戏状态

  示例:
    程序输出:
      {
        "penalty_id": "gold_loss_30",
        "type": "gold_loss",
        "value": 45,
        "target": "party"
      }
    
    LLM 输出:
      {
        "narrative_penalties": [{
          "penalty_id": "gold_loss_30",
          "narrative": "在撤退的混乱中，你们的金币袋被哥布林抢走了。
                        索林愤怒地挥舞战锤，但敌人已经消失在黑暗中。"
        }]
      }
    
    合并结果:
      {
        "penalty_id": "gold_loss_30",
        "type": "gold_loss",
        "value": 45,
        "target": "party",
        "narrative": "在撤退的混乱中，你们的金币袋被哥布林抢走了。
                      索林愤怒地挥舞战锤，但敌人已经消失在黑暗中。"
      }
```

### 12.5 各严重程度示例输出

#### Minor 失败示例

```json
{
  "severity": "minor",
  "narrative_penalties": [
    {
      "penalty_id": "gold_loss_30",
      "narrative": "在撤退的混乱中，你们的金币袋被哥布林抢走了。"
    },
    {
      "penalty_id": "consumable_used",
      "narrative": "艾拉拉在战斗中用掉了最后一瓶治疗药水。"
    }
  ],
  "overall_narrative": "你们勉强逃出了地牢，虽然损失了一些金币和物资，但至少所有人都活着回来了。索林的伤口还在隐隐作痛，但他笑着说：'下次我们会准备得更充分。'"
}
```

#### Moderate 失败示例

```json
{
  "severity": "moderate",
  "narrative_penalties": [
    {
      "penalty_id": "equipment_damage_2",
      "narrative": "索林的锁子甲在食人魔的重击下裂开了好几处，需要铁匠修复。"
    },
    {
      "penalty_id": "scar_acquired",
      "narrative": "艾拉拉的左臂被酸液溅到，留下了永久的疤痕。她现在对酸液有了本能的恐惧，但也因此更加警觉。"
    }
  ],
  "overall_narrative": "你们被迫撤退，带着伤痕和损失回到了酒馆。索林的铠甲破损严重，艾拉拉的左臂留下了永久的伤疤。但你们学到了宝贵的经验——下次面对酸液喷射者时，要保持距离。"
}
```

#### Severe 失败示例

```json
{
  "severity": "severe",
  "narrative_penalties": [
    {
      "penalty_id": "character_death",
      "narrative": "索林为了掩护队友撤退，独自面对亡灵大军。他的战锤最后一次挥舞，击碎了三个骷髅，但第四个从背后刺穿了他的胸膛。'快走！'——这是他最后的话语。"
    },
    {
      "penalty_id": "equipment_lost",
      "narrative": "索林的战锤和锁子甲被亡灵大军践踏，已经无法找回。"
    },
    {
      "penalty_id": "world_state_negative",
      "narrative": "亡灵势力因为这次胜利而更加嚣张，灰谷镇的威胁等级上升了。"
    }
  ],
  "overall_narrative": "你们失去了索林。那个固执、忠诚、嗜酒的矮人战士，为了让我们活下去，选择了留下。他的战锤至今还挂在酒馆壁炉旁的墙上。灰谷镇的亡灵势力因为这次胜利而更加猖獗，但我们发誓——我们会回来的，为了索林。"
}
```

#### Catastrophic 失败示例

```json
{
  "severity": "catastrophic",
  "narrative_penalties": [
    {
      "penalty_id": "party_wipe",
      "narrative": "亡灵大军将你们团团围住，没有任何退路。索林倒下了，艾拉拉倒下了，亚瑟倒下了...最后只剩下你，背靠着冰冷的石墙，看着骷髅们逼近。"
    },
    {
      "penalty_id": "all_equipment_lost",
      "narrative": "你们的所有装备都被亡灵大军夺走，成为了它们的战利品。"
    },
    {
      "penalty_id": "world_state_catastrophic",
      "narrative": "灰谷镇完全沦陷，成为了亡灵的新领地。附近的村庄也开始受到威胁。"
    }
  ],
  "overall_narrative": "全灭。没有人生还。酒馆里，人们沉默地摘下了英雄之壁上的名牌。索林、艾拉拉、亚瑟...他们的故事结束了。但酒馆还在，新的冒险者会来，他们会看到墙上的名字，会问起那些英雄的故事。而我们，必须面对亡灵势力扩张的现实——灰谷镇已经沦陷，下一个会是哪里？"
}
```

### 12.6 LLM 不可用时的降级方案

```
LLM 降级方案:
  当 LLM API 不可用时，使用预置模板:

  Minor 失败模板:
    "你们勉强逃出了{location}，损失了{gold_loss}金币和{consumable_count}件消耗品。
     虽然失败了，但至少所有人都活着回来了。"

  Moderate 失败模板:
    "你们被迫从{location}撤退，带着伤痕回到了酒馆。
     {damaged_equipment_count}件装备需要修复，{scar_count}名角色获得了伤疤。
     这次失败让你们学到了宝贵的经验。"

  Severe 失败模板:
    "你们失去了{dead_character_names}。{death_narrative}
     他们的牺牲不会被遗忘——英雄之壁上将永远刻着他们的名字。"

  Catastrophic 失败模板:
    "全灭。没有人生还。酒馆里，人们沉默地摘下了英雄之壁上的名牌。
     {dead_character_names}...他们的故事结束了。
     但酒馆还在，新的冒险者会来，继续他们未完成的使命。"
```

---

## 13. 测试规格

### 13.1 单元测试

#### Test Suite: XP 计算

```
TEST 1: 基础遭遇 XP 计算
  Input: difficulty="medium", avg_level=3
  Expected: 100 × 3 = 300

TEST 2: 参与度乘数 - 存活
  Input: survived=true, knocked_out=false
  Expected: 1.0 (base) + 0.1 (从未倒地) = 1.1

TEST 3: 参与度乘数 - 救回队友最多
  Input: most_revives=true
  Expected: 1.0 + 0.1 = 1.1

TEST 4: 目标奖励 - 2 个次要目标
  Input: side_objectives=2
  Expected: 1.0 + 0.2 = 1.2

TEST 5: 时间惩罚 - 正常完成
  Input: actual=90min, expected=90min
  Expected: actual <= expected*1.2 → 无惩罚 → 1.0

TEST 6: 总 XP 计算 (v1.3 更新: 参与度和完成奖励已修正)
  Input: 3 medium encounters (avg_level=3), survived+从未倒地, 1 side objective, 短冒险
  Expected: (300×3) × 1.1 × 1.1 + 150 = 1,089 + 150 = 1,239
```

#### Test Suite: 严重程度判定

```
TEST 7: 灾难性 - 全灭
  Input: dead=4, total=4
  Expected: "catastrophic"

TEST 8: 严重 - 有死亡
  Input: dead=2, total=4
  Expected: "severe"

TEST 9: 中等 - 大部分倒地
  Input: dead=0, unconscious=3, total=4
  Expected: "moderate"

TEST 10: 中等 - 低完成度
  Input: dead=0, unconscious=0, completion=50%
  Expected: "moderate"

TEST 11: 轻微 - 高完成度
  Input: dead=0, unconscious=1, completion=80%
  Expected: "minor"

TEST 12: 严重 - 低完成度被击败 (非撤退)
  Input: dead=0, unconscious=2, total=4, completion=20%, retreated=false
  Expected: "severe"
  → completion < 25% 且非主动撤退: 仍判 Severe
```

#### Test Suite: 伤疤选择

```
TEST 13: 伤害类型筛选
  Input: damage_type="fire", existing_scar_ids=[]
  Expected: 返回火焰伤疤池中的候选

TEST 14: 排除已存在伤疤
  Input: damage_type="fire", existing_scar_ids=["scar_fire_trauma"]
  Expected: 候选中不包含 scar_fire_trauma

TEST 15: 严重程度筛选
  Input: failure_severity="moderate"
  Expected: scar_severity 为 "light" (60%), "moderate" (35%), 或 "severe" (5%)
```

#### Test Suite: 金币经济

```
TEST 16: 物品出售价格
  Input: base_value=100, rarity="common", condition="pristine", reputation="known"
  Expected: 100 × 1 × 0.3 × 1.0 × 1.0 = 30

TEST 17: 修复成本
  Input: condition_levels_to_repair=2, item_level=3
  Formula: base_repair_cost(per_level) = 10gp; cost = (condition_levels × 10) × (1 + item_level × 0.1)
  Expected: (2 × 10) × (1 + 3 × 0.1) = 20 × 1.3 = 26 gp

TEST 18: 失败金币损失 (v1.4 修正：分级固定值)
  Input: party_avg_level=3, severity="moderate"
  Expected: 50 gp 损失 (Lv3-4 Moderate = 50gp, 参见 GDD-v1.md 金币损失分级表)
```

#### Test Suite: 声望阈值

```
TEST 19: 声望等级判定
  Input: reputation=25
  Expected: tier="known"

TEST 20: 声望变化
  Input: old=15, change=+10
  Expected: new=25, tier crossed from "novice" to "known"

TEST 21: 声望下限
  Input: reputation=5, change=-10
  Expected: new=0 (不会变为负数)
```

#### Test Suite: 传承点

```
TEST 22: 传承点计算 - Lv5 (v1.3: level+5)
  Input: level=5
  Expected: 5 + 5 = 10 HP

TEST 23: 传承点消费
  Input: available=10, spend=5
  Expected: available=5, spent=5

TEST 24: 传承点不足
  Input: available=10, spend=14
  Expected: 返回 false
```

### 13.2 集成测试

```
TEST 24: 完整成功结算流程
  Given: 4 人队伍完成短冒险
  Step 1: 触发成功结算
  Step 2: 验证 XP 分配正确
  Step 3: 验证战利品生成
  Step 4: 验证关系更新
  Step 5: 验证声望增加
  Step 6: 验证世界状态正向变化
  Step 7: 验证 LLM 叙事生成

TEST 25: 完整失败结算流程 (Minor)
  Given: 4 人队伍，1 人倒地，完成 80%
  Step 1: 触发失败结算
  Step 2: 验证严重程度 = "minor"
  Step 3: 验证金币损失（分级固定值，见 GDD-v1.md §13.2）
  Step 4: 验证无伤疤生成
  Step 5: 验证声望减少 5

TEST 26: 完整失败结算流程 (Moderate)
  Given: 4 人队伍，全队撤退，完成 50%
  Step 1: 触发失败结算
  Step 2: 验证严重程度 = "moderate"
  Step 3: 验证 1-2 件装备损坏
  Step 4: 验证 1-2 个伤疤生成
  Step 5: 验证声望减少 10

TEST 27: 完整失败结算流程 (Severe)
  Given: 4 人队伍，2 人死亡，2 人撤退
  Step 1: 触发失败结算
  Step 2: 验证严重程度 = "severe"
  Step 3: 验证死亡角色处理
  Step 4: 验证传承触发
  Step 5: 验证声望减少 15

TEST 28: 完整失败结算流程 (Catastrophic)
  Given: 4 人队伍全灭
  Step 1: 触发失败结算
  Step 2: 验证严重程度 = "catastrophic"
  Step 3: 验证所有非绑定装备丢失
  Step 4: 验证所有角色标记死亡
  Step 5: 验证声望减少 20

TEST 29: 角色死亡流程
  Given: 角色 HP=0
  Step 1: 验证 Unconscious 状态
  Step 2: 等待 3 轮无治疗
  Step 3: 验证角色死亡
  Step 4: 验证传承触发
  Step 5: 验证英雄传记生成

TEST 30: 世界状态变异
  Given: 冒险成功，主题="拯救城镇"
  Step 1: 验证 region.state 从 "threatened" 变为 "safe"
  Step 2: 验证 region.threat_level 减少
  Step 3: 验证世界状态持久化
```

### 13.3 边缘情况

```
EDGE 1: 全灭后传承
  Given: 4 人全灭
  Expected: 所有 4 个角色触发传承，玩家可选择继承哪个

EDGE 2: 最大伤疤数
  Given: 角色已有 3 个火焰伤疤
  Expected: 新火焰伤疤替换最旧的

EDGE 3: 金币溢出
  Given: party_gold = 999999, 收入 1000
  Expected: party_gold = 999999 (上限)

EDGE 4: 声望溢出
  Given: reputation = 95, 增加 10
  Expected: reputation = 100 (上限)

EDGE 5: 传承点不足
  Given: available = 2, 尝试消费 3
  Expected: 返回 false，不执行

EDGE 6: 复活超时
  Given: 角色死亡后 4 次冒险
  Expected: 无法复活

EDGE 7: 重复伤疤
  Given: 角色已有 scar_fire_trauma
  Expected: 新伤疤不能是 scar_fire_trauma

EDGE 8: 世界状态版本兼容
  Given: 旧版本存档
  Expected: 自动迁移到新版本
```

### 13.4 平衡验证

```
BALANCE 1: 金币经济可持续性
  验证: 平均每 10 次冒险，玩家能负担得起 1 次酒馆升级
  方法: 模拟 1000 次冒险循环，统计金币净流入

BALANCE 2: XP 曲线
  验证: Lv1→Lv5 需要约 10-15 次短冒险
  方法: 计算平均 XP/冒险，验证升级节奏

BALANCE 3: 伤疤频率
  验证: 每 3-5 次失败冒险产生 1 个伤疤
  方法: 模拟失败场景，统计伤疤生成概率

BALANCE 4: 死亡频率
  验证: 每 10-15 次冒险有 1 次角色死亡
  方法: 模拟战斗，统计死亡概率

BALANCE 5: 声望增长
  验证: 从 Novice 到 Legendary 需要约 50-70 次冒险
  方法: 计算平均声望/冒险，验证增长曲线
```

---

## 附录 A: 伤疤 JSON 数据文件格式

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "ScarPool",
  "type": "array",
  "items": {
    "type": "object",
    "required": ["id", "name", "damage_type", "severity", "effects", "cosmetic"],
    "properties": {
      "id": {
        "type": "string",
        "pattern": "^scar_[a-z_]+$"
      },
      "name": {
        "type": "string"
      },
      "damage_type": {
        "type": "string",
        "enum": ["fire", "cold", "lightning", "necrotic", "psychic", "physical", "poison", "acid", "generic"]
      },
      "severity": {
        "type": "string",
        "enum": ["light", "moderate", "severe"]
      },
      "narrative_template": {
        "type": "string"
      },
      "effects": {
        "type": "object",
        "properties": {
          "penalties": {
            "type": "array",
            "items": {
              "type": "object",
              "properties": {
                "type": { "type": "string" },
                "value": {},
                "description": { "type": "string" }
              }
            }
          },
          "bonuses": {
            "type": "array",
            "items": {
              "type": "object",
              "properties": {
                "type": { "type": "string" },
                "value": {},
                "description": { "type": "string" }
              }
            }
          }
        }
      },
      "cosmetic": {
        "type": "object",
        "properties": {
          "body_part": { "type": "string" },
          "visual_effect": { "type": "string" },
          "sprite_overlay": { "type": "string" },
          "description": { "type": "string" }
        }
      },
      "removal_methods": {
        "type": "array",
        "items": {
          "type": "string",
          "enum": ["temple", "potion", "quest", "time", "replacement"]
        }
      }
    }
  }
}
```

---

## 附录 B: 知识标签 JSON 数据文件格式

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "KnowledgeTags",
  "type": "array",
  "items": {
    "type": "object",
    "required": ["tag", "name", "description", "mechanical_effect", "acquisition_condition"],
    "properties": {
      "tag": {
        "type": "string",
        "pattern": "^[a-z_]+$"
      },
      "name": {
        "type": "string"
      },
      "description": {
        "type": "string"
      },
      "mechanical_effect": {
        "type": "string"
      },
      "adventure_theme": {
        "type": "string"
      },
      "acquisition_condition": {
        "type": "object",
        "properties": {
          "type": {
            "type": "string",
            "enum": ["adventure_completed", "boss_defeated", "trap_triggered", "hidden_found", "ko_by_type"]
          },
          "count": { "type": "integer" },
          "theme": { "type": "string" }
        }
      }
    }
  }
}
```

---

## 附录 C: 传承点消费 JSON 数据文件格式

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "HeritagePointCosts",
  "type": "array",
  "items": {
    "type": "object",
    "required": ["cost", "effect", "duration", "description"],
    "properties": {
      "cost": {
        "type": "integer",
        "minimum": 1,
        "maximum": 10
      },
      "effect": {
        "type": "string"
      },
      "duration": {
        "type": "string",
        "enum": ["permanent", "one_adventure"]
      },
      "description": {
        "type": "string"
      },
      "mechanical": {
        "type": "object"
      }
    }
  }
}
```

---

*文档版本: v1.3*  
*创建日期: 2026-05-04*  
*修订日期: 2026-05-10 (v1.3 — MAJOR REVISION: 5项创意决策执行)*  
*状态: 已修订 (待重新审查)*  
*下一步: `/design-review 08-failure-growth.md --depth lean` 重新审查*
