# Game Concept: 《酒馆与命运》

*Created: 2026-05-07*
*Status: Approved*
*Source: Extracted from GDD-v1.md, design/pillars.md, start.md*

---

## Elevator Pitch

> 在一间神秘的酒馆中招募冒险者、培养感情、打造装备，然后踏入由AI编织的命运之路——每一场冒险都是一段独一无二的故事，而每一次失败都不会被遗忘。

> **Tavern & Destiny** — A Roguelike DND Adventure Game where you run a tavern, recruit adventurers, and embark on AI-narrated adventures where every choice matters and every death leaves a scar.

---

## Core Identity

| Aspect | Detail |
| ---- | ---- |
| **Genre** | Roguelike × DND RPG（回合制策略 + AI动态叙事） |
| **Platform** | PC（MonoGame / .NET 8） |
| **Target Audience** | DND 爱好者、策略RPG玩家、叙事驱动游戏玩家（Mid-core to Hardcore） |
| **Player Count** | 单人 |
| **Session Length** | 短冒险 ~30分钟 / 中冒险 ~3小时 / 长冒险 ~6小时 |
| **Monetization** | Premium（买断制） |
| **Estimated Scope** | Large（9+ 个月，48周路线图） |
| **Comparable Titles** | Darkest Dungeon（酒馆管理+永久风险）、Slay the Spire（分支路线+程序化地图）、Baldur's Gate 3（叙事深度+角色关系） |

---

## Core Fantasy

玩家经营一间神秘酒馆，招募各具个性的冒险者，组建四人队伍踏入由大模型实时生成的冒险。每次冒险都是独一无二的故事——角色有记忆、有性格、有情感关系；战斗是原汁原味的DND 5e回合制；失败不会重来，而是留下永久的伤疤、传承和世界演进。

**核心情感承诺**：
- **"我的冒险故事是我的"** — AI不是替代设计，而是服务于叙事的"变化引擎"。每个决策产生真实的叙事后果，每个角色有自己的性格和关系网。
- **"我的战士真的会死"** — 永久死亡、伤疤系统让每个战术决策有重量。这不是可以"重开一局"的普通Roguelike。
- **"酒馆里的这些人我舍不得"** — 角色不是数值容器，而是一个个有故事的人。酒馆从破旧小屋成长到金碧辉煌的过程，是你与这些角色共同书写的。

---

## Unique Hook

> "It's like Darkest Dungeon meets Baldur's Gate 3, AND ALSO every adventure is uniquely AI-narrated with persistent world consequences — no two players will ever have the same story."

核心差异化：LLM不是替代设计师，而是服务于"变化"——程序化生成有因果关系的叙事，而非随机拼凑。离线模式存在但为降级体验。

---

## Visual Identity Anchor

> **SFC黄金时代像素风格 × 暗黑奇幻酒馆氛围 × DND经典设定**

- **美术风格**：SFC时代像素艺术（参考FF6、Chrono Trigger），手工像素感与程序化生成配合
- **色调基调**：酒馆暖色调（烛光、木质感）vs. 冒险冷色调（地牢、荒野），形成"家"与"旅途"的视觉对比
- **角色设计**：像素角色清晰可辨，夸张的头身比和色彩区分种族/职业，类似FF6的spirte设计语言
- **UI风格**：仿羊皮纸质感 + 中世纪手稿装饰元素，与像素游戏画面形成层次对比

---

## Player Experience Analysis (MDA Framework)

### Target Aesthetics (What the player FEELS)

| Aesthetic | Priority | How We Deliver It |
| ---- | ---- | ---- |
| **Narrative**（叙事） | 1 (Primary) | LLM四Agent管线生成独立冒险故事；角色有记忆/性格/关系；玩家选择驱动剧情 |
| **Challenge**（挑战） | 2 | DND 5e SRD完整规则体系；回合制战术战斗；永久后果 |
| **Fantasy**（幻想） | 3 | DND世界观沉浸；扮演酒馆老板+冒险队长的双重身份 |
| **Discovery**（探索） | 4 | 程序化生成的地图、事件和秘密；分支路线选择 |
| **Expression**（表达） | 5 | 角色构筑深度（职业/专长/法术）；队伍组建策略 |
| **Sensation**（感官） | 6 | SFC像素美术风格；中世纪氛围音乐/音效 |
| **Submission**（休闲） | 7 | 酒馆休整阶段提供舒缓节奏 |
| **Fellowship**（社交） | N/A | 纯单人游戏 |

