# 冒险生成系统 — 叙事质量对抗性评审

> **评审角色**: Narrative Director (对抗性评审)
> **日期**: 2026-05-10
> **被评审文档**: `design/gdd/06-adventure-generation.md` (v1.0)
> **关联文档**: `design/gdd/02-llm-integration.md` (v1.2)
> **评审方法**: 对抗性设计评审 — 从叙事连贯性、角色深度、玩家幻想实现这三个维度攻击架构假设
> **前提阅读**: 本评审聚焦于 `06-adventure-generation.md` 本身的设计缺陷。与 `llm-integration-narrative-review-2026-05-10.md`（LLM集成架构批评）存在交叉引用，但分析角度不同——本评审关注的是冒险生成管线的"叙事架构"，而非LLM调用的"工程架构"。

> **游戏叙事承诺 (Pillar 1)**:
> - "这个故事是我的" — 每次冒险由LLM编剧Agent生成独一无二的剧本
> - "命运的骰子在转动" — 三层管线让冒险既有LLM创意又有程序平衡
> - "选择真的有后果" — 分支节点不可返回，每个选择导向不同结局

---

## 评审总结

**管线总体评价**: 三层管线架构在技术层面设计精巧——LLM蓝图生成 → 程序化实例化 → DM Agent实时叙事的分工清晰。但从叙事连贯性角度审视，**三层之间的信息传递存在系统性断裂**：Screenwriter生成完整剧本 → 但DM Agent无法访问剧本全貌 → 实例化层在中间充当中介时丢失了大量叙事上下文。这导致了一个悖论：**越是精心设计的剧本，DM Agent在实时叙述时越无法忠实呈现**。

**关键矛盾**: 编剧Agent拥有4000 input / 2000 output tokens的预算来生成蓝图，但DM Agent只有2000 input / 500 output tokens来消费这个蓝图。4000 tokens创造的信息，被压缩到200-400 tokens的"adventure_summary"中传递给DM Agent。信息压缩比约为10:1。在10:1的压缩率下，任何叙事连贯性都是不可能的。

**发现的严重问题**: 2个BLOCKER / 4个MAJOR / 3个MINOR

---

## 发现1: BLOCKER — DM Agent缺乏完整叙事弧线知识

**严重性**: 🔴 BLOCKER  
**影响的叙事承诺**: "命运的骰子在转动"（DM应有完整故事掌控）、"选择真的有后果"（DM应理解选择在全弧线中的位置）  
**关联GDD章节**: §1.3 三层管线架构、§9.2 上下文注入策略  
**关联已有评审**: llm-integration-narrative-review 批判#2（DM Agent被过度节流）

### 问题描述

编剧Agent（§3.1）生成完整的冒险蓝图，包含20+个字段：
- `plot_outline.central_conflict` — 核心冲突
- `plot_outline.acts.{act_1, act_2, act_3}` — 三幕结构，含twist（转折）和climax（高潮）
- `plot_outline.nodes[]` — 5-50个场景节点，每个节点有description、choices、encounter_id等
- `plot_outline.endings[]` — 2-4个结局条件
- `key_npcs[]` — 每个NPC的motivation、secret、personality_tags、voice_style
- `narrative_hooks.personal_stakes[]` — 每个角色的个人叙事钩子

然而，DM Agent（§9.2）收到的上下文仅为：
- System Prompt (~300 tokens)
- **adventure_summary** — "由AdventureLog生成的200字摘要" (~200-400 tokens)
- current_node info (~100 tokens)
- 最近5个动作历史 (~200 tokens)
- 活跃NPC信息（当前节点关联的）(~100 tokens)
- 队伍状态 (~100 tokens)

**DM Agent完全不知道以下信息：**
| 蓝图中的关键叙事数据 | DM Agent是否知晓 | 后果 |
|---------------------|:---:|------|
| 三幕结构（故事处于哪一幕？） | ❌ | 不知道叙事节奏——第一幕该铺垫，第三幕该高潮 |
| 核心冲突的真相 | ❌ | "考古领队主动打开封印"这个twist只存在于blueprint中，DM不知道何时该透露 |
| 尚未发生的节点 | ❌ | "玩家即将进入Boss房间"——DM不知道，无法做叙事铺垫 |
| 可能的结局条件 | ❌ | DM不知道"打断仪式"和"让仪式完成"各通向什么结局 |
| NPC的完整动机和秘密 | ❌（仅当前节点关联NPC） | NPC维尔特有"secret: 他主动打开了封印"，DM不知道，对话中无法暗示 |
| 角色专属叙事钩子 | ❌ | 艾尔登的"发现失传防护法术"钩子——DM不知道，无法在相关场景提及 |
| Act 2的twist | ❌ | "考古领队主动打开了封印"——DM不知道，无法在Act 1做伏笔 |

### 叙事后果

以一个实际案例说明。蓝图中定义了：
- **Act 2 twist**: "考古队领队维尔特主动打开了封印，为了获得永生"
- **side_library节点**: 玩家发现维尔特的日记，暗示他并非被控制
- **ritual_chamber节点**: 维尔特站在法阵中央

在理想叙事中：
1. entrance_campsite → DM暗示"商会的人欲言又止"（伏笔1）
2. entry_hall → 尸体上的灼痕是负能量（伏笔2）
3. side_library → 发现日记，揭露真相
4. dark_corridor → DM可以叙事："石棺上的符文与你之前在图书馆看到的如出一辙——这一切都与维尔特有关"
5. ritual_chamber → DM知道真相已揭露，可以写出维尔特"不是被控制，而是自愿的"

在DM Agent的2000 token上下文中：
1. entrance_campsite → DM描述营地（无伏笔能力——不知道后面有twist）
2. entry_hall → DM描述尸体灼痕（无伏笔能力）
3. side_library → DM描述图书室（不知道日记的重要性）
4. dark_corridor → DM描述战斗（前5个动作的上下文是战斗动作，不是叙事）
5. ritual_chamber → DM只知道"当前节点是个boss房间"，**对维尔特的故事一无所知**

**结果**: 一个精心设计的三幕反转剧本，在实际游玩中变成了一连串孤立的场景描述。twist在side_library发生时DM不知道这是twist，在ritual_chamber高潮时DM不知道前面发生过什么。整个叙事弧线消失了。

### 根因

三层管线的信息流存在结构性缺陷：

