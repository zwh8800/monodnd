# 酒馆与命运 — 整体技术架构设计文档

> **项目**: 酒馆与命运 (Tavern & Destiny)
> **引擎**: MonoGame 3.8.5+ (C# 12 / .NET 8)
> **场景框架**: Nez (Scene/Entity/Component)
> **规则基线**: DND 5e SRD（经Roguelike调整）
> **文档版本**: v2.0 (MonoGame迁移版)
> **前置文档**: 01-engine-selection.md, GDD-v1.md, 各子系统设计文档
> **语言政策**: 游戏文本统一采用简体中文，技术标识符使用英文PascalCase/camelCase

---

## 目录

1. [架构概述](#1-架构概述)
2. [模块详细设计](#2-模块详细设计)
3. [数据流与接口](#3-数据流与接口)
4. [技术栈与工具](#4-技术栈与工具)
5. [开发路线图](#5-开发路线图)
6. [风险与应对](#6-风险与应对)

---

## 1. 架构概述

### 1.1 核心设计哲学

```
┌─────────────────────────────────────────────────────────────────┐
│                    设计哲学 (Design Philosophy)                    │
├─────────────────────────────────────────────────────────────────┤
│                                                                   │
│  ① LLM = 皮肤层，程序 = 骨骼层                                     │
│     LLM生成叙事文本、对话、描述，不决策任何数值、战斗结果、           │
│     故事走向。所有规则判定由程序完成。                               │
│                                                                   │
│  ② 严格的JSON Schema验证                                          │
│     所有Agent输出必须通过Schema验证后才能进入游戏系统。               │
│     不合规输出触发自动重试（最多3次），耗尽后降级到离线模板。          │
│                                                                   │
│  ③ 程序层不依赖LLM，可完全单元测试                                  │
│     所有数值计算、规则判定、分支逻辑在无LLM环境下也可运行。            │
│     LLM是增量体验，不是必需品。                                      │
│                                                                   │
│  ④ 数值由程序控制，LLM只负责叙事文本                                  │
│     伤害数值、检定DC、战利品掉落、世界状态变更——全部程序控制。        │
│     LLM只负责"描述这一切看起来/听起来是什么样"。                     │
│                                                                   │
│  ⑤ 数据驱动设计                                                    │
│     DND 5e的规则（法术、怪物、职业）尽量配置在SQLite或JSON中，        │
│     避免硬编码。                                                   │
│                                                                   │
│  ⑥ 代码即场景                                                      │
│     所有游戏对象通过C#代码创建，不依赖可视化编辑器。                   │
│     Nez Entity/Component组合取代预制体体系。所有场景是Scene子类。     │
│     像素坐标、精灵帧、碰撞体——全部在代码中定义。                      │
│                                                                   │
└─────────────────────────────────────────────────────────────────┘
```

### 1.2 分层架构总览

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           表现层 (Presentation Layer)                          │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │  渲染管线: MonoGame SpriteBatch + Nez PostProcessors                  │   │
│  │  ┌────────────┐ ┌────────────┐ ┌──────────┐ ┌────────┐ ┌────────┐   │   │
│  │  │  Tile渲染   │ │ 角色精灵    │ │ 粒子特效  │ │ 动画    │ │ 迷雾    │   │   │
│  │  └────────────┘ └────────────┘ └──────────┘ └────────┘ └────────┘   │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │  UI层: Myra UI (像素风)                                               │   │
│  │  ┌────────────┐ ┌────────────┐ ┌──────────┐ ┌────────────┐ ┌──────┐ │   │
│  │  │ 酒馆UI     │ │ 战斗UI     │ │ 地图UI   │ │ 角色面板   │ │ 对话  │ │   │
│  │  └────────────┘ └────────────┘ └──────────┘ └────────────┘ └──────┘ │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
┌─────────────────────────────────────────────────────────────────────────────┐
│                       场景管理层 (Scene Management)                           │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │  Nez Core + Scene/Entity/Component                                    │   │
│  │  ┌──────────────┐ ┌────────────────┐ ┌────────────┐ ┌────────────┐  │   │
│  │  │ TavernScene  │ │ AdventureScene │ │CombatScene │ │ Loading    │  │   │
│  │  │ (酒馆)       │ │ (地图探索)     │ │ (战斗)     │ │ Scene     │  │   │
│  │  └──────────────┘ └────────────────┘ └────────────┘ └────────────┘  │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
│  场景切换: Core.StartSceneTransition<TScene>()  + 预加载策略                   │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
┌─────────────────────────────────────────────────────────────────────────────┐
│                       游戏逻辑层 (Game Logic Layer)                           │
│                                                                             │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌────────────────┐  │
│  │  战斗引擎      │  │  角色系统      │  │  酒馆系统      │  │  冒险系统        │  │
│  │  CombatEngine │  │  Character   │  │  Tavern      │  │  Adventure     │  │
│  │               │  │  System      │  │  System      │  │  Generation    │  │
│  │  · CombatFSM  │  │  · 属性管理   │  │  · 招募       │  │  · 蓝图解析     │  │
│  │  · DiceRoller │  │  · 职业/种族  │  │  · 升级       │  │  · 实例化引擎   │  │
│  │  · ActionRes  │  │  · 升级进阶   │  │  · 商店       │  │  · 遭遇生成     │  │
│  │    olver      │  │  · 关系系统   │  │  · NPC调度    │  │  · 战利品生成   │  │
│  │  · AISystem   │  │  · 伤疤/传承  │  │  · 事件系统   │  │  · 分支管理     │  │
│  │  · GoRogue    │  │  · 条件追踪   │  │  · 短/长休   │  │                │  │
│  │   集成        │  │              │  │              │  │                │  │
│  └──────────────┘  └──────────────┘  └──────────────┘  └───────┬────────┘  │
│                                                                 │           │
│  ┌──────────────────────────────┐  ┌────────────────────────────┐           │
│  │   世界状态管理器               │  │   物品/装备系统              │           │
│  │   WorldStateManager          │  │   ItemSystem                │           │
│  └──────────────────────────────┘  └────────────────────────────┘           │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
┌─────────────────────────────────────────────────────────────────────────────┐
│                        LLM 集成层 (LLM Integration Layer)                     │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │  LLMGateway (全局单例, HttpClient + System.Text.Json)                │   │
│  │                                                                       │   │
│  │  ┌──────────────────┐ ┌──────────────────┐ ┌──────────────────────┐  │   │
│  │  │  编剧Agent        │ │  DM Agent         │ │  文案Agent            │  │   │
│  │  │  (冒险前生成)     │ │  (实时叙事)       │ │  (按需生成)           │  │   │
│  │  └──────────────────┘ └──────────────────┘ └──────────────────────┘  │   │
│  │  ┌──────────────────┐ ┌──────────────────┐ ┌──────────────────────┐  │   │
│  │  │  平衡Agent        │ │  Schema验证器     │ │  缓存管理器           │  │   │
│  │  │  (蓝图验证)       │ │  JsonSchema.Net  │ │  sqlite-net          │  │   │
│  │  └──────────────────┘ └──────────────────┘ └──────────────────────┘  │   │
│  │  ┌──────────────────┐ ┌──────────────────┐ ┌──────────────────────┐  │   │
│  │  │  请求队列+限流     │ │  Token预算控制器   │ │  离线降级器           │  │   │
│  │  └──────────────────┘ └──────────────────┘ └──────────────────────┘  │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
┌─────────────────────────────────────────────────────────────────────────────┐
│                        数据持久化层 (Data Persistence Layer)                   │
│  ┌────────────────────────┐  ┌────────────────────────┐  ┌──────────────┐   │
│  │  sqlite-net            │  │  System.Text.Json      │  │  LLM缓存     │   │
│  │  · 角色数据             │  │  · 世界状态快照          │  │  (sqlite-net)│   │
│  │  · 物品/装备            │  │  · 存档序列化            │  │              │   │
│  │  · 冒险日志             │  │  · 配置表(JSON)         │  │              │   │
│  │  · 关系数据             │  │  · 角色模板             │  │              │   │
│  └────────────────────────┘  └────────────────────────┘  └──────────────┘   │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 1.3 模块依赖关系

```
┌──────────────────────────────────────────────────────────────────────────┐
│                     模块依赖关系 (Module Dependencies)                     │
│                                                                          │
│  Core.StartSceneTransition<T> ──────────────────────────────────────────┐ │
│     │ 场景切换                              │                            │ │
│     ▼                                      ▼                            │ │
│  CombatEngine ◄── CharacterSystem ──► AdventureSystem                    │ │
│     │  战斗结算     ↑ 提供属性              │  蓝图生成                    │ │
│     │              │                       ▼                            │ │
│     ▼              │                  LLMGateway                         │ │
│  SettlementSystem ─┘  ◄── TavernSystem ──► WorldStateManager             │ │
│    (失败/惩罚)      │    ↑ 触发事件           │                           │ │
│                    │    │                   ▼                            │ │
│                    │    └────── DataPersistence (sqlite-net + JSON)      │ │
│                    └───────────────────────┘                             │ │
│                                                                          │ │
│  GoRogue ───► CombatEngine (FOV/Pathfinding)                             │ │
│  GoRogue ───► AdventureSystem (MapGeneration)                            │ │
│  Nez Core ──► 所有Scene子类 (ECS基础架构)                                  │ │
│  Myra ──────► 所有UI面板 (XML布局 + 数据绑定)                              │ │
│  FontStashSharp ──► 所有UI/渲染 (动态字体+中文)                            │ │
└──────────────────────────────────────────────────────────────────────────┘
```

### 1.4 全局服务注册表

以下组件通过 `ServiceLocator` 模式注册为全局服务。所有服务在 `Game1.Initialize()` 中按顺序初始化：

```csharp
// 全局服务注册 — 在 Game1.Initialize() 中调用
public enum ServiceInitOrder
{
    EventBus = 0,          // 第0优先级，其他服务依赖它通信
    GameStateManager = 1,  // 全局状态管理、场景切换
    DataPersistence = 2,   // sqlite-net + JSON 数据存取
    LLMGateway = 3,        // LLM请求唯一入口
    WorldStateManager = 4, // 世界状态追踪
    AudioManager = 5,      // BGM/SFX管理
    ResourceCache = 6      // 预加载资源管理
}

// 使用方式
ServiceLocator.Initialize();
ServiceLocator.Register<IEventBus>(new EventBus());
ServiceLocator.Register<IGameStateManager>(new GameStateManager());
ServiceLocator.Register<IDataPersistence>(new DataPersistence());
ServiceLocator.Register<ILLMGateway>(new LLMGateway());
ServiceLocator.Register<IWorldStateManager>(new WorldStateManager());
ServiceLocator.Register<IAudioManager>(new AudioManager());
ServiceLocator.Register<IResourceCache>(new ResourceCache());

ServiceLocator.FinalizeRegistration();

// 在任何地方访问
var eventBus = ServiceLocator.Get<IEventBus>();
```

| 服务名 | 接口 | 实现类 | 职责 | 初始化顺序 |
|--------|------|--------|------|:----------:|
| `EventBus` | IEventBus | EventBus | 全局事件总线（C# event系统） | 0 |
| `GameStateManager` | IGameStateManager | GameStateManager | 全局状态管理、场景切换 | 1 |
| `DataPersistence` | IDataPersistence | DataPersistence | sqlite-net + JSON 数据持久化 | 2 |
| `LLMGateway` | ILLMGateway | LLMGateway | LLM请求唯一入口 | 3 |
| `WorldStateManager` | IWorldStateManager | WorldStateManager | 世界状态追踪 | 4 |
| `AudioManager` | IAudioManager | AudioManager | BGM/SFX管理 | 5 |
| `ResourceCache` | IResourceCache | ResourceCache | 预加载资源管理 | 6 |

`EventBus` 是第0优先级——其他所有服务依赖它进行跨系统通信。

---

## 2. 模块详细设计

### 2.1 场景管理 (Scene Management)

#### 2.1.1 Nez Scene 子系统

使用 Nez 框架的 `Scene`/`Entity`/`Component` 模式替代 Godot 场景树。每种游戏场景都是 `Nez.Scene` 的子类：

```csharp
using Nez;

public enum SceneId
{
    MainMenu,
    Tavern,
    AdventureMap,
    Combat,
    Loading,
    Settlement,
    Dialogue
}

public class TavernScene : Scene
{
    public override void OnStart()
    {
        AddSceneComponent(new TavernBackgroundRenderer());
        AddSceneComponent(new NPCSchedulerSystem());

        // 创建酒馆NPC实体
        var barkeeper = CreateEntity("barkeeper")
            .AddComponent(new SpriteRenderer(ServiceLocator.Get<IResourceCache>().GetTexture("npc_barkeeper")))
            .AddComponent(new NPCDialogueComponent("npc_barkeeper"));

        var recruitmentBoard = CreateEntity("recruitment_board")
            .AddComponent(new SpriteRenderer(ServiceLocator.Get<IResourceCache>().GetTexture("ui_board")))
            .AddComponent(new InteractableComponent("recruitment"));

        // 所有UI通过Myra管理
    }
}

public class CombatScene : Scene
{
    private CombatEngine _engine;

    public override void OnStart()
    {
        _engine = new CombatEngine();
        AddSceneComponent(_engine);

        // 使用GoRogue的ArrayMap渲染战斗地图
        var map = ServiceLocator.Get<GoRogueMapSystem>();
        AddEntity(map.CreateMapEntity());
    }
}

public class AdventureScene : Scene
{
    private AdventureInstance _adventure;

    public override void OnStart()
    {
        _adventure = ServiceLocator.Get<IAdventureSystem>().CurrentInstance;
        // 从AdventureInstance构建节点图
    }
}
```

#### 2.1.2 场景切换

```csharp
// 场景切换通过 Core.StartSceneTransition 实现
public static class SceneManager
{
    public static void SwitchTo<TScene>() where TScene : Scene, new()
    {
        // Nez内置场景过渡
        Core.StartSceneTransition(new FadeTransition(() => new TScene()));
    }

    public static void SwitchTo<TScene>(object context) where TScene : Scene, new()
    {
        // 带上下文的场景切换（如传入AdventureInstance）
        ServiceLocator.Get<IGameStateManager>().TransitionContext = context;
        Core.StartSceneTransition(new FadeTransition(() => new TScene()));
    }
}
```

#### 2.1.3 场景列表

| 场景ID | C# 类 | 类型 | 说明 |
|--------|-------|:----:|------|
| `main_menu` | MainMenuScene | 独立 | 新游戏/继续/设置 |
| `tavern` | TavernScene | 持久 | 元游戏核心空间 |
| `adventure_map` | AdventureScene | 按需加载 | 节点图探索 |
| `combat` | CombatScene | 按需加载 | 战术地图战斗 |
| `loading` | LoadingScene | 叠加 | 过渡动画 |
| `settlement` | SettlementScene | 叠加 | 冒险完成/失败结算 |
| `dialogue` | DialogueScene | 叠加 | NPC对话弹窗 |

#### 2.1.4 场景状态机

```
┌──────────┐   新游戏/读档    ┌────────┐
│ main_menu │ ──────────────▶ │ tavern │
└──────────┘                  └───┬────┘
                                  │
                          ┌───────┴───────┐
                          │               │
                          ▼               ▼
                    ┌────────────┐  ┌──────────┐
                    │ adventure  │  │  combat  │ ←── 从冒险或酒馆进入
                    │ _map       │  │          │
                    └──────┬─────┘  └────┬─────┘
                           │             │
                           └──────┬──────┘
                                  ▼
                           ┌───────────┐
                           │ settlement│ ──→ tavern (返回酒馆)
                           └───────────┘
```

#### 2.1.5 预加载策略

```csharp
public class ResourceCache : IResourceCache
{
    private readonly Dictionary<string, Texture2D> _textures = new();
    private readonly Dictionary<string, Effect> _effects = new();
    private ContentManager _content;

    public void Preload()
    {
        // 常驻资源（游戏全程加载）
        LoadTexture("ui_window", "Sprites/UI/window_frame");
        LoadTexture("ui_button", "Sprites/UI/button");

        // 按需预加载队列
        var gameState = ServiceLocator.Get<IGameStateManager>();
        gameState.OnSceneChanging += (_, sceneId) =>
        {
            switch (sceneId)
            {
                case SceneId.Combat:
                    PreloadCombatTextures();
                    break;
                case SceneId.AdventureMap:
                    PreloadAdventureTextures();
                    break;
            }
        };
    }

    private void PreloadCombatTextures()
    {
        LoadTexture("tile_dungeon_floor", "Sprites/Tilesets/dungeon_floor");
        LoadTexture("tile_dungeon_wall", "Sprites/Tilesets/dungeon_wall");
        LoadTexture("fx_attack_slash", "Sprites/FX/attack_slash");
        // ...
    }

    private void LoadTexture(string key, string assetPath)
    {
        if (!_textures.ContainsKey(key))
            _textures[key] = _content.Load<Texture2D>(assetPath);
    }

    public Texture2D GetTexture(string key) =>
        _textures.TryGetValue(key, out var tex) ? tex : throw new KeyNotFoundException(key);
}
```

**预加载规则**:
- 战斗场景：在冒险中进入战斗节点前预加载
- 对话场景：常驻内存（复用实例）
- 结算场景：冒险完成时预加载
- 酒馆场景：游戏全程常驻

---

### 2.2 战斗引擎 (CombatEngine)

#### 2.2.1 架构定位

战斗引擎是**纯程序系统**，不依赖任何LLM调用。所有数值计算由程序完成，LLM只负责接收战斗日志并生成叙事文本（通过DM Agent）。

#### 2.2.2 核心模块

```
┌──────────────────────────────────────────────────────────────┐
│                     CombatEngine (战斗引擎)                     │
│                                                                │
│  ┌──────────────────┐  ┌──────────────────┐  ┌─────────────┐  │
│  │  CombatFSM        │  │  DiceRoller      │  │  Action     │  │
│  │  · 16状态有限机   │  │  · d20检定       │  │  Resolver   │  │
│  │  · 状态守卫条件   │  │  · 优势/劣势     │  │  · 攻击结算  │  │
│  │  · 同时选择机制   │  │  · 伤害骰        │  │  · 法术结算  │  │
│  └──────────────────┘  └──────────────────┘  │  · 豁免结算  │  │
│                                                └─────────────┘  │
│  ┌──────────────────┐  ┌──────────────────┐  ┌─────────────┐  │
│  │  ConditionSystem  │  │  AISystem        │  │  Terrain    │  │
│  │  · 14种DND条件   │  │  · 目标选择启发  │  │  Interact   │  │
│  │  · 持续时间追踪   │  │  · 行为树决策    │  │  · 8种标签  │  │
│  │  · 堆叠互斥规则   │  │  · 敌人类型模式  │  │  · 环境危害  │  │
│  └──────────────────┘  └──────────────────┘  └─────────────┘  │
│                                                                │
│  ┌──────────────────┐  ┌──────────────────┐                    │
│  │  GoRogue集成      │  │  CombatLog       │                    │
│  │  · FOV视野计算    │  │  · 完整日志记录   │                    │
│  │  · A* Pathfinding │  │  · 格式化输出    │ ←──→ DM Agent     │
│  │  · ArrayMap地图   │  │  · 导出用于LLM  │                    │
│  └──────────────────┘  └──────────────────┘                    │
└──────────────────────────────────────────────────────────────┘
```

#### 2.2.3 GoRogue 集成

```csharp
using GoRogue;
using GoRogue.MapViews;
using GoRogue.FOV;
using GoRogue.Pathing;

public class CombatMapManager
{
    // GoRogue的ArrayMap作为战斗地图底层数据结构
    private ArrayMap<bool> _blockedMap;  // true = 不可通行
    private FOVHandler _fovHandler;
    private AStar _pathfinder;

    public CombatMapManager(int width, int height)
    {
        _blockedMap = new ArrayMap<bool>(width, height);
        _pathfinder = new AStar(_blockedMap, Distance.MANHATTAN);
    }

    public void InitializeFromTileData(bool[,] walkableTiles)
    {
        for (int x = 0; x < _blockedMap.Width; x++)
        for (int y = 0; y < _blockedMap.Height; y++)
            _blockedMap[x, y] = !walkableTiles[x, y];
    }

    public IEnumerable<Coord> CalculateFOV(Coord origin, int radius)
    {
        _fovHandler ??= new FOVHandler(_blockedMap, FieldOfView.CIRCULAR);
        _fovHandler.CalculateFOV(origin, radius, Distance.EUCLIDEAN);
        return _fovHandler.CurrentFOV;
    }

    public IEnumerable<Coord> FindPath(Coord start, Coord end)
    {
        return _pathfinder.ShortestPath(start, end) ?? Enumerable.Empty<Coord>();
    }
}
```

#### 2.2.4 回合流程关键路径

战斗引擎使用C#枚举实现有限状态机：

```csharp
public enum CombatState
{
    Initialization,
    RollInitiative,
    RoundStart,
    SimultaneousSelection,
    ActionPhase,
    BonusActionPhase,
    MovementPhase,
    ReactionWindow,
    TurnEnd,
    RoundEnd,
    Victory,
    Defeat
}

public class CombatFSM
{
    public CombatState CurrentState { get; private set; }
    private readonly Dictionary<(CombatState, CombatState), Func<bool>> _guards = new();

    public bool CanTransition(CombatState to)
    {
        var key = (CurrentState, to);
        return _guards.TryGetValue(key, out var guard) && guard();
    }

    public void Transition(CombatState to)
    {
        if (!CanTransition(to))
            throw new InvalidOperationException($"Cannot transition from {CurrentState} to {to}");
        CurrentState = to;
        OnStateEntered?.Invoke(to);
    }

    public event Action<CombatState> OnStateEntered;
}
```

```
攻击检定管线 (Attack Resolution Pipeline) ← 纯程序，不依赖LLM
────────────────────────────────────────────────────────
1. 动作声明 → 玩家/AI选择行动、目标
2. 攻击检定 → d20 + 属性调整值 + 熟练加值 + 其他加值
3. 命中判定 → 攻击结果 ≥ 目标AC？自然20/自然1？
4. 优势/劣势 → 是否需骰2次取高/低
5. 暴击判定 → 自然20 → 伤害骰取最大值
6. 伤害计算 → 基础伤害 + 属性调整值 + 附魔 + 暴击
7. 伤害类型 → 抗性/免疫/易伤 → 最终伤害
8. 专注检定 → 施法者受伤害 → DC = max(10, 伤害/2)
9. 条件应用 → 附加效果/状态生效
10. 战斗日志 → 记录完整计算链 → (可选的)DM Agent渲染为叙事文本
```

#### 2.2.5 DiceRoller 纯函数实现

```csharp
public static class DiceRoller
{
    private static readonly Random _shared = new();

    public static int Roll(int sides) => _shared.Next(1, sides + 1);

    public static int RollD20() => Roll(20);

    public static AttackRollResult RollAttack(int bonus, bool hasAdvantage, bool hasDisadvantage)
    {
        var hasBoth = hasAdvantage && hasDisadvantage;
        var rolls = hasBoth ? new[] { RollD20() } :
                    hasAdvantage ? new[] { RollD20(), RollD20() } :
                    hasDisadvantage ? new[] { RollD20(), RollD20() } :
                    new[] { RollD20() };

        var rawValue = hasAdvantage && !hasBoth ? Math.Max(rolls[0], rolls[1]) :
                       hasDisadvantage && !hasBoth ? Math.Min(rolls[0], rolls[1]) :
                       rolls[0];

        return new AttackRollResult
        {
            RawValue = rawValue,
            Total = rawValue + bonus,
            IsCritical = rolls.Contains(20),
            IsCriticalMiss = rolls.Contains(1),
            Rolls = rolls,
            Bonus = bonus,
            Advantage = hasAdvantage,
            Disadvantage = hasDisadvantage
        };
    }

    public static DamageRollResult RollDamage(string diceExpression, int bonus)
    {
        // "2d6+3" 解析与掷骰
        var (count, sides, extraBonus) = ParseDiceExpression(diceExpression);
        var rolls = Enumerable.Range(0, count).Select(_ => Roll(sides)).ToArray();
        return new DamageRollResult
        {
            Rolls = rolls,
            Bonus = bonus + extraBonus,
            Total = rolls.Sum() + bonus + extraBonus
        };
    }

    private static (int count, int sides, int bonus) ParseDiceExpression(string expr)
    {
        // 示例: "2d6+3" → (2, 6, 3)
        var parts = expr.Split('d', '+');
        // 解析逻辑
        return (int.Parse(parts[0]), int.Parse(parts[1].Split('+')[0]),
                parts.Length > 2 ? int.Parse(parts[2]) : 0);
    }
}

public record AttackRollResult
{
    public int RawValue { get; init; }
    public int Total { get; init; }
    public bool IsCritical { get; init; }
    public bool IsCriticalMiss { get; init; }
    public int[] Rolls { get; init; } = Array.Empty<int>();
    public int Bonus { get; init; }
    public bool Advantage { get; init; }
    public bool Disadvantage { get; init; }
}

public record DamageRollResult
{
    public int[] Rolls { get; init; } = Array.Empty<int>();
    public int Bonus { get; init; }
    public int Total { get; init; }
}
```

#### 2.2.6 与DND 5e的关键偏离

| 规则 | 标准5e | 本游戏 | 理由 |
|------|--------|--------|------|
| 先攻 | 整场固定 | 每轮重骰 | 增加不确定性+节奏感 |
| 暴击 | 伤害骰翻倍 | 伤害骰取最大值 | 更爽更快 |
| 死亡豁免 | 3次成功/失败 | 3轮无治疗死亡 | 紧迫感 |
| 疲劳 | 6级渐进 | 3级 | 减少管理 |
| 行动选择 | 轮流 | 同时选择→按先攻结算 | 减少等待 |
| 负重 | 磅数 | 槽位制 | 简化管理 |

---

### 2.3 角色系统 (CharacterSystem)

#### 2.3.1 数据模型分层

```
角色数据 = 数值层 + 叙事层
─────────────────────────────
数值层 (程序控制，100%可测试):
  · 六维属性 (STR/DEX/CON/INT/WIS/CHA) + 调整值
  · HP/AC/速度/熟练加值/先攻/法术位
  · 职业等级/子职业/已学特性
  · 种族/亚种/种族特性
  · 技能熟练/专精/专长
  · 装备槽（主手/副手/护甲/饰品）
  · 条件/伤疤/疲乏等级
  · 关系值 (角色间数值化关系)

叙事层 (LLM生成，Schema约束):
  · 姓名/性别/年龄
  · 性格标签 (2-5个)
  · 背景故事 (2-3段)
  · 外观描述
  · 个人目标
  · 冒险记忆 (关键事件摘要)
  · 伤疤叙事描述
```

#### 2.3.2 核心接口

```csharp
public interface ICharacterSystem
{
    // 数据存取
    CharacterData GetCharacter(string charId);
    void SaveCharacter(CharacterData data);
    void DeleteCharacter(string charId);

    // 属性计算
    int GetAbilityModifier(Ability ability, int score);
    int GetProficiencyBonus(int level);
    int GetSkillModifier(string charId, Skill skill);
    int GetSpellSaveDc(string charId);
    int GetSpellAttackMod(string charId);

    // 等级进阶
    LevelUpResult LevelUp(string charId);
    void ApplyAsi(string charId, AsiChoice choice);

    // 关系管理
    int GetRelationship(string charA, string charB);
    void ModifyRelationship(string charA, string charB, int delta);

    // 叙事层
    void SetNarrativeData(string charId, CharacterNarrative narrative);
    CharacterNarrativeContext GetNarrativeContext(string charId);

    // 事件
    event Action<CharacterData> CharacterCreated;
    event Action<string, int, int> CharacterLeveledUp;
    event Action<string, string> CharacterDied;
    event Action<string, string, int, int> RelationshipChanged;
}
```

#### 2.3.3 数据模型 — C# record

```csharp
public record CharacterData
{
    public string CharacterId { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public CharacterStatus Status { get; init; } = CharacterStatus.Alive;
    public CharacterNarrative Narrative { get; init; } = new();
    public CharacterStats Stats { get; init; } = new();
    public CharacterCombatConfig Combat { get; init; } = new();
    public ClassProgression ClassProgression { get; init; } = new();
    public RaceData Race { get; init; } = new();
    public Dictionary<Skill, int> SkillModifiers { get; init; } = new();
    public SpellcastingData Spellcasting { get; init; } = new();
    public EquipmentData Equipment { get; init; } = new();
    public Dictionary<string, RelationshipEntry> Relationships { get; init; } = new();
    public List<Condition> Conditions { get; init; } = new();
    public List<Scar> Scars { get; init; } = new();
    public List<string> Feats { get; init; } = new();
    public InheritanceData Inheritance { get; init; } = new();
    public AdventureLog AdventureLog { get; init; } = new();
}

public record CharacterStats
{
    public int Level { get; init; }
    public int Xp { get; init; }
    public int XpToNext { get; init; }
    public int ProficiencyBonus { get; init; }
    public int Speed { get; init; }
    public int InitiativeModifier { get; init; }
    public int ArmorClass { get; init; }
    public HitPoints HitPoints { get; init; } = new();
    public HitDice HitDice { get; init; } = new();
    public Dictionary<Ability, AbilityScore> Abilities { get; init; } = new();
    public List<Ability> SavingThrowProficiencies { get; init; } = new();
    public int ExhaustionLevel { get; init; }
    public bool Inspiration { get; init; }
}

public record AbilityScore
{
    public int Score { get; init; }
    public int Modifier => (int)Math.Floor((Score - 10) / 2.0);
}

public record HitPoints
{
    public int Max { get; init; }
    public int Current { get; init; }
    public int Temporary { get; init; }
}

public enum Ability { Str, Dex, Con, Int, Wis, Cha }
public enum Skill
{
    Acrobatics, AnimalHandling, Arcana, Athletics,
    Deception, History, Insight, Intimidation,
    Investigation, Medicine, Nature, Perception,
    Performance, Persuasion, Religion, SleightOfHand,
    Stealth, Survival
}
public enum CharacterStatus { Alive, Dead, Retired }
```

#### 2.3.4 角色生成管线

```
角色生成 = 程序化数值层 → LLM叙事层 → 合并写入
───────────────────────────────────────────────

Phase 1: 程序化数值层
  输入: race + class + target_level
  → Standard Array + 种族加成 → 六维属性
  → 职业进阶表 → HP/熟练加值/特性
  → 种族特性表 → 种族特质
  → 衍生值计算 → AC/技能调整值/法术位
  → 起始装备分配
  输出: CharacterNumericalBlock (纯JSON，无叙事数据)

Phase 2: LLM叙事层 (通过文案Agent)
  输入: race + class + numerical_summary
  → Schema约束生成 → name/gender/personality_tags/backstory/appearance/goal
  → 最多3次重试 → 失败使用模板
  输出: CharacterNarrativeBlock

Phase 3: 合并写入
  → 合并Phase 1 + Phase 2
  → 写入SQLite (sqlite-net ORM)
  → 触发 CharacterCreated 事件
```

```csharp
public class CharacterGenerator
{
    public async Task<CharacterData> GenerateCharacter(string raceId, string classId, int targetLevel)
    {
        // Phase 1: 程序化数值层
        var numerical = NumericalGenerator.Generate(raceId, classId, targetLevel);

        // Phase 2: LLM叙事层
        var llmGateway = ServiceLocator.Get<ILLMGateway>();
        var narrative = await llmGateway.CallAgent<CopywriterAgent, CharacterNarrative>(
            new CharacterNarrativeRequest(raceId, classId, numerical.Summary)
        );

        // Phase 3: 合并写入
        var data = new CharacterData
        {
            CharacterId = $"char_{Guid.NewGuid():N}",
            Stats = numerical.Stats,
            Narrative = narrative,
            // ...
        };
        ServiceLocator.Get<IDataPersistence>().Save(data);
        return data;
    }
}
```

#### 2.3.5 角色状态总览

完整角色属性块（Character Stat Block）定义见 `subsystems/01-character-system.md §2.1`，顶层结构如下：

```json
{
  "character_id": "char_7a3f2b1c",
  "status": "alive",
  "narrative": { /* LLM生成的叙事数据 */ },
  "stats": { /* 六维/HP/AC/法术位等核心数值 */ },
  "combat": { /* 战斗相关配置 */ },
  "class_progression": { /* 职业/等级/子职业/特性 */ },
  "race": { /* 种族/亚种/特质 */ },
  "skills": { /* 技能熟练/调整值 */ },
  "spellcasting": { /* 施法能力/法术位 */ },
  "equipment": { /* 装备槽/背包 */ },
  "relationships": { /* 角色间关系值 */ },
  "conditions": [],
  "scars": [],
  "feats": [],
  "inheritance": { /* 知识传承 */ },
  "adventure_log": { /* 冒险统计 */ }
}
```

---

### 2.4 酒馆系统 (TavernSystem)

#### 2.4.1 系统边界

酒馆系统负责冒险之间的**元游戏层**。它管理酒馆升级、角色招募、商店、NPC调度和酒馆事件。所有数值由程序控制，LLM只负责招募文本、事件描述和NPC对话。

#### 2.4.2 核心模块

```
┌───────────────────────────────────────────────────────────┐
│                    TavernSystem (酒馆系统)                   │
│                                                             │
│  ┌───────────────┐  ┌───────────────┐  ┌────────────────┐  │
│  │  TavernLevel   │  │  Recruitment  │  │  ShopSystem    │  │
│  │  Manager       │  │  Manager      │  │  · 6种商店类型  │  │
│  │  · Lv1-10升级   │  │  · 招募板管理  │  · 库存刷新      │  │
│  │  · 声望XP计算   │  │  · 角色生成管 │  · 定价/折扣     │  │
│  │  · 区域解锁     │  │    线         │  · 特殊商品      │  │
│  │  · Prestige    │  │  · 槽位管理   │  └────────────────┘  │
│  └───────────────┘  │  · 招募费计算  │                      │
│                     └───────────────┘                      │
│  ┌───────────────┐  ┌───────────────┐  ┌────────────────┐  │
│  │  NPCScheduler  │  │  EventSystem  │  │  RestSystem    │  │
│  │  · 固定NPC     │  │  · 事件池     │  · 短休           │  │
│  │  · 轮换NPC     │  │  · 触发条件   │  · 长休           │  │
│  │  · 生命周期    │  │  · 机械结果   │  · 资源恢复       │  │
│  │  · 访问权重    │  │  · LLM叙事   │                  │  │
│  └───────────────┘  └───────────────┘  └────────────────┘  │
└───────────────────────────────────────────────────────────┘
```

#### 2.4.3 酒馆升级路径

酒馆10级 + Prestige系统。升级解锁新区域和功能：

| 等级 | 解锁 | 关键功能 |
|:----:|------|----------|
| Lv1 | 大厅 | 基础招募、基础任务板、基础休息 |
| Lv2 | 铁匠铺 | 装备修复/打造 |
| Lv3 | 炼金台 | 药水/卷轴制作 |
| Lv4 | — | 中冒险解锁、角色槽+1 |
| Lv5 | 图书馆 | 法术学习、新职业/专长 |
| Lv6 | — | 关系系统深化、酒馆事件启用 |
| Lv7 | 神殿 | 复活角色、移除诅咒 |
| Lv8 | — | 长冒险解锁、传奇任务线 |
| Lv9 | — | 双任务并发、英雄传记完整 |
| Lv10 | 英雄之壁完整 | 传说装备、全功能巅峰 |

#### 2.4.4 事件结果分离模型

```
事件结果 = 机械结果 (程序控制) + 叙事结果 (LLM生成)

机械结果 (程序控制):
  · 关系值变化 (+/- N)
  · 任务生成/解锁
  · 物品/金币奖励
  · 战斗触发
  · buff/debuff 应用
  · 设施状态变更

叙事结果 (LLM生成):
  · 场景描写 (DM Agent)
  · NPC对话 (DM Agent)
  · 角色情感表达 (DM Agent)
  · 氛围文本 (DM Agent)

执行流程:
  1. 程序决定机械结果
  2. 将机械结果 + 上下文 传入 LLM
  3. LLM生成符合结果的叙事文本
  4. UI展示叙事文本 + 机械结果通知
```

```csharp
public class EventSystem
{
    public async Task<TavernEventResult> TriggerEvent(string eventId)
    {
        // Step 1: 程序决定机械结果
        var eventDef = _eventPool[eventId];
        var mechanicalResult = MechanicalResolver.Resolve(eventDef);

        // Step 2: LLM生成叙事文本
        var llmGateway = ServiceLocator.Get<ILLMGateway>();
        var narrative = await llmGateway.CallAgent<DMAgent, NarrativeText>(
            new TavernEventNarrativeRequest(eventDef, mechanicalResult)
        );

        // Step 3: 合并结果
        return new TavernEventResult(mechanicalResult, narrative);
    }
}

public record TavernEventResult(
    MechanicalResult Mechanics,
    NarrativeText Narrative
);

public record MechanicalResult(
    Dictionary<string, int> RelationshipDeltas,
    List<string> Rewards,
    int GoldDelta,
    string? CombatTriggerId
);
```

---

### 2.5 冒险系统 (AdventureSystem)

#### 2.5.1 三层生成管线（核心架构）

这是整个项目最核心的技术系统。详见 `subsystems/06-adventure-generation.md`，此处给出架构概览：

```
┌────────────── 第一层：冒险蓝图生成（冒险开始前）──────────────┐
│  编剧Agent + 平衡Agent                                          │
│  → adventure_blueprint.json (严格的JSON Schema约束)             │
│  → 验证：Schema验证 + 业务逻辑验证 + 平衡Agent                    │
│  → 失败策略：3次重试 → 离线模板                                  │
└──────────────────────────────┬──────────────────────────────────┘
                                ↓
┌────────────── 第二层：程序化实例化（冒险开始前）──────────────┐
│  AdventureInstantiator (纯程序，不依赖LLM，可单元测试)           │
│  14步算法：                                                    │
│   1. 解析+验证蓝图JSON          8. 休息节点配置                   │
│   2. 生成节点图                 9. 谜题节点配置                   │
│   3. 分配房间模板               10. Boss数据生成                  │
│   4. GoRogue地牢生成(走廊+房间) 11. 走廊连接                     │
│   5. 遭遇生成(CR预算→敌人)      12. 交互标签放置                  │
│   6. 对话节点配置               13. 触发器设置                    │
│   7. 探索节点(陷阱/战利品)      14. 冒险状态机初始化              │
└──────────────────────────────┬──────────────────────────────────┘
                                ↓
┌────────────── 第三层：DM Agent实时叙事（冒险进行中）──────────┐
│  玩家每个动作后按需调用DM Agent（<2秒响应时间）：                │
│  · 进入新房间 → scene_atmosphere                               │
│  · NPC对话 → npc_dialogue + options                            │
│  · 检定结果 → skill_check_result                               │
│  · 战斗动作 → combat_narration                                  │
│  · 选择呈现 → choice_presentation                               │
│  失败降级：静态模板                                              │
└────────────────────────────────────────────────────────────────┘
```

#### 2.5.2 GoRogue 地牢生成集成

```csharp
using GoRogue.MapGeneration;
using GoRogue.MapViews;
using GoRogue.Random;

public class DungeonGenerator
{
    public ArrayMap<TileType> GenerateDungeon(int width, int height, string theme)
    {
        // 使用GoRogue的MapGeneration生成战术地图
        var generator = new GoRogue.MapGeneration.Generators.CaveGenerator(width, height);
        var map = generator.Generate();

        var result = new ArrayMap<TileType>(width, height);
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
            result[x, y] = map[x, y] ? TileType.Floor : TileType.Wall;

        // 根据主题设置tileset
        ApplyThemeTiles(result, theme);
        return result;
    }

    private void ApplyThemeTiles(ArrayMap<TileType> map, string theme)
    {
        // 主题: dungeon / forest / cave / castle ...
        // 映射 TileType → Texture2D 在渲染层处理
    }
}

public enum TileType { Floor, Wall, Water, Lava, Trap, Door, Hidden }
```

#### 2.5.3 实例化14步算法

```csharp
public class AdventureInstantiator
{
    public async Task<AdventureInstance> InstantiateAdventure(
        AdventureBlueprint blueprint,
        PartyState partyState)
    {
        // Step 1: 解析蓝图 → 验证 → 解析依赖
        var validated = await BlueprintParser.ParseAndValidate(blueprint);

        // Step 2: 生成冒险节点图
        var nodeGraph = NodeGraphGenerator.Generate(validated.Nodes, validated.Meta.Tier);

        // Step 3-13: 各节点类型配置
        foreach (var (nodeId, node) in nodeGraph)
        {
            switch (node.Type)
            {
                case NodeType.Combat:
                case NodeType.EliteCombat:
                case NodeType.Boss:
                    node.Encounter = EncounterGenerator.Generate(node, validated.DifficultyProfile);
                    // GoRogue生成战术地图
                    node.TacticalMap = new DungeonGenerator().GenerateDungeon(20, 15, validated.Meta.Theme);
                    break;

                case NodeType.Dialogue:
                    node.DialogueConfig = DialogueConfigurator.Configure(node, validated.KeyNpcs);
                    break;

                case NodeType.Exploration:
                    node.ExplorationConfig = ExplorationConfigurator.Configure(node, validated.DifficultyProfile);
                    break;

                case NodeType.Merchant:
                    node.ShopConfig = MerchantConfigurator.Configure(node, validated.DifficultyProfile.LootTier);
                    break;

                case NodeType.Rest:
                    node.RestConfig = RestConfigurator.Configure(node, validated.DifficultyProfile);
                    break;
            }
        }

        // Step 14: 初始化冒险状态机
        var adventureState = new AdventureState(blueprint, nodeGraph);

        return new AdventureInstance(adventureState, nodeGraph);
    }
}

public record AdventureInstance(
    AdventureState State,
    Dictionary<string, AdventureNode> NodeGraph
);

public record AdventureState(
    AdventureBlueprint Blueprint,
    Dictionary<string, AdventureNode> NodeGraph
)
{
    public string CurrentNodeId { get; set; } = "";
    public bool IsCompleted { get; set; }
    public bool IsFailed { get; set; }
}
```

#### 2.5.4 冒险模板

系统内置三种冒险模板用于离线降级和蓝图参考：

| 模板 | 时长 | 节点数 | 战斗 | 结构 |
|------|:----:|:------:|:----:|------|
| 短冒险 | ~30min | 5-8 | 3-5 | 线性+1高潮 |
| 中冒险 | ~3h | 15-25 | 8-15 | 2-3地点+1谜题+分支 |
| 长冒险 | ~6h | 30-50 | 15-25 | 三幕+多结局 |

#### 2.5.5 遭遇难度计算

基于DND 5e DMG的XP阈值系统（简化CR预算表）：

| 队伍Lv×人数 | Easy CR | Medium CR | Hard CR | Deadly CR |
|:----------:|:-------:|:---------:|:-------:|:---------:|
| Lv1 × 4 | 0.5 | 1.0 | 1.5 | 2.0 |
| Lv3 × 4 | 1.5 | 3.0 | 4.5 | 6.0 |
| Lv5 × 4 | 3.0 | 5.0 | 7.5 | 10.0 |

---

### 2.6 LLM Gateway

#### 2.6.1 架构定位

LLM Gateway是**游戏中所有LLM请求的唯一入口**。所有游戏系统通过Gateway请求LLM生成内容，Gateway负责调度、验证、缓存、降级。

核心原则:
- LLM只做皮肤层: 生成叙事文本，不决策数值
- 严格Schema验证: 每个Agent的输出必须通过JSON Schema验证
- 优雅离线降级: API不可用时核心体验不受影响

#### 2.6.2 核心实现

```csharp
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

public class LLMGateway : ILLMGateway
{
    private readonly HttpClient _httpClient;
    private readonly SchemaValidator _schemaValidator;
    private readonly CacheManager _cacheManager;
    private readonly RateLimiter _rateLimiter;
    private readonly TokenBudgetManager _budgetManager;
    private readonly FallbackManager _fallbackManager;
    private readonly Dictionary<string, LLMAgent> _agents = new();

    public LLMGateway()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _schemaValidator = new SchemaValidator();
        _cacheManager = new CacheManager();
        _rateLimiter = new RateLimiter();
        _budgetManager = new TokenBudgetManager();
        _fallbackManager = new FallbackManager();
        RegisterDefaultAgents();
    }

    private void RegisterDefaultAgents()
    {
        RegisterAgent(new ScreenwriterAgent());
        RegisterAgent(new DMAgent());
        RegisterAgent(new CopywriterAgent());
        RegisterAgent(new BalancerAgent());
    }

    public async Task<TResponse> CallAgent<TAgent, TResponse>(
        AgentRequest request,
        CancellationToken ct = default)
        where TAgent : LLMAgent
        where TResponse : class
    {
        // Step 1: Pre-Validate
        var preErrors = _schemaValidator.Validate(request.Input, request.OutputSchema);
        if (preErrors.Count > 0)
            throw new GatewayException($"Pre-validation failed: {string.Join(", ", preErrors)}");

        // Step 2: Cache Lookup
        var cacheKey = _cacheManager.ComputeKey(request);
        var cached = await _cacheManager.GetAsync<TResponse>(cacheKey);
        if (cached != null)
            return cached;

        // Step 3: Rate Check
        if (!_rateLimiter.TryConsume(request.AgentId, request.EstimatedTokens))
        {
            // Rate limited — 降级或入队
            return await _fallbackManager.GetFallback<TResponse>(request);
        }

        // Step 4-6: 发送API请求 + 解析 + 验证
        for (int retry = 0; retry <= 3; retry++)
        {
            try
            {
                var agent = _agents[request.AgentId];
                var jsonResponse = await SendRequest(agent, request.Input, ct);

                var response = JsonSerializer.Deserialize<TResponse>(jsonResponse);
                if (response == null)
                    throw new GatewayException("Null response after deserialization");

                var errors = _schemaValidator.Validate(
                    JsonNode.Parse(jsonResponse), request.OutputSchema);

                if (errors.Count > 0)
                {
                    if (retry < 3)
                    {
                        request.Input["_last_error"] = string.Join("; ", errors);
                        continue; // 重试
                    }
                    return await _fallbackManager.GetFallback<TResponse>(request);
                }

                // Step 7: Cache Write
                await _cacheManager.SetAsync(cacheKey, response, request.CacheTtl);

                return response;
            }
            catch (HttpRequestException) when (retry < 3)
            {
                await Task.Delay(1000 * (retry + 1)); // 指数退避
            }
        }

        // 所有重试耗尽
        return await _fallbackManager.GetFallback<TResponse>(request);
    }

    private async Task<string> SendRequest(LLMAgent agent, JsonElement input, CancellationToken ct)
    {
        var payload = agent.BuildPayload(input);
        var jsonContent = new StringContent(
            JsonSerializer.Serialize(payload),
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync(agent.ApiEndpoint, jsonContent, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }

    public void RegisterAgent(LLMAgent agent)
    {
        _agents[agent.Id] = agent;
    }
}
```

#### 2.6.3 Agent 基类

```csharp
public abstract class LLMAgent
{
    public abstract string Id { get; }
    public abstract string ApiEndpoint { get; }
    public abstract int MaxInputTokens { get; }
    public abstract int MaxOutputTokens { get; }
    public abstract JsonSchema OutputSchema { get; }
    public virtual TimeSpan CacheTtl => TimeSpan.FromHours(24);
    public abstract JsonObject BuildPayload(JsonElement input);
}

public class DMAgent : LLMAgent
{
    public override string Id => "dm_agent";
    public override string ApiEndpoint =>
        "https://api.openai.com/v1/chat/completions";
    public override int MaxInputTokens => 2000;
    public override int MaxOutputTokens => 500;
    public override JsonSchema OutputSchema => DMAgentSchema.Schema;

    public override JsonObject BuildPayload(JsonElement input)
    {
        return new JsonObject
        {
            ["model"] = "gpt-4o-mini",
            ["messages"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "system",
                    ["content"] = GetSystemPrompt()
                },
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = input.GetProperty("user_message").GetString()
                }
            },
            ["max_tokens"] = MaxOutputTokens,
            ["response_format"] = new JsonObject { ["type"] = "json_object" }
        };
    }

    private string GetSystemPrompt() => """
        你是《酒馆与命运》的地下城主（DM），负责生成符合DND 5e风格的叙事文本。
        你的输出必须严格遵循JSON Schema定义的结构。
        你只负责叙事文本，不决定任何数值结果。
        所有文本使用简体中文。
        """;
}
```

#### 2.6.4 Agent调度表

| Agent | 职责 | 调用时机 | 阻塞模式 | Token预算 | 模型等级 |
|-------|------|----------|:--------:|:---------:|:--------:|
| 编剧Agent | 生成冒险蓝图 | 冒险开始前 | 异步(加载等待) | 4000/2000 | 高质量(GPT-4) |
| DM Agent | 实时叙事 | 玩家每个动作后 | 异步(不阻塞UI) | 2000/500 | 中等(GPT-4o-mini) |
| 文案Agent | 物品/技能描述 | 新物品获得时 | 异步(后台生成) | 1000/300 | 轻量 |
| 平衡Agent | 检查CR/战利品 | 蓝图生成后 | 自动同步 | 3000/1000 | 高质量(GPT-4) |

#### 2.6.5 请求生命周期

```
1. Pre-Validate (本地校验) → 失败返回GatewayError
2. Cache Lookup (语义Hash查SQLite缓存) → 命中跳到Step 7
3. Rate Check (per-agent配额+全局配额) → 超限入队或降级
4. Send to API (HttpClient → 云端LLM) → 构造请求体+注入system prompt
5. Parse (System.Text.Json提取) → 处理Markdown代码块包裹
6. Validate (JsonSchema.Net + Business Logic) → 失败最多3次重试
7. Cache Write (写入SQLite缓存 via sqlite-net)
8. Post-Process (填充默认值、业务预处理)
9. 返回 AgentResponse 给调用方
```

#### 2.6.6 降级链

```
Primary Model → Secondary Model → Tertiary Model → Cached Response → Static Template

示例 (编剧Agent):
  primary:   OpenAI GPT-4o
  secondary: OpenAI GPT-4o-mini
  tertiary:  Anthropic Claude-3-Haiku
  fallback:  adventure_blueprint_templates.json (内置50+模板)
```

#### 2.6.7 缓存策略

| 缓存类型 | 键生成 | 存储 | TTL |
|----------|--------|:----:|:---:|
| 场景描写 | semantic_hash(theme + party_summary) | SQLite via sqlite-net | 24h |
| NPC对话 | hash(npc_type + check_result) | SQLite | 12h |
| 冒险蓝图 | hash(tier + level + theme) | SQLite | 7d |
| 物品描述 | item_type + rarity | SQLite | 永久 |

#### 2.6.8 速率限制

```
双层限流机制:

第一层 — 本地限流:
  Global: 30 req/min, 50000 tokens/min
  Per-Agent:
    screenwriter: 3 req/min, 15000 tokens/min
    dm_agent:     20 req/min, 15000 tokens/min
    copywriter:   15 req/min, 8000 tokens/min
    balancer:     3 req/min, 6000 tokens/min

第二层 — API限流:
  HTTP 429 → 读取Retry-After header
  HTTP 429 → 切换到备选模型
```

#### 2.6.9 离线降级成本

| 场景 | 策略 | 玩家体验影响 |
|------|------|:----------:|
| API不可用 | 使用离线冒险模板 | 冒险多样性降低，但仍可玩 |
| 定额用完 | 降低模型等级 | 叙事质量下降，不丧失功能 |
| 超时 | 返回缓存/模板 | 叙事可能重复，核心玩法不受影响 |

---

### 2.7 世界状态管理器 (WorldStateManager)

#### 2.7.1 职责

世界状态管理器负责追踪冒险成功/失败对游戏世界的**永久影响**。这些影响存储在SQLite中，并在生成下一次冒险时作为LLM的上下文输入。

```csharp
public interface IWorldStateManager
{
    RegionState GetRegionState(string regionId);
    void SetRegionState(string regionId, RegionState state);
    FactionRelation GetFactionRelation(string factionId);
    void SetFactionRelation(string factionId, FactionRelation relation);
    void AddWorldEvent(WorldEvent worldEvent);
    List<WorldEvent> GetActiveEvents();
    AdventureMark GetAdventureMark(string adventureId);
    WorldStateContext GetWorldContextForLLM();

    event Action<string, RegionState> RegionChanged;
    event Action<string, FactionRelation> FactionChanged;
    event Action<WorldEvent> EventTriggered;
}
```

#### 2.7.2 数据分类

```
世界状态 = 持久变化的数据，影响后续游戏
───────────────────────────────────────────

区域状态:
  · 各区域的安全/危险等级
  · 已解锁/摧毁的城镇和地标
  · 当前活跃的势力及其影响力

势力关系:
  · 各势力对酒馆的友好度 (hostile/neutral/friendly/allied)
  · 各势力控制的区域
  · 活跃的势力冲突事件

世界事件:
  · 正在发生的重大事件 (瘟疫/战争/天灾)
  · 玩家行动触发的连锁反应
  · 即将到来的威胁 (倒计时)

冒险印记:
  · 已完成/失败的冒险记录
  · 未完成的任务线状态
  · 特殊NPC的生死状态
```

#### 2.7.3 与LLM的交互

```csharp
public record WorldStateContext
{
    public int TavernLevel { get; init; }
    public List<WorldEvent> ActiveEvents { get; init; } = new();
    public List<FactionInfo> Factions { get; init; } = new();
    public List<string> RecentChanges { get; init; } = new();
    public Dictionary<string, RegionState> Regions { get; init; } = new();
}

public class WorldStateManager : IWorldStateManager
{
    private readonly SQLiteConnection _db;

    public WorldStateContext GetWorldContextForLLM()
    {
        return new WorldStateContext
        {
            TavernLevel = GetTavernLevel(),
            ActiveEvents = GetActiveEvents(),
            Factions = GetAllFactions(),
            RecentChanges = GetRecentChanges(10),
            Regions = GetAllRegions()
        };
    }
}
```

---

### 2.8 UI系统 (UISystem)

#### 2.8.1 设计原则

```
UI设计原则:
───────────────────
1. 像素风一致: FF6风格窗口框架，深色镶边+深色半透明背景
2. 数据与表现分离: UI只负责展示，逻辑由各系统处理
3. 响应式布局: Myra Grid + StackPanel 适配不同分辨率
4. 骰子可见: 玩家可查看每个骰子的完整计算过程
5. 战斗日志: LLM叙事文本在战斗日志框中展示
6. 代码构建: 所有UI通过Myra XML布局定义 + C#代码控制逻辑
```

#### 2.8.2 Myra UI集成

```csharp
// Myra UI 初始化 — 在 Game1.LoadContent() 中
using Myra;
using Myra.Graphics2D.UI;

public class UIManager
{
    private Desktop _desktop;
    private readonly Dictionary<string, Widget> _panels = new();

    public void Initialize(Game game)
    {
        MyraEnvironment.Game = game;
        _desktop = new Desktop();
    }

    // 使用Myra XML布局加载UI面板
    public T LoadPanel<T>(string xmlPath) where T : Widget
    {
        var asset = ServiceLocator.Get<IResourceCache>().GetText(xmlPath);
        var panel = (T)Myra.Xml.Project.LoadFromXml(asset, null);
        _panels[typeof(T).Name] = panel;
        return panel;
    }

    // 数据绑定
    public void BindCombatLog(CombatLog log)
    {
        var logWidget = (ScrollViewer)_panels["CombatLogPanel"];
        log.OnEntryAdded += entry =>
        {
            // 程序日志行
            var proceduralLabel = new Label
            {
                Text = entry.ProceduralText,
                Font = FontStashSharp.DefaultFont,
                TextColor = Color.LightGray
            };
            // LLM叙事行
            var narrativeLabel = new Label
            {
                Text = entry.NarrativeText,
                Font = FontStashSharp.DefaultFont,
                TextColor = Color.White
            };
            logWidget.Content = new VerticalStackPanel
            {
                Children = { narrativeLabel, proceduralLabel }
            };
        };
    }
}
```

#### 2.8.3 Myra XML 布局示例

```xml
<!-- 战斗日志面板 (CombatLogPanel.xml) -->
<Grid RowSpacing="4" ColumnSpacing="4">
  <Grid.ColumnsProportions>
    <Proportion Type="Fill"/>
  </Grid.ColumnsProportions>
  <Grid.RowsProportions>
    <Proportion Type="Auto"/>
    <Proportion Type="Fill"/>
  </Grid.RowsProportions>

  <Label Grid.Row="0" Text="战斗日志"
         TextColor="#FFD700" Font="pixel-font-16"/>

  <ScrollViewer Grid.Row="1" Id="logScrollViewer">
    <VerticalStackPanel Id="logContent" Spacing="2"/>
  </ScrollViewer>
</Grid>
```

```csharp
// C#代码加载XML面板
public class CombatUI
{
    private Grid _combatPanel;
    private VerticalStackPanel _logContent;

    public void Initialize()
    {
        var uiManager = ServiceLocator.Get<UIManager>();
        _combatPanel = uiManager.LoadPanel<Grid>("UI/CombatLayout.xml");
        _logContent = (VerticalStackPanel)_combatPanel.FindChildById("logContent");
    }

    public void AppendLogEntry(LogEntry entry)
    {
        var narrativeLabel = new Label
        {
            Text = entry.NarrativeText,
            TextColor = Color.Wheat,
            Font = ServiceLocator.Get<IResourceCache>().GetFont("pixel-font-14"),
            Wrap = true
        };
        var proceduralLabel = new Label
        {
            Text = entry.ProceduralText,
            TextColor = new Color(180, 180, 180),
            Font = ServiceLocator.Get<IResourceCache>().GetFont("pixel-font-12"),
            Wrap = true
        };
        _logContent.Children.Add(narrativeLabel);
        _logContent.Children.Add(proceduralLabel);
    }
}
```

#### 2.8.4 UI子系统划分

| UI模块 | 关联系统 | 核心Myra组件 |
|--------|----------|-------------|
| 酒馆UI | TavernSystem | Grid, ListBox, Button, Label — 招募板、任务板、商店面板 |
| 战斗UI | CombatEngine | Grid, HorizontalStackPanel, ScrollViewer — 先攻条、行动菜单、战斗日志 |
| 地图UI | AdventureSystem | 自定义SpriteBatch渲染 + Myra叠加层 — 节点图、小地图 |
| 角色面板 | CharacterSystem | TabControl, Grid, Label — 属性页、装备页、技能页 |
| 对话UI | LLMGateway | TextBox, ListBox — 对话窗口、选项列表 |
| 结算UI | SettlementSystem | Grid, Label — 成功/失败画面、奖励展示 |
| 主菜单 | GameState | Menu, Button — 新游戏、继续、设置 |

#### 2.8.5 战斗日志双轨机制

```
战斗日志 = 程序日志 + LLM叙事
────────────────────────────────

程序日志 (程序生成，格式固定):
  "[战士] 攻击 [哥布林]: d20(13) + 5 = 18 vs AC 15 → 命中"
  "[战士] 伤害: 1d8(6) + 3 = 9 斩击"

LLM叙事 (DM Agent生成，描述性文本):
  "索林怒吼着挥动长剑，剑气划破空气，
  在哥布林肮脏的皮甲上留下一道深深的伤口。"

两者并行展示:
  ┌──────────────────────────────────────┐
  │ 战斗日志                              │
  │ ──────────────────────────────────    │
  │ 索林怒吼着挥动长剑，剑气划破空气，     │
  │ 在哥布林肮脏的皮甲上留下一道深深的伤口。│
  │                                       │
  │ [战士] d20(13) + 5 = 18 vs AC 15 → 命中│
  │ [战士] 1d8(6) + 3 = 9 斩击             │
  └──────────────────────────────────────┘
```

#### 2.8.6 像素风主题配置

```csharp
public class PixelTheme
{
    public static void Apply(Desktop desktop)
    {
        var styles = Desktop.DefaultStyles;

        // 窗口样式
        styles.WindowStyle = new WindowStyle
        {
            Background = new SolidBrush(new Color(20, 20, 40, 230)),
            Border = new SolidBrush(new Color(60, 50, 30)),
            BorderThickness = new Thickness(2)
        };

        // 按钮样式
        styles.ButtonStyle = new ButtonStyle
        {
            Background = new SolidBrush(new Color(40, 35, 25)),
            OverBackground = new SolidBrush(new Color(60, 50, 35)),
            PressedBackground = new SolidBrush(new Color(30, 25, 15)),
            TextColor = Color.White,
            Font = FontStashSharp.DefaultFont
        };

        // 标签样式
        styles.LabelStyle = new LabelStyle
        {
            TextColor = Color.White,
            Font = FontStashSharp.DefaultFont
        };

        // 滚动视图样式
        styles.ScrollViewerStyle = new ScrollViewerStyle
        {
            Background = new SolidBrush(new Color(10, 10, 20, 200))
        };
    }
}
```

---

## 3. 数据流与接口

### 3.1 核心数据流图

```
                    ┌───────────────────────────────────────┐
                    │          LLM API (云端)                │
                    │  OpenAI / Claude / 国产模型            │
                    └────────────┬──────────────────────────┘
                                 │ HTTP POST (JSON via HttpClient)
                                 ▼
┌──────────┐    ┌───────────────────────────────────────┐
│ 玩家操作  │───▶│           LLM Gateway                  │
│ (输入)    │    │  · 请求队列 · Schema验证 · 缓存 · 降级 │
└──────────┘    └────────────┬──────────────────────────┘
                             │ AgentResponse (已验证JSON)
                             ▼
                    ┌───────────────────┐
                    │  游戏逻辑层        │
                    │  ──────────────   │
                    │  · 冒险系统        │ ← 接收蓝图
                    │  · 战斗引擎        │ ← 接收叙事文本
                    │  · 酒馆系统        │ ← 接收招募/事件文本
                    │  · 结算系统        │ ← 接收惩罚叙事
                    └────────┬──────────┘
                             │
                    ┌────────▼──────────┐
                    │  程序状态更新      │
                    │  ──────────────   │
                    │  · 数值计算        │ ← 不依赖LLM
                    │  · 规则判定        │
                    │  · 分支逻辑        │
                    │  · 世界状态变更    │
                    └───────────────────┘
```

### 3.2 LLM与程序层的职责边界

```
"LLM只做皮肤层，程序做骨骼层"
────────────────────────────────

LLM做:
  · 生成NPC说的话（不是NPC做的事）
  · 写战斗描述文字（不是计算伤害数值）
  · 生成选项的风味描述（不是定义选项的后果）
  · 给角色取名字和写背景故事（不是决定角色的属性）
  · 描述伤疤的外观（不是选择伤疤的数值效果）

程序做:
  · 计算所有伤害、治疗、buff数值
  · 决定命中/未命中
  · 控制NPC的行动逻辑
  · 执行剧情分支的走向
  · 管理角色升级和属性变化
  · 计算战利品掉落
  · 应用状态效果
  · 变更世界状态
```

### 3.3 事件总线 (EventBus)

所有跨系统通信通过 `EventBus` 的C# event实现：

```csharp
public interface IEventBus
{
    // 通用事件发布/订阅
    void Publish<T>(T eventData) where T : GameEvent;
    void Subscribe<T>(Action<T> handler) where T : GameEvent;
    void Unsubscribe<T>(Action<T> handler) where T : GameEvent;
}

// 预定义游戏事件
public abstract record GameEvent
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string SourceId { get; init; } = "";
}

public record CombatStarted(EncounterData Encounter) : GameEvent;
public record CombatEnded(string Result, List<string> Casualties) : GameEvent;
public record AdventureStarted(string BlueprintId) : GameEvent;
public record AdventureCompleted(string Result, Dictionary<string, object> Changes) : GameEvent;
public record TavernLevelUp(int NewLevel) : GameEvent;
public record CharacterRecruited(CharacterData Character) : GameEvent;
public record CharacterDied(string CharacterId, string Cause) : GameEvent;
public record RelationshipChanged(string CharA, string CharB, int Delta) : GameEvent;
public record WorldEventTriggered(string EventId) : GameEvent;
public record LLMRequestStarted(string AgentId, string RequestId) : GameEvent;
public record LLMResponseReceived(string AgentId, string RequestId, string Status) : GameEvent;

// 内部实现
public class EventBus : IEventBus
{
    private readonly Dictionary<Type, Delegate> _handlers = new();
    private readonly object _lock = new();

    public void Publish<T>(T eventData) where T : GameEvent
    {
        lock (_lock)
        {
            if (_handlers.TryGetValue(typeof(T), out var handler))
                ((Action<T>)handler)?.Invoke(eventData);
        }
    }

    public void Subscribe<T>(Action<T> handler) where T : GameEvent
    {
        lock (_lock)
        {
            var type = typeof(T);
            if (_handlers.TryGetValue(type, out var existing))
                _handlers[type] = Delegate.Combine(existing, handler);
            else
                _handlers[type] = handler;
        }
    }

    public void Unsubscribe<T>(Action<T> handler) where T : GameEvent
    {
        lock (_lock)
        {
            if (_handlers.TryGetValue(typeof(T), out var existing))
                _handlers[typeof(T)] = Delegate.Remove(existing, handler);
        }
    }
}
```

### 3.4 数据流关键路径

**路径1: 玩家接任务 → 冒险开始**

```
玩家点击任务板 → TavernSystem.CreateAdventure(tier)
  → await LLMGateway.CallAgent<ScreenwriterAgent, AdventureBlueprint>(input)
    → 编剧Agent生成冒险蓝图
    → JsonSchema.Net Schema验证 + 平衡Agent验证
    → 返回 AdventureBlueprint
  → await AdventureSystem.Instantiate(blueprint, partyState)
    → 14步实例化算法 (纯C#，无LLM)
    → 返回 AdventureInstance
  → SceneManager.SwitchTo<AdventureScene>(instance)
```

**路径2: 战斗动作 → 玩家看到结果**

```
玩家选择"攻击" → CombatEngine.ExecuteAction(action)
  → DiceRoller.RollAttack(...)      # 纯程序计算
  → ActionResolver.Resolve(...)     # 纯程序计算
  → CombatLog.Record(...)           # 记录日志
  → EventBus.Publish(new AttackResolved(...))  # 事件驱动UI更新
  → UI.Update()                     # 更新数值显示
  → await LLMGateway.CallAgent<DMAgent, NarrativeText>(combatContext)  # 异步，不阻塞
    → DM Agent生成叙事文本
    → UI.AppendNarration(text)      # 追加到战斗日志
```

**路径3: 冒险结束 → 结算**

```
AdventureSystem.CheckCompletion() → 判定成功/失败
  → SettlementSystem.CalculateRewards()   # 纯程序
    → XP分配、战利品生成、世界状态变更
  → SettlementSystem.CalculatePenalties()  # 纯程序
    → 伤疤判定(预定义池)、资源消耗、关系变化
  → await LLMGateway.CallAgent<DMAgent, NarrativeText>(settlementContext)  # 异步
    → DM Agent生成结算叙事文本
  → SceneManager.SwitchTo<SettlementScene>(result)
```

### 3.5 接口契约

```
系统间接口规范:
───────────────
1. 所有接口参数为 C# record 或 JsonElement
2. 所有接口返回 record 或 void（通过 EventBus 传递结果）
3. LLM相关接口使用 async Task<T> await LLMGateway.CallAgent()
4. 非LLM接口使用同步调用
5. EventBus 事件数据包含 SourceId 和 Timestamp
```

---

## 4. 技术栈与工具

### 4.1 核心技术栈

| 层级 | 选型 | 版本 | 说明 |
|------|------|:----:|------|
| **游戏框架** | MonoGame | 3.8.5+ | MIT许可证，跨平台，C#/.NET原生 |
| **语言** | C# | 12+ | .NET 8+，record类型、模式匹配 |
| **场景/实体** | Nez | 最新 | Scene/Entity/Component ECS，内置FSM/行为树 |
| **地图系统** | MonoGame.Extended | 6.0+ | Tiled/LDtk地图加载，正交/等距 |
| **Roguelike工具** | GoRogue | 3.x | FOV视野、A*寻路、ArrayMap、MapGeneration |
| **UI框架** | Myra | 最新 | 像素风UI，XML布局+数据绑定 |
| **数据持久化** | sqlite-net | 最新 | 轻量ORM，SQLite原生绑定 |
| **LLM集成** | HttpClient + System.Text.Json | .NET内置 | HTTP/JSON原生支持 |
| **Schema验证** | JsonSchema.Net | 最新 | JSON Schema Draft 7+验证库 |
| **字体渲染** | FontStashSharp | 最新 | 动态字体+简体中文支持 |
| **内容管线** | MGCB (MonoGame Content Builder) | — | 纹理/音效/字体编译 |
| **AI辅助开发** | monogame-mcp | — | MCP协议AI辅助开发MonoGame项目 |
| **单元测试** | xUnit + FluentAssertions | 最新 | C#标准测试栈 |
| **版本控制** | Git + GitHub | — | 纯文本.cs/.csproj，方便diff |

### 4.2 NuGet包清单

```xml
<!-- TavernAndDestiny.csproj — NuGet依赖 -->
<ItemGroup>
  <!-- 游戏框架 -->
  <PackageReference Include="MonoGame.Framework.DesktopGL" Version="3.8.5.*" />

  <!-- 场景管理/ECS -->
  <PackageReference Include="Nez" Version="2.*" />

  <!-- 地图/Tiled支持 -->
  <PackageReference Include="MonoGame.Extended" Version="6.0.*" />
  <PackageReference Include="MonoGame.Extended.Content.Pipeline" Version="6.0.*" />
  <PackageReference Include="MonoGame.Extended.Tiled" Version="6.0.*" />

  <!-- Roguelike工具 -->
  <PackageReference Include="GoRogue" Version="3.*" />

  <!-- UI框架 -->
  <PackageReference Include="Myra" Version="1.*" />

  <!-- 数据持久化 -->
  <PackageReference Include="sqlite-net-pcl" Version="1.9.*" />

  <!-- JSON处理 -->
  <PackageReference Include="JsonSchema.Net" Version="7.*" />

  <!-- 字体渲染 -->
  <PackageReference Include="FontStashSharp.MonoGame" Version="1.*" />

  <!-- LLM集成（.NET内置，无需额外包） -->
  <!-- System.Net.Http + System.Text.Json 已包含在.NET 8+ SDK中 -->
</ItemGroup>

<!-- 测试项目 -->
<ItemGroup>
  <PackageReference Include="xunit" Version="2.*" />
  <PackageReference Include="FluentAssertions" Version="7.*" />
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
</ItemGroup>
```

### 4.3 工具链

| 用途 | 工具 | 说明 |
|------|------|------|
| **像素美术** | Aseprite | 精灵绘制、逐帧动画、像素字体 |
| **地图编辑** | Tiled | 基于Tile的地图编辑，Tiled格式 |
| **音频** | Bosca Ceoil / LMMS | 芯片音乐、像素风音效 |
| **项目管理** | GitHub Projects / Notion | 任务跟踪 |
| **LLM服务** | OpenAI GPT-4o-mini (主力) | 备用: DeepSeek / 通义千问 / Ollama本地 |
| **IDE** | JetBrains Rider / VS Code | C#开发 |
| **AI开发辅助** | opencode + monogame-mcp | AI-first开发范式，MCP协议辅助 |

### 4.4 项目结构

```
TavernAndDestiny/
├── src/
│   ├── TavernAndDestiny.csproj
│   ├── Program.cs                    # 入口: 创建Game1实例并Run()
│   ├── Core/
│   │   ├── Game1.cs                  # MonoGame主入口 (继承Game)
│   │   ├── ServiceLocator.cs         # 服务定位器模式
│   │   ├── EventBus.cs               # C#事件总线
│   │   └── GameState.cs              # 全局状态枚举/上下文
│   ├── Systems/
│   │   ├── Combat/
│   │   │   ├── CombatEngine.cs       # 战斗引擎主类
│   │   │   ├── CombatFSM.cs          # 有限状态机
│   │   │   ├── DiceRoller.cs         # 骰子工具
│   │   │   ├── ActionResolver.cs     # 行动结算器
│   │   │   ├── AISystem.cs           # AI行为系统
│   │   │   ├── ConditionSystem.cs    # 条件追踪
│   │   │   ├── InitiativeSystem.cs   # 先攻系统
│   │   │   ├── CombatLog.cs          # 战斗日志
│   │   │   ├── GoRogueMapManager.cs  # GoRogue集成
│   │   │   └── TerrainInteract.cs    # 地形交互
│   │   ├── Character/
│   │   │   ├── CharacterSystem.cs    # 角色系统主类
│   │   │   ├── CharacterData.cs      # 角色数据模型(record)
│   │   │   ├── CharacterGenerator.cs # 角色生成管线
│   │   │   ├── NumericalGenerator.cs # 数值层生成
│   │   │   ├── LevelUpManager.cs     # 升级管理
│   │   │   ├── RaceData.cs           # 种族数据
│   │   │   ├── ClassData.cs          # 职业数据
│   │   │   └── RelationshipSystem.cs # 关系系统
│   │   ├── Tavern/
│   │   │   ├── TavernSystem.cs       # 酒馆系统主类
│   │   │   ├── TavernLevelManager.cs # 酒馆升级
│   │   │   ├── RecruitmentManager.cs # 招募管理
│   │   │   ├── ShopSystem.cs         # 商店系统
│   │   │   ├── NPCScheduler.cs       # NPC调度
│   │   │   ├── EventSystem.cs        # 酒馆事件
│   │   │   └── RestSystem.cs         # 短休/长休
│   │   ├── Adventure/
│   │   │   ├── AdventureSystem.cs    # 冒险系统主类
│   │   │   ├── BlueprintParser.cs    # 蓝图解析器
│   │   │   ├── AdventureInstantiator.cs # 实例化引擎
│   │   │   ├── NodeGraphGenerator.cs # 节点图生成
│   │   │   ├── EncounterGenerator.cs # 遭遇生成器
│   │   │   ├── LootGenerator.cs      # 战利品生成
│   │   │   ├── SettlementSystem.cs   # 结算系统
│   │   │   └── DungeonGenerator.cs   # GoRogue地牢生成
│   │   ├── WorldState/
│   │   │   ├── WorldStateManager.cs  # 世界状态管理器
│   │   │   └── WorldStateData.cs     # 世界状态数据模型
│   │   └── Items/
│   │       ├── ItemSystem.cs         # 物品系统
│   │       ├── ItemData.cs           # 物品数据模型
│   │       └── EquipmentManager.cs   # 装备管理
│   ├── Gateway/
│   │   ├── LLMGateway.cs             # LLM网关主类
│   │   ├── LLMAgent.cs               # Agent基类(abstract)
│   │   ├── Agents/
│   │   │   ├── ScreenwriterAgent.cs   # 编剧Agent
│   │   │   ├── DMAgent.cs            # DM Agent
│   │   │   ├── CopywriterAgent.cs     # 文案Agent
│   │   │   └── BalancerAgent.cs       # 平衡Agent
│   │   ├── Validation/
│   │   │   └── SchemaValidator.cs     # JsonSchema.Net封装
│   │   ├── Cache/
│   │   │   └── CacheManager.cs        # SQLite语义缓存
│   │   └── Fallback/
│   │       ├── FallbackManager.cs     # 降级管理
│   │       └── Templates/            # 离线模板(JSON)
│   ├── Scenes/
│   │   ├── MainMenuScene.cs          # 主菜单场景
│   │   ├── TavernScene.cs            # 酒馆场景
│   │   ├── AdventureScene.cs         # 冒险地图场景
│   │   ├── CombatScene.cs            # 战斗场景
│   │   ├── LoadingScene.cs           # 加载场景
│   │   ├── SettlementScene.cs        # 结算场景
│   │   └── DialogueScene.cs          # 对话场景
│   ├── Entities/
│   │   ├── CharacterEntity.cs        # 角色实体(Nez Entity)
│   │   ├── EnemyEntity.cs            # 敌人实体
│   │   ├── ItemEntity.cs             # 物品实体
│   │   └── NPCEntity.cs              # NPC实体
│   ├── UI/
│   │   ├── UIManager.cs              # UI管理器
│   │   ├── PixelTheme.cs             # 像素风主题
│   │   ├── Layouts/                  # Myra XML布局文件
│   │   │   ├── TavernLayout.xml
│   │   │   ├── CombatLayout.xml
│   │   │   ├── CharacterPanel.xml
│   │   │   └── DialoguePanel.xml
│   │   └── Widgets/                  # 自定义UI组件
│   │       ├── DiceWidget.cs
│   │       ├── InitiativeBar.cs
│   │       └── CombatLogWidget.cs
│   └── Data/
│       ├── Database/
│       │   ├── DatabaseContext.cs     # sqlite-net DbContext
│       │   └── Migrations/           # 数据库迁移
│       ├── Templates/                 # JSON离线模板
│       │   ├── adventures/            # 冒险模板
│       │   ├── narratives/            # 叙事模板
│       │   └── descriptions/          # 描述模板
│       ├── Config/                    # 配置表(JSON)
│       │   ├── races.json
│       │   ├── classes.json
│       │   ├── spells.json
│       │   ├── monsters.json
│       │   └── items.json
│       └── Schemas/                   # JSON Schema定义
│           ├── adventure_blueprint.schema.json
│           ├── npc_dialogue.schema.json
│           ├── narrative_text.schema.json
│           ├── item_description.schema.json
│           ├── balance_report.schema.json
│           ├── character_narrative.schema.json
│           └── settlement_result.schema.json
├── tests/
│   ├── TavernAndDestiny.Tests.csproj
│   ├── Unit/
│   │   ├── Combat/
│   │   │   ├── DiceRollerTests.cs
│   │   │   ├── ActionResolverTests.cs
│   │   │   ├── CombatFSMTests.cs
│   │   │   └── ConditionSystemTests.cs
│   │   ├── Character/
│   │   │   ├── CharacterGeneratorTests.cs
│   │   │   ├── RaceDataTests.cs
│   │   │   └── LevelUpTests.cs
│   │   ├── Adventure/
│   │   │   ├── BlueprintParserTests.cs
│   │   │   ├── EncounterGeneratorTests.cs
│   │   │   └── DungeonGeneratorTests.cs
│   │   └── Gateway/
│   │       ├── SchemaValidatorTests.cs
│   │       └── CacheManagerTests.cs
│   └── Integration/
│       ├── CombatToCharacterTests.cs
│       └── AdventureInstantiateTests.cs
├── Content/
│   ├── Sprites/
│   │   ├── Characters/
│   │   ├── Enemies/
│   │   ├── Tilesets/
│   │   └── UI/
│   ├── Tilesets/                    # Tiled .tsx文件
│   ├── Maps/                        # Tiled .tmx文件
│   ├── Fonts/
│   │   └── NotoSansCJKsc-Regular.ttf # FontStashSharp使用
│   ├── Audio/
│   │   ├── BGM/
│   │   └── SFX/
│   └── Content.mgcb                 # MGCB内容清单
└── docs/
    ├── GDD-v1.md
    ├── technical/
    │   ├── 01-engine-selection.md
    │   ├── 02-overall-architecture.md
    │   └── ...
    └── subsystems/
        ├── 01-character-system.md
        ├── 02-llm-integration.md
        ├── 04-combat-system.md
        ├── 05-map-exploration.md
        └── 06-adventure-generation.md
```

### 4.5 JSON Schema 中心管理

所有Schema文件集中在 `Data/Schemas/` 目录，LLM Gateway在运行时通过 `JsonSchema.Net` 加载使用：

```csharp
public class SchemaValidator
{
    private readonly Dictionary<string, JsonSchema> _schemas = new();

    public void LoadFromDirectory(string schemaDir)
    {
        foreach (var file in Directory.GetFiles(schemaDir, "*.schema.json"))
        {
            var json = File.ReadAllText(file);
            var schema = JsonSchema.FromText(json);
            var name = Path.GetFileNameWithoutExtension(file);
            _schemas[name] = schema;
        }
    }

    public List<string> Validate(JsonNode data, JsonSchema schema)
    {
        var result = schema.Evaluate(data, new EvaluationOptions
        {
            OutputFormat = OutputFormat.List
        });

        return result.Errors?.Select(e => $"{e.Key}: {e.Value}").ToList() ?? new();
    }

    public JsonSchema GetSchema(string name) => _schemas[name];
}
```

Schema文件清单：

```
Data/Schemas/
├── adventure_blueprint.schema.json     # 编剧Agent输出
├── npc_dialogue.schema.json            # DM Agent对话输出
├── narrative_text.schema.json          # DM Agent叙事输出
├── item_description.schema.json        # 文案Agent输出
├── balance_report.schema.json          # 平衡Agent输出
├── character_narrative.schema.json     # 角色叙事层
└── settlement_result.schema.json       # 结算叙事输出
```

### 4.6 语言政策

| 上下文 | 语言 | 示例 |
|--------|------|------|
| 面向玩家的叙事文本 | 简体中文 | "你推开酒馆的木门，一股温暖的炉火气息扑面而来..." |
| UI界面文字 | 简体中文 | "招募"、"装备"、"法术"、"开始冒险" |
| 内部技术标识符 | 英文PascalCase/camelCase | `CharacterId`, `adventureBlueprint`, `NodeType` |
| JSON Schema字段名 | 英文snake_case | `plot_outline`, `interaction_tags`, `difficulty_profile` |
| 枚举值 | 英文PascalCase | `Combat`, `Dialogue`, `Exploration` |
| LLM Agent System Prompt | 中文 (要求输出中文叙事) | 见llm-integration.md §3.1.2 |

---

## 5. 开发路线图

### 5.1 阶段总览

```
Phase 1 (MVP)           Phase 2              Phase 3              Phase 4
"第一次冒险"            "酒馆活起来"          "长路漫漫"             "命运之书"
12-16周                12周                  12周                  12周
```

### 5.2 Phase 1 — MVP ("第一次冒险")

**目标**: 玩家能招募4人队伍 → 完成短冒险 → 体验核心循环

| 优先级 | 模块 | C#实现内容 | 估算 |
|:------:|------|-----------|:----:|
| P0 | 战斗引擎 | CombatFSM, DiceRoller, ActionResolver, AISystem, GoRogue FOV/寻路 | 6-8周 |
| P0 | 角色系统 | CharacterData(record), NumericalGenerator, 3职业×3种族, Lv1-5 | 3-4周 |
| P0 | 酒馆UI | Myra XML布局, 招募板(9预设角色), 任务板, 基础装备管理 | 2-3周 |
| P0 | 地图/探索 | Nez AdventureScene, GoRogue地牢生成, 节点导航, 3房间模板 | 3-4周 |
| P0 | LLM Gateway | HttpClient + System.Text.Json基础Gateway, DMAgent(战斗叙述), 文案Agent | 2-3周 |
| P0 | 美术资源 | 基础Tileset, 3角色精灵, 3敌人精灵, MGCB内容管线 | 持续 |
| P1 | 短冒险模板 | 5个主题的离线模板JSON | 1-2周 |
| P1 | 结算系统 | SettlementSystem, XP/战利品计算 | 2周 |
| P2 | 音效 | 骰子音效, 基础战斗音效 | 1周 |

**MVP不包含**:
- 酒馆升级、铁匠/炼金/图书馆/神殿
- 角色关系系统、伤疤系统
- 中冒险、长冒险、分支地图
- 编剧Agent、平衡Agent（MVP用模板代替）
- BGM、环境音效

### 5.3 Phase 2 — "酒馆活起来"

| 模块 | C#实现内容 |
|------|-----------|
| 酒馆系统 | TavernLevelManager, ShopSystem, 铁匠/炼金 |
| 角色系统 | RelationshipSystem, ScarSystem, 6种关系类型 |
| 冒险系统 | 编剧Agent上线、平衡Agent上线、短冒险×10 |
| LLM | ScreenwriterAgent + BalancerAgent完整管线、缓存策略优化 |
| 酒馆事件 | EventSystem, 6种事件, 机械结果+LLM叙事 |
| NPC调度 | NPCScheduler, 固定NPC+轮换NPC |
| 美术 | 酒馆区域视觉、更多敌人精灵 |
| 商店系统 | 杂货商、铁匠铺、炼金店库存+定价 |

### 5.4 Phase 3 — "长路漫漫"

| 模块 | C#实现内容 |
|------|-----------|
| 冒险系统 | 中冒险解锁、分支地图、场景交互标签(8种)、GoRogue高级地图 |
| 角色系统 | 知识传承、30+专长、新职业(牧师/游侠/圣武士) |
| 战斗 | 更多敌人类型、Boss多阶段、地形交互(Flammable/Pushable等) |
| 酒馆 | 图书馆(Lv5)、训练系统、神殿(Lv7) |
| 结算 | ScarSystem深化、中惩罚系统、WorldState变更 |
| LLM | 中冒险蓝图生成、上下文优化 |
| 音效 | BGM基础、环境音 |

### 5.5 Phase 4 — "命运之书"

| 模块 | C#实现内容 |
|------|-----------|
| 冒险系统 | 长冒险、多结局系统、三幕结构 |
| 角色系统 | 全12职业、英雄传记(LLM叙事)、传承深化 |
| 酒馆 | 英雄之壁完整、Prestige系统、全区域 |
| 结算 | 灾难性惩罚、全灭处理、世界剧变 |
| LLM | 长冒险蓝图、Agent协作优化 |
| 美术 | 完整酒馆全景、Boss动画、特效 |
| 世界演进 | 完整世界状态网络、势力系统、区域状态 |

### 5.6 测试策略

```
测试分层:
─────────

Unit Tests (单元测试) — 覆盖所有纯程序逻辑
  使用: xUnit + FluentAssertions
  · DiceRoller: 骰子概率分布、优势/劣势
  · CombatEngine: 攻击检定、伤害计算、状态应用
  · CharacterSystem: 属性计算、升级公式、法术位
  · AdventureInstantiator: 蓝图解析、实例化算法、遭遇生成
  · SettlementSystem: XP计算、战利品生成
  · GoRogue集成: FOV计算、寻路验证、地图生成
  全部可离线运行，不依赖LLM

Integration Tests (集成测试) — 系统间交互
  · 角色→战斗: 角色属性正确传入战斗引擎
  · 战斗→结算: 战斗胜负正确触发结算
  · 冒险→地图: 蓝图正确实例化为地图节点
  · LLM→冒险: Schema验证通过后正确解析
  · GoRogue→战斗: FOV/寻路正确集成到战斗系统

E2E Tests (端到端) — 完整游戏流程
  · 酒馆→招募→任务→冒险→战斗→结算→返回
  · LLM全流程: 请求→生成→验证→展示

手动测试:
  · LLM叙事质量评估 (不能自动化)
  · 战斗平衡性测试
  · 像素美术视觉验收
```

---

## 6. 风险与应对

### 6.1 技术风险

| 风险 | 概率 | 影响 | 应对措施 |
|:----:|:----:|:----:|----------|
| **LLM API成本超出预算** | 中 | 高 | Token预算控制、缓存策略、离线模板降级、选择性调用 |
| **LLM输出不符合Schema** | 中 | 中 | 严格重试机制(最多3次)、增强prompt(附带失败原因)、兜底默认值 |
| **MonoGame无可视化编辑器** | 高 | 中 | 代码创建所有对象(代码即场景)，配合Tiled编辑地图，monogame-mcp辅助调试 |
| **MonoGame社区较小** | 中 | 中 | 核心框架稳定，GoRogue/Nez/Myra有独立维护，关键路径自己维护 |
| **C#热重载限制** | 中 | 中 | 开发使用dotnet watch + MonoGame的HotReload，核心循环提前充分测试 |
| **GoRogue与Nez版本兼容** | 低 | 中 | 锁定稳定版本，上游更新前在CI中验证兼容性 |
| **中文像素字体现成选择少** | 低 | 低 | 使用Noto Sans CJK (FontStashSharp动态加载)或自建位图字体 |
| **Myra UI对回合制回合复杂度** | 中 | 中 | Myra Grid/StackPanel构建复杂布局，自定义Widget处理战斗菜单 |

### 6.2 MonoGame特有风险

| 风险 | 概率 | 影响 | 应对措施 |
|:----:|:----:|:----:|----------|
| **无场景编辑器，全部代码手写** | 确定 | 中 | 建立Entity创建工厂和Scene初始化模板，使用monogame-mcp辅助生成样板代码 |
| **内容管线(MGCB)配置复杂** | 中 | 中 | 建立标准化的Content.mgcb模板，自动化构建脚本 |
| **无内置TileMap/寻路系统** | 确定 | 中 | Nez提供基础支持，GoRogue覆盖寻路/FOV/地图生成 |
| **无内置UI系统** | 确定 | 中 | Myra UI成熟稳定，XML布局可热修改 |
| **.NET版本和依赖冲突** | 低 | 中 | 使用global.json锁定.NET 8 SDK，NuGet版本锁定文件 |

### 6.3 产品风险

| 风险 | 概率 | 影响 | 应对措施 |
|:----:|:----:|:----:|----------|
| **LLM叙事质量不稳定** | 高 | 中 | Schema约束+System Prompt迭代、人工评估集、缓存聚合评分高的输出 |
| **DND 5e规则复杂度超预期** | 中 | 高 | MVP阶段精简规则(只保留3职业+基本规则)、渐进式规则完善 |
| **Roguelike节奏与DND规则冲突** | 中 | 中 | 通过GDD§5.3-5.4的Roguelike调整参数解决，IP阶段验证 |
| **玩家对抗性叙事疲劳** | 中 | 低 | 缓存+多样化模板、控制短冒险中LLM调用频率 |
| **美术风格不统一** | 中 | 中 | 购买统一风格的Tileset素材包，AI生成后手动调整 |

### 6.4 项目风险

| 风险 | 概率 | 影响 | 应对措施 |
|:----:|:----:|:----:|----------|
| **1-3人团队开发周期过长** | 高 | 高 | MVP严格范围控制、优先保证核心循环可玩、非核心功能推迟 |
| **LLM API提供商变更/涨价** | 中 | 高 | Gateway层抽象多模型支持、降级链包含不同供应商 |
| **第三方NuGet包停维护** | 低 | 中 | 核心层自建抽象(尤其LLM Gateway和SQLite)，不直接依赖特定包API |
| **git合并冲突(C#/XML文件)** | 低 | 低 | 纯文本.cs/.csproj/.xml可diff，模块化分文件管理 |

### 6.5 架构层面的安全护栏

```
架构安全护栏:
─────────────

① LLM输出隔离:
   LLM响应经JsonSchema.Net验证 → 业务逻辑验证 → 后处理
   任何不符合Schema的字段被拒绝/使用默认值
   数值字段(CR/伤害/DC)由程序计算或验证，不接受LLM提供的数值

② 离线降级保障:
   内置50+冒险模板、100+叙事模板、物品/角色描述模板
   LLM完全不可用时，游戏核心循环不受影响
   降级链: 主模型 → 备选模型 → 缓存 → 模板

③ 编译器即验证器:
   dotnet build 作为AI代码质量第一道防线
   所有C#代码在提交前必须通过dotnet build无错误
   CI pipeline: dotnet build → dotnet test → 手动验收测试

④ 服务降级:
   LLM调用超时 → 不阻塞主线程 → 返回默认叙事文本
   战斗引擎完全离线运行，LLM只负责事后叙事渲染
   Token预算是软限制（超限降级模型等级，不拒绝服务）

⑤ 数据安全:
   本地SQLite存档 + JSON备份
   存档文件独立于游戏目录
   LLM请求日志不包含敏感信息（不上传角色数据原文到外部服务）
```

### 6.6 备选方案

| 场景 | 备选方案 |
|------|----------|
| MonoGame 暴露出严重缺陷 | FNA (MonoGame兼容替代品) 或 Stride Engine (迁移成本高) |
| 角色系统复杂度失控 | 从全DND 5e规则退化为简化版（保留核心6维+HP+AC） |
| LLM成本过高 | 全面切换到本地模型(Ollama+DeepSeek)或纯模板驱动 |
| 团队无法完成MVP | 缩小范围：仅战斗引擎+酒馆基本交互，取消LLM集成 |
| Nez废弃/不维护 | 独立维护ECS层，或迁移到MonoGame.Extended的ECS |

---

> **文档版本**: v2.0 (MonoGame迁移版)
> **创建日期**: 2026-05-05
> **前置文档**: 01-engine-selection.md, GDD-v1.md, 各子系统设计文档
> **下一阶段**: 确认本文档后进入Phase 1 MVP开发，从战斗引擎和角色系统开始
