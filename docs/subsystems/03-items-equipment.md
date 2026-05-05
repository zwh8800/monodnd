# 物品与装备系统 — 技术设计文档

> **Subsystem**: Items & Equipment  
> **Game**: 《酒馆与命运》(Tavern & Destiny)  
> **Rules Reference**: DND 5e SRD
> **Language Policy**: 游戏文本统一采用简体中文，技术标识符使用英文snake_case  
> **Version**: 1.0 — MVP + Phase 2 scope  
> **Status**: 初始设计

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
        "weapon", "armor", "shield", "potion",
        "scroll", "wand", "ring", "amulet",
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
      "description": "基础金币价值（未含稀有度加成）"
    },
    "icon_path": {
      "type": "string",
      "description": "像素图标资源路径，如 'assets/items/weapons/longsword.png'"
    },
    "level_requirement": {
      "type": "integer",
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
        "ac_formula": {
          "type": "string",
          "description": "AC 计算公式字符串，用于系统解析。如 '11 + dex_mod'（皮甲）、'14 + min(dex_mod, 2)'（胸甲）、'18'（全身板甲）"
        },
        "max_dex_bonus": {
          "type": "integer",
          "nullable": true,
          "description": "敏捷调整值上限。轻甲无上限(null)、中甲最大为 2、重甲为 0"
        },
        "strength_requirement": {
          "type": "integer",
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
        "max_durability": { "type": "integer", "default": 100 },
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
  }
}
```

### 2.2 完整物品类型枚举

| type | 说明 | 装备槽位 | 可附魔 | 可堆叠 |
|------|------|----------|--------|--------|
| `weapon` | 武器（近战/远程） | Main Hand / Off Hand / Both | 是 | 否 |
| `armor` | 护甲（轻/中/重） | Armor | 是 | 否 |
| `shield` | 盾牌 | Off Hand | 是 | 否 |
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
| `item_blowgun` | 吹箭筒 | 1 | piercing | 1 | ammunition (25/100), loading | 10 |

#### 军用远程武器 (Martial Ranged Weapons)

| id | 名称 | 伤害 | 伤害类型 | 重量 | 属性 | 射程 | 价值(gp) |
|----|------|------|----------|------|------|------|----------|
| `item_hand_crossbow` | 手弩 | 1d6 | piercing | 3 | ammunition (30/120), light, loading | 30/120 | 75 |
| `item_heavy_crossbow` | 重弩 | 1d10 | piercing | 18 | ammunition (100/400), heavy, loading, two_handed | 100/400 | 50 |
| `item_longbow` | 长弓 | 1d8 | piercing | 2 | ammunition (150/600), heavy, two_handed | 150/600 | 50 |
| `item_net` | 网 | — | — | 3 | special, thrown (5/15) | 5/15 | 1 |

### 2.5 完整护甲表

#### 轻甲 (Light Armor)

| id | 名称 | 基础AC | AC公式 | 重量 | 潜行劣势 | 价值(gp) |
|----|------|--------|--------|------|----------|----------|
| `item_padded` | 棉甲 | 11 | `11 + dex_mod` | 8 | true | 5 |
| `item_leather` | 皮甲 | 11 | `11 + dex_mod` | 10 | false | 10 |
| `item_studded_leather` | 镶钉皮甲 | 12 | `12 + dex_mod` | 13 | false | 45 |

#### 中甲 (Medium Armor)

| id | 名称 | 基础AC | AC公式 | 重量 | 力量要求 | 潜行劣势 | 价值(gp) |
|----|------|--------|--------|------|----------|----------|----------|
| `item_hide` | 兽皮甲 | 12 | `12 + min(dex_mod, 2)` | 12 | — | false | 10 |
| `item_chain_shirt` | 链甲衫 | 13 | `13 + min(dex_mod, 2)` | 20 | — | false | 50 |
| `item_scale_mail` | 鳞甲 | 14 | `14 + min(dex_mod, 2)` | 45 | — | true | 50 |
| `item_breastplate` | 胸甲 | 14 | `14 + min(dex_mod, 2)` | 20 | — | false | 400 |
| `item_half_plate` | 半身板甲 | 15 | `15 + min(dex_mod, 2)` | 40 | — | true | 750 |

#### 重甲 (Heavy Armor)

| id | 名称 | 基础AC | AC公式 | 重量 | 力量要求 | 潜行劣势 | 价值(gp) |
|----|------|--------|--------|------|----------|----------|----------|
| `item_ring_mail` | 环甲 | 14 | `14` | 40 | — | true | 30 |
| `item_chain_mail` | 链甲 | 16 | `16` | 55 | STR 13 | true | 75 |
| `item_splint` | 板条甲 | 17 | `17` | 60 | STR 15 | true | 200 |
| `item_plate` | 全身板甲 | 18 | `18` | 65 | STR 15 | true | 1500 |

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
| Uncommon | uncommon | ×1.5 | 1 | 10% | ×5 | `#1eff00` (绿) |
| Rare | rare | ×2.0 | 1–2 | 40% | ×50 | `#0070dd` (蓝) |
| Very Rare | very_rare | ×3.0 | 2–3 | 70% | ×500 | `#a335ee` (紫) |
| Legendary | legendary | ×5.0 | 3 | 100% | ×5000 | `#ff8000` (橙) |
| Artifact | artifact | ×10.0 | 特殊 | 100% | ×50000 | `#e6cc80` (金) |

