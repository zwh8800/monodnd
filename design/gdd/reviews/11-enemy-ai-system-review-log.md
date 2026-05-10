# 11-enemy-ai-system.md — 审查日志

---

## Review — 2026-05-10 — Verdict: APPROVED（修订后）

**Scope signal**: L
**Specialists**: game-designer, systems-designer, ai-programmer, gameplay-programmer, qa-lead, performance-analyst, creative-director
**Blocking items**: 9 (all resolved) | **Recommended**: 7

**Summary**: 初次审查为 MAJOR REVISION NEEDED（8 项阻塞）。核心问题：① 与 `04-combat-system.md` §11 的权威冲突——两个文档互相声称权威源且内容不一致；② 无界 `threat_score = estimated_dpr × 2` 导致所有敌人行为坍缩为"攻击最高 DPR"——摧毁了 Player Fantasy 承诺的"可辨识个性"；③ `estimated_dpr` 完全未定义——打分公式最高权重变量无数据源；④ 5 个扁平模板无法产生"个性"——所有敌人类型使用同一公式同一权重。创意总监强制要求 9 项架构级重构：补充模式权威关系、权重向量替代单一公式（个性=不同权重）、刺客完全重设计、estimated_dpr 精确定义、Healer 友方打分公式、两阶段协作（解决时序悖论）、Easy 降权+噪声（非随机）、位置 tile 评分算法、行为级 AC。

**修订完成**: 用户选择逐项修订——所有 9 项阻塞已解决。文档从 153 行扩至 323 行（+111%）。行为类型从 5 扁平模板重构为 6 种权重向量体系，验收标准从 6 条（3 条不可测试）重写为 7 条行为级 AC，Edge Cases 从 6 条扩至 13 条。用户接受修订并标记 Approved。

**Prior verdict resolved**: N/A (first review)

---

## Original Review — 2026-05-10 — Verdict: MAJOR REVISION NEEDED

**Scope signal**: L
**Specialists**: game-designer, systems-designer, ai-programmer, gameplay-programmer, qa-lead, performance-analyst, creative-director
**Blocking items**: 8 | **Recommended**: 7

**Summary**: 创意总监裁决 MAJOR REVISION NEEDED——GDD 在创意层面有三个根本性失败：① 公式摧毁了 Player Fantasy（无界威胁分让行为趋同）；② 与权威源文档矛盾（04§11 已有更丰富的设计被降级）；③ 结构性数学 bug（死目标满分、协作时序悖论、治疗者无公式）。创意总监强制要求 9 项修改：权威关系补充模式、权重向量体系、刺客重设计、estimated_dpr 精确定义、Healer 友方公式、两阶段协作、Easy 降权+噪声、位置 tile 算法、行为级 AC。

### Blocking Items (Resolved):
1. 🔴🔴🔴 权威冲突 — `04-combat-system.md` §11 与本文档互相声称权威源
2. 🔴🔴🔴 threat_score 无界 — `DPR×2` 轻易超越其他因子总和
3. 🔴🔴🔴 estimated_dpr 未定义 — 最高权重变量无计算方法
4. 🔴🔴 hp_score 给死亡目标满分 — `(1-0/max_hp)×15=15`
5. 🔴🔴 刺客自杀逻辑 — "永不撤退+最高威胁目标"=自杀冲锋
6. 🔴🔴 治疗者无友方打分公式
7. 🔴🔴 5 模板无法产生个性 — Player Fantasy 承诺无法交付
8. 🔴 验收标准 3/6 不可测试

### Recommended Revisions (Not Blocking):
9. Easy 难度"完全随机"与 Player Fantasy 矛盾
10. 集火时序悖论
11. 仅覆盖 14 种 DND 条件中的 5 种
12. 位置选择无算法规范
13. 行动执行接口缺失
14. 撤退执行完全未定义
15. Tactical 难度帧预算超标
16. AOE/借机攻击/法术位管理缺失

**Prior verdict resolved**: N/A (first review)
