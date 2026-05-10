# ADR-0002：全局服务注册模式 — ServiceLocator

## Status
Accepted

## Date
2026-05-06

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | MonoGame 3.8.5+ |
| **Domain** | Core（服务注册与依赖管理基础架构） |
| **Knowledge Risk** | LOW — 纯 C# 实现，无外部 API 依赖 |
| **References Consulted** | `src/DndGame/Core/ServiceLocator.cs`（126 行）、`src/DndGame/Core/GameRoot.cs`（200 行）；`docs/technical/02-overall-architecture.md` §1.4 |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | `dotnet build` zero errors；13 个 Core 测试全绿（含 5 个 ServiceLocator 测试） |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0000（MonoGame）、ADR-0001（ECS — 服务在 GameRoot.Initialize 中注册，Scene 通过 ServiceLocator 访问服务） |
| **Enables** | ADR-0003（EventBus 通过 ServiceLocator 注册为第 0 优先级服务）、ADR-0004（LLMGateway 作为第 3 优先级服务）、ADR-0005（DataPersistence 作为第 2 优先级服务） |
| **Blocks** | 所有依赖全局服务的子系统实现 |
| **Ordering Note** | 必须与 ADR-0001 的 GameRoot.Initialize 管线同步 — 服务注册时序由架构文档 §1.4 定义 |

## Context

### Problem Statement
MonoGame 不提供内置的依赖注入容器。项目需要一种模式来管理 6 个全局服务（EventBus、GameStateManager、DataPersistence、LLMGateway、WorldStateManager、AudioManager），保证严格的初始化顺序（EventBus 必须最先注册），并允许所有 Scene 和 System 以一种简单、可测试的方式访问它们。

### Constraints
- MonoGame 的 `Game` 由框架实例化（`new GameRoot()`），不存在构造函数注入的自然入口 — MS DI 的 `GetRequiredService<T>()` 无处接入
- AI-First 范式：服务注册机制必须足够简单，AI 可完全理解并在上下文中正确使用（API 面 < 5 个方法）
- 零外部依赖原则（核心系统层级）：不应为服务注册引入 NuGet 包
- 初始化顺序是硬性约束（EventBus → GameStateManager → DataPersistence → LLMGateway → WorldStateManager → AudioManager → ResourceCache）
- 必须支持单元测试隔离（测试之间独立，互不污染）

### Requirements
- 必须支持接口到实现的类型安全注册（`Register<IEventBus>(new EventBus())`）
- 必须防止运行时意外修改（注册后冻结机制）
- 必须保证初始化顺序（手动注册天然保证）
- 必须支持测试中的清理（`Reset()`）
- 服务获取必须是 O(1) 时间复杂度

## Decision

**使用自定义静态 ServiceLocator 作为全局服务注册表，不使用 Microsoft.Extensions.DependencyInjection 或其他 DI 容器**。

### 架构示意图

```
┌─────────────────────────────────────────────────────────────────┐
│                  GameRoot.Initialize()                           │
│                                                                 │
│  ServiceLocator.Register<IEventBus>(new EventBus());         // 0│
│  ServiceLocator.Register<IGameStateManager>(new ...);         // 1│
│  ServiceLocator.Register<IDataPersistence>(new ...);          // 2│
│  ServiceLocator.Register<ILLMGateway>(new ...);               // 3│
│  ServiceLocator.Register<IWorldStateManager>(new ...);        // 4│
│  ServiceLocator.Register<IAudioManager>(new ...);             // 5│
│  ServiceLocator.Register<IResourceCache>(new ...);            // 6│
│                                                                 │
│  ServiceLocator.FinalizeRegistration();  // ← 冻结，后续 Register 抛异常│
└─────────────────────────────────────────────────────────────────┘
                              │
          ┌───────────────────┼───────────────────┐
          ▼                   ▼                   ▼
    CombatScene          TavernScene         AdventureScene
    │                   │                   │
    └─ ServiceLocator.Get<IEventBus>()      │
    └─ ServiceLocator.Get<ILLMGateway>()    │
    └─ ServiceLocator.Get<IResourceCache>() │
```

### 关键接口