```
编剧Agent (4000/2000 tokens) → 蓝图 JSON (完整叙事信息)
    ↓
实例化引擎 (程序层) → 节点图 + 遭遇数据 + NPC配置 (只保留程序化需要的信息)
    ↓
DM Agent (2000/500 tokens) → 实时叙事 (只收到adventure_summary，丢失90%叙事信息)
```

**核心问题**: 实例化引擎是程序层，它关心的信息是"这个战斗节点的CR是多少"、"这个NPC的stat_block是什么"——不是"这个故事的twist是什么"、"NPC的秘密何时该揭露"。所以实例化引擎在消费蓝图时，把叙事信息丢掉了。而当DM Agent需要这些信息时，实例化引擎没有把它传递下去。

### 修复建议

**方案A: 增强DM Agent上下文注入（推荐）**

在DM Agent的上下文构建中，从蓝图中提取"叙事骨架"注入：

```json
// DM Agent上下文中新增 narrative_skeleton 字段 (~400-600 tokens)
{
  "narrative_skeleton": {
    "current_act": "act_2",
    "current_act_description": "回廊深处的探索，发现古代机关的真相",
    "central_conflict": "考古领队维尔特主动打开了封印，为了获得永生",
    "upcoming_twist": "维尔特不是被控制的——他是主动的",
    "twist_reveal_condition": "玩家到达side_library或ritual_chamber",
    "unrevealed_secrets_for_current_npcs": [
      {"npc": "维尔特", "secret": "他主动打开了封印", "hint_when": "在ritual_chamber之前的任何节点"}
    ],
    "foreshadowing_targets": [
      {"theme": "封印不是被破坏的，是被主动打开的", "plant_in": "entrance_campsite或entry_hall"}
    ],
    "available_endings": ["打断仪式-victory", "让仪式完成-victory", "撤退-defeat"],
    "ending_conditions": "玩家在ritual_chamber的选择决定结局"
  }
}
```

这需要Screenwriter Agent在生成蓝图时额外输出一个"叙事骨架摘要"——或者由程序从蓝图中提取。

**方案B: 让Blueprint在整个生命周期中可用**

不使用adventure_summary，而是让DM Agent直接从AdventureState中获取当前节点在完整蓝图中的位置。程序在构建DM Agent上下文时，提取：
- 当前act的所有节点描述（紧凑版，~200 tokens）
- 当前节点的前驱和后继节点（当前节点在叙事弧中的位置）
- 当前节点关联NPC的secret和motivation

这不需要LLM额外工作——程序可以从已验证的蓝图中提取。

**方案C: 分级上下文注入（折中）**

根据DM Agent的request_type动态决定注入内容：
- `scene_atmosphere`: 注入current_act_info + 前后节点预览（DM需要知道当前场景在故事中的位置）
- `npc_dialogue`: 注入该NPC的完整secret + motivation + voice_style
- `combat_narration`: 轻量级上下文（战斗不需要完整叙事弧线）
- `choice_presentation`: 注入available_endings_summary（DM需要知道选择通向什么）

---

## 发现2: BLOCKER — 动作历史压缩导致关键叙事信息永久丢失

**严重性**: 🔴 BLOCKER  
**影响的叙事承诺**: "选择真的有后果"（选择后果在压缩中消失）、"这个故事是我的"（DM失去故事记忆）  
**关联GDD章节**: §9.2 上下文压缩规则、§8.3 AdventureLog.get_summary()

### 问题描述

§9.2 的上下文压缩规则规定：
> "超出10个动作的历史 → 由AdventureLog生成200字摘要替代"

这条规则存在三个严重问题：

**问题A: 200字是什么概念？**

200汉字 ≈ 130 tokens ≈ 英文约100词。以下是一个实际的例子——假设玩家在当前冒险中已经完成了这些动作：

1. 在营地接受马库斯的任务委托
2. 调查营地的帐篷，发现考古队失踪的线索
3. 索林用蛮力砸开封印之门
4. 在前厅发现第一具尸体，检查灼痕
5. 艾尔登解读壁画，了解幽影领主的传说
6. 莉莉丝发现通往图书室的密门
7. 在图书室阅读维尔特的日记，发现他主动打开了封印
8. 遭遇石棺伏击，击败暗影仆从
9. 索林在战斗中使用了战技，莉莉丝受到轻伤
10. 艾尔登消耗了1个2环法术位
11. 队伍讨论是否要在进入Boss房间前休息
12. 最终决定继续前进

这12个动作被压缩为200字的摘要时：
- 动作1-10的详细信息（谁做了什么、为什么、发现了什么）全部丢失
- 动作11-12的叙事上下文丢失

200字的摘要可能是这样的：
*"队伍在营地接受委托后进入了古庙。索林砸开了封印之门。他们在前厅发现了尸体和壁画。莉莉丝发现了图书室，阅读了维尔特的日记。随后遭遇石棺伏击并击败敌人。队伍决定继续前进。"*

所有角色细节、情感层次、发现的微妙细节——全部丢失。

**问题B: AdventureLog.get_summary()的实现不支持叙事压缩**

§8.3的AdventureLog代码显示：
```gdscript
func get_summary(max_chars: int = 400) -> String:
    var summary = ""
    for entry in entries.slice(-10):  # 最近10条
        summary += entry.data.get("summary", "") + "。"
    if summary.length() > max_chars:
        summary = summary.substr(0, max_chars) + "..."
    return summary
```

这个方法只是**拼接**最近10条log entry的summary字段，然后截断到max_chars。它不是"摘要"——它是"拼接+截断"。这意味着：
- 如果截断发生在句子中间，信息就是不完整的
- 没有优先级机制——重要的叙事发现（"维尔特是叛徒"）和不重要的细节（"索林消耗了1个hit die"）被同等对待
- 没有LLM参与摘要生成——纯字符串拼接

**问题C: 10个动作的上限极其容易达到**

一次短冒险（30分钟）中玩家的动作数量估算：
- 进入场景：7-8次（每个节点一次）
- 与NPC对话：1-2次
- 探索交互（检查尸体、阅读壁画、搜索房间）：3-4次
- 战斗动作：3-4次（每次战斗1-2个回合的动作叙述）
- 选择呈现：2-3次

**总计：16-21个动作**。在30分钟的短冒险中，10个动作的上限可能在冒险进度60%时就已经达到。这意味着冒险最后40%的时间里，DM Agent对冒险前60%发生的事情**完全失忆**。

对于中冒险（3小时）：可能50-80个动作。10个动作的上限意味着DM Agent只记得最近15-20分钟发生的事情。

### 叙事后果

