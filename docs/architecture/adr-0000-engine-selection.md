# ADR-0000：游戏框架与编程语言选型 — MonoGame 3.8.5+ / C# 12 / .NET 8

## Status
Proposed

## Date
2026-05-06

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | MonoGame 3.8.5+ |
| **Domain** | Core（根决策，影响所有系统） |
| **Knowledge Risk** | LOW — MonoGame API 自 XNA 时代（2006）以来高度稳定，LLM 训练数据充分覆盖 |
| **References Consulted** | `docs/technical/01-engine-selection.md`（完整对比分析，9 引擎评估）、`docs/technical/02-overall-architecture.md`、`design/gdd/GDD-v1.md` |
| **Post-Cutoff APIs Used** | None — 所有核心 API（`SpriteBatch`、`ContentManager`、`Game` 基类）均在 LLM 训练截止日期前稳定 |
| **Verification Required** | `dotnet build` zero errors/warnings；跨平台编译通过（Windows/macOS/Linux）；`dotnet test` 全绿 |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | None — 根 ADR |
| **Enables** | ADR-0001（ECS 架构选型）、ADR-0002（服务注册模式）、ADR-0003（跨系统通信）、ADR-0004（LLM 集成架构）、ADR-0005（数据持久化方案） |
| **Blocks** | 所有 Epic 和 Story — 引擎选型是所有实现工作的前提 |
| **Ordering Note** | 必须是第一个 Accepted 的 ADR |

## Context

### Problem Statement
《酒馆与命运》采用 **AI-First（Vibe Coding）开发范式**，大部分代码由 AI 生成，开发者负责需求描述、代码审查、架构决策和 AI 纠错。传统引擎选型框架关注渲染能力、编辑器体验和社区规模，但在 AI-First 范式下，核心权重变为：**(1) 编译期类型安全 > 编辑器功能，(2) API 稳定性 + 小 API 面 > 社区规模，(3) 代码驱动 > 可视化驱动**。需要从 9 个候选引擎中选出最适合 AI 生成代码 + 编译期验证质量门禁的技术底座。

### Constraints
- 小团队（1-3 人），核心技能 C#/Lua/JavaScript/C++
- AI-First 开发范式：编译期类型安全是硬性需求，AI 生成代码必须有编译器作为第一道防线
- 跨域混淆风险必须可控：AI 不应将其他框架的 API 混入目标代码
- 预算有限，优先免费/开源方案（MIT 许可证）
- 2D 像素渲染必须原生支持，不能是 3D 引擎的附加功能
- DND 5e 规则系统的复杂度要求强类型语言支撑
- 需要 HTTP 客户端（LLM API 调用）和 SQLite 持久化
- 中文像素字体渲染必须可用

### Requirements
- 必须支持编译期类型检查（`dotnet build` 捕获语法错误、类型错误、空引用）
- 必须零跨域 API 混淆风险（AI 不会混入其他引擎 API）
- 必须支持 PC 平台（Windows/macOS/Linux），后续覆盖移动端
- 必须集成 Roguelike 基础设施（FOV、寻路、地图生成）
- 必须可用单元测试框架（xUnit）覆盖核心逻辑
- 性能目标：集成显卡 60fps，内存 < 512MB

## Decision

**选择 MonoGame 3.8.5+ 作为游戏框架，C# 12 作为编程语言，.NET 8 作为运行时**。

### 架构示意图

```
┌──────────────────────────────────────────────────────────────┐
│           AI-First 开发工作流 (MonoGame + C#)                  │
│                                                              │
│  AI 生成 C# 代码 → dotnet build (2-5s) → 编译错误 → AI 修复   │
│       ↓ 编译通过                                              │
│  LSP 实时检查 (类型/空引用/风格) → 即时反馈                     │
│       ↓                                                      │
│  dotnet test → 单元测试通过 → 代码审查 → 合并                  │
│                                                              │
│  对比 Godot GDScript:                                        │
│  AI 生成代码 → 运行游戏 (30s-2min) → 运行时错误 → AI 修复      │
│  ↑ 无编译期检查，所有错误在运行时暴露                           │
└──────────────────────────────────────────────────────────────┘
```

