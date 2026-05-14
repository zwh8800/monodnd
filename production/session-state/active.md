## Session Extract — /ux-design tavern-main 2026-05-12 → 酒馆主屏幕 UX 规格 — 完成
- Task: 设计酒馆主屏幕 (Tavern Main) UX 规格
- Status: ✅ Complete — 全部 17 节已填妥，交叉引用检查通过（1 隐性缺口已修正，4 新模式标记为 gap）
- File: design/ux/tavern-main.md
- Mode: UX Spec（全新创建，逐节审批）
- Sections: 17（Purpose & Player Need / Context on Arrival / Navigation Position / Entry & Exit Points / Layout Specification (4 sub) / States & Variants (7 states) / Interaction Map / Events Fired (13 events) / Transitions & Animations (20 animations) / Data Requirements / Accessibility / Localization / Acceptance Criteria (12 ACs) / Open Questions (11 items)）
- Design decisions:
  - 酒馆 = "家"的容器——空间驱动交互（主）+ 菜单辅助（辅），参考 Darkest Dungeon Hamlet 双模式
  - 6 按钮底部操作栏：名册/休息/出发/英雄/升级/对话（快捷键 R/Z/E/H/U/T）
  - 12 个覆盖层统一弹出动画：11 个 overlay_pop (0.15s) + 1 个 overlay_slide_up (0.2s)，反向播放退出
  - 4 种关闭覆盖层方式：Esc / 手柄B / X按钮 / 点击遮罩
  - 覆盖层栈深度上限 4 层（GDD §1E.4 E10）
  - 7 种屏幕状态：默认/空/满载/忧郁暮色/加载/首次引导/设施部分解锁
  - 忧郁暮色模式——失败归来触发，poof 粒子恢复动画
  - 休息按钮不弹覆盖层——场景内播放动画序列
  - 出发冒险 = fade_black 0.3s 场景切换（不可逆）
  - 手柄 D-pad 场景焦点导航 + 2px 金色虚线边框指示器
- GDD coverage: 100% — 全部 12 条 UI 需求已覆盖
- Accessibility: Standard 层零缺口
- Navigation consistency: 零不一致——与 GDD §2.3 屏幕流程图完全匹配
- New patterns flagged for library: overlay_pop, overlay_slide_up, 场景焦点导航, 覆盖层栈管理
- Open questions: 10 technical/art/design + 1 pattern library gap
- Next:
  1. /ux-review tavern-main
  2. Add 4 new patterns to interaction-patterns.md
  3. Design remaining screens: Title Screen, Pause Menu, individual overlay specs
  4. /gate-check pre-production
- Task: 设计设置界面 (Settings) UX 规格
- Status: ✅ Complete — 全部 14 章节 + Cross-Reference Check + Handoff
- File: design/ux/settings.md (~400 lines)
- Mode: UX Spec（全新创建，逐节审批）
- Design decisions:
  - 无障碍等级: Basic → Standard（accessibility-requirements.md 已同步更新）
  - 四标签页扁平结构: 画面 / 声音 / 游戏 / 控制
  - 统一下拉选择器（一致性优先）
  - 方案 A 单列列表式布局 + 控制标签页双列键位表
  - 首次访问引导（锚定画面标签页 + 引导文字）
  - 暂停入口轻提示（不锁定标签页）
  - 键位冲突覆盖流程（确认对话框二次确认）
  - 三重关闭机制 + 未保存更改提示
- New patterns to add to library: 连续滑块, 下拉选择器, 开关, 键位重绑行
- GDD coverage: 100%
- Next:
  1. /ux-review settings
  2. Add 4 new patterns to interaction-patterns.md
  3. Create Title Screen / Pause Menu UX specs
  4. /gate-check pre-production

---