> **稀有度修正值 (rarity_modifier)**：用于战利品生成时调整掉率权重。数值越高，表示该稀有度的出现概率经基础概率调整后的实际权重。

### 3.2 稀有度概率表 — 按冒险类型

#### 短冒险 (Short Adventure, ~30 min, CR 0–4)

| 来源 | Common | Uncommon | Rare | Very Rare | Legendary | Artifact |
|------|--------|----------|------|-----------|-----------|----------|
| 普通敌人掉落 | 70% | 25% | 4% | 1% | 0% | 0% |
| 精英敌人掉落 | 40% | 35% | 18% | 5% | 2% | 0% |
| Boss 掉落 | 20% | 30% | 25% | 15% | 8% | 2% |
| 宝箱/隐藏区域 | 50% | 30% | 15% | 4% | 1% | 0% |
| 任务奖励 | 30% | 35% | 25% | 8% | 2% | 0% |
| 商人出售 | 80% | 15% | 5% | 0% | 0% | 0% |

#### 中冒险 (Medium Adventure, ~3 hr, CR 5–10)

| 来源 | Common | Uncommon | Rare | Very Rare | Legendary | Artifact |
|------|--------|----------|------|-----------|-----------|----------|
| 普通敌人掉落 | 50% | 35% | 12% | 2.5% | 0.5% | 0% |
| 精英敌人掉落 | 25% | 30% | 28% | 12% | 4% | 1% |
| Boss 掉落 | 10% | 20% | 30% | 20% | 15% | 5% |
| 宝箱/隐藏区域 | 35% | 30% | 22% | 10% | 2.5% | 0.5% |
| 任务奖励 | 20% | 30% | 28% | 15% | 6% | 1% |
| 商人出售 | 60% | 25% | 12% | 2.5% | 0.5% | 0% |
| 制作 (铁匠铺) | 85% | 12% | 3% | 0% | 0% | 0% |

#### 长冒险 (Long Adventure, ~6 hr, CR 11–16+)

| 来源 | Common | Uncommon | Rare | Very Rare | Legendary | Artifact |
|------|--------|----------|------|-----------|-----------|----------|
| 普通敌人掉落 | 35% | 35% | 20% | 7% | 2.5% | 0.5% |
| 精英敌人掉落 | 15% | 25% | 30% | 18% | 9% | 3% |
| Boss 掉落 | 5% | 15% | 25% | 25% | 20% | 10% |
| 宝箱/隐藏区域 | 20% | 28% | 28% | 16% | 6% | 2% |
| 任务奖励 | 10% | 20% | 30% | 22% | 14% | 4% |
| 商人出售 | 45% | 30% | 18% | 5% | 1.5% | 0.5% |
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
P = {common: 0.20, uncommon: 0.30, rare: 0.25, very_rare: 0.15, legendary: 0.08, artifact: 0.02}

roll = 0.47:
  common:    0.47 < 0.20? No
  uncommon:  0.47 < 0.50? Yes → 返回 uncommon

roll = 0.72:
  common:    0.72 < 0.20? No
  uncommon:  0.72 < 0.50? No
  rare:      0.72 < 0.75? Yes → 返回 rare
```

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

```
角色装备栏 (Character Equipment Slots):

┌─────────────────────────────────────────┐
│  [Head — Helmet]                        │  ← 头盔 (Phase 2, MVP暂不启用)
├──────────────┬──────────────────────────┤
│ [Body — Armor]                          │  ← 护甲
├──────────────┼──────────────┬───────────┤
│ [Main Hand]  │ [Off Hand]   │ [Back]    │  ← 主手/副手/披风
├──────────────┴──────────────┼───────────┤
│ [Hands — Gloves]            │ [Waist]   │  ← 手套/腰带 (Phase 2)
├─────────────────────────────┼───────────┤
│ [Feet — Boots]              │           │  ← 靴子
├──────────────┬──────────────┴───────────┤
│ [Ring 1]     │ [Ring 2]                │  ← 戒指 ×2
├──────────────┼─────────────────────────┤
│ [Amulet]     │                          │  ← 护符
├──────────────┴─────────────────────────┤
│  📦 Backpack (12 slots)                 │  ← 背包 (所有非装备物品)
└─────────────────────────────────────────┘

