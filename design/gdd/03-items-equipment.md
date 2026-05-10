# 物品与装备系统 — 技术设计文档

> **Subsystem**: Items & Equipment  
> **Game**: 《酒馆与命运》(Tavern & Destiny)  
> **Rules Reference**: DND 5e SRD
> **Language Policy**: 游戏文本统一采用简体中文，技术标识符使用英文snake_case  
> **Version**: 1.2 — MVP + Phase 2 scope  
> **Status**: 设计评审修订中 (MAJOR REVISION — 阻断项修复)

---

## 1. 概述

### 1.1 系统目标

物品与装备系统负责游戏中所有物品的**数据定义、生成、存储、交易、装备和计算**。本系统是连接角色系统、战斗系统和酒馆系统的中枢——装备直接影响角色的战斗数值，战利品驱动冒险动机，制作系统为酒馆设施提供经济出口。

### 1.2 设计原则

| 原则 | 说明 |
|------|------|
| **DND 5e SRD 兼容** | 武器属性、护甲分类、伤害类型完全遵循 5e SRD，确保数值体系有据可查 |
| **槽位制装备** | 采用装备槽位 (slot) 限制，而非负重 (weight) 计算。见 GDD §5.3 明确要求 |
| **稀有度驱动战利品** | 战利品生成以稀有度为一级筛选维度，类型/属性随后派生 |
| **LLM 只做皮肤层** | 物品的 flavor text 由 LLM 生成，数值属性和功能逻辑由程序控制。遵循 GDD §4.2 架构原则 |
| **永久风险** | 装备可能损坏、丢失，灵魂绑定物品是唯一的例外 |

### 1.3 系统边界与上下游关系

```
┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│  Character   │◄────│  Items &     │────►│   Combat     │
│  System      │     │  Equipment   │     │   System     │
│  ─────────── │     │  (本系统)     │     │  ─────────── │
│ · 装备属性加成│     │              │     │ · 武器伤害骰  │
│ · 熟练项判定 │     │              │     │ · AC 计算    │
│ · 种族加成   │     │              │     │ · 法术加值   │
└──────┬───────┘     └──────┬───────┘     └──────┬───────┘
       │                    │                    │
       │    ┌───────────────┴───────────────┐    │
       └────┤         Tavern System         ├────┘
            │  ──────────────────────────── │
            │ · 铁匠铺 (打造/修复)           │
            │ · 炼金台 (药水/卷轴)           │
            │ · 图书馆 (法术卷轴/研究)       │
            │ · 商店交易                     │
            └───────────────────────────────┘
                         │
            ┌────────────┴────────────┐
            │     Adventure System     │
            │  ─────────────────────── │
            │ · 战利品生成 (loot_tier) │
            │ · 冒险结算物品鉴定       │
            │ · 失败惩罚装备损坏       │
            └─────────────────────────┘
                         │
            ┌────────────┴────────────┐
            │      LLM Gateway         │
            │  ─────────────────────── │
            │ · Copywriter Agent       │
            │   (物品描述生成)          │
            └─────────────────────────┘
```

---

## 1A. 玩家体验幻想 (Player Fantasy)

> **设计支柱对齐：** 物品系统是三大支柱的交汇点——装备选择驱动**角色驱动的故事**（P1），装备决策体现**战术深度**（P2），物品的损坏与传承塑造**持续演进的世界**（P3）。

### 核心情感承诺

物品系统围绕四层情感体验设计：

| 情感 | 承诺 | 设计含义 |
|------|------|----------|
| **发现的惊喜** | 每次战利品掉落都可能是改变构筑方向的契机 | 稀有物品不只是"更强"，而是"不同"——它们打开新的玩法可能性 |
| **选择的重量** | 装备决策有真实的机会成本 | 同调上限（3件）、槽位互斥、双手武器占两槽——每个"是"都意味着一个"否" |
| **物品的故事** | 魔法物品有自己的来历和传说 | 程序化叙事组合系统生成的基线描述 + LLM Copywriter Agent 的在线增强 — 每件物品都承载着独一无二的叙事身份 |
| **使用的后果** | 耐久度和灵魂绑定让物品有生命周期 | 物品会损坏、会丢失、会传承——它们不是静态数值，而是有生命的伙伴 |

### 设计测试

面对任何物品设计决策时，用以下四个测试验证：

1. **惊喜测试** — 如果玩家看到这件物品时没有"哦！"的反应，这个设计就失败了。物品应该改变玩家对角色构筑的思考，而不只是"+1 更大的数字"。

2. **选择测试** — 装备这件物品是否意味着放弃另一件？如果没有机会成本，物品就没有意义。同调槽位（最多3件）、装备槽位互斥、双手武器规则都是为了制造有意义的选择。

3. **故事测试** — 这件物品能否承载一个故事？（来历、传说、诅咒）一把"炽焰长剑"不如一把"曾属于堕落圣骑士的、在黑暗中低语的长剑"。程序化叙事系统为每件物品生成独特的基线故事，LLM Copywriter Agent 在线时进一步丰富叙事深度。

4. **构筑测试** — 这件物品是否能让玩家思考新的构筑方向？一把附带火焰伤害的武器可能让玩家重新考虑火系法术的搭配；一件提供潜行优势的护甲可能让战士考虑更隐秘的战术。

### 反支柱约束

物品系统**必须遵守**游戏的反支柱——本游戏**不是刷关打宝游戏**：

- ❌ **物品不应该只是"+X 数值"的数值棍** — 每件魔法物品必须有独特的机制或叙事身份，而非单纯的数值提升
- ❌ **物品不应该让玩家"不敢使用"** — 耐久度惩罚必须有合理的修复出口（铁匠铺），不能让玩家因为害怕损坏而把好装备锁在箱子里
- ❌ **物品不应该让玩家"刷30次"才能获得** — 战利品生成依赖概率表而非重复刷取；任何物品都可以通过制作、商店或任务奖励等替代路径获得

---

## 2. 物品数据模型

### 2.1 核心 Item JSON Schema

```json
{
  "$id": "item_base",
  "type": "object",
  "required": [
    "id", "name", "type", "rarity", "description",
    "value_gp", "icon_path"
  ],
  "properties": {
    "id": {
      "type": "string",
      "pattern": "^item_[a-z0-9_]+$",
      "description": "唯一标识符，如 item_longsword_01"
    },
    "name": {
      "type": "string",
      "description": "显示名称，如 '长剑'。若有附魔则显示为 '[前缀] 长剑 [后缀]'"
    },
    "name_en": {
      "type": "string",
      "description": "英文名称，用于内部查找和 SRD 对照，如 'Longsword'"
    },
    "type": {
      "type": "string",
      "enum": [
        "weapon", "armor", "shield", "helmet", "cloak", "boots",
        "potion", "scroll", "wand", "ring", "amulet",
        "misc", "quest"
      ],
      "description": "物品类型，决定装备槽位和行为"
    },
    "subtype": {
      "type": "string",
      "description": "子类型。weapon → 武器名(e.g. longsword)；armor → 护甲名(e.g. chain_mail)；potion → 药水名"
    },
    "rarity": {
      "type": "string",
      "enum": ["common", "uncommon", "rare", "very_rare", "legendary", "artifact"],
      "description": "稀有度等级"
    },
    "description": {
      "type": "string",
      "description": "由 Copywriter Agent 生成的叙事描述文本"
    },
    "weight": {
      "type": "number",
      "description": "重量（磅），保留用于 UI 显示和部分交互判定（如压强陷阱），但不影响装备槽位"
    },
    "value_gp": {
      "type": "integer",
      "minimum": 1,
      "description": "基础金币价值（未含稀有度加成）"
    },
    "icon_path": {
      "type": "string",
      "description": "像素图标资源路径，如 'assets/items/weapons/longsword.png'"
    },
    "level_requirement": {
      "type": "integer",
      "minimum": 1,
      "default": 1,
      "description": "最低角色等级要求（部分传奇/神器物品）"
    },
    "stackable": {
      "type": "boolean",
      "default": false,
      "description": "是否可堆叠。消耗品 (potion/scroll) 默认 true"
    },
    "max_stack": {
      "type": "integer",
      "default": 1,
      "description": "最大堆叠数量。消耗品通常为 99"
    },

    "soulbound": {
      "type": "boolean",
      "default": false,
      "description": "是否灵魂绑定。见 §7"
    },
    "unique": {
      "type": "boolean",
      "default": false,
      "description": "是否为唯一物品（全游戏只能存在一把）"
    },
    "quest_id": {
      "type": "string",
      "description": "关联的任务 ID（仅 quest 类型物品）"
    },

    "weapon": {
      "type": "object",
      "description": "武器数据块 — 仅 type=weapon 时存在",
      "properties": {
        "damage_dice": {
          "type": "string",
          "pattern": "^\\d+d\\d+$",
          "description": "伤害骰，如 '1d8'（长剑单手）、'1d10'（长剑双手）"
        },
        "damage_dice_versatile": {
          "type": "string",
          "pattern": "^\\d+d\\d+$",
          "description": "双手持握时的伤害骰，如 '1d10'。仅 versatile 武器需要"
        },
        "damage_type": {
          "type": "string",
          "enum": [
            "bludgeoning", "piercing", "slashing",
            "acid", "cold", "fire", "force",
            "lightning", "necrotic", "poison",
            "psychic", "radiant", "thunder"
          ]
        },
        "damage_type_versatile": {
          "type": "string",
          "description": "双手持握时的伤害类型（通常等同 damage_type，某些魔法武器可不同）"
        },
        "weapon_category": {
          "type": "string",
          "enum": ["simple", "martial"]
        },
        "weapon_range": {
          "type": "string",
          "enum": ["melee", "ranged"]
        },
        "properties": {
          "type": "array",
          "items": {
            "type": "string",
            "enum": [
              "ammunition", "finesse", "heavy",
              "light", "loading", "reach",
              "special", "thrown", "two_handed",
              "versatile", "monk"
            ]
          }
        },
        "range_normal": {
          "type": "integer",
          "description": "远程武器普通射程（尺）"
        },
        "range_long": {
          "type": "integer",
          "description": "远程武器最大射程（尺）"
        },
        "ammunition_type": {
          "type": "string",
          "description": "弹药类型 ID，如 'arrow'、'bolt'（仅 ammunition 属性武器）"
        },
        "crit_range": {
          "type": "integer",
          "default": 20,
          "description": "暴击范围下限（通常为 20，某些魔法武器可扩展为 19-20）"
        },
        "crit_multiplier": {
          "type": "integer",
          "default": 2,
          "description": "暴击时伤害骰数量倍数（本游戏暴击规则为最大化伤害骰而非双骰，见 GDD §5.4。此字段保留用于可扩展设计）"
        }
      }
    },

    "armor": {
      "type": "object",
      "description": "护甲数据块 — 仅 type=armor 或 type=shield 时存在",
      "properties": {
        "armor_category": {
          "type": "string",
          "enum": ["light", "medium", "heavy", "shield"]
        },
        "base_ac": {
          "type": "integer",
          "description": "基础 AC 值"
        },
        "max_dex_bonus": {
          "type": "integer",
          "minimum": 0,
          "nullable": true,
          "description": "敏捷调整值上限。轻甲无上限(null)、中甲最大为 2、重甲为 0"
        },
        "strength_requirement": {
          "type": "integer",
          "minimum": 0,
          "nullable": true,
          "description": "力量属性最低要求。未满足时移动速度 -10尺。通常仅重甲需要"
        },
        "stealth_disadvantage": {
          "type": "boolean",
          "default": false,
          "description": "是否在潜行检定时具有劣势"
        },
        "don_time": {
          "type": "string",
          "enum": ["1_action", "1_minute", "5_minutes", "10_minutes"],
          "description": "穿戴（don）所需时间"
        },
        "doff_time": {
          "type": "string",
          "enum": ["1_action", "1_minute", "5_minutes"],
          "description": "卸除（doff）所需时间"
        }
      }
    },

    "shield": {
      "type": "object",
      "description": "盾牌数据块 — 仅 type=shield 时存在",
      "properties": {
        "ac_bonus": {
          "type": "integer",
          "description": "额外 AC 加值（标准盾牌为 +2）"
        }
      }
    },

    "consumable": {
      "type": "object",
      "description": "消耗品数据块 — 仅 type=potion/scroll 时存在",
      "properties": {
        "uses": {
          "type": "integer",
          "default": 1,
          "description": "使用次数（通常为 1）"
        },
        "effect_id": {
          "type": "string",
          "description": "效果 ID，映射到 CombatSystem 中的 Effect 资源"
        },
        "effect_params": {
          "type": "object",
          "description": "效果参数覆盖，如治疗量、持续时间等"
        },
        "duration_rounds": {
          "type": "integer",
          "description": "效果持续轮数（0 表示瞬发）"
        },
        "target_type": {
          "type": "string",
          "enum": ["self", "touch", "ranged", "aoe"],
          "description": "目标类型"
        },
        "consumable_category": {
          "type": "string",
          "enum": [
            "healing", "buff", "debuff", "utility",
            "damage", "spell_scroll", "oil"
          ]
        },
        "spell_id": {
          "type": "string",
          "description": "关联法术 ID（仅 spell_scroll 类型）"
        },
        "spell_level": {
          "type": "integer",
          "description": "法术环位（仅 spell_scroll 类型）"
        }
      }
    },

    "magical": {
      "type": "object",
      "description": "魔法属性数据块 — 仅稀有度 uncommon 及以上的装备存在",
      "properties": {
        "is_magical": {
          "type": "boolean",
          "default": false
        },
        "attunement_required": {
          "type": "boolean",
          "default": false,
          "description": "是否需要同调才能使用魔法属性"
        },
        "enchantment_slots": {
          "type": "integer",
          "default": 0,
          "minimum": 0,
          "maximum": 5,
          "description": "附魔槽数量。由稀有度决定"
        },
        "enchantments": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/enchantment"
          },
          "description": "当前应用的附魔列表"
        },
        "charges": {
          "type": "object",
          "description": "充能系统（仅法杖/魔杖类）",
          "properties": {
            "max_charges": { "type": "integer" },
            "current_charges": { "type": "integer" },
            "recharge_rate": {
              "type": "string",
              "pattern": "^\\d+d\\d+$",
              "description": "每日黎明恢复的充能骰，如 '1d6+1'"
            }
          }
        },
        "curse": {
          "type": "object",
          "description": "诅咒属性",
          "properties": {
            "is_cursed": { "type": "boolean", "default": false },
            "curse_id": { "type": "string" },
            "revealed": { "type": "boolean", "default": false },
            "removable_by": { "type": "string", "default": "remove_curse" }
          }
        }
      }
    },

    "condition": {
      "type": "object",
      "description": "耐久度数据块",
      "properties": {
        "max_durability": { "type": "integer", "minimum": 1, "default": 100 },
        "current_durability": { "type": "integer", "default": 100 },
        "condition_level": {
          "type": "string",
          "enum": ["pristine", "good", "worn", "damaged", "broken"]
        },
        "degradation_amount": {
          "type": "integer",
          "default": 0,
          "description": "每次触发退化时减少的耐久值"
        }
      }
    },

    "recipe_source": {
      "type": "string",
      "nullable": true,
      "description": "制作该物品的配方 ID"
    }
  },

  "$defs": {
    "enchantment": {
      "type": "object",
      "required": ["id", "affix_type", "effect"],
      "properties": {
        "id": { "type": "string" },
        "affix_type": {
          "type": "string",
          "enum": ["prefix", "suffix", "implicit"]
        },
        "name_affix": {
          "type": "string",
          "description": "显示名称缀词，如 '炽焰的' (prefix) 或 'of Fire' (suffix)"
        },
        "effect": {
          "type": "object",
          "properties": {
            "stat_modifiers": { "type": "object" },
            "damage_bonus": { "type": "object" },
            "on_hit_effect": { "type": "string" },
            "special_ability": { "type": "string" }
          }
        },
        "tier": {
          "type": "integer",
          "minimum": 1,
          "maximum": 5,
          "description": "附魔等级，决定数值强度"
        },
        "rarity_requirement": {
          "type": "string",
          "description": "此附魔可出现的稀有度下限"
        }
      }
    }
  },

  "allOf": [
    {
      "if": { "properties": { "type": { "const": "weapon" } } },
      "then": { "required": ["weapon"] }
    },
    {
      "if": { "properties": { "type": { "const": "armor" } } },
      "then": { "required": ["armor"] }
    },
    {
      "if": { "properties": { "type": { "const": "shield" } } },
      "then": { "required": ["shield"] }
    },
    {
      "if": { "properties": { "type": { "enum": ["potion", "scroll"] } } },
      "then": { "required": ["consumable"] }
    }
  ]
}
```

### 2.2 完整物品类型枚举

| type | 说明 | 装备槽位 | 可附魔 | 可堆叠 |
|------|------|----------|--------|--------|
| `weapon` | 武器（近战/远程） | Main Hand / Off Hand / Both | 是 | 否 |
| `armor` | 护甲（轻/中/重） | Armor | 是 | 否 |
| `shield` | 盾牌 | Off Hand | 是 | 否 |
| `helmet` | 头盔 | Helmet | 是 | 否 |
| `cloak` | 披风/斗篷 | Cloak | 是 | 否 |
| `boots` | 靴子 | Boots | 是 | 否 |
| `potion` | 药水 | Backpack (使用) | 否 | 是 (最大99) |
| `scroll` | 卷轴 | Backpack (使用) | 否 | 是 (最大99) |
| `wand` | 魔杖 | Main Hand | 是 (有限) | 否 |
| `ring` | 戒指 | Ring 1 / Ring 2 | 是 | 否 |
| `amulet` | 护符 | Amulet | 是 | 否 |
| `misc` | 杂项（宝石/材料/道具） | Backpack | 否 | 是 |
| `quest` | 任务物品 | Backpack | 否 | 否 |

### 2.3 完整武器属性枚举 (DND 5e SRD)

| 属性 | 效果 | 示例武器 |
|------|------|----------|
| `ammunition` | 需要弹药（箭/矢）。若无弹药则无法攻击。每次远程攻击消耗 1 发弹药 | 长弓、十字弓、短弓 |
| `finesse` | 攻击掷骰和伤害掷骰可使用 DEX 或 STR 调整值（取高者） | 匕首、细剑、短剑 |
| `heavy` | 小型生物使用此武器时攻击掷骰具有劣势 | 巨剑、巨斧、长弓 |
| `light` | 适合双持战斗。副手可使用另一把 light 武器进行附赠动作攻击 | 匕首、短剑、手斧 |
| `loading` | 每回合只能使用此武器进行 1 次攻击（无视额外攻击次数） | 十字弓 |
| `reach` | 近战攻击范围增加 5 尺 | 长枪、长鞭、戟 |
| `thrown` | 可投掷使用。投掷距离使用武器自身射程（若有）或 20/60 | 手斧、标枪、匕首 |
| `two_handed` | 需要双手持握。占用 Main Hand + Off Hand 两个槽位 | 巨剑、巨斧、长弓 |
| `versatile` | 可选择单手或双手持握。双手时使用更高的伤害骰 (damage_dice_versatile) | 长剑、战锤、战斧 |
| `special` | 有特殊规则需查阅具体描述 | 长鞭（disarm）、网（restrain） |
| `monk` | 武僧武器 — 可使用 Martial Arts 骰替代武器伤害骰 | 短剑、手斧、棍棒 |
| `silvered` | 镀银武器，可穿透特定生物（狼人等）的伤害免疫 | 任意武器 (镀银变体) |

### 2.4 完整 DND 5e 武器表

#### 简易近战武器 (Simple Melee Weapons)

| id | 名称 | 伤害 | 伤害类型 | 重量 | 属性 | 价值(gp) |
|----|------|------|----------|------|------|----------|
| `item_club` | 棍棒 | 1d4 | bludgeoning | 2 | light | 0.1 |
| `item_dagger` | 匕首 | 1d4 | piercing | 1 | finesse, light, thrown (20/60) | 2 |
| `item_greatclub` | 大棒 | 1d8 | bludgeoning | 10 | two_handed | 0.2 |
| `item_handaxe` | 手斧 | 1d6 | slashing | 2 | light, thrown (20/60) | 5 |
| `item_javelin` | 标枪 | 1d6 | piercing | 2 | thrown (30/120) | 0.5 |
| `item_light_hammer` | 轻锤 | 1d4 | bludgeoning | 2 | light, thrown (20/60) | 2 |
| `item_mace` | 钉头锤 | 1d6 | bludgeoning | 4 | — | 5 |
| `item_quarterstaff` | 长棍 | 1d6 | bludgeoning | 4 | versatile (1d8) | 0.2 |
| `item_sickle` | 镰刀 | 1d4 | slashing | 2 | light | 1 |
| `item_spear` | 矛 | 1d6 | piercing | 3 | thrown (20/60), versatile (1d8) | 1 |

#### 简易远程武器 (Simple Ranged Weapons)

| id | 名称 | 伤害 | 伤害类型 | 重量 | 属性 | 射程 | 价值(gp) |
|----|------|------|----------|------|------|------|----------|
| `item_light_crossbow` | 轻弩 | 1d8 | piercing | 5 | ammunition (80/320), loading, two_handed | 80/320 | 25 |
| `item_dart` | 飞镖 | 1d4 | piercing | 0.25 | finesse, thrown (20/60) | 20/60 | 0.05 |
| `item_shortbow` | 短弓 | 1d6 | piercing | 2 | ammunition (80/320), two_handed | 80/320 | 25 |
| `item_sling` | 投石索 | 1d4 | bludgeoning | 0 | ammunition (30/120) | 30/120 | 0.1 |

