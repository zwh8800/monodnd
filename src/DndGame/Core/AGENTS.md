# Core 框架

自定义 ECS（Entity-Component-System）框架 + 游戏基础设施。不依赖 Nez——全部手写。

## 结构

```
Core/
├── GameRoot.cs              # 主 Game 类：游戏循环、场景管理、SpriteBatch、UI
├── Scene.cs                 # 自定义场景基类：Entity 容器、SceneComponent、生命周期
├── Entity.cs                # 可组合实体：AddComponent<T>()、GetComponent<T>()
├── Component.cs             # 组件基类：OnAddedToEntity/OnRemovedFromEntity 回调
├── SceneComponent.cs        # 场景级系统组件（无 Owner Entity）
├── ServiceLocator.cs        # 全局服务注册表，线程安全
├── EventBus.cs              # IEventBus + 实现，快照后调用（snapshot-then-invoke）
├── GameStateManager.cs      # SceneId 枚举 + 状态追踪
└── ServiceRegistration.cs   # 服务初始化顺序引导
```

## 何处查找

| 需求 | 位置 | 备注 |
|------|------|------|
| 场景生命周期指南 | `Scene.cs` | Initialize/Begin/Update/Draw/End |
| 实体创建 | `Entity.cs` | 使用 `scene.CreateEntity("名称")`，而非 `new Entity()` |
| 组件模式 | `Component.cs` | 继承并覆写生命周期回调 |
| 系统间通信 | `EventBus.cs` | 发布/订阅，绝不直接引用其他系统的具体类 |
| 全局服务 | `ServiceLocator.cs` | 仅限基础设施接口（IEventBus、IGameStateManager 等） |
| 启动顺序 | `ServiceRegistration.cs` | 按顺序注册——先 EventBus，最后 FinalizeRegistration() |
| 游戏入口 | `GameRoot.cs` → `Program.cs` | Program.Main() → new GameRoot() → Run() |

## 约定

- **自定义 ECS** — 不使用 Nez。Scene/Entity/Component 均为手写，API 模仿 Nez 以便未来迁移。
- **场景切换** — 通过 `GameRoot.Instance.StartSceneTransition(newScene)`，非直接 `new`。
- **服务定位器** — 仅用于基础设施。业务系统通过 IEventBus 解耦。
- **快照后调用** — EventBus 在调用处理器前对订阅者列表进行快照，防止在处理器内取消订阅时出现集合修改异常。
- **线程安全** — ServiceLocator 在注册/解析时使用 `lock(_syncRoot)`。

## 反模式

- ❌ 在构造函数中进行繁重操作——改用 `Initialize()` / `Begin()`
- ❌ 直接实例化 `new Entity()` —— 使用 `scene.CreateEntity("名称")`
- ❌ 在 UI/Scene 中包含业务逻辑——业务逻辑属于 Systems/
- ❌ 直接引用另一个系统的具体类——使用 IEventBus
- ❌ 在未调用 `FinalizeRegistration()` 的情况下注册新服务

## 注释

- **Game1.cs 是死代码** — `src/DndGame/Game1.cs`（21 行），从未实例化。Program.cs 直接创建 GameRoot。
- **ServiceLocator 部分接线** — 已注册 3 个服务（IEventBus、IGameStateManager、IFontService）。仍需注册 4 个服务（IDataPersistence、ILLMGateway、IWorldStateManager、IAudioManager、IResourceCache）。
- **无 Nez 依赖** — csproj 中无 Nez NuGet 包。自定义 ECS 有意取代 Nez。
- **目标上线词** — 50-80 行，不重复父级（AGENTS.md）内容。