MVP 阶段槽位: Main Hand, Off Hand, Armor, Ring 1, Ring 2, Amulet, Backpack(12)
Phase 2 扩展槽位: Head, Hands, Feet, Back, Waist
```

### 4.2 槽位枚举

```gdscript
enum EquipmentSlot {
    MAIN_HAND,      # 主手
    OFF_HAND,       # 副手
    ARMOR,          # 护甲
    HEAD,           # 头盔 (Phase 2)
    BACK,           # 披风 (Phase 2)
    HANDS,          # 手套 (Phase 2)
    WAIST,          # 腰带 (Phase 2)
    FEET,           # 靴子 (Phase 2)
    RING_1,         # 戒指 1
    RING_2,         # 戒指 2
    AMULET,         # 护符
    BACKPACK        # 背包 (12格)
}
```

### 4.3 槽位兼容矩阵

| 物品类型 ↓ / 槽位 → | Main Hand | Off Hand | Armor | Ring | Amulet | Backpack |
|---------------------|-----------|----------|-------|------|--------|----------|
| `weapon` | ✅ | ✅ (限定条件) | ❌ | ❌ | ❌ | ✅ |
| `armor` | ❌ | ❌ | ✅ | ❌ | ❌ | ✅ |
| `shield` | ❌ | ✅ | ❌ | ❌ | ❌ | ✅ |
| `potion` | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |
| `scroll` | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |
| `wand` | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ |
| `ring` | ❌ | ❌ | ❌ | ✅ | ❌ | ✅ |
| `amulet` | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ |
| `misc` | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |
| `quest` | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |

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

### 5.1 附魔数据模型 (Enchantment)

```json
{
  "id": "ench_fire_damage_1",
  "affix_type": "prefix",
  "name_affix": "炽焰",
  "name_affix_en": "Flaming",
  "tier": 1,
  "rarity_requirement": "uncommon",
  "stack_limit": 1,
  "compatible_types": ["weapon"],
  "incompatible_enchantments": ["ench_frost_damage_1", "ench_frost_damage_2"],
  "effect": {
    "stat_modifiers": {},
    "damage_bonus": {
      "type": "fire",
      "dice": "1d6",
      "description": "命中时额外造成 1d6 火焰伤害"
    },
    "on_hit_effect": null,
    "special_ability": null
  },
  "curse": null
}
```

### 5.2 附魔槽数量生成算法

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

### 5.3 附魔缀词系统

#### 前缀表 (Prefix) — 武器

| ID | 前缀 | 英文 | 位阶 | 稀有度要求 | 效果 | 冲突 |
|----|------|------|------|-----------|------|------|
| `ench_keen` | 锋利的 | Keen | 1 | uncommon | crit_range 扩展为 19-20 | — |
| `ench_flame_1` | 炽焰的 | Flaming | 1 | uncommon | 命中额外 +1d6 火焰伤害 | 寒冰类 |
| `ench_frost_1` | 霜痕的 | Frost | 1 | uncommon | 命中额外 +1d6 寒冰伤害 | 炽焰类 |
| `ench_shock_1` | 震击的 | Shocking | 1 | uncommon | 命中额外 +1d6 闪电伤害 | — |
| `ench_venom_1` | 毒牙的 | Venomous | 1 | uncommon | 命中额外 +1d6 毒素伤害，DC 13 CON 豁免 | — |
| `ench_radiant_1` | 光耀的 | Radiant | 1 | uncommon | 命中额外 +1d6 光耀伤害 | 黯蚀类 |
| `ench_necrotic_1` | 黯蚀的 | Necrotic | 1 | uncommon | 命中额外 +1d6 黯蚀伤害 | 光耀类 |
| `ench_giant_slayer` | 巨人之祸 | Giant's Bane | 2 | rare | 对巨人(giant)类型额外 +2d6 伤害 | — |
| `ench_dragon_slayer` | 龙之祸 | Dragon's Bane | 2 | rare | 对龙类(dragon)额外 +2d6 伤害 | — |
| `ench_undead_slayer` | 亡灵之祸 | Undead's Bane | 2 | rare | 对亡灵(undead)额外 +2d6 伤害 | — |
| `ench_vampiric_1` | 吸血之 | Vampiric | 2 | rare | 暴击回复 1d6 HP | — |
| `ench_holy` | 神圣的 | Holy | 3 | very_rare | 命中额外 +2d6 光耀伤害，对邪魔/亡灵 +3d6 | 黯蚀类 |
| `ench_unholy` | 亵渎的 | Unholy | 3 | very_rare | 命中额外 +2d6 黯蚀伤害，对天界生物 +3d6 | 光耀类 |
| `ench_storm` | 风暴之 | Tempest | 3 | very_rare | 命中额外 +1d8 闪电 + 1d8 雷鸣 | — |
| `ench_legendary_keen` | 削金断玉的 | Vorpal | 4 | legendary | crit_range 18-20，自然20触发斩首效果 | — |
| `ench_inferno` | 地狱火的 | Hellfire | 4 | legendary | 命中额外 +3d6 火焰伤害，忽视火焰抗性 | 寒冰类 |
| `ench_time_stop` | 时光的 | Chronal | 5 | artifact | 暴击时目标失一回合 | — |

#### 后缀表 (Suffix) — 武器

| ID | 后缀 | 英文 | 位阶 | 稀有度要求 | 效果 |
|----|------|------|------|-----------|------|
| `ench_of_accuracy_1` | 精准之 | of Accuracy | 1 | uncommon | 攻击掷骰 +1 |
| `ench_of_power_1` | 力量之 | of Power | 1 | uncommon | 伤害掷骰 +1 |
| `ench_of_warding_1` | 守护之 | of Warding | 1 | uncommon | 持有者 AC +1 |
| `ench_of_haste` | 疾风之 | of Haste | 2 | rare | 先攻 +3 |
| `ench_of_accuracy_2` | 精魂之 | of Precision | 2 | rare | 攻击掷骰 +2 |
| `ench_of_power_2` | 巨力之 | of Might | 2 | rare | 伤害掷骰 +2 |
| `ench_of_life_steal` | 噬命之 | of Life Steal | 2 | rare | 每次命中回复 1d4 HP |
| `ench_of_critical` | 毁灭之 | of Ruin | 3 | very_rare | 暴击倍数 +1 (若最大化规则，改为 +1d6 暴击附加) |
| `ench_of_accuracy_3` | 天神之 | of Divinity | 3 | very_rare | 攻击掷骰 +3 |
| `ench_of_power_3` | 泰坦之 | of Titans | 3 | very_rare | 伤害掷骰 +3 |
| `ench_of_echo` | 回响之 | of Echoes | 4 | legendary | 攻击命中后获得额外一次附赠攻击 |
| `ench_of_annihilation` | 湮灭之 | of Annihilation | 5 | artifact | 暴击时额外 +4d6 力场伤害 |

#### 前缀表 (Prefix) — 护甲/盾牌

| ID | 前缀 | 英文 | 位阶 | 稀有度要求 | 效果 |
|----|------|------|------|-----------|------|
| `ench_resist_fire_1` | 耐火之 | Fire Resistant | 1 | uncommon | 火焰抗性 |
| `ench_resist_cold_1` | 耐寒之 | Frost Resistant | 1 | uncommon | 寒冰抗性 |
| `ench_resist_lightning_1` | 绝缘之 | Lightning Resistant | 1 | uncommon | 闪电抗性 |
| `ench_reinforced_1` | 强化的 | Reinforced | 1 | uncommon | 基础 AC +1 |
| `ench_shadow_1` | 暗影之 | of Shadows | 2 | rare | 潜行检定优势 |
| `ench_ether_1` | 以太之 | Ethereal | 2 | rare | 每短休 1 次：消耗反应，物理攻击失手 |
| `ench_spell_resist` | 咒抗之 | Spellward | 3 | very_rare | 法术豁免优势 |
| `ench_fortification` | 不屈之 | Fortified | 3 | very_rare | 免疫暴击 |
| `ench_reinforced_2` | 不朽的 | Impervious | 3 | very_rare | 基础 AC +2 |
| `ench_invulnerability` | 无敌的 | of Invulnerability | 4 | legendary | 每日1次：1分钟内非魔法武器伤害免疫 |
| `ench_god_skin` | 神皮之 | Godskin | 5 | artifact | AC +3，所有豁免+2 |

### 5.4 附魔堆叠规则

```
堆叠规则:
  1. 同一 ID 的附魔不可叠加 (stack_limit = 1)
     - 例外: tier 递增的同类附魔（如 "炽焰" vs "地狱火"），高 tier 覆盖低 tier
  2. 冲突附魔不可共存
     - 火焰 vs 寒冰 (对立元素不可共存)
     - 光耀 vs 黯蚀 (对立能量不可共存)
  3. 相同来源的数值加值(bonus)取最高值，不累加
     - 例: 两件物品各提供 "攻击掷骰+1"，只取最大的 +1，不累加为 +2
     - 例外: 不同来源的加值可叠加 (物品加值 + 法术加值 + 职业特性)
  4. 附魔效果的应用顺序:
     implicit (物品固有) → prefix (前缀附魔) → suffix (后缀附魔)
     后应用的数值类效果若与前面冲突，高值覆盖低值，不累加
