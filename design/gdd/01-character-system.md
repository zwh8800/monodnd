# 角色系统 — 技术设计文档

> **项目**: 酒馆与命运 (Tavern & Destiny)
> **规则基线**: DND 5e SRD（经Roguelike调整）
> **语言政策**: 游戏文本统一采用简体中文，技术标识符使用英文snake_case
> **文档版本**: v1.0
> **对应GDD版本**: GDD-v1.0

---

## 1. 概述

### 1.1 系统目的

角色系统是《酒馆与命运》的**核心数据枢纽**，负责：

- **定义角色的所有数值属性**（六维属性、衍生值、法术位、技能、专长、状态）
- **管理角色的叙事属性**（姓名、种族、性格、背景故事——由LLM生成，程序存储）
- **提供角色生命周期管理**（创建→升级→受伤→伤疤→退役/死亡→传承）
- **向其他系统提供角色数据接口**（战斗系统读取HP/AC/法术位；酒馆系统读取关系值；冒险系统读取角色状态以生成适配内容）

### 1.2 系统范围

本子系统覆盖以下功能模块：

| 模块 | 说明 | MVP (Phase 1) | Full Game |
|------|------|:---:|:---:|
| 六维属性系统 | STR/DEX/CON/INT/WIS/CHA + 调整值 | YES | YES |
| 种族系统 | 9种族，MVP 3种族 | Human/Elf/Dwarf | 全部9种族 |
| 职业系统 | 12职业，MVP 3职业 | Fighter/Wizard/Rogue | 全部12职业 |
| 等级进阶 | Lv1-20升级表、法术位表 | Lv1-5 | Lv1-20 |
| 技能系统 | 18项DND 5e技能 | YES | YES |
| 专长系统 | Feat池 | 3-5个MVP专长 | 全量专长 |
| 装备槽位 | 10槽位模型 | YES | YES |
| 关系系统 | 角色间关系值+效果 | Phase 2 | YES |
| 伤疤系统 | 永久负面/混合状态 | Phase 2 | YES |
| 知识传承 | 死亡/退役继承 | Phase 3 | YES |
| 条件追踪 | 15项DND 5e条件（14标准+疲乏） | YES | YES |

### 1.3 与其他系统的关系

```
美味吃吃吃
┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│   酒馆系统    │────▶│  角色系统     │◀────│  战斗系统     │
│  (招募/关系)  │     │  (数据中心)   │     │  (HP/AC/法术) │
└──────────────┘     └──────┬───────┘     └──────────────┘
                            │
              ┌─────────────┼─────────────┐
              ▼             ▼             ▼
        ┌──────────┐ ┌──────────┐ ┌──────────┐
        │ 冒险生成  │ │ 失败结算  │ │ LLM网关   │
        │ (读取等级) │ │ (写伤疤)  │ │ (叙事层)  │
        └──────────┘ └──────────┘ └──────────┘
```

- **酒馆系统** → 角色系统：发起招募请求、触发关系变更事件
- **战斗系统** ← 角色系统：读取HP/AC/属性调整值/法术位/豁免/技能加值
- **冒险生成系统** ← 角色系统：读取角色等级和数量以计算CR
- **失败结算系统** → 角色系统：写入伤疤、永久状态、关系变化
- **LLM网关** ← 角色系统：提供角色叙事数据作为Prompt上下文

### 1.4 关键设计决策

| 决策 | 方案 | 理由 |
|------|------|------|
| 数值层与叙事层分离 | 程序控制所有数值；LLM只负责文本 | 确保数值可控、可测试、可平衡；LLM不决定游戏逻辑 |
| 装备负重模型 | 槽位制（slot-based），非重量制（weight-based） | 减少微管理，提升Roguelike节奏；符合GDD Section 5.3 |
| 法术位系统 | 保留DND 5e原版环位表（1-9环），不采用冷却制 | GDD Section 5.2明确保留法术位表以维持资源管理深度 |
| 死亡豁免 | 双轨制：3轮无治疗=死亡（主要）+ HP=0时受伤累积2次死亡失败=死亡（次要） | GDD Section 5.4；有层次的风险递进，兼顾紧迫感与救援窗口 |
| 暴击规则 | 自然20时伤害骰取最大值，非双骰 | GDD Section 5.4；更快结算，更有"爽感" |
| 先攻规则 | 每轮重骰先攻 | GDD Section 5.4；增加不确定性与战斗节奏 |
| 疲劳系统 | 简化为3级（正常/疲乏/力竭） | GDD Section 5.3；减少6级管理负担 |
| STR-DEX平衡 | 保留house rule（每轮重骰先攻+暴击取最大值），STR获补偿：①物理伤害减免=STR_mod（钝击/挥砍/穿刺）；②推撞/擒抱DC用Athletics优势；③背包容量绑定STR（10+STR_mod×2）；④重甲AC天花板确保（板甲18≥轻甲+DEX上限17） | 2026-05-09 `/review-all-gdds`裁决：保留本游戏的节奏特性(house rule)，通过STR补偿而非削弱DEX来平衡 |

### 1.5 DND 5e SRD 范围标记

本文档中每条规则使用以下标记：

| 标记 | 含义 |
|------|------|
| `[SRD-FULL]` | 完全遵循DND 5e SRD规则，未做修改 |
| `[SRD-MODIFIED]` | 基于SRD规则，但参数/机制有调整（注明修改内容） |
| `[CUSTOM]` | 完全原创规则，不在SRD范围内 |

---

### 1.6 依赖关系 (Dependencies)

#### 1.6.1 上游依赖（本系统依赖）

| 依赖系统 | 依赖内容 | 状态 | 风险 |
|----------|----------|:----:|:----:|
| **事件总线 (EventBus)** | 系统间通信（升级信号、关系变化信号、伤疤生成信号） | ✅ 已实现 | 低 |
| **骰子系统** | 属性检定、攻击骰、豁免骰、偷袭骰 | ❌ 未设计 | **高** — 阻塞战斗系统对接 |

#### 1.6.2 下游依赖（依赖本系统的系统）

| 依赖系统 | 依赖内容 | GDD状态 |
|----------|----------|:-------:|
| **物品装备系统** | 装备槽位、属性加成、护甲计算 | ✅ `03-items-equipment.md` |
| **战斗系统** | HP/AC/属性调整值/法术位/豁免/技能加值/条件 | ✅ `04-combat-system.md` |
| **冒险生成系统** | 角色等级和数量以计算CR | ✅ `06-adventure-generation.md` |
| **酒馆系统** | 招募、关系值、角色状态 | ✅ `07-tavern-system.md` |
| **失败与成长系统** | 伤疤写入、传承点、永久状态 | ✅ `08-failure-growth.md` |
| **LLM集成网关** | 角色叙事数据作为Prompt上下文 | ✅ `02-llm-integration.md` |
| **UI系统** | 角色面板、装备界面、关系界面 | ✅ `09-ui-ux-design.md` |
| **条件效果系统** | 14种DND 5e条件的施加/移除 | ❌ 未设计 |
| **世界状态系统** | 角色对世界的影响记录 | ❌ 未设计 |
| **敌人AI系统** | 读取角色属性以决策目标 | ❌ 未设计 |
| **存档系统** | 角色数据序列化/反序列化 | ❌ 未设计 |

#### 1.6.3 跨文档对齐记录

| 本GDD章节 | 对应文档 | 对齐状态 | 备注 |
|-----------|----------|:--------:|------|
| §8.3 传承点公式 | `08-failure-growth.md` §7.6 | ✅ 已对齐 | 统一为 `floor(xp/500)` |
| §7.2 伤疤效果 | `08-failure-growth.md` §4 | ⚠️ 待对齐 | 伤疤消除成本需同步更新 |
| §2.4.1 XP获取 | `08-failure-growth.md` §2.2 | ✅ 已对齐 | XP计算由failure-growth统一管理 |
| §2.8.2 疲乏系统 | `04-combat-system.md` §9.2 | ✅ 已对齐 | 3级简化与GDD §5.3一致 |

---

### 1.7 可调参数 (Tuning Knobs)

以下是角色系统中可调整的平衡参数。调整前请参考Player Fantasy章节的设计测试。

| 参数 | 当前值 | 安全范围 | 影响面 | 调整建议 |
|------|:------:|:--------:|--------|----------|
| **标准数组** | [15,14,13,12,10,8] | 固定 | 角色基础战力 | 不建议修改 |
| **自由分配点** | 3 | 2-4 | 角色独特性 | 2点=微小差异, 4点=显著差异 |
| **关系变化值（常态）** | ±1 | ±1 | 关系发展速度 | 不建议超过±2 |
| **关系变化值（重大事件）** | ±2~3 | ±2~3 | 关系剧变感 | 超过±3会过于剧烈 |
| **关系衰减** | 无 | 0~1/30天 | 关系维护需求 | 添加1/30天衰减增加真实感 |
| **伤疤严重度权重（轻度）** | 70% | 50-80% | 轻度伤疤频率 | 降低会增加中/重度伤疤 |
| **伤疤严重度权重（中度）** | 25% | 15-35% | 中度伤疤频率 | — |
| **伤疤严重度权重（重度）** | 5% | 2-10% | 重度伤疤频率 | 超过10%会过于惩罚 |
| **传承点公式** | floor(xp/500) | floor(xp/250~1000) | 死亡遗产价值 | 分母越大，遗产越微薄 |
| **传承点: 额外金币** | 1点=50GP | 25-100GP | 起始经济优势 | — |
| **传承点: 等级+1** | 3点 | 2-5点 | 起始等级优势 | 成本越高，死亡代价越重 |
| **传承点: 绑定装备** | 5点 | 3-8点 | 装备传承 | — |
| **死亡豁免轮数** | 3轮 | 2-4轮 | 死亡紧迫感 | 2轮=极高压力, 4轮=较宽松 |
| **疲乏等级上限** | 3级 | 3-6级 | 疲劳管理负担 | 3级=简化, 6级=原版5e |
| **冒险完成奖励（短）** | 150 XP | 100-300 | 短冒险回报 | — |
| **冒险完成奖励（中）** | 750 XP | 500-1500 | 中冒险回报 | — |
| **冒险完成奖励（长）** | 2500 XP | 1500-5000 | 长冒险回报 | — |
| **MVP等级上限** | Lv5 | Lv3-Lv7 | 内容量 | Lv3=极简, Lv7=更多内容 |

---

## 1A. 玩家体验幻想 (Player Fantasy)

> **本节是所有设计决策的锚点。当任何机制与本节描述的感受冲突时，以本节为准。**

### 1A.1 核心情感承诺

当玩家打开角色面板时，他们看到的不仅是数值矩阵，而是一个**活生生的人**。

- **"我的角色是独一无二的"** — 属性值暗示着这个人的天赋与缺陷。两个同种族同职业的角色在数值上应有可感知的差异——一个矮人战士可能天赋异禀（STR 18），另一个可能体弱但坚韧（STR 13, CON 17）。差异来源于命运的骰子，而非工业化的标准分配。

- **"每一道伤疤都是一段故事"** — 伤疤是永久的、痛苦的、不可逆的。它们不是"超级英雄的起源"，而是**战争的代价**。一个被火烧过的角色会害怕火焰，而不是因此获得火焰超能力。伤疤应该让人畏惧，不该让人期待。

- **"酒馆里的这些人我舍不得"** — 角色不是数值容器，而是一个个有故事的人。关系不是"好感度计数器"，而是共同经历的沉淀。两个经常争吵但彼此信任的战友，在数值上应该反映"信任"而非"争吵"。

- **"死亡是有重量的"** — 当一个角色死去，玩家感到的不是失去了一组最优Build，而是**送走了一位老朋友**。但这位朋友的遗产——技能、知识、故事——会在下一位冒险者身上延续。传承是微薄的遗物，不是富可敌国的遗产。

### 1A.2 设计测试

当面对任何设计决策时，问自己：

1. **独特性测试**：两个同种族同职业的角色，在不看叙事数据的情况下，玩家能否区分它们？
2. **伤疤测试**：如果一个玩家说"我希望下次受伤拿到这个伤疤"，这个设计就失败了。
3. **关系测试**：系统是否能产生"两个经常争吵但彼此信任的战友"这种复杂关系？
4. **死亡测试**：角色死亡时，玩家是感到"失去了一组数据"还是"送走了一位朋友"？
5. **传承测试**：新角色是否能感受到前人的遗产，而非仅仅继承一组buff？

### 1A.3 感官锚点

- **视觉**：角色sprite上的伤疤像素标记、关系界面中的人物表情、英雄之壁上的传记卷轴
- **听觉**：骰子落地的声音（DND的物理根基）、角色死亡时的沉默（比任何音效都沉重）
- **触觉**：鼠标悬停在伤疤上时弹出的故事、关系值变化时的微妙动画

---

## 2. 数据模型

### 2.1 完整角色属性块 (Character Stat Block)

#### 2.1.1 顶层Schema `[CUSTOM: 整合Schema]`

```json
{
  "character_id": "char_7a3f2b1c",
  "created_at": "2026-05-04T12:00:00Z",
  "updated_at": "2026-05-04T18:30:00Z",
  "status": "alive",

  "narrative": {
    "name": "索林·铁锤",
    "race": "dwarf",
    "gender": "male",
    "age": 67,
    "personality_tags": ["固执", "忠诚", "嗜酒"],
    "backstory": "曾是矮人王国铁炉堡的守卫队副官，在一次兽人突袭中失去了队友...",
    "appearance_description": "红棕色胡须编成三股辫，左眉有一道旧刀疤。从不脱下他的锁子甲...",
    "personal_goal": "寻找失落的神器——铁炉之心",
    "avatar_path": "assets/sprites/characters/dwarf_fighter_01.png"
  },

  "stats": {
    "level": 3,
    "xp": 1500,
    "xp_to_next": 2700,
    "proficiency_bonus": 2,
    "speed": 25,
    "initiative_modifier": 0,
    "armor_class": 16,
    "hit_points": {
      "max": 28,
      "current": 22,
      "temporary": 0
    },
    "hit_dice": {
      "type": "d10",
      "max": 3,
      "current": 2
    },
    "abilities": {
      "str": { "score": 16, "modifier": 3, "label": "力量" },
      "dex": { "score": 10, "modifier": 0, "label": "敏捷" },
      "con": { "score": 14, "modifier": 2, "label": "体质" },
      "int": { "score": 8,  "modifier": -1,"label": "智力" },
      "wis": { "score": 12, "modifier": 1, "label": "感知" },
      "cha": { "score": 13, "modifier": 1, "label": "魅力" }
    },
    "saving_throw_proficiencies": ["str", "con"],
    "death_saving_throws": {
      "rounds_without_healing": 0,
      "stable": false
    },
    "exhaustion_level": 0,
    "inspiration": false
  },

  "combat": {
    "weapon_proficiencies": ["simple", "martial"],
    "armor_proficiencies": ["light", "medium", "heavy", "shield"],
    "fighting_style": "protection"
  },

  "class_progression": {
    "primary_class": {
      "name": "fighter",
      "level": 3,
      "subclass": "champion",
      "features": [
        "fighting_style_protection",
        "second_wind",
        "action_surge",
        "improved_critical"
      ]
    },
    "multiclass": []
  },

  "race": {
    "name": "dwarf",
    "subrace": "hill_dwarf",
    "traits": ["darkvision", "dwarven_resilience", "stonecunning", "dwarven_toughness"],
    "languages": ["common", "dwarvish"]
  },

  "skills": {
    "proficiencies": [
      { "skill": "athletics", "expertise": false },
      { "skill": "intimidation", "expertise": false },
      { "skill": "perception", "expertise": false }
    ],
    "modifiers": {
      "acrobatics": 0, "animal_handling": 1, "arcana": -1, "athletics": 5,
      "deception": 1, "history": -1, "insight": 1, "intimidation": 3,
      "investigation": -1, "medicine": 1, "nature": -1, "perception": 3,
      "performance": 1, "persuasion": 1, "religion": -1,
      "sleight_of_hand": 0, "stealth": 0, "survival": 1
    }
  },

  "spellcasting": {
    "caster_level": 0,
    "spellcasting_ability": "int",
    "spell_save_dc": 8,
    "spell_attack_modifier": 0,
    "slots": {
      "1st": { "max": 0, "current": 0 }, "2nd": { "max": 0, "current": 0 },
      "3rd": { "max": 0, "current": 0 }, "4th": { "max": 0, "current": 0 },
      "5th": { "max": 0, "current": 0 }, "6th": { "max": 0, "current": 0 },
      "7th": { "max": 0, "current": 0 }, "8th": { "max": 0, "current": 0 },
      "9th": { "max": 0, "current": 0 }
    },
    "cantrips_known": [],
    "spells_known": [],
    "spells_prepared": []
  },

  "equipment": {
    "slots": {
      "main_hand": null, "off_hand": null, "armor": null,
      "helmet": null, "boots": null, "cloak": null,
      "ring_1": null, "ring_2": null, "amulet": null
    },
    "backpack": [],
    "attunements": [],
    "gold": 150
  },

  "relationships": {
    "char_player": { "value": 0, "label": "neutral", "type": "none" },
    "char_elara": { "value": 3, "label": "friendly", "type": "comrade" }
  },

  "conditions": [],
  "scars": [],
  "feats": [],

  "inheritance": {
    "inherited_from": null,
    "inherited_skill": null,
    "inherited_knowledge_tags": [],
    "legacy_points": 0
  },

  "adventure_log": {
    "adventures_completed": 0,
    "total_kills": 0,
    "total_damage_dealt": 0,
    "total_damage_taken": 0,
    "critical_hits": 0,
    "critical_fails": 0,
    "memorable_events": []
  }
}
```

