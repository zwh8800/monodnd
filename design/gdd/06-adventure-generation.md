# 冒险生成系统 — 技术设计文档

> **Subsystem**: Adventure Generation System
> **Game**: 《酒馆与命运》(Tavern & Destiny)
> **Rules Reference**: DND 5e SRD
> **Language Policy**: 游戏文本统一采用简体中文，技术标识符使用英文snake_case
> **Version**: 1.0
> **Status**: 初始设计
> **对应GDD**: GDD-v1.md §4.2, §4.3, §6
> **依赖文档**: llm-integration.md, map-exploration.md, character-system.md

---

## 1. 概述

### 1.1 系统目的

冒险生成系统是《酒馆与命运》**最核心、最复杂的技术子系统**。它是一个三层管线（Three-Layer Pipeline），负责将LLM生成的叙事蓝图（Blueprint）转化为可交互、可战斗、可结算的完整冒险体验。

本系统是连接"创意层"（LLM Agent）与"执行层"（游戏系统）的桥梁——LLM负责"写什么故事"，程序负责"怎么把这个故事变成可以玩的游戏"。

### 1.2 核心设计原则

```
┌─────────────────────────────────────────────────────────────┐
│                    LLM = 皮肤层 (Skin)                       │
│  · 叙事文本、NPC对话、场景描写、物品风味描述                    │
│  · 决定"What the NPC says"，不决定"What the NPC does"         │
│  · 所有LLM输出必须通过JSON Schema验证                         │
├─────────────────────────────────────────────────────────────┤
│                    程序 = 骨骼层 (Bone)                       │
│  · 数值计算、规则判定、分支逻辑、遭遇平衡                       │
│  · 决定伤害数值、检定DC、战利品掉落、世界状态变更                │
│  · 所有程序逻辑可完全单元测试，不依赖LLM                       │
└─────────────────────────────────────────────────────────────┘
```

### 1.3 三层管线架构

```
┌────────────── 第一层：冒险蓝图生成（冒险开始前）──────────────┐
│                                                                │
│  输入：任务等级 + 酒馆角色状态 + 世界状态 + 主题池             │
│                                                                │
│  编剧Agent(Screenwriter Agent)生成：                            │
│  ┌──────────────────────────────────────────┐                  │
│  │ adventure_blueprint.json                  │                  │
│  │  · 标题、主题、基调                        │                  │
│  │  · 核心冲突与三幕结构                      │                  │
│  │  · 节点图(nodes[]) — 场景序列+连接关系     │                  │
│  │  · 关键NPC定义                             │                  │
│  │  · 难度配置(CR范围/遭遇数/战利品等级)       │                  │
│  │  · 世界状态挂钩                            │                  │
│  └──────────────────────────────────────────┘                  │
│                                                                │
│  平衡Agent(Balancer Agent)自动验证：                            │
│  · CR合理性、战利品分配、叙事连贯性                              │
│  · 程序侧硬性规则兜底验证                                       │
│                                                                │
│  失败策略：最多3次重试 → 降级到离线模板                          │
└────────────────────────────────────────────────────────────────┘
                              ↓
┌────────────── 第二层：程序化实例化（冒险开始前）──────────────┐
│                                                                │
│  AdventureInstantiator 消费蓝图 → 生成可玩内容：               │
│  ┌──────────────────────────────────────────┐                  │
│  │ 1. 解析+验证蓝图JSON                       │                  │
│  │ 2. 生成节点图（Map System兼容格式）          │                  │
│  │ 3. 为每个节点分配房间模板                    │                  │
│  │ 4. 为战斗节点生成遭遇数据（CR预算→敌人组合）  │                  │
│  │ 5. 为对话节点生成NPC对话配置                  │                  │
│  │ 6. 为探索节点生成隐藏物体+陷阱                │                  │
│  │ 7. 为商人节点生成商店库存                     │                  │
│  │ 8. 为Boss节点生成多阶段Boss数据               │                  │
│  │ 9. 连接房间走廊、放置交互标签                  │                  │
│  │ 10. 初始化冒险状态机                          │                  │
│  └──────────────────────────────────────────┘                  │
│                                                                │
│  关键：此层完全不依赖LLM，可独立单元测试                        │
└────────────────────────────────────────────────────────────────┘
                              ↓
┌────────────── 第三层：DM Agent实时叙事（冒险进行中）──────────┐
│                                                                │
│  玩家每个动作后按需调用DM Agent：                               │
│  ┌──────────────────────────────────────────┐                  │
│  │ · 进入新房间 → scene_atmosphere            │                  │
│  │ · NPC对话 → npc_dialogue + options         │                  │
│  │ · 检定结果 → skill_check_result            │                  │
│  │ · 战斗动作 → combat_narration              │                  │
│  │ · 选择呈现 → choice_presentation           │                  │
│  └──────────────────────────────────────────┘                  │
│                                                                │
│  所有LLM调用是"皮肤层"——不决定结果，只决定表述                  │
│  所有结果逻辑（伤害、检定、分支走向）由程序控制                   │
│  响应时间要求：< 2秒；失败时降级到静态模板                       │
└────────────────────────────────────────────────────────────────┘
```

### 1.4 与其他系统的关系

```
                    ┌──────────────────────┐
                    │   LLM Gateway        │
                    │  (Agent调度/验证/缓存) │
                    └──────────┬───────────┘
                               │ 编剧Agent/平衡Agent/DM Agent
                               ▼
┌──────────────┐    ┌──────────────────────┐    ┌──────────────┐
│ Character    │───▶│  Adventure Generation │◀───│ Tavern       │
│ System       │    │  (本系统)              │    │ System       │
│ (角色等级/   │    │  ─────────────────── │    │ (任务板/     │
│  属性/状态)  │    │  · 蓝图解析器          │    │  声望/酒馆   │
└──────────────┘    │  · 实例化引擎          │    │  等级)       │
                    │  · 遭遇生成器          │    └──────────────┘
                    │  · 战利品生成器         │
                    │  · 状态管理器          │
                    │  · 结算系统            │
                    └──────┬───────┬────────┘
                           │       │
              ┌────────────┘       └────────────┐
              ▼                                 ▼
    ┌──────────────┐                   ┌──────────────┐
    │ Map System   │                   │ Combat System│
    │ (地图渲染/   │                   │ (战斗引擎/   │
    │  探索/交互)  │                   │  回合制/法术) │
    └──────────────┘                   └──────────────┘
```

| 关系系统 | 交互方向 | 数据内容 |
|----------|----------|----------|
| LLM Gateway | 本系统→Gateway | 调用编剧Agent/平衡Agent/DM Agent |
| Character System | 读取 | 角色等级、属性、职业、装备状态 |
| Map System | 本系统→Map | 节点图、房间模板、交互标签、遭遇配置 |
| Combat System | 本系统→Combat | 遭遇数据、敌人stat block、Boss阶段 |
| Tavern System | 读取/写入 | 任务配置、声望等级、酒馆解锁状态 |
| Items & Equipment | 读取 | 物品模板、战利品池、商人库存 |

### 1.5 MVP范围

| 功能 | MVP (Phase 1) | Phase 2+ |
|------|:---:|:---:|
| 短冒险蓝图解析+实例化 | YES | YES |
| 中/长冒险蓝图解析+实例化 | - | YES |
| 离线冒险模板(5个) | YES | 50+模板 |
| 基础遭遇生成(CR预算) | YES | 完整5e规则 |
| 基础战利品分布 | YES | 完整稀有度体系 |
| 冒险状态管理(无存档) | YES | 存档系统 |
| DM Agent实时叙事 | YES | YES |
| 结算系统(成功/失败) | YES | 完整伤疤+传承 |
| 世界状态挂钩 | 简化版 | 完整版 |
| 分支逻辑 | 简化(1-2分支) | 完整多分支 |

---

## 1A. 玩家体验幻想 (Player Fantasy)

冒险生成系统的核心情感承诺：

- **"这个故事是我的"** — 每次冒险由LLM编剧Agent基于我的队伍、世界状态、主题生成独一无二的剧本。不存在"重复刷同一个副本"。

- **"命运的骰子在转动"** — 三层管线让每次冒险既有LLM的创意（第一层），又有程序的平衡保障（第二层），还有DM Agent的实时叙事（第三层）。玩家感受到的是"一个会讲故事的DM在主持我的冒险"。

- **"选择真的有后果"** — 分支节点不可返回，每个选择导向不同的遭遇和结局。世界状态挂钩让这次冒险的成败影响下一次。

### 反支柱合规
- ✅ 不是刷关打宝 — LLM生成独一无二叙事
- ✅ 不是纯随机Roguelike — 程序层保障平衡
- ✅ 不是预设内容 — 每次冒险都是新故事

---

## 1B. 可调参数 (Tuning Knobs)

| 参数 | 当前值 | 安全范围 | 影响面 |
|------|:------:|:--------:|--------|
| **三层管线重试次数** | 3 | 2-5 | 蓝图生成可靠性 |
| **离线模板数(MVP)** | 5 | 5-20 | 离线冒险多样性 |
| **离线模板数(Phase 2+)** | 50+ | 30-100 | 离线冒险多样性 |
| **CR预算容忍度** | ±0.5 | ±0.2-1.0 | 遭遇难度弹性 |
| **分支深度(短冒险)** | 1 | 0-2 | 剧情复杂度 |
| **分支深度(中冒险)** | 2 | 1-4 | 剧情复杂度 |
| **节点数(短冒险)** | 7-8 | 6-10 | 冒险长度 |
| **节点数(中冒险)** | 15-25 | 12-30 | 冒险长度 |
| **节点数(长冒险)** | 30-50 | 25-55 | 冒险长度 |
| **战利品等级上限(短)** | 2 | 1-3 | 奖励期望 |
| **战利品等级上限(中)** | 3 | 2-4 | 奖励期望 |
| **战利品等级上限(长)** | 4 | 3-5 | 奖励期望 |
| **世界状态挂钩数(短)** | 1-2 | 1-4 | 世界演进深度 |
| **世界状态挂钩数(中)** | 2-4 | 2-6 | 世界演进深度 |
| **世界状态挂钩数(长)** | 3-6 | 3-8 | 世界演进深度 |

---

## 1C. 依赖关系 (Dependencies)

| 依赖系统 | 依赖内容 | 状态 |
|----------|----------|:----:|
| **LLM集成网关** | 编剧Agent/平衡Agent | ✅ 已审查 |
| **角色系统** | 队伍数据(等级/属性/关系) | ✅ 已审查 |
| **世界状态系统** | 世界事件/势力关系 | ❌ 未设计 |
| **地图探索系统** | 节点图生成 | ✅ 已审查 |
| **战斗系统** | 遭遇CR/敌人配置 | ✅ 已审查 |
| **物品装备系统** | 战利品tier | ✅ 已审查 |

> **⚠️ 技术栈迁移 (v1.2, 2026-05-09)**: 本文档含约 16 处 GDScript 代码示例，需在实现前翻译为 C#（项目技术栈: MonoGame/.NET 8）。受影响章节: §2.4, §4.2, §4.5-4.9, §5.3, §6.3, §4.12。
>
> **迁移路线图**:
> 1. 逐节提取 GDScript 代码块 → 保留为算法规范注释
> 2. 翻译为 C#：`func` → `method`, `Dictionary` → `Dictionary<string,object>`, `randi_range` → `Random.Next`, `match` → `switch`
> 3. GDScript 特有语法（`load()`, `preload()`, `yield()`）替换为 MonoGame 等效调用
> 4. 翻译后删除 GDScript 原文，替换为 `// 算法规范: 原 GDScript 伪代码已翻译`
> 5. 预计工作量: 1-2 个实现会话

---

## 2. 冒险蓝图解析器

### 2.1 输入数据

输入为编剧Agent生成的 `adventure_blueprint.json`，完整Schema定义见 [llm-integration.md §3.1.4](./llm-integration.md)。

### 2.2 解析管线

```
┌─────────────┐    ┌─────────────┐    ┌─────────────┐    ┌─────────────┐    ┌─────────────┐
│  JSON Parse │───▶│   Schema    │───▶│  Business   │───▶│ Dependency  │───▶│  Adventure  │
│  (JSON解析) │    │ Validation  │    │   Logic     │    │ Resolution  │    │  Instance   │
│             │    │(Schema验证) │    │ Validation  │    │(依赖解析)   │    │(冒险实例)   │
└─────────────┘    └─────────────┘    │(业务逻辑)   │    └─────────────┘    └─────────────┘
                                      └─────────────┘
```

### 2.3 Schema验证

Schema验证由LLM Gateway的SchemaValidator组件执行，参照llm-integration.md中定义的完整adventure_blueprint.json Schema。验证失败触发重试（最多3次），每次重试附加上一次失败原因到prompt。

### 2.4 业务逻辑验证规则

Schema验证通过后，程序执行以下业务逻辑验证：