```

### 5.5 随机附魔生成算法

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
        - 槽3: 隐含 (100% implicit)

     b. 从对应类型的附魔池中筛选候选:
        candidates = enchantments.filter(e =>
          e.rarity_requirement <= item.rarity
          AND e.compatible_types.contains(item.type)
          AND item.enchantments 中不含 e.incompatible_enchantments
          AND item.enchantments 中不含相同 e.id
          AND item.enchantments 中不含与 e 冲突的附魔
        )
        // 按位阶 (tier) 加权随机选择
        // tier 权重: 若 item.rarity == rarity_requirement → weight=5
        //            若 item.rarity > rarity_requirement 一个等级 → weight=3
        //            若 item.rarity > rarity_requirement 多个等级 → weight=1

     c. 从 candidates 中按加权随机选择附魔 enchantment
        weighted_roll = weighted_random(candidates, weight_fn)

     d. 应用附魔: item.magical.enchantments.append(enchantment)

  4. 返回 item
```

### 5.6 诅咒机制

诅咒是负面附魔，通常出现在看似强大的物品上。**诅咒在鉴定 (Identify) 之前不可见**。

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
  3. 诅咒只能通过以下方式移除:
     - 酒馆神殿的 "移除诅咒" 服务 (声望 Lv.7 解锁)
     - 冒险中找到的 "移除诅咒" 卷轴
     - 牧师/圣武士施放 Remove Curse 法术 (Phase 3)

