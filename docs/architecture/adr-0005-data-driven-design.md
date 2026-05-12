# ADR-0005：数据驱动设计 — JSON 配置 + SQLite 持久化

## Status
Accepted

## Date
2026-05-06

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | MonoGame 3.8.x stable（当前 3.8.4.1） |
| **Domain** | Core（数据架构原则，影响所有子系统的数值管理和持久化） |
| **Knowledge Risk** | LOW — 基于 .NET 标准库 `System.Text.Json` + `sqlite-net` NuGet 包 |
| **References Consulted** | `docs/technical/02-overall-architecture.md` §1.1（⑤数据驱动设计）、§1.4（DataPersistence 服务）；`design/gdd/01-character-system.md`、`design/gdd/03-items-equipment.md`；`design/gdd/GDD-v1.md` |
| **Post-Cutoff APIs Used** | None — `System.Text.Json` (since .NET Core 3.0) 稳定；sqlite-net 社区维护 |
| **Verification Required** | JSON 配置加载成功（类型反序列化无异常）；SQLite 读写通过；`dotnet test` 覆盖 |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0000（MonoGame + .NET 标准库）、ADR-0002（ServiceLocator — DataPersistence 作为第 2 优先级服务） |
| **Enables** | 所有需要数值配置的子系统（角色属性表、装备参数、法术数据、怪物模板、酒馆升级表） |
| **Blocks** | 任何包含硬编码游戏数值的代码 — 所有数值必须在 JSON 或 SQLite 中定义 |
| **Ordering Note** | DataPersistence 在 ServiceLocator 中为第 2 优先级 — EventBus 和 GameStateManager 之后 |

## Context

### Problem Statement
DND 5e 规则系统包含大量可配置数据：种族属性加成、职业进阶表、装备参数、法术伤害、怪物模板、酒馆升级消耗。AI-First 开发范式下，这些数据如果硬编码在 C# 中，AI 修改时容易引入逻辑错误，且无法通过 JSON Schema 独立验证。需要一个清晰的数据分层架构：**只读配置用 JSON，运行时动态数据用 SQLite，禁止任何游戏数值硬编码在 C# 中**。

### Constraints
- AI-First 范式：数据配置（JSON）可由 AI 安全生成和修改（JSON Schema 验证），不触及 C# 编译
- 编译期类型安全：JSON 反序列化为 C# `record` 类型——类型不匹配在加载时（而非运行时）暴露
- 存档必须支持多槽位、跨平台迁移、损坏恢复
- 配置表必须支持热重载（开发阶段）——修改 JSON 无需重启游戏
- 性能约束：配置加载 < 100ms（启动时），存档读/写 < 50ms

### Requirements
- 所有游戏数值（种族属性、职业进阶、装备参数、法术数据、怪物模板）必须存储在 JSON 配置文件中
- 运行时动态数据（角色状态、装备实例、关系值、冒险日志）使用 SQLite 存储
- C# 代码中禁止硬编码任何游戏数值——使用命名常量或从配置加载
- JSON 配置必须能反序列化为 C# `record` 类型
- SQLite 访问通过 `sqlite-net` ORM（轻量，PCL 兼容）
- 存档必须支持完整性校验（hash 或版本标记）

## Decision

**采用双层数据架构：只读配置（JSON + System.Text.Json） + 运行时数据（SQLite + sqlite-net ORM），禁止 C# 代码中硬编码任何游戏数值**。

### 数据分层架构