### 核心选择理由（按重要性排列）

**1. 编译期类型安全 — `dotnet build` 作为第一道防线**

C# 编译器在 AI 代码生成后 2-5 秒内捕获：语法错误、类型错误（`string`→`int`）、空引用风险（Nullable Reference Types）、未定义方法/属性、switch 穷尽性、泛型约束违反。相比 GDScript 动态类型（所有错误运行时暴露），反馈周期缩短 10-50 倍。

**2. 零跨域混淆风险**

XNA/MonoGame API 自 2006 年高度稳定且封闭唯一。没有 `MonoBehaviour`、`Start()`/`Update()` 约定、`GameObject` 等 Unity 概念。AI 训练数据中不存在与 XNA API 混淆的对象，不可能把 Unity API 混入 MonoGame 代码——这与 Godot C# 形成鲜明对比（AI 频繁混入 Unity API，编译通过但逻辑错误）。

**3. C# 团队熟悉度 + 一流语言特性**

泛型、LINQ、async/await、record、模式匹配——构建 DND 5e 规则系统的理想工具。团队成员无需学习 GDScript/GML。

**4. 成熟的 Roguelike 生态**

GoRogue（FOV/A*/地图生成）、RogueSharp 等专用库，Godot 生态中无等效物。

**5. 代码即场景 — 完美匹配 AI 工作流**

无可视化编辑器，所有游戏对象通过 C# 代码创建。AI 可覆盖 100% 开发工作，而非仅脚本层面。

**6. monogame-mcp**

社区开发的 MCP 工具，让 AI 直接操作 MonoGame 项目（创建 csproj、管理 Content.mgcb、生成 SpriteBatch 样板代码）。

**7. 商业验证 + .NET 生态**

MonoGame 有成功商业案例（8-Bit Adventures 2、Celeste 原型、Stardew Valley 早期）。.NET 8 提供 NuGet（30 万+ 包）、HttpClient、System.Text.Json、LINQ、Native AOT 编译。

**8. 数据驱动设计天然适配**

C# `record` + System.Text.Json 反序列化 + JSON Schema 验证构成完整的数据驱动链，编译器确保数据类型正确。

### 关键接口

```csharp
// 游戏入口 — Program.cs 直接创建 GameRoot（非 Game1.cs 包装）
public class GameRoot : Game
{
    protected override void Initialize() { /* 服务注册管线 */ }
    protected override void LoadContent() { /* 资源预加载 */ }
    protected override void Update(GameTime gameTime) { /* 主循环 */ }
    protected override void Draw(GameTime gameTime) { /* 渲染管线 */ }
}

// 服务定位器 — 全局服务注册与访问
public static class ServiceLocator
{
    public static void Register<T>(T service);
    public static T Get<T>();
    public static void FinalizeRegistration(); // 锁定注册表
}

// 事件总线 — 跨系统解耦通信
public interface IEventBus
{
    void Subscribe<TEvent>(Action<TEvent> handler);
    void Publish<TEvent>(TEvent @event);
    void Unsubscribe<TEvent>(Action<TEvent> handler);
}
```

## Alternatives Considered

### Alternative A：Godot 4.x + GDScript
- **Description**：使用 Godot 原生脚本语言 GDScript，2D 像素渲染零配置，内置 TileMapLayer、HTTPRequest
- **Pros**：最好的 2D 像素开箱即用体验；编辑器拖拽工作流友好；社区活跃（B 站/QQ 群）
- **Cons**：GDScript 动态类型，AI 生成代码所有错误运行时暴露；`:=` 类型推断陷阱（`instantiate()` 返回 `Variant`）；GDScript 训练数据仅为 C# 的 1/10~1/100
- **Rejection Reason**：动态类型与 AI-First 范式根本冲突。编译器无法发挥质量门禁作用，AI 生成代码的调试成本极高。用户实际经验证实："用 AI 开发 Godot 就会写出一堆甚至语法报错的代码而没有前期检查出来"

