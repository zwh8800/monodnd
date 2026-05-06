# 酒馆系统 — 技术设计文档

> **Subsystem**: Tavern System (酒馆系统)
> **Game**: 《酒馆与命运》(Tavern & Destiny)
> **Rules Reference**: DND 5e SRD
> **Language Policy**: 游戏文本统一采用简体中文，技术标识符使用英文snake_case
> **Version**: 1.0 — MVP + Full Game scope
> **Status**: 初始设计
> **References**: GDD-v1.md §3, Character System doc, Items & Equipment doc, LLM Integration doc

---

## 1. 概述

### 1.1 系统目的

酒馆系统是《酒馆与命运》的**元游戏层核心**——它是冒险之间的"生活空间"，不只是菜单界面，而是一个**活的空间**。玩家在这里招募角色、管理装备、接取任务、体验角色之间的故事。

参考坐标: Darkest Dungeon's Hamlet (元进度+风险管理) × BG3's Camp (角色关系+叙事事件)。

### 1.2 设计原则

| 原则 | 说明 |
|------|------|
| **活的空间** | 角色在酒馆中有可见的闲逛、对话、争吵——不是静态菜单，NPC有日常行为 |
| **程序控制骨骼，LLM绘制皮肤** | 所有数值计算、条件判定、事件触发由程序控制；LLM只生成叙事文本 |
| **渐进解锁** | 酒馆设施、功能区域随声望等级逐步解锁，形成长线目标 |
| **风险有后果** | 角色可能死亡、装备可能损坏、关系可能破裂——酒馆是处理后果的地方 |
| **永久世界演进** | 冒险的成功/失败改变世界状态，酒馆反映这些变化 |

### 1.3 与其他子系统的关系

```
                    ┌──────────────────────────────────────┐
                    │         Tavern System (本系统)         │
                    │  ──────────────────────────────────── │
                    │  · 酒馆升级 (Level 1-10)               │
                    │  · 角色招募 (Recruitment)              │
                    │  · NPC调度 (NPC Scheduling)            │
                    │  · 商店 (Shops)                        │
                    │  · 酒馆事件 (Tavern Events)            │
                    │  · 关系事件 (Relationship Events)      │
                    │  · 对话系统 (Dialogue)                 │
                    │  · 训练系统 (Training)                 │
                    │  · 任务板 (Quest Board)                │
                    │  · 休息恢复 (Rest & Recovery)          │
                    │  · 角色管理 (Roster Management)        │
                    └──┬───────┬───────┬───────┬────────────┘
                       │       │       │       │
         ┌─────────────┴──┐ ┌──┴───────┴──┐ ┌──┴─────────────┐
         │ Character      │ │ Items &     │ │ LLM Gateway    │
         │ System         │ │ Equipment   │ │                │
         │ ────────────── │ │ ─────────── │ │ ────────────── │
         │ · 角色数据模型  │ │ · 武器/护甲  │ │ · 编剧Agent     │
         │ · 关系系统      │ │ · 药水/卷轴  │ │ · DM Agent      │
         │ · 升级系统      │ │ · 定价/交易  │ │ · 文案Agent     │
         │ · 伤疤/传承     │ │ · 制作配方   │ │ · Schema验证    │
         └────────┬───────┘ └──────┬───────┘ └────────┬────────┘
                  │                │                   │
          ┌───────┴────────────────┴───────────────────┴──────┐
          │              Adventure Generation                 │
          │  ────────────────────────────────────────────────  │
          │  · 冒险蓝图 (AdventureBlueprint)                   │
          │  · 地图生成 (Node Graph)                           │
          │  · 战斗系统 (Combat System)                        │
          └───────────────────────────────────────────────────┘
```

### 1.4 MVP vs Full Game 范围

| 功能 | MVP (Phase 1) | Full Game |
|------|:---:|:---:|
| 酒馆升级 (Lv1-10) | ❌ (Lv1 fixed) | ✅ |
| 区域解锁 (铁匠/炼金/图书馆/神殿/英雄之壁) | ❌ | ✅ |
| 招募系统 (基础版) | ✅ (9个预设) | ✅ (全管线) |
| NPC调度系统 | ❌ | ✅ |
| 商店系统 (基础版) | ✅ (杂货商) | ✅ (全商店) |
| 酒馆事件系统 | ❌ | ✅ |
| 关系事件系统 | ❌ | ✅ |
| 对话系统架构 | ❌ | ✅ |
| 训练系统 | ❌ | ✅ |
| 任务板系统 | ✅ (基础版) | ✅ (完整版) |
| 休息与恢复机制 | ✅ (基础版) | ✅ (完整版) |
| 角色管理与界面 | ✅ | ✅ |

---

## 2. 酒馆升级系统

### 2.1 酒馆等级总表 (Level 1-10)

酒馆等级是游戏的**核心元进度**。酒馆XP (Tavern XP) 通过完成冒险累积，解锁新设施与功能。声望系统独立运作（详见 §2.3）。

> **数据权威来源**: 本表与 [failure-growth.md §10.2](../subsystems/08-failure-growth.md) 保持同步。酒馆XP数值以 failure-growth.md 为准。

| 等级 | 名称 | 所需酒馆XP | 累计酒馆XP | 升级成本(Gold) | 解锁区域 | 解锁功能 | 视觉变化 |
|:----:|------|:------:|:---------:|:------------:|----------|----------|----------|
| Lv1 | 破旧小屋 | 0 | 0 | 0 | 大厅 (Hall) | 基础招募、基础任务板、基础休息 | 小木屋，吧台一张，壁炉破损 |
| Lv2 | 路边客栈 | 500 | 500 | 300 | 铁匠铺 (Blacksmith) | 装备修复、基础武器/护甲打造、基础商店 | 木质招牌挂起，铁砧搬入后院 |
| Lv3 | 繁荣旅店 | 1,200 | 1,700 | 1,200 | 炼金台 (Alchemist) | 药水/卷轴制作、炼金材料商店 | 二楼加盖，壁炉翻新，吧台扩建 |
| Lv4 | 知名驿站 | 2,500 | 4,200 | 3,000 | — | 中冒险解锁、任务板扩容、角色槽位+1 | 石墙加固，大厅悬挂冒险者战利品 |
| Lv5 | 冒险者大厅 | 4,500 | 8,700 | 9,000 | 图书馆 (Library) | 法术卷轴、技能训练、新职业解锁、专长学习 | 地下室扩建为图书室，书架装满古籍 |
| Lv6 | 英雄驿站 | 7,500 | 16,200 | 15,000 | — | 角色关系系统深化、酒馆事件系统启用、角色槽位+1 | 花园扩建，NPC休闲区建立 |
| Lv7 | 圣殿驿站 | 12,000 | 28,200 | 24,000 | 神殿 (Temple) | 角色复活、诅咒移除、祝福buff | 侧翼圣殿建成，彩绘玻璃窗 |
| Lv8 | 传说枢纽 | 18,000 | 46,200 | 38,000 | — | 长冒险解锁、传奇任务线开启、角色槽位+1 | 英雄之壁揭开，前代冒险者雕塑 |
| Lv9 | 命运交汇 | 26,000 | 72,200 | 60,000 | — | 双任务同时发布、英雄传记完整功能、Prestige系统 | 整栋建筑扩建为三层石堡，魔法灯笼悬浮 |
| Lv10 | 酒馆与命运 | 36,000 | 108,200 | 100,000 | 英雄之壁完整功能 | 传说装备解锁、全功能巅峰、传奇冒险、角色槽位满 | 金碧辉煌的大厅，壁炉上方悬挂历代英雄画像 |

### 2.2 酒馆XP获取规则

酒馆XP来源于冒险完成和特殊成就。完整规则见 [failure-growth.md §10.2](../subsystems/08-failure-growth.md)。

| 来源 | 酒馆XP | 说明 |
|------|:------:|------|
| 完成短冒险 | 100 XP | 基础值 |
| 完成中冒险 | 300 XP | 基础值 |
| 完成长冒险 | 600 XP | 基础值 |
| 金币投资 | 50 XP/100 gp | 投资到酒馆升级 |
| 首次完成某主题冒险 | 200 XP | 每主题一次 |
| 角色达到Lv5 | 150 XP | 每角色一次 |
| 角色达到Lv10 | 300 XP | 每角色一次 |

### 2.3 声望系统 (Reputation)

声望是独立于酒馆等级的**0-100分制**社交系统，影响商店折扣、冒险解锁、NPC态度等。完整规则见 [failure-growth.md §10.1](../subsystems/08-failure-growth.md)。

| 声望范围 | 等级 | 解锁内容 |
|:--------:|------|----------|
| 0-19 | 新手 (Novice) | 基础招募、短冒险 |
| 20-39 | 知名 (Known) | 中冒险解锁、铁匠铺 |
| 40-59 | 受尊敬 (Respected) | 图书馆、更好商人 |
| 60-79 | 著名 (Famous) | 长冒险解锁、更好战利品 |
| 80-100 | 传说 (Legendary) | 传奇任务、传说装备 |

### 2.3 区域数据模型

每个酒馆区域 (Area) 是酒馆内一个可交互的功能空间。

```json
{
  "area_id": "area_blacksmith",
  "name": "铁匠铺",
  "name_en": "Blacksmith",
  "description": "铁锤的敲击声从后院传来。老铁匠格里姆正在为下一批冒险者打造装备。",
  "unlock_tavern_level": 2,
  "unlock_cost_gold": 500,
  "unlock_materials": {
    "material_iron_ingot": 10,
    "material_wood_plank": 20
  },
  "unlock_quest_required": false,
  "unlock_quest_id": null,
  "facilities": [
    {
      "facility_id": "fac_blacksmith_repair",
      "name": "装备修复",
      "description": "修复损坏的武器和护甲",
      "menu_actions": ["repair", "view_repair_cost"],
      "requires_npc": "npc_blacksmith_grim",
      "npc_present_by_default": true
    },
    {
      "facility_id": "fac_blacksmith_craft",
      "name": "装备打造",
      "description": "使用材料和金币打造武器、护甲、盾牌",
      "menu_actions": ["browse_recipes", "craft", "view_materials"],
      "requires_npc": "npc_blacksmith_grim",
      "recipe_pool_id": "blacksmith_recipes"
    },
    {
      "facility_id": "fac_blacksmith_upgrade",
      "name": "装备升级",
      "description": "为普通装备添加附魔槽 (common→uncommon)",
      "menu_actions": ["select_item", "upgrade", "view_cost"],
      "unlock_tavern_level": 5,
      "upgrade_dc": 18,
      "upgrade_skill": "smithing"
    }
  ],
  "shop_inventory": {
    "shop_type": "blacksmith",
    "base_stock_count": 8,
    "random_extra": "1d4",
    "refresh_trigger": "on_long_rest",
    "guaranteed_items": [
      { "item_id": "item_dagger", "count": 2, "price_gp": 2 },
      { "item_id": "item_shortsword", "count": 1, "price_gp": 10 },
      { "item_id": "item_leather", "count": 1, "price_gp": 10 },
      { "item_id": "item_shield", "count": 1, "price_gp": 10 },
      { "item_id": "material_iron_ingot", "count": 5, "price_gp": 5 },
      { "item_id": "material_leather_strap", "count": 3, "price_gp": 2 }
    ]
  },
  "visual_data": {
    "background_path": "data/assets/backgrounds/tavern_blacksmith.png",
    "npc_positions": [{ "npc_id": "npc_blacksmith_grim", "x": 340, "y": 220 }],
    "interaction_points": [
      { "id": "anvil", "label": "铁砧", "x": 400, "y": 280 },
      { "id": "forge", "label": "熔炉", "x": 280, "y": 260 }
    ]
  }
}
```