```gdscript
class BlueprintBusinessValidator:

    static func validate(blueprint: Dictionary, party_state: Dictionary) -> Array[ValidationError]:
        var errors: Array[ValidationError] = []
        var warnings: Array[ValidationError] = []

        # ── 规则1: 节点数量范围 ──
        var nodes = blueprint.plot_outline.nodes
        var tier = blueprint.meta.tier
        match tier:
            "short":
                if nodes.size() < 7 or nodes.size() > 8:
                    errors.append(ValidationError.new("NODE_COUNT", "短冒险节点数应为7-8，实际: %d" % nodes.size()))
            "medium":
                if nodes.size() < 15 or nodes.size() > 25:
                    errors.append(ValidationError.new("NODE_COUNT", "中冒险节点数应为15-25，实际: %d" % nodes.size()))
            "long":
                if nodes.size() < 30 or nodes.size() > 50:
                    errors.append(ValidationError.new("NODE_COUNT", "长冒险节点数应为30-50，实际: %d" % nodes.size()))

        # ── 规则2: 所有节点引用有效 ──
        var node_ids = {}
        for node in nodes:
            node_ids[node.node_id] = true
        for node in nodes:
            for conn in node.get("connections", []):
                if conn.target_node_id not in node_ids:
                    errors.append(ValidationError.new("INVALID_REF",
                        "节点 %s 引用了不存在的目标节点 %s" % [node.node_id, conn.target_node_id]))
            for choice in node.get("choices", []):
                if choice.get("target_node") and choice.target_node not in node_ids:
                    errors.append(ValidationError.new("INVALID_REF",
                        "节点 %s 的选择 %s 引用了不存在的目标节点 %s" % [node.node_id, choice.choice_id, choice.target_node]))

        # ── 规则3: 无孤立节点（从起始节点BFS可达）──
        var adjacency = _build_adjacency(nodes)
        var start_node = _find_start_node(nodes)
        if start_node:
            var reachable = _bfs(start_node, adjacency)
            for node in nodes:
                if node.node_id not in reachable:
                    errors.append(ValidationError.new("ORPHAN_NODE",
                        "节点 %s 不可从起始节点到达" % node.node_id))

        # ── 规则4: CR范围匹配推荐等级 ──
        var recommended_level = blueprint.difficulty_profile.recommended_level
        var cr_range = blueprint.difficulty_profile.cr_range
        var expected_max_cr = _get_expected_max_cr(recommended_level, tier)
        if cr_range.max > expected_max_cr:
            warnings.append(ValidationError.new("CR_RANGE_HIGH",
                "CR上限 %.1f 超过推荐等级 %d 的预期上限 %.1f" % [cr_range.max, recommended_level, expected_max_cr]))

        # ── 规则5: 战利品等级匹配冒险等级 ──
        var loot_tier = blueprint.difficulty_profile.loot_tier
        var expected_loot_tier = _get_expected_loot_tier(tier, recommended_level)
        if abs(loot_tier - expected_loot_tier) > 1:
            warnings.append(ValidationError.new("LOOT_TIER_MISMATCH",
                "战利品等级 %d 与冒险等级不匹配，预期: %d" % [loot_tier, expected_loot_tier]))

        # ── 规则6: 关键NPC有有效的stat_block_id ──
        for npc in blueprint.key_npcs:
            if npc.get("stat_block_id") and not EnemyDB.has_stat_block(npc.stat_block_id):
                errors.append(ValidationError.new("INVALID_STAT_BLOCK",
                    "NPC %s 引用了不存在的stat_block: %s" % [npc.npc_id, npc.stat_block_id]))

        # ── 规则7: 每个分支选择都指向有效节点 ──
        for node in nodes:
            if node.type == "branch":
                for branch in node.get("branch_config", {}).get("branches", []):
                    if branch.target_node not in node_ids:
                        errors.append(ValidationError.new("INVALID_BRANCH",
                            "分支节点 %s 的分支 %s 指向无效节点 %s" % [node.node_id, branch.branch_id, branch.target_node]))

        # ── 规则8: 战斗节点必须有encounter_id ──
        for node in nodes:
            if node.type in ["combat", "elite_combat", "boss", "ambush"]:
                if not node.get("encounter_id"):
                    errors.append(ValidationError.new("MISSING_ENCOUNTER",
                        "战斗节点 %s 缺少 encounter_id" % node.node_id))

        # ── 规则9: 起始节点必须是opening类型 ──
        if start_node:
            var start_data = _find_node(nodes, start_node)
            if start_data and start_data.type != "opening":
                warnings.append(ValidationError.new("START_TYPE",
                    "起始节点 %s 类型为 %s，建议为 opening" % [start_node, start_data.type]))

        # ── 规则10: Boss节点应在最后20%的节点中 ──
        for node in nodes:
            if node.type == "boss":
                if node.order_index < nodes.size() * 0.8:
                    warnings.append(ValidationError.new("BOSS_PLACEMENT",
                        "Boss节点 %s 的位置偏前 (index %d / %d)" % [node.node_id, node.order_index, nodes.size()]))

        return errors + warnings
```

### 2.5 依赖解析

蓝图验证通过后，解析器需要将蓝图中的引用ID解析为实际的游戏数据：

```
依赖解析流程:

1. NPC stat_block解析
   blueprint.key_npcs[].stat_block_id → EnemyDB.get_stat_block(id)
   如果stat_block不存在 → 使用同CR的默认stat_block + 警告

2. 遭遇模板解析
   blueprint.encounters[].enemy_groups[].enemy_type → EnemyDB.get_template(type)
   如果模板不存在 → 使用theme匹配的同CR替代模板

3. 物品模板解析
   blueprint.loot_profile.thematic_items[].item_id → ItemDB.get_template(id)
   如果物品不存在 → 程序化生成同rarity同type的替代物品

4. Tileset解析
   node.environment.tileset → TilesetDB.get_path(tileset_id)
   如果tileset不存在 → 使用theme默认tileset

5. 对话树解析
   npc_config.dialogue_tree_id → DialogueDB.get_tree(id)
   如果对话树不存在 → 使用role默认对话树
```

### 2.6 错误处理策略

```
验证结果处理:

├─ Schema验证失败
│   → 重试(最多3次)，每次附加失败原因到prompt
│   → 3次全部失败 → 降级到离线模板
│
├─ 业务逻辑验证 — 错误(errors)
│   → 重试(最多3次)，附加具体错误描述
│   → 3次全部失败 → 降级到离线模板
│
├─ 业务逻辑验证 — 警告(warnings)
│   → 记录警告日志
│   → 程序自动修正(如调整CR、补充缺失字段)
│   → 继续实例化流程
│
└─ 依赖解析失败
│   → 使用替代数据 + 警告日志
│   → 继续实例化流程
```

---

## 3. 冒险类型模板

### 3.1 短冒险模板 — "一次喝完的酒" (~30分钟)

```json
{
  "template_id": "adventure_template_short",
  "tier": "short",
  "meta": {
    "estimated_duration_minutes": 30,
    "recommended_party_size": 4,
    "node_count_range": [7, 8],
    "structure": "linear_with_one_climax"
  },
  "node_distribution": {
    "opening": { "count": 1, "percentage": 10 },
    "combat": { "count_range": [3, 5], "percentage": 35 },
    "exploration": { "count_range": [1, 2], "percentage": 30 },
    "dialogue": { "count_range": [0, 1], "percentage": 12 },
    "rest": { "count_range": [0, 1], "percentage": 8 },
    "merchant": { "count_range": [0, 1], "percentage": 5 },
    "boss": { "count": 1, "percentage": 0 }
  },
  "structure_rules": {
    "boss_placement": "last_25_percent",
    "branch_chance": "minimal",
    "max_branches": 2,
    "rest_before_boss": true,
    "opening_must_be_first": true
  },
  "difficulty_profile_template": {
    "encounter_count": { "easy": 1, "medium": 1, "hard": 1, "deadly": 0 },
    "boss_difficulty": "hard",
    "rest_opportunities": 1,
    "short_rest_opportunities": 1,
    "trap_density": "low",
    "puzzle_complexity": "none"
  },
  "loot_profile_template": {
    "tier_range": [1, 2],
    "expected_items": 3,
    "consumable_ratio": 0.6,
    "boss_guaranteed_rarity": "uncommon"
  },
  "example_node_sequence": [
    { "index": 0, "type": "opening", "description": "任务引入/场景设定" },
    { "index": 1, "type": "exploration", "description": "初步探索，发现线索" },
    { "index": 2, "type": "combat", "description": "第一场战斗(普通遭遇)" },
    { "index": 3, "type": "dialogue", "description": "NPC交互/情报获取" },
    { "index": 4, "type": "combat", "description": "第二场战斗(中等遭遇)" },
    { "index": 5, "type": "rest", "description": "短休恢复点" },
    { "index": 6, "type": "boss", "description": "Boss战" },
    { "index": 7, "type": "epilogue", "description": "结局/结算" }
  ]
}
```

### 3.2 中冒险模板 — "一个晚上的故事" (~3小时)

```json
{
  "template_id": "adventure_template_medium",
  "tier": "medium",
  "meta": {
    "estimated_duration_minutes": 180,
    "recommended_party_size": 4,
    "node_count_range": [15, 25],
    "structure": "2_3_locations_with_puzzle_and_choices"
  },
  "node_distribution": {
    "opening": { "count": 1, "percentage": 4 },
    "combat": { "count_range": [5, 8], "percentage": 32 },
    "elite_combat": { "count_range": [1, 2], "percentage": 5 },
    "exploration": { "count_range": [3, 5], "percentage": 18 },
    "dialogue": { "count_range": [3, 6], "percentage": 23 },
    "puzzle": { "count_range": [1, 2], "percentage": 5 },
    "rest": { "count_range": [1, 3], "percentage": 9 },
    "merchant": { "count_range": [0, 1], "percentage": 4 },
    "boss": { "count_range": [1, 2], "percentage": 0 },
    "branch": { "count_range": [2, 3], "percentage": 0 }
  },
  "structure_rules": {
    "boss_placement": "mini_boss_at_act2_start_plus_end_boss",
    "branch_chance": "moderate",
    "max_branches": 3,
    "puzzle_required": true,
    "act_structure": "2_or_3_acts",
    "rest_between_acts": true,
    "merchant_at_act_boundary": true,
    "midpoint_boss_index": "floor(node_count * 0.4)"
  },
  "difficulty_profile_template": {
    "encounter_count": { "easy": 2, "medium": 4, "hard": 2, "deadly": 1 },
    "boss_difficulties": ["hard", "deadly"],
    "rest_opportunities": 2,
    "short_rest_opportunities": 3,
    "trap_density": "medium",
    "puzzle_complexity": "moderate"
  },
  "loot_profile_template": {
    "tier_range": [2, 3],
    "expected_items": 8,
    "consumable_ratio": 0.5,
    "boss_guaranteed_rarity": "rare",
    "thematic_items_count": 2
  },
  "example_node_sequence": [
    { "index": 0, "type": "opening", "act": 1 },
    { "index": 1, "type": "dialogue", "act": 1 },
    { "index": 2, "type": "exploration", "act": 1 },
    { "index": 3, "type": "combat", "act": 1 },
    { "index": 4, "type": "branch", "act": 1 },
    { "index": 5, "type": "combat", "act": 1 },
    { "index": 6, "type": "rest", "act": 1 },
    { "index": 7, "type": "elite_combat", "act": 2, "description": "中Boss" },
    { "index": 8, "type": "dialogue", "act": 2 },
    { "index": 9, "type": "exploration", "act": 2 },
    { "index": 10, "type": "puzzle", "act": 2 },
    { "index": 11, "type": "branch", "act": 2 },
    { "index": 12, "type": "merchant", "act": 2 },
    { "index": 13, "type": "rest", "act": 2 },
    { "index": 14, "type": "combat", "act": 3 },
    { "index": 15, "type": "dialogue", "act": 3 },
    { "index": 16, "type": "exploration", "act": 3 },
    { "index": 17, "type": "combat", "act": 3 },
    { "index": 18, "type": "rest", "act": 3 },
    { "index": 19, "type": "boss", "act": 3 },
    { "index": 20, "type": "epilogue", "act": 3 }
  ]
}
```

### 3.3 长冒险模板 — "一部史诗" (~6小时)

```json
{
  "template_id": "adventure_template_long",
  "tier": "long",
  "meta": {
    "estimated_duration_minutes": 360,
    "recommended_party_size": 4,
    "node_count_range": [30, 50],
    "structure": "three_acts_multiple_endings"
  },
  "node_distribution": {
    "opening": { "count": 1, "percentage": 2 },
    "combat": { "count_range": [9, 15], "percentage": 26 },
    "elite_combat": { "count_range": [2, 4], "percentage": 7 },
    "exploration": { "count_range": [4, 7], "percentage": 13 },
    "dialogue": { "count_range": [9, 15], "percentage": 26 },
    "puzzle": { "count_range": [1, 3], "percentage": 5 },
    "rest": { "count_range": [3, 5], "percentage": 9 },
    "merchant": { "count_range": [3, 5], "percentage": 5 },
    "boss": { "count": 3, "percentage": 0 },
    "branch": { "count_range": [5, 8], "percentage": 0 },
    "skill_challenge": { "count_range": [1, 3], "percentage": 3 },
    "story": { "count_range": [1, 3], "percentage": 4 }
  },
  "structure_rules": {
    "boss_placement": "one_per_act",
    "branch_chance": "high",
    "max_branches": 8,
    "multiple_endings": true,
    "ending_count_range": [2, 4],
    "act_structure": "three_acts",
    "act_lengths": { "act_1": "25%", "act_2": "45%", "act_3": "30%" },
    "rest_between_acts": true,
    "merchant_per_act": true,
    "puzzle_in_act_2": true,
    "moral_choices": true,
    "persistent_consequences": true
  },
  "difficulty_profile_template": {
    "encounter_count": { "easy": 3, "medium": 6, "hard": 4, "deadly": 2 },
    "boss_difficulties": ["hard", "deadly", "deadly"],
    "rest_opportunities": 4,
    "short_rest_opportunities": 6,
    "trap_density": "medium",
    "puzzle_complexity": "complex"
  },
  "loot_profile_template": {
    "tier_range": [3, 4],
    "expected_items": 15,
    "consumable_ratio": 0.4,
    "boss_guaranteed_rarity": "rare",
    "final_boss_rarity": "very_rare",
    "thematic_items_count": 4,
    "unique_legendary_chance": 0.3
  }
}
```