```csharp
public static class ServiceLocator
{
    // 注册服务（冻结后抛 InvalidOperationException）
    public static void Register<TInterface>(TInterface instance) where TInterface : notnull;

    // 获取服务（未注册抛 InvalidOperationException）
    public static TInterface Get<TInterface>() where TInterface : notnull;

    // 安全获取（未注册返回 false）
    public static bool TryGet<TInterface>(out TInterface? service) where TInterface : notnull;

    // 冻结注册表
    public static void FinalizeRegistration();

    // 仅测试用 — 清空注册表
    public static void Reset();
}
```

### 线程安全设计

```csharp
// Register/FinalizeRegistration/Reset 使用显式锁同步
lock (_lock) { _services[type] = instance; }

// Get 无锁 — 注册表在 Initialize() 后冻结为只读，
// Dictionary 的并发读取在 .NET 中是线程安全的
public static TInterface Get<TInterface>()
{
    if (!_services.TryGetValue(typeof(TInterface), out var service))
        throw ...;
    return (TInterface)service;
}
```

### 初始化顺序契约

| 优先级 | 服务 | 接口 | 原因 |
|:---:|------|------|------|
| 0 | EventBus | IEventBus | 其他所有服务依赖它进行跨系统通信 |
| 1 | GameStateManager | IGameStateManager | 全局状态追踪、场景切换 |
| 2 | DataPersistence | IDataPersistence | sqlite-net + JSON 存取 |
| 3 | LLMGateway | ILLMGateway | LLM 请求唯一入口 |
| 4 | WorldStateManager | IWorldStateManager | 世界状态追踪 |
| 5 | AudioManager | IAudioManager | BGM/SFX 管理 |
| 6 | ResourceCache | IResourceCache | 预加载资源管理 |

### 为什么不是 MS DI

**关键判断：MonoGame 的 `Game` 由框架实例化，无构造函数注入入口。**

```csharp
// MS DI 的理想使用方式 — MonoGame 中不可行
var provider = services.BuildServiceProvider();
var game = provider.GetRequiredService<GameRoot>(); // GameRoot 需由框架创建
game.Run();  // ❌ 框架不认这种入口

// MonoGame 现实 — 框架控制入口
using var game = new GameRoot(); // 框架实例化
game.Run();
```

要在 MonoGame 中强行使用 MS DI，只能把容器暴露为静态属性——**这比直接使用 ServiceLocator 多一层间接，且失去了构造函数注入的所有优势**。

## Alternatives Considered

### Alternative A：Microsoft.Extensions.DependencyInjection
- **Description**：使用 .NET 标准 DI 容器（`ServiceCollection` → `BuildServiceProvider` → `GetRequiredService<T>`）
- **Pros**：行业标准；构造函数注入使依赖显式可见；内置 Singleton/Scoped/Transient 生命周期管理
- **Cons**：MonoGame `Game` 类由框架实例化，无构造函数注入入口——只能通过静态属性暴露容器，与 ServiceLocator 无异；引入 NuGet 依赖；API 面扩大（`ServiceCollection`/`ServiceProvider`/`ServiceLifetime` 等），增加 AI 的 API 混淆风险
- **Rejection Reason**：核心优势（构造函数注入）在 MonoGame 中无法发挥；实际使用模式与 ServiceLocator 等价但多一层抽象。对于 AI-First + MonoGame + 小团队的组合，这是引入复杂度而无收益

### Alternative B：手动构造函数注入
- **Description**：每个 Scene/System 在构造函数中显式声明所有依赖，无全局注册表
- **Pros**：依赖最显式；无全局状态；最易测试
- **Cons**：每个 Scene 都需手动传递 6+ 个服务引用；GameRoot 成为"服务分发器"，违反 SRP；切换服务实现需要修改所有构造函数调用点
- **Rejection Reason**：6 个全局服务 × 7 个 Scene = 42 处手动注入点。维护成本和出错概率过高

### Alternative C：C# 原生 event + 全局静态字段
- **Description**：每个服务声明为 `public static` 字段，不使用任何注册表抽象
- **Pros**：极简；零额外代码
- **Cons**：无法强制初始化顺序；无法冻结（运行时任何代码可修改静态字段）；测试清理困难（需手动还原每个字段）
- **Rejection Reason**：缺少冻结机制意味着 AI 可能在运行时意外覆盖服务引用——无法通过编译期检查防范

