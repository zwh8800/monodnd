# 地图与探索系统 — 技术设计文档

> **Subsystem**: Map & Exploration
> **Game**: 《酒馆与命运》(Tavern & Destiny)
> **Rules Reference**: DND 5e SRD
> **Language Policy**: 游戏文本统一采用简体中文，技术标识符使用英文snake_case
> **Version**: 1.2 — MVP + Phase 2 scope (2026-05-10 revision)
> **Status**: 已修订 — `/design-review` 审查后修复 7个硬阻断 + 8个结构性修复
> **对应GDD**: GDD-v1.md §4.2, §4.3, §5.6

> **v1.2 修订摘要** (2026-05-10):
> - 🔴 B1: tileset格式从Godot .tres改为MonoGame兼容 (.png spritesheet + .json元数据)
> - 🔴 B2: 定义 `max_vision=40 tiles` 常量并注册到§1B调参
> - 🔴 B3: 新增探索节点(exploration_ruins)和分支节点(branch_crossroads)房间模板
> - 🔴 B4: Flammable_Liquid火灾修复 — LOS=true + spread_chance降至25% + max_spread_targets=3
> - 🔴 B5: CR预算简化表对齐TEST 40 (Deadly Lv5×4从CR 10.0修正为8.5)
> - 🔴 B6: WCAG 1.4.1修复 — 标签颜色+形状双重编码
> - 🔴 B7: 分支选择添加二次确认机制
> - 🟡 S1: MVP添加最小分支节点（末端1个二选一branch）
> - 🟡 S2: 被动察觉重新设计 — DC范围收窄至10-15, 手动Search为主要发现通道
> - 🟡 S3: FOV方法统一为GoRogue内置递归阴影投射
> - 🟡 S4: 房间模板Rotation启用 (90°/180°/270°)
> - 🟡 S5: 标签碰撞优先级正式纳入§6.5设计规格
> - 🟡 S6: TEST 34计算修正 (passive=21而非11)
> - 🟡 S7: 视觉提示从12+层精简至5层
> - 🟡 S8: 走廊遭遇率从15%调至25-30%

---

## 1. 概述

### 1.1 系统目的

地图与探索系统是"冒险循环"的空间载体，负责将LLM生成的叙事蓝图转化为可交互的tile-based地图，并管理玩家在地图上的所有探索行为。本系统是战斗系统、角色系统和LLM叙事系统之间的空间桥梁——战斗发生在地图的某个房间中，角色技能决定探索的结果，LLM为每个空间节点提供叙事皮肤。

### 1.2 核心设计决策

| 决策 | 方案 | 理由 |
|------|------|------|
| 地图来源 | LLM生成叙事蓝图 → 程序实例化为可玩地图 | GDD §4.2三层管线架构；叙事与地图天然关联 |
| 地图结构 | 基于节点的有向图 (node graph) + tile-based房间渲染 | 节点图处理宏观结构（分支/连接），tile系统处理微观交互 |
| 程序层独立性 | 所有地图生成、探索检定、战斗触发逻辑由程序控制，不依赖LLM | GDD §4.2关键原则：程序层不依赖LLM，可完全单元测试 |
| LLM角色 | 场景描写、NPC对话、检定叙述由DM Agent实时生成 | LLM = 皮肤层，不决定结果，只决定表述 |
| 交互标签 | 场景物体使用枚举标签（Pushable/Flammable等），非自由文本 | GDD §5.6明确要求；程序根据标签渲染交互选项和处理效果 |
| Tile规格 | 32×32像素 / tile, 5尺 × 5尺 游戏内 | GDD §5.5战斗UI规范；与战斗地图网格一致 |
| 迷雾系统 | 三层状态 (unexplored/explored/visible) + 视线ray casting | DND标准的探索不确定性 |

### 1.3 与其他系统的关系

```
                    ┌──────────────────────┐
                    │   Adventure System    │
                    │  (冒险生成系统)        │
                    │  ─────────────────── │
                    │  · 提供 adventure_    │
                    │    blueprint.json     │
                    │  · 节点图(nodes[])    │
                    │  · 主题标签            │
                    │  · 交互标签分配        │
                    └──────────┬───────────┘
                               │
                               ▼
              ┌────────────────┴────────────────┐
              │       Map & Exploration          │
              │       (本系统)                    │
              │  ─────────────────────────────── │
              │  · 节点图生成算法                  │
              │  · 房间/走廊Tile渲染               │
              │  · 迷雾与视野                      │
              │  · 探索行动(Search/Interact等)     │
              │  · 交互标签系统                    │
              │  · 遭遇放置                       │
              │  · 陷阱与隐藏物体                   │
              │  · 小地图                         │
              └────┬──────────────┬──────────────┘
                   │              │
    ┌──────────────┴───┐   ┌──────┴──────────────┐
    │  Character System │   │   Combat System     │
    │  (角色系统)        │   │   (战斗系统)         │
    │  ──────────────── │   │  ─────────────────  │
    │  · 察觉/调查检定    │   │  · 遭遇触发→战斗    │
    │  · 移动速度        │   │  · 地形加成        │
    │  · 黑暗视觉        │   │  · 交互标签效果     │
    │  · 隐匿/巧手检定    │   │  · 环境伤害        │
    └───────────────────┘   └─────────────────────┘
                   │              │
    ┌──────────────┴──────────────┴──────────────┐
    │              LLM Gateway                    │
    │  ────────────────────────────────────────  │
    │  · DM Agent: 场景描写、检定叙述             │
    │  · 文案Agent: 可读物品内容生成               │
    └─────────────────────────────────────────────┘
```

### 1.4 MVP范围

| 功能 | MVP (Phase 1) | Phase 2+ |
|------|:---:|:---:|
| 节点图生成 (combat/dialogue/exploration/rest/boss + 1 branch) | YES | 全部8种节点类型 |
| 地图结构 (线性主路径 + 末端1二选一分支) | YES | 完整分支+可选路径网络 |
| 基础Tileset渲染 (地牢/森林/城镇) | 3种Tileset | 10+种Tileset |
| 迷雾系统 (三层状态) | YES | 动态光源交互 |
| 基础探索行动 (Search/Interact) | YES | Cast Spell/Use Item探索 |
| 小地图 | YES | 全图Toggle |
| 交互标签 (Pushable/Flammable/Climbable/Breakable/Readable) | 5个标签 | 全部8个标签 |
| 陷阱系统 | 3种陷阱 | 全量陷阱 |
| 场景转换 | fade + 预加载 | slide/instant + 动画过渡 |
| 房间模板 | 8个MVP模板 | 20+模板(含多主题变体) |
| 房间模板旋转 (90°/180°/270°) | YES | 镜像变换 |
| 被动察觉机制 | 辅助提示 (DC 10-15, 手动Search为主) | 完整被动+主动双通道 |

---

## 1A. 玩家体验幻想 (Player Fantasy)

> **本节是所有设计决策的锚点。**

当玩家踏入地牢时，他们应该感受到：

- **"每一步都是未知"** — 迷雾系统让探索成为逐渐揭开的发现之旅。黑暗视觉和光源的选择影响你能看到什么——精灵能看到远处微光中的线索，人类必须依靠火把的有限范围。

- **"环境是有生命的"** — 交互标签让场景物体不只是背景。推倒柱子堵路、点燃油桶制造火墙、攀爬高处获得优势——环境是你的盟友也是敌人。

- **"我的技能决定我的体验"** — 感知高的角色能发现隐藏的暗门和陷阱，敏捷高的角色能绕过危险，力量高的角色能推倒障碍物。探索不是"点A到点B"，而是角色的能力在环境中的延伸。

- **"地牢是活的"** — 每扇门后都可能是一个新房间（惊喜），也可能是一个伏击（危险）。你永远不知道下一格是什么——这是探索的核心张力。

### 设计测试
1. **发现测试**: 玩家在5分钟内是否至少有一次"啊哈！"的发现时刻？
2. **选择测试**: 在分支节点，两个选项是否让玩家犹豫？
3. **技能测试**: 一个堆了感知/调查的角色是否在探索中感受到明显优势？

---

## 1B. 可调参数 (Tuning Knobs)

| 参数 | 当前值 | 安全范围 | 影响面 |
|------|:------:|:--------:|--------|
| **迷雾alpha (explored)** | 0.5 | 0.3-0.7 | 已探索区域可辨识度 |
| **黑暗视觉范围(Dwarf/Elf)** | 12 tiles | 8-16 | 种族探索优势 |
| **火把明亮范围** | 8 tiles | 6-12 | 人类探索体验 |
| **火把微光范围** | 8 tiles | 4-10 | 人类探索体验 |
| **最大视野上限 (max_vision)** | 40 tiles | 24-60 | 防止极端视野溢出；darkvision+光源合并的上限 |
| **相机缩放默认** | 2.0x | 1.5-3.0 | 视野范围 |
| **预加载缓存大小** | 5 rooms | 3-10 | 场景切换流畅度 |
| **燃烧传播概率 (Flammable)** | 25%/轮 | 15-35% | 火焰系统威胁度 |
| **燃烧传播概率 (Flammable_Liquid)** | 25%/轮 | 15-30% | Flammable_Liquid传播独立调谐 |
| **隐藏物体DC范围** | 10-15 | 8-18 | 发现难度梯度（MVP窄范围，Phase 2扩展） |
| **CR预算容忍度** | ±0.3 | ±0.1-0.5 | 遭遇难度弹性 |
| **模板变换(Rotation)** | 启用 (90°/180°/270°) | 0°/90°/180°/270° | 房间视觉多样性 |
| **队伍视野模式** | 最优视野 | 最优/领队/各自 | 黑暗视觉体验 |

---

## 1C. 依赖关系 (Dependencies)

| 依赖系统 | 依赖内容 | 状态 | 风险 |
|----------|----------|:----:|:----:|
| **角色系统** | 感知/调查/隐匿/运动技能，黑暗视觉，移动速度 | ✅ 已审查 | 低 |
| **冒险生成系统** | adventure_blueprint.json节点数据 | ✅ 已设计 | 低 |
| **战斗系统** | 遭遇触发→战斗切换，地形加成 | ✅ 已审查 | 中 — 先攻规则需对齐 |
| **LLM集成网关** | DM Agent场景描写 | ✅ 已审查 | 低 |
| **GoRogue** | FOV(递归阴影投射)/A*/地图生成框架 | ✅ 已集成 (v2.6.4) | 低 |

> **设计说明 (v1.2)**: ① 先攻规则以GDD-v1.md为准（每轮重骰先攻）。② FOV采用GoRogue内置递归阴影投射（替代原§7.2.2自定义双射线方案，性能提升10-30×）。③ GoRogue AStar当前仅支持4方向移动——8方向对角线移动需自定义距离度量或自实现加权A*。

---

## 2. 节点图生成算法

### 2.1 输入数据

输入为编剧Agent生成的 `adventure_blueprint` 中的 `plot_outline.nodes[]` 数组（参见 GDD §4.2 及 Appendix A）。

### 2.2 完整节点类型定义

#### 2.2.1 `combat` — 战斗遭遇节点

```json
{
  "node_id": "combat_goblin_ambush",
  "type": "combat",
  "description": "狭窄的山谷通道中，一群哥布林从岩壁后跳出来伏击队伍",
  "encounter_id": "enc_goblin_ambush_01",
  "encounter_config": {
    "difficulty": "medium",
    "cr_budget": 1.5,
    "enemies": [
      { "template_id": "goblin_warrior", "count": 3, "cr": 0.25 },
      { "template_id": "goblin_shaman", "count": 1, "cr": 0.5 }
    ],
    "total_enemy_count": 4,
    "formation": "ambush_flanks",
    "trigger_type": "proximity",
    "trigger_radius_tiles": 6,
    "can_avoid": false,
    "avoid_dc": null,
    "avoid_skill": null,
    "loot_pool": "tier_1_common",
    "terrain_features": [
      { "type": "boulder", "tags": ["HalfCover", "Climbable"], "position": [4, 7] },
      { "type": "crate", "tags": ["Breakable", "Flammable"], "position": [12, 3] }
    ]
  },
  "connections": [
    { "target_node": "rest_camp_clearing", "direction": "east", "door_type": "open" }
  ],
  "theme_tags": ["canyon", "ambush", "goblin"],
  "interaction_tags": ["Climbable", "Flammable", "Breakable"],
  "room_size": [20, 15]
}
```

**字段说明**:

| 字段 | 类型 | 必需 | 说明 |
|------|------|:---:|------|
| `node_id` | string | YES | 唯一节点标识符 |
| `type` | "combat" | YES | 节点类型枚举 |
| `description` | string | YES | LLM生成的叙事描述（非机械性文本, 用于场景描写） |
| `encounter_id` | string | YES | 遭遇配置的唯一ID，关联遭遇预制体数据库 |
| `encounter_config.difficulty` | enum | YES | `trivial` / `easy` / `medium` / `hard` / `deadly` |
| `encounter_config.cr_budget` | number | YES | 遭遇CR预算总值（基于4人队伍Lv=recommended_level计算） |
| `encounter_config.enemies[]` | array | YES | 敌人列表 |
| `encounter_config.formation` | enum | YES | 敌人初始阵型: `line` / `arc` / `ambush_flanks` / `scatter` / `rear_ambush` / `boss_front` |
| `encounter_config.trigger_type` | enum | YES | `proximity` / `interaction` / `skill_check_failure` / `timed` |
| `encounter_config.can_avoid` | bool | YES | 是否可通过技能检定或对话跳过战斗 |
| `encounter_config.loot_pool` | string | YES | 战利品池ID |
| `terrain_features` | array | YES | 战斗房间中的地形特征 |
| `connections` | array | YES | 出口连接（至少1个） |
| `theme_tags` | array | YES | 主题标签，用于Tileset选择和房间装饰 |
| `interaction_tags` | array | YES | 可用的交互标签枚举 |
| `room_size` | [int, int] | NO | 房间尺寸(tiles)，默认[20,15] |

#### 2.2.2 `dialogue` — NPC对话节点