### 2.4 六区域完整定义

| 区域ID | 名称 | 解锁Lv | 建造金币 | 建造材料 | 核心功能 | NPC |
|--------|------|:------:|:--------:|----------|----------|-----|
| `area_hall` | 大厅 (Hall) | Lv1 | 0 | — | 招募板、任务板、休息、闲聊、酒馆事件 | 酒馆老板 (固定) |
| `area_blacksmith` | 铁匠铺 (Blacksmith) | Lv2 | 500 | 铁锭×10, 木板×20 | 装备修复、武器/护甲/盾牌打造、装备升级 | 老铁匠格里姆 |
| `area_alchemist` | 炼金台 (Alchemist) | Lv3 | 800 | 草药×15, 水晶粉×5, 玻璃瓶×10 | 药水制作、卷轴抄写、炼金材料商店 | 炼金术士泽尔 |
| `area_library` | 图书馆 (Library) | Lv5 | 2,000 | 古代典籍×3, 羊皮纸×20, 魔法墨水×10 | 法术卷轴购买/研究、技能训练、新职业解锁、专长学习 | 图书管理员赛琳 |
| `area_temple` | 神殿 (Temple) | Lv7 | 5,000 | 圣水×10, 银锭×20, 圣徽×5 | 角色复活、诅咒移除、疾病治疗、属性恢复、祝福buff | 神父卡斯托 |
| `area_heroes_wall` | 英雄之壁 (Hero's Wall) | 首次冒险完成 | 0 (自动) | — | 查看/生成角色传记、查看冒险历史、传承装备展示 | 无固定NPC |

### 2.5 升级资源需求明细

除声望XP外，酒馆升级还需消耗金币和材料：

| 升至Lv | 金币 | 材料 | 前置条件 |
|:------:|:----:|------|----------|
| Lv2 | 300 | 木材×30, 铁锭×5 | 声望 1,200 |
| Lv3 | 600 | 石材×50, 铁锭×15, 皮革×10 | 声望 3,700 |
| Lv4 | 1,200 | 石材×80, 铁锭×30, 水晶粉×5 | 声望 8,700, 完成≥3次冒险 |
| Lv5 | 2,500 | 古代典籍×2, 水晶粉×15, 魔法墨水×5 | 声望 17,700 |
| Lv6 | 5,000 | 石材×150, 水晶粉×25, 精华×5 | 声望 32,700, 拥有≥1个Lv5角色 |
| Lv7 | 10,000 | 圣水×5, 银锭×15, 古代典籍×5 | 声望 56,700 |
| Lv8 | 18,000 | 龙鳞碎片×3, 秘银锭×10, 精华×15 | 声望 94,700, 完成≥1次中冒险 |
| Lv9 | 30,000 | 龙鳞碎片×10, 秘银锭×25, 传奇精华×1 | 声望 154,700 |
| Lv10 | 50,000 | 龙鳞碎片×25, 传奇精华×5, 龙心×1 | 声望 254,700, 完成≥1次长冒险 |

### 2.6 Prestige系统 (威望)

Prestige 是**跨存档的永久元进度**。达到Lv10后解锁 Prestige：

```
Prestige = 永久解锁内容，跨越所有存档和新游戏
────────────────────────────────────────────────
触发: 酒馆等级达到 Lv10

Prestige 获取:
  - 达到Prestige Lv1: 全角色起始金币 +50
  - 达到Prestige Lv2: 起始随机 uncommon 物品 ×1
  - 达到Prestige Lv3: 起始角色 Lv2 (而非Lv1)
  - 达到Prestige Lv4: 新角色种族池 +3 (解锁全种族)
  - 达到Prestige Lv5: 起始随机 rare 物品 ×1
  - Prestige Lv5+: 每额外Prestige等级 +10%声望XP获取

Prestige升级条件:
  Prestige Lv1: 第一次达到酒馆Lv10
  Prestige Lv2: 累计完成 20 次冒险
  Prestige Lv3: 累计完成 50 次冒险
  Prestige Lv4: 累计完成 100 次冒险
  Prestige Lv5: 累计完成 200 次冒险
```

---

## 3. 招募系统

### 3.1 招募板数据模型

招募板 (Recruitment Board) 是大厅中的核心交互对象，展示可招募的冒险者。

```json
{
  "board_id": "board_main",
  "area_id": "area_hall",
  "refresh_config": {
    "auto_refresh_on": "return_from_adventure",
    "manual_refresh_cost_gold": 200,
    "manual_refresh_cooldown_adventures": 1,
    "available_slots": 3,
    "max_slots_by_tavern_level": {
      "1": 3, "2": 3, "3": 4, "4": 4, "5": 5,
      "6": 5, "7": 6, "8": 6, "9": 7, "10": 8
    }
  },
  "roster_config": {
    "base_roster_size": 6,
    "max_roster_size": 12,
    "roster_upgrades": [
      { "upgrade_level": 4, "new_size": 7, "cost_gold": 1000 },
      { "upgrade_level": 6, "new_size": 9, "cost_gold": 3000 },
      { "upgrade_level": 8, "new_size": 11, "cost_gold": 8000 },
      { "upgrade_level": 10, "new_size": 12, "cost_gold": 15000 }
    ]
  }
}
```

### 3.2 招募流程 (UI Flow)

```
招募流程 (Recruitment Flow)
───────────────────────────────
Step 1: 查看招募板 (View Board)
  ┌──────────────────────────────┐
  │ 招募板 — 可招募冒险者 (3/5)   │
  │                              │
  │ ┌──────────────────────────┐ │
  │ │ [头像] 索林·铁锤          │ │
  │ │ Lv1 矮人战士               │ │
  │ │ "固执/忠诚/嗜酒"           │ │
  │ │ 招募费: 100 GP            │ │
  │ │ [检查] [招募]             │ │
  │ └──────────────────────────┘ │
  │ ┌──────────────────────────┐ │
  │ │ [头像] 艾拉拉·月歌        │ │
  │ │ Lv1 精灵法师               │ │
  │ │ "优雅/好奇/完美主义"       │ │
  │ │ 招募费: 150 GP            │ │
  │ │ [检查] [招募]             │ │
  │ └──────────────────────────┘ │
  │ ...                          │
  └──────────────────────────────┘

Step 2: 检查角色 (Inspect)
  打开角色详情面板:
    - 六维属性 (STR/DEX/CON/INT/WIS/CHA)
    - HP/AC/速度
    - 技能熟练项
    - 种族特性
    - LLM生成的背景故事
    - 个性标签
  [招募] [取消]

Step 3: 确认招募 (Confirm Recruit)
  ┌──────────────────────────────┐
  │ 确认招募: 索林·铁锤          │
  │ 招募费: 100 GP              │
  │ 当前金币: 350 GP            │
  │ 角色槽: 3/6                 │
  │                              │
  │ [确认招募] [取消]           │
  └──────────────────────────────┘

Step 4: 招募成功
  - 扣除招募费
  - 角色加入roster
  - 触发 LLM 文案Agent → 生成招募场景文本
    "索林·铁锤推开酒馆的木门，朝你点了点头..."
  - 角色出现在酒馆大厅 (可见闲逛)
```

### 3.3 角色生成管线

#### 3.3.1 双管线架构

```
Phase 1: 程序化数值层 (Procedural Numerical Layer)
  ┌──────────────────────────────────────────────┐
  │ 输入: race + class + target_level             │
  │ → 属性生成 (Standard Array + 种族加成)        │
  │ → HP/AC/衍生值计算                            │
  │ → 技能熟练/豁免熟练/起始装备分配               │
  │ → 验证数值合法性 (scores 3-20)                │
  │ 输出: Character Numerical Block               │
  └──────────────────┬───────────────────────────┘
                     │
                     ▼
Phase 2: LLM叙事层 (LLM Narrative Layer)
  ┌──────────────────────────────────────────────┐
  │ 输入: race + class + stat_distribution        │
  │ Agent: 文案Agent (Copywriter Agent)           │
  │ 输出: name / gender / personality_tags        │
  │       / backstory / appearance_description    │
  │       / personal_goal                         │
  │ Schema验证 → 重试最多3次                       │
  │ 失败 → 使用预置模板                            │
  └──────────────────┬───────────────────────────┘
                     │
                     ▼
Phase 3: 合并写入 (Merge & Persist)
  ┌──────────────────────────────────────────────┐
  │ 数值层 + 叙事层 → Complete Character Data      │
  │ → 写入SQLite (结构化数据)                     │
  │ → 发出 character_created 信号                  │
  └──────────────────────────────────────────────┘
```

#### 3.3.2 招募池生成算法

```
Algorithm: GenerateRecruitmentPool(tavern_level, player_level)
────────────────────────────────────────────────────────
输入:
  tavern_level: int (1-10)
  player_level: int (当前队伍平均等级)
输出:
  candidates: Array<CharacterData> (size = available_slots)

Step 1: 确定候选人数
  available = board.available_slots[tavern_level]
  reserved = min(1, available)  // 至少1个对应玩家等级的角色

Step 2: 分配等级分布
  levels = []
  levels.append(player_level)  // 保证1个匹配等级
  for i in range(available - 1):
    level = normal_distribution(mean=player_level, stddev=1.5)
    level = clamp(level, 1, player_level + 1)
    levels.append(level)

Step 3: 选择种族与职业
  种族权重 (MVP: Human 40%, Elf 30%, Dwarf 30%)
  职业权重 (MVP: Fighter 35%, Wizard 30%, Rogue 35%)
  避免重复: 同一池中不出现2个相同种族+职业组合

Step 4: 对每个候选人调用生成管线
  for i in range(available):
    candidate = CharacterGenerator.generate(race, class, levels[i])
    candidates.append(candidate)

Step 5: 计算招募费
  招募费 = base_cost + level_cost
  base_cost: Lv1 = 100 GP
  level_cost: 每超过玩家等级1级 +80 GP, 低1级 -40 GP

Step 6: 返回候选池
  return candidates
```

### 3.4 招募费用表

| 角色等级 | 玩家等级 | 价差 | 基础费 | 总招募费 |
|:--------:|:--------:|:----:|:------:|:--------:|
| Lv1 | Lv1 | 0 | 100 | **100 GP** |
| Lv1 | Lv3 | -2 | 100 | **60 GP** |
| Lv2 | Lv1 | +1 | 100 | **180 GP** |
| Lv3 | Lv1 | +2 | 100 | **260 GP** |
| Lv3 | Lv3 | 0 | 100 | **100 GP** |
| Lv4 | Lv3 | +1 | 100 | **180 GP** |
| Lv5 | Lv4 | +1 | 100 | **180 GP** |

### 3.5 角色槽位管理

```
Roster管理规则:
────────────────
基础槽位: 6 (Lv1-3)
最大槽位: 12 (Lv10)

槽位扩展:
  Lv4: 7 slots (1,000 GP)
  Lv6: 9 slots (3,000 GP)
  Lv8: 11 slots (8,000 GP)
  Lv10: 12 slots (15,000 GP)

Party组成: 从roster中选4人组成冒险队伍
  - 未满4人时: 无法出发
  - 超过12人时: 必须先解雇

角色解雇 (Dismissal):
  - 解雇的角色标记为 status="dismissed"
  - 角色数据保留在数据库 (不删除)
  - 30天后角色可从酒馆中"重返" (小概率，5%)
  - 英雄传记保留在英雄之壁
  - 解雇费用: 0 GP
  - 有 soulbound 物品的角色解雇 → 物品自动收藏至酒馆仓库
```

### 3.6 完整招募JSON Schema

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "RecruitmentBoardState",
  "type": "object",
  "required": ["board_id", "last_refreshed", "candidates", "refresh_info"],
  "properties": {
    "board_id": { "type": "string" },
    "last_refreshed": { "type": "string", "format": "date-time" },
    "candidates": {
      "type": "array",
      "minItems": 1,
      "maxItems": 8,
      "items": {
        "type": "object",
        "required": ["character_id", "name", "race", "class", "level", "recruit_cost_gp", "stat_summary", "narrative_preview"],
        "properties": {
          "character_id": { "type": "string", "pattern": "^char_[a-f0-9]{8}$" },
          "name": { "type": "string" },
          "race": { "type": "string", "enum": ["human","elf","dwarf","halfling","half_orc","gnome","tiefling","dragonborn","half_elf"] },
          "class": { "type": "string", "enum": ["fighter","wizard","rogue","cleric","ranger","paladin","sorcerer","bard","druid","monk","warlock","barbarian"] },
          "level": { "type": "integer", "minimum": 1, "maximum": 20 },
          "gender": { "type": "string", "enum": ["male","female"] },
          "personality_tags": { "type": "array", "items": { "type": "string" }, "minItems": 2, "maxItems": 5 },
          "stat_summary": {
            "type": "object",
            "required": ["str","dex","con","int","wis","cha","hp","ac"],
            "properties": {
              "str": { "type": "integer" }, "dex": { "type": "integer" },
              "con": { "type": "integer" }, "int": { "type": "integer" },
              "wis": { "type": "integer" }, "cha": { "type": "integer" },
              "hp": { "type": "integer" }, "ac": { "type": "integer" }
            }
          },
          "narrative_preview": { "type": "string", "description": "1句话角色概述", "maxLength": 80 },
          "recruit_cost_gp": { "type": "integer", "minimum": 50 },
          "appearance_summary": { "type": "string", "maxLength": 60 },
          "generation_source": { "type": "string", "enum": ["procedural_llm", "template_fallback", "returning_character"] },
          "available_since": { "type": "string", "format": "date-time" },
          "expires_at": { "type": "string", "format": "date-time", "description": "候选人在招募板上过期的时间(通常为3次冒险后)" }
        }
      }
    },
    "refresh_info": {
      "type": "object",
      "required": ["auto_refresh_on", "manual_refresh_cost_gold", "manual_refresh_cooldown_remaining"],
      "properties": {
        "auto_refresh_on": { "type": "string" },
        "manual_refresh_cost_gold": { "type": "integer" },
        "manual_refresh_cooldown_remaining": { "type": "integer" }
      }
    }
  }
}
```

---

## 4. NPC调度系统

### 4.1 NPC类型定义

| NPC类型 | ID前缀 | 作用 | 是否可对话 | 是否可交易 | 出现条件 |
|---------|--------|------|:---:|:---:|----------|
| 固定NPC | `npc_fixed_` | 区域常驻NPC（铁匠、炼金术士、图书管理员、神父、酒馆老板） | YES | YES (对应区域) | 区域解锁后常驻 |
| 商人NPC | `npc_merchant_` | 旅行商人、特殊物品贩售者 | YES | YES | 概率出现 |
| 任务NPC | `npc_quest_` | 任务发布者、故事线NPC | YES | NO | 世界事件/冒险进度触发 |
| 故事NPC | `npc_story_` | 推进主线/支线故事的角色 | YES | NO | 故事进度触发 |
| 神秘旅人 | `npc_traveler_` | 带来情报、解锁新冒险、特殊交易 | YES | YES (有限) | 低概率随机出现 |
| 训练师 | `npc_trainer_` | 提供技能训练、专长教学 | YES | YES (训练费) | 图书馆解锁后 |

### 4.2 NPC调度模型

```json
{
  "npc_schedule": {
    "fixed_npcs": [
      {
        "npc_id": "npc_fixed_barkeep_maria",
        "name": "玛莉亚",
        "name_en": "Maria",
        "role": "酒馆老板",
        "area": "area_hall",
        "always_present": true,
        "dialogue_tree_id": "dt_barkeep_maria",
        "personality": ["热情", "精明", "母爱泛滥"],
        "services": ["房间租赁", "基础消息打听"]
      },
      {
        "npc_id": "npc_fixed_blacksmith_grim",
        "name": "格里姆·铁砧",
        "name_en": "Grim Anvil",
        "role": "铁匠",
        "area": "area_blacksmith",
        "always_present": true,
        "unlock_tavern_level": 2,
        "dialogue_tree_id": "dt_blacksmith_grim",
        "personality": ["沉默寡言", "手艺偏执", "暗藏温柔"],
        "services": ["装备修复", "装备打造", "装备升级"]
      }
    ],
    "rotating_npcs": [
      {
        "npc_id": "npc_merchant_zik",
        "name": "兹克斯",
        "role": "旅行商人",
        "type": "merchant",
        "appearance_weight": 30,
        "appearance_conditions": {
          "min_tavern_level": 1,
          "cooldown_adventures": 2,
          "max_concurrent_visits": 3
        },
        "visit_duration_adventures": 1,
        "inventory_pool": "traveling_merchant_pool",
        "special_deals": [
          { "item_id": "elixir_of_darkvision", "discount_percent": 25, "chance": 15 }
        ]
      },
      {
        "npc_id": "npc_traveler_mysterious",
        "role": "神秘旅人",
        "type": "mysterious_traveler",
        "appearance_weight": 15,
        "appearance_conditions": {
          "min_tavern_level": 3,
          "cooldown_adventures": 4,
          "trigger_on_return": true
        },
        "visit_duration_adventures": 1,
        "services": ["情报提供", "特殊冒险解锁", "稀有物品交易"],
        "special_event_chance": 40,
        "special_event_pool": "mysterious_traveler_events"
      },
      {
        "npc_id": "npc_quest_giver_pool",
        "role": "任务发布者",
        "type": "quest_giver",
        "appearance_weight": 50,
        "appearance_conditions": {
          "min_tavern_level": 1,
          "requires_active_storyline": true,
          "replaces_existing": true
        },
        "visit_duration_adventures": 999,
        "quest_pool_ids": ["story_quests", "side_quests", "character_quests"],
        "departure_condition": "quest_completed_or_refused"
      },
      {
        "npc_id": "npc_trainer_master",
        "role": "训练师",
        "type": "trainer",
        "appearance_weight": 20,
        "appearance_conditions": {
          "min_tavern_level": 5,
          "cooldown_adventures": 3
        },
        "visit_duration_adventures": 3,
        "training_options": [
          { "skill": "athletics", "cost_gp": 500, "days": 3 },
          { "feat_id": "great_weapon_master", "cost_gp": 2000, "days": 7 }
        ]
      }
    ]
  },
  "schedule_logic": {
    "check_trigger": "on_return_from_adventure or on_enter_tavern",
    "max_concurrent_rotating_npcs": 3,
    "priority_order": ["quest_giver > story_npc > mysterious_traveler > trainer > merchant"],
    "conflict_resolution": "higher_priority NPC replaces lower_priority if slots full"
  }
}
```

### 4.3 NPC生命周期

```
NPC Lifecycle (生命周期):
────────────────────────────────

