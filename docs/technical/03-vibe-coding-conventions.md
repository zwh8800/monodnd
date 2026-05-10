# Vibe Coding 开发规约

> 文档版本：v1.0
> 更新日期：2026-05-05
> 适用范围：《酒馆与命运》全部 C# 代码开发
> 前置文档：01-engine-selection.md, 02-overall-architecture.md, GDD-v1.md

---

## 目录

1. [Vibe Coding 概述](#1-vibe-coding-概述)
2. [工具链与工作流](#2-工具链与工作流)
3. [C# 代码规范](#3-c-代码规范)
4. [AI 代码生成验证流程](#4-ai-代码生成验证流程)
5. [数据驱动设计规范](#5-数据驱动设计规范)
6. [Git 工作流与版本管理](#6-git-工作流与版本管理)
7. [Sisyphus Agent 使用规约](#7-sisyphus-agent-使用规约)

---

## 1. Vibe Coding 概述

### 1.1 什么是 Vibe Coding

Vibe Coding 是一种 AI-first 开发范式。AI 承担主要编码工作，人类开发者负责架构决策、代码审查和质量把控。开发者描述需求，AI 生成实现，编译器验证正确性。

这不是"让 AI 帮你写代码"那么简单。这是一套完整的工作流：AI 生成代码，编译器捕获错误，LSP 提供实时反馈，自动化测试验证逻辑。人类从"怎么写"中解放出来，专注于"做什么"和"做得对不对"。

### 1.2 核心理念

**代码即场景。** 所有游戏对象通过 C# 代码创建。不需要可视化编辑器，不需要拖拽节点。AI 擅长生成代码，就给 AI 代码让它生成。MonoGame 没有 GUI 编辑器，这反而是优势，AI 能覆盖 100% 的开发工作。

**编译器即验证器。** AI 生成的代码一定有错误。编译器在 2-5 秒内给出确定的错误报告，比运行时发现的隐晦 bug 好处理一百倍。

**渐进式类型安全。** 类型安全是一条光谱。运行时检查（Python/GDScript）在最左端，正式验证在最右端。本项目占据编译期类型检查到 JSON Schema 验证的区间，这是 AI 代码质量保证的最佳实践区间。

### 1.3 三道防线

AI 代码质量验证的三道防线，按反馈速度排列：

```
第一道：dotnet build（编译期）
  反馈周期 2-5 秒
  捕获：语法错误、类型不匹配、缺少引用、拼写错误

第二道：LSP 实时检查（开发期）
  反馈周期：即时
  捕获：类型警告、未使用变量、空引用提示

第三道：dotnet test（自动化测试）
  反馈周期 10-30 秒
  捕获：逻辑错误、边界条件、回归问题
```

三道防线缺一不可。编译通过不等于逻辑正确，测试通过不等于架构合理。人类审查是最终把关。

### 1.4 适用范围

本规约适用于项目的全部 C# 代码开发，包括游戏核心逻辑、渲染和表现层、LLM 集成层、数据层、测试代码和构建配置。

### 1.5 核心原则

| 原则 | 说明 |
|------|------|
| 编译器先于运行 | `dotnet build` 通过之前，不需要运行游戏 |
| 数据驱动 | 数值配置在 JSON/SQLite，不在代码中硬编码 |
| LLM 是皮肤层 | LLM 生成叙事文本，不决策数值和规则 |
| 接口优于实现 | 参数类型用接口，不用具体类 |
| 测试覆盖逻辑 | 纯程序逻辑必须有单元测试 |
| 一个类一个职责 | 严格 SRP，不堆砌功能 |
| 失败必须优雅 | LLM 不可用时，游戏核心不受影响 |

---

## 2. 工具链与工作流

### 2.1 opencode + OhMyOpenCode (Sisyphus) 工作流

**opencode** 是终端 AI 编程工具。**OhMyOpenCode** 是 opencode 的扩展框架，提供 Agent 编排能力。**Sisyphus** 是 OhMyOpenCode 的主编排 Agent，负责任务分解、子任务委托、结果验证和代码合并。

Sisyphus 不直接写代码。它像项目经理一样把任务拆成小块，分给专门的子 Agent，然后验证结果。

#### 2.1.1 标准工作流

```
用户描述需求
  → Sisyphus 分析需求，分解任务
  → 并行委托给子 Agent（explore/librarian/oracle/deep）
  → 子 Agent 生成 C# 代码
  → Sisyphus 运行 dotnet build 验证编译
  → 编译失败 → 返回子 Agent 修复
  → 编译通过 → 运行 dotnet test
  → 测试失败 → 返回子 Agent 修复
  → 全部通过 → Sisyphus 审查代码质量
  → 审查通过 → 合并代码到工作区
  → 通知用户完成
```

#### 2.1.2 典型场景

```
用户输入：给 CombatEngine 添加借机攻击规则

Sisyphus 分解：
  1. explore: 搜索当前 CombatEngine 实现
  2. oracle: 确认 DND 5e 借机攻击实现方案
  3. deep: 实现逻辑
  4. deep: 添加单元测试

验证：dotnet build → dotnet test → 审查 → 合并
```

### 2.2 monogame-mcp 集成

MCP（Model Context Protocol）让 AI 助手直接操作 MonoGame 项目。monogame-mcp 是 MCP 在 MonoGame 生态中的实现。

#### 2.2.1 核心功能

| 功能 | 说明 |
|------|------|
| 项目结构感知 | AI 理解 .csproj 的依赖关系 |
| Entity/Component 生成 | 基于自定义 ECS 架构自动生成代码 |
| Content Pipeline 操作 | 管理 Content.mgcb 和资源文件 |
| NuGet 包管理 | 增删改包引用 |
| XNB 编译触发 | 调用 MGCB 编译原始资源 |
| SpriteBatch 样板生成 | 生成标准渲染循环代码 |

#### 2.2.2 使用场景

创建新实体时，AI 通过 monogame-mcp 创建 Entity、添加 SpriteRenderer 组件、设置纹理路径。添加资源时，AI 将新图片添加到 Content.mgcb，触发 MGCB 编译，在代码中引用编译后的 .xnb。

monogame-mcp 是加分项不是必需品。AI 通过标准 C# 代码生成也能工作。它生成的代码同样需要 `dotnet build` 验证。

### 2.3 开发循环

```
需求描述 → Sisyphus 分解 → Agent 实现 → dotnet build → 修复编译错误
  → dotnet test → 修复测试 → 代码审查 → 合并
```

各环节退出标准：

| 环节 | 退出标准 |
|------|----------|
| 需求描述 | 用户确认需求理解正确 |
| 任务分解 | Sisyphus 列出子任务清单，用户确认 |
| Agent 实现 | 子 Agent 完成代码生成并提交 |
| dotnet build | 零错误零警告 |
| dotnet test | 全部测试通过 |
| 代码审查 | 审查清单全部通过 |
| 合并 | 代码合并到目标分支 |

### 2.4 IDE 配置

推荐 VS Code + C# Dev Kit 或 JetBrains Rider。两者都提供一流的 C# LSP 支持。

必须启用 **Nullable Reference Types**（`<Nullable>enable</Nullable>`）。这是编译器检测空引用的关键开关。

---

## 3. C# 代码规范

### 3.1 命名规范

| 元素 | 规则 | 示例 |
|------|------|------|
| 命名空间 | PascalCase | `TavernAndDestiny.Systems.Combat` |
| 类 | PascalCase | `CombatEngine`, `CharacterData` |
| 接口 | IPascalCase | `ICombatEngine`, `ICharacterSystem` |
| 方法 | PascalCase | `GetCharacter()`, `CalculateDamage()` |
| 公有属性/字段 | PascalCase | `MaxHitPoints`, `PartySize` |
| 私有字段 | _camelCase | `_maxHp`, `_currentLevel` |
| 常量 | UPPER_SNAKE_CASE | `MAX_PARTY_SIZE`, `DEFAULT_AC` |
| 枚举类型 | PascalCase | `AbilityScore`, `DamageType` |
| 枚举值 | PascalCase | `Strength`, `Slashing` |
| 局部变量 | camelCase | `damageRoll`, `targetAc` |
| 参数 | camelCase | `characterId`, `targetEntity` |

文件命名与主类一致：`CombatEngine.cs` 对应 `class CombatEngine`。一个文件一个公开类。接口文件以 I 开头。

### 3.2 数据结构规范

**不可变数据用 record 类型。**

```csharp
public record CharacterData(string CharacterId, CharacterStats Stats);
public record DiceRollResult(int Total, bool IsCritical, bool IsFumble);
```

**可变数据用 class。**

```csharp
public class CombatEngine
{
    private CombatState _state;
    public void ExecuteAction(IAction action) { ... }
}
```

**枚举用 enum 不用 int。** 枚举值全部显式指定。

```csharp
public enum DamageType
{
    Slashing = 1, Piercing = 2, Bludgeoning = 3,
    Fire = 4, Cold = 5, Lightning = 6,
    Necrotic = 7, Radiant = 8, Psychic = 9,
    Acid = 10, Poison = 11, Thunder = 12, Force = 13
}
```

**JSON 序列化统一使用 System.Text.Json。**

```csharp
public class AdventureBlueprint
{
    [JsonPropertyName("blueprint_id")]
    public string BlueprintId { get; init; }
}
```

#### 3.2.1 禁止的操作

- 禁止 `dynamic`。放弃编译期类型检查等于放弃第一道防线。
- 禁止 `object` 做参数类型。用泛型或接口。
- 避免不必要的 `var`。只在类型从右侧表达式明确可见时使用。
- 避免 `as` 转换后不检查 null。用模式匹配代替。

### 3.3 架构规范

**单一职责原则。** 一个类只做一件事。如果无法用一句话描述类的职责，说明需要拆分。

```csharp
// CombatEngine → 管理战斗流程
// DiceRoller → 处理骰子检定
// ConditionSystem → 管理状态效果
```

**系统间通过 IEventBus 解耦。** 不直接引用其他系统的具体类。

```csharp
public class CombatEngine
{
    private readonly IEventBus _eventBus;
    public void OnCharacterDeath(string characterId)
    {
        _eventBus.Publish(new CharacterDiedEvent(characterId));
    }
}
```

**使用 ServiceLocator 管理依赖。** 注册在 GameRoot 初始化时。

```csharp
ServiceLocator.Register<IEventBus>(eventBus);
ServiceLocator.Register<ICombatEngine>(combatEngine);
var engine = ServiceLocator.Resolve<ICombatEngine>();
```

**数据驱动。** 数值配置在 JSON 中。

```csharp
// 正确：从 JSON 读取
public class RaceData
{
    [JsonPropertyName("race_id")] public string RaceId { get; init; }
    [JsonPropertyName("ability_bonuses")] public Dictionary<string, int> AbilityBonuses { get; init; }
    [JsonPropertyName("speed")] public int Speed { get; init; }
}
// 错误：if (race == "elf") { dex += 2; } 硬编码
```

**接口优于实现。** 方法参数用接口。

```csharp
// 正确
public void AddCombatant(ICombatant combatant) { ... }
// 错误：每种类型一个重载
public void AddCombatant(PlayerCharacter pc) { ... }
public void AddCombatant(EnemyCharacter ec) { ... }
```

### 3.4 自定义 ECS 特定规范（非 Nez）

```csharp
// Entity 创建：scene.CreateEntity("name") 而非 new Entity()
var player = scene.CreateEntity("player");
player.AddComponent(new SpriteRenderer());

// Component 添加：entity.AddComponent()
// 构造函数不做繁重操作，放在 OnAddedToEntity / OnStart 中
public class HealthComponent : Component
{
    public HealthComponent(int maxHp) => _maxHp = maxHp; // 只赋值
    public override void OnAddedToEntity() { /* 注册事件 */ }
    public override void OnStart() { /* 启动逻辑 */ }
}

// 场景切换：GameRoot.Instance.StartSceneTransition()
GameRoot.Instance.StartSceneTransition(new CombatScene(data));

// 避免在 Update() 中做繁重计算
```

### 3.5 GoRogue 特定规范

```csharp
// 地图：使用 ArrayMap 或 Map 作为基础数据结构
// FOV：使用 GoRogue.FOV
public class PlayerFovSystem
{
    private FOV _fov;
    public void RecalculateFov(Point pos, int radius = 8)
    {
        _fov = new FOV(walkabilityMap);
        _fov.Calculate(pos.X, pos.Y, radius, Radius.SQUARE); // GoRogue 2.6.4 使用 Radius.SQUARE
    }
}

// 寻路：使用 GoRogue.Pathfinding.AStar
var path = _aStar.ShortestPath(start, end);

// 地图生成：使用 GoRogue.MapGeneration.Generators
var generator = new CaveGenerator(width, height);
generator.Generate(map);
```

使用 `IMapView<T>` 接口作为参数类型。注意 GoRogue 的 `Coord` 与自定义ECS的 `Point` 之间坐标系转换。

### 3.6 Myra UI 特定规范

**代码布局优先。** UI 用 C# 代码创建，匹配 AI 工作流。

```csharp
public class CombatUI : Widget
{
    public CombatUI()
    {
        var panel = new Panel { Width = 300, Height = 400 };
        panel.AddChild(new Label { Text = "选择行动" });
        panel.AddChild(new ListBox());
        AddChild(panel);
    }
}
```

**复杂布局可用 XML 定义 + 代码绑定。** 视觉效果主题配置集中在 `UITheme.cs`。UI 只负责展示和输入收集，不包含业务逻辑。

---

## 4. AI 代码生成验证流程

### 4.1 三道防线详解

#### 第一道防线：dotnet build

```
命令: dotnet build src/TavernAndDestiny/TavernAndDestiny.csproj
反馈周期: 2-5 秒
```

编译器捕获的常见 AI 代码错误类型：

| 错误类型 | AI 常见原因 | 编译器信息 |
|----------|-------------|-----------|
| 语法错误 | 括号不匹配、缺少分号 | CS1002, CS1026 |
| 类型不匹配 | 混淆参数类型 | CS0266, CS1503 |
| 缺少引用 | 未加 using | CS0246, CS0103 |
| 方法未定义 | AI 幻想了方法名 | CS0117 |
| 空引用风险 | 未检查可空类型 | CS8600 |
| switch 未穷举 | 漏掉枚举值 | CS8509 |
| 泛型约束违反 | 不满足 where 条件 | CS0311 |

#### 第二道防线：LSP 实时检查

C# 的 LSP 实现（Roslyn）是业界最成熟的之一。AI 通过 opencode 生成代码时 LSP 同样生效，即时标记未使用变量、类型推断冲突、可能的空引用和代码风格问题。

#### 第三道防线：dotnet test

```
命令: dotnet test
框架: xUnit + FluentAssertions
反馈周期: 10-30 秒
```

测试覆盖范围：纯逻辑单元测试（战斗引擎、骰子、属性计算）、系统集成测试（系统间交互）、Schema 验证测试（LLM 输出格式）、数据加载测试（JSON/SQLite 完整性）。

### 4.2 编译错误修复 SOP

```
1. dotnet build 发现错误
2. AI 根据错误代码（CSxxxx）定位问题
3. AI 修复代码（不添加 #pragma warning disable，不改为 dynamic 绕过，不使用 as 不检查 null）
4. 重新 dotnet build
5. 重复直到零错误零警告
6. 启用 TreatWarningsAsErrors=true，所有警告视为错误
```

常见错误代码速查：CS0103 名称不存在、CS0117 类型不包含某成员、CS0246 类型找不到、CS0266 无法隐式转换、CS1503 参数不匹配、CS8600 空引用警告。

### 4.3 测试要求

| 模块 | 测试要求 |
|------|----------|
| 战斗引擎 | 必须有单元测试 |
| 骰子系统 | 必须有单元测试 |
| 角色系统 | 必须有单元测试 |
| 冒险实例化 | 必须有单元测试 |
| LLM Gateway | 必须有集成测试 + 降级测试 |

**命名格式：** `MethodName_Scenario_ExpectedResult`

```csharp
[Fact]
public void CalculateDamage_NormalHit_ReturnsCorrectDamage()
{
    // Arrange
    var engine = new CombatEngine();
    // Act
    var result = engine.CalculateDamage(character, weapon, target);
    // Assert
    Assert.True(result > 0);
}
```

**测试注意事项：**
- 测试也由 AI 生成，但开发者审核逻辑正确性
- 用具名常量代替 magic number
- 纯逻辑测试不依赖 MonoGame 的 GraphicsDevice
- 已有测试不能出现回归

### 4.4 常见 AI 代码错误

| 错误模式 | 表现 | 修复 |
|----------|------|------|
| 错用 Unity API | MonoBehaviour、GameObject.Find | 替换 自定义ECS/MonoGame 等效 API |
| async void | 返回 void 而非 Task | 改为 async Task |
| 忘记 await | 返回 Task 但不 await | 添加 await |
| 硬编码路径 | "Content/textures/x.png" | 用 ContentManager.Load |
| switch 不穷举 | default 处理所有遗漏 | 穷举所有分支 |
| 直接 new Entity | new Entity() | scene.CreateEntity() |
| 忽略 CancellationToken | 异步方法不带取消令牌 | 加 CancellationToken 参数 |

---

## 5. 数据驱动设计规范

### 5.1 JSON Schema 管理

所有 LLM 输出的 JSON Schema 集中在 `src/TavernAndDestiny/Data/Schemas/` 目录下。

```
Data/Schemas/
├── adventure_blueprint.schema.json     # 编剧Agent输出
├── narrative_text.schema.json          # DM Agent输出
├── dialogue_options.schema.json        # DM Agent对话选项
├── item_description.schema.json        # 文案Agent输出
├── balance_report.schema.json          # 平衡Agent输出
├── character_narrative.schema.json     # 角色叙事层
└── penalty_result.schema.json          # 惩罚/伤疤输出
```

**验证流程：**

```
LLM 输出 JSON
  → JsonSchema.Net 验证（格式校验）
  → 失败：重试最多 3 次
  → 业务逻辑验证（数值范围、关联约束）
  → 写入缓存
  → 返回给调用系统
```

### 5.2 游戏数据配置

配置目录：`src/TavernAndDestiny/Data/Config/`

```json
{
  "race_id": "elf_high",
  "name": "高等精灵",
  "ability_bonuses": { "dexterity": 2, "intelligence": 1 },
  "speed": 30,
  "traits": ["keen_senses", "fey_ancestry", "trance"]
}
```

程序启动时从 JSON 加载到内存，由 `DataManager` 统一管理。

```csharp
public class DataManager : IDataManager
{
    private Dictionary<string, RaceData> _races = new();

    public void LoadAllConfigs()
    {
        _races = LoadJsonConfig<RaceData>("races.json");
    }

    private Dictionary<string, T> LoadJsonConfig<T>(string file) where T : IHasId
    {
        var json = File.ReadAllText($"Data/Config/{file}");
        return JsonSerializer.Deserialize<List<T>>(json)!.ToDictionary(i => i.Id);
    }
}
```

**修改规则：** 改 JSON 文件不需要改代码。新增配置项确保 C# 类已定义 `[JsonPropertyName]` 映射。

### 5.3 存档系统

| 数据类型 | 存储方式 |
|----------|----------|
| 角色数据 | sqlite-net |
| 物品/装备 | sqlite-net |
| 世界状态 | JSON (System.Text.Json) |
| 存档快照 | JSON |
| LLM 缓存 | sqlite-net（键=语义哈希，值=响应） |
| 冒险日志 | sqlite-net |

```csharp
public class CharacterEntity
{
    [PrimaryKey] public string CharacterId { get; set; }
    public string Name { get; set; }
    public string RaceId { get; set; }
    public string StatsJson { get; set; }         // 复杂数据序列化存储
    public string NarrativeJson { get; set; }
    public string EquipmentJson { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SaveManager
{
    private readonly SQLiteAsyncConnection _db;
    public SaveManager(string path) => _db = new SQLiteAsyncConnection(path);

    public async Task SaveCharacter(CharacterEntity c)
    {
        c.UpdatedAt = DateTime.UtcNow;
        await _db.InsertOrReplaceAsync(c);
    }
}
```

---

## 6. Git 工作流与版本管理

### 6.1 分支策略

```
main → 稳定版本，只接受 dev 合并
  └── dev → 开发主线
       ├── feature/{name}
       ├── fix/{name}
       ├── refactor/{name}
       └── docs/{name}
```

生命周期：创建分支 → 开发 + 验证 → PR 到 dev → 审查通过 → squash merge → 删除分支。

### 6.2 提交规范

```
格式: <type>(<scope>): <描述>
示例:
  feat(combat): 添加优势/劣势骰子机制
  fix(initiative): 修复平局先攻排序错误
  refactor(event-bus): 提取 IEventBus 接口
  docs(vibe-coding): 添加开发规约
  test(combat): 添加借机攻击单元测试
  chore(build): 更新 NuGet 包版本
```

| type 类型 | 用途 |
|-----------|------|
| feat | 新功能 |
| fix | 修复 bug |
| refactor | 重构 |
| docs | 文档 |
| test | 测试 |
| chore | 构建/配置 |

作用域对应模块：combat、character、tavern、adventure、settlement、gateway、ui、map、data、save、build。

### 6.3 AI 辅助提交规则

- AI 代码必须通过 `dotnet build` + `dotnet test` 后才能提交。
- 不允许提交编译失败的代码。这是硬性规则。
- 一个提交只做一件事。不混合多个不相关的变更。
- 提交信息由 AI 生成，但开发者审核。

### 6.4 .gitignore 关键规则

```
# MonoGame
bin/; obj/
Content/*.xnb; *.mgcontent
Content/obj/

# IDE
.vs/; .vscode/settings.json; .idea/
*.user; *.suo

# OS
.DS_Store; Thumbs.db; *.swp

# Build
[Dd]ebug/; [Rr]elease/; packages/
```

---

## 7. Sisyphus Agent 使用规约

### 7.1 Agent 分工

| Agent | 职责 | 适用场景 |
|-------|------|----------|
| **explore** | 代码库搜索 | 查找实现、了解架构、搜索引用 |
| **librarian** | 外部资料查找 | API 文档、NuGet 包、社区示例 |
| **oracle** | 架构决策 | 技术选型、设计模式、复杂算法 |
| **writing** | 文档编写 | 技术文档、README |
| **deep** | 复杂实现 | 核心功能编码 |
| **momus** | 代码审查 | 检查规范符合性 |

简单编辑 Sisyphus 直接处理。需要搜索理解的任务委托 explore。架构决策委托 oracle。复杂多文件实现委托 deep。

### 7.2 System Prompt 模板

Sisyphus 委托 deep Agent 时注入以下 System Prompt，确保代码符合项目规范：

```
你是《酒馆与命运》项目的 C# 开发者。

## 项目上下文
- 游戏框架: MonoGame 3.8.5+ (.NET 8+)
- ECS: 自定义轻量 Scene/Entity/Component（非 Nez，见 ADR-0001）
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

## 自定义 ECS 特定规范
- scene.CreateEntity("name") 而非 new Entity()
- entity.AddComponent() 添加组件
- GameRoot.Instance.StartSceneTransition() 切换场景
- Component 构造函数不做繁重操作
- ⚠️ 本项目使用自定义 ECS（见 `src/DndGame/Core/`），不引入 Nez NuGet 包

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

### 7.3 代码审查检查清单

#### 编译检查
- [ ] `dotnet build` 零错误零警告
- [ ] Nullable Reference Types 启用且无空引用警告
- [ ] 没有 `#pragma warning disable`

#### 测试检查
- [ ] `dotnet test` 全部通过
- [ ] 新代码有对应单元测试
- [ ] 已有测试无回归

#### 命名检查
- [ ] 类 PascalCase，文件与类名一致
- [ ] 方法 PascalCase，私有字段 _camelCase
- [ ] 常量 UPPER_SNAKE_CASE，接口 IPascalCase

#### 架构检查
- [ ] 无硬编码数值（应在 JSON 配置中）
- [ ] 接口优于实现
- [ ] 无 dynamic/object/非必要类型转换
- [ ] LLM 输出经过 Schema 验证
- [ ] 数据驱动而非硬编码
- [ ] 一个类一个职责

#### 代码质量
- [ ] 没有死代码（注释掉的、未使用的）
- [ ] 没有魔法数字（用具名常量）
- [ ] async 方法正确使用 CancellationToken
- [ ] switch 穷举所有分支
- [ ] null 检查用 `?.` 和 `??`

#### 安全
- [ ] 外部输入有验证（JSON 反序列化、用户输入）
- [ ] LLM 降级路径正常（缓存/模板）
- [ ] 没有硬编码 API Key
- [ ] 文件路径用 Path.Combine
- [ ] 网络请求有超时和错误处理

### 7.4 常见审查场景

**AI 生成了 Unity API（MonoBehaviour/GameObject/Instantiate）。** 标记为编译错误，替换为 自定义ECS/MonoGame 等效 API，将 Unity API 关键词加入 System Prompt 禁止列表。

**AI 硬编码了数值。** 要求 AI 解释来源，从 DND 规则提取为具名常量或 JSON 配置。

**AI 忘记 await。** 标记编译警告，修改为 async Task，确保调用链正确处理异步。

### 7.5 委托策略

任务分解原则：一个子任务一个输出。有依赖的先执行，独立的并行执行。搜索成本小于实现成本的委托给 explore。

```
任务来了
  ├─ 搜索现有代码？    → explore
  ├─ 查外部资料？      → librarian
  ├─ 架构决策？        → oracle
  ├─ 复杂实现？        → deep
  ├─ 文档？            → writing
  └─ 简单编辑？        → Sisyphus 直接处理
```

验证反馈回路：

```
Agent 完成 → dotnet build → 失败返回 Agent 修复
  → dotnet test → 失败返回 Agent 修复
  → 代码审查 → 不通过返回 Agent 修复
  → 全部通过 → 合并代码
```

---

## 附录 A：外部资料查阅规范

查阅优先级：项目内现有代码和文档 > MonoGame/GoRogue 官方文档 > GitHub 示例项目 > NuGet README > 社区博客。

不查阅 Unity 相关文档、Godot 相关文档、GDScript 代码、过时的 .NET Framework 文档。

AI 查阅外部资料后的 API 使用方式，必须通过 `dotnet build` 验证。

## 附录 B：常见命令速查

```bash
dotnet build src/TavernAndDestiny/TavernAndDestiny.csproj
dotnet build -p:TreatWarningsAsErrors=true    # 严格模式
dotnet test                                   # 全部测试
dotnet test --filter "FullyQualifiedName~CombatEngineTests"  # 特定测试
dotnet publish -c Release                     # 发布
# dotnet add package Nez                        # ⚠️ 不安装——本项目使用自定义 ECS
dotnet mgcb Content/Content.mgcb              # 编译内容管线
```

---

> 本规约随项目演进持续更新。所有变更通过 PR 提交到 dev 分支，经审核后合并到 main。
>
> Vibe Coding 的核心不是"AI 写代码"，而是"人类负责价值判断，AI 负责机械实现"。三道防线（dotnet build → LSP → dotnet test）让这个分工成为可能。
