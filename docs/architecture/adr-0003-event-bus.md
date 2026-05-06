# ADR-0003：跨系统通信模式 — IEventBus + Snapshot-then-Invoke

## Status
Proposed

## Date
2026-05-06

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | MonoGame 3.8.5+ |
| **Domain** | Core（系统间解耦通信基础架构） |
| **Knowledge Risk** | LOW — 纯 C# 实现，基于 `Delegate.Combine`/`Delegate.Remove`，无外部 API |
| **References Consulted** | `src/DndGame/Core/EventBus.cs`（136 行）；`docs/technical/02-overall-architecture.md` §1.2、§1.4；`docs/subsystems/02-llm-integration.md`、`docs/subsystems/04-combat-system.md` |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | `dotnet build` zero errors；8 个 EventBus 测试全绿；Snapshot-then-Invoke 无死锁 |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0000（MonoGame）、ADR-0001（ECS）、ADR-0002（ServiceLocator — EventBus 作为第 0 优先级服务注册） |
| **Enables** | ADR-0004（LLM Gateway 通过 EventBus 发送叙事就绪事件）、所有子系统（CombatEngine ↔ CharacterSystem ↔ TavernSystem 通过 EventBus 解耦） |
| **Blocks** | 所有跨系统通信实现 |
| **Ordering Note** | EventBus 必须最先注册（ServiceLocator 初始化顺序第 0 位），因为其他所有服务都依赖它 |

## Context

### Problem Statement
项目有 7+ 子系统（战斗引擎、角色系统、酒馆系统、冒险生成器、世界状态、物品系统、LLM Gateway），它们之间需要通信但不能直接引用对方的具体类——这会产生强耦合，使单元测试无法隔离，也违反 AI-First 开发范式（AI 生成的代码不应"猜测"其他系统的 API）。

需要一种机制允许系统 A 发布事件（如"角色死亡"、"战斗结束"、"冒险完成"），系统 B、C、D 订阅并响应——**而 A 不知道 B、C、D 的存在**。

### Constraints
- 禁止系统间直接引用（如 `CombatEngine` 中不能有 `CharacterSystem` 的 `using`）
- AI-First 范式：事件系统代码需简单到 AI 可完全理解并生成正确的订阅/发布代码
- 零外部依赖（核心系统层级）
- 必须线程安全——事件可能由 LLM 异步回调线程发布，但处理程序在主线程执行
- 必须防止发布-订阅过程中的死锁（处理程序内部可能订阅/取消订阅其他事件）
- 性能约束：事件发布 < 10μs（不含处理程序执行时间）

### Requirements
- 必须支持类型安全的事件发布/订阅（泛型约束）
- 必须支持同一事件类型的多个订阅者
- 必须支持安全的取消订阅（处理程序被移除后不再被调用）
- 必须防止死锁（处理程序回调期间不持有锁）
- 必须可脱离 MonoGame 渲染上下文进行单元测试

## Decision

**使用自定义 IEventBus 接口 + EventBus 实现，采用 Snapshot-then-Invoke（快照-然后调用）模式**。

### 架构示意图

```
┌──────────────┐     Publish(CharacterDied)     ┌──────────────┐
│ CombatEngine │ ─────────────────────────────▶ │   EventBus   │
│              │                                 │              │
└──────────────┘                                 │ _handlers:   │
                                                 │  Dict<Type,  │
┌──────────────┐     Subscribe<CharacterDied>    │   Delegate>  │
│ TavernSystem │ ◀───────────────────────────── │              │
│              │     handler.Invoke(evt)          └──────┬───────┘
└──────────────┘                                        │
                                                        │
┌──────────────┐     Subscribe<CharacterDied>           │
│ Settlement   │ ◀──────────────────────────────────────┘
│ System       │     handler.Invoke(evt)
└──────────────┘

关键特性：
  · CombatEngine 不知道 TavernSystem/SettlementSystem 的存在
  · 新增订阅者无需修改 CombatEngine
  · 所有事件类型通过泛型约束，编译期类型安全
```

### Snapshot-then-Invoke 模式（核心设计）

```csharp
public void Publish<TEvent>(TEvent evt) where TEvent : notnull
{
    Delegate? handlers;
    lock (_lock)
    {
        // 1. 在锁内获取当前委托快照
        _handlers.TryGetValue(typeof(TEvent), out handlers);
    }
    // 2. 锁已释放！在锁外调用处理程序

    if (handlers is Action<TEvent> action)
    {
        action.Invoke(evt);
    }
}

// 为什么必须在锁外调用？三大原因：
//
// 1. 死锁预防：
//    处理程序内部可能 Publish 其他事件或 Subscribe/Unsubscribe，
//    如果持有锁时执行这些操作，可能导致锁的重入问题
//
// 2. 减少锁争用：
//    长时间运行的处理程序（如 LLM API 调用回调）不会阻塞
//    其他线程的 Subscribe/Unsubscribe 操作
//
// 3. 快照语义：
//    本次 Publish 使用调用时的委托快照，
//    Publish 期间的新增订阅/取消订阅不影响当前执行
//    下一轮 Publish 才会看到变更
```