### 3.4 模板选择算法

```
Algorithm: SelectAdventureTemplate(tier, party_level, theme_preference)
──────────────────────────────────────────────────────────────────────
输入:
  tier: "short" / "medium" / "long"
  party_level: int (平均等级)
  theme_preference: String (可选)

输出:
  template: AdventureTemplate

Step 1: 加载对应tier的基础模板
  template = load("data/adventure_templates/template_%s.json" % tier)

Step 2: 根据party_level调整难度参数
  if party_level <= 2:
    template.difficulty_profile.encounter_count.deadly = 0
    template.difficulty_profile.boss_difficulty = "medium"
  elif party_level <= 5:
    template.difficulty_profile.encounter_count.deadly = min(1, template...)
  # Lv6+ 使用模板默认值

Step 3: 根据theme_preference调整节点类型分布
  if theme_preference == "combat_heavy":
    template.node_distribution.combat.percentage += 10
    template.node_distribution.dialogue.percentage -= 10
  elif theme_preference == "narrative_heavy":
    template.node_distribution.dialogue.percentage += 10
    template.node_distribution.combat.percentage -= 10

Step 4: 返回调整后的模板
  return template
```

---

## 4. 程序化实例化算法

### 4.1 总览

实例化是将蓝图转化为可玩内容的核心流程。以下是完整的14步算法：

```
Algorithm: InstantiateAdventure(blueprint, party_state)
──────────────────────────────────────────────────────
输入:
  blueprint: AdventureBlueprint (已验证)
  party_state: PartyState (角色等级/职业/装备)

输出:
  adventure_instance: AdventureInstance (可玩的冒险)

Step 1:  解析蓝图 → 验证 → 解析依赖 (Section 2)
Step 2:  生成冒险节点图 (Map System兼容格式)
Step 3:  为每个节点分配房间模板 (基于type + theme)
Step 4:  为战斗节点生成遭遇数据 (CR预算→敌人组合)
Step 5:  为对话节点生成NPC对话配置
Step 6:  为探索节点生成隐藏物体+陷阱+战利品
Step 7:  为商人节点生成商店库存
Step 8:  为休息节点生成恢复事件
Step 9:  为谜题节点生成谜题数据
Step 10: 为Boss节点生成完整Boss数据(多阶段)
Step 11: 连接房间走廊 (Map System)
Step 12: 放置交互标签 (基于蓝图标签)
Step 13: 设置遭遇触发器和陷阱放置
Step 14: 初始化冒险状态机
```

### 4.2 Step 2: 生成冒险节点图

```gdscript
func generate_adventure_node_graph(blueprint: Dictionary) -> Dictionary:
    """将蓝图nodes[]转换为Map System兼容的节点图"""
    var nodes = blueprint.plot_outline.nodes
    var graph = {}

    for node_data in nodes:
        var node_id = node_data.node_id
        var map_node = {
            "node_id": node_id,
            "type": _map_node_type(node_data.type),
            "name": node_data.get("name", ""),
            "description": node_data.description,
            "order_index": node_data.order_index,
            "required_to_proceed": node_data.get("required_to_proceed", false),
            "connections": [],
            "interaction_tags": node_data.get("interaction_tags", []),
            "theme_tags": node_data.get("theme_tags", []) + blueprint.theme_tags,
            "environment": node_data.get("environment", {}),
            "room_size": _get_room_size(node_data.type, blueprint.meta.tier)
        }

        # 转换连接格式
        for conn in node_data.get("connections", []):
            map_node.connections.append({
                "target_node": conn.target_node_id,
                "connection_type": conn.get("connection_type", "default"),
                "direction": _infer_direction(node_id, conn.target_node_id, nodes),
                "door_type": _determine_door_type(conn)
            })

        graph[node_id] = map_node

    return graph
```

### 4.3 Step 3: 房间模板分配

```
Algorithm: AssignRoomTemplates(node_graph, theme_tags)
──────────────────────────────────────────────────────
for each node in node_graph:
    # 1. 确定房间尺寸
    node.room_size = get_default_room_size(node.type, tier)
        # 参见 map-exploration.md §2.3.2 默认房间尺寸表

    # 2. 选择房间模板
    candidates = RoomTemplateDB.query({
        "node_type": node.type,
        "size_match": node.room_size,
        "theme_intersection": node.theme_tags
    })

    if candidates.is_empty():
        # 使用通用模板 + 程序化填充
        template = RoomTemplateDB.get_generic(node.type, node.room_size)
    else:
        # 选择主题匹配度最高的
        template = max_by_theme_match(candidates, node.theme_tags)

    node.room_template = template
```

### 4.4 Step 4: 遭遇数据生成

详见 Section 6（遭遇生成系统）。

### 4.5 Step 5: 对话节点配置

```gdscript
func configure_dialogue_node(node_data: Dictionary, blueprint: Dictionary) -> Dictionary:
    """为对话节点生成完整的NPC对话配置"""
    var npc_id = node_data.get("npc_config", {}).get("npc_id", "")
    var npc_data = _find_npc(blueprint.key_npcs, npc_id)

    if not npc_data:
        # 使用默认NPC配置
        return _generate_default_dialogue_config(node_data)

    return {
        "npc_id": npc_data.npc_id,
        "name": npc_data.name,
        "stat_block_id": npc_data.get("stat_block_id", "commoner"),
        "personality_tags": npc_data.personality_tags,
        "role": npc_data.role,
        "initial_attitude": npc_data.get("relation_to_party", "neutral"),
        "dialogue_tree_id": npc_data.get("dialogue_tree_id", ""),
        "key_topics": _extract_topics(node_data, npc_data),
        "reaction_thresholds": _build_reaction_thresholds(npc_data),
        "dialogue_outcomes": node_data.get("dialogue_outcomes", []),
        "voice_style": npc_data.get("voice_style", "neutral")
    }
```

### 4.6 Step 6: 探索节点数据生成

```gdscript
func configure_exploration_node(node_data: Dictionary, difficulty_profile: Dictionary) -> Dictionary:
    """为探索节点生成隐藏物体、陷阱和战利品"""
    var config = {
        "hidden_checks": [],
        "loot_spots": [],
        "traps": [],
        "environmental_storytelling": []
    }

    # 1. 生成隐藏检定 (基于陷阱密度)
    var trap_density = difficulty_profile.trap_density
    var trap_count = _get_trap_count(trap_density, node_data.room_size)
    for i in range(trap_count):
        config.traps.append(_generate_trap(difficulty_profile, node_data.theme_tags))

    # 2. 生成隐藏物体
    var hidden_count = randi_range(1, 3)
    for i in range(hidden_count):
        config.hidden_checks.append({
            "check_id": "%s_hidden_%d" % [node_data.node_id, i],
            "type": _random_hidden_type(),
            "detection_skill": "perception",
            "detection_dc": _calculate_detection_dc(difficulty_profile),
            "passive_detection_possible": randf() > 0.5,
            "reveal_condition": "proximity_3_tiles"
        })

    # 3. 生成战利品点
    var loot_count = randi_range(1, 2)
    for i in range(loot_count):
        config.loot_spots.append({
            "spot_id": "%s_loot_%d" % [node_data.node_id, i],
            "loot_pool": "tier_%d_common" % difficulty_profile.loot_tier,
            "detection_dc": _calculate_loot_dc(difficulty_profile),
            "check_skill": "investigation",
            "guaranteed": randf() > 0.3,
            "spawn_chance": randf_range(0.5, 1.0)
        })

    # 4. 环境叙事线索
    config.environmental_storytelling = _generate_env_storytelling(node_data)

    return config
```

### 4.7 Step 7: 商人库存生成

```gdscript
func configure_merchant_node(node_data: Dictionary, loot_tier: int) -> Dictionary:
    """为商人节点生成商店库存"""
    var guaranteed_items = []
    var random_pool = []

    # 固定商品: 治疗药水 + 基础消耗品
    guaranteed_items.append({"item_id": "health_potion", "count": 3, "price_gp": 50})
    guaranteed_items.append({"item_id": "antidote", "count": 2, "price_gp": 40})

    # 根据loot_tier添加高级商品
    if loot_tier >= 2:
        guaranteed_items.append({"item_id": "greater_health_potion", "count": 1, "price_gp": 150})
    if loot_tier >= 3:
        guaranteed_items.append({"item_id": "scroll_of_fireball", "count": 1, "price_gp": 300})

    # 随机商品池
    var random_count = randi_range(2, 4)
    random_pool = ItemDB.query_random({
        "tier": loot_tier,
        "type_in": ["potion", "scroll", "weapon", "armor"],
        "count": random_count,
        "exclude_ids": guaranteed_items.map(func(i): return i.item_id)
    })

    return {
        "guaranteed_items": guaranteed_items,
        "random_items_pool": random_pool,
        "random_price_multiplier_range": [0.8, 1.5],
        "bargain_config": {
            "can_haggle": true,
            "haggle_skill": "persuasion",
            "haggle_dc_base": 15,
            "max_discount_percent": 30
        }
    }
```

### 4.8 Step 8: 休息节点配置

```gdscript
func configure_rest_node(node_data: Dictionary, difficulty_profile: Dictionary) -> Dictionary:
    """为休息节点生成恢复事件"""
    var rest_type = "short_rest"
    var long_rest_available = false

    # 长休条件: 冒险进度超过40% 且 不在危险区域
    var progress_percent = node_data.order_index * 100.0 / _total_nodes
    if progress_percent > 40 and node_data.get("safe_zone", true):
        long_rest_available = true
        rest_type = "long_rest"

    return {
        "rest_type": rest_type,
        "long_rest_available": long_rest_available,
        "short_rest_bonuses": _generate_rest_bonuses(node_data.theme_tags),
        "camp_events": _generate_camp_events(node_data, difficulty_profile),
        "supply_restrictions": {
            "requires_rations": false,
            "ambush_risk": 0.05 if rest_type == "short_rest" else 0.10
        }
    }
```

### 4.9 Step 9: 谜题节点配置

```gdscript
func configure_puzzle_node(node_data: Dictionary, difficulty_profile: Dictionary) -> Dictionary:
    """为谜题节点生成谜题数据"""
    var puzzle_complexity = difficulty_profile.puzzle_complexity

    var puzzle_type = _select_puzzle_type(puzzle_complexity)
    var puzzle_data = PuzzleDB.generate({
        "type": puzzle_type,
        "complexity": puzzle_complexity,
        "theme": node_data.theme_tags
    })

    return {
        "puzzle_id": puzzle_data.id,
        "puzzle_type": puzzle_type,
        "elements": puzzle_data.elements,
        "solution": puzzle_data.solution,
        "solution_logic": puzzle_data.logic_description,
        "max_attempts": 3 if puzzle_complexity == "simple" else 5,
        "hints_available": _generate_puzzle_hints(puzzle_data, puzzle_complexity),
        "success_effect": "unlock_path",
        "failure_effect": _select_failure_effect(puzzle_complexity),
        "on_solve": {
            "unlock_connection": _find_locked_connection(node_data),
            "grant_loot_pool": "tier_%d_rare" % difficulty_profile.loot_tier
        }
    }
```

### 4.10 Step 10: Boss数据生成

详见 Section 6.5（Boss stat block）。

### 4.11 Step 11-13: 走廊连接与标签放置

参照 [map-exploration.md §11.3](./map-exploration.md) 走廊生成规范和 §6.3 标签分配机制。

### 4.12 Step 14: 初始化冒险状态机

```gdscript
func initialize_adventure_state(blueprint: Dictionary, node_graph: Dictionary) -> AdventureState:
    """初始化冒险运行时状态"""
    var state = AdventureState.new()

    state.adventure_id = blueprint.id
    state.adventure_title = blueprint.meta.title
    state.tier = blueprint.meta.tier
    state.node_graph = node_graph
    state.current_node_id = _find_start_node(node_graph)
    state.visited_nodes = [state.current_node_id]
    state.choices_made = []
    state.resources_consumed = {
        "spell_slots_used": {},
        "hit_dice_used": 0,
        "items_consumed": [],
        "gold_spent": 0
    }
    state.npc_interactions = {}
    state.quest_objectives = _extract_objectives(blueprint)
    state.world_state_changes_pending = []
    state.adventure_log = AdventureLog.new()
    state.start_time = Time.get_unix_time_from_system()

    return state
```

---

## 5. 分支逻辑实现

### 5.1 选择数据模型

```json
{
  "choice_id": "choose_elf_path",
  "flavor_text": "跟随精灵符文的指引",
  "hint": "也许能找到古老的庇护所",
  "consequence_type": "branch",
  "target_node": "elf_shrine",
  "conditions": {
    "skill_check": {
      "ability": "wis",
      "skill": "perception",
      "dc": 12,
      "auto_pass_if": "elf_in_party"
    },
    "item_requirement": null,
    "relationship_threshold": null,
    "flag_requirement": null
  },
  "consequences": {
    "immediate": {
      "world_flag_set": "chose_elf_path",
      "relationship_effects": { "char_003": 1 }
    },
    "downstream": {
      "affects_node": "elf_shrine",
      "modifies_encounter": "reduce_difficulty_by_1"
    }
  }
}
```

