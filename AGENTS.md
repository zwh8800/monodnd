# PROJECT KNOWLEDGE BASE

Roguelike DND 5e pixel-art RPG ("酒馆与命运 / Tavern & Destiny") — MonoGame 3.8.5+ / C# 12 / .NET 8. Pre-implementation: **zero source code exists**, only design docs. AI-First (Vibe Coding) paradigm: Sisyphus orchestrates, AI generates all code, `dotnet build` is the quality gate.

## STRUCTURE

```
dnd/
├── docs/
│   ├── GDD-v1.md                   # Game Design Document (source of truth for mechanics)
│   ├── technical/
│   │   ├── 01-engine-selection.md  # Why MonoGame (AI safety argument)
│   │   ├── 02-overall-architecture.md  # ★ Code map, module design, C# examples
│   │   ├── 03-vibe-coding-conventions.md # Dev rules, forbidden ops, review checklist
│   │   ├── 04-dev-environment-setup.md   # Setup guide, NuGet packages, commands
│   │   └── 05-implementation-plan.md     # 48-week roadmap, task IDs (P0-01 format)
│   └── subsystems/                # Module deep-dives (01-09)
└── .sisyphus/                      # Sisyphus Agent runtime
```

Planned code structure (does NOT exist yet):
```
src/DndGame/          # Core/, Scenes/, Systems/*, Gateway/, Entities/, UI/, Data/
tests/DndGame.Tests/  # xUnit + FluentAssertions
Data/                 # JSON configs, schemas, templates, SQLite
DndGame.sln
```

## WHERE TO LOOK

| Need | Location |
|------|----------|
| Architecture / code map | `docs/technical/02-overall-architecture.md` |
| Coding rules / forbidden ops | `docs/technical/03-vibe-coding-conventions.md` |
| Setup / commands / NuGet | `docs/technical/04-dev-environment-setup.md` |
| Task IDs / milestones | `docs/technical/05-implementation-plan.md` |
| Combat rules / DND deviations | `docs/subsystems/04-combat-system.md` |
| LLM Gateway / 4 Agents / Schema | `docs/subsystems/02-llm-integration.md` |
| Game design canon | `docs/GDD-v1.md` |

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
- 不写中文注释 (代码注释用英文)

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
- **Git commits**: `type(scope): description` — types: feat/fix/refactor/docs/test/chore, scopes: combat/character/tavern/adventure/settlement/gateway/ui/map/data/save/build

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
- ❌ Chinese comments in code — code comments must be English
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

- **Project state**: Pre-implementation Phase 0. No `.cs`, `.csproj`, `.sln` files exist yet. All code will be generated by AI using the docs as specification.
- **First action**: Run Phase 0 from `05-implementation-plan.md` — `dotnet new sln`, `dotnet new mgdesktopgl`, install NuGet packages, create directory structure.
- **No `.gitignore` exists** — template is in `04-dev-environment-setup.md` §3.7 but hasn't been committed. Create it before any code.
- **Chinese game text**: All player-facing strings (UI, narrative, items, dialogue) are 简体中文. Technical identifiers (code, JSON keys, enums) are English. Use FontStashSharp + NotoSansCJKsc for rendering.
- **DND 5e SRD baseline**: Game uses DND 5e SRD rules with documented deviations — see GDD §5.3-5.4 and subsystems/04-combat-system.md.
- **Offline-first**: Game must be fully playable without any LLM API call. LLM is additive experience, not required.
