# ADR-0004：LLM 集成架构 — 皮肤层 + 事件结果分离模型

## Status
Accepted

## Date
2026-05-06

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | MonoGame 3.8.x stable（当前 3.8.4.1） |
| **Domain** | Core（跨系统架构原则，影响 LLM Gateway 及所有子系统） |
| **Knowledge Risk** | LOW — LLM 集成基于 .NET 标准库 `HttpClient` + `System.Text.Json`，无引擎级 API 依赖 |
| **References Consulted** | `design/gdd/GDD-v1.md`、`docs/technical/02-overall-architecture.md` §1.1-1.2、`design/gdd/02-llm-integration.md`（4213 行完整设计）、`design/gdd/06-adventure-generation.md` |
| **Post-Cutoff APIs Used** | None — `HttpClient` 和 `System.Text.Json` 均为 .NET 长期稳定 API |
| **Verification Required** | JSON Schema 验证覆盖率 100%；离线降级路径可达（无 API 仍可完整游玩） |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0000（MonoGame + .NET 标准库）、ADR-0001（ECS）、ADR-0002（ServiceLocator — LLMGateway 作为第 3 优先级服务）、ADR-0003（IEventBus — LLM 结果通过 NarrativeReady 事件发布） |
| **Enables** | 所有依赖 LLM 叙事的子系统（冒险生成、战斗叙述、NPC 对话、物品描述） |
| **Blocks** | LLMGateway 实现、4 个 Agent（编剧/DM/文案/平衡）、Schema 验证器、缓存管理器、离线降级器 |
| **Ordering Note** | LLMGateway 在 ServiceLocator 中为第 3 优先级——DataPersistence（缓存依赖）注册之后 |

## Context

### Problem Statement
《酒馆与命运》的核心特色是通过大语言模型（LLM）驱动动态叙事——每一场冒险都是独一无二的故事。但 LLM 是不可靠的：它可能生成格式错误、数值不合理、逻辑矛盾的内容。项目需要一个严格的架构边界，确保 LLM 的不可靠性被隔离在"皮肤层"，不能污染游戏的核心数值和规则判定。

核心挑战：
1. **LLM 不能决策数值** — 伤害、检定 DC、掉落、状态变化必须由程序计算
2. **LLM 输出必须可验证** — 所有输出通过 JSON Schema 验证，不合法则重试或降级
3. **游戏必须离线可玩** — LLM API 不可用时，核心游玩体验不能中断

### Constraints
- LLM API 调用有延迟（200ms-5s）和成本（token 计费）
- LLM 可能返回格式错误、超时、或包含不安全内容
- AI-First 开发范式：LLM Gateway 本身也由 AI 生成代码，必须保持架构简单可验证
- 玩家不应感知到 API 延迟——叙事生成应尽可能异步
- 预算有限，需支持模型切换（OpenAI → 本地 Ollama → 国产模型）和 token 预算控制

### Requirements
- 必须严格分离"程序决策"和"LLM 叙事"——LLM 永远不碰数值
- 必须对每个 Agent 输出进行 JSON Schema 验证
- 必须有离线降级路径（静态模板作为 LLM 不可用时的 fallback）
- 必须支持语义缓存（相同上下文复用已有叙事）
- 必须控制 token 预算（每次冒险有上限）
- 必须支持最多 3 次重试（Schema 验证失败时）
- 4 个 Agent：编剧（冒险蓝图）、DM（实时叙事）、文案（按需生成）、平衡（蓝图验证）

## Decision

**LLM = 皮肤层，程序 = 骨骼层。采用事件结果分离模型（Event Result Separation Model），4 Agent 架构，严格 Schema 验证 + 离线降级**。

### 核心原则

