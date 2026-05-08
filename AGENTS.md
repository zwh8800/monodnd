# PROJECT KNOWLEDGE BASE

**Generated:** 2026-05-08
**Commit:** b44b0e2
**Branch:** master

Roguelike DND 5e pixel-art RPG ("酒馆与命运 / Tavern & Destiny") — MonoGame 3.8.5+ / C# 12 / .NET 8. Pre-implementation: **Phase 0 complete** — Core infrastructure implemented (11 C# files), design docs finalized (26 MD files, 30k+ lines). AI-First (Vibe Coding) paradigm: Sisyphus orchestrates, AI generates all code, `dotnet build` is the quality gate.

## STRUCTURE

```
dnd/
├── docs/                            # 26 MD files, 30k+ lines (see docs/AGENTS.md)
│   ├── GDD-v1.md                   # Game Design Document (source of truth)
│   ├── technical/                  # 5 architecture/implementation docs
│   │   ├── 02-overall-architecture.md  # ★ Code map, module design, C# examples
│   │   ├── 03-vibe-coding-conventions.md # Dev rules, forbidden ops, review checklist
│   │   ├── 04-dev-environment-setup.md   # Setup guide, NuGet packages, commands
│   │   └── 05-implementation-plan.md     # 48-week roadmap, task IDs
│   └── subsystems/                # 9 module deep-dives (01-09), all design phase
└── .sisyphus/                      # Sisyphus Agent runtime
```

**Implemented (Phase 0):**
```
src/DndGame/                        # 11 C# files, 1140 lines
├── Core/                           # 8 files: ServiceLocator, EventBus, Scene/Entity/Component (custom ECS)
│   ├── ServiceLocator.cs          # Global service registry, thread-safe
│   ├── EventBus.cs                # IEventBus + implementation, snapshot-then-invoke
│   ├── GameRoot.cs                # Main Game class (170 lines)
│   ├── Scene.cs                   # Custom Scene base (NOT Nez.Scene)
│   ├── Entity.cs                  # Custom Entity with AddComponent<T>()
│   ├── Component.cs               # Custom Component base
│   ├── SceneComponent.cs          # Scene-level systems
│   └── GameStateManager.cs        # SceneId enum + state tracking
├── Scenes/MainMenuScene.cs        # Only scene, renders "你好酒馆"
├── Program.cs                     # Entry point
└── Game1.cs                       # DEAD CODE — wrapper never used

tests/DndGame.Tests/                # 13 tests, 100% pass
└── Unit/                           # ServiceLocatorTests.cs, EventBusTests.cs
```

**Planned but empty (scaffolds only):**
```
src/DndGame/
├── Systems/                       # Adventure/, Character/, Combat/, Items/, Tavern/, WorldState/ (empty)
├── Gateway/                       # Agents/, Cache/, Fallback/, Validation/ (empty)
├── Entities/, UI/, Data/          # Empty stubs
└── Content/                       # Fonts/ exists, rest empty

Data/                              # JSON configs, schemas, templates (planned, empty)
DndGame.slnx                       # Solution (new .NET 9 format, not .sln)
```

## WHERE TO LOOK

| Need | Location |
|------|----------|
| Architecture / code map | `docs/technical/02-overall-architecture.md` |
| Coding rules / forbidden ops | `docs/technical/03-vibe-coding-conventions.md` |
| Setup / commands / NuGet | `docs/technical/04-dev-environment-setup.md` |
| Task IDs / milestones | `docs/technical/05-implementation-plan.md` |
| Combat rules / DND deviations | `design/gdd/04-combat-system.md` |
| LLM Gateway / 4 Agents / Schema | `design/gdd/02-llm-integration.md` |
| Game design canon | `design/gdd/GDD-v1.md` |
| Documentation index | `docs/AGENTS.md` |
| Implemented Core classes | `src/DndGame/Core/` (8 files) |
| Tests for Core patterns | `tests/DndGame.Tests/Unit/` (2 files) |
| All subsystem designs | `design/gdd/` (9 files, all design phase) |

## AGENT DELEGATION

Task routing rules — follow this before delegating:

| Need | Delegate to | Notes |
|------|-------------|-------|
| Search existing codebase | `explore` | Always `run_in_background=true` |
| External docs / NuGet / API examples | `librarian` | Always `run_in_background=true` |
| Architecture decisions / hard debugging | `oracle` | Read-only, expensive — consult after 2+ failed attempts |
| Complex multi-file implementation | `deep` | Inject System Prompt (see below) |
| Code review / checklist verification | `momus` | Checks spec compliance |
| Documentation writing | `writing` | — |
| Simple single-file edit | Sisyphus direct | Only when truly trivial |