Arrival → Active → Departure
 ─────────────────────────────

Arrival (到达):
  - 触发条件检查: 每次从冒险返回时 / 每次进入酒馆时
  - 随机掷骰 (weighted)
  - 故事NPC可能有强制出现逻辑
  - LLM生成NPC到达场景文本:
    "一个披着深蓝色斗篷的旅人推开了酒馆的木门..."

Active (在酒馆中):
  - NPC在对应区域可见 (精灵渲染)
  - 玩家可与之对话
  - NPC有自己的日常动画循环 (铁匠打铁、商人整理货物)
  - 持续 visit_duration 次冒险

Departure (离开):
  - 到期自动离开 或 触发离开条件
  - 任务NPC: 任务完成/拒绝后离开
  - LLM生成离开场景文本:
    "旅人朝你点了点头，起身离开了酒馆..."
  - 记录visit_history (SQLite)
```

### 4.4 NPC对话与事件集成

NPC对话由 **DM Agent** 实时生成。每个NPC关联一个 `dialogue_tree_id`，对话选项的后果由程序控制（关系值变化、任务触发、物品交易）。

**对话触发流程**:
1. 玩家点击NPC → 读取 `dialogue_tree_id`
2. 程序根据当前状态（世界状态、队伍状态、关系值）确定可用对话选项
3. DM Agent 为选项生成叙事化的 `flavor_text`
4. 玩家选择 → 程序处理后果 → DM Agent 生成叙事反馈

---

## 5. 商店系统

### 5.1 商店总览

| 商店 | 区域 | 解锁条件 | 出售类型 | 刷新周期 | 库存基准 |
|------|------|----------|----------|----------|:---:|
| 杂货商 | 大厅 | Lv1 (初始) | 基础消耗品、基础武器、弹药、口粮 | 每次冒险返回后 | 8+d6 |
| 铁匠铺 | 铁匠铺 | Lv2 | 武器、护甲、盾牌、铁匠材料 | 每次长休后 | 6+d4 |
| 炼金店 | 炼金台 | Lv3 | 药水、油、毒药、炼金材料 | 每次冒险返回后 | 6+d4 |
| 魔法书店 | 图书馆 | Lv5 | 法术卷轴、魔杖、魔法墨水 | 每次长休后 | 4+d4 |
| 神殿商店 | 神殿 | Lv7 | 圣水、祝福物品、复活材料 | 每次长休后 | 4+d4 |
| 旅行商人 | 随机 | 随机出现 | 随机稀有物品、折扣品 | 出现时随机生成 | 6+d4 |

### 5.2 定价模型

定价模型引用 Items & Equipment System §12.2，酒馆特有修正如下：

**卖价 (Shop → Player)**:
```
price = base_value × rarity_multiplier × condition_adjuster × tavern_reputation_discount

rarity_multiplier:
  common: ×1, uncommon: ×5, rare: ×50, very_rare: ×500, legendary: ×5000, artifact: ×50000

tavern_reputation_discount (酒馆声望折扣):
  Lv1-2: ×1.25 (新手加价)
  Lv3-4: ×1.10
  Lv5-6: ×1.00
  Lv7-8: ×0.95
  Lv9-10: ×0.90
```

**回收价 (Player → Shop)**:
```
sell_price = base_value × rarity_multiplier × 0.5 × condition_adjuster × tavern_reputation_discount
```

### 5.3 商店库存刷新机制

```
Algorithm: RefreshShopInventory(shop_id, tavern_level, adventure_tier)
──────────────────────────────────────────────────────────────────
Step 1: 确定库存大小
  base_count = shop_inventory[shop_id].base_stock_count
  random_extra = roll(shop_inventory[shop_id].random_extra)
  total_slots = base_count + random_extra