#### 2.1.2 衍生值计算公式

| 衍生值 | 公式 | 示例（Lv3 Fighter / STR 16 / DEX 10 / CON 14） | SRD标记 |
|--------|------|------|:---:|
| HP最大值 | Lv1: 职业Hit Die最大值 + CON调整值<br>Lv2+: 上一级HP + Hit Die期望值 + CON调整值<br>**每级HP增加至少+1**（即使公式结果为0或负数） | Lv1: 10 + 2 = **12**<br>Lv2: 12 + 6(期望) + 2 = **20**<br>Lv3: 20 + 6 + 2 = **28**<br>极端示例: CON=1 Wizard: 6+(-5)=**1**, Lv2: max(1, 1+4-5)=**1** | `[SRD-FULL + 5e PHB p.15 minimum]` |
| AC | 基于护甲公式（见Section 2.1.3） | 锁子甲: **16** | `[SRD-FULL]` |
| 先攻调整值 | DEX调整值 + 其他来源加成 | DEX 10 -> **+0** | `[SRD-FULL]` |
| 熟练加值 | `floor((level - 1) / 4) + 2` | Lv3: `floor(2/4) + 2 = 0 + 2 =` **+2** | `[SRD-FULL]` |
| 被动感知 | `10 + WIS调整值 + (熟练则+PB)` | 熟练: `10 + 1 + 2 =` **13** | `[SRD-FULL]` |
| 被动调查 | `10 + INT调整值 + (熟练则+PB)` | 未熟练: `10 + (-1) =` **9** | `[SRD-FULL]` |
| 负重（槽位数） | 基于STR score（见Section 2.1.4） | STR 16 -> **16 背包槽** | `[CUSTOM: 槽位制替代重量制]` |
| 跳跃距离（尺） | 助跑: STR score<br>立定: STR score / 2 | 助跑: **16 尺** | `[SRD-MODIFIED: 5e原版是STR score*2]` |
| 死亡豁免 | 双轨制：①每轮rounds_without_healing+1，≥3即死亡；②HP=0时受到任何伤害→death_failures+2，≥3即死亡 | - | `[CUSTOM: 替代5e原版3成功/3失败]` |
| 法术豁免DC | `8 + 施法属性调整值 + 熟练加值` | Wizard Lv5 INT 16: `8 + 3 + 3 =` **14** | `[SRD-FULL]` |
| 法术攻击加值 | `施法属性调整值 + 熟练加值` | Wizard Lv5 INT 16: `3 + 3 =` **+6** | `[SRD-FULL]` |
| 武器攻击加值 | `熟练加值 + 力量调整值(近战) 或 敏捷调整值(远程/灵巧)` | Fighter Lv3 战斧: `2 + 3 =` **+5** | `[SRD-FULL]` |
| 武器伤害 | `武器伤害骰 + 力量调整值(近战) 或 敏捷调整值(远程/灵巧)` | 战斧: `1d8 + 3` | `[SRD-FULL]` |
| 专注检定DC | `max(10, 受到伤害值 / 2)` | 受到15伤害: `max(10, 7) =` **10**<br>受到30伤害: `max(10, 15) =` **15** | `[SRD-FULL]` |

#### 2.1.3 AC计算公式 `[SRD-FULL]`

```
无护甲: AC = 10 + DEX调整值
轻甲:   AC = 护甲基础值 + DEX调整值
中甲:   AC = 护甲基础值 + DEX调整值 (最大+2)
重甲:   AC = 护甲基础值 (DEX不加成)
盾牌:   AC += 2
```

全护甲表：

| 护甲类型 | 名称 | 基础AC | DEX加成上限 | 力量要求 | 隐匿劣势 |
|----------|------|:------:|:------------:|:--------:|:--------:|
| 无护甲 | Unarmored | 10 | 无上限 | - | - |
| 轻甲 | Padded | 11 | 无上限 | - | 是 |
| 轻甲 | Leather | 11 | 无上限 | - | - |
| 轻甲 | Studded Leather | 12 | 无上限 | - | - |
| 中甲 | Hide | 12 | +2 | - | - |
| 中甲 | Chain Shirt | 13 | +2 | - | - |
| 中甲 | Scale Mail | 14 | +2 | - | 是 |
| 中甲 | Breastplate | 14 | +2 | - | - |
| 中甲 | Half Plate | 15 | +2 | - | 是 |
| 重甲 | Ring Mail | 14 | 0 | - | 是 |
| 重甲 | Chain Mail | 16 | 0 | STR 13 | 是 |
| 重甲 | Splint | 17 | 0 | STR 15 | 是 |
| 重甲 | Plate | 18 | 0 | STR 15 | 是 |
| 盾牌 | Shield | +2 | - | - | - |

AC计算示例:
- 战士（DEX 10, STR 16, 装备Chain Mail + Shield）: AC = 16 + 2 = **18**
- 盗贼（DEX 16, STR 8, 装备Studded Leather）: AC = 12 + 3 = **15**
- 法师（DEX 14, STR 8, 无护甲, Mage Armor法术）: AC = 13 + 2 = **15**

#### 2.1.4 负重槽位模型 `[CUSTOM]`

**基础背包槽位**: 10
**力量加成**: 每点STR调整值(正数) +2槽位
**敏捷加成**: 不提供额外槽位

| STR score | STR modifier | 额外槽位 | 总槽位 |
|:---------:|:------------:|:--------:|:------:|
| 1 | -5 | 0 | 10 |
| 4-5 | -3 | 0 | 10 |
| 6-7 | -2 | 0 | 10 |
| 8-9 | -1 | 0 | 10 |
| 10-11 | 0 | 0 | 10 |
| 12-13 | +1 | 2 | 12 |
| 14-15 | +2 | 4 | 14 |
| 16-17 | +3 | 6 | 16 |
| 18-19 | +4 | 8 | 18 |
| 20+ | +5 | 10 | 20 |

**槽位规则**:
- 每个物品占用1个槽位（武器、药水、卷轴、杂物）
- 弹药（箭矢/弩矢）每20支占1槽
- 金币不占槽位（独立存储）
- 超出槽位限制 -> 移动速度减半，无法疾跑
- 装备槽不影响背包槽: 装备在身的武器/护甲/饰品不计入背包槽位

---

### 2.2 种族定义

#### 2.2.1 种族总览

| # | 种族 | MVP | 速度(尺) | 体型 | 属性加成 | 核心特质 |
|:--:|------|:---:|:--------:|:---:|----------|----------|
| 1 | Human (人类) | YES | 30 | 中型 | 全属性+1 | 多才多艺 |
| 2 | Elf (精灵) | YES | 30 | 中型 | DEX+2 | 黑暗视觉、敏锐感知、精灵血统 |
| 3 | Dwarf (矮人) | YES | **25** | 中型 | CON+2 | 黑暗视觉、矮人韧性、石工知识 |
| 4 | Halfling (半身人) | - | 25 | 小型 | DEX+2 | 幸运、勇敢、半身人灵巧 |
| 5 | Half-Orc (半兽人) | - | 30 | 中型 | STR+2, CON+1 | 黑暗视觉、坚韧不屈、凶蛮攻击 |
| 6 | Gnome (侏儒) | - | 25 | 小型 | INT+2 | 黑暗视觉、侏儒狡黠 |
| 7 | Tiefling (提夫林) | - | 30 | 中型 | CHA+2, INT+1 | 黑暗视觉、地狱抗力、地狱遗赠 |
| 8 | Dragonborn (龙裔) | - | 30 | 中型 | STR+2, CHA+1 | 龙族血统、吐息武器、伤害抗力 |
| 9 | Half-Elf (半精灵) | - | 30 | 中型 | CHA+2, 任选两项+1 | 黑暗视觉、精灵血统、多才多艺 |

#### 2.2.2 MVP种族详细数据

##### Human (人类) `[SRD-FULL]`

```json
{
  "race_id": "human", "name": "人类", "name_en": "Human",
  "mvp": true, "speed": 30, "size": "medium",
  "ability_increases": { "str": 1, "dex": 1, "con": 1, "int": 1, "wis": 1, "cha": 1 },
  "languages": ["common"], "bonus_language_choice": 1,
  "traits": [], "proficiencies": [], "darkvision_range": 0
}
```

##### Elf (精灵) `[SRD-FULL]`

```json
{
  "race_id": "elf", "name": "精灵", "name_en": "Elf",
  "mvp": true, "speed": 30, "size": "medium",
  "ability_increases": { "dex": 2 },
  "languages": ["common", "elvish"],
  "darkvision_range": 60,
  "traits": [
    { "id": "darkvision", "name": "黑暗视觉",
      "description": "60尺微光环境视为明亮，黑暗视为微光（黑白）",
      "mechanical": { "darkvision_range": 60 } },
    { "id": "keen_senses", "name": "敏锐感知",
      "description": "获得察觉技能熟练项",
      "mechanical": { "proficiency": "perception" } },
    { "id": "fey_ancestry", "name": "精灵血统",
      "description": "魅惑豁免优势，免疫魔法睡眠",
      "mechanical": { "charm_save_advantage": true, "sleep_immunity": true } },
    { "id": "trance", "name": "出神",
      "description": "每天深度冥想4小时代替8小时长休",
      "mechanical": { "long_rest_duration": 240 } }
  ],
  "subraces": {
    "high_elf": {
      "name": "高等精灵", "ability_increases": { "int": 1 },
      "traits": [
        { "id": "cantrip", "name": "戏法",
          "mechanical": { "learn_cantrip": 1, "spell_list": "wizard", "casting_ability": "int" } },
        { "id": "elf_weapon_training", "name": "精灵武器训练",
          "mechanical": { "proficiencies": ["longsword", "shortsword", "shortbow", "longbow"] } },
        { "id": "extra_language", "name": "额外语言",
          "mechanical": { "bonus_language_choice": 1 } }
      ]
    },
    "wood_elf": {
      "name": "木精灵", "ability_increases": { "wis": 1 }, "speed": 35,
      "traits": [
        { "id": "mask_of_the_wild", "name": "荒野伪装",
          "mechanical": { "hide_in_light_obscurement": true } },
        { "id": "elf_weapon_training", "name": "精灵武器训练",
          "mechanical": { "proficiencies": ["longsword", "shortsword", "shortbow", "longbow"] } }
      ]
    }
  }
}
```

##### Dwarf (矮人) `[SRD-FULL]`

```json
{
  "race_id": "dwarf", "name": "矮人", "name_en": "Dwarf",
  "mvp": true, "speed": 25, "size": "medium",
  "ability_increases": { "con": 2 },
  "languages": ["common", "dwarvish"],
  "darkvision_range": 60,
  "traits": [
    { "id": "darkvision", "name": "黑暗视觉",
      "mechanical": { "darkvision_range": 60 } },
    { "id": "dwarven_resilience", "name": "矮人韧性",
      "description": "对抗毒素豁免优势，毒素伤害抗性",
      "mechanical": { "poison_save_advantage": true, "resistance": ["poison"] } },
    { "id": "stonecunning", "name": "石工知识",
      "description": "与石制品相关的智力(历史)检定双倍熟练加值",
      "mechanical": { "skill_expertise_conditional": { "skill": "history", "condition": "stonework" } } }
  ],
  "proficiencies": ["battleaxe", "handaxe", "light_hammer", "warhammer"],
  "subraces": {
    "hill_dwarf": {
      "name": "丘陵矮人", "ability_increases": { "wis": 1 },
      "traits": [
        { "id": "dwarven_toughness", "name": "矮人坚韧",
          "description": "每升一级额外+1 HP",
          "mechanical": { "bonus_hp_per_level": 1 } }
      ]
    },
    "mountain_dwarf": {
      "name": "山脉矮人", "ability_increases": { "str": 2 },
      "traits": [
        { "id": "dwarven_armor_training", "name": "矮人护甲训练",
          "description": "获得轻甲和中甲熟练项",
          "mechanical": { "proficiencies": ["light_armor", "medium_armor"] } }
      ]
    }
  }
}
```

#### 2.2.3 完整版种族（非MVP，Phase 2+）

##### Halfling (半身人) `[SRD-FULL]`

```json
{
  "race_id": "halfling", "name": "半身人", "name_en": "Halfling",
  "speed": 25, "size": "small",
  "ability_increases": { "dex": 2 },
  "languages": ["common", "halfling"],
  "traits": [
    { "id": "lucky", "name": "幸运",
      "mechanical": { "reroll_nat1": true, "reroll_limit": "once_per_check" } },
    { "id": "brave", "name": "勇敢",
      "mechanical": { "frightened_save_advantage": true } },
    { "id": "halfling_nimbleness", "name": "半身人灵巧",
      "mechanical": { "move_through_medium_or_larger": true } }
  ],
  "subraces": {
    "lightfoot": {
      "ability_increases": { "cha": 1 },
      "traits": [{ "id": "naturally_stealthy", "name": "天生隐匿",
        "mechanical": { "hide_behind_medium_or_larger": true } }]
    },
    "stout": {
      "ability_increases": { "con": 1 },
      "traits": [{ "id": "stout_resilience", "name": "强韧抗力",
        "mechanical": { "poison_save_advantage": true, "resistance": ["poison"] } }]
    }
  }
}
```

##### Half-Orc (半兽人) `[SRD-FULL]`

```json
{
  "race_id": "half_orc", "name": "半兽人", "name_en": "Half-Orc",
  "speed": 30, "size": "medium",
  "ability_increases": { "str": 2, "con": 1 },
  "languages": ["common", "orc"], "darkvision_range": 60,
  "traits": [
    { "id": "relentless_endurance", "name": "坚韧不屈",
      "mechanical": { "survive_at_1hp": "once_per_long_rest" } },
    { "id": "savage_attacks", "name": "凶蛮攻击",
      "mechanical": { "crit_damage": "add_one_weapon_die" } },
    { "id": "menacing", "name": "威吓",
      "mechanical": { "proficiency": "intimidation" } }
  ]
}
```

##### Gnome (侏儒) `[SRD-FULL]`

```json
{
  "race_id": "gnome", "name": "侏儒", "name_en": "Gnome",
  "speed": 25, "size": "small",
  "ability_increases": { "int": 2 },
  "languages": ["common", "gnomish"], "darkvision_range": 60,
  "traits": [
    { "id": "gnome_cunning", "name": "侏儒狡黠",
      "mechanical": { "int_wis_cha_magic_save_advantage": true } }
  ],
  "subraces": {
    "forest_gnome": {
      "ability_increases": { "dex": 1 },
      "traits": [
        { "id": "natural_illusionist", "name": "天生幻术师",
          "mechanical": { "learn_cantrip": "minor_illusion", "casting_ability": "int" } },
        { "id": "speak_with_small_beasts", "name": "与小型动物交谈",
          "mechanical": { "communicate_with_small_beasts": true } }
      ]
    },
    "rock_gnome": {
      "ability_increases": { "con": 1 },
      "traits": [
        { "id": "artificers_lore", "name": "工匠知识",
          "mechanical": { "skill_expertise_conditional": { "skill": "history", "condition": "magic_alchemy_tech" } } },
        { "id": "tinker", "name": "修补匠",
          "mechanical": { "create_clockwork_toy": true, "toy_effects": ["lighter", "music_box", "clockwork_toy"] } }
      ]
    }
  }
}
```

##### Tiefling (提夫林) `[SRD-FULL]`

```json
{
  "race_id": "tiefling", "name": "提夫林", "name_en": "Tiefling",
  "speed": 30, "size": "medium",
  "ability_increases": { "cha": 2, "int": 1 },
  "languages": ["common", "infernal"], "darkvision_range": 60,
  "traits": [
    { "id": "hellish_resistance", "name": "地狱抗力",
      "mechanical": { "resistance": ["fire"] } },
    { "id": "infernal_legacy", "name": "地狱遗赠",
      "mechanical": {
        "innate_spellcasting": {
          "casting_ability": "cha",
          "spells": {
            "cantrip": "thaumaturgy",
            "1st_level_at_lv3": "hellish_rebuke",
            "2nd_level_at_lv5": "darkness"
          },
          "slots": "once_per_long_rest"
        }
      }
    }
  ]
}
```

##### Dragonborn (龙裔) `[SRD-FULL]`