诅咒物品生成概率 (物品稀有度 ≥ rare 时):
  Rare: 5% 概率附带隐藏诅咒
  Very Rare: 8%
  Legendary: 12%
  Artifact: 15%
```


---

## 6. 耐久度系统

### 6.1 条件等级

| 等级 | 英文 | 耐久度范围 | AC影响 | 伤害/攻击影响 | 修复成本 (价值%) | 图标边框 |
|------|------|-----------|--------|-------------|-----------------|----------|
| Pristine | pristine | 100% | 无影响 | 无影响 | — | 无色 |
| Good | good | 75–99% | 无影响 | 无影响 | 10% | 黄色 |
| Worn | worn | 50–74% | -1 AC | -1 伤害 | 25% | 橙色 |
| Damaged | damaged | 25–49% | -2 AC | -1 伤害掷骰, -1 攻击掷骰 | 50% | 红色 |
| Broken | broken | 0% | 物品无法使用 | 物品无法使用 | 100% (或不可修复) | 深红 |

### 6.2 退化规则

```
退化触发条件:
  1. 战斗使用:
     - 武器: 每次暴击失手 (自然1) → 退化 5%
     - 护甲: 每次受到暴击 → 退化 3%
     - 盾牌: 每次成功格挡 → 退化 1%
     - 每次战斗 (所有装备) → 退化 1% (战斗磨损)
  2. 环境:
     - 强酸/火焰/寒冰的环境伤害 → 所有装备退化 2–10% (取决于伤害强度)
     - 陷阱 (尖刺/碾压) → 护甲退化 5–15%
  3. 冒险失败惩罚:
     - 中等失败: 随机 1-2 件装备退化 25%
     - 严重失败: 随机 2-3 件装备退化 50%
     - 灾难性失败: 所有装备退化至 broken

退化计算公式:
  durability_loss = degradation_amount × (100 / max_durability)
  current_durability = max(current_durability - durability_loss, 0)
  condition_level = DetermineConditionLevel(current_durability, max_durability)
```

### 6.3 条件对物品性能的影响

```
性能修正公式:

武器:
  若 condition_level == "worn":
    damage_roll -= 1
  若 condition_level == "damaged":
    damage_roll -= 1
    attack_roll -= 1
  若 condition_level == "broken":
    武器不可使用 (equippable = false)

护甲:
  若 condition_level == "worn":
    ac_bonus -= 1
  若 condition_level == "damaged":
    ac_bonus -= 2
  若 condition_level == "broken":
    护甲不可使用 (equippable = false)
```

### 6.4 修复系统

```
修复流程:
  1. 玩家在酒馆铁匠铺选择要修复的物品
  2. 显示修复成本和所需材料:

     修复成本(gp) = item.value_gp × rarity_multiplier × condition_fix_rate
     - rarity_multiplier: common=1, uncommon=1.5, rare=2, very_rare=3, legendary=5, artifact=10
     - condition_fix_rate = (100 - current_durability) / 100 * rate_per_tier
       rate_per_tier: pristine→0%, good→0.10, worn→0.25, damaged→0.50, broken→1.00

     材料需求:
     - 从 worn 修复到 good: 基础材料 ×1
     - 从 damaged 修复到 good: 基础材料 ×2 + 稀有材料 ×1
     - 从 broken 修复到 good: 基础材料 ×3 + 稀有材料 ×2 + 铁砧费用

  3. 修复时间:
     - 至 good 状态: 瞬时 (1 酒馆场景帧)
     - 至 worn 状态: 1 次短休时长
     - 从 broken 修复: 1 次长休时长

修复示例:
  一把稀有 (rare) 长剑 (value_gp=15) 处于 damaged (35%):
  基础修复成本 = 15 × 2 × 0.50 = 15 gp
  需要: 铁矿 ×2 + 秘银锭 ×1
```

### 6.5 物品损坏 (Broken)

```
Broken 状态效果:
  - 物品不可装备 (equippable = false)
  - 仍在背包中占用空间
  - 不可在战斗中使用
  - 附魔效果全部失效 (即使是神器)