### 关键接口

```csharp
public interface IEventBus
{
    // 订阅事件 — 每次发布时，handler 按订阅顺序依次调用
    void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : notnull;

    // 取消订阅 — 移除指定 handler，空委托链时清理字典条目
    void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : notnull;

    // 发布事件 — Snapshot-then-Invoke 模式
    void Publish<TEvent>(TEvent evt) where TEvent : notnull;
}
```

### 预期事件类型（设计阶段定义）

```csharp
// 战斗系统事件
public record CharacterDied(string CharacterId, string KillerId, DamageType FinalBlow);
public record CombatEnded(string CombatId, CombatResult Result, List<string> Survivors);
public record DamageDealt(string TargetId, int Amount, DamageType Type, bool IsCritical);

// 角色系统事件
public record CharacterCreated(string CharacterId, string RaceId, string ClassId);
public record CharacterLeveledUp(string CharacterId, int OldLevel, int NewLevel);
public record RelationshipChanged(string CharA, string CharB, int Delta, int NewValue);

// 酒馆系统事件
public record TavernEventTriggered(string EventId, MechanicalResult Result);
public record AdventurerRecruited(string CharacterId, int RecruitmentFee);
public record FacilityUpgraded(string FacilityId, int OldLevel, int NewLevel);

// 冒险系统事件
public record AdventureStarted(string AdventureId, string BlueprintId);
public record AdventureCompleted(string AdventureId, AdventureOutcome Outcome);
public record NodeEntered(string NodeId, NodeType Type);

// LLM 事件
public record NarrativeReady(string RequestId, NarrativeText Text);
public record LLMFallbackTriggered(string AgentType, string Reason);
```

### 订阅生命周期管理

```csharp
// ✅ 正确模式 — OnAddedToEntity 订阅，OnRemovedFromEntity 取消订阅
public class CombatHudComponent : Component
{
    public override void OnAddedToEntity(Entity entity)
    {
        base.OnAddedToEntity(entity);
        ServiceLocator.Get<IEventBus>().Subscribe<DamageDealt>(OnDamageDealt);
    }

    public override void OnRemovedFromEntity()
    {
        ServiceLocator.Get<IEventBus>().Unsubscribe<DamageDealt>(OnDamageDealt);
        base.OnRemovedFromEntity();
    }

    private void OnDamageDealt(DamageDealt evt) { /* 显示伤害数字 */ }
}

// ❌ 错误模式 — 忘记取消订阅导致内存泄漏和处理幽灵调用
public class LeakyComponent : Component
{
    public override void OnAddedToEntity(Entity entity)
    {
        ServiceLocator.Get<IEventBus>().Subscribe<DamageDealt>(OnDamageDealt);
        // 缺少 OnRemovedFromEntity 中的 Unsubscribe！
    }
}
```

## Alternatives Considered

### Alternative A：C# 原生 event 关键字
- **Description**：每个系统暴露 `public event Action<TEventArgs>` 字段，其他系统直接 `+=` 订阅
- **Pros**：C# 语言原生支持，零额外代码
- **Cons**：需要直接引用发布者的具体类（`combatEngine.CharacterDied += handler`），违反解耦原则；无法集中管理事件路由；无法防止内存泄漏（`event` 持有订阅者强引用）
- **Rejection Reason**：直接引用发布者产生强耦合，与"系统间禁止直接引用"的核心约束冲突

### Alternative B：MediatR / 消息中介库
- **Description**：使用 MediatR 的 `INotification`/`INotificationHandler` 模式
- **Pros**：成熟库，支持 Pipeline Behavior（日志、验证等）；社区验证的 CQRS 模式
- **Cons**：引入 NuGet 依赖；API 面大（`IRequest`/`IRequestHandler`/`INotification`/`INotificationHandler`/`IPipelineBehavior`），增加 AI 混淆风险；对 2D 游戏过度设计（这些模式设计给企业级 CQRS 系统）
- **Rejection Reason**：136 行 EventBus 即可满足需求，MediatR 的 Pipeline/Mediator 模式对游戏开发是过度设计且引入不必要的外部依赖