**Delegation principles:**
- One sub-task = one output. Dependencies first, independents in parallel.
- Search cost < implementation cost → delegate to `explore`.
- Every delegated task must pass the verification loop:

```
Agent completes → dotnet build → fail → return to Agent
  → dotnet test → fail → return to Agent
  → code review (checklist below) → fail → return to Agent
  → all pass → merge
```

**Development loop exit criteria** — no phase is done until its exit standard is met:

| Phase | Exit standard |
|-------|---------------|
| Requirement | User confirms understanding is correct |
| Task decomposition | Sub-task list presented, user confirms |
| Agent implementation | Sub-agent finishes code generation |
| `dotnet build` | Zero errors, zero warnings |
| `dotnet test` | All tests pass |
| Code review | Checklist fully passed |
| Merge | Code merged to target branch |

**Typical decomposition example:**

```
User: "给 CombatEngine 添加借机攻击规则"

Sisyphus decomposes:
  1. explore: search current CombatEngine implementation
  2. oracle: confirm DND 5e opportunity attack design
  3. deep: implement logic (inject System Prompt)
  4. deep: add unit tests

Verify: dotnet build → dotnet test → review → merge
```

## CLAUDE CODE GAME STUDIOS REFERENCE

**关键文档位置：** 项目根目录下的Claude Code Game Studios框架

### 核心文档

| 文档 | 位置 | 用途 |
|------|------|------|
| **项目概述** | `CCGS-README.md` | 49个代理、72个技能、12个钩子、11个规则、39个模板 |
| **主配置** | `CLAUDE.md` | 技术栈、项目结构、协调规则、协作协议 |
| **工作流程** | `docs/WORKFLOW-GUIDE.md` | 7阶段流水线，从概念到发布 |
| **协作原则** | `docs/COLLABORATIVE-DESIGN-PRINCIPLE.md` | 用户驱动协作，非自主执行 |
| **测试框架** | `CCGS Skill Testing Framework/README.md` | 技能和代理的质量保证 |
| **设计标准** | `design/CLAUDE.md` | GDD、UX规范、快速规范标准 |

### 代理分层系统（49个代理）

```
Tier 1 — 导演 (Opus模型)
  creative-director    technical-director    producer

Tier 2 — 部门主管 (Sonnet模型)
  game-designer        lead-programmer       art-director
  audio-director       narrative-director    qa-lead
  release-manager      localization-lead

Tier 3 — 专家 (Sonnet/Haiku模型)
  gameplay-programmer  engine-programmer     ai-programmer
  network-programmer   tools-programmer      ui-programmer
  systems-designer     level-designer        economy-designer
  technical-artist     sound-designer        writer
  world-builder        ux-designer           prototyper
  performance-analyst  devops-engineer       analytics-engineer
  security-engineer    qa-tester             accessibility-specialist
  live-ops-designer    community-manager
```

**引擎专家：**
- **Godot 4**: `godot-specialist` (GDScript, Shaders, GDExtension)
- **Unity**: `unity-specialist` (DOTS/ECS, Shaders/VFX, Addressables, UI Toolkit)
- **Unreal Engine 5**: `unreal-specialist` (GAS, Blueprints, Replication, UMG/CommonUI)

### 技能分类（72个斜杠命令）

**入门和导航：** `/start` `/help` `/project-stage-detect` `/setup-engine` `/adopt`

**游戏设计：** `/brainstorm` `/map-systems` `/design-system` `/quick-design` `/review-all-gdds` `/propagate-design-change`

**架构：** `/create-architecture` `/architecture-decision` `/architecture-review` `/create-control-manifest`

**故事和冲刺：** `/create-epics` `/create-stories` `/dev-story` `/sprint-plan` `/sprint-status` `/story-readiness` `/story-done` `/estimate`

**评论和分析：** `/design-review` `/code-review` `/balance-check` `/content-audit` `/scope-check` `/perf-profile` `/tech-debt` `/gate-check` `/consistency-check`

**QA和测试：** `/qa-plan` `/smoke-check` `/soak-test` `/regression-suite` `/test-setup` `/test-helpers` `/test-evidence-review` `/test-flakiness` `/skill-test` `/skill-improve`

