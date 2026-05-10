# Epic: Core — 核心游戏数据层

> **Layer**: Core（依赖 Foundation）
> **GDD**: 多源 — 角色系统(`design/gdd/01-character-system.md`)、物品装备(`design/gdd/03-items-equipment.md`)、条件效果(`design/gdd/10-condition-effects-system.md`)、世界状态(`design/quick-specs/world-state-system-2026-05-10.md`)、LLM集成网关(`design/gdd/02-llm-integration.md`)
> **Architecture Module**: 游戏逻辑层 — CharacterSystem + ItemSystem + ConditionSystem + WorldStateManager + LLMGateway
> **Status**: Ready
> **Stories**: Not yet created — run `/create-stories core`

## Overview

Core Epic 覆盖《酒馆与命运》的 5 个核心游戏数据系统：角色系统、物品装备系统、条件效果系统、世界状态系统和 LLM 集成网关。这些系统构成了所有 Feature 层游戏玩法（战斗、冒险生成、酒馆、失败与成长、对话）的数据底座——角色数据模型被 11 个下游系统共享（ADR-0008 FROZEN 协议保护），物品/装备定义角色成长的核心反馈回路，条件效果提供 DND 5e 14 种条件的策略维度，世界状态实现 Pillar 3（持续演进的世界）的最小可行表达，LLM Gateway 提供核心差异化叙事体验的管道。

Core 层的所有核心逻辑为纯 C# 实现（角色属性计算、装备槽位管理、条件追踪、世界状态增量），可在无 GraphicsDevice 的环境中完全单元测试。LLM Gateway 使用 .NET 标准库 HttpClient + System.Text.Json，同样不依赖引擎 API。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0001: ECS 架构 | 角色/物品/条件 Entity 持有 Data 组件（CharacterData/EquipmentData/ConditionData） | LOW |
| ADR-0003: EventBus | 角色创建/死亡/升级事件、装备变更事件、条件应用/移除事件、LLM 叙事就绪事件 | LOW |
| ADR-0004: LLM 集成 | LLM = 皮肤层 + 事件结果分离模型 + 4 Agent（编剧/DM/文案/平衡）+ Schema 验证 + 离线降级 | LOW |
| ADR-0005: 数据驱动 | JSON 配置（种族/职业/装备/法术/怪物）+ SQLite 持久化（角色实例/世界状态），禁止硬编码 | LOW |
| ADR-0008: 角色数据冻结 | 核心字段 FROZEN（禁止删除/重命名/改类型），仅允许 Extension — 11 下游系统的共享契约 | LOW |

**Epic 最高 Engine Risk**: LOW — 全部 Core 系统核心逻辑为纯 C#，LLM Gateway 使用 .NET 标准库，无引擎级 API 依赖。

## GDD Requirements

### 系统 6: 角色系统 (Character System)

| TR-ID | Requirement | ADR Coverage | Design Doc |
|-------|-------------|--------------|------------|
| — | 六维属性模型（STR/DEX/CON/INT/WIS/CHA）+ 调整值公式 | ADR-0008 ✅ Frozen | `design/gdd/01-character-system.md` §2.1 |
| — | 角色数据双层（数值层 + 叙事层）——程序控制数值，LLM 生成叙事 | ADR-0004 ✅ + ADR-0008 ✅ | 同上 §2.1 |
| — | Standard Array + 种族加成 → 六维属性 | ADR-0005 ✅（JSON配置） | 同上 §2.2 |
| — | 职业进阶表（3 职业 MVP：战士/法师/盗贼） | ADR-0005 ✅ + ADR-0008 ✅ | 同上 §2.3 |
| — | 3 种族 × 3 亚种 MVP（人类/精灵/矮人） | ADR-0005 ✅ + ADR-0008 ✅ | 同上 §2.2 |
| — | HP/AC/速度/熟练加值/先攻/法术位衍生值 | ADR-0008 ✅ Frozen | 同上 §2.1.2 |
| — | 角色生成管线（程序化数值 → LLM 叙事 → 合并写入） | ADR-0004 ✅ | 同上 §2.4 |
| — | 关系值系统（双轴模型：信任/忠诚） | ADR-0008 ✅ Extension-Only | 同上 §2.5 |
| — | 伤疤/传承数据模型 | ADR-0008 ✅ Extension-Only | 同上 §2.5 |
| — | ICharacterSystem 接口（15+ 方法） | ADR-0001 ✅（ECS组件） | `docs/architecture/architecture.md` §2.3.2 |

