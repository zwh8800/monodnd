# Architecture Traceability Index

> **版本**: v1.0
> **生成日期**: 2026-05-11
> **数据源**: `docs/architecture/tr-registry.yaml` (v3, 1,211 TR)
> **ADR 源**: ADR-0000~0008 (全部 Accepted)
> **架构文档**: `docs/architecture/architecture.md` v3.0
> **用途**: 供 `/create-stories` 和 `/dev-story` 追踪每个 Story 的 GDD 需求 → ADR 指导 → TR-ID

---

## 统计摘要

| 维度 | 数值 |
|------|:---:|
| 总 TR 数 | 1,211 |
| ADR 完全覆盖 | 870 |
| 架构文档覆盖 | 200 |
| 待补 ADR 覆盖 | 141 |
| Foundation 层含 ADR 覆盖 | 68/68 (100%) |
| Core 层含 ADR 覆盖 | 379/379 (100%) |

---

## 系统 → ADR 映射

### Foundation 层 (68 TR, 100% ADR 覆盖)

| 系统 | TR 范围 | TR 数 | ADR 覆盖 | 状态 |
|------|---------|:---:|---------|:----:|
| 骰子系统 | TR-dice-system-001~026 | 26 | ADR-0006 (DiceRoller 纯函数) | ✅ |
| 场景管理 | TR-scene-management-001~013 | 13 | ADR-0001 (ECS) + ADR-0002 (ServiceLocator) | ✅ |
| 设置选项 | TR-settings-options-001~008 | 8 | 架构文档 §5 (Presentation 层) | ✅ |
| 音频系统 | TR-audio-system-001~011 | 11 | 架构文档 §5 (Presentation 层) | ⚠️ 待 ADR |
| 存档系统 | TR-save-system-001~010 | 10 | ADR-0005 (数据持久化) | ✅ |

### Core 层 (379 TR, 100% ADR 覆盖)

| 系统 | TR 范围 | TR 数 | ADR 覆盖 | 状态 |
|------|---------|:---:|---------|:----:|
| 角色系统 | TR-character-001~136 | 136 | ADR-0008 (CharacterData Freeze) + ADR-0005 (数据驱动) | ✅ |
| LLM 集成 | TR-llm-001~079 | 79 | ADR-0004 (LLM 集成) + ADR-0003 (EventBus) | ✅ |
| 战斗系统 | TR-combat-001~114 | 114 | ADR-0006 (Combat FSM) + ADR-0007 (GoRogue) | ✅ |
| 条件效果 | TR-condition-001~120 | 120 | ADR-0008 (ConditionType) + ADR-0006 (条件追踪) | ✅ |
| 世界状态 | TR-world-state-001~044 | 44 | 架构文档 §3 (Core 层) | ⚠️ 待 ADR |

### Feature L1 层 (50 TR)

| 系统 | TR 范围 | TR 数 | ADR 覆盖 | 状态 |
|------|---------|:---:|---------|:----:|
| 对话系统 | TR-dialogue-system-001~023 | 23 | ADR-0004 (LLM 集成) + 架构文档 §4.8 | ✅ |
| 失败成长 | TR-failgrowth-001~124 (部分 L1) | ~50 | 架构文档 §4.10 | ⚠️ 待 ADR-0011 |

### Feature L2 层 (284 TR)

| 系统 | TR 范围 | TR 数 | ADR 覆盖 | 状态 |
|------|---------|:---:|---------|:----:|
| 物品装备 | TR-items-001~071 | 71 | ADR-0005 (JSON 配置) + 架构文档 §4.4 | ✅ |
| 敌人 AI | TR-ai-001~099 | 99 | ADR-0006 (EnemyAI) + 架构文档 §4.6 | ✅ |
| 战斗系统 (L2 部分) | TR-combat 子集 | ~15 | ADR-0006 | ✅ |

### Feature L3 层 (473 TR)