### 5.2 分支类型

| 分支类型 | consequence_type | 效果 | 示例 |
|----------|-----------------|------|------|
| 故事分支 | `story` | 改变叙事走向，设置世界标记 | 选择帮助或背叛NPC |
| 战斗分支 | `combat` | 触发不同遭遇 | 选择正面交锋或偷袭 |
| 路径分支 | `branch` | 导向不同节点路径 | 选择左边或右边的路 |
| 技能分支 | `skill_check` | 通过检定解锁选项 | DC 15说服让NPC让路 |
| 物品分支 | `item` | 消耗/使用物品 | 使用钥匙开门 |
| 关系分支 | `relationship` | 基于角色关系值 | 关系≥5时NPC主动帮助 |
| 伤疤风险 | `scar_risk` | 高风险高回报 | 冒险使用禁忌魔法 |

### 5.3 选择解析流程

```
Algorithm: ResolveChoice(choice, adventure_state, party_state)
──────────────────────────────────────────────────────────────
输入:
  choice: ChoiceData (玩家选择)
  adventure_state: AdventureState (当前冒险状态)
  party_state: PartyState (队伍状态)

输出:
  result: ChoiceResult (选择结果)

Step 1: 检查前置条件
  if choice.conditions.skill_check:
    roll = d20 + get_skill_modifier(actor, choice.conditions.skill_check)
    if roll < choice.conditions.skill_check.dc:
      return ChoiceResult.new(false, "skill_check_failed", choice.failure_path)

  if choice.conditions.item_requirement:
    if not party_has_item(choice.conditions.item_requirement):
      return ChoiceResult.new(false, "missing_item", null)

  if choice.conditions.relationship_threshold:
    if get_relationship_value(actor, target) < choice.conditions.relationship_threshold:
      return ChoiceResult.new(false, "relationship_too_low", null)

Step 2: 执行即时后果
  for effect in choice.consequences.immediate:
    execute_effect(effect, adventure_state, party_state)

Step 3: 记录选择
  adventure_state.choices_made.append({
    "choice_id": choice.choice_id,
    "node_id": adventure_state.current_node_id,
    "timestamp": Time.get_unix_time_from_system(),
    "outcome": "success"
  })

Step 4: 导航到目标节点
  adventure_state.current_node_id = choice.target_node
  adventure_state.visited_nodes.append(choice.target_node)

Step 5: 返回结果
  return ChoiceResult.new(true, "success", choice.target_node)
```

### 5.4 失败技能检定处理

```
技能检定失败策略:

1. 硬失败 (无替代路径)
   → 显示失败描述文本
   → 该选项变为不可选(灰色)
   → 玩家必须选择其他选项

2. 软失败 (有替代路径)
   → 显示失败描述文本
   → 导航到failure_path节点(通常是更难的路径)
   → 例: 说服失败 → 只能战斗通过

3. 部分成功
   → 检定结果接近DC(差值≤3)
   → 获得部分好处 + 部分惩罚
   → 例: 开锁差1点 → 门开了但触发了警报

4. 可重试
   → 消耗资源后可重试(如花费金币贿赂)
   → 每次重试DC+2
   → 最多重试3次
```

### 5.5 持久选择追踪

```gdscript
class ChoiceTracker:
    """追踪冒险中的所有选择及其影响"""

    var choices: Array[Dictionary] = []
    var flags: Dictionary = {}  # 世界标记
    var node_modifiers: Dictionary = {}  # 节点修改器

    func record_choice(choice_data: Dictionary) -> void:
        choices.append(choice_data)

        # 设置世界标记
        for flag in choice_data.get("flags_set", []):
            flags[flag] = true

        # 应用下游修改器
        for modifier in choice_data.get("downstream_modifiers", []):
            if modifier.target_node not in node_modifiers:
                node_modifiers[modifier.target_node] = []
            node_modifiers[modifier.target_node].append(modifier)

    func get_node_modifiers(node_id: String) -> Array:
        return node_modifiers.get(node_id, [])

    func has_flag(flag: String) -> bool:
        return flags.get(flag, false)

    func get_choices_at_node(node_id: String) -> Array:
        return choices.filter(func(c): return c.node_id == node_id)
```

---

## 6. 遭遇生成系统

### 6.1 遭遇数据模型

```json
{
  "encounter_id": "enc_goblin_ambush_01",
  "type": "combat",
  "difficulty": "medium",
  "name": "哥布林伏击",
  "description": "一群哥布林从灌木丛中跳出来",
  "enemy_groups": [
    { "enemy_type": "goblin_warrior", "count": 3, "cr": 0.25 },
    { "enemy_type": "goblin_shaman", "count": 1, "cr": 0.5 }
  ],
  "total_enemy_count": 4,
  "calculated_cr": 1.5,
  "formation": "ambush_flanks",
  "trigger_type": "proximity",
  "trigger_radius_tiles": 6,
  "can_avoid": false,
  "special_conditions": [],
  "terrain_features": [
    { "type": "boulder", "tags": ["HalfCover"], "position": [4, 7] },
    { "type": "crate", "tags": ["Breakable", "Flammable"], "position": [12, 3] }
  ],
  "rewards": {
    "xp": 300,
    "gold_range": { "min": 30, "max": 80 },
    "item_drops": ["goblin_ear_necklace"]
  },
  "loot_pool": "tier_1_common"
}
```

### 6.2 遭遇难度计算 (DND 5e XP阈值)

基于DND 5e DMG p.82的遭遇构建规则：

```
单角色XP阈值表:
┌──────┬───────┬────────┬────────┬────────┐
│ Lv   │ Easy  │ Medium │ Hard   │ Deadly │
├──────┼───────┼────────┼────────┼────────┤
│  1   │   25  │    50  │    75  │   100  │
│  2   │   50  │   100  │   150  │   200  │
│  3   │   75  │   150  │   225  │   400  │
│  4   │  125  │   250  │   375  │   500  │
│  5   │  250  │   500  │   750  │  1100  │
│  6   │  300  │   600  │   900  │  1400  │
│  7   │  350  │   750  │  1100  │  1700  │
│  8   │  450  │   900  │  1400  │  2100  │
│  9   │  550  │  1100  │  1600  │  2400  │
│ 10   │  600  │  1200  │  1900  │  2800  │
└──────┴───────┴────────┴────────┴────────┘

敌人数量XP倍率:
┌──────────────┬────────┐
│ 敌人数量      │ 倍率   │
├──────────────┼────────┤
│ 1            │ ×1.0   │
│ 2            │ ×1.5   │
│ 3-6          │ ×2.0   │
│ 7-10         │ ×2.5   │
│ 11-14        │ ×3.0   │
│ 15+          │ ×4.0   │
└──────────────┴────────┘
```

**计算示例**:

```
场景: 4人Lv3队伍，Medium难度遭遇

Step 1: 计算XP预算
  每人Medium阈值 = 150 XP
  总XP预算 = 150 × 4 = 600 XP (adjusted)

Step 2: 反推实际怪物XP
  如果选3个敌人(倍率×2.0):
    实际怪物总XP = 600 / 2.0 = 300 XP
    可选: 3个CR 1/2 (每只100XP) = 300 XP ✓

  如果选5个敌人(倍率×2.0):
    实际怪物总XP = 600 / 2.0 = 300 XP
    可选: 5个CR 1/4 (每只50XP) + 1个CR 1/2 (100XP) = 350 XP ≈ ✓

Step 3: 转换为CR预算(简化版)
  Lv3 × 4人 Medium → CR预算 ≈ 3.0
```

**简化CR预算速查表** (本游戏使用, 基于4人队伍):

| 队伍Lv×人数 | Easy CR | Medium CR | Hard CR | Deadly CR |
|------------|:---:|:---:|:---:|:---:|
| Lv1 × 4 | 0.5 | 1.0 | 1.5 | 2.0 |
| Lv2 × 4 | 1.0 | 2.0 | 3.0 | 4.0 |
| Lv3 × 4 | 1.5 | 3.0 | 4.5 | 6.0 |
| Lv4 × 4 | 2.0 | 4.0 | 6.0 | 8.0 |
| Lv5 × 4 | 3.0 | 5.0 | 7.5 | 10.0 |
| Lv6 × 4 | 3.5 | 6.0 | 9.0 | 12.0 |
| Lv7 × 4 | 4.0 | 7.5 | 11.0 | 15.0 |
| Lv8 × 4 | 5.0 | 9.0 | 14.0 | 18.0 |
| Lv9 × 4 | 6.0 | 11.0 | 16.0 | 22.0 |
| Lv10 × 4 | 7.0 | 12.0 | 19.0 | 26.0 |

> **非4人队伍缩放**: 实际CR预算 = 查表值 × (actual_party_size / 4)。例如3人Lv3 Medium: CR 3.0 × (3/4) = 2.25。

### 6.3 敌人组合生成算法

```
Algorithm: GenerateEnemyComposition(cr_budget, theme_tags, difficulty)
──────────────────────────────────────────────────────────────────────
输入:
  cr_budget: float (CR预算)
  theme_tags: Array[String] (主题标签)
  difficulty: String (难度等级)

输出:
  enemies: Array[{template_id, count, cr, role}]

Step 1: 从敌人数据库筛选候选
  candidates = EnemyDB.query({
      "theme_match": theme_tags,
      "cr_max": cr_budget + 1,
      "cr_min": cr_budget * 0.05
  })

Step 2: 确定敌人数量范围
  if difficulty == "easy": target_count = randi_range(1, 2)
  elif difficulty == "medium": target_count = randi_range(2, 4)
  elif difficulty == "hard": target_count = randi_range(3, 5)
  elif difficulty == "deadly": target_count = randi_range(4, 7)

Step 3: 贪心组合 + 角色平衡
  remaining_budget = cr_budget
  enemies = []

  # 先选一个"主力"敌人(CR最高的)
  main_enemy = select_highest_cr(candidates, remaining_budget * 0.6)
  enemies.append(main_enemy)
  remaining_budget -= main_enemy.cr

  # 再选"填充"敌人
  while remaining_budget > 0.125 and enemies.size() < target_count:
      filler = select_weighted_random(candidates, remaining_budget)
      enemies.append(filler)
      remaining_budget -= filler.cr

Step 4: 分配角色
  assign_roles(enemies):
    最高CR → brute (肉盾)
    次高CR → glass_cannon (高输出)
    最低CR → support / controller (辅助/控制)

Step 5: 验证并调整总CR
  actual_cr = sum(e.cr for e in enemies)
  if abs(actual_cr - cr_budget) > CR_TOLERANCE:
      adjust(enemies, cr_budget)

  adjust() 算法:
    如果 actual_cr < cr_budget - CR_TOLERANCE (不足):
      while actual_cr < cr_budget - CR_TOLERANCE and len(enemies) < MAX_ENEMIES:
        添加最低CR的候选敌人(CR ≤ remaining_budget, 主题匹配优先)
        actual_cr += 新敌人的CR
    如果 actual_cr > cr_budget + CR_TOLERANCE (超出):
      while actual_cr > cr_budget + CR_TOLERANCE and len(enemies) > MIN_ENEMIES:
        移除最低CR的敌人
        actual_cr -= 被移除敌人的CR
    如果仍超出容忍度:
      记录警告: "遭遇CR预算无法精确匹配，偏差: %.1f" % (actual_cr - cr_budget)

  CR_TOLERANCE = 0.5
  MAX_ENEMIES = 10
  MIN_ENEMIES = 2

Step 6: 候选池空回退
  if candidates.is_empty():
    放宽主题过滤 — 保留cr_max/cr_min约束，移除theme_match要求
    candidates = EnemyDB.query({ "cr_max": cr_budget + 1, "cr_min": cr_budget * 0.05 })
    记录警告: "无主题匹配敌人，使用通用候选池"
  if candidates.is_empty():
    使用最近似CR的默认敌人(如 goblin/cr 0.25)
    记录错误: "无法生成遭遇——敌人数据库为空"
```

### 6.4 MVP敌人Stat Block

#### Goblin (哥布林) — CR 1/4

```json
{
  "template_id": "goblin",
  "name": "哥布林",
  "name_en": "Goblin",
  "cr": 0.25,
  "xp": 50,
  "size": "small",
  "type": "humanoid",
  "alignment": "neutral_evil",
  "ac": 15,
  "ac_source": "leather_armor_shield",
  "hp": { "average": 7, "hit_dice": "2d6" },
  "speed": 30,
  "abilities": {
    "str": 8, "dex": 14, "con": 10,
    "int": 10, "wis": 8, "cha": 8
  },
  "skills": { "stealth": 6 },
  "senses": { "darkvision": 60 },
  "languages": ["common", "goblin"],
  "challenge_rating": 0.25,
  "proficiency_bonus": 2,
  "traits": [
    {
      "name": "Nimble Escape",
      "description": "哥布林可以使用附赠动作进行撤离或躲藏"
    }
  ],
  "actions": [
    {
      "name": "Scimitar",
      "type": "melee_weapon_attack",
      "attack_bonus": 4,
      "reach": 5,
      "damage": "1d6+2 slashing",
      "damage_average": 5
    },
    {
      "name": "Shortbow",
      "type": "ranged_weapon_attack",
      "attack_bonus": 4,
      "range": "80/320",
      "damage": "1d6+2 piercing",
      "damage_average": 5
    }
  ],
  "role": "glass_cannon",
  "ai_behavior": "hit_and_run",
  "theme_tags": ["goblin", "forest", "cave"]
}
```