**具体场景**: 玩家在图书室（节点3）发现了维尔特的秘密——他是主动打开封印的叛徒。这个发现应该显著影响后续所有叙事：DM应该在描述中暗示"这一切都说得通了"，NPC的反应应该不同，战斗应该有"你知道真相"的叙事色彩。

但在当前架构下：
- 到达ritual_chamber（节点6）时，节点3的"发现日记"动作可能已经滑出10个动作窗口
- DM Agent的上下文中只有最近5个动作的详细信息和200字的摘要
- 200字摘要可能根本没有提到"维尔特是叛徒"这个关键信息
- DM Agent会生成一个**好像什么都没有发生过**的Boss房间描述

**"选择真的有后果"的承诺在此刻完全破裂**——不是因为选择没有后果，而是因为DM Agent不记得你做了什么选择。

### 根因

设计和实现之间存在不一致：
- 设计（§9.2）："超出10个动作的历史 → 由AdventureLog生成**200字摘要**替代"（暗示通过LLM生成智能摘要）
- 实现（§8.3）：AdventureLog.get_summary()是**字符串拼接+截断**（不是智能摘要）

正确的实现应该是：当历史超过10个动作时，调用一个轻量级LLM（或本地规则引擎）生成一个结构化的叙事摘要——保留关键剧情转折、重要发现、NPC互动——而不是简单地拼接+截断。

### 修复建议

**方案A: 结构化动作历史+分层保留（推荐）**

不将所有动作历史压缩为单一摘要字符串，而是维护分层动作记录：

```
动作历史分为三层:
1. 近层（最近5个动作）→ 完整保留 → DM Agent收到完整细节
2. 中层（6-15个动作）→ 结构化摘要 → DM Agent收到 {节点, 类型, 关键叙事影响}
3. 远层（16+动作）→ 叙事里程碑 → DM Agent收到 "已完成的关键叙事事件" 列表
```

分层保留确保：
- 近期的战斗细节有完整上下文
- 中期的探索和发现被压缩但保留关键信息
- 早期的关键叙事转折点（如"发现了叛徒的秘密"）永不丢失

**方案B: 用LLM生成智能摘要（推荐配合方案A）**

当动作超过阈值时，使用一次轻量级LLM调用（Gpt-4o-mini, ~100 tokens）生成叙事摘要，保留：
- 关键发现："在图书室发现了维尔特的日记，揭示他主动打开了封印"
- NPC互动："与马库斯交谈，他隐瞒了古庙的危险性"
- 选择后果："选择砸开封印而不是用法术解除——幽影领主知道你们来了"

而不是当前的纯字符串拼接。

**方案C: 叙事里程碑系统**

在AdventureState中维护一个独立的"叙事里程碑"列表：
```json
{
  "narrative_milestones": [
    {"event": "discovered_wilert_is_traitor", "node": "side_library", "importance": "critical"},
    {"event": "learned_shadow_lord_legend", "node": "entry_hall", "importance": "important"},
    {"event": "broke_seal_with_force", "node": "sealed_entrance", "importance": "minor"}
  ]
}
```

当DM Agent上下文构建时，根据importance字段决定是否注入。critical级别的里程碑永远不会从上下文中移除——即使它发生在50个动作之前。

---

## 发现3: MAJOR — 分支路径的叙事闭合未得到设计保障

**严重性**: 🟠 MAJOR  
**影响的叙事承诺**: "选择真的有后果"  
**关联GDD章节**: §5 分支逻辑实现、§3.3 长冒险模板（multiple_endings）

### 问题描述

分支逻辑（§5）允许玩家在不同的节点间选择不同的路径。一旦选择，不可返回。节点图可以有2-8个分支（取决于冒险长度）。

但GDD没有回答一个关键的叙事问题：**当玩家只经历了N个可能分支中的1个时，这个单一分支的叙事是否自洽且令人满足？**

以文档中的示例为例：
- `ritual_chamber`节点有两个选择：`disrupt_ritual`（打断仪式）和`fight_full_power`（让仪式完成）
- 选打断仪式 → 幽影领主虚弱版 → 维尔特死亡 → ending: victory_disrupted
- 选让仪式完成 → 幽影领主完整版 → 更好战利品 → ending: victory_full

假设玩家选了"打断仪式"：
1. 维尔特死亡。但之前的叙事中，维尔特是核心反派——他的秘密（主动打开封印）在打断仪式后直接被"维尔特死亡"终止。**玩家可能想知道：他为什么要这么做？他后悔吗？** ——但维尔特死了，这些问题没有答案。
2. ending: victory_disrupted 的描述是"封印重新稳固"。但玩家在图书室学到的关于维尔特的全部背景——他的研究、他的堕落的动机——在这个结局中没有得到回应。
3. 玩家可能不知道"如果让仪式完成"会有什么后果——因为没有看到另一条路径。但叙事应该让玩家感到**自己的选择是正确的、有意义的**，而不是让他们怀疑"另一条路会不会更好"。

### 叙事后果

在分支叙事设计中，有一个常见的问题：**"分支与主干"的不对称性**。设计者（LLM编剧Agent）看到了所有的分支，知道全貌，所以他认为每个分支都是完整故事的一部分。但玩家只看到一条路径——如果这条路径没有提供完整的情感闭合，玩家会感到故事"没讲完"。

当前GDD中没有以下保障：
- ❌ 没有要求每个分支路径必须有独立的情感弧线（setup → conflict → resolution）
- ❌ 没有要求epilogue节点必须回应冒险中揭示的NPC秘密
- ❌ 没有在ending schema中要求"情感闭合度"验证
- ❌ 没有要求编剧Agent为每个ending提供"该分支的独立叙事弧线摘要"

### 根因

Screenwriter Agent的System Prompt（§3.1.2）只要求"设计有意义的剧情节点，每个节点提供2-4个选择"和"选择必须有真实的后果"。但它没有要求"每个分支路径必须自成完整的叙事弧线，不依赖其他分支的内容来获得满足感"。

这是典型的"设计者偏差"——设计者知道全貌，所以感受不到玩家的单一路径体验。

### 修复建议

**方案A: 编剧Agent System Prompt增强（推荐）**

在编剧Agent的System Prompt中添加分支叙事闭合规则：

```
分支叙事闭合规则:
- 每个分支路径必须自成完整的"起承转合"弧线
- 如果分支导致某个NPC死亡，该NPC的秘密必须在死亡前或通过其他方式（日记、遗言、其他NPC的揭示）得到回应
- 每个ending必须回应冒险中揭示的至少1个核心秘密
- 禁止"悬而未决"的分支——如果A路径选择偷窃，B路径选择谈判，两条路径都必须有独立的叙事收束，不要依赖"玩家在另一条路径会知道"来补全信息
```