```json
{
  "race_id": "dragonborn", "name": "龙裔", "name_en": "Dragonborn",
  "speed": 30, "size": "medium",
  "ability_increases": { "str": 2, "cha": 1 },
  "languages": ["common", "draconic"],
  "traits": [
    { "id": "draconic_ancestry", "name": "龙族血统",
      "mechanical": {
        "choose_ancestry": ["black","blue","brass","bronze","copper","gold","green","red","silver","white"],
        "resistance": "matches_ancestry",
        "breath_weapon": {
          "shape": "matches_ancestry",
          "damage_type": "matches_ancestry",
          "damage": "2d6_per_4_levels",
          "save_dc": "8 + CON_mod + PB",
          "save_stat": "dex_or_con",
          "area": "15ft_cone_or_5x30ft_line",
          "uses": "once_per_short_rest"
        }
      }
    }
  ],
  "ancestry_table": {
    "black":  { "damage_type": "acid",     "breath_shape": "5x30ft_line" },
    "blue":   { "damage_type": "lightning","breath_shape": "5x30ft_line" },
    "brass":  { "damage_type": "fire",     "breath_shape": "5x30ft_line" },
    "bronze": { "damage_type": "lightning","breath_shape": "5x30ft_line" },
    "copper": { "damage_type": "acid",     "breath_shape": "5x30ft_line" },
    "gold":   { "damage_type": "fire",     "breath_shape": "15ft_cone" },
    "green":  { "damage_type": "poison",   "breath_shape": "15ft_cone" },
    "red":    { "damage_type": "fire",     "breath_shape": "15ft_cone" },
    "silver": { "damage_type": "cold",     "breath_shape": "15ft_cone" },
    "white":  { "damage_type": "cold",     "breath_shape": "15ft_cone" }
  }
}
```

##### Half-Elf (半精灵) `[SRD-FULL]`

```json
{
  "race_id": "half_elf", "name": "半精灵", "name_en": "Half-Elf",
  "speed": 30, "size": "medium",
  "ability_increases": { "cha": 2 },
  "ability_increase_choices": "choose_two_different_abilities_except_cha_each_plus_1",
  "languages": ["common", "elvish"], "bonus_language_choice": 1,
  "darkvision_range": 60,
  "traits": [
    { "id": "fey_ancestry", "name": "精灵血统",
      "mechanical": { "charm_save_advantage": true, "sleep_immunity": true } },
    { "id": "skill_versatility", "name": "多才多艺",
      "mechanical": { "choose_two_skill_proficiencies": true } }
  ]
}
```

---

### 2.3 职业定义

#### 2.3.1 MVP职业总览

| 职业 | Hit Die | 主要属性 | 豁免熟练 | MVP等级上限 | 子职业(Lv3) | 定位 |
|------|:-------:|:--------:|:---------:|:-----------:|:-----------:|------|
| Fighter (战士) | d10 | STR or DEX | STR, CON | Lv5 | Champion | 前线坦克/物理输出 |
| Wizard (法师) | d6 | INT | INT, WIS | Lv5 | Evocation | 后方施法者/AOE |
| Rogue (盗贼) | d8 | DEX | DEX, INT | Lv5 | Thief | 高爆发/技能专家 |

#### 2.3.2 Fighter (战士) -- MVP `[SRD-FULL]`

Lv1-5职业进阶:

| 等级 | 熟练加值 | 特性 | 法术位 |
|:----:|:--------:|------|:------:|
| 1 | +2 | Fighting Style, Second Wind (恢复1d10+Fighter Lv HP) | - |
| 2 | +2 | Action Surge (额外1动作, 每短休1次) | - |
| 3 | +2 | Improved Critical (暴击19-20), Subclass: Champion | - |
| 4 | +2 | Ability Score Improvement (ASI) | - |
| 5 | +3 | Extra Attack (攻击动作可攻击2次) | - |

**Fighter完整Lv1-20特性表**:

| Lv | PB | 特性 | 细节 |
|:--:|:--:|------|------|
| 1 | +2 | Fighting Style, Second Wind | 战斗风格选1; 附赠回1d10+Lv HP |
| 2 | +2 | Action Surge (x1) | 额外1动作/短休 |
| 3 | +2 | Subclass: Champion, Improved Critical | 暴击阈值降至19 |
| 4 | +2 | ASI | +2任一属性或+1两项或选专长 |
| 5 | +3 | Extra Attack (x1) | 攻击动作攻击2次 |
| 6 | +3 | ASI | - |
| 7 | +3 | Remarkable Athlete | (Champion) 非熟练STR/DEX/CON检定+半个PB |
| 8 | +3 | ASI | - |
| 9 | +4 | Indomitable (x1) | 豁免失败可重骰/长休 |
| 10 | +4 | Additional Fighting Style | (Champion) 再选1战斗风格 |
| 11 | +4 | Extra Attack (x2) | 攻击动作攻击3次 |
| 12 | +4 | ASI | - |
| 13 | +5 | Indomitable (x2) | - |
| 14 | +5 | ASI | - |
| 15 | +5 | Superior Critical | (Champion) 暴击阈值降至18 |
| 16 | +5 | ASI | - |
| 17 | +6 | Action Surge (x2), Indomitable (x3) | - |
| 18 | +6 | Survivor | (Champion) 回合开始HP<半血时恢复5+CON_mod HP |
| 19 | +6 | ASI | - |
| 20 | +6 | Extra Attack (x3) | 攻击动作攻击4次 |

**Fighting Style选项**:

| 风格 | 效果 |
|------|------|
| Archery | 远程武器攻击骰+2 |
| Defense | 穿着护甲时AC+1 |
| Dueling | 单手武器且无其他武器时伤害+2 |
| Great Weapon Fighting | 双手武器伤害骰1或2时可重骰一次（必须接受第二次） |
| Protection | 5尺内敌人攻击队友时可用反应赋予劣势 |
| Two-Weapon Fighting | 副手攻击伤害骰可加属性调整值 |

**战斗风格游戏调整说明** `[CUSTOM]`:
- Protection: 原作需要盾牌，本游戏放宽为只要邻接队友即可
- Great Weapon Fighting重骰: 本游戏简化为"双手武器伤害+2"以加速结算（玩家可设置切换回原版规则）

#### 2.3.3 Wizard (法师) -- MVP `[SRD-FULL]`

Lv1-5职业进阶:

| 等级 | PB | 特性 | 1环 | 2环 | 3环 |
|:----:|:--:|------|:---:|:---:|:---:|
| 1 | +2 | Spellcasting, Arcane Recovery | 2 | - | - |
| 2 | +2 | Arcane Tradition: Evocation (Sculpt Spells) | 3 | - | - |
| 3 | +2 | Spells upgrade (2nd level slots) | 4 | 2 | - |
| 4 | +2 | ASI | 4 | 3 | - |
| 5 | +3 | Spells upgrade (3rd level slots) | 4 | 3 | 2 |

**Wizard完整Lv1-20特性表**:

| Lv | PB | Cantrips | 特性 | 1st | 2nd | 3rd | 4th | 5th | 6th | 7th | 8th | 9th |
|:--:|:--:|:--------:|------|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| 1 | +2 | 3 | Spellcasting, Arcane Recovery | 2 | - | - | - | - | - | - | - | - |
| 2 | +2 | 3 | Arcane Tradition (Evocation: Sculpt Spells) | 3 | - | - | - | - | - | - | - | - |
| 3 | +2 | 3 | - | 4 | 2 | - | - | - | - | - | - | - |
| 4 | +2 | 4 | ASI | 4 | 3 | - | - | - | - | - | - | - |
| 5 | +3 | 4 | - | 4 | 3 | 2 | - | - | - | - | - | - |
| 6 | +3 | 4 | Potent Cantrip (Evocation: 敌人戏法豁免成功仍受一半伤害) | 4 | 3 | 3 | - | - | - | - | - | - |
| 7 | +3 | 4 | - | 4 | 3 | 3 | 1 | - | - | - | - | - |
| 8 | +3 | 4 | ASI | 4 | 3 | 3 | 2 | - | - | - | - | - |
| 9 | +4 | 4 | - | 4 | 3 | 3 | 3 | 1 | - | - | - | - |
| 10 | +4 | 5 | Empowered Evocation (+INT_mod to evocation damage) | 4 | 3 | 3 | 3 | 2 | - | - | - | - |
| 11 | +4 | 5 | - | 4 | 3 | 3 | 3 | 2 | 1 | - | - | - |
| 12 | +4 | 5 | ASI | 4 | 3 | 3 | 3 | 2 | 1 | - | - | - |
| 13 | +5 | 5 | - | 4 | 3 | 3 | 3 | 2 | 1 | 1 | - | - |
| 14 | +5 | 5 | Overchannel (Evocation: 最大伤害但受2d12每法术等级) | 4 | 3 | 3 | 3 | 2 | 1 | 1 | - | - |
| 15 | +5 | 5 | - | 4 | 3 | 3 | 3 | 2 | 1 | 1 | 1 | - |
| 16 | +5 | 5 | ASI | 4 | 3 | 3 | 3 | 2 | 1 | 1 | 1 | - |
| 17 | +6 | 5 | - | 4 | 3 | 3 | 3 | 2 | 1 | 1 | 1 | 1 |
| 18 | +6 | 5 | Spell Mastery (1st+2nd at-will) | 4 | 3 | 3 | 3 | 3 | 1 | 1 | 1 | 1 |
| 19 | +6 | 5 | ASI | 4 | 3 | 3 | 3 | 3 | 2 | 1 | 1 | 1 |
| 20 | +6 | 5 | Signature Spells (2x 3rd level at-will) | 4 | 3 | 3 | 3 | 3 | 2 | 2 | 1 | 1 |

**关键特性**:

| 特性ID | 名称 | Lv | 效果 | SRD |
|--------|------|:--:|------|:---:|
| arcane_recovery | 奥术恢复 | 1 | 短休恢复总法术环位=ceil(Wizard Lv/2)，单环不超过5 | FULL |
| evocation_savant | 塑能学者 | 2 | 抄写塑能法术时间和金币减半 | FULL |
| sculpt_spells | 法术塑形 | 2 | AOE法术指定1+法环数同伴自动豁免成功+0伤害 | FULL |
| potent_cantrip | 强效戏法 | 6 | 敌人对戏法豁免成功仍受一半伤害 | FULL |
| empowered_evocation | 强能塑能 | 10 | 每次塑能法术伤害骰+INT_mod | FULL |
| overchannel | 充能过载 | 14 | 每长休1次塑能法术伤害取最大值，之后受2d12/法环坏死伤害 | FULL |

**法术书与准备法术规则** `[SRD-FULL]`:
- 1级拥有6个1环法术
- 此后每升一级可学2个新法术（必须是自己已有法术位的环位）
- 每天长休后可准备 `INT_mod + Wizard Level` 个法术
- 仪式标记的法术可不必准备而以仪式施放（仅仪式模式，耗时+10分钟）

#### 2.3.4 Rogue (盗贼) -- MVP `[SRD-FULL]`

Lv1-5职业进阶:

| 等级 | PB | 偷袭骰数 | 特性 |
|:----:|:--:|:------:|------|
| 1 | +2 | 1d6 | Expertise (2技能专精), Sneak Attack, Thieves' Cant |
| 2 | +2 | 1d6 | Cunning Action (附赠: 疾走/撤离/躲藏) |
| 3 | +2 | 2d6 | Subclass: Thief (Fast Hands, Second-Story Work) |
| 4 | +2 | 2d6 | ASI |
| 5 | +3 | 3d6 | Uncanny Dodge (反应使可见攻击伤害减半) |

**Rogue完整Lv1-20特性表**:

| Lv | PB | Sneak | 特性 | 细节 |
|:--:|:--:|:-----:|------|------|
| 1 | +2 | 1d6 | Expertise, Sneak Attack, Thieves' Cant | 2技能专精(双倍PB), 优势或有队友邻接时额外1d6 |
| 2 | +2 | 1d6 | Cunning Action | 附赠: Dash/Disengage/Hide |
| 3 | +2 | 2d6 | Subclass: Thief | Fast Hands(附赠巧手/工具/物品), 攀爬不耗额外速度, 助跑跳距+DEX_mod |
| 4 | +2 | 2d6 | ASI | - |
| 5 | +3 | 3d6 | Uncanny Dodge | 反应: 可见攻击者造成的伤害减半 |
| 6 | +3 | 3d6 | Expertise (x2 more) | - |
| 7 | +3 | 4d6 | Evasion | DEX豁免成功=0伤害, 失败=半伤害 |
| 8 | +3 | 4d6 | ASI | - |
| 9 | +4 | 5d6 | Supreme Sneak (Thief) | 半速移动时潜行优势 |
| 10 | +4 | 5d6 | ASI | - |
| 11 | +4 | 6d6 | Reliable Talent | 熟练技能的最低骰值=10 |
| 12 | +4 | 6d6 | ASI | - |
| 13 | +5 | 7d6 | Use Magic Device (Thief) | 无视职业/种族/等级限制使用魔法物品 |
| 14 | +5 | 7d6 | Blindsense | 感知10尺内隐藏/隐形生物 |
| 15 | +5 | 8d6 | Slippery Mind | WIS豁免熟练 |
| 16 | +5 | 8d6 | ASI | - |
| 17 | +6 | 9d6 | Thief's Reflexes (Thief) | 战斗第一轮获得额外一轮(先攻-10) |
| 18 | +6 | 9d6 | Elusive | 非失能时对你的攻击不能获得优势 |
| 19 | +6 | 10d6 | ASI | - |
| 20 | +6 | 10d6 | Stroke of Luck | 每短休1次: 失手->命中 或 技能失败->骰20 |

**偷袭规则** `[SRD-FULL]`:
- 触发条件: (攻击骰优势) OR (5尺内有队友且自己无劣势)
- 条件限制: 必须使用灵巧武器(finesse)或远程武器
- 伤害: 额外`ceil(Rogue Lv / 2) × d6`穿刺伤害（与武器伤害类型相同）
- 频率: 每回合1次 (注意: 每**回合**，不是每**轮**——借机攻击也可偷袭)
- 暴击: 偷袭骰也取最大值（本游戏规则）

#### 2.3.5 完整版非MVP职业（Phase 2+）

##### Cleric (牧师) `[SRD-FULL]`

| 属性 | 值 |
|------|-----|
| Hit Die | d8 |
| 主属性 | Wisdom |
| 豁免熟练 | WIS, CHA |
| 施法 | Full Caster, WIS施法, 准备法术=WIS_mod + Cleric Lv |
| 仪式施法 | 是 |
| 1级特性 | Spellcasting, Divine Domain (Life: 治疗+2+法术环位) |
| 2级 | Channel Divinity x1 (Turn Undead + Domain specific) |
| 5级 | Destroy Undead (CR 1/2) |
| 8级 | Divine Strike / Potent Spellcasting |
| 护甲熟练 | Light/Medium/Shield (+Life Domain: Heavy) |
| 武器熟练 | Simple only |

##### Ranger (游侠) `[SRD-FULL]`

| 属性 | 值 |
|------|-----|
| Hit Die | d10 |
| 主属性 | Dexterity, Wisdom |
| 豁免熟练 | STR, DEX |
| 施法 | Half Caster, WIS施法, 已知法术表 |
| 1级特性 | Favored Enemy (选一种敌人类别, 追踪和INT检定优势+1语言), Natural Explorer (选一种地形, 导航/觅食/潜行优势) |
| 2级 | Fighting Style, Spellcasting |
| 3级 | Subclass: Hunter (Colossus Slayer: 对受伤敌人额外1d8) |
| 5级 | Extra Attack |
| 技能熟练 | 3项 |

##### Paladin (圣武士) `[SRD-FULL]`

| 属性 | 值 |
|------|-----|
| Hit Die | d10 |
| 主属性 | Strength, Charisma |
| 豁免熟练 | WIS, CHA |
| 施法 | Half Caster, CHA施法, 准备法术=CHA_mod + floor(Paladin Lv/2) |
| 1级特性 | Divine Sense, Lay on Hands (Lv x5 HP池) |
| 2级 | Fighting Style, Spellcasting, Divine Smite (消耗法术位, 每环1d8+1d8额外光耀伤害, 对不死和邪魔+1d8) |
| 3级 | Subclass: Devotion (Sacred Weapon: CHA加命中) |
| 5级 | Extra Attack |
| 护甲熟练 | Light/Medium/Heavy/Shield |
| 武器熟练 | Simple/Martial |

##### Sorcerer (术士) `[SRD-FULL]`

| 属性 | 值 |
|------|-----|
| Hit Die | d6 |
| 主属性 | Charisma |
| 豁免熟练 | CON, CHA |
| 施法 | Full Caster, CHA施法, 已知法术表, 戏法: Lv1=4/Lv4=5/Lv10=6 |
| 法术之力点 | = Sorcerer Level |
| Metamagic | Lv3: 选2种 / Lv10: +1 / Lv17: +1 |
| 1级 | Spellcasting, Sorcerous Origin (Draconic: 龙语+HP+1/Lv+AC=13+DEX) |
| 已知法术 | Lv1=2, Lv2=3, Lv3=4, Lv4=5, Lv5=6, Lv6=7, Lv7=8, Lv8=9, Lv9=10, Lv10=11, Lv11=12, Lv13=13, Lv15=14, Lv17=15 |

##### Bard (吟游诗人) `[SRD-FULL]`

| 属性 | 值 |
|------|-----|
| Hit Die | d8 |
| 主属性 | Charisma |
| 豁免熟练 | DEX, CHA |
| 施法 | Full Caster, CHA施法, 已知法术表, 仪式施法 |
| 技能熟练 | 3项（可从全部技能选） |
| 1级 | Spellcasting, Bardic Inspiration (d6, Lv5=d8, Lv10=d10, Lv15=d12; 点数=CHA_mod/长休, Lv5=短休恢复) |
| 2级 | Jack of All Trades (非熟练技能检定+半PB), Song of Rest (d6短休恢复, Lv9=d8, Lv13=d10, Lv17=d12) |
| 3级 | Subclass: Lore (Cutting Words: reaction减敌人检定=Inspiration骰), Expertise |
| 5级 | Font of Inspiration (短休恢复灵感) |
| 已知法术 | Lv1=4, Lv2=5, Lv3=6, Lv4=7, Lv5=8, Lv6=9, Lv7=10, Lv8=11, Lv9=12, Lv10=14, Lv11=15, Lv13=16, Lv15=19, Lv17=20, Lv20=22 |