### Alternative C：GoRogue 内置消息系统
- **Description**：使用 GoRogue 的 `MessageBus`
- **Pros**：与 GoRogue 生态系统集成
- **Cons**：GoRogue 的 MessageBus 面向网格/地图事件，不适合跨系统叙事事件（如 CharacterCreated、TavernEventTriggered）；类型安全较弱
- **Rejection Reason**：GoRogue 的事件系统专为 Roguelike 网格场景设计，无法覆盖酒馆 UI、角色创建、LLM 异步回调等非网格事件

## Consequences

### Positive
- 完全解耦 — 发布者不知道订阅者的存在，新增事件消费者无需修改发布者代码
- 零外部依赖 — 136 行纯 C#
- 线程安全 — `lock` 保护写操作，快照模式保护读操作
- 死锁防护 — Snapshot-then-Invoke 确保发布时不持有锁
- 编译期类型安全 — `where TEvent : notnull` 泛型约束
- 内存安全 — 实体销毁时 `OnRemovedFromEntity` 清理订阅，防止幽灵调用
- 可测试 — 订阅/发布逻辑可脱离 MonoGame 渲染上下文进行纯单元测试

### Negative
- 隐式依赖 — 订阅者依赖特定事件类型但不在构造函数中声明，需通过文档和命名约定补偿
- 事件链调试困难 — 事件 A → 事件 B → 事件 C 的调用链在调试器中不可直接追踪（可通过事件日志缓解）
- 订阅生命周期需手动管理 — 忘记取消订阅导致内存泄漏（但 `OnRemovedFromEntity` 模式标准化后可缓解）

### Risks
| Risk | Severity | Mitigation |
|------|----------|------------|
| 处理程序中抛出未捕获异常导致后续订阅者不被调用 | Medium | `Delegate.Combine` 的多播委托按顺序调用，异常会中断后续调用。缓解：处理程序内部 try-catch；或在 EventBus 层面包装 try-catch（权衡：静默吞异常 vs 中断调用） |
| 事件类型爆炸 — 随着系统增多，事件类型膨胀 | Low | 使用 `record` 定义事件（轻量），按子系统命名前缀分组（CombatXxx、CharacterXxx、TavernXxx） |
| Snapshot 语义导致"丢失"最新订阅 | Low | 快照仅在当前 Publish 调用期间有效，下一轮 Publish 即使用最新订阅列表——延迟 < 1 帧（~16ms） |

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| 02-overall-architecture.md §1.2 | "接口优于实现，系统间通过 IEventBus 解耦" | IEventBus 接口 + 泛型事件类型，发布者零依赖订阅者 |
| 02-overall-architecture.md §1.4 | EventBus 为第 0 优先级服务 | ServiceLocator 初始化顺序保证 EventBus 最先注册 |
| 03-vibe-coding-conventions.md | 禁止系统间直接引用 | EventBus 替代直接引用，系统仅依赖 IEventBus 接口和事件 record 类型 |
| 02-llm-integration.md | LLM API 异步回调传递叙事结果 | NarrativeReady 事件将异步 LLM 结果通知主线程 |
| 04-combat-system.md | 战斗日志 → DM Agent 叙事 | CombatLog 通过 EventBus 发布，DM Agent 订阅生成叙事 |
| 07-tavern-system.md | 酒馆事件触发多系统响应 | TavernEventTriggered 事件通知角色/物品/世界状态系统 |

## Performance Implications
- **CPU**：`Publish()` — 1 次 `TryGetValue` + 1 次 `Delegate` 类型转换 + N 次 handler 调用（handler 耗时取决于具体实现）。EventBus 本身开销 < 100ns
- **Memory**：`Dictionary<Type, Delegate>` 存储 ~20 事件类型 × ~3 订阅者 = ~5KB
- **Load Time**：EventBus 初始化 = `new EventBus()` — < 1μs

## Migration Plan
N/A — Phase 0 已实现，8 个单元测试覆盖。

## Validation Criteria
- `dotnet build` zero errors
- `dotnet test` — 8 个 EventBus 测试全绿（Subscribe/Publish/Unsubscribe/MultipleHandlers/SnapshotSafety/DeadlockPrevention）
- Publish 期间 Subscribe/Unsubscribe 无死锁验证
- 处理程序中抛异常不导致 EventBus 内部状态损坏

## Related Decisions
- ADR-0000 — MonoGame 引擎选型（EventBus 运行在 .NET 环境中）
- ADR-0001 — ECS 架构（实体生命周期管理订阅/取消订阅）
- ADR-0002 — ServiceLocator（EventBus 通过 ServiceLocator 获取）
- ADR-0004 — LLM 皮肤层（LLM 结果通过 NarrativeReady 事件发布）
- `docs/technical/02-overall-architecture.md` §1.2、§1.4
- `src/DndGame/Core/EventBus.cs` — 当前实现
