# 对抗性设计审查：经济、成长与资源平衡

> **审查人**: economy-designer（经济设计师）  
> **审查对象**: `design/gdd/08-failure-growth.md` v1.0（+ 交叉引用 `01-character-system.md`, `03-items-equipment.md`, `07-tavern-system.md`, `design/registry/entities.yaml`）  
> **审查日期**: 2026-05-10  
> **审查类型**: 对抗性审查（Adversarial Review）——目标是发现问题，而非验证正确性  
> **审查范围**: 金币经济、传承点经济、XP 成长节奏、通胀控制、跨文档一致性

---

## 执行摘要

本文档识别出 **6 个阻塞级 (BLOCKING) 问题** 和 **10 个建议级 (RECOMMENDED) 问题**。

最严重的问题包括：(1) 传承点公式在 `character-system` v1.2 和 `failure-growth` §7.6 之间的跨文档冲突（同一 Lv5 角色获得 10 HP vs 21 HP——相差 2.1 倍）；(2) 卖价文本（50%）与公式（×0.3）不一致；(3) 物品状态枚举在 `failure-growth` GDD 内部自相矛盾；以及 (4) 传承点消费表中存在多个陷阱选项和套利漏洞，可能使死亡成为权力玩家的最优策略。

---

## 阻塞级问题 (BLOCKING)

必须在任何开发工作开始前解决。

---

### [BLOCKING] 问题 1：传承点公式跨文档冲突（failure-growth §7.6 vs character-system §1.4）

**严重性**: 🔴 BLOCKING — 数值路径完全不同  
**标签**: `[economy-designer]` `[consistency]`

**发现**：
- `08-failure-growth.md` §7.6 定义：`heritage_points = floor(xp / 500) + points_from_level`
  - Lv5 角色，10,400 XP → `floor(10400/500) + 1 = 21 HP`
- `01-character-system.md` v1.2（2026-05-10 修订）定义：`heritage_points = level + 5`（线性公式）
  - Lv5 角色 → `5 + 5 = 10 HP`
  - 金币购买硬上限 2 次（最多 +100 GP）
- `entities.yaml` §heritage_points_formula 注释："已确认: 以 failure-growth §7.6 的 floor(xp/500) 为准, character-system 原写 floor(xp/1000) 已废弃。"——但 character-system v1.2 再次改变了公式，registry 尚未更新。
- character-system §1.4 调参表自身标记：`§8.3 传承点公式 | 08-failure-growth.md §7.6 | ⚠️ 待对齐`

**影响**：这是同一个角色的两种**完全不同的 HP 产出**。21 HP 可以买 4 个 minor feats（每个 5 HP）；10 HP 只能买 2 个。21 HP 足以让一个 Lv5 死亡变成套利机会；10 HP 则更接近"微薄遗物"的设计意图（character-system §1.4 调参表注："始终'微薄遗物'"）。

**推荐方案**：
- **选项 A（推荐）**：采用 `level + 5` 线性公式（character-system v1.2 版本），配合金币硬上限 2 次、feat 硬上限 1 次。更新 failure-growth §7.6 和 entities.yaml。
- **选项 B**：保留 `floor(xp/500)` 但大幅提高消费价格（feat → 15 HP，起始金币 → 3 HP/50gp）。
- **选项 C**：折中方案 `floor(xp/1000) + level`，约在两者之间。

---

### [BLOCKING] 问题 2：卖价文本与公式不一致（failure-growth §11.1）

**严重性**: 🔴 BLOCKING — 文本与公式矛盾  
**标签**: `[economy-designer]`

**发现**：
- `§11.1` 收入来源表，第 3 行："物品出售 | 基础价值 ×50%"
- `§11.1` 卖价公式：`sell_price = base_value × rarity_multiplier × 0.3 × condition_adjuster × reputation_multiplier`
- 这是一个 **40% 的差异**——文本声称五折卖价，公式实际为三折（乘以 ×0.3 基准乘数）。
- `entities.yaml` §item_price_formula 注释确认 0.3 为权威值："已对齐: 08-failure-growth.md §11.1 和 07-tavern-system.md 的卖价公式已统一为 0.3 基准乘数"。

**影响**：依赖文本描述的玩家会预期卖价翻倍。实现必须二选一。