Step 2: 生成必定商品
  guaranteed = shop_inventory[shop_id].guaranteed_items
  直接加入库存 (不受随机影响)

Step 3: 生成随机商品
  remaining_slots = total_slots - len(guaranteed)
  for i in range(remaining_slots):
    rarity = RollRarity("shop", adventure_tier)  // Items System §3.3
    item_type = RollItemType(rarity, "shop")      // Items System §9.2
    item = GenerateBaseItem(item_type, rarity, tavern_level)
    inventory.append(item)

Step 4: 特殊商品检查
  for special in shop_inventory[shop_id].special_items:
    if roll_chance(special.chance):
      inventory.append(special.item)

Step 5: 应用酒馆声望折扣
  for item in inventory:
    item.display_price = item.base_price × tavern_reputation_discount

Step 6: 返回完整库存
```

### 5.4 各商店必定商品表

#### 杂货商 (General Store) — Lv1

| 物品 | 数量 | 单价 (GP) |
|------|:---:|:---------:|
| 治疗药水 (2d4+2) | 3 | 50 |
| 解毒剂 | 2 | 40 |
| 火把 ×5 | 3 | 5 |
| 口粮 (1日份) | 5 | 2 |
| 箭矢 ×20 | 3 | 1 |
| 弩矢 ×20 | 3 | 1 |
| 睡袋 | 2 | 10 |
| 绳索 (50尺) | 2 | 10 |

#### 铁匠铺 (Blacksmith) — Lv2

| 物品 | 数量 | 单价 (GP) |
|------|:---:|:---------:|
| 匕首 (1d4) | 2 | 2 |
| 短剑 (1d6) | 1 | 10 |
| 手斧 (1d6) | 1 | 5 |
| 皮甲 (11+DEX) | 1 | 10 |
| 盾牌 (+2 AC) | 1 | 10 |
| 铁锭 | 5 | 5 |
| 皮革束带 | 3 | 2 |
| 维修工具包 | 1 | 25 |

#### 炼金店 (Alchemist) — Lv3

| 物品 | 数量 | 单价 (GP) |
|------|:---:|:---------:|
| 治疗药水 (2d4+2) | 3 | 50 |
| 高级治疗药水 (4d4+4) | 1 | 250 |
| 解毒剂 | 2 | 40 |
| 炼金火油 (1d4 火/轮) | 1 | 75 |
| 强酸瓶 (2d6 酸) | 1 | 50 |
| 隐身药水 | 1 | 750 |
| 草药 | 5 | 3 |
| 水晶粉 | 3 | 20 |

#### 魔法书店 (Arcane Shop) — Lv5

| 物品 | 数量 | 单价 (GP) |
|------|:---:|:---------:|
| 1环法术卷轴 (随机3种) | 各1 | 50 |
| 2环法术卷轴 (随机1种) | 1 | 150 |
| 魔法墨水 | 5 | 25 |
| 羊皮纸 | 10 | 5 |
| 魔杖核心 | 1 | 100 |

#### 神殿商店 (Temple Shop) — Lv7

| 物品 | 数量 | 单价 (GP) |
|------|:---:|:---------:|
| 圣水 | 3 | 50 |
| 祝福圣徽 (+1 对抗亡灵 AC) | 1 | 500 |
| 复活卷轴 (Revivify) | 1 | 5,000 |
| 移除诅咒卷轴 | 1 | 2,500 |
| 高等复原术卷轴 | 1 | 3,000 |

### 5.5 特殊物品解锁条件

| 物品 | 商店 | 解锁条件 |
|------|------|----------|
| 锋锐油 (+3 武器 1h) | 炼金店 | 酒馆 Lv5 |
| 加速药水 | 炼金店 | 酒馆 Lv7 |
| 力量药水 (STR=21 1h) | 炼金店 | 酒馆 Lv9 |
| 3环法术卷轴 | 魔法书店 | 酒馆 Lv7 |
| 魔法武器 (+1) | 铁匠铺 | 酒馆 Lv6 |
| 全身板甲 | 铁匠铺 | 酒馆 Lv5 |
| 传说级物品 | 旅行商人 | 酒馆 Lv9+ / Prestige |

---

## 6. 酒馆事件系统

### 6.1 事件触发条件

```
事件触发检查 (Event Trigger Check):
────────────────────────────────────
触发时机:
  1. 从冒险返回后 (每次必定检查)
  2. 每次在酒馆进行长休后 (概率检查)
  3. 特定角色关系值跨越阈值时 (必定触发)

检查逻辑:
  on_return_from_adventure():
    event_chance = base_chance(25%) + modifiers
    modifiers:
      tavern_level >= 6: +10%
      有角色HP<25%返回: +15% (受伤引发关注)
      冒险失败: +30%
      上次事件距今 >= 3次冒险: +20%
    
    if roll(100) < event_chance:
      trigger_event()

on_long_rest_complete():
    event_chance = base_chance(10%) + modifiers
    modifiers:
      角色关系值 >= 5 (Bonded): +15%
      角色关系值 <= -5 (Nemesis): +20%
      tavern_level >= 8: +5%
    
    if roll(100) < event_chance:
      trigger_event()

on_relationship_threshold_crossed():
    必定触发关系事件 (见 Section 7)
```

### 6.2 事件池系统

```json
{
  "event_pool": {
    "pool_id": "tavern_event_pool",
    "events": [
      {
        "event_id": "evt_character_argument",
        "event_type": "character_interaction",
        "name": "角色争吵",
        "weight": 25,
        "min_tavern_level": 1,
        "cooldown_adventures": 3,
        "participants": { "count": 2, "selection": "lowest_relationship_pair" },
        "triggers": {
          "prohibited_if": [
            { "condition": "relationship_value_between > 3" },
            { "condition": "both_characters_same_personality_tag", "tag": "平和" }
          ],
          "preferred_if": [
            { "condition": "relationship_value_between < -1" }
          ]
        },
        "outcomes": [
          {
            "outcome_id": "player_mediate_success",
            "trigger": "player_choice_mediate AND persuasion_dc_14",
            "mechanical_effect": { "relationship_change": 0, "player_relationship_change": 1 },
            "narrative_prompt": "玩家成功调解了{A}和{B}的争吵..."
          },
          {
            "outcome_id": "player_mediate_fail",
            "trigger": "player_choice_mediate AND persuasion_failed",
            "mechanical_effect": { "relationship_change": -2 },
            "narrative_prompt": "玩家试图调解但让事情更糟了..."
          },
          {
            "outcome_id": "player_side_a",
            "trigger": "player_choice_side_a",
            "mechanical_effect": { "relationship_change_a_player": 1, "relationship_change_b_player": -2 },
            "narrative_prompt": "玩家站在{A}一边..."
          },
          {
            "outcome_id": "player_ignore",
            "trigger": "player_choice_ignore",
            "mechanical_effect": { "relationship_change": -1 },
            "narrative_prompt": "玩家无视了争吵..."
          }
        ],
        "llm_prompt_template": "酒馆事件：角色争吵\n{A} 和 {B} 在酒馆中发生了争执。{A}的性格是{personality_a}，{B}的性格是{personality_b}。他们最近的关系值为{value}。\n\n请生成一段2-3句的争吵场景描述。"
      },
      {
        "event_id": "evt_personal_request",
        "event_type": "character_quest",
        "name": "角色个人请求",
        "weight": 15,
        "min_tavern_level": 2,
        "cooldown_adventures": 5,
        "participants": { "count": 1, "selection": "random_roster" },
        "triggers": {
          "preferred_if": [
            { "condition": "character_adventures_completed >= 2" },
            { "condition": "character_has_personal_goal" }
          ]
        },
        "outcomes": [
          {
            "outcome_id": "accept_request",
            "trigger": "player_choice_accept",
            "mechanical_effect": {
              "generate_side_quest": true,
              "quest_type": "character_personal",
              "reward_on_complete": { "relationship_change": 3, "character_trait_gain": true }
            }
          },
          {
            "outcome_id": "decline_request",
            "trigger": "player_choice_decline",
            "mechanical_effect": { "relationship_change": -1 }
          }
        ]
      },
      {
        "event_id": "evt_mysterious_traveler",
        "event_type": "npc_arrival",
        "name": "神秘旅人",
        "weight": 12,
        "min_tavern_level": 3,
        "cooldown_adventures": 5,
        "participants": { "count": 0, "selection": "none" },
        "triggers": {},
        "outcomes": [
          {
            "outcome_id": "unlock_adventure",
            "trigger": "player_listens",
            "mechanical_effect": { "unlock_special_adventure": true, "adventure_tier": "medium" }
          },
          {
            "outcome_id": "rare_trade",
            "trigger": "player_asks_trade",
            "mechanical_effect": { "offer_special_deal": true }
          }
        ]
      },
      {
        "event_id": "evt_tavern_attack",
        "event_type": "combat",
        "name": "酒馆被袭",
        "weight": 5,
        "min_tavern_level": 4,
        "cooldown_adventures": 8,
        "participants": { "count": "all_roster", "selection": "all_present" },
        "triggers": {
          "preferred_if": [
            { "condition": "previous_adventure_enemy_faction_escaped" },
            { "condition": "world_event_active", "event_type": "invasion" }
          ]
        },
        "outcomes": [
          {
            "outcome_id": "victory",
            "trigger": "combat_victory",
            "mechanical_effect": {
              "tavern_reputation_xp": 200,
              "all_participant_relationship": 2,
              "loot": "boss_tier_defense"
            }
          },
          {
            "outcome_id": "defeat",
            "trigger": "combat_defeat",
            "mechanical_effect": {
              "tavern_damage": "随机设施暂时不可用 (1次冒险)",
              "gold_loss_percent": 20
            }
          }
        ]
      },
      {
        "event_id": "evt_romance",
        "event_type": "character_interaction",
        "name": "浪漫事件",
        "weight": 8,
        "min_tavern_level": 3,
        "cooldown_adventures": 6,
        "participants": { "count": 2, "selection": "compatible_orientation_pair" },
        "triggers": {
          "required": [
            { "condition": "relationship_value_between >= 4" },
            { "condition": "genders_compatible" },
            { "condition": "no_conflicting_nemesis_relationship" }
          ]
        },
        "outcomes": [
          {
            "outcome_id": "relationship_bloom",
            "trigger": "player_encourages",
            "mechanical_effect": { "relationship_change": 3, "label_upgrade": "lovers" }
          },
          {
            "outcome_id": "relationship_stay_friends",
            "trigger": "player_stays_neutral",
            "mechanical_effect": { "relationship_change": 1 }
          }
        ]
      },
      {
        "event_id": "evt_training",
        "event_type": "training",
        "name": "训练事件",
        "weight": 12,
        "min_tavern_level": 5,
        "cooldown_adventures": 4,
        "participants": { "count": 2, "selection": "skill_difference_pair" },
        "triggers": {
          "preferred_if": [
            { "condition": "one_character_has_higher_skill" },
            { "condition": "compatible_classes" }
          ]
        },
        "outcomes": [
          {
            "outcome_id": "skill_transfer",
            "trigger": "player_initiates",
            "mechanical_effect": {
              "relationship_change": 3,
              "skill_proficiency_shared": true,
              "label_upgrade": "mentor_student"
            }
          }
        ]
      },
      {
        "event_id": "evt_conflict_of_faith",
        "event_type": "character_interaction",
        "name": "信仰冲突",
        "weight": 10,
        "min_tavern_level": 2,
        "cooldown_adventures": 4,
        "participants": { "count": 2, "selection": "opposing_traits_pair" },
        "triggers": {
          "required": [
            { "condition": "personality_conflict_score >= 5" }
          ]
        },
        "outcomes": [
          {
            "outcome_id": "player_mediates",
            "trigger": "player_choice_mediate AND religion_dc_15",
            "mechanical_effect": { "relationship_change": 1 }
          },
          {
            "outcome_id": "unresolved",
            "trigger": "default",
            "mechanical_effect": { "relationship_change": -2 }
          }
        ]
      },
      {
        "event_id": "evt_celebration",
        "event_type": "buff",
        "name": "庆祝活动",
        "weight": 18,
        "min_tavern_level": 2,
        "cooldown_adventures": 1,
        "participants": { "count": "all_roster", "selection": "all" },
        "triggers": {
          "preferred_if": [
            { "condition": "previous_adventure_success" },
            { "condition": "boss_defeated_in_previous_adventure" }
          ]
        },
        "outcomes": [
          {
            "outcome_id": "morale_buff",
            "trigger": "automatic",
            "mechanical_effect": {
              "buff_id": "buff_celebration_morale",
              "buff_duration": "next_adventure",
              "buff_effect": "所有队员在下一次冒险中获得+1 全豁免"
            }
          }
        ]
      }
    ],
    "priority_rules": {
      "story_events_override_random": true,
      "max_events_per_tavern_visit": 1,
      "forced_events": ["evt_tavern_attack_world_event"]
    }
  }
}
```

### 6.3 事件频率与优先级

```
事件频率控制:
  - 每次从冒险返回: 25% + 修正 → 平均每2-3次冒险触发1次
  - 每次酒馆长休: 10% + 修正 → 平均每5-7次休息触发1次
  - 关系阈值跨越: 100% → 立即触发