**生产：** `/milestone-review` `/retrospective` `/bug-report` `/bug-triage` `/reverse-document` `/playtest-report`

**发布：** `/release-checklist` `/launch-checklist` `/changelog` `/patch-notes` `/hotfix`

**创意和内容：** `/prototype` `/onboard` `/localize`

**团队编排：** `/team-combat` `/team-narrative` `/team-ui` `/team-release` `/team-polish` `/team-audio` `/team-level` `/team-live-ops` `/team-qa`

### 7阶段工作流程

```
Phase 1: Concept         — 从"没有想法"到结构化游戏概念文档
Phase 2: Systems Design  — 创建所有设计文档（GDD）
Phase 3: Technical Setup — 关键技术决策，架构决策记录（ADR）
Phase 4: Pre-Production  — UX规范，原型化，故事创建，冲刺规划
Phase 5: Production      — 核心生产循环，按冲刺工作
Phase 6: Polish          — 性能、平衡、无障碍、音频、视觉打磨
Phase 7: Release         — 发布准备，协调发布，发布后支持
```

**每个阶段都有门控检查：** `/gate-check concept`, `/gate-check systems-design`, 等

### 协作原则（关键摘要）

**核心哲学：** 用户驱动的协作，而非自主执行

**协作模式：** 问题 → 选项 → 决策 → 起草 → 批准

1. **提问** - 代理在提出解决方案前先问问题
2. **呈现选项** - 代理展示2-4个选项及其优缺点
3. **用户决策** - 用户始终做出最终决定
4. **起草** - 代理在最终确定前展示工作
5. **批准** - 没有用户签字，什么都不会写入

**设计哲学：**
- **MDA框架** - 机制、动态、美学分析
- **自我决定理论** - 自主性、能力感、关联性
- **心流状态设计** - 挑战-技能平衡
- **Bartle玩家类型** - 受众定位和验证
- **验证驱动开发** - 测试先行，然后实现

### 测试框架（CCGS Skill Testing Framework）

**关键文件：**
- `catalog.yaml` - 所有72个技能和49个代理的主注册表
- `quality-rubric.md` - 类别特定的通过/失败指标
- `skills/` - 技能的行为规范文件
- `agents/` - 代理的行为规范文件

**测试命令：**
- `/skill-test static [skill-name]` - 检查一个技能（7项检查）
- `/skill-test spec [skill-name]` - 根据书面规范评估技能
- `/skill-test category [skill-name]` - 根据类别指标评估技能
- `/skill-test audit` - 查看完整覆盖情况
- `/skill-improve [skill-name]` - 测试 → 诊断 → 提出修复 → 重新测试循环

### 对OpenCode的借鉴意义

1. **代理分层结构** - 三层代理组织可用于管理不同角色的代理
2. **技能分类系统** - 72个技能的分类方法可用于组织功能
3. **钩子验证机制** - 12个钩子可用于实现自动化验证
4. **路径范围规则** - 基于文件路径的规则执行机制
5. **协作协议** - 用户驱动的协作模式
6. **工作流程门控** - 阶段门控检查机制
7. **模板系统** - 文档模板可标准化输出
8. **测试框架** - 技能和代理的测试方法

## DEEP AGENT SYSTEM PROMPT

When delegating code generation to `deep` or `unspecified-high`, inject this context. **All items are mandatory — omissions cause the most common AI errors.**

```
你是《酒馆与命运》项目的 C# 开发者。

## 项目上下文
- 游戏框架: MonoGame 3.8.5+ (.NET 8+)
- ECS: Nez (Scene/Entity/Component)
- Roguelike: GoRogue (FOV, A*, 地图生成)
- UI: Myra (代码布局优先)
- 字体: FontStashSharp + Noto Sans CJK
- 数据: sqlite-net
- 测试: xUnit + FluentAssertions

## 代码规范
- 类/方法: PascalCase, 私有字段: _camelCase, 常量: UPPER_SNAKE_CASE
- 接口: IPascalCase, 枚举: PascalCase 值
- 不可变数据用 record, 可变数据用 class
- JSON 用 System.Text.Json, JsonPropertyName 用 snake_case
- 禁止 dynamic 和 object 做参数类型

## 架构约束
- LLM = 皮肤层: 只生成叙事文本, 不决策数值
- 数据驱动: 数值在 JSON/SQLite, 不硬编码
- 接口优于实现, 系统间通过 IEventBus 解耦
- 一个类一个职责 (SRP)

## Nez 特定规范
- scene.CreateEntity("name") 而非 new Entity()
- entity.AddComponent() 添加组件
- Core.StartSceneTransition() 切换场景
- Component 构造函数不做繁重操作

## 禁止
- 不使用 Unity API (MonoBehaviour, GameObject, Instantiate)
- 不使用 async void (用 async Task)
- 不使用 #pragma warning disable
- 不使用 dynamic
- 不硬编码路径
- XML 文档注释（`/// <summary>`）和行内解释性注释（`//`）使用简体中文