#### Skeleton (骷髅) — CR 1/4

```json
{
  "template_id": "skeleton",
  "name": "骷髅",
  "name_en": "Skeleton",
  "cr": 0.25,
  "xp": 50,
  "size": "medium",
  "type": "undead",
  "alignment": "lawful_evil",
  "ac": 13,
  "ac_source": "armor_scraps",
  "hp": { "average": 13, "hit_dice": "2d8+4" },
  "speed": 30,
  "abilities": {
    "str": 10, "dex": 14, "con": 15,
    "int": 6, "wis": 8, "cha": 5
  },
  "damage_vulnerabilities": ["bludgeoning"],
  "damage_immunities": ["poison"],
  "condition_immunities": ["exhaustion", "poisoned"],
  "senses": { "darkvision": 60 },
  "languages": [],
  "challenge_rating": 0.25,
  "proficiency_bonus": 2,
  "actions": [
    {
      "name": "Shortsword",
      "type": "melee_weapon_attack",
      "attack_bonus": 4,
      "reach": 5,
      "damage": "1d6+2 piercing",
      "damage_average": 5
    },
    {
      "name": "Shortbow",
      "type": "ranged_weapon_attack",
      "attack_bonus": 4,
      "range": "80/320",
      "damage": "1d6+2 piercing",
      "damage_average": 5
    }
  ],
  "role": "brute",
  "ai_behavior": "aggressive_melee",
  "theme_tags": ["undead", "dungeon", "crypt"]
}
```

#### Bandit (强盗) — CR 1/8

```json
{
  "template_id": "bandit",
  "name": "强盗",
  "name_en": "Bandit",
  "cr": 0.125,
  "xp": 25,
  "size": "medium",
  "type": "humanoid",
  "alignment": "any_non_lawful",
  "ac": 12,
  "ac_source": "leather_armor",
  "hp": { "average": 11, "hit_dice": "2d8+2" },
  "speed": 30,
  "abilities": {
    "str": 11, "dex": 12, "con": 12,
    "int": 10, "wis": 10, "cha": 10
  },
  "languages": ["common"],
  "challenge_rating": 0.125,
  "proficiency_bonus": 2,
  "actions": [
    {
      "name": "Scimitar",
      "type": "melee_weapon_attack",
      "attack_bonus": 3,
      "reach": 5,
      "damage": "1d6+1 slashing",
      "damage_average": 4
    },
    {
      "name": "Light Crossbow",
      "type": "ranged_weapon_attack",
      "attack_bonus": 3,
      "range": "80/320",
      "damage": "1d8+1 piercing",
      "damage_average": 5
    }
  ],
  "role": "brute",
  "ai_behavior": "melee_first",
  "theme_tags": ["bandit", "road", "urban"]
}
```

#### Wolf (狼) — CR 1/4

```json
{
  "template_id": "wolf",
  "name": "狼",
  "name_en": "Wolf",
  "cr": 0.25,
  "xp": 50,
  "size": "medium",
  "type": "beast",
  "alignment": "unaligned",
  "ac": 13,
  "ac_source": "natural_armor",
  "hp": { "average": 11, "hit_dice": "2d8+2" },
  "speed": 40,
  "abilities": {
    "str": 12, "dex": 15, "con": 12,
    "int": 3, "wis": 12, "cha": 6
  },
  "skills": { "perception": 3, "stealth": 4 },
  "senses": { "passive_perception": 13 },
  "challenge_rating": 0.25,
  "proficiency_bonus": 2,
  "traits": [
    {
      "name": "Keen Hearing and Smell",
      "description": "狼在基于听觉或嗅觉的感知检定上有优势"
    },
    {
      "name": "Pack Tactics",
      "description": "狼在5尺内有友方生物且友方未失能时，攻击检定有优势"
    }
  ],
  "actions": [
    {
      "name": "Bite",
      "type": "melee_weapon_attack",
      "attack_bonus": 4,
      "reach": 5,
      "damage": "2d4+2 piercing",
      "damage_average": 7,
      "special": "如果目标是生物，DC 11力量豁免失败则倒地"
    }
  ],
  "role": "flanker",
  "ai_behavior": "pack_hunter",
  "theme_tags": ["beast", "forest", "wilderness"]
}
```

#### Kobold (狗头人) — CR 1/8

```json
{
  "template_id": "kobold",
  "name": "狗头人",
  "name_en": "Kobold",
  "cr": 0.125,
  "xp": 25,
  "size": "small",
  "type": "humanoid",
  "alignment": "lawful_evil",
  "ac": 12,
  "ac_source": "natural_armor",
  "hp": { "average": 5, "hit_dice": "2d6-2" },
  "speed": 30,
  "abilities": {
    "str": 7, "dex": 15, "con": 9,
    "int": 8, "wis": 7, "cha": 8
  },
  "senses": { "darkvision": 60 },
  "languages": ["common", "draconic"],
  "challenge_rating": 0.125,
  "proficiency_bonus": 2,
  "traits": [
    {
      "name": "Sunlight Sensitivity",
      "description": "在阳光下，攻击检定和感知检定有劣势"
    },
    {
      "name": "Pack Tactics",
      "description": "5尺内有友方且友方未失能时，攻击检定有优势"
    }
  ],
  "actions": [
    {
      "name": "Dagger",
      "type": "melee_weapon_attack",
      "attack_bonus": 4,
      "reach": 5,
      "damage": "1d4+2 piercing",
      "damage_average": 4
    },
    {
      "name": "Sling",
      "type": "ranged_weapon_attack",
      "attack_bonus": 4,
      "range": "30/120",
      "damage": "1d4+2 bludgeoning",
      "damage_average": 4
    }
  ],
  "role": "swarm",
  "ai_behavior": "pack_swarm",
  "theme_tags": ["kobold", "cave", "dungeon"]
}
```

### 6.5 MVP Boss Stat Block

#### Goblin Boss (哥布林首领) — CR 1

```json
{
  "template_id": "goblin_boss",
  "name": "哥布林首领",
  "name_en": "Goblin Boss",
  "cr": 1,
  "xp": 200,
  "size": "small",
  "type": "humanoid",
  "alignment": "neutral_evil",
  "ac": 17,
  "ac_source": "chain_shirt_shield",
  "hp": { "average": 21, "hit_dice": "6d6" },
  "speed": 30,
  "abilities": {
    "str": 10, "dex": 14, "con": 10,
    "int": 10, "wis": 8, "cha": 10
  },
  "skills": { "stealth": 6 },
  "senses": { "darkvision": 60 },
  "languages": ["common", "goblin"],
  "challenge_rating": 1,
  "proficiency_bonus": 2,
  "traits": [
    {
      "name": "Nimble Escape",
      "description": "可以使用附赠动作进行撤离或躲藏"
    }
  ],
  "reactions": [
    {
      "name": "Redirect Attack",
      "description": "当哥布林首领被攻击命中时，可以选择5尺内的一个哥布林替自己承受这次攻击"
    }
  ],
  "actions": [
    {
      "name": "Multiattack",
      "description": "进行两次弯刀攻击"
    },
    {
      "name": "Scimitar",
      "type": "melee_weapon_attack",
      "attack_bonus": 4,
      "reach": 5,
      "damage": "1d6+2 slashing",
      "damage_average": 5
    },
    {
      "name": "Javelin",
      "type": "ranged_weapon_attack",
      "attack_bonus": 4,
      "range": "30/120",
      "damage": "1d6+2 piercing",
      "damage_average": 5
    }
  ],
  "phases": [
    {
      "phase": 1,
      "name": "指挥作战",
      "hp_threshold_percent": 100,
      "mechanics": [
        { "type": "summon", "template": "goblin", "count": 2, "every_n_rounds": 4 },
        { "type": "commanding_shout", "effect": "all_goblins_attack_bonus_1", "range_tiles": 10 }
      ]
    },
    {
      "phase": 2,
      "name": "困兽之斗",
      "hp_threshold_percent": 50,
      "mechanics": [
        { "type": "desperate_attack", "effect": "two_extra_attacks_per_round" },
        { "type": "flee_attempt", "trigger": "hp_below_25_percent", "dc_to_stop": 15 }
      ],
      "phase_transition": {
        "dialogue_narrative": "哥布林首领发出尖锐的嚎叫，召唤所有剩余的手下！",
        "vfx": "goblin_warcry"
      }
    }
  ],
  "role": "boss",
  "ai_behavior": "boss_commander",
  "theme_tags": ["goblin", "boss", "forest", "cave"]
}
```

#### Skeleton Champion (骷髅勇士) — CR 2

```json
{
  "template_id": "skeleton_champion",
  "name": "骷髅勇士",
  "name_en": "Skeleton Champion",
  "cr": 2,
  "xp": 450,
  "size": "medium",
  "type": "undead",
  "alignment": "lawful_evil",
  "ac": 15,
  "ac_source": "chain_mail",
  "hp": { "average": 32, "hit_dice": "5d8+10" },
  "speed": 30,
  "abilities": {
    "str": 14, "dex": 14, "con": 15,
    "int": 6, "wis": 10, "cha": 8
  },
  "damage_vulnerabilities": ["bludgeoning"],
  "damage_immunities": ["poison"],
  "condition_immunities": ["exhaustion", "poisoned"],
  "senses": { "darkvision": 60 },
  "languages": [],
  "challenge_rating": 2,
  "proficiency_bonus": 2,
  "actions": [
    {
      "name": "Multiattack",
      "description": "进行两次长剑攻击"
    },
    {
      "name": "Longsword",
      "type": "melee_weapon_attack",
      "attack_bonus": 4,
      "reach": 5,
      "damage": "1d8+2 slashing",
      "damage_average": 6
    }
  ],
  "phases": [
    {
      "phase": 1,
      "name": "亡灵守卫",
      "hp_threshold_percent": 100,
      "mechanics": [
        { "type": "undead_aura", "effect": "frightened_dc_12", "range_tiles": 10 }
      ]
    },
    {
      "phase": 2,
      "name": "亡灵狂怒",
      "hp_threshold_percent": 50,
      "mechanics": [
        { "type": "extra_attack", "count": 1 },
        { "type": "necrotic_strike", "damage": "1d6_necrotic", "every_n_rounds": 3 }
      ],
      "phase_transition": {
        "dialogue_narrative": "骷髅勇士的眼窝中燃起暗紫色的火焰，它的动作变得更加迅猛！",
        "vfx": "necromantic_burst"
      }
    }
  ],
  "role": "boss",
  "ai_behavior": "undead_guardian",
  "theme_tags": ["undead", "boss", "dungeon", "crypt"]
}
```

#### Bandit Leader (强盗头目) — CR 2

```json
{
  "template_id": "bandit_leader",
  "name": "强盗头目",
  "name_en": "Bandit Leader",
  "cr": 2,
  "xp": 450,
  "size": "medium",
  "type": "humanoid",
  "alignment": "any_non_lawful",
  "ac": 15,
  "ac_source": "studded_leather_shield",
  "hp": { "average": 52, "hit_dice": "8d8+16" },
  "speed": 30,
  "abilities": {
    "str": 14, "dex": 14, "con": 14,
    "int": 12, "wis": 10, "cha": 14
  },
  "skills": { "athletics": 4, "intimidation": 4 },
  "languages": ["common"],
  "challenge_rating": 2,
  "proficiency_bonus": 2,
  "traits": [
    {
      "name": "Rallying Cry",
      "description": "作为附赠动作，选择30尺内一个友方生物，给予其临时HP等于强盗头目的CHA调整值(2)"
    }
  ],
  "actions": [
    {
      "name": "Multiattack",
      "description": "进行两次弯刀攻击"
    },
    {
      "name": "Scimitar",
      "type": "melee_weapon_attack",
      "attack_bonus": 4,
      "reach": 5,
      "damage": "1d6+2 slashing",
      "damage_average": 5
    }
  ],
  "phases": [
    {
      "phase": 1,
      "name": "指挥若定",
      "hp_threshold_percent": 100,
      "mechanics": [
        { "type": "rallying_cry", "effect": "grant_temp_hp_2_to_ally", "every_n_rounds": 2 },
        { "type": "summon", "template": "bandit", "count": 2, "once_at_start": true }
      ]
    },
    {
      "phase": 2,
      "name": "背水一战",
      "hp_threshold_percent": 40,
      "mechanics": [
        { "type": "reckless_attack", "effect": "attack_advantage_but_attacked_advantage" },
        { "type": "intimidating_shout", "effect": "frightened_dc_13_wis", "range_tiles": 10, "once": true }
      ],
      "phase_transition": {
        "dialogue_narrative": "强盗头目扔掉盾牌，双手握刀，'来吧！老子今天跟你们拼了！'",
        "vfx": "battle_cry"
      }
    }
  ],
  "role": "boss",
  "ai_behavior": "bandit_commander",
  "theme_tags": ["bandit", "boss", "road", "urban"]
}
```

#### Troll (巨魔) — CR 5

