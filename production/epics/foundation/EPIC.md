# Epic: Foundation — 基础设施层

> **Layer**: Foundation
> **GDD**: 多源 — 事件总线(ADR-0003)、骰子系统(`design/quick-specs/dice-system-2026-05-09.md`)、场景管理(`design/quick-specs/scene-management-2026-05-09.md`)、设置选项(`design/quick-specs/settings-options-2026-05-09.md`)、音频系统(`design/quick-specs/audio-system-2026-05-09.md`)
> **Architecture Module**: Core 基础设施 — GameRoot + Scene/Entity/Component + ServiceLocator + IEventBus + DiceRoller + AudioManager
> **Status**: Ready
> **Stories**: Not yet created — run `/create-stories foundation`

## Overview

Foundation Epic 覆盖《酒馆与命运》的 5 个零依赖基础设施系统：事件总线（已实现）、骰子系统、场景管理、设置选项和音频系统。这些系统构成了所有 Core 和 Feature 层系统的依赖底座——EventBus 是跨系统通信的唯一通道，DiceRoller 是所有 DND 5e 检定的计算核心，Scene/Entity/Component 是游戏对象的组织骨架，Settings 提供玩家控制权，Audio 是 Pillar 2 的感官锚点。

Foundation 层的所有系统均为纯 C# 实现（或已实现），不依赖 MonoGame 渲染 API，可在无 GraphicsDevice 的环境中完全单元测试。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0000: MonoGame 引擎选型 | MonoGame 3.8.5+ / C# 12 / .NET 8 — 编译期类型安全优先，零跨域混淆 | LOW |
| ADR-0001: ECS 架构 | 自定义轻量 Scene/Entity/Component/SceneComponent，不引入 Nez | LOW |
| ADR-0002: ServiceLocator | 静态 ServiceLocator，7 服务按序注册，FinalizeRegistration() 冻结 | LOW |
| ADR-0003: EventBus | IEventBus + Snapshot-then-Invoke，类型安全，线程安全，防死锁 | LOW |

**Epic 最高 Engine Risk**: LOW — 全部 Foundation 系统为纯 C# 或已实现，无引擎级 API 依赖。

## GDD Requirements

### 系统 1: 事件总线 (Event Bus)

| TR-ID | Requirement | ADR Coverage | Design Doc |
|-------|-------------|--------------|------------|
| — | 已实现（Phase 0 Core/EventBus.cs, 136 行） | ADR-0003 ✅ | `docs/architecture/adr-0003-event-bus.md` |
| — | IEventBus.Publish<T> / Subscribe<T> / Unsubscribe<T> | ADR-0003 ✅ | 同上 |
| — | Snapshot-then-Invoke 防死锁模式 | ADR-0003 ✅ | 同上 |
| — | 8 个单元测试全绿 | ADR-0003 ✅ | 同上 |

> ⚠️ TR-ID 尚未正式注册。`tr-registry.yaml` 当前为空。事件总线已实现并通过测试，验收标准由 ADR-0003 覆盖。

### 系统 2: 骰子系统 (Dice System)

| TR-ID | Requirement | ADR Coverage | Design Doc |
|-------|-------------|--------------|------------|
| — | RollDie(sides) 基础掷骰 | ❌ 无专用 ADR | `design/quick-specs/dice-system-2026-05-09.md` AC-DICE.01 |
| — | RollD20Check 攻击/豁免/属性检定 | ❌ 无专用 ADR | 同上 AC-DICE.02-07 |
| — | 优势/劣势（掷 2d20 取高/低） | ❌ 无专用 ADR | 同上 AC-DICE.03-04 |
| — | 自然 20 暴击 / 自然 1 大失败（仅 Attack 类型） | ❌ 无专用 ADR | 同上 AC-DICE.05-07 |
| — | RollInitiative（每轮重骰，无优劣势） | ❌ 无专用 ADR | 同上 AC-DICE.11 |
| — | RollDamage 伤害骰 + 暴击取最大值 | ❌ 无专用 ADR | 同上 AC-DICE.12-14 |
| — | ParseDiceExpression 骰子表达式解析 | ❌ 无专用 ADR | 同上 AC-DICE.15-19 |
| — | 无状态纯函数——相同输入相同结构结果 | ❌ 无专用 ADR | 同上 AC-DICE.20 |

> ⚠️ 骰子系统无专用 ADR。ADR-0006（战斗引擎架构）提及 DiceRoller 作为 CombatEngine 的子模块，但 ADR-0006 属于 Feature 层。骰子系统作为 Foundation 层独立系统，其架构地位由 ADR-0000（纯 C# 无引擎依赖）间接覆盖。Stories 中将引用 quick-spec 的 AC 标记作为验收标准。

### 系统 3: 场景管理 (Scene Management)