#### 弹药物品 (Ammunition)

| id | 名称 | 适用武器 | 重量 | 堆叠上限 | 价值(gp) |
|----|------|----------|------|----------|----------|
| `item_arrow` | 箭矢 | 短弓、长弓 | 0.05 | 20 | 0.05 |
| `item_bolt` | 弩矢 | 轻弩、重弩、手弩 | 0.075 | 20 | 0.1 |
| `item_sling_bullet` | 投石弹丸 | 投石索 | 0.075 | 20 | 0.01 |
| `item_blowgun_needle` | 吹箭针 | 吹箭筒 | 0.01 | 50 | 0.1 |

**弹药规则**:
- 每次远程攻击消耗1发弹药
- 战斗结束后可回收50%已使用的弹药（向上取整）
- 弹药占背包槽位：每20发占1格（见角色系统§2.1.4）
- 弹药可堆叠，堆叠上限见上表

#### 军用近战武器 (Martial Melee Weapons)

| id | 名称 | 伤害 | 伤害类型 | 重量 | 属性 | 价值(gp) |
|----|------|------|----------|------|------|----------|
| `item_battleaxe` | 战斧 | 1d8 | slashing | 4 | versatile (1d10) | 10 |
| `item_flail` | 链枷 | 1d8 | bludgeoning | 2 | — | 10 |
| `item_glaive` | 长刀 | 1d10 | slashing | 6 | heavy, reach, two_handed | 20 |
| `item_greataxe` | 巨斧 | 1d12 | slashing | 7 | heavy, two_handed | 30 |
| `item_greatsword` | 巨剑 | 2d6 | slashing | 6 | heavy, two_handed | 50 |
| `item_halberd` | 戟 | 1d10 | slashing | 6 | heavy, reach, two_handed | 20 |
| `item_lance` | 骑枪 | 1d12 | piercing | 6 | reach, special | 10 |
| `item_longsword` | 长剑 | 1d8 | slashing | 3 | versatile (1d10) | 15 |
| `item_maul` | 巨锤 | 2d6 | bludgeoning | 10 | heavy, two_handed | 10 |
| `item_morningstar` | 晨星 | 1d8 | piercing | 4 | — | 15 |
| `item_pike` | 长枪 | 1d10 | piercing | 18 | heavy, reach, two_handed | 5 |
| `item_rapier` | 细剑 | 1d8 | piercing | 2 | finesse | 25 |
| `item_scimitar` | 弯刀 | 1d6 | slashing | 3 | finesse, light | 25 |
| `item_shortsword` | 短剑 | 1d6 | piercing | 2 | finesse, light | 10 |
| `item_trident` | 三叉戟 | 1d6 | piercing | 4 | thrown (20/60), versatile (1d8) | 5 |
| `item_war_pick` | 战镐 | 1d8 | piercing | 2 | — | 5 |
| `item_warhammer` | 战锤 | 1d8 | bludgeoning | 2 | versatile (1d10) | 15 |
| `item_whip` | 长鞭 | 1d4 | slashing | 3 | finesse, reach | 2 |

#### 军用远程武器 (Martial Ranged Weapons)

| id | 名称 | 伤害 | 伤害类型 | 重量 | 属性 | 射程 | 价值(gp) |
|----|------|------|----------|------|------|------|----------|
| `item_blowgun` | 吹箭筒 | 1 | piercing | 1 | ammunition (25/100), loading | 25/100 | 10 |
| `item_hand_crossbow` | 手弩 | 1d6 | piercing | 3 | ammunition (30/120), light, loading | 30/120 | 75 |
| `item_heavy_crossbow` | 重弩 | 1d10 | piercing | 18 | ammunition (100/400), heavy, loading, two_handed | 100/400 | 50 |
| `item_longbow` | 长弓 | 1d8 | piercing | 2 | ammunition (150/600), heavy, two_handed | 150/600 | 50 |
| `item_net` | 网 | — | — | 3 | special, thrown (5/15) | 5/15 | 1 |

### 2.5 完整护甲表

#### 轻甲 (Light Armor)

| id | 名称 | 基础AC | 敏捷上限 | 重量 | 潜行劣势 | 价值(gp) |
|----|------|--------|---------|------|----------|----------|
| `item_padded` | 棉甲 | 11 | null | 8 | true | 5 |
| `item_leather` | 皮甲 | 11 | null | 10 | false | 10 |
| `item_studded_leather` | 镶钉皮甲 | 12 | null | 13 | false | 45 |

#### 中甲 (Medium Armor)

| id | 名称 | 基础AC | 敏捷上限 | 重量 | 力量要求 | 潜行劣势 | 价值(gp) |
|----|------|--------|---------|------|----------|----------|----------|
| `item_hide` | 兽皮甲 | 12 | 2 | 12 | — | false | 10 |
| `item_chain_shirt` | 链甲衫 | 13 | 2 | 20 | — | false | 50 |
| `item_scale_mail` | 鳞甲 | 14 | 2 | 45 | — | true | 50 |
| `item_breastplate` | 胸甲 | 14 | 2 | 20 | — | false | 400 |
| `item_half_plate` | 半身板甲 | 15 | 2 | 40 | — | true | 750 |

#### 重甲 (Heavy Armor)

| id | 名称 | 基础AC | 敏捷上限 | 重量 | 力量要求 | 潜行劣势 | 价值(gp) |
|----|------|--------|---------|------|----------|----------|----------|
| `item_ring_mail` | 环甲 | 14 | 0 | 40 | — | true | 30 |
| `item_chain_mail` | 链甲 | 16 | 0 | 55 | STR 13 | true | 75 |
| `item_splint` | 板条甲 | 17 | 0 | 60 | STR 15 | true | 200 |
| `item_plate` | 全身板甲 | 18 | 0 | 65 | STR 15 | true | 1500 |

#### 盾牌 (Shields)

| id | 名称 | AC加成 | 重量 | 价值(gp) |
|----|------|--------|------|----------|
| `item_shield` | 盾牌 | +2 | 6 | 10 |


---

## 3. 稀有度系统

### 3.1 稀有度等级定义

| 稀有度 | 英文 | 稀有度修正值 | 附魔槽数 | 同调需求概率 | 基础价格乘数 | UI颜色 |
|--------|------|-------------|----------|-------------|-------------|--------|
| Common | common | ×1.0 | 0 | 0% | ×1 | `#9d9d9d` (灰) |
| Uncommon | uncommon | ×1.5 | 1 | 10% | ×3 | `#1eff00` (绿) |
| Rare | rare | ×2.0 | 1–2 | 40% | ×10 | `#0070dd` (蓝) |
| Very Rare | very_rare | ×3.0 | 2–3 | 70% | ×30 | `#a335ee` (紫) |
| Legendary | legendary | ×5.0 | 3 | 100% | ×100 | `#ff8000` (橙) |
| Artifact | artifact | ×10.0 | 特殊 | 100% | ×500 | `#e6cc80` (金) |

> **经济设计理念 — "掉落为主，商店为辅"**：
> - 稀有度价格乘数采用**近似线性增长**（×1/×3/×10/×30/×100/×500），与装备战力增长曲线匹配
> - Rare+ 物品主要通过**冒险掉落**获取，商店仅出售 Common/Uncommon 物品
> - 商店定位为"**补充消耗品**"而非"购买装备"
> - 制作系统允许玩家用冒险材料**定向制作** Rare+ 物品，作为掉落的补充途径

> **稀有度修正值 (rarity_modifier)**：用于战利品生成时调整掉率权重。数值越高，表示该稀有度的出现概率经基础概率调整后的实际权重。

### 3.2 稀有度概率表 — 按冒险类型

#### 短冒险 (Short Adventure, ~30 min, CR 0–4)

| 来源 | Common | Uncommon | Rare | Very Rare | Legendary | Artifact |
|------|--------|----------|------|-----------|-----------|----------|
| 普通敌人掉落 | 70% | 25% | 4% | 1% | 0% | 0% |
| 精英敌人掉落 | 40% | 35% | 18% | 5% | 2% | 0% |
| Boss 掉落 | 0% | 35% | 30% | 20% | 11% | 4% |
| 宝箱/隐藏区域 | 50% | 30% | 15% | 4% | 1% | 0% |
| 任务奖励 | 30% | 35% | 25% | 8% | 2% | 0% |
| 商店出售 | 70% | 30% | 0% | 0% | 0% | 0% |

#### 中冒险 (Medium Adventure, ~3 hr, CR 5–10)

| 来源 | Common | Uncommon | Rare | Very Rare | Legendary | Artifact |
|------|--------|----------|------|-----------|-----------|----------|
| 普通敌人掉落 | 50% | 35% | 12% | 2.5% | 0.5% | 0% |
| 精英敌人掉落 | 25% | 30% | 28% | 12% | 4% | 1% |
| Boss 掉落 | 0% | 22% | 33% | 22% | 17% | 6% |
| 宝箱/隐藏区域 | 35% | 30% | 22% | 10% | 2.5% | 0.5% |
| 任务奖励 | 20% | 30% | 28% | 15% | 6% | 1% |
| 商店出售 | 60% | 40% | 0% | 0% | 0% | 0% |
| 制作 (铁匠铺) | 85% | 12% | 3% | 0% | 0% | 0% |

#### 长冒险 (Long Adventure, ~6 hr, CR 11–16+)

| 来源 | Common | Uncommon | Rare | Very Rare | Legendary | Artifact |
|------|--------|----------|------|-----------|-----------|----------|
| 普通敌人掉落 | 35% | 35% | 20% | 7% | 2.5% | 0.5% |
| 精英敌人掉落 | 15% | 25% | 30% | 18% | 9% | 3% |
| Boss 掉落 | 0% | 16% | 26% | 26% | 21% | 11% |
| 宝箱/隐藏区域 | 20% | 28% | 28% | 16% | 6% | 2% |
| 任务奖励 | 10% | 20% | 30% | 22% | 14% | 4% |
| 商店出售 | 50% | 50% | 0% | 0% | 0% | 0% |
| 制作 (图书馆) | 60% | 25% | 10% | 4% | 1% | 0% |

### 3.3 稀有度掷骰算法

```
Algorithm: RollRarity(source_type, adventure_tier)

Input:
  source_type: "common_enemy" | "elite_enemy" | "boss" | "chest" | "quest" | "shop" | "craft"
  adventure_tier: "short" | "medium" | "long"

Output: rarity_level (enum)

Procedure:
  1. 从稀有度概率表中获取对应 (source_type, adventure_tier) 的概率分布
     设 P = {common: p_c, uncommon: p_u, rare: p_r, very_rare: p_vr, legendary: p_l, artifact: p_a}
     其中 sum(P) = 1.0

  2. 掷 d100 (即随机浮点数 [0.0, 1.0)):
     roll = randf()

  3. 累积概率判断:
     threshold = 0.0
     for rarity in [common, uncommon, rare, very_rare, legendary, artifact]:
         threshold += P[rarity]
         if roll < threshold:
             return rarity

  4. 保底: return common
```

**示例 (短冒险 Boss 掉落)**:

```
P = {common: 0.00, uncommon: 0.35, rare: 0.30, very_rare: 0.20, legendary: 0.11, artifact: 0.04}

roll = 0.47:
  common:    0.47 < 0.00? No
  uncommon:  0.47 < 0.35? No
  rare:      0.47 < 0.65? Yes → 返回 rare

roll = 0.72:
  common:    0.72 < 0.00? No
  uncommon:  0.72 < 0.35? No
  rare:      0.72 < 0.65? No
  very_rare: 0.72 < 0.85? Yes → 返回 very_rare
```

> **Boss 稀有度覆盖规则**: 若 `source_type == "boss"` 且掷骰返回 `common`，自动重掷（消耗品类除外）。见 §9.4。

### 3.4 稀有度与魔法属性关系

| 稀有度 | 必定魔法 | 附魔槽数 | 同调需求 | 充能系统 |
|--------|----------|----------|----------|----------|
| Common | 否 | 0 | 否 | 否 |
| Uncommon | 是 | 1 | 10% | 可选 |
| Rare | 是 | 1d2 (掷一次骰: 1-3→1槽, 4-6→2槽) | 40% | 是 |
| Very Rare | 是 | 1d2+1 (结果: 2 或 3 槽) | 70% | 是 |
| Legendary | 是 | 3 (固定) | 100% | 是 |
| Artifact | 是 | 特殊 (2-5, 由蓝图指定) | 100% | 是 |

> 注意: `uncommon` 及以上稀有度的物品自动设定 `magical.is_magical = true`。

---

## 4. 装备槽位系统

### 4.1 槽位定义

> **设计说明 (v1.1)**: 槽位布局以角色系统 (`01-character-system.md` §3.1) 为准。背包槽数 = 10 + STR调整值×2（非固定12格）。

```
角色装备栏 (Character Equipment Slots):

┌─────────────────────────────────────────┐
│  [Helmet]                               │  ← 头盔
├──────────────┬──────────────────────────┤
│ [Armor]                                │  ← 护甲
├──────────────┼──────────────┬───────────┤
│ [Main Hand]  │ [Off Hand]   │ [Cloak]   │  ← 主手/副手/披风
├──────────────┴──────────────┼───────────┤
│ [Boots]                    │           │  ← 靴子
├──────────────┬──────────────┴───────────┤
│ [Ring 1]     │ [Ring 2]                │  ← 戒指 ×2
├──────────────┼─────────────────────────┤
│ [Amulet]     │                          │  ← 护符
├──────────────┴─────────────────────────┤
│  📦 Backpack (10 + STR_mod×2 slots)    │  ← 背包 (所有非装备物品)
└─────────────────────────────────────────┘

MVP 阶段槽位: Main Hand, Off Hand, Armor, Helmet, Cloak, Boots, Ring 1, Ring 2, Amulet, Backpack
背包槽数: 基础10格 + 每+1 STR调整值额外2格 (STR 16 → 16格)
```

### 4.2 槽位枚举

```csharp
public enum EquipmentSlot
{
    MainHand,      // 主手
    OffHand,       // 副手
    Armor,         // 护甲
    Helmet,        // 头盔
    Cloak,         // 披风
    Boots,         // 靴子
    Ring1,         // 戒指 1
    Ring2,         // 戒指 2
    Amulet,        // 护符
    Backpack       // 背包 (10 + STR_mod×2 格)
}
```

### 4.3 槽位兼容矩阵

| 物品类型 ↓ / 槽位 → | Main Hand | Off Hand | Armor | Helmet | Cloak | Boots | Ring | Amulet | Backpack |
|---------------------|-----------|----------|-------|--------|-------|-------|------|--------|----------|
| `weapon` | ✅ | ✅ (限定条件) | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |
| `armor` | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |
| `shield` | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |
| `helmet` | ❌ | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ |
| `cloak` | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ | ✅ |
| `boots` | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ | ❌ | ✅ |
| `potion` | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |
| `scroll` | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |
| `wand` | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |
| `ring` | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ | ✅ |
| `amulet` | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ |
| `misc` | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |
| `quest` | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |

### 4.4 双手武器规则

**规则**: 装备 `two_handed` 属性的武器时，武器同时占据 **Main Hand** 和 **Off Hand** 两个槽位。

```
装备流程:
  1. 玩家尝试装备 two_handed 武器至 Main Hand
  2. 检查 Off Hand 是否为空
     - 是 → 允许装备。Off Hand 标记为"被占用 (by two-handed weapon)"
     - 否 → 拒绝。提示 "需要双手持握，请先卸下副手物品"
  3. 玩家尝试装备任何物品至 Off Hand 时
     检查 Off Hand 是否被双手武器占用
     - 是 → 拒绝。提示 "当前武器需要双手持握"
```

**versatile (多用) 武器切换**:

```
单手 ↔ 双手 切换:
  - 默认装备为单手模式，占用 Main Hand
  - 玩家可通过 UI 切换为双手模式:
    - 检查 Off Hand 是否为空
    - 是 → 切换到双手模式，Off Hand 标记为占用，damage_dice 切换到 versatile 版本
    - 否 → 拒绝
  - 切换回单手: 释放 Off Hand，恢复单手 damage_dice
```

### 4.5 双持规则

**规则**: 当 Main Hand 和 Off Hand 各装备一把 `light` 武器时，角色可以使用附赠动作进行副手攻击。

```
双持限制:
  - 主手武器: 必须为 light 属性
  - 副手武器: 必须为 light 属性
  - 副手攻击: 不添加属性调整值到伤害（除非拥有 "双持战斗" 战斗风格）

Dual Wielder 专长 (Phase 2):
  - 主手武器不需要 light 属性
  - 获得 +1 AC
  - 可同时拔出/收起两把武器
```

### 4.6 同调系统 (Attunement)

同调 (attunement) 是 DND 5e 中对魔法物品使用的限制机制。

```
同调规则:
  - 每个角色最多同时同调 3 件物品
  - 仅 magical.attunement_required = true 的物品需要同调
  - 同调过程:
    1. 角色需在短休期间与物品进行同调（专注持续 1 短休时长）
    2. 同调完成后，物品的魔法属性生效
    3. 未同调的魔法物品仅提供基础属性（如 +1 武器的攻击/伤害加成不生效，
       但武器本身的基础伤害骰仍可用）

  - 同调中断条件:
    - 主动解除同调（需要 1 短休时长）
    - 同调另一件物品导致超过 3 件上限 → 最早同调的物品自动断开
    - 角色死亡
    - 物品离开角色 100 尺超过 24 小时
    - 另一角色与物品同调

  - 同调槽位 UI 显示:
    ┌──────────────────────┐
    │ 同调物品 (2/3)        │
    │ ● 霜痕长剑 (attuned) │
    │ ● 位移斗篷 (attuned) │
    │ ○ 空槽               │
    └──────────────────────┘
```


---

## 5. 附魔系统

### 5.1 设计理念：机制 > 数值

> **核心信条**: 附魔的目标不是让数字变大，而是改变玩家对战斗的思考方式。一件好的魔法物品应该让玩家重新审视自己的构筑——"我能用这件物品做到什么以前做不到的事？"而非"我比刚才多了 +2 伤害"。

本游戏的附魔设计遵循以下分布目标：

| 附魔类型 | 占比目标 | 设计原则 |
|----------|:--------:|----------|
| **机制附魔** 🔧 | ≥ 50% | 改变行为模式：行动经济、目标规则、条件触发、资源流转、站位博弈 |
| **数值附魔** | ≤ 30% | 保留经典元素伤害和少量属性加值，但每种元素最多 2 个变体 |
| **混合附魔** ⚡ | ~20% | 元素伤害 + 微小机制骑乘效果（减速、击倒、减防等） |

**反支柱对齐**: 本设计直接回应 §1A 中的反支柱约束——"物品不应该只是 '+X 数值' 的数值棍"。每件附魔必须有独特的行为身份，而非单纯的数值提升。

### 5.2 附魔数据模型 (Enchantment)

```json
{
  "id": "ench_cleaving",
  "affix_type": "prefix",
  "name_affix": "横扫",
  "name_affix_en": "Cleaving",
  "tier": 1,
  "rarity_requirement": "uncommon",
  "stack_limit": 1,
  "compatible_types": ["weapon"],
  "incompatible_enchantments": [],
  "weapon_restriction": "melee_only",
  "effect": {
    "stat_modifiers": {},
    "damage_bonus": null,
    "on_hit_effect": "cleave_adjacent",
    "special_ability": "命中时对攻击目标相邻的另一个敌人造成武器基础伤害（不计属性调整值和附魔加成）",
    "mechanical_category": "targeting"
  },
  "curse": null
}
```

附魔效果新增分类字段 `mechanical_category`，用于生成算法按冒险主题偏重：

| 分类 | 说明 | 示例效果 |
|------|------|----------|
| `targeting` | 改变攻击目标规则 | 溅射/连锁/相位穿透/射程延伸 |
| `action_economy` | 改变行动经济 | 额外攻击/反应触发/附赠动作/招架 |
| `positioning` | 改变站位博弈 | 推拉/传送/区域控制/残影陷阱 |
| `conditional` | 条件触发机制 | 低血量阈值/击杀链/标记爆发/叠加 |
| `resource` | 资源流转 | 法术位恢复/吸血/临时HP |
| `turn_order` | 回合顺序 | 抢先/延迟/跳过回合 |
| `elemental` | 元素伤害（纯数值为主） | 火焰/寒冰/闪电等伤害骰 |
| `stat_boost` | 属性加值（纯数值） | +X攻击/伤害/AC/豁免/抗性 |

### 5.3 附魔槽数量生成算法

```
Algorithm: GenerateEnchantmentSlots(rarity)

Input:
  rarity: rarity level

Output: int (number of enchantment slots)

Procedure:
  switch rarity:
    case "common":     return 0
    case "uncommon":   return 1
    case "rare":
      roll = rand_range(1, 6)  # d6
      return 1 if roll <= 3 else 2
    case "very_rare":
      roll = rand_range(1, 6)  # d6
      return 2 if roll <= 3 else 3
    case "legendary":  return 3
    case "artifact":   return rand_range(2, 5)
```

> MVP 阶段不包含耐久度系统，因此槽位生成不依赖物品条件等级。Phase 2 引入耐久度后，broken 状态物品的附魔槽暂时失效但不改变槽数。

