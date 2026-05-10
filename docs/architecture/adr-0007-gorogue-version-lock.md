# ADR-0007：GoRogue 版本锁定 — 2.6.4 保持至 MVP 发布

## Status
Accepted

## Date
2026-05-10

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | MonoGame 3.8.5+ |
| **Domain** | Core — GoRogue 2.6.4 提供 FOV、A* 寻路、地图生成等 Roguelike 基础设施 |
| **Knowledge Risk** | LOW — GoRogue 2.6.4 API（`Coord`、`ArrayMap<T>`、`FOV`、`AStar`、`Distance`）高度稳定，LLM 训练数据充分覆盖 |
| **References Consulted** | `src/DndGame/DndGame.csproj`（当前 PackageReference）、`src/DndGame/Systems/Combat/GoRogueMapManager.cs`（实际使用代码）、GoRogue 2.6.4 NuGet 文档、GoRogue 3.x SadRogue.Primitives 迁移指南 |
| **Post-Cutoff APIs Used** | None — GoRogue 2.6.4 API 在 LLM 训练截止日期前已稳定 |
| **Verification Required** | `dotnet build` zero errors/warnings；`dotnet test` 91 个现有测试全部通过 |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0000（MonoGame 引擎 — GoRogue 在 .NET Standard 2.0 上运行，与 MonoGame 3.8.5+ 完全兼容） |
| **Enables** | Map 探索系统（FOV、A* 寻路、地图生成）、Combat 战斗空间系统（空间网格、覆盖范围）、Adventure 冒险实例化（地下城生成） |
| **Blocks** | Map Epic 全部 Story、Combat Epic（空间部分）、Adventure Epic（地下城生成部分） |
| **Ordering Note** | 必须在任何 Map 或 Combat 空间系统 Story 开始实现前 Accepted |

## Context

### Problem Statement
项目当前状态存在版本不一致：`DndGame.csproj` 中已锁定 GoRogue 2.6.4（第36行 `PackageReference Include="GoRogue" Version="2.6.4"`），但部分设计文档引用 GoRogue 3.*。GoRogue 3.x 引入了破坏性变更——将坐标系统迁移至 `SadRogue.Primitives` 库（`Coord` 变为 `Point`，命名空间变更）。当前 91 个测试在 2.6.4 上全部通过。需要做出明确决策：锁定当前版本还是升级。

### Constraints
- GoRogueMapManager.cs（109行）已使用 2.6.4 API：`GoRogue.Coord`、`GoRogue.MapViews.ArrayMap<bool>`、`GoRogue.FOV`、`GoRogue.Pathing.AStar`、`GoRogue.Pathing.Distance.MANHATTAN`、`GoRogue.MapViews.IMapView<bool>`
- GoRogue 3.x 需要 `SadRogue.Primitives` NuGet 包作为依赖，API 命名空间全面变更
- GoRogue 2.6.4 目标框架为 .NET Standard 2.0，与项目的 .NET 8 兼容
- MVP 阶段对 Roguelike 空间功能的需求已充分覆盖：FOV 视野计算、A* 寻路、ArrayMap 地形数据管理——GoRogue 2.6.4 全部满足
- 升级到 3.x 需要重写 GoRogueMapManager.cs 并更新所有调用点，且性能/功能无增量收益

### Requirements
- 必须消除设计文档与代码的版本不一致
- 必须确保所有依赖 GoRogue 的系统（Map 探索、Combat 空间网格、Adventure 地下城生成）使用确定的 API 版本
- 必须保留 GoRogue 3.x 的迁移路径，供 MVP 发布后评估

## Decision

**永久锁定 GoRogue 2.6.4 用于 MVP 开发。Post-MVP 评估 3.x 迁移可行性。**

### 当前 API 使用情况

```csharp
// GoRogueMapManager.cs — 当前 2.6.4 API 使用清单
using GoRogue;
using GoRogue.MapViews;
using GoRogue.Pathing;

// 核心类型
Coord                                  // 坐标 (X, Y) — 2.6.4 原生类型
ArrayMap<bool>                        // 二维数组映射 — 通行性/透明性
FOV                                   // 视野计算 — BooleanFOV, CurrentFOV
AStar                                 // A* 寻路 — ShortestPath(Coord, Coord)
Distance.MANHATTAN                    // 曼哈顿距离启发式
IMapView<bool>                        // 只读地图视图接口

// 静态辅助方法
public static Coord ToCoord(int x, int y) => new Coord(x, y);
public static (int X, int Y) FromCoord(Coord c) => (c.X, c.Y);
```