```json
{
  "node_id": "dialogue_elf_guardian",
  "type": "dialogue",
  "description": "一位银发精灵守护者站在古老的石门前，他审视着你们的队伍",
  "npc_config": {
    "npc_id": "npc_elf_guardian_lyrian",
    "name": "莱瑞安·月歌",
    "stat_block_id": "elf_knight",
    "personality_tags": ["高傲", "智慧", "警觉"],
    "role": "gate_guardian",
    "initial_attitude": "neutral",
    "dialogue_tree_id": "dt_elf_guardian_intro",
    "key_topics": ["ancient_gate", "lost_kingdom", "party_origins"],
    "reaction_thresholds": {
      "hostile_if": "player_attacks",
      "friendly_if": "persuasion_dc_15_or_elf_in_party",
      "helpful_if": "persuasion_dc_20_or_share_ancient_knowledge"
    }
  },
  "dialogue_outcomes": [
    {
      "outcome_id": "guardian_permitted",
      "trigger": "become_friendly_or_higher",
      "effect": "open_gate",
      "target_node": "ancient_chamber"
    },
    {
      "outcome_id": "guardian_combat",
      "trigger": "attack_or_become_hostile",
      "effect": "combat_trigger",
      "target_node": "combat_elf_guardian"
    },
    {
      "outcome_id": "guardian_tricked",
      "trigger": "deception_dc_18",
      "effect": "open_gate_with_warning",
      "target_node": "ancient_chamber",
      "consequence": "elf_guardian_later_ambush"
    }
  ],
  "connections": [
    { "target_node": "ancient_chamber", "direction": "north", "door_type": "locked_magical" },
    { "target_node": "forest_trail", "direction": "south", "door_type": "open" }
  ],
  "theme_tags": ["forest", "ancient", "elven"],
  "interaction_tags": ["Readable", "Climbable"],
  "room_size": [20, 15]
}
```

**字段说明**:

| 字段 | 类型 | 必需 | 说明 |
|------|------|:---:|------|
| `npc_config` | object | YES | NPC完整对话配置 |
| `npc_config.dialogue_tree_id` | string | YES | 对话树配置ID |
| `npc_config.reaction_thresholds` | object | YES | 态度转变阈值（程序判定，LLM仅生成对话文本） |
| `dialogue_outcomes` | array | YES | 对话可能的结果分支 |
| `dialogue_outcomes[].trigger` | string | YES | 触发条件表达式 |
| `dialogue_outcomes[].effect` | string | YES | 效果标识 |
| `dialogue_outcomes[].consequence` | string | NO | 后续叙事后果标记 |

#### 2.2.3 `exploration` — 探索/发现节点

```json
{
  "node_id": "exploration_abandoned_library",
  "type": "exploration",
  "description": "一个被遗忘的图书室，书架倒塌，满地散落着羊皮纸。空气中弥漫着古老的魔法气息",
  "exploration_config": {
    "hidden_checks": [
      {
        "check_id": "secret_door_behind_bookcase",
        "type": "secret_door",
        "detection_skill": "perception",
        "detection_dc": 15,
        "passive_detection_possible": true,
        "reveal_condition": "proximity_3_tiles",
        "effect_on_find": "unlock_room",
        "target_node": "hidden_vault"
      },
      {
        "check_id": "trapped_floor_rune",
        "type": "trap",
        "detection_skill": "investigation",
        "detection_dc": 13,
        "passive_detection_possible": false,
        "trap_config": {
          "trap_id": "trap_arcane_rune",
          "trigger_type": "step_on",
          "effect": "arcane_explosion",
          "damage": "3d6",
          "damage_type": "force",
          "save_stat": "dex",
          "save_dc": 14,
          "disarm_skill": "arcana",
          "disarm_dc": 15
        }
      }
    ],
    "loot_spots": [
      {
        "spot_id": "ancient_tome",
        "item_id": "tome_forgotten_knowledge",
        "detection_dc": 12,
        "check_skill": "investigation",
        "guaranteed": false,
        "spawn_chance": 0.7
      },
      {
        "spot_id": "scroll_shelf",
        "loot_pool": "tier_2_scrolls",
        "detection_dc": 10,
        "check_skill": "perception",
        "guaranteed": true,
        "spawn_chance": 1.0
      }
    ],
    "environmental_storytelling": [
      {
        "cue_id": "blood_trail",
        "visual_hint": "地板上有一道干涸的血迹，通向东北角的书柜",
        "related_check": "secret_door_behind_bookcase",
        "hint_bonus": "passive_perception_dc_reduce_2"
      },
      {
        "cue_id": "scorch_marks",
        "visual_hint": "墙壁上有爆炸留下的焦痕，呈放射状",
        "related_check": "trapped_floor_rune",
        "hint_bonus": "investigation_dc_reduce_3"
      }
    ]
  },
  "connections": [
    { "target_node": "hallway_west", "direction": "west", "door_type": "open" },
    { "target_node": "hidden_vault", "direction": "east", "door_type": "hidden",
      "requires_check": "secret_door_behind_bookcase" }
  ],
  "theme_tags": ["dungeon", "arcane", "ruins"],
  "interaction_tags": ["Readable", "Breakable", "Flammable"],
  "room_size": [24, 18]
}
```

**字段说明**:

| 字段 | 类型 | 必需 | 说明 |
|------|------|:---:|------|
| `hidden_checks` | array | YES | 隐藏检定列表 |
| `hidden_checks[].passive_detection_possible` | bool | YES | 是否可通过被动察觉发现 |
| `hidden_checks[].reveal_condition` | string | YES | 发现条件: `proximity_N_tiles` / `interaction` / `skill_check` |
| `loot_spots` | array | NO | 战利品生成点 |
| `loot_spots[].guaranteed` | bool | YES | 是否必定生成（false则使用spawn_chance概率） |
| `environmental_storytelling` | array | YES | 环境叙事线索 |
| `environmental_storytelling[].hint_bonus` | string | YES | 发现该线索后对相关检定的DC降低值 |

#### 2.2.4 `puzzle` — 谜题节点

```json
{
  "node_id": "puzzle_elemental_pedestals",
  "type": "puzzle",
  "description": "四个石台环绕着中央的魔法结界，每个石台上刻着不同的元素符文",
  "puzzle_config": {
    "puzzle_id": "puz_elemental_sequence",
    "puzzle_type": "sequence_activation",
    "elements": [
      { "id": "pedestal_fire", "rune": "火", "position": [4, 5], "hint_text": "从灰烬中重生" },
      { "id": "pedestal_water", "rune": "水", "position": [4, 10], "hint_text": "滋养万物的源泉" },
      { "id": "pedestal_earth", "rune": "土", "position": [16, 5], "hint_text": "坚实不移的基础" },
      { "id": "pedestal_air", "rune": "风", "position": [16, 10], "hint_text": "无形却无所不在" }
    ],
    "solution": ["pedestal_fire", "pedestal_air", "pedestal_water", "pedestal_earth"],
    "solution_logic": "激活顺序对应四大元素的诞生顺序",
    "max_attempts": 3,
    "hints_available": [
      {
        "hint_level": 1,
        "unlock_condition": "investigation_dc_12_or_time_60s",
        "text": "石台下的铭文中提到了'创世之歌'"
      },
      {
        "hint_level": 2,
        "unlock_condition": "history_dc_15_or_arcane_dc_14",
        "text": "火焰生于虚空，风随其后，水从天降，大地最后凝聚"
      }
    ],
    "success_effect": "barrier_dispelled",
    "failure_effect": "elemental_guardians_awaken",
    "failure_consequence": {
      "type": "combat",
      "encounter_id": "enc_elemental_guardians",
      "can_retry_after_combat": true
    },
    "on_solve": {
      "unlock_connection": "inner_sanctum",
      "grant_loot_pool": "tier_2_rare"
    }
  },
  "connections": [
    { "target_node": "inner_sanctum", "direction": "north", "door_type": "magical_barrier",
      "initially_locked": true, "unlocked_by": "puzzle_solve" },
    { "target_node": "hallway_south", "direction": "south", "door_type": "open" }
  ],
  "theme_tags": ["ancient", "arcane", "temple"],
  "interaction_tags": ["Readable", "Electrical"],
  "room_size": [24, 18]
}
```

**字段说明**:

| 字段 | 类型 | 必需 | 说明 |
|------|------|:---:|------|
| `puzzle_type` | enum | YES | `sequence_activation` / `matching_pairs` / `riddle_answer` / `pressure_plates` / `light_refraction` / `chess_logic` |
| `solution` | array/string | YES | 正确答案（格式取决于puzzle_type） |
| `max_attempts` | int | YES | 失败多少次后触发failure_effect |
| `hints_available` | array | YES | 可获取的提示（通过技能检定或时间推移解锁） |
| `failure_effect` | string | YES | 失败效果描述 |
| `failure_consequence.type` | enum | YES | `combat` / `damage` / `trap` / `lock_permanently` |
| `on_solve.unlock_connection` | string | NO | 解决后解锁的连接（门/通道） |

#### 2.2.5 `merchant` — 商人节点

```json
{
  "node_id": "merchant_wandering_alchemist",
  "type": "merchant",
  "description": "一辆破旧的马车停在小路边，车上坐着一位戴着圆眼镜的地精炼金术士",
  "merchant_config": {
    "npc_id": "npc_goblin_alchemist_zix",
    "name": "兹克斯",
    "stat_block_id": "goblin_commoner",
    "personality_tags": ["狡猾", "健谈", "贪财"],
    "shop_inventory": {
      "guaranteed_items": [
        { "item_id": "health_potion", "count": 3, "price_gp": 50 },
        { "item_id": "antidote", "count": 2, "price_gp": 40 },
        { "item_id": "alchemist_fire", "count": 1, "price_gp": 75 }
      ],
      "random_items_pool": "tier_1_potions",
      "random_count": 3,
      "random_price_multiplier_range": [0.8, 1.5],
      "special_items": [
        {
          "item_id": "elixir_of_darkvision",
          "count": 1,
          "price_gp": 120,
          "requires_persuasion_dc": 15,
          "unlock_description": "兹克斯从马车底下的暗格中取出这瓶药水"
        }
      ]
    },
    "bargain_config": {
      "can_haggle": true,
      "haggle_skill": "persuasion",
      "haggle_dc_base": 15,
      "max_discount_percent": 30,
      "each_attempt_dc_increase": 2,
      "max_attempts": 3,
      "failure_penalty": "price_increase_10_percent"
    },
    "buyback_config": {
      "accepts_sell": true,
      "buy_percentage_of_value": 0.4,
      "special_buy_tags": ["potion", "reagent"],
      "special_buy_multiplier": 0.6
    }
  },
  "connections": [
    { "target_node": "crossroads", "direction": "west", "door_type": "open" },
    { "target_node": "swamp_path", "direction": "east", "door_type": "open" }
  ],
  "theme_tags": ["forest", "road", "merchant"],
  "interaction_tags": ["Readable", "Flammable"],
  "room_size": [16, 12]
}
```

#### 2.2.6 `rest` — 休整节点

```json
{
  "node_id": "rest_hidden_grotto",
  "type": "rest",
  "description": "瀑布后隐藏着一个温暖的小洞窟，空气中弥漫着萤火虫的微光",
  "rest_config": {
    "rest_type": "short_rest",
    "short_rest_bonuses": [
      { "type": "bonus_recovery", "effect": "额外恢复1个Hit Die (自然温泉效果)",
        "trigger": "use_grotto_spring" }
    ],
    "long_rest_available": false,
    "long_rest_conditions": ["not_in_danger_zone", "adventure_progress_percent_gt_40"],
    "camp_events": [
      {
        "event_id": "grotto_spirits",
        "trigger_chance": 0.35,
        "description": "萤火虫聚集在一起，形成了一个发光的人形……",
        "outcomes": [
          {
            "outcome_id": "blessing",
            "trigger": "wisdom_dc_14_or_nature_dc_12",
            "effect": "grant_temp_hp_d6_to_all"
          },
          { "outcome_id": "neutral", "trigger": "default", "effect": "narrative_only" }
        ]
      }
    ],
    "supply_restrictions": {
      "requires_rations": false,
      "environmental_hazard": false,
      "ambush_risk": 0.05
    }
  },
  "connections": [
    { "target_node": "forest_path", "direction": "south", "door_type": "open" }
  ],
  "theme_tags": ["forest", "water", "safe_haven"],
  "interaction_tags": ["Readable", "Climbable"],
  "room_size": [16, 12]
}
```

#### 2.2.7 `boss` — Boss遭遇节点

```json
{
  "node_id": "boss_lich_throne_room",
  "type": "boss",
  "description": "巨大的石制王座立于台阶之上，巫妖王阿尔萨斯端坐其位，亡灵力量在房间中翻涌",
  "encounter_config": {
    "encounter_id": "enc_boss_lich_althazar",
    "difficulty": "deadly",
    "cr_budget": 8.0,
    "boss_enemy": {
      "template_id": "lich_lord",
      "name": "巫妖王·阿尔萨斯",
      "cr": 7,
      "phases": [
        {
          "phase": 1,
          "name": "亡灵召唤",
          "hp_threshold_percent": 100,
          "mechanics": [
            { "type": "summon", "template": "skeleton_warrior", "count": 2, "every_n_rounds": 3 },
            { "type": "passive_aura", "effect": "undead_regeneration_5", "range_tiles": 12 }
          ]
        },
        {
          "phase": 2,
          "name": "死灵风暴",
          "hp_threshold_percent": 50,
          "mechanics": [
            { "type": "aoe", "spell_id": "circle_of_death", "every_n_rounds": 4 },
            { "type": "teleport", "trigger": "damage_taken", "cooldown_rounds": 3 },
            { "type": "passive_aura", "effect": "life_drain_5", "range_tiles": 6 }
          ],
          "phase_transition": {
            "dialogue_narrative": "巫妖王大笑，'凡人的挣扎真是有趣！让你们见识真正的力量！'",
            "vfx": "necromantic_burst",
            "full_heal": false
          }
        }
      ]
    },
    "minions": [
      { "template_id": "skeleton_archer", "count": 2, "cr": 0.25 }
    ],
    "formation": "boss_front",
    "room_features": [
      { "type": "throne_platform", "position": [10, 14], "effect": "elevation_advantage",
        "tags": ["Climbable"] },
      { "type": "necromantic_pillar", "position": [4, 4], "effect": "heal_undead_3_per_round",
        "tags": ["Breakable", "Flammable"] },
      { "type": "necromantic_pillar", "position": [16, 4], "effect": "heal_undead_3_per_round",
        "tags": ["Breakable", "Flammable"] },
      { "type": "soul_cage", "position": [10, 2], "effect": "trapped_souls_release_on_break",
        "tags": ["Breakable", "Electrical"] }
    ],
    "loot_pool": "boss_tier_legendary",
    "guaranteed_loot": [
      { "item_id": "lich_phylactery_fragment", "count": 1 },
      { "item_id": "crown_of_undeath", "count": 1, "requires_attunement": true }
    ]
  },
  "connections": [
    { "target_node": "exit_passage", "direction": "east", "door_type": "sealed",
      "initially_locked": true, "unlocked_by": "boss_defeated" }
  ],
  "theme_tags": ["dungeon", "necromancy", "boss"],
  "interaction_tags": ["Breakable", "Flammable", "Climbable", "Electrical"],
  "room_size": [28, 20]
}
```

**Boss专有字段说明**:

| 字段 | 类型 | 必需 | 说明 |
|------|------|:---:|------|
| `boss_enemy.phases[]` | array | YES | Boss阶段机，每阶段有独立的mechanic集合 |
| `phases[].hp_threshold_percent` | int | YES | 触发此阶段的HP百分比阈值 |
| `phases[].mechanics[]` | array | YES | 该阶段的战斗机制 |
| `phase_transition` | object | NO | 阶段转换时的演出配置（叙事、VFX） |
| `room_features[]` | array | YES | Boss房间特有的交互特征 |