> 角色系统是项目依赖面最广的模块（11 下游系统）。ADR-0008 确立了数据冻结协议，确保并行开发期间数据模型稳定。

### 系统 7: 物品装备系统 (Items & Equipment)

| TR-ID | Requirement | ADR Coverage | Design Doc |
|-------|-------------|--------------|------------|
| — | 装备槽位制（主手/副手/护甲/2饰品） | ADR-0005 ✅ + ADR-0008 ✅（Equipment 在 CharacterData 中 Frozen） | `design/gdd/03-items-equipment.md` §2 |
| — | 武器分类（简易/军用）+ 伤害骰表达式 + 属性要求 | ADR-0005 ✅（JSON配置） | 同上 §2.1 |
| — | 护甲分类（轻/中/重）+ AC 公式 + 敏捷上限/隐术劣势 | ADR-0005 ✅ | 同上 §2.2 |
| — | 饰品/消耗品/钥匙物品 | ADR-0005 ✅ | 同上 §2.3-2.5 |
| — | 附魔系统（81% 机制化：属性加值/伤害加值/条件触发） | ADR-0005 ✅ | 同上 §3 |
| — | 鉴定系统（未知 → 鉴定 → 已知，检定或消耗） | ADR-0005 ✅ | 同上 §4 |
| — | 物品定价从 JSON 配置读取（程序化计算，不硬编码） | ADR-0005 ✅ | 同上 §5 |
| — | 背包槽位制（MVP 无耐久度） | ADR-0008 ✅（Equipment 字段） | 同上 §6 |

> 物品装备系统的 GDD v1.2 MAJOR REVISION 完成了 11 项阻断修复（附魔机制化/定价解耦/耐久MVP移除/鉴定新系统/AC可测试化）。

### 系统 8: 条件效果系统 (Status Effects / Conditions)

| TR-ID | Requirement | ADR Coverage | Design Doc |
|-------|-------------|--------------|------------|
| — | 14 种 DND 5e 条件（目盲/魅惑/耳聋/恐慌/擒抱/失能/隐形/麻痹/石化/中毒/倒地/束缚/震慑/昏迷） | ADR-0006 ✅（CombatEngine 引用） | `design/gdd/10-condition-effects-system.md` §2 |
| — | 条件持续时间追踪（回合数/直到下次攻击/永久） | ADR-0006 ✅ | 同上 §3 |
| — | 条件堆叠/互斥规则（同类型不堆叠，部分互斥） | ADR-0006 ✅ | 同上 §4 |
| — | 条件对检定的影响（优势/劣势/自动失败/自动成功） | ADR-0006 ✅ + ADR-0003 ✅ | 同上 §2 |
| — | 条件应用/移除通过 EventBus 通知 | ADR-0003 ✅ | 同上 §7 |
| — | 13 种伤害类型（钝击/斩击/穿刺/酸蚀/寒冷/火焰/力场/闪电/黯蚀/毒素/心灵/光耀/雷鸣） | ADR-0006 ✅（enum 定义） | 同上 §5 |

> 条件效果系统 GDD v1.1 完成了 8 项阻断 + 6 项建议修复。ADR-0006 在战斗引擎架构中定义了 14 种条件和 13 种伤害类型的 C# enum 数据模型。

### 系统 9: 世界状态系统 (World State)