### 2.6.4 vs 3.x 破坏性变更对比

| 概念 | GoRogue 2.6.4 | GoRogue 3.x (SadRogue.Primitives) | 影响 |
|------|---------------|-----------------------------------|------|
| 坐标类型 | `GoRogue.Coord` | `SadRogue.Primitives.Point` | 所有坐标变量需重命名+类型变更 |
| 地图视图 | `GoRogue.MapViews.ArrayMap<T>` | `SadRogue.Primitives.GridViews.ArrayView<T>` | 命名空间+类名变更 |
| FOV | `GoRogue.FOV` | `SadRogue.Primitives.FOV` | 命名空间变更；API 签名可能有差异 |
| A* 寻路 | `GoRogue.Pathing.AStar` | `SadRogue.Primitives.GoRogue.Pathing.AStar` | 嵌套命名空间变更 |
| NuGet 包 | 单包 `GoRogue` | 双包 `SadRogue.Primitives` + `GoRogue` | 依赖管理复杂度增加 |

### 版本锁定策略

```
MVP 开发期间:
  · .csproj 保持 <PackageReference Include="GoRogue" Version="2.6.4" />
  · 所有新 Map/Combat/Adventure 代码使用 2.6.4 API
  · 设计文档统一更新为 2.6.4 引用

Post-MVP 评估:
  · 审查 GoRogue 3.x 新增功能（性能优化、新算法）
  · 评估 SadRogue.Primitives 生态成熟度
  · 如果决策升级 → 创建独立 ADR 定义迁移计划和回滚策略
```

## Alternatives Considered

### Alternative A：升级到 GoRogue 3.x（SadRogue.Primitives）

- **Description**：将 csproj 改为 `GoRogue` 3.x + `SadRogue.Primitives`，重写 GoRogueMapManager.cs 以使用新 API（`Point` 替代 `Coord`，`ArrayView<T>` 替代 `ArrayMap<T>`）
- **Pros**：获得 3.x 新功能（性能优化、更多地图生成算法、更好的 A* 启发式）；与 GoRogue 最新版本对齐
- **Cons**：破坏性 API 变更——所有使用 `Coord`/`ArrayMap`/`FOV`/`AStar` 的代码需重写；引入额外 NuGet 依赖 `SadRogue.Primitives`；当前 91 测试在 2.6.4 通过，升级后可能引入回归 bug；MVP 阶段无功能增量收益——2.6.4 已满足全部空间需求
- **Rejection Reason**：MVP 阶段零功能收益 + 破坏性变更风险 + 重写工作量。GoRogue 2.6.4 的 FOV/A*/ArrayMap 功能完整且经过充分测试，能满足所有 MVP 需求。升级应在 Post-MVP 独立评估

### Alternative B：保持 GoRogue 2.6.4（Accepted）

- **Description**：永久锁定 2.6.4，更新所有设计文档引用，GoRogueMapManager.cs 保持不变
- **Pros**：零风险——所有现有代码和 91 测试继续工作；零工作量——无需重写任何代码；API 稳定性高——2.6.4 是成熟的稳定版本；设计文档与代码对齐
- **Cons**：错过 3.x 的性能优化和新功能；未来可能需要迁移
- **Selection Reason**：MVP 开发期应优先稳定性和速度。2.6.4 完全满足 FOV/A*/地图生成的需求，升级的风险和成本大于收益

## Consequences

### Positive
- 消除设计文档与 csproj 的版本不一致
- GoRogueMapManager.cs 零修改——所有现有代码不变
- 91 个现有测试零回归风险
- 新 Map 探索/Combat 空间代码可直接复用 GoRogueMapManager 的模式
- GoRogue 2.6.4 的 `Coord` → `(X, Y)` 转换辅助方法已封装（`ToCoord`/`FromCoord`），隔离了坐标类型的变更面

### Negative
- 与 GoRogue 最新版本脱节——无法使用 3.x 的性能优化和新算法
- GoRogue 2.6.4 不再接收新功能更新（仅安全修复）
- 未来迁移时，所有 `Coord` 引用需变更为 `Point`——变更面广（但通过静态辅助方法已隔离主要入口）

### Risks

