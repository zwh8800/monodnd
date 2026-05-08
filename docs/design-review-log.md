# 设计评审日志

## 评审 #1 — 2026-05-08 — 裁决: NEEDS REVISION（已修订为 APPROVED）
**范围信号:** L（大型修订）
**文档:** `design/gdd/GDD-v1.md` v1.0
**模式:** full（全深度评审）
**咨询专家:** game-designer, systems-designer, economy-designer, narrative-director, qa-lead, ux-designer, creative-director（7位）
**发现:** 阻塞 10 条 | 建议修订 8 条 | 次要建议 6 条
**修订结果:** 12条全部解决 — GDD v1.0 → v1.1

**摘要：** GDD的愿景（双层循环、LLM皮肤层、DND Roguelike）在品类中是真正的创新，但初始设计在三个维度上系统性地削弱了自身情感目标：固定节点结构破坏了"故事独特性"；死亡/复活机制让"死亡有重量"失去可信度；纯数值关系无法产生"舍不得"的情感绑定。创意总监裁决为MAJOR REVISION NEEDED，建议2-3周设计修订。修订后建立了设计支柱（`docs/design-pillars.md`），收窄MVP至可行范围，修正战斗/死亡/伤疤/冒险结构/经济核心机制。GDD升级至v1.1。

**关键改动：**
1. 创建 `docs/design-pillars.md`（3支柱+反支柱+设计测试）
2. MVP收窄至DM Agent only，排除关系/伤疤/酒馆升级
3. 同时选择→按先攻顺序选择
4. 死亡添加豁免骰（自然20=1HP）
5. 伤疤改为加法叠加+净负面（推迟Phase 2）
6. 冒险结构参数化（标准/急袭/探索三种模式）
7. 10条可测试AC标准重写
8. Token预算上调+API成本更新
9. 金币惩罚改为分级固定值
10. 跨文档5处矛盾修复