可修复性:
  - Common/Uncommon: 几乎总是可修复
  - Rare/Very Rare: 可修复，但提示 "此物品修复后将失去所有附魔" (50% 概率保留)
  - Legendary: 需特殊任务材料才可修复
  - Artifact: 不可修复 — 损坏即永久失去

永久破坏:
  当 broken 物品再次受到用于退化的伤害时:
    - Common/Uncommon: 10% 概率永久破坏 (物品从背包中删除)
    - Rare/Very Rare: 5% 概率
    - Legendary/Artifact: 不触发 (需特殊事件)
```

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

## 10. LLM 物品描述生成

### 10.1 Copywriter Agent 角色定义

遵循 GDD §7 中定义的 LLM Agent 架构，Copywriter Agent 负责为物品生成叙事 flavor text。**Copywriter Agent 不决定物品数值**，仅生成描述文案。

### 10.2 输入 Schema

```
Agent: Copywriter Agent
调用时机: 物品生成 (战利品/制作/商店) 时
频率: 每次新物品（有缓存优化）
Token预算: 1000 input / 300 output
模型: 轻量模型 (GPT-3.5 / Claude Haiku)

输入 Schema (copywriter_request.json):
```

```json
{
  "request_id": "copywriter_req_20240504_001",
  "character_context": {
    "party_level": 3,
    "party_composition": [
      { "name": "索林", "race": "dwarf", "class": "fighter", "personality": "固执/忠诚" },
      { "name": "艾琳", "race": "elf", "class": "wizard", "personality": "好奇/傲慢" }
    ]
  },
  "adventure_context": {
    "theme": "Gothic_Horror",
    "tone": "mystery",
    "location": "被遗忘的墓穴"
  },
  "item_data": {
    "name": "霜痕长剑",
    "type": "weapon",
    "subtype": "longsword",
    "rarity": "rare",
    "material": "寒铁合金",
    "properties": ["versatile"],
    "enchantments": [
      {
        "name": "霜痕",
        "effect_description": "命中时额外造成 1d6 寒冰伤害"
      }
    ],
    "source": "boss_loot",
    "source_name": "冰霜妖灵领主"
  }
}
```

### 10.3 输出 Schema

```json
{
  "request_id": "copywriter_req_20240504_001",
  "description": "这把长剑握在手中有一种不自然的冰凉。剑刃泛着淡蓝色的微光，仿佛封印着一片冬天的气息。传说这是冰霜妖灵领主用千年寒铁铸造的三把剑之一——每一把都承载着它冻结的灵魂碎片。",
  "flavor_text": "封印着一片冬天的气息",
  "narrative_tags": ["寒冷", "古老", "诅咒残留", "领主遗物"],
  "short_description": "一把泛着蓝光的寒铁长剑，剑刃永远冰凉。"
}
```

### 10.4 缓存策略

```
Copywriter 缓存规则:
  - 相同 item_id → 命中缓存 → 返回已有描述
  - 相同 (type + rarity + enchantment_ids) → 90% 命中缓存
  - 相同 (type + rarity + adventure_theme) → 50% 命中缓存
  - 缓存存储: SQLite，键 = MD5(item_signature)

  缓存容量: 最多 5000 条
  淘汰策略: LRU (最近最少使用)
```

### 10.5 离线降级方案

当 LLM API 不可用时，使用预置描述模板：

```
离线描述模板结构:

{
  "item_longsword": {
    "common": "一把普通的钢制长剑。",
    "uncommon": "一把制作精良的长剑，刃口在光下泛着寒光。",
    "rare": "这把长剑的剑身上隐隐流转着魔法符文。",
    "very_rare": "这把长剑如同活物般微微颤动，渴望战斗。",
    "legendary": "传说级别的长剑，每一次挥动都能听见远古英雄的低语。",
    "artifact": "这把剑的名字刻在历史中——它的每一次出鞘都将改变命运的走向。"
  },
  "enchantment_overrides": {
    "ench_flame_1": "剑身上燃烧着永不熄灭的魔法火焰。",
    "ench_frost_1": "剑刃覆盖着一层永不融化的冰霜。"
  }
}

组合规则:
  description = base_template[item_subtype][rarity] + " " + enchantment_overrides[ench_id]
  例如: "一把制作精良的长剑，刃口在光下泛着寒光。剑刃覆盖着一层永不融化的冰霜。"
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