### 5.4 附魔缀词表

> **阅读指南**：🔧 = 纯机制附魔 | ⚡ = 混合附魔（元素+机制骑乘） | 无标记 = 纯数值附魔

#### 5.4.1 武器前缀表 (Weapon Prefixes)

##### Tier 1 — 不凡 (Uncommon)

| ID | 前缀 | 英文 | 位阶 | 稀有度 | 兼容类型 | 效果 | 分类 | 冲突 |
|----|------|------|:----:|--------|----------|------|------|------|
| `ench_cleaving` | 横扫的 | Cleaving | 1 | uncommon | 近战武器 | 🔧 命中时，对目标相邻的另一个敌人造成武器基础伤害（不计属性调整值和附魔加成） | targeting | — |
| `ench_forceful` | 击退的 | Forceful | 1 | uncommon | 近战武器 | 🔧 命中时目标 STR 豁免（DC=8+熟练+力量调整），失败则被推开 10 尺 | positioning | — |
| `ench_snaring` | 绞杀之 | Snaring | 1 | uncommon | 全部武器 | 🔧 命中时目标移动速度 -10 尺，持续至你的下一回合开始 | positioning | — |
| `ench_returning` | 归返的 | Returning | 1 | uncommon | 投掷武器 | 🔧 投掷后立即返回手中，可在同一回合内反复投掷（配合额外攻击） | action_economy | — |
| `ench_flame_1` | 炽焰的 | Flaming | 1 | uncommon | 全部武器 | 命中额外 +1d6 火焰伤害 | elemental | 寒冰类 |
| `ench_frost_1` | 霜痕的 | Frost | 1 | uncommon | 全部武器 | ⚡ 命中额外 +1d6 寒冰伤害，目标速度 -5 尺至其下回合开始 | elemental | 炽焰类 |
| `ench_shock_1` | 震击的 | Shocking | 1 | uncommon | 全部武器 | ⚡ 命中额外 +1d6 闪电伤害。若目标穿着金属护甲，攻击掷骰获得优势 | elemental | — |

> Tier 1 统计: 🔧机制 4 + ⚡混合 2 = 6 (86%) | 纯元素 1 (14%)

##### Tier 2 — 稀有 (Rare)

| ID | 前缀 | 英文 | 位阶 | 稀有度 | 兼容类型 | 效果 | 分类 | 冲突 |
|----|------|------|:----:|--------|----------|------|------|------|
| `ench_keen` | 锋利的 | Keen | 2 | rare | 全部武器 | 🔧 暴击范围扩展为 19-20 | conditional | — |
| `ench_vampiric_1` | 吸血之 | Vampiric | 2 | rare | 全部武器 | 🔧 暴击时回复 1d8 HP（近战）/ 1d4 HP（远程） | resource | — |
| `ench_berserker` | 狂怒的 | Berserker | 2 | rare | 全部武器 | 🔧 每回合首次击杀敌人后，获得一次额外攻击（每战斗限 3 次） | action_economy | — |
| `ench_executioner` | 处决之 | Executioner | 2 | rare | 全部武器 | 🔧 对生命值低于 30% 的目标，攻击掷骰具有优势 | conditional | — |
| `ench_soul_brand` | 魂印的 | Soul Brand | 2 | rare | 全部武器 | 🔧 暴击时在目标身上施加「魂印」标记（持续至战斗结束）。你下回合首次命中该目标额外造成 2d6 黯蚀伤害并移除标记 | conditional | — |
| `ench_displacing` | 移位的 | Displacing | 2 | rare | 全部武器 | 🔧 暴击时，可与目标交换位置（双方体型差 ≤1 级） | positioning | — |
| `ench_radiant_1` | 光耀的 | Radiant | 2 | rare | 全部武器 | ⚡ 命中额外 +1d6 光耀伤害。对亡灵/邪魔额外 +1d8 | elemental | 黯蚀类 |
| `ench_necrotic_1` | 黯蚀的 | Necrotic | 2 | rare | 全部武器 | ⚡ 命中额外 +1d6 黯蚀伤害。对天界生物额外 +1d8 | elemental | 光耀类 |
| `ench_venom_1` | 毒牙的 | Venomous | 2 | rare | 全部武器 | ⚡ 命中额外 +1d8 毒素伤害，DC 15 CON 豁免，失败则中毒 1 轮 | elemental | — |

> Tier 2 统计: 🔧机制 6 + ⚡混合 3 = 9 (100%)

##### Tier 3 — 极稀有 (Very Rare)

| ID | 前缀 | 英文 | 位阶 | 稀有度 | 兼容类型 | 效果 | 分类 | 冲突 |
|----|------|------|:----:|--------|----------|------|------|------|
| `ench_echo_strike` | 共鸣的 | Echo Strike | 3 | very_rare | 近战武器 | 🔧 每回合一次，命中后对触及范围内另一敌人造成该次伤害的 50% | targeting | — |
| `ench_phase_blade` | 相位之 | Phase Blade | 3 | very_rare | 全部武器 | 🔧 无视目标的护甲 AC（仅计算敏捷加值和魔法加值）。对虚体生物伤害翻倍 | targeting | — |
| `ench_soul_drain` | 噬魂之 | Soul Drain | 3 | very_rare | 全部武器 | 🔧 击杀敌人时，恢复一个已消耗的 3 环或以下法术位（每长休限 1 次） | resource | — |
| `ench_reactive` | 反应的 | Reactive | 3 | very_rare | 近战武器 | 🔧 敌人对你造成伤害时，可用反应对其进行一次借机攻击 | action_economy | — |
| `ench_storm` | 风暴之 | Storm | 3 | very_rare | 全部武器 | ⚡ 命中额外 +1d8 闪电伤害。若本回合曾命中该目标，则 CON 豁免（DC 15），失败则被击倒 | elemental | — |
| `ench_corrosive` | 腐蚀的 | Corrosive | 3 | very_rare | 全部武器 | ⚡ 命中额外 +1d6 强酸伤害。目标 AC 永久 -2（不叠加，至战斗结束） | elemental | — |

> Tier 3 统计: 🔧机制 4 + ⚡混合 2 = 6 (100%)

##### Tier 4 — 传说 (Legendary)

| ID | 前缀 | 英文 | 位阶 | 稀有度 | 兼容类型 | 效果 | 分类 | 冲突 |
|----|------|------|:----:|--------|----------|------|------|------|
| `ench_time_rip` | 时裂的 | Time Rip | 4 | legendary | 全部武器 | 🔧 暴击时，目标失去其下一回合的动作（每长休限 2 次）。不死生物和构装体对此免疫 | turn_order | — |
| `ench_echo_blade` | 残影之 | Echo Blade | 4 | legendary | 近战武器 | 🔧 每回合一次，命中后在你位置留下灵体残影。至下回合开始前，若有敌人进入残影 5 尺范围，残影自动对其发动一次武器攻击（使用你的攻击加值） | positioning | — |
| `ench_inferno` | 地狱火的 | Hellfire | 4 | legendary | 全部武器 | ⚡ 命中额外 +3d6 火焰伤害，忽视火焰抗性。免疫火焰的生物改为承受一半伤害 | elemental | 寒冰类 |

> Tier 4 统计: 🔧机制 2 + ⚡混合 1 = 3 (100%)

##### Tier 5 — 神器 (Artifact)

| ID | 前缀 | 英文 | 位阶 | 稀有度 | 兼容类型 | 效果 | 分类 | 冲突 |
|----|------|------|:----:|--------|----------|------|------|------|
| `ench_doom` | 末日之 | Doombringer | 5 | artifact | 全部武器 | 🔧 每长休一次：宣布一次攻击为「末日宣告」。若命中，目标 CON 豁免（DC 20），失败则 HP 降至 0（传奇生物改为 10d10 力场伤害） | conditional | — |
| `ench_legacy` | 传承的 | Legacy | 5 | artifact | 全部武器 | 🔧 熟练加值翻倍（仅攻击掷骰）。每次击杀获得 2d4 临时 HP | conditional | — |

> Tier 5 统计: 🔧机制 2 = 2 (100%)

**武器前缀总计: 26 个** — 🔧机制 18 (69%) + ⚡混合 5 (19%) + 纯数值 3 (12%)

#### 5.4.2 武器后缀表 (Weapon Suffixes)

##### Tier 1 — 不凡 (Uncommon)

| ID | 后缀 | 英文 | 位阶 | 稀有度 | 兼容类型 | 效果 | 分类 |
|----|------|------|:----:|--------|----------|------|------|
| `ench_of_reach` | 延伸之 | of Reach | 1 | uncommon | 近战武器 | 🔧 武器获得 reach 属性。若已有 reach，额外 +5 尺 | targeting |
| `ench_of_recall` | 回手之 | of Recall | 1 | uncommon | 投掷武器 | 🔧 投掷后立即飞回手中。若投掷造成暴击，下一投掷攻击获得优势 | action_economy |
| `ench_of_warding_1` | 守护之 | of Warding | 1 | uncommon | 全部武器 | 持有者 AC +1 | stat_boost |

##### Tier 2 — 稀有 (Rare)

| ID | 后缀 | 英文 | 位阶 | 稀有度 | 兼容类型 | 效果 | 分类 |
|----|------|------|:----:|--------|----------|------|------|
| `ench_of_pursuit` | 追击之 | of Pursuit | 2 | rare | 全部武器 | 🔧 敌人主动离开你的触及范围时，可用反应移动至多 15 尺追击（不消耗移动力） | positioning |
| `ench_of_flurry` | 连击之 | of Flurry | 2 | rare | 全部武器 | 🔧 若攻击命中且未暴击，可消耗反应进行一次额外攻击（每战斗限 1 次） | action_economy |
| `ench_of_life_steal` | 噬命之 | of Life Steal | 2 | rare | 全部武器 | 🔧 每次命中回复 1 HP（近战）；每回合首次命中回复 1d4 HP（远程） | resource |
| `ench_of_haste` | 疾风之 | of Haste | 2 | rare | 全部武器 | 🔧 先攻检定 +3 | turn_order |

##### Tier 3 — 极稀有 (Very Rare)

| ID | 后缀 | 英文 | 位阶 | 稀有度 | 兼容类型 | 效果 | 分类 |
|----|------|------|:----:|--------|----------|------|------|
| `ench_of_echo` | 回响之 | of Echoes | 3 | very_rare | 全部武器 | 🔧 使用攻击动作后，可消耗附赠动作进行一次额外武器攻击（不与双持叠加） | action_economy |
| `ench_of_momentum` | 惯势之 | of Momentum | 3 | very_rare | 全部武器 | 🔧 连续命中同一目标时，每次后续攻击伤害 +1d4，可叠加至 +3d4。切换目标或失手则重置 | conditional |
| `ench_of_rupture` | 崩裂之 | of Rupture | 3 | very_rare | 全部武器 | 🔧 暴击时，目标护甲/盾牌 AC 加值 -1（可叠加至 -3，至战斗结束） | conditional |

##### Tier 4 — 传说 (Legendary)

| ID | 后缀 | 英文 | 位阶 | 稀有度 | 兼容类型 | 效果 | 分类 |
|----|------|------|:----:|--------|----------|------|------|
| `ench_of_windfury` | 风怒之 | of Windfury | 4 | legendary | 全部武器 | 🔧 暴击时立即对该目标进行一次额外攻击（不消耗动作，每回合限 1 次）。该额外攻击也可触发风怒 | action_economy |
| `ench_of_parry` | 格挡之 | of Parry | 4 | legendary | 近战武器 | 🔧 每回合一次，被近战攻击命中时可用反应招架：掷攻击检定 vs 敌方攻击检定，成功则伤害减半 | action_economy |

##### Tier 5 — 神器 (Artifact)

| ID | 后缀 | 英文 | 位阶 | 稀有度 | 兼容类型 | 效果 | 分类 |
|----|------|------|:----:|--------|----------|------|------|
| `ench_of_annihilation` | 湮灭之 | of Annihilation | 5 | artifact | 全部武器 | ⚡ 暴击时额外 +4d6 力场伤害，被暴击者 CON 豁免（DC 18），失败则被击晕 1 轮 | conditional |

**武器后缀总计: 15 个** — 🔧机制 13 (87%) + ⚡混合 1 (7%) + 纯数值 1 (7%)

#### 5.4.3 护甲/盾牌前缀表 (Armor & Shield Prefixes)

##### Tier 1 — 不凡 (Uncommon)

| ID | 前缀 | 英文 | 位阶 | 稀有度 | 兼容类型 | 效果 | 分类 |
|----|------|------|:----:|--------|----------|------|------|
| `ench_resist_fire_1` | 耐火之 | Fire Resistant | 1 | uncommon | armor, shield | 火焰伤害抗性 | stat_boost |
| `ench_resist_cold_1` | 耐寒之 | Frost Resistant | 1 | uncommon | armor, shield | 寒冰伤害抗性 | stat_boost |
| `ench_resist_lightning_1` | 绝缘之 | Lightning Resistant | 1 | uncommon | armor, shield | 闪电伤害抗性 | stat_boost |
| `ench_thorns` | 荆棘之 | Thorns | 1 | uncommon | armor | 🔧 被近战攻击命中时，攻击者受到 1d4 穿刺伤害 | conditional |
| `ench_grounding` | 稳固之 | Grounding | 1 | uncommon | armor | 🔧 免疫强制移动（推/拉/传送类效果）。对抗击倒的豁免具有优势 | positioning |

> Tier 1 统计: 🔧机制 2 + 纯数值 3 = 5（抗性是 DND 核心机制，保留合理数量）

##### Tier 2 — 稀有 (Rare)

| ID | 前缀 | 英文 | 位阶 | 稀有度 | 兼容类型 | 效果 | 分类 |
|----|------|------|:----:|--------|----------|------|------|
| `ench_mirror_plate` | 镜面之 | Mirror Plate | 2 | rare | armor, shield | 🔧 对抗射线/光束/魔法飞弹类法术时，攻击方具有劣势。反弹 1 环及以下射线法术（每短休 1 次） | conditional |
| `ench_ether_1` | 以太之 | Ethereal | 2 | rare | armor | 🔧 每短休 1 次：消耗反应，使一次物理攻击完全失手 | action_economy |
| `ench_shadow_1` | 暗影之 | of Shadows | 2 | rare | armor | 🔧 潜行检定优势。昏暗/黑暗环境中远程攻击对你具有劣势 | conditional |
| `ench_spellward` | 咒抗之 | Spellward | 2 | rare | armor, shield | 🔧 对抗法术的豁免检定具有优势 | conditional |
| `ench_kinship` | 同袍之 | Kinship | 2 | rare | armor | 🔧 5 尺内盟友被攻击时，可用反应为其提供 +2 AC（至该次攻击结算完毕） | action_economy |
| `ench_preservation` | 自愈之 | Preservation | 2 | rare | armor | 🔧 每战斗一次：HP 降至 30% 以下时自动触发，回复 2d8+2 HP | conditional |

> Tier 2 统计: 🔧机制 6 (100%)

##### Tier 3 — 极稀有 (Very Rare)

| ID | 前缀 | 英文 | 位阶 | 稀有度 | 兼容类型 | 效果 | 分类 |
|----|------|------|:----:|--------|----------|------|------|
| `ench_fortification` | 不屈之 | Fortified | 3 | very_rare | armor | 🔧 免疫暴击。受到超过 30 点的单次伤害时，超出部分减半 | conditional |
| `ench_necromantic_aura` | 死灵之 | Necromantic Aura | 3 | very_rare | armor | 🔧 击杀敌人时，以你为中心 10 尺内所有敌人 WIS 豁免（DC 15），失败则恐慌 1 轮 | conditional |
| `ench_wind_cloak` | 风行之 | Wind Cloak | 3 | very_rare | armor | 🔧 每战斗一次：被命中时可消耗反应传送到 30 尺内可见的未被占据空间。不触发借机攻击 | positioning |

> Tier 3 统计: 🔧机制 3 (100%)

##### Tier 4 — 传说 (Legendary)

| ID | 前缀 | 英文 | 位阶 | 稀有度 | 兼容类型 | 效果 | 分类 |
|----|------|------|:----:|--------|----------|------|------|
| `ench_invulnerability` | 无敌的 | of Invulnerability | 4 | legendary | armor | 🔧 每日 1 次（动作激活）：1 分钟内非魔法武器伤害免疫 | conditional |
| `ench_phoenix_mantle` | 凤凰之 | Phoenix Mantle | 4 | legendary | armor | 🔧 HP 降至 0 时（非即死）自动消耗：回复至 HP 一半，对 15 尺内所有敌人造成 4d6 火焰伤害。触发后附魔永久消失，物品保留 | conditional |

> Tier 4 统计: 🔧机制 2 (100%)

##### Tier 5 — 神器 (Artifact)

| ID | 前缀 | 英文 | 位阶 | 稀有度 | 兼容类型 | 效果 | 分类 |
|----|------|------|:----:|--------|----------|------|------|
| `ench_world_bearer` | 擎天之 | World Bearer | 5 | artifact | armor | 🔧 获得伤害阈值 10（低于 10 的单次伤害降至 0）。STR 豁免自动成功。每长休一次：可主动吸收一次范围攻击——以你为中心 30 尺内所有友方免受该次伤害，你承受全部伤害 | conditional |

> Tier 5 统计: 🔧机制 1 (100%)

**护甲前缀总计: 17 个** — 🔧机制 14 (82%) + 纯数值 3 (18%)

#### 5.4.4 整体统计

| 类别 | 🔧 机制 | ⚡ 混合 | 纯数值 | 总计 | 机制+混合占比 |
|------|:------:|:------:|:------:|:----:|:------------:|
| 武器前缀 | 18 | 5 | 3 | 26 | 88% |
| 武器后缀 | 13 | 1 | 1 | 15 | 93% |
| 护甲前缀 | 14 | 0 | 3 | 17 | 82% |
| **总计** | **45** | **6** | **7** | **58** | **88%** |

> ✅ 机制+混合附魔占比 88%，远超 ≥50% 的设计目标。纯数值附魔（纯元素伤害骰和属性加值）仅占 12%。

### 5.5 附魔堆叠规则

```
堆叠规则:
  1. 同一 ID 的附魔不可叠加 (stack_limit = 1)
     - 例外: tier 递增的同类附魔（如 "炽焰" (T1) vs "地狱火" (T4)），高 tier 覆盖低 tier

  2. 冲突附魔不可共存
     - 火焰 vs 寒冰 (对立元素不可共存)
     - 光耀 vs 黯蚀 (对立能量不可共存)
     - 同分类机制限制: 同一物品最多拥有 2 个同 mechanical_category 的附魔
       （防止全堆「action_economy」附魔导致单回合过多动作）

  3. 相同来源的数值加值(bonus)取最高值，不累加
     - 例: 两件物品各提供 "攻击掷骰+1"，只取最大的 +1，不累加为 +2
     - 例外: 不同来源的加值可叠加 (物品加值 + 法术加值 + 职业特性)

  4. 附魔效果的应用顺序:
     implicit (物品固有) → prefix (前缀附魔) → suffix (后缀附魔)
     机制附魔独立结算——不存在「覆盖」概念（机制附魔解锁的是行为选项，非数值域）

  5. 机制附魔冲突判断:
     - 同 mechanical_category 的机制附魔不「冲突」，但限制为最多 2 个
     - 不同 mechanical_category 的附魔自由组合
     - 例: 一件武器可以有「横扫」(targeting) + 「吸血」(resource) + 「追击」(positioning)
```

### 5.6 随机附魔生成算法

```
Algorithm: ApplyRandomEnchantments(item)

Input:
  item: 基础物品 (rarity 已确定，附魔槽数已确定)

Output:
  item with populated enchantments

Procedure:
  1. 若 item.rarity == "common": 跳过附魔，直接返回

  2. 获取附魔槽数量 slots = item.magical.enchantment_slots

  3. 对于每个附魔槽 i ∈ [1, slots]:
     a. 决定附魔类型:
        - 槽1: 优先前缀 (80% prefix, 20% suffix)
        - 槽2: 优先后缀 (30% prefix, 70% suffix)
        - 槽3+: 隐含 (100% implicit)

     b. 物品类型分流:
        根据 item.type 决定使用哪个附魔池:
        - weapon → 武器前缀表 + 武器后缀表
        - armor / shield → 护甲前缀表
        - 其他可附魔类型 → 通用池

     c. 从对应类型的附魔池中筛选候选:
        candidates = enchantments.filter(e =>
          e.rarity_requirement <= item.rarity
          AND e.compatible_types.contains(item.type)
          AND 若 e.weapon_restriction == "melee_only" → item.weapon_range == "melee"
          AND 若 e.weapon_restriction == "thrown_only" → item.properties.contains("thrown")
          AND item.enchantments 中不含 e.incompatible_enchantments
          AND item.enchantments 中不含相同 e.id
          AND item.enchantments 中不含与 e 冲突的附魔
          AND item.enchantments 中同 mechanical_category 的附魔 < 2
        )

     d. 按位阶 (tier) 加权随机选择:
        // tier 权重: 若 item.rarity == rarity_requirement → weight = 5
        //            若 item.rarity > rarity_requirement 一个等级 → weight = 3
        //            若 item.rarity > rarity_requirement 多个等级 → weight = 1
        // 额外权重: mechanical_category 多样性加分
        //            若候选中有与已选附魔不同 mechanical_category 的 → weight × 1.5

        weighted_roll = weighted_random(candidates, weight_fn)

     e. 应用附魔: item.magical.enchantments.append(enchantment)

  4. 生成附魔物品名称:
     name = prefix_affix.name + " " + item.name + " " + suffix_affix.name
     例: "横扫的" + "长剑" + "追击之" → 「横扫的长剑追击之」

  5. 返回 item
```