##### Druid (德鲁伊) `[SRD-FULL]`

| 属性 | 值 |
|------|-----|
| Hit Die | d8 |
| 主属性 | Wisdom |
| 豁免熟练 | INT, WIS |
| 施法 | Full Caster, WIS施法, 准备=WIS_mod + Druid Lv, 仪式施法 |
| 盔甲限制 | 不穿金属护甲或盾牌 |
| 1级 | Spellcasting, Druidic |
| 2级 | Wild Shape (CR 1/4, 无飞行/游泳, Lv4=CR 1/2 无飞行, Lv8=CR 1, Moon Druid更快) |
| 3级 | Subclass: Moon (Combat Wild Shape: 附赠变形, 消耗法术位恢复1d8/环位) |
| 护甲熟练 | Light/Medium/Shield (no metal) |
| 武器熟练 | Club/Dagger/Dart/Javelin/Mace/Quarterstaff/Scimitar/Sickle/Sling/Spear |

##### Monk (武僧) `[SRD-FULL]`

| 属性 | 值 |
|------|-----|
| Hit Die | d8 |
| 主属性 | Dexterity, Wisdom |
| 豁免熟练 | STR, DEX |
| 无护甲AC | 10 + DEX_mod + WIS_mod |
| 气点 | = Monk Level (短休恢复) |
| 1级 | Unarmored Defense**, Martial Arts (附赠徒手攻击, 徒手=DEX可选, MA骰: Lv1-4=d4/Lv5-10=d6/Lv11-16=d8/Lv17+=d10) |
| 2级 | Ki (疾风连击: 1气附赠2攻击; 坚强防御: 1气附赠闪避; 风之步: 1气附赠撤离+跳跃x2), Unarmored Movement (+10尺: Lv6=+15/Lv10=+20/Lv14=+25/Lv18=+30) |
| 3级 | Subclass: Open Hand, Deflect Missiles (反应减少远程伤害1d10+DEX_mod+Monk Lv) |
| 5级 | Extra Attack, Stunning Strike (1气, 震慑CON豁免) |
| 技能熟练 | 2项 |

##### Warlock (邪术师) `[SRD-FULL]`

| 属性 | 值 |
|------|-----|
| Hit Die | d8 |
| 主属性 | Charisma |
| 豁免熟练 | WIS, CHA |
| 法术位 | Pact Magic (独立): Lv2=2个/Lv11=3/Lv17=4, 短休恢复, 环位: Lv1-2=1st/Lv3-4=2nd/Lv5-6=3rd/Lv7-8=4th/Lv9+=5th |
| 已知法术 | 按Lv: 1=2/2=3/3=4/4=5/5=6/6=7/7=8/8=9/9=10/10=10/11=11/13=12/15=13/17=14/19=15 |
| 邪魔召唤 | Lv2=2/Lv5=3/Lv7=4/Lv9=5/Lv12=6/Lv15=7/Lv18=8 |
| 秘法奥术(Mystic Arcanum) | Lv11=6环/Lv13=7环/Lv15=8环/Lv17=9环，每长休1次 |

##### Barbarian (野蛮人) `[SRD-FULL]`

| 属性 | 值 |
|------|-----|
| Hit Die | d12 |
| 主属性 | Strength |
| 豁免熟练 | STR, CON |
| 1级 | Rage (x2/Lv3=3/Lv6=4/Lv12=5/Lv17=6/Lv20=无限, +2伤害/Lv9=+3/Lv16=+4, B/P/S抗性, STR检定+豁免优势, 专注中或穿重甲时不能狂暴), Unarmored Defense (AC=10+DEX_mod+CON_mod) |
| 2级 | Reckless Attack (攻击骰优势, 但对其攻击也优势), Danger Sense (DEX豁免优势对抗可见效果) |
| 3级 | Subclass: Berserker (Frenzy: 狂暴时每轮附赠1次武器攻击, 结束后获得1级疲乏) |
| 5级 | Extra Attack, Fast Movement (+10尺速度不穿重甲时) |

---

### 2.4 等级进阶总表

#### 2.4.1 熟练加值与XP阈值 `[SRD-FULL]`

| 等级 | XP需求 | 累计XP | 熟练加值 |
|:----:|:------:|:------:|:--------:|
| 1 | 0 | 0 | +2 |
| 2 | 300 | 300 | +2 |
| 3 | 900 | 1,200 | +2 |
| 4 | 2,700 | 3,900 | +2 |
| 5 | 6,500 | 10,400 | +3 |
| 6 | 14,000 | 24,400 | +3 |
| 7 | 23,000 | 47,400 | +3 |
| 8 | 34,000 | 81,400 | +3 |
| 9 | 48,000 | 129,400 | +4 |
| 10 | 64,000 | 193,400 | +4 |
| 11 | 85,000 | 278,400 | +4 |
| 12 | 100,000 | 378,400 | +4 |
| 13 | 120,000 | 498,400 | +5 |
| 14 | 140,000 | 638,400 | +5 |
| 15 | 165,000 | 803,400 | +5 |
| 16 | 195,000 | 998,400 | +5 |
| 17 | 225,000 | 1,223,400 | +6 |
| 18 | 265,000 | 1,488,400 | +6 |
| 19 | 305,000 | 1,793,400 | +6 |
| 20 | 355,000 | 2,148,400 | +6 |

**XP获取规则** (每次冒险结束结算):
- 完整XP计算模型见 [failure-growth.md §2.2 成功结算流程](./08-failure-growth.md)
- 核心逻辑：按遭遇难度计算基准XP × 参与度乘数 × 目标奖励 × 速度奖励 + 完成奖励
- 多人满额获取: 每个角色独立获取全额XP（不分摊）

> **注意**: 本系统不再定义独立的XP公式。所有XP计算由 failure-growth.md §2.2 统一管理，确保冒险结算、角色升级、酒馆进度使用同一个XP来源。

#### 2.4.2 升级流程 `[CUSTOM: 事件驱动]`

```
角色获得足够XP触发升级:

  Step 1: character.stats.level += 1

  Step 2: 更新熟练加值 (查表, floor((new_level - 1) / 4) + 2)

  Step 3: 增加HP最大值
    new_hp_max = old_hp_max + HitDie期望值(向上取整) + CON_mod
    **每级HP增加至少+1** (即使公式结果为0或负数, D&D 5e PHB p.15)
    (Hit Die期望值: d6=4, d8=5, d10=6, d12=7)
    (Lv1时: new_hp_max = HitDie最大值 + CON_mod)

  Step 4: 如果新等级有ASI:
    - emit_signal("level_up_asi", character_id)
    - 玩家选择: +2单属性 / +1两不同属性 / 选新专长
    - 重新计算所有受影响衍生值

  Step 5: 如果新等级有施法者位阶提升:
    - 查法术位表更新 spell_slots
    - 如果解锁新环位: emit_signal("unlock_spell_level", character_id, new_level)

  Step 6: 解锁新等级的职业特性(features)

  Step 7: 如果触发子职业选择:
    - emit_signal("subclass_selection", character_id)
    - 玩家选择子职业后解锁对应特性

  Step 8: 更新所有衍生值 (HP/AC/技能调整值/法术DC/攻击加值)

  Step 9: emit_signal("character_leveled_up", character_id, old_level, new_level)

  Step 10: (可选) LLM生成升级叙事文本
    Prompt: "{name} 从 Lv{old} 升到了 Lv{new}，学会了 {new_features}..."
    Output: 1-2句成长描述 (如"索林在战斗中磨练了技艺，他的剑更快了...")
```

---

### 2.5 法术位表（完整DND 5e）

#### 2.5.1 Full Caster — Wizard, Cleric, Druid, Bard, Sorcerer `[SRD-FULL]`

| Lv | PB | 1st | 2nd | 3rd | 4th | 5th | 6th | 7th | 8th | 9th |
|:--:|:--:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| 1 | +2 | 2 | - | - | - | - | - | - | - | - |
| 2 | +2 | 3 | - | - | - | - | - | - | - | - |
| 3 | +2 | 4 | 2 | - | - | - | - | - | - | - |
| 4 | +2 | 4 | 3 | - | - | - | - | - | - | - |
| 5 | +3 | 4 | 3 | 2 | - | - | - | - | - | - |
| 6 | +3 | 4 | 3 | 3 | - | - | - | - | - | - |
| 7 | +3 | 4 | 3 | 3 | 1 | - | - | - | - | - |
| 8 | +3 | 4 | 3 | 3 | 2 | - | - | - | - | - |
| 9 | +4 | 4 | 3 | 3 | 3 | 1 | - | - | - | - |
| 10 | +4 | 4 | 3 | 3 | 3 | 2 | - | - | - | - |
| 11 | +4 | 4 | 3 | 3 | 3 | 2 | 1 | - | - | - |
| 12 | +4 | 4 | 3 | 3 | 3 | 2 | 1 | - | - | - |
| 13 | +5 | 4 | 3 | 3 | 3 | 2 | 1 | 1 | - | - |
| 14 | +5 | 4 | 3 | 3 | 3 | 2 | 1 | 1 | - | - |
| 15 | +5 | 4 | 3 | 3 | 3 | 2 | 1 | 1 | 1 | - |
| 16 | +5 | 4 | 3 | 3 | 3 | 2 | 1 | 1 | 1 | - |
| 17 | +6 | 4 | 3 | 3 | 3 | 2 | 1 | 1 | 1 | 1 |
| 18 | +6 | 4 | 3 | 3 | 3 | 3 | 1 | 1 | 1 | 1 |
| 19 | +6 | 4 | 3 | 3 | 3 | 3 | 2 | 1 | 1 | 1 |
| 20 | +6 | 4 | 3 | 3 | 3 | 3 | 2 | 2 | 1 | 1 |

#### 2.5.2 Half Caster — Paladin, Ranger `[SRD-FULL]`

| Lv | 1st | 2nd | 3rd | 4th | 5th |
|:--:|:---:|:---:|:---:|:---:|:---:|
| 1 | - | - | - | - | - |
| 2 | 2 | - | - | - | - |
| 3 | 3 | - | - | - | - |
| 4 | 3 | - | - | - | - |
| 5 | 4 | 2 | - | - | - |
| 6 | 4 | 2 | - | - | - |
| 7 | 4 | 3 | - | - | - |
| 8 | 4 | 3 | - | - | - |
| 9 | 4 | 3 | 2 | - | - |
| 10 | 4 | 3 | 2 | - | - |
| 11 | 4 | 3 | 3 | - | - |
| 12 | 4 | 3 | 3 | - | - |
| 13 | 4 | 3 | 3 | 1 | - |
| 14 | 4 | 3 | 3 | 1 | - |
| 15 | 4 | 3 | 3 | 2 | - |
| 16 | 4 | 3 | 3 | 2 | - |
| 17 | 4 | 3 | 3 | 3 | 1 |
| 18 | 4 | 3 | 3 | 3 | 1 |
| 19 | 4 | 3 | 3 | 3 | 2 |
| 20 | 4 | 3 | 3 | 3 | 2 |

#### 2.5.3 Warlock (邪术师) Pact Magic `[SRD-FULL]`

| Lv | Slots | Slot Level | Spells Known | Cantrips | Invocations |
|:--:|:-----:|:----------:|:------------:|:--------:|:-----------:|
| 1 | 1 | 1st | 2 | 2 | - |
| 2 | 2 | 1st | 3 | 2 | 2 |
| 3 | 2 | 2nd | 4 | 2 | 2 |
| 4 | 2 | 2nd | 5 | 3 | 2 |
| 5 | 2 | 3rd | 6 | 3 | 3 |
| 6 | 2 | 3rd | 7 | 3 | 3 |
| 7 | 2 | 4th | 8 | 3 | 4 |
| 8 | 2 | 4th | 9 | 3 | 4 |
| 9 | 2 | 5th | 10 | 3 | 5 |
| 10 | 2 | 5th | 10 | 4 | 5 |
| 11 | 3 | 5th | 11 + MA 6th | 4 | 5 |
| 12 | 3 | 5th | 11 | 4 | 6 |
| 13 | 3 | 5th | 12 + MA 7th | 4 | 6 |
| 14 | 3 | 5th | 12 | 4 | 6 |
| 15 | 3 | 5th | 13 + MA 8th | 4 | 7 |
| 16 | 3 | 5th | 13 | 4 | 7 |
| 17 | 4 | 5th | 14 + MA 9th | 4 | 7 |
| 18 | 4 | 5th | 14 | 4 | 8 |
| 19 | 4 | 5th | 15 | 4 | 8 |
| 20 | 4 | 5th | 15 | 4 | 8 |

- MA = Mystic Arcanum, 每长休1次
- 邪术师法术位短休恢复（与GDD Section 5.4的法术位恢复调整一致）

#### 2.5.4 法术位恢复机制 `[SRD-MODIFIED: GDD Section 5.4]`

| 恢复方式 | 本游戏规则 | DND 5e原版差异 |
|----------|-----------|---------------|
| Short Rest (短休) | 恢复**一半**1环法术位(向上取整), 邪术师恢复全部 | 保留资源管理深度 |
| Long Rest (长休) | 恢复全部法术位至最大值 | 同 |
| Arcane Recovery (Wizard) | 短休额外恢复 ceil(Level/2) 总环位, 单环不超过5 | 同 |

计算示例:
- Lv5 Wizard短休: 恢复1环位全部(4个) + Arcane Recovery恢复 ceil(5/2)=3环(可选1环+2环)
- Lv5 Warlock短休: 恢复全部2个3环位
- Lv5 Cleric短休: 恢复1环位全部(4个), 2-3环在长休才恢复

---

### 2.6 技能系统

#### 2.6.1 全部18项技能与属性关联 `[SRD-FULL]`

| # | 技能 (Skill) | 中文名 | 关联属性 | 常见用途 | 未熟练可用 |
|:--:|-------------|--------|:--------:|----------|:----------:|
| 1 | Acrobatics | 特技 | **DEX** | 平衡、翻滚、脱离擒抱 | YES |
| 2 | Animal Handling | 驯兽 | **WIS** | 安抚动物、控制坐骑 | YES |
| 3 | Arcana | 奥秘 | **INT** | 辨识法术、魔法物品 | YES |
| 4 | Athletics | 运动 | **STR** | 攀爬、跳跃、游泳 | YES |
| 5 | Deception | 欺瞒 | **CHA** | 撒谎、伪装 | YES |
| 6 | History | 历史 | **INT** | 历史事件、古代文明 | YES |
| 7 | Insight | 洞悉 | **WIS** | 识破谎言、揣摩意图 | YES |
| 8 | Intimidation | 威吓 | **CHA** | 胁迫、恐吓 | YES |
| 9 | Investigation | 调查 | **INT** | 搜索线索、推理 | YES |
| 10 | Medicine | 医疗 | **WIS** | 急救、诊断病情 | YES |
| 11 | Nature | 自然 | **INT** | 动植物辨认、地形 | YES |
| 12 | Perception | 察觉 | **WIS** | 发现隐藏、听到动静 | YES |
| 13 | Performance | 表演 | **CHA** | 演奏、演讲、舞蹈 | YES |
| 14 | Persuasion | 说服 | **CHA** | 谈判、交涉 | YES |
| 15 | Religion | 宗教 | **INT** | 神祇、仪式、圣物 | YES |
| 16 | Sleight of Hand | 巧手 | **DEX** | 扒窃、藏物、开锁 | YES |
| 17 | Stealth | 隐匿 | **DEX** | 潜行、躲藏 | YES |
| 18 | Survival | 生存 | **WIS** | 追踪、觅食、导航 | YES |

所有技能均可用于未熟练检定（unskilled check），无"必须熟练才能尝试"的限制。

#### 2.6.2 技能调整值计算公式

```
if proficient:
  skill_bonus = ability_modifier + proficiency_bonus
  if expertise (专精):
    skill_bonus = ability_modifier + (proficiency_bonus * 2)

if not proficient:
  skill_bonus = ability_modifier

passive_score = 10 + skill_bonus
Passive Perception = 10 + WIS_mod + (if proficient in perception -> +PB)
Passive Investigation = 10 + INT_mod + (if proficient -> +PB)
Passive Insight = 10 + WIS_mod + (if proficient -> +PB)
```

计算示例:
- Lv3 Fighter, STR 16(+3), Athletics熟练, PB=2: +3+2 = **+5**
- Lv5 Rogue, DEX 18(+4), Stealth专精, PB=3: +4+(3x2) = **+10**
- Lv1 Wizard, INT 16(+3), Arcana未熟练: 仅 = **+3** (未熟练不加PB)

#### 2.6.3 Jack of All Trades (Bard Lv2) `[SRD-FULL]`

未熟练技能: `ability_modifier + floor(PB / 2)` (向下取整)
- PB=2时: ability_mod + 1
- PB=4时: ability_mod + 2
- PB=6时: ability_mod + 3

---

### 2.7 专长系统 (Feat System)

#### 2.7.1 专长获取时机 `[SRD-MODIFIED]`

