# ADR-0001：ECS 架构选型 — 自定义轻量 Scene/Entity/Component

## Status
Accepted

## Date
2026-05-06

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | MonoGame 3.8.5+ |
| **Domain** | Core（场景管理、实体组合基础架构） |
| **Knowledge Risk** | LOW — 自定义 ECS 为 MonoGame 原生 C# 实现，无外部 API 依赖 |
| **References Consulted** | `src/DndGame/Core/Scene.cs`（146 行）、`Entity.cs`（158 行）、`Component.cs`（55 行）、`SceneComponent.cs`（56 行）、`GameRoot.cs`（200 行）；`docs/technical/02-overall-architecture.md` §2.1 |
| **Post-Cutoff APIs Used** | None — 仅依赖 MonoGame `Game` 基类、`GameTime`、`SpriteBatch` |
| **Verification Required** | `dotnet build` zero errors；13 个现有 Core 测试全绿；场景切换（MainMenuScene → 任意 Scene）无崩溃 |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0000（MonoGame 引擎选型） |
| **Enables** | ADR-0002（ServiceLocator 服务注册）、ADR-0003（IEventBus 跨系统通信）—— ECS 是这些服务运行的宿主架构 |
| **Blocks** | 所有 Scene 子类实现（MainMenuScene、TavernScene、AdventureScene、CombatScene 等） |
| **Ordering Note** | 必须在任何 Scene 子类实现前 Accepted |

## Context

### Problem Statement
项目需要一个组织游戏对象和场景的架构模式。原始设计文档（`02-overall-architecture.md` §2.1）推荐使用 Nez 框架的 Scene/Entity/Component 体系，但 Phase 0 实际实现了一套自定义轻量 ECS（`Scene`、`Entity`、`Component`、`SceneComponent` 四个类，共 415 行）。需要正式决策：**保持并完善自定义 ECS，还是迁移到 Nez**。

### Constraints
- AI-First 开发范式：ECS 的实现代码同样由 AI 生成，保持简单可理解至关重要
- 项目已 Phase 0 完成：自定义 ECS 已实现且通过 13 个单元测试——迁移到 Nez 有回归风险
- 不需要 Nez 的高级功能（FSM/行为树/GOAP）——这些对于 2D 回合制 Roguelike 是过度设计
- 自定义 ECS 零外部依赖——减少 NuGet 依赖链，降低 AI 的 API 混淆风险
- 性能目标：2D 像素游戏，场景实体数预期 < 200

### Requirements
- 必须支持场景切换（MainMenu ↔ Tavern ↔ Adventure ↔ Combat）
- 必须支持实体-组件组合模式（Entity.AddComponent）
- 必须支持场景级系统（SceneComponent，如渲染器、战斗引擎）
- 必须与 ServiceLocator/IEventBus 无缝集成
- 必须可脱离 MonoGame 渲染上下文进行单元测试

## Decision

**保持并完善当前自定义轻量 Scene/Entity/Component 实现，不引入 Nez 框架**。

### 架构示意图

```
┌────────────────────────────────────────────────────────────────┐
│                     GameRoot (单例)                              │
│  · 管理当前 Scene 引用 (_currentScene)                            │
│  · 场景切换队列 (_nextScene)                                      │
│  · 全局 SpriteBatch 共享                                          │
│  · Update(){ _currentScene?.Update(gameTime); }                  │
│  · Draw(){ _currentScene?.Draw(gameTime); }                     │
└──────────────────────┬─────────────────────────────────────────┘
                       │ 持有
                       ▼
┌────────────────────────────────────────────────────────────────┐
│                      Scene (抽象基类)                             │
│  · List<Entity> _entities                                       │
│  · List<SceneComponent> _sceneComponents                         │
│  · CreateEntity(name) → Entity                                  │
│  · AddSceneComponent<T>(T) → T                                  │
│  · Initialize() / Begin() / Update() / Draw() / End()           │
└──────┬──────────────────────────────┬───────────────────────────┘
       │ 包含                          │ 包含
       ▼                              ▼
┌──────────────────┐    ┌──────────────────────────────────┐
│  Entity           │    │  SceneComponent (抽象基类)         │
│  · string Name    │    │  · Scene? Scene                  │
│  · Vector2 Pos    │    │  · bool Enabled                  │
│  · bool Enabled   │    │  · OnAddedToScene()              │
│  · AddComponent() │    │  · Update() / Draw()              │
│  · GetComponent() │    └──────────────────────────────────┘
│  · Destroy()      │
└──────┬───────────┘
       │ 包含
       ▼
┌──────────────────────────────────────────────────────────┐
│  Component (抽象基类)                                       │
│  · Entity? Entity                                         │
│  · bool Enabled                                           │
│  · OnAddedToEntity() / OnRemovedFromEntity()               │
│  · Update() / Draw()                                      │
└──────────────────────────────────────────────────────────┘
```

