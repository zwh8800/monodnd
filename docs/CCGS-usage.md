## CCGS 完整工作流程指南（72 个技能）
### 核心理念
CCGS 将游戏开发分为 **7 个阶段**，每个阶段都有明确的门控检查（`/gate-check`）。你必须通过当前阶段的门控才能进入下一阶段。
---
## 阶段总览
```
Phase 1: Concept         →  Phase 2: Systems Design  →  Phase 3: Technical Setup
    ↓                              ↓                              ↓
Phase 4: Pre-Production  →  Phase 5: Production      →  Phase 6: Polish
    ↓                              ↓                              ↓
Phase 7: Release
```
---
## Phase 1: Concept（概念阶段）
**目标**：从"没有想法"到结构化游戏概念文档
### 步骤流程
```
1. /start                          # 引导式入门，确定当前位置
   ↓
2. /brainstorm [可选: 类型提示]     # 协作式创意构思，生成 10 个概念
   ↓
3. /setup-engine [引擎] [版本]      # 配置引擎（如 godot 4.6）
   ↓
4. /art-bible                      # 创建艺术圣经（视觉身份规范）
   ↓
5. /design-review design/gdd/game-concept.md  # 验证概念文档（可选但推荐）
   ↓
6. /map-systems                    # 将概念分解为系统索引
   ↓
7. /gate-check concept             # 门控检查：是否准备好进入 Systems Design
```
### 使用的技能（7 个）
| 技能 | 用途 | 调用的代理 |
|------|------|-----------|
| `/start` | 引导式入门 | — |
| `/brainstorm` | 创意构思 | `game-designer` |
| `/setup-engine` | 引擎配置 | — |
| `/art-bible` | 艺术圣经编写 | `art-director` |
| `/design-review` | 概念验证 | `creative-director` |
| `/map-systems` | 系统分解 | `game-designer` |
| `/gate-check concept` | 门控检查 | — |
### 关键产出
- `design/gdd/game-concept.md` — 游戏概念文档
- `design/gdd/systems-index.md` — 系统索引
- `design/art/art-bible.md` — 艺术圣经
- `.claude/docs/technical-preferences.md` — 引擎配置
---
## Phase 2: Systems Design（系统设计阶段）
**目标**：为每个系统创建完整的 GDD（游戏设计文档）
### 步骤流程
```
1. /map-systems next               # 获取下一个待设计的系统
   ↓
2. /design-system [系统名]          # 引导式逐节 GDD 编写
   ↓
3. /design-review [GDD路径]         # 验证 GDD 完整性
   ↓
4. /quick-design [小变更]           # 轻量级设计规范（可选）
   ↓
5. /consistency-check               # 跨 GDD 一致性检查（可选）
   ↓
6. 重复 1-5 直到所有 MVP 系统完成
   ↓
7. /review-all-gdds                 # 跨 GDD 一致性审查
   ↓
8. /gate-check systems-design       # 门控检查
```
### 使用的技能（7 个）
| 技能 | 用途 | 调用的代理 |
|------|------|-----------|
| `/map-systems next` | 获取下一个待设计系统 | — |
| `/design-system` | 引导式 GDD 编写 | `systems-designer`、`economy-designer`、`narrative-director` |
| `/design-review` | GDD 验证 | `creative-director` |
| `/quick-design` | 轻量级设计规范 | — |
| `/consistency-check` | 跨 GDD 一致性检查 | — |
| `/review-all-gdds` | 跨 GDD 一致性审查 | — |
| `/gate-check systems-design` | 门控检查 | — |
### GDD 的 8 个必需章节
1. **Overview** — 一段话摘要
2. **Player Fantasy** — 玩家想象/感受
3. **Detailed Rules** — 明确的机制规则
4. **Formulas** — 所有计算公式（变量定义和范围）
5. **Edge Cases** — 异常情况处理
6. **Dependencies** — 依赖的其他系统
7. **Tuning Knobs** — 可配置的值（安全范围）
8. **Acceptance Criteria** — 可测试的成功条件
### 关键产出
- `design/gdd/[系统名].md` — 每个系统的 GDD（8 个必需章节）
- `design/gdd/gdd-cross-review-*.md` — 跨 GDD 审查报告
---
## Phase 3: Technical Setup（技术设置阶段）
**目标**：做出关键技术决策，创建架构决策记录（ADR）
### 步骤流程
```
1. /create-architecture             # 创建主架构文档
   ↓
2. /architecture-decision [决策]     # 为每个重要技术决策创建 ADR
   ↓  （重复至少 3 次）
3. /architecture-review             # 验证所有 ADR 的完整性和一致性
   ↓
4. /create-control-manifest         # 从 ADR 生成平面程序员规则
   ↓
5. /test-setup                      # 搭建测试框架和 CI/CD 流水线
   ↓
6. /gate-check technical-setup      # 门控检查
```
### 使用的技能（6 个）
| 技能 | 用途 | 调用的代理 |
|------|------|-----------|
| `/create-architecture` | 主架构文档 | `technical-director` |
| `/architecture-decision` | 创建 ADR | `technical-director` |
| `/architecture-review` | ADR 验证 | `technical-director` |
| `/create-control-manifest` | 生成程序员规则 | — |
| `/test-setup` | 搭建测试框架 | — |
| `/gate-check technical-setup` | 门控检查 | — |
### 关键产出
- `docs/architecture/architecture.md` — 主架构文档
- `docs/architecture/adr-*.md` — 架构决策记录（至少 3 个）
- `docs/architecture/control-manifest.md` — 程序员规则手册
- `docs/architecture/tr-registry.yaml` — 需求追溯注册表
- `tests/` — 测试框架
---
## Phase 4: Pre-Production（预生产阶段）
**目标**：创建 UX 规范、原型化风险机制、将设计转化为可实现的故事
### 步骤流程
```
1. /ux-design [屏幕名]              # 为关键屏幕创建 UX 规范
   ↓
2. /ux-review all                   # 验证 UX 规范
   ↓
3. /prototype [假设]                # 原型化风险机制（可选）
   ↓
4. /create-epics layer: foundation  # 创建史诗（每模块一个）
   ↓
5. /create-stories [史诗名]          # 将史诗分解为故事
   ↓
6. /sprint-plan new                 # 创建第一个冲刺计划
   ↓
7. /story-readiness [故事路径]       # 验证故事是否可实现
   ↓
8. 实现故事 → /story-done            # 完成故事
   ↓
9. /playtest-report                 # 游戏测试报告（至少 3 次）
   ↓
10. /gate-check pre-production      # 门控检查
```
### 使用的技能（10 个）
| 技能 | 用途 | 调用的代理 |
|------|------|-----------|
| `/ux-design` | UX 规范编写 | `ux-designer` |
| `/ux-review` | UX 规范验证 | — |
| `/prototype` | 原型制作 | `prototyper` |
| `/create-epics` | 创建史诗 | — |
| `/create-stories` | 分解故事 | — |
| `/sprint-plan` | 冲刺规划 | `producer` |
| `/story-readiness` | 故事验证 | — |
| `/dev-story` | 实现故事 | 对应程序员代理 |
| `/story-done` | 故事完成审查 | — |
| `/playtest-report` | 游戏测试报告 | — |
| `/gate-check pre-production` | 门控检查 | — |
### 关键产出
- `design/ux/*.md` — UX 规范
- `prototypes/` — 原型（带 README）
- `production/epics/*/EPIC.md` — 史诗文件
- `production/epics/*/story-*.md` — 故事文件
- `production/sprints/sprint-*.md` — 冲刺计划
- `production/playtests/playtest-*.md` — 游戏测试报告
---
## Phase 5: Production（生产阶段）
**目标**：核心生产循环，按冲刺工作，逐个故事实现
### 步骤流程（每个冲刺）
```
1. /sprint-plan new                 # 创建新冲刺计划
   ↓
2. /story-readiness [故事路径]       # 验证故事是否可实现
   ↓
3. /dev-story [故事路径]             # 实现故事
   ↓
4. /story-done [故事路径]            # 故事完成审查
   ↓
5. /code-review [文件路径]           # 代码审查（可选）
   ↓
6. /test-helpers                    # 生成测试辅助库（可选）
   ↓
7. /test-evidence-review [故事路径]  # 测试证据审查（可选）
   ↓
8. 重复 2-7 直到冲刺完成
   ↓
9. /sprint-status                   # 检查冲刺进度
   ↓
10. /scope-check                    # 检查范围蔓延（可选）
   ↓
11. /content-audit                  # 内容审计（可选）
   ↓
12. /retrospective                  # 冲刺回顾
   ↓
13. 重复 1-12 直到所有 MVP 故事完成
   ↓
14. /gate-check production          # 门控检查
```
### 使用的技能（15 个）
| 技能 | 用途 | 调用的代理 |
|------|------|-----------|
| `/sprint-plan` | 冲刺规划 | `producer` |
| `/story-readiness` | 故事验证 | — |
| `/dev-story` | 实现故事 | 对应程序员代理 |
| `/story-done` | 故事完成审查 | — |
| `/code-review` | 代码审查 | `lead-programmer` |
| `/test-helpers` | 生成测试辅助库 | — |
| `/test-evidence-review` | 测试证据审查 | — |
| `/sprint-status` | 冲刺进度检查 | — |
| `/scope-check` | 范围蔓延检查 | — |
| `/content-audit` | 内容审计 | — |
| `/estimate` | 工作量估算 | — |
| `/retrospective` | 冲刺回顾 | — |
| `/propagate-design-change` | 设计变更传播 | — |
| `/bug-report` | Bug 报告 | — |
| `/bug-triage` | Bug 分类 | — |
| `/gate-check production` | 门控检查 | — |
### 团队编排技能（9 个）
当功能跨越多个领域时，使用团队编排技能：
| 技能 | 用途 | 协调的代理 |
|------|------|-----------|
| `/team-combat` | 战斗功能 | `game-designer`、`gameplay-programmer`、`ai-programmer`、`technical-artist`、`sound-designer`、`qa-tester` |
| `/team-narrative` | 叙事内容 | `narrative-director`、`writer`、`world-builder`、`level-designer` |
| `/team-ui` | UI 功能 | `ux-designer`、`ui-programmer`、`technical-artist`、`qa-tester` |
| `/team-level` | 关卡设计 | `level-designer`、`narrative-director`、`world-builder`、`art-director`、`systems-designer`、`qa-tester` |
| `/team-audio` | 音频 | `audio-director`、`sound-designer`、`technical-artist`、`gameplay-programmer` |
| `/team-polish` | 协调打磨 | `performance-analyst`、`technical-artist`、`sound-designer`、`qa-tester` |
| `/team-release` | 协调发布 | `release-manager`、`qa-lead`、`devops-engineer`、`producer` |
| `/team-live-ops` | 运营规划 | `live-ops-designer`、`economy-designer`、`analytics-engineer`、`community-manager`、`writer`、`narrative-director` |
| `/team-qa` | QA 周期 | `qa-lead`、`qa-tester` |
### 关键产出
- 实现的代码
- 通过的测试
- 完成的故事文件
---
## Phase 6: Polish（打磨阶段）
**目标**：性能、平衡、无障碍、音频、视觉打磨
### 步骤流程
```
1. /perf-profile                    # 性能分析
   ↓
2. /balance-check [数据文件]         # 平衡分析
   ↓
3. /asset-audit                     # 资产审计
   ↓
4. /asset-spec [资产名]              # 资产规范（如需要）
   ↓
5. /playtest-report                 # 游戏测试（至少 3 次）
   ↓
6. /tech-debt                       # 技术债务评估
   ↓
7. /security-audit                  # 安全审计（可选）
   ↓
8. /soak-test                       # 浸泡测试（可选）
   ↓
9. /test-flakiness                  # 测试稳定性检查（可选）
   ↓
10. /regression-suite               # 回归测试套件（可选）
   ↓
11. /team-polish [功能]              # 协调打磨
   ↓
12. /localize src/                   # 本地化检查
   ↓
13. /gate-check polish               # 门控检查
```
### 使用的技能（13 个）
| 技能 | 用途 | 调用的代理 |
|------|------|-----------|
| `/perf-profile` | 性能分析 | `performance-analyst` |
| `/balance-check` | 平衡分析 | `economy-designer` |
| `/asset-audit` | 资产审计 | — |
| `/asset-spec` | 资产规范 | `technical-artist` |
| `/playtest-report` | 游戏测试报告 | — |
| `/tech-debt` | 技术债务评估 | — |
| `/security-audit` | 安全审计 | `security-engineer` |
| `/soak-test` | 浸泡测试 | — |
| `/test-flakiness` | 测试稳定性检查 | — |
| `/regression-suite` | 回归测试套件 | — |
| `/team-polish` | 协调打磨 | 4 个专家 |
| `/localize` | 本地化检查 | `localization-lead` |
| `/gate-check polish` | 门控检查 | — |
### 关键产出
- 性能优化报告
- 平衡分析报告
- 资产审计报告
- 游戏测试报告（至少 3 个）
- 技术债务清单
- 安全审计报告
---
## Phase 7: Release（发布阶段）
**目标**：发布准备，协调发布，发布后支持
### 步骤流程
```
1. /release-checklist [版本]         # 发布前验证
   ↓
2. /launch-checklist                # 完整跨部门验证
   ↓
3. /team-release                    # 协调发布
   ↓
4. /patch-notes [版本]               # 生成补丁说明
   ↓
5. /changelog [版本]                 # 生成变更日志
   ↓
6. 发布！
   ↓
7. /hotfix [问题描述]                # 发布后紧急修复（如需要）
   ↓
8. /day-one-patch                   # 首日补丁（如需要）
   ↓
9. /milestone-review                # 里程碑回顾（可选）
```
### 使用的技能（9 个）
| 技能 | 用途 | 调用的代理 |
|------|------|-----------|
| `/release-checklist` | 发布前验证 | `release-manager` |
| `/launch-checklist` | 跨部门验证 | — |
| `/team-release` | 协调发布 | `release-manager`、`qa-lead`、`devops-engineer`、`producer` |
| `/patch-notes` | 补丁说明 | `community-manager` |
| `/changelog` | 变更日志 | — |
| `/hotfix` | 紧急修复 | — |
| `/day-one-patch` | 首日补丁 | — |
| `/milestone-review` | 里程碑回顾 | — |
| `/gate-check release` | 门控检查 | — |
### 关键产出
- 发布清单
- 补丁说明
- 变更日志
- 发布的版本
---
## 跨阶段技能（随时可用）
### 入门和导航（5 个）
| 技能 | 用途 |
|------|------|
| `/start` | 引导式入门 |
| `/help` | 上下文感知的"下一步做什么" |
| `/project-stage-detect` | 完整项目审计，确定当前阶段 |
| `/setup-engine` | 配置引擎、固定版本、设置偏好 |
| `/adopt` | 棕地审计和迁移计划 |
### 评论和分析（10 个）
| 技能 | 用途 |
|------|------|
| `/design-review` | GDD 验证 |
| `/code-review` | 代码审查 |
| `/balance-check` | 平衡分析 |
| `/asset-audit` | 资产审计 |
| `/content-audit` | 内容审计 |
| `/scope-check` | 范围蔓延检查 |
| `/perf-profile` | 性能分析 |
| `/tech-debt` | 技术债务评估 |
| `/gate-check` | 门控检查 |
| `/reverse-document` | 从代码生成设计文档 |
### QA 和测试（10 个）
| 技能 | 用途 |
|------|------|
| `/qa-plan` | QA 测试计划 |
| `/smoke-check` | 烟雾测试门控 |
| `/soak-test` | 浸泡测试协议 |
| `/regression-suite` | 回归测试套件 |
| `/test-setup` | 测试框架搭建 |
| `/test-helpers` | 测试辅助库 |
| `/test-evidence-review` | 测试证据审查 |
| `/test-flakiness` | 测试稳定性检查 |
| `/skill-test` | 技能测试 |
| `/skill-improve` | 技能改进 |
### 生产管理（6 个）
| 技能 | 用途 |
|------|------|
| `/milestone-review` | 里程碑回顾 |
| `/retrospective` | 冲刺回顾 |
| `/bug-report` | Bug 报告 |
| `/bug-triage` | Bug 分类 |
| `/playtest-report` | 游戏测试报告 |
| `/onboard` | 新成员入职 |
### 发布（5 个）
| 技能 | 用途 |
|------|------|
| `/release-checklist` | 发布前验证 |
| `/launch-checklist` | 跨部门验证 |
| `/changelog` | 变更日志 |
| `/patch-notes` | 补丁说明 |
| `/hotfix` | 紧急修复 |
### 创意和内容（3 个）
| 技能 | 用途 |
|------|------|
| `/prototype` | 原型制作 |
| `/localize` | 本地化检查 |
| `/day-one-patch` | 首日补丁 |
### UX 和界面设计（2 个）
| 技能 | 用途 |
|------|------|
| `/ux-design` | UX 规范编写 |
| `/ux-review` | UX 规范验证 |
### 团队编排（9 个）
| 技能 | 用途 |
|------|------|
| `/team-combat` | 战斗功能 |
| `/team-narrative` | 叙事内容 |
| `/team-ui` | UI 功能 |
| `/team-level` | 关卡设计 |
| `/team-audio` | 音频 |
| `/team-polish` | 协调打磨 |
| `/team-release` | 协调发布 |
| `/team-live-ops` | 运营规划 |
| `/team-qa` | QA 周期 |
---
## 快速参考：何时使用什么
### 当你...
| 场景 | 使用 |
|------|------|
| 刚开始，没有想法 | `/start` → `/brainstorm` |
| 有想法，想开始设计 | `/setup-engine` → `/art-bible` → `/map-systems` → `/design-system` |
| 设计完成，想开始编码 | `/create-architecture` → `/architecture-decision` × 3 → `/create-control-manifest` → `/test-setup` |
| 想开始实现功能 | `/create-epics` → `/create-stories` → `/sprint-plan` → `/dev-story` |
| 冲刺中，检查进度 | `/sprint-status` |
| 故事完成 | `/story-done` |
| 冲刺结束 | `/retrospective` → `/sprint-plan new` |
| 想检查质量 | `/code-review`、`/design-review`、`/balance-check`、`/asset-audit` |
| 想发布 | `/release-checklist` → `/launch-checklist` → `/team-release` |
| 出了 bug | `/hotfix` |
| 不知道做什么 | `/help` |
---
## 门控检查汇总
| 门控 | 通过条件 |
|------|----------|
| `/gate-check concept` | 引擎配置 + 概念文档 + 系统索引 + 艺术圣经 |
| `/gate-check systems-design` | 所有 MVP 系统有 GDD + 跨 GDD 审查通过 |
| `/gate-check technical-setup` | 主架构文档 + 至少 3 个 ADR + 控制清单 + 测试框架 |
| `/gate-check pre-production` | UX 规范 + 原型 + 故事 + 冲刺计划 + 游戏测试 |
| `/gate-check production` | 所有 MVP 故事完成 + 游戏测试通过 |
| `/gate-check polish` | 性能达标 + 平衡通过 + 3 次游戏测试 + 资产审计 |
| `/gate-check release` | 发布清单全部通过 |
---
## 三种审查强度
| 模式 | 说明 | 适用场景 |
|------|------|----------|
| `full` | 每个步骤都有导演审查 | 新项目、学习系统 |
| `lean` | 仅在阶段转换时审查 | 有经验的开发者 |
| `solo` | 无导演审查 | 游戏 Jam、原型、最快速度 |
设置方式：`/start` 时选择，或编辑 `production/review-mode.txt`
单次覆盖：任何技能加 `--review solo` 参数
---
## 技能总数统计
| 类别 | 数量 |
|------|------|
| 入门和导航 | 5 |
| 游戏设计 | 6 |
| 架构 | 4 |
| 故事和冲刺 | 8 |
| 评论和分析 | 10 |
| QA 和测试 | 10 |
| 生产管理 | 6 |
| 发布 | 5 |
| 创意和内容 | 3 |
| UX 和界面设计 | 2 |
| 团队编排 | 9 |
| **总计** | **72** |