#### 2.2.8 `branch` — 分支选择节点

```json
{
  "node_id": "branch_crossroads_forest",
  "type": "branch",
  "description": "森林中的三岔路口：左边的路标记着精灵符文，中间的路弥漫着腐臭味，右边的路蜿蜒向上通向一处峭壁",
  "branch_config": {
    "branches": [
      {
        "branch_id": "elf_path",
        "label": "精灵符文之路",
        "flavor_text": "发光的符文似乎在轻声呼唤，空气中有花香。也许能找到古老的庇护所。",
        "target_node": "elf_shrine",
        "visible_unless": null,
        "hint_condition": "perception_dc_12_reveal_elf_guard_prints",
        "difficulty_hint": "安全 — 适合寻求帮助和知识的队伍",
        "reward_hint": "古老的精灵魔法物品",
        "cost": null
      },
      {
        "branch_id": "undead_path",
        "label": "腐臭之路",
        "flavor_text": "空气中弥漫着浓烈的腐臭味。危险是肯定的，但有危险的地方常有宝藏。",
        "target_node": "undead_lair",
        "visible_unless": null,
        "hint_condition": "survival_dc_14_reveal_undead_tracks",
        "difficulty_hint": "危险 — 需要战斗准备",
        "reward_hint": "亡灵战利品和稀有装备",
        "cost": null
      },
      {
        "branch_id": "cliff_path",
        "label": "峭壁之路",
        "flavor_text": "小路蜿蜒向上，通向高处的悬崖。视野开阔，但路途险峻。",
        "target_node": "cliff_overlook",
        "visible_unless": "athletics_dc_10_or_climb_speed",
        "hint_condition": null,
        "difficulty_hint": "困难 — 需要攀爬能力",
        "reward_hint": "俯瞰全局的战略优势",
        "cost": { "type": "resource", "resource": "exhaustion_level_1_if_fail_climb_dc_12" }
      }
    ],
    "allow_return": false,
    "branch_locked_after_choice": true,
    "show_on_minimap": true,
    "minimap_label": "Forest Crossroads"
  },
  "connections": [
    { "target_node": "forest_entrance", "direction": "south", "door_type": "open" }
  ],
  "theme_tags": ["forest", "choice", "mystery"],
  "interaction_tags": ["Readable", "Climbable"],
  "room_size": [20, 15]
}
```

**Branch专有字段说明**:

| 字段 | 类型 | 必需 | 说明 |
|------|------|:---:|------|
| `branches[]` | array | YES | 2-4个分支选项 |
| `branches[].visible_unless` | string | NO | 隐藏此选项的条件（""表示始终可见） |
| `branches[].hint_condition` | string | NO | 满足条件时显示额外提示信息 |
| `branches[].difficulty_hint` | string | YES | 难度提示文本 |
| `branches[].reward_hint` | string | YES | 奖励提示文本 |
| `allow_return` | bool | YES | 选择后是否可以返回此节点重新选择 |
| `branch_locked_after_choice` | bool | YES | 选后是否锁定（不可更改） |
| `require_confirm` | bool | YES | 是否要求二次确认（默认true，防止误触导致不可逆选择） |

**分支选择UX规范 (v1.2)**:
1. **两步确认**: 玩家首次点击分支选项 → 选项高亮 + 显示警告文案"此选择不可更改，确定要前往 [分支名] 吗？" → 再次点击确认 → 执行
2. **锁定动画**: 确认后未选中的分支路径播放"关闭"动画（合上门/路径断裂），明确告知"已不可达"
3. **小地图标记**: 已封锁路径在小地图上显示🔒图标
4. **Esc取消**: 首次点击高亮后按Esc可取消选择（回到未选择状态）

### 2.3 节点图生成算法

#### 2.3.1 算法伪码

```
Algorithm: GenerateNodeGraph(adventure_blueprint)
─────────────────────────────────────────────────
Input:  adventure_blueprint (from 编剧Agent)
Output: validated_node_graph (ready for room instantiation)

Step 1 — 提取节点列表
  nodes = adventure_blueprint.plot_outline.nodes[]
  total_nodes = len(nodes)
  difficulty = adventure_blueprint.difficulty_profile

Step 2 — 节点类型验证与填充
  for each node in nodes:
    validate_node_schema(node)          // 根据2.2节schema验证
    fill_defaults(node)                 // 补全可选字段默认值
    validate_cr_budget(node)            // 确保CR预算在预设范围内

Step 3 — 构建邻接矩阵
  adjacency = {}                        // node_id -> [connected_node_ids]
  for each node in nodes:
    for each conn in node.connections:
      adjacency[node.node_id].append(conn.target_node)

Step 4 — 连通性验证 (BFS)
  start_node = find_start_node(nodes)   // 寻找入度为0的起始节点
  visited = bfs(start_node, adjacency)
  unreachable = nodes - visited
  if unreachable:
    raise GraphValidationError("存在不可达节点: unreachable")

Step 5 — 死路处理
  for each node_id in nodes:
    if node_id not in adjacency or len(adjacency[node_id]) == 1:
      outgoing = get_connections(node_id)
      if len(outgoing) == 0 and node.type not in ["boss", "rest"]:
        // 非终点的死路标记为警告
        warn("死路节点非终点: " + node_id)

Step 6 — 分支平衡检查
  branch_weights = compute_branch_distribution(nodes)
  for each branch in branch_nodes:
    weighted_children = branch.branches[]
    avg_difficulty = mean([get_path_difficulty(b.target_node) for b in weighted_children])
    for each child in weighted_children:
      child_difficulty = get_path_difficulty(child.target_node)
      if abs(child_difficulty - avg_difficulty) > THRESHOLD (0.3):
        warn("分支不平衡: " + child.branch_id)

Step 7 — 房间尺寸分配
  for each node in nodes:
    if node.room_size is null:
      node.room_size = get_default_room_size(node.type, total_nodes)

Step 8 — 遭遇放置
  for each node in nodes:
    if node.type in ["combat", "boss", "exploration"]:
      place_encounters(node, difficulty)    // 详见 Section 9

Step 9 — 交互标签分配
  for each node in nodes:
    resolve_tags(node)                     // 合并blueprint标签 + 节点类型默认标签
    validate_tags(node.interaction_tags)   // 确保标签与room_size不冲突

Step 10 — 返回验证后的节点图
  Return: validated_node_graph
```

#### 2.3.2 默认房间尺寸分配表

| 节点类型 | 短冒险 (5-8 nodes) | 中冒险 (15-25 nodes) | 长冒险 (30-50 nodes) |
|----------|:---:|:---:|:---:|
| combat | [16, 12] | [20, 15] | [24, 18] |
| dialogue | [14, 10] | [16, 12] | [20, 15] |
| exploration | [18, 14] | [22, 16] | [26, 20] |
| puzzle | [20, 15] | [24, 18] | [28, 20] |
| merchant | [12, 10] | [16, 12] | [16, 12] |
| rest | [14, 10] | [16, 12] | [18, 14] |
| boss | [22, 16] | [28, 20] | [32, 24] |
| branch | [16, 12] | [20, 15] | [24, 18] |

#### 2.3.3 图验证规则

```
连通性规则:
  R1. 图中所有节点必须从起始节点BFS可达（入度为0的节点为起始）
  R2. 每个节点必须有至少1个入边（除起始节点）
  R3. 分支节点必须有2-4个子节点

死路规则:
  R4. 只有 boss/rest/merchant 节点允许为死路（0个出边）
  R5. 其他节点类型的死路触发警告，自动添加"返回前节点"连接

分支平衡规则:
  R6. 从分支节点出发的各路径总difficulty差异不超过30%
  R7. 每个分支路径的节点数差异不超过50%

CR预算规则:
  R8. 单房间CR预算不得超过队伍平均等级+4
  R9. 连续2个combat房间的CR预算之和不超过队伍平均等级+5

难度曲线规则:
  R10. 起始3个节点中不得出现deadly难度
  R11. boss节点必须是最后3个节点之一
```

---

## 3. 地图渲染规格

### 3.1 Tile规格

| 参数 | 值 | 说明 |
|------|-----|------|
| Tile像素尺寸 | 32 × 32 px | 与GDD §5.5战斗地图规范一致 |
| 游戏内网格尺寸 | 5ft × 5ft / tile | DND标准网格 |
| 角色精灵尺寸 | 16 × 24 px | 2头身像素精灵 (GDD §8.1) |
| 房间默认宽度 | 20 tiles = 100ft | 中冒险combat房间标准 |
| 房间默认高度 | 15 tiles = 75ft | 中冒险combat房间标准 |
| 走廊最小宽度 | 3 tiles = 15ft | 确保至少3人可并排移动 |
| 走廊最大长度 | 12 tiles = 60ft | 过长走廊自动插入视觉分隔 |

### 3.2 层级系统 (Layer System)

```
渲染顺序 (从下到上):
┌──────────────────────────────────────────┐
│ Layer 6 — Fog (迷雾)                      │  ← 最上层，覆盖未探索区域
├──────────────────────────────────────────┤
│ Layer 5 — UI Overlay (UI叠加层)           │  ← 交互提示/角色名字/伤害数字
├──────────────────────────────────────────┤
│ Layer 4 — Effects (特效层)                │  ← 魔法粒子/火焰动画/陷阱激活
├──────────────────────────────────────────┤
│ Layer 3 — Characters (角色层)             │  ← 玩家/NPC/敌人精灵
├──────────────────────────────────────────┤
│ Layer 2 — Objects (物体层)                │  ← 家具/宝箱/陷阱/交互物体
├──────────────────────────────────────────┤
│ Layer 1 — Terrain (地形层)                │  ← 墙壁/地面/水/植被
├──────────────────────────────────────────┤
│ Layer 0 — Background (背景层)             │  ← 天空/远景/房间外部装饰
└──────────────────────────────────────────┘

渲染层级对应:
  Layer 0 = Background (背景层)
  Layer 1 = Terrain (地形层)
  Layer 2 = Objects (物件层)
  Characters/Effects/UI/Fog 使用独立渲染层级
```

### 3.3 Tileset选择算法

```
Algorithm: SelectTileset(node)
─────────────────────────────────
Input:  node (with theme_tags[])
Output: tileset_path (Resource path)

Step 1 — 主题标签解析
  theme_tags = node.theme_tags + adventure_blueprint.theme_tags

Step 2 — Tileset优先级匹配
  Tileset映射表 (按优先级排序):
  MonoGame资源格式: .png (spritesheet, 通过MGCB编译为.xnb) + .json (tile元数据: 碰撞/标签)

  | theme_tag  | Tileset ID        | Spritesheet (.png)                          | 元数据 (.json)                              | 优先级 |
  |------------|-------------------|---------------------------------------------|---------------------------------------------|--------|
  | dungeon    | dungeon_basic     | assets/tilesets/dungeon_basic.png           | assets/tilesets/dungeon_basic.json          | 1 |
  | crypt      | dungeon_crypt     | assets/tilesets/dungeon_crypt.png           | assets/tilesets/dungeon_crypt.json          | 2 |
  | cave       | cave_natural      | assets/tilesets/cave_natural.png            | assets/tilesets/cave_natural.json           | 1 |
  | forest     | forest_temperate  | assets/tilesets/forest_temperate.png        | assets/tilesets/forest_temperate.json       | 1 |
  | swamp      | swamp_murky       | assets/tilesets/swamp_murky.png             | assets/tilesets/swamp_murky.json            | 2 |
  | town       | town_medieval     | assets/tilesets/town_medieval.png           | assets/tilesets/town_medieval.json          | 1 |
  | ruins      | ruins_ancient     | assets/tilesets/ruins_ancient.png           | assets/tilesets/ruins_ancient.json          | 1 |
  | temple     | temple_holy       | assets/tilesets/temple_holy.png             | assets/tilesets/temple_holy.json            | 2 |
  | canyon     | canyon_desert     | assets/tilesets/canyon_desert.png           | assets/tilesets/canyon_desert.json          | 2 |
  | snow       | snow_mountain     | assets/tilesets/snow_mountain.png           | assets/tilesets/snow_mountain.json          | 3 |

  JSON元数据格式:
  {
    "tileset_id": "dungeon_basic",
    "tile_size_px": 32,
    "columns": 16,
    "tile_properties": {
      "0": { "walkable": false, "tags": ["wall"] },
      "1": { "walkable": true,  "tags": ["floor"] },
      ...
    }
  }

Step 3 — 标签匹配
  for each tag in theme_tags:
    if tag in tileset_map:
      // 返回spritesheet路径 + 元数据路径的元组
      return tileset_map[tag]

Step 4 — 默认回退
  return { spritesheet: "assets/tilesets/dungeon_basic.png",
           metadata:   "assets/tilesets/dungeon_basic.json" }
```

### 3.4 房间模板系统

#### 3.4.1 模板数据结构

```json
{
  "template_id": "room_combat_arena",
  "name": "竞技场式战斗房间",
  "node_type": "combat",
  "size": [20, 15],
  "tileset_tags": ["dungeon", "generic"],
  "tile_grid": "data/room_templates/combat_arena.json",
  "rotation": [0, 90, 180, 270],
  "entry_points": [
    { "position": [10, 0], "direction": "south", "label": "南入口" }
  ],
  "exit_points": [
    { "position": [10, 14], "direction": "north", "label": "北出口" }
  ],
  "cover_spots": [
    { "position": [3, 5], "type": "half_cover", "tile": "pillar" },
    { "position": [16, 5], "type": "half_cover", "tile": "pillar" },
    { "position": [3, 10], "type": "three_quarter_cover", "tile": "broken_wall" },
    { "position": [16, 10], "type": "three_quarter_cover", "tile": "broken_wall" }
  ],
  "environmental_features": [
    { "position": [5, 3], "type": "brazier", "tags": ["Flammable"],
      "light_source": { "radius": 8, "dim_radius": 16 } },
    { "position": [15, 12], "type": "debris", "tags": ["Breakable"] }
  ],
  "enemy_spawn_points": [
    { "position": [8, 12], "formation_role": "frontline" },
    { "position": [12, 12], "formation_role": "frontline" },
    { "position": [4, 8], "formation_role": "flanker" },
    { "position": [16, 8], "formation_role": "flanker" }
  ],
  "player_spawn_point": [10, 2],
  "decoration_set": "dungeon_ruined"
}
```

#### 3.4.2 全套MVP房间模板

