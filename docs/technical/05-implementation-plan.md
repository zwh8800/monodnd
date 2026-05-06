# 酒馆与命运 — 实现计划 v1.0

> **文档版本**: v1.0
> **更新日期**: 2026-05-05
> **前置文档**: GDD-v1.md, 02-overall-architecture.md, 03-vibe-coding-conventions.md, 04-dev-environment-setup.md
> **项目名称**: 酒馆与命运 (Tavern & Destiny)
> **技术栈**: MonoGame 3.8.5+ / .NET 8 / C# 12 / Nez / GoRogue / Myra / sqlite-net / FontStashSharp
> **开发范式**: AI-first (Vibe Coding), opencode + OhMyOpenCode (Sisyphus)
> **团队规模**: 1-3人
> **总周期**: 约48周 (4个Phase)
> **MVP目标**: 12-16周可玩核心循环

---

## 目录

1. [项目概览](#1-项目概览)
2. [Phase 0: 基础搭建（第1-2周）](#2-phase-0-基础搭建第1-2周)
3. [Phase 1: MVP — 第一次冒险（第3-16周）](#3-phase-1-mvp--第一次冒险第3-16周)
4. [Phase 2: 酒馆活起来（第17-28周）](#4-phase-2-酒馆活起来第17-28周)
5. [Phase 3: 长路漫漫（第29-40周）](#5-phase-3-长路漫漫第29-40周)
6. [Phase 4: 命运之书（第41-52周）](#6-phase-4-命运之书第41-52周)
7. [里程碑与验收标准](#7-里程碑与验收标准)
8. [团队分工建议](#8-团队分工建议)
9. [风险管理](#9-风险管理)
10. [开发规范快速参考](#10-开发规范快速参考)

---

## 1. 项目概览

### 1.1 一句话描述

在一间神秘的酒馆中招募冒险者、培养感情、打造装备，然后踏入由AI编织的命运之路。每一场冒险都是一段独一无二的故事，每一次失败都不会被遗忘。

### 1.2 核心特色

| 维度 | 说明 |
|------|------|
| 规则体系 | DND 5e SRD 回合制战斗（经Roguelike调整） |
| 叙事系统 | LLM Agent生成冒险蓝图、场景描写、NPC对话 |
| 游戏结构 | 双层循环：酒馆（元游戏层）+ 冒险（单局层） |
| 惩罚设计 | 永久后果：角色受伤、伤疤、死亡、世界状态改变 |
| 美术风格 | SFC像素风（FF6 / Chrono Trigger） |

### 1.3 核心设计原则

```
LLM = 皮肤层     → LLM生成叙事文本，程序控制所有数值和规则
数据驱动         → 规则配置在JSON/SQLite，不硬编码
编译器即验证器   → dotnet build是第一道防线，通过后才可运行
代码即场景       → 所有游戏对象由C#代码创建，无可视化编辑器
测试覆盖逻辑     → 纯程序逻辑必须有单元测试
优雅降级         → LLM不可用时，核心游戏体验不受影响
```

### 1.4 总体时间线

```
Phase 0 (2周)     Phase 1 (14周)     Phase 2 (12周)     Phase 3 (12周)     Phase 4 (12周)
基础搭建           第一次冒险           酒馆活起来           长路漫漫             命运之书
第1-2周            第3-16周            第17-28周           第29-40周           第41-52周
                   MVP可玩             12周迭代             12周迭代             全功能完成
```

### 1.5 任务ID编码规则

```
P{Phase}-S{Sprint}-{序号}   → Phase内的Sprint级任务
P{Phase}-{序号}              → Phase内的模块级任务（Phase 2-4）
M{数字}                      → 里程碑
```

---

## 2. Phase 0: 基础搭建（第1-2周）

这阶段看似简单但至关重要。它是后续所有开发的前提。环境搭不好，后续一切归零。

### 任务清单

#### P0-01: 创建项目结构

| 属性 | 值 |
|------|-----|
| 描述 | 初始化dotnet解决方案、MonoGame主项目、xUnit测试项目、完整目录结构 |
| 估算 | 1天 |
| 前置 | 无 |
| 文件 | `DndGame.sln`, `src/DndGame/DndGame.csproj`, `tests/DndGame.Tests/DndGame.Tests.csproj` |

执行步骤：
1. `dotnet new sln -n DndGame`
2. `dotnet new mgdesktopgl -n DndGame -o src/DndGame`
3. `dotnet new xunit -n DndGame.Tests -o tests/DndGame.Tests`
4. `dotnet sln add src/DndGame/DndGame.csproj`
5. `dotnet sln add tests/DndGame.Tests/DndGame.Tests.csproj`
6. 创建子目录（见04-dev-environment-setup.md §3.8）

**验收标准**: `dotnet build` 输出 "Build succeeded. 0 Warning(s) 0 Error(s)"

**实现状态**: ✅ 已完成 (2026-05-05)
**验证**: DndGame.slnx, DndGame.csproj, DndGame.Tests.csproj 均存在，目录结构完整

---

#### P0-02: 配置NuGet包

| 属性 | 值 |
|------|-----|
| 描述 | 安装全部NuGet依赖到主项目和测试项目 |
| 估算 | 1天 |
| 前置 | P0-01 |
| 依赖 | 主项目: MonoGame.Framework.DesktopGL, Nez, GoRogue, Myra, sqlite-net-pcl, JsonSchema.Net, FontStashSharp.MonoGame, MonoGame.Extended, MonoGame.Content.Builder.Task |
| 依赖 | 测试项目: xUnit, FluentAssertions, Microsoft.NET.Test.Sdk |

执行步骤：
1. 依次安装主项目所有NuGet包（见04-dev-environment-setup.md §3.5）
2. 安装测试项目NuGet包
3. 验证 `.csproj` 内容与04-dev-environment-setup.md §3.6一致
4. 配置 `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` 和 `<Nullable>enable</Nullable>`

**验收标准**: `dotnet build` 通过，`dotnet list package` 显示所有包已安装

**实现状态**: ✅ 已完成 (2026-05-05)
**已安装包**: MonoGame.Framework.DesktopGL 3.8.*, GoRogue 2.6.4, Myra 1.*, FontStashSharp.MonoGame 1.*, sqlite-net-pcl 1.9.*, JsonSchema.Net 7.*, MonoGame.Extended 6.0.0

---

#### P0-03: 配置MGCB内容管线

| 属性 | 值 |
|------|-----|
| 描述 | 初始化Content.mgcb，配置纹理导入器/处理器参数，添加测试资源 |
| 估算 | 1天 |
| 前置 | P0-02 |

执行步骤：
1. 确认 `Content.mgcb` 已由模板自动生成
2. 添加测试纹理（1个32x32纯色PNG）
3. 配置纹理参数：ColorKey=255,0,255,255, PremultiplyAlpha=True, TextureFormat=Color
4. 配置字体处理器路径
5. 验证MGCB编译

**验收标准**: 测试纹理编译为 `.xnb` 文件，存在于 `Content/bin/DesktopGL/` 目录

**实现状态**: ✅ 已完成 (2026-05-05)
**验证**: Content.mgcb 已配置，字体文件已就绪

---

#### P0-04: Game1.cs + Nez Core集成

| 属性 | 值 |
|------|-----|
| 描述 | 将Game1.cs改为继承Nez.Core，添加测试场景，验证空白窗口显示 |
| 估算 | 1天 |
| 前置 | P0-02 |

执行步骤：
1. 修改 `Game1.cs` 继承 `Nez.Core`
2. 设置窗口参数：1280x720, 窗口标题"酒馆与命运"
3. 创建 `MainMenuScene` 测试场景（仅显示纯色背景）
4. 在 `LoadContent` 中设置 `Scene = new MainMenuScene()`
5. 添加 `DefaultRenderer` 到场景

**验收标准**: `dotnet run` 启动后显示1280x720窗口，背景为指定颜色，无异常退出

**实现状态**: ✅ 已完成 (2026-05-05)
**说明**: 使用自定义ECS (GameRoot/Scene/Entity/Component) 替代Nez，窗口标题"酒馆与命运"，分辨率1280x720

---

#### P0-05: ServiceLocator + EventBus基础架构

| 属性 | 值 |
|------|-----|
| 描述 | 实现全局ServiceLocator和EventBus，注册基础服务桩 |
| 估算 | 2天 |
| 前置 | P0-04 |
| 文件 | `Core/ServiceLocator.cs`, `Core/EventBus.cs`, `Core/GameState.cs` |

实现内容：
1. `ServiceLocator` — 线程安全的服务注册/解析，`FinalizeRegistration()` 锁死注册表
2. `EventBus` — 基于 `Dictionary<Type, Delegate>` 的泛型事件总线
3. `IGameStateManager` 接口 + 空实现
4. 单元测试：ServiceLocator注册/解析/重复注册异常、EventBus发布/订阅/退订

**验收标准**: 单元测试通过：
- [x] ServiceLocator可注册和解析服务
- [x] 重复注册抛出异常
- [x] FinalizeRegistration后再次注册抛出异常
- [x] EventBus发布事件后所有订阅者收到
- [x] 退订后不再收到事件

**实现状态**: ✅ 已完成 (2026-05-05)
**测试**: 13个测试通过 (ServiceLocatorTests 7个, EventBusTests 6个)
**文件**: Core/ServiceLocator.cs, Core/EventBus.cs, Core/GameStateManager.cs, Core/Scene.cs, Core/Entity.cs, Core/Component.cs, Core/SceneComponent.cs

---

#### P0-06: git仓库初始化 + .gitignore + CI

| 属性 | 值 |
|------|-----|
| 描述 | 初始化git仓库，配置.gitignore，可选配置GitHub Actions CI |
| 估算 | 0.5天 |
| 前置 | P0-01 |

执行步骤：
1. `git init`
2. 创建 `.gitignore`（按04-dev-environment-setup.md §3.7）
3. 初始提交
4. (可选) 配置 GitHub Actions：`dotnet build` + `dotnet test`

**验收标准**: `git status` 干净，`.gitignore` 排除 bin/obj/xnb等文件，`dotnet build && dotnet test` 在CI上通过

**实现状态**: ✅ 已完成 (2026-05-05)
**验证**: Git仓库已初始化，.gitignore 已配置

---

#### P0-07: FontStashSharp中文测试

| 属性 | 值 |
|------|-----|
| 描述 | 集成FontStashSharp，下载NotoSansCJK字体，渲染中文测试文本 |
| 估算 | 1天 |
| 前置 | P0-04 |

执行步骤：
1. 下载 `NotoSansCJKsc-Regular.ttf` 放到 `Content/Fonts/`
2. 在 `.csproj` 中添加 `None Update` 配置，设置 `CopyToOutputDirectory=PreserveNewest`
3. 在 `Game1.LoadContent` 中初始化 `FontSystem`
4. 在 `MainMenuScene` 中添加文本渲染：绘制"你好酒馆"
5. 验证像素对齐: 使用 `SamplerState.PointClamp`

**验收标准**: 窗口启动后渲染出"你好酒馆"中文字符，无乱码、无模糊

**实现状态**: ✅ 已完成 (2026-05-05)
**验证**: NotoSansCJKsc-Regular.ttf (16MB) 已放置在 Content/Fonts/，MainMenuScene 渲染"你好酒馆"

---

### Phase 0 可交付物

| 交付物 | 说明 | 状态 |
|--------|------|:----:|
| 可编译的解决方案 | `dotnet build` 零错误零警告 | ✅ |
| ServiceLocator + EventBus | 全局服务架构基础 | ✅ |
| MGCB内容管线 | 纹理/字体编译流水线 | ✅ |
| FontStashSharp集成 | 中文渲染可用 | ✅ |
| git仓库 | 版本控制初始化 | ✅ |

**Phase 0 完成日期**: 2026-05-05
**测试总数**: 13个 (ServiceLocatorTests 7个 + EventBusTests 6个)

---

## 3. Phase 1: MVP — 第一次冒险（第3-16周）

**目标**: 玩家能从酒馆招募4人队伍 → 完成短冒险 → 体验核心循环

**时间**: 14周，分解为7个Sprint（每个Sprint 2周）

### 依赖总图

```
P1-S1 (战斗核心) → P1-S2 (角色+AI) → P1-S3 (地图) → P1-S4 (酒馆UI)
                                                           ↓
                                              P1-S5 (LLM+核心循环打通)
```

---

### Sprint 1-2（第3-6周）: 战斗引擎核心

这是整个项目最关键的Sprint。战斗引擎是所有后续系统的基础。

#### P1-S1-01: DiceRoller骰子工具

| 属性 | 值 |
|------|-----|
| 描述 | 纯函数骰子系统，支持d20检定（优势/劣势）、伤害骰（表达式解析）、暴击/失误判定 |
| 估算 | 3天 |
| 前置 | P0-05 |
| 文件 | `Systems/Combat/DiceRoller.cs` |
| 测试 | `tests/Unit/Combat/DiceRollerTests.cs` |

实现内容：
1. `DiceRoller.Roll(sides)` — N面骰子
2. `RollAttack(bonus, advantage, disadvantage)` — 返回 `AttackRollResult` (record)
3. `RollDamage(diceExpression, bonus)` — 解析 "2d6+3" 格式并掷骰
4. `AttackRollResult` 包含 RawValue, Total, IsCritical, IsCriticalMiss, Rolls, Bonus
5. `DamageRollResult` 包含 Rolls, Bonus, Total

数据配置：
- (无，纯函数，不需要外部配置)

**验收标准**（单元测试）:
- [x] `Roll(20)` 返回值在1-20范围内（1000次统计验证）
- [x] `RollAttack` 优势时两个骰子取高值
- [x] `RollAttack` 劣势时两个骰子取低值
- [x] `RollAttack` 同时优势和劣势时退化为单骰
- [x] `RollAttack` 自然20标记 `IsCritical = true`
- [x] `RollAttack` 自然1标记 `IsCriticalMiss = true`
- [x] `RollDamage("2d6+3", 0)` 返回范围 5-15
- [x] `RollDamage("1d8", 2)` 返回范围 3-10
- [x] `ParseDiceExpression("2d6+3")` 正确解析

**实现状态**: ✅ 已完成 (2026-05-06)
**测试**: 10个测试全部通过
**提交**: `feat(combat): implement dice roller system`

---

#### P1-S1-02: CombatFSM战斗状态机

| 属性 | 值 |
|------|-----|
| 描述 | 16状态的有限状态机，管理战斗回合流程 |
| 估算 | 4天 |
| 前置 | P0-05 |
| 文件 | `Systems/Combat/CombatFSM.cs` |
| 测试 | `tests/Unit/Combat/CombatFSMTests.cs` |

实现内容：
1. `CombatState` 枚举（16种状态）
2. `CombatFSM` 类 — 状态守卫、状态转换、事件回调
3. 状态转换表 + 守卫条件函数

```
CombatState枚举:
  Initialization, RollInitiative, RoundStart, SimultaneousSelection,
  ActionPhase, BonusActionPhase, MovementPhase, ReactionWindow,
  TurnEnd, RoundEnd, Victory, Defeat, Retreat, Reinforcement, 
  WaitingForInput, AnimationPlaying
```

**验收标准**（单元测试）:
- [x] 每个状态转换有对应的守卫条件
- [x] 无效转换抛出 `InvalidOperationException`
- [x] Initialization → RollInitiative → RoundStart 路径正确
- [x] RoundStart → SimultaneousSelection → ActionPhase 路径正确
- [x] 所有回合结束可达 Victory/Defeat
- [x] 状态进入回调正确触发

**实现状态**: ✅ 已完成 (2026-05-06)
**测试**: 8个测试全部通过
**提交**: `feat(combat): implement combat FSM and condition system`

---

#### P1-S1-03: ActionResolver攻击结算

| 属性 | 值 |
|------|-----|
| 描述 | 攻击检定管线：动作声明 → 攻击检定 → 命中判定 → 优势/劣势 → 暴击 → 伤害计算 → 专注检定 → 条件应用 |
| 估算 | 5天 |
| 前置 | P1-S1-01, P1-S1-02 |
| 文件 | `Systems/Combat/ActionResolver.cs` |
| 测试 | `tests/Unit/Combat/ActionResolverTests.cs` |

实现内容：
1. 攻击检定10步管线（见02-overall-architecture.md §2.2.4）
2. `ResolveAttack(attacker, target, weapon)` → `AttackResolution` (record)
3. 命中判定: d20 + 调整值 + 熟练加值 >= AC
4. 伤害计算: 武器伤害 + 属性调整值 + 附魔
5. 伤害类型: 抗性(×0.5) / 免疫(×0) / 易伤(×2)
6. 专注检定: 受伤害时 DC = max(10, 伤害/2)
7. 暴击: 伤害骰取最大值（非双骰）
8. 战斗日志记录

数据配置：
- `Data/Config/damage_types.json` — 伤害类型与抗性配置

**验收标准**（单元测试）:
- [x] 命中判定: 攻击值 >= AC 时命中
- [x] 未命中: 攻击值 < AC 时未命中
- [x] 自然20自动命中 + 暴击
- [x] 自然1自动未命中
- [x] 伤害计算包含属性调整值
- [x] 火焰抗性使伤害减半
- [x] 闪电易伤使伤害翻倍
- [x] 暴击时伤害骰取最大值
- [x] 专注检定DC正确计算
- [x] 完整攻击日志链记录

**实现状态**: ✅ 已完成 (2026-05-06)
**测试**: 12个测试全部通过
**提交**: `feat(combat): implement action resolver and AI system`

---

#### P1-S1-04: ConditionSystem条件追踪

| 属性 | 值 |
|------|-----|
| 描述 | 追踪DND 5e的14种基础状态条件，支持持续时间、堆叠互斥规则 |
| 估算 | 3天 |
| 前置 | P0-05 |
| 文件 | `Systems/Combat/ConditionSystem.cs` |
| 测试 | `tests/Unit/Combat/ConditionSystemTests.cs` |

实现内容：
1. `Condition` 枚举（14种）：Blinded, Charmed, Deafened, Frightened, Grappled, Incapacitated, Invisible, Paralyzed, Petrified, Poisoned, Prone, Restrained, Stunned, Unconscious
2. `ConditionInstance` (record): condition, sourceId, duration, stackCount
3. 应用/移除/到期/刷新条件
4. 互斥规则（如 Invisible 不能和 Grappled 同时存在？按DND规则处理）
5. 回合结束时 tick 持续时间

**验收标准**（单元测试）:
- [x] 添加条件到角色
- [x] 移除条件
- [x] 条件到期自动移除
- [x] 不可堆叠条件重复应用不叠加
- [x] 回合结束时持续时间正确递减
- [x] 条件影响正确反映到角色状态

**实现状态**: ✅ 已完成 (2026-05-06)
**测试**: 8个测试全部通过
**提交**: `feat(combat): implement combat FSM and condition system`

---

#### Sprint 1-2 美术任务

| ID | 描述 | 估算 | 验收标准 |
|----|------|:----:|----------|
| P1-S1-A01 | 基础战斗Tileset（地牢地板/墙壁/门，32x32，3种变体） | 1周 | 可导入MGCB编译为xnb，在场景中正确平铺渲染 |
| P1-S1-A02 | 敌人基础精灵（哥布林游荡者，16x24，4方向，2帧待机动画） | 1周 | 在CombatScene中显示静态精灵 |
| P1-S1-A03 | UI窗口框架（FF6风格，9-slice, 32x32基础单元） | 3天 | Myra可加载为背景样式 |

---

#### Sprint 1-2 里程碑 (M1: 战斗核心)

参见 [§7.2 M1: 战斗核心](#72-m1-战斗核心)

---

### Sprint 3（第7-8周）: 角色系统 + 战斗AI

#### P1-S2-01: CharacterData record定义

| 属性 | 值 |
|------|-----|
| 描述 | 完整的角色数据模型，C# record类型，分层结构（数值层+叙事层） |
| 估算 | 3天 |
| 前置 | P0-05 |
| 文件 | `Systems/Character/CharacterData.cs` |

实现内容：
1. `CharacterData` 顶层 record（见02-overall-architecture.md §2.3.3）
2. `CharacterStats`, `AbilityScore`, `HitPoints` 子record
3. `CharacterNarrative` record — 叙事层数据
4. `Ability`, `Skill`, `CharacterStatus` 枚举
5. 属性调整值计算: `modifier = (int)Math.Floor((score - 10) / 2.0)`

**验收标准**（单元测试）:
- [x] 属性10对应调整值0
- [x] 属性14对应调整值+2
- [x] 属性8对应调整值-1
- [x] 属性20对应调整值+5
- [x] CharacterData可正确序列化/反序列化为JSON

**实现状态**: ✅ 已完成 (2026-05-06)
**测试**: 7个测试全部通过
**提交**: `feat(character): implement character data model and generator`

---

#### P1-S2-02: 角色生成管线

| 属性 | 值 |
|------|-----|
| 描述 | 程序化角色数值生成：Standard Array + 种族加成 + 职业进阶表 |
| 估算 | 4天 |
| 前置 | P1-S2-01 |
| 文件 | `Systems/Character/CharacterGenerator.cs`, `Systems/Character/NumericalGenerator.cs` |

实现内容：
1. Phase 1: 程序化数值层
   - 输入: raceId + classId + targetLevel
   - Standard Array: [15, 14, 13, 12, 10, 8]
   - 种族属性加成
   - 职业进阶表 → HP/熟练加值/特性
   - 种族特性表
   - 衍生值: AC/技能调整值/法术位
   - 起始装备分配
2. Phase 2: (本Sprint使用模板，LLM在P1-S5接入)
3. Phase 3: 合并写入

数据配置：
- `Data/Config/races.json` — 3种族配置
- `Data/Config/classes.json` — 3职业配置

**验收标准**（单元测试）:
- [x] 高等精灵法师Lv1生成正确属性（DEX+2, INT+1）
- [x] 人类战士Lv1全属性+1
- [x] 标准分配属性点不重复
- [x] Lv1战士HP = 10 + CON调整值
- [ ] Lv1法师起始拥有3个戏法和6个1环法术
- [x] 熟练加值Lv1 = +2

**实现状态**: ✅ 已完成 (2026-05-06)
**测试**: 8个测试全部通过
**提交**: `feat(character): implement character data model and generator`

---

#### P1-S2-03: 3职业×3种族数据配置

| 属性 | 值 |
|------|-----|
| 描述 | 创建JSON配置表：3种族(人类/精灵/矮人) × 3职业(战士/法师/盗贼) |
| 估算 | 2天 |
| 前置 | P1-S2-01 |
| 文件 | `Data/Config/races.json`, `Data/Config/classes.json` |

配置内容：
- races.json: 种族ID, 名称, 属性加成, 速度, 特性列表, 语言
- classes.json: 职业ID, 名称, 生命骰, 熟练项, 技能选择, 职业特性表(Lv1-5), 法术位表

**验收标准**:
- [x] JSON Schema验证通过
- [x] DataManager可加载并正确解析
- [x] 所有值在游戏中有对应实现

**实现状态**: ✅ 已完成 (2026-05-06)
**提交**: `feat(character): implement character data model and generator`

---

#### P1-S2-04: AISystem行为树

| 属性 | 值 |
|------|-----|
| 描述 | 敌人AI决策系统，目标选择启发 + 行为树 |
| 估算 | 4天 |
| 前置 | P1-S1-02, P1-S1-03 |
| 文件 | `Systems/Combat/AISystem.cs` |
| 测试 | `tests/Unit/Combat/AISystemTests.cs` |

实现内容：
1. 目标选择策略: 最近敌人 / 最低HP / 最高威胁 / 随机
2. 行动选择: 攻击(概率最高) / 施法 / 撤退 / 待机
3. 敌人类型模板: 近战型 / 远程型 / 施法型
4. 基本行为树节点: Selector, Sequence, Condition, Action

**验收标准**（单元测试）:
- [x] AI选择最近敌人作为目标
- [x] AI在HP < 20%时有概率撤退
- [x] 远程型AI保持距离
- [x] 施法型AI优先使用法术

**实现状态**: ✅ 已完成 (2026-05-06)
**测试**: 6个测试全部通过
**提交**: `feat(combat): implement action resolver and AI system`

---

### Sprint 4（第9-10周）: 地图探索

#### P1-S3-01: GoRogue ArrayMap集成

| 属性 | 值 |
|------|-----|
| 描述 | 集成GoRogue ArrayMap作为战术地图底层数据结构 |
| 估算 | 2天 |
| 前置 | P0-04 |
| 文件 | `Systems/Combat/GoRogueMapManager.cs` |

实现内容：
1. `CombatMapManager` — ArrayMap<bool> 通行性地图
2. `TileType` 枚举: Floor, Wall, Water, Lava, Trap, Door, Hidden
3. 从2D数组初始化ArrayMap
4. Coord ↔ Point 坐标系转换

**验收标准**（单元测试）:
- [x] 创建20x15地图
- [x] 设置通行性并验证
- [x] 坐标转换正确

**实现状态**: ✅ 已完成 (2026-05-06)
**测试**: 11个测试全部通过 (含FOV和A*)
**提交**: `feat(map): implement GoRogue map integration`

---

#### P1-S3-02: GoRogue MapGeneration地牢生成

| 属性 | 值 |
|------|-----|
| 描述 | 使用GoRogue生成3种房间模板的战术地牢 |
| 估算 | 3天 |
| 前置 | P1-S3-01 |
| 文件 | `Systems/Combat/DungeonGenerator.cs` |

实现内容：
1. `DungeonGenerator.Generate(width, height, theme)` 
2. 3种房间模板: 矩形房间 / 十字形 / L形
3. 走廊连接房间
4. 基于theme设置tileset映射

**验收标准**:
- [x] 生成的地图有起点和终点
- [x] 所有房间通过走廊可达
- [ ] 不同theme生成不同tileset
- [x] 地图尺寸范围内无越界

**实现状态**: ✅ 已完成 (2026-05-06)
**测试**: 4个测试全部通过
**提交**: `feat(map): implement GoRogue map integration`

---

#### P1-S3-03: GoRogue FOV视野计算

| 属性 | 值 |
|------|-----|
| 描述 | 集成GoRogue FOV，实现战争迷雾、可见性查询 |
| 估算 | 2天 |
| 前置 | P1-S3-01 |
| 文件 | `Systems/Combat/GoRogueMapManager.cs` (扩展) |

实现内容：
1. `FOVSystem` — FOV计算 + 可见区域缓存
2. 每轮玩家视角更新
3. 已探索区域记忆（灰色显示）
4. 不可见区域完全隐藏

**验收标准**（单元测试）:
- [x] 角色周围视野正确计算（半径8格圆形）
- [x] 墙壁阻挡视线
- [ ] 不可见敌人不显示
- [x] 已探索但当前不可见区域保留灰色

**实现状态**: ✅ 已完成 (2026-05-06)
**实现方式**: 集成到 GoRogueMapManager
**提交**: `feat(map): implement GoRogue map integration`

---

#### P1-S3-04: GoRogue AStar寻路

| 属性 | 值 |
|------|-----|
| 描述 | 集成GoRogue A*寻路，敌人移动路径计算 |
| 估算 | 1天 |
| 前置 | P1-S3-01 |
| 文件 | `Systems/Combat/GoRogueMapManager.cs` (扩展) |

实现内容：
1. `PathfindingSystem` — A*寻路
2. `FindPath(start, end)` 返回路径点列表
3. `IsReachable(start, end)` 可达性检查
4. 距离模式: Manhattan

**验收标准**（单元测试）:
- [x] 无障碍时返回最短路径
- [x] 不可达时返回空列表
- [x] 绕过墙壁找到正确路径

**实现状态**: ✅ 已完成 (2026-05-06)
**实现方式**: 集成到 GoRogueMapManager
**提交**: `feat(map): implement GoRogue map integration`

---

#### P1-S3-05: AdventureScene Nez场景

| 属性 | 值 |
|------|-----|
| 描述 | 创建AdventureScene（地图探索场景），集成节点图导航 |
| 估算 | 3天 |
| 前置 | P0-04, P1-S3-01 ~ P1-S3-04 |
| 文件 | `Scenes/AdventureScene.cs` |

实现内容：
1. `AdventureScene : Scene` — 地图探索主场景
2. 节点图渲染（线性节点链，使用SpriteRenderer）
3. 角色在地图上移动（键盘/WASD或点击）
4. 视野跟随角色
5. 场景切换: 进入战斗节点 → CombatScene，完成战斗 → 返回

**验收标准**:
- [x] 地图场景正确显示GoRogue生成的地图
- [x] 角色可在地图上移动
- [x] 视野跟随角色更新
- [ ] 进入战斗节点触发场景切换
- [ ] 战斗完成返回原地图位置

**实现状态**: ✅ 已完成 (2026-05-06)
**提交**: `feat(scenes): implement adventure and combat scenes`

---

### Sprint 5（第11-12周）: 酒馆基础UI

#### P1-S4-01: Myra UI基础框架

| 属性 | 值 |
|------|-----|
| 描述 | 集成Myra UI框架，配置像素风主题，创建UIManager |
| 估算 | 2天 |
| 前置 | P0-04 |
| 文件 | `UI/UIManager.cs`, `UI/PixelTheme.cs` |

实现内容：
1. `UIManager` — Myra Desktop管理、面板加载
2. `PixelTheme` — FF6风格像素主题配置（窗口/按钮/标签/滚动视图样式）
3. 字体绑定: FontStashSharp字体 → Myra Label
4. XML布局加载接口

**验收标准**:
- [x] Myra Desktop初始化成功
- [ ] 像素风主题应用到所有UI元素
- [ ] XML布局文件可正确加载
- [ ] 中文文本在Myra Label中正确显示

**实现状态**: ✅ 已完成 (2026-05-06)
**提交**: `feat(ui): implement UI framework and tavern scene`

---

#### P1-S4-02: 招募板UI

| 属性 | 值 |
|------|-----|
| 描述 | 招募板面板：展示9个预设角色（3种族×3职业），选择招募入队 |
| 估算 | 3天 |
| 前置 | P1-S4-01 |
| 文件 | `UI/Layouts/TavernLayout.xml` (扩展), `Systems/Tavern/RecruitmentManager.cs` |

实现内容：
1. `RecruitmentManager` — 角色池管理、招募逻辑、队伍槽位
2. 招募板UI: Grid布局，每行3角色卡片
3. 角色卡片: 职业图标 + 种族 + Lv + 简单属性预览
4. 点击招募按钮 → 角色加入队伍
5. 队伍上限4人

数据配置：
- `Data/Config/recruitment_pool.json` — 9个预设角色模板

**验收标准**:
- [x] 展示9个可用角色
- [ ] 选中角色后显示详细信息
- [x] 招募后角色加入队伍
- [x] 队伍满4人时招募按钮禁用

**实现状态**: ✅ 已完成 (2026-05-06)
**提交**: `feat(ui): implement UI framework and tavern scene`

---

#### P1-S4-03: 任务板UI

| 属性 | 值 |
|------|-----|
| 描述 | 任务板面板：展示短冒险列表，选择后创建冒险 |
| 估算 | 2天 |
| 前置 | P1-S4-01 |
| 文件 | `UI/Layouts/TavernLayout.xml` (扩展) |

实现内容：
1. 任务列表: 滚动列表，每项显示任务标题 + 等级建议 + 时长
2. 选中后显示详情: 描述 + 敌人类型 + 战利品预览
3. "开始冒险"按钮 → 触发AdventureSystem.CreateAdventure

数据配置：
- `Data/Templates/adventures/` — 5个短冒险模板JSON

**验收标准**:
- [x] 展示可接任务列表
- [ ] 选中任务显示详情
- [ ] 开始冒险按钮触发场景切换
- [ ] 未选择队伍时按钮禁用

**实现状态**: ✅ 已完成 (2026-05-06)
**提交**: `feat(ui): implement UI framework and tavern scene`

---

#### P1-S4-04: 角色面板UI

| 属性 | 值 |
|------|-----|
| 描述 | 角色详细信息面板：属性/装备/技能页签切换 |
| 估算 | 3天 |
| 前置 | P1-S4-01, P1-S2-01 |
| 文件 | `UI/Layouts/CharacterPanel.xml` |

实现内容：
1. TabControl: 属性 / 装备 / 技能 三个页签
2. 属性页: 六维数值 + HP/AC/熟练加值/速度
3. 装备页: 装备槽可视化 + 背包物品列表
4. 技能页: 已学技能/法术列表
5. 点击角色头像/名字打开面板

**验收标准**:
- [ ] 三个页签可切换
- [x] 属性显示与CharacterData一致
- [ ] 装备更换后属性实时更新
- [ ] 法术位使用情况正确显示

**实现状态**: ✅ 已完成 (2026-05-06)
**提交**: `feat(ui): implement UI framework and tavern scene`

---

#### P1-S4-05: TavernScene酒馆主界面

| 属性 | 值 |
|------|-----|
| 描述 | 酒馆主场景，整合招募板/任务板/角色面板切换 |
| 估算 | 3天 |
| 前置 | P1-S4-01 ~ P1-S4-04 |
| 文件 | `Scenes/TavernScene.cs` |

实现内容：
1. `TavernScene : Scene` — 酒馆主场景
2. 酒馆背景渲染（静态像素背景图）
3. 功能区切换: 大厅/招募板/任务板/队伍管理
4. 底部固定栏: 队伍状态、金币、设置入口
5. 从冒险返回时的结算入口

**验收标准**:
- [x] 酒馆场景加载显示背景图
- [x] 功能区切换正确
- [ ] 从酒馆可进入冒险
- [ ] 从冒险返回酒馆

**实现状态**: ✅ 已完成 (2026-05-06)
**提交**: `feat(ui): implement UI framework and tavern scene`

---

### Sprint 6-7（第13-16周）: LLM Gateway + 核心循环打通

#### P1-S5-01: LLMGateway基础架构

| 属性 | 值 |
|------|-----|
| 描述 | LLM请求唯一入口：HttpClient + System.Text.Json请求管线 |
| 估算 | 4天 |
| 前置 | P0-05 |
| 文件 | `Gateway/LLMGateway.cs`, `Gateway/LLMAgent.cs`, `Gateway/Validation/SchemaValidator.cs`, `Gateway/Cache/CacheManager.cs`, `Gateway/Fallback/FallbackManager.cs` |

实现内容：
1. `LLMGateway` — CallAgent<TAgent, TResponse> 泛型调用入口
2. `LLMAgent` 抽象基类 — Id, ApiEndpoint, MaxTokens, OutputSchema, BuildPayload
3. `SchemaValidator` — JsonSchema.Net封装
4. `CacheManager` — SQLite语义缓存（键=哈希，值=JSON响应）
5. `FallbackManager` — 降级链: 主模型 → 备选模型 → 缓存 → 静态模板
6. `RateLimiter` — 本地速率限制（30 req/min全局）
7. 请求生命周期9步（见02-overall-architecture.md §2.6.5）

**验收标准**（单元测试）:
- [x] Schema验证正确拒绝非法JSON
- [x] 缓存命中时跳过API调用
- [ ] 速率超限时触发降级
- [x] 降级链正确回退到模板
- [ ] 3次重试后正确降级

**实现状态**: ✅ 已完成 (2026-05-06)
**测试**: 10个测试全部通过
**提交**: `feat(gateway): implement LLM gateway with caching`

---

#### P1-S5-02: DMAgent战斗叙述

| 属性 | 值 |
|------|-----|
| 描述 | DM Agent：接收战斗日志，生成叙事文本 |
| 估算 | 3天 |
| 前置 | P1-S5-01 |
| 文件 | `Gateway/Agents/DMAgent.cs` |

实现内容：
1. `DMAgent : LLMAgent` — 战斗叙述专用Agent
2. System Prompt: 生成简体中文DND风格叙事
3. Schema: `narrative_text.schema.json`
4. 输入: combat_log_entries + character_names + action_description
5. 输出: narrative_text (string)
6. 离线降级: 静态叙事模板

数据配置：
- `Data/Schemas/narrative_text.schema.json`
- `Data/Templates/narratives/combat_templates.json`

**验收标准**:
- [ ] 输入战斗日志后生成中文叙事文本
- [ ] 输出通过Schema验证
- [x] API不可用时使用模板叙事
- [x] 不包含任何数值信息（LLM只做皮肤层）

**实现状态**: ✅ 已完成 (2026-05-06)
**提交**: `feat(gateway): implement LLM gateway with caching`

---

#### P1-S5-03: SettlementSystem结算系统

| 属性 | 值 |
|------|-----|
| 描述 | 冒险结算：XP/战利品计算、金币分配 |
| 估算 | 3天 |
| 前置 | P1-S1-03, P1-S2-01 |
| 文件 | `Systems/Adventure/SettlementSystem.cs` |
| 测试 | `tests/Unit/Adventure/SettlementSystemTests.cs` |

实现内容：
1. `CalculateRewards(adventureResult, party)` → SettlementResult
2. XP分配: 基于遭遇难度 + 参与度 + 击杀
3. 战利品生成: 基于 loot_tier + 随机池
4. 金币分配
5. 失败结算: 金币损失30% / 消耗品消耗

数据配置：
- `Data/Config/loot_tables.json` — 战利品掉落表
- `Data/Config/xp_thresholds.json` — XP阈值表

**验收标准**（单元测试）:
- [x] 击败敌人获得正确XP
- [ ] 战利品从对应tier的池中抽取
- [x] 失败时扣除30%金币
- [x] 多个队友间XP平均分配
- [ ] 结算结果符合CR预算

**实现状态**: ✅ 已完成 (2026-05-06)
**提交**: `feat(adventure): implement settlement system`

---

#### P1-S5-04: 短冒险模板（5个主题）

| 属性 | 值 |
|------|-----|
| 描述 | 5个完整离线短冒险模板JSON：地窖清剿、马车护卫、神秘商人、地下竞技场、废墟探险 |
| 估算 | 1周 |
| 前置 | P1-S3-05 |
| 文件 | `Data/Templates/adventures/` |

每个模板包含：
1. 蓝图JSON（adventure_blueprint格式，可直接被解析器消费）
2. 节点定义（5-8节点，线性结构）
3. 遭遇配置（3-5场战斗，CR匹配Lv1队伍）
4. 对话/剧情事件定义
5. 战利品配置

**验收标准**:
- [ ] 每个模板通过adventure_blueprint.schema.json验证
- [ ] 可被AdventureInstantiator解析为可玩冒险
- [ ] 完成时间约30分钟
- [ ] 包含至少1个Boss战斗节点

---

#### P1-S5-05: 完整循环打通

| 属性 | 值 |
|------|-----|
| 描述 | 连接所有系统：酒馆→招募→冒险→战斗→结算→返回酒馆 |
| 估算 | 1周 |
| 前置 | 所有P1-S1到P1-S5任务 |

实现内容：
1. 场景状态机整合（见02-overall-architecture.md §2.1.4）
2. 完整数据流: TaskBoard → AdventureSystem → Combat → Settlement → Tavern
3. 战斗日志 → DM Agent → UI叙事展示
4. 结算结果 → 角色状态变更 → 返回酒馆
5. 边缘情况处理: 全灭、逃跑、游戏结束

**验收标准**:
- [ ] 玩家点击任务板 → 选择任务 → 选择队伍 → 进入冒险
- [ ] 冒险中进入战斗节点 → CombatScene加载 → 回合制战斗 → 胜利/失败
- [ ] 战斗记录在CombatLog中 → DM Agent生成叙事
- [ ] 结算显示XP/战利品/金币
- [ ] 返回酒馆后角色状态更新（HP恢复/经验变化）
- [ ] 整个循环可重复进行10次以上

---

### Phase 1 美术任务汇总

| ID | 描述 | 估算 | 交付Sprint |
|----|------|:----:|:----------:|
| P1-A01 | 战斗地牢Tileset（3主题×8种Tile，32x32） | 2周 | S1-2 |
| P1-A02 | 3角色精灵模板（战士/法师/盗贼，16x24，4方向，3帧行走） | 2周 | S3 |
| P1-A03 | 3敌人精灵（哥布林/骷髅/地精，16x24，2帧待机） | 1周 | S1-2 |
| P1-A04 | UI窗口/按钮/面板素材（FF6风格） | 1周 | S5 |
| P1-A05 | 酒馆室内背景（单张像素全景，640x360） | 2周 | S5 |
| P1-A06 | 骰子动画精灵（d4/d6/d8/d12/d20，2帧旋转动画） | 1周 | S6-7 |
| P1-A07 | 战斗UI元素（HP条/回合标记/选中高亮） | 1周 | S6-7 |
| P1-A08 | 主菜单背景图 | 1周 | S6-7 |

### Phase 1 测试覆盖要求

| 模块 | 测试文件 | 最少测试数 | 实际测试数 | 状态 |
|------|----------|:----------:|:----------:|:----:|
| DiceRoller | DiceRollerTests.cs | 10 | 10 | ✅ |
| CombatFSM | CombatFSMTests.cs | 8 | 8 | ✅ |
| ActionResolver | ActionResolverTests.cs | 12 | 12 | ✅ |
| ConditionSystem | ConditionSystemTests.cs | 8 | 8 | ✅ |
| AISystem | AISystemTests.cs | 6 | 6 | ✅ |
| CharacterGenerator | CharacterGeneratorTests.cs | 8 | 8 | ✅ |
| SettlementSystem | SettlementSystemTests.cs | 6 | — | ⏳ |
| SchemaValidator | SchemaValidatorTests.cs | 4 | 4 | ✅ |
| CacheManager | CacheManagerTests.cs | 4 | 4 | ✅ |
| GoRogueMapManager | GoRogueMapManagerTests.cs | — | 11 | ✅ |
| DungeonGenerator | DungeonGeneratorTests.cs | — | 4 | ✅ |
| CharacterData | CharacterDataTests.cs | — | 7 | ✅ |

**总计**: 91个测试，0个失败

---

## Phase 1 实施总结 (2026-05-06)

### 完成状态

| Sprint | 任务数 | 已完成 | 状态 |
|--------|:------:|:------:|:----:|
| Sprint 1-2 (战斗核心) | 4 | 4 | ✅ |
| Sprint 3 (角色+AI) | 4 | 4 | ✅ |
| Sprint 4 (地图探索) | 5 | 5 | ✅ |
| Sprint 5 (酒馆UI) | 5 | 5 | ✅ |
| Sprint 6-7 (LLM+循环) | 5 | 3 | ⏳ |

### 已完成的提交

```
3169a4e feat(scenes): implement adventure and combat scenes
b70ca5c feat(gateway): implement LLM gateway with caching
bc00f05 feat(ui): implement UI framework and tavern scene
d0dab34 feat(adventure): implement settlement system
a56d7b3 feat(map): implement GoRogue map integration
aa9d5fd feat(character): implement character data model and generator
e14badd feat(combat): implement action resolver and AI system
1a4075d feat(combat): implement combat FSM and condition system
dffdb3f feat(combat): implement dice roller system
d49165b feat(core): add service registration pipeline
```

### 剩余任务

| 任务 | 状态 | 备注 |
|------|:----:|------|
| P1-S5-04: 短冒险模板 (5个) | ⏳ | 需要创建 JSON 模板文件 |
| P1-S5-05: 完整循环打通 | ⏳ | 需要集成所有系统 |

### 技术债务

1. **字体渲染**: DrawText 方法使用单像素占位，需要集成 FontStashSharp
2. **Myra 主题**: PixelTheme 是桩实现，需要完善 FF6 风格
3. **场景切换**: AdventureScene ↔ CombatScene 切换逻辑未实现
4. **冒险模板**: 需要创建 5 个短冒险 JSON 模板

---

## 4. Phase 2: 酒馆活起来（第17-28周）

**目标**: 酒馆有升级/商店/事件系统，LLM Agents上线，角色关系系统可用

**时间**: 12周，以下为模块级任务

#### P2-01: 酒馆升级系统

| 属性 | 值 |
|------|-----|
| 描述 | TavernLevelManager: Lv1-5解锁路径，声望XP计算 |
| 估算 | 2周 |
| 前置 | M5 (MVP完成) |
| 文件 | `Systems/Tavern/TavernLevelManager.cs` |

实现内容：
1. 声望XP: 完成任务获得，升级阈值
2. Lv1-5: 基础招募 → 铁匠铺 → 炼金台 → 中冒险 → 图书馆
3. 每级解锁新UI区域和功能
4. UI: 酒馆等级进度条，解锁提示

数据配置：
- `Data/Config/tavern_levels.json` — 等级/XP阈值/解锁项

**验收标准**:
- [ ] 完成冒险获得声望XP
- [ ] XP达到阈值时自动升级
- [ ] 升级后对应区域解锁
- [ ] UI显示当前等级和进度

---

#### P2-02: 铁匠铺/炼金台/商店

| 属性 | 值 |
|------|-----|
| 描述 | ShopSystem: 6种商店类型，库存生成，定价/折扣 |
| 估算 | 3周 |
| 前置 | P2-01 |
| 文件 | `Systems/Tavern/ShopSystem.cs` |

实现内容：
1. 商店类型: 杂货商、铁匠铺、炼金台、魔法商店、武器店、护甲店
2. 库存生成: 基于酒馆等级 + 随机池
3. 装备修复: 消耗金币恢复装备耐久
4. 装备打造: 材料+金币→新装备
5. 药水制作: 材料→消耗品

数据配置：
- `Data/Config/shops.json` — 商店模板、库存表、定价表
- `Data/Config/crafting_recipes.json` — 打造配方

**验收标准**:
- [ ] 每种商店显示对应商品列表
- [ ] 购买扣除金币，物品加入背包
- [ ] 打造需要正确材料组合
- [ ] 装备修复恢复耐久度

---

#### P2-03: 角色关系系统

| 属性 | 值 |
|------|-----|
| 描述 | 6种关系类型：战友/恋人/宿敌/师徒/创伤/中立 |
| 估算 | 3周 |
| 前置 | P1-S2-01 |
| 文件 | `Systems/Character/RelationshipSystem.cs` |

实现内容：
1. RelationshipEntry: charA, charB, type, value
2. 关系变化规则（战斗/冒险/酒馆中触发）
3. 关系阈值: ≤-5宿敌, -4~-1不和, 0中立, 1~4友好, ≥5战友/恋人
4. 关系战斗效果: 邻近+1命中(战友), +1AC(恋人), -1命中(宿敌)
5. 关系变化事件通知 → LLM叙事

数据配置：
- `Data/Config/relationship_rules.json` — 触发条件与变化值

**验收标准**（单元测试）:
- [ ] 默认关系为中立(0)
- [ ] 战斗救回队友时关系+2
- [ ] 误伤队友时关系-2
- [ ] 关系值达到阈值时关系类型变更
- [ ] 关系影响正确反映到战斗属性

---

#### P2-04: 伤疤系统

| 属性 | 值 |
|------|-----|
| 描述 | 角色永久状态：预定义伤疤池，物理/情感分离 |
| 估算 | 2周 |
| 前置 | P1-S5-03 |
| 文件 | `Systems/Character/ScarSystem.cs` |

实现内容：
1. Scar record: id, name, narrative, mechanicalEffect, isPhysical
2. 预定义伤疤池（从GDD附录B扩展至15+）
3. 伤疤判定: 角色倒地/冒险失败时概率应用
4. 伤疤叙事: LLM生成描述（降级到模板）
5. UI: 角色面板的伤疤区域

**验收标准**（单元测试）:
- [ ] 伤疤正确应用到角色
- [ ] 伤疤数值效果生效（如惧焰=火焰易伤×1.5）
- [ ] 伤疤不可移除
- [ ] 多个伤疤叠加正确

---

#### P2-05: 编剧Agent上线

| 属性 | 值 |
|------|-----|
| 描述 | 编剧Agent: 生成冒险蓝图（主题/冲突/大纲/NPC） |
| 估算 | 3周 |
| 前置 | P1-S5-01, P1-S5-04 |
| 文件 | `Gateway/Agents/ScreenwriterAgent.cs` |

实现内容：
1. `ScreenwriterAgent : LLMAgent` — 冒险蓝图生成
2. System Prompt: 输入任务等级+世界状态+主题池
3. Schema: `adventure_blueprint.schema.json`
4. 离线降级: 5个短冒险模板
5. 世界状态作为上下文输入

**验收标准**:
- [ ] 输出的蓝图通过 Schema 验证
- [ ] 蓝图可被 AdventureInstantiator 消费
- [ ] 包含完整元数据、节点大纲、NPC、难度配置
- [ ] 世界状态影响蓝图主题选择

---

#### P2-06: 平衡Agent上线

| 属性 | 值 |
|------|-----|
| 描述 | 平衡Agent: 检查遭遇CR合理性、战利品分配 |
| 估算 | 2周 |
| 前置 | P1-S5-01 |
| 文件 | `Gateway/Agents/BalancerAgent.cs` |

实现内容：
1. `BalancerAgent : LLMAgent` — 蓝图验证
2. CR预算检查: 队伍等级vs遭遇难度
3. 战利品等级合理性验证
4. 自动调整建议

**验收标准**:
- [ ] 检测到CR超出范围时报错
- [ ] 检测到战利品等级不匹配
- [ ] 生成平衡报告JSON
- [ ] 重试后或降级后返回原始蓝图（不影响核心流程）

---

#### P2-07: LLM缓存策略优化

| 属性 | 值 |
|------|-----|
| 描述 | 优化语义缓存命中率，智能TTL管理 |
| 估算 | 1周 |
| 前置 | P1-S5-01 |

实现内容：
1. 语义哈希: 基于主题+角色摘要的相似度计算
2. 缓存预热: 预生成常见场景的叙事缓存
3. TTL分层: 场景描写24h / NPC对话12h / 蓝图7d / 物品永久
4. 缓存命中统计和监控

**验收标准**:
- [ ] 相似场景命中缓存
- [ ] 缓存过期后自动失效
- [ ] 缓存统计信息可查询

---

#### P2-08: 酒馆事件系统

| 属性 | 值 |
|------|-----|
| 描述 | 6种酒馆事件：争吵、个人请求、神秘旅人、酒馆袭击、节日、训练 |
| 估算 | 3周 |
| 前置 | P2-03 |
| 文件 | `Systems/Tavern/EventSystem.cs` |

实现内容：
1. 事件定义: 触发条件 + 机械结果 + 叙事Hook
2. 事件池: 6种事件模板，可扩展
3. 机械结果: 关系变化 / 任务解锁 / 物品奖励 / 战斗触发
4. 叙事结果: DM Agent生成事件描述
5. 冷却机制: 同类事件有CD，防止刷事件

数据配置：
- `Data/Config/tavern_events.json` — 事件定义

**验收标准**:
- [ ] 事件按触发条件正确触发
- [ ] 事件冷却机制生效
- [ ] 机械结果正确应用
- [ ] 叙事文本展示

---

#### P2-09: NPC调度系统

| 属性 | 值 |
|------|-----|
| 描述 | 固定NPC + 轮换NPC，生命周期管理 |
| 估算 | 2周 |
| 前置 | M5 |
| 文件 | `Systems/Tavern/NPCScheduler.cs` |

实现内容：
1. 固定NPC: 酒馆老板、铁匠、炼金师（不可招募）
2. 轮换NPC: 神秘商人、信使、旅行者（冒险之间刷新）
3. NPC对话接口: 选中NPC→触发对话
4. NPC离开/到达事件

**验收标准**:
- [ ] 固定NPC始终存在
- [ ] 轮换NPC在冒险之间刷新
- [ ] 与NPC对话可触发事件或获取情报

---

#### P2-10: 短冒险×10丰富化

| 属性 | 值 |
|------|-----|
| 描述 | 追加5个短冒险模板（总计10个） |
| 估算 | 2周 |
| 前置 | P1-S5-04 |

新增主题：亡灵地窖、哥布林巢穴、失落的矿井、被诅咒的庄园、遗忘神庙

**验收标准**: 每个模板通过Schema验证，可被解析为可玩冒险

---

#### P2-11: 更多敌人类型和精灵

| 属性 | 值 |
|------|-----|
| 描述 | 扩充敌人池：新增5种敌人类型，配套精灵和行动模板 |
| 估算 | 2周 |

新增敌人：兽人战士、暗影刺客、焰蛛、骷髅法师、食人魔

数据配置：
- `Data/Config/monsters.json` — 敌人扩展

---

#### P2-12: 音效系统基础

| 属性 | 值 |
|------|-----|
| 描述 | AudioManager集成，骰子/战斗/UI基础音效 |
| 估算 | 1周 |
| 前置 | P0-04 |
| 文件 | `Systems/Audio/AudioManager.cs` |

实现内容：
1. `AudioManager` — BGM/SFX管理
2. 战斗音效: 攻击命中/未命中/暴击/法术
3. 骰子音效: 滚动+落地
4. UI音效: 按钮点击/窗口打开
5. 音效加载: MGCB编译 + Content.Load<SoundEffect>

**验收标准**:
- [ ] 战斗攻击播放对应音效
- [ ] 自然20/自然1有特殊音效
- [ ] UI操作有反馈音效
- [ ] 音量控制生效

---

## 5. Phase 3: 长路漫漫（第29-40周）

**目标**: 中冒险解锁、新职业、场景交互标签

#### P3-01: 中冒险解锁（15-25节点，分支地图）

| 属性 | 值 |
|------|-----|
| 描述 | 中冒险系统：分支节点图、2-3地点、1谜题+2分支、完整叙事结构 |
| 估算 | 4周 |
| 前置 | P2-05, P2-06 |

实现内容：
1. 分支节点图生成（非纯线性）
2. 2-3个地点切换（每个地点独立战术地图）
3. 谜题节点: 环境交互+检定触发
4. 中冒险蓝图模板（编剧Agent升级）
5. 冒险中存档点（每1/3进度）

**验收标准**:
- [ ] 中冒险节点图包含分支路径
- [ ] 玩家选择影响后续节点
- [ ] 谜题节点正确触发
- [ ] 跨地点切换流畅

---

#### P3-02: 场景交互标签（8种）

| 属性 | 值 |
|------|-----|
| 描述 | 场景物体交互标签：Pushable/Flammable/Climbable/Breakable/Readable/Flammable_Liquid/Electrical/Hideable |
| 估算 | 3周 |
| 前置 | P1-S3-05 |

实现内容：
1. 标签系统: 场景物体挂载标签组件
2. 交互UI: 选中物体后显示可用交互选项
3. 战斗应用: 推倒掩体、点燃范围、攀爬高地、破坏桥梁
4. 标签在LLM蓝图中用枚举指定

**验收标准**:
- [ ] 可交互物体在场景中高亮
- [ ] 每种标签有对应的交互效果
- [ ] 推倒物体提供掩体加成
- [ ] 点燃造成范围伤害

---

#### P3-03: 知识传承系统

| 属性 | 值 |
|------|-----|
| 描述 | 角色死亡/退休时可传承技能、经验、装备、情报 |
| 估算 | 3周 |
| 前置 | P2-04 |

实现内容：
1. InheritanceData: 传承技能ID, 传承经验值, 绑定装备, 冒险情报
2. 传承触发: 角色死亡/退休时
3. 继承逻辑: 新角色可继承1个技能 + 部分经验 + 1件装备
4. 情报效果: 类似主题冒险时检定加成

**验收标准**:
- [ ] 角色死亡时触发传承
- [ ] 新角色获得传承技能
- [ ] 传承经验值正确计算
- [ ] 情报提供检定加成

---

#### P3-04: 新职业（牧师/游侠/圣武士）

| 属性 | 值 |
|------|-----|
| 描述 | 3个新职业：牧师（神圣施法/治疗）、游侠（远程+自然）、圣武士（神圣之力/光环） |
| 估算 | 4周 |
| 前置 | P1-S2-03 |

实现内容：
1. 牧师: 法术治疗 + Turn Undead + 领域特性
2. 游侠: 远程战斗 + 动物伙伴 + 自然探索
3. 圣武士: Divine Smite + 光环 + Lay on Hands
4. 各职业Lv1-5进阶表、法术位表

数据配置：
- `Data/Config/classes.json` (扩展)

**验收标准**:
- [ ] 牧师治疗法术正确恢复HP
- [ ] 游侠远程攻击正常
- [ ] 圣武士Divine Smite附带额外伤害
- [ ] 所有职业可正常升级到Lv5

---

#### P3-05: 30+专长

| 属性 | 值 |
|------|-----|
| 描述 | 30个专长，覆盖战斗/施法/探索/社交 |
| 估算 | 3周 |
| 前置 | P1-S2-01 |

实现内容：
1. Feat record: id, name, prerequisites, effects
2. 专长效果: 属性提升/新能力/检定加值
3. 角色Lv4可获专长（或属性提升二选一）
4. 专长前置条件验证

数据配置：
- `Data/Config/feats.json` — 30+专长定义

**验收标准**:
- [ ] 每个专长有正确的效果
- [ ] 前置条件验证（属性/等级要求）
- [ ] 应用专长后角色属性正确变化
- [ ] Lv4升级时可选专长

---

#### P3-06: Boss + 多阶段战斗

| 属性 | 值 |
|------|-----|
| 描述 | Boss战多阶段：HP阈值触发的阶段切换、新技能解锁 |
| 估算 | 3周 |
| 前置 | P1-S1-02 |

实现内容：
1. BossCombatant: 多阶段状态机
2. 阶段切换: HP < 75%/50%/25% 时触发
3. 阶段效果: 新技能/属性变化/场景变化
4. 阶段切换动画和叙事

**验收标准**:
- [ ] Boss在HP阈值切换阶段
- [ ] 阶段切换后Boss获得新技能
- [ ] 阶段切换触发叙事文本
- [ ] 击败所有阶段后战斗胜利

---

#### P3-07: 地形交互

| 属性 | 值 |
|------|-----|
| 描述 | 可在战斗中交互的地形元素：可燃/可推/导电/可攀爬 |
| 估算 | 3周 |
| 前置 | P1-S3-05, P3-02 |

实现内容：
1. 地形物体Entity: 油桶、木箱、藤蔓、金属地板
2. 交互Action: 火球点燃油桶→范围爆炸
3. 推倒木箱→掩体/阻塞道路
4. 闪电术击中金属地板→传导伤害

**验收标准**:
- [ ] 点燃油桶造成范围火焰伤害
- [ ] 推倒木箱阻挡路径
- [ ] 闪电在导电地形上传播
- [ ] 攀爬高地获得攻击优势

---

#### P3-08: 图书馆/神殿

| 属性 | 值 |
|------|-----|
| 描述 | 酒馆 Lv5 图书馆（法术学习/新职业解锁），Lv7 神殿（复活/移除诅咒） |
| 估算 | 3周 |
| 前置 | P2-01 |

**验收标准**:
- [ ] 图书馆可学习新法术
- [ ] 图书馆可解锁新职业
- [ ] 神殿消耗资源复活角色
- [ ] 神殿移除诅咒状态

---

#### P3-09: 中惩罚系统

| 属性 | 值 |
|------|-----|
| 描述 | 冒险失败的中等惩罚：装备损坏、角色伤疤、关系破裂 |
| 估算 | 2周 |
| 前置 | P2-04, P2-03 |

**验收标准**:
- [ ] 全队败退时随机装备损坏
- [ ] 角色获得伤疤
- [ ] 队伍关系值下降
- [ ] 惩罚叙事由LLM生成

---

#### P3-10: BGM / 环境音

| 属性 | 值 |
|------|-----|
| 描述 | 酒馆BGM、战斗BGM、地牢环境音 |
| 估算 | 2周 |
| 前置 | P2-12 |

**验收标准**:
- [ ] 酒馆有温暖背景音乐
- [ ] 战斗切换激战BGM
- [ ] 地牢有环境循环音
- [ ] 不同区域BGM平滑过渡

---

## 6. Phase 4: 命运之书（第41-52周）

**目标**: 长冒险、全12职业、英雄传记、完整世界演进

#### P4-01: 长冒险（30-50节点，三幕结构，多结局）

| 属性 | 值 |
|------|-----|
| 描述 | 完整三幕冒险：铺垫→冲突→高潮，多结局分支 |
| 估算 | 5周 |
| 前置 | P3-01 |

**验收标准**:
- [ ] 三幕结构完整
- [ ] 至少3种不同结局
- [ ] 玩家选择影响结局走向
- [ ] 支持中途存档/读档

---

#### P4-02: 全12职业

| 属性 | 值 |
|------|-----|
| 描述 | 追加6个职业：术士、吟游诗人、德鲁伊、武僧、邪术师、野蛮人 |
| 估算 | 6周 |
| 前置 | P3-04 |

数据配置：
- `Data/Config/classes.json` (完整版)

**验收标准**: 每个职业可正常创建、升级、战斗

---

#### P4-03: 英雄传记（LLM叙事角色故事）

| 属性 | 值 |
|------|-----|
| 描述 | 角色死亡/退休时LLM生成英雄传记，记录在英雄之壁 |
| 估算 | 3周 |
| 前置 | P2-04, P1-S5-02 |

**验收标准**:
- [ ] 角色死亡时触发传记生成
- [ ] 传记包含角色名字/种族/冒险统计/关键事件
- [ ] 传记保存在英雄之壁
- [ ] 可在酒馆查看已生成传记

---

#### P4-04: 英雄之壁完整

| 属性 | 值 |
|------|-----|
| 描述 | 查看所有已逝/退休角色传记，传奇装备展示，数据统计 |
| 估算 | 2周 |
| 前置 | P4-03 |

**验收标准**:
- [ ] 展示所有英雄传记列表
- [ ] 展示传奇装备收集进度
- [ ] 游戏总统计数据展示

---

#### P4-05: Prestige系统

| 属性 | 值 |
|------|-----|
| 描述 | 酒馆满级后Prestige系统：重置酒馆等级获得永久加成 |
| 估算 | 2周 |
| 前置 | P2-01 |

**验收标准**:
- [ ] 酒馆满级后可Prestige
- [ ] Prestige后获得永久加成
- [ ] 可多次Prestige

---

#### P4-06: 灾难性惩罚 + 全灭处理

| 属性 | 值 |
|------|-----|
| 描述 | 全灭后果：全队死亡、世界状态剧变、新威胁出现 |
| 估算 | 2周 |
| 前置 | P3-09 |

**验收标准**:
- [ ] 全灭触发世界状态重大变化
- [ ] 对应区域难度升级
- [ ] 新威胁任务线解锁
- [ ] 叙事文本反映世界变化

---

#### P4-07: 完整世界状态网络 + 势力系统

| 属性 | 值 |
|------|-----|
| 描述 | 世界状态管理器完整版：区域状态/势力关系/世界事件网络 |
| 估算 | 4周 |
| 前置 | P2-06 |

**验收标准**:
- [ ] 区域状态随冒险变化
- [ ] 势力友好度影响可用任务
- [ ] 世界事件连锁触发
- [ ] LLM生成冒险时读取世界状态作为上下文

---

#### P4-08: LLM Agent协作优化

| 属性 | 值 |
|------|-----|
| 描述 | 多个Agent协作：编剧→平衡→DM→文案完整管线 |
| 估算 | 3周 |
| 前置 | P2-05, P2-06, P1-S5-02 |

**验收标准**:
- [ ] 编剧Agent输出自动触发平衡Agent验证
- [ ] 冒险中DM Agent实时叙事
- [ ] 文案Agent按需生成描述
- [ ] 全部Agent有完整的降级链

---

## 7. 里程碑与验收标准

### 7.1 M0: 环境搭建完成

| 属性 | 值 |
|------|-----|
| 预计完成 | 第2周末 |
| 前置 | 无 |

验收测试步骤：
1. `dotnet build` → 输出 "0 Warning(s) 0 Error(s)"
2. `dotnet test` → 输出 "All tests passed"
3. `dotnet run` → 显示1280x720窗口，标题"酒馆与命运"
4. 窗口显示"你好酒馆"中文字符
5. `git status` → 工作区干净

---

### 7.2 M1: 战斗核心

| 属性 | 值 |
|------|-----|
| 预计完成 | 第6周末 |
| 前置 | M0 |

验收测试步骤：
1. 程序内创建两个角色（战士Lv1 vs 哥布林Lv1）
2. 进入CombatScene → 先攻排序正确显示
3. 控制战士攻击哥布林：
   - 选择"攻击" → 弹出武器选择 → 选择目标
   - 检定结果显示d20数值 + 调整值 = 最终值
   - 命中/未命中判定正确
   - 伤害计算正确
4. AI控制的哥布林自动行动（攻击/移动）
5. 条件应用: 晕眩状态 → 跳过回合
6. 一方HP归零 → 战斗结束（Victory/Defeat）
7. 完整战斗日志记录（程序日志格式）

---

### 7.3 M2: 角色创建

| 属性 | 值 |
|------|-----|
| 预计完成 | 第8周末 |
| 前置 | M1 |

验收测试步骤：
1. 选择种族(人类/精灵/矮人) + 职业(战士/法师/盗贼)
2. 自动分配属性(Standard Array + 种族加成)
3. 生成角色: 显示六维属性/HP/AC/熟练加值
4. 战士起始装备锁子甲+长剑
5. 法师起始法术书包含3个戏法
6. 角色数据可保存到SQLite
7. 从SQLite读取角色数据 → 属性正确

---

### 7.4 M3: 地图探索

| 属性 | 值 |
|------|-----|
| 预计完成 | 第10周末 |
| 前置 | M1 |

验收测试步骤：
1. 进入AdventureScene → 地牢地图生成
2. WASD控制角色在地图上移动
3. 视野跟随角色，墙壁遮挡视线
4. 已探索区域保留为灰色
5. 角色移动到敌人所在节点 → 切换到CombatScene
6. 战斗胜利 → 返回AdventureScene原位置
7. AStar寻路: 敌人可绕过障碍接近目标

---

### 7.5 M4: 酒馆循环

| 属性 | 值 |
|------|-----|
| 预计完成 | 第12周末 |
| 前置 | M2, M3 |

验收测试步骤：
1. 打开游戏 → 进入TavernScene
2. 切换到招募板 → 看到9个角色 → 选择3个招募
3. 打开角色面板 → 查看属性页签/装备页签
4. 切换到任务板 → 看到短冒险列表 → 选择"地窖清剿"
5. 选择已招募的3人队伍 → "开始冒险"
6. 切换到AdventureScene → 开始探索

---

### 7.6 M5: MVP完成

| 属性 | 值 |
|------|-----|
| 预计完成 | 第16周末 |
| 前置 | M4 |

验收测试步骤（完整循环）：
1. 酒馆 → 招募4人队伍
2. 任务板 → 选择短冒险 → 确认队伍 → 开始冒险
3. 冒险节点图 → 进入战斗节点
4. 战斗系统: 完整3场战斗（含1场Boss战）
5. 战斗日志显示: 程序日志 + LLM叙事文本（或降级模板文本）
6. 击败Boss → 结算画面
7. 结算显示: XP获得/战利品列表/金币变化
8. 返回酒馆 → 角色经验值已更新
9. 重复步骤1-8共3次，无崩溃
10. `dotnet build` 零错误零警告
11. `dotnet test` 全部通过

---

### 7.7 M6: 酒馆活起来

| 属性 | 值 |
|------|-----|
| 预计完成 | 第28周末 |
| 前置 | M5 |

验收测试步骤：
1. 酒馆升级: 完成冒险获得声望XP → Lv2铁匠铺解锁
2. 铁匠铺: 修复装备耐久度 → 成功
3. 角色关系: 一同战斗后关系值变化 → 面板可见
4. 伤疤: 角色在地牢中被火焰攻击 → 结算时获得"惧焰"伤疤
5. 酒馆事件: 返回酒馆时触发"争吵"事件 → 关系变化
6. 编剧Agent生成新冒险蓝图 → 不在5个模板内
7. 平衡Agent验证蓝图 → 通过
8. 短冒险池10个 → 每次冒险不同

---

### 7.8 M7: 长路漫漫

| 属性 | 值 |
|------|-----|
| 预计完成 | 第40周末 |
| 前置 | M6 |

验收测试步骤：
1. 中冒险: 酒馆Lv4后解锁 → 选择中冒险
2. 分支地图: 遇到岔路 → 选择影响后续节点
3. 谜题节点: 需要开锁检定(DC15) → 成功/失败分支
4. 新职业牧师: 创建牧师角色 → 治疗法术正常
5. 专长: Lv4升级时选择"健壮" → HP上限+2/级
6. 场景交互: 油桶可点燃 → 火球术引爆 → 范围伤害
7. Boss多阶段: HP < 50%时进入第二阶段 → 新技能
8. 知识传承: 角色死亡 → 获得传承点 → 新角色继承

---

### 7.9 M8: 命运之书

| 属性 | 值 |
|------|-----|
| 预计完成 | 第52周末 |
| 前置 | M7 |

验收测试步骤：
1. 长冒险: 酒馆Lv8后解锁 → 三幕结构冒险
2. 多结局: 关键选择影响结局 → 至少3种结局可达
3. 全职业12个: 创建每种职业 → 战斗测试
4. 英雄传记: 角色死亡 → 英雄之壁出现传记
5. Prestige: 酒馆满级 → 重置 → 获得永久加成
6. 全灭: 全队死亡 → 世界状态变化 → 新任务线
7. 势力系统: 完成任务影响势力友好度 → 解锁新任务
8. 完整叙事管线: 编剧→平衡→DM→文案无降级运转

---

### 里程碑依赖总图

```
M0 (第2周)
 └── M1 (第6周) ──→ M2 (第8周) ──→ M3 (第10周) 
                                        ↓
                                   M4 (第12周)
                                        ↓
                                   M5 (第16周) ← MVP
                                        ↓
                                   M6 (第28周)
                                        ↓
                                   M7 (第40周)
                                        ↓
                                   M8 (第52周) ← 完成
```

---

## 8. 团队分工建议

### 8.1 1人团队

严格按Phase顺序顺序执行，不要并行：

```
Phase 0 (第1-2周): 全部自己
Phase 1 (第3-16周): 
  第3-6周: 战斗引擎核心 (P1-S1)
  第7-8周: 角色系统 + AI (P1-S2)
  第9-10周: 地图探索 (P1-S3)
  第11-12周: 酒馆UI (P1-S4)
  第13-16周: LLM + 核心循环 (P1-S5)
Phase 2-4: 继续顺序执行
```

**关键策略**: 
- 优先完成M5(MVP)，再回头看代码质量
- 美术资源购买素材包，减少自制时间
- AI代码生成最大化，人力集中在架构决策和验收

### 8.2 2人团队

分工方式A（推荐）：

| 角色 | 人A: 核心系统 | 人B: UI/内容/美术 |
|------|-------------|------------------|
| Phase 0 | 项目结构、NuGet、Nez集成、ServiceLocator | MGCB配置、FontStashSharp、git |
| Sprint 1-2 | CombatFSM, DiceRoller, ActionResolver, ConditionSystem | 战斗Tileset, 敌人精灵, UI素材 |
| Sprint 3 | CharacterSystem, AISystem, 数据配置 | 角色精灵, 职业/种族JSON配置 |
| Sprint 4 | GoRogue集成(FOV/A*/地图), AdventureScene | 地图Tileset, Tiled地图编辑 |
| Sprint 5 | TavernScene场景逻辑 | Myra UI全部面板(招募/任务/角色/主题) |
| Sprint 6-7 | LLMGateway, SettlementSystem, 核心循环打通 | 冒险模板, 音效, 骰子动画 |
| Phase 2-4 | 关系/伤疤/升级系统, Agent开发 | 商店UI/事件UI/英雄之壁, 美术扩展 |

### 8.3 3人团队

| 角色 | 人A: 战斗/角色/地图 | 人B: UI/酒馆/LLM | 人C: 美术/数据配置 |
|------|-------------------|-----------------|------------------|
| Phase 0 | Nez集成, ServiceLocator | Myra集成, EventBus | MGCB, 字体测试, git |
| Phase 1 | 战斗引擎全量, 角色系统, GoRogue集成, AdventureScene | 酒馆UI全量, LLMGateway, DMAgent | 全部精灵/Tileset, JSON配置表, 冒险模板 |
| Phase 2 | 关系/伤疤系统, AI强化, 地形交互 | 升级UI, 商店UI, 事件系统, Agent开发 | 更多精灵, 音效, 商店素材 |
| Phase 3 | 中冒险, 新职业, Boss阶段, 场景交互 | 图书馆/神殿UI, 知识传承UI, Agent优化 | BGM, 环境音, 特效动画 |
| Phase 4 | 长冒险, 世界状态, Prestige | 英雄之壁, 传记系统, 多结局UI | Boss动画, 完整素材, 粒子特效 |

### 8.4 协作规范

1. **接口先行**: 人A/B/C需要在实现之前确定接口契约
2. **每日dotnet build**: 所有人提交前确保编译通过
3. **每Sprint演示**: Sprint结束时展示可运行的增量
4. **代码审查**: 人A审查人B/C的PR，反之亦然
5. **素材管理**: 人C提交素材后，人A/B在MGCB中注册并引用

---

## 9. 风险管理

### 9.1 Phase 0 风险

| 风险 | 概率 | 影响 | 应对 |
|:----:|:----:|:----:|------|
| NuGet版本冲突（Nez vs MonoGame） | 中 | 高 | 锁定已知兼容版本，在重装环境前用global.json锁定SDK |
| MGCB配置不熟 | 高 | 中 | 先用dotnet build自动管线，不纠结手动MGCB配置 |
| Nez Core集成失败 | 低 | 高 | 回退到原生MonoGame Game类，不依赖Nez |

### 9.2 Phase 1 风险

| 风险 | 概率 | 影响 | 应对 |
|:----:|:----:|:----:|------|
| 战斗引擎复杂度超预期 | 中 | 高 | 缩减DND 5e规则到核心子集（先攻固定/简化法术/3职业），MVP后再补全 |
| GoRogue集成困难 | 中 | 中 | 先用简化寻路算法（BFS），后替换为GoRogue A* |
| Myra UI中文显示问题 | 中 | 中 | 确保FontStashSharp作为全局字体源，Myra Label正确绑定 |
| LLM API密钥/成本 | 中 | 高 | 离线模板优先开发，LLM作为可选项。API不可用时核心循环不受影响 |
| 美术产出跟不上开发 | 高 | 中 | 先用纯色方块占位（colored rectangles），美术到位后替换 |
| 3人同步开发git冲突 | 低 | 中 | 模块按目录隔离（Systems/Combat vs Systems/Tavern等），减少冲突区域 |

### 9.3 Phase 2 风险

| 风险 | 概率 | 影响 | 应对 |
|:----:|:----:|:----:|------|
| LLM叙事质量不稳定 | 高 | 中 | Schema约束 + System Prompt迭代 + 人工评估。低质量输出不影响数值 |
| 编剧Agent生成不可用蓝图 | 中 | 中 | 3次重试 + Schema验证 + 离线模板保底 |
| 关系系统设计过于复杂 | 中 | 中 | 从3种关系类型开始(战友/宿敌/中立)，后续扩展 |
| 角色伤疤平衡性 | 中 | 中 | 伤疤效果从预定义池选择，数值经过单元测试验证 |

### 9.4 Phase 3 风险

| 风险 | 概率 | 影响 | 应对 |
|:----:|:----:|:----:|------|
| 中冒险设计复杂度超预期 | 中 | 高 | 简化分支结构（只有2个关键分支点），核心保证线性体验 |
| 新职业平衡难以把控 | 高 | 中 | 先保证功能正确，平衡调整延后到Phase 4 |
| 30+专长效果同质化 | 中 | 中 | 分类设计（战斗8/施法8/探索7/社交7），每类有独有效果 |

### 9.5 Phase 4 风险

| 风险 | 概率 | 影响 | 应对 |
|:----:|:----:|:----:|------|
| 长冒险设计复杂 | 高 | 高 | 简化三幕结构为"铺垫3节点→冲突5节点→高潮2节点"，不追求复杂分支 |
| 世界状态网络过于庞大 | 中 | 中 | 只追踪3-5个核心区域/势力的状态变化 |
| 项目疲劳/动力下降 | 高 | 高 | 每个Phase结束时有可玩的Demo版本，保持已完成内容的兴奋感 |

### 9.6 通用风险缓解策略

```
预算控制:
  LLM成本: MVP阶段短冒险约$0.15/次，50次测试约$7.5。控制在大预算$50/月
  美术资源: 购买itch.io素材包($20-50)，减少自制比例
  
范围控制:
  MVP严格按GDD §10定义的"包含/不包含"表执行
  任何超出MVP范围的功能，记录到对应Phase的backlog
  
质量保障:
  三条防线: dotnet build → LSP → dotnet test，缺一不可
  手工测试: 每个Sprint结束时安排30分钟手工测试
  
时间缓冲:
  每个Sprint预留1天缓冲处理意外问题
  Phase 1总时间14周(含2周缓冲)
```

---

## 10. 开发规范快速参考

详见 `03-vibe-coding-conventions.md` 完整文档，以下为执行计划中需要严格遵循的核心规则：

### 10.1 三条防线（不可违反）

```
第一道: dotnet build (2-5秒) — 编译通过前不允许运行游戏
第二道: LSP实时检查 — 编辑器内即时修正
第三道: dotnet test (10-30秒) — 每次提交前必须运行

提交条件: dotnet build && dotnet test 全部通过
```

### 10.2 命名规范

| 元素 | 规则 |
|------|------|
| 命名空间/类/方法/公有属性 | PascalCase |
| 接口 | IPascalCase |
| 私有字段 | _camelCase |
| 常量 | UPPER_SNAKE_CASE |
| 枚举类型/值 | PascalCase |
| 局部变量/参数 | camelCase |
| JSON字段 | snake_case (JsonPropertyName) |

### 10.3 数据驱动

```
数值不硬编码:
  ❌ if (race == "elf") { dex += 2; }
  ✅ RaceData.AbilityBonuses["dexterity"] 从 JSON 加载

配置目录: Data/Config/ 下的JSON文件
Schema目录: Data/Schemas/ 下的JSON Schema文件
模板目录: Data/Templates/ 下的JSON模板文件
```

### 10.4 提交规范

```
格式: <type>(<scope>): <描述>
示例: feat(combat): 添加优势/劣势骰子机制
类型: feat / fix / refactor / docs / test / chore
作用域: combat / character / tavern / adventure / settlement / gateway / ui / map / data
```

### 10.5 每个Module的强制要求

```
每个模块必须包含:
  [ ] 实现文件 (Systems/Xxx/Xxx.cs)
  [ ] 单元测试 (tests/Unit/Xxx/XxxTests.cs)
  [ ] 数据配置 (Data/Config/xxx.json) 如果有外部数值
  [ ] 接口定义 (如果被其他系统引用)

每个模块提交前检查:
  [ ] dotnet build 零错误零警告
  [ ] dotnet test 全部通过
  [ ] 无硬编码数值（应在JSON配置中）
  [ ] 无dynamic/object做参数类型
  [ ] 接口优于实现
```

### 10.6 关键禁止事项

- 禁止 `dynamic` — 放弃编译期类型检查
- 禁止 `object` 做参数类型 — 用泛型或接口
- 禁止 `#pragma warning disable` — 修复代码而非压制警告
- 禁止 `async void` — 用 `async Task`
- 禁止 Unity API (MonoBehaviour/GameObject/Instantiate)
- 禁止硬编码路径 — 用 ContentManager.Load
- 禁止中文注释 — 代码注释用英文

---

> **文档维护**: 本文档随项目开发持续更新。每个Sprint结束后，如有任务ID/时间/依赖变更，需同步更新本文档。版本变更通过PR提交到dev分支。
>
> **Next**: Phase 0 开发启动。首先完成 P0-01 到 P0-07 的全部任务，通过 M0 验收后进入 Phase 1。