```json
{
  "template_id": "troll",
  "name": "巨魔",
  "name_en": "Troll",
  "cr": 5,
  "xp": 1800,
  "size": "large",
  "type": "giant",
  "alignment": "chaotic_evil",
  "ac": 15,
  "ac_source": "natural_armor",
  "hp": { "average": 84, "hit_dice": "8d10+40" },
  "speed": 30,
  "abilities": {
    "str": 18, "dex": 13, "con": 20,
    "int": 7, "wis": 9, "cha": 7
  },
  "skills": { "perception": 2 },
  "senses": { "darkvision": 60 },
  "languages": ["giant"],
  "challenge_rating": 5,
  "proficiency_bonus": 3,
  "traits": [
    {
      "name": "Keen Smell",
      "description": "巨魔在基于嗅觉的感知检定上有优势"
    },
    {
      "name": "Regeneration",
      "description": "巨魔在回合开始时恢复10点HP。如果巨魔受到火焰或酸液伤害，在其下一回合开始前此特性失效。巨魔只有在以火焰或酸液伤害降至0HP时才会死亡。"
    }
  ],
  "actions": [
    {
      "name": "Multiattack",
      "description": "进行三次攻击：两次爪击和一次啮咬"
    },
    {
      "name": "Claw",
      "type": "melee_weapon_attack",
      "attack_bonus": 7,
      "reach": 5,
      "damage": "1d6+4 slashing",
      "damage_average": 7
    },
    {
      "name": "Bite",
      "type": "melee_weapon_attack",
      "attack_bonus": 7,
      "reach": 5,
      "damage": "1d6+4 piercing",
      "damage_average": 7
    }
  ],
  "phases": [
    {
      "phase": 1,
      "name": "狂暴",
      "hp_threshold_percent": 100,
      "mechanics": [
        { "type": "regeneration", "amount": 10, "trigger": "turn_start" },
        { "type": "multiattack", "attacks": ["claw", "claw", "bite"] }
      ]
    },
    {
      "phase": 2,
      "name": "暴怒",
      "hp_threshold_percent": 50,
      "mechanics": [
        { "type": "regeneration", "amount": 10, "trigger": "turn_start" },
        { "type": "rampage", "effect": "bonus_action_move_half_speed_and_bite_after_reducing_to_0" },
        { "type": "throw_boulder", "damage": "3d10_bludgeoning", "range": 60, "every_n_rounds": 3 }
      ],
      "phase_transition": {
        "dialogue_narrative": "巨魔发出震耳欲聋的咆哮，伤口中涌出绿色的酸液——它变得更加狂暴！",
        "vfx": "troll_rage",
        "full_heal": false
      }
    }
  ],
  "role": "boss",
  "ai_behavior": "berserker",
  "theme_tags": ["troll", "boss", "swamp", "forest", "cave"]
}
```

### 6.6 遭遇放置阵型

参照 [map-exploration.md §9.3](./map-exploration.md) 的6种阵型模板：line, arc, ambush_flanks, scatter, rear_ambush, boss_front。

---

## 7. 战利品分布系统

### 7.1 战利品分布规则

> **金币数据源**: 本节仅定义战利品分布。金币的权威计算公式见 §9.5（骰子公式）。§7.1-7.2 的每节点/每CR金币表已移除，以 §9.5 为单一数据源。

| 节点类型 | 战利品等级 | 物品掉落 | 特殊规则 |
|----------|-----------|----------|----------|
| combat (普通) | tier ± 0 | 0-1件 | — |
| elite_combat | tier + 0 | 1件 | 保底1件uncommon |
| boss | tier + 1 | 2-3件 | **保底1件rare+** |
| exploration | tier ± 1 | 0-2件 | 需检定发现 |
| merchant | - | - | 玩家购买 |
| rest | - | - | 无战利品 |
| puzzle | tier + 0 | 1件 | 解谜奖励 |

### 7.2 金币计算

> **已迁移**: 金币的权威计算已整合至 §9.5 骰子公式（按冒险长度和节点类型分表）。§7.2 原"基于CR的金币分布表"已移除以避免与 §9.5 产生数据源矛盾。所有金币生成统一调用 `RollGold(tier, cr_range, node_type)` 函数（定义见 §9.5）。

### 7.3 物品稀有度分布

```
Loot Tier → 稀有度概率分布:

Tier 1 (基础):
  common: 70%, uncommon: 25%, rare: 5%, very_rare: 0%, legendary: 0%

Tier 2 (精良):
  common: 40%, uncommon: 40%, rare: 15%, very_rare: 5%, legendary: 0%

Tier 3 (稀有):
  common: 20%, uncommon: 35%, rare: 30%, very_rare: 10%, legendary: 5%

Tier 4 (史诗):
  common: 10%, uncommon: 20%, rare: 35%, very_rare: 25%, legendary: 10%

Tier 5 (传说):
  common: 5%, uncommon: 10%, rare: 25%, very_rare: 35%, legendary: 25%
```

### 7.4 Boss战利品特殊规则

```
Boss战利品保证:

1. 每个Boss至少掉落1件 rare+ 物品
2. 最终Boss至少掉落1件 very_rare+ 物品
3. Boss掉落物品有50%概率与Boss主题相关(thematic_items)
4. Boss掉落金币为普通遭遇的3-5倍

Boss战利品生成流程:
  Step 1: 从loot_profile.thematic_items中选择1件作为保底掉落
  Step 2: 根据loot_tier + 1的稀有度概率生成1-2件随机物品
  Step 3: 生成金币(3-5倍普通遭遇)
  Step 4: 如果是最终Boss，额外生成1件very_rare+物品
```

### 7.5 战利品放置

```
战利品放置规则:

1. 击败敌人掉落
   → 战斗结束后，战利品显示在敌人死亡位置
   → 玩家点击拾取

2. 容器内
   → 宝箱/箱子/桶中的战利品
   → 需要交互(开锁/打破)才能获取
   → 部分容器有陷阱

3. 隐藏战利品
   → 需要Search检定发现
   → 在exploration节点中常见
   → 被动察觉可发现部分

4. 环境战利品
   → 壁画后的暗格、地板下的密室
   → 需要调查检定+环境线索
```

---

## 8. 冒险状态管理

### 8.1 冒险运行时状态数据模型

```json
{
  "adventure_id": "adv_003_forgotten_corridor",
  "adventure_title": "被遗忘的回廊",
  "tier": "short",
  "status": "in_progress",

  "current_node_id": "dark_corridor",
  "visited_nodes": ["entrance_campsite", "sealed_entrance", "entry_hall", "dark_corridor"],
  "node_visit_order": ["entrance_campsite", "sealed_entrance", "entry_hall", "dark_corridor"],

  "choices_made": [
    {
      "choice_id": "break_seal_force",
      "node_id": "sealed_entrance",
      "character_id": "char_001",
      "outcome": "success",
      "roll_result": 18,
      "dc": 15,
      "timestamp": 1714800000
    }
  ],

  "resources_consumed": {
    "spell_slots_used": {
      "char_003": { "1st": 1, "2nd": 0 }
    },
    "hit_dice_used": 0,
    "items_consumed": ["health_potion"],
    "gold_spent": 0,
    "hp_lost": {
      "char_001": 8,
      "char_002": 0,
      "char_003": 5
    }
  },

  "npc_interactions": {
    "npc_caravan_master": {
      "times_talked": 1,
      "attitude": "neutral",
      "topics_discussed": ["quest_briefing"],
      "secrets_revealed": []
    }
  },

  "quest_objectives": [
    { "id": "obj_find_archaeologists", "status": "in_progress", "description": "找到失踪的考古队" },
    { "id": "obj_investigate_seal", "status": "in_progress", "description": "调查封印的真相" },
    { "id": "obj_defeat_boss", "status": "pending", "description": "击败封印中的存在" }
  ],

  "world_state_changes_pending": [
    {
      "event_id": "we_shadow_seal_broken",
      "change_type": "conditional",
      "on_success": "封印被重新加固",
      "on_failure": "封印破碎，幽影扩散"
    }
  ],

  "flags": {
    "found_library": true,
    "read_journal": true,
    "disrupted_ritual": false
  },

  "adventure_log": {
    "entries": [
      { "timestamp": 1714799900, "type": "node_enter", "node": "entrance_campsite", "summary": "队伍抵达废弃营地" },
      { "timestamp": 1714799950, "type": "choice", "node": "sealed_entrance", "summary": "索林用蛮力砸开封印" },
      { "timestamp": 1714800000, "type": "combat", "node": "dark_corridor", "summary": "石棺伏击战开始" }
    ]
  },

  "start_time": 1714799800,
  "play_time_seconds": 600,
  "save_count": 0
}
```

### 8.2 存档系统

```
存档规则:

短冒险: 无存档（一次性体验，30分钟内完成）
中冒险: 每完成1/3自动存档 + 玩家手动存档
长冒险: 自由存档 + 每个节点自动存档

存档数据:
  adventure_state (完整状态JSON)
  + party_snapshot (角色状态快照)
  + world_state_snapshot (世界状态快照)
  + timestamp

存档存储:
  user://saves/adventure_{id}/save_{slot}.json

加载流程:
  1. 读取存档JSON
  2. 恢复adventure_state
  3. 恢复party_state
  4. 恢复world_state
  5. 重新加载当前房间
  6. 恢复迷雾状态
```

### 8.3 冒险日志

冒险日志用于两个目的：
1. **LLM上下文窗口** — 为DM Agent提供冒险至今的摘要
2. **玩家回顾** — 在冒险结束后展示冒险历程

```gdscript
class AdventureLog:
    var entries: Array[Dictionary] = []
    var max_entries: int = 100

    func add_entry(type: String, data: Dictionary) -> void:
        entries.append({
            "timestamp": Time.get_unix_time_from_system(),
            "type": type,
            "data": data
        })
        if entries.size() > max_entries:
            entries.pop_front()

    func get_summary(max_chars: int = 400) -> String:
        """生成冒险摘要，用于DM Agent上下文"""
        var summary = ""
        for entry in entries.slice(-10):  # 最近10条
            summary += entry.data.get("summary", "") + "。"
        if summary.length() > max_chars:
            summary = summary.substr(0, max_chars) + "..."
        return summary

    func get_full_log() -> Array[Dictionary]:
        """完整日志，用于冒险结算"""
        return entries
```

### 8.4 冒险完成判定

```
冒险完成条件:

胜利条件 (任一满足):
  1. 到达并完成所有ending节点中的victory类型
  2. 击败最终Boss
  3. 完成所有required_to_proceed节点
  4. 满足特定ending的conditions列表

失败条件 (任一满足):
  1. 全队HP降至0（全灭）
  2. 玩家主动选择撤退
  3. 超时（长冒险有时间限制）
  4. 关键NPC死亡且无替代路径

进行中:
  以上条件均未满足
```

---

## 9. DM Agent 实时交互

### 9.1 DM Agent调用时机

| 触发事件 | request_type | 阻塞模式 | 响应时间要求 |
|----------|-------------|---------|------------|
| 进入新房间 | `scene_atmosphere` | 异步(先显示房间) | < 2秒 |
| NPC对话触发 | `npc_dialogue` | 同步(等待响应) | < 3秒 |
| 技能检定完成 | `skill_check_result` | 异步(先显示数值) | < 2秒 |
| 战斗动作结算 | `combat_narration` | 异步(不阻塞UI) | < 2秒 |
| 选择呈现 | `choice_presentation` | 异步(先显示选项) | < 2秒 |
| 事件结果 | `event_outcome` | 异步 | < 2秒 |

### 9.2 上下文注入策略

```
DM Agent上下文构建 (4000 token预算):

┌─────────────────────────────────────────────────────────┐
│ 静态层 (总是注入): ~1800-2400 tokens                      │
│  · System Prompt (~300 tokens)                           │
│  · 冒险蓝图摘要 (~600-1000 tokens)  ← 新增：编剧Agent生成  │
│    · 核心冲突、关键NPC动机、伏笔列表、预期结局             │
│    · 直接注入蓝图段落，非动作日志截断                       │
│  · 冒险存档摘要 (~200-400 tokens)                         │
│    · 由AdventureLog智能摘要生成(层级压缩算法)              │
│  · 当前场景信息 (current_node) (~100 tokens)              │
├─────────────────────────────────────────────────────────┤
│ 动态层 (按需注入): ~300-600 tokens                       │
│  · 最近8个动作历史(~300 tokens)   ← 从5个扩展到8个         │
│  · 活跃NPC信息(~100 tokens)                              │
│  · 队伍状态(~100 tokens)                                 │
│  · 具体动作上下文(~100 tokens)                            │
├─────────────────────────────────────────────────────────┤
│ 预留安全余量: ~400 tokens                                │
└─────────────────────────────────────────────────────────┘

总消耗: 2500-3400 tokens (在4000预算内)
```
DM Agent上下文构建 (2000 token预算):

┌─────────────────────────────────────────┐
│ 静态层 (总是注入): ~600-800 tokens       │
│  · System Prompt (~300 tokens)           │
│  · 冒险摘要 (adventure_summary)          │
│    (~200-400 tokens)                     │
│  · 当前场景信息 (current_node)           │
│    (~100 tokens)                         │
├─────────────────────────────────────────┤
│ 动态层 (按需注入): ~200-450 tokens       │
│  · 最近5个动作历史 (~200 tokens)         │
│  · 活跃NPC信息 (~100 tokens)            │
│  · 队伍状态 (~100 tokens)               │
│  · 具体动作上下文 (~50 tokens)           │
├─────────────────────────────────────────┤
│ 预留安全余量: ~200 tokens                │
└─────────────────────────────────────────┘

总消耗: 1000-1450 tokens (在2000预算内)
```

**上下文压缩规则**:
- 超出8个动作的历史 → 由AdventureLog生成层级摘要（最近5动作全文→6-15动作关键词摘要→16+动作单句摘要）
- NPC超过3个 → 只保留与当前节点直接关联的，保留关键叙事承诺
- 队伍状态 → 传递HP百分比和显著状态效果
- **关键叙事承诺保护**: 摘要算法标记并保留"玩家承诺"类动作（如"答应归还护符"、"发誓击败Boss"），不参与压缩截断

### 9.3 叙事与程序结果合并

```
合并策略:

1. 程序先计算结果
   → 伤害数值、检定成功/失败、物品掉落
   → 结果确定后，将结果传给DM Agent

2. DM Agent生成叙事包装
   → 基于程序结果生成描述文本
   → 不改变任何数值结果

3. UI同时显示
   → 数值结果立即显示（伤害数字、HP变化）
   → 叙事文本异步到达后叠加显示（战斗日志框）

示例流程:
  程序: 索林攻击命中，AC 13 vs 总攻击值 18，造成 8 点伤害
  DM Agent: "索林的战锤重重砸在石棺上，碎石飞溅！"
  UI: 显示 "-8 HP" 动画 + 战斗日志文本
```

### 9.4 离线降级

当DM Agent不可用时，使用静态叙事模板：

```json
{
  "fallback_templates": {
    "scene_atmosphere": {
      "dungeon": "阴暗的走廊中弥漫着潮湿的空气。火把的光芒在墙壁上投下摇曳的影子。",
      "forest": "阳光透过茂密的树冠洒下斑驳的光影。空气中弥漫着泥土和树叶的气息。",
      "cave": "洞穴深处回荡着水滴的声音。黑暗中偶尔传来不明生物的低语。"
    },
    "combat_narration": {
      "hit": "{atturer}的{weapon}击中了{target}！",
      "miss": "{attacker}的{weapon}未能穿透{target}的防御。",
      "critical": "致命一击！{attacker}的{weapon}精准命中要害！",
      "kill": "{target}倒下了。"
    },
    "skill_check_result": {
      "success": "{character}成功完成了检定。",
      "failure": "{character}未能通过检定。",
      "critical_success": "完美的表现！{character}轻松完成了挑战。",
      "critical_failure": "灾难性的失败！{character}的尝试适得其反。"
    }
  }
}
```

---

## 10. 世界状态挂钩

### 10.1 世界状态数据模型

> **权威定义**: 世界状态的完整 JSON Schema 定义见 [failure-growth.md §9.1](../subsystems/08-failure-growth.md)。本节仅列出与本系统直接相关的关键字段说明，不应独立定义数据模型。

本系统通过 `world_state_hooks` 机制消费和变更世界状态。关键交互字段：

```
核心变更路径:
  region_states.status → adventure 成功/失败时通过 hooks 写入
  region_states.threat_level → 影响未来冒险难度
  active_factions.relation_value → 势力关系变化
  world_events → 新事件触发
  global_flags → 全局标记设置
```

> **重要**: `region_states.status` 的合法值（safe/threatened/fallen/liberated/destroyed）、`active_factions` 的结构、`world_events` 的 Schema 等，均以 failure-growth.md §9.1 为准。本节中的示例代码仅供理解 hooks 机制，不作为数据契约。

### 10.2 世界状态变更流程

```
冒险结算时的世界状态变更:

Step 1: 收集冒险中的world_state_hooks
  hooks = blueprint.world_state_hooks

Step 2: 根据冒险结果确定变更
  for hook in hooks:
    if adventure_outcome == "success":
      change = hook.on_success
    elif adventure_outcome == "failure":
      change = hook.on_failure

Step 3: 应用变更到世界状态
  for change in changes:
    if change.type == "region_status":
      world_state.region_states[change.region].status = change.new_status
    elif change.type == "faction_relation":
      world_state.active_factions[change.faction].relation_value += change.delta
    elif change.type == "global_flag":
      world_state.global_flags[change.flag] = change.value
    elif change.type == "new_event":
      world_state.world_events.append(change.event)
    elif change.type == "new_location":
      world_state.region_states[change.region].known_locations.append(change.location)

Step 4: 记录变更历史
  world_state.completed_adventures.append({
    "adventure_id": blueprint.id,
    "outcome": adventure_outcome,
    "world_changes_applied": changes.map(func(c): return c.description)
  })
```

### 10.3 世界状态对冒险生成的影响

```
世界状态 → 编剧Agent输入:

1. 区域威胁等级
   threat_level高的区域 → 冒险难度自动+1
   threat_level低的区域 → 冒险难度自动-1

2. 势力关系
   hostile势力 → 冒险中可能出现该势力敌人
   friendly势力 → 冒险中可能出现该势力盟友

3. 已完成冒险
   避免重复主题(avoid_themes)
   已知地点可作为冒险背景

4. 世界事件
   活跃事件 → 强制主题(forced_theme)
   事件影响区域 → 冒险区域选择

5. 全局标记
   特定标记 → 解锁/锁定冒险类型
   例: "ancient_temple_discovered" → 解锁古庙主题冒险
```

### 10.4 世界状态演化示例

| 冒险事件 | 成功后果 | 失败后果 |
|----------|----------|----------|
| 拯救城镇 | 该城镇开放为新酒馆分部，声望+5 | 该城镇变为废墟，出现新的亡灵任务 |
| 击败Boss | 该区域安全，新NPC出现，威胁等级-1 | Boss势力扩张，后续任务更难，威胁等级+2 |
| 获得神器 | 神器加入轮回池，全局标记设置 | 神器落入敌手，成为未来Boss装备 |
| 惹怒势力 | 该势力成为敌人，关系-3 | 该势力追杀，随机遭遇增加 |
| 发现秘密 | 新任务线解锁，新地点发现 | 秘密被其他势力获取，出现竞争任务 |

---

## 11. 冒险结算流程

> **结算管线权威源**: 2026-05-09 裁决——`08-failure-growth.md` 拥有结算管线的**单一权威**。本章仅定义"触发条件"和"冒险生成系统的职责范围"，所有 XP/战利品/关系/声望/世界状态/LLM叙事的计算逻辑由 failure-growth §§2-3 统一处理。

### 11.1 冒险完成触发

冒险生成系统的职责：检测冒险完成条件 → 发出 `adventure_completed` 信号 → failure-growth 接管结算。

触发条件:
  - Boss 被击败 AND 最终目标达成
  - 全队撤退（部分失败）
  - 全队死亡（灾难性失败）

发出信号后，以下事项委托 failure-growth:
  - XP 计算与分配 → `08-failure-growth.md` §2.2
  - 战利品生成 → `08-failure-growth.md` §2 Step 3
  - 关系更新 → `08-failure-growth.md` §2 Step 4
  - 声望变化 → `08-failure-growth.md` §2 Step 5
  - 世界状态变更 → `08-failure-growth.md` §2 Step 6
  - LLM 叙事生成 → `08-failure-growth.md` §2 Step 8

**金币消耗（经济槽）**: 冒险结算管线必须包含强制性金币消耗检查:
  - 补给消耗: 每节点 5 GP × party_size（口粮/弹药/住宿）→ 短冒险约 35-40 GP/人
  - 装备耐久退化: 参照 `03-items-equipment.md` §6（Phase 2 完整实现，MVP 跳过）
  - 鉴定费用: 参照 `03-items-equipment.md` §5A.2（Uncommon 25 GP, Rare 50 GP, Very Rare 100 GP, Legendary 250 GP）
  - 酒馆设施维护: 参照 `07-tavern-system.md`（每冒险固定维护费）

冒险生成系统保留的职责：
  - 提供冒险上下文（主题、难度、参与角色、关键事件）给结算管线
  - 记录冒险日志到世界状态

> **已废弃内容**: 以下§11.2-§11.3（失败结算管线、伤疤系统）已迁移至 `08-failure-growth.md` §§3-4 作为单一权威源。此处仅保留触发条件定义。

### 11.2 失败触发

冒险失败由以下条件触发，具体结算委托 failure-growth §3:
  - 轻微: 部分队员倒地但最终逃脱
  - 中等: 全队败退但无人死亡
  - 严重: 有角色永久死亡
  - 灾难性: 全灭

### 11.3 伤疤系统

伤疤的生成、效果、叙事由 `08-failure-growth.md` §4 和 `01-character-system.md` §7 共同定义。

---

---

## 12. 冒险难度缩放

### 12.1 难度缩放公式

```
基于party_level的难度缩放:

1. 敌人HP缩放
   scaled_hp = base_hp × (1 + (party_level - recommended_level) × 0.15)
   最小缩放: 0.5x, 最大缩放: 2.0x

2. 敌人伤害缩放
   scaled_damage = base_damage × (1 + (party_level - recommended_level) × 0.10)
   最小缩放: 0.7x, 最大缩放: 1.5x

3. 豁免DC缩放
   scaled_dc = base_dc + floor((party_level - recommended_level) × 0.5)
   最小DC: 8, 最大DC: 25

4. 战利品质量缩放（反刷机制）
   loot_tier_penalty = floor((party_level - recommended_level) / 3)
   effective_loot_tier = max(1, base_loot_tier - loot_tier_penalty)
   (队伍等级过高时降级战利品——防止刷低级冒险获取高级装备)

5. XP奖励缩放（反刷机制）
   xp_multiplier = max(0.1, 1.0 - (party_level - recommended_level) × 0.05)
   (等级越高，同难度冒险的XP越少——鼓励挑战更高难度)
```

### 12.2 难度自适应设计（已移除）

> **⚠️ 设计评审裁决 (2026-05-10)**: 原§12.2"动态难度调整"(隐藏Director系统暗中修改遭遇数据)因以下原因被移除:
> 1. 违背Pillar 2"战术深度——原汁原味DND"——暗中帮/罚玩家破坏 mastery 承诺
> 2. 阈值阶梯函数(80% HP边界)引发博弈行为
> 3. 与Roguelike DND的"诚实"传统相悖
>
> **替代方案**: 通过预设计遭遇难度曲线 + 可选难度节点（如分支选择"正面突袭"vs"潜行绕路"）让玩家自我调节挑战水平。所有遭遇难度由程序在冒险开始时确定性计算，不随进度暗中变化。

### 12.3 难度配置文件

```json
{
  "difficulty_profile_id": "dp_lv3_short_normal",
  "recommended_level": 3,
  "party_size": 4,
  "tier": "short",
  "modifier": 0,
  "cr_range": { "min": 0.5, "max": 4 },
  "encounter_count": { "easy": 1, "medium": 1, "hard": 1, "deadly": 0 },
  "total_encounters": 3,
  "loot_tier": 2,
  "rest_opportunities": 1,
  "short_rest_opportunities": 1,
  "trap_density": "low",
  "puzzle_complexity": "none",
  "xp_budget": {
    "total": 1200,
    "per_encounter_average": 400
  }
}
```

---

## 13. 离线冒险模板

### 13.1 模板结构

离线模板与LLM生成的蓝图格式完全相同（`adventure_blueprint.json` Schema），但内容是静态的。当LLM不可用时，系统从模板库中选择匹配的模板。

### 13.2 模板分类

> **MVP范围**: 仅5个短冒险模板（覆盖5类主题各1个）。中/长冒险无离线模板——仅LLM生成。Phase 2+ 扩展到50+模板（含中/长冒险）。

| 分类 | 主题 | MVP短冒险 | Phase 2+ 短 | Phase 2+ 中 | Phase 2+ 长 |
|------|------|:---:|:---:|:---:|:---:|
| 地牢探索 | dungeon, crypt, cave | 1 | 5 | 3 | 1 |
| 荒野冒险 | forest, swamp, mountain | 1 | 4 | 3 | 1 |
| 城镇阴谋 | urban, political, intrigue | 1 | 3 | 3 | 1 |
| 救援任务 | rescue, escort, defense | 1 | 3 | 2 | 1 |
| 盗窃行动 | heist, stealth, infiltration | 1 | 2 | 2 | 1 |
| Boss挑战 | boss_rush, arena, challenge | — | 3 | 1 | 0 |
| **合计** | | **5** | **20** | **14** | **5** |

### 13.3 模板选择算法

```
Algorithm: SelectOfflineTemplate(tier, party_level, theme_preference)
──────────────────────────────────────────────────────────────────────
输入:
  tier: "short" / "medium" / "long"
  party_level: int
  theme_preference: String (可选)

输出:
  template: AdventureBlueprint (静态)

Step 1: 筛选匹配tier的模板
  candidates = TemplateDB.query({ "tier": tier })

Step 2: 按主题偏好过滤
  if theme_preference:
    themed = candidates.filter(func(t): return theme_preference in t.theme_tags)
    if not themed.is_empty():
      candidates = themed

Step 3: 按等级匹配度排序
  candidates.sort(func(a, b):
    return abs(a.difficulty_profile.recommended_level - party_level) <
           abs(b.difficulty_profile.recommended_level - party_level)
  )

Step 4: 随机选择前3个中的1个 (避免每次都选同一个)
  top_3 = candidates.slice(0, min(3, candidates.size()))
  template = top_3[randi() % top_3.size()]

Step 5: 根据party_level微调难度
  template = adjust_template_difficulty(template, party_level)

Step 6: 返回
  return template