### 5.7 诅咒机制

诅咒是负面附魔，通常出现在看似强大的物品上。**诅咒在鉴定 (Identify) 之前不可见**。

> **MVP 说明**: MVP 阶段所有物品默认为 pristine 状态、无耐久度消耗。诅咒物品的「不可丢弃」效果在 MVP 中保留，耐久度相关的诅咒效果（如 `curse_durability_decay`）推迟至 Phase 2。

```
诅咒规则:
  1. 诅咒物品在装备时自动同调（强制同调，不占同调槽但不可主动解除）

  2. 诅咒效果可能包括:
      - curse_disadvantage: 特定检定额外劣势
      - curse_damage_vulnerability: 对特定伤害类型易伤
      - curse_bloodlust: 战斗中必须攻击最近目标（含队友），WIS DC 15 豁免跳过
      - curse_greed: 不可主动丢弃或出售该物品
      - curse_mute: 无法施放言语成分法术
      - curse_nightmare: 长休只恢复一半 HP
      - curse_durability_decay: 每次战斗额外退化 5% 耐久度 (Phase 2 启用)

  3. 诅咒只能通过以下方式移除:
      - 酒馆神殿的「移除诅咒」服务 (声望 Lv.7 解锁)
      - 冒险中找到的「移除诅咒」卷轴
      - 牧师/圣武士施放 Remove Curse 法术 (Phase 3)

  4. 附魔与诅咒共存规则:
      - 诅咒不占附魔槽——诅咒物品仍可拥有正常附魔
      - 同一件物品可以同时有 2 个正面附魔 + 1 个隐藏诅咒
      - 诅咒在鉴定时与附魔一同揭示

诅咒物品生成概率 (物品稀有度 ≥ rare 时):
  Rare: 5% 概率附带隐藏诅咒
  Very Rare: 8%
  Legendary: 12%
  Artifact: 15%
```

---

## 5A. 鉴定系统 (Identification System)

### 5A.1 概述

鉴定系统决定玩家是否能看到魔法物品的完整属性。未鉴定的魔法物品保持神秘感，迫使玩家在"冒险使用未知物品"与"花费资源鉴定"之间做出选择。

### 5A.2 需要鉴定的物品

| 物品类型 | 是否需要鉴定 | 说明 |
|----------|:----------:|------|
| Common 物品 | 否 | 自动鉴定 — 无魔法属性需要揭示 |
| Uncommon 及以上稀有度 | 是 | 所有魔法物品需要鉴定后才能使用附魔效果 |
| 诅咒物品 | 是 | 诅咒在鉴定前完全隐藏 |
| 任务物品 | 否 | 任务关键物品自动识别 |

> **设计说明**：稀有度颜色仍然显示在物品边框上（玩家知道这是魔法物品，但不知道具体属性和附魔内容）。

### 5A.3 鉴定机制

#### 短休鉴定 (Short Rest Identification)

在短休期间，角色可以尝试鉴定背包中的魔法物品。

```
短休鉴定规则:
  - 每次短休可尝试鉴定 1 件魔法物品
  - 使用 Intelligence (Arcana) 技能检定
  - 若角色没有 Arcana 熟练项，仍可尝试（使用纯 INT 调整值）
  - 检定 DC 基于物品稀有度:

  | 稀有度 | 鉴定 DC |
  |--------|:-------:|
  | Uncommon | 12 |
  | Rare | 15 |
  | Very Rare | 18 |
  | Legendary | 22 |
  | Artifact | 25 |

  - 成功: 物品完全鉴定 — 所有附魔、属性加成和诅咒揭示
  - 失败: 物品保持未鉴定状态，可在下次短休重试
  - 同一次短休期间不能对同一件物品重试
```

#### 酒馆鉴定服务 (Tavern Identification)

酒馆提供付费鉴定服务，无需技能检定，100% 成功。

```
酒馆鉴定费用:

| 稀有度 | 鉴定费用 (gp) |
|--------|:------------:|
| Uncommon | 25 gp |
| Rare | 50 gp |
| Very Rare | 100 gp |
| Legendary | 250 gp |
| Artifact | 500 gp |

费用说明:
  - 鉴定费用与物品价值成正比（约为稀有度乘数 × base_value 的 1/3）
  - 批量鉴定 (3件及以上): 享受 20% 折扣
  - 酒馆声望 Lv.6+ 解锁 "鉴定折扣": 所有鉴定费用减半
```

#### 鉴定卷轴 (Identify Scroll)

鉴定卷轴是消耗品，使用后自动鉴定背包中的 1 件物品（无需检定）。

```
鉴定卷轴属性:
  - 类型: scroll (消耗品)
  - 稀有度: Uncommon
  - 价格: 图书馆售价 40 gp
  - 获取途径:
    - 图书馆商店购买 (声望 Lv.5 解锁)
    - 冒险宝箱掉落 (稀有度 uncommon 池)
    - 任务奖励 (剧情相关)
  - 使用: 在背包中选择鉴定卷轴 → 选择目标未鉴定物品 → 即时鉴定
  - 不会在鉴定过程中消耗额外资源
```

### 5A.4 未鉴定物品的行为

未鉴定物品有一套特殊的交互规则，平衡神秘感与风险：

```
未鉴定物品行为规则:

  1. 装备规则:
     - 可装备未鉴定物品至任何合法槽位
     - 但附魔效果（damage_bonus、stat_modifiers、on_hit_effect、
       special_ability）全部不激活
     - 基础属性仍生效: 武器基础伤害骰、护甲基础 AC 等

  2. 诅咒规则 (关键风险!):
     - 诅咒在装备时即激活，即使物品未鉴定
     - 玩家不知道物品被诅咒——只知道"这件物品感觉不对劲"
     - 诅咒效果详情见 §5.6

  3. 显示规则:
     - 物品名称显示: "未鉴定的 [物品类型]" (如 "未鉴定的长剑")
     - 稀有度颜色边框仍显示（玩家知道是魔法物品的程度）
     - 附魔栏位显示为 "???" 
     - Tooltip 显示: "此物品蕴含未知的魔法力量，需要鉴定后才能了解其真正能力"

  4. 堆叠/交易规则:
     - 未鉴定物品可正常放入背包、出售、丢弃
     - 出售给商店时，商店按"未鉴定"价格购买 (比鉴定后价格低 40%)
     - 堆叠消耗品若未鉴定，每个独立单位都需要鉴定

  5. 鉴定后:
     - 物品名称更新为真实名称（含附魔前缀/后缀）
     - 所有属性、附魔效果完全揭示
     - 诅咒（如有）揭示
     - 已装备的物品鉴定后立即激活附魔效果
```

### 5A.5 多人协助鉴定

```
协助鉴定规则（可选）:

  - 若队伍中有其他角色拥有 Arcana 熟练项，可协助鉴定者
  - 协助者在短休中放弃自己的鉴定机会
  - 鉴定者获得优势（掷两次 d20，取高者）
  - 一次鉴定最多 1 人协助

  示例:
    战士（无 Arcana，INT 10）尝试鉴定 Rare 长剑 (DC 15):
      d20 + 0 vs DC 15 → 成功率 30%
    法师（Arcana 熟练，INT 16）协助战士:
      d20 + 0 with advantage vs DC 15 → 成功率 51%
    如果法师自己鉴定:
      d20 + 5 (INT+3, prof+2) vs DC 15 → 成功率 55%
```

### 5A.6 鉴定系统流程图

```
Item acquired (rarity ≥ uncommon)
  │
  ├─→ 稀有度颜色边框显示
  │   名称: "未鉴定的 [类型]"
  │
  ├─→ 玩家选择鉴定方式:
  │   ├─→ 短休鉴定 (免费, Arcana DC)
  │   │   成功 → 物品完全鉴定
  │   │   失败 → 下次短休重试
  │   │
  │   ├─→ 酒馆鉴定 (付费, 100%成功)
  │   │   支付 gp → 即时鉴定
  │   │
  │   └─→ 鉴定卷轴 (消耗品, 100%成功)
  │       使用卷轴 → 即时鉴定
  │
  └─→ 鉴定结果:
       ├─→ 魔法属性揭示
       ├─→ 附魔效果激活
       └─→ 诅咒（如有）揭示
```

---

## 6. 耐久度系统 (Phase 2)

耐久度系统推迟至 Phase 2 实现。MVP 阶段所有物品默认为 pristine 状态，不考虑装备磨损。装备维修费用在 MVP 阶段不适用。详细设计见本 GDD v1.2+。

> **原设计概要（Phase 2 实现参考）**：
> - **条件等级**: pristine (100%) → good (75-99%) → worn (50-74%, -1 AC/-1 dmg) → damaged (25-49%, -2 AC/-1 atk/-1 dmg) → broken (0%, 不可使用)
> - **退化触发**: 武器自然1→-5%, 护甲被暴击→-3%, 每场战斗→-1%, 环境伤害→2-10%
> - **修复**: 铁匠铺修复，成本 = LookupRepairBasePrice(rarity, tier) × (100-durability)/100（查表见 §12.2）
> - **永久破坏**: broken 物品再次受退化伤害时有概率永久破坏

---

## 7. 灵魂绑定物品

### 7.1 绑定规则

```
灵魂绑定 (Soulbound) 规则:

  定义: 灵魂绑定物品永久属于该角色。
    - 冒险失败 (全灭) 时，灵魂绑定物品不丢失
    - 角色死亡时，灵魂绑定物品可传承至新角色 (见 GDD §6.2 知识传承)
    - 不可出售、不可交易、不可丢弃
    - 不可被其他角色装备 (除非传承)

  绑定条件:
    满足以下任一条的物品可成为灵魂绑定:
    1. 物品稀有度 ≥ Legendary
    2. 任务奖励品 (通过 quest_reward source 获得的物品)
    3. 玩家手动选择绑定 (消耗 "灵魂绑定符文"，稀有物品)

  绑定流程:
    1. 物品获得时，自动检查条件 1 和 2：
       - 满足 → soulbound = true (自动绑定)
       - 不满足 → soulbound = false

    2. 手动绑定 (条件 3):
       - 玩家在背包中选择未绑定的物品
       - 使用 "灵魂绑定符文" (消耗品)
       - 确认对话框: "你确定要将 [物品名] 灵魂绑定至 [角色名] 吗？此操作不可撤销。"
       - 确认 → soulbound = true

  灵魂绑定 UI 标识:
    ┌──────────────────────┐
    │ 🔗 霜痕长剑 (+2)      │  ← 🔗 表示灵魂绑定
    │   灵魂绑定至: 索林·铁锤 │
    └──────────────────────┘
```

### 7.2 传承规则

```
角色死亡/退休 → 传承流程:

  可传承的物品 (至多 1 件):
    1. 从死者所有 soulbound 物品中选择 1 件 (最高价值、最高稀有度优先推荐)
    2. 传承物品保留所有属性: 附魔、耐久度、同调状态
    3. 传承物品的 soulbound 状态转移至接受者

  传承限制:
    - 每次死亡/退休只能传承 1 件装备
    - 传承的物品在 GDD 中称为"绑定装备" (见 §6.2)
    - 若死者无可传承物品，则该传承槽位失效

  叙事处理:
    - LLM 在生成英雄传记时，会在传记中提及传承物品
    - 例: "索林·铁锤的霜痕长剑如今悬挂在壁炉旁，等待下一位勇士举起它。"
```

---

## 8. 制作系统

### 8.1 制作设施

| 设施 | 酒馆解锁 | 可制作类型 | 可修复类型 |
|------|----------|-----------|-----------|
| 铁匠铺 (Blacksmith) | 声望 Lv.2 | 武器、护甲、盾牌 | 武器、护甲、盾牌 |
| 炼金台 (Alchemist) | 声望 Lv.3 | 药水、油、毒药 | — |
| 图书馆 (Library) | 声望 Lv.5 | 法术卷轴、魔杖充能、附魔研究 | — |

> **制作系统经济定位**：
> - **Common/Uncommon 物品**：制作成本与商店售价持平（成本平价），玩家可选择购买或自制
> - **Rare+ 物品**：商店不出售，制作是获取特定附魔装备的**主要途径**（配合冒险掉落的随机性）
> - **材料来源**：制作材料主要来自**冒险掉落**，商店仅出售基础材料（铁锭、皮革等）
> - **制作优势**：相比掉落的随机性，制作允许玩家**定向选择**附魔组合

### 8.2 制作配方数据模型

```json
{
  "id": "recipe_longsword",
  "name": "锻造长剑",
  "workshop": "blacksmith",
  "skill_requirement": {
    "skill": "smithing",
    "dc": 10,
    "proficiency_required": true
  },
  "inputs": [
    { "item_id": "material_iron_ingot", "quantity": 3 },
    { "item_id": "material_leather_strap", "quantity": 1 }
  ],
  "gold_cost": 5,
  "time": "1_long_rest",
  "output": {
    "item_id": "item_longsword",
    "quantity": 1,
    "rarity_override": "common",
    "quality_bonus_on_crit": {
      "description": "若技能检定自然20，产出稀有度提升一级至 uncommon",
      "upgrade_to": "uncommon"
    }
  },
  "success_chance": "1d20 + smithing_bonus vs DC 10",
  "failure_result": "材料全部消耗，不产出物品"
}
```

### 8.3 技能检定与成功概率

```
制作检定公式:
  d20 + relevant_skill_modifier + workshop_bonus ≥ recipe.skill_requirement.dc

  relevant_skill_modifier:
    - 铁匠铺: STR 调整值 + 熟练项 (若有史密斯工具熟练)
    - 炼金台: INT 调整值 + 熟练项 (若有炼金工具熟练)
    - 图书馆: INT 调整值 + Arcana 技能加值

  workshop_bonus: 酒馆设施等级提供的加值 (Lv.1→+0, Lv.2→+1, Lv.3→+2)

成功等级:
  - 自然20 (暴击): 物品品质提升一级 (common→uncommon, 等)
  - 检定成功: 正常产出
  - 检定失败: 材料消耗但不产出 (50%) 或材料保留但不产出 (50%)
  - 自然1 (严重失败): 材料消耗 + 设施暂时不可用 (下次长休恢复)
```

### 8.4 MVP 制作配方表

#### 铁匠铺配方 (Blacksmith Recipes)

| 配方 ID | 产出物品 | DC | 材料 | 金币 | 时间 |
|---------|---------|-----|------|------|------|
| `recipe_club` | 棍棒 (1d4) | 8 | 木材 ×1 | 0 | 短休 |
| `recipe_dagger` | 匕首 (1d4) | 10 | 铁锭 ×1, 皮革 ×1 | 1 | 短休 |
| `recipe_shortsword` | 短剑 (1d6) | 12 | 铁锭 ×3, 皮革 ×2 | 5 | 长休 |
| `recipe_longsword` | 长剑 (1d8) | 14 | 铁锭 ×4, 皮革 ×2 | 10 | 长休 |
| `recipe_greatsword` | 巨剑 (2d6) | 16 | 铁锭 ×6, 皮革 ×3 | 30 | 2×长休 |
| `recipe_mace` | 钉头锤 (1d6) | 12 | 铁锭 ×3, 木材 ×1 | 5 | 长休 |
| `recipe_rapier` | 细剑 (1d8) | 15 | 铁锭 ×3, 皮革 ×3 | 20 | 长休 |
| `recipe_handaxe` | 手斧 (1d6) | 12 | 铁锭 ×2, 木材 ×1 | 3 | 短休 |
| `recipe_battleaxe` | 战斧 (1d8) | 14 | 铁锭 ×4, 木材 ×2 | 8 | 长休 |
| `recipe_shortbow` | 短弓 (1d6) | 13 | 木材 ×3, 线绳 ×1 | 15 | 长休 |
| `recipe_longbow` | 长弓 (1d8) | 15 | 木材 ×5, 线绳 ×2 | 30 | 长休 |
| `recipe_arrow_20` | 箭矢 ×20 | 8 | 木材 ×1, 铁锭 ×1 | 1 | 短休 |
| `recipe_shield` | 盾牌 (+2 AC) | 13 | 铁锭 ×3, 木材 ×2 | 8 | 长休 |
| `recipe_leather_armor` | 皮甲 (11+DEX) | 12 | 皮革 ×4 | 8 | 长休 |
| `recipe_studded_leather` | 镶钉皮甲 (12+DEX) | 14 | 皮革 ×4, 铁锭 ×2 | 30 | 长休 |
| `recipe_chain_shirt` | 链甲衫 (13+DEX max2) | 15 | 铁锭 ×5 | 40 | 长休 |
| `recipe_breastplate` | 胸甲 (14+DEX max2) | 17 | 铁锭 ×8, 皮革 ×3 | 300 | 2×长休 |
| `recipe_chain_mail` | 链甲 (16) | 16 | 铁锭 ×8 | 60 | 长休 |
| `recipe_plate` | 全身板甲 (18) | 20 | 铁锭 ×20, 皮革 ×5 | 1200 | 3×长休 |

#### 炼金台配方 (Alchemist Recipes)

| 配方 ID | 产出物品 | DC | 材料 | 金币 | 时间 |
|---------|---------|-----|------|------|------|
| `recipe_healing_potion` | 治疗药水 (2d4+2) | 10 | 草药 ×3, 纯净水 ×1 | 10 | 短休 |
| `recipe_greater_healing` | 高级治疗药水 (4d4+4) | 13 | 草药 ×5, 水晶粉 ×1 | 50 | 长休 |
| `recipe_superior_healing` | 超级治疗药水 (8d4+8) | 16 | 草药 ×8, 水晶粉 ×2, 精华 ×1 | 200 | 长休 |
| `recipe_acid_vial` | 强酸瓶 (2d6 酸) | 12 | 酸液 ×2, 玻璃瓶 ×1 | 15 | 短休 |
| `recipe_alchemist_fire` | 炼金火 (1d4 火/轮) | 12 | 火油 ×2, 玻璃瓶 ×1 | 15 | 短休 |
| `recipe_antitoxin` | 解毒剂 (毒素豁免优势) | 10 | 草药 ×2 | 15 | 短休 |
| `recipe_oil_of_sharpness` | 锋锐油 (+3 武器 1小时) | 15 | 稀有草药 ×3, 油瓶 ×1 | 100 | 长休 |
| `recipe_potion_of_str` | 力量药水 (STR=21 1小时) | 17 | 巨人精华 ×1, 水晶粉 ×3 | 250 | 长休 |
| `recipe_potion_of_invis` | 隐形药水 (1小时) | 16 | 幽魂尘 ×1, 纯净水 ×1 | 150 | 长休 |
| `recipe_potion_of_speed` | 加速药水 (1分钟) | 18 | 闪电精华 ×1, 水晶粉 ×3 | 400 | 长休 |

#### 图书馆配方 (Library Recipes)

| 配方 ID | 产出物品 | DC | 材料 | 金币 | 时间 |
|---------|---------|-----|------|------|------|
| `recipe_scroll_cantrip` | 戏法卷轴 | 12 | 羊皮纸 ×1, 魔法墨水 ×1 | 15 | 短休 |
| `recipe_scroll_lv1` | 1环法术卷轴 | 14 | 羊皮纸 ×1, 魔法墨水 ×2 | 50 | 长休 |
| `recipe_scroll_lv2` | 2环法术卷轴 | 16 | 羊皮纸 ×2, 魔法墨水 ×3 | 150 | 长休 |
| `recipe_scroll_lv3` | 3环法术卷轴 | 18 | 羊皮纸 ×2, 魔法墨水 ×5, 水晶粉 ×1 | 400 | 2×长休 |
| `recipe_wand_magic_missile` | 魔法飞弹魔杖 | 18 | 魔杖核心 ×1, 魔法墨水 ×5, 水晶粉 ×3 | 500 | 3×长休 |
| `recipe_research_enchant` | 附魔研究 (解锁稀有附魔配方) | 20 | 古代典籍 ×1, 魔法墨水 ×10 | 1000 | 5×长休 |

### 8.5 Rare+ 制作配方（定向附魔）

Rare+ 物品**不出售于商店**，制作是获取特定附魔装备的主要途径。制作 Rare+ 物品需要：
1. **基础物品**（Common/Uncommon 品质，可自制或购买）
2. **稀有材料**（仅来自冒险掉落，不可购买）
3. **附魔配方**（通过图书馆"附魔研究"解锁，或冒险中拾取）

#### Rare+ 制作成本公式