**方案B: 蓝图验证中检测叙事闭合度（辅助）**

在业务逻辑验证（§2.4）中添加叙事闭合检测规则：
- 检查每个ending的description是否提及了冒险中的关键主题（从central_conflict和theme_tags提取关键词）
- 检查是否有NPC的secret在玩家选择的分支中永远无法被揭示
- 检查epilogue节点是否出现在每条可能路径的末端

这不需要LLM——可以通过关键词匹配实现。

---

## 发现4: MAJOR — 非关键NPC无对话系统，与"活的世界"幻想矛盾

**严重性**: 🟠 MAJOR  
**影响的叙事承诺**: "不是在消费预设内容，而是在经历自己的故事"  
**关联GDD章节**: §3.1.4 key_npcs（minItems: 1, maxItems: 12）、§4.5 对话节点配置、§4.7 商人节点

### 问题描述

GDD定义了两类NPC：

| 类别 | 定义源 | 数量 | 拥有的叙事数据 |
|------|--------|:---:|---------------|
| 关键NPC (key_npcs) | 编剧Agent生成 | 1-12个 | ✅ 完整的 personality_tags, motivation, secret, voice_style, appearance, dialogue_tree_id |
| 非关键NPC | **无定义** | 0个 | ❌ 无任何叙事数据 |

但是，下列游戏场景**必然**需要非关键NPC的对话能力：

| 场景 | 需要的NPC类型 | 当前处理 | 问题 |
|------|-------------|----------|------|
| 商人节点（§4.7） | 商人NPC，出售物品 | 只生成库存数据，无NPC人格 | 玩家点击的是"商店UI"，不是"一个活生生的商人" |
| 战斗胜利后 | 投降的敌人想谈判/求饶 | 无系统 | 敌人只有stat_block，没有"求饶对话" |
| 探索节点发现幸存者 | 被困的NPC、考古队幸存者 | 环境叙事文本（§4.6），无NPC | 玩家看到"一具尸体"而不是"可以交谈的濒死NPC" |
| 休息节点（§4.8） | 路过的旅人、营地幽灵 | camp_events可能触发NPC | camp_events未定义具体的NPC对话 |
| 玩家试图与任何非key_npcs互动 | — | _generate_default_dialogue_config() 或返回空 | 玩家问："你是谁？"→ NPC回答："..."（沉默）|

§4.5中的fallback路径是`_generate_default_dialogue_config(node_data)`——这个函数未在文档中定义，但根据上下文，它可能返回一个只有通用问候语的NPC。**一个没有personality_tags、没有motivation、没有secret的NPC——是一个"假NPC"**。

### 叙事后果

考虑以下游戏流程：
1. 玩家在combat节点击败了一群哥布林
2. 最后一个哥布林投降，玩家想审问他
3. 程序检测到"敌人投降"事件，但没有NPC配置可以用于对话
4. 系统要么忽略玩家的审问意图（返回"这个哥布林不会说话"），要么调用DM Agent生成对话——但DM Agent没有这个哥布林的人格数据

此时，玩家会感受到世界不是"活的"——哥布林只是一个带着stat_block的战斗棋子，不是一个有恐惧、有动机、可能在谈判中背叛你的生物。

这与游戏的核心幻想（§1A: "这个NPC不只是帮助玩家的工具人——他有自己的动机和秘密"）直接矛盾。如果只有12个key_npcs才有"灵魂"，那么世界中的其他角色——商人、敌人、路人——都是没有灵魂的背景板。

### 根因

系统设计以"编剧Agent生成NPC"为唯一NPC来源。但编剧Agent只生成对剧情有关键影响的NPC。所有其他NPC——它们在叙事中可能是次要的，但对"世界的鲜活感"至关重要——被完全遗漏了。

这是一个系统边界问题：**NPC深度 ≠ 编剧Agent的key_npcs范围**。key_npcs是"剧情NPC"，而游戏还需要"世界NPC"和"临时NPC"。

### 修复建议

**方案A: 三层NPC系统（推荐）**

```
第一层：关键NPC（编剧Agent生成）
  - 数量: 1-12（当前key_npcs）
  - 数据: 完整的personality, motivation, secret, voice_style
  - 用途: 剧情推进

第二层：节点NPC（程序化生成 + 模板）
  - 数量: 每个需要NPC的节点1-3个
  - 数据: 程序的从NPC模板库中选择role + personality，附加当前节点的theme_tags
  - 用途: 商人、可选的支线对话、环境叙事传递者

第三层：临时NPC（DM Agent即兴生成）
  - 数量: 按需生成（投降的敌人、路过的旅人）
  - 数据: 程序的生成 {race, role, current_attitude} → DM Agent接收后即兴生成人格
  - 用途: 战斗后互动、意外遭遇
```

**方案B: NPC模板库 + 运行时实例化**

建立一个NPC模板库（20-30个通用模板，按role分类）：
```json
{
  "template_id": "merchant_generic_01",
  "role": "merchant",
  "personality_pool": ["精明", "健谈", "谨慎"],
  "voice_style_options": ["快语速，喜欢使用商业比喻", "慢条斯理，每句话都像在谈判"],
  "generic_motivations": ["利润", "安全", "信息"]
}
```

当程序化实例化给商人节点分配NPC时：
1. 如果蓝图中有key_npc的role是merchant → 使用key_npc
2. 如果没有 → 从模板库中选择匹配theme_tags的商人模板 + 随机化personality

**方案C: 投降/俘虏对话的最小可行系统**

为"战斗胜利后敌人投降"添加一个最小对话系统：
- 程序检测敌人HP降至0但选择非致命伤害 → 触发"captured"状态
- 为captured状态的敌人生成最小NPC数据：{name: "哥布林俘虏", role: "captive", attitude: "fearful", personality: 随机从["懦弱", "愤怒", "求饶"]中选择}
- DM Agent使用这个最小数据生成对话

---

## 发现5: MAJOR — "伤疤风险"分支类型被声明但完全未定义

**严重性**: 🟠 MAJOR  
**影响的叙事承诺**: "选择真的有后果"  
**关联GDD章节**: §5.2 分支类型表、§5.4 失败技能检定处理
**关联依赖**: `08-failure-growth.md` §4、`01-character-system.md` §7

### 问题描述