- 职业ASI等级时选择: **+2单一属性** 或 **+1两项不同属性** 或 **选择1个专长**
- MVP阶段: 5个可选专长
- 完整版: 30+专长

#### 2.7.2 MVP专长 (5个)

| Feat ID | 名称 | 前置 | 效果 |
|---------|------|------|------|
| great_weapon_master | 巨武器大师 | 熟练军用近战武器 | (主动) 双手/两用武器攻击-5命中+10伤害; (被动) 暴击或将目标降至0HP时可附赠攻击 |
| sharpshooter | 神射手 | 熟练远程武器 | (主动) 远程攻击-5命中+10伤害; (被动) 远程无视半掩体和3/4掩体; 长射程不劣势 |
| war_caster | 战斗施法者 | 能施法 | 专注豁免优势; 持握武器/盾牌可满足姿势成分; 借机攻击可施单目标法术 |
| tough | 坚韧 | 无 | HP上限+2/每级(含已获得等级), 以后每级额外+2 |
| lucky | 幸运 | 无 | 每长休3点, 攻击/技能/豁免可消耗1点多骰1d20选结果 |

#### 2.7.3 完整版专长扩展池 (Phase 2+)

30个专长总表:

| # | Feat ID | 名称 | 前置 | 效果概述 |
|:--:|---------|------|------|----------|
| 1 | alert | 警觉 | - | 先攻+5, 不被突袭, 隐形敌人对你无优势 |
| 2 | athlete | 运动健将 | - | STR/DEX+1, 站立仅5尺, 攀爬速度=行走速度 |
| 3 | actor | 演员 | - | CHA+1, 模仿声音优势, 伪装他人优势 |
| 4 | charger | 冲撞者 | - | 疾跑后附赠攻击或推撞(攻击+5或推撞DC+5) |
| 5 | crossbow_expert | 弩专家 | - | 无视装填, 5尺内远程无劣势, 攻击后附赠手弩攻击 |
| 6 | defensive_duelist | 防守决斗者 | DEX 13+ | 反应: 灵巧武器加PB到AC对抗该次攻击 |
| 7 | dual_wielder | 双武器战斗者 | - | 可双持非轻武器, 双持+1AC, 拔双武器仅1次交互 |
| 8 | dungeon_delver | 地城探索者 | - | 察觉/调查陷阱密门优势, 陷阱豁免优势, 搜索陷阱快速 |
| 9 | durable | 强韧 | - | CON+1, Hit Die恢复下限=2xCON_mod |
| 10 | elemental_adept | 元素精熟 | 施法能力 | 选1元素: 该元素法术无视抗力, 伤害骰1视为2 |
| 11 | heavily_armored | 重甲专家 | 中甲熟练 | STR+1, 获得重甲熟练 |
| 12 | heavy_armor_master | 重甲大师 | 重甲熟练 | STR+1, 穿重甲时非魔法B/P/S伤害-3 |
| 13 | inspiring_leader | 激励领袖 | CHA 13+ | 10分钟演讲后6友方获临时HP=Lv+CHA_mod |
| 14 | keen_mind | 敏锐心智 | - | INT+1, 永远知北方, 知日出日落, 准记1月记忆 |
| 15 | lightly_armored | 轻甲专家 | - | STR/DEX+1, 获得轻甲熟练 |
| 16 | linguist | 语言学家 | - | INT+1, 学3语言, 可写密码 |
| 17 | mage_slayer | 法师杀手 | - | 5尺内施法借机, 被打断专注劣势, 魔法豁免优势 |
| 18 | magic_initiate | 魔法学徒 | - | 选1职业: 获2戏法+1个1环法术(长休1次) |
| 19 | martial_adept | 战斗老手 | - | 获2战技骰(d6)+学2战技(战士战斗大师列表) |
| 20 | medium_armor_master | 中甲大师 | 中甲熟练 | 中甲DEX上限+1(变+3), 中甲无隐匿劣势 |
| 21 | mobile | 灵动 | - | 速度+10, 疾跑无视困难地形, 攻击目标后不被借机 |
| 22 | moderately_armored | 中甲专家 | 轻甲熟练 | STR/DEX+1, 获中甲+盾牌熟练 |
| 23 | mounted_combatant | 骑乘战斗者 | - | 骑乘时攻击小型以下优势, 坐骑被攻击可重定向到自己, DEX豁免成功0伤害 |
| 24 | observant | 观察力 | - | INT/WIS+1, 读唇语, 被动察觉+5, 被动调查+5 |
| 25 | polearm_master | 长柄大师 | - | 长柄攻击后附赠1d4尾端攻击, 进入触及借机 |
| 26 | resilient | 强韧 | - | 选1属性+1, 获该属性豁免熟练 |
| 27 | ritual_caster | 仪典施法者 | INT/WIS 13+ | 学2个1环仪式, 之后可抄仪式法术 |
| 28 | savage_attacker | 凶残攻击者 | - | 每回合1次武器伤害骰优势(骰2次取高) |
| 29 | sentinel | 哨兵 | - | 借机命中目标速度=0, 撤离仍触发, 5尺内敌人攻击队友时反应攻击 |
| 30 | shield_master | 盾牌大师 | - | 攻击后附赠推撞, 单目标DEX豁免成功时反应0伤害, 盾AC加到DEX豁免 |
| 31 | skilled | 多面手 | - | 获任意3技能或工具熟练 |
| 32 | skulker | 潜行者 | DEX 13+ | 轻度遮蔽可躲藏, 远程失手不暴露, 微光感知劣势变正常 |
| 33 | spell_sniper | 法术狙击手 | 施法能力 | 法术攻击射程x2, 无视半/3/4掩体, 学1攻击戏法 |
| 34 | tavern_brawler | 酒馆斗士 | - | STR/CON+1, 徒手1d4+熟练, 徒手命中附赠擒抱 |
| 35 | weapon_master | 武器大师 | - | STR/DEX+1, 获4项武器熟练 |

---

### 2.8 条件追踪系统 (Conditions)

#### 2.8.1 完整条件列表 `[SRD-FULL + SRD-MODIFIED]`

| # | ID | 名称 | 机械效果 | 附加效果 |
|:--:|----|------|----------|----------|
| 1 | Blinded | 目盲 | 攻击劣势, 对其攻击优势, 依赖视觉技能自动失败 | - |
| 2 | Charmed | 魅惑 | 不能攻击魅惑者或将其作为有害目标, 魅惑者社交优势 | - |
| 3 | Deafened | 耳聋 | 依赖听觉技能自动失败 | - |
| 4 | Frightened | 恐慌 | 恐慌源在视线内时攻击/技能劣势, 不能主动接近 | - |
| 5 | Grappled | 擒抱 | 速度=0, 擒抱者失能时结束, 逃脱DC=擒抱者运动检定 | - |
| 6 | Incapacitated | 失能 | 不能采取动作或反应 | - |
| 7 | Invisible | 隐形 | 重度遮蔽, 攻击优势, 对其攻击劣势 | - |
| 8 | Paralyzed | 麻痹 | 失能+不能移动/说话, STR/DEX豁免自动失败, 5尺内攻击自动暴击 | - |
| 9 | Petrified | 石化 | 变为无机物->麻痹+重量x10+停止老化+免疫伤害疾病 | 已受毒素/疾病暂停 |
| 10 | Poisoned | 中毒 | 攻击和技能检定劣势 | - |
| 11 | Prone | 倒地 | 只能爬行, 攻击劣势, 5尺内对其攻击优势, 站立消耗一半速度 | 远程对其攻击劣势 |
| 12 | Restrained | 束缚 | 速度=0, 攻击劣势, 对其攻击优势, DEX豁免劣势 | - |
| 13 | Stunned | 震慑 | 失能+不能移动+说话困难, STR/DEX豁免自动失败, 对其攻击优势 | - |
| 14 | Unconscious | 昏迷 | 失能+不能移动说话+不知晓周围+掉落手中物+倒地, 5尺内攻击自动暴击 | STR/DEX豁免自动失败 |

#### 2.8.2 疲乏系统 (Exhaustion) `[SRD-MODIFIED: 简化为3级]`

| 等级 | 名称 | 效果 | 恢复方式 |
|:----:|------|------|----------|
| 0 | 正常 | 无 | - |
| 1 | 疲乏 (Exhaustion 1) | 技能检定劣势 | 长休消除1级 |
| 2 | 疲乏 (Exhaustion 2) | 速度减半 | 长休消除1级 |
| 3 | 力竭 (Exhaustion 3) | 攻击骰和豁免检定劣势; HP最大值减半 | 长休消除1级 |

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

**注意**: 本模型与 GDD-v1.md §5.4 和 combat-system.md §9.2 保持一致。

#### 2.8.3 条件数据存储Schema

```json
{
  "condition_id": "char_7a3f2b1c_cond_001",
  "character_id": "char_7a3f2b1c",
  "condition_type": "poisoned",
  "source_id": "trap_poison_dart",
  "source_name": "毒镖陷阱",
  "applied_at": "round_5",
  "duration": { "type": "rounds", "remaining": 3 },
  "save_ends": {
    "ability": "con", "dc": 13,
    "can_retry_each_turn": true, "retry_action": "end_of_turn"
  },
  "mechanical_effects": {
    "attack_disadvantage": true,
    "ability_check_disadvantage": true
  }
}
```

#### 2.8.4 条件堆叠与交互规则

1. **同名条件不堆叠**: 两次中毒=一次中毒, 持续时间取较长者
2. **不同条件可共存**: 角色可同时目盲+倒地+中毒
3. **来源刷新**: 同一来源重新施加相同条件时, 刷新持续时间
4. **优势/劣势互斥**: 有多源优势 + 任一源劣势 = **无优无劣** (平骰1次)
   - 3源优势 + 1源劣势 = 无优无劣
   - 1源优势 + 1源劣势 = 无优无劣
   - 2源优势 + 0源劣势 = 优势
   - 1源劣势 + 0源优势 = 劣势
5. **免疫优先**: 角色对某条件免疫, 忽略所有施加尝试
6. **失能的传递**: Paralyzed/Stunned/Unconscious 自动包含 Incapacitated
7. **倒地交互**: 昏迷角色自动倒地; 0HP角色默认倒地
8. **0HP状态**: HP降至0 = Unconscious + Prone + 不可行动

---

## 3. 装备槽位模型

### 3.1 槽位定义 `[CUSTOM: 替代DND 5e重量制]`

装备界面布局:

```
主角装备 (Character Equipment Panel)
┌────────────────────────────┐
│  [Helmet]   头部            │
│  [Amulet]   颈部            │
│  [Armor]    躯干            │
│  [Cloak]    背部            │
│  [Main Hand] 主手  [Off Hand] 副手
│  [Ring 1]   手指1 [Ring 2] 手指2
│  [Boots]    脚部            │
│                             │
│  背包: [ ] [ ] [ ] ... (10+STR_modx2 格)
│  金币: 150 GP               │
│  同调: [ ] [ ] [ ] (最多3件)│
└────────────────────────────┘
```

### 3.2 槽位约束

| 槽位 | 可装备类型 | 数量 | 特殊规则 |
|------|-----------|:----:|----------|
| Main Hand | 武器/法杖/盾牌 | 1 | 双手武器占Main+Off |
| Off Hand | 武器/盾牌 | 1 | 双持需Two-Weapon Fighting |
| Armor | 护甲(轻/中/重) | 1 | 不熟练则攻击/属性检定/豁免劣势且不可施法 |
| Helmet | 头盔/头冠 | 1 | - |
| Boots | 靴子/鞋子 | 1 | - |
| Cloak | 披风/斗篷 | 1 | - |
| Ring 1 | 戒指 | 1 | - |
| Ring 2 | 戒指 | 1 | - |
| Amulet | 护符/项链 | 1 | - |
| Backpack | 消耗品/杂物/备用武器 | 10+STR_modx2 | 超限->速度减半+不能疾跑 |

**注意**: 装备在身的武器/护甲/饰品不计入背包槽位。

### 3.3 同调系统 (Attunement) `[SRD-FULL]`

- 每角色最多同调 **3件** 魔法物品
- 需要同调的物品标有 `requires_attunement: true`
- 同调流程: 短休专注于此物品 -> 完成同调
- 取消同调: 短休断开 / 距离物品超过100尺持续24小时
- 同一角色不能同调同一物品的多个副本
- 部分物品有职业/种族/等级同调要求

```json
{
  "item_id": "flame_tongue_longsword",
  "name": "焰舌长剑",
  "type": "weapon", "rarity": "rare",
  "requires_attunement": true,
  "attunement_requirements": { "any": true },
  "slot": "main_hand",
  "effects_while_attuned": [
    { "type": "bonus_damage", "dice": "2d6", "damage_type": "fire" },
    { "type": "light_source", "radius": 40, "dim_radius": 80 }
  ]
}
```

---


## 4. 关系系统数据模型

### 4.1 关系数据结构 `[CUSTOM]`

```json
{
  "character_id": "char_7a3f2b1c",
  "relationships": {
    "char_player": {
      "character_id": "char_player",
      "character_name": "亚瑟·晨星",
      "value": 3,
      "history": [
        {
          "event": "saved_from_death_save",
          "change": 2,
          "adventure_id": "adv_001",
          "timestamp": "2026-05-04T12:30:00Z"
        },
        {
          "event": "shared_loot_preference",
          "change": 1,
          "adventure_id": "adv_001",
          "timestamp": "2026-05-04T12:45:00Z"
        }
      ],
      "labels": [
        {
          "type": "comrade",
          "name": "战友",
          "acquired_at_value": 3,
          "acquired_timestamp": "2026-05-04T12:30:00Z"
        }
      ]
    },
    "char_elara": {
      "character_id": "char_elara",
      "character_name": "艾拉拉·月歌",
      "value": -2,
      "history": [],
      "labels": []
    }
  }
}
```

### 4.2 关系阈值与效果

| 数值 | 关系标签 | 战斗效果 | 叙事效果 |
|:----:|----------|----------|----------|
| <= -5 | Nemesis (宿敌) | 邻接攻击-1命中, 对宿敌攻击+1伤害 | 嘲讽对话, 可触发"决斗"事件 |
| -4 ~ -1 | Hostile (不和) | 无法使用"援助"动作, 不接受援助 | 冷淡对话 |
| 0 | Neutral (中立) | 无特殊效果 | 正常对话 |
| 1 ~ 4 | Friendly (友好) | 可执行"援助"动作(附赠, 给予队友下次攻击优势) | 友好对话 |
| >= 5 | Bonded (牵绊) | 战友: 邻接+1命中; 恋人: 邻接+1AC; 师徒: 可分享熟练项 | 专属对话, 营地亲密场景 |

### 4.3 关系变化触发规则

#### 战斗中 (每场战斗限每个事件类型1次)

| 触发事件 | 变化值 | 条件/说明 |
|----------|:------:|-----------|
| 从0HP中救回队友 (治疗至HP>0) | **+2** | - |
| 治疗受伤(<50%HP)队友 | **+1** | 战斗中, 每个被治疗者限1次 |
| 承受原本命中队友的伤害 (如借机打断/保护) | **+3** | 每场战斗限1次 |
| AOE法术误伤队友 | **-2** | 队友受到伤害 |
| 队友0HP时选择攻击而非施救 (当轮有治疗能力时) | **-3** | - |
| 1轮内配合击杀同一敌人 | **+1** | 每敌人限1次 |
| 抢夺队友已标记的击杀 | **-1** | 队友已标记目标, 由他人完成最后一击 |

#### 冒险中

| 触发事件 | 变化值 | 说明 |
|----------|:------:|------|
| 做出与对方性格标签一致的选择 | **+1** | 如"忠诚"队友看到玩家遵守承诺 |
| 做出与对方性格标签相悖的选择 | **-1** | 如"正直"队友看到玩家偷窃 |
| 分享战利品给特定队友 | **+2** | 主动将物品给予对方 |
| 抢夺队友想要的战利品 | **-3** | 分配时选择对方喜欢的物品 |
| 选择支援队友的检定 | **+1** | "援助"帮助对方完成技能检定 |
| 有能力却不救助队友 (冒险事件中) | **-2** | - |

#### 酒馆中

| 触发事件 | 变化值 | 说明 |
|----------|:------:|------|
| 同种族闲聊事件 | **+1** | 酒馆自然触发 |
| 对立信仰/阵营事件 | **-2** | 触发后玩家可调解 |
| 训练事件 (师徒) | **+3** | 一方传授技能 |
| 争吵事件 | **-1 ~ -3** | 玩家调解成功可减至-1或消除 |
| 共同饮酒事件 | **+1** | 酒馆自然触发 |
| 偏袒一方 (争吵/决斗中) | **+1偏 / -1另** | 玩家选择 |

### 4.4 关系类型触发条件

关系>=5时, 根据互动历史触发特定关系类型判定:

```json
{
  "comrade": {
    "name": "战友",
    "trigger": "coop_kills >= 3 AND value >= 5",
    "combat_bonus": { "adjacent_attack_bonus": 1 },
    "description": "并肩作战让你们配合默契"
  },
  "lovers": {
    "name": "恋人",
    "trigger": "shared_rest_events >= 2 AND compatible_personalities AND value >= 5",
    "combat_bonus": { "adjacent_ac_bonus": 1 },
    "description": "你们的心跳在战场上共振"
  },
  "mentor_student": {
    "name": "师徒",
    "trigger": "training_event_completed AND value >= 5",
    "combat_bonus": { "shared_proficiency": "mentor_skill", "duration": "once_per_encounter" },
    "description": "师徒之间的默契超越语言"
  },
  "trauma": {
    "name": "创伤",
    "trigger": "witnessed_ally_near_death AND value_once_exceeded_5",
    "combat_bonus": { "avoidance_penalty": "single_combat_avoidance_if_triggered" },
    "description": "过去的阴影让你们的关系变得复杂"
  }
}
```

