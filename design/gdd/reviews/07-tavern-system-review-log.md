# 07-tavern-system.md — 审查日志

---

## Review — 2026-05-10 — Verdict: MAJOR REVISION NEEDED → APPROVED (修订后)

**Scope signal**: L  
**Specialists**: game-designer, systems-designer, economy-designer, ux-designer, ui-programmer, narrative-director, gameplay-programmer, qa-lead, creative-director  
**Blocking items**: 9 (8 resolved in revision, 1 pending ADR) | **Recommended**: 10

### Summary

创意总监裁定 MAJOR REVISION NEEDED — 设计哲学「酒馆是家而非菜单」正确，但 6 个交互系统的机制实施全部与之矛盾。发现三套互斥声望/XP 数值体系、§2.1 vs §2.6 双重金币成本（2.1× 差异）、复活 11,000 GP vs 招募 100 GP（110× 经济剪刀差）、MVP 三方死锁等 9 个阻断问题。用户决策混合方案（空间交互主+菜单辅）、大幅降低复活成本+提升招募费、MVP 固定 Lv1+行商铁匠、两体系并行（声望 0-100 + 累计酒馆XP）、以 §2.1 金币为准。8 项文档问题已完成修订。IEventBus 请求/响应扩展需独立 ADR。

**Prior verdict resolved**: First review