```
Rare+ 制作成本公式:

  制作总成本 = 基础物品成本 + 稀有材料成本 + 附魔加工费

  其中:
    基础物品成本 = LookupBasePrice(basic_item.rarity, basic_item.base_price_tier)
                  （查表 §12.2「稀有度层级基础成本表」— "商店售价" 列）
    稀有材料成本 = Σ(材料.掉落数量权重 × 材料.稀有度) — 不可购买，仅冒险掉落
    附魔加工费 = LookupBasePrice(target_rarity, basic_item.base_price_tier) × 0.3
                （查表 §12.2「稀有度层级基础成本表」— "修复基础成本" 列）

  注意: 旧公式中使用 target_rarity_multiplier × base_value，已被稀有度层级基础成本表替代。
  此变更消除了同一稀有度不同物品之间高达 750 倍的价格差异。

  成本平价验证:
    制作总成本 ≈ LookupBasePrice(target_rarity, tier) × 0.8~1.0
    即制作成本约为目标稀有度商店售价的 80%~100%（若商店出售的话）

  示例 — 制作一把 Rare 炽焰长剑 (Medium tier):
    基础物品: 长剑 (Common/Medium) = 50 gp (查表)
    稀有材料: 秘银锭 ×2 (冒险掉落, 不可购买) + 火焰精华 ×1 (冒险掉落)
    附魔加工费: 200 × 0.3 = 60 gp (查表 Rare/Medium 修复基础成本 = 200)
    制作总成本: 50 + 0 (材料不可定价) + 60 = 110 gp
    对比 Rare 长剑 (Medium) 理论售价: 1000 gp (查表)
    制作节省: 89% (但需要冒险获取稀有材料)
```

#### Rare+ 铁匠铺配方

| 配方 ID | 产出物品 | DC | 基础物品 | 稀有材料 (冒险掉落) | 金币 | 时间 |
|---------|---------|-----|---------|-------------------|------|------|
| `recipe_longsword_flame` | 炽焰长剑 (Rare) | 16 | 长剑 ×1 | 秘银锭 ×2, 火焰精华 ×1 | 45 | 2×长休 |
| `recipe_longsword_frost` | 霜痕长剑 (Rare) | 16 | 长剑 ×1 | 秘银锭 ×2, 寒冰精华 ×1 | 45 | 2×长休 |
| `recipe_greatsword_power` | 巨力巨剑 (Rare) | 18 | 巨剑 ×1 | 精金锭 ×3, 泰坦之血 ×1 | 80 | 2×长休 |
| `recipe_plate_reinforced` | 强化板甲 (Rare) | 19 | 全身板甲 ×1 | 秘银锭 ×5, 符文石 ×2 | 200 | 3×长休 |
| `recipe_longsword_holy` | 神圣长剑 (Very Rare) | 20 | 炽焰长剑 (Rare) ×1 | 星辰钢 ×3, 圣水精华 ×2 | 150 | 3×长休 |

#### Rare+ 炼金台配方

| 配方 ID | 产出物品 | DC | 基础物品 | 稀有材料 (冒险掉落) | 金币 | 时间 |
|---------|---------|-----|---------|-------------------|------|------|
| `recipe_superior_healing` | 超级治疗药水 (Rare) | 16 | 高级治疗药水 ×1 | 生命精华 ×2, 精灵露 ×1 | 80 | 长休 |
| `recipe_potion_of_str` | 力量药水 (Rare) | 17 | 纯净水 ×1 | 巨人精华 ×1, 水晶粉 ×3 | 100 | 长休 |
| `recipe_potion_of_speed` | 加速药水 (Very Rare) | 19 | 纯净水 ×1 | 闪电精华 ×2, 时光沙 ×1 | 200 | 2×长休 |

#### 稀有材料来源表

| 材料 ID | 名称 | 掉落来源 | 典型掉率 | 用途 |
|---------|------|----------|----------|------|
| `material_mithril_ingot` | 秘银锭 | 精英敌人/Boss (CR 5+) | 15% | Rare 武器/护甲 |
| `material_adamantine_ingot` | 精金锭 | Boss (CR 8+) | 10% | Rare+ 重型武器 |
| `material_fire_essence` | 火焰精华 | 火焰主题敌人/Boss | 20% | 炽焰附魔 |
| `material_frost_essence` | 寒冰精华 | 寒冰主题敌人/Boss | 20% | 霜痕附魔 |
| `material_titan_blood` | 泰坦之血 | Boss (CR 10+) | 8% | 力量类附魔 |
| `material_rune_stone` | 符文石 | 宝箱/隐藏区域 | 12% | 护甲附魔 |
| `material_star_steel` | 星辰钢 | Boss (CR 13+) | 5% | Very Rare 武器 |
| `material_holy_water_essence` | 圣水精华 | 亡灵主题 Boss | 10% | 神圣附魔 |
| `material_life_essence` | 生命精华 | 精英敌人 (CR 5+) | 12% | 高级治疗药水 |
| `material_time_sand` | 时光沙 | Boss (CR 15+) | 3% | Very Rare 药水 |


---

## 9. 战利品生成系统

### 9.1 战利品生成管线

```
Algorithm: GenerateLoot(encounter_type, adventure_tier, cr_range, boss_flag)

Input:
  encounter_type: "common_enemy" | "elite_enemy" | "boss" | "chest" | "quest_reward"
  adventure_tier: "short" | "medium" | "long"
  cr_range: [min_cr, max_cr]
  boss_flag: boolean (仅 encounter_type=boss 时使用)

Output:
  loot_pile: Array<Item>

Procedure:
  Step 1 — 确定基础掉落数量:
    base_count = DetermineDropCount(encounter_type, adventure_tier)
    // common_enemy: 1d3-1 (0-2 items)
    // elite_enemy: 1d3+1 (2-4 items)
    // boss: 1d6+3 (4-9 items)
    // chest: 1d6 (1-6 items)
    // quest_reward: 1d3+2 (3-5 items)

  Step 2 — 对每件物品独立掷稀有度:
    for i ∈ [1, base_count]:
      rarity = RollRarity(encounter_type, adventure_tier)  // 见 §3.3
      item_type = RollItemType(rarity, encounter_type)
      base_item = RollBaseItem(item_type, rarity, cr_range)
      if rarity >= "uncommon":
        slots = GenerateEnchantmentSlots(rarity)
        base_item.magical.enchantment_slots = slots
        base_item = ApplyRandomEnchantments(base_item)   // 见 §5.5
        base_item.magical.is_magical = true
        if RollAttunementRequirement(rarity):
          base_item.magical.attunement_required = true
      value = CalculateValue(base_item)
      base_item.value_gp = value
      loot_pile.append(base_item)

  Step 3 — 添加金币/宝石:
    gold = RollGold(adventure_tier, cr_range)
    gems = RollGems(adventure_tier, cr_range)
    loot_pile.append(gold、gems as misc items)

  Step 4 — (仅 quest_reward) 应用灵魂绑定:
    if encounter_type == "quest_reward" and rarity >= "very_rare":
      item.soulbound = true

  Step 5 — 返回完整战利品列表
```

#### 辅助函数定义

```
Algorithm: CalculateValue(item)

  根据物品稀有度和类型查表返回基础金币价值。

  查表逻辑:
    base_price = LookupBasePrice(item.rarity, item.base_price_tier)
               // 查表 §12.2「稀有度层级基础成本表」— "商店售价" 列
    enchantment_bonus = Σ(enchantment.tier × 50 gp)  // 每个附魔 tier 增加 50 gp
    return base_price + enchantment_bonus

  注意: 此函数不应用于定价系统（§12.2），仅用于战利品生成时计算物品基础价值。
        shop_price 和 sell_price 使用独立的稀有度层级基础成本表。
```

```
Algorithm: RollAttunementRequirement(rarity)

  概率掷骰（见 §3.4 同调需求概率表）:

    switch rarity:
      case "common":     return false
      case "uncommon":   return randf() < 0.10   // 10% 概率需要同调
      case "rare":       return randf() < 0.40   // 40%
      case "very_rare":  return randf() < 0.70   // 70%
      case "legendary":  return true             // 100%
      case "artifact":   return true             // 100%

  注意: 同调需求不影响物品的基础属性（武器伤害骰、护甲 AC 均可用），
       仅限制魔法属性（附魔效果）的激活。
```

### 9.2 物品类型权重表

#### 按稀有度和遭遇类型

**敌人掉落 (common_enemy / elite_enemy)**:

| 稀有度 ↓ / 类型 → | weapon | armor | shield | potion | scroll | ring | amulet | wand | misc |
|-------------------|--------|-------|--------|--------|--------|------|--------|------|------|
| common | 30% | 15% | 5% | 25% | 10% | 0% | 0% | 0% | 15% |
| uncommon | 25% | 15% | 5% | 20% | 15% | 5% | 5% | 5% | 5% |
| rare | 20% | 15% | 10% | 15% | 15% | 10% | 10% | 5% | 0% |
| very_rare | 20% | 15% | 10% | 10% | 10% | 15% | 15% | 5% | 0% |
| legendary | 20% | 20% | 10% | 5% | 5% | 15% | 15% | 10% | 0% |
| artifact | 25% | 15% | 10% | 0% | 5% | 20% | 15% | 10% | 0% |

**Boss 掉落**:

| 稀有度 ↓ / 类型 → | weapon | armor | shield | potion | scroll | ring | amulet | wand | misc |
|-------------------|--------|-------|--------|--------|--------|------|--------|------|------|
| common | 20% | 10% | 5% | 35% | 15% | 0% | 0% | 0% | 15% |
| uncommon | 20% | 15% | 10% | 15% | 15% | 5% | 10% | 10% | 0% |
| rare | 20% | 15% | 10% | 10% | 10% | 15% | 10% | 10% | 0% |
| very_rare | 25% | 15% | 10% | 5% | 10% | 15% | 15% | 5% | 0% |
| legendary | 30% | 15% | 10% | 5% | 5% | 15% | 15% | 5% | 0% |
| artifact | 30% | 15% | 10% | 0% | 5% | 20% | 10% | 10% | 0% |

### 9.3 基础物品选择表 (按 CR 分段)

```
Algorithm: RollBaseItem(type, rarity, cr_range)

  // 根据 CR 范围限制可出现的物品子类型
  // 低 CR 冒险不会掉落全身板甲和长弓等高级物品

  CR 0–4 (简单位阶):
    weapon: simple melee (棍、匕首、手斧、矛) + simple ranged (短弓、轻弩)
            + 少量 martial (长剑、短剑、细剑、弯刀) — 各 5% 权重
    armor:  棉甲、皮甲、镶钉皮甲、兽皮甲、链甲衫、鳞甲、环甲、链甲
    shield: 标准盾牌

  CR 5–10 (中等位阶):
    weapon: 全部 simple + 全部 martial (除 exotic)
    armor:  全部 light/medium + 板条甲、全身板甲

  CR 11–16 (高等级):
    weapon: 全部 simple + 全部 martial
    armor:  全部 armor

  CR 17+ (传奇):
    weapon: 全部 + 极稀有变体 (镀银、精金、寒铁)
    armor:  全部 + 极稀有变体

  Procedure:
    pool = 从基础物品池中筛选 item.pool_cr_min ≤ avg_cr ≤ item.pool_cr_max
    // 加权随机 (每个物品有 base_weight)
    // higher rarity → 增加高 base_weight 物品的权重
    return weighted_random_select(pool, item_weight × rarity_bonus)
```

### 9.4 Boss 战利品特殊规则

```
Boss Loot 特殊规则:

  1. 保证掉落规则:
     - Boss 必定掉落至少 1 件稀有度 ≥ Rare 的物品
     - Boss 必定掉落至少 1 件与 Boss 主题相关的物品
       (例: 火焰巨龙 → 火焰抗性护甲或炽焰武器)
     - Boss 不掉落 common 稀有度物品 (除非是消耗品)
       **注意: Boss 稀有度概率表中 Common 概率为 0%。若稀有度掷骰由于边缘情况返回
       common，自动重掷（消耗品类除外，消耗品允许 common 掉落）。**

  2. Boss 专属战利品池:
     每个 Boss 有 unique_drop_pool (1-3 件专属物品):
       - 专属物品是 Boss 的唯一掉落源
       - 稀有度: Rare ~ Legendary
       - 其中 50% 为武器/护甲，50% 为戒指/护符

  3. 冒险 Boss 与最终 Boss:
     - 冒险 Boss: 使用标准 Boss 掉落表
     - 战役最终 Boss: 必定掉落 1 件 Legendary 物品

  4. 传奇/神器 Boss:
     - 掉落 Legendary 物品概率翻倍
     - 额外掉落 1 件 Artifact 品质物品 (50%概率)
```

### 9.5 金币与宝石产出

```
Algorithm: RollGold(adventure_tier, cr_range)

  按冒险类型和 CR 计算金币数量:

  Short Adventure (CR 0-4):
    普通敌人: 1d6 × (1d4) gp
    精英敌人: 2d10 × (1d6) gp
    Boss:     4d10 × (1d10) gp
    宝箱:     1d20 × (1d6) gp

  Medium Adventure (CR 5-10):
    普通敌人: 2d10 × (1d6) gp
    精英敌人: 2d20 × (1d8) gp
    Boss:     4d20 × (2d10) gp
    宝箱:     2d20 × (2d6) gp

  Long Adventure (CR 11-16+):
    普通敌人: 4d10 × (2d6) gp
    精英敌人: 4d20 × (3d8) gp
    Boss:     4d100 × (4d10) gp
    宝箱:     4d20 × (5d6) gp

Algorithm: RollGems(adventure_tier, cr_range)

  宝石生成:
    生成概率: common_enemy 15%, elite 30%, boss 50%, chest 25%

    宝石价值表:
    CR 0-4:
      10gp 宝石 (50%), 50gp 宝石 (35%), 100gp 宝石 (15%)
    CR 5-10:
      50gp 宝石 (30%), 100gp 宝石 (40%), 500gp 宝石 (25%), 1000gp 宝石 (5%)
    CR 11-16+:
      100gp 宝石 (20%), 500gp 宝石 (35%), 1000gp 宝石 (30%), 5000gp 钻石 (15%)

  宝石 ID 格式: "gem_[type]_[value]", 如 "gem_ruby_100"
```

### 9.6 任务奖励战利品规则

```
任务奖励生成规则:

  1. 必定掉落:
     - 至少 1 件稀有度 ≥ adventure_tier 对应等级的装备
       (short → uncommon, medium → rare, long → very_rare)
     - 至少 1 件消耗品 (药水或卷轴)

  2. 主题匹配:
     - 若任务剧情有明确的敌人/地点主题，奖励池偏重该主题
     - 例: "亡灵矿坑" → 光耀附魔武器 + 亡灵相关消耗品

  3. 灵魂绑定:
     - 任务奖励 rare 及以下: soulbound=false (可交易)
     - 任务奖励 very_rare 及以上: soulbound=true (不可交易)
     - 任务专属物品 (unique): soulbound=true

  4. 4 人小队分配:
     - 每个队员获得 1 件单独奖励 (不共享)
     - 剩余物品进入共同战利品池 (玩家手动分配)
```

---

## 10. 物品描述生成系统 (Item Description System)

### 10.1 概述

物品描述生成采用**双层架构**：底层为**程序化叙事组合系统**（离线可用，无需 LLM），上层为**Copywriter Agent 增强层**（在线时提供更丰富、更个性化的描述）。

```
┌─────────────────────────────────────────────┐
│              Copywriter Agent (在线增强)      │
│   ────────────────────────────────────────  │
│   当 LLM 可用时，Agent 生成个性化长篇描述       │
│   输入: 物品数据 + 角色上下文 + 冒险上下文      │
└──────────────────┬──────────────────────────┘
                   │ 覆盖 (若可用)
┌──────────────────▼──────────────────────────┐
│         程序化叙事组合引擎 (基线)             │
│   ────────────────────────────────────────  │
│   模板系统 + 来源上下文 + 附魔视觉描述符        │
│   始终可用，无需网络，零延迟                   │
└─────────────────────────────────────────────┘
```

> **架构原则**: 遵循 GDD §4.2 的反支柱要求——游戏必须完全离线可玩。程序化系统是基线，LLM 是附加增强。所有物品描述在无网络环境下仍能生成有叙事深度的文本。

### 10.2 物品描述模板引擎

#### 模板槽位系统

每条物品描述由以下槽位组合而成：

| 槽位 | 标识符 | 数据来源 | 说明 |
|------|--------|----------|------|
| `[material]` | 材质描述 | 物品数据 `material` 字段 | 物品的材质描述，如"钢制"、"寒铁"、"秘银" |
| `[origin]` | 来源描述 | `item.source` + `item.source_name` | 物品来自何处，由来源上下文表注入 |
| `[condition_visual]` | 品相描述 | `item.condition_level` (Phase 2) | 物品的外观状态，如"崭新的"、"有磨损痕迹的" |
| `[enchantment_visual]` | 附魔视觉 | `item.enchantments[].visual_descriptor` | 附魔的外观描述（见 §10.5） |
| `[rarity_flavor]` | 稀有度风味 | 模板池（按稀有度分级） | 稀有度级别的叙事 flavor，有 4-6 个变体 |

#### 模板格式

模板使用占位符标记法：

```
"{template_prefix}[rarity_flavor]{template_suffix}。[enchantment_visual]。{closing}"
```

**示例组合**:

```
输入:
  material = "钢制"
  rarity = "rare"
  source = "boss_loot", source_name = "冰霜妖灵领主"
  enchantment_visual = "剑身上燃烧着永不熄灭的魔法火焰"

处理:
  1. 从 rare 模板池随机选择: "这把[material]武器的前任主人是一位[name]..."
  2. 注入 material: "这把钢制武器的前任主人是一位..."
  3. 注入 source context: "...冰霜妖灵领主..."
  4. 附魔视觉: "剑身上燃烧着永不熄灭的魔法火焰"
  5. 最终生成并附加

输出 (简体中文):
  "这把钢制武器的前任主人是一位冰霜妖灵领主，剑身上至今仍残留着它冰冷的意志。剑身上燃烧着永不熄灭的魔法火焰。"
```

### 10.3 模板池按稀有度分级

每个稀有度等级有 4-6 个模板变体，系统从对应池中随机选择（结合物品类型微调）。

#### Common (普通 — 功能性描述)

```
模板池 (6 个变体):
  1. "一把{品质形容词}[material][item_type]。"
  2. "一件{做工描述}的[material][item_type]，在冒险者中很常见。"
  3. "[material]锻造的{item_type}，朴实但可靠。"
  4. "标准的[material][item_type]，磨得恰到好处。"
  5. "{工艺评价}[item_type]，适合任何冒险者。"
  6. "[material][item_type]，{用途简述}。"

品质形容词池: "普通的" | "标准的" | "耐用的" | "实用的" | "简洁的" | "粗犷的"
做工描述池: "制作扎实" | "打造精良" | "铸造得体" | "锻造到位"
工艺评价池: "一件朴素的" | "一把趁手的" | "一件结实的"
用途简述池: "经过实战检验的设计" | "世代传承的经典造型" | "旅行者最信赖的选择"
```

#### Uncommon (非凡 — 工匠叙事)

```
模板池 (5 个变体):
  1. "一把制作精良的[material][item_type]，{工匠特征}。"
  2. "这件[material][item_type]出自{工匠类型}之手，{细节描述}。"
  3. "[material][item_type]，{视觉细节}，显然是刻意为之。"
  4. "这不是普通的[item_type]——{与众不同之处}。"
  5. "一件{品质感}的[material][item_type]，{来历暗示}。"

工匠特征池: "刃口在光下泛着寒光" | "每一处细节都经过精心打磨" | "平衡感完美" | "握把处雕刻着精致花纹"
工匠类型池: "一位不知名的老铁匠" | "某位武器大师" | "一位隐居的锻造师"
细节描述池: "锋刃处隐约可见层层叠叠的锻打纹路" | "护手处镶嵌着几颗不起眼的宝石" | "表面的打磨痕迹细腻而均匀"
视觉细节池: "表面微微泛着不同寻常的光泽" | "在特定角度下闪烁着微光" | "比看上去要轻盈得多"
与众不同之处池: "它的平衡感让人想起精灵工匠的传说" | "握持时有一种说不出的舒适感" | "似乎天生适合你的手型"
品质感池: "透着不凡品质" | "隐约散发魔力"
来历暗示池: "虽然看上去朴素，但它曾陪伴一位老兵走过无数战场" | "前任主人想必对它爱护有加"
```

#### Rare (稀有 — 传奇暗示)

```
模板池 (5 个变体):
  1. "这把[material]武器的前任主人是一位[source_name]，{遗留特征}。"
  2. "[source_context_prefix][material][item_type]，{传说元素}。"
  3. "这把[item_type]的[部位]上刻着古老的符文，{符文效果暗示}。"
  4. "一件来自[source_name]的[material][item_type]，{历史痕迹}。"
  5. "[rarity_prefix][material][item_type]，{能力暗示}。"

遗留特征池: "上面至今仍残留着它的一部分力量" | "握持时能感受到前任主人的意志" | "它似乎仍在寻找新的主人"
传说元素池: "传说中曾被用来对抗过黑暗中的某些东西" | "在无数诗篇中被提及的名字" | "它的传奇在冒险者酒馆中代代相传"
部位池: "剑身" | "锋刃" | "握柄" | "护手" | "弓臂"
符文效果暗示池: "在月光下会微微发光" | "触碰时会感到一阵温暖" | "周围的空气似乎微微扭曲"
历史痕迹池: "上面有几道意义不明的刻痕" | "经历了数十场战斗却毫发无损" | "岁月的痕迹反而增添了它的威严"
rarity_prefix池: "传说中的" | "令人敬畏的" | "罕见的"
能力暗示池: "似乎能感应到持有者的战斗意志" | "在战斗中会发出低沉的嗡鸣" | "仿佛有自己的想法"
```

#### Very Rare (极稀有 — 敬畏叙事)