```
AC Calculation Pipeline:

  Step 1: 确定基础 AC
    if equipped armor:
      base_ac = armor.base_ac_value  // 或解析 armor.ac_formula
    if no armor (unarmored):
      base_ac = 10 + dex_mod

  Step 2: 应用护甲敏捷上限
    if armor.max_dex_bonus != null:
      effective_dex = min(dex_mod, armor.max_dex_bonus)
    else:
      effective_dex = dex_mod

    // 重甲: max_dex_bonus = 0, 不添加敏捷
    // 中甲: max_dex_bonus = 2
    // 轻甲: max_dex_bonus = null (无上限)

  Step 3: 添加盾牌加值
    if off_hand has shield:
      ac += shield.ac_bonus  // +2 standard

  Step 4: 添加附魔加值
    for each equipped item:
      for each enchantment (if attuned):
        if enchantment.effect.stat_modifiers.ac:
          // 使用 GDD 堆叠规则: 同名加值取最大值
          ac += max_ac_bonus_from_enchantments

  Step 5: 应用耐久度修正
    if armor.condition_level == "worn":
      ac -= 1
    if armor.condition_level == "damaged":
      ac -= 2
    if armor.condition_level == "broken":
      // 护甲不可用，处理为 unarmored
      ac = 10 + dex_mod

  Step 6: 添加种族/职业特性
    // 例: 蛮族 Unarmored Defense, 法师 Mage Armor
    // 这些由 Character System 提供，此处仅为接口

  Final AC = base_ac + effective_dex + shield_bonus + enchantment_bonus
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

  Step 5: 应用耐久度修正
    if condition_level == "worn":  damage_penalty = 1
    if condition_level == "damaged": damage_penalty = 1 (and attack_penalty = 1)
    if condition_level == "broken": weapon unusable

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
| 杂货商 | 大厅 | 初始 | 基础消耗品、基础武器、弹药 | 每次冒险返回后 |
| 铁匠铺 | 铁匠铺 | 声望 Lv.2 | 武器、护甲、盾牌 | 每次长休后 |
| 炼金店 | 炼金台 | 声望 Lv.3 | 药水、油、毒药、材料 | 每次冒险返回后 |
| 魔法书店 | 图书馆 | 声望 Lv.5 | 法术卷轴、魔杖、魔法材料 | 每次长休后 |

### 12.2 定价模型

```
价格公式:

  卖价 (Shop → Player):
    price = base_value × rarity_multiplier × condition_adjuster × reputation_modifier

  回收价 (Player → Shop):
    sell_price = base_value × rarity_multiplier × 0.5 × condition_adjuster × reputation_modifier

  其中:
    rarity_multiplier:
      common:     ×1
      uncommon:   ×5
      rare:       ×50
      very_rare:  ×500
      legendary:  ×5000
      artifact:   ×50000 (传说级，通常不出售于常规商店)

    condition_adjuster:
      pristine: 1.0
      good:     0.9
      worn:     0.65
      damaged:  0.35
      broken:   0.05

    reputation_modifier:
      声望 Lv.1-2: ×1.25
      声望 Lv.3-5: ×1.10
      声望 Lv.6-8: ×1.00
      声望 Lv.9+:   ×0.90

定价示例:
  一把稀有 (rare) 长剑，耐久 pristine:
    sell_price = 15 × 50 × 1.0 × 1.10 (声望 Lv.3) = 825 gp

  玩家卖出同一把剑 (worn, 60%):
    buyback_price = 15 × 50 × 0.5 × 0.65 × 1.10 = 268 gp
```

### 12.3 商店库存刷新

```
商店刷新算法:

  1. 基础库存大小:
     杂货商: 8 + d6 物品
     铁匠铺: 6 + d4 物品
     炼金店: 6 + d4 物品
     魔法书店: 4 + d4 物品

  2. 库存生成:
     for i in [1, inventory_size]:
       rarity = RollRarity("shop", adventure_tier)
       item = generate item as per loot generation
       inventory.append(item)

  3. 必定刷新物品 (不受随机影响):
     杂货商: 箭矢×20 (×3), 治疗药水 (×2), 火把×5, 口粮×5
     铁匠铺: 基础维修工具包 (×1)
     炼金店: 治疗药水 (×3), 解毒剂 (×2)
     魔法书店: 1环法术卷轴 (随机3种, 各×1)

  4. 特殊商品 (稀有, 每个刷新周期有概率出现):
     铁匠铺:  稀有材料 (25%概率), 魔法武器 (10%概率)
     炼金店:  稀有配方 (15%概率)
     魔法书店: 3环法术卷轴 (10%概率)
```

### 12.4 修复与升级成本

```
修理成本 (见 §6.4):
  repair_cost = item.value_gp × rarity_multiplier × condition_fix_rate

升级成本 (常规→魔法):
  // 铁匠铺: 为 common 武器/护甲 添加 1 个附魔槽 (uncommon 化)
  upgrade_cost = item.value_gp × 20 + rare_materials × 3
  成功概率: DC 18 smithing check
  失败: 物品 durability -25%，材料消耗
  暴击失败 (自然1): 物品 broken

  // 图书馆: 为 uncommon+ 物品添加额外附魔槽 (附魔研究)
  upgrade_cost = item.value_gp × 100 + arcane_crystals × 5
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
    - 价格: ×1.5 基础价格 (旅行溢价)
    - 回收价: ×0.3 基础价格 (只接受宝石和货币交换)
    - 特殊: 可能出售该冒险主题的专属消耗品
      (例: 亡灵主题冒险中出售圣水和光耀卷轴)
    - 无 reputation_modifier (不享受酒馆声望折扣)
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
  assert 至少 1% Legendary drop (expected ~8%)
  assert 至少 0.1% Artifact drop (expected ~2%)
  assert 不低于 10% Common drop (expected ~20%)