### Core Mechanics

1. **DND 5e回合制战斗** — FSM驱动的战术战斗（14种条件、地形效果、视野/光源规则），与标准5e有6项节奏优化偏差
2. **AI程序化冒险叙事** — LLM四Agent管线（DM/World/Character/Item）生成剧情大纲→实例化→实时叙事，Schema验证保证质量
3. **酒馆元游戏系统** — 角色招募/培养/关系管理，酒馆Lv1-10升级解锁新设施，英雄之壁记录传奇
4. **永久后果与传承** — 角色可永久死亡，伤疤系统记录惩罚，知识和传承延续到后续冒险
5. **三层冒险生成管线** — 蓝图（LLM生成故事骨架）→ 实例化（程序化填充地图/敌人）→ 实时叙事（LLM响应玩家行动）

---

## Game Pillars

> **核心体验等式**: 叙事沉浸 ≈ 策略构筑 ≫ 随机变化
> 来源: `design/pillars.md`（经 `/review-all-gdds` 评审通过）

### Pillar 1: 角色驱动的故事 (Character-Driven Stories)

每次冒险产生独特叙事；角色有记忆、性格和关系；玩家的选择和后果驱动故事走向，而非预设剧本。

*Design test*: 当"增加一个刷怪房间"和"增加一个NPC对话事件"冲突时，选择NPC对话——因为叙事优先于战斗密度。

*Visual corollary*: 角色的视觉设计必须传达个性和背景故事——不仅仅是"一个精灵弓箭手"，而是"一个失去家人的精灵弓箭手"——通过像素动画中的姿态、表情和物品配件来体现。

### Pillar 2: 战术深度——原汁原味DND (Tactical DND Depth)

完整保留DND 5e SRD规则体系，提供深度角色构筑和策略空间；战斗决策有真实后果；游戏难度通过规则深度而非数值膨胀实现。

*Design test*: 当我们考虑简化一个DND规则以加快节奏时，必须确保简化后的决策空间不低于原规则的80%——否则选择原规则。

*Visual corollary*: 战斗UI必须清晰呈现所有DND状态和数值——像素美学不能以牺牲战术信息可读性为代价。

### Pillar 3: 持续演进的世界 (Evolving Persistent World)

冒险的成败永久改变世界状态；酒馆是持续发展的家园而非静态菜单；角色可能永久死亡但知识和传承延续；每次冒险为下一次创造新的可能性。

*Design test*: 每次冒险结束时，世界状态或酒馆状态必须至少有1项可观察到的变化——如果没有变化，说明缺乏后果感。

*Visual corollary*: 酒馆环境的视觉升级必须清晰可辨——从破旧到大气的渐进变化是玩家进度感的视觉锚点。

### Pillar Priority: 角色驱动 > 战术深度 > 持续演进

### Anti-Pillars (What This Game Is NOT)

- **NOT 刷关打宝游戏** — 核心驱动是叙事和策略选择，非重复刷取装备/经验
- **NOT 纯随机Roguelike** — 随机性服务于叙事和战斗变化，非核心体验；程序化叙事保证每次冒险的独特性
- **NOT 预设内容消费** — LLM程序化生成叙事，无固定剧本；玩家创造故事，非消费故事
- **NOT 靠预写文本填充叙事的游戏** — 叙事体验的核心是LLM实时生成，非预存模板库

---

## Inspiration and References

| Reference | What We Take From It | What We Do Differently | Why It Matters |
| ---- | ---- | ---- | ---- |
| **Darkest Dungeon** | 酒馆管理+永久风险+角色情感绑定 | AI叙事替代预写文本；像素更偏向SFC RPG风格 | 酒馆作为"家"的元游戏模型 |
| **Baldur's Gate 3** | 选择有后果+角色关系+环境交互 | 像素2D而非3D；程序化叙事替代手工剧本 | 叙事深度标杆 |
| **Slay the Spire** | 分支路线+程序化地图骨架+单局策略 | DND规则替代卡牌；强制叙事节点而非纯战斗 | 已验证的roguelike循环结构 |
| **FF6 / Chrono Trigger** | SFC像素美术风格+角色sprite设计语言 | 现代像素渲染技术（动态光照、粒子效果） | 美术方向锚点 |
| **Divinity: Original Sin 2** | 场景交互+战斗创意+元素互动 | 简化为2D；DND规则替代DoS规则 | 策略自由度方向 |
| **DND 5e SRD** | 核心规则体系（属性/专长/法术/战斗） | 6项节奏优化偏差（重投先攻/重击最大化/死亡简化等） | 战术深度基础 |