```
┌─────────────────────────────────────────────────────────────────┐
│                      数据分层架构                                │
│                                                                 │
│  ┌─────────────────────┐    ┌─────────────────────────────────┐ │
│  │  配置层 (JSON)       │    │  运行时数据层 (SQLite)           │ │
│  │  只读，启动时加载     │    │  读写，游戏过程中变更            │ │
│  │                     │    │                                 │ │
│  │  Data/config/       │    │  存档目录 (per-save-slot)        │ │
│  │  ├─ races.json      │    │  ├─ characters.db               │ │
│  │  ├─ classes.json    │    │  ├─ items.db                    │ │
│  │  ├─ spells.json     │    │  ├─ relationships.db            │ │
│  │  ├─ monsters.json   │    │  ├─ adventure_log.db            │ │
│  │  ├─ equipment.json  │    │  ├─ world_state.db              │ │
│  │  ├─ tavern.json     │    │  └─ tavern_state.db             │ │
│  │  └─ formulas.json   │    │                                 │ │
│  └──────────┬──────────┘    └────────────────┬────────────────┘ │
│             │                                │                  │
│             ▼                                ▼                  │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  C# record 类型 (编译期类型安全)                            │  │
│  │  · CharacterStats  · EquipmentData  · SpellData           │  │
│  │  · MonsterTemplate · RaceData       · ClassProgression    │  │
│  └──────────────────────────────────────────────────────────┘  │
│             │                                                   │
│             ▼                                                   │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  IDataPersistence 接口                                     │  │
│  │  · LoadConfig<T>(string path) → T                         │  │
│  │  · Save<T>(T entity) / Load<T>(string id) → T?            │  │
│  │  · Query<T>(string sql) → List<T>                         │  │
│  └──────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

### JSON 配置设计（示例）

```json
// Data/config/races.json
{
  "races": [
    {
      "race_id": "human",
      "name": "人类",
      "ability_bonuses": {
        "str": 1, "dex": 1, "con": 1, "int": 1, "wis": 1, "cha": 1
      },
      "speed": 30,
      "size": "medium",
      "traits": ["versatile", "skilled"],
      "languages": ["common", "choice_1"]
    }
  ]
}
```

```csharp
// 对应的 C# record — 编译期验证类型正确性
public record RaceData
{
    [JsonPropertyName("race_id")]
    public string RaceId { get; init; } = "";

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("ability_bonuses")]
    public Dictionary<Ability, int> AbilityBonuses { get; init; } = new();

    [JsonPropertyName("speed")]
    public int Speed { get; init; }

    [JsonPropertyName("size")]
    public string Size { get; init; } = "";

    [JsonPropertyName("traits")]
    public List<string> Traits { get; init; } = new();

    [JsonPropertyName("languages")]
    public List<string> Languages { get; init; } = new();
}
```

### SQLite 数据模型（示例）

```csharp
// sqlite-net ORM 实体 — 自动映射到数据库表
[Table("characters")]
public class CharacterRecord
{
    [PrimaryKey]
    [Column("character_id")]
    public string CharacterId { get; set; } = "";

    [Column("name")]
    public string Name { get; set; } = "";

    [Column("race_id")]
    public string RaceId { get; set; } = "";

    [Column("class_id")]
    public string ClassId { get; set; } = "";

    [Column("level")]
    public int Level { get; set; }

    [Column("data_json")]  // 复杂嵌套数据序列化为 JSON
    public string DataJson { get; set; } = "";  // CharacterStats 完整序列化

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
```

### 硬编码禁止规则

```csharp
// ❌ 禁止 — 硬编码游戏数值
public class CombatEngine
{
    public int CalculateDamage()
    {
        return 10;  // ❌ 魔法数字！应该从 JSON 配置或公式表读取
    }
}

// ✅ 正确 — 从配置加载
public class CombatEngine
{
    private readonly WeaponConfig _config;

    public int CalculateDamage(string weaponId)
    {
        var weapon = _config.GetWeapon(weaponId);
        return weapon.BaseDamage;  // 从 JSON 配置读取
    }
}

// ✅ 正确 — 使用命名常量（仅用于游戏规则常量，非数值参数）
public static class CombatConstants
{
    public const int CRITICAL_HIT_MULTIPLIER = 2;           // 暴击倍率（规则常量）
    public const int DEATH_SAVE_ROUNDS_WITHOUT_HEAL = 3;    // 无治疗死亡轮数
    public const int MAX_EXHAUSTION_LEVEL = 3;              // 最大疲劳等级
}
```

### 关键接口

```csharp
public interface IDataPersistence
{
    // ── 配置层（JSON）──
    T LoadConfig<T>(string configPath) where T : class;
    Task<T> LoadConfigAsync<T>(string configPath) where T : class;