**关系类型转移规则**: 当关系值再次变化导致关系类型不再满足条件时，30天内关系类型保留但效果消失(仅保留标签用于叙事)。关系回到阈值后恢复效果。

---

## 5. 角色生成系统

### 5.1 生成架构 `[CUSTOM]`

```
角色生成管线 (Character Generation Pipeline)
=============================================

Step 1: 程序化数值层 (Procedural Numerical Layer)
  ┌───────────────────────────────────────────┐
  │ 根据种族+职业模板 -> 生成六维属性          │
  │ -> 计算HP/AC/衍生值                       │
  │ -> 分配技能熟练/豁免熟练/起始装备           │
  │ -> 验证数值合法性 (scores 3-20)            │
  └────────────┬──────────────────────────────┘
               │
               v
Step 2: LLM 叙事层 (LLM Narrative Layer)
  ┌───────────────────────────────────────────┐
  │ 输入: 种族 + 职业 + 属性分布 + 性格标签池  │
  │ 输出: 姓名/性别/性格/背景故事/外观描述      │
  │ -> Schema 验证JSON格式                     │
  │ -> 重试最多3次, 失败用预置模板              │
  └────────────┬──────────────────────────────┘
               │
               v
Step 3: 合并写入 (Merge & Persist)
  ┌───────────────────────────────────────────┐
  │ 数值层 + 叙事层 = Complete Character Data  │
  │ -> 写入 SQLite (结构化数据)                │
  │ -> 发出生成完成信号                         │
  └───────────────────────────────────────────┘
```

### 5.2 程序化数值生成

#### 5.2.1 属性生成法 `[SRD-MODIFIED + CUSTOM: 受控随机性]`

本游戏采用**标准数组 + 3自由点 + 种族加成**, 非随机4d6k3:

1. **基础数组**: `[15, 14, 13, 12, 10, 8]` — DND 5e Standard Array
2. **职业引导分配**:
   - 最高值(15) -> 职业主属性1 (如 Fighter->STR)
   - 次高值(14) -> 职业主属性2或CON (如 Wizard->CON)
   - 其余随机分配给剩余4项
3. **自由分配点**: +3点可分配到任意属性（每点+1），上限20
   - 这是本游戏的核心设计：标准数组保证基础平衡，自由点创造角色独特性
   - 示例：矮人战士可能将3点全加STR(18)成为力量型，或分给CON+WIS(15+13)成为坚韧型
4. **种族加成**: 在基础数组+自由点之上加种族ability_increases
5. **验证**: 确保无属性超过20 (Lv1上限)

**设计理由**: 纯随机4d6k3在无DM的程序化游戏中会产生灾难性极端值（如STR 3的战士）。标准数组保证每个角色都有基本的战斗力，而3自由点让每个角色有可感知的差异——两个同种族同职业的战士，一个可能天赋异禀(STR 18)，另一个可能体弱但坚韧(STR 13, CON 17)。

生成示例:

| 角色 | 种族 | 职业 | 自由点分配 | STR | DEX | CON | INT | WIS | CHA |
|------|------|------|:----------:|:---:|:---:|:---:|:---:|:---:|:---:|
| Human Fighter (力量型) | Human | Fighter | +3 STR | 19 | 14 | 15 | 9 | 13 | 11 |
| Human Fighter (坚韧型) | Human | Fighter | +2 CON, +1 WIS | 16 | 14 | 17 | 9 | 14 | 11 |
| High Elf Wizard | Elf | Wizard | +3 INT | 8 | 15 | 14 | 19 | 12 | 10 |
| Dwarf Fighter (力量型) | Mountain Dwarf | Fighter | +3 STR | 20 | 10 | 16 | 8 | 12 | 13 |
| Dwarf Fighter (敏捷型) | Mountain Dwarf | Fighter | +2 DEX, +1 WIS | 17 | 12 | 16 | 8 | 13 | 13 |
| Lightfoot Halfling Rogue | Halfling | Rogue | +3 DEX | 10 | 20 | 14 | 13 | 12 | 14 |

#### 5.2.2 HP初始化

```
Lv1 HP = 职业Hit Die最大值 + CON_mod

Fighter d10 Lv1: 10 + CON_mod
Wizard d6  Lv1:  6 + CON_mod
Rogue d8   Lv1:  8 + CON_mod

含种族加成示例:
- Mountain Dwarf Fighter (CON 14+2=16, +3): 10+3 = 13
- Hill Dwarf额外+1HP/每级: 10+3+1 = 14
- Human Wizard (CON 14, +2): 6+2 = 8
- Halfling Rogue (CON 14, +2): 8+2 = 10
```

#### 5.2.3 起始装备分配

基于职业predefined equipment choices:

```
Fighter：(a) Chain Mail 或 (b) Leather + Longbow + 20 Arrows
        (a) 军用武器 + 盾牌 或 (b) 两把军用武器
        (a) Light Crossbow + 20 Bolts 或 (b) 两把Handaxe
        + Dungeoneer's Pack

Wizard：(a) Quarterstaff 或 (b) Dagger
        (a) Component Pouch 或 (b) Arcane Focus
        (a) Scholar's Pack 或 (b) Explorer's Pack
        + Spellbook

Rogue：(a) Rapier 或 (b) Shortsword
        (a) Shortbow + 20 Arrows 或 (b) Shortsword
        (a) Burglar's Pack / Dungeoneer's Pack / Explorer's Pack
        + Leather Armor + 2 Daggers + Thieves' Tools
```

### 5.3 LLM叙事层生成

> **设计注释**：原系统使用3个personality_tags（属性暗示 + 种族刻板印象 + 反刻板印象），虽然简洁但存在两个问题：(1) 反刻板印象维度过于抽象，LLM常输出意义模糊的标签；(2) 3个标签无法覆盖角色扮演中最重要的维度——恐惧/弱点和说话风格。因此扩展为5维度系统，以职业原型替代种族刻板印象（避免种族本质主义），新增随机经历（提供具体而非抽象的独特性）、恐惧/弱点（创造角色弧光核心）、说话风格（直接影响对话系统的文本生成质量）。种族刻板印象保留为可选字段，供偏好传统D&D风格的玩家使用。

#### 5.3.1 生成Prompt模板 `[CUSTOM]`

```
System: 你是《酒馆与命运》中负责生成角色叙事层的叙事Agent。
根据以下角色数值信息, 为该角色生成完整的5维度性格画像与叙事描述。

User: 
=== 角色数值信息 ===
种族: {race_name} ({race_name_en})
子种族: {subrace_name}
职业: {class_name}
性别: {gender} (random)
主属性: STR {str}, DEX {dex}, CON {con}, INT {int}, WIS {wis}, CHA {cha}
(最高属性: {highest_stat}, 最低属性: {lowest_stat})
等级: 1

=== 生成要求 ===
1. name: 符合{race_name}命名传统的完整姓名
2. gender: male 或 female
3. personality (5维度性格画像):
   3.1 属性暗示 (attribute_hint): 1个性格形容词，由最高/最低属性推导
       - 高STR->好斗/坚毅/强势 | 高DEX->机敏/灵动/不安分 | 高CON->坚韧/沉稳/忍耐
       - 高INT->博学/好奇/理性 | 高WIS->洞察/冷静/谨慎 | 高CHA->外向/热情/说服力
       - 低属性反向推导（如低CHA->孤僻/不善交际）
       示例: "坚毅", "机敏", "热情"
   3.2 职业原型 (class_archetype): 1个符合{class_name}典型性格的形容词
       - Fighter→守护者/好战者/纪律严明/荣誉至上 | Wizard→求知者/野心家/孤傲/完美主义
       - Rogue→自由者/投机者/狡黠/重情义 | Cleric→虔诚者/救世者/狂热/内省
       - 可随机选择该职业下的任一原型方向
       示例: "守护者", "求知者", "自由者"
   3.3 种族刻板印象 (race_stereotype) - 可选字段, 可留空
       - 仅在希望保留传统D&D种族风味时填写
       - 如矮人->固执, 精灵->优雅, 半兽人->粗犷, 半身人->乐天
       - 若留空则跳过, 不影响其他维度
       示例: "固执" 或 ""
   3.4 随机经历 (life_experience): 1个具体的人生事件(非抽象标签), 塑造了角色性格
       - 必须是一个具体的事件, 不是形容词
       - 示例: "曾在战争中失去家人", "被导师背叛", "在街头长大", "目睹了神迹",
                "曾是贵族但家道中落", "被龙养大", "在修道院度过童年", "曾与魔鬼交易"
   3.5 恐惧/弱点 (fear_weakness): 1个具体的人性弱点或恐惧
       - 必须是人性的、可共鸣的弱点
       - 示例: "害怕被抛弃", "恐高", "无法拒绝求助", "害怕黑暗",
                "对权力的渴望", "容易轻信他人", "不敢表达真实想法", "对火焰有心理创伤"
   3.6 说话风格 (speech_style): 1个描述角色说话方式的短语
       - 直接影响后续对话系统的文本生成
       - 示例: "话多且喜欢打比喻", "寡言但句句精炼", "正式且用词考究",
                "粗俗直白", "总带口头禅'以锤子之名'", "喜欢用反问句",
                "慢条斯理", "语速极快且跳跃"
4. backstory: 2-3段短文 (不少于50汉字), 解释角色如何成为{class_name}
   - 必须呼应3.4的随机经历和3.5的恐惧/弱点
5. appearance_description: 详细外观描述 (不少于20汉字)
6. personal_goal: 角色个人目标 (不少于10汉字)

=== 输出格式 (严格JSON, 不要额外文本) ===
{
  "name": "...",
  "gender": "male/female",
  "personality": {
    "attribute_hint": "坚毅",
    "class_archetype": "守护者",
    "race_stereotype": "",
    "life_experience": "曾在战争中失去家人",
    "fear_weakness": "害怕再次失去战友",
    "speech_style": "寡言但句句精炼"
  },
  "backstory": "...(2-3段, 呼应life_experience和fear_weakness)...",
  "appearance_description": "...",
  "personal_goal": "..."
}
```

#### 5.3.2 生成后验证

```
验证步骤:
1. JSON有效性 -> 解析成功
2. 必需字段完整性 -> 10个字段全部存在:
   name, gender, backstory, appearance_description, personal_goal (5个)
   + personality.attribute_hint, personality.class_archetype,
     personality.life_experience, personality.fear_weakness, personality.speech_style (5个)
3. personality字段验证:
   3a. attribute_hint: 非空字符串, 长度 <= 10字符
   3b. class_archetype: 非空字符串, 长度 <= 10字符
   3c. race_stereotype: 可为空字符串(OPTIONAL), 非空时长 <= 10字符
   3d. life_experience: 非空字符串, 长度 >= 4字符 (必须是具体事件, 非抽象形容词)
   3e. fear_weakness: 非空字符串, 长度 >= 3字符 (必须是具体弱点)
   3f. speech_style: 非空字符串, 长度 >= 4字符 (必须有对话风格信息)
4. 多样性检查 -> 5个核心维度值互不相同 (race_stereotype除外)
5. 长度检查 -> backstory >= 50 chars, appearance >= 20 chars, personal_goal >= 10 chars
6. 性别名字一致性 -> 启发式检查
7. 叙事一致性 -> backstory必须提及life_experience事件
失败: 重试最多3次, 最终备选使用预置模板
```

### 5.4 完整生成JSON Schema

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "CharacterGenerationResult",
  "type": "object",
  "required": ["character_id", "narrative", "stats", "race_data", "class_data", "skills", "equipment"],
  "properties": {
    "character_id": {
      "type": "string",
      "pattern": "^char_[a-f0-9]{8}$"
    },
    "narrative": {
      "type": "object",
      "required": ["name", "race", "gender", "personality", "backstory", "appearance_description", "personal_goal"],
      "properties": {
        "name": { "type": "string", "minLength": 2, "maxLength": 40 },
        "race": { "type": "string", "enum": ["human","elf","dwarf","halfling","half_orc","gnome","tiefling","dragonborn","half_elf"] },
        "gender": { "type": "string", "enum": ["male","female"] },
        "personality": {
          "type": "object",
          "required": ["attribute_hint", "class_archetype", "life_experience", "fear_weakness", "speech_style"],
          "properties": {
            "attribute_hint": { "type": "string", "minLength": 1, "maxLength": 10, "description": "由最高/最低属性推导的性格形容词" },
            "class_archetype": { "type": "string", "minLength": 1, "maxLength": 10, "description": "职业原型性格方向, 如Fighter->守护者/好战者" },
            "race_stereotype": { "type": "string", "maxLength": 10, "description": "种族刻板印象(可选, 可为空字符串)" },
            "life_experience": { "type": "string", "minLength": 4, "maxLength": 60, "description": "具体人生经历事件, 非抽象形容词" },
            "fear_weakness": { "type": "string", "minLength": 3, "maxLength": 40, "description": "人性化恐惧或弱点" },
            "speech_style": { "type": "string", "minLength": 4, "maxLength": 40, "description": "角色说话方式, 直接影响对话系统文本生成" }
          }
        },
        "backstory": { "type": "string", "minLength": 50 },
        "appearance_description": { "type": "string", "minLength": 10 },
        "personal_goal": { "type": "string", "minLength": 10 }
      }
    },
    "stats": {
      "type": "object",
      "required": ["level", "abilities", "armor_class", "hit_points", "speed", "initiative_modifier"],
      "properties": {
        "level": { "type": "integer", "minimum": 1, "maximum": 20 },
        "abilities": {
          "type": "object",
          "required": ["str","dex","con","int","wis","cha"],
          "properties": {
            "str": { "type": "object", "required": ["score","modifier"] },
            "dex": { "type": "object", "required": ["score","modifier"] },
            "con": { "type": "object", "required": ["score","modifier"] },
            "int": { "type": "object", "required": ["score","modifier"] },
            "wis": { "type": "object", "required": ["score","modifier"] },
            "cha": { "type": "object", "required": ["score","modifier"] }
          }
        },
        "armor_class": { "type": "integer", "minimum": 5, "maximum": 30 },
        "hit_points": {
          "type": "object",
          "required": ["max"],
          "properties": { "max": { "type": "integer", "minimum": 1 } }
        },
        "speed": { "type": "integer", "minimum": 15, "maximum": 60 },
        "initiative_modifier": { "type": "integer", "minimum": -5, "maximum": 10 }
      }
    },
    "race_data": {
      "type": "object",
      "required": ["race_id", "traits", "languages"],
      "properties": {
        "race_id": { "type": "string" },
        "subrace_id": { "type": "string" },
        "traits": { "type": "array", "items": { "type": "string" } },
        "languages": { "type": "array", "items": { "type": "string" } }
      }
    },
    "class_data": {
      "type": "object",
      "required": ["class_id", "level", "features"],
      "properties": {
        "class_id": { "type": "string" },
        "level": { "type": "integer" },
        "features": { "type": "array", "items": { "type": "string" } },
        "spellcasting": { "type": "object" }
      }
    },
    "skills": {
      "type": "object",
      "required": ["proficiencies"],
      "properties": {
        "proficiencies": {
          "type": "array",
          "items": {
            "type": "string",
            "enum": [
              "acrobatics","animal_handling","arcana","athletics","deception",
              "history","insight","intimidation","investigation","medicine",
              "nature","perception","performance","persuasion","religion",
              "sleight_of_hand","stealth","survival"
            ]
          }
        }
      }
    },
    "equipment": {
      "type": "object",
      "required": ["slots", "backpack"],
      "properties": {
        "slots": {
          "type": "object",
          "properties": {
            "main_hand": {}, "off_hand": {}, "armor": {},
            "helmet": {}, "boots": {}, "cloak": {},
            "ring_1": {}, "ring_2": {}, "amulet": {}
          }
        },
        "backpack": { "type": "array", "items": { "type": "string" } }
      }
    }
  }
}
```

---

## 6. 角色升级与进阶

### 6.1 XP经验值表 `[SRD-FULL]`

(完整表见 Section 2.4.1)

**MVP阶段焦点 (Lv1-5)**:

| 升至等级 | 所需XP | 累计XP |
|:-------:|:------:|:------:|
| 2 | 300 | 300 |
| 3 | 900 | 1,200 |
| 4 | 2,700 | 3,900 |
| 5 | 6,500 | 10,400 |

### 6.2 ASI (Ability Score Improvement) 分布 `[SRD-FULL]`

| 职业 | Lv4 | Lv6 | Lv8 | Lv10 | Lv12 | Lv14 | Lv16 | Lv19 | 总计 |
|------|:---:|:---:|:---:|:----:|:----:|:----:|:----:|:----:|:----:|
| Fighter | YES | YES | YES | - | YES | YES | YES | YES | **7** |
| Rogue | YES | - | YES | YES | YES | - | YES | YES | **6** |
| Barbarian | YES | - | YES | - | YES | - | YES | YES | **5** |
| Monk | YES | - | YES | - | YES | - | YES | YES | **5** |
| 其余8职业 | YES | - | YES | - | YES | - | YES | YES | **5** |

**ASI选项**:
1. 单项属性 +2 (上限20)
2. 两项不同属性各 +1 (上限20)
3. 选择一个专长 (Feat)

### 6.3 子职业选择时机 `[SRD-FULL]`

| 职业 | 子职业等级 | MVP可用子职业 | 完整版可选子职业 |
|------|:---------:|--------------|----------------|
| Fighter | 3 | Champion (勇士) | Champion, Battle Master, Eldritch Knight |
| Wizard | 2 | Evocation (塑能) | Evocation + 7个其他学派 |
| Rogue | 3 | Thief (盗贼) | Thief, Assassin, Arcane Trickster |
| Cleric | 1 | Life (生命) | Life + 6个其他领域 |
| Ranger | 3 | Hunter (猎人) | Hunter, Beast Master |
| Paladin | 3 | Devotion (奉献) | Devotion, Ancients, Vengeance |
| Sorcerer | 1 | Draconic (龙脉) | Draconic, Wild Magic |
| Bard | 3 | Lore (逸闻) | Lore, Valor |
| Druid | 2 | Moon (月亮) | Moon, Land |
| Monk | 3 | Open Hand (散打) | Open Hand, Shadow, Four Elements |
| Warlock | 1 | Fiend (邪魔) | Fiend, Archfey, Great Old One |
| Barbarian | 3 | Berserker (狂战) | Berserker, Totem Warrior |

### 6.4 升级流程 (详见 Section 2.4.2)

### 6.5 兼职规则 `[SRD-FULL -- Phase 3实现]`

```
兼职前置条件:
  - 满足新职业的属性要求 (通常主属性 >= 13)
  - 角色总等级可在1-20之间自由分配