```

#### Test 6: 耐久度退化

```
测试名称: test_condition_degradation
目标: 验证耐久度退化和性能修正

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
目标: 验证定价公式

场景:
  1. Common 长剑, pristine, 声望 Lv.3:
     price = 15 × 1 × 1.0 × 1.10 = 16gp (舍入)
  2. Rare 细剑, worn(60%), 声望 Lv.5:
     price = 25 × 50 × 0.65 × 1.10 = 893gp
  3. 卖出 rare 物品 → price × 0.5
     893 × 0.5 = 446gp
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
目标: 运行 100 次完整短冒险，验证经济平衡

流程:
  for i in range(100):
    adventure = generate_short_adventure()
    for encounter in adventure.encounters:
      loot = generate_loot(encounter)
      add_to_party_inventory(loot)

  # 验证:
  assert total_gold in range(500, 5000)  # 合理范围
  assert magic_items_count in range(3, 15)
  assert average_rarity > "common" and average_rarity < "legendary"
  # 确保 market_value 可用于后续铁匠铺/商店消费
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

  4. Broken 物品:
     - 武器 broken → 不可攻击
     - 护甲 broken → AC 归零
     - broken 物品的附魔 → 全部失效
     - 尝试修复 broken → 检查成本和材料

  5. 灵魂绑定:
     - 绑定物品不可出售 (出售确认框禁用)
     - 全灭后绑定物品保留
     - 角色死亡传承绑定物品

  6. 背包满:
     - 背包 12/12 → 新战利品 → 弹出 "背包已满，是否丢弃物品？"
     - 丢弃物品确认对话框

  7. 诅咒物品:
     - 装备诅咒物品 → 自动同调 (不占槽位)
     - 尝试卸下 → "你无法放下这把剑...它似乎不愿离开你的手"
     - 神殿 Remove Curse → 诅咒消失，物品可正常卸下

  8. 附魔冲突:
     - 尝试为火焰附魔武器添加寒冰附魔 → 弹窗 "此物品已有炎属性附魔，寒冰附魔无法共存"
```

### 13.4 平衡验证测试

```
测试名称: test_loot_table_balance_1000_runs
目标: 通过 1000 次 seeded 运行验证战利品表的长期平衡

参数:
  rng_seed = 42
  iterations = 1000

  每个冒险类型的预期:
    Short Adventure × 1000:
      平均金币收入: 150-400 gp/次
      平均魔法物品: 1-3 uncommon/次
      Rare+ 物品占比: < 10%

    Medium Adventure × 1000:
      平均金币收入: 500-2000 gp/次
      平均魔法物品: 3-8, 至少 1 rare/次
      Very Rare+ 物品占比: < 5%

    Long Adventure × 1000:
      平均金币收入: 2000-10000 gp/次
      平均魔法物品: 8-20, 至少 1 very_rare/次
      Legendary+ 物品占比: < 2%

  验证:
    for each tier:
      实际平均值在预期的 ± 15% 范围内
      无物品超出 CR 范围限制
      无 unexpected_rarity 出现
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
| 装备槽位 | Main Hand, Off Hand, Armor, Ring ×2, Amulet, Backpack(12) | + Head, Hands, Feet, Back, Waist |
| 武器类型 | 全部 SRD 简易+军用武器 | + 奇门武器 (exotic) |
| 物品类型 | weapon, armor, shield, potion, scroll, misc, quest | + wand, ring, amulet (全套功能) |
| 附魔系统 | 前缀+后缀, 最多1 tier | 多tier附魔, 隐含附魔 |
| 同调系统 | 有 (attunement check) | 完整同调UI和叙事 |
| 耐久度 | 完整 5 级条件系统 | 环境退化、永久破坏动画 |
| 灵魂绑定 | Legendary+ 自动绑定 | 手动符文绑定、绑定叙事 |
| 制作系统 | 铁匠铺 + 炼金台 (基础配方) | + 图书馆、附魔研究 |
| 战利品 | 完整稀有度表 + CR 分段 | + Boss 专属池、主题匹配 |
| LLM 描述 | Copywriter Agent + 离线降级 | 缓存优化、批量预生成 |
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

*文档版本: v1.0*
*创建日期: 2026-05-04*
*状态: 初始技术设计 — 待与 Character System 和 Combat System TDD 对接*
*预计行数: ~2000+*
*读者: 系统开发者、数值设计师、QA 工程师*

> **下一步**: 本 TDD 完成后，需要与 Character System 和 Combat System 的 TDD 进行交叉评审，确保接口一致、数据流正确。