**模板 1: 竞技场 (combat_arena)** — 标准战斗房间
```
尺寸: 20 × 15 tiles (100ft × 75ft)
布局:
  ┌──────────────────────┐
  │   ██          ██     │  ← 北出口 (x=10, y=14)
  │   ██   [E2]   ██     │     E1/E2 = 敌人前线生成点
  │                      │
  │   █    [E3]  [E4]   █│     █ = 石柱 (half cover)
  │                      │     ██ = 断墙 (three-quarter cover)
  │           ●           │     ● = 火盆 (Flammable, 光源)
  │                      │
  │   █             █    │
  │                      │
  │       [P1][P2]       │     P1-P4 = 玩家生成点 (2×2格)
  │       [P3][P4]       │
  │                      │
  │   ░░░░░░░░░░░░░░░   │     ░ = 碎石地 (difficult terrain)
  │                      │
  └──────────────────────┘
        南入口 (x=10, y=0)
可交互物: 火盆×1 (Flammable), 碎石×4 (Breakable), 石柱×4 (HalfCover)
敌人生成点: 4个
掩体: 石柱×4 (half cover), 断墙×2 (three-quarter cover)
```

**模板 2: 伏击峡谷 (combat_ambush)**
```
尺寸: 20 × 15 tiles (100ft × 75ft)
布局: 两侧高台(Climbable DC 12) + 中央通道
  [E1][E2]在两侧高台上(远程优势), [E3][E4]在地面
可交互物: 高台×2 (Climbable), 松动石块×3 (Pushable, 可从高台推下造成伤害)
特殊: 高台提供 elevated advantage (+2 对下方目标的远程攻击)
```

**模板 3: 狭窄通道 (combat_corridor)**
```
尺寸: 16 × 12 tiles (80ft × 60ft)
布局: 狭窄通道 + 壁垒掩体 + 可破坏木桶路障
可交互物: 木桶×4 (Breakable, Flammable), 壁垒×2 (HalfCover)
敌人生成点: 3个
特殊: 狭窄通道限制了移动，AOE法术效果更显著
```

**模板 4: 对话厅 (dialogue_chamber)**
```
尺寸: 16 × 12 tiles (80ft × 60ft)
布局: NPC中心偏上 + 书柜装饰 + 地毯/可阅读物品
可交互物: 书柜×2 (Readable, 可能含线索), 地毯装饰×3
NPC位置: [8, 9]
特殊: 非敌对区域，角色可自由走动但不可战斗开始
```

**模板 5: 商人营地 (merchant_camp)**
```
尺寸: 16 × 12 tiles (80ft × 60ft)
布局: 商人+货箱 + 篝火(光源) + 帐篷装饰
可交互物: 货箱×2 (Breakable, 商人商品展示), 篝火(Fire, 光源), 帐篷(装饰)
NPC位置: [8, 7]
特殊: 篝火提供短休机会 (但不可长休)
```

**模板 6: 休整洞窟 (rest_grotto)**
```
尺寸: 16 × 12 tiles (80ft × 60ft)
布局: 洞穴围合 + 温泉(healing water: 额外恢复1d4 HP) + 营地篝火
可交互物: 温泉 (每角色每短休可用1次恢复1d4 HP)
特殊: 绝对安全区域，无法触发战斗
```

**模板 7: Boss王座厅 (boss_throne)**
```
尺寸: 28 × 20 tiles (140ft × 100ft)
布局: 王座高台(Climbable) + 死灵柱×4(Breakable AC 13 HP 30, Flammable, Electrical)
      + 魂笼(Breakable, Electrical) + 柱子×4(half cover掩体)
敌人: Boss (王座平台) + 2-3仆从
特殊: Boss房间的门在战斗开始后锁定，Boss死亡后解锁
```

**模板 8: 探索遗迹 (exploration_ruins)** ★ 新增 (v1.2)
```
尺寸: 22 × 16 tiles (110ft × 80ft)
布局: 中央大厅 + 两侧可破坏墙壁 + 倒塌书架(Readable, Flammable)
      + 隐藏隔间(暗门DC 14) + 碎石堆(difficult terrain)
可交互物: 书架×3 (Readable, 可能含线索), 碎石×4 (Breakable, 清理后开辟通道),
          石柱×2 (HalfCover), 火盆×1 (light source)
隐藏物生成点: 4个 (暗门/隐藏隔间/隐藏物品/陷阱各1)
环境线索: 血迹/焦痕/足迹 — 每房间随机2-3条
敌人生成点: 0-2个 (可选遭遇，概率40%)
特殊: 非强制战斗区域，但可能有巡逻敌人(触发概率40%)
      模板包含4个隐藏物槽位，由adventure_blueprint填充具体内容
```

**模板 9: 分岔路口 (branch_crossroads)** ★ 新增 (v1.2, MVP最小分支)
```
尺寸: 20 × 15 tiles (100ft × 75ft)
布局: 中央丁字/十字路口 + 每条岔路有独特视觉标识(符文石门/藤蔓拱门/铁栅栏)
      + 中央信息柱(Readable, 提供各路径的hint信息)
可交互物: 信息柱×1 (Readable, 显示各分支的difficulty_hint/reward_hint),
          路标×3 (Readable, 每条岔路方向标识),
          火盆×1 (light source)
NPC: 可选 — 1个路标守护者NPC (dialogue节点可替换)
特殊: 选择一条岔路后其他路径永久封锁(门关闭动画),
      确认选择需要二次点击(防误触机制),
      小地图上已封锁路径显示🔒图标
```

### 3.5 相机系统

| 参数 | 值 | 说明 |
|------|-----|------|
| 跟随目标 | 队伍领队 (party_leader) | 可切换跟随角色 |
| 相机模式 | smooth follow + 边界限制 | 平滑跟随，不超出房间范围 |
| 平滑速度 | 5.0 (lerp factor) | 越高越快 |
| 默认缩放 | 2.0x | 32px tile放大至64px显示 |
| 缩放范围 | 1.5x ~ 3.0x | 鼠标滚轮缩放 |
| 缩放步进 | 0.25x | 每次滚轮变化量 |
| 房间边距 | 2 tiles | 相机不超出房间边界2格 |
| 战斗自动缩放 | 是 | 进入战斗自动缩放以包含所有敌人+玩家 |

---

## 4. 场景转换系统

### 4.1 转换类型

| 类型 | 时长 | 效果 | 使用场景 |
|------|:---:|------|----------|
| `fade_to_black` | 0.5s | 画面渐黑→加载→渐亮 | 房间切换（默认） |
| `slide_left` | 0.4s | 画面向左滑出，新场景从右滑入 | 相邻房间，方向感知 |
| `slide_right` | 0.4s | 同上但反向 | 同上 |
| `slide_up` | 0.4s | 同上，纵向 | 上下楼层切换 |
| `slide_down` | 0.4s | 同上，纵向反向 | 同上 |
| `instant` | 0.0s | 立即切换 | 同房间内的场景变化（如对话触发） |
| `dissolve` | 0.8s | 像素化溶解 | 记忆/闪回/魔法传送 |
| `portal` | 1.2s | 漩涡特效 | 传送门/跨区域传送 |

### 4.2 触发条件

| 触发器 | 检测方式 | 说明 |
|--------|----------|------|
| 房间边界 | 角色移动到出口tile (doorway position) | 自动触发fade转换 |
| 对话触发 | 对话结束后 effect="open_gate" | 触发slide转换（方向由connection.direction决定） |
| 物品交互 | 使用传送物品 | 触发portal转换 |
| 战斗强制 | boss_defeated事件 | 解锁门后自动trigger |
| GM指令 | Debug/GM模式下 | instant转换 |

### 4.3 预加载策略

- 预加载范围: 当前房间 + 所有相邻房间 (1跳)
- 预加载内容: TileMap + Object层数据 (Characters和Effects不预加载)
- 最多缓存房间数: 5个 (LRU逐出)
- 预加载半径: 1跳
- 卸载策略: 距离当前房间超过1跳的旧房间自动卸载

---

## 5. 探索机制

### 5.1 移动系统

#### 5.1.1 移动参数

| 参数 | 值 | 说明 |
|------|-----|------|
| 移动方式 | 点击目标tile | 鼠标左键点击可到达的tile |
| 路径寻找算法 | A* (A-Star) | 八方向移动 (允许对角线，但碰到角落时阻挡) |
| 基础速度 | 6 tiles/轮 (30ft) | 对应DND标准30ft移动速度 |
| 矮人速度 | 5 tiles/轮 (25ft) | Dwarf基础速度25ft |
| 木精灵速度 | 7 tiles/轮 (35ft) | Wood Elf基础速度35ft |
| 疾跑(Dash) | 速度×2 | 消耗动作(Action) |
| 困难地形 | 速度×0.5 | 碎石/泥沼/荆棘等，每tile消耗2倍移动力 |
| 对角线移动 | 允许 | 每对角tile消耗1.5ft移动力 |
| 穿越友方 | 可穿越 | 友方角色tile视为可通过(不可停留) |
| 穿越敌方 | 不可穿越 | 除非使用Disengage动作或特殊能力 |

#### 5.1.2 A*寻路算法参数

```
A*成本:
  - 正交移动成本: 1.0
  - 对角线移动成本: 1.414 (sqrt(2))
  - 困难地形倍率: 2.0x
  - 启发式: Octile距离 (八方向)
  - 对角线角落阻挡: 如果两个相邻正交tile都不可通行, 禁止对角线移动

算法复杂度: O(N log N) where N = walkable tiles in range
典型查询时间: < 1ms for 20×15 room (300 tiles)
```

### 5.2 探索行动

#### 5.2.1 探索行动定义

| 行动ID | 名称 | 消耗 | 效果 | 范围 | 触发条件 |
|--------|------|:---:|------|:---:|----------|
| `search` | 搜索 (Search) | 动作 | 主动察觉/调查检定，发现当前房间内所有隐藏物体 | 当前房间已探索区域 | 非战斗状态 |
| `interact` | 交互 (Interact) | 附赠动作 | 与场景物体交互 (开门/推箱子/阅读/攀爬) | 1 tile | 任意 |
| `use_item` | 使用物品 (Use Item) | 动作 | 使用背包中的物品 (药水/卷轴/工具) | 取决于物品 | 任意 |
| `cast_spell_out` | 施法(非战斗) | 动作+法术位 | 施放法术用于探索 (侦测魔法/光亮术/羽落术) | 取决于法术 | 非战斗 |
| `hide` | 躲藏 (Hide) | 动作 | 隐匿检定对抗敌人被动察觉 | 自身 | 非战斗/战斗 |
| `disarm` | 解除陷阱 | 动作 | 巧手/奥秘检定解除已发现的陷阱 | 1 tile | 非战斗 |
| `examine` | 详细检查 | 动作 | 对特定物体进行深入调查检定 | 1 tile | 非战斗 |

#### 5.2.2 感知检定集成 (链接角色系统)

> **v1.2 重新设计**: 被动察觉从"主发现通道"降级为"辅助提示机制"，手动Search升级为"主要发现通道"。DC范围从5-25收窄至10-15(MVP)，确保所有角色通过主动搜索都能发现核心内容——高感知角色获得更快的提示，而非独占发现权。

```
被动感知 (Passive Perception) = 10 + WIS_mod + (if proficient in perception -> +PB)
  - 持续作用，无需玩家输入
  - 当角色接近隐藏物体时，计算 passive_perception vs Detection DC:
    ① 如果 被动感知 >= DC + 3: 显示"注意"提示（黄色"!"闪烁）→ 暗示附近有可疑物
    ② 如果 被动感知 >= DC (但 < DC + 3): 显示微弱的"注意"提示（半透明"!"）
    ③ 如果 被动感知 < DC: 无提示，需玩家主动Search
  - **被动感知不再自动揭示物体** — 仅提供辅助暗示

主动搜索 (Active Search) = d20 + WIS_mod + (if proficient in perception -> +PB)
  - ★ 主要发现通道
  - 玩家点击"搜索"按钮触发
  - 对整个当前房间已探索区域内的**所有隐藏物体**进行检定
  - 如果 d20 + modifier >= DC: 物体完全显现
  - 每回合可使用1次搜索动作
  - 搜索无发现时显示"你仔细搜索了周围，没有发现异常"（避免玩家无限重复搜索）

详细检查 (Examine) = d20 + INT_mod + (if proficient in investigation -> +PB)
  - 用于已发现的物体/区域进行深入调查
  - 揭示物体的额外信息 (陷阱机制/魔法属性/隐藏隔间)
  - 检定成功揭示 all 相关信息

环境线索 (Environmental Storytelling):
  - 被动线索: 当角色经过线索附近时，如果 passive_perception >= (DC - hint_bonus)
    → UI上显示微妙的视觉提示（统一"!"标记）
  - 主动线索: 搜索成功后全量揭示
  - 线索效果: 发现线索后相关检定的DC降低（如血迹→暗门DC-2）
```

**设计原理 (v1.2)**:
- **为什么降级被动察觉？** 原设计中Rogue被动17自动发现95%内容（10/11机械陷阱），Fighter被动10只能发现18%。这制造了"一个角色让探索失效，另一个让探索绝望"的二元体验——都不是好体验。
- **为什么手动Search为主？** Search消耗1回合（action cost），让探索成为有意义的资源分配决策——"我应该花一回合搜索这个房间，还是继续前进？"这服务于P1（选择属于玩家）和P2（战术深度）。
- **为什么DC 10-15？** DC ≤ 15确保Lv3 Rogue（主动+7，d20均值17.5）在大多数情况下能发现，但需要投入Search回合。DC ≥ 10确保Fighter（主动+0，d20均值10.5）通过Search也能发现大部分内容（DC 10 = 55%成功率），只是需要更多回合。

#### 5.2.3 检定流程图

```
玩家进入房间
  │
  ├─→ 立即计算: 每个角色的 Passive Perception
  │     ├─ 被动 >= DC + 3 → 显示"注意"提示 (黄色"!"闪烁)
  │     ├─ 被动 >= DC (但 < DC + 3) → 微弱"注意"提示 (半透明"!")
  │     └─ 被动 < DC → 无提示，需玩家主动Search
  │
  ├─→ 环境线索检测: Passive Perception vs (EnvironmentalCue.DC - hint_bonus)
  │     ├─ 通过 → 统一"!"提示 (如"墙上有焦痕")
  │     └─ 失败 → 无提示
  │
  └─→ 玩家交互
        ├─ 点击"搜索" → Active Perception Check (d20 + modifier, 覆盖当前房间)
        │     ├─ 成功 → 物体全量显现
        │     └─ 失败 → "没有发现异常"提示，可重复尝试（消耗1回合）
        │
        ├─ 点击已发现的物体 → Examine (d20 + Investigation vs DC)
        │     ├─ 成功 → 获取详细信息 (陷阱DC/物品属性/隐藏隔间)
        │     └─ 失败 → 基本信息已获取，不可重复Examine同一物体
        │
        ├─ 点击已发现的陷阱 → Disarm (d20 + ThievesTools/DEX vs DC)
        │     ├─ 成功 → 陷阱解除
        │     ├─ 失败(差5以内) → 无效果，可重试
        │     └─ 大失败(差10以上) → 陷阱触发!
        │
        └─ 点击交互物体 → Interact (基于标签系统处理, 见Section 6)
```