优先级系统:
  Level 1 (最高): 故事事件 (story_events) — 强制触发，无视概率
  Level 2: 关系阈值事件 — 关系值跨越±5时必定触发
  Level 3: 角色个人请求 — 优先于普通随机事件
  Level 4: 普通随机事件 — 基于权重随机选择
```

### 6.4 事件结果分离

```
事件结果 = 机械结果 (程序控制) + 叙事结果 (LLM生成)
──────────────────────────────────────────────────

机械结果 (Program-controlled):
  - 关系值变化 (+/- N)
  - 任务生成/解锁
  - 物品/金币奖励
  - 战斗触发
  - buff/debuff 应用
  - 设施状态变更

叙事结果 (LLM-generated):
  - 场景描写 (DM Agent)
  - NPC对话 (DM Agent)
  - 角色情感表达 (DM Agent)
  - 氛围文本 (DM Agent)

执行流程:
  1. 程序决定机械结果
  2. 将机械结果 + 上下文 传入 LLM
  3. LLM 生成符合结果的叙事文本
  4. UI 展示叙事文本 + 机械结果通知
```

---

## 7. 关系事件系统

### 7.1 关系阈值事件

当角色间关系值跨越临界阈值时，触发特殊关系事件：

| 跨越方向 | 阈值 | 触发的标签 | 事件ID |
|----------|:----:|-----------|--------|
| 上升 | 5 | Bonded (牵绊) | `evt_rel_bond_formed` |
| 上升 | 10 | Deep Bond (深厚牵绊) | `evt_rel_deep_bond` |
| 下降 | -5 | Nemesis (宿敌) | `evt_rel_nemesis_formed` |
| 下降 | -10 | Arch Nemesis (死敌) | `evt_rel_arch_nemesis` |
| 上升 | 0 (from negative) | Neutral (和解) | `evt_rel_reconciliation` |

### 7.2 五种关系类型详细数据

引用 Character System §4.4，完善酒馆端的处理逻辑：

```json
{
  "relationship_types": {
    "comrade": {
      "name": "战友",
      "name_en": "Comrade",
      "trigger_conditions": {
        "relationship_value": { "min": 5 },
        "coop_kills": { "min": 3 },
        "adventures_together": { "min": 1 }
      },
      "combat_effects": {
        "adjacent_bonus": { "attack_roll": 1, "description": "邻接攻击+1命中" }
      },
      "tavern_effects": {
        "formation_bonus": { "unlock": true, "description": "可设置战友编队阵型" },
        "special_dialogue": { "unlock": true }
      },
      "upkeep": {
        "decay_per_adventure_without_interaction": -1,
        "min_value_without_upkeep": 3
      },
      "events": ["evt_comrade_sparring", "evt_comrade_drink", "evt_comrade_story"]
    },
    "lovers": {
      "name": "恋人",
      "name_en": "Lovers",
      "trigger_conditions": {
        "relationship_value": { "min": 5 },
        "shared_rest_events": { "min": 2 },
        "compatible_personalities": true,
        "compatible_genders": true
      },
      "combat_effects": {
        "adjacent_bonus": { "ac": 1, "description": "邻接AC+1" },
        "protective_instinct": { "description": "当恋人HP<25%时，所有攻击+1伤害" }
      },
      "tavern_effects": {
        "intimate_scene": { "unlock": true, "frequency": "每3次冒险1次" },
        "jealousy_mechanic": { "trigger_on": "player_flirts_with_other", "relationship_penalty": -2 }
      },
      "events": ["evt_lovers_moonlight", "evt_lovers_gift", "evt_lovers_quarrel"]
    },
    "rivals": {
      "name": "宿敌",
      "name_en": "Rivals",
      "trigger_conditions": {
        "relationship_value": { "max": -5 },
        "negative_interactions": { "min": 3 }
      },
      "combat_effects": {
        "adjacent_penalty": { "attack_roll": -1, "description": "邻接攻击-1命中" },
        "competitive_fire": { "description": "对宿敌已标记的击杀目标伤害+2" }
      },
      "tavern_effects": {
        "argument_events": { "frequency_increase": 2.0 },
        "duel_challenge": { "can_trigger": true, "cooldown": "5 adventures" }
      },
      "events": ["evt_rivals_argument", "evt_rivals_duel", "evt_rivals_grudging_respect"]
    },
    "mentor_student": {
      "name": "师徒",
      "name_en": "Mentor-Student",
      "trigger_conditions": {
        "relationship_value": { "min": 5 },
        "training_event_completed": true,
        "level_difference": { "min": 2 }
      },
      "combat_effects": {
        "shared_proficiency": { "description": "每遭遇1次，徒弟可使用师傅的1个技能熟练项" }
      },
      "tavern_effects": {
        "training_events": { "frequency_increase": 3.0 },
        "xp_share": { "description": "共同冒险时，徒弟获得额外5% XP" }
      },
      "events": ["evt_mentor_teaching", "evt_student_gratitude", "evt_mentor_proud"]
    },
    "trauma_bond": {
      "name": "创伤牵绊",
      "name_en": "Trauma Bond",
      "trigger_conditions": {
        "witnessed_ally_near_death": true,
        "previous_relationship_value_exceeded": 5,
        "then_critical_event_occurred": true
      },
      "combat_effects": {
        "avoidance": { "description": "当触发角色HP<25%时，有30%概率该角色本回合无法行动(创伤闪回)" },
        "protectiveness": { "description": "当另一角色HP<25%时，AC+1" }
      },
      "tavern_effects": {
        "flashback_events": { "frequency": "每4次冒险1次" },
        "comfort_mechanic": { "description": "玩家可通过对话选择安抚创伤角色" }
      },
      "events": ["evt_trauma_flashback", "evt_trauma_comfort", "evt_trauma_healing"]
    }
  }
}
```

### 7.3 关系事件数据模型

```json
{
  "event_id": "evt_comrade_sparring",
  "event_type": "relationship",
  "relationship_type": "comrade",
  "trigger_condition": "relationship_label_acquired OR relationship_value_crossed_5",
  "participants": {
    "primary": "character_a_id",
    "secondary": "character_b_id"
  },
  "scenes": [
    {
      "scene_id": "tavern_sparring_yard",
      "scene_description_template": "{A} 和 {B} 在酒馆后院的训练场切磋武艺。{A} 手持{weapon_a}，{B} 则握着{weapon_b}。",
      "dialogue_available": true,
      "mechanical_outcomes": [
        { "type": "relationship_change", "value": 1 },
        { "type": "xp_gain", "value": 50, "target": "both" }
      ],
      "llm_prompt": "战友切磋训练场景。请描述{A}和{B}的简短战斗训练对话(3-4句)。"
    }
  ]
}
```

### 7.4 关系值衰减系统

```
关系衰减规则 (Relationship Decay):
────────────────────────────────────
非互动衰减:
  - 连续2次冒险未在同一队伍: -1
  - 连续5次冒险未在同一队伍: -2
  - 角色在酒馆但不在冒险队伍中: 无衰减 (酒馆中有互动)
  - 角色死亡: 关系值冻结

衰减豁免:
  - 关系标签为 "lovers" / "mentor_student": 衰减减半 (取整)
  - "trauma_bond": 不减 (创伤不会遗忘)
  - 酒馆Lv6+: 衰减速度-50%
  - 酒馆Lv9+: 不再衰减