## Consequences

### Positive
- 零外部依赖 — 126 行纯 C#
- AI 可完全理解 — API 面极小（5 个方法），AI 生成代码时不会混淆
- 严格的初始化顺序保证 — 手动注册天然保证执行顺序
- 冻结机制 — `FinalizeRegistration()` 后运行时不可能修改注册表
- 测试友好 — `Reset()` 支持测试间完全隔离
- O(1) 服务查找 — `Dictionary` 直接索引，无 DI 容器的表达式树/委托开销
- 线程安全 — 读无锁（冻结后），写有锁保护

### Negative
- 服务依赖隐式 — 类的构造函数看不到依赖（`ServiceLocator.Get<T>()` 隐藏依赖），需通过命名约定和文档补偿
- 全局静态状态 — 单元测试需调用 `Reset()`，测试并行可能需额外同步（xUnit 默认串行，非问题）
- Service Locator 反模式争议 — 传统 OOP 中受批评，但在 MonoGame 生态中 `GameServiceContainer` 就是类似模式

### Risks
| Risk | Severity | Mitigation |
|------|----------|------------|
| 开发者忘记注册某服务 | Low | `Get<T>()` 立即抛 `InvalidOperationException`，游戏启动即崩溃，0 时延暴露 |
| 注册顺序错误 | Low | 目前仅 EventBus 有顺序依赖（优先级 0），其他服务互不依赖，风险面极小 |
| AI 在 `FinalizeRegistration()` 后尝试注册 | Low | 抛出明确异常 + 冻结状态可通过 `IsFinalized` 检查 |
| 测试并行时 `Reset()` 冲突 | Low | xUnit 默认串行运行测试；如需并行，加 `[Collection]` 特性即可 |

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| 02-overall-architecture.md §1.4 | 全局服务注册表（6 个服务 + 严格初始化顺序） | ServiceLocator + 手动注册管线，EventBus 为第 0 优先级 |
| 02-overall-architecture.md §1.2 | "接口优于实现"原则 | 所有服务通过接口注册（`Register<IEventBus>(new EventBus())`） |
| 03-vibe-coding-conventions.md | AI-First 开发范式 — 代码简单可验证 | 126 行实现，5 个方法 API，AI 完全可理解 |
| 03-vibe-coding-conventions.md | 禁止 `dynamic` 和 `object` 做参数类型 | `Register<T>` 和 `Get<T>` 使用泛型约束，编译期类型安全 |

## Performance Implications
- **CPU**：`Get<T>()` = 一次 `Dictionary.TryGetValue` + 一次类型转换 — < 20ns
- **Memory**：`Dictionary<Type, object>` 存储 7 个条目 — < 1KB
- **Load Time**：7 次 `Register` + `FinalizeRegistration` < 1μs

## Migration Plan
N/A — Phase 0 已实现，无需迁移。

**未来扩展**：如需按场景隔离服务（如每个 Scene 独立的资源缓存），可通过 `Scene` 级别的 `AddSceneComponent` 注册场景作用域服务，ServiceLocator 仅保留全局单例。

## Validation Criteria
- `dotnet build` zero errors
- `dotnet test` — ServiceLocator 5 个测试全绿（注册、获取、重复注册异常、冻结后注册异常、Reset）
- 运行时启动无 `InvalidOperationException`（所有 7 个服务已注册）
- `FinalizeRegistration()` 后任何 `Register()` 调用抛异常

## Related Decisions
- ADR-0000 — MonoGame 引擎选型（ServiceLocator 运行在 .NET 环境中）
- ADR-0001 — ECS 架构（服务在 GameRoot.Initialize 中注册，Scene 通过 ServiceLocator 访问）
- ADR-0003 — IEventBus（EventBus 是第 0 优先级注册服务）
- `docs/technical/02-overall-architecture.md` §1.4 — 全局服务注册表原始设计
- `src/DndGame/Core/ServiceLocator.cs` — 当前实现