---

## 6. 交互标签系统实现

### 6.1 完整标签定义

#### 6.1.1 `Pushable` — 可推倒/推动

```json
{
  "tag": "Pushable",
  "object_schema": {
    "object_id": "pushable_boulder_01",
    "name": "松动的大石块",
    "pushable": {
      "push_distance_tiles": 3,
      "push_direction": "free",
      "required_strength_score": 13,
      "strength_check_dc": 12,
      "weight_class": "heavy",
      "cover_provided": "three_quarter",
      "block_passage": true,
      "damage_on_collision": {
        "enabled": true,
        "damage": "2d6",
        "damage_type": "bludgeoning",
        "save_stat": "dex",
        "save_dc": 13,
        "save_success_damage": "half"
      }
    }
  }
}
```

**推动规则**:
- 推动距离 = `push_distance_tiles` tiles
- 推动消耗: 1个动作 + 全部移动力 (本回合剩余速度归零)
- 如果 `required_strength_score` 不满足 → 需通过 `strength_check_dc` 检定
- 碰撞: 如果推动路径上有生物 → 生物需DEX豁免，失败受碰撞伤害并被推后1格
- 推动后的物体所在tile变为不可通行 (除非 `block_passage: false`)
- 物体提供掩体覆盖方向: 与推动方向相反的方向

**战斗应用**:
- 推石柱堵塞走廊 → 阻止敌人增援
- 推箱子形成掩体 → 远程角色获得AC加成
- 推倒书架砸向敌人 → 造成范围伤害 + 创造difficult terrain

#### 6.1.2 `Flammable` — 可点燃

```json
{
  "tag": "Flammable",
  "object_schema": {
    "object_id": "flammable_crate_01",
    "name": "木箱",
    "flammable": {
      "ignite_conditions": [
        { "source_type": "fire_spell", "auto_ignite": true },
        { "source_type": "torch", "auto_ignite": false, "interaction_check_dc": 0 },
        { "source_type": "fire_damage", "auto_ignite": true, "min_damage": 1 }
      ],
      "burn_duration_rounds": 4,
      "burn_damage_per_round": "1d6",
      "burn_damage_type": "fire",
      "burn_radius_tiles": 1,
      "spread_rules": {
        "can_spread": true,
        "spread_to_tags": ["Flammable", "Flammable_Liquid"],
        "spread_range_tiles": 2,
        "spread_chance_per_round": 0.25,
        "spread_requires_line_of_sight": true
      },
      "light_source": {
        "bright_radius_tiles": 4,
        "dim_radius_tiles": 8
      },
      "extinguish_conditions": [
        { "method": "water_spell", "immediate": true },
        { "method": "smother", "requires_action": true, "check_dc": 10 }
      ],
      "after_burn": {
        "object_destroyed": true,
        "leaves_terrain": "ash_pile",
        "loot_destroyed": true
      }
    }
  }
}
```

**点燃规则**:
- 火焰法术 (Fire Bolt / Burning Hands) 自动点燃
- 火把: 需要玩家交互动作 (使用火把点击物体)
- 燃烧持续 `burn_duration_rounds` 轮
- 每轮结束时:
  1. 对燃烧物体的tile及相邻tile上的所有生物造成伤害
  2. 检定spread，可能点燃附近的其他Flammable物体
  3. burn_duration_rounds -= 1，当为0时火焰熄灭

#### 6.1.3 `Climbable` — 可攀爬

```json
{
  "tag": "Climbable",
  "object_schema": {
    "object_id": "climbable_cliff_edge",
    "name": "悬崖边缘",
    "climbable": {
      "climb_height_tiles": 4,
      "climb_speed_multiplier": 0.5,
      "climb_check_dc": 12,
      "required_skill": "athletics",
      "climb_duration_actions": 2,
      "fail_consequence": {
        "type": "fall",
        "fall_distance_tiles": 2,
        "fall_damage_dice": "2d6",
        "save_stat": "dex",
        "save_dc": 10,
        "save_success": "catch_ledge",
        "save_fail": "full_fall"
      },
      "height_advantage": {
        "enabled": true,
        "bonus_to_attack": 0,
        "advantage_on_ranged": true,
        "line_of_sight_bonus_tiles": 4
      },
      "capacity": 4,
      "rope_assist": {
        "enabled": true,
        "reduces_dc_by": 5,
        "reduces_duration_by_actions": 1
      }
    }
  }
}
```

**攀爬规则**:
- 攀爬检定: `d20 + Athletics modifier vs climb_check_dc`
- 攀爬消耗: `climb_duration_actions` 个动作 (通常2个)
- 失败: 触发 `fail_consequence` (通常是跌落伤害)
- 高处优势: 处于高处的角色对下方目标有远程攻击优势
- 视野加成: 高处额外获得 `line_of_sight_bonus_tiles` tiles视野

#### 6.1.4 `Breakable` — 可破坏

```json
{
  "tag": "Breakable",
  "object_schema": {
    "object_id": "breakable_wooden_door",
    "name": "木门",
    "breakable": {
      "hp": 15,
      "ac": 13,
      "damage_threshold": 5,
      "vulnerabilities": ["bludgeoning"],
      "resistances": ["piercing"],
      "immunities": ["poison", "psychic"],
      "break_methods": [
        { "method": "attack", "requires_weapon": true },
        { "method": "strength_check", "dc": 15, "action_type": "action" },
        { "method": "spell", "spell_types": ["force", "thunder"] },
        { "method": "thieves_tools", "dc": 12, "action_type": "bonus_action" }
      ],
      "on_break": {
        "sound": "wood_crash",
        "vfx": "splinters",
        "terrain_change": "rubble_pile",
        "creates_opening": true,
        "loot_drop": null,
        "alert_enemies": true,
        "alert_radius_tiles": 15
      },
      "repairable": false
    }
  }
}
```

**破坏规则**:
- 伤害计算: 攻击命中后投伤害骰，如果 `damage >= damage_threshold` 则扣除HP
- 弱点/抗力: 伤害类型匹配则乘以相应倍率
- 减到0HP → 物体被破坏，触发 `on_break` 效果
- 力量检定破坏: `d20 + STR_mod vs strength_check DC`
- 巧手开锁/撬门: `d20 + ThievesTools_mod vs thieves_tools DC`
- 破门噪音: 15 tiles范围内的敌人进入警觉状态

#### 6.1.5 `Readable` — 可阅读

```json
{
  "tag": "Readable",
  "object_schema": {
    "object_id": "readable_ancient_tome",
    "name": "古老的典籍",
    "readable": {
      "content_type": "lore_fragment",
      "content_ids": ["lore_ancient_kingdom_01", "lore_elemental_origin"],
      "language": "elvish",
      "requires_language": true,
      "language_fallback": "部分可辨识 (通用语注释)",
      "skill_check_required": {
        "skill": "arcana",
        "dc": 14,
        "success_effect": "reveal_hidden_meaning",
        "hidden_content_ids": ["lore_secret_passage_location"]
      },
      "reading_duration_actions": 1,
      "read_by_llm": true,
      "consumable": false,
      "on_read_effects": [
        {
          "effect_type": "grant_knowledge_tag",
          "tag_id": "ancient_kingdom_known",
          "mechanical_bonus": "下次遇到ancient主题冒险时, History检定+1"
        },
        {
          "effect_type": "reveal_map_node",
          "node_id": "hidden_vault",
          "condition": "arcane_check_success"
        }
      ]
    }
  }
}
```

**阅读规则**:
- 阅读消耗: 1个动作
- 语言检查: 如果角色未掌握 `language` → 仅显示 `language_fallback` 文本
- 技能检定: 如果 `skill_check_required` 不为空 → 检定后决定是否揭示额外内容
- LLM叙事: 阅读文本内容由DM Agent实时生成（保证每次冒险的文本略有不同）
- 效果触发: `on_read_effects` 中的效果在阅读完成时触发

#### 6.1.6 `Flammable_Liquid` — 可燃液体

```json
{
  "tag": "Flammable_Liquid",
  "object_schema": {
    "object_id": "flammable_liquid_oil_barrel",
    "name": "油桶",
    "flammable_liquid": {
      "ignite_conditions": [
        { "source_type": "fire_spell", "auto_ignite": true },
        { "source_type": "fire_damage", "auto_ignite": true, "min_damage": 1 },
        { "source_type": "torch", "auto_ignite": false, "interaction_check_dc": 0 }
      ],
      "spread_on_break": {
        "radius_tiles": 3,
        "terrain_change": "oil_slick",
        "persists_until": "cleaned_or_burned"
      },
      "burn_radius_tiles": 3,
      "burn_damage_per_round": "2d6",
      "burn_damage_type": "fire",
      "burn_duration_rounds": 6,
      "explosion": {
        "enabled": true,
        "explosion_radius_tiles": 2,
        "explosion_damage": "4d6",
        "explosion_damage_type": "fire",
        "save_stat": "dex",
        "save_dc": 15,
        "save_success": "half_damage",
        "ignites_all_in_radius": true
      },
      "spread_rules": {
        "can_spread": true,
        "spread_to_tags": ["Flammable", "Flammable_Liquid"],
        "spread_range_tiles": 3,
        "spread_chance_per_round": 0.25,
        "spread_requires_line_of_sight": true,
        "max_spread_targets_per_round": 3,
        "same_tile_reignition_prevented": true
      },
      "light_source": {
        "bright_radius_tiles": 8,
        "dim_radius_tiles": 16
      },
      "after_burn": {
        "object_destroyed": true,
        "terrain_change": "scorched_ground",
        "leaves_slick_residue": false
      },
      "extinguish_conditions": [
        { "method": "water_spell", "effect": "reduces_burn_duration_by_2" },
        { "method": "smother", "effect": "reduces_burn_duration_by_1", "check_dc": 15 }
      ]
    }
  }
}
```

**区别对比 (Flammable vs Flammable_Liquid)**:

| 属性 | Flammable | Flammable_Liquid |
|------|-----------|------------------|
| 燃烧半径 | 1 tile | 3 tiles |
| 每轮伤害 | 1d6 | **2d6** |
| 持续时间 | 4 轮 | **6 轮** |
| 传播范围 | 2 tiles | 3 tiles |
| 传播概率 | 25% | **25%** |
| 传播需视线 | ✅ 是 | ✅ 是 |
| 每轮最大传播目标数 | 无限制 | **3** |
| 同tile防重复点燃 | - | ✅ 是 |
| 爆炸伤害 | 无 | **4d6** (半径2) |
| 光源强度 | 4/8 tiles | 8/16 tiles |

#### 6.1.7 `Electrical` — 导电

```json
{
  "tag": "Electrical",
  "object_schema": {
    "object_id": "electrical_metal_grating",
    "name": "金属栅栏",
    "electrical": {
      "conductivity": "high",
      "lightning_spell_reaction": {
        "conduct_damage": true,
        "conduct_range_tiles": 4,
        "damage_multiplier": 1.0,
        "chain_to_adjacent": true,
        "chain_max_targets": 3,
        "chain_damage_falloff_percent": 25
      },
      "shock_effect": {
        "enabled": true,
        "shock_damage": "1d6",
        "shock_damage_type": "lightning",
        "shock_save_stat": "con",
        "shock_save_dc": 13,
        "save_success": "no_damage",
        "save_fail_effect": "stunned_1_round"
      },
      "interaction_with_water": {
        "water_adjacent_tiles": 2,
        "water_effect": "conducts_to_all_water_tiles",
        "water_damage": "1d8",
        "water_save_dc": 12
      },
      "grounding": {
        "can_be_grounded": true,
        "grounding_effect": "disables_conductivity"
      }
    }
  }
}
```

**导电规则**:
- 闪电法术 (Lightning Bolt / Shocking Grasp / Call Lightning) 命中导电体时:
  1. 基础伤害施加于导电体上的所有生物
  2. 链式传导: 从导电体向 `conduct_range_tiles` 范围内的生物跳跃 (最多 chain_max_targets个)
  3. 每跳衰减: 下一跳伤害 = 上一跳伤害 × (1 - chain_damage_falloff_percent / 100)
- 水域交互: 如果导电体 2 tiles内有水 → 水变为带电 (伤害所有水中生物)
- 震慑效果: 生物在导电体上受到电击 → CON豁免失败则震慑1轮
- 接地: 使用金属武器攻击导电体可接地 (消耗1个交互动作)，解除导电效果

#### 6.1.8 `Hideable` — 可躲藏

```json
{
  "tag": "Hideable",
  "object_schema": {
    "object_id": "hideable_dense_bushes",
    "name": "茂密的灌木丛",
    "hideable": {
      "hide_type": "foliage",
      "stealth_bonus": 2,
      "cover_provided": "three_quarter",
      "visibility": "heavily_obscured",
      "capacity": 2,
      "size_restriction": ["small", "medium"],
      "ambush_mechanic": {
        "enabled": true,
        "attack_from_hiding": {
          "advantage_on_attack": true,
          "reveals_on_attack": true,
          "reveals_on_miss": false,
          "reveals_on_damage_taken": true
        },
        "surprise_round_possible": true
      },
      "reveal_conditions": [
        { "condition": "enemy_within_1_tile", "auto_reveal": true },
        { "condition": "area_spell_damage", "auto_reveal": true },
        { "condition": "enemy_active_perception_dc_15", "auto_reveal": true },
        { "condition": "light_source_within_2_tiles", "auto_reveal": false,
          "disadvantage_on_stealth": true }
      ],
      "enter_cost": {
        "action_type": "bonus_action",
        "movement_cost_multiplier": 2.0
      }
    }
  }
}
```

**躲藏规则**:
- 躲藏动作: 消耗1个附赠动作 + 移动至Hideable物体tile
- 隐匿检定: `d20 + Stealth_mod + stealth_bonus vs 敌人被动察觉`
- 成功 → 角色进入 Hidden 状态
- 伏击: 从躲藏状态发起攻击获得优势，命中后显现（miss不显现）
- 敌人检测: 每轮结束时，distance ≤ 2 tiles的敌人主动察觉检定 vs 隐匿值
- 自动暴露: 敌人走进同一tile / 受到范围伤害 / 光源照亮

### 6.2 交互标签JSON Schema (用于Adventure Blueprint)

```json
{
  "$id": "interaction_tag_placement",
  "type": "object",
  "required": ["tag_type", "object_id", "position", "template_id"],
  "properties": {
    "tag_type": {
      "type": "string",
      "enum": ["Pushable", "Flammable", "Climbable", "Breakable", "Readable",
               "Flammable_Liquid", "Electrical", "Hideable"]
    },
    "object_id": { "type": "string" },
    "position": {
      "type": "object",
      "required": ["x", "y"],
      "properties": {
        "x": { "type": "integer", "minimum": 0 },
        "y": { "type": "integer", "minimum": 0 }
      }
    },
    "template_id": { "type": "string", "description": "该标签对应的物体预制体ID" },
    "sprite_override": { "type": "string" },
    "loot_table_id": { "type": "string" },
    "initially_hidden": { "type": "boolean", "default": false },
    "hidden_detection_dc": { "type": "integer" },
    "condition": { "type": "string", "description": "该标签生效的前置条件表达式" }
  }
}
```