```

---

## 8. 对话系统架构

### 8.1 对话树结构

对话树采用**有向图 (directed graph)** 结构，节点为对话状态，边为玩家选择。

```json
{
  "dialogue_tree_id": "dt_barkeep_maria",
  "npc_id": "npc_fixed_barkeep_maria",
  "root_node_id": "node_greeting",
  "nodes": {
    "node_greeting": {
      "node_id": "node_greeting",
      "node_type": "speech",
      "speaker": "npc",
      "conditions_to_show": [],
      "text_template": "欢迎回来，{player_name}！要来一杯麦酒吗？还是想打听点什么？",
      "dynamic_text": true,
      "choices": [
        { "choice_id": "choice_ask_adventures", "target_node": "node_adventure_gossip",
          "choice_type": "speech", "label_template": "最近有什么冒险的消息？" },
        { "choice_id": "choice_ask_characters", "target_node": "node_character_gossip",
          "choice_type": "speech", "label_template": "酒馆里的人最近怎么样？" },
        { "choice_id": "choice_order_drink", "target_node": "node_order_drink",
          "choice_type": "speech", "label_template": "来一杯。" },
        { "choice_id": "choice_leave", "target_node": "node_goodbye",
          "choice_type": "speech", "label_template": "只是路过。" }
      ]
    },
    "node_adventure_gossip": {
      "node_id": "node_adventure_gossip",
      "node_type": "speech",
      "speaker": "npc",
      "conditions_to_show": [{ "condition": "has_active_quests", "operator": ">" }],
      "text_template": "哦，最近有几个有趣的消息...{gossip_text}",
      "dynamic_text": true,
      "llm_context": {
        "prompt_type": "bartender_gossip",
        "variables": ["player_name", "world_events", "active_quests"]
      },
      "choices": [
        { "choice_id": "choice_accept_quest_hint", "target_node": "node_quest_detail",
          "choice_type": "branch", "label_template": "告诉我更多。[接取任务]" },
        { "choice_id": "choice_back", "target_node": "node_greeting",
          "choice_type": "speech", "label_template": "知道了，谢谢。" }
      ]
    },
    "node_character_gossip": {
      "node_id": "node_character_gossip",
      "node_type": "speech",
      "speaker": "npc",
      "conditions_to_show": [{ "condition": "roster_size", "operator": ">=", "value": 2 }],
      "text_template": "让我想想...{character_gossip_text}",
      "dynamic_text": true,
      "llm_context": {
        "prompt_type": "bartender_character_gossip",
        "variables": ["roster_characters", "recent_relationships_changes"]
      },
      "choices": [
        { "choice_id": "choice_back", "target_node": "node_greeting",
          "choice_type": "speech", "label_template": "有意思。" }
      ]
    },
    "node_order_drink": {
      "node_id": "node_order_drink",
      "node_type": "check",
      "speaker": "system",
      "text_template": "一杯麦酒要5枚铜板。",
      "check": {
        "type": "gold_check",
        "amount": 5,
        "success_node": "node_drink_served",
        "failure_node": "node_no_money"
      }
    },
    "node_drink_served": {
      "node_id": "node_drink_served",
      "node_type": "speech",
      "speaker": "npc",
      "text_template": "咕噜咕噜——啊，还是这里的麦酒最对味。",
      "dynamic_text": false,
      "effects": [
        { "type": "gold_change", "amount": -5 },
        { "type": "temp_buff", "buff_id": "buff_tipsy", "description": "微醺: +1 CHA检定，持续到下次冒险开始" }
      ],
      "choices": [
        { "choice_id": "choice_back", "target_node": "node_greeting",
          "choice_type": "speech", "label_template": "好的。" }
      ]
    },
    "node_no_money": {
      "node_id": "node_no_money",
      "node_type": "speech",
      "speaker": "npc",
      "text_template": "没钱？没关系，这杯算我请你的。下次记得带来好消息。",
      "dynamic_text": false,
      "effects": [
        { "type": "temp_buff", "buff_id": "buff_tipsy", "description": "微醺: +1 CHA检定，持续到下次冒险开始" }
      ],
      "choices": [
        { "choice_id": "choice_back", "target_node": "node_greeting",
          "choice_type": "speech", "label_template": "谢谢，玛莉亚。" }
      ]
    },
    "node_goodbye": {
      "node_id": "node_goodbye",
      "node_type": "end",
      "speaker": "npc",
      "text_template": "注意安全。酒馆的门永远为冒险者敞开。",
      "dynamic_text": false,
      "choices": []
    }
  }
}
```

### 8.2 对话节点类型

| 节点类型 | 描述 | speaker | 示例 |
|----------|------|---------|------|
| `speech` | NPC或玩家的纯文本对话 | `npc` / `player` | NPC说话 |
| `choice` | 玩家选择节点 (枢纽) | `player` | 选项列表 |
| `check` | 技能/属性/金币检定 | `system` | DC 15 说服检定 |
| `branch` | 条件分支 (自动跳转) | `system` | 检查关系值是否达标 |
| `end` | 对话结束 | `system` | 退出对话UI |

### 8.3 LLM填充对话节点

对话节点的 `text_template` 中的 `{variable}` 占位符由程序填充（如玩家名字、金币数）。`dynamic_text: true` 标记的节点在运行时调用 **DM Agent** 实时生成叙事文本。

```
Dynamic Text 流程:
  1. 程序组装 DM Agent 输入 (npc_context + world_state + player_history)
  2. 调用 LLMGateway → DM Agent
  3. 验证输出 (长度、格式)
  4. 在对话UI中渲染
  5. 缓存结果 (相同NPC + 相同情境 → 命中缓存)
```

### 8.4 对话后果系统

对话选择的效果分为以下几类，均由**程序控制**：

| 效果类型 | effect_type | 示例 |
|----------|-------------|------|
| 关系变化 | `relationship_change` | 玩家支持NPC A → A关系+1, B关系-1 |
| 金币变化 | `gold_change` | 买酒 -5 GP |
| 任务触发 | `quest_trigger` | 接受个人请求 → 生成side_quest |
| 物品获取 | `item_grant` | NPC赠送物品 |
| 状态Buff | `buff_apply` | 喝酒后微醺buff |
| 情报解锁 | `knowledge_unlock` | 得知隐藏冒险位置 |
| 设施解锁 | `facility_unlock` | 特殊对话解锁新服务 |

### 8.5 对话状态持久化

```json
{
  "dialogue_state": {
    "npc_id": "npc_fixed_barkeep_maria",
    "visited_node_ids": ["node_greeting", "node_adventure_gossip"],
    "chosen_choice_ids": ["choice_ask_adventures", "choice_back"],
    "last_conversation_time": "2026-05-04T18:00:00Z",
    "conversation_count": 5,
    "topics_discussed": ["adventures", "characters"],
    "player_reputation_with_npc": 3,
    "npc_memory": [
      {
        "memory_id": "mem_001",
        "topic": "player_helped_find_lost_ring",
        "content": "玩家曾帮我找回过丢失的戒指。",
        "importance": 5,
        "decay_rate": 0.1
      }
    ]
  }
}
```

### 8.6 NPC记忆系统

NPC记忆 (NPC Memory) 是一个长期存储系统，记录NPC对玩家的重要记忆：

```
记忆规则:
─────────
记忆类型:
  - player_helped: 玩家帮助了NPC (正面)
  - player_betrayed: 玩家背叛/欺骗了NPC (负面)
  - player_shared_secret: 玩家分享了秘密
  - quest_completed: 完成NPC发布的Quest
  - player_gift: 玩家送给NPC礼物
  - witnessed_event: NPC目睹了玩家的行为

记忆重要性 (importance: 1-10):
  ≥ 7: 永久记忆 (不会遗忘)
  4-6: 长期记忆 (decay_rate 0.05 per adventure)
  1-3: 短期记忆 (decay_rate 0.15 per adventure)

重要性 < 1时，记忆被"遗忘" (不再在对话中引用)。

LLM注入: 在对话生成时，NPC的当前有效记忆作为上下文注入。
```

### 8.7 对话UI流

```
对话UI布局:
┌───────────────────────────────────────────┐
│  ┌─────────────────────────────────────┐  │
│  │ [NPC头像] 玛莉亚 (酒馆老板)          │  │
│  │ "欢迎回来，亚瑟！要来一杯麦酒吗？     │  │
│  │  还是想打听点什么？"                  │  │
│  └─────────────────────────────────────┘  │
│                                           │
│  ┌─ 选择 ──────────────────────────────┐  │
│  │ ▶ 最近有什么冒险的消息？             │  │
│  │   酒馆里的人最近怎么样？             │  │
│  │   来一杯。 (5 GP) [钱袋图标]         │  │
│  │   只是路过。                         │  │
│  └─────────────────────────────────────┘  │
│                                           │
│  ┌─ 检定提示 (仅在check节点出现) ──────┐  │
│  │ 需要: 说服 DC 14                     │  │
│  │ 你的加值: +5                         │  │
│  │ 成功概率: 60% [骰子图标]             │  │
│  └─────────────────────────────────────┘  │
│                                           │
│  [关系: 友好 (3)] [金币: 350 GP]          │
└───────────────────────────────────────────┘
```

---

## 9. 训练系统

### 9.1 训练总览

训练系统允许玩家消耗金币和时间在酒馆中提升角色能力。

| 训练类型 | 需求区域 | 解锁酒馆Lv | 效果 | 耗时 | 费用 |
|----------|:---:|:---:|------|:---:|------|
| 技能训练 | 图书馆 | Lv5 | 获得指定技能熟练项 | 3天 | 见表9.2 |
| 专长学习 | 图书馆 | Lv5 | 学习1个新专长 | 7天 | 见表9.3 |
| 职业解锁 | 图书馆 | Lv5 | 解锁新职业 (可创建) | 10天 + 特殊任务 | 见表9.4 |
| 等级训练 | 训练师NPC | Lv5 | 花费金币代替XP | 1天/级 | 见表9.5 |
| 技能专精 | 训练师NPC | Lv7 | 已有熟练技能→专精 | 5天 | 1,500 GP |
| 属性训练 | 训练师NPC | Lv8 | 单属性+1 (上限18) | 10天 | 5,000 GP |

### 9.2 技能训练费用表

| 技能 | 所需天数 | 金币 | 前置条件 |
|------|:---:|:----:|----------|
| 任意已开放技能 | 3天 | 300 GP | 图书馆解锁 |
| 语言学习 (common以外) | 5天 | 500 GP | 图书馆解锁 |
| 工具熟练 (Smithing/Alchemy/etc) | 5天 | 600 GP | 对应设施解锁 |
| 武器熟练 (单类) | 3天 | 400 GP | 铁匠铺解锁 |
| 护甲熟练 (轻/中/重) | 5天 | 800/1200/2000 GP | 铁匠铺解锁 |

### 9.3 专长学习费用表

| 专长 | 所需天数 | 金币 | 前置条件 |
|------|:---:|:----:|----------|
| Great Weapon Master | 7天 | 2,000 GP | STR 15+, 军用近战武器熟练 |
| Sharpshooter | 7天 | 2,000 GP | DEX 15+, 远程武器熟练 |
| War Caster | 5天 | 1,500 GP | 能施法 |
| Tough | 5天 | 1,500 GP | 无 |
| Lucky | 7天 | 2,500 GP | 无 |
| Alert | 5天 | 1,200 GP | 无 |
| Mobile | 5天 | 1,200 GP | DEX 13+ |
| Sentinel | 7天 | 2,000 GP | STR 13+ |
| Shield Master | 5天 | 1,500 GP | Shield熟练 |
| Dual Wielder | 5天 | 1,500 GP | DEX 13+ |

完整专长列表见 Character System §2.7.3。

### 9.4 职业解锁

| 新职业 | 所需天数 | 金币 | 前置任务ID | 其他前置条件 |
|--------|:---:|:----:|-----------|-------------|
| Cleric (牧师) | 10天 | 3,000 GP | `quest_cleric_initiation` | Temple解锁 |
| Ranger (游侠) | 10天 | 2,500 GP | `quest_ranger_trial` | 完成一次森林主题冒险 |
| Paladin (圣武士) | 10天 | 3,500 GP | `quest_paladin_oath` | Temple解锁, CHA 13+ |
| Sorcerer (术士) | 5天 | 2,000 GP | 无 (天赋觉醒) | CHA 13+, 随机触发 |
| Bard (吟游诗人) | 7天 | 2,000 GP | `quest_bard_audition` | CHA 13+ |
| Druid (德鲁伊) | 10天 | 2,500 GP | `quest_druid_circle` | 完成一次自然主题冒险 |
| Monk (武僧) | 10天 | 2,000 GP | `quest_monk_discipline` | DEX 13+, WIS 13+ |
| Warlock (邪术师) | 5天 | 2,000 GP | `quest_warlock_pact` | CHA 13+, 图书馆Lv5 |
| Barbarian (野蛮人) | 7天 | 1,500 GP | `quest_barbarian_rage` | STR 13+ |

### 9.5 等级训练 (Buy XP)

```
花费金币换取经验值:
  gold_cost = next_level_xp × 0.25

示例:
  Lv1→Lv2: 300 × 0.25 = 75 GP
  Lv3→Lv4: 2700 × 0.25 = 675 GP
  Lv5→Lv6: 14000 × 0.25 = 3500 GP