**推荐方案**：将 §11.1 表格中的"基础价值 ×50%"修正为"基础价值 ×30%（受稀有度/状态/声望修正）"以匹配公式和 registry。

---

### [BLOCKING] 问题 3：物品状态枚举在 GDD 内部自相矛盾（failure-growth §6.2 vs §11.1）

**严重性**: 🔴 BLOCKING — GDD 内部不一致  
**标签**: `[economy-designer]` `[consistency]`

**发现**：
- `§6.2` 物品状态等级枚举：`Pristine → Worn → Damaged → Broken → Destroyed`（5 个状态，无 "good"）
- `§11.1` condition_adjuster 引用：`pristine/good/worn/damaged/broken`（包含 "good"）
- `§7.3` 继承数据模型的 `inherited_equipment.condition` 枚举：`["pristine", "worn", "damaged"]`（仅 3 个状态，无 "good" 和 "broken"）
- `03-items-equipment.md` §3.1 状态枚举：包含 "good"（5 项：pristine/good/worn/damaged/broken）
- `entities.yaml` §item_price_formula：包含 "good" 在 condition.values 中

**影响**：§6.2 落后于 items-equipment.md 的权威枚举，导致 GDD 内部出现两个不同的状态枚举。对 sell_price 公式而言，一个状态为 "good" 的物品将找不到匹配的 adjuster，导致公式未定义或运行时错误。

**推荐方案**：
1. **立即修复**：将 §6.2 的物品状态枚举更新为匹配 items-equipment.md：`Pristine → Good → Worn → Damaged → Broken → Destroyed`（6 个状态）。
2. **同时修复**：将 §7.3 继承数据模型的 condition 枚举添加 "good"（至少 4 项：pristine/good/worn/damaged）。
3. 将此对齐状态注册到 entities.yaml 并标记 resolved。

---

### [BLOCKING] 问题 4：`adventure_tier` 在金币上限公式中的数值未定义（failure-growth §11.4）

**严重性**: 🔴 BLOCKING — 公式不可计算  
**标签**: `[economy-designer]`

**发现**：
- `§11.4` 通胀控制 #3：`上限 = 100 × adventure_tier × party_level`
- `adventure_tier` 在整个代码库中定义为字符串枚举：`"short" | "medium" | "long"`
- 公式要求 adventure_tier 为一个数值乘数。它应该取什么值？
  - 如果 short=1, medium=2, long=3：Lv5 短冒险 = 100×1×5 = 500gp 上限，中冒险 = 1000gp
  - 如果使用 adventure_tier 映射的某种 CR/难度系数——则完全未定义

**影响**：实现者无法编码；平衡者无法建模。此公式目前是伪代码。

**推荐方案**：显式定义 adventure_tier 到数值倍率的映射，例如：
```
short  → multiplier = 1
medium → multiplier = 2  
long   → multiplier = 3
```
或使用冒险推荐等级（recommended_level）替代 adventure_tier 作为乘数。更新 entities.yaml。

---

### [BLOCKING] 问题 5："同一物品类型"在递减收益规则中未定义（failure-growth §11.4）

**严重性**: 🔴 BLOCKING — 规则模糊，可被利用  
**标签**: `[economy-designer]`

**发现**：
- `§11.4` 通胀控制 #1："同一物品类型连续出售: 价格每次 -10%（最低 50%）"
- "同一物品类型"的粒度完全未定义：
  - **相同物品 ID**（如：3 把 `item_longsword_001`）？→ 玩家只需稍微改变物品即绕过
  - **相同物品类别**（如：所有 martial_melee 武器）？→ 过于严厉，影响正常游戏
  - **相同稀有度**？→ 高稀有度物品的出售反而受到不成比例的惩罚
- 边界情况：如果你出售长剑 → 短剑 → 长剑，惩罚是否重置？还是独立追踪？

**影响**：没有明确定义的规则无法实现，且为玩家创造了套利机会（例如：出售 1 把长剑，等待 1 次冒险，出售下一把以避免惩罚，或交叉出售不同"类型"的物品）。