```

### 13.4 模板与LLM生成的区别

| 维度 | LLM生成 | 离线模板 |
|------|---------|---------|
| 叙事多样性 | 每次不同 | 固定文本 |
| NPC性格 | 动态生成 | 预设对话 |
| 分支选择 | 动态生成 | 固定选项 |
| 机械完整性 | 相同 | 相同 |
| 战斗平衡 | 相同 | 相同 |
| 地图结构 | 相同 | 相同 |
| 玩家体验 | "活的" | "可重复的" |

---

## 14. 验收标准 (Acceptance Criteria)

> **格式**: GIVEN/WHEN/THEN。所有AC必须由QA测试人员独立验证，无需阅读实现代码。

### AC-BP: 蓝图验证

**AC-BP-01: 有效短冒险蓝图通过验证**
- GIVEN 一个符合Schema且所有节点引用有效的短冒险蓝图(7-8个节点)
- WHEN 蓝图通过 BlueprintBusinessValidator 验证
- THEN 验证结果为通过(is_valid=true)，无错误(errors为空)，最多有警告

**AC-BP-02: 节点数不足时验证失败**
- GIVEN 一个仅含3个节点的短冒险蓝图
- WHEN 蓝图通过验证
- THEN 验证失败，错误列表包含 NODE_COUNT 错误码

**AC-BP-03: 孤立节点被检测**
- GIVEN 蓝图中存在一个不与任何其他节点连接的节点
- WHEN BFS可达性检查执行
- THEN 验证结果包含 ORPHAN_NODE 错误码

**AC-BP-04: 战斗节点缺少encounter_id时验证失败**
- GIVEN 蓝图中存在 type="combat" 但无 encounter_id 字段的节点
- WHEN 业务逻辑验证规则8执行
- THEN 验证结果包含 MISSING_ENCOUNTER 错误码

### AC-CR: CR预算计算

**AC-CR-01: Lv3×4人 Medium遭遇CR预算正确**
- GIVEN party_level=3, party_size=4, difficulty="medium"
- WHEN calculate_cr_budget() 执行
- THEN 返回的CR预算在 2.5-3.5 范围内

**AC-CR-02: 非4人队伍缩放正确**
- GIVEN party_level=3, party_size=3, difficulty="medium"
- WHEN calculate_cr_budget() 执行
- THEN 返回的CR预算 ≈ 3.0 × (3/4) = 2.25（容忍±0.5）

### AC-LOOT: 战利品生成

**AC-LOOT-01: Boss保证至少1件rare+物品**
- GIVEN 任意Boss节点，任意战利品等级
- WHEN generate_boss_loot() 执行100次
- THEN 每次执行结果中，稀有度 ≥ Rare 的物品数量 ≥ 1

**AC-LOOT-02: 战利品稀有度分布符合Tier概率**
- GIVEN tier=2, 固定随机种子, 1000次运行
- WHEN generate_random_loot(tier=2) 执行
- THEN common概率在35%-45%, uncommon在35%-45%, rare在10%-20%, very_rare在3%-7%

### AC-OFFLINE: 离线降级

**AC-OFFLINE-01: LLM Schema验证3次失败后降级到离线模板**
- GIVEN LLM API返回3次Schema验证失败的响应
- WHEN 冒险蓝图生成管线执行
- THEN 系统选择匹配tier和party_level的离线模板，成功生成冒险实例

**AC-OFFLINE-02: 离线模板选择的冒险机械完整性不变**
- GIVEN 离线模板被选中
- WHEN 冒险实例化完成
- THEN 节点数、节点类型分布、Boss位置、CR预算范围与同等级LLM生成冒险一致

### AC-DDA: 难度系统（已移除动态调整）

**AC-DDA-01: 难度由冒险开始时确定，不随进度变化**
- GIVEN 任意冒险实例
- WHEN 玩家从第1个节点推进到第N个节点
- THEN 所有未触发遭遇的CR预算和敌人配置与冒险实例化时完全一致（无暗中修改）

### AC-VF: 胜利/失败检测

**AC-VF-01: 击败最终Boss触发胜利**
- GIVEN 冒险状态为 in_progress，current_node 为Boss节点
- WHEN Boss被击败且所有 required_to_proceed 节点已完成
- THEN 冒险status变为 completed，outcome为 victory

**AC-VF-02: 全队HP降至0触发失败**
- GIVEN 冒险状态为 in_progress
- WHEN 所有队伍成员HP降至0
- THEN 冒险status变为 failed，outcome为 total_party_kill

### AC-DM: DM Agent实时交互

**AC-DM-01: DM Agent不可用时降级到静态模板**
- GIVEN DM Agent API调用在1.5秒内未响应
- WHEN 进入新房间触发 scene_atmosphere 调用
- THEN UI显示 §9.4 中对应环境的静态后备模板文本

**AC-DM-02: 上下文包含蓝图摘要信息**
- GIVEN DM Agent被调用
- WHEN 上下文构建完成
- THEN 上下文包含: 核心冲突描述、关键NPC名称和动机、当前冒险章节信息（非仅动作日志截断）

---

## 15. 测试规格

> **⚠️ 语言迁移 (2026-05-10)**: 以下测试代码为 GDScript 伪代码（算法规范）。在实现前必须翻译为 C# xUnit + FluentAssertions 格式（项目测试框架见 AGENTS.md）。固定种子消除所有非确定性测试。测试文件路径: `tests/unit/Adventure/`。

### 15.1 单元测试

#### 蓝图验证测试

```gdscript
# tests/adventure/test_blueprint_parser.gd

func test_valid_short_blueprint_passes():
    var blueprint = load_test_blueprint("valid_short_01")
    var result = parser.validate(blueprint)
    assert_true(result.is_valid)
    assert_eq(result.errors.size(), 0)

func test_invalid_node_count_fails():
    var blueprint = load_test_blueprint("valid_short_01")
    blueprint.plot_outline.nodes = blueprint.plot_outline.nodes.slice(0, 3)  # 只留3个节点
    var result = parser.validate(blueprint)
    assert_false(result.is_valid)
    assert_true(result.errors.any(func(e): return e.code == "NODE_COUNT"))

func test_orphan_node_detected():
    var blueprint = load_test_blueprint("valid_short_01")
    # 添加一个没有连接的孤立节点
    blueprint.plot_outline.nodes.append({
        "node_id": "orphan_node",
        "type": "combat",
        "description": "孤立节点",
        "order_index": 99,
        "connections": []
    })
    var result = parser.validate(blueprint)
    assert_true(result.errors.any(func(e): return e.code == "ORPHAN_NODE"))

func test_invalid_node_reference_detected():
    var blueprint = load_test_blueprint("valid_short_01")
    blueprint.plot_outline.nodes[0].connections[0].target_node_id = "nonexistent_node"
    var result = parser.validate(blueprint)
    assert_true(result.errors.any(func(e): return e.code == "INVALID_REF"))

func test_missing_encounter_id_on_combat_node():
    var blueprint = load_test_blueprint("valid_short_01")
    # 找到一个combat节点并删除encounter_id
    for node in blueprint.plot_outline.nodes:
        if node.type == "combat":
            node.erase("encounter_id")
            break
    var result = parser.validate(blueprint)
    assert_true(result.errors.any(func(e): return e.code == "MISSING_ENCOUNTER"))
```

#### CR预算计算测试

```gdscript
# tests/adventure/test_cr_budget_calculator.gd

func test_lv3_party_medium_encounter():
    var budget = calculator.calculate_cr_budget(3, 4, "medium")
    assert_almost_eq(budget, 3.0, 0.5)

func test_lv5_party_deadly_encounter():
    var budget = calculator.calculate_cr_budget(5, 4, "deadly")
    assert_almost_eq(budget, 10.0, 1.0)

func test_lv1_party_easy_encounter():
    var budget = calculator.calculate_cr_budget(1, 4, "easy")
    assert_almost_eq(budget, 0.5, 0.25)

func test_solo_lv3_hard_encounter():
    var budget = calculator.calculate_cr_budget(3, 1, "hard")
    assert_almost_eq(budget, 1.5, 0.5)
```

#### 战利品分布测试

```gdscript
# tests/adventure/test_loot_generator.gd

func test_boss_guaranteed_rare_drop():
    var loot = generator.generate_boss_loot(2, "goblin_boss")
    var rare_items = loot.items.filter(func(i): return i.rarity in ["rare", "very_rare", "legendary"])
    assert_true(rare_items.size() >= 1)

func test_loot_tier_distribution_100_runs():
    var distribution = {"common": 0, "uncommon": 0, "rare": 0, "very_rare": 0, "legendary": 0}
    for i in range(100):
        var loot = generator.generate_random_loot(2)
        for item in loot.items:
            distribution[item.rarity] += 1
    # Tier 2: common ~40%, uncommon ~40%, rare ~15%, very_rare ~5%
    assert_true(distribution.common > 25 and distribution.common < 55)
    assert_true(distribution.uncommon > 25 and distribution.uncommon < 55)

func test_gold_range_per_cr():
    var gold_cr0 = generator.calculate_gold_drop(0.25)
    var gold_cr5 = generator.calculate_gold_drop(5)
    assert_true(gold_cr5 > gold_cr0 * 3)
```

#### 节点连通性测试

```gdscript
# tests/adventure/test_node_connectivity.gd

func test_all_nodes_reachable_from_start():
    var blueprint = load_test_blueprint("valid_medium_01")
    var graph = parser.parse(blueprint)
    var start = _find_start(graph)
    var reachable = _bfs(start, graph)
    for node_id in graph:
        assert_true(node_id in reachable, "节点 %s 不可从起始节点到达" % node_id)

func test_boss_node_is_last():
    var blueprint = load_test_blueprint("valid_short_01")
    var nodes = blueprint.plot_outline.nodes
    var boss_nodes = nodes.filter(func(n): return n.type == "boss")
    for boss in boss_nodes:
        assert_true(boss.order_index >= nodes.size() * 0.7,
            "Boss节点应在最后30%位置")
```

#### 选择解析测试

```gdscript
# tests/adventure/test_choice_resolver.gd

func test_skill_check_success():
    var choice = {"skill_check": {"ability": "str", "dc": 10}}
    var result = resolver.resolve_choice(choice, mock_state, mock_party)
    # STR 16 (+3) + d20 >= 10 → 应该经常成功
    assert_true(result.success_rate > 0.6)

func test_item_requirement_blocks_choice():
    var choice = {"conditions": {"item_requirement": "ancient_key"}}
    var party_without_key = mock_party_no_item("ancient_key")
    var result = resolver.resolve_choice(choice, mock_state, party_without_key)
    assert_false(result.success)

func test_relationship_threshold_check():
    var choice = {"conditions": {"relationship_threshold": 5}}
    var state_with_low_rel = mock_state_with_relationship(3)
    var result = resolver.resolve_choice(choice, state_with_low_rel, mock_party)
    assert_false(result.success)
```

### 15.2 集成测试

```gdscript
# tests/adventure/test_full_pipeline.gd

func test_full_adventure_generation_pipeline():
    """完整管线: LLM输出 → 蓝图 → 地图 → 可玩冒险"""
    # 1. 加载测试蓝图(模拟LLM输出)
    var blueprint = load_test_blueprint("integration_test_01")

    # 2. 验证蓝图
    var validation = parser.validate(blueprint)
    assert_true(validation.is_valid)

    # 3. 实例化
    var instance = await instantiator.instantiate(blueprint, mock_party_state)
    assert_not_null(instance)
    assert_true(instance.node_graph.size() > 0)

    # 4. 验证所有节点都有房间模板
    for node_id in instance.node_graph:
        assert_not_null(instance.node_graph[node_id].room_template)

    # 5. 验证所有战斗节点都有遭遇数据
    for node_id in instance.node_graph:
        var node = instance.node_graph[node_id]
        if node.type in ["combat", "elite_combat", "boss"]:
            assert_not_null(node.encounter_data)

    # 6. 验证节点图连通性
    var start = _find_start(instance.node_graph)
    var reachable = _bfs(start, instance.node_graph)
    assert_eq(reachable.size(), instance.node_graph.size())
```

### 15.3 边缘情况测试

| 测试场景 | 预期行为 |
|----------|----------|
| 蓝图无战斗节点 | 验证通过，但警告"无战斗遭遇" |
| 蓝图全是战斗节点 | 验证通过，但警告"无叙事节点" |
| 孤立节点 | 验证失败，错误码ORPHAN_NODE |
| 不可能的遭遇(CR过高) | 平衡Agent标记为rejected |
| 缺失NPC引用 | 使用默认NPC + 警告 |
| 空节点数组 | 验证失败，错误码NODE_COUNT |
| Boss不在最后位置 | 警告BOSS_PLACEMENT |
| 分支指向不存在的节点 | 验证失败，错误码INVALID_BRANCH |
| 循环引用(A→B→A) | 验证通过(允许回环)，但警告 |
| 所有选择都失败 | 保底路径(强制前进) |

### 15.4 平衡验证

```
平衡验证测试 (100次种子运行):

1. CR预算分布
   · 每个难度等级的CR预算在预期范围内
   · 无单个遭遇CR超过安全阈值(队伍等级×人数×1.8)

2. 战利品分布
   · 100次运行的平均稀有度分布符合tier预期
   · Boss保底rare+物品100%触发
   · 金币总量在预期范围内

3. XP曲线
   · 短冒险完成后，Lv1角色应接近Lv2(300 XP)
   · 中冒险完成后，Lv3角色应接近Lv4(1200 XP)
   · 无XP通胀(单次冒险XP不超过下一级需求的200%)

4. 难度曲线
   · 前3个节点无deadly遭遇
   · Boss节点难度≥hard
   · 连续2个hard+遭遇之间有rest节点
```

---

*文档版本: v1.3*
*创建日期: 2026-05-04*
*修订日期: 2026-05-10 — /design-review 修订（12个阻断项已解决）*
*状态: 设计评审修订完成 — 见 reviews/06-adventure-generation-review-log.md*
*下一步: /story-readiness → /dev-story*