§5.2 分支类型表中列出了7种consequence_type：

| 分支类型 | consequence_type | 效果 | 示例 |
|----------|-----------------|------|------|
| 故事分支 | story | 改变叙事走向 | 选择帮助或背叛NPC |
| 战斗分支 | combat | 触发不同遭遇 | 正面交锋或偷袭 |
| 路径分支 | branch | 导向不同节点 | 左边或右边的路 |
| 技能分支 | skill_check | 检定解锁选项 | DC 15说服 |
| 物品分支 | item | 消耗/使用物品 | 使用钥匙开门 |
| 关系分支 | relationship | 基于关系值 | 关系≥5时NPC帮助 |
| **伤疤风险** | **scar_risk** | **高风险高回报** | **冒险使用禁忌魔法** |

`scar_risk` 是7种分支类型之一,被列入正式的分支类型表。但在整个GDD中：
- ❌ §5.3 选择解析流程 — 没有scar_risk的解析逻辑
- ❌ §5.4 失败处理 — 没有scar_risk的失败策略
- ❌ §5.5 持久选择追踪 — 没有scar_risk的特殊追踪
- ❌ §3.1.4 蓝图Schema — choices的consequence_type枚举中包含了scar_risk，但没有定义其专属字段
- ❌ §11.3 伤疤系统 — 委托给failure-growth.md，但没有任何"scar_risk选择如何连接到伤疤系统"的定义

### 叙事后果

`scar_risk` 是最有叙事潜力的分支类型。考虑以下场景：

*玩家面对一个濒死的古代石魔像（Boss）。它发出了最后一个咒语——"凡人之躯，承受我的诅咒！"*

*选择A：躲避咒语（DC 16敏捷豁免）——安全，但石魔像的遗骸会崩塌，无法回收古代核心。*
*选择B：承受诅咒，强行回收核心（scar_risk）——获得传说级物品，但角色获得永久伤疤。*

这是一个经典的DND叙事时刻——高风险高回报的抉择。但当前系统无法处理它：

1. `scar_risk`的consequence_type被触发后，程序不知道接下来该做什么
2. 没有字段定义"scar的具体内容"——是失去一只眼睛？还是被亡灵能量腐蚀？
3. 没有字段定义"risk是什么" vs "reward是什么"——选择succeed和fail的后果各是什么？
4. 在蓝图中，编剧Agent如何描述scar_risk选择的后果？Schema中choices的story_consequence字段太泛化

### 根因

`scar_risk`被当作一个类型标签放入枚举中，但类型标签需要配套的解析逻辑和数据结构。从设计模式来看，`consequence_type: "scar_risk"` 应该触发一条独特的分支——它不是简单的节点跳转（branch），也不是检定（skill_check），而是一个"高风险赌注"——成功获得巨大奖励，失败获得永久惩罚。

当前的分支解析算法（§5.3）只处理了`skill_check`、`item_requirement`、`relationship_threshold`三种条件。scar_risk需要的是一种不同的算法——"概率性结果分支"。

### 修复建议

**方案A: 在choice Schema中为scar_risk添加专属字段（推荐）**

```json
{
  "choice_id": "absorb_curse",
  "flavor_text": "承受诅咒，强行回收核心",
  "consequence_type": "scar_risk",
  "scar_risk_config": {
    "risk_type": "curse_absorption",
    "success_condition": "DC 17 constitution save",
    "on_success": {
      "reward": "获得古代魔像核心（传说级物品）",
      "narrative": "你咬紧牙关，诅咒的能量在你体内肆虐——但你扛住了。核心在你手中脉动。",
      "scar_saved": false
    },
    "on_failure": {
      "penalty": "获得永久伤疤：石化之触（左臂逐渐石化）",
      "narrative": "诅咒的能量超出你的承受。你的左臂传来刺骨的寒意——皮肤开始变成灰色的石头。",
      "scar_saved": true,
      "scar_data": {
        "scar_id": "stone_touch",
        "mechanical_effect": "-2 DEX, 无法使用双手武器",
        "narrative_description": "左臂从指尖到手肘已部分石化，触感迟钝但异常坚硬"
      }
    }
  }
}
```

**方案B: 在§5.3选择解析流程中添加scar_risk处理分支**

在选择解析算法中，在Step 1（检查前置条件）之后添加：

```
Step 1.5: 处理scar_risk类型
  if choice.consequence_type == "scar_risk":
    roll = d20 + get_relevant_save_modifier(actor, choice.scar_risk_config)
    if roll >= choice.scar_risk_config.success_condition.dc:
      narrative_outcome = choice.scar_risk_config.on_success
      apply_reward(narrative_outcome.reward)
    else:
      narrative_outcome = choice.scar_risk_config.on_failure
      apply_scar(narrative_outcome.scar_data)
    // 通知DM Agent生成对应的叙事文本
    emit_narrative_event(narrative_outcome.narrative)
```

---

## 发现6: MAJOR — 离线模板的"独特性"承诺在20个模板下无法兑现

**严重性**: 🟠 MAJOR  
**影响的叙事承诺**: "这个故事是我的"、离线也能玩但保留核心体验  
**关联GDD章节**: §13 离线冒险模板、§1A 玩家体验幻想  
**关联已有评审**: llm-integration-narrative-review 批判#5（模板质量声明矛盾）

### 问题描述

§1A定义的核心幻想：
> "每次冒险由LLM编剧Agent基于我的队伍、世界状态、主题生成独一无二的剧本。不存在'重复刷同一个副本'。"

§13定义的离线体验：
> "与LLM生成的区别: 叙事多样性=固定文本, NPC性格=预设对话, 分支选择=固定选项, 玩家体验='可重复的'"

§13.2定义的模板数量：
- 短冒险：20个模板（覆盖6个主题类别）
- 中冒险：14个模板
- 长冒险：5个模板

现在做一个简单的计算：一个核心玩家每周玩3次短冒险。在离线模式下，20个模板 ÷ 3次/周 = 6.7周就会耗尽所有短冒险模板。之后，玩家开始重复体验——而且是**完全相同的叙事文本**（"叙事多样性=固定文本"）。

更关键的是：§13.4的对比表坦率承认了差距——"叙事多样性: LLM=每次不同, 离线=固定文本"。但游戏在离线开始时没有任何机制告知玩家："你现在进入了模板模式，故事文本是固定的。"

### 叙事后果

以下是在离线模式下玩家可能经历的体验退化：