兼职效果:
  HP: 新职业Hit Die的一半(向下取整) + CON_mod 为新HP加成
      (从第2职业Lv1按满Hit Die)

  熟练加值: 基于角色总等级 (非职业等级)

  武器/护甲熟练:
    新职业提供: 轻甲/中甲/盾牌/简易武器熟练(因职业而异)
    注: 兼职获得重甲熟练需满足原职业有

  法术位 (Multi-class Spellcaster):
    总施法者等级 = Full_Caster_Levels + ceil(Half_Caster_Levels / 2)
    注: Warlock的Pact Magic独立计算, 不合并
    示例: Wizard 4 / Cleric 1 = 5级施法者 -> 使用Full Caster表Lv5行(4,3,2)

  职业特性: 按各自职业等级获得 (非总等级)
  ASI: 按各自职业等级获得 (非总等级, 与5e原版不同)
  Extra Attack: 多个来源互不叠加

MVP阶段 (Phase 1): 不支持兼职
Phase 3: 完整实现
```

---


## 7. 伤疤与永久状态

### 7.1 伤疤数据模型 `[CUSTOM]`

```json
{
  "scar_id": "scar_char_7a3f2b1c_001",
  "character_id": "char_7a3f2b1c",
  "acquired_at": {
    "adventure_id": "adv_007",
    "encounter_id": "enc_dragon_fire",
    "timestamp": "2026-05-04T15:30:00Z"
  },
  "name": "惧焰",
  "name_en": "Fire Trauma",
  "narrative": "索林在龙息中烧伤了大半个背部，此后每次看到火焰都会不由自主地颤抖。但这份恐惧也让他比任何人都更早感知到火源。",
  "narrative_template": "{character_name} 在 {adventure_name} 中被烈焰吞噬，从此对火焰心有余悸。",
  "scar_category": "fire_trauma",
  "severity": "moderate",
  "mechanical_effects": {
    "penalties": [
      {
        "type": "damage_vulnerability",
        "damage_type": "fire",
        "multiplier": 1.5,
        "description": "火焰伤害 +50%"
      }
    ],
    "bonuses": [
      {
        "type": "save_advantage",
        "stat": "dex",
        "condition": "against_fire_effects",
        "description": "对抗火焰效果时DEX豁免优势"
      }
    ],
    "cosmetic": {
      "body_part": "back",
      "visual_effect": "burn_scar_pattern",
      "sprite_overlay": "scar_burn_back",
      "description": "背部大面积烧伤疤痕"
    }
  },
  "removable": false,
  "removal_condition": "special_quest_only",
  "source_damage_info": {
    "damage_type": "fire",
    "total_damage_dealt": 34,
    "hit_0hp": true,
    "source_name": "红龙·灰烬之翼的吐息"
  }
}
```

### 7.2 完整伤疤效果表 `[CUSTOM: 惩罚为主]`

> **设计哲学变更：伤疤是战争创伤，而非超英起源。**
>
> 旧哲学（已废弃）：每道伤疤都是"双刃剑"——惩罚与增益并存，玩家可能主动追求特定伤疤以获取增益。
>
> **新哲学：「多数惩罚，极少数有限补偿」**
> - **80%的伤疤仅含纯粹惩罚**（26/32）——数值减损、倍率惩罚、豁免劣势等。玩家必须畏惧战斗中的创伤，而非期待伤疤带来的"异能"。
> - **20%的伤疤含极有限补偿**（6/32）——仅当叙事逻辑自然成立时才保留，且补偿值在原基础上减半。例如：失去一只眼睛后，剩余的眼睛适应黑暗，获得**30尺**黑暗视觉（原60尺）；灵魂被黯蚀能量撕裂后，对亡灵攻击获得微弱的预知感（AC+1，原+2）。
> - **补偿必须叙事逻辑自洽**：被火烧伤不会获得火焰豁免优势（那是超英起源），被酸液腐蚀不会获得酸伤害抵抗（那是变异异能）。唯一保留补偿的场景是**人体自然的代偿机制**（如失去视觉→其他感官增强）或**本能性的自我保护**（如旧伤肋骨→下意识护住弱点）。
>
> 此设计确保伤疤系统始终是玩家畏惧的惩罚机制，而非隐藏的"增益获取途径"。

#### 火焰伤害伤疤

| ID | 名称 | 惩罚 | 补偿 | 外观标记 |
|----|------|------|------|----------|
| scar_fire_trauma | 惧焰 | 火焰伤害 x1.5 | — | 烧伤疤痕(背部) |
| scar_branded | 烙印 | CHA-2 (非威吓的社交检定) | — | 面部烙印 |
| scar_seared_lungs | 炙肺 | HP上限永久-5 | — | 呼吸伴随嘶哑声 |
| scar_fire_hair | 焦发 | 第一次被敌人注意时劣势 | — | 永久烧焦的发梢 |
| scar_blistered_hands | 灼手 | 巧手-2, 远程武器攻击-1 | — | 手掌水疱 |

#### 寒冷伤害伤疤

| ID | 名称 | 惩罚 | 补偿 | 外观标记 |
|----|------|------|------|----------|
| scar_frostbite | 冻伤指节 | 巧手-2 | — | 手指发蓝 |
| scar_chill_bones | 寒骨 | 速度-5尺 | — | 关节僵硬 |
| scar_cold_veins | 寒血 | 死亡豁免窗口-1轮 (2轮不治即死) | — | 血管呈蓝色可见 |
| scar_frozen_memory | 冰封记忆 | 历史-2 | — | 银色发丝 |

#### 闪电伤害伤疤

| ID | 名称 | 惩罚 | 补偿 | 外观标记 |
|----|------|------|------|----------|
| scar_nerve_damage | 神经损伤 | 先攻-2 | — | 手部震颤 |
| scar_static_touch | 静电之触 | 徒手接触NPC有10%几率使其敌意 | — | 头发偶尔竖立 |
| scar_lightning_twitch | 闪电抽动 | DEX豁免-1 | — | 肌肉偶尔抽搐 |
| scar_heart_arrythmia | 心脉紊乱 | CON豁免-1 | — | 颈部血管闪蓝光 |

#### 黯蚀伤害伤疤

| ID | 名称 | 惩罚 | 补偿 | 外观标记 |
|----|------|------|------|----------|
| scar_withered_limb | 枯肢 | STR-2 (永久) | — | 手臂萎缩/灰白 |
| scar_soul_scar | 魂痕 | 第1次死亡豁免自动失败 | 对亡灵+1 AC | 胸口灰白斑痕 |
| scar_life_drain_echo | 生命回响 | HP上限永久-10 | — | 眼下黑圈 |
| scar_spectral_mark | 灵痕 | 对驱散/放逐豁免劣势 | — | 皮肤上飘忽的灰色符文 |

#### 心灵伤害伤疤

| ID | 名称 | 惩罚 | 补偿 | 外观标记 |
|----|------|------|------|----------|
| scar_paranoia | 偏执狂 | 短休获益有50%几率失败 | — | 时刻环顾四周 |
| scar_broken_mind | 破碎心智 | INT-2 (永久) | — | 神情恍惚 |
| scar_nightmare_plagued | 梦魇缠身 | 长休50%不消除疲乏 | — | 严重黑眼圈 |
| scar_psychic_scar | 心灵裂痕 | WIS豁免-1 | — | 眉间深深皱纹 |

#### 物理伤害伤疤

| ID | 名称 | 惩罚 | 补偿 | 外观标记 |
|----|------|------|------|----------|
| scar_battle_hardened | 老兵之手 | 主手武器攻击-1 | — | 手臂布满伤疤 |
| scar_lost_eye | 独眼 | 察觉-2, 远程攻击劣势(>30尺) | 获得30尺黑暗视觉 | 独眼/眼罩 |
| scar_limp | 跛行 | 速度-10尺 | 倒地后站立仅需5尺 | 用手杖/明显跛行 |
| scar_missing_finger | 断指 | 巧手-2 | 获得徒手攻击熟练项 | 断一指 |
| scar_broken_ribs | 旧伤肋骨 | CON-1 | 受到暴击时伤害-1 | 呼吸伴疼痛/偶尔捂胸 |
| scar_shattered_jaw | 碎颚 | 说服/欺瞒-2 | — | 下颌畸形 |
| scar_severed_ear | 缺耳 | 察觉-1 | — | 缺一只耳朵 |
| scar_spinal_twist | 脊弯 | 跳跃距离减半 | 不会被推倒 | 姿势微驼 |

#### 毒/酸伤害伤疤

| ID | 名称 | 惩罚 | 补偿 | 外观标记 |
|----|------|------|------|----------|
| scar_acid_scarred | 酸蚀 | CHA-2 | — | 面部/皮肤腐蚀痕迹 |
| scar_toxin_tolerance | 毒抗 | 治疗药水效果-50% | — | 皮肤微绿 |
| scar_chemical_burns | 化学灼伤 | 巧手-1, 表演-1 | — | 手部漂白斑痕 |

### 7.3 伤疤生成算法

#### 7.3.1 程序化选择 (Procedural Selection)

```
Scar Generation Algorithm:
---------------------------
输入: character, combat_log (最近5轮), damage_summary

Step 1: 确定主要伤害类型
  统计 combat_log 中各伤害类型的累计值
  选最高累计伤害类型

Step 2: 从对应伤害类型池中筛选候选伤疤
  根据 severity 等级筛选:
    - 轻度 (累计伤害 < 角色HP最大值): 仅轻度伤疤
    - 中度 (累计伤害 HP*1~2): 轻+中度伤疤
    - 重度 (累计伤害 > HP*2 或 HP曾降至0): 全部伤疤

Step 3: 排除已存在伤疤
  同一角色不能有同一ID的重复伤疤

Step 4: 加权随机选择
  权重: 轻度70% / 中度25% / 重度5% (但重度触发时重度和中度权重提升)
  选1个效果

Step 5: 返回选中伤疤的 mechanical_effects + cosmetic
```

#### 7.3.2 LLM叙事生成

```
Prompt: 根据以下战斗数据, 为角色生成伤疤的叙事描述。

=== 战斗数据 ===
角色名: {name}, 种族: {race}, 职业: {class}
冒险名称: {adventure_name}
伤害来源: {source_name} ({source_type})
总受到伤害: {total_damage}
主要伤害类型: {primary_damage_type}
曾HP降至0: {was_knocked_out}
最后的敌人: {final_enemy}

=== 程序选中的伤疤效果 ===
伤疤名称: {scar_name}
数值惩罚: {penalties_summary}
数值补偿: {bonuses_summary}

=== 生成要求 ===
1. narrative: 2-3句叙事描述, 将数值效果自然地写入故事
   格式: 先描述受伤的瞬间, 再描述现在的后遗症,
         最后暗示一点正面效果
2. 使用 {narrative_template} 作为基础模板

输出格式 (JSON):
{"narrative": "..."}
```

### 7.4 伤疤消除/缓解机制

| 方法 | 效果 | 获取方式 |
|------|------|----------|
| 神殿祈祷 | 移除1个轻度伤疤 | 酒馆声望Lv7解锁神殿 |
| 传奇药水 | 移除1个中度或轻度伤疤 | 稀有掉落/炼金台制作 |
| 灵魂换约 | 移除1个重度伤疤, 代价: -2任一属性永久 | 邪魔/旧日支配者特殊事件 |
| 时间治愈 | 轻度伤疤30天后自动转为仅外观(无数值效果) | 自然流逝 |
| 替代伤疤 | 新同类型伤疤替换旧伤疤 (可选择保留哪个外观) | 新的重度伤害事件 |

### 7.5 永久条件追踪

除伤疤外, 角色可拥有的其他永久状态:

```json
{
  "permanent_conditions": [
    {
      "type": "curse",
      "id": "curse_of_the_damned",
      "name": "堕者诅咒",
      "source": "adv_015_boss_curse",
      "effect": "HP最大值-现有HP减半(恢复需移除诅咒)",
      "removal": "神殿特殊仪式 (消耗1000GP)"
    },
    {
      "type": "blessing",
      "id": "blessing_of_the_ancients",
      "name": "古贤祝福",
      "source": "adv_012_success_bonus",
      "effect": "长休后获得1d4临时HP",
      "removal": "永不"
    },
    {
      "type": "reputation",
      "id": "marked_by_thieves_guild",
      "name": "盗贼公会注意",
      "source": "adv_008_choice_betrayal",
      "effect": "城镇中随机遭遇扒窃事件",
      "removal": "完成盗贼公会任务线"
    }
  ]
}
```

---

## 8. 知识传承系统

### 8.1 继承触发条件

角色状态变为 `dead` 或 `retired` 时触发继承流程。

### 8.2 继承数据模型 `[CUSTOM]`

```json
{
  "inheritance": {
    "from_character": {
      "character_id": "char_dead_001",
      "name": "索林·铁锤",
      "class": "fighter",
      "level": 5,
      "cause": "death_in_combat",
      "final_adventure_id": "adv_015"
    },
    "to_character": "char_new_002",
    "inherited_skill": {
      "skill_name": "athletics",
      "skill_source_character_level": 5,
      "transfer_type": "half_proficiency",
      "mechanical": "新角色获得运动技能熟练 (如果已有则获得专精)"
    },
    "inherited_knowledge_tags": [
      {
        "tag": "dragon_lair_known",
        "description": "前人已探索过龙穴的陷阱分布",
        "mechanical": "下次遇到龙穴主题冒险时, 寻找陷阱的察觉检定+2"
      },
      {
        "tag": "undead_weakness_known",
        "description": "记忆中记载了如何有效对付亡灵",
        "mechanical": "对亡灵敌人第一次攻击优势"
      }
    ],
    "legacy_points": 10,
    "legacy_point_uses": [
      {
        "cost": 1,
        "effect": "新角色获得额外50起始金币",
        "used": false
      },
      {
        "cost": 3,
        "effect": "新角色起始等级+1 (但战斗经验获取-20%)",
        "used": false
      },
      {
        "cost": 5,
        "effect": "继承一件已绑定的魔法装备",
        "used": false
      }
    ],
    "inherited_equipment": [],
    "biography": "矮人战士索林·铁锤，在第七次冒险中为掩护队友撤退，独自面对亡灵军团。他的最后一击是一座倒塌的石柱，为同伴赢得了逃跑的时间。酒馆的壁炉旁, 他的斧子至今还挂着。"
  }
}
```

### 8.3 传承规则

| 可传承内容 | 条件 | 效果 |
|------------|------|------|
| 1个技能 | 自动 | 新角色获得该技能熟练 (若同职业可选专精) |
| 知识标签 | 基于冒险日志中的关键事件 | 遇到类似场景/敌人时获得数值加成 |
| 传承点数 | floor(dead_character_total_xp / 500) | 可用于购买传承加成 |
| 绑定装备 | 消耗3传承点 | 选1件同调中的魔法装备传递 |
| 英雄传记 | 自动生成 | LLM根据完整冒险日志生成, 存入英雄之壁 |

### 8.4 知识标签效果示例

| 知识标签 | 获取条件 | 效果 |
|----------|----------|------|
| dragon_lair_known | 完成1次龙类Boss冒险 | 龙穴陷阱察觉+2 |
| undead_weakness_known | 对抗亡灵Boss战斗记录 | 对亡灵第一次攻击优势 |
| goblin_tactics_memorized | 完成3次哥布林主题冒险 | 对哥布林类敌人先攻+2 |
| trapped_hallway_marked | 在陷阱中倒地过 | 地城走廊感知被动+1 |
| secret_passage_sense | 发现过5个以上隐藏路径 | 察觉隐藏门+3 |
| poison_antidote_recipe | 被中毒KO过 | 辨识毒物优势 |

### 8.5 英雄传记生成 `[CUSTOM]`

```
LLM Prompt: 传记生成