    // ── 运行时数据层（SQLite）──
    Task SaveAsync<T>(T entity) where T : new();
    Task<T?> LoadAsync<T>(object primaryKey) where T : new();
    Task<List<T>> QueryAsync<T>(string sql, params object[] args) where T : new();
    Task DeleteAsync<T>(object primaryKey) where T : new();

    // ── 存档管理 ──
    Task CreateSaveSlot(string slotId);
    Task LoadSaveSlot(string slotId);
    Task DeleteSaveSlot(string slotId);
    List<string> ListSaveSlots();
    bool ValidateSaveIntegrity(string slotId);
}
```

### 配置热重载（开发阶段）

```csharp
// 开发阶段：监听 JSON 文件变更，自动重新加载配置
public class ConfigWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;

    public ConfigWatcher(string configDirectory)
    {
        _watcher = new FileSystemWatcher(configDirectory, "*.json")
        {
            NotifyFilter = NotifyFilters.LastWrite
        };
        _watcher.Changed += OnConfigChanged;
        _watcher.EnableRaisingEvents = true;
    }

    private void OnConfigChanged(object sender, FileSystemEventArgs e)
    {
        // 重新加载变更的配置文件
        var config = ServiceLocator.Get<IDataPersistence>().LoadConfig<object>(e.Name);
        ServiceLocator.Get<IEventBus>().Publish(new ConfigReloaded(e.Name));
    }
}
```

## Alternatives Considered

### Alternative A：纯 JSON 文件持久化
- **Description**：所有数据（配置 + 运行时）使用 JSON 文件存储，不使用 SQLite
- **Pros**：简单，人类可读，易于调试；零依赖
- **Cons**：无查询能力——查找"所有 HP < 50% 的角色"需要遍历全部文件并反序列化；无事务支持——写入中断可能损坏存档；存档增大后性能下降（全量序列化）
- **Rejection Reason**：JSON 文件适合只读配置，不适合频繁读写的运行时数据。角色状态、关系值、冒险日志需要结构化查询和事务保证

### Alternative B：纯 SQLite 存储
- **Description**：所有数据（配置 + 运行时）使用 SQLite 存储，无 JSON 文件
- **Pros**：统一存储层；强查询能力；事务支持
- **Cons**：配置表编辑不直观（需要 SQL INSERT 或专用工具）；SQL 不易版本控制（diff 困难）；AI 生成 SQL 比生成 JSON 更容易出错
- **Rejection Reason**：配置表（种族、职业、装备参数）需要频繁调整和版本追踪——JSON 更易 diff 和审查。AI 生成 JSON 的可靠性远高于生成 SQL

### Alternative C：ScriptableObject / 资源文件
- **Description**：使用 Unity 的 ScriptableObject 或 Godot 的 Resource 文件
- **Pros**：引擎原生支持，编辑器可视化编辑
- **Cons**：MonoGame 不支持此类资源系统；二进制格式不可 diff；依赖编辑器工具链
- **Rejection Reason**：MonoGame 无可视化编辑器，二进制资源文件与 AI-First 代码驱动范式不兼容

## Consequences

### Positive
- AI 安全修改 — AI 生成 JSON 配置数据，风险远低于生成 C# 逻辑代码。JSON Schema 验证作为独立防线
- 版本控制友好 — JSON 文件天然可 diff，配置变更一目了然
- 编译期类型安全 — JSON → C# `record` 反序列化，类型不匹配在加载时暴露
- 配置热重载 — 开发阶段修改 JSON 无需重启游戏
- 存档完整性 — SQLite 事务 + 完整性校验保证存档不损坏
- 双层分离 — 配置和运行时数据职责清晰，互不干扰
- 跨平台存档迁移 — SQLite 文件 + JSON 快照均为平台无关格式

### Negative
- 启动加载开销 — 所有 JSON 配置文件需在启动时反序列化（可通过懒加载优化）
- 嵌套数据的 SQLite 存储需序列化为 JSON 字符串 — `data_json` 列存储完整对象，丧失部分 SQL 查询能力
- 配置键名一致性 — C# 属性名（PascalCase）与 JSON 键名（snake_case）需手动映射（`[JsonPropertyName]`）
- 双层存储增加维护成本 — 需同时维护 JSON 配置格式和 SQLite 表结构

### Risks
| Risk | Severity | Mitigation |
|------|----------|------------|
| JSON 配置文件格式错误导致启动崩溃 | Medium | JSON Schema 验证 + 默认配置 fallback；CI 中增加配置格式检查 |
| SQLite 数据库文件损坏 | Low | 事务写入 + 完整性校验 + 自动备份（写入前复制旧文件） |
| 配置键名映射遗漏（JsonPropertyName 未覆盖新字段） | Medium | 序列化单元测试覆盖所有配置类型；CI 中检查 `JsonPropertyName` 覆盖率 |
| AI 生成 JSON 数据违反 Schema | Low | JsonSchema.Net 自动验证；Schema 定义明确约束（required 字段、类型、范围） |

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| GDD-v1.md §5.3-5.4 | DND 5e 规则数据化（种族、职业、法术、怪物） | JSON 配置文件承载所有 5e SRD 数据 |
| 02-overall-architecture.md §1.1 | ⑤数据驱动设计："规则尽量配置在 JSON 中，避免硬编码" | 双层架构：JSON 配置 + SQLite 运行时 |
| 02-overall-architecture.md §1.4 | DataPersistence 为第 2 优先级服务 | ServiceLocator 注册顺序 |
| 01-character-system.md §2.1-2.3 | 角色数据模型（数值层 + 叙事层） | 数值层通过 JSON 配置加载，叙事层存储于 SQLite |
| 03-items-equipment.md §3 | 装备参数、武器伤害、护甲值 | JSON 配置表 (equipment.json) |
| 04-combat-system.md §2.5 | DiceRoller 纯函数（不依赖外部状态） | 伤害骰表达式（"2d6+3"）从 JSON 配置读取 |

## Performance Implications
- **CPU**：JSON 反序列化 < 50ms（全量配置加载）；SQLite 读 < 5ms（单条索引查询）
- **Memory**：全量配置数据驻留内存 < 5MB（record 类型轻量）；SQLite 缓存 < 2MB
- **Load Time**：启动时全量配置加载 < 100ms；存档加载 < 50ms（sqlite-net 异步查询）
- **Network**：N/A（本地存储，离线优先）

## Migration Plan
N/A — Phase 0 已完成 Core 基础设施。DataPersistence 服务尚未实现——此 ADR 定义了其接口和架构约束。

实现步骤：
1. 创建 `src/DndGame/Data/DataPersistence.cs`（实现 IDataPersistence 接口）
2. 创建 `Data/config/` 目录及初始配置 JSON（races、classes、spells、monsters、equipment）
3. 创建 `Data/Schemas/` 目录及 JSON Schema 文件（7 个）
4. 注册 DataPersistence 到 ServiceLocator（第 2 优先级）
5. 编写配置加载的单元测试（类型反序列化、Schema 验证）

## Validation Criteria
- `dotnet build` zero errors
- `dotnet test` — DataPersistence 单元测试覆盖：JSON 加载、SQLite CRUD、存档槽管理
- 所有 `Data/config/*.json` 文件通过 JSON Schema 验证
- C# 代码中搜索硬编码数值（regex: `=\s*\d+` 在非 const 上下文中）— 零非 const 魔法数字
- 存档完整性校验通过（损坏文件检测）

## Related Decisions
- ADR-0000 — MonoGame + .NET（提供 System.Text.Json + sqlite-net NuGet）
- ADR-0001 — ECS（实体的数值组件从配置加载）
- ADR-0002 — ServiceLocator（DataPersistence 第 2 优先级服务）
- ADR-0004 — LLM 皮肤层（Schema 文件、模板文件存储在 Data/ 目录）
- `docs/technical/02-overall-architecture.md` §1.4 — DataPersistence 服务定义
- `design/gdd/01-character-system.md` — 角色数据模型设计
- `design/gdd/03-items-equipment.md` — 物品/装备数据模型设计