```
┌─────────────────────────────────────────────────────────────────┐
│                 事件结果分离模型 (Event Result Separation)          │
│                                                                 │
│  机械结果 (PROGRAM 控制):                                         │
│    · 关系值变化 (+/- N)         · 任务生成/解锁                    │
│    · 物品/金币奖励               · 战斗触发                        │
│    · buff/debuff 应用            · 设施状态变更                    │
│    · 伤害数值、检定 DC、掉落率     · 故事分支条件判定               │
│                                                                 │
│  叙事结果 (LLM 生成, Schema 约束):                                 │
│    · 场景描述 (DM Agent)         · NPC 对话 (DM Agent)            │
│    · 角色情感表达 (DM Agent)     · 氛围文本 (DM Agent)            │
│    · 物品描述 (文案 Agent)       · 冒险蓝图 (编剧 Agent)           │
│                                                                 │
│  执行流程:                                                        │
│    1. 程序决定机械结果                                              │
│    2. 机械结果 + 上下文 → LLM                                      │
│    3. LLM 生成符合结果的叙事文本                                     │
│    4. JSON Schema 验证 → 失败重试(最多3次) → 耗尽降级到模板          │
│    5. UI 展示叙事文本 + 机械结果通知                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 4 Agent 架构

```
┌─────────────────────────────────────────────────────────────────┐
│  LLM Gateway (全局单例, HttpClient + System.Text.Json)           │
│                                                                 │
│  ┌──────────────┐ ┌──────────────┐ ┌──────────────┐ ┌────────┐ │
│  │ 编剧 Agent    │ │ DM Agent     │ │ 文案 Agent    │ │平衡    │ │
│  │ (冒险前生成)  │ │ (实时叙事)    │ │ (按需生成)    │ │Agent   │ │
│  │              │ │              │ │              │ │(蓝图   │ │
│  │ · 冒险蓝图    │ │ · 场景氛围    │ │ · 物品描述    │ │ 验证)  │ │
│  │ · 角色叙事    │ │ · NPC对话     │ │ · 装备叙事    │ │        │ │
│  │ · 任务文本    │ │ · 战斗叙述    │ │ · 招募文本    │ │        │ │
│  │              │ │ · 检定描述    │ │              │ │        │ │
│  └──────┬───────┘ └──────┬───────┘ └──────┬───────┘ └───┬────┘ │
│         │               │               │              │      │
│         ▼               ▼               ▼              ▼      │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │              JSON Schema 验证层 (JsonSchema.Net)          │  │
│  │  · adventure_blueprint  · narrative_text                 │  │
│  │  · dialogue_options     · item_description               │  │
│  │  · balance_report       · character_narrative            │  │
│  │  · penalty_result                                        │  │
│  └──────────────────────────────────────────────────────────┘  │
│         │                                                      │
│         ▼                                                      │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  缓存管理层 (sqlite-net 语义缓存)                           │  │
│  │  离线降级层 (静态模板 fallback)                              │  │
│  └──────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

### LLM 输出验证管线

```
LLM 输出 JSON
  → JsonSchema.Net 验证 (格式检查)
  → Fail: 重试 (最多 3 次)
  → 业务逻辑验证 (值范围、跨字段约束)
  → 写入缓存
  → 返回给调用系统
  → 所有重试耗尽 → 降级到静态模板
```

### 调用优先级

| Agent | 优先级 | 阻塞模式 | 典型延迟 | 缓存策略 |
|-------|:---:|:---:|------|------|
| DM Agent | HIGH | 同步（对话）/ 异步（战斗叙述） | < 2s | 按 NPC + 场景缓存 |
| 编剧 Agent | MEDIUM | 异步（加载界面等待） | 3-10s | 语义 Hash 缓存 |
| 文案 Agent | LOW | 异步（后台生成） | 1-5s | 按物品/事件类型缓存 |
| 平衡 Agent | AUTO | 嵌入管线（编剧后自动触发） | 1-3s | 不缓存 |

### 关键接口