### 6.3 标签分配机制 (程序 + LLM 协调)

```
Algorithm: AssignInteractionTags(room)
─────────────────────────────────────
输入: room_data (node from blueprint + template)
输出: placed_tags[] (具体位置+标签配置)

Step 1 — 读取blueprint标签意图
  blueprint_tags = room_data.interaction_tags    // LLM指定的标签类型

Step 2 — 房间模板默认标签
  template_tags = room_template.environmental_features

Step 3 — 标签合并与去重
  merged_tag_types = set(blueprint_tags ∪ extract_types(template_tags))

Step 4 — 程序化位置分配
  for each tag_type in merged_tag_types:
    count = get_tag_count(tag_type, room_data.room_size)
    for i in range(count):
      position = find_valid_position(room_data, tag_type, placed_positions)
      object_id = generate_tag_object_id(room_data.node_id, tag_type, i)
      template_id = select_best_template(tag_type, room_data.theme_tags)
      placed_tags.append({tag_type, object_id, position, template_id})

Step 5 — LLM叙事关联 (可选, 非阻塞)
  如果 room_data.description 中提到特定物体:
    匹配description中的关键词 → 程序化位置 → 附加LLM生成的flavor描述

Step 6 — 返回完整标签列表
  return placed_tags
```

**每种节点类型的默认标签数量**:

| 节点类型 | Pushable | Flammable | Climbable | Breakable | Readable | Flammable_Liquid | Electrical | Hideable |
|----------|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| combat | 0-2 | 1-3 | 0-2 | 2-4 | 0-1 | 0-1 | 0-1 | 1-2 |
| dialogue | 0 | 0-1 | 0 | 0 | 1-3 | 0 | 0 | 0 |
| exploration | 1-2 | 1-2 | 1-2 | 1-3 | 2-4 | 0-1 | 0-1 | 1-2 |
| puzzle | 0-1 | 0-1 | 0-1 | 1-2 | 2-4 | 0 | 0-1 | 0 |
| merchant | 0 | 0-1 | 0 | 1-2 | 0-2 | 0 | 0 | 0 |
| rest | 0 | 1 | 0-1 | 0 | 0-1 | 0 | 0 | 0-1 |
| boss | 1-2 | 2-4 | 1-2 | 3-6 | 0-1 | 0-2 | 1-3 | 1-2 |
| branch | 0 | 0-1 | 0-1 | 0 | 1-3 | 0 | 0 | 0-1 |

### 6.4 交互标签视觉渲染

> **v1.2 简化**: 从8+视觉提示精简为**5层**（遵循认知负荷理论4±1原则），同时满足WCAG 1.4.1（颜色+形状双重编码）。

| 视觉提示 | 条件 | 效果 | 无障碍 |
|----------|------|------|--------|
| 物体高亮边框 (颜色+形状) | 角色距离 ≤ 2 tiles | 彩色闪烁边框 + 形状区分: Pushable=棕色▨ / Flammable=橙色▲ / Climbable=绿色⬆ / Breakable=红色✕ / Readable=蓝色◈ | 颜色+形状双重编码 |
| 注意提示 (统一"!") | 被动察觉或环境线索发现可疑物 | 淡黄色闪烁感叹号 (统一替代原"?"和"!") | 单字符，非颜色依赖 |
| 火焰动画 | Flammable物体被点燃后 | 循环火焰精灵动画 | 动态，非颜色依赖 |
| 裂纹纹理 | Breakable物体HP < 50% | 表面覆盖裂纹纹理 | 纹理，非颜色依赖 |
| 发光脉冲 | Readable物体含有未读内容 | 淡金色光晕脉冲 | 亮度，非颜色依赖 |

**交互图标**: 仅当鼠标悬停时显示（原"距离≤2 tiles常显"改为hover触发），降低视觉噪音。

**设计说明**: 原8种提示（白色边框/8色边框/悬浮图标/"? "/"! "/火焰/裂纹/发光）导致12+同时存在的视觉指示器。精简后：
- 合并 "?" 与 "!" → 统一为"注意提示"
- 悬浮图标仅在hover时显示（非永久常驻）
- 颜色边框仅在近距离(≤2 tiles)激活，配合形状编码确保色盲可辨识

### 6.5 标签处理优先级

> **v1.2 新增**: 从TEST 37中提取的标签碰撞处理优先级正式纳入设计规格。

当同一物体或同区域内多个标签同时触发时（如Boss房间火球命中多标签物体），按以下**优先级从高到低**顺序处理：

```
1. 燃烧 (Flammable / Flammable_Liquid) — 爆炸可被燃烧触发
2. 爆炸 (Flammable_Liquid.explosion) — 爆炸可破坏物体
3. 破坏 (Breakable) — 破坏可能释放导电体或暴露新物体
4. 导电 (Electrical) — 导电是二级反应，依赖物体完整性
5. 推动 (Pushable) — 推动是机械效果，最后处理
```

**处理规则**:
- 每个阶段独立执行，前一阶段的输出状态作为下一阶段的输入
- 如果物体在前序阶段被破坏（如Breakable在阶段3被摧毁），后续阶段（导电/推动）跳过该物体
- 每个阶段最多迭代5次（防止级联爆炸）
- 同一tile不会被同一标签重复触发

**验证**: TEST 37 应与此优先级保持一致。

---

## 7. 迷雾与视野系统

### 7.1 迷雾三层状态

| 状态 | 英文 | 视觉效果 | 触发条件 |
|------|------|----------|----------|
| 未探索 (Unexplored) | `fog_black` | 完全不透明的黑色 | 房间进入时所有tile的默认状态 |
| 已探索 (Explored) | `fog_dimmed` | 半透明灰色 (50% alpha) + 灰度滤镜 | 曾经在视野中但当前不在 |
| 可见 (Visible) | `fog_none` | 完全透明，全色渲染 | 当前在角色视线范围内 |

### 7.2 视野计算 (Line of Sight)

#### 7.2.1 视线算法: Shadow Casting (Recursive Octant)

```
Algorithm: CalculateVisibility(source_pos, vision_range, obstacles)
──────────────────────────────────────────────────────
Input:
  source_pos: Vector2i    // 视野源位置 (通常为角色位置)
  vision_range: int       // 视野范围(tiles)
  obstacles: Set<Vector2i> // 阻挡视线的tile集合 (墙壁/柱子等)

Output:
  visible_tiles: Set<Vector2i>  // 当前可见的tile集合

Algorithm (Recursive Shadow Casting):
  将360度视野分为8个扇区(octants)，对每个octant进行递归光线投射
  时间复杂度: O(N) where N = tiles within vision_range
  空间复杂度: O(N)

  **实现方案**: 采用GoRogue内置FOV (GoRogue.FOV类, v2.6.4已集成)
  - API: `FOV.Calculate(source_pos, vision_range, is_transparent_func)`
  - 性能: 单角色20格半径 ~0.15ms (递归阴影投射, 非自定义射线)
  - 多角色: 4角色同时计算 ~0.6ms
  - 触发频率: 角色移动后触发 (非每帧无条件)
```

#### 7.2.2 性能说明（已废弃）

> **v1.2废弃**: 原§7.2.2自定义双射线方案（每tile发射2条射线, 4角色=10,056射线/帧）已被GoRogue内置FOV替代——性能差距10-30×。GoRogue递归阴影投射在60fps下4角色FOV仅占0.6ms帧预算。

```

#### 7.2.2 简化实现

使用射线投射的简化实现：
- 从角色位置向vision_range半径内的每个tile中心发射射线
- 每个tile发射2条射线 (角到角精度)
- 使用射线投射检测碰撞
- 如果碰撞点在该tile内 → tile可见
- 如果射线未碰撞 → tile可见

### 7.3 视野范围

#### 7.3.1 角色视野 (基于种族)

| 种族 | 基础视野(tiles) | 黑暗视觉(tiles) | 黑暗视觉说明 |
|------|:---:|:---:|------|
| Human (人类) | 0 (需光源) | **0** | 无黑暗视觉，完全黑暗中目盲 |
| Elf (精灵) | 0 (需光源) | **12** (60ft) | 60尺黑暗视觉 |
| Dwarf (矮人) | 0 (需光源) | **12** (60ft) | 60尺黑暗视觉 |
| Halfling (半身人) | 0 (需光源) | **0** | 无黑暗视觉 |
| Half-Orc (半兽人) | 0 (需光源) | **12** (60ft) | 60尺黑暗视觉 |
| Gnome (侏儒) | 0 (需光源) | **12** (60ft) | 60尺黑暗视觉 |
| Tiefling (提夫林) | 0 (需光源) | **12** (60ft) | 60尺黑暗视觉 |
| Dragonborn (龙裔) | 0 (需光源) | **0** | 无黑暗视觉 |
| Half-Elf (半精灵) | 0 (需光源) | **12** (60ft) | 60尺黑暗视觉 |

#### 7.3.2 Darkvision机制

```
Darkvision:
  - 微光环境 (Dim Light): 视为明亮 (如同正常视野)
  - 黑暗环境 (Darkness): 视为微光 (可辨识但灰阶，察觉检定劣势)
  - 无Darkvision的角色在黑暗中: 目盲状态 (攻击劣势, 对其攻击优势)
  - Darkvision范围外: 同样目盲

游戏内实现:
  if has_darkvision:
    dim_to_bright_range = min(light_dim_range + darkvision_range, max_vision)
    dark_to_dim_range = min(darkvision_range, max_vision)
```

### 7.4 光源系统

| 光源类型 | 明亮半径(tiles) | 微光半径(tiles) | 持续时间 | 来源 |
|----------|:---:|:---:|------|------|
| 火炬 (Torch) | 8 (40ft) | 8 (40ft) | 1小时 | 物品 |
| 提灯 (Lantern) | 6 (30ft) | 6 (30ft) | 6小时 | 物品 |
| 蜡烛 (Candle) | 1 (5ft) | 1 (5ft) | 1小时 | 物品 |
| 光亮术 (Light cantrip) | 8 (40ft) | 8 (40ft) | 1小时 | 法术 |
| 昼明术 (Daylight) | 12 (60ft) | 12 (60ft) | 1小时 | 3环法术 |
| 妖火 (Faerie Fire) | 2 (10ft) | 0 | 1分钟 | 1环法术 |
| 焰舌剑 (Flame Tongue) | 8 (40ft) | 8 (40ft) | 永久(拔出时) | 魔法物品 |
| 不灭明焰 (Continual Flame) | 8 (40ft) | 8 (40ft) | 永久 | 2环法术 |
| 篝火 (Campfire) | 6 (30ft) | 6 (30ft) | 持续至熄灭 | 环境 |
| 燃烧Flammable物体 | 4 (20ft) | 8 (40ft) | 物体燃烧时间 | 交互标签 |

### 7.5 房间揭露规则

```
1. 玩家进入新房间 → 整个房间的所有tile初始为 Unexplored(黑色)
2. 房间入口附近的 3×3 区域自动变为 Visible (照亮入口)
3. 队伍中视野最远的角色的视野实时计算
4. 被光源照亮的区域 (参考角色视野+光源) → 变为 Visible
5. 曾经Visible但当前不在视线内的tile → 降级为 Explored (灰色, 可看到地形但看不到生物)
6. 房间被揭露后: 小地图上该房间永久显示其轮廓 (不再被迷雾覆盖)
7. 当前Visible区域: 可看到生物、可交互物体
8. Explored区域: 可看到地形(不变)，但生物和可移动物体会隐藏
```

---

## 8. 小地图系统

### 8.1 小地图渲染规格

| 参数 | 值 | 说明 |
|------|-----|------|
| 位置 | 右上角 | 不遮挡主要视野 |
| 尺寸 | 200 × 150 px | 固定像素大小 |
| 缩放 | 1 tile = 4 px | 32→4的缩小比 |
| 背景 | 半透明深色 (Color(0, 0, 0, 0.7)) | 不遮挡背后的游戏画面 |
| 边框 | 1px金色边框 (Color(0.8, 0.7, 0.3, 0.9)) | 与游戏UI风格一致 |
| 更新频率 | 每帧刷新 | 实时反映角色移动和迷雾变化 |

### 8.2 小地图显示内容

```
小地图渲染层序:
  1. 背景: 已探索房间 → 深灰色填充
  2. 未探索房间: 不渲染 (或显示为问号"?"标记)
  3. 房间墙壁/轮廓: 白色细线
  4. 走廊: 浅灰色细线 (仅已探索的走廊)
  5. 节点图标: 彩色圆点 (见8.3)
  6. 队伍位置: 闪烁的青色三角箭头▼
  7. 当前房间: 高亮边框 (淡金色闪烁)
  8. 迷雾覆盖: 在当前可见范围外覆盖半透明黑色
```

### 8.3 节点图标颜色编码

| 节点类型 | 图标 | 颜色 | 色值 |
|----------|:---:|------|------|
| combat | ⚔ | 红色 | #D32F2F |
| dialogue | 💬 | 蓝色 | #1976D2 |
| exploration | 🔍 | 紫色 | #7B1FA2 |
| puzzle | 🧩 | 青色 | #00838F |
| merchant | 🪙 | 金色 | #F9A825 |
| rest | 🔥 | 绿色 | #388E3C |
| boss | 💀 | 暗红/黑 | #B71C1C |
| branch | ✦ | 橙色 | #E65100 |
| 未探索节点 | ? | 灰色 | #9E9E9E |

### 8.4 交互

- **鼠标悬停节点图标**: 显示tooltip (节点名称 + 类型 + difficulty标签)
- **点击已探索房间**: 如果玩家在当前房间的出口附近 → 触发移动至该房间
- **点击未探索房间**: 显示"尚未探索"提示，不触发移动
- **全图切换 (Tab键)**: 打开全屏地图视图，显示整个已探索的节点图 (包括所有节点的连接关系)

---

## 9. 遭遇放置算法

### 9.1 CR预算计算

```
算法: CalculateCRBudget(party_levels[], encounter_difficulty)
─────────────────────────────────────────────────────────────
输入:
  party_levels: Array[int]       // 队伍成员等级列表, 例如 [3, 3, 3, 3]
  encounter_difficulty: string   // "easy"/"medium"/"hard"/"deadly"

输出:
  cr_budget: float               // CR预算总值