限制:
  - 每天最多训练1级
  - 通过训练获得的XP不附带任何冒险记忆/事件
  - 训练角色在训练期间不可参加冒险
```

### 9.6 训练事件

训练期间可能触发LLM生成的特殊场景：

```
训练事件 (Training Events):
  概率: 每次训练完成时 15% 概率触发
  类型:
    - 突破性领悟: XP额外 +10%
    - 意外受伤: 训练延期1天
    - 导师赏识: 关系+2 (如果由NPC训练)
    - 灵感迸发: 免费获得1个相关技能的小加成

LLM生成:
  Prompt: "角色{character_name}在{training_type}训练中..."
  输出: 2-3句的训练成果描述
```

---

## 10. 任务板系统

### 10.1 任务板数据模型

```json
{
  "quest_board": {
    "board_id": "quest_board_main",
    "area_id": "area_hall",
    "refresh_config": {
      "auto_refresh_on": "return_from_adventure",
      "available_quest_slots": 5,
      "max_slots_by_tavern_level": {
        "1": 3, "2": 4, "3": 4, "4": 5, "5": 5,
        "6": 6, "7": 6, "8": 7, "9": 8, "10": 10
      }
    },
    "quest_generation": {
      "short_adventure_unlock": 1,
      "medium_adventure_unlock": 4,
      "long_adventure_unlock": 8,
      "premium_quest_unlock": 5,
      "legendary_quest_unlock": 9
    }
  }
}
```

### 10.2 冒险可用性规则

| 冒险类型 | 解锁酒馆Lv | 推荐角色Lv | CR范围 | 预估时长 | 基础报酬 (XP) | 基础报酬 (GP) |
|----------|:---:|:---:|:--:|:--:|:---:|:---:|
| 短冒险 | Lv1 | 1-4 | 0.125-4 | 30-45 min | 300 | 100-300 |
| 中冒险 | Lv4 | 4-10 | 2-10 | 2.5-3.5 hr | 1,500 | 500-1,500 |
| 长冒险 | Lv8 | 8-16 | 6-20 | 5-7 hr | 5,000 | 2,000-8,000 |
| 传奇冒险 | Lv9 | 12-20 | 10-30 | 8-12 hr | 12,000 | 5,000-20,000 |

### 10.3 任务板UI流

```
任务板UI流 (Quest Board UI Flow):
────────────────────────────────────

Step 1: 浏览 (Browse)
  ┌──────────────────────────────────────┐
  │ 任务板 (5/7 可用)                     │
  │                                      │
  │ ┌──────────────────────────────────┐ │
  │ │ ⚔ 被遗忘的回廊    [短冒险]        │ │
  │ │ 难度: ★★★☆☆    推荐Lv: 3         │ │
  │ │ 主题: 哥特恐怖/神秘               │ │
  │ │ "古庙之下，不该被唤醒的东西..."    │ │
  │ │ 报酬: 400 XP / 150-300 GP        │ │
  │ │ [查看详情] [接受]                │ │
  │ └──────────────────────────────────┘ │
  │ ┌──────────────────────────────────┐ │
  │ │ 🏰 矿坑幽影      [中冒险]  [NEW!] │ │
  │ │ 难度: ★★★★☆    推荐Lv: 5         │ │
  │ │ ...                               │ │
  │ └──────────────────────────────────┘ │
  └──────────────────────────────────────┘

Step 2: 查看详情 (Inspect)
  ┌──────────────────────────────────────┐
  │ 冒险详情: 被遗忘的回廊               │
  │ ──────────────────────────────────── │
  │ 类型: 短冒险                         │
  │ 难度: Medium (中等)                  │
  │ 推荐等级: 3                          │
  │ 预估时长: 35 分钟                    │
  │ 预估战斗: 3-5 场                     │
  │ 休息点: 1                            │
  │ 主要敌人: 亡灵、幽影生物              │
  │ 主题标签: 古代遗迹, 背叛, 禁忌知识    │
  │                                      │
  │ 报酬:                                │
  │  · 400 XP (基础)                     │
  │  · 200-400 GP (范围)                 │
  │  · 稀有度: Uncommon-Rare             │
  │  · 世界影响: 远山区域状态变化         │
  │                                      │
  │ [接受任务] [返回]                    │
  └──────────────────────────────────────┘

Step 3: 接受 (Accept)
  ┌──────────────────────────────────────┐
  │ 接受任务: 被遗忘的回廊               │
  │ ──────────────────────────────────── │
  │ 请选择冒险队伍 (4人):                │
  │                                      │
  │ ☑ 索林·铁锤 (Lv3 战士)              │
  │ ☑ 莉莉丝·影步 (Lv3 盗贼)            │
  │ ☑ 艾尔登·星焰 (Lv3 法师)            │
  │ ☐ 格罗姆·石拳 (Lv2 战士)            │
  │ ☐ 艾拉拉·月歌 (Lv3 精灵法师)        │
  │                                      │
  │ [出发冒险] [取消]                    │
  └──────────────────────────────────────┘

Step 4: 出发 (Depart)
  队伍从酒馆出发 → 场景转换 → 进入冒险
```

### 10.4 任务轮换规则

```
任务轮换 (Quest Rotation):
─────────────────────────────
新任务生成:
  - 每次从冒险返回时刷新
  - 保证至少替换1个旧任务
  - 新任务数 = 1-3个 (随机)

任务过期:
  - 任务在板上停留 3 次冒险后过期
  - 过期任务移到"归档任务" (可在图书馆查看)
  - 部分任务可重复接取 (如"地窖清剿")

特殊任务:
  - 角色个人请求: 不接受则3次冒险后过期
  - 世界事件任务: 在世界事件结束前可用
  - 限时任务: 1次冒险内有效，紧急高回报
  - 传奇任务: 永久可用 (一旦解锁)
```

### 10.5 高级任务类型

| 任务类型 | 描述 | 解锁条件 | 特点 |
|----------|------|----------|------|
| 角色个人任务 | 由角色个人请求触发 | 酒馆事件触发 | 完成后角色获得特质/专长 |
| 连续任务线 | 2-5个关联任务 | 完成前置任务 | 每步难度递增，最终奖励独特 |
| 阵营任务 | 代表某势力执行 | 世界事件/阵营关系 | 影响阵营声望，解锁阵营奖励 |
| 紧急任务 | 限时高回报 | 随机触发 | 1次冒险内有效，敌人更强但报酬3× |
| 传奇任务 | 终极挑战 | 酒馆Lv9 + 完成长冒险 | 唯一性，完成后世界状态重大改变 |
| 每日任务 | 可重复 | 酒馆Lv2+ | 小规模，随机生成，适合刷资源 |

---

## 11. 休息与恢复机制

### 11.1 短休与长休

酒馆提供安全环境下的休息恢复，与冒险中的休息规则一致：

| 休息类型 | 触发方式 | HP恢复 | 法术位恢复 | 条件恢复 | 其他效果 |
|----------|----------|--------|-----------|----------|----------|
| 短休 (Short Rest) | 点击"休息"按钮 (1小时) | 恢复100% HP | 恢复所有1环法术位; 邪术师恢复全部 | — | 可掷Hit Dice (上限=角色等级) |
| 长休 (Long Rest) | 点击"过夜"按钮 (8小时) | 恢复100% HP | 恢复全部法术位至最大值 | 移除1级疲乏 | Hit Dice恢复一半(最少1个); 精灵仅需4小时 |

### 11.2 Hit Dice 恢复

```
Hit Dice 恢复规则:
  短休: 可消耗Hit Dice恢复HP
    每消耗1个Hit Die: 回复 Hit Die值 + CON_mod HP
    例: Fighter d10, CON+2 → 回复 1d10+2 HP
    本游戏简化: 期望值 (6+2=8) 替代掷骰

  长休: 恢复已消耗的Hit Dice
    恢复数量 = ceil(角色等级 / 2)
    例: Lv5 → 恢复3个Hit Dice
```

### 11.3 神殿治疗服务

| 服务 | 效果 | 金币 | 神殿Lv要求 | 其他消耗 |
|------|------|:---:|:---:|------|
| 治疗轻伤 | 恢复100% HP | 50 | Lv7 | — |
| 移除诅咒 | 移除1个诅咒物品或角色诅咒 | 2,500 | Lv7 | 圣水×1 |
| 移除疾病 | 治愈疾病 | 500 | Lv7 | 草药×3 |
| 复原术 | 恢复降低的属性值 | 1,500 | Lv7 | 水晶粉×3 |
| 复活角色 | 复活死亡角色 (死亡≤7天) | 10,000 | Lv7 | 钻石×1 (价值1,000 GP) |
| 高等复活 | 复活死亡角色 (死亡≤30天) | 25,000 | Lv9 | 钻石×2 (价值5,000 GP) |
| 祝福术 | 下次冒险获得+1 全豁免 | 500 | Lv7 | — |
| 高等祝福 | 下次冒险获得+2 全豁免 | 2,000 | Lv9 | 圣水×5 |

### 11.4 出发前Buff物品

可在出发前购买/制作的临时buff：

| 物品 | 效果 | 持续时间 | 价格 (GP) | 来源 |
|------|------|:---:|:---:|------|
| 冒险家干粮 | 短休额外恢复1d6 HP | 1次冒险 | 15 | 杂货商 |
| 勇气啤酒 | 对抗恐慌豁免优势 | 下次冒险 | 20 | 杂货商/酒馆 |
| 清醒药剂 | 免疫睡眠 | 下次冒险 | 30 | 炼金店 |
| 夜视药水 | 获得60尺黑暗视觉 | 下次冒险 | 100 | 炼金店 |
| 防护药水 | AC+1 | 下次冒险首次遭遇 | 150 | 炼金店 |
| 活力圣水 | 临时HP +10 | 下次冒险首次遭遇 | 200 | 神殿商店 |

---

## 12. 角色管理与界面

### 12.1 Roster管理

```
Roster管理界面:
┌────────────────────────────────────────────────────────┐
│ 冒险者名册 (Roster)                          [7/12]    │
│ ─────────────────────────────────────────────────────  │
│                                                        │
│ ┌───┬──────────┬───────┬──────┬──────┬──────┬───────┐ │
│ │ # │ 姓名      │ 职业   │ 等级 │ HP   │ 状态  │ 操作  │ │
│ ├───┼──────────┼───────┼──────┼──────┼──────┼───────┤ │
│ │ 1 │ 索林·铁锤 │ 战士  │ Lv3  │ 28/28│ 就绪 │ [选]  │ │
│ │ 2 │ 莉莉丝    │ 盗贼  │ Lv3  │ 22/22│ 就绪 │ [选]  │ │
│ │ 3 │ 艾尔登    │ 法师  │ Lv3  │ 18/18│ 就绪 │ [选]  │ │
│ │ 4 │ 格罗姆    │ 战士  │ Lv2  │ 25/25│ 训练中│ [选] │ │
│ │ 5 │ 艾拉拉    │ 法师  │ Lv3  │ 20/20│ 轻伤 │ [选]  │ │
│ │ 6 │ 卢克      │ 游侠  │ Lv1  │ 12/12│ 就绪 │ [选]  │ │
│ │ 7 │ 维拉      │ 牧师  │ Lv4  │ 30/35│ 诅咒 │ [选]  │ │
│ └───┴──────────┴───────┴──────┴──────┴──────┴───────┘ │
│                                                        │
│ [查看角色] [解雇] [排序: 等级▼] [筛选: 全部]            │
│                                                        │
│ 当前冒险队伍: 索林, 莉莉丝, 艾尔登, [_空_]              │
│ [出发冒险]                                              │
└────────────────────────────────────────────────────────┘
```

### 12.2 角色面板

点击角色名称打开详细面板：

```
角色面板 Tabs:
  ┌──────────┬──────────┬──────────┬──────────┬──────────┐
  │ 属性     │ 装备     │ 技能     │ 关系     │ 传记     │
  └──────────┴──────────┴──────────┴──────────┴──────────┘