## Session Extract — /review-all-gdds 2026-05-06 → 阻塞修订完成
- Original Verdict: FAIL → 5/5 blocking issues resolved
- GDDs reviewed: 10 (1 master GDD + 9 subsystem GDDs)
- Blocking issues resolved:
  1. C-3: 疲劳等级 — Character §2.8.2 修订为3级模型 ✓
  2. C-1: XP公式 — Adventure/Character 引用 Failure §2.2 按遭遇模型 ✓
  3. C-2: 声望标度 — Tavern §2.1-2.3 改为0-100 + 独立酒馆XP ✓
  4. C-4: 世界状态Schema — Adventure §10.1 引用 Failure §9.1 ✓
  5. D-1: 设计支柱 — 创建 design/pillars.md ✓
- Files changed: 4 (01-character-system.md, 06-adventure-generation.md, 07-tavern-system.md, design/pillars.md)
- Recommended next: 重新运行 /review-all-gdds 验证 → /gate-check → /create-architecture
- Report: docs/gdd-cross-review-2026-05-06.md

---

## Session Extract — /map-systems 2026-05-08 → 系统索引创建
- Task: Systems decomposition
- Status: Systems index created
- File: design/gdd/systems-index.md
- Systems identified: 26 (8 categories, 19 MVP, 4 VS, 3 Alpha)
- Review mode: lean (default)
- Review gates: TD-SYSTEM-BOUNDARY skipped (lean), PR-SCOPE skipped (lean), CD-SYSTEMS skipped (lean)
- High-risk: 角色系统 (11 dependents — bottleneck), LLM网关 (technical), 冒险生成 (technical/scope), 战斗系统 (design/complexity)
- Next: Design individual system GDDs — start with 骰子系统 (design order #2)
- Dependency layers: Foundation (5) → Core (5) → Feature1 (7) → Feature2 (2) → Feature3 (6) → Presentation (1)

---

## Session Extract — /design-system 条件效果系统 2026-05-09
- Task: Designing condition-effects-system GDD (system #8)
- Status: Complete — all 12 sections written
- File: design/gdd/10-condition-effects-system.md
- Registry: 15 condition entries tagged for authority migration
- Systems index: #8 status updated to "Designed"
- Verdict: CONCERNS (原始 FAIL → 13/18 阻断已解决)
- GDDs reviewed: 9 system GDDs (14 files total inc. meta-docs)
- Flagged for revision: 01-character-system, 04-combat-system, 06-adventure-generation, 07-tavern-system, 08-failure-growth, GDD-v1, entities.yaml
- Resolved (13): 疲乏Lv3(GDD版)、背包公式(slot)、酒馆解锁(等级)、法术位恢复(一半)、条件所有权(char权威)、结算管线(failure-growth权威)、金币→XP(已删除)、先攻范围(已修正)、GDScript(迁移路线图)、材料经济(已标注)、Lv4真空(已标注)、过期路径(已修复)、条件笔误(已修正)
- Remaining (5): DEX独大、Pillar 3 MVP缺失、战斗认知过载、酒馆身份脱节、3个缺失GDD(条件效果/世界状态/敌人AI)
- Recommended next: 逐个解决5个剩余阻断 → 重新运行 /review-all-gdds 验证
- Report: design/gdd/gdd-cross-review-2026-05-09.md

---

## Session Extract — /ux-design patterns 2026-05-12 → 交互模式库补全
- Task: 补全 interaction-patterns.md（7 个新模式）
- Status: Complete — 全部 7 个模式已定义并写入
- File: design/ux/interaction-patterns.md
- Mode: Retrofit（在现有 5 个模式基础上追加）
- New patterns added:
  1. §8 模式 6: 工具提示 (Tooltip) — 悬停300ms/聚焦500ms触发，智能翻转定位
  2. §9 模式 7: 覆盖层弹出/关闭 — 三重关闭机制，overlay_pop/slide_up 双动画
  3. §10 模式 8: 长列表虚拟滚动 — >50项启用，视口+上下2行缓冲，条目数比例滚动条
  4. §11 模式 9: 进度条/状态条 — 统一填充条规范，四色渐变，分段条变体
  5. §12 模式 10: 倒计时指示器 — 继承模式11，紧迫闪烁+到期音效
  6. §13 模式 11: 拖放-装备 — 双轨操作(鼠标拖放+键盘菜单)，8槽位约束
  7. §14 模式 12: 高亮/选中态 — 三层体系(L1悬停/L2聚焦/L3选中)，元模式
- §15: 缺口与所需模式 — 7项标记✅，仅"自动完成输入"剩余
- Cross-reference: ✅ GDD全覆盖/无障碍全覆盖/模式间交叉引用完整
- Sections: 全部 12 个模式 + 概述 + 全局约定 + 缺口表
- Next: 建议运行 /ux-review interaction-patterns 验证 → /gate-check pre-production

---

> ⚠️ **已废弃**: 以下关于节点地图的会话记录描述的是旧设计的一部分。节点地图已与"房间探索"统一为单一的"探索阶段"，战斗在同地图上以区域限制模式触发。请参考 `design/ux/exploration.md` 了解当前设计。

## Session Extract — /ux-design node-map 2026-05-12 → 节点地图 UX 规格 — 完成

- Task: 设计节点地图 (Node Map) UX 规格
- Status: ✅ Complete — 全部 14 节已填妥，交叉引用检查通过
- File: design/ux/node-map.md
- Mode: UX Spec（全新创建，逐节审批）
- Sections: 14（Purpose & Player Need / Player Context on Arrival / Navigation Position / Entry & Exit Points / Layout Specification (4 sub) / States & Variants (9 states) / Interaction Map / Events Fired (11 events) / Transitions & Animations (19 animations) / Data Requirements (10 data items) / Accessibility / Localization / Acceptance Criteria (14 ACs) / Open Questions (9 OQs)）
- Design decisions:
  - 混合渐进型 (C) — 相邻 1-2 跳可见，更远隐藏，渐进揭示
  - 四种节点视觉状态：未探索/可见/已访问/锁定态（补充 GDD 09 原三态）
  - 方案 C 布局：地图优先 + 按需浮层（地图占 ~90% 屏幕，顶部标题栏 + 底部状态条 + 图例栏）
  - 三层节点尺寸层级：Boss(48px) > 分支(40px) > 普通(28-32px)——不依赖颜色传达重要性
  - 连接线 4 种视觉编码：实线(可达)/虚线(隐藏)/断裂(锁定)/粗线(已走过)
  - 手柄导航基于拓扑连接关系而非屏幕坐标
  - 9 种屏幕状态：首次进入/正常探索/全图模式/分支确认/锁定后/过渡/错误/手柄/团灭
  - 团灭"逐个熄灭"动画 → Settlement 自动过渡
  - 图例栏建议国际化纯图标方案（移除文字依赖）
- GDD coverage: 100% — GDD 09 §5.1 / GDD 05 / GDD 06 全部 UI 需求已覆盖
- Accessibility: Standard 层零缺口（键盘完整路径/四状态双编码/色盲兼容/reduced-motion/手柄）
- Navigation consistency: 与 party-select.md 出口和 GDD 09 §2.3 屏幕流完全匹配
- New patterns flagged: 🆕 节点图标 (Node Icon) / 🆕 拓扑连接线 (Connection Line) / 🆕 当前位置脉冲标记 (Position Pulse Marker)
- Open questions: 9 (1 game-design / 1 art-localization / 1 ui-programmer / 2 ux-designer / 1 lead-programmer / 1 narrative-director / 2 cross-cutting)
- Next:
  1. /ux-review node-map
  2. Add 3 new patterns to interaction-patterns.md
  3. Continue remaining screens: Title Screen, Pause Menu, Room Exploration

---

## Session Extract — /ux-design exploration 2026-05-12 → 探索界面 UX 规格 — 完成

- Task: 设计探索界面 (Exploration) UX 规格
- Status: ✅ Complete — 全部 16 节已填妥，交叉引用检查全部通过
- File: design/ux/exploration.md
- Mode: UX Spec（全新创建，逐节审批）
- Sections: 16（Purpose & Player Need / Player Context on Arrival / Navigation Position / Entry & Exit Points / Layout Specification (4 sub) / States & Variants (10 states) / Interaction Map (4 groups) / Events Fired (26 events) / Transitions & Animations (3 sub: scenes + overlays + feedback) / Data Requirements (45 items) / Accessibility (8 sub) / Localization (5 sub) / Acceptance Criteria (14 ACs) / Open Questions (pending)）
- Design decisions:
  - 方案 A 全屏沉浸布局：半透明浮动 HUD（Zone T 顶栏 + Zone G 全屏网格 + Zone P 队伍状态 + Zone A 操作栏），与 FF6 暗色窗风格一致
  - 小地图 200×150px 右上角半透明覆盖，节点颜色+图标双重编码
  - 底部操作栏 6 按钮横排（搜索/交互/物品/躲藏/解除/检查），键盘 E/F/I/H/D/X 快捷键
  - 探索 = 冒险中"地面状态"——战斗/对话/谜题/商人/休整均从此出发并返回
  - 10 种屏幕状态：默认/空/首次引导/加载/黑暗环境/感知检定/分支选择/全屏地图/战斗预警/团灭过渡
  - 操作栏标签本地化：中文 2 字标签 → 拉丁语纯图标 + Tooltip（方案 A）
  - 26 个事件（含 10 个 ⚡ 持久化状态事件 + 6 个 📊 分析事件）
  - 22 种动画规范：场景过渡 7、覆盖层 6、按钮反馈 3、拾取飞入 3、骰子 4、环境 3（+ reduced-motion 全部适配）
  - 5 种新模式候选：Click-to-Move / Environmental Alert / Proximity Action Prompt / Scene Transition / Perception Check Result
- GDD coverage: 100% — 05-map-exploration / 06-adventure-generation / 09-ui-ux-design 全部 UI 需求覆盖
- Accessibility: Standard 层零缺口（键盘全路径/手柄映射/9项双重编码全部通过/色盲模式/字体缩放/reduced-motion）
- Navigation consistency: 与 party-select.md + hud.md + GDD 09 §2.3 屏幕流完全匹配
- New patterns flagged for library: 5（N1-N5，见 C3 新模式候选清单）
- Next:
  1. /ux-review exploration
  2. Add 5 new patterns to interaction-patterns.md
  3. Continue remaining screens: Title Screen, Pause Menu, Combat HUD (full spec)
  4. /gate-check pre-production

---

## Session Extract — /ux-design exploration 2026-05-12 → 探索界面 UX 规格 — 完成

---

## Session Extract — /ux-design 探索统一 2026-05-14

- Task: 统一节点地图与房间探索为单一大地图探索阶段
- Status: 完成
- Files:
  - design/ux/exploration.md (完整重写，约1000行)
  - design/gdd/09-ui-ux-design.md (导航树与术语更新)
  - design/ux/party-select.md (导航标签更新)
  - design/gdd/GDD-v1.md (架构术语更新)
  - design/gdd/04-combat-system.md (战斗后返回路径更新，7处)
  - design/gdd/06-adventure-generation.md (房间概念变更，4处)
  - design/quick-specs/scene-management-2026-05-09.md (术语更新，4处)
  - design/ux/deprecated/node-map.md (废弃说明扩展)
- Key Decisions:
  - 移除独立的 MAP_VIEW 屏幕，改为探索界面内的全屏地图覆盖层
  - 战斗不切换场景，在同地图上以区域限制模式触发
  - "节点图"保留为内部数据结构，不作为玩家可见的UI概念
- Next: 代码实现阶段