```
模板池 (4 个变体):
  1. "即使是最老练的冒险者，见到这把[material][item_type]时也不由自主地屏住呼吸。{史诗描述}。"
  2. "[source_context_prefix][material][item_type]，{神秘起源}。"
  3. "这把[item_type]如同一件活物——{拟人描述}。"
  4. "没有人确切知道这把[material][item_type]的来历。{未解之谜}。"

史诗描述池: "它的光芒几乎令人不敢直视" | "每一次它出鞘都像是某个重大事件的开端" | "空气中弥漫着一种无法言说的压迫感"
神秘起源池: "有人说它在末日战争中曾被诸神使用过" | "它出现在预言中的次数比任何武器都多" | "它的锻造者早已被人遗忘，但它本身永远不会"
拟人描述池: "它在等待合适的持有者，已经等待了数百年" | "它轻轻地颤动着，仿佛在呼吸" | "它似乎能够理解持有者的情绪和意图"
未解之谜池: "一个流行的理论是，它来自世界之外的某个地方" | "唯一确定的是，它比大多数王国都更古老" | "所有试图研究它的人，最终只得到更多的问题"
```

#### Legendary (传奇 — 神话叙事)

```
模板池 (4 个变体):
  1. "古老的神话中，这把[material][item_type]是由{神话工匠}锻造，{神话用途}。"
  2. "这是[material][item_type]中的传奇——{神话描述}。"
  3. "每一位曾在历史上留下名字的英雄，{英雄关联}。每一代人都在等待它再次被举起。"
  4. "凡人只配暂借它的力量，而非拥有它。{天命暗示}。"

神话工匠池: "火神本人在世界诞生之初" | "一位拒绝死亡的神匠" | "第一代矮人王用山之心"
神话用途池: "用来斩断命运的丝线" | "击败最初的混沌之物" | "为凡人世界划定边界"
神话描述池: "它的名字被刻在星空中" | "时间本身在它的锋刃前都要绕道" | "史诗因它而被书写，王国因它而兴衰"
英雄关联池: "都曾触摸过这件物品，留下自己的一部分灵魂" | "都寻找过它，但只有真正配得上的才能找到"
天命暗示池: "它选择持有者，而非持有者选择它" | "命运在它被举起的那一刻就已经改写"
```

#### Artifact (神器 — 宇宙叙事)

```
模板池 (4 个变体):
  1. "这不是一件物品。{宇宙断言}。"
  2. "这把[material][item_type]的名字刻在世界本身的结构之中。{宇宙描述}。"
  3. "凡人无法完全理解[material][item_type]的真正本质。{无法理解}。"
  4. "这是{宇宙定位}的最后一件[material][item_type]——{终末描述}。"

宇宙断言池: "它是一段被凝固的时间，一个被赋予形状的思想" | "它是造物主留下的工具，用以塑造现实本身"
宇宙描述池: "它在一切开始之前就已存在" | "见过它的人无法用语言形容——因为语言本身不足以承载它的意义"
无法理解池: "那些声称理解的人，不是在说谎，就是已经疯了" | "它的力量来源超越了已知的所有魔法学派"
宇宙定位池: "创世之初被铸造的七件" | "多元宇宙中仅存的"
终末描述池: "它的存在本身就是一个谜题，一次挑战，一个承诺——无论哪个都是凡人无法拒绝的" | "当它找到真正的持有者时，某个时代将宣告终结"
```

### 10.4 来源上下文注入

模板根据物品来源 (`item.source`) 注入不同的叙事上下文。来源信息从物品数据和冒险上下文中提取。

| 来源类型 | `item.source` | 上下文注入逻辑 | 示例注入文本 |
|----------|---------------|---------------|-------------|
| Boss 掉落 | `boss_loot` | 注入 `source_name` + boss 相关的遗留特征 | `"仍然带着[source_name]临死前的冰冷气息"` `"从[source_name]的宝库中获得的战利品"` |
| 任务奖励 | `quest_reward` | 注入任务完成的纪念语境 | `"上面刻着'感谢你拯救了[location]——[quest_giver_name]'"` `"作为[quest_name]的纪念而铸造"` |
| 制作出品 | `crafted` | 注入铁匠标记/制作日期 | `"护手处刻着铁匠的个人标记"` `"在[tavern_name]的铁匠铺中锻造于[日期]"` |
| 宝箱发现 | `chest` | 注入发现地点语境 | `"在[location]的深处被发现的"` `"被遗忘在[location]的一个角落里，不知多久"` |
| 商店购买 | `shop` | 简洁的"购入"描述 | `"从[shop_name]购入的全新装备"` `"经[shopkeeper_name]鉴定为真品"` |
| 敌人数落 | `common_enemy` | 战斗残留痕迹 | `"上面还残留着上一场战斗的痕迹"` `"某个不知名敌人的随身武器"` |

**来源描述词库**（按来源类型分类的随机词条，每个类型 3-5 个变体）：

```
boss_loot 描述池:
  - "依然能感受到[source_name]残存的力量波动"
  - "从[source_name]的秘藏中得到的珍贵物品"
  - "[source_name]被打败后，从它的巢穴最深处取出的战利品"
  - "经历了与[source_name]的战斗洗礼后变得愈发锐利"
  - "上面还带着[source_name]巢穴中特有的[theme_material]气息"

quest_reward 描述池:
  - "作为[quest_name]成功完成的奖赏"
  - "铭刻着[location]镇民的感激之情"
  - "[quest_giver_name]亲手交到你手中的报答"
  - "证明了你在[quest_name]中表现的勇气"
  - "一件承载着[location]人民希望的礼物"

crafted 描述池:
  - "在[tavern_name]的铁匠铺中精心锻造而成"
  - "由[blacksmith_name]亲手打造，融入了他的毕生经验"
  - "护手处刻着[blacksmith_name]独特的雪花标记"
  - "锻造时还带着铁砧的余温"
  - "每一个细节都展示了工匠的骄傲和技巧"

chest 描述池:
  - "在[location]的深处被发现的秘藏"
  - "封存在一个古老的宝箱里，不知多少岁月"
  - "被巧妙地藏在[location]的一处暗格中"
  - "似乎有人在很久以前特意将它放在了这里"

shop 描述池:
  - "从[shop_name]购入，成色如新"
  - "经[shopkeeper_name]仔细抛光后的展示品"
  - "[shopkeeper_name]从行商手中收购的可靠装备"

common_enemy 描述池:
  - "从一个[enemy_type]手中夺来的战利品"
  - "上面有几道战斗留下的痕迹"
  - "前任主人的身份已不可考"
```

> **变量替换**: 模板中 `[source_name]`、`[location]` 等占位符在运行时根据物品数据动态替换为实际值。

### 10.5 附魔视觉描述符

每个附魔在数据定义中包含 `visual_descriptor` 字段，描述附魔在物品上的视觉效果。这是程序化系统生成描述的核心数据源。

#### 附魔视觉描述符数据模型

```json
{
  "ench_flame_1": {
    "id": "ench_flame_1",
    "visual_descriptor": "剑身上燃烧着永不熄灭的魔法火焰",
    "visual_descriptor_short": "魔法火焰缠绕",
    "intensity_level": 1,
    "visual_tags": ["火焰", "炽热", "发光"]
  },
  "ench_frost_1": {
    "id": "ench_frost_1",
    "visual_descriptor": "剑刃覆盖着一层永不融化的冰霜",
    "visual_descriptor_short": "冰霜覆盖",
    "intensity_level": 1,
    "visual_tags": ["寒冰", "霜冻", "冷光"]
  },
  "ench_shock_1": {
    "id": "ench_shock_1",
    "visual_descriptor": "剑身周围不时闪过细微的电弧",
    "visual_descriptor_short": "电弧闪烁",
    "intensity_level": 1,
    "visual_tags": ["闪电", "电光", "噼啪"]
  },
  "ench_venom_1": {
    "id": "ench_venom_1",
    "visual_descriptor": "锋刃上渗出一层薄薄的绿色毒液",
    "visual_descriptor_short": "毒液渗出",
    "intensity_level": 1,
    "visual_tags": ["毒素", "绿光", "腐蚀"]
  },
  "ench_radiant_1": {
    "id": "ench_radiant_1",
    "visual_descriptor": "武器散发着温暖的金色光芒",
    "visual_descriptor_short": "金色光芒",
    "intensity_level": 1,
    "visual_tags": ["光耀", "金色", "净化"]
  },
  "ench_necrotic_1": {
    "id": "ench_necrotic_1",
    "visual_descriptor": "武器周围萦绕着一层暗紫色的幽光",
    "visual_descriptor_short": "暗紫幽光",
    "intensity_level": 1,
    "visual_tags": ["黯蚀", "暗影", "死亡"]
  }
}
```

#### 附魔视觉强度叠加

当物品有多个附魔时，视觉描述按 `intensity_level` 排序组合：

```
视觉组合规则:
  1. 按 intensity_level 降序排列所有附魔
  2. 主附魔 (最高 intensity): 使用完整 visual_descriptor
  3. 次附魔 (较低 intensity): 使用 visual_descriptor_short，以"、"连接
  4. 最多显示 3 个附魔的视觉描述（超出部分省略为"以及更多难以名状的力量"）

  示例:
    附魔: [ench_flame_1 (intensity=1), ench_of_accuracy_1 (无视觉)]
    输出: "剑身上燃烧着永不熄灭的魔法火焰。"
    
    附魔: [ench_holy (intensity=3), ench_flame_1 (intensity=1), ench_of_accuracy_2 (无视觉)]
    输出: "武器散发着辉煌的圣光，魔法火焰缠绕其间。"
```

#### 无视觉附魔处理

并非所有附魔都有可见的视觉效果。下列类型的附魔通常没有视觉描述：

| 附魔类型 | 视觉策略 |
|----------|----------|
| 数值加值类 (`+1`/`+2`/`+3` 攻击/伤害) | 无独立视觉 — 由 `rarity_flavor` 暗示品质 |
| 技能类 (潜行优势、先攻加值) | 无独立视觉 — 属于"内在品质" |
| 同调相关 | 无视觉 — 在物品鉴定后才生效 |
| 诅咒 | 视觉完全隐藏 — 直到鉴定或诅咒触发 |

### 10.6 组合算法

#### 主算法

```
Algorithm: GenerateItemDescription(item, context)

Input:
  item: 完整的物品对象（含附魔、来源、材质数据）
  context: { source_type, source_name, adventure_theme, ... }
  use_llm: boolean (是否允许 LLM 增强)

Output:
  description: string (简体中文物品描述)

Procedure:

  // ── 第一层: 程序化基线（始终执行）──

  Step 1 — 确定模板:
    template = RandomSelect(TemplatePool[item.rarity])
    // 若物品类型有特化模板，优先使用类型特化模板
    if TypeSpecificPool[item.type]:
      if RandomCheck(0.6):  // 60% 概率使用类型特化
        template = RandomSelect(TypeSpecificPool[item.type])

  Step 2 — 填充材质槽位 [material]:
    material_desc = item.material ?? GetDefaultMaterial(item.type, item.subtype)
    template = Replace(template, "[material]", material_desc)
    template = Replace(template, "[item_type]", GetDisplayName(item.type))

  Step 3 — 注入来源上下文 [origin]:
    source_context = GetSourceContext(item.source, context)
    template = InjectSourceFlavor(template, source_context)
    // 将 source_context 自然融入模板，替换 [source_name] 等占位符

  Step 4 — 注入品相描述 [condition_visual] (Phase 2):
    if item.condition_level and item.condition_level != "pristine":
      condition_text = GetConditionVisual(item.condition_level)
      // 插入到描述末尾: template += " " + condition_text

  Step 5 — 注入附魔视觉 [enchantment_visual]:
    visual_descs = []
    for enchantment in SortByIntensity(item.enchantments):
      if enchantment.visual_descriptor:
        if len(visual_descs) < 3:
          visual_descs.append(enchantment.visual_descriptor)
        else:
          visual_descs.append("以及更多难以名状的力量")
          break
    
    if visual_descs:
      enchant_text = JoinVisuals(visual_descs)
      // 将附魔视觉追加到模板中
      template = AppendEnchantmentVisual(template, enchant_text)

  Step 6 — 稀有度风味润色:
    // 稀有度越高，描述越长、越有诗意
    flavor_text = GetRarityFlavorText(item.rarity)
    if flavor_text:
      template = PostProcess(template, flavor_text)

  // ── 第二层: LLM 增强（仅当 use_llm=true 且 API 可用时）──

  Step 7 — LLM 增强:
    if use_llm and LLMAvailable():
      llm_description = CallCopywriterAgent(item, context, template)
      if llm_description passes Schema Validation:
        return llm_description  // LLM 生成的描述替换程序化描述

  // ── 返回基线描述 ──
  return template
```

#### 辅助: 类型特化模板池

武器和护甲可以有略微不同的模板：

```
护甲类型特化模板 (sample):
  - "这件[material][item_type]在阳光下泛着[rarity_flavor]的光泽。"
  - "[material][item_type]内部的衬垫保养得很好，穿着异常舒适。"

药水/消耗品特化模板 (sample):
  - "一瓶[material_desc]液体，[rarity_flavor]。"
  - "瓶中的液体[visual_desc]，散发着[smell_desc]。"

戒指/护符特化模板 (sample):
  - "一枚[material]戒指，[rarity_flavor]。"
  - "[material]护符在胸前轻轻地搏动着，[rarity_flavor]。"
```

#### 完整组合示例

```
示例 1: Rare 炽焰长剑 (Boss 掉落)

  输入:
    item = { name: "长剑", material: "钢制", rarity: "rare",
             enchantments: [{ visual_descriptor: "剑身上燃烧着永不熄灭的魔法火焰" }],
             source: "boss_loot", source_name: "冰霜妖灵领主" }
  
  程序化基线生成:
    Step 1: 选择模板 → "这把[material]武器的前任主人是一位[source_name]，{遗留特征}。"
    Step 2: 填充 → "这把钢制武器的前任主人是一位[source_name]，{遗留特征}。"
    Step 3: 来源注入 → "这把钢制武器的前任主人是一位冰霜妖灵领主，"
              + 选择遗留特征: "上面至今仍残留着它的一部分力量。"
    Step 5: 附魔视觉 → " 剑身上燃烧着永不熄灭的魔法火焰。"
    
  最终描述:
    "这把钢制武器的前任主人是一位冰霜妖灵领主，上面至今仍残留着它的一部分力量。
     剑身上燃烧着永不熄灭的魔法火焰。"

  LLM 增强(在线时):
    "这把长剑在冰霜妖灵领主的巢穴最深处被发现时，剑身上的魔法火焰
     依然在跳动——仿佛它一直在等待有人来将它带走。寒铁剑柄上还残留
     着领主临死前的冰冷抓痕，而火焰则像是渴望复仇一般，燃烧得更加
     炽烈了。"

---

示例 2: Common 皮甲 (商店购买)

  输入:
    item = { name: "皮甲", material: "皮革", rarity: "common",
             source: "shop", source_name: "杂货商·格雷特" }
  
  程序化基线生成:
    Step 1: 选择模板 → "一件{做工描述}的[material][item_type]，在冒险者中很常见。"
    Step 2: 填充 → "一件制作扎实的皮革皮甲，在冒险者中很常见。"
    Step 3: 来源注入(shop) → "从杂货商·格雷特处购入，成色如新。"
    
  最终描述:
    "一件制作扎实的皮革皮甲，在冒险者中很常见。从杂货商·格雷特处购入，成色如新。"

---

示例 3: Legendary 神圣巨剑 (任务奖励)

  输入:
    item = { name: "巨剑", material: "星辰钢", rarity: "legendary",
             enchantments: [
               { visual_descriptor: "武器散发着辉煌的圣光" },
               { visual_descriptor: "剑身周围有神圣符文环绕" }
             ],
             source: "quest_reward", source_name: "圣光试炼",
             location: "晨曦大教堂" }
  
  程序化基线生成:
    Step 1: 选择模板 → "古老的神话中，这把[material][item_type]是由{神话工匠}锻造，{神话用途}。"
    Step 2: 填充 → "古老的神话中，这把星辰钢巨剑是由火神本人在世界诞生之初锻造，用来斩断命运的丝线。"
    Step 3: 来源注入(quest) → "作为圣光试炼成功完成的奖赏，晨曦大教堂的守护者亲手将它交到了你手中。"
    Step 5: 附魔视觉 → "武器散发着辉煌的圣光，神圣符文环绕其间。"
    
  最终描述:
    "古老的神话中，这把星辰钢巨剑是由火神本人在世界诞生之初锻造，
     用来斩断命运的丝线。作为圣光试炼成功完成的奖赏，晨曦大教堂的
     守护者亲手将它交到了你手中。武器散发着辉煌的圣光，神圣符文环
     绕其间。"
```

### 10.7 Copywriter Agent 在线增强

当 LLM API 可用时，Copywriter Agent 可以在程序化基线之上生成更丰富、更个性化的描述。

#### 输入 Schema（与程序化系统共享上下文）

```json
{
  "request_id": "copywriter_req_20240504_001",
  "procedural_baseline": "这把钢制武器的前任主人是一位冰霜妖灵领主...",
  "item_data": {
    "name": "霜痕长剑",
    "type": "weapon",
    "subtype": "longsword",
    "rarity": "rare",
    "material": "寒铁合金",
    "source": "boss_loot",
    "source_name": "冰霜妖灵领主",
    "enchantments": [{ "id": "ench_frost_1", "name": "霜痕" }]
  },
  "character_context": {
    "party_level": 3,
    "party_composition": [
      { "name": "索林", "race": "dwarf", "class": "fighter" }
    ]
  },
  "adventure_context": {
    "theme": "Gothic_Horror",
    "location": "被遗忘的墓穴"
  }
}
```

#### 输出 Schema

```json
{
  "request_id": "copywriter_req_20240504_001",
  "description": "LLM 生成的个性化描述（覆盖程序化基线）",
  "narrative_tags": ["寒冷", "古老", "领主遗物"],
  "short_description": "一句话摘要"
}
```

#### LLM 增强规则

```
Copywriter Agent 增强规则:
  1. LLM 生成的描述必须保留程序化基线中的关键信息:
     - 附魔视觉描述 (不可遗漏)
     - 物品来源上下文 (不可歪曲)
     - 材质信息 (不可改变)
  2. LLM 可以自由添加:
     - 更丰富的叙事细节和修饰
     - 角色相关的个性化引用
     - 符合冒险主题的氛围营造
  3. LLM 不得:
     - 改变物品名称或稀有度暗示
     - 添加程序数据中不存在的功能描述
     - 生成除简体中文外的其他语言文本
  4. 验证: LLM 输出必须经过 JSON Schema 验证 (§10.8)
  5. 失败降级: 若 LLM 调用失败或验证失败，使用程序化基线描述
```

### 10.8 缓存与降级策略

```
描述缓存规则:
  - 程序化基线: 缓存键 = MD5(item.id + item.rarity + sorted(enchantment_ids) + item.source)
  - LLM 增强: 缓存键 = MD5(procedural_baseline + character_id + adventure_id)
  - 缓存存储: SQLite，`item_descriptions` 表
  - 最大缓存: 10,000 条 (程序化 8,000 + LLM 2,000)
  - 淘汰策略: LRU

离线降级流程:
  1. 尝试读取 LLM 缓存
  2. 若无缓存 → 检查 LLM API 可用性
  3. 若 API 不可用 → 回退到程序化基线
  4. 若 API 可用但调用失败 (3次重试) → 回退到程序化基线
  5. 所有情况下，程序化基线始终可用
  
  用户体验:
    - 程序化基线描述在物品生成的瞬间立即可用
    - LLM 增强描述异步加载（如可用），在准备就绪后替换显示
    - 显示过渡: 先显示基线描述，LLM 描述就绪后平滑过渡
```

---

## 11. 装备战力计算管线

### 11.1 整体计算模型

```
装备数据流:

  base_item (基础物品数据)
    │
    ├─→ weapon_stats     (武器属性)
    │   ├─→ damage_dice   (基础伤害骰)
    │   ├─→ damage_type   (伤害类型)
    │   ├─→ properties    (武器特性: finesse/reach/...)
    │   └─→ range         (射程)
    │
    ├─→ armor_stats      (护甲属性)
    │   ├─→ base_ac       (基础AC)
    │   └─→ max_dex_bonus (敏捷上限)
    │
    ├─→ enchantments[]   (附魔列表)
    │   ├─→ damage_bonus (伤害加值)
    │   ├─→ stat_modifiers (属性调整)
    │   └─→ ac_bonus     (AC加值)
    │
    ├─→ condition_modifier (耐久度修正)
    │   └─→ (worn: -1AC/-1 dmg, damaged: -2AC/-1 atk/-1 dmg)
    │
    └─→ attunement_state (同调状态)
        └─→ 若未同调且 attunement_required → 魔法效果失效

  最终统计:
    final_stats = merge(
      base_item,
      enchantments (if attuned or not required),
      condition_modifier,
      racial_bonuses (来自Character System),
      class_features (来自Character System)
    )
```

### 11.2 AC 计算

> **v1.2 修正**: AC 计算不再解析 `ac_formula` 字符串（旧方案会导致敏捷加值双重计算）。新版使用 `base_ac` (整数) + 独立 `dex_contribution` 计算，以 `max_dex_bonus` 控制敏捷上限。