## 验证
- 生成的代码必须通过 dotnet build
- 逻辑代码必须有 xUnit 测试, AAA 模式
- 测试命名: MethodName_Scenario_ExpectedResult
```

## THREE DEFENSE LINES (VERIFICATION ORDER)

**Every code change must pass all three before merge.**

1. **`dotnet build`** (2-5s) — catches syntax, type mismatch, missing refs, AI-hallucinated methods
2. **LSP real-time** (instant) — unused vars, nullable refs, style issues
3. **`dotnet test`** (10-30s) — logic errors, boundary conditions, regressions

### Compile Error Fix SOP

```
1. dotnet build discovers error
2. Agent locates issue by error code (CSxxxx)
3. Agent fixes code — NEVER use these workarounds:
   - ❌ #pragma warning disable
   - ❌ change to dynamic to bypass
   - ❌ `as` cast without null check
4. Re-run dotnet build
5. Repeat until zero errors AND zero warnings
6. Must pass with TreatWarningsAsErrors=true
```

Common error codes: `CS0103` name not found, `CS0117` member doesn't exist (AI hallucination), `CS0246` type not found, `CS0266` implicit conversion fail, `CS1503` argument mismatch, `CS8600` nullable warning, `CS8509` switch not exhaustive.

### Test Requirements by Module

| Module | Test type required |
|--------|--------------------|
| Combat engine | Unit tests (mandatory) |
| Dice system | Unit tests (mandatory) |
| Character system | Unit tests (mandatory) |
| Adventure instantiation | Unit tests (mandatory) |
| LLM Gateway | Integration tests + degradation/fallback tests (mandatory) |

**Test rules:**
- Naming: `MethodName_Scenario_ExpectedResult` with AAA pattern (Arrange/Act/Assert)
- Pure logic tests must NOT depend on MonoGame's `GraphicsDevice`
- Use named constants, not magic numbers in test assertions
- No regressions on existing tests

## CODE REVIEW CHECKLIST

Apply after every agent-generated code change:

**Compile:** `dotnet build` zero errors/warnings · Nullable enabled, no warnings · No `#pragma warning disable`
**Test:** `dotnet test` all pass · New code has unit tests · No regressions
**Naming:** PascalCase classes/methods · _camelCase private fields · UPPER_SNAKE_CASE constants · IPascalCase interfaces
**Architecture:** No hardcoded values (→ JSON) · Interface > implementation · No dynamic/object/unchecked `as` · LLM output has Schema validation · One class one responsibility
**Quality:** No dead code · No magic numbers (named constants) · async methods use CancellationToken · switch exhaustive · null checks with `?.` and `??`
**Security:** External input validated · LLM fallback path works (cache/template) · No hardcoded API keys · Paths use Path.Combine · Network requests have timeout+error handling

### Review Failure Handlers

When review catches these patterns, handle as follows:

| Failure | Handler |
|---------|---------|
| AI generated Unity API (`MonoBehaviour`, `GameObject`, `Instantiate`) | Mark as compile error → replace with Nez equivalent → add keyword to System Prompt ban list |
| AI hardcoded a numeric value | Ask AI to explain source → extract to named constant or JSON config |
| AI forgot `await` / used `async void` | Mark compile warning → change to `async Task` → verify call chain handles async correctly |

## CONVENTIONS

*Only deviations from standard C# / .NET patterns are listed.*