```csharp
public interface ILLMGateway
{
    // 调用指定 Agent 并返回 Schema 验证后的结果
    Task<TResponse> CallAgent<TAgent, TResponse>(AgentRequest request)
        where TAgent : ILLMAgent
        where TResponse : class;

    // 获取当前 token 预算状态
    TokenBudgetStatus GetBudgetStatus(string adventureId);

    // 事件 — LLM 结果就绪时通过 EventBus 发布
    // NarrativeReady, LLMFallbackTriggered, BudgetExceeded
}

public interface ILLMAgent
{
    string AgentId { get; }
    string SystemPrompt { get; }
    JsonSchema OutputSchema { get; }
    int MaxRetries { get; }  // 默认 3
}

public record AgentRequest(
    string AgentType,
    object Context,
    Dictionary<string, string> Parameters
);
```

### 离线降级策略

```
LLM API 不可用时的降级链:
  1. 语义缓存命中 → 返回缓存结果
  2. 静态模板匹配 → 按 Agent 类型选择预定义模板
  3. 通用 fallback → 返回安全的通用文本

保证: 游戏核心循环（战斗、探索、酒馆管理）在完全无 LLM 的情况下
       仍可游玩——叙事变回固定文本，但玩法不受影响。
```

## Alternatives Considered

### Alternative A：LLM 完全控制游戏
- **Description**：让 LLM 决定战斗结果、对话选择、故事走向——AI 成为"隐形的 DM"
- **Pros**：理论上最灵活、最沉浸——每个选择都可能有无限可能
- **Cons**：LLM 输出不可控——可能生成不平衡的战斗（1 级角色面对龙）、破坏游戏经济（无限金币）、遗忘之前的对话上下文；token 成本不可预测；离线完全不可玩
- **Rejection Reason**：放弃程序对游戏数值的控制 = 放弃游戏平衡和可测试性。LLM 当前的能力不足以保证"好玩"的游戏体验

### Alternative B：完全离线无 LLM
- **Description**：所有叙事使用预写模板，不使用任何 LLM API
- **Pros**：100% 可靠、零延迟、零成本、完全可控
- **Cons**：失去"每一场冒险都是独一无二的故事"的核心差异化特色；预写模板的叙事多样性有限
- **Rejection Reason**：LLM 驱动的动态叙事是项目的核心特色和差异化优势。但架构确保离线退化路径，两者不冲突

### Alternative C：混合模式 — LLM 建议 + 程序审批
- **Description**：LLM 可以提议数值变化（如"这个陷阱应该造成 2d6 伤害"），程序验证合理性后采纳或拒绝
- **Pros**：比"LLM 完全控制"安全，比"LLM 只有叙事"灵活
- **Cons**：边界模糊——什么情况 LLM 可以建议？什么情况必须程序决定？模糊边界导致 AI 生成的代码难以判断正确性
- **Rejection Reason**：模糊的边界是 AI-First 范式的敌人。严格的"程序控制数值 + LLM 生成叙事"二分法让 AI 生成的代码有清晰的正确性标准

## Consequences

### Positive
- 严格的程序/LLM 边界 — LLM 生成的代码只需关注叙事文本，程序的代码只需关注数值计算
- 完整的验证管线 — JSON Schema + 业务逻辑验证 + 3 次重试 + 模板降级
- 离线可玩 — LLM API 不可用时核心体验不受影响
- 模型无关 — 通过 Agent 抽象支持任意 LLM 后端（OpenAI / Ollama / 国产模型）
- 成本可控 — Token 预算管理防止单次冒险超额消费
- 语义缓存 — 相同上下文复用已有叙事，减少 API 调用和成本

### Negative
- LLM 的创意受限 — LLM 不能"惊喜"玩家（如意外掉落传说装备），所有数值由程序预设
- 开发复杂度增加 — 需要维护 Schema 定义、验证逻辑、缓存、降级模板
- API 延迟不可控 — DM Agent 的实时对话有 < 2s 的响应时间要求，依赖 API 响应速度