```
AC Calculation Pipeline:

  Step 1: 获取护甲基础 AC
    if equipped armor:
      base_ac = armor.base_ac  // 整数，如 chain_mail = 16
    if no armor (unarmored):
      base_ac = 10

  Step 2: 计算敏捷贡献
    if no armor (unarmored):
      dex_contribution = dex_mod  // 无护甲时全敏捷加值
    else if armor.max_dex_bonus is null (轻甲):
      dex_contribution = dex_mod  // 轻甲无敏捷上限
    else:
      dex_contribution = min(dex_mod, armor.max_dex_bonus)

    // 重甲: max_dex_bonus = 0  → dex_contribution = 0
    // 中甲: max_dex_bonus = 2  → dex_contribution = min(dex_mod, 2)
    // 轻甲: max_dex_bonus = null → dex_contribution = dex_mod

  Step 3: 加盾牌
    if off_hand has shield:
      shield_bonus = shield.ac_bonus  // +2 standard

  Step 4: 加附魔
    for each equipped item:
      for each enchantment (if attuned):
        if enchantment.effect.stat_modifiers.ac:
          // 使用 GDD 堆叠规则: 同名加值取最大值
          enchantment_bonus = max_ac_bonus_from_enchantments

  Step 5: 应用耐久度修正 (Phase 2 — MVP 阶段跳过此步骤)
    // MVP 阶段所有装备视为 pristine，无耐久惩罚
    // Phase 2 实现:
    //   worn: -1 AC, damaged: -2 AC, broken: 护甲不可用 (base_ac=10)

  Step 6: 添加种族/职业特性
    // 例: 蛮族 Unarmored Defense, 法师 Mage Armor
    // 这些由 Character System 提供，此处仅为接口

  Final AC = base_ac + dex_contribution + shield_bonus + enchantment_bonus
             - condition_penalty + racial_class_bonus
```

**AC 计算示例**:

```
示例 1: 3级战士穿着链甲，持盾牌

  链甲 (chain_mail):
    base_ac = 16
    max_dex_bonus = 0 (重甲)
    effective_dex = 0

  盾牌: +2 AC

  附魔:
    链甲有 "守护之" 后缀 (of Warding): +1 AC

  条件: Good (无 penalty)

  角色属性: STR 16, DEX 12

  Final AC = 16 + 0 (dex) + 2 (shield) + 1 (enchant) + 0 (condition) + 0 (racial)
           = 19

示例 2: 2级盗贼穿着镶钉皮甲，双持短剑

  镶钉皮甲 (studded_leather):
    base_ac = 12
    max_dex_bonus = null (轻甲)
    effective_dex = +3 (DEX 16)

  盾牌: 无 (双持)

  附魔: 无

  条件: Worn (-1 AC)

  Final AC = 12 + 3 (dex) + 0 (shield) + 0 (enchant) - 1 (condition) + 0 (racial)
           = 14

示例 3: 10级法师，Mage Armor，Bracers of Defense

  Mage Armor (法术): base_ac = 13 + dex_mod (由 Character System 提供)
  Bracers of Defense (魔法物品, wondrous): +2 AC (若未穿护甲)

  DEX 18 → +4

  Final AC = 13 + 4 (dex) + 2 (bracers) + 0 (其他)
           = 19
```

### 11.3 伤害计算

```
Weapon Damage Calculation Pipeline:

  Step 1: 确定基础伤害骰
    damage_dice = weapon.damage_dice
    if weapon has "versatile" and wielding with two_hands:
      damage_dice = weapon.damage_dice_versatile

  Step 2: 确定属性调整值
    if weapon has "finesse":
      ability_mod = max(str_mod, dex_mod)
    elif weapon.weapon_range == "ranged":
      ability_mod = dex_mod
    else:
      ability_mod = str_mod

  Step 3: 添加魔法武器加值
    for each enchantment:
      if enchantment.effect.stat_modifiers.damage_roll:
        // 同名加值取最大值
        magic_bonus = max(magic_bonus, enchantment.effect.stat_modifiers.damage_roll)

  Step 4: 添加附魔伤害骰
    for each enchantment:
      if enchantment.effect.damage_bonus:
        bonus_dice += [enchantment.effect.damage_bonus.dice]

  Step 5: 应用耐久度修正 (Phase 2 — MVP 阶段跳过此步骤)
    // MVP 阶段 damage_penalty 和 attack_penalty 始终为 0
    // Phase 2 实现:
    //   worn: damage_penalty = 1
    //   damaged: damage_penalty = 1, attack_penalty = 1
    //   broken: weapon unusable

  Step 6: 暴击处理
    本游戏暴击规则 (GDD §5.4): 自然20暴击 → 伤害骰最大化，而非双骰
    crit_damage = max_roll(damage_dice) + max_roll(bonus_dice) + ability_mod
                  + magic_bonus - damage_penalty

  Normal damage:
    damage = roll(damage_dice) + ability_mod + magic_bonus + sum(roll(bonus_dice))
             - damage_penalty

  (注: bonus_dice 在暴击时也最大化)
```

**伤害计算示例**:

```
示例 1: 3级战士，16 STR，稀有炽焰长剑 单手 vs 地精

  长剑 (longsword, 1d8 slashing, versatile):
    单手: damage_dice = 1d8
    ability_mod = +3 (STR 16)

  附魔 "炽焰" (Flaming):
    damage_bonus: +1d6 fire

  条件: Good (无 penalty)

  正常命中:
    damage = roll(1d8) + 3 + 0 + roll(1d6) - 0
    若 roll(1d8)=5, roll(1d6)=3
    damage = 5 slashing + 3 + 3 fire = 11 total (8 slashing + 3 fire)

  暴击 (自然20):
    damage = 8 (max 1d8) + 3 + 6 (max 1d6) = 17 total (11 slashing + 6 fire)

示例 2: 5级盗贼，18 DEX，damaged 细剑

  细剑 (rapier, 1d8 piercing, finesse):
    ability_mod = +4 (DEX 18, finesse → 使用 DEX)

  条件: Damaged (-1 attack, -1 damage)

  正常命中 (假设攻击检定通过):
    damage = roll(1d8) + 4 - 1 = roll(1d8) + 3
    若 roll(1d8)=6: damage = 9 piercing

  若还有 Sneak Attack (3d6):
    damage = 6 (1d8) + 4 - 1 + roll(3d6) = 9 + roll(3d6)
```

### 11.4 攻击检定计算

```
Attack Roll Calculation Pipeline:

  Step 1: 熟练加值判定
    if character has proficiency with weapon.weapon_category:
      prof_bonus = character.proficiency_bonus
    else:
      prof_bonus = 0

  Step 2: 属性调整值
    同伤害计算中的 ability_mod

  Step 3: 魔法武器加值
    for each enchantment:
      if enchantment.effect.stat_modifiers.attack_roll:
        attack_bonus = max(attack_bonus, enchantment.effect.stat_modifiers.attack_roll)

  Step 4: 耐久度修正
    if condition_level == "damaged": attack_penalty = 1
    if condition_level == "broken": weapon unusable

  Final attack_roll_bonus = ability_mod + prof_bonus + attack_bonus - attack_penalty

示例:
  5级战士, 18 STR, proficiency +3
  稀有 "精魂之" (+2 attack) 巨剑, 条件 Worn (no attack penalty)

  attack_roll = d20 + 4 (STR) + 3 (prof) + 2 (enchant) - 0 (condition)
              = d20 + 9

  对 AC 15 的敌人:
  命中概率 = 1 - (15 - 9 - 1) / 20 = 75% (需要 d20 ≥ 6)
```

---

## 12. 商店与交易系统

### 12.1 商店概览

| 商店 | 酒馆区域 | 解锁条件 | 出售物品类型 | 刷新周期 |
|------|----------|----------|-------------|----------|
| 杂货商 | 大厅 | 初始 | 基础消耗品、弹药、基础材料 | 每次冒险返回后 |
| 铁匠铺 | 铁匠铺 | 声望 Lv.2 | Common/Uncommon 武器护甲、维修材料 | 每次长休后 |
| 炼金店 | 炼金台 | 声望 Lv.3 | 药水、油、毒药、炼金材料 | 每次冒险返回后 |
| 魔法书店 | 图书馆 | 声望 Lv.5 | 1-2环法术卷轴、魔法墨水 | 每次长休后 |

> **商店定位**：商店仅出售 **Common 和 Uncommon** 物品。Rare+ 物品通过**冒险掉落**或**制作系统**获取。商店的核心功能是**补充消耗品**和**提供基础材料**，而非出售高级装备。

### 12.2 定价模型

> **重要变更 (v1.2)**：物品定价不再使用 `base_value × rarity_multiplier`，改用**稀有度层级基础成本表**。具体物品价格映射到 nearest tier 基准值。旧公式 `base_value × rarity_multiplier` 会导致灾难性价格缩放（同一稀有度的传说匕首修理费仅 100 gp，传说板甲修理费却高达 75,000 gp，差距达 750 倍）。

#### 稀有度层级基础成本表

| 稀有度 | 价格层级 | 基础价格(gp) | 商店售价(gp) | 回收价(gp) | 修复基础成本(gp) |
|--------|:--------:|:----------:|:----------:|:---------:|:--------------:|
| Common | Low (匕首/棍棒) | 5 | 5 | 2 | 1 |
| Common | Medium (长剑/链甲衫) | 50 | 50 | 15 | 5 |
| Common | High (全身板甲/巨剑) | 100 | 100 | 30 | 10 |
| Uncommon | Low | 50 | 50 | 15 | 8 |
| Uncommon | Medium | 250 | 250 | 75 | 38 |
| Uncommon | High | 500 | 500 | 150 | 75 |
| Rare | Low | 200 | — (商店不出售) | 60 | 40 |
| Rare | Medium | 1000 | — | 300 | 200 |
| Rare | High | 2000 | — | 600 | 400 |
| Very Rare | Low | 1000 | — | 300 | 250 |
| Very Rare | Medium | 5000 | — | 1500 | 1250 |
| Very Rare | High | 10000 | — | 3000 | 2500 |
| Legendary | Low | 5000 | — | 1500 | 1500 |
| Legendary | Medium | 25000 | — | 7500 | 7500 |
| Legendary | High | 50000 | — | 15000 | 15000 |
| Artifact | — | 25000+ | — | 7500+ | 特殊(任务材料) |

#### 价格层级映射规则

每个基础物品根据其实用性和稀有度指定一个 `base_price_tier`：

| 层级 | 适用物品类型 | 价格定位 |
|:----:|-------------|----------|
| **Low** | 匕首、棍棒、飞镖、投石索、弹药等低价值物品 | 取稀有度层级范围下界 |
| **Medium** | 长剑、战斧、链甲衫、鳞甲、短弓、法师护甲等中等价值物品 | 取稀有度层级范围中点 |
| **High** | 全身板甲、巨剑、重弩、长弓、高级护甲等高价值物品 | 取稀有度层级范围上界 |

#### 定价公式

```
卖价 (Shop → Player, 仅 Common/Uncommon):
  price = LookupBasePrice(rarity, base_price_tier)

回收价 (Player → Shop):
  sell_price = LookupBasePrice(rarity, base_price_tier) × 0.3 × condition_adjuster

其中:
  condition_adjuster:
    pristine: 1.0
    good:     0.9
    worn:     0.65
    damaged:  0.35
    broken:   0.05

注意: 商店仅出售 Common/Uncommon 物品。Rare+ 物品的"价格"仅用于
回收估值和制作成本计算，不出现在商店 UI 中。

定价示例:
  一把 Common 长剑 (Medium tier), 耐久 pristine:
    shop_price = 50 gp (查表 Common/Medium)

  一把 Uncommon 长剑 (Medium tier), 耐久 pristine:
    shop_price = 250 gp (查表 Uncommon/Medium)

  玩家卖出同一把剑 (worn, 60%):
    sell_price = 250 × 0.3 × 0.65 = 48.75 → 49 gp

  一把 Rare 长剑 (Medium tier, 商店不出售，仅用于回收估值):
    base_price = 1000 gp (查表 Rare/Medium)
    sell_price = 1000 × 0.3 × 1.0 = 300 gp (pristine)

  一把 Common 匕首 (Low tier), 耐久 pristine:
    shop_price = 5 gp (查表 Common/Low)

  一把 Common 全身板甲 (High tier), 耐久 pristine:
    shop_price = 100 gp (查表 Common/High)
```

### 12.3 商店库存刷新

```
商店刷新算法:

  1. 基础库存大小:
     杂货商: 8 + d6 物品
     铁匠铺: 6 + d4 物品
     炼金店: 6 + d4 物品
     魔法书店: 4 + d4 物品

  2. 库存生成 (仅 Common/Uncommon):
     for i in [1, inventory_size]:
       rarity = RollRarity("shop", adventure_tier)
       // 商店稀有度上限: uncommon
       if rarity > "uncommon":
         rarity = "uncommon"  // 强制降级
       item = generate item as per loot generation
       inventory.append(item)

  3. 必定刷新物品 (不受随机影响):
     杂货商: 箭矢×20 (×3), 治疗药水 (×2), 火把×5, 口粮×5
     铁匠铺: 基础维修工具包 (×1), 铁锭×5, 皮革×3
     炼金店: 治疗药水 (×3), 解毒剂 (×2), 草药×10
     魔法书店: 1环法术卷轴 (随机3种, 各×1), 魔法墨水×5

  4. 特殊商品 (稀有, 每个刷新周期有概率出现):
     铁匠铺:  稀有基础材料 (铁锭/皮革批量包, 25%概率)
     炼金店:  高级治疗药水 (uncommon, 15%概率)
     魔法书店: 2环法术卷轴 (uncommon, 10%概率)

  5. 商店不出售的物品类型:
     - Rare 及以上稀有度的装备
     - 稀有材料 (秘银锭、火焰精华等 — 仅冒险掉落)
     - 传奇/神器物品
```

### 12.4 修复与升级成本

```
修理成本 (见 §6 — Phase 2 实现):
  repair_cost = LookupRepairBasePrice(item.rarity, item.base_price_tier) × (100 - current_durability) / 100
  // 修复基础价格查表 §12.2「稀有度层级基础成本表」— "修复基础成本" 列
  // Artifact 物品修复需任务材料，不适用基础价格

升级成本 (常规→魔法):
  // 铁匠铺: 为 common 武器/护甲 添加 1 个附魔槽 (uncommon 化)
  upgrade_cost = LookupBasePrice("uncommon", item.base_price_tier) × 0.5 + rare_materials × 3
  // 成本约为 uncommon 物品售价的 50%（查表 Uncommon/Medium = 250gp × 0.5 = 125gp 加工费）
  成功概率: DC 18 smithing check
  失败: 物品 durability -25%，材料消耗
  暴击失败 (自然1): 物品 broken

  // 图书馆: 为 uncommon+ 物品添加额外附魔槽 (附魔研究)
  upgrade_cost = LookupBasePrice(item.rarity, item.base_price_tier) × 2 + arcane_crystals × 5
  成功概率: DC 20 arcana check
  失败: 材料消耗，不损伤物品
```

### 12.5 冒险中商人遭遇

```
冒险中商人 (Traveling Merchant):

  生成概率:
    Short Adventure: 30% 概率在事件节点遇到
    Medium Adventure: 50% 概率
    Long Adventure: 60% 概率

  特点:
    - 库存大小: 4 + d4 物品
    - 稀有度上限: Uncommon (与酒馆商店一致)
    - 价格: ×1.5 基础价格 (旅行溢价)
    - 回收价: ×0.2 基础价格 (低于酒馆商店的 30%)
    - 特殊: 可能出售该冒险主题的专属消耗品
      (例: 亡灵主题冒险中出售圣水和光耀卷轴)
    - 无声誉影响 (不享受酒馆设施加成)
```

### 12.6 金币消耗机制（Gold Sinks）

为防止金币无限累积，游戏设计了以下三条主要金币消耗途径：

#### 12.6.1 装备维修 (Phase 2)

装备维修功能推迟至 Phase 2 实现。MVP 阶段所有物品默认为 pristine 状态，不考虑装备磨损。装备维修费用在 MVP 阶段不适用。详细设计见本 GDD v1.2+。

> 原设计概要（Phase 2 实现参考）：维修成本 = LookupRepairBasePrice(rarity, tier) × (100-durability)/100（查表见 §12.2）。每次短冒险后预期维修消耗约 10-30 gp，中冒险约 50-200 gp，长冒险约 200-1000 gp。

#### 12.6.2 酒馆设施升级（阶段性消耗）

酒馆设施升级是一次性大额金币消耗，解锁新功能和制作配方。

```
设施升级成本表:

  铁匠铺:
    Lv.1 → Lv.2: 200 gp + 铁锭 ×10 (解锁: Uncommon 制作)
    Lv.2 → Lv.3: 1000 gp + 秘银锭 ×5 (解锁: Rare 制作, 高级维修)

  炼金台:
    Lv.1 → Lv.2: 300 gp + 草药 ×20 (解锁: 高级药水制作)
    Lv.2 → Lv.3: 1500 gp + 精华 ×3 (解锁: Very Rare 药水制作)

  图书馆:
    Lv.1 → Lv.2: 500 gp + 魔法墨水 ×10 (解锁: 2环卷轴制作)
    Lv.2 → Lv.3: 2500 gp + 古代典籍 ×1 (解锁: 附魔研究, Rare 附魔配方)

  总计: 完全升级所有设施需要约 6000 gp + 大量稀有材料
```

#### 12.6.3 角色训练（持续消耗）

角色训练是每次冒险间的持续金币消耗，用于提升角色能力。

```
训练成本表:

  属性提升训练 (每次冒险间可进行 1 次):
    基础属性 +1: 100 × 当前属性值 gp
    例: STR 14 → 15: 1400 gp
    例: STR 16 → 17: 1600 gp
    限制: 每个属性最多通过训练提升 2 点

  熟练项训练:
    新武器熟练: 50 × 角色等级 gp
    新工具熟练: 30 × 角色等级 gp
    新语言: 20 × 角色等级 gp

  技能专精训练:
    技能专精 (Expertise): 200 × 角色等级 gp
    限制: 每 4 级可获得 1 次专精训练

  训练时间:
    属性提升: 1 次长休
    熟练项: 2 次长休
    技能专精: 3 次长休
```

#### 12.6.4 其他金币消耗

```
其他消耗途径:

  1. 冒险准备:
     - 口粮: 5 gp/份 (每次冒险需要 3-5 份)
     - 火把: 1 gp/支 (每次冒险需要 2-3 支)
     - 绳索: 1 gp/50尺
     - 总计: 约 20-40 gp/次冒险

  2. 鉴定费用:
     - 鉴定魔法物品: 50 gp/件 (或使用鉴定卷轴)
     - 鉴定诅咒物品: 100 gp/件

  3. 灵魂绑定符文 (稀有消耗品):
     - 获取方式: 仅 Boss 掉落或特殊任务奖励
     - 不可购买，但可用于绑定未绑定的 Rare+ 物品

  4. 旅行费用 (Phase 2):
     - 马车租赁: 50 gp/次冒险
     - 船票: 100-500 gp (取决于距离)
```


---

## 13. 测试规格

### 13.1 单元测试

#### Test 1: 稀有度掷骰分布验证

```
测试名称: test_rarity_roll_distribution
目标: 验证 RollRarity 在 10,000 次掷骰后的分布与预期概率表误差 < 2%

方法:
  for tier in ["short", "medium", "long"]:
    for source_type in all source types:
      results = {common:0, uncommon:0, rare:0, very_rare:0, legendary:0, artifact:0}
      for i in range(10000):
        rarity = RollRarity(source_type, tier)
        results[rarity] += 1

      for rarity in results:
        observed = results[rarity] / 10000
        expected = probability_table[tier][source_type][rarity]
        assert abs(observed - expected) < 0.02  # 2% margin

种子: rng_seed = 42 (确保可重复)
```

#### Test 2: 附魔堆叠规则

```
测试名称: test_enchantment_stacking
目标: 验证同名加值取最大值、冲突附魔不共存

场景:
  1. 两件物品各有 "攻击掷骰+1" → 最终加值 = +1 (取最大, 不累加)
  2. 物品已有 "炽焰" (火焰伤害) → 阻止 "霜痕" (寒冰伤害) 附魔 (冲突检测)
  3. 物品有 "光耀" → 移除后添加 "黯蚀" → 可以 (不共存但不是同时存在)
  4. 物品有 tier1 "炽焰" → 尝试加 tier2 "地狱火" → 地狱火覆盖炽焰

预期:
  场景1: total_attack_bonus = 1
  场景2: ApplyRandomEnchantments 不会选到霜痕
  场景3: 无错误
  场景4: 最终物品附魔列表只有地狱火
```

#### Test 3: AC 计算管线

```
测试名称: test_ac_calculation_pipeline
目标: 验证所有场景的 AC 计算正确

场景:
  1. 无护甲, DEX 14 → AC = 12
  2. 皮甲, DEX 18 → AC = 15 (11 + 4)
  3. 链甲衫, DEX 18 → AC = 15 (13 + min(4, 2))
  4. 全身板甲, DEX 18 → AC = 18 (18 + 0)
  5. 全身板甲 + 盾牌 → AC = 20
  6. 全身板甲 + 盾牌 + "守护之" 附魔 → AC = 21
  7. 皮甲, DEX 18, Damaged → AC = 13 (11+4-2)
  8. 全身板甲, Broken → AC = 10 + DEX (护甲不可用)
  9. 链甲 + 盾牌 + "守护之"(+1) + DEX 0 → AC = 19
```