| TR-ID | Requirement | ADR Coverage | Design Doc |
|-------|-------------|--------------|------------|
| — | 区域状态追踪（5 种状态：safe/threatened/fallen/liberated/destroyed） | ADR-0005 ✅（数据驱动） | `design/quick-specs/world-state-system-2026-05-10.md` §2.1 |
| — | 势力关系增量（reputation [-100,100] + disposition 阈值自动判定） | ADR-0005 ✅ | 同上 §2.2, §4 |
| — | 世界事件日志（追加型，按时间倒序） | ADR-0005 ✅ | 同上 §2.3 |
| — | 冒险日志墙（名称/结局/存活角色/摘要） | ADR-0005 ✅ | 同上 §2.4 |
| — | NPC 状态追踪（6 种状态） | ADR-0005 ✅ | 同上 §2.5 |
| — | IWorldStateManager 接口（11 方法）+ WorldStateSnapshot DTO | ADR-0002 ✅（ServiceLocator #4）+ ADR-0003 ✅ | 同上 §3 |
| — | WorldStateChangedEvent 通过 EventBus 发布 | ADR-0003 ✅ | 同上 §3.3 |
| — | RestoreFromSnapshot 不触发事件（存档加载安全） | ADR-0003 ✅ | 同上 §7.2 |
| — | 14 个单元测试覆盖全部接口行为 | — | 同上 §8 |

> 世界状态系统 quick-spec Draft 状态（待审批）。数据模型以 `08-failure-growth.md` §9.1 为单一权威源。IWorldStateManager 在 ServiceLocator 中为第 4 优先级服务。

### 系统 10: LLM 集成网关 (LLM Gateway)

| TR-ID | Requirement | ADR Coverage | Design Doc |
|-------|-------------|--------------|------------|
| — | 事件结果分离模型（程序决策数值，LLM 只生成叙事） | ADR-0004 ✅ | `design/gdd/02-llm-integration.md` §1 |
| — | 4 Agent 架构（编剧/DM/文案/平衡） | ADR-0004 ✅ | 同上 §2 |
| — | JSON Schema 验证（7 种 Schema） | ADR-0004 ✅ | 同上 §3 |
| — | 最多 3 次重试（Schema 验证失败时） | ADR-0004 ✅ | 同上 §3 |
| — | 离线降级路径（静态模板 fallback） | ADR-0004 ✅ | 同上 §4 |
| — | 语义缓存（sqlite-net，键=语义哈希） | ADR-0005 ✅ + ADR-0004 ✅ | 同上 §5 |
| — | Token 预算控制（每次冒险上限） | ADR-0004 ✅ | 同上 §6 |
| — | ILLMGateway 接口（ServiceLocator #3） | ADR-0002 ✅ | `docs/architecture/architecture.md` §1.4 |
| — | NarrativeReady 事件通过 EventBus 发布 | ADR-0003 ✅ | 同上 |
| — | 模型切换支持（OpenAI/Ollama/国产模型） | ADR-0004 ✅ | 同上 §7 |

> LLM 集成网关 GDD v1.2（5757 行）是最长的子系统设计文档。ADR-0004 确立了 LLM = 皮肤层的核心原则，确保 LLM 不可靠性被隔离。

## Untraced Requirements Summary

`tr-registry.yaml` 当前为空——所有 Core 系统的验收标准由各自 Approved GDD 或 quick-spec 定义，但尚未正式分配 TR-ID。

**影响**: `/create-stories` 阶段将逐条为每个 AC 和 GDD 需求创建 TR-ID 注册条目。Stories 中的 TR-ID 将在创建时填充。

**关键风险**: 角色系统有 11 个下游依赖（ADR-0008）。在 `/create-stories core` 开始前，角色数据模型必须确认 FROZEN（ADR-0008 已 Accepted ✅）。任何角色数据的结构性变更都将导致级联返工。

## Dependencies

### 上游依赖（Core 依赖 Foundation）