| 冒险# | 玩家的期待 | 实际体验 | 退化程度 |
|:---:|-----------|---------|:---:|
| 1-3 | 新鲜故事 | 新鲜故事（不同模板） | 无退化 |
| 4-6 | 新鲜故事 | 新鲜故事（不同模板） | 无退化 |
| 7-9 | 新鲜故事 | 可能重复但模板不同所以还行 | 轻微 |
| 10-12 | 新鲜故事 | 开始明显重复——"这个地牢我好像来过" | 中等 |
| 15+ | 新鲜故事 | 完全重复——NPC台词一模一样 | **严重——"这个故事是我的"幻想完全破裂** |

更糟糕的是，如果玩家在模板模式下获得了好的战利品/经验，他们可能选择继续在模板模式下玩——**这创造了一个负反馈循环**：玩家机械性地重复刷模板以获取奖励，而不是沉浸在故事中。这与Pillar 1（反"刷关打宝"）直接矛盾。

当前设计中，没有任何叙事降级机制可以使模板体验更具动态性：
- ❌ 无文本变量替换（"山洞深处传来{random_sound}的声音"）
- ❌ 无NPC名称随机化
- ❌ 无分支节点顺序随机化
- ❌ 无战利品叙事随机化

### 根因

离线模板被设计为LLM蓝图的"静态等价物"——同样的JSON结构，但内容是固定的。这是一个合理的工程决策（确保离线模式的程序化实例化路径与LLM路径完全相同）。但它忽略了叙事层面的差异：LLM可以用`"description": "根据给定队伍和世界状态生成"`，而模板只能用`"description": "固定字符串"`。

20个固定模板对于功能性测试来说是足够的，但对于一个承诺"每次冒险都是独一无二"的游戏来说远远不够。

### 修复建议

**方案A: 文本模板变量替换系统（推荐，低成本高收益）**

为离线模板的description和dialogue字段引入变量占位符，在实例化时填充：

```
模板文本: "你来到了{location_adjective}的{location_type}。空气中弥漫着{scent}的气味。{npc_name}，一个{npc_appearance}的{npc_race}，正在{action}。"

运行时填充: "你来到了幽暗的洞窟。空气中弥漫着霉腐的气味。卡尔多，一个独眼的人类，正在翻找他的背包。"
```

变量池从模板的theme_tags和party_state中提取：
- `{location_adjective}`: 从theme对应的形容词池中随机选择（每个theme 10-15个形容词）
- `{npc_name}`: 从NPC名称池中随机选择（按race分类，每个30-50个名称）
- `{scent}`: 从环境气味池中随机选择

这样即使模板是固定的，文本组合有100+种变体。

**方案B: 模板反重复机制**

维护一个"最近使用的模板"队列（最近5个），在模板选择算法（§13.3）中添加规则：
- Step 4（随机选择前3个中的1个）改为"随机选择前3个中最近未使用过的"
- 如果所有前3个都使用过 → 扩大到前5个
- 如果所有匹配tier的模板都使用过 → 重置队列 + 使用文本变量替换（方案A）增加变体感

**方案C: 离线模式透明化**

在UI中明确标注模板模式：
- 冒险开始前："当前为离线模式。冒险使用叙事模板——故事文本是预设的，但所有游戏机制（战斗、战利品、分支选择）完全保留。"
- 如果玩家完成了所有模板："你已经完成所有可用的离线冒险模板。新的模板将在下次在线同步时解锁。"

这管理了玩家预期，避免了"惊喜破裂"。

---

## 发现7: MINOR — "皮肤层"原则在NPC叙事行为上的边界模糊

**严重性**: 🟡 MINOR  
**影响的叙事承诺**: 原则声明的一致性  
**关联GDD章节**: §1.2 LLM=皮肤层 原则、§9.3 叙事与程序结果合并

### 问题描述

§1.2定义的核心原则：
> "LLM决定'What the NPC says'，不决定'What the NPC does'"

但在实际叙事设计中，这条边界是模糊的。考虑以下案例：

**案例A: NPC提供任务**
- NPC马库斯说："我需要你们去古庙找回我的货物。"
- 这是"says"（对话）还是"does"（任务分配）？
- 当前方案：编剧Agent在蓝图中定义了NPC马库斯的motivation和quest_giver角色。程序将NPC关联到节点。DM Agent在对话中生成任务描述文本。
- **判定**: 合理——程序控制了任务的存在（节点关联），LLM控制了任务的表述。

**案例B: NPC背叛队伍**
- 维尔特在ritual_chamber中说："我等这一刻已经太久了！"并攻击玩家。
- 这是"says"（背叛宣言）还是"does"（背叛行动）？
- 当前方案：编剧Agent定义了维尔特的role为"traitor"、secret为"主动打开封印"。程序在ritual_chamber节点触发combat。DM Agent在战斗前生成背叛宣言。
- **判定**: 模糊——LLM定义了"维尔特是叛徒"（story-level），程序执行了"战斗触发"（mechanical-level）。但"背叛"这一核心叙事行为是由LLM决定的（通过定义role和secret），不是程序。

**案例C: NPC秘密揭示**
- 玩家在side_library读日记发现"维尔特主动打开了封印"。
- 这是叙事信息——"主动打开封印"这个真相是LLM（编剧Agent）创造的。
- 程序层只负责"日记在side_library节点可交互"和"揭示后的flag设置"。
- **判定**: LLM决定了秘密的内容和性质——这是核心叙事决策。

### 分析

"LLM决定says，程序决定does"这个二元分法在以下场景中适用：
- ✅ 造成8点伤害（程序）vs "战锤砸在石棺上，碎石飞溅"（LLM）
- ✅ DC 15检定失败（程序）vs "你的手指滑了一下，撬锁工具掉在地上"（LLM）
- ✅ 获得护符（程序）vs "护符在你手中微微发光，你感到一股暖意"（LLM）

但在以下场景中不适用：
- ❌ "维尔特是叛徒"——这既是故事事件也是角色行为
- ❌ "马库斯隐瞒了古庙的危险"——这既是秘密也是行为（欺骗）
- ❌ NPC之间的关系（friendly/hostile）——这既是角色状态也是叙事行为

实际上，GDD的实践已经超越了它声明的原则。编剧Agent在蓝图中填入了大量叙事决策——NPC的motivation、secret、role、故事twist、结局条件。这些都是"叙事行为"，不是"皮肤文本"。**原则声明与实际操作之间存在gap——这个gap不是bug，是feature**（因为纯皮肤层的DM Agent无法驱动故事），但声明不准确。

### 修复建议

