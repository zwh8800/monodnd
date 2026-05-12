# Technical Preferences

<!-- Populated by /setup-engine + manual configuration. -->
<!-- All agents reference this file for project-specific standards and conventions. -->

## Engine & Language

- **Engine**: MonoGame 3.8.x 稳定版（当前 3.8.4.1，NuGet `3.8.*` 通配符自动跟踪）
- **Language**: C# 12（.NET 8+）
- **Rendering**: MonoGame SpriteBatch（2D 像素渲染，SamplerState.PointClamp）
- **Physics**: 不适用（回合制 RPG，无实时物理需求）

## Input & Platform

- **Target Platforms**: PC（Windows/macOS/Linux）
- **Input Methods**: Keyboard/Mouse, Gamepad（部分支持）
- **Primary Input**: Keyboard/Mouse
- **Gamepad Support**: Partial — UI 导航支持方向键，战斗操作支持快捷键
- **Touch Support**: None — 当前不计划移动端
- **Platform Notes**: DesktopGL 跨平台桌面，无触摸交互需求

## Naming Conventions

- **Classes**: PascalCase（如 `CombatEngine`）
- **Interfaces**: IPascalCase（如 `IEventBus`）
- **Private Fields**: `_camelCase`（如 `_currentHealth`）
- **Public Properties**: PascalCase（如 `MaxHealth`）
- **Methods**: PascalCase（如 `TakeDamage()`）
- **Constants**: UPPER_SNAKE_CASE（如 `MAX_HEALTH`）
- **Files**: PascalCase matching class（如 `CombatEngine.cs`）
- **Enums**: PascalCase values（如 `SceneId.MainMenu`）

## Performance Budgets

- **Target Framerate**: 60fps（集成显卡可达成）
- **Frame Budget**: 16.6ms
- **Draw Calls**: 2D SpriteBatch 批量渲染，目标 < 500/帧
- **Memory Ceiling**: < 512MB（.NET 8 运行时 + 游戏资源）

## Testing

- **Framework**: xUnit + FluentAssertions
- **Test Naming**: `MethodName_Scenario_ExpectedResult`（AAA 模式：Arrange/Act/Assert）
- **Minimum Coverage**: 未强制，但所有战斗/规则/骰子系统必须有单元测试
- **Required Tests**: 战斗引擎（xUnit 必须）、骰子系统（xUnit 必须）、角色系统（xUnit 必须）、冒险实例化（xUnit 必须）、LLM Gateway（集成测试 + 降级/回退测试 必须）

## Forbidden Patterns

<!-- 项目代码库中绝不应出现的模式 -->
- `dynamic` — 放弃编译期类型检查
- `object` 作为参数类型 — 使用泛型或接口
- `#pragma warning disable` — 修复代码，不要抑制警告
- `async void` — 始终使用 `async Task`
- Unity API（`MonoBehaviour`、`GameObject`、`Instantiate`、`Transform`）— 使用自定义 ECS/MonoGame 等效物
- Nez NuGet 包 — 项目使用自定义 ECS（ADR-0001）
- 硬编码路径（如 `"Content/textures/x.png"`）— 使用 `ContentManager.Load`
- 硬编码数值 — 放入 JSON 配置或命名常量
- 缺少 XML 文档注释的公开 API — 所有 public/protected 符号必须有 `/// <summary>`（简体中文）
- LLM 决定数值/战斗结果/故事分支 — 程序控制这些（LLM = 皮肤层）

## Allowed Libraries / Addons

<!-- 按实际安装的依赖记录，不添加推测性依赖 -->
- **MonoGame.Framework.DesktopGL** `3.8.*` — 核心框架
- **MonoGame.Content.Builder.Task** `3.8.*` — 内容构建
- **GoRogue** `2.6.4` — FOV、A*、地图生成
- **Myra** `1.5.*` — UI 框架（代码布局优先）
- **FontStashSharp.MonoGame** `1.5.*` — 动态字体渲染（Noto Sans CJK）
- **JsonSchema.Net** `7.*` — LLM 输出 Schema 验证
- **sqlite-net-pcl** `1.9.*` + **SQLitePCLRaw.bundle_green** `2.1.*` — 数据持久化
- **MonoGame.Extended** `6.0.0` — 扩展功能（纹理图集、Tiled 地图等）

## Architecture Decisions Log

<!-- 快速引用，完整 ADR 在 docs/architecture/ -->

| ADR | 标题 | 状态 |
|-----|------|------|
| ADR-0000 | 游戏框架与编程语言选型 — MonoGame 3.8.x / C# 12 / .NET 8 | Accepted |
| ADR-0001 | ECS 架构选型 — 自定义 Scene/Entity/Component | Accepted |
| ADR-0002 | 服务注册模式 — ServiceLocator | Accepted |
| ADR-0003 | 跨系统通信 — IEventBus 事件总线 | Accepted |
| ADR-0004 | LLM 集成架构 | Accepted |
| ADR-0005 | 数据持久化方案 | Accepted |

## Engine Specialists

<!-- 本项目使用 MonoGame，CCGS 框架中无专属 monogame-specialist -->
<!-- 以下映射基于 MonoGame C# 项目的实际需求 -->

- **Primary**: lead-programmer（架构决策、代码审查、跨系统协调）
- **Language/Code Specialist**: lead-programmer（C# 代码审查 — primary 覆盖）
- **Shader Specialist**: technical-artist（Shader/VFX 开发，项目中极少使用）
- **UI Specialist**: ui-programmer（Myra UI 实现、屏幕流、数据绑定）
- **Additional Specialists**: engine-programmer（核心引擎/渲染管线）、gameplay-programmer（游戏机制/战斗/角色）、ai-programmer（AI/行为树/寻路）
- **Routing Notes**: 架构审查和 C# 代码审查 invoke primary（lead-programmer）。游戏机制实现 invoke gameplay-programmer。引擎系统 invoke engine-programmer。UI 实现 invoke ui-programmer。本项目无专用 MonoGame specialist 代理，架构审查和代码质量由 lead-programmer 负责。

### File Extension Routing

| File Extension / Type | Specialist to Spawn |
|-----------------------|---------------------|
| Game code (.cs files) | lead-programmer（审查）/ gameplay-programmer（实现） |
| Shader / material files (.fx, .fxh) | technical-artist |
| UI / screen files (Myra .cs) | ui-programmer |
| Content pipeline (.mgcb) | engine-programmer |
| Native extension / plugin files (.dll, native) | engine-programmer |
| General architecture review | lead-programmer |