| TR-ID | Requirement | ADR Coverage | Design Doc |
|-------|-------------|--------------|------------|
| — | 4 个 MVP 场景（MainMenu/Tavern/Adventure/Combat） | ADR-0001 ✅ | `design/quick-specs/scene-management-2026-05-09.md` AC-SM.01 |
| — | GameRoot 场景切换队列（帧边界安全） | ADR-0001 ✅ | 同上 |
| — | 场景切换携带上下文数据（队伍/任务配置） | ADR-0001 ✅ | 同上 AC-SM.02-03 |
| — | 完整往返不崩溃不丢失数据 | ADR-0001 ✅ | 同上 AC-SM.07 |

> ⚠️ Phase 0 已实现 Scene/Entity/Component/GameRoot 框架（415 行，13 测试全绿）。场景管理的验收标准由 ADR-0001（ECS 架构）+ quick-spec AC 标记覆盖。

### 系统 4: 设置选项 (Settings/Options)

| TR-ID | Requirement | ADR Coverage | Design Doc |
|-------|-------------|--------------|------------|
| — | 三组音量滑块（主音量/音效/音乐） | ❌ 无专用 ADR | `design/quick-specs/settings-options-2026-05-09.md` AC-SET.02 |
| — | 键位映射表显示（只读） | ❌ 无专用 ADR | 同上 AC-SET.03 |
| — | 设置持久保存 | ADR-0005 ✅（数据驱动） | 同上 AC-SET.04 |
| — | 主菜单和酒馆均可访问 | ❌ 无专用 ADR | 同上 AC-SET.01 |

> 设置选项为独立 Meta 系统，无游戏系统依赖。其数据持久化由 ADR-0005（JSON/SQLite）覆盖。Stories 中将引用 quick-spec 的 AC 标记。

### 系统 5: 音频系统 (Audio System)

| TR-ID | Requirement | ADR Coverage | Design Doc |
|-------|-------------|--------------|------------|
| — | 骰子音效（滚动/落地/Nat20/Nat1） | ❌ 无专用 ADR | `design/quick-specs/audio-system-2026-05-09.md` AC-AUD.01-03 |
| — | 战斗音效（命中/未命中/暴击/施法/受伤/敌人死亡） | ❌ 无专用 ADR | 同上 AC-AUD.04 |
| — | 音量控制（主音量×音效音量） | ADR-0002 ✅（ServiceLocator 注册） | 同上 AC-AUD.05 |
| — | 多音效并发不截断 | ❌ 无专用 ADR | 同上 AC-AUD.06 |

> AudioManager 在 ServiceLocator 中注册为第 5 优先级服务（ADR-0002）。音频系统的验收标准由 ADR-0002 + quick-spec AC 标记覆盖。

## Untraced Requirements Summary

`tr-registry.yaml` 当前为空——所有 Foundation 系统的验收标准由各自 quick-spec 的 AC 标记和 Accepted ADR 覆盖，但尚未正式分配 TR-ID。

**影响**: `/create-stories` 阶段将逐条为每个 AC 创建 TR-ID 注册条目。Stories 中的 TR-ID 将在创建时填充，而非在此 Epic 中预分配。

## Dependencies

### 上游依赖（Foundation 依赖谁）

| 系统 | 依赖内容 | 状态 |
|------|----------|:----:|
| — | Foundation 层无游戏系统依赖 | — |

### 下游依赖（谁依赖 Foundation）

| 系统 | 依赖内容 | 状态 |
|------|----------|:----:|
| 角色系统 (#6, Core) | EventBus + DiceRoller | ✅ ADR |
| 物品装备 (#7, Core) | EventBus | ✅ ADR |
| 条件效果 (#8, Core) | EventBus | ✅ ADR |
| 世界状态 (#9, Core) | EventBus | ✅ Quick Spec |
| LLM 网关 (#10, Core) | EventBus | ✅ ADR |
| 地图探索 (#12, Feature) | DiceRoller + SceneManagement | ✅ ADR |
| 战斗系统 (#13, Feature) | DiceRoller + SceneManagement + EventBus | ✅ ADR |
| 全部 Feature/Presentation 层 | EventBus + SceneManagement | ✅ ADR |

## Definition of Done

此 Epic 完成条件：
- 所有 5 个 Foundation 系统的 Stories 实现完成，经 `/story-done` 关闭
- 事件总线：已实现 ✅（8 个测试全绿）——只需确认与新系统的集成无回归
- 骰子系统：20 条 AC 全部通过 xUnit 测试验证
- 场景管理：8 条 AC 全部通过（含完整往返循环测试）
- 设置选项：4 条 AC 全部通过（含持久化验证）
- 音频系统：6 条 AC 全部通过（含并发音效测试）
- `dotnet build` 零错误零警告（TreatWarningsAsErrors=true）
- `dotnet test` 全绿，无回归
- 无硬编码游戏数值（ADR-0005）
- 无 Nez API 引用（ADR-0001）
- 无 Unity API 引用

## Next Step

运行 `/create-stories foundation` 将此 Epic 拆分为可实现的 Stories。