| 系统 | 依赖内容 | Foundation 系统 | 状态 |
|------|----------|----------------|:----:|
| 角色系统 (#6) | 属性检定使用 DiceRoller | 骰子系统 (#2) | ✅ Quick Spec |
| 角色系统 (#6) | 角色事件通过 EventBus | 事件总线 (#1) | ✅ 已实现 |
| 物品装备 (#7) | 装备事件通过 EventBus | 事件总线 (#1) | ✅ 已实现 |
| 条件效果 (#8) | 条件事件通过 EventBus | 事件总线 (#1) | ✅ 已实现 |
| 世界状态 (#9) | WorldStateChangedEvent | 事件总线 (#1) | ✅ 已实现 |
| LLM 网关 (#10) | NarrativeReady Event | 事件总线 (#1) | ✅ 已实现 |
| LLM 网关 (#10) | 语义缓存存入 SQLite | ServiceLocator (#2 → DataPersistence) | ✅ 已实现 |
| 全部 Core 系统 | 通过 ServiceLocator 访问服务 | ServiceLocator (#2) | ✅ 已实现 |

### 下游依赖（谁依赖 Core）

| 系统 | 依赖内容 | Core 系统 | 状态 |
|------|----------|----------|:----:|
| 战斗系统 (#13, Feature) | HP/AC/属性/法术位 + 装备 + 条件 + 骰子 + AI | 角色+物品+条件+骰子 | ✅ ADR |
| 冒险生成 (#14, Feature) | LLM 蓝图生成 + 世界状态上下文 + 地图实例化 | LLM网关+世界状态 | ✅ ADR |
| 酒馆系统 (#16, Feature) | 招募/关系/装备管理/任务板 | 角色+物品+世界状态 | ✅ GDD |
| 失败与成长 (#17, Feature) | 伤疤/传承/世界状态写入 + LLM 叙事 | 角色+世界状态+LLM | ✅ GDD |
| 对话系统 (#15, Feature) | LLM 对话生成 + 角色关系上下文 | LLM+角色 | ✅ Quick Spec |
| 敌人AI (#11, Feature) | 目标优先级基于角色属性和条件 | 角色+条件 | ✅ GDD |
| 存档系统 (#18, Feature) | 角色数据/世界状态序列化 | 角色+世界状态 | ✅ Quick Spec |

## Definition of Done

此 Epic 完成条件：
- 所有 5 个 Core 系统的 Stories 实现完成，经 `/story-done` 关闭
- 角色系统：GDD §2.1-2.5 全部验收标准通过 + 11 下游系统接口无回归
- 物品装备系统：GDD §2-6 全部验收标准通过（含附魔机制化/定价解耦）
- 条件效果系统：14 种条件 + 13 种伤害类型 + 堆叠/互斥规则测试通过
- 世界状态系统：14 个 quick-spec 测试用例通过 + IWorldStateManager 接口完整
- LLM 网关：4 Agent 管线 + Schema 验证 + 离线降级路径 + 语义缓存全部可用
- `dotnet build` 零错误零警告（TreatWarningsAsErrors=true）
- `dotnet test` 全绿，无回归
- 角色数据模型遵守 ADR-0008 FROZEN 协议（无删除/重命名/类型变更）
- 所有游戏数值从 JSON/SQLite 读取（ADR-0005，无硬编码）
- LLM 输出经 JSON Schema 验证（ADR-0004）
- LLM 不决策任何数值/规则结果（ADR-0004 皮肤层约束）
- 无 Nez API 引用（ADR-0001）
- 无 Unity API 引用

## Next Step

运行 `/create-stories core` 将此 Epic 拆分为可实现的 Stories。

**⚠️ 关键建议**: Core Epic 应按依赖顺序创建 Stories：角色系统 → 物品装备 → 条件效果 → 世界状态 → LLM 网关。角色系统是最长（L 规模）且依赖面最广的模块，应优先完成并通过 ADR-0008 FROZEN 验证后再展开下游系统。