### Alternative B：Godot 4.x + C#
- **Description**：使用 Godot 引擎但采用 C# 脚本绑定（.NET 6+）
- **Pros**：保留 Godot 编辑器体验 + C# 编译期类型安全
- **Cons**：AI 训练数据中 Unity C# 占比远大于 Godot C#，AI 频繁生成 `MonoBehaviour`、`Start()`、`Update()`、`GameObject.Find()` 等 Unity API 调用。这些代码在 Godot 中编译通过但运行时行为完全不同——"编译通过的逻辑错误比语法错误更危险"。godogen 项目（GDScript→C# 迁移工具）实证了这一问题
- **Rejection Reason**：跨域 API 混淆风险极高且隐蔽。编译通过但逻辑错误的代码比运行时错误更难排查

### Alternative C：Unity 6 + C#
- **Description**：使用市场占有率最高的游戏引擎，C# 生态完善
- **Pros**：最大社区和资产商店；成熟的 D&D 5e SDK；C# 编译期检查
- **Cons**：3D 引擎强做 2D 有额外开销；包体 ~150MB 起步；API 面过大（700+ 命名空间），AI 频繁生成废弃 API；许可条款历史不稳定
- **Rejection Reason**：引擎臃肿，包体过大，对 2D 像素项目大量 3D 功能冗余。API 面过大导致 AI 生成废弃 API

### Alternative D：Pygame
- **Description**：Python 生态最流行的游戏开发库，基于 SDL2
- **Pros**：Python 是 AI 训练数据最丰富的语言；快速原型极快
- **Cons**：不支持移动端（违反跨平台需求）；Python 无编译步骤，AI 错误只能运行时发现；性能瓶颈
- **Rejection Reason**：不支持移动端是硬伤，无编译期检查与 AI-First 范式冲突。**仅推荐用于原型验证**

### Alternative E：GameMaker / RPG Maker / LibGDX / Cocos2d-x
- **Rejection Reason**：
  - GameMaker：GML 语言天花板低，AI 训练数据极少
  - RPG Maker：D&D 5e 规则几乎不可实现，非代码驱动
  - LibGDX：iOS 支持不完善，Java + Android 跨域混淆
  - Cocos2d-x：社区衰退，维护停滞

## Consequences

### Positive
- AI 生成代码有确定性的编译期质量门禁（`dotnet build` 2-5s 反馈）
- 零跨域 API 混淆风险——AI 不可能混入 Unity/Godot API
- GoRogue 等 Roguelike 专用库可直接集成，无需自建 FOV/A*
- .NET 标准库（HttpClient、System.Text.Json、LINQ）零额外依赖
- 代码即场景——AI 覆盖 100% 开发工作
- 支持 Native AOT 编译，最小发布包 ~5-15MB
- 核心游戏逻辑可脱离 MonoGame 上下文进行纯单元测试

### Negative
- 无可视化编辑器，传统手写代码效率低于 Godot/Unity（在 AI-First 范式下此劣势减弱）
- 社区较小，中文资料极少——主要依赖官方英文文档和 GitHub 样例
- 需要自建 UI 系统（通过 Myra 框架缓解）
- MonoGame 更新缓慢（但 API 稳定性因此极高）
- 移动端适配需额外工作