### 场景生命周期

```
Scene 创建 → Initialize() → Begin() → [Update() → Draw()] × N → End()
                  ↑ 初始化实体/系统    ↑ 每帧循环               ↑ 切换/销毁
```

### 场景切换流程

```csharp
// GameRoot.Update() 中的延迟切换机制
if (_nextScene != null)
{
    _currentScene?.End();       // 1. 清理旧场景
    _currentScene = _nextScene;  // 2. 切换引用
    _nextScene = null;           // 3. 清除队列
    _currentScene.Initialize();  // 4. 初始化新场景
    _currentScene.Begin();       // 5. 触发 Begin
}
// 优势：切换发生在帧边界，保证当前帧完整渲染
```

### 关键接口

```csharp
// Scene — 场景基类（146 行）
public abstract class Scene
{
    public Entity CreateEntity(string name);
    public T AddSceneComponent<T>(T component) where T : SceneComponent;
    public virtual void Initialize();
    public virtual void Begin();
    public virtual void Update(GameTime gameTime);
    public virtual void Draw(GameTime gameTime);
    public virtual void End();
}

// Entity — 实体组合（158 行）
public class Entity
{
    public string Name { get; }
    public Scene Scene { get; }
    public Vector2 Position { get; set; }
    public bool Enabled { get; set; }
    public Entity AddComponent(Component component);    // 链式调用
    public T? GetComponent<T>() where T : Component;
    public void Destroy();
}

// Component — 实体组件（55 行）
public abstract class Component
{
    public Entity? Entity { get; }
    public bool Enabled { get; set; }
    public virtual void OnAddedToEntity(Entity entity);
    public virtual void OnRemovedFromEntity();
    public virtual void Update(GameTime gameTime);
    public virtual void Draw(GameTime gameTime);
}

// SceneComponent — 场景级组件（56 行）
// 用于 CombatEngine、TavernBackgroundRenderer 等场景级系统
public abstract class SceneComponent
{
    public Scene? Scene { get; }
    public virtual void OnAddedToScene(Scene scene);
    public virtual void Update(GameTime gameTime);
    public virtual void Draw(GameTime gameTime);
}
```

### 与设计文档的偏离

原始文档推荐 `scene.CreateEntity("name")` 语法（模仿 Nez），实际实现采用了相同的 API 签名——**行为兼容但实现独立**。如果未来发现自定义 ECS 无法满足需求，可平滑迁移到 Nez（API 面一致）。

## Alternatives Considered

### Alternative A：Nez 框架
- **Description**：使用 Nez 的完整 Scene/Entity/Component 架构，附带 FSM、行为树、GOAP、后处理、物理等子系统
- **Pros**：成熟稳定、功能丰富；内置场景过渡效果（FadeTransition 等）；社区验证过的 ECS 架构
- **Cons**：引入额外 NuGet 依赖；大量不需要的功能（2D 回合制不需要 GOAP/物理）；AI 训练数据中 Nez 的占比远小于 MonoGame，增加 API 混淆风险；迁移现有 1140 行代码和 13 个测试有回归风险
- **Rejection Reason**：对 2D 回合制 Roguelike 过度设计。自定义 ECS 415 行即可满足需求，Nez 引入的复杂度（FSM/行为树/GOAP）对本项目是负担而非增益。且 AI 在 Nez 上的训练数据不足，引入后反向增加 API 混淆风险

### Alternative B：完全无 ECS — 直接在 Scene 中管理
- **Description**：每个 Scene 子类直接管理自己的实体列表和渲染逻辑，无 Entity/Component 抽象
- **Pros**：极简，零抽象层；适合实体种类少、逻辑简单的场景
- **Cons**：代码重复严重（每个 Scene 都要写实体管理、启用/禁用、遍历渲染）；无法复用组件（如 SpriteRenderer 每 Scene 重写）；违反 SRP
- **Rejection Reason**：项目有 7+ Scene 子类、10+ 实体类型、20+ 组件类型——无 ECS 将导致大量重复代码和测试成本

### Alternative C：GoRogue 内置 ECS
- **Description**：使用 GoRogue 的 `IGameObject`/`IComponent` 体系
- **Pros**：与 GoRogue FOV/A* 深度集成；减少自定义代码量
- **Cons**：GoRogue 的 ECS 设计面向网格 Roguelike（坐标绑定强耦合）；不适用于非网格场景（酒馆 UI、对话）；与 Scene 切换生命周期不匹配
- **Rejection Reason**：GoRogue ECS 局限于网格空间，无法覆盖酒馆 UI、对话等非网格场景。自定义 ECS 提供统一的场景/实体/组件抽象

## Consequences