**方案A: 精炼原则声明（推荐）**

将§1.2的原则从二元分法改为三层分法：

```
叙事决策层（LLM Screenwriter Agent）:
  · 故事结构（三幕/结局）
  · NPC的角色、动机、秘密
  · 核心冲突和twist
  · 选择的分支路径

机械执行层（程序）:
  · 数值计算（伤害、DC、CR）
  · 规则判定（检定成功/失败）
  · 状态变更（HP、法术位、物品）
  · 分支条件评估

叙事表达层（LLM DM Agent + 文案Agent）:
  · 场景描写、对话生成、动作叙述
  · 在不改变叙事决策的前提下渲染氛围和情感
```

这更准确地反映了实际的职责划分。

---

## 发现8: MINOR — choice_presentation响应延迟时的叙事体验降级未被定义

**严重性**: 🟡 MINOR  
**影响的叙事承诺**: "命运的骰子在转动"（流畅的叙事体验）  
**关联GDD章节**: §9.1 DM Agent调用时机

### 问题描述

§9.1定义choice_presentation为异步调用，响应时间要求<2秒。调用的"阻塞模式"为"异步(先显示选项)"——意味着UI先显示选项按钮，叙事文本随后叠加。

但choice_presentation的DM Agent调用生成的是什么？DM Agent在有choice_presentation请求时，生成的是选择点的**叙事包装**——DM用文字描述"你站在岔路口，两条路都消失在黑暗中。左路传来低沉的嗡鸣，右路则异常安静。"这种叙事包装增加了选择的重量和氛围。

问题是：
1. 如果LLM在2秒内返回 → 完美体验：叙事文本和选项同时出现
2. 如果LLM在2-5秒返回 → 降级体验：选项先出现1-3秒，叙事文本闪烁出现——打断沉浸感
3. 如果LLM超时（>8秒）→ 选项出现但叙事文本使用fallback模板——"你面临一个选择。"

对于3，fallback模板（§9.4）只定义了scene_atmosphere、combat_narration、skill_check_result的fallback。**没有定义choice_presentation的fallback模板**。当前fallback_templates（§9.4）只有4个key，choice_presentation不在其中。

### 叙事后果

当choice_presentation的LLM调用完全失败时（网络断开、超时），UI上只显示选项按钮，没有任何叙事文本。玩家看到的可能是一个突兀的选项列表：
```
[打断仪式]  [让仪式完成]
```
没有任何上下文包装。

这比fallback的skill_check_result模板（`"{character}成功完成了检定。"`）更糟糕——至少检定结果有一个通用模板。选择呈现完全没有fallback。

### 修复建议

**方案A: 为choice_presentation添加fallback模板（推荐）**

在§9.4的fallback_templates中添加：
```json
{
  "choice_presentation": {
    "default": "你面临一个抉择。",
    "branch": "前方的路分出了几条岔路。每条路都通向未知。",
    "combat_choice": "战斗的局势瞬息万变。你必须在转瞬之间做出决定。",
    "dialogue_choice": "对方等待着你的回应。空气似乎凝固了。",
    "moral_choice": "这是一个没有简单答案的问题。你的选择将定义你是谁。"
  }
}
```

**方案B: 使用蓝色节点预生成（推荐配合）**

在实例化阶段（第二层），程序可以检测哪些节点有choices数组。对于有choices的节点，程序可以预先生成一个简单的选择包装文本（不需要LLM），以便在DM Agent响应延迟时立即显示：

```
如果节点有choices且choice_presentation模板存在:
  UI先显示 [模板化的选择包装文本] + [选项按钮]
当DM Agent响应到达时:
  UI平滑替换包装文本（不闪烁）
```

---

## 发现9: MINOR — 长冒险多结局的"意义差异"验证缺失

**严重性**: 🟡 MINOR  
**影响的叙事承诺**: "选择真的有后果"（多个结局需要真正不同）  
**关联GDD章节**: §3.3 长冒险模板（multiple_endings: true, ending_count_range: [2, 4]）、§3.1.4 蓝图Schema末尾的endings字段

### 问题描述

§3.3的长冒险模板承诺了2-4个结局。§3.1.4的Schema定义了endings数组（minItems: 1, maxItems: 4），每个ending有type（victory/partial_victory/retreat/defeat/secret）、title、description、conditions、rewards、penalties。

但**没有验证机制确保2-4个结局是"有意义地不同"**。一个LLM可能生成：

```json
{
  "endings": [
    {
      "ending_id": "victory_good",
      "type": "victory",
      "title": "英雄凯旋",
      "description": "你们击败了Boss，拯救了村庄。",
      "conditions": ["击败Boss"],
      "rewards": {"xp_bonus": 1000, "gold_bonus": 500}
    },
    {
      "ending_id": "victory_better",
      "type": "victory",
      "title": "传说英雄",
      "description": "你们击败了Boss，拯救了村庄，村民为你们立了雕像。",
      "conditions": ["击败Boss", "救出所有人质"],
      "rewards": {"xp_bonus": 1200, "gold_bonus": 600}  // 仅数值不同
    }
  ]
}
```

这两个结局在叙事层面**本质上是相同的**——都是"击败Boss→拯救村庄"。唯一的区别是XP和金币数值。从叙事角度看，玩家得到了一个"更好的战利品"结局，而不是一个"不同的故事"结局。

更差的情况——LLM可能生成"调色板交换"式结局：
- ending_1: "你击败了火之Boss"（rewards包含火系物品）
- ending_2: "你击败了冰之Boss"（rewards包含冰系物品）
- 叙事结构完全相同，只是换了元素主题

### 叙事后果

当玩家投入6小时完成一次长冒险后，他们希望结局**回答不同的问题**或**呈现不同的主题收束**：

| 有意义的结局差异 | 调色板交换式差异 |
|-----------------|----------------|
| "你选择宽恕Boss，Boss成为盟友" vs "你处决了Boss，但发现了他的动机" | 两个结局都是"击败Boss"，只是掉落物品不同 |
| "你破坏了神器，世界回归正常" vs "你保留了神器，获得了力量但改变了世界" | 两个结局都是"世界恢复正常"，只是描述略有不同 |
| "你背叛了队伍，独自获得力量" vs "你牺牲自己，拯救队伍" | 两个结局都是"队伍胜利"，只是谁的奖励多一点 |

当前的Schema和验证系统无法区分这两种情况。ending的conditions是字符串列表（`"conditions": ["击败Boss", "救出所有人质"]`），程序只能检查字符串匹配——无法判断"击败Boss+救出人质"的结局与"击败Boss"的结局是否**在叙事层面有本质不同**。

### 根因

LLM理解"意义"但程序不验证"意义"。Schema验证只检查结构（type是有效枚举、rewards有xp_bonus等），不检查语义。这是一个经典的"可量化验证"vs"不可量化验证"的问题——与llm-integration-narrative-review批判#3（缺乏叙事质量验证Agent）同根。

### 修复建议

**方案A: 编剧Agent System Prompt中添加结局多样性要求（推荐）**

```
结局多样性规则:
- 每个结局必须对应一个不同的核心叙事问题
  例: "力量值得付出什么代价？" vs "信任他人是否值得？" vs "秩序与自由哪个更重要？"
- 每个结局必须有一个不同于其他结局的narrative_resolution类型
  可选类型: transformation(角色改变), sacrifice(牺牲), revelation(真相揭示), restoration(恢复), escalation(冲突升级)
- 禁止仅通过数值差异(多500 XP, 多一件物品)来区分结局
- 如果无法提供3个有意义差异的结局，只提供2个——不要为了凑数而生成调色板交换
```

**方案B: 程序验证中检测结局语义重叠（辅助）**

在蓝图验证中添加一个启发式检查：
1. 提取每个ending的description和title
2. 计算ending间关键词重叠率（Jaccard相似度）
3. 如果两个ending的description关键词重叠率 > 70% → 警告"结局可能缺乏足够差异"
4. 如果所有ending的type相同（全是victory） → 警告"结局类型单调"

---

## 补充发现

### 补充A: NPC对话配置中voice_style字段直接传递给DM Agent但无使用指导

**严重性**: 🟡 MINOR

§4.5的NPC对话配置包含`voice_style`字段（来自蓝图key_npcs），但§9.2的DM Agent上下文注入策略中，没有说明voice_style如何被注入和使用。DM Agent的System Prompt（llm-integration §3.2.2）说"对话描写体现NPC的性格和情绪"，但没有提到voice_style。结果是：编剧Agent精心定义了维尔特的voice_style为"文雅但有轻微的狂躁，经常自问自答"，但DM Agent可能永远不会看到这个信息。

**修复**: 在DM Agent的NPC上下文字段中明确注入voice_style，并在System Prompt中说明如何使用它。

### 补充B: §5.1选择数据模型中的consequences.downstream字段定义过简

**严重性**: 🟡 MINOR

§5.1的选择数据模型中，downstream consequences定义为：
```json
"downstream": {
  "affects_node": "elf_shrine",
  "modifies_encounter": "reduce_difficulty_by_1"
}
```

这是一个过于简化的下游后果模型。在叙事层面，"在选择A中帮助了NPC X"会导致"在节点Y中NPC X出现帮助玩家"——这需要的不仅是difficulty变化，还需要一个叙事结果（NPC的出现、对话、行为）。当前模型只有一个`modifies_encounter`字段，不足以表达"NPC X在后续节点中出现并帮助玩家"这种叙事回调。

**修复**: 扩展downstream consequences模型，添加narrative_callbacks字段：
```json
"downstream": {
  "narrative_callbacks": [
    {
      "trigger_node": "elf_shrine",
      "callback_type": "npc_appears_to_help",
      "npc_id": "npc_elara",
      "description": "艾拉拉出现在神殿中，感谢你之前的帮助，并提供一盏魔法灯笼"
    }
  ]
}
```

---

## 总结与优先级排序

```
修复优先级:
  P0 (BLOCKER — 叙事支柱破裂，MVP前必须修复):
    1. DM Agent缺乏完整叙事弧线知识 (#1)
       → 增强上下文注入，从蓝图中提取叙事骨架传递给DM Agent
    2. 动作历史压缩导致关键叙事信息永久丢失 (#2)
       → 分层动作保留 + 叙事里程碑系统

  P1 (MAJOR — 影响叙事体验质量，MVP中期修复):
    3. 分支路径的叙事闭合无设计保障 (#3)
       → 编剧Agent System Prompt增强 + 蓝图叙事闭合验证
    4. 非关键NPC无对话系统 (#4)
       → 三层NPC系统：关键NPC（LLM）+ 节点NPC（模板）+ 临时NPC（DM即兴）
    5. "伤疤风险"分支类型完全未定义 (#5)
       → 在choice Schema中添加scar_risk_config + 解析算法中添加处理分支
    6. 离线模板的"独特性"承诺不可兑现 (#6)
       → 文本变量替换 + 反重复机制 + 离线模式透明化

  P2 (MINOR — 增强叙事质量，后续迭代优化):
    7. "皮肤层"原则声明与实际操作不一致 (#7)
       → 精炼为三层分法
    8. choice_presentation fallback未定义 (#8)
       → 添加fallback模板
    9. 长冒险多结局意义差异验证缺失 (#9)
       → System Prompt增强 + 启发式重叠检测
    补充A. voice_style传递断裂
    补充B. downstream consequences模型过简
```

**如果只能修两件事**:
1. **修复#1**（DM Agent获得完整叙事弧线知识）——这解决了80%的"DM Agent像盲人"问题。不需要改变LLM调用参数，只需要改变上下文构建逻辑。
2. **修复#2**（分层动作保留）——这解决了"故事失忆症"。不需要更多token，只需要更智能地管理token。

这两项修复都是**上下文构建层的改动**，不涉及LLM调用参数调整，不改变管线架构，不增加API成本。但它们对叙事连贯性的改善是最直接的。

---

> **评审判定**: CONCERNS — 三层管线架构在技术层面设计优秀，但管线之间的叙事信息传递存在两个BLOCKER级断裂：DM Agent看不见编剧Agent生成的完整剧本（#1），以及动作历史的纯字符串截断压缩导致叙事失忆（#2）。这两个问题使精心设计的剧本在实时游戏中无法被忠实呈现。幸运的是，这两个问题都可以在不改变管线架构、不增加API成本的前提下，通过优化上下文构建逻辑来修复。

> **交叉参考**: 本评审与 `llm-integration-narrative-review-2026-05-10.md` 的三个CRITICAL发现形成互补：那个评审关注LLM Gateway层的DM Agent能力限制（输入/输出上限、角色数据缺失、创作约束），本评审关注冒险生成层的叙事上下文管理（蓝图→DM Agent信息传递、动作历史压缩、分支叙事闭合）。两个评审合在一起，完整描述了从"剧本创作"到"实时叙述"整个管线中的叙事质量风险。