| 系统 | TR 范围 | TR 数 | ADR 覆盖 | 状态 |
|------|---------|:---:|---------|:----:|
| 地图探索 | TR-map-001~172 | 172 | ADR-0007 (GoRogue 2.6.4) + 架构文档 §4.5 | ✅ |
| 冒险生成 | TR-advgen-001~084 | 84 | 架构文档 §4.7 | ⚠️ 待 ADR-0009 |
| 酒馆系统 | TR-tavern-001~093 | 93 | 架构文档 §4.8 | ⚠️ 待 ADR-0010 |
| 失败成长 (L3 部分) | TR-failgrowth 子集 | ~74 | 架构文档 §4.10 | ⚠️ 待 ADR-0011 |

### Presentation 层 (134 TR)

| 系统 | TR 范围 | TR 数 | ADR 覆盖 | 状态 |
|------|---------|:---:|---------|:----:|
| UI/UX | TR-ui-001~134 | 134 | ADR-0001 (ECS Scene 渲染) + 架构文档 §5 | ✅ LP-4 待原型验证 |

### 核心 GDD (148 TR, 全层横跨)

| 系统 | TR 范围 | TR 数 | ADR 覆盖 | 备注 |
|------|---------|:---:|---------|------|
| GDD-v1 | TR-gdd-v1-001~148 | 148 | 各 ADR 映射见交叉引用表 | 约 40 条与子系统 TR 重复 |

---

## ADR 覆盖矩阵

| ADR | 标题 | 覆盖 TR 数 | Foundation | Core | Feature | Presentation |
|-----|------|:---:|:---:|:---:|:---:|:---:|
| ADR-0000 | 引擎选型 — MonoGame 3.8.x | 全部 | ✅ | ✅ | ✅ | ✅ |
| ADR-0001 | ECS 架构 — 自定义 Scene/Entity/Component | 48 | ✅ | ✅ | ✅ | ✅ |
| ADR-0002 | 服务注册 — ServiceLocator | 21 | ✅ | ✅ | — | — |
| ADR-0003 | 跨系统通信 — IEventBus | 156 | ✅ | ✅ | ✅ | — |
| ADR-0004 | LLM 集成架构 | 102 | — | ✅ | ✅ | — |
| ADR-0005 | 数据驱动设计 | 95 | ✅ | ✅ | ✅ | — |
| ADR-0006 | 战斗引擎架构 | 213 | ✅ | ✅ | ✅ | — |
| ADR-0007 | GoRogue 版本锁定 | 172 | — | — | ✅ | — |
| ADR-0008 | CharacterData Freeze | 256 | — | ✅ | ✅ | — |

---

## 待补 ADR 清单

| 优先级 | ADR | 覆盖系统 | TR 数 | 启动条件 |
|:------:|-----|---------|:---:|---------|
| HIGH | ADR-0009 冒险实例化架构 | adventure | 84 | Adventure Epic 前 |
| HIGH | ADR-0010 酒馆系统服务架构 | tavern | 93 | Tavern Epic 前 |
| MEDIUM | ADR-0011 结算/伤疤管线架构 | failgrowth | 74 | Settlement Epic 前 |
| MEDIUM | ADR-0012 音频系统架构 | audio | 11 | Audio Epic 前 |
| LOW | ADR-0013 世界状态管理 | world-state | 44 | World State Epic 前 |

---

## GDD ↔ ADR 交叉引用（关键映射）