### Risks
| Risk | Severity | Mitigation |
|------|----------|------------|
| LLM API 不稳定/限流 | High | 离线降级模板 + 语义缓存；支持多模型切换 |
| LLM 输出格式变化（模型更新后） | Medium | Schema 验证捕获格式错误 → 重试 → 降级；定期更新 Schema 定义 |
| Token 成本超预算 | Medium | Token 预算管理器按冒险追踪；LOW 优先级 Agent 可在预算耗尽时跳过 |
| LLM "幻觉"生成不存在的 DND 规则 | Low | LLM 只生成叙事文本，不涉及规则判定——机械结果由程序计算 |
| Schema 定义不完整导致漏检 | Medium | 每个 Agent 上线前 Schema 覆盖率审计；异常叙事写入日志供人工审查 |

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| GDD-v1.md §1.2 | "每一场冒险都是独一无二的故事" | LLM 驱动的动态叙事 + 编剧 Agent 生成冒险蓝图 |
| GDD-v1.md §1.4 | 情感目标："这个故事是我的" | DM Agent 根据玩家选择生成个性化叙事 |
| 02-overall-architecture.md §1.1 | 设计哲学①②③④（LLM = 皮肤层、Schema 验证、程序不依赖 LLM、程序控制数值） | 事件结果分离模型 + 4 Agent + Schema 验证管线 |
| 02-overall-architecture.md §1.4 | LLMGateway 为第 3 优先级服务 | ServiceLocator 初始化顺序 |
| 02-llm-integration.md §2 | 三级优先级请求队列 + 4 Agent + RateLimiter | 本 ADR 采纳该设计并适配 C#/MonoGame |
| 06-adventure-generation.md §1 | 三层生成管线（蓝图 → 实例化 → 实时叙事） | 编剧 Agent（第一层）+ DM Agent（第三层） |

## Performance Implications
- **CPU**：JSON Schema 验证 < 5ms（JsonSchema.Net）；HTTP 请求异步不阻塞主线程
- **Memory**：语义缓存 SQLite 数据库 < 50MB（包含约 1000 条缓存条目）
- **Network**：单次 LLM API 调用的数据传输 < 10KB 请求 + < 5KB 响应
- **Latency**：DM Agent 对话响应目标 < 2s（P95）；编剧 Agent 蓝图生成 < 10s

## Migration Plan
N/A — 此 ADR 确立架构原则。LLM Gateway 尚未实现（Phase 0 仅完成了 Core 基础设施）。实现时需：

1. 创建 `src/DndGame/Gateway/LLMGateway.cs`（基于 HttpClient + System.Text.Json）
2. 实现 4 个 Agent（`Gateway/Agents/`）
3. 实现 Schema 验证器（`Gateway/Validation/`，JsonSchema.Net）
4. 实现缓存管理器（`Gateway/Cache/`，sqlite-net）
5. 实现离线降级器（`Gateway/Fallback/`，静态模板）
6. 在 `Data/Schemas/` 创建 7 个 JSON Schema 文件

## Validation Criteria
- JSON Schema 验证覆盖率 100%（7 个 Schema 全部定义）
- 离线降级路径可达：关闭 LLM API 后游戏仍可完整游玩
- Schema 验证失败 → 3 次重试 → 耗尽后降级到模板
- Token 预算耗尽后停止 LOW 优先级 Agent 调用
- `dotnet test` — LLM Gateway 有集成测试覆盖（含降级/fallback 测试）

## Related Decisions
- ADR-0000 — MonoGame + .NET（提供 HttpClient/System.Text.Json）
- ADR-0001 — ECS（LLM 叙事通过 Entity 组件展示）
- ADR-0002 — ServiceLocator（LLMGateway 作为第 3 优先级服务）
- ADR-0003 — IEventBus（NarrativeReady 事件）
- ADR-0005 — 数据驱动设计（Schema 文件、模板文件存储在 Data/ 目录）
- `design/gdd/02-llm-integration.md` — 完整 LLM 集成架构设计（4213 行）
- `design/gdd/06-adventure-generation.md` — 三层冒险生成管线