基于DND 5e遭遇构建规则 (DMG p.82):
  Step 1: 计算每个角色的XP阈值

    角色等级 → 难度XP阈值表 (单角色):
    | Lv | Easy  | Medium | Hard   | Deadly |
    |----|-------|--------|--------|--------|
    | 1  | 25    | 50     | 75     | 100    |
    | 2  | 50    | 100    | 150    | 200    |
    | 3  | 75    | 150    | 225    | 400    |
    | 4  | 125   | 250    | 375    | 500    |
    | 5  | 250   | 500    | 750    | 1100   |
    | 6  | 300   | 600    | 900    | 1400   |
    | 7  | 350   | 750    | 1100   | 1700   |
    | 8  | 450   | 900    | 1400   | 2100   |
    | 9  | 550   | 1100   | 1600   | 2400   |
    | 10 | 600   | 1200   | 1900   | 2800   |

  Step 2: 累加所有成员的XP阈值
    total_xp_budget = sum(threshold[level][difficulty] for level in party_levels)

  Step 3: 应用敌人数量的遭遇倍率
    敌人数量 → XP倍率:
    | 1      | ×1.0  |
    | 2      | ×1.5  |
    | 3-6    | ×2.0  |
    | 7-10   | ×2.5  |
    | 11-14  | ×3.0  |
    | 15+    | ×4.0  |

返回: cr_budget (基于adjusted XP转换为CR等价)
```

**简化版(本游戏使用)**:

| 队伍Lv×人数 | Easy CR | Medium CR | Hard CR | Deadly CR |
|------------|:---:|:---:|:---:|:---:|
| Lv1 × 4 | 0.5 | 1.0 | 1.5 | 2.0 |
| Lv2 × 4 | 1.0 | 2.0 | 3.0 | 4.0 |
| Lv3 × 4 | 1.5 | 3.0 | 4.5 | 6.0 |
| Lv4 × 4 | 2.0 | 4.0 | 6.0 | 7.5 |
| Lv5 × 4 | 3.0 | 5.0 | 7.0 | 8.5 |

> **设计说明 (v1.2)**: 简化表为近似值（±0.3 CR容忍度，参见§1B），精确CR预算以combat-system GDD为准。本表仅支持4人同等级队伍——非标准队伍（3人/5人/混合等级）必须回退至完整XP阈值表法（Step 1-3）。Deadly列从原2×Lv线性值修正为更接近DND 5e XP阈值的非线性值。Lv5 Deadly从CR 10.0修正为CR 8.5（对齐TEST 40预期CR 8.0）。

### 9.2 敌人组合生成

```
算法: GenerateEnemyComposition(cr_budget, theme_tags)
─────────────────────────────────────────────────────
输入:
  cr_budget: float
  theme_tags: Array[String]      // 例如 ["goblin", "forest"]

输出:
  enemies: Array[{template_id, count, cr}]

Step 1: 从敌人数据库筛选
   candidate_enemies = EnemyDB.query({
       "theme_match": theme_tags,  // 主题匹配
       "cr_max": cr_budget + 2,    // 最大CR不超过预算+2
       "cr_min": cr_budget * 0.05  // 最小体面
   })

Step 2: 贪心组合
   remaining_budget = cr_budget
   enemies = []
   max_iterations = 20
   while remaining_budget > 0 and max_iterations:
       candidates = [e for e in candidate_enemies if e.cr <= remaining_budget]
       if candidates.is_empty(): break
       chosen = weighted_random(candidates)  // CR越接近remaining_budget权重越高
       enemies.append({chosen.template_id, 1, chosen.cr})
       remaining_budget -= chosen.cr
       max_iterations--

Step 3: 验证与调整
   actual_cr_total = sum(enemy.cr for enemy in enemies)
   if abs(actual_cr_total - cr_budget) > 0.5:
       adjust(enemies, cr_budget)  // 向上或向下微调

Step 4: 返回
   return enemies
```

### 9.3 敌人放置阵型

```
阵型模板 (所有位置的坐标相对于房间中心):
──────────────────────────────────────────

阵型1: line (横排)
  位置: 房间北侧排成一行
  间距: 2 tiles
  [E1]  [E2]  [E3]  [E4]
  适用: 守卫类遭遇，敌人已列阵等待

阵型2: arc (弧形/半包围)
  位置: 北侧弧形分布
       [E1]  [E2]  [E3]
          [E4]  [E5]
  适用: 正面交锋遭遇

阵型3: ambush_flanks (侧翼伏击)
  位置: 东西两侧各一半敌人
  [E1]                  [E3]
        [E2]      [E4]
  适用: 伏击，玩家进入中央后触发

阵型4: scatter (分散)
  位置: 房间内随机分布 (至少相距3 tiles)
  适用: 野生动物/游荡怪物

阵型5: rear_ambush (后方伏击)
  位置: 玩家进入房间后，敌人出现在入口处
   [P1][P2]   [E1][E2]
   [P3][P4]   [E3]
  适用: 陷阱触发后的包围

阵型6: boss_front (Boss阵型)
  位置: Boss在房间正北王座，仆从分布两侧
         [Boss]
    [M1]       [M2]
         [M3]
  适用: Boss遭遇
```

### 9.4 触发条件

| 触发类型 | 检测方式 | 范例 |
|----------|----------|------|
| `proximity` | 任何角色进入trigger_radius_tiles范围 | 玩家走进伏击区 |
| `interaction` | 与特定物体交互 (开门/开宝箱) | 打开错误的宝箱触发陷阱+遭遇 |
| `skill_check_failure` | 检定失败触发 | 撬锁失败触发警报 |
| `timed` | 进入房间后N轮自动触发 | 仪式在第3轮完成 |
| `dialogue` | 对话选择特定选项 | 激怒NPC触发战斗 |
| `boss_defeated` | Boss死亡后触发 | 隐藏出口出现 |

### 9.5 可选遭遇 (可躲避)

```json
{
  "can_avoid": true,
  "avoid_methods": [
    {
      "method": "stealth",
      "skill": "stealth",
      "dc": 14,
      "group_check": true,
      "success": "bypass_without_combat",
      "xp_reward_percent": 50
    },
    {
      "method": "dialogue",
      "trigger_node": "dialogue_guard_captain",
      "success_dialogue_outcome": "guard_permits_passage",
      "xp_reward_percent": 75
    },
    {
      "method": "alternate_route",
      "route_node": "cliff_path",
      "requires": "athletics_dc_12_or_climb_speed",
      "xp_reward_percent": 100
    }
  ]
}
```

---

## 10. 陷阱与隐藏物体系统

### 10.1 陷阱类型定义

#### 10.1.1 机械陷阱

| 陷阱ID | 名称 | 触发方式 | 搜索DC | 解除DC | 效果 | 伤害 |
|--------|------|----------|:---:|:---:|------|------|
| trap_pressure_plate | 压力板 | step_on (80%) | 12 | 巧手 12 | 触发连动机关 | 取决于连动 |
| trap_tripwire | 绊索 | cross_line | 10 | 巧手 10 | 目标倒地 + 触发连动 | - |
| trap_dart_wall | 毒镖墙 | pressure_plate连动 | 15 | 巧手 13 | 发射毒镖 | 1d4穿刺 + DC 11 CON 中毒2d6 |
| trap_pit | 陷坑 | step_on (100%) | 10 | 无法解除 (可绕过) | 掉落 | 1d6/每10ft坠落 |
| trap_collapsing_ceiling | 塌顶 | tripwire连动 | 13 | 巧手 15 | 天花板塌落 | 3d10钝击, DEX DC 14 减半 |
| trap_swinging_blade | 摆刀 | proximity_3tiles | 12 | 巧手 14 | 摆刀横扫 | 2d10挥砍, DEX DC 13 减半 |
| trap_bear_trap | 捕兽夹 | step_on (60%) | 8 | 巧手 10 | 夹住脚 (束缚) | 1d6穿刺, 速度=0直到解除 |
| trap_net_snare | 网陷阱 | tripwire连动 | 11 | 巧手 12 | 网从上方落下 | 束缚 (STR DC 12 挣脱) |

#### 10.1.2 魔法陷阱

| 陷阱ID | 名称 | 触发方式 | 搜索DC | 解除DC | 效果 | 伤害 |
|--------|------|----------|:---:|:---:|------|------|
| trap_glyph_warding | 守卫刻纹 | proximity_2tiles | 奥秘 14 | 奥秘 15 | 刻纹触发法术 | 取决于存储法术 |
| trap_explosive_rune | 爆炸符文 | step_on / touch | 奥秘 13 | 奥秘 14 | 火焰爆炸 | 3d8火焰, DEX DC 14 减半 |
| trap_arcane_eye | 奥术之眼 | proximity_5tiles | 察觉 16 | 驱散魔法(3环) | 警报敌人 | - |
| trap_sleep_rune | 睡眠符文 | proximity_2tiles | 奥秘 12 | 奥秘 13 | 睡眠波 | 5d8 HP受影响, WIS DC 13 |

### 10.2 陷阱数据模型

```json
{
  "trap_id": "trap_explosive_rune_01",
  "name": "爆炸符文",
  "type": "magical",
  "subtype": "rune",
  "trigger": {
    "type": "step_on",
    "trigger_chance": 1.0,
    "trigger_tile": [7, 5],
    "trigger_area_tiles": [[7, 5]]
  },
  "detection": {
    "passive_perception_dc": 16,
    "active_investigation_dc": 14,
    "active_arcana_dc": 13,
    "passive_detection_possible": false
  },
  "disarm": {
    "skill": "arcana",
    "dc": 14,
    "requires_tools": false,
    "thieves_tools_alternative": false,
    "dispel_magic_level": 3,
    "failure_consequence": {
      "difference_0_to_4": "no_effect_can_retry",
      "difference_5_to_9": "trap_activates",
      "difference_10_plus": "trap_activates_with_disadvantage_on_save"
    }
  },
  "effect": {
    "damage": "3d8",
    "damage_type": "fire",
    "area": {
      "type": "circle",
      "radius_tiles": 2,
      "center_tile": [7, 5]
    },
    "save": {
      "ability": "dex",
      "dc": 14,
      "success_effect": "half_damage"
    },
    "secondary_effects": [
      { "type": "ignite", "target_tags": ["Flammable", "Flammable_Liquid"], "radius_tiles": 3 }
    ],
    "vfx": "fire_explosion_medium",
    "sfx": "explosion_fire"
  },
  "cooldown": {
    "type": "none"
  },
  "bypass_methods": [
    { "method": "jump_over", "requires": "str_score_13_or_athletics_dc_12" },
    { "method": "step_around", "requires": "acrobatics_dc_10" },
    { "method": "trigger_from_distance", "requires": "ranged_attack_or_mage_hand" }
  ]
}
```

### 10.3 陷阱触发序列

```
玩家step_on触发tile / 进入proximity范围 / cross_line
  │
  ▼
Step 1 — 检测 (Detection)
  ├─ 被动察觉: if character.passive_perception >= trap.detection.passive_perception_dc
  │     → UI闪烁提示"脚下有异常" (0.3s视觉提示)
  │     → 陷阱仍会触发但玩家获得 +2 DEX豁免 优势 (反应更快)
  └─ 未通过: 无预警, 直接进入Step 2

Step 2 — 触发 (Trigger)
  ├─ 播放陷阱激活动画 (地板下陷/符文发光/箭矢弹射)
  └─ 进入Step 3

Step 3 — 效果 (Effect)
  ├─ 伤害骰子: roll damage
  ├─ 豁免检定: each affected creature → d20 + save_modifier vs save_dc
  │     ├─ 成功: 无伤害 / 半伤害 (取决于effect.save.success_effect)
  │     └─ 失败: 全伤害 + 可能的secondary_effects
  ├─ 应用伤害到角色HP
  ├─ 应用任何secondary_effects (点燃/束缚/中毒等)
  └─ 触发LLM DM Agent生成叙述文本

Step 4 — 后处理
  ├─ 如果陷阱是一次性的 → 标记为 disarmed
  ├─ 如果陷阱有cooldown → 启动冷却计时器
  └─ 更新Explored区域中陷阱的视觉状态
```

### 10.4 隐藏物体

#### 10.4.1 隐藏物体类型

| 类型 | 检测方式 | 范例 |
|------|----------|------|
| 暗门 (Secret Door) | 被动察觉 DC 15 / 主动调查 DC 12 | 书柜后的隐藏通道 |
| 隐藏通道 | 被动察觉 DC 18 / 灰烬/痕迹线索降低DC | 墙后的秘密走廊 |
| 隐藏隔间 | 调查 DC 14 / 环境线索暗示存在 | 桌子暗格中的法术卷轴 |
| 隐藏物品 | 被动察觉 DC 13 / 主动搜索 DC 10 | 床底下的金币袋 |
| 隐藏陷阱 | 被动察觉 DC 16 (仅盗贼)/ 调查 DC 14 | 伪装成地砖的陷坑 |
| 幻象墙壁 | 调查 DC 15 / 触碰自动发现 | 看起来是墙壁但可穿越 |

#### 10.4.2 隐藏路径发现规则

```
发现优先级 (从易到难):
  Level 1: 环境线索 → 发现线索 → DC降低
    例: "地板上有一道拖拽的血迹通向东北墙" → 暗门DC -2

  Level 2: 被动察觉 → 检测到异常
    if passive_perception >= hidden_dc:
      → 显示半透明闪烁 (物体未完全揭示, 但提示存在)

  Level 3: 主动搜索 → 全量发现
    玩家点击Search → d20 + perception vs DC → 暗门/物体完全显现

  Level 4: 直接交互 → 强制显现
    尝试与可疑墙壁交互 → 发现暗门

  Level 5: 魔法/特殊能力
    Detect Magic → 侦测魔法暗门
    Find Traps → 感知陷阱位置
    True Seeing → 无视所有幻象
```

---

## 11. 房间与走廊生成

### 11.1 房间生成流程

```
Algorithm: GenerateRoom(node_data, node_graph)
─────────────────────────────────────────────
Step 1 — 确定房间尺寸
  size = node_data.room_size 或 从Section 2.3.2表获取默认值
  宽度 tiles = size[0], 高度 tiles = size[1]

Step 2 — 选择房间模板
  template = select_template(node_data.type, size, node_data.theme_tags)
  匹配规则:
    1. node_type 精确匹配
    2. size 差值不超过 4 tiles (宽度/高度分别检查)
    3. tileset_tags 与 node_data.theme_tags 有交集
    4. 如果无精确匹配 → 使用最接近的模板 + 程序化填充

Step 3 — 加载TileMap模板
  tilemap = load(template.tile_grid) 或从template结构数据程序化生成

Step 4 — 调整出入口
  for each connection in node_data.connections:
    确保有对应方向的出口 (自动添加 door_tile 如果模板不包含)
    door_type:
      "open" → 无门，仅tile边界
      "locked" → 上锁的门 (Breakable AC 15 HP 20, 或巧手DC 15, 或对应钥匙)
      "locked_magical" → 魔法封印 (需解谜或驱散魔法)
      "hidden" → 暗门 (Section 10.4规则)
      "sealed" → 石封 (不可开启直到条件满足)
      "magical_barrier" → 能量屏障 (可见但不可通行, 需解谜关闭)