System: 你是酒馆说书人，为逝去或退休的冒险者撰写英雄传记。

Input:
- 角色姓名/种族/职业/等级
- 冒险日志摘要 (adventure_log中的 memorable_events)
- 关系记录 (与谁友好/宿敌)
- 战斗统计 (总击杀/伤害/暴击)
- 死亡/退休场景描述

Output: 2-3段传记 (中文), 记录在酒馆英雄之壁

示例输出:
"矮人战士索林·铁锤，生前完成了7次冒险，击杀47名敌人。
他最骄傲的一战是在被遗忘的回廊中，以一把战锤击碎了三只
食人魔。在最后的冒险中，他为掩护队友撤退，独自抵挡亡灵
大军。酒馆的壁炉旁，他的斧子至今还挂着。"
```

---

## 9. 测试规格

### 9.1 单元测试用例

#### Test Suite: Stat Calculation Pipeline

```
TEST 1: 属性调整值计算
  Input: ability_score = 16
  Expected: floor((16-10)/2) = 3
  Edge: 3 -> -4, 10 -> 0, 20 -> 5, 1 -> -5

TEST 2: 熟练加值计算
  Input: level = 3
  Expected: floor((3-1)/4) + 2 = 2
  Table: Lv1=2 / Lv4=2 / Lv5=3 / Lv9=4 / Lv13=5 / Lv17=6 / Lv20=6

TEST 3: 技能调整值 (熟练)
  Given: Fighter Lv3, STR 16, Athletics熟练, PB=2
  Expected: 3 + 2 = 5

TEST 4: 技能调整值 (专精)
  Given: Rogue Lv5, DEX 18, Stealth专精, PB=3
  Expected: 4 + 6 = 10

TEST 5: 技能调整值 (未熟练)
  Given: Wizard Lv1, INT 16, Arcana未熟练, PB=2
  Expected: 3 (不加PB)

TEST 6: HP计算 Lv1
  Given: Fighter d10, CON 14(+2)
  Expected: 10 + 2 = 12

TEST 7: HP计算 Lv1 Hill Dwarf
  Given: Hill Dwarf Fighter d10, CON 14(+2) + 种族坚韧
  Expected: 10 + 2 + 1 = 13

TEST 8: HP升级 (Lv2)
  Given: Lv1 HP=12, Fighter d10期望=6, CON=+2
  Expected: 12 + 6 + 2 = 20

TEST 9: AC计算 (重甲+盾牌)
  Given: Chain Mail(16), Shield(+2), DEX 10
  Expected: 16 + 2 = 18

TEST 10: AC计算 (轻甲)
  Given: Studded Leather(12), DEX 16(+3)
  Expected: 12 + 3 = 15

TEST 11: AC计算 (中甲, DEX高)
  Given: Half Plate(15), DEX 18(+4, capped at +2)
  Expected: 15 + 2 = 17

TEST 12: 法术DC
  Given: Wizard Lv5, INT 16(+3), PB=3
  Expected: 8 + 3 + 3 = 14

TEST 13: 法术攻击加值
  Given: Wizard Lv5, INT 16(+3), PB=3
  Expected: 3 + 3 = 6

TEST 14: 先攻
  Given: DEX 14(+2), 无其他加成
  Expected: 2

TEST 15: 被动察觉 (熟练)
  Given: WIS 12(+1), Perception熟练, PB=2
  Expected: 10 + 1 + 2 = 13

TEST 16: 被动察觉 (未熟练)
  Given: WIS 12(+1), Perception未熟练, PB=2
  Expected: 10 + 1 = 11
```

#### Test Suite: Level-Up Verification

```
TEST 17: Lv1 -> Lv2 Fighter
  Verify: PB保持2, HP增加 (6+CON_mod), 获得Action Surge
  Verify: Spell slots unchanged (0)

TEST 18: Lv2 -> Lv3 Wizard
  Verify: PB保持2, HP增加 (4+CON_mod)
  Verify: 1st slots change from 3 to 4, 2nd slots become 2
  Verify: No new class feature (Wizard Lv3 has no feature)

TEST 19: Lv3 -> Lv4 Rogue
  Verify: PB保持2, HP增加 (5+CON_mod)
  Verify: 获得ASI, 触发 level_up_asi 信号
  Verify: Sneak Attack stays at 2d6

TEST 20: Lv4 -> Lv5 Fighter
  Verify: PB从2变为3, HP增加 (6+CON_mod)
  Verify: 获得 Extra Attack

TEST 21: Lv5 Wizard: 法术位验证
  Verify: 1st=4, 2nd=3, 3rd=2
  Verify: 解锁3环法术

TEST 22: Lv5 Rogue: 偷袭验证
  Verify: Sneak Attack = 3d6
  Verify: 获得 Uncanny Dodge
```

#### Test Suite: Condition Stacking Rules

```
TEST 23: 同名条件不堆叠
  Apply: Poisoned (source A, 3 rounds) + Poisoned (source B, 5 rounds)
  Expected: 1个Poisoned, duration = 5 rounds

TEST 24: 不同条件共存
  Apply: Blinded + Prone + Poisoned
  Expected: 3个独立条件同时生效

TEST 25: 优势/劣势互斥
  Scenarios:
    (a) 2 Advantage + 1 Disadvantage = Neutral (平骰)
    (b) 1 Advantage + 0 Disadvantage = Advantage
    (c) 0 Advantage + 2 Disadvantage = Disadvantage
    (d) 1 Advantage + 1 Disadvantage = Neutral

TEST 26: 来源刷新
  Apply: Blinded (source A, 3 rounds)
  After 2 rounds: Apply Blinded (source A, 4 rounds)
  Expected: duration reset to 4

TEST 27: 免疫
  Character has Sleep Immunity (Elf)
  Apply: Sleep effect
  Expected: 免疫, 不添加条件

TEST 28: 麻痹包含失能
  Apply: Paralyzed
  Verify: both "paralyzed" AND functionally "incapacitated" (no actions/reactions)
```

#### Test Suite: Relationship Threshold Crossing

```
TEST 29: 阈值从2跨越到5
  Given: value=2, Friendly
  Change: +3 (shared_loot + saved_from_death)
  Expected: value=5, Bonded

TEST 30: 阈值从-3跨越到-5
  Given: value=-3, Hostile
  Change: -2 (betrayed_trust)
  Expected: value=-5, Nemesis

TEST 31: 阈值从-6提升到-4
  Given: value=-6, Nemesis
  Change: +2 (saved_in_combat)
  Expected: value=-4, Hostile

TEST 32: 关系类型判定
  Given: value=5, coop_kills=4
  Expected: 触发 comrade 关系类型

TEST 33: 关系类型条件消失
  Given: comrade, value=5
  Change: -1
  Expected: value=4 -> Friendly, 关系类型保留但效果消失 (30天宽限)
```

### 9.2 集成测试场景

```
TEST 34: 完整角色创建流程
  Step 1: 选择种族 (Dwarf, Hill) + 职业 (Fighter)
  Step 2: 验证属性生成符合 Standard Array + 种族加成
  Step 3: 验证HP=10+CON_mod+种族坚韧
  Step 4: 验证技能熟练: Athletics/Intimidation/Perception (Fighter+Elf or Fighter基础)
  Step 5: 验证起始装备分配正确
  Step 6: 验证LLM叙事层生成成功 + Schema合法

TEST 35: 角色创建 -> 装备 -> 战斗数值验证
  Given: Lv1 Dwarf Fighter (STR 17, DEX 10, CON 16)
  Equip: Chain Mail + Shield (+ Battleaxe)
  Verify: AC = 16 + 2 = 18
  Verify: 战斧命中 = PB(2) + STR_mod(3) = +5
  Verify: 战斧伤害 = 1d8 + 3
  Verify: 先攻 = 0
  Verify: 被动察觉 = 10 + WIS_mod + (如果Fighter取了Perception熟练 + PB)

TEST 36: 多角色战斗交互
  Given: Fighter (AC 18, HP 28), Wizard (AC 12, HP 18), Rogue (AC 15, HP 22)
  Scenario: Wizard casts Fireball (DC 14, 8d6 fire)
    - Rogue Lv7+ Evasion: DEX save success = 0 damage
    - Fighter: DEX save failure = full damage
  Verify: 正确应用Evasion和普通豁免规则

TEST 37: 关系变化 -> 战斗加成
  Given: Fighter与Rogue关系值=5, 类型=comrade
  Verify: 两人相邻时, Fighter攻击+1命中
  Given: 关系因误伤降到4
  Verify: +1命中效果消失

TEST 38: 升级 -> 法术位恢复
  Given: Lv3 Wizard, 短休
  Verify: 1环法术位恢复到 ceil(4/2)=2 (一半向上取整)
  Verify: Arcane Recovery 恢复 ceil(3/2)=2环位 (可选1环+1环 or 一个2环)

TEST 39: 死亡豁免新规则
  Given: Fighter HP降至0, 倒地
  Round 1 (无治疗): rounds_without_healing=1
  Round 2 (无治疗): rounds_without_healing=2
  Round 3 (无治疗): rounds_without_healing=3 -> DEATH
  Alternative Round 2 (被治疗): rounds_without_healing 重置为0, HP恢复
```

### 9.3 边缘情况目录

```
=== 属性极端值 ===
EDGE 1: 属性score=1, 调整值=-5
EDGE 2: 属性score=20, 调整值=+5 (Lv1上限)
EDGE 3: 属性score=20+ASI选择再+2 (max=20, 超过不生效)
EDGE 4: 魔法物品/效果可将属性推至22-30 (调整值+6到+10)

=== 条件交互 ===
EDGE 5: 昏迷(Unconscious) + 束缚(Restrained)
  -> 攻击优势(昏迷5尺内自动暴击) + 束缚攻击优势
  -> 但优势不堆叠, 暴击仍生效

EDGE 6: 目盲(Blinded)攻击隐形(Invisible)目标
  -> Blinded: 对目标攻击劣势 + 对其攻击优势
  -> Invisible: 对观察者优势, 对其劣势
  -> 结果: 无优无劣 (劣势+优势=平骰)

EDGE 7: 中毒 + 恐慌 + 束缚
  -> 3个劣势来源 (攻击), 1个优势来源 (任意)
  -> 结果: 劣势 (优势/劣势互斥后看剩余)

=== 技能检定 ===
EDGE 8: Reliable Talent (Rogue Lv11) + 劣势
  -> 熟练技能最低骰10, 但劣势仍适用 (骰2次取低, 然后最低值至少10? 不, 先骰2次取低, 再应用Reliable Talent)

EDGE 9: Bard Jack of All Trades + 未熟练技能 + Expertise
  -> 不适用 (Jack of All Trades仅对完全非熟练且有训练来源的技能)
  -> 实际上Bard cannot have expertise on non-proficient skills

=== 法术相关 ===
EDGE 10: Warlock 5级法术位+Full Caster 5级法术位兼职
  -> Pact Magic (3rd level, 2 slots, 短休恢复)
  -> Full Caster slots (4/3/2 长休恢复)
  -> 两者独立跟踪

EDGE 11: 反应施法 (War Caster + Counterspell)
  -> 借机攻击用掉反应 -> Spell
  -> 同一轮内不能再Counterspell

=== 装备超限 ===
EDGE 12: 背包槽满 (14格/14格) + 拾取新物品
  -> 移动速度减半, 无法疾跑
  -> 丢弃物品后立即恢复

EDGE 13: 同调满 (3/3) + 新魔法物品需要同调
  -> 必须先取消1个同调 (短休过程中)
  -> 解除的物品效果立即消失

=== 伤疤 ===
EDGE 14: 同一角色重复受到相同类型伤害
  -> 已有惧焰伤疤, 再次受到大量火焰伤害
  -> 选项: 不生成新伤疤 / 替换为更重的火焰伤疤 / 叠加 (不同ID)
  -> 本游戏策略: 选不同ID的同类伤疤(如从惧焰变为炙肺), 或加重现有伤疤

EDGE 15: 伤疤补偿>惩罚
  -> 如独眼: -2察觉 + 60尺黑暗视觉
  -> 对于已有黑暗视觉的Dwarf: 黑暗视觉范围扩大到120尺
  -> 对于无黑暗视觉的Human: 新增60尺黑暗视觉
```

### 9.4 平衡验证公式 `[CUSTOM]`

```
=== 队伍战斗力指数 (Party Power Index) ===

PPI = (avg_level * 2) + (avg_ac - 10) + (avg_primary_stat - 10)

其中:
  avg_level = 队伍平均等级
  avg_ac = 队伍平均AC
  avg_primary_stat = 队伍成员主属性的平均值 (每人取最高1项属性)

CR建议值 = PPI * 0.5 (Easy) 到 PPI * 1.3 (Deadly)

验证: Lv3 4人队 (Fighter/Wizard/Rogue/Cleric)
  avg_level = 3
  avg_ac = (18+12+15+18)/4 = 15.75
  avg_primary_stat = (16+16+17+16)/4 = 16.25
  PPI = (3*2) + (15.75-10) + (16.25-10) = 6 + 5.75 + 6.25 = 18
  Easy CR: 18 * 0.5 = 9 -> CR 2-3 encounter
  Deadly CR: 18 * 1.3 = 23.4 -> CR 5-6 encounter

验证: Lv1 单人 (Human Fighter, AC 18, STR 16)
  avg_level = 1
  avg_ac = 18
  avg_primary_stat = 16
  PPI = (1*2) + (18-10) + (16-10) = 2 + 8 + 6 = 16
  Easy CR: 16 * 0.5 = 8 -> CR 2 encounter
  Deadly CR: 16 * 1.3 = 20.8 -> CR 4-5 encounter

注意: 此公式为粗略估算，不适用于CR 20+的高等级遭遇。高等级遭遇应参考D&D 5e DMG第82页的XP阈值表。
```

---

## 10. 验收标准 (Acceptance Criteria)

> **本节定义角色系统"完成"的标准。所有标准必须通过QA验证后，系统才能标记为"已实现"。**

### 10.1 核心功能验收

| # | 验收标准 | 测试方法 | 通过条件 |
|---|----------|----------|----------|
| AC-1 | 角色创建管线完整运行 | 端到端测试 | 选择种族+职业→生成完整角色数据→写入SQLite→读取一致 |
| AC-2 | 属性生成产生可感知差异 | 多次生成同种族同职业 | 10次生成中至少3次的主属性值不同（因自由点分配） |
| AC-3 | HP公式永远不会产生≤0 | 单元测试 | CON=1到CON=20的所有极端值测试通过 |
| AC-4 | 伤疤系统以惩罚为主 | 代码审查 | 80%+伤疤无补偿，剩余补偿值为原始值的50% |
| AC-5 | 传承点公式与failure-growth对齐 | 跨文档验证 | 两个GDD使用相同公式：floor(xp/500) |
| AC-6 | Personality生成5维度 | LLM输出验证 | 5个维度全部非空且互不相同 |

### 10.2 数值平衡验收

| # | 验收标准 | 测试方法 | 通过条件 |
|---|----------|----------|----------|
| AC-7 | Lv1→5升级节奏合理 | 模拟计算 | 9-15次短冒险达到Lv5（4.5-7.5小时） |
| AC-8 | PPI公式产生合理CR | 边界测试 | Lv1 solo: CR 2-5; Lv3 4人: CR 2-6; Lv20 4人: CR 13-17 |
| AC-9 | 伤疤严重度分布符合权重 | 统计测试 | 100次生成中：轻度60-80%，中度15-35%，重度2-10% |
| AC-10 | 关系变化不会导致单次剧变 | 代码审查 | 单次关系变化≤±3 |

### 10.3 叙事质量验收

| # | 验收标准 | 测试方法 | 通过条件 |
|---|----------|----------|----------|
| AC-11 | LLM角色生成通过Schema验证 | 自动化测试 | 100次生成中≥95次通过JSON Schema验证 |
| AC-12 | 角色backstory≥50字符 | 自动化测试 | 所有生成的backstory长度≥50字符 |
| AC-13 | Personality标签互不相同 | 自动化测试 | 5个维度的值互不相同 |
| AC-14 | 伤疤叙事符合"惩罚为主"哲学 | 人工审查 | 伤疤叙事不暗示"获得超能力" |

### 10.4 性能验收

| # | 验收标准 | 测试方法 | 通过条件 |
|---|----------|----------|----------|
| AC-15 | 角色JSON序列化<5ms | 性能测试 | 100次序列化平均<5ms |
| AC-16 | 角色JSON反序列化<10ms | 性能测试 | 100次反序列化平均<10ms |
| AC-17 | SQLite查询<1ms | 性能测试 | 单次角色查询<1ms |

---

**文档版本历史**:

| 版本 | 日期 | 变更 |
|------|------|------|
| v1.0 | 2026-05-04 | 初始版本 |
| v1.1 | 2026-05-09 | 设计评审修订：新增Player Fantasy/Dependencies/Tuning Knobs/Acceptance Criteria章节；修复HP公式最小值地板；对齐传承点定义；重写伤疤设计哲学；属性生成引入受控随机性；修复PPI公式；扩展Personality维度；补全缺失公式 |