- **`TreatWarningsAsErrors=true`** + **`Nullable=enable`** — strictest mode
- **`record` for immutable data**, `class` for mutable — never `struct`, never `dynamic`
- **ServiceLocator pattern** (not DI container) — global service registry with ordered init
- **IEventBus for system decoupling** — systems never reference each other's concrete classes
- **JSON field names are `snake_case`** (`[JsonPropertyName("race_id")]`) while C# properties are PascalCase
- **Game text in 简体中文**, technical identifiers in English — player-facing strings Chinese, code/IDs English
- **LLM = skin layer** — LLM generates narrative text ONLY; program decides all numeric values, rule outcomes, and story branching
- **Data-driven** — game values in JSON/SQLite, never hardcoded in C#
- **Nez conventions**: `scene.CreateEntity("name")` not `new Entity()`, `AddComponent()` for composition, `Core.StartSceneTransition()` for scene changes
- **Test naming**: `MethodName_Scenario_ExpectedResult` with AAA pattern
- **Git commits**: `type(scope): 中文描述` — types: feat/fix/refactor/docs/test/chore, scopes: combat/character/tavern/adventure/settlement/gateway/ui/map/data/save/build. 描述部分使用简体中文（除 `type(scope):` 前缀外）
- **文档语言**: 所有文档（.md 文件）必须使用简体中文编写，包括设计文档、技术文档、注释说明等

## SERVICE INITIALIZATION ORDER

Critical for `Game1.Initialize()` — EventBus must be first (all other services depend on it):

```
0: IEventBus           — inter-system communication
1: IGameStateManager   — global state, scene transitions
2: IDataPersistence    — sqlite-net + JSON
3: ILLMGateway         — LLM request entry point
4: IWorldStateManager  — world state tracking
5: IAudioManager       — BGM/SFX
6: IResourceCache      — preloaded resources
```

After all registered: `ServiceLocator.FinalizeRegistration()` — locks the registry, subsequent `Register()` throws.

## ANTI-PATTERNS

**Hard bans (will fail code review):**
- ❌ `dynamic` — abandons compile-time type checking
- ❌ `object` as parameter type — use generics or interfaces
- ❌ `#pragma warning disable` — fix the code, don't suppress
- ❌ `async void` — always `async Task`
- ❌ Unity API (`MonoBehaviour`, `GameObject`, `Instantiate`, `Transform`) — use Nez equivalents
- ❌ Hardcoded paths (e.g., `"Content/textures/x.png"`) — use `ContentManager.Load`
- ❌ Hardcoded numeric values in code — put in JSON config or named constants
- ❌ Missing XML doc comments on public API — all public/protected symbols must have `/// <summary>` in 简体中文
- ❌ LLM deciding numeric values, battle outcomes, or story branching — program controls these
- ❌ LLM output bypassing JSON Schema validation — all LLM output must pass Schema check
- ❌ Submitting code that fails `dotnet build`

**Architecture violations:**
- ❌ Direct inter-system references — use `IEventBus`
- ❌ Multiple responsibilities in one class — one class, one reason to change
- ❌ Business logic in UI (Myra) — UI only collects input and displays data

**Common AI errors to watch for:**
| Pattern | Symptom | Fix |
|---------|---------|-----|
| Unity API | `MonoBehaviour`, `GameObject.Find` | Replace with Nez/MonoGame API |
| `async void` | Returns void not Task | Change to `async Task` |
| Missing `await` | Returns Task but doesn't await | Add `await` |
| Hardcoded path | `"Content/textures/x.png"` | Use `ContentManager.Load` |
| Non-exhaustive switch | `default` catches all missed cases | Enumerate all branches |
| `new Entity()` | Direct construction | Use `scene.CreateEntity()` |
| Ignored CancellationToken | Async methods missing it | Add CancellationToken parameter |

**DND 5e deviations (NOT standard 5e):**
- Initiative re-rolled each round (not fixed order)
- Critical hits maximize damage dice (not double dice)
- Death saves: 3 rounds without healing = death (not 3 successes/failures)
- Exhaustion: 3 levels only (not 6)
- Encumbrance: slot-based (not pound-based)
- Action selection: simultaneous → resolve by initiative (not sequential turns)

## EVENT RESULT SEPARATION MODEL

All game events follow this split — **never** let LLM decide mechanical outcomes:

```
Mechanical result (PROGRAM controls):
  · Relationship deltas (+/- N)
  · Quest generation/unlock
  · Item/gold rewards
  · Combat triggers
  · Buff/debuff application
  · Facility state changes

Narrative result (LLM generates, Schema-constrained):
  · Scene descriptions (DM Agent)
  · NPC dialogue (DM Agent)
  · Character emotional expressions (DM Agent)
  · Atmosphere text (DM Agent)

Execution flow:
  1. Program decides mechanical result
  2. Mechanical result + context → LLM
  3. LLM generates narrative matching the result
  4. UI displays narrative + mechanical notification
```