### Positive
- 零外部依赖——自定义 ECS 仅依赖 `Microsoft.Xna.Framework`（MonoGame 基础库）
- 极轻量——4 个类 415 行代码，AI 可完全理解并在上下文中精确生成
- 完全控制——可针对项目需求定制（如 SceneComponent 的场景级系统概念）
- API 面与 Nez 行为兼容（`CreateEntity`/`AddComponent`）——未来迁移成本低
- 已通过 13 个单元测试验证——场景创建、实体组合、组件生命周期均测试覆盖
- 场景切换延迟机制（`_nextScene` 队列）保证帧边界安全性

### Negative
- 需要自行维护和扩展 ECS（但代码量仅 415 行，维护成本低）
- 缺少 Nez 的场景过渡动画（`FadeTransition` 等）——需自建（但可后续按需添加）
- 缺少 Nez 的 Entity 处理系统（EntityProcessor）——对 2D 回合制非必要
- 没有可视化编辑器或场景调试工具

### Risks
| Risk | Severity | Mitigation |
|------|----------|------------|
| ECS 过于简陋，无法支持复杂场景需求 | Low | 当前需求已明确（7 Scene、<200 实体/场景），415 行代码覆盖全部；如需扩展，代码量可控 |
| 性能瓶颈（遍历所有实体/组件） | Low | 2D 像素游戏实体数 < 200，线性遍历性能充足；可后续添加组件索引 |
| 与 Nez 行为兼容但细节不一致 | Low | 使用相同的 API 签名（`CreateEntity`/`AddComponent`），如迁移只需替换基类 |

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| GDD-v1.md §1.2 | 场景管理（主菜单 → 酒馆 → 冒险 → 战斗） | Scene 基类 + GameRoot 延迟切换机制实现 7 场景切换 |
| 02-overall-architecture.md §2.1 | Nez Scene 子系统（Scene/Entity/Component 模式） | 自定义 ECS 与 Nez API 行为兼容，满足所有场景管理需求 |
| 02-overall-architecture.md §2.1.5 | 预加载策略（ResourceCache + SceneChanging 事件） | SceneComponent 提供场景级系统挂载点，ResourceCache 可注册为 SceneComponent |
| 02-overall-architecture.md §2.2 | CombatEngine 作为 AddSceneComponent 挂载 | SceneComponent 抽象支持战斗引擎作为场景级系统运行 |
| 04-combat-system.md | GoRogue 地图实体集成 | Entity + Position 支持 GoRogue 坐标映射；自定义 Entity 可持有 GoRogue 数据 |
| 07-tavern-system.md | 酒馆 NPC 实体（招募板、酒保、轮换 NPC） | Entity + AddComponent 组合模式（NPCDialogueComponent + SpriteRenderer + InteractableComponent） |

## Performance Implications
- **CPU**：每帧遍历 < 200 Entity × 3-5 Component = < 1000 次虚方法调用。2D 像素游戏 60fps 下此开销 < 0.1ms
- **Memory**：Entity（~100 bytes）+ Component（~50 bytes）× 200 = ~30KB 内存占用
- **Load Time**：Scene 创建开销忽略不计（无资源加载）；实际开销在 Texture2D/Font 加载
- **Network**：N/A

## Migration Plan
N/A — 此 ADR 确认保持现有实现。项目从 Phase 0 起即基于自定义 ECS，无需迁移。

**未来迁移路径**（如果自定义 ECS 无法满足需求）：
- **迁移到 Nez**：API 面行为兼容（`CreateEntity`/`AddComponent`），仅需更换基类引用和 NuGet 包。预计迁移成本 < 2 天
- **扩展自定义 ECS**：按需添加 EntityProcessor、场景过渡、组件索引等（当前 415 行，扩展余地充足）

## Validation Criteria
- `dotnet build` zero errors, zero warnings
- `dotnet test` 全绿（13 个现有 Core 测试覆盖：ServiceLocator 5 个 + EventBus 8 个）
- 场景创建 + 实体组合流程测试通过（至少 1 个 Scene 子类 + 1 个 Entity + 1 个 Component 集成测试）
- 场景切换无崩溃（MainMenuScene → 目标 Scene → 返回 MainMenuScene）

## Related Decisions
- ADR-0000 — MonoGame 引擎选型（ECS 必须运行在 MonoGame 之上）
- ADR-0002 — ServiceLocator 服务注册模式（服务在 GameRoot.Initialize 中注册，Scene 通过 ServiceLocator.Get 访问）
- ADR-0003 — IEventBus 跨系统通信（SceneComponent 之间通过 EventBus 解耦）
- `docs/technical/02-overall-architecture.md` §2.1 — 场景管理原始设计（Nez 推荐方案）
- `src/DndGame/Core/Scene.cs` `Entity.cs` `Component.cs` `SceneComponent.cs` — 当前实现
