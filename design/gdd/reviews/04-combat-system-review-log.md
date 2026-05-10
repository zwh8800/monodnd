# 04-combat-system.md 评审记录

## Review — 2026-05-10 — Verdict: MAJOR REVISION NEEDED → 5 blockers resolved → APPROVED

**Scope signal:** M (中等 — 5个上游依赖+4个下游依赖，12+公式，15%文档修订量)

**Specialists:** game-designer, systems-designer, ai-programmer, economy-designer, qa-lead, ux-designer, creative-director

**Blocking items:** 5 (all resolved in same session) | **Recommended:** 18

**Summary:**
首次 `/design-review` 对 `04-combat-system.md` (3102行) 的全面评审。6位领域专家 + creative-director 识别出5个结构性阻塞问题：先攻系统FSM与文本自相矛盾（§2.3固定 vs §3.2每轮重骰）、撤退DC公式符号错误（存活敌人/Boss修饰符与描述相反）、AI行为类型三文档枚举断裂、装备损坏术语与failure-growth condition系统未对齐、QA验收标准不可靠（主观语言+无骰子策略）。

creative-director 裁决为 MAJOR REVISION NEEDED，确认设计方向正确但执行一致性有问题（多轮修订拼凑遗留的文本矛盾）。修订为M规模——5个P0/P1阻塞在本次会话全部修复：FSM重绘+代码修正、撤退公式符号反转+示例重算、AI行为类型统一映射表、装备损坏术语全部对齐failure-growth、12条AC重写为客观可验证标准+骰子Mock规范。文档版本升至v2.0。

**Prior verdict resolved:** 否 — 首次评审。2026-05-09交叉审查的17/18阻断关注跨文档一致性；本次评审关注内部矛盾和新发现的结构性问题。

**Key creative-director ruling:**
> "这份文档的核心设计方向是正确的——每轮重骰先攻比固定顺序更适合Roguelike节奏，双轨死亡机制比标准5e死亡豁免更有戏剧张力。但执行一致性出了问题。5个阻塞项的修复是文本级手术——不需要改变任何设计决策，只需要让文档说实话。"

**Revision details:**
| Blocker | Fix applied |
|---------|------------|
| P0-1: 先攻矛盾 | FSM图重绘(RoundEnd→RollInitiative)、§2.3重写、§2.2状态表修正、§2.4守卫代码改、附录A清理 |
| P0-2: 撤退DC符号 | 存活敌人-1→+1、Boss -5→+5、公式分离(roll vs DC)、示例重算、TEST 43.6修复、新增"被包围"修饰符 |
| P0-3: AI行为枚举 | §11.3.1新增统一映射表——06 9种→04/11 5种理论类型 |
| P1-4: 装备损坏术语 | §14.5全部替换为condition级别(Pristine→Worn→Damaged→Broken→Destroyed)、TPK"丢失"统一 |
| P1-5: QA AC重写 | 12条AC全部客观化、状态数12→15、骰子Mock规范(IDiceRoller)、文档v2.0 |