## LLM OUTPUT VALIDATION

All LLM Agent output must go through this pipeline — **never** bypass Schema validation:

```
LLM outputs JSON
  → JsonSchema.Net validation (format check)
  → Fail: retry up to 3 times
  → Business logic validation (value ranges, cross-field constraints)
  → Write to cache
  → Return to calling system
  → All retries exhausted → fallback to static templates
```

Schema files located at: `Data/Schemas/` (adventure_blueprint, narrative_text, dialogue_options, item_description, balance_report, character_narrative, penalty_result)

## EXTERNAL REFERENCE RULES

**Consultation priority:** Project docs/code > MonoGame/Nez/GoRogue official docs > GitHub examples > NuGet README > Community blogs

**Do NOT consult:**
- Unity documentation or API references
- Godot documentation or GDScript code
- Outdated .NET Framework docs
- Any API usage learned from external sources must pass `dotnet build` verification

## GIT WORKFLOW

**Branch strategy:**

```
main → stable, only accepts dev merges
  └── dev → development主线
       ├── feature/{name}
       ├── fix/{name}
       ├── refactor/{name}
       └── docs/{name}
```

Branch lifecycle: create branch → develop + verify → PR to dev → review pass → squash merge → delete branch.

**AI commit rules (hard):**
- AI code MUST pass `dotnet build` + `dotnet test` before commit — no exceptions
- One commit = one thing — never mix unrelated changes
- Commit messages AI-generated, developer-reviewed
- Commit message 格式：`type(scope): 简体中文描述` — 前缀 `type(scope):` 使用英文，描述部分必须使用简体中文
  - ✅ `feat(combat): 实现骰子系统`
  - ✅ `fix(test): 修复暴击测试的不稳定性`
  - ❌ `feat(combat): implement dice roller system`（描述应为中文）

## COMMANDS

```bash
# Build (first defense line — MUST pass before any commit)
dotnet build src/DndGame/DndGame.csproj

# Strict build (all warnings as errors)
dotnet build -p:TreatWarningsAsErrors=true

# Run all tests (third defense line)
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~CombatEngineTests"

# Run game
dotnet run --project src/DndGame

# Content pipeline (manual)
dotnet mgcb Content/Content.mgcb

# Publish release
dotnet publish -c Release src/DndGame/DndGame.csproj

# NuGet package management
dotnet add package Nez --version 2.*
```

## NOTES

- **Project state**: Phase 0 complete — Core infrastructure (ServiceLocator, EventBus, Scene/Entity/Component, GameStateManager) implemented with 13 passing tests. All 9 subsystems designed but zero business logic code.
- **Custom ECS**: Uses hand-rolled Scene/Entity/Component system in Core/, NOT Nez. Nez NuGet package NOT in csproj despite docs referencing Nez conventions. Update docs OR add Nez dependency.
- **GoRogue version mismatch**: csproj has 2.6.4, docs specify 3.*. Lock to 2.6.4 and update docs.
- **Game1.cs is dead code**: Program.cs creates GameRoot directly; Game1.cs never instantiated. Remove or repurpose.
- **ServiceLocator partially wired**: GameRoot.Initialize() calls ServiceRegistration.RegisterAll() (registers IEventBus, IGameStateManager, IFontService). Remaining 4 services (IDataPersistence, ILLMGateway, IWorldStateManager, IAudioManager, IResourceCache) not yet registered. FinalizeRegistration() never called.
- **Missing NuGet packages**: Nez (not installed), MonoGame.Extended.Tiled (not installed), .editorconfig (missing), Directory.Build.props (missing).
- **Chinese game text**: All player-facing strings (UI, narrative, items, dialogue) are 简体中文. Technical identifiers (code, JSON keys, enums) are English. FontStashSharp + NotoSansCJKsc used.
- **DND 5e SRD baseline**: Game uses DND 5e SRD rules with documented deviations — see GDD §5.3-5.4 and subsystems/04-combat-system.md.
- **Offline-first**: Game must be fully playable without any LLM API call. LLM is additive experience, not required.
- **LLM = skin layer**: LLM generates narrative text ONLY; program controls all numeric values, rule outcomes, and story branching.
- **Build gate**: `dotnet build` zero errors AND zero warnings. `TreatWarningsAsErrors=true`. `dotnet test` all pass before any commit.