### Risks
| Risk | Severity | Mitigation |
|------|----------|------------|
| 无编辑器导致开发效率低 | Medium | Nez ECS 减少样板代码；monogame-mcp 辅助 AI 开发；数据驱动设计减少手写量 |
| 社区小中文资料少 | Medium | AI 代码生成替代社区教程查找；关键问题追踪 GitHub Issues |
| GoRogue/Nez 停维护 | Low | 核心战斗/角色系统不依赖这些库；FOV/寻路可实现性低，备选方案多 |
| monogame-mcp 不成熟 | Medium | 加分项非必需品；AI 通过标准 C# 代码生成也能工作 |
| .NET 发布包体大 | Low | .NET 8 Native AOT 编译，最小发布包 5-15MB |

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| GDD-v1.md §1.2 | 2D 像素渲染（FF6 风格，16x24 精灵，32x32 Tileset） | MonoGame SpriteBatch + SamplerState.PointClamp 精确控制每像素渲染 |
| GDD-v1.md §1.2 | 回合制战斗（DND 5e 完整规则） | C# 强类型 + record 建模规则系统，编译器验证类型正确性 |
| GDD-v1.md §1.2 | LLM 集成（HTTP API + JSON 解析） | .NET 标准库 HttpClient + System.Text.Json，零第三方依赖 |
| GDD-v1.md §1.2 | 中文支持（像素风格中文字体） | FontStashSharp + Noto Sans CJK，动态字体渲染 |
| GDD-v1.md §1.2 | 本地持久化（SQLite 存档） | Microsoft.Data.Sqlite / sqlite-net，NuGet 直接集成 |
| All subsystems | AI-First 开发范式 | 编译期类型安全 + 零跨域混淆 + 代码即场景 = AI 适配度最高的引擎 |
| 02-overall-architecture.md §1.4 | 全局服务注册与初始化 | ServiceLocator 模式在 C#/.NET 中的自然实现 |
| 04-combat-system.md | GoRogue FOV/A* 集成 | MonoGame 生态中 GoRogue 直接可用，无需移植 |

## Performance Implications
- **CPU**：MonoGame 裸金属 2D 渲染，无 3D 管线开销，集成显卡 60fps 可达成
- **Memory**：.NET 8 运行时 + 游戏资源，目标 < 512MB（Native AOT 进一步减小）
- **Load Time**：MGCB 内容管线编译 + Scene 预加载策略，目标 < 3s 场景切换
- **Network**：HttpClient 异步 LLM API 调用，不阻塞主线程（async/await）

## Migration Plan
N/A — 这是根基决策，项目从零开始基于此技术栈构建。已有实现（Phase 0：11 C# 文件、1140 行代码）全部基于 MonoGame + 自定义 ECS。

**备选迁移路径**（如果 MonoGame 在开发中暴露出严重问题）：
- **Plan B → Godot C#**：数据层复用，逻辑层 C# 代码适配 Godot 节点系统；必须配合 Unity API 黑名单检测
- **Plan C → Unity 6**：数据层复用，逻辑层适配 GameObject/Component 架构；代价：包体增大、许可风险
- **Plan D → 混合方案**：Pygame 快速原型验证 + MonoGame 正式开发

## Validation Criteria
- `dotnet build` zero errors, zero warnings（`TreatWarningsAsErrors=true`）
- `dotnet test` 全绿（13 个现有 Core 测试无回归）
- `dotnet run` 启动显示 "你好酒馆" 主菜单
- 跨平台编译验证（Windows/macOS/Linux 至少两个平台通过）
- AI 生成的代码中零 Unity API 调用（`MonoBehaviour`、`GameObject` 等关键词在代码库中不存在）

## Related Decisions
- `docs/technical/01-engine-selection.md` — 完整的 9 引擎对比分析（853 行），此 ADR 的详细背景
- `docs/technical/02-overall-architecture.md` — 基于此决策的整体架构设计
- `design/gdd/GDD-v1.md` — 游戏设计规范，定义此 ADR 需满足的功能需求
- `design/gdd/02-llm-integration.md` — LLM 集成架构，依赖 HttpClient + System.Text.Json
- `design/gdd/04-combat-system.md` — 战斗引擎，依赖 GoRogue FOV/A*
- 待创建：ADR-0001（ECS 架构选型）、ADR-0002（服务注册模式）、ADR-0003（跨系统通信）、ADR-0004（LLM 集成架构）、ADR-0005（数据持久化方案）