| Risk | Severity | Mitigation |
|------|----------|------------|
| GoRogue 2.6.4 存在未被发现的 bug | Low | 2.6.4 是经过社区多年验证的稳定版本；FOV/A*/ArrayMap 功能简单且边界清晰 |
| Post-MVP 迁移成本过高 | Medium | 本文档记录了完整的破坏性变更清单（Coord→Point 等）；GoRogueMapManager 已通过 `ToCoord`/`FromCoord` 静态方法隔离坐标类型——迁移时仅需修改封装层 |
| 3.x 的新功能在 MVP 实现后变得必要 | Low | MVP 范围已明确定义；3.x 的新功能（如更多地图生成算法）属于 Post-MVP 增强 |
| NuGet 包 2.6.4 被下架 | Low | NuGet 包永久保留；本地 nuget cache 已有缓存 |

### Post-MVP 迁移路径

```
迁移步骤（当决策升级到 3.x 时）:
  1. 添加 NuGet: SadRogue.Primitives + GoRogue (3.x)
  2. 更新 GoRogueMapManager.cs:
     · using GoRogue → using SadRogue.Primitives
     · Coord → Point (构造函数签名相同: Point(x, y))
     · ArrayMap<T> → ArrayView<T> (索引器语义相同)
     · FOV → FOV (API 基本兼容，需验证 BooleanFOV)
     · AStar → SadRogue.Primitives.GoRogue.Pathing.AStar
  3. 更新 ToCoord/FromCoord 静态方法（重命名）
  4. 更新所有调用 GoRogueMapManager 的代码
  5. dotnet build → dotnet test → 验证 91+ 测试
  6. 创建独立 ADR 记录迁移决策
```

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| 05-map-exploration.md | FOV 视野计算（节点图 / 迷雾揭示） | GoRogue 2.6.4 `FOV` + `BooleanFOV` + `CurrentFOV` — GoRogueMapManager.CalculateFOV() 已封装 |
| 05-map-exploration.md | A* 寻路（角色移动路径计算） | GoRogue 2.6.4 `AStar` + `Distance.MANHATTAN` — GoRogueMapManager.FindPath() 已封装 |
| 05-map-exploration.md | 地图数据管理（通行性 / 透明性 / 地形类型） | GoRogue 2.6.4 `ArrayMap<bool>` — GoRogueMapManager 已封装 _walkabilityMap / _transparencyMap |
| 04-combat-system.md | 战斗空间网格（移动范围、AOE 覆盖区域） | 通过 GoRogueMapManager 的 `ArrayMap<bool>` 和 `FOV` 支持网格范围判定 |
| 06-adventure-generation.md | 地下城程序化生成 | GoRogue 2.6.4 的 `MapViews` 和寻路基础设施可支撑生成算法（如 BSP、Drunkard's Walk 等） |

## Performance Implications
- **CPU**：GoRogue 2.6.4 的 `FOV.Calculate()` 和 `AStar.ShortestPath()` 为纯 CPU 计算，<20 实体场景下 <1ms/帧
- **Memory**：`ArrayMap<bool>` 每个 tile 1 byte（bool），100×100 地图 = ~10KB；FOV/AStar 内部状态 <50KB
- **Load Time**：N/A — GoRogue 为纯逻辑库，无资源加载

## Validation Criteria
- `dotnet build` zero errors, zero warnings
- `dotnet test` 91 个现有测试全部通过，零回归
- csproj 中 GoRogue 版本确认为 `2.6.4`（非 `3.*`）
- 所有设计文档中 GoRogue 版本引用更新为 2.6.4
- GoRogueMapManager.cs 编译通过，所有 GoRogue API 调用有效

## Related Decisions
- ADR-0000 — MonoGame 引擎选型（GoRogue 在 .NET 生态中运行）
- ADR-0001 — ECS 架构（GoRogueMapManager 作为 System 被 SceneComponent 引用）
- ADR-0006 — 战斗引擎架构（Combat 依赖 GoRogue 空间网格）
- `design/gdd/05-map-exploration.md` — 地图探索系统（直接使用 GoRogue FOV/A*）
- `design/gdd/04-combat-system.md` — 战斗系统（空间网格范围判定）
- `src/DndGame/Systems/Combat/GoRogueMapManager.cs` — 当前 GoRogue 2.6.4 封装层（109行）
- `src/DndGame/DndGame.csproj` — 当前 PackageReference 为 GoRogue 2.6.4

## Architecture Alignment
- **Current csproj state**: `GoRogue Version="2.6.4"` ✅
- **Current code state**: GoRogueMapManager.cs uses `Coord`, `ArrayMap<bool>`, `FOV`, `AStar` — 2.6.4 API ✅
- **Design docs state**: Some docs reference 3.* — needs update ⚠️
- **Resolution**: This ADR defines 2.6.4 as the canonical version. All design docs will be updated in a follow-up documentation pass.