**推荐方案**：准确定义分组键。推荐使用 `(category, rarity)` 组合（例如：`(martial_melee, common)`）。示例：出售 3 把普通长剑 → 第 3 次处罚 -20%。更新 §11.4 并添加边界情况（交叉销售行为、重置计时器）。

---

### [BLOCKING] 问题 6：adventure_tier 在 entities.yaml 中重复定义且语义冲突

**严重性**: 🔴 BLOCKING — 数据结构冲突  
**标签**: `[economy-designer]` `[consistency]`

**发现**：
- `entities.yaml` 中 `adventure_tier` 在 formulas 部分出现了**两次**：
  - `xp_completion_reward` 下：`values: [short, medium, long]`，描述为"冒险长度"
  - `reputation_change_formula` 下：`values: [short, medium, long]`，描述为"冒险长度"
- 但在 `reputation_change_formula` 中，`difficulty_modifier` 也列出了 `Deadly: 1.6`，而成功声誉公式说 `Base × 难度系数`——但 Deadly 难度是否存在独立的难度维度（与冒险长度分开），还是 difficulty_modifier 的 Deadly 值是多余的，未明确。

**影响**：Registry 中的重复定义将导致代码生成和验证的混淆。

**推荐方案**：为 `adventure_tier` 创建单一的 registry 条目（在 constants 或 entities 中），并在需要时引用它。明确难度（Easy/Normal/Hard/Deadly）与冒险长度（short/medium/long）是正交维度。

---

## 建议级问题 (RECOMMENDED)

非阻塞性，但在数值调整前需要设计层面的决策。

---

### [RECOMMENDED] 问题 7：传承点消费表中的陷阱选项与套利漏洞（failure-growth §7.5）

**严重性**: 🟡 HIGH — 平衡崩溃  
**标签**: `[economy-designer]`

**发现**：

