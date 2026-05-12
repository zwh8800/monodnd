# 架构审查报告

**日期**: 2026-05-11  
**引擎**: MonoGame 3.8.x stable（3.8.4.1 当前）  
**范式**: AI-First（Vibe Coding）  
**审查人**: technical-director + Sisyphus

---

## 审查摘要

| 项目 | 数值 |
|------|------|
| GDD 已审阅 | —（ADR-0000 专项审查，非全量） |
| ADR 已审阅 | 9（ADR-0000 ~ ADR-0008） |
| ADR 状态 | 全部 Accepted |
| 引擎一致性 | 9/9 ADR 引擎兼容 |
| 依赖链 | 无循环，拓扑排序正确 |
| TR 注册表 | 1,211 条需求（tr-registry.yaml） |

---

## 审查发现

### 🔴 CONCERN 1: ADR-0000 风险表含过时 Nez 引用 **[已修复]**

**位置**: ADR-0000 L189-193（风险缓解表）  
**问题**: 风险表写"Nez ECS 减少样板代码"，但 ADR-0001 已明确拒绝 Nez 并选择自定义 ECS。此引用会误导 AI 代码生成（误以为项目使用 Nez）。  
**修复**: 已将"Nez ECS"替换为"自定义 ECS (ADR-0001)"，并将"GoRogue/Nez 停维护"风险项更新为"GoRogue 停维护"（加注 Nez 不在项目中使用）。

### ⚠️ CONCERN 2: MonoGame 版本语义模糊 **[已修复]**

**位置**: ADR-0000 标题、Decision 节、Engine Compatibility 表  
**问题**: 原文多处写"MonoGame 3.8.5+"，但 csproj 使用 `3.8.*` 通配符（解析到稳定版 3.8.4.1），3.8.5 尚为 preview。"3.8.5+" 暗示最低 3.8.5，与实际不符。  
**修复**:
- ADR-0000 标题改为"MonoGame 3.8.x"
- Engine Compatibility 表改为"MonoGame 3.8.x stable（当前 3.8.4.1，目标 3.8.5 正式发布后跟进）"
- Decision 节更新版本描述
- VERSION.md "项目锁定"行更新为明确区分当前稳定版与未来目标

### ⚠️ CONCERN 3: Technical Preferences 全空 **[已修复]**

**位置**: `.claude/docs/technical-preferences.md`  
**问题**: 87 行全部标记为 `[TO BE CONFIGURED]`，后续技能（`/architecture-review`、`/code-review`、`/dev-story`）无法路由到正确的引擎专家。  
**修复**: 已根据项目实际配置填充全部字段：
- 引擎、语言、渲染管线
- 输入与平台目标
- 命名规范（对齐 AGENTS.md）
- 性能预算（60fps / <512MB）
- 测试框架（xUnit + FluentAssertions）
- 禁止模式（dynamic、Unity API、Nez 等）
- 已安装的依赖库列表
- ADR 决策日志
- 引擎专家路由表（MonoGame 映射到 lead-programmer + engine/gameplay/ui-programmer）

---

## 次要发现（非阻塞）

| # | 问题 | 说明 |
|---|------|------|
| 1 | Engine Reference 库不完整 | `breaking-changes.md`、`deprecated-apis.md`、`modules/` 目录缺失 — 建议运行 `/setup-engine refresh` 补充 |
| 2 | `control-manifest.md` API 名称不一致 | 使用了 `Resolve<T>()`，但 ADR-0002 定义为 `Get<T>()` — 建议统一 |
| 3 | MonoGame.Extended 6.0.0 使用策略未决策 | 已安装但用途未在 ADR 中明确 |
| 4 | Feature 层系统缺少专门 ADR | 冒险实例化、酒馆、结算系统无对应 ADR（已在 architecture.md §8 中标记） |

---

## 审查结论

### Verdict: **PASS**（所有 CONCERN 已修复）

本报告针对 ADR-0000 引擎选型进行专项审查。发现的 3 项 CONCERN 已全部修复。9 个 ADR 全部 Accepted，依赖链完整无循环，引擎版本语义已明确。

### 遗留建议

1. 运行 `/setup-engine refresh` 补充 Engine Reference 文档
2. 为 Feature 层系统创建 ADR（冒险实例化 → ADR-0009，酒馆 → ADR-0010，结算 → ADR-0011）
3. 修复 `control-manifest.md` 中 `Resolve<T>()` → `Get<T>()` 的不一致

### Gate Guidance

可运行 `/gate-check pre-production` 验证进入 Sprint 1 的条件。

---

## 变更记录

| 文件 | 变更 |
|------|------|
| `docs/architecture/adr-0000-engine-selection.md` | 修复 Nez 引用（3 处）+ 版本语义明确 |
| `docs/engine-reference/monogame/VERSION.md` | 明确版本锁定语义 |
| `.claude/docs/technical-preferences.md` | 全量填充（87 行 → 动态内容） |
| `docs/architecture/architecture-review-2026-05-11.md` | 本报告（新建） |