Tab 1: 属性 (Stats)
  - 六维属性 (STR/DEX/CON/INT/WIS/CHA) + 调整值
  - HP/AC/先攻/速度
  - 职业等级/子职业
  - 种族特性
  - 专长列表
  - 法术位 (若适用)
  - 当前条件 (conditions)
  - 伤疤 (scars)

Tab 2: 装备 (Equipment)
  - 装备槽位视图 (Main Hand/Off Hand/Armor/Helmet/...)
  - 点击槽位 → 可选替换装备
  - 背包物品列表
  - 金币
  - [装备管理] [比较物品]

Tab 3: 技能 (Skills)
  - 18项技能 + 调整值
  - 熟练标记 (●)
  - 专精标记 (★★)
  - 被动感知/调查/洞察

Tab 4: 关系 (Relationships)
  - 与其他角色关系值 + 标签
  - 关系历史 (最近5条)
  - 关系网络图 (视觉化)

Tab 5: 传记 (Biography)
  - 背景故事 (LLM生成)
  - 冒险历史 (已完成的冒险列表)
  - 击杀统计
  - 关键事件摘要
```

### 12.3 装备管理界面

```
装备管理 (Equipment Management):
  - 拖拽装备到槽位 (Drag & Drop)
  - 双击装备 → 查看详情
  - 右键装备 → 选项菜单 (装备/卸下/比较/出售/丢弃)
  - 同调物品显示金色边框
  - 灵魂绑定物品显示🔗图标
  - 耐久度条显示 (绿色→黄色→红色)
  - 损坏物品显示红色X
  - [自动装备最佳] 按钮 (P0 MVP)
```

### 12.4 关系网络视图

```
关系网络 (Relationship Network):
  以玩家为中央节点，显示所有roster角色之间的连线:
    - 绿色连线: Bonded (牵绊)
    - 蓝色连线: Friendly (友好)
    - 灰色连线: Neutral (中立)
    - 橙色连线: Hostile (不和)
    - 红色连线: Nemesis (宿敌)

  连线粗细 = |relationship_value| / 10

  悬停连线: 显示关系详情 (数值 + 标签 + 最近事件)
  点击角色节点: 打开该角色的关系面板
```

### 12.5 冒险历史

```json
{
  "character_id": "char_7a3f2b1c",
  "adventure_history": [
    {
      "adventure_id": "adv_003_forgotten_corridor",
      "adventure_name": "被遗忘的回廊",
      "adventure_type": "short",
      "date": "2026-05-04",
      "role": "frontline_tank",
      "performance": {
        "kills": 5,
        "damage_dealt": 87,
        "damage_taken": 42,
        "critical_hits": 1,
        "times_knocked_out": 0,
        "skill_checks_passed": 3,
        "skill_checks_failed": 1
      },
      "outcome": "success",
      "relationship_changes": [
        { "target": "莉莉丝·影步", "change": 2, "reason": "保护了她免受暗影仆从攻击" }
      ],
      "items_obtained": ["amulet_of_watchers"],
      "scars_acquired": [],
      "memorable_moment": "以一记暴击战锤粉碎了活化棺材，为队伍在伏击中打开了突破口"
    }
  ]
}
```

---

## 13. 测试规格

### 13.1 单元测试

#### Test 1: 酒馆等级进度

```
TEST 1: 酒馆声望XP阈值
  输入: tavern_level = 3, reputation_xp = 3,700
  预期: tavern_level == 3 (刚好在Lv3)
  输入: reputation_xp = 3,701
  预期: tavern_level == 4 (跨越到Lv4)
  边界: Lv10 上限, reputation_xp = 254,700 → 10, 不再增长

TEST 2: 区域解锁条件
  输入: tavern_level = 1, 尝试解锁 area_blacksmith
  预期: false (酒馆等级不足)
  输入: tavern_level = 2, tavern_gold = 500, 尝试解锁 area_blacksmith
  预期: true, gold → 0

TEST 3: 声望XP获取计算
  输入: 短冒险, Hard难度 → XP = 300 × 1.3 = 390
  输入: 长冒险, Deadly难度 → XP = 2,500 × 1.6 = 4,000
```

#### Test 2: 招募池生成

```
TEST 4: 基础招募池
  输入: tavern_level = 1, player_level = 3, MVP种族职业
  预期: 3个候选人, 等级分布: 1个Lv3, 2个在Lv2-4之间
  
TEST 5: 招募费计算
  输入: Lv1候选人, party_avg_level = 3
  预期: recruit_cost = 100 - 40 = 60 GP
  验证: cost > 0 (不能为负)

TEST 6: 角色重复检查
  输入: 池中已有 Human Fighter → 不再生成同组合
  预期: 候选池中无完全相同的 race+class 组合

TEST 7: Roster满员
  输入: roster_size = 6, max_roster_size = 6
  预期: recruit_character() 返回 false
```

#### Test 3: 商店交易

```
TEST 8: 卖价计算
  输入: Common 长剑 (base_value=15), 酒馆 Lv3 → 声望折扣 1.10
  预期: price = 15 × 1 × 1.10 = 16 (取整)

TEST 9: 回收价计算
  输入: Uncommon 皮甲 (base_value=10), worn(60%), 酒馆 Lv3
  预期: sell_price = 10 × 5 × 0.5 × 0.65 × 1.10 = 17 (取整)

TEST 10: 商店库存刷新
  输入: 铁匠铺, 酒馆 Lv3
  预期: 库存 = 6+d4 物品 + guaranteed_items (必定商品)
  验证: guaranteed_items 100% 出现

TEST 11: 玩家金币不足
  输入: 购买价格为100 GP的物品, player_gold = 50
  预期: purchase() 返回 false, gold 保持不变
```

#### Test 4: 事件触发条件

```
TEST 12: 事件概率分布
  输入: 10,000次模拟返回触发事件检查
  预期: 触发率 ≈ 25% (base) + 修正
  假设: 酒馆Lv6 (+10%), 返回检查 → 35%触发率

TEST 13: 事件冷却
  输入: 事件A触发 → 下次返回必定不触发A
  预期: cooldown_remaining > 0, 选择其他事件

TEST 14: 参与者不足
  输入: 事件需要2参与者, roster只有1人
  预期: 事件无法触发

TEST 15: 事件优先级
  输入: 同时满足story_event和random_event
  预期: story_event 优先触发
```

#### Test 5: 关系阈值

```
TEST 16: 阈值跨越检测
  输入: relationship_value: 4 → +2 → 6
  预期: 触发 relationship_threshold_crossed (5, up)
  输入: relationship_value: -4 → -2 → -6
  预期: 触发 relationship_threshold_crossed (-5, down)

TEST 17: 关系标签获得
  输入: value=5, coop_kills=4, adventures_together=2
  预期: 获得 "comrade" 标签

TEST 18: 关系衰减
  输入: value=6, 连续2次冒险未在同一队伍
  预期: value → 5 (无标签不衰减 - 但5以上有标签豁免)
```

#### Test 6: 对话树

```
TEST 19: 对话树遍历
  输入: 从 root_node 开始, 选择 choice_ask_adventures
  预期: 到达 node_adventure_gossip

TEST 20: 检定节点
  输入: node_order_drink, gold = 5
  预期: 到达 node_drink_served
  输入: gold = 0
  预期: 到达 node_no_money

TEST 21: 条件节点
  输入: roster_size = 0
  预期: node_character_gossip 不可见 (conditions_to_show 未满足)
```

#### Test 7: 训练系统

```
TEST 22: 训练费用
  输入: train_skill, "athletics"
  预期: cost = 300 GP, days = 3

TEST 23: 等级训练费用
  输入: current_level = 2, next_level_xp = 900
  预期: cost = 900 × 0.25 = 225 GP

TEST 24: 训练限制
  输入: 同一天训练2次
  预期: 第二次训练被拒绝
```

### 13.2 集成测试

#### Test I1: 完整酒馆流程

```
TEST I1: 完整酒馆流程 (enter → interact → recruit → depart)
──────────────────────────────────────────────────────────
Step 1: 进入酒馆
  - 加载大厅场景
  - 酒馆老板可见
  - 招募板和任务板可用

Step 2: 与招募板交互
  - 打开招募板 → 显示3个候选人
  - 检查候选人A → 查看属性
  - 招募候选人A → 扣除100 GP → 角色加入roster

Step 3: 与任务板交互
  - 打开任务板 → 显示3个可用任务
  - 查看任务详情 → 显示难度/报酬
  - 接受短冒险 → 进入队伍选择

Step 4: 组队出发
  - 从roster选择4人 → party_selected
  - 确认出发 → adventure_departed
  - 场景转换到冒险地图

Step 5: 返回酒馆
  - 冒险结算 (XP/金币/战利品)
  - 声望XP更新
  - 关系值更新 (基于冒险中互动)
  - 检查事件触发
```

#### Test I2: 事件链

```
TEST I2: 角色争吵 → 调解 → 关系恢复
  前提: 两个角色关系值 = -2
  触发: evt_character_argument
  玩家: 选择调解 (persuasion DC 14)
  成功: 关系值不变, 玩家关系+1
  失败: 关系值 -2
```

#### Test I3: 经济循环平衡

```
TEST I3: 100次短冒险经济验证
  模拟: 100次短冒险完成
  预期:
    平均声望XP: 250-400/次
    平均金币净收入: 150-400/次 (扣除酒馆花费)
    招募频率: 每2-3次冒险1个新角色
    角色平均寿命: 5-8次冒险
    酒馆Lv提升: 约15-20次冒险到Lv2, 约80-100次到Lv5
```

### 13.3 边界情况测试

| 边界情况 | 预期行为 |
|----------|----------|
| Roster满 (12人) 招募 | 提示"名册已满，请先解雇角色" |
| 金币不足招募 | [招募] 按钮灰色，悬停显示"金币不足" |
| 金币不足买物品 | [购买] 按钮灰色，显示差额 |
| 事件0个合格参与者 | 跳过事件，不触发 |
| 商店库存为0 (全部卖完) | 显示"暂时售罄" |
| 同时触发2个事件 | 优先触发优先级高的，另一个保留 |
| 最后一个角色被解雇 | 阻止解雇，提示"至少需要1个角色" |
| 角色复活时酒馆未解锁神殿 | [复活] 按钮灰色，显示"需要神殿 Lv7" |
| 长休时正在发生事件 | 事件先处理完成，再执行休息 |
| 对话中角色关系值跨越阈值 | 对话结束后触发关系事件 |

---

*文档版本: v1.0*
*创建日期: 2026-05-04*
*状态: 初始设计阶段*
*关联文档: GDD-v1.md §3, Character System, Items & Equipment System, LLM Integration Architecture*