Step 5 — 放置可交互物体
  基于 node_data.interaction_tags + 模板default features → 按Section 6.3算法放置

Step 6 — 放置装饰
  基于 theme_tags 选择装饰元素集 (decoration_set):
    dungeon_ruined: 蜘蛛网/碎石/破损火把/骷髅
    forest_temperate: 草地斑块/小树/蘑菇/藤蔓/落叶
    town_medieval: 桶/箱子/招牌/街灯/旗帜
    cave_natural: 钟乳石/石笋/水坑/发光蘑菇
    temple_holy: 蜡烛/香炉/跪垫/圣徽

Step 7 — 设置光照
  ambient_light = theme_to_ambient(node_data.theme_tags):
    dungeon: 0.15 (非常暗, 依赖光源和Darkvision)
    forest: 0.4 (微光环境, 部分可见)
    town: 0.7 (明亮环境)
    cave: 0.1 (完全黑暗)
    temple: 0.5 (有烛光)

Step 8 — 返回完整Room场景数据
```

### 11.2 主题环境特征表

| 主题 | 环境物体 | 光源环境 | 特有交互 |
|------|----------|:---:|------|
| dungeon | 蜘蛛网(减速)、水坑(导电)、碎石(difficult terrain)、破损火把 | 暗(0.15) | 暗门频繁(15%概率/房间) |
| forest | 树木(Climbable)、灌木(Hideable)、落叶(足迹线索)、溪流(water) | 微光(0.4) | 自然陷阱(捕兽夹/落石) |
| town | 家具(Readable)、货摊(Breakable)、招牌(Readable)、街灯(Light) | 明(0.7) | 盗窃/扒手事件 |
| cave | 钟乳石(danger)、石笋(HalfCover)、发光蘑菇(Light)、水塘(water) | 极暗(0.1) | 坍塌风险、回声机制 |
| temple | 圣徽(Readable)、烛台(Flammable)、跪垫、香炉(magical mist) | 中(0.5) | 神圣/亵渎区域效果 |
| ruins | 倒塌柱子(Breakable)、藤蔓(Climbable)、石板(Readable)、杂草(difficult) | 微光(0.35) | 考古发现、隐藏历史 |

### 11.3 走廊生成规范

```
走廊规格:
  - 宽度: 3 tiles (15ft) — 确保3人可并排
  - 最大直段长度: 12 tiles (60ft) — 超过则插入转角或视觉分隔
  - 转角: 90度转角, 至少2 tiles宽度
  - 死胡同: 仅允许通向隐藏门或暗门

走廊Tileset:
  - 使用连接房间的区域tileset
  - 墙面: 填充墙壁tile (与房间墙壁一致)
  - 地面: 使用connector地面的过渡tile (连接两个不同主题时)

走廊生成算法:
  Step 1: 根据两个房间的位置确定走廊方向 (H/V/L型)
  Step 2: 计算走廊长度 (曼哈顿距离 - 房间半径)
  Step 3: 生成直段 → 如果长度>12自动插入转角
  Step 4: 在走廊中点(或三分之一+三分之二位置)可选放置:
    - 火把/光源 (概率60%)
    - 环境装饰 (蜘蛛网/裂缝)
    - 小型遭遇/巡逻敌人 (可选遭遇, 概率25-30%) — v1.2从15%调高, 确保走廊持续有"可能有事发生"的张力
  Step 5: 画连接线到每个出口
```

### 11.4 门放置与锁机制

```json
{
  "door_id": "door_ancient_gate",
  "name": "古老的符文石门",
  "position": [10, 0],
  "orientation": "horizontal",
  "door_type": "locked_magical",
  "visual": {
    "sprite_open": "assets/sprites/doors/gate_ancient_open.png",
    "sprite_closed": "assets/sprites/doors/gate_ancient_closed.png",
    "anim_duration": 0.4
  },
  "lock_config": {
    "lock_dc": 16,
    "break_dc": 22,
    "breakable_stats": {
      "hp": 40,
      "ac": 18,
      "damage_threshold": 10,
      "vulnerabilities": ["force"],
      "resistances": ["piercing", "slashing"],
      "immunities": ["poison", "psychic", "fire"]
    },
    "key_item_id": "ancient_gate_key",
    "key_is_consumable": false,
    "spell_bypass": ["knock"],
    "dispel_magic_dc": 14
  },
  "initially_locked": true,
  "unlocked_by": "puzzle_elemental_pedestals",
  "auto_close_after_rounds": 0
}
```

**锁难度分级**:

| 难度 | 开锁DC | 破坏DC | 典型场景 |
|------|:---:|:---:|------|
| 简单木门 | 10 | 13 | 普通房屋/酒馆 |
| 标准锁 | 12 | 15 | 一般地牢/商店后门 |
| 精良锁 | 15 | 18 | 宝箱/重要房间 |
| 复杂锁 | 18 | 20 | 金库/贵族宅邸 |
| 专家锁 | 20 | 22 | 传奇宝箱/远古遗迹 |
| 魔法封印 | 不可(非魔法) | 25 | 远古传送门 |

---

## 12. 测试规格

### 12.1 单元测试

#### Test Suite: 图连通性验证

```
TEST 1: 完全连通图
  Given: 线性3节点图 (start→combat→boss)
  Expected: 所有节点可达, 无错误

TEST 2: 不可达节点
  Given: 4节点图, 其中1节点无入边且非起始节点
  Expected: GraphValidationError("存在不可达节点")

TEST 3: 分支平衡 (差异<30%)
  Given: branch节点有2子节点, fight1=CR 2, fight2=CR 2.5
  Expected: 无警告 (差异20%<30%)

TEST 4: 分支不平衡 (差异>30%)
  Given: branch有2子节点, fight1=CR 1, fight2=CR 5
  Expected: 警告"分支不平衡"

TEST 5: 死路非终点
  Given: combat节点无出边且非boss/rest
  Expected: 警告"死路节点非终点"

TEST 6: Boss在最后3节点
  Given: boss节点是图中的第1个节点
  Expected: 警告"boss不在最后3节点"
```

#### Test Suite: 感知检定计算

```
TEST 7: 被动察觉 (熟练, Lv3)
  Input: WIS 12(+1), perception熟练, PB=2
  Expected: 10 + 1 + 2 = 13

TEST 8: 被动察觉 (未熟练)
  Input: WIS 12(+1), perception未熟练, PB=2
  Expected: 10 + 1 = 11

TEST 9: 被动察觉 (专精, Rogue Lv5)
  Input: WIS 14(+2), perception专精, PB=3
  Expected: 10 + 2 + 6 = 18

TEST 10: 主动搜索 (成功)
  Input: d20=15, WIS+1, perception熟练PB+2, DC=15
  Expected: 15+1+2=18 >= 15 → 成功

TEST 11: 主动搜索 (失败)
  Input: d20=8, WIS+1, perception熟练PB+2, DC=15
  Expected: 8+1+2=11 < 15 → 失败
```

#### Test Suite: 迷雾边缘情况

```
TEST 12: 视线被墙壁阻挡
  Given: 角色和tile之间有墙壁
  Expected: tile不可见

TEST 13: 角落可见性
  Given: 沿对角线方向, 两侧为空
  Expected: 角落tile可见

TEST 14: Darkvision范围边界
  Given: 有Darkvision 12 tiles, 空间完全黑暗, tile距离13 tiles
  Expected: 超过Darkvision范围, tile不可见

TEST 15: 光源叠加
  Given: 2个Torch (每8 tile明亮, 8 tile微光) 距离6 tiles
  Expected: 重叠区域明亮半径内合并, 微光区扩展

TEST 16: 已探索区域的半透明状态
  Given: tile曾经Visible但角色已离开
  Expected: Fog layer alpha=0.5 (灰色), 地形可见但生物不可见
```

#### Test Suite: 交互标签解析

```
TEST 17: 标签合并
  Input: blueprint_tags=["Flammable", "Breakable"], template_tags有[Flammable, Climbable]
  Expected: 合并后={Flammable, Breakable, Climbable}

TEST 18: 标签验证 (尺寸冲突)
  Input: room_size=[8,8], 请求12个标签物体
  Expected: 警告"标签物体过多, 空间不足"

TEST 19: 燃烧传播
  Given: Flammable object A点燃, Flammable object B距离2 tiles
  Expected: 每轮25%概率传播到B

TEST 20: 燃烧传播 (Flammable_Liquid)
  Given: Flammable_Liquid点燃, Flammable object距离3 tiles
  Expected: 每轮40%概率传播 (比普通传播高)

TEST 21: 标签堆叠 (同物体多标签)
  Given: 物体上同时有Flammable + Breakable标签
  Expected: 两个标签均独立生效, 破坏时检查on_break, 燃烧时检查burn

TEST 22: 推倒碰撞
  Given: 推动物块沿路径有1个敌人
  Expected: 敌人DEX豁免DC 13, 失败受2d6钝击并推后1格
```

### 12.2 集成测试

#### Test Suite: 完整房间转换流程

```
TEST 23: 正常房间切换 (fade)
  Setup: start_room→combat_room (open door connection)
  Action: 移动角色到出口tile
  Expected: fade_to_black 0.5s → 加载combat_room → 渐亮 → node_entered信号

TEST 24: 对话触发场景转换
  Setup: dialogue_elf_guardian→ancient_chamber (locked_magical door)
  Action: 说服NPC → effect="open_gate"
  Expected: 门解锁动画 → slide_up转换 → ancient_chamber加载

TEST 25: Boss战后解锁
  Setup: boss_lich → exit_passage (sealed door)
  Action: boss_defeated事件
  Expected: 门解锁 → 出口标记激活
```

#### Test Suite: 战斗触发流程

```
TEST 26: proximity触发
  Setup: 包含4个goblins的combat房间, trigger_radius=6 tiles
  Action: 角色移动至距离敌人6 tiles范围内
  Expected: 敌人出现 → 先攻检定 → 切换到战斗模式

TEST 27: interaction触发
  Setup: 触发object的交互触发遭遇
  Action: 角色打开"伪装的宝箱"
  Expected: 陷阱触发 → 遭遇开始

TEST 28: 陷阱触发序列
  Setup: 爆炸符文陷阱 (DC 14 DEX save, 3d8 fire)
  Action: 角色踩上符文tile
  Expected:
    Step 1: 被动察觉检查 (如果通过→闪烁提示)
    Step 2: 陷阱激活动画
    Step 3: 豁免检定 (d20 + DEX_mod vs DC 14)
    Step 4: 伤害应用
    Step 5: LLM DM Agent生成叙述
```

#### Test Suite: 完整探索流程

```
TEST 29: 搜索隐藏暗门
  Setup: 暗门 (DC 15 perception), 角色 passive=13
  Action: 点击"搜索"
  Expected: d20 + perception vs DC 15 → 成功揭示暗门

TEST 30: 环境线索降低DC
  Setup: 暗门DC 15, 发现血迹线索 (-2 DC)
  Expected: 有效DC降至13, 更容易发现

TEST 31: 解锁然后通过门
  Setup: locked door (DC 15)
  Action: 盗贼使用thieves_tools → d20+Proficiency+DEX vs DC 15
  Expected: 成功 → 门打开 → 可通行
```

### 12.3 边界情况

```
TEST 32: 单个最小房间
  Input: 1-node combat (short adventure), room=[16,12]
  Expected: 正常生成，无crash

TEST 33: 只有一个出口的房间
  Given: rest房间只有entry没有exit
  Expected: 允许 (rest是合法死路)

TEST 34: DC极端值 (DC 30)
  Given: 隐藏物体DC=30, Lv5角色WIS 20(+5), Perception Expertise(PB=3→+6), 主动察觉加值=+11
  Expected: 被动察觉21 < DC 30 → 无被动提示, 主动Search需d20≥19才能发现 (d20+11≥30)
  (v1.2修正: 原计算"3+5+3=11"遗漏了WIS_mod和Expertise的完整计算，被动察觉实际=10+5+6=21)

TEST 35: DC 0 (自动成功)
  Given: 被动察觉 11 vs 隐藏物体 DC 0
  Expected: 被动察觉11 ≥ DC+3=3 → 显示"注意"提示（黄色"!"闪烁）
  (v1.2修正: 被动不再自动揭示物体，改为显示提示)

TEST 36: 空探索房间
  Given: exploration节点无hidden_checks, loot_spots, environmental_storytelling
  Expected: 正常渲染, 无交互但可通行

TEST 37: 所有标签同时触发
  Given: Boss房间物体被火球击中 (触发Flammable+Flammable_Liquid+Breakable+Electrical+Pushable)
  Expected: 按§6.5定义的优先级顺序处理: 燃烧→爆炸→破坏→导电→推动, 每阶段独立执行且互不覆盖
  (v1.2: 处理顺序已从TEST中提取并正式纳入§6.5设计规格)

TEST 38: 极限CR (Deadly+1)
  Given: Lv3×4队伍遇到CR 7 boss
  Expected: CRValidator返回contains_warning=true, is_fatal=false, recommended_action="allow_with_warning"
  (v1.2: 断言从模糊描述"根据规则允许"改为可验证的具体返回值)
```

### 12.4 平衡验证

```
TEST 39: CR预算计算 (Easy, Lv3×4)
  Input: party=[3,3,3,3], difficulty="easy"
  Expected: 4 × 75 XP = 300 XP → CR ≈ 1.5 budget

TEST 40: CR预算计算 (Deadly, Lv5×4)
  Input: party=[5,5,5,5], difficulty="deadly"
  Expected: 4 × 1100 XP = 4400 XP, adjusted for 4 enemies (×2) = 8800
  → CR ≈ 7-8 budget

TEST 41: 房间尺寸分布
  Given: 中冒险20个节点
  Verify: boss房间尺寸 >= combat房间尺寸
  Verify: rest/merchant房间是最小的

TEST 42: 遭遇频率（注：此测试属于冒险生成系统）
  (v1.2: 此测试本质上是冒险生成系统的设计约束，非地图系统代码行为测试。建议迁移至06-adventure-generation.md。)
  Given: 中冒险20个节点
  Verify: combat节点数在 4-10 范围内 (20%-50%)
  Verify: boss节点出现在最后3个节点位置
```

---

*文档版本: v1.2*
*创建日期: 2026-05-04*
*修订日期: 2026-05-10 (v1.2 — `/design-review` 审查后修复)*
*作者: 酒馆与命运 开发团队*
*状态: 已修订 — 审查判定 MAJOR REVISION NEEDED → 7硬阻断+8结构性修复已应用*
*审查记录: 参见 `design/gdd/reviews/05-map-exploration-review-log.md`*

*下一步: 基于此TDD实现原型，优先完成MVP范围的7个房间模板和基础探索流程*