**A. 1 HP 换 +50 起始金币是陷阱选项**
- §11.3 经济平衡表：Lv1-2 净收入为 +50gp/**每次冒险**。用 1 HP（永久消耗）换取的只是单次冒险的收入优势——而这笔钱在 1 次冒险后就能赚回来。
- 相比之下，1 HP 可以换 +5 临时 HP（1 次冒险），这在战斗中可能直接救一条命。
- **结论**：此选项在当前的 XP→HP 公式下几乎不可能被选择。如果 character-system 采用 `level+5` 公式（Lv5=10 HP），它比 XP-based 公式（Lv5=21 HP）稍微更有吸引力，但仍然非常弱。

**B. 5 HP 换 minor feat（如 Lucky/Tough）可能过于便宜**
- Lucky feat：每天 3 次优势（攻击/豁免/敌人攻击劣势）。DND 5e 中这是最强的专长之一。
- Tough feat：每等级 +2 HP。Lv5 角色获得 +10 HP 上限——相当于 +2 CON 的生命值。
- 如果 Lv5 死亡 = 21 HP（failure-growth 公式），玩家可以立即购买 4 个 feats（Lucky + Tough + 2 个其他）。这使得**死亡对权力玩家来说是可取的**——死一个 Lv5 角色比用活着的角色刷 XP 更高效。
- 即使 Lv5=10 HP（character-system 公式），也可以买 2 个 feats。

**C. 2 HP 换"起始等级+1 但 XP -20%"**
- 如果 Lv5 角色在新冒险中从 Lv6 开始，但队伍是 Lv4——难度平衡崩溃。冒险难度基于队伍平均等级。
- XP -20% 惩罚是否适用于整个冒险，还是仅适用于升级？如果是前者：+1 等级 = ~+1 遭遇的 XP 优势，但 -20% 的惩罚使净效果为负——成为另一个陷阱。

**推荐方案**：
1. 为 HP 消费表添加硬上限：
   - Feat 购买：**每角色终身最多 1 次**（已在 character-system v1.2 调参表第 140 行暗示：`1-3次`）
   - 起始金币：**每个新角色最多 2 次**（已在 character-system v1.2 确认）
   - 将此硬编码到 failure-growth §7.5
2. 重新平衡 HP 成本：
   - +50 起始金币：从 1 HP → **不消耗 HP**（改为：新角色自带 50gp，作为 baseline 而不是 HP 消费）
   - Minor feat：从 5 HP → **10 HP**
   - 起始等级 +1：添加队伍等级限制（不能超过队伍最高等级 +1）
3. 如果采用 character-system 的 `level+5` 公式（更少的 HP），则无需大幅调整成本——较低的 HP 产出自然限制了购买力。

---

### [RECOMMENDED] 问题 8：XP 成长节奏——Lv4→Lv5 瓶颈未被解决（failure-growth §2.2.5 注释）

**严重性**: 🟡 HIGH — 玩家留存风险  
**标签**: `[economy-designer]`

**发现**：
- GDD 自身在 §2.2.5 设计说明中标记了此问题（v1.1 注释）：
  > "⚠️ 调优待办 (2026-05-09): Lv4→Lv5需6,500 XP，按短冒险每角色~500 XP/次估算需约13次冒险。在MVP仅提供短冒险（中冒险需酒馆Lv4解锁）的前提下，Lv4后的进度真空违反MVP标准'重复10次不腻'。"
- 此调优待办在 v1.0 文档中仍然存在，未在 v1.2 中解决。
- 分析：Lv1→Lv2（300 XP）：1 次短冒险（150 完成 + ~200 遭遇 = 350）即可。Lv2→Lv3（900 XP）：~2-3 次冒险。Lv3→Lv4（2,700 XP）：~5-6 次冒险。Lv4→Lv5（6,500 XP）：~13 次冒险。**节奏从 1→2→5→13 急剧膨胀**。
- MVP 上限为 Lv5，但玩家在 Lv4 会遇到严重的进度墙。
- 完成奖励 150/750/2500（已从 300/1500/5000 减半）：在 Lv4→Lv5，完成奖励仅覆盖 150/6500 = 2.3%。在 Lv1→Lv2，覆盖 150/300 = 50%。

**推荐方案**：
1. **为 Lv4→Lv5 添加经验加速**：不需要改变 XP 表，而是为中高等级添加等级缩放乘数（例如，Lv4+ 角色获得 ×1.2 XP）。
2. **或降低 Lv4→Lv5 阈值**：从 6,500 降至 ~4,500（从 ~3,900 到 ~4,500 = ~1,800 增量 vs 原来的 3,800 增量）。
3. **或确保中冒险在 MVP 中更早解锁**：将中冒险解锁从酒馆 Lv4 移至 Lv3。
4. 将此调优待办变为实际的设计决策——不要带着一个已知问题进入实现阶段。

---

### [RECOMMENDED] 问题 9：酒馆经验值/金币正向反馈循环（failure-growth §10.2 vs §11.3）

**严重性**: 🟡 HIGH — 长期经济螺旋风险  
**标签**: `[economy-designer]`

**发现**：
- §10.2.1：每 100gp 投资到酒馆升级 = 50 Tavern XP
- 酒馆升级 → 解锁更好的冒险 → 更高的金币收入 → 更多金币可投资
- 闭环：金币 → Tavern XP → 酒馆 Lv↑ → 更好的冒险 → 更多金币 → 更多 Tavern XP
- 模拟：在 Lv5-6 时，净收入为 +150gp/冒险。如果玩家将所有 150gp 投入酒馆，每次冒险获得 75 Tavern XP + 基础冒险 Tavern XP（100-600）。Lv9→Lv10 需要 36,000 Tavern XP。以每次冒险 ~250-300 Tavern XP（混合投入）计算，需要 ~120-144 次冒险才能从 Lv9 升级到 Lv10。

**螺旋风险**：如果玩家优化投入（所有金币投入酒馆），Lv5→Lv8 的升级速度显著快于仅依赖冒险 Tavern XP 的玩家。这会产生一条"最优路径"——所有金币都用于投资，直到酒馆满级，其他所有消费（装备、药水、招募）都被推迟。

同时，Lv9→Lv10 需要 36,000 Tavern XP——距离非常大。玩家是否会感受到进展，还是会放弃？

**推荐方案**：
1. **为金币投资添加递减收益**：每次连续投资获得更少的 Tavern XP（例如，每次连续冒险 -10%，底限 25 XP/100gp）。
2. **或限制每次冒险的金币投资上限**：例如，最多 300gp/冒险可转换为 Tavern XP。
3. **审查 Lv9→Lv10 的需求**：36,000 Tavern XP 是否过高？如果每个冒险平均 ~250 Tavern XP，那就是 ~144 次冒险。在中冒险解锁（Lv4 酒馆）到长冒险解锁（Lv8 酒馆）之间：这个差距是否太大？

---

### [RECOMMENDED] 问题 10：复活成本作为惩罚还是摩擦（failure-growth §5.3）

**严重性**: 🟡 MEDIUM — 玩家体验  
**标签**: `[economy-designer]`

**发现**：
- 复活成本：1,000 gp + 稀有材料（凤凰羽毛/龙鳞/精灵泪）
- §11.3 经济平衡表：Lv7-8 净收入 = +200gp/冒险
- 单纯从金币角度：1,000gp / 200gp = **5 次冒险**才能存够复活金
- 附加成本：稀有材料（获取方式未定义——它们也是掉落物吗？需要特殊任务吗？）、永久 CON -1、1 次冒险的冷却时间
- 如果玩家在存复活金期间死亡了另一位角色，他们将面临同时存两份复活金的困境

**风险**：5+ 次冒险的储蓄可能不是"有意义的摩擦"，而是"导致玩家放弃该角色"的惩罚。对于已经因 Lv4→Lv5 进度墙而感到挫败的玩家，再失去一个角色可能成为退出点。

**推荐方案**：
1. 分层复活成本：低级角色复活更便宜（例如，Lv1-3：300gp，Lv4-6：600gp，Lv7+：1,000gp）。
2. 为稀有材料提供明确的获取路径（保证掉落或酒馆任务）。
3. 考虑"欠债复活"：角色立即复活但背负债务（未来冒险的金币收入 -30% 直到还清 1,000gp）。

---

### [RECOMMENDED] 问题 11：灾难性失败后重建可行性（failure-growth §3.1 vs §7.5）

**严重性**: 🟡 MEDIUM — 体验阻塞  
**标签**: `[economy-designer]`

**发现**：
- 灾难性失败（全灭）：所有非绑定装备丢失 + 所有角色死亡 + 世界状态灾难性变化
- §10.3 元进度持久化：**角色金币不持久化**（随角色死亡丢失）
- 继承系统：4 个死亡角色产生大量 HP（例如，4×Lv5 = 84 HP（failure-growth 公式）或 40 HP（character-system 公式））
- 问题：玩家拥有大量 HP 但 **0 金币**。HP 消费选项：
  - 1 HP → 50gp（最多 2 次，character-system 硬上限）：**总共 100gp**
  - 3 HP → 继承绑定装备（但装备已丢失——不可用）
  - 5 HP → minor feat（新角色获得专长，但不解决金币危机）
- 更严重的问题：如果 catastrophic failure 丢失了世界状态中的 key_locations 和 NPC——新冒险的起始区域可能已经沦陷，无法从易难度冒险开始重建

**影响**：玩家可能陷入困境——拥有强大的新角色（专长/技能），但无金币购买基础装备，且起始区域过于危险。这可能迫使玩家手动重置世界状态（放弃存档），从而破坏整个"失败是内容"的设计哲学。

**推荐方案**：
1. 灾难性失败后，提供"救济金"机制（例如，酒馆金库自动发放 200gp 的紧急资金用于基础装备）。
2. 确保灾难性失败后，至少有一个"安全"区域可用于 Lv1 冒险。
3. 为继承池添加"金币继承"选项（例如，3 HP → 继承 50% 死亡角色的金币）。

---

### [RECOMMENDED] 问题 12：简单冒险刷金缺乏防护（failure-growth §11.4）

**严重性**: 🟡 MEDIUM — 经济漏洞  
**标签**: `[economy-designer]`

**发现**：
- §11.4 通胀控制 #3 设定了冒险金币上限，但：
  - 上限 = 100 × adventure_tier × party_level（adventure_tier 数值未定义——见阻塞问题 4）
  - 即使有上限，也没有机制阻止玩家连续刷 Lv1 短冒险赚取 500gp（如果上限有效）
- §11.4 通胀控制 #1（出售递减收益）仅影响物品出售，不影响冒险金币奖励
- 没有基于冒险完成次数的经验/金币衰减机制

**对比参考**：许多 roguelike 游戏使用"每次冒险后敌人变强"或"同一冒险主题奖励递减"来防止刷。本系统依靠世界状态变化——但这取决于世界状态是否真的使简单冒险变得不可用（如果起始区域保持 safe/threatened，则可能不适用）。

**推荐方案**：
1. 添加"冒险疲劳"机制：连续完成同一长度/主题的冒险 → 金币奖励 -15%（叠加，最多 -50%），在完成不同主题冒险后重置。
2. 或：简单冒险在完成 N 次后变为"已清剿"（不再生成），直到世界状态变化刷新它们。
3. 或：简单冒险的遭遇数量有下限（不能缩水到 1 个遭遇）——确保每次冒险有最低时间成本。

---

### [RECOMMENDED] 问题 13：酒馆经验值来源——金币投资与角色升级的脱节

**严重性**: 🟡 MEDIUM — 系统协调  
**标签**: `[economy-designer]`

**发现**：
- §10.2.1 酒馆 XP 来源 #2："每 100 gp 投资到酒馆升级: 50 Tavern XP"
- 问题：这是 **Tavern XP**（酒馆升级），而角色有独立的 **Character XP**（角色升级）
- 金币投资加速酒馆升级，但不加速角色升级
- 这意味着：一个富裕的玩家可能拥有 Lv8 酒馆（解锁长冒险），但角色只有 Lv4——而长冒险是为 Lv7+ 队伍设计的
- 结果：玩家可能解锁了超过队伍能力的冒险，或者被要求刷低等级冒险来升级角色以匹配已解锁的内容

**推荐方案**：
1. 确保冒险生成系统检查队伍等级并降级冒险难度（这似乎已在 difficulty_scaling_formula 中处理，但需要确认它是否在"玩家酒馆等级远高于队伍等级"的场景下正确工作）。
2. 或为金币投资添加"角色等级门槛"：酒馆 Lv5 需要至少 1 个角色达到 Lv5。

---

### [RECOMMENDED] 问题 14：伤疤补偿的经济影响——未计入经济模型

**严重性**: 🟡 MEDIUM  
**标签**: `[economy-designer]`

**发现**：
- 多个伤疤提供直接影响经济的补偿：
  - `scar_fire_hair`：免费火焰戏法（节省卷轴/训练成本）
  - `scar_frostbite`：寒冷伤害抵抗（节省药水/装备成本）
  - `scar_toxin_tolerance`：中毒伤害抵抗（节省解毒剂成本，但治疗药水效果 -50%）
  - `scar_broken_mind`：异怪语获得 + 心灵伤害抵抗（节省语言训练成本）
- 伤疤移除成本：500gp（神殿祈祷，仅 Light）/ 300gp + 材料（传奇药水）/ 属性永久 -2（灵魂换约）
- 某些伤疤组合可能产生净正面效果，使玩家希望获得伤疤而非避免它们

**风险**：对于某些 build，获得特定伤疤可以节省 500-1,000gp 的等效支出。如果伤疤的补偿超过惩罚，玩家可能故意让角色在特定伤害类型中倒地以获得"好"伤疤。

**推荐方案**：
1. 审查每个伤疤的惩罚-补偿平衡，确保惩罚始终 ≥ 补偿（净负面）。
2. 对于提供抵抗的伤疤：考虑添加"该伤害类型的治疗药水效果 -50%"作为对称惩罚。
3. 限制角色可拥有的同类型伤疤数量（已在 §4.4 中限制为 3 个）——确保此限制被强制执行。

---

### [RECOMMENDED] 问题 15：装备修复经济——condition_level 修复成本与 description 不一致

**严重性**: 🟡 MEDIUM — 公式与文本矛盾  
**标签**: `[economy-designer]`

**发现**：
- §11.2 支出表："装备修复 | 10-50 gp/级 | 每个 condition level"
- entities.yaml §repair_cost_formula：`item.value_gp × rarity_multiplier × condition_fix_rate`
  - 对于 worn（fix_rate=0.25）：一把普通长剑（15gp）→ 15 × 1 × 0.25 = 3.75gp
  - 对于 damaged（fix_rate=0.50）：15 × 0.50 = 7.5gp
  - 对于 broken（fix_rate=1.00）：15 × 1.00 = 15gp
- 但 §6.4 修复选项 #1 说："修复费用: 10-50 gp per condition level"
- 这两个不匹配——formula 对于普通物品产生的是个位数 gp，但 §6.4/§11.2 声称是 10-50gp

**影响**：修复成本浮动约 3-20 倍，取决于使用哪个定义。

**推荐方案**：以公式为准（entities.yaml），将 §6.4 和 §11.2 的文本更新为"修复费用 = 基于物品价值和稀有度的公式（§entities.yaml repair_cost_formula），范围 1-7,500gp"。

---

### [RECOMMENDED] 问题 16：部分成功结算——XP 奖励与完成百分比的对齐问题（failure-growth §3.3）

**严重性**: 🟢 LOW — 逻辑裂缝  
**标签**: `[economy-designer]`

**发现**：
- §3.3 部分成功规则：
  - `completion >= 75%`：Minor 惩罚 + 保留所有已完成的次要目标 XP
  - `completion >= 50%`：保留 **50%** 已完成次要目标的 XP
  - `completion >= 25%`：保留 **25%** 已完成次要目标的 XP
- 问题：如果一个队伍完成了 90% 的冒险（包括 3 个次要目标），获得所有次要目标 XP 是合理的。但如果完成 80%（未击败 Boss 但有 3 个次要目标），他们获得的 XP 可能比完成 95%（击败 Boss 但只有 1 个次要目标）的队伍多吗？
- 次要目标奖励与完成百分比之间的相互作用没有文档化。需要明确：完成百分比仅影响次要目标 XP 的保留比例，还是也影响基础 XP？

**推荐方案**：将 §3.3 规则重写为明确的公式——指定基础 XP 和次要目标 XP 各自由完成百分比如何影响。

---

## 总结：按优先级排列的行动项

| # | 优先级 | 类别 | 问题 | 行动 |
|---|:------:|------|------|------|
| 1 | 🔴 BLOCK | 跨文档 | 传承点公式冲突 | 选择单一公式，同步所有 3 份文档 |
| 2 | 🔴 BLOCK | 文本/公式 | 卖价 50% vs ×0.3 | 修正 §11.1 表格文本 |
| 3 | 🔴 BLOCK | 内部矛盾 | "good" 状态不一致 | 将 §6.2/§7.3 对齐到 items-equipment.md 的 6 状态枚举 |
| 4 | 🔴 BLOCK | 未定义 | adventure_tier 数值未定义 | 定义 short=1/medium=2/long=3 或等效映射 |
| 5 | 🔴 BLOCK | 模糊 | "同一物品类型"未定义 | 精确定义分组键，添加边界情况 |
| 6 | 🔴 BLOCK | Registry | adventure_tier 重复定义 | 合并为单一 registry 条目 |
| 7 | 🟡 HIGH | 平衡 | HP 消费表陷阱/套利 | 添加硬上限，重新平衡成本 |
| 8 | 🟡 HIGH | 节奏 | Lv4→Lv5 瓶颈 | 添加经验加速或降低阈值 |
| 9 | 🟡 HIGH | 螺旋 | 金币→Tavern XP 反馈循环 | 添加递减收益或投资上限 |
| 10 | 🟡 MED | 体验 | 复活成本可能过高 | 分层成本或欠债机制 |
| 11 | 🟡 MED | 阻塞 | 灾难后重建不可行 | 救济金或金币继承选项 |
| 12 | 🟡 MED | 漏洞 | 刷金防护不足 | 冒险疲劳或清剿机制 |
| 13 | 🟡 MED | 协调 | 酒馆/角色等级脱节 | 等级门槛或降级扩展 |
| 14 | 🟡 MED | 平衡 | 伤疤补偿净正面 | 审查惩罚-补偿平衡 |
| 15 | 🟡 MED | 文本/公式 | 修复成本不一致 | 以公式为准，更新文本 |
| 16 | 🟢 LOW | 逻辑 | 部分成功 XP 模糊 | 重写为明确公式 |

---

*审查完成日期: 2026-05-10*  
*下一步: 由 creative-director 裁决阻塞问题，由 game-designer 对建议问题进行优先级排序*  
*需同步更新的文档: 08-failure-growth.md, 01-character-system.md, design/registry/entities.yaml*