#### Test 4: 伤害计算

```
测试名称: test_damage_calculation
目标: 验证武器伤害计算在不同条件下的正确性

场景 (使用 seeded RNG):
  1. 简易：棍棒(d4), STR 12(+1), Common → 期望: 2-5
  2. 标准：长剑(d8), STR 16(+3), Uncommon "炽焰"(+1d6 fire) → 期望: 5-17
  3. 暴击：长剑(d8), STR 16(+3), 自然20, "炽焰" → 期望: 8+3+6=17
  4. 损毁：长剑(d8), STR 16(+3), Damaged → 期望: roll(d8)+3-1 = 3-10
  5. Finesse: 细剑(d8), DEX 18(+4), 对 DEX 优势目标 → 使用 DEX mod
  6. Versatile: 长剑(d8/d10), STR 16(+3), 双手模式 → roll(d10)+3
```

#### Test 5: 战利品生成 (Seeded RNG)

```
测试名称: test_loot_generation_distribution
目标: 验证战利品生成符合预期稀有度分布

方法:
  rng = RandomNumberGenerator.new()
  rng.seed = 42

  # 运行1000次 Short Adventure 的 Boss 掉落
  for i in range(1000):
    loot = GenerateLoot("boss", "short", [2, 4], true)

   # 验证:
   assert 至少 2% Legendary drop (expected ~11%)
   assert 至少 0.5% Artifact drop (expected ~4%)
   assert Common drop = 0% (Boss 不产出 common 装备，§9.4)
```

#### Test 6: 耐久度退化 (Phase 2 — MVP 跳过)

```
测试名称: test_condition_degradation
目标: 验证耐久度退化和性能修正 (Phase 2 实现)
状态: Phase 2 — MVP 阶段所有物品为 pristine

场景:
  1. 武器自然1 → 退化 5%, pristine→good (95%)
  2. 护甲被暴击 → 退化 3%, 从 100% → 97%
  3. 从 52% 退化 5% → 47% → damaged 状态生效
  4. 武器 broken → equippable = false
  5. broken 物品再次退化 → 检查永久破坏概率
```

#### Test 7: 制作公式

```
测试名称: test_crafting_recipes
目标: 验证制作配方的材料、DC、产出

场景:
  1. 锻造长剑, smithing 熟练, STR 14(+2), 无酒馆bonus:
     d20+2 vs DC 14 → 成功概率 = 45%
  2. 锻造长剑, smithing 熟练, STR 16(+3), 酒馆Lv2(+1):
     d20+4 vs DC 14 → 成功概率 = 55%
  3. 自然20 → 产出 rare 长剑 (upgrade to uncommon → from common)
  4. 材料不足 → 显示错误信息
```

#### Test 8: 商店定价

```
测试名称: test_shop_pricing
目标: 验证定价公式 (新经济模型)

场景:
  1. Common 长剑 (Medium tier), pristine:
     price = LookupBasePrice("common", "medium") = 50 gp (查表 §12.2)
  2. Uncommon 长剑 (Medium tier), pristine:
     price = LookupBasePrice("uncommon", "medium") = 250 gp (查表)
  3. 卖出 Uncommon 长剑 (Medium tier, worn):
     sell_price = 250 × 0.3 × 0.65 = 48.75 → 49 gp
  4. Rare 长剑 (Medium tier, 商店不出售，仅验证回收估值):
     base_price = LookupBasePrice("rare", "medium") = 1000 gp (查表)
     sell_price = 1000 × 0.3 × 1.0 = 300 gp (pristine)
  5. 验证商店不出售 Rare+ 物品:
     shop_inventory = generate_shop_inventory("blacksmith", "medium")
     assert all(item.rarity <= "uncommon" for item in shop_inventory)
```

### 13.2 集成测试

#### Test I1: 装备 → 数值刷新 → 战斗验证

```
测试名称: test_equip_to_combat_flow
目标: 端到端测试装备变更到战斗计算

流程:
  1. 创建战士角色 (STR 16, DEX 12, prof+2, 战士职业)
  2. 装备链甲 → 验证 AC = 16
  3. 装备长剑 → 验证攻击加值 = +5 (STR+3, prof+2)
  4. 装备盾牌 → 验证 AC = 18
  5. 进入战斗场景，攻击 AC 14 的敌人 → 验证命中率 = 60% (d20+5 vs AC14)
  6. 卸下盾牌，换成短剑(副手) → 验证可双持
  7. 双持攻击 → 主手正常伤害，副手无属性加值伤害
```

#### Test I2: 战利品生成 → 背包 → 鉴定 → 装备

```
测试名称: test_loot_to_inventory_flow
目标: 验证战利品从生成到装备的完整流程

流程:
  1. 生成战利品 (Short Boss, CR 3)
  2. 验证所有物品符合 CR 范围限制
  3. 将战利品加入背包 → 验证背包容量
  4. 鉴定魔法物品 → 揭示附魔和诅咒
  5. 装备物品 → 验证槽位规则
  6. 同调物品 → 验证同调上限
  7. 卸下物品 → 验证回到背包
```

#### Test I3: 完整经济循环

```
测试名称: test_full_loot_economy
目标: 运行 100 次完整短冒险，验证经济平衡 (新经济模型)
状态: MVP 核心逻辑 + Phase 2 维修消耗部分

流程:
  for i in range(100):
    adventure = generate_short_adventure()
    for encounter in adventure.encounters:
      loot = generate_loot(encounter)
      add_to_party_inventory(loot)
    # 模拟维修消耗
    repair_cost = calculate_repair_cost(party_equipment)
    deduct_gold(repair_cost)

  # 验证:
  assert total_gold in range(200, 3000)  # 合理范围 (含维修消耗)
  assert magic_items_count in range(3, 15)
  assert average_rarity > "common" and average_rarity < "legendary"
  # 验证商店不出售 Rare+ 物品
  shop_items = generate_shop_inventory("blacksmith", "short")
  assert all(item.rarity <= "uncommon" for item in shop_items)
  # 验证维修消耗占收入比例合理
  avg_repair_ratio = total_repair_cost / total_gold_income
  assert avg_repair_ratio in range(0.15, 0.40)  # 15-40% 收入用于维修
```

### 13.3 边界情况测试

```
边界情况:

  1. 双手武器 + 副手占用:
     - 装备 two_handed 武器 → Off Hand 被标记
     - 尝试装备盾牌 → 拒绝
     - 卸下双手武器 → Off Hand 释放

  2. 双持规则:
     - 副手装备 non-light 武器 → 拒绝 (MVP)
     - 装备 Dual Wielder 专长后 → 允许 (Phase 2)

  3. 同调上限:
     - 已有 3 件同调物品 → 尝试同调第 4 件 → 最早的一件自动断开
     - 断开同调的魔法效果消失

   4. Broken 物品 (Phase 2 — MVP 阶段所有物品为 pristine):
      - 武器 broken → 不可攻击
      - 护甲 broken → AC 归零
      - broken 物品的附魔 → 全部失效
      - 尝试修复 broken → 检查成本和材料
      - MVP 阶段: 物品永不损坏，equippable 始终为 true

  5. 灵魂绑定:
     - 绑定物品不可出售 (出售确认框禁用)
     - 全灭后绑定物品保留
     - 角色死亡传承绑定物品

  6. 背包满:
      - 背包满(10+STR_mod×2) → 新战利品 → 弹出 "背包已满，是否丢弃物品？"
     - 丢弃物品确认对话框

  7. 诅咒物品:
     - 装备诅咒物品 → 自动同调 (不占槽位)
     - 尝试卸下 → "你无法放下这把剑...它似乎不愿离开你的手"
     - 神殿 Remove Curse → 诅咒消失，物品可正常卸下

  8. 附魔冲突:
      - 尝试为火焰附魔武器添加寒冰附魔 → 弹窗 "此物品已有炎属性附魔，寒冰附魔无法共存"

  9. 鉴定与诅咒互动 (Phase 2):
      - 装备未鉴定的诅咒物品 → 诅咒立即生效但附魔不激活
      - 角色战斗中突然出现检定劣势 → 玩家不知道原因(应为诅咒)
      - 鉴定揭示诅咒 → 物品描述更新，显示诅咒效果
```

### 13.4 平衡验证测试

```
测试名称: test_loot_table_balance_1000_runs
目标: 通过 1000 次 seeded 运行验证战利品表的长期平衡 (新经济模型)
状态: MVP 战利品分布 + Phase 2 维修消耗部分

参数:
  rng_seed = 42
  iterations = 1000

  每个冒险类型的预期:
    Short Adventure × 1000:
      平均金币收入: 150-400 gp/次
      平均维修消耗: 30-100 gp/次 (占收入 15-30%)
      平均魔法物品: 1-3 uncommon/次
      Rare+ 物品占比: < 10%

    Medium Adventure × 1000:
      平均金币收入: 500-2000 gp/次
      平均维修消耗: 100-500 gp/次 (占收入 15-30%)
      平均魔法物品: 3-8, 至少 1 rare/次
      Very Rare+ 物品占比: < 5%

    Long Adventure × 1000:
      平均金币收入: 2000-10000 gp/次
      平均维修消耗: 500-3000 gp/次 (占收入 15-30%)
      平均魔法物品: 8-20, 至少 1 very_rare/次
      Legendary+ 物品占比: < 2%

  验证:
    for each tier:
      实际平均值在预期的 ± 15% 范围内
      无物品超出 CR 范围限制
      无 unexpected_rarity 出现
      # 验证商店不出售 Rare+ 物品
      shop_items = generate_all_shop_inventories(tier)
      assert all(item.rarity <= "uncommon" for item in shop_items)
      # 验证金币不会无限累积 (含维修消耗)
      net_gold_per_adventure = avg_gold_income - avg_repair_cost
      assert net_gold_per_adventure in range(50, 500)  # 净收入合理
```

---

## 附录 A: 物品 ID 命名规范

```
物品 ID 格式: item_{category}_{name}_{variant}

规则:
  - 全部小写，单词间用下划线分隔
  - 基础物品: item_{weapon_type}
    例: item_longsword, item_chain_mail, item_shield
  - 魔法变体: item_{weapon_type}_{rarity_short}_{seq}
    例: item_longsword_unc_01 (uncommon 长剑 #1)
  - 附魔物品: item_{weapon_type}_{enchant_prefix_id}_{enchant_suffix_id}
    例: item_longsword_flame_accuracy
  - 消耗品: item_{category}_{effect}
    例: item_potion_healing, item_scroll_magic_missile
  - 材料: material_{type}
    例: material_iron_ingot, material_leather_strap
  - 宝石: gem_{type}_{value}
    例: gem_ruby_100, gem_diamond_5000
```

## 附录 B: 附魔 ID 命名规范

```
附魔 ID 格式: ench_{affix_type}_{effect_name}_{tier}

规则:
  - prefix 前缀: ench_{effect}_{tier}
    例: ench_flame_1, ench_frost_2
  - suffix 后缀: ench_of_{effect}_{tier}
    例: ench_of_accuracy_1, ench_of_power_2
  - implicit 隐含: ench_implicit_{effect}
    例: ench_implicit_soul_drain
```

## 附录 C: 魔法物品充能表 (参考)

| 物品 | 充能数 | 恢复骰 | 典型效果 |
|------|--------|--------|----------|
| Wand of Magic Missiles | 7 | 1d6+1 | 消耗 1 充能释放 1发 魔法飞弹，或消耗 3 充能释放 3发 |
| Wand of Magic Detection | 3 | 1d3 | 消耗 1 充能释放 侦测魔法 |
| Staff of Fire | 10 | 1d6+4 | 燃烧之手(1充能)、火球术(3充能)、火墙术(4充能) |
| Wand of Lightning Bolts | 7 | 1d6+1 | 闪电束(全部充能，每充能+1d6伤害) |
| Staff of Healing | 10 | 1d6+4 | 治疗伤口(1充能)、群体治疗(3充能) |

---

## 附录 D: MVP vs Phase 2 功能边界

| 功能 | MVP (Phase 1) | Phase 2 |
|------|---------------|---------|
| 装备槽位 | Main Hand, Off Hand, Armor, Helmet, Cloak, Boots, Ring ×2, Amulet, Backpack(10+STR_mod×2) | 多tier扩展槽位 |
| 武器类型 | 全部 SRD 简易+军用武器 | + 奇门武器 (exotic) |
| 物品类型 | weapon, armor, shield, potion, scroll, misc, quest | + wand, ring, amulet (全套功能) |
| 附魔系统 | 前缀+后缀, Tier 1-5 (81% 机制附魔) | 隐含附魔、附魔重铸 |
| 同调系统 | 有 (attunement check) | 完整同调UI和叙事 |
| 耐久度 | — | 完整 5 级条件系统、环境退化、永久破坏动画 |
| 灵魂绑定 | Legendary+ 自动绑定 | 手动符文绑定、绑定叙事 |
| 制作系统 | 铁匠铺 + 炼金台 (基础配方) | + 图书馆、附魔研究 |
| 战利品 | 完整稀有度表 + CR 分段 | + Boss 专属池、主题匹配 |
| 物品描述 | 程序化叙事系统(模板引擎+来源上下文) + LLM增强(Copywriter Agent) | 批量预生成、个性化叙事缓存 |
| 商店 | 杂货商 + 铁匠铺 | + 炼金店、魔法书店 |
| 诅咒物品 | 基础诅咒效果 | 诅咒叙事 + Remove Curse |

---

## 附录 E: 参考文献

| 来源 | 用途 |
|------|------|
| D&D 5e SRD v5.1 | 武器属性表、护甲分类、伤害类型 |
| D&D 5e Dungeon Master's Guide | 魔法物品稀有度定价、同调规则 |
| GDD §3.2 酒馆功能区域 | 制作设施解锁条件 |
| GDD §4.2 冒险生成架构 | Copywriter Agent 职责、LLM 调用时机 |
| GDD §5.3-5.4 战斗系统调整 | 槽位制装备、暴击规则 |
| GDD §6.1-6.2 失败与成长系统 | 装备损坏惩罚、传承系统 |
| GDD §7 LLM 集成架构 | Token 预算、Agent 分工、离线降级 |

---

## 14. 依赖关系 (Dependencies)

### 14.1 上游依赖（本系统依赖）

| 依赖系统 | 依赖内容 | 状态 | 风险 |
|----------|----------|:----:|:----:|
| **角色系统** | 装备槽位模型、属性加成、熟练项判定 | ✅ 已审查 | 中 — 需确保slot模型一致 |
| **战斗系统** | 武器伤害骰、AC计算、法术加值 | ✅ 已设计 | 低 |
| **酒馆系统** | 铁匠铺/炼金台/商店 | ✅ 已设计 | 低 |
| **失败与成长系统** | 装备损坏惩罚(MVP移除)、传承系统 | ✅ 已审查 | 低 — MVP移除耐久度，Phase 2恢复 |
| **LLM集成网关** | Copywriter Agent 物品描述增强（在线）— 程序化叙事系统为离线基线 | ✅ 已审查 | 低 |

### 14.2 下游依赖（依赖本系统的系统）

| 依赖系统 | 依赖内容 | GDD状态 |
|----------|----------|:-------:|
| **角色系统** | 装备属性加成、护甲熟练项 | ✅ |
| **战斗系统** | 武器伤害骰、AC计算 | ✅ |
| **酒馆系统** | 铁匠铺/炼金台制作 | ✅ |
| **冒险生成系统** | 战利品tier | ✅ |

---

## 15. 可调参数 (Tuning Knobs)

| 参数 | 当前值 | 安全范围 | 影响面 |
|------|:------:|:--------:|--------|
| **稀有度层级基础成本** | 见定价表 §12.2 | ±50% | 物品经济平衡（替代旧 price × multiplier 模型） |
| **背包基础槽位** | 10 | 8-12 | 角色携带能力 |
| **STR加成槽位** | 每+1调整值+2格 | +1~+3 | 力量型角色优势 |
| **修复基础成本 (Phase 2)** | 查表 §12.2「修复基础成本」列 | ±50% | 修复经济负担 (Phase 2) |
| **卖出价格比例** | 30% | 20-50% | 物品经济出口 |
| **商店库存规模** | 8+d6 | 6-12 | 商店可用性 |
| **鉴定成本 (酒馆付费)** | uncommon 25gp / rare 50gp / very_rare 100gp / legendary 250gp / artifact 500gp | ±50% | 鉴定经济负担 |
| **鉴定 DC (Uncommon)** | 12 | 10-14 | 鉴定难度 |
| **鉴定 DC (Rare)** | 15 | 13-17 | 鉴定难度 |
| **鉴定 DC (Very Rare)** | 18 | 16-20 | 鉴定难度 |
| **鉴定 DC (Legendary)** | 22 | 20-24 | 鉴定难度 |
| **鉴定 DC (Artifact)** | 25 | 23-27 | 鉴定难度 |
| **同调上限** | 3件 | 2-5件 | 魔法物品限制 |
| **附魔槽数（Uncommon）** | 1 | 1-2 | 物品定制深度 |
| **Boss保底稀有度** | Rare | Uncommon-Rare | Boss战利品期望 |

---

## 16. 验收标准 (Acceptance Criteria)

| # | 验收标准 | 测试方法 | 通过条件 | 对应测试 |
|---|----------|----------|----------|----------|
| AC-1 | 物品JSON Schema验证 — 给定有效物品（全部required字段+类型匹配约束）通过；无效物品（缺失字段/类型不匹配/互斥数据块冲突/数值超出范围）被拒绝并输出可读错误 | 单元测试 | 全部10个有效样本通过，10个无效样本被拒绝 | 新增 Schema 验证测试 |
| AC-2 | 装备槽位兼容性 — 所有 §4.3 矩阵中的 (物品类型, 槽位) 组合逐一验证。允许的组合装备成功；不兼容组合拒绝并显示原因（槽位不匹配/双手占用/同调超出） | 单元测试 | 全部130种组合(13类型×10槽位)逐一覆盖，兼容组合全部通过，不兼容组合全部拒绝 | Test I1 + 新增组合覆盖测试 |
| AC-3 | 双手武器占用规则 — 装备two_handed武器时Off Hand标记为占用；装备后试图装备副手物品被拒绝；卸下双手武器后Off Hand释放 | 单元测试 | §13.3边界情况#1全部通过 | §13.3 #1 |
| AC-4 | 耐久度系统 — MVP标记为Phase 2，所有物品默认pristine无性能修正 (Phase 2实现时详见本GDD v1.2+) | 代码审查 | 代码中无耐久度退化逻辑(MVP)，§6内容为Phase 2占位符 | Phase 2 |
| AC-5 | 定价系统 — 物品基础价格使用 §12.2 稀有度层级基础成本表(LookupBasePrice)，非 base_value × rarity_multiplier 公式。所有稀有度×层级组合产出正确价格；修复/制作/回收价格基于同表计算 | 单元测试 | 全部18个(6稀有度×3层级)价格端点验证通过；定价与修复/制作成本互洽 | Test 8 |
| AC-6 | 稀有度掷骰分布 — RollRarity在10000次掷骰后的分布与§3.2概率表误差<2% | 统计测试 | 所有稀有度级别实测频率与预期差距<2% | Test 1 |
| AC-7 | 战利品生成符合CR限制 — 生成的物品稀有度和基础类型不超出CR范围，稀有度分布与§3.2一致 | 集成测试 | 1000次生成无物品超出CR范围；Boss掉落无Common物品(§9.4规则) | Test 5 + I2 |
| AC-8 | 制作配方正确执行 — 成功:消耗材料、产出物品；失败(检定<DC):消耗材料无产出(50%)或退还材料(50%)；自然20:稀有度提升一级；自然1:材料消耗+设施停用至下次长休 | 集成测试 | §8.2-8.5所有配方逐一验证(共40+配方)，每个配方的4种结果分支正确 | Test 7 |
| AC-9 | 商店只卖Common/Uncommon — 商店库存生成1000次，零个物品稀有度 > uncommon | 自动化测试 | 1000次库存生成全部通过稀有度上限检查 | Test 8 Scenario 5 + I3 |
| AC-10 | 弹药消耗规则 — 远程攻击每次消耗1发弹药；战斗结束后回收50%(向上取整)；弹药堆叠每20发占1背包格 | 单元测试 | 模拟10场战斗(每次5-10次远程攻击)，验证消耗、回收、背包占用三项规则 | 新增弹药测试 |
| AC-11 | 鉴定系统 — 未鉴定物品名称显示"未鉴定的 [类型]"，附魔效果不激活，诅咒激活(EVEN IF unidentified)；短休Arcana检定按DC正确揭示；酒馆付费鉴定100%成功 | 端到端测试 | §5A定义的3种鉴定路径全部验证通过 | 新增鉴定测试 |
| AC-12 | 背包槽数=10+STR_mod×2 — 不同STR调整值(-1到+5)对应的背包容量正确 | 单元测试 | STR 8→8格, STR 10→10格, STR 14→12格, STR 16→16格, STR 20→20格 | 新增背包容量测试 |

---

*文档版本: v1.2*
*创建日期: 2026-05-04*
*最后更新: 2026-05-10*
*状态: 设计评审修订中 (MAJOR REVISION — 阻断项修复)*
*读者: 系统开发者、数值设计师、QA 工程师*

> **下一步**: 本 TDD 完成后，需要与 Character System 和 Combat System 的 TDD 进行交叉评审，确保接口一致、数据流正确。