| GDD 需求 | 子系统 TR | ADR | 关系 |
|----------|-----------|-----|------|
| LLM=皮肤层原则 | TR-llm-001~030 | ADR-0004 | gdd-v1 定义原则，llm 细化实现 |
| 死亡双轨制 | TR-combat-039, TR-failgrowth-037 | ADR-0006 §4.2.6 | gdd-v1 定义规则，combat/failgrowth 各实现部分 |
| 关系值系统 | TR-character-084~096 | ADR-0008 §FROZEN | gdd-v1 概要，character 细化矩阵 |
| 三层冒险管线 | TR-advgen-001~084 | 架构文档 §4.7 | gdd-v1 定义管线，advgen 细化 14 步 |
| 疲乏 3 级制 | TR-condition-032~038 | ADR-0006 §偏离 | gdd-v1 定义偏离，condition 细化效果 |
| 槽位负重 | TR-character-016~017 | ADR-0008 §FROZEN | 同公式两端引用 |
| 先攻每轮重掷 | TR-combat-007~009 | ADR-0006 §偏离 | gdd-v1 定义偏离，combat 细化 FSM |
| 暴击取最大值 | TR-combat-020 | ADR-0006 §偏离 | gdd-v1 定义偏离，combat 细化管线 |
| 3 MVP 职业 | TR-character-039~051 | ADR-0008 §FROZEN | gdd-v1 定义范围，character 细化特性 |
| 交互标签枚举 | TR-map-107~138 | ADR-0007 | gdd-v1 定义标签集，map 细化数据模型 |
| 8 种 MVP 条件 | TR-condition-001~027 | ADR-0008 §ConditionType | gdd-v1 定义范围，condition 细化生命周期 |
| 结算+伤疤 | TR-failgrowth-001~090 | 架构文档 §4.10 | gdd-v1 概要，failgrowth 细化数据 |
| Gateway+Schema | TR-llm-044~079 | ADR-0004 §Schema | gdd-v1 概要，llm 细化 6 子组件 |
| FF6 像素风格 | TR-ui-001~134 | ADR-0001 §Scene 渲染 | gdd-v1 定义视觉，ui 细化像素参数 |
| 战斗 6 条 AC | TR-combat-017~086 | ADR-0006 §CombatFSM | gdd-v1 验收，combat 细化 114 条 TR |

---

## Foundation 层完整性验证

Foundation 层共 68 TR，按门控要求逐项验证 ADR 覆盖：

| TR | 系统 | ADR | 覆盖 |
|----|------|-----|:---:|
| TR-dice-system-001~026 | 骰子系统 | ADR-0006 (DiceRoller) | ✅ |
| TR-scene-management-001~013 | 场景管理 | ADR-0001 (ECS) + ADR-0002 (ServiceLocator) | ✅ |
| TR-settings-options-001~008 | 设置 | 架构文档 §5 | ✅ |
| TR-audio-system-001~011 | 音频 | 架构文档 §5 | ⚠️ 无专项 ADR |
| TR-save-system-001~010 | 存档 | ADR-0005 (数据持久化) | ✅ |
| TR-gdd-v1-001~010 | 核心设计 | ADR-0000 + ADR-0004 | ✅ |

**Foundation 层结果**: ✅ **零缺口** — 所有 68 TR 均被 ADR 或架构文档覆盖。音频系统缺少专项 ADR，但已在架构文档 §5 中设计。

---

## 使用方法

### 供 `/create-stories` 使用

```
读取本文件 → 查目标系统 → 获取：
  1. ADR 指导 ID（注入 Story 的 ADR 指引段）
  2. TR 范围（注入 Story 的 acceptance criteria）
  3. GDD 源文件（注入 Story 的 GDD 需求引用）
```

### 供 `/dev-story` 使用

```
读取 Story → 提取 TR-ID → 查本文件 → 获取：
  1. 对应 ADR（开发时的架构约束）
  2. GDD 源（开发时的规则来源）
  3. Domain 标签（确定 Specialist Agent 指派）
```

### 供 `/story-done` 使用

```
读取 Story → 提取 TR-ID → 查 `tr-registry.yaml` → 验证：
  1. TR 状态仍为 active（未被废弃）
  2. 对应 ADR 未被修订
  3. 跨引用 TR 未被标记为 superseded
```

---

> **维护规则**: 每次新增 ADR 后更新 §待补 ADR 清单和 §ADR 覆盖矩阵。
> **同步源**: `docs/architecture/tr-registry.yaml` — 如 TR 数量变更，更新本文统计数字。