**Non-game inspirations**: 中世纪酒馆文化（《魔戒》跃马客栈、《冰与火之歌》的旅馆场景）；DND跑团文化（地下城主即兴叙事的魔力）；日本SFC RPG黄金时代的怀旧美学。

---

## Target Player Profile

| Attribute | Detail |
| ---- | ---- |
| **Age range** | 18-40 |
| **Gaming experience** | Mid-core to Hardcore — 理解RPG系统和策略构筑 |
| **Time availability** | 短冒险适合工作日晚间；中长冒险适合周末 |
| **Platform preference** | PC（键鼠为主要输入） |
| **Current games they play** | Darkest Dungeon, Slay the Spire, Baldur's Gate 3, Divinity: Original Sin 2, DND桌游 |
| **What they're looking for** | 有重量感的战术决策 + 独一无二的叙事体验 + 角色情感绑定 |
| **What would turn them away** | 缺乏深度的战斗系统；机械重复的刷怪循环；AI生成的文本质量不稳定 |

---

## Technical Considerations

| Consideration | Assessment |
| ---- | ---- |
| **Engine** | MonoGame 3.8.5+ / .NET 8 / C# 12 |
| **Key Technical Challenges** | LLM Schema验证管线（JsonSchema.Net）；三层冒险生成管线；程序化地图生成（GoRogue）；Myra UI在像素分辨率下的可读性 |
| **Art Style** | SFC黄金时代像素艺术（16-bit），手工像素感 + 现代渲染技术 |
| **Art Pipeline Complexity** | Medium — 像素sprite + 程序化生成辅助（LLM头像生成→像素化处理） |
| **Audio Needs** | Moderate — 氛围BGM（酒馆/战斗/探索3套）+ DND主题SFX |
| **Networking** | None — 纯离线优先；LLM API为在线增强（离线有降级模板） |
| **Content Volume** | 程序化生成为主；手工内容为辅（核心系统+静态降级模板）；9个子系统覆盖 |
| **Procedural Systems** | 角色（LLM生成叙事层）、冒险（三层管线）、地图（GoRogue）、物品描述（LLM） |

---

## Risks and Open Questions

### Design Risks
- LLM生成质量不稳定性 → Schema验证 + 3次重试 + 静态模板降级
- 叙事体验与战术节奏的平衡 → 支柱优先级系统已建立（叙事>策略>随机）
- 永久后果导致的挫败感 → 伤疤/传承系统将失败转化为正面叙事资产

### Technical Risks
- LLM API延迟和费用 → 离线优先架构；本地缓存；Schema约束控制token消耗
- MonoGame像素渲染管线性能 → .NET 8 + 硬件加速；性能预算待配置
- 程序化叙事的一致性 → 世界状态hooks系统确保前后因果

### Scope Risks
- 48周路线图能否完成9个子系统 → 分阶段交付（Phase 0→P0→P1→P2→P3）
- LLM集成调试时间不可控 → 预留大量调试和降级测试时间

### Open Questions
- 像素美术资产如何与LLM生成的视觉描述衔接？
- 离线模式的叙事质量能达到什么程度？

---

## MVP Definition

**Core hypothesis**: 玩家会为一个提供"独特叙事体验 + 深度DND战术 + 永久后果"的Roguelike游戏投入30分钟到6小时的深度游玩时间。

**Required for MVP (P0)**:
1. 酒馆核心循环（招募→培养→组队→冒险→结算）
2. 基础DND战斗系统（属性/职业/回合制/骰子）
3. 一层冒险生成（简化管线：手工+参数化）
4. 离线可玩（无LLM依赖）

**Phase 0 (Complete)**: Core基础设施 — ServiceLocator, EventBus, Scene/Entity/Component, GameStateManager (11 C# files, 13 tests)

---

## Next Steps

- [x] Game concept approved (routed from GDD-v1.md + design/pillars.md)
- [x] Create game pillars document (`design/pillars.md` — completed via /review-all-gdds)
- [x] Engine selection (`/setup-engine` — MonoGame/.NET 8)
- [x] Decompose concept into systems (26 design docs in `docs/`)
- [ ] Create Art Bible — **current step** (`/art-bible`)
- [ ] Create master architecture document (`/create-architecture`)
- [ ] Begin Phase 1 implementation
