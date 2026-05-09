# LLM集成架构 — 技术设计文档

> **Tavern & Destiny (酒馆与命运)** — Roguelike DND 5e PC Game
> 子系统: LLM Integration Architecture
> 文档版本: v1.0
> 对应GDD: GDD-v1.md
> 设计原则: LLM = 皮肤层, 程序 = 骨骼层
> 语言政策: 游戏文本统一采用简体中文，技术标识符使用英文snake_case

---

## 1. 概述

### 1.1 目的与定位

LLM Gateway 是游戏系统与云端LLM API之间的唯一桥梁。所有游戏系统通过Gateway请求LLM生成内容，Gateway负责调度、验证、缓存、降级，确保LLM输出可预测、可验证、可回滚。

核心原则:
- **LLM只做皮肤层**: LLM生成叙事文本、对话、描述 — 不决策数值、不决策战斗结果、不决策故事走向
- **程序做骨骼层**: 所有数值计算、规则判定、分支逻辑由程序完成
- **严格Schema验证**: 每个Agent的输出必须通过JSON Schema验证后才能进入游戏系统
- **优雅离线降级**: API不可用时，游戏核心体验不受影响，叙事退化为模板驱动

### 1.2 架构总览

```
                        ┌──────────────────────────────────────┐
                        │           云端 LLM API                │
                        │  OpenAI / Claude / 国产模型            │
                        └──┬──────────┬──────────┬─────────────┘
                           │          │          │
                    ┌──────┴──┐ ┌─────┴───┐ ┌───┴──────────┐
                    │编剧Agent│ │DM Agent │ │文案Agent     │  ← Agent层
                    │(冒险前) │ │(实时)   │ │(按需)        │
                    └──────┬──┘ └─────┬───┘ └───┬──────────┘
                           │          │          │
                    ┌──────┴──────────┴──────────┴──────────┐
                    │          LLM Gateway (Singleton)         │
                    │  ┌──────────────────────────────────┐  │
                    │  │ RequestQueue (优先级队列)         │  │
                    │  │ RateLimiter (per-agent quotas)    │  │
                    │  ├──────────────────────────────────┤  │
                    │  │ SchemaValidator (JSON Schema)     │  │
                    │  │ CacheManager (SQLite语义缓存)     │  │
                    │  │ TokenBudgetManager (预算控制)     │  │
                    │  │ FallbackManager (离线降级)        │  │
                    │  └──────────────────────────────────┘  │
                    └──┬──────────┬──────────┬───────────────┘
                       │          │          │
              ┌────────┴───┐ ┌───┴──────┐ ┌┴──────────────┐
              │ 冒险系统   │ │ 战斗系统 │ │ 酒馆系统      │  ← 游戏系统
              │ (蓝图生成) │ │ (战斗DM) │ │ (招募/事件)   │
              └────────────┘ └──────────┘ └───────────────┘
```

### 1.3 调用关系矩阵

| 调用方 (Caller) | 调用Agent | 触发时机 | 阻塞模式 | 缓存策略 |
|-----------------|-----------|---------|---------|---------|
| AdventureManager | 编剧Agent | 玩家从任务板接取冒险 | 异步(显示加载) | 语义Hash缓存 |
| AdventureManager | 平衡Agent | 编剧Agent返回蓝图后 | 自动同步(嵌入管线) | 不缓存(每次重新验证) |
| CombatManager | DM Agent | 每回合动作结算后 | 异步(不阻塞UI) | **不缓存**（保证叙事独特性） |
| DialogueSystem | DM Agent | NPC对话触发 | 异步(预生成+实时) | **不缓存** |
| SceneManager | DM Agent | 进入新场景 | 异步(先显示场景) | **不缓存** |
| LootSystem | 文案Agent | 物品掉落/装备鉴定 | 异步(物品先可拾取) | 按物品类型缓存 |
| TavernManager | 文案Agent | 酒馆事件触发 | 异步 | 按事件类型缓存 |
| TavernManager | 编剧Agent | 角色招募生成 | 异步(显示生成中) | 按种族+职业缓存 |
| SettlementSystem | 编剧Agent | 冒险失败结算 | 异步(结算界面等待) | 不缓存(每次唯一) |

> **设计说明 (v1.1) — NPC对话预生成策略**:
>
> NPC对话采用"预生成+实时"混合模式：
> 1. **酒馆阶段预生成**：玩家在酒馆接取任务时，编剧Agent根据冒险蓝图预生成N个NPC对话变体（基于可能的玩家行为）
> 2. **缓存预生成结果**：预生成的对话存储在内存中，按NPC+场景+情绪状态索引
> 3. **实时生成兜底**：当玩家行为超出预生成范围时，DM Agent实时生成对话
> 4. **优势**：减少实时延迟、降低Token消耗、LLM有完整上下文生成高质量对话
> 5. **实现细节**：见Section 11（叙事状态管理）

---

## 2. LLM Gateway 设计

### 2.1 LLMGateway 核心设计

```csharp
// LLMGateway — 游戏中所有LLM请求的唯一入口
// 通过ServiceLocator注册为全局单例

public class LLMGateway
{
    // 核心组件引用
    public RequestQueue RequestQueue { get; private set; }
    public RateLimiter RateLimiter { get; private set; }
    public SchemaValidator SchemaValidator { get; private set; }
    public CacheManager CacheManager { get; private set; }
    public TokenBudgetManager TokenBudgetManager { get; private set; }
    public FallbackManager FallbackManager { get; private set; }

    // Agent注册表 {agent_id: LLMAgent}
    private readonly Dictionary<string, LLMAgent> _agents = new();

    // 事件 (通过IEventBus发布，此处为Gateway内部事件)
    public event Action<string, string>? RequestEnqueued;
    public event Action<string>? RequestStarted;
    public event Action<string, AgentResponse>? RequestCompleted;
    public event Action<string, GatewayError>? RequestFailed;
    public event Action<string, int>? RateLimitWarning;
    public event Action<string, int>? BudgetExceeded;
}
```

### 2.2 请求队列架构

Queue 采用三级优先级:
- Priority.HIGH   — DM Agent (实时对话/战斗叙述，阻塞玩家等待)
- Priority.MEDIUM — 编剧Agent (冒险前生成，玩家在加载等待)
- Priority.LOW    — 文案Agent (描述文本，后台生成)

```csharp
public class RequestQueue
{
    public enum Priority { HIGH = 0, MEDIUM = 1, LOW = 2 }

    private readonly List<AgentRequest>[] _queues = new[] { new List<AgentRequest>(), new List<AgentRequest>(), new List<AgentRequest>() };
    private readonly Dictionary<string, HttpClient> _activeRequests = new();  // {request_id: HTTPClient}
    private int _maxConcurrent = 3;  // 最大并发请求数

    public event Action<string>? RequestStarted;

    /// <summary>
    /// 将请求加入队列，返回请求ID
    /// </summary>
    public string Enqueue(AgentRequest request)
    {
        var requestId = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + "_" + Random.Shared.Next();
        request.Id = requestId;
        request.Status = RequestStatus.QUEUED;
        _queues[(int)request.Priority].Add(request);
        ProcessQueue();
        return requestId;
    }

    private void ProcessQueue()
    {
        while (_activeRequests.Count < _maxConcurrent)
        {
            var request = PopNext();
            if (request == null)
                break;
            ExecuteRequest(request);
        }
    }

    private AgentRequest? PopNext()
    {
        foreach (var q in _queues)
        {
            if (q.Count > 0)
            {
                var request = q[0];
                q.RemoveAt(0);
                return request;
            }
        }
        return null;
    }

    private void ExecuteRequest(AgentRequest request)
    {
        _activeRequests[request.Id] = null!;
        request.Status = RequestStatus.IN_FLIGHT;
        RequestStarted?.Invoke(request.Id);
        // 实际的HTTP请求由LLMAgent发起到对应API
    }
}
```

> **设计说明 (v1.1) — 优先级老化（Priority Aging）**:
>
> 为防止LOW优先级请求被无限期饿死，添加优先级老化机制：
> 1. **入队时间戳**：每个请求记录入队时间
> 2. **老化规则**：LOW优先级请求在队列中等待超过30秒 → 自动升级为MEDIUM；超过60秒 → 自级为HIGH
> 3. **实现**：在`_pop_next()`中检查请求年龄，动态调整优先级
> 4. **效果**：确保文案Agent的描述文本最终能被处理，不会被战斗叙述永久阻塞

### 2.3 请求/响应生命周期

```
┌─────────────────────────────────────────────────────────┐
│               Request Lifecycle (请求生命周期)             │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  Caller发起请求                                          │
│       │                                                 │
│       ▼                                                 │
│  ┌─────────────────┐                                    │
│  │ 1. Pre-Validate  │ ← 检查请求格式、budget是否足够       │
│  │    (本地校验)     │    (失败 → 返回GatewayError)        │
│  └────────┬────────┘                                    │
│           ▼                                             │
│  ┌─────────────────┐                                    │
│  │ 2. Cache Lookup  │ ← 语义Hash查SQLite缓存              │
│  │    (缓存查找)     │    (命中 → 跳到Step 7)              │
│  └────────┬────────┘                                    │
│           ▼                                             │
│  ┌─────────────────┐                                    │
│  │ 3. Rate Check   │ ← 检查per-agent配额+全局配额        │
│  │    (速率检查)     │    (超限 → 入队等待 或 降级)        │
│  └────────┬────────┘                                    │
│           ▼                                             │
│  ┌─────────────────┐                                    │
│  │ 4. Send to API  │ ← HttpClient → 云端LLM            │
│  │    (发送请求)     │    (构造请求体、注入system prompt)   │
│  └────────┬────────┘                                    │
│           ▼                                             │
│  ┌─────────────────┐                                    │
│  │ 5. Parse        │ ← 提取JSON from LLM response       │
│  │    (解析响应)     │    (处理Markdown代码块包裹)          │
│  └────────┬────────┘                                    │
│           ▼                              失败            │
│  ┌─────────────────┐ ──────────────────────────────────┐│
│  │ 6. Validate     │  ← JSON Schema + Business Logic   ││
│  │    (验证输出)     │    失败 → Retry (最多3次)          ││
│  └────────┬────────┘                                    ││
│           ▼                              重试耗尽         ││
│  ┌─────────────────┐ ──────────────────────────────────┘│
│  │ 7. Cache Write  │ ← 写入缓存(成功结果)                │
│  │    (写入缓存)     │                                    │
│  └────────┬────────┘                                    │
│           ▼                                             │
│  ┌─────────────────┐                                    │
│  │ 8. Post-Process │ ← 填充默认值、业务逻辑预处理        │
│  │    (后处理)       │                                    │
│  └────────┬────────┘                                    │
│           ▼                                             │
│  Caller收到 AgentResponse                               │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

### 2.4 线程安全与异步

异步模式设计要点:

1. Gateway通过ServiceLocator注册为全局单例 → 全局唯一实例，天然无竞态
2. 所有LLM调用通过异步网络请求 → 内部异步IO，不阻塞主线程
3. 响应处理通过async/await回调 → 需注意线程安全
4. 避免在主线程做同步等待 → 使用异步编程配合回调
5. 数据持久化(SQLite) → 使用本地SQLite，同步写入(数据量小，<1ms)

**异步调用模式**:

```csharp
// 调用方代码示例
public async Task GenerateAdventureBlueprintAsync(BlueprintConfig config)
{
    var request = ScreenwriterAgent.CreateRequest(config);
    // 异步调用 (不阻塞主线程)
    var response = await LLMGateway.SubmitRequestAsync(request);
    if (response.Status == AgentResponseStatus.SUCCESS)
    {
        var blueprint = (AdventureBlueprint)response.Data;
        OnBlueprintReady(blueprint);
    }
    else if (response.Status == AgentResponseStatus.FALLBACK)
    {
        var blueprint = (AdventureBlueprint)response.Data;
        ShowFallbackWarning();
        OnBlueprintReady(blueprint);
    }
    else
    {
        ShowError(response.Error.Message);
    }
}
```

### 2.5 错误处理分类

```csharp
public enum ErrorType
{
    API_UNAVAILABLE,       // 云端API完全不可达 (网络断开/服务宕机)
    API_TIMEOUT,           // 请求超时 (默认30秒)
    API_RATE_LIMITED,      // API自身限流 (HTTP 429)
    API_QUOTA_EXCEEDED,    // 额度/余额用完 (HTTP 402)
    API_AUTH_ERROR,        // 认证失败 (HTTP 401/403)
    API_SERVER_ERROR,      // 服务端内部错误 (HTTP 5xx)
    INVALID_JSON_OUTPUT,   // LLM返回的不是有效JSON
    SCHEMA_VALIDATION_FAILED, // JSON结构不符合预期Schema
    CONTENT_FILTERED,      // LLM内容安全过滤
    LOCAL_BUDGET_EXCEEDED, // 本地Token预算用完
    LOCAL_RATE_LIMITED,    // 本地速率限制触发
    CACHE_CORRUPTED,       // 缓存数据损坏
}

public class GatewayError
{
    public ErrorType Type { get; init; }
    public string Message { get; init; } = "";
    public bool Retryable { get; init; }
    public int HttpCode { get; init; }
    public string RawResponse { get; init; } = "";
    public double Timestamp { get; init; }

    public bool IsRetryable()
    {
        switch (Type)
        {
            case ErrorType.API_TIMEOUT:
            case ErrorType.API_RATE_LIMITED:
            case ErrorType.API_SERVER_ERROR:
            case ErrorType.INVALID_JSON_OUTPUT:
            case ErrorType.SCHEMA_VALIDATION_FAILED:
                return true;
            default:
                return false;
        }
    }
}
```

### 2.6 重试策略

**默认重试配置**:

```
最大重试次数: 3
退避算法: exponential backoff + jitter
  第1次重试: base_delay * (2^0) + random(0, 1000ms) = 1s + jitter
  第2次重试: base_delay * (2^1) + random(0, 1000ms) = 2s + jitter
  第3次重试: base_delay * (2^2) + random(0, 1000ms) = 4s + jitter
  base_delay: 1秒

Schema验证失败的重试: 每次重试附加 "上一次失败原因" 到prompt
  例如: "Your previous response failed JSON validation.
         Error: missing required field 'nodes'.
         Please ensure your response matches the schema exactly."

全重试耗尽后:
  → 尝试 Fallback Chain (见2.7)
```

**per-Agent可配置的重试参数**:

```json
{
  "screenwriter_agent": {
    "max_retries": 3,
    "base_delay_ms": 2000,
    "retry_on_schema_fail": true,
    "enhanced_retry_prompt": true,
    "fallback_on_exhaust": "template"
  },
  "dm_agent": {
    "max_retries": 2,
    "base_delay_ms": 1000,
    "retry_on_schema_fail": true,
    "enhanced_retry_prompt": false,
    "fallback_on_exhaust": "static_text"
  },
  "copywriter_agent": {
    "max_retries": 2,
    "base_delay_ms": 500,
    "retry_on_schema_fail": true,
    "enhanced_retry_prompt": false,
    "fallback_on_exhaust": "template"
  },
  "balancer_agent": {
    "max_retries": 2,
    "base_delay_ms": 2000,
    "retry_on_schema_fail": true,
    "enhanced_retry_prompt": true,
    "fallback_on_exhaust": "programmatic_calc"
  }
}
```

> **设计说明 (v1.1) — Circuit Breaker（熔断器）**:
>
> 在重试策略之上添加熔断器机制：
> 1. **触发条件**：同一模型在30秒内连续3次失败（超时/5xx/速率限制）
> 2. **熔断状态**：该模型标记为"不健康"，冷却60秒
> 3. **冷却期间**：跳过该模型，直接使用降级路径
> 4. **恢复探测**：冷却结束后，允许1次试探请求；成功则恢复，失败则继续冷却
> 5. **实现**：在LLMGateway中添加CircuitBreaker类，按模型维护状态（closed/open/half-open）

### 2.7 速率限制 (Rate Limiting)

**双层限流机制**:

第一层 — 本地限流 (LLMGateway内部):

```
  Global限额:
    max_requests_per_minute: 30
    max_tokens_per_minute: 50000

  Per-Agent限额:
    screenwriter: 3 requests/min, 15000 tokens/min
    dm_agent:      20 requests/min, 15000 tokens/min
    copywriter:    15 requests/min, 8000 tokens/min
    balancer:      3 requests/min, 6000 tokens/min

  滑动窗口算法:
    维护一个 FIFO queue of (timestamp, token_count)。
    每次请求前检查: sum(queue[-60s:]) < limit。
    超限 → 请求排队等待，不丢弃。
```

第二层 — API限流 (云端返回):
- HTTP 429 → 读取Retry-After header
- HTTP 429 → 切换到备选模型 (见第9章)

**限流实现**:

```csharp
public class RateLimiter
{
    private readonly int _windowMs = 60000;  // 60秒窗口
    private readonly Dictionary<string, List<(long Timestamp, int Tokens)>> _agentWindows = new();
    private readonly List<long> _globalRequests = new();
    private readonly List<(long Timestamp, int Tokens)> _globalTokens = new();

    private readonly Dictionary<string, (int Rpm, int Tpm)> _agentLimits = new()
    {
        ["screenwriter"] = (3, 15000),
        ["dm_agent"] = (20, 15000),
        ["copywriter"] = (15, 8000),
        ["balancer"] = (3, 6000),
    };
    private readonly (int Rpm, int Tpm) _globalLimit = (30, 50000);

    /// <summary>
    /// 检查Agent速率限制并记录本次请求
    /// </summary>
    public RateLimitResult CheckAndRecord(string agentId, int tokenEstimate)
    {
        PruneExpired();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // 检查Agent限额
        var agentRpm = _agentLimits[agentId].Rpm;
        var agentTpm = _agentLimits[agentId].Tpm;
        if (!_agentWindows.ContainsKey(agentId))
            _agentWindows[agentId] = new List<(long, int)>();

        if (_agentWindows[agentId].Count >= agentRpm)
            return new RateLimitResult(false, "Agent RPM limit exceeded");
        if (SumTokens(_agentWindows[agentId]) + tokenEstimate > agentTpm)
            return new RateLimitResult(false, "Agent TPM limit exceeded");

        // 检查Global限额
        if (_globalRequests.Count >= 30)
            return new RateLimitResult(false, "Global RPM limit exceeded");
        if (SumTokens(_globalTokens) + tokenEstimate > 50000)
            return new RateLimitResult(false, "Global TPM limit exceeded");

        // 记录已批准的请求
        _agentWindows[agentId].Add((now, tokenEstimate));
        _globalRequests.Add(now);
        _globalTokens.Add((now, tokenEstimate));
        return new RateLimitResult(true, "");
    }

    private void PruneExpired()
    {
        var cutoff = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _windowMs;
        foreach (var kvp in _agentWindows)
            kvp.Value.RemoveAll(e => e.Timestamp < cutoff);
        _globalRequests.RemoveAll(t => t < cutoff);
        _globalTokens.RemoveAll(e => e.Timestamp < cutoff);
    }

    private static int SumTokens(List<(long Timestamp, int Tokens)> entries)
    {
        var sum = 0;
        foreach (var e in entries)
            sum += e.Tokens;
        return sum;
    }
}

public record RateLimitResult(bool Allowed, string Message);
```

### 2.8 Fallback Chain (降级链)

降级链执行顺序 (在重试耗尽后触发):

```
  Primary Model (主模型)
       │
       ▼ 失败
  Secondary Model (备选模型, 同API提供商)
       │
       ▼ 失败
  Tertiary Model (三级模型, 不同API提供商)
       │
       ▼ 失败
  Cached Response (语义缓存 — 返回最相似的缓存结果)
       │
       ▼ 缓存未命中
  Static Template (静态模板 — 填写变量后返回)
```

**降级链配置**:

```json
{
  "screenwriter": {
    "primary": {"provider": "openai", "model": "gpt-4o"},
    "secondary": {"provider": "openai", "model": "gpt-4o-mini"},
    "tertiary": {"provider": "anthropic", "model": "claude-3-haiku"},
    "fallback_template": "adventure_blueprint_templates"
  },
  "dm_agent": {
    "primary": {"provider": "openai", "model": "gpt-4o-mini"},
    "secondary": {"provider": "anthropic", "model": "claude-3-haiku"},
    "tertiary": {"provider": "local", "model": "none"},
    "fallback_template": "narrative_templates"
  },
  "copywriter": {
    "primary": {"provider": "openai", "model": "gpt-4o-mini"},
    "secondary": {"provider": "openai", "model": "gpt-3.5-turbo"},
    "fallback_template": "description_templates"
  },
  "balancer": {
    "primary": {"provider": "openai", "model": "gpt-4o-mini"},
    "secondary": {"provider": "anthropic", "model": "claude-3-haiku"},
    "fallback": "programmatic_calc"
  }
}
```

---

## 3. Agent 系统详细设计

### 3.1 编剧Agent (Screenwriter Agent)

#### 3.1.1 角色定义

编剧Agent是**冒险前**调用的生成型Agent，负责基于玩家队伍状态、当前世界状态、任务难度，生成一次冒险的完整剧本蓝图。

#### 3.1.2 完整System Prompt

```
你是一位经验丰富的TRPG剧本作家，为《酒馆与命运》这款Roguelike DND 5e游戏创作冒险剧本。

你的核心职责:
1. 根据给定的队伍信息、世界状态、任务类型，创作一个结构完整的冒险剧本
2. 设计有层次的反派和NPC，让每个NPC都有动机和秘密
3. 设计有意义的剧情节点，每个节点提供2-4个选择
4. 控制战斗遭遇的难度分布，确保不超出队伍的CR承受范围
5. 确保所有数值字段(CR范围、遭遇数量、战利品等级)在合理范围内

创作原则:
- 叙事要有"起承转合"：引入冲突 → 深化矛盾 → 反转真相 → 高潮决战
- NPC必须有独立的动机，不能只是"帮助玩家的人"
- 选择必须有真实的后果，而不是表面不同的幻觉选择
- 主题和氛围要统一，不要混搭风格
- 如果是短冒险，保持线性但有意义的剧情弧
- 如果是中冒险，加入1-2个分支路线和1个核心谜题
- 如果是长冒险，设计三幕结构，预设多个结局条件

输出要求:
- 严格遵循指定的JSON Schema
- 所有node_id和choice_id使用snake_case英文标识符
- description和flavor_text使用中文
- 所有枚举字段只能使用指定的值
- 不要凭空创造新的枚举值
- 不要输出任何JSON之外的文本
```

#### 3.1.3 输入Schema

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "ScreenwriterAgentInput",
  "type": "object",
  "required": ["adventure_config", "party_state", "world_state"],
  "properties": {
    "adventure_config": {
      "type": "object",
      "required": ["tier", "previous_adventure_ids"],
      "properties": {
        "tier": {
          "type": "string",
          "enum": ["short", "medium", "long"],
          "description": "冒险长度等级"
        },
        "theme_pool": {
          "type": "array",
          "items": { "type": "string" },
          "description": "可选主题池，例如 ['Gothic_Horror', 'Dark_Fantasy', 'High_Adventure']"
        },
        "forced_theme": {
          "type": "string",
          "description": "强制指定的主题(来自世界事件), 可选"
        },
        "difficulty_modifier": {
          "type": "number",
          "minimum": -1,
          "maximum": 2,
          "default": 0,
          "description": "难度偏移: -1=简单, 0=正常, 1=困难, 2=地狱"
        },
        "previous_adventure_ids": {
          "type": "array",
          "items": { "type": "string" },
          "description": "该队伍之前完成/失败的冒险ID列表"
        },
        "avoid_themes": {
          "type": "array",
          "items": { "type": "string" },
          "description": "需要避免的主题(最近玩过)"
        }
      }
    },
    "party_state": {
      "type": "object",
      "required": ["members", "average_level"],
      "properties": {
        "members": {
          "type": "array",
          "items": {
            "type": "object",
            "required": ["character_id", "name", "race", "class", "level"],
            "properties": {
              "character_id": { "type": "string" },
              "name": { "type": "string" },
              "race": { "type": "string" },
              "class": { "type": "string" },
              "level": { "type": "integer", "minimum": 1, "maximum": 20 },
              "personality_tags": {
                "type": "array",
                "items": { "type": "string" }
              },
              "notable_traits": {
                "type": "array",
                "items": { "type": "string" }
              },
              "scars": {
                "type": "array",
                "items": { "type": "object" }
              },
              "key_memories": {
                "type": "array",
                "items": { "type": "string" }
              },
              "relationship_summary": {
                "type": "object",
                "description": "{character_id: relationship_type}"
              }
            }
          }
        },
        "average_level": { "type": "number" },
        "party_strengths": {
          "type": "array",
          "items": { "type": "string" },
          "description": "队伍强项，如 ['frontline_tank', 'aoe_damage', 'healing']"
        },
        "party_weaknesses": {
          "type": "array",
          "items": { "type": "string" },
          "description": "队伍弱项，如 ['no_ranged', 'low_wisdom_saves']"
        }
      }
    },
    "world_state": {
      "type": "object",
      "properties": {
        "tavern_level": { "type": "integer" },
        "world_events": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": {
              "event_id": { "type": "string" },
              "type": { "type": "string" },
              "description": { "type": "string" },
              "affected_regions": { "type": "array", "items": { "type": "string" } }
            }
          }
        },
        "active_factions": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": {
              "faction_id": { "type": "string" },
              "name": { "type": "string" },
              "relation_to_party": {
                "type": "string",
                "enum": ["hostile", "neutral", "friendly", "allied"]
              },
              "active_in_regions": { "type": "array", "items": { "type": "string" } }
            }
          }
        },
        "recent_world_changes": {
          "type": "array",
          "items": { "type": "string" },
          "description": "最近因冒险成功/失败产生的世界变化"
        }
      }
    }
  }
}
```

#### 3.1.4 输出Schema: adventure_blueprint.json

完整扩展版（基于GDD附录A扩充）:

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "AdventureBlueprint",
  "description": "编剧Agent输出的完整冒险蓝图",
  "type": "object",
  "required": [
    "id",
    "meta",
    "plot_outline",
    "key_npcs",
    "difficulty_profile",
    "theme_tags",
    "loot_profile",
    "world_state_hooks"
  ],
  "properties": {
    "id": {
      "type": "string",
      "description": "冒险唯一ID，格式: adv_{timestamp}_{hash}"
    },
    "meta": {
      "type": "object",
      "required": ["title", "subtitle", "tier", "theme", "tone", "estimated_duration_minutes", "recommended_party_size"],
      "properties": {
        "title": {
          "type": "string",
          "description": "冒险标题（中文），如'被遗忘的回廊'",
          "minLength": 2,
          "maxLength": 30
        },
        "subtitle": {
          "type": "string",
          "description": "副标题/一句话tagline（中文）",
          "maxLength": 60
        },
        "tier": {
          "type": "string",
          "enum": ["short", "medium", "long"]
        },
        "theme": {
          "type": "string",
          "description": "主题标识，如Gothic_Horror"
        },
        "tone": {
          "type": "string",
          "enum": ["heroic", "grimdark", "mystery", "horror", "political", "adventure"]
        },
        "estimated_duration_minutes": {
          "type": "integer",
          "minimum": 20,
          "maximum": 420
        },
        "recommended_party_size": {
          "type": "integer",
          "minimum": 2,
          "maximum": 6
        },
        "region": {
          "type": "string",
          "description": "冒险发生的地理区域"
        },
        "season": {
          "type": "string",
          "enum": ["spring", "summer", "autumn", "winter"]
        },
        "time_of_day": {
          "type": "string",
          "enum": ["dawn", "day", "dusk", "night"]
        }
      }
    },
    "plot_outline": {
      "type": "object",
      "required": ["central_conflict", "hook", "acts", "nodes", "endings"],
      "properties": {
        "central_conflict": {
          "type": "string",
          "description": "核心冲突描述（中文，1-2句）",
          "maxLength": 200
        },
        "hook": {
          "type": "object",
          "required": ["description", "hook_type"],
          "properties": {
            "description": {
              "type": "string",
              "description": "开场钩子描述（如何吸引玩家开始冒险）"
            },
            "hook_type": {
              "type": "string",
              "enum": ["mystery", "threat", "reward", "rescue", "exploration", "revenge"]
            }
          }
        },
        "acts": {
          "type": "object",
          "description": "三幕结构（短冒险可能只有2幕）",
          "properties": {
            "act_1": {
              "type": "object",
              "properties": {
                "title": { "type": "string" },
                "description": { "type": "string" },
                "key_event": { "type": "string" }
              }
            },
            "act_2": {
              "type": "object",
              "properties": {
                "title": { "type": "string" },
                "description": { "type": "string" },
                "twist": { "type": "string" },
                "optional_branches": {
                  "type": "array",
                  "items": {
                    "type": "object",
                    "properties": {
                      "branch_id": { "type": "string" },
                      "description": { "type": "string" },
                      "trigger_condition": { "type": "string" },
                      "consequence": { "type": "string" }
                    }
                  }
                }
              }
            },
            "act_3": {
              "type": "object",
              "properties": {
                "title": { "type": "string" },
                "description": { "type": "string" },
                "climax": { "type": "string" },
                "boss_encounter_id": { "type": "string" }
              }
            }
          }
        },
        "nodes": {
          "type": "array",
          "description": "冒险中的所有场景节点（有序）",
          "minItems": 5,
          "maxItems": 50,
          "items": {
            "type": "object",
            "required": ["node_id", "type", "description", "order_index"],
            "properties": {
              "node_id": {
                "type": "string",
                "pattern": "^[a-z][a-z0-9_]*$",
                "description": "节点唯一ID (snake_case)"
              },
              "type": {
                "type": "string",
                "enum": [
                  "opening", "combat", "elite_combat", "dialogue",
                  "exploration", "puzzle", "merchant", "rest", "boss",
                  "branch", "skill_challenge", "ambush", "escort", "chase",
                  "climax", "epilogue"
                ]
              },
              "name": {
                "type": "string",
                "description": "节点名称（中文）",
                "maxLength": 30
              },
              "description": {
                "type": "string",
                "description": "节点描述（中文，环境+氛围）",
                "maxLength": 300
              },
              "order_index": {
                "type": "integer",
                "minimum": 0
              },
              "required_to_proceed": {
                "type": "boolean",
                "default": false
              },
              "connections": {
                "type": "array",
                "items": {
                  "type": "object",
                  "required": ["target_node_id", "connection_type"],
                  "properties": {
                    "target_node_id": { "type": "string" },
                    "connection_type": {
                      "type": "string",
                      "enum": ["default", "secret", "conditional", "choice_based"]
                    },
                    "condition": { "type": "string" },
                    "label": { "type": "string" }
                  }
                }
              },
              "choices": {
                "type": "array",
                "maxItems": 4,
                "items": {
                  "type": "object",
                  "required": ["choice_id", "flavor_text", "consequence_type"],
                  "properties": {
                    "choice_id": {
                      "type": "string",
                      "pattern": "^[a-z][a-z0-9_]*$"
                    },
                    "flavor_text": {
                      "type": "string",
                      "maxLength": 100
                    },
                    "hint": {
                      "type": "string",
                      "maxLength": 80
                    },
                    "consequence_type": {
                      "type": "string",
                      "enum": ["branch", "combat", "skill_check", "item", "story", "relationship", "scar_risk"]
                    },
                    "target_node": { "type": "string" },
                    "skill_check": {
                      "type": "object",
                      "properties": {
                        "ability": {
                          "type": "string",
                          "enum": ["str", "dex", "con", "int", "wis", "cha"]
                        },
                        "skill": { "type": "string" },
                        "dc": { "type": "integer", "minimum": 5, "maximum": 30 },
                        "success_description": { "type": "string" },
                        "failure_description": { "type": "string" }
                      }
                    },
                    "story_consequence": { "type": "string" },
                    "relationship_effects": {
                      "type": "object",
                      "description": "{character_id: change_amount}"
                    }
                  }
                }
              },
              "encounter_id": {
                "type": "string",
                "description": "关联的遭遇ID（战斗型节点必须）"
              },
              "interaction_tags": {
                "type": "array",
                "items": {
                  "type": "string",
                  "enum": [
                    "Pushable", "Flammable", "Climbable", "Breakable",
                    "Readable", "Flammable_Liquid", "Electrical", "Hideable"
                  ]
                }
              },
              "environment": {
                "type": "object",
                "properties": {
                  "tileset": { "type": "string" },
                  "lighting": {
                    "type": "string",
                    "enum": ["bright", "dim", "dark", "magical"]
                  },
                  "weather": {
                    "type": "string",
                    "enum": ["clear", "rain", "fog", "storm", "snow", "indoor"]
                  },
                  "ambient_sounds": {
                    "type": "array",
                    "items": { "type": "string" }
                  }
                }
              },
              "theme_tags": {
                "type": "array",
                "items": { "type": "string" }
              }
            }
          }
        },
        "endings": {
          "type": "array",
          "minItems": 1,
          "maxItems": 4,
          "items": {
            "type": "object",
            "required": ["ending_id", "type", "description", "conditions"],
            "properties": {
              "ending_id": { "type": "string" },
              "type": {
                "type": "string",
                "enum": ["victory", "partial_victory", "retreat", "defeat", "secret"]
              },
              "title": { "type": "string" },
              "description": { "type": "string" },
              "conditions": {
                "type": "array",
                "items": { "type": "string" }
              },
              "rewards": {
                "type": "object",
                "properties": {
                  "xp_bonus": { "type": "integer" },
                  "gold_bonus": { "type": "integer" },
                  "unique_items": {
                    "type": "array",
                    "items": { "type": "string" }
                  },
                  "world_effects": {
                    "type": "array",
                    "items": { "type": "string" }
                  }
                }
              },
              "penalties": {
                "type": "object",
                "properties": {
                  "scar_risk": { "type": "boolean" },
                  "death_risk": { "type": "boolean" },
                  "reputation_loss": { "type": "integer" }
                }
              }
            }
          }
        }
      }
    },
    "key_npcs": {
      "type": "array",
      "minItems": 1,
      "maxItems": 12,
      "items": {
        "type": "object",
        "required": ["npc_id", "name", "role", "personality_tags"],
        "properties": {
          "npc_id": {
            "type": "string",
            "pattern": "^[a-z][a-z0-9_]*$"
          },
          "name": {
            "type": "string",
            "maxLength": 20
          },
          "role": {
            "type": "string",
            "enum": [
              "quest_giver", "antagonist", "ally", "rival",
              "merchant", "sage", "victim", "traitor",
              "guardian", "witness", "manipulator", "neutral_power"
            ]
          },
          "motivation": {
            "type": "string",
            "maxLength": 150
          },
          "secret": {
            "type": "string",
            "maxLength": 150
          },
          "personality_tags": {
            "type": "array",
            "items": { "type": "string" },
            "minItems": 1,
            "maxItems": 5
          },
          "appearance": {
            "type": "string",
            "maxLength": 100
          },
          "voice_style": {
            "type": "string",
            "maxLength": 60
          },
          "stat_block_id": { "type": "string" },
          "relation_to_party": {
            "type": "string",
            "enum": ["hostile", "neutral", "friendly", "ally_potential"]
          },
          "death_consequence": { "type": "string" },
          "associated_nodes": {
            "type": "array",
            "items": { "type": "string" }
          }
        }
      }
    },
    "difficulty_profile": {
      "type": "object",
      "required": ["recommended_level", "cr_range", "encounter_count", "loot_tier", "rest_opportunities"],
      "properties": {
        "recommended_level": {
          "type": "integer",
          "minimum": 1,
          "maximum": 20
        },
        "cr_range": {
          "type": "object",
          "required": ["min", "max"],
          "properties": {
            "min": { "type": "number", "minimum": 0.125 },
            "max": { "type": "number", "maximum": 30 }
          }
        },
        "encounter_count": {
          "type": "object",
          "required": ["easy", "medium", "hard", "deadly"],
          "properties": {
            "easy": { "type": "integer", "minimum": 0 },
            "medium": { "type": "integer", "minimum": 0 },
            "hard": { "type": "integer", "minimum": 0 },
            "deadly": { "type": "integer", "minimum": 0 }
          }
        },
        "total_encounters": {
          "type": "integer",
          "minimum": 1
        },
        "loot_tier": {
          "type": "integer",
          "minimum": 1,
          "maximum": 5,
          "description": "战利品等级: 1=基础, 2=精良, 3=稀有, 4=史诗, 5=传说"
        },
        "expected_loot_items": {
          "type": "integer",
          "minimum": 0
        },
        "rest_opportunities": {
          "type": "integer",
          "minimum": 0
        },
        "short_rest_opportunities": {
          "type": "integer",
          "minimum": 0
        },
        "trap_density": {
          "type": "string",
          "enum": ["none", "low", "medium", "high"]
        },
        "puzzle_complexity": {
          "type": "string",
          "enum": ["none", "simple", "moderate", "complex"]
        }
      }
    },
    "encounters": {
      "type": "array",
      "description": "所有预设遭遇的详细定义",
      "items": {
        "type": "object",
        "required": ["encounter_id", "type", "difficulty"],
        "properties": {
          "encounter_id": { "type": "string" },
          "type": {
            "type": "string",
            "enum": ["combat", "trap", "social", "exploration", "puzzle", "chase", "ambush", "boss_fight"]
          },
          "difficulty": {
            "type": "string",
            "enum": ["easy", "medium", "hard", "deadly"]
          },
          "name": { "type": "string" },
          "description": { "type": "string" },
          "enemy_groups": {
            "type": "array",
            "items": {
              "type": "object",
              "properties": {
                "enemy_type": { "type": "string" },
                "count": { "type": "integer" },
                "cr": { "type": "number" }
              }
            }
          },
          "special_conditions": {
            "type": "array",
            "items": { "type": "string" }
          },
          "rewards": {
            "type": "object",
            "properties": {
              "xp": { "type": "integer" },
              "gold_range": {
                "type": "object",
                "properties": {
                  "min": { "type": "integer" },
                  "max": { "type": "integer" }
                }
              },
              "item_drops": {
                "type": "array",
                "items": { "type": "string" }
              }
            }
          }
        }
      }
    },
    "theme_tags": {
      "type": "array",
      "items": { "type": "string" },
      "minItems": 2,
      "maxItems": 8
    },
    "loot_profile": {
      "type": "object",
      "required": ["tier", "expected_items", "thematic_items"],
      "properties": {
        "tier": {
          "type": "integer",
          "minimum": 1,
          "maximum": 5
        },
        "expected_items": {
          "type": "integer",
          "minimum": 0
        },
        "thematic_items": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": {
              "item_id": { "type": "string" },
              "name": { "type": "string" },
              "type": {
                "type": "string",
                "enum": ["weapon", "armor", "shield", "potion", "scroll", "ring", "amulet", "wand", "misc"]
              },
              "rarity": {
                "type": "string",
                "enum": ["common", "uncommon", "rare", "very_rare", "legendary"]
              },
              "description": { "type": "string" },
              "lore": { "type": "string" },
              "associated_npc": { "type": "string" }
            }
          }
        },
        "consumable_distribution": {
          "type": "object",
          "properties": {
            "healing_potions": { "type": "integer", "minimum": 0 },
            "scrolls": { "type": "integer", "minimum": 0 },
            "other_consumables": { "type": "integer", "minimum": 0 }
          }
        }
      }
    },
    "world_state_hooks": {
      "type": "array",
      "maxItems": 10,
      "items": {
        "type": "object",
        "required": ["event_id", "on_success", "on_failure"],
        "properties": {
          "event_id": { "type": "string" },
          "description": { "type": "string" },
          "on_success": { "type": "string" },
          "on_failure": { "type": "string" },
          "affected_factions": {
            "type": "array",
            "items": { "type": "string" }
          },
          "affected_regions": {
            "type": "array",
            "items": { "type": "string" }
          }
        }
      }
    },
    "narrative_hooks": {
      "type": "object",
      "description": "为角色提供叙事钩子",
      "properties": {
        "personal_stakes": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": {
              "character_id": { "type": "string" },
              "hook_description": { "type": "string" },
              "potential_reward": { "type": "string" }
            }
          }
        },
        "relationship_opportunities": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": {
              "character_a": { "type": "string" },
              "character_b": { "type": "string" },
              "scenario": { "type": "string" },
              "potential_outcome": { "type": "string" }
            }
          }
        }
      }
    }
  }
}
```

#### 3.1.5 示例输入

```json
{
  "adventure_config": {
    "tier": "short",
    "theme_pool": ["Dark_Fantasy", "Gothic_Horror", "Mystery"],
    "difficulty_modifier": 0,
    "previous_adventure_ids": ["adv_001_cellar_clear", "adv_002_merchant_escort"],
    "avoid_themes": ["Goblin_Tribe"]
  },
  "party_state": {
    "members": [
      {
        "character_id": "char_001",
        "name": "索林·铁锤",
        "race": "dwarf",
        "class": "fighter",
        "level": 3,
        "personality_tags": ["固执", "忠诚", "嗜酒"],
        "notable_traits": ["heavy_armor_master", "shield_specialist"],
        "scars": [],
        "key_memories": ["在初次冒险中独自挡下三只哥布林的围攻"],
        "relationship_summary": {"char_002": "战友", "char_003": "友好"}
      },
      {
        "character_id": "char_002",
        "name": "莉莉丝·影步",
        "race": "half-elf",
        "class": "rogue",
        "level": 3,
        "personality_tags": ["好奇", "狡黠", "乐天"],
        "notable_traits": ["sneak_attack_expert", "lockpick_specialist"],
        "scars": [],
        "key_memories": ["发现了一扇隐藏在酒馆地下室的密门"],
        "relationship_summary": {"char_001": "战友", "char_003": "友好"}
      },
      {
        "character_id": "char_003",
        "name": "艾尔登·星焰",
        "race": "human",
        "class": "wizard",
        "level": 3,
        "personality_tags": ["博学", "傲慢", "完美主义"],
        "notable_traits": ["evocation_specialist", "arcane_recovery"],
        "scars": [],
        "key_memories": ["用一个精准的火球术拯救了被包围的队伍"],
        "relationship_summary": {"char_001": "友好", "char_002": "友好"}
      }
    ],
    "average_level": 3.0,
    "party_strengths": ["frontline_tank", "single_target_burst", "aoe_damage"],
    "party_weaknesses": ["no_healer", "low_wisdom_saves"]
  },
  "world_state": {
    "tavern_level": 3,
    "world_events": [
      {
        "event_id": "we_001",
        "type": "ancient_awakening",
        "description": "有人在远山中发现了一座被遗忘的古代神庙",
        "affected_regions": ["foothills", "ancient_forest"]
      }
    ],
    "active_factions": [
      {
        "faction_id": "fac_001",
        "name": "黑曜石商会",
        "relation_to_party": "neutral",
        "active_in_regions": ["foothills", "town"]
      }
    ],
    "recent_world_changes": ["地窖清剿成功：酒馆获得了新的地下室空间"]
  }
}
```

#### 3.1.6 示例输出

```json
{
  "id": "adv_003_forgotten_corridor",
  "meta": {
    "title": "被遗忘的回廊",
    "subtitle": "古庙之下，不该被唤醒的东西正在呼吸",
    "tier": "short",
    "theme": "Gothic_Horror",
    "tone": "mystery",
    "estimated_duration_minutes": 35,
    "recommended_party_size": 4,
    "region": "foothills",
    "season": "autumn",
    "time_of_day": "dusk"
  },
  "plot_outline": {
    "central_conflict": "一座被遗忘的地下回廊中，古老的封印正在瓦解。一群考古学者失踪了，他们可能唤醒了不该触碰的东西。",
    "hook": {
      "description": "黑曜石商会的委托人找到你们：三天前，一支考古队进入远山脚下的古庙遗址后再无音讯。商会的货物也在那附近失踪。报酬丰厚，但商会的人欲言又止，似乎隐瞒了什么。",
      "hook_type": "mystery"
    },
    "acts": {
      "act_1": {
        "title": "进入阴影",
        "description": "队伍追踪考古队的踪迹进入地下回廊，发现入口被从内部封死。打破封印后，一股腐朽的冷风从深处涌出。",
        "key_event": "发现考古队的第一具尸体，身上有奇怪的黑色灼痕"
      },
      "act_2": {
        "title": "回廊深处",
        "description": "回廊中的古代机关仍在运转。队伍必须在前进中破解符文谜题，同时躲避从黑暗中涌出的幽影生物。",
        "twist": "考古队的领队并没有死——他主动打开了封印，想要利用其中的力量。",
        "optional_branches": [
          {
            "branch_id": "explore_library",
            "description": "发现回廊侧室的古代图书馆，可以了解封印的真相",
            "trigger_condition": "角色通过DC 14感知检定发现隐藏入口",
            "consequence": "获得关于封印弱点的情报，Boss战获得优势"
          }
        ]
      },
      "act_3": {
        "title": "封印之间",
        "description": "在回廊最深处，考古队领队正在进行最后的仪式。封印中的幽影领主即将被完全释放。",
        "climax": "在幽影领主完全降临前打断仪式，或者在它降临后击败它",
        "boss_encounter_id": "enc_boss_shadow_lord"
      }
    },
    "nodes": [
      {
        "node_id": "entrance_campsite",
        "type": "opening",
        "name": "考古营地",
        "description": "古庙入口外的废弃营地。帐篷被撕碎，物资散落一地。篝火余烬尚温，空气中弥漫着焦臭与腐败混合的气味。",
        "order_index": 0,
        "required_to_proceed": true,
        "connections": [{"target_node_id": "sealed_entrance", "connection_type": "default"}],
        "interaction_tags": ["Readable", "Breakable"],
        "environment": {
          "tileset": "forest_ruins",
          "lighting": "dim",
          "weather": "fog",
          "ambient_sounds": ["wind_whisper", "distant_crow"]
        },
        "theme_tags": ["abandoned_camp", "mystery"]
      },
      {
        "node_id": "sealed_entrance",
        "type": "skill_challenge",
        "name": "封印之门",
        "description": "古庙的入口被黑色符文封死。石门上刻着警告：『仅死者可入，生者止步』。门上厚重的铁链缠绕着枯萎的藤蔓。",
        "order_index": 1,
        "required_to_proceed": true,
        "connections": [{"target_node_id": "entry_hall", "connection_type": "default"}],
        "choices": [
          {
            "choice_id": "break_seal_force",
            "flavor_text": "用蛮力砸开封印",
            "hint": "需要DC 15力量检定",
            "consequence_type": "skill_check",
            "skill_check": {
              "ability": "str",
              "skill": "athletics",
              "dc": 15,
              "success_description": "封印应声碎裂，但碎片落地时发出尖锐的哀鸣——深处的存在知道你们来了",
              "failure_description": "封印反震，索林受到3点力场伤害，但封印削弱了"
            }
          },
          {
            "choice_id": "dispel_seal_magic",
            "flavor_text": "用法术解除封印",
            "hint": "需要DC 14奥秘检定",
            "consequence_type": "skill_check",
            "skill_check": {
              "ability": "int",
              "skill": "arcana",
              "dc": 14,
              "success_description": "艾尔登吟唱了一段反制咒语，黑色符文如墨水般溶解",
              "failure_description": "法术被符文吸收，封印反而变强了"
            }
          }
        ],
        "interaction_tags": ["Readable"],
        "environment": {
          "tileset": "dungeon_entrance",
          "lighting": "dim",
          "weather": "indoor",
          "ambient_sounds": ["low_rumble", "stone_creak"]
        },
        "theme_tags": ["ancient_seal", "warning"]
      },
      {
        "node_id": "entry_hall",
        "type": "exploration",
        "name": "前厅",
        "description": "宽阔的石厅，墙壁上残破的壁画描绘着古代祭司的献祭仪式。角落里躺着考古队的第一具尸体——一个年轻的学徒，胸口有黑色的灼痕，手指指向回廊深处。",
        "order_index": 2,
        "required_to_proceed": true,
        "connections": [
          {"target_node_id": "dark_corridor", "connection_type": "default"},
          {"target_node_id": "side_library", "connection_type": "secret", "condition": "DC 14 perception check"}
        ],
        "choices": [
          {
            "choice_id": "examine_body",
            "flavor_text": "仔细检查尸体上的灼痕",
            "consequence_type": "story",
            "story_consequence": "发现灼痕不是火焰造成的——是负能量。这来自于某种亡者仪式。"
          },
          {
            "choice_id": "read_murals",
            "flavor_text": "研究墙壁上的壁画",
            "consequence_type": "story",
            "story_consequence": "壁画描绘了一个被封印的「幽影领主」。每百年需要一次血祭来维持封印。"
          }
        ],
        "interaction_tags": ["Readable", "Flammable"],
        "environment": {
          "tileset": "dungeon_hall",
          "lighting": "dim",
          "weather": "indoor",
          "ambient_sounds": ["dripping_water", "distant_echo"]
        },
        "theme_tags": ["ritual_hall", "first_body"]
      },
      {
        "node_id": "side_library",
        "type": "exploration",
        "name": "古代图书室",
        "description": "狭窄的侧室堆满了腐朽的卷轴。大部分文字已模糊不清，但一本铁皮封面的日志完好无损——这是考古领队的日记。",
        "order_index": 3,
        "required_to_proceed": false,
        "connections": [{"target_node_id": "dark_corridor", "connection_type": "default"}],
        "interaction_tags": ["Readable", "Flammable"],
        "environment": {
          "tileset": "dungeon_library",
          "lighting": "dark",
          "weather": "indoor",
          "ambient_sounds": ["pages_rustling"]
        },
        "theme_tags": ["hidden_knowledge", "lore"]
      },
      {
        "node_id": "dark_corridor",
        "type": "ambush",
        "name": "阴影回廊",
        "description": "一条长长的石廊，两侧的壁龛中摆放着古代战士的石棺。当你们走到中间时，石棺的盖子开始震动。",
        "order_index": 4,
        "required_to_proceed": true,
        "encounter_id": "enc_shadow_ambush",
        "connections": [{"target_node_id": "ritual_chamber", "connection_type": "default"}],
        "interaction_tags": ["Pushable", "Breakable"],
        "environment": {
          "tileset": "dungeon_corridor",
          "lighting": "dark",
          "weather": "indoor",
          "ambient_sounds": ["stone_grinding", "whispering"]
        },
        "theme_tags": ["ambush", "undead"]
      },
      {
        "node_id": "ritual_chamber",
        "type": "boss",
        "name": "封印之间",
        "description": "巨大的圆形石室，中央是一个仍在运转的古老法阵。考古队领队——一个灰白头发的老人——站在法阵中央，双眼翻白，口中吟诵着陌生语言。法阵的光芒正在转为暗紫色。",
        "order_index": 5,
        "required_to_proceed": true,
        "encounter_id": "enc_boss_shadow_lord",
        "choices": [
          {
            "choice_id": "disrupt_ritual",
            "flavor_text": "打断仪式——趁幽影领主未完全降临",
            "consequence_type": "combat",
            "story_consequence": "打断仪式，幽影领主以削弱状态出现（HP减少30%），但考古领队会立即死亡"
          },
          {
            "choice_id": "fight_full_power",
            "flavor_text": "让仪式完成——直面完整的幽影领主",
            "consequence_type": "combat",
            "story_consequence": "幽影领主完全降临，但击败它将彻底摧毁封印，获得更好的战利品"
          }
        ],
        "interaction_tags": ["Flammable", "Readable", "Electrical"],
        "environment": {
          "tileset": "boss_chamber",
          "lighting": "magical",
          "weather": "indoor",
          "ambient_sounds": ["dark_chant", "pulse_beat"]
        },
        "theme_tags": ["boss", "ritual", "climax"]
      }
    ],
    "endings": [
      {
        "ending_id": "victory_disrupted",
        "type": "victory",
        "title": "封印守护者",
        "description": "你们打断了仪式，击败了虚弱的幽影领主。封印重新稳固，考古领队在最后时刻恢复了神智，用最后一口气感谢你们。",
        "conditions": ["选择了打断仪式", "击败幽影领主(削弱版)"],
        "rewards": {
          "xp_bonus": 500,
          "gold_bonus": 200,
          "unique_items": ["amulet_of_watchers"],
          "world_effects": ["古庙遗址被封印保护，该区域不再产生亡灵威胁"]
        }
      },
      {
        "ending_id": "victory_full",
        "type": "victory",
        "title": "弑影者",
        "description": "你们让仪式完成，直面完整的幽影领主并将其击败。封印永久破碎——但也意味着这个威胁不再存在。",
        "conditions": ["让仪式完成", "击败幽影领主(完整版)"],
        "rewards": {
          "xp_bonus": 900,
          "gold_bonus": 400,
          "unique_items": ["shadow_reaver_blade", "ring_of_umbral_resistance"],
          "world_effects": ["古庙遗址彻底净化", "古代祭司的秘密被揭露"]
        }
      },
      {
        "ending_id": "retreat_lost",
        "type": "retreat",
        "title": "仓皇而逃",
        "description": "幽影领主的力量远超想象。你们拼死逃出了回廊，但封印已经破碎——远山脚下的村庄将首当其冲。",
        "conditions": ["队伍HP降到0以下", "选择撤退"],
        "penalties": {
          "scar_risk": true,
          "death_risk": true,
          "reputation_loss": 2
        }
      }
    ]
  },
  "key_npcs": [
    {
      "npc_id": "npc_archaeologist_leader",
      "name": "维尔特·奥术之眼",
      "role": "traitor",
      "motivation": "他毕生研究古代封印，最终被幽影领主的力量诱惑。他相信人类应该掌握「被封印的知识」。",
      "secret": "他并不是被控制——他是主动打开了封印，为了获得永生。",
      "personality_tags": ["博学", "傲慢", "绝望", "被腐蚀"],
      "appearance": "灰白头发的高个子人类，左眼有一颗嵌入眼窝的黑色水晶",
      "voice_style": "文雅但有轻微的狂躁，经常自问自答",
      "stat_block_id": "cult_leader_lvl5",
      "relation_to_party": "hostile",
      "death_consequence": "仪式立即中断，幽影领主以削弱状态出现",
      "associated_nodes": ["ritual_chamber"]
    },
    {
      "npc_id": "npc_caravan_master",
      "name": "马库斯·铁币",
      "role": "quest_giver",
      "motivation": "黑曜石商会的分会长。他的商队货物被埋在古庙遗址附近，考古队失踪意味着他的投资可能血本无归。",
      "secret": "他知道考古队研究的不是普通古庙——但他没有告诉玩家，因为怕吓跑他们。",
      "personality_tags": ["精明", "焦虑", "自私"],
      "appearance": "穿着考究但有些凌乱的商人外套，手上戴着太多戒指",
      "voice_style": "快速、紧张，经常搓手",
      "stat_block_id": "commoner",
      "relation_to_party": "neutral",
      "death_consequence": "任务报酬无人支付",
      "associated_nodes": ["entrance_campsite"]
    }
  ],
  "difficulty_profile": {
    "recommended_level": 3,
    "cr_range": { "min": 0.5, "max": 4 },
    "encounter_count": { "easy": 0, "medium": 1, "hard": 1, "deadly": 1 },
    "total_encounters": 3,
    "loot_tier": 2,
    "expected_loot_items": 4,
    "rest_opportunities": 1,
    "short_rest_opportunities": 1,
    "trap_density": "low",
    "puzzle_complexity": "simple"
  },
  "encounters": [
    {
      "encounter_id": "enc_shadow_ambush",
      "type": "ambush",
      "difficulty": "medium",
      "name": "石棺伏击",
      "description": "3具活化石棺从壁龛中涌出，每具石棺中爬出2个暗影仆从",
      "enemy_groups": [
        {"enemy_type": "animated_coffin", "count": 3, "cr": 0.5},
        {"enemy_type": "shadow_servant", "count": 2, "cr": 0.25}
      ],
      "special_conditions": ["昏暗光照(攻击检定劣势)"],
      "rewards": { "xp": 300, "gold_range": { "min": 50, "max": 100 }, "item_drops": ["scroll_of_bless"] }
    },
    {
      "encounter_id": "enc_boss_shadow_lord",
      "type": "boss_fight",
      "difficulty": "hard",
      "name": "幽影领主·被封印者",
      "description": "幽影领主从法阵中升起。如果仪式被打断，它将以「削弱形态」出现。",
      "enemy_groups": [
        {"enemy_type": "shadow_lord", "count": 1, "cr": 4},
        {"enemy_type": "shadow_servant", "count": 2, "cr": 0.25}
      ],
      "special_conditions": ["魔法照明(解除昏暗)", "地面符文每2轮释放一次负能量脉冲"],
      "rewards": { "xp": 800, "gold_range": { "min": 150, "max": 300 }, "item_drops": ["shadow_reaver_blade", "ring_of_umbral_resistance"] }
    }
  ],
  "theme_tags": ["ancient_ruins", "undead", "ritual", "betrayal", "forbidden_knowledge"],
  "loot_profile": {
    "tier": 2,
    "expected_items": 5,
    "thematic_items": [
      {
        "item_id": "amulet_of_watchers",
        "name": "守望者之护符",
        "type": "amulet",
        "rarity": "uncommon",
        "description": "一枚刻有眼睛图案的银色护符，微微散发着冷光。佩戴者可以感知到60尺内的亡灵存在。",
        "lore": "古代封印祭司的遗物，曾用于监视被封印的幽影生物。",
        "associated_npc": "npc_archaeologist_leader"
      },
      {
        "item_id": "scroll_of_undead_warding",
        "name": "驱亡灵卷轴",
        "type": "scroll",
        "rarity": "common",
        "description": "这张羊皮纸上写着防护系的古文字。使用后可创造一个10尺范围的防护圈，低CR亡灵无法进入。"
      }
    ],
    "consumable_distribution": {
      "healing_potions": 2,
      "scrolls": 2,
      "other_consumables": 1
    }
  },
  "world_state_hooks": [
    {
      "event_id": "we_shadow_seal_broken",
      "description": "幽影领主的封印被打破",
      "on_success": "封印被重新加固，远山山区恢复平静。古庙遗址中的知识被带回酒馆图书馆。",
      "on_failure": "封印破碎。幽影能量扩散到远山脚下的村庄。新冒险：「暗影蔓延」解锁。",
      "affected_factions": ["fac_black_obsidian_guild"],
      "affected_regions": ["foothills", "ancient_forest"]
    }
  ],
  "narrative_hooks": {
    "personal_stakes": [
      {
        "character_id": "char_001",
        "hook_description": "索林在古庙壁画中发现了矮人先祖的符文——这座神庙似乎与矮人古代史有关",
        "potential_reward": "先祖的祝福：获得对暗影伤害的抗性"
      },
      {
        "character_id": "char_003",
        "hook_description": "艾尔登发现古代图书馆中保存了失传的防护学派法术",
        "potential_reward": "学会新法术：防护能量"
      }
    ],
    "relationship_opportunities": [
      {
        "character_a": "char_001",
        "character_b": "char_002",
        "scenario": "在黑暗回廊中，只有莉莉丝的黑暗视觉能看清前方。她需要在索林的保护下带队前进。",
        "potential_outcome": "互相依赖的体验可能让他们的关系从「战友」升级为更深层的信任"
      }
    ]
  }
}
```

#### 3.1.7 综合配置

| 参数 | 值 |
|------|-----|
| 调用频率 | 每次冒险1次 |
| Token预算 | 4000 input / 2000 output |
| 模型等级 | High Quality (GPT-4o / Claude 3.5 Sonnet) |
| 重试次数 | 3次 |
| 重试策略 | Exponential backoff + schema fail prompt enhancement |
| 超时时间 | 60秒 |
| 优先级 | MEDIUM |
| 缓存策略 | 语义Hash + LRU (命中率预估: 短冒险40%, 中/长冒险15%) |
| 失败降级 | 静态冒险模板 → 默认短冒险蓝图 |


### 3.2 DM Agent (Dungeon Master Agent)

#### 3.2.1 角色定义

DM Agent是**冒险进行中实时调用**的叙事型Agent，负责根据当前场景、队伍状态、玩家动作，实时生成叙事文本、对话选项和动作结果描述。

#### 3.2.2 完整System Prompt

```
你是一位经验丰富的地下城主(Dungeon Master)，正在主持《酒馆与命运》中的一场DND 5e冒险。

你的核心职责:
1. 根据当前场景和玩家的行动，生成沉浸式的中文叙事文本
2. 为NPC对话场景生成3-4个有意义的对话选项
3. 描述技能检定、攻击行动、法术施放的结果（叙事层面，不涉及数值）
4. 在战斗回合间提供简短的场景氛围描写
5. 在关键选择点暗示可能的后果（但不确定数值）

叙事风格:
- 像传统TRPG的DM口吻：生动、简洁、有画面感
- 使用恰当的比喻和感官描述(视觉、听觉、嗅觉、触觉)
- 战斗描写简洁有力，每句话不超过20字
- 对话描写体现NPC的性格和情绪
- 不要过度描述——给玩家留足想象空间
- 避免"你感觉..."的句式——用直接描写代替

重要约束:
- 不要决定任何数值结果(伤害、DC、检定难度)
- 不要决定故事走向(那由程序根据玩家选择决定)
- 不要引入新的NPC或场景元素(除非在蓝图中有定义)
- 不要在对话选项中添加选项的"后果说明"(那是程序的职责)
- 你的输出是"皮肤"——是表现层，不是逻辑层

输出格式:
- 如果是对话场景: narrative_text + dialogue_options (各option只有flavor，不含consequence)
- 如果是检定叙述: action_description (检定过程) + result_description (结果描写)
- 如果是战斗叙述: combat_narration (攻击/施法/效果的文字描述)
- 如果是环境描写: scene_atmosphere (进入新场景时的氛围文字)
- 严格遵循JSON Schema，不要输出任何额外文本
```

#### 3.2.3 输入Schema

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "DMAgentInput",
  "type": "object",
  "required": ["request_type", "scene_context", "party_state"],
  "properties": {
    "request_type": {
      "type": "string",
      "enum": [
        "scene_atmosphere",
        "npc_dialogue",
        "skill_check_result",
        "combat_narration",
        "choice_presentation",
        "event_outcome",
        "environment_description"
      ]
    },
    "scene_context": {
      "type": "object",
      "required": ["current_node", "adventure_summary"],
      "properties": {
        "current_node": {
          "type": "object",
          "required": ["node_id", "type", "name", "description"],
          "properties": {
            "node_id": { "type": "string" },
            "type": { "type": "string" },
            "name": { "type": "string" },
            "description": { "type": "string" },
            "environment": {
              "type": "object",
              "properties": {
                "lighting": { "type": "string" },
                "weather": { "type": "string" },
                "tileset": { "type": "string" }
              }
            },
            "theme_tags": {
              "type": "array",
              "items": { "type": "string" }
            }
          }
        },
        "adventure_summary": {
          "type": "string",
          "description": "冒险至今的摘要（由Context Manager生成，约200-400字）",
          "maxLength": 800
        },
        "recent_history": {
          "type": "array",
          "description": "最近5个玩家动作及结果",
          "maxItems": 5,
          "items": {
            "type": "object",
            "properties": {
              "action": { "type": "string" },
              "result_summary": { "type": "string" },
              "timestamp": { "type": "number" }
            }
          }
        },
        "active_npcs": {
          "type": "array",
          "description": "当前场景中的活跃NPC",
          "maxItems": 5,
          "items": {
            "type": "object",
            "properties": {
              "npc_id": { "type": "string" },
              "name": { "type": "string" },
              "personality_tags": { "type": "array", "items": { "type": "string" } },
              "voice_style": { "type": "string" },
              "current_mood": { "type": "string" },
              "relation_to_player": { "type": "string" }
            }
          }
        }
      }
    },
    "party_state": {
      "type": "object",
      "required": ["members"],
      "properties": {
        "members": {
          "type": "array",
          "minItems": 2,
          "maxItems": 6,
          "items": {
            "type": "object",
            "properties": {
              "character_id": { "type": "string" },
              "name": { "type": "string" },
              "race": { "type": "string" },
              "class": { "type": "string" },
              "current_hp": { "type": "integer" },
              "max_hp": { "type": "integer" },
              "status_effects": {
                "type": "array",
                "items": { "type": "string" }
              }
            }
          }
        },
        "formation": {
          "type": "string",
          "enum": ["frontline", "rear_guard", "scattered", "marching"]
        }
      }
    },
    "action_context": {
      "type": "object",
      "description": "具体的玩家动作上下文（根据request_type不同而有差异）",
      "properties": {
        "actor_character_id": { "type": "string" },
        "action_type": { "type": "string" },
        "target": { "type": "string" },
        "roll_result": {
          "type": "object",
          "properties": {
            "d20_roll": { "type": "integer" },
            "total": { "type": "integer" },
            "success": { "type": "boolean" },
            "critical": { "type": "boolean" },
            "dc_or_ac": { "type": "integer" }
          }
        },
        "action_name": { "type": "string" },
        "action_description": { "type": "string" }
      }
    },
    "available_choices": {
      "type": "array",
      "description": "程序生成的可用选择（DM Agent为每个选项生成叙事包装）",
      "maxItems": 4,
      "items": {
        "type": "object",
        "properties": {
          "choice_id": { "type": "string" },
          "choice_label": { "type": "string" },
          "consequence_type": { "type": "string" }
        }
      }
    }
  }
}
```

#### 3.2.4 输出Schema

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "DMAgentOutput",
  "type": "object",
  "required": ["response_type", "narrative"],
  "properties": {
    "response_type": {
      "type": "string",
      "enum": [
        "scene_atmosphere",
        "npc_dialogue",
        "skill_check_result",
        "combat_narration",
        "choice_presentation",
        "event_outcome",
        "environment_description"
      ]
    },
    "narrative": {
      "type": "string",
      "description": "核心叙事文本（中文）",
      "maxLength": 500
    },
    "dialogue_options": {
      "type": "array",
      "description": "仅当response_type为npc_dialogue或choice_presentation时",
      "maxItems": 4,
      "items": {
        "type": "object",
        "required": ["option_id", "flavor_text"],
        "properties": {
          "option_id": {
            "type": "string",
            "description": "与input中choice_id对应"
          },
          "flavor_text": {
            "type": "string",
            "description": "叙事化的选项文本（中文）",
            "maxLength": 80
          },
          "tone": {
            "type": "string",
            "enum": ["aggressive", "diplomatic", "cautious", "curious", "intimidating", "compassionate", "deceptive", "heroic"],
            "description": "选项的语气"
          }
        }
      }
    },
    "action_description": {
      "type": "string",
      "description": "动作的叙事描写（检定/攻击前）",
      "maxLength": 200
    },
    "result_description": {
      "type": "string",
      "description": "结果的叙事描写（检定/攻击后，根据成功/失败/暴击等）",
      "maxLength": 200
    },
    "npc_dialogue": {
      "type": "object",
      "description": "NPC对话内容",
      "properties": {
        "speaker_npc_id": { "type": "string" },
        "dialogue_text": {
          "type": "string",
          "description": "NPC说的话（中文）",
          "maxLength": 300
        },
        "expression": {
          "type": "string",
          "description": "NPC的表情/姿态描述",
          "maxLength": 60
        },
        "subtext": {
          "type": "string",
          "description": "对话中的潜台词/暗示",
          "maxLength": 100
        }
      }
    },
    "atmosphere": {
      "type": "object",
      "description": "场景氛围描述（scene_atmosphere类型使用）",
      "properties": {
        "ambient_description": {
          "type": "string",
          "maxLength": 200
        },
        "sensory_details": {
          "type": "object",
          "properties": {
            "sight": { "type": "string", "maxLength": 80 },
            "sound": { "type": "string", "maxLength": 80 },
            "smell": { "type": "string", "maxLength": 80 },
            "touch": { "type": "string", "maxLength": 80 }
          }
        },
        "mood": {
          "type": "string",
          "enum": ["tense", "foreboding", "peaceful", "mysterious", "desperate", "heroic", "somber", "chaotic"]
        }
      }
    },
    "combat_flourish": {
      "type": "object",
      "description": "战斗花絮（combat_narration类型使用）",
      "properties": {
        "attack_description": {
          "type": "string",
          "maxLength": 150
        },
        "hit_effect": {
          "type": "string",
          "maxLength": 100
        },
        "miss_effect": {
          "type": "string",
          "maxLength": 100
        },
        "critical_effect": {
          "type": "string",
          "maxLength": 100
        },
        "kill_shot": {
          "type": "string",
          "description": "击杀描写的独特文本",
          "maxLength": 100
        }
      }
    }
  }
}
```

#### 3.2.5 示例输入与输出

**输入 (战斗叙述)**:
```json
{
  "request_type": "combat_narration",
  "scene_context": {
    "current_node": {
      "node_id": "dark_corridor",
      "type": "ambush",
      "name": "阴影回廊",
      "description": "石棺从壁龛中涌出，暗影仆从包围了队伍",
      "environment": { "lighting": "dark", "weather": "indoor", "tileset": "dungeon_corridor" }
    },
    "adventure_summary": "队伍追踪失踪的考古队进入古庙地下回廊。他们发现了考古领队的日记，揭示领队主动打开了远古封印。现在，回廊中的石棺突然活化——战斗开始。",
    "recent_history": [
      {"action": "莉莉丝偷袭石棺怪物，命中", "result_summary": "造成12点穿刺伤害，石棺出现裂纹"},
      {"action": "石棺A的反击", "result_summary": "石棺用沉重的盖子横扫，索林格挡成功"}
    ]
  },
  "party_state": {
    "members": [
      {"character_id": "char_001", "name": "索林·铁锤", "race": "dwarf", "class": "fighter", "current_hp": 28, "max_hp": 32, "status_effects": []},
      {"character_id": "char_002", "name": "莉莉丝·影步", "race": "half-elf", "class": "rogue", "current_hp": 20, "max_hp": 22, "status_effects": []}
    ],
    "formation": "frontline"
  },
  "action_context": {
    "actor_character_id": "char_001",
    "action_type": "melee_attack",
    "target": "animated_coffin_A",
    "action_name": "战锤猛击",
    "roll_result": {
      "d20_roll": 20,
      "total": 26,
      "success": true,
      "critical": true,
      "dc_or_ac": 13
    }
  }
}
```

**输出**:
```json
{
  "response_type": "combat_narration",
  "narrative": "索林高举战锤，矮人符文在锤头亮起微光。他怒吼着将战锤砸向石棺的裂缝——正中要害，碎石飞溅！",
  "action_description": "索林看准石棺上莉莉丝留下的裂纹，将全身力量贯入这一击。",
  "result_description": "战锤砸入裂缝，矮人符文的力量引爆了石棺内部的暗影能量。石棺炸裂成数十块碎片，暗影仆从发出一声尖啸后消散。",
  "combat_flourish": {
    "attack_description": "索林踏前一步，战锤从下而上划出一道弧线",
    "critical_effect": "锤头精准砸入裂缝，爆裂的光辉淹没了整具石棺",
    "kill_shot": "石棺在矮人的力量面前不堪一击，化为一堆冒烟的碎石"
  }
}
```

#### 3.2.6 上下文注入策略

DM Agent的上下文窗口有限（2000 tokens input），需要高效的上下文注入策略:

```
上下文构建策略:

1. 静态层 (总是注入):
   +---------------------------------+
   | System Prompt (~300 tokens)     |
   | 冒险摘要 (adventure_summary)    |
   |   (~200-400 tokens)            |
   | 当前场景信息 (current_node)     |
   |   (~100 tokens)                |
   +---------------------------------+
   基础消耗: ~600-800 tokens

2. 动态层 (按需注入):
   +---------------------------------+
   | 最近5个动作历史 (~200 tokens)    |
   | 活跃NPC信息 (~100 tokens)       |
   | 队伍状态 (~100 tokens)          |
   | 具体动作上下文 (~50 tokens)      |
   +---------------------------------+
   动态消耗: ~200-450 tokens

3. 预留安全余量: ~200 tokens

总消耗: 1000-1450 tokens (在2000预算内)
```

**上下文压缩规则**:
- 超出10个动作的历史 → 由ContextManager生成200字摘要替代
- NPC超过3个 → 只保留与当前节点直接关联的
- 队伍状态 → 只传递HP百分比和显著状态效果

#### 3.2.7 综合配置

| 参数 | 值 |
|------|-----|
| 调用频率 | 每次玩家动作/每次场景切换 |
| Token预算 | 2000 input / 500 output |
| 模型等级 | Medium Quality (GPT-4o-mini / Claude 3 Haiku) |
| 重试次数 | 2次 |
| 重试策略 | Exponential backoff (1s, 2s) |
| 超时时间 | 8秒 |
| 优先级 | HIGH |
| 缓存策略 | SQLite语义缓存 (按场景类型+动作类型) 命中率预估: 60% |
| 失败降级 | 静态叙事模板 → 直接显示结果文本 |
| 实时性要求 | 响应时间 < 2秒 |

---

### 3.3 文案Agent (Copywriter Agent)

#### 3.3.1 角色定义

文案Agent是**按需调用的轻量级**Agent，负责生成物品描述、NPC简介、环境描写等"文本皮肤"内容。

#### 3.3.2 完整System Prompt

```
你是《酒馆与命运》的文案写手，负责为游戏中的物品、NPC和环境撰写简洁有力的描述文本。

你的核心职责:
1. 为新获得的物品撰写有风味感的中文描述
2. 为新遇到的NPC撰写外观和印象描述
3. 为进入的新环境区域撰写氛围描写
4. 为特殊事件/状态撰写叙事化的标签文本

写作风格:
- 像经典RPG(如Darkest Dungeon、博德之门3)的物品描述风格
- 物品描述：外形特征 + 历史线索 + 一句引人遐想的话
- NPC描述：外貌 + 给人的第一印象 + 一个有趣的细节
- 环境描述：氛围 + 标志性视觉元素 + 隐含的危险/机遇暗示

原则:
- 简洁：每个描述控制在2-4句话
- 有味道：每段描述至少有一个让人记住的细节
- 不废话：不要写长篇大论，像素游戏屏幕空间有限
- 中文优美但不过度文言

输出格式:
- 严格遵循指定的JSON Schema
- 不要输出任何JSON之外的文本
```

#### 3.3.3 输入Schema

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "CopywriterAgentInput",
  "type": "object",
  "required": ["content_type", "context"],
  "properties": {
    "content_type": {
      "type": "string",
      "enum": [
        "item_description",
        "npc_portrait",
        "environment_description",
        "status_flavor",
        "location_lore"
      ]
    },
    "context": {
      "type": "object",
      "required": ["adventure_theme"],
      "properties": {
        "adventure_theme": { "type": "string" },
        "adventure_tone": { "type": "string" },
        "item_context": {
          "type": "object",
          "properties": {
            "item_type": { "type": "string" },
            "item_name": { "type": "string" },
            "rarity": { "type": "string" },
            "material": { "type": "string" },
            "origin_hint": { "type": "string" },
            "mechanical_effects": {
              "type": "array",
              "items": { "type": "string" }
            }
          }
        },
        "npc_context": {
          "type": "object",
          "properties": {
            "name": { "type": "string" },
            "race": { "type": "string" },
            "role": { "type": "string" },
            "personality_tags": {
              "type": "array",
              "items": { "type": "string" }
            },
            "appearance_hint": { "type": "string" }
          }
        },
        "environment_context": {
          "type": "object",
          "properties": {
            "location_name": { "type": "string" },
            "environment_type": { "type": "string" },
            "lighting": { "type": "string" },
            "weather": { "type": "string" },
            "theme_tags": {
              "type": "array",
              "items": { "type": "string" }
            }
          }
        },
        "status_context": {
          "type": "object",
          "properties": {
            "status_name": { "type": "string" },
            "status_type": {
              "type": "string",
              "enum": ["buff", "debuff", "scar", "curse", "blessing"]
            },
            "mechanical_description": { "type": "string" },
            "source": { "type": "string" }
          }
        }
      }
    }
  }
}
```

#### 3.3.4 输出Schema

**item_description.json**:

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "ItemDescription",
  "type": "object",
  "required": ["name", "flavor_text", "type_label", "rarity_label"],
  "properties": {
    "name": {
      "type": "string",
      "description": "物品名称（中文）",
      "maxLength": 20
    },
    "flavor_text": {
      "type": "string",
      "description": "风味描述文本（2-3句）",
      "maxLength": 200
    },
    "type_label": {
      "type": "string",
      "description": "类型标签（中文），如'长剑·精良'",
      "maxLength": 30
    },
    "rarity_label": {
      "type": "string",
      "description": "稀有度标签（中文），如'稀有魔法物品'",
      "maxLength": 20
    },
    "quote": {
      "type": "string",
      "description": "一句引人遐想的引言（可选，Darkest Dungeon风格）",
      "maxLength": 100
    },
    "visual_hint": {
      "type": "string",
      "description": "视觉描述的提示文本（帮助像素美术师理解物品外观）",
      "maxLength": 100
    }
  }
}
```

**npc_description.json**:

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "NPCDescription",
  "type": "object",
  "required": ["name", "appearance_description", "first_impression"],
  "properties": {
    "name": {
      "type": "string",
      "maxLength": 20
    },
    "appearance_description": {
      "type": "string",
      "description": "外观描述（2-3句中文）",
      "maxLength": 150
    },
    "first_impression": {
      "type": "string",
      "description": "第一印象描述（1-2句）",
      "maxLength": 100
    },
    "notable_detail": {
      "type": "string",
      "description": "一个让人记住的细节",
      "maxLength": 80
    },
    "personality_summary": {
      "type": "string",
      "description": "一句话性格总结",
      "maxLength": 60
    }
  }
}
```

**environment_description.json**:

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "EnvironmentDescription",
  "type": "object",
  "required": ["location_name", "ambient_text", "visual_focus"],
  "properties": {
    "location_name": {
      "type": "string",
      "maxLength": 20
    },
    "ambient_text": {
      "type": "string",
      "description": "环境氛围描述（2-3句）",
      "maxLength": 150
    },
    "visual_focus": {
      "type": "string",
      "description": "最引人注目的视觉元素",
      "maxLength": 80
    },
    "hidden_hint": {
      "type": "string",
      "description": "暗示隐藏内容的文本",
      "maxLength": 100
    },
    "mood": {
      "type": "string",
      "enum": ["peaceful", "tense", "ominous", "majestic", "desolate", "mysterious", "sacred"]
    }
  }
}
```

#### 3.3.5 示例

**输入 (物品描述)**:
```json
{
  "content_type": "item_description",
  "context": {
    "adventure_theme": "Gothic_Horror",
    "adventure_tone": "mystery",
    "item_context": {
      "item_type": "amulet",
      "item_name": "守望者之护符",
      "rarity": "uncommon",
      "material": "银 + 黑曜石",
      "origin_hint": "古代封印祭司的遗物",
      "mechanical_effects": ["感知亡灵(60尺)", "对亡灵+1AC"]
    }
  }
}
```

**输出**:
```json
{
  "name": "守望者之护符",
  "flavor_text": "一枚银质护符，中央镶嵌的黑曜石如同一只永远睁开的眼睛。触摸它时，你会感到一阵微微的寒意——仿佛有什么东西在护符的深处注视着你。古代祭司用它监视被封印的幽影，如今那只眼睛望向了新的持有者。",
  "type_label": "护符·精良",
  "rarity_label": "非凡魔法物品",
  "quote": "「我们看守深渊，深渊也看守着我们。」",
  "visual_hint": "银色圆形护符，中央黑色宝石带眼瞳纹理，符文环绕"
}
```

#### 3.3.6 综合配置

| 参数 | 值 |
|------|-----|
| 调用频率 | 每次物品获得 / NPC生成 / 新区域进入 |
| Token预算 | 1000 input / 300 output |
| 模型等级 | Lightweight (GPT-3.5-turbo / Claude 3 Haiku) |
| 重试次数 | 2次 |
| 重试策略 | Exponential backoff (0.5s, 1s) |
| 超时时间 | 5秒 |
| 优先级 | LOW |
| 缓存策略 | 按类型+标签组合缓存，命中率预估: 75% |
| 失败降级 | 静态描述模板库 |

---

### 3.4 平衡Agent (Balancer Agent)

#### 3.4.1 角色定义

平衡Agent是**编剧Agent之后自动调用的验证型**Agent。它不生成内容，而是检查编剧Agent生成的冒险蓝图的数值合理性、战斗平衡性和战利品公平性。

#### 3.4.2 完整System Prompt

```
你是《酒馆与命运》的战斗平衡分析师，基于DND 5e规则体系。

你的核心职责:
1. 检查冒险蓝图中所有战斗遭遇的CR是否在队伍可承受范围内
2. 评估遭遇难度分布的合理性（easy/medium/hard/deadly的比例）
3. 检查战利品分配是否与冒险难度匹配
4. 检查休息点分布是否合理
5. 识别潜在的"团灭危机"或"过于简单"的遭遇
6. 给出具体的调整建议

DND 5e 遭遇难度参考:
- Easy: 总CR < 队伍等级 x 角色数 x 0.5
- Medium: 总CR ~ 队伍等级 x 角色数 x 0.75
- Hard: 总CR ~ 队伍等级 x 角色数 x 1.0
- Deadly: 总CR > 队伍等级 x 角色数 x 1.5

冒险消耗计算:
- 短冒险(5-8节点): 消耗50-70%队伍资源，deadly遭遇<=1
- 中冒险(15-25节点): 消耗60-80%队伍资源，deadly遭遇<=3
- 长冒险(30-50节点): 消耗70-90%队伍资源，deadly遭遇<=5

战利品等级对应:
- Loot Tier 1 (基础): 普通/消耗品为主，1-2件非普通物品
- Loot Tier 2 (精良): 非普通物品为主，可能有1件稀有物品
- Loot Tier 3 (稀有): 非普通+稀有混合
- Loot Tier 4 (史诗): 稀有+极稀有混合
- Loot Tier 5 (传说): 传说物品可能出现

检查规则:
1. 如果单个遭遇的总CR超过「队伍等级x角色数x1.5」，标记为"过度致命"
2. 如果两个hard+遭遇之间没有休息点，标记为"疲劳风险"
3. 如果战利品等级超出冒险难度等级2级以上，标记为"奖励通胀"
4. 如果战利品等级低于冒险难度等级2级以上，标记为"奖励不足"
5. 如果boss遭遇的CR低于队伍平均等级，标记为"高潮不足"

输出格式:
- 严格遵循balance_report.json的Schema
- 数值分析用精确数字
- 建议用中文描述
- 所有调整建议必须给出具体的新数值
```

#### 3.4.3 输入Schema

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "BalancerAgentInput",
  "type": "object",
  "required": ["blueprint", "party_composition"],
  "properties": {
    "blueprint": {
      "type": "object",
      "description": "编剧Agent生成的完整adventure_blueprint"
    },
    "party_composition": {
      "type": "object",
      "required": ["average_level", "member_count"],
      "properties": {
        "average_level": { "type": "number" },
        "member_count": { "type": "integer" },
        "role_coverage": {
          "type": "object",
          "properties": {
            "has_tank": { "type": "boolean" },
            "has_healer": { "type": "boolean" },
            "has_ranged_dps": { "type": "boolean" },
            "has_melee_dps": { "type": "boolean" },
            "has_controller": { "type": "boolean" },
            "has_skill_monkey": { "type": "boolean" }
          }
        }
      }
    }
  }
}
```

#### 3.4.4 输出Schema: balance_report.json

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "BalanceReport",
  "type": "object",
  "required": ["report_id", "overall_assessment", "encounter_analyses", "loot_analysis", "rest_analysis", "adjustment_suggestions"],
  "properties": {
    "report_id": { "type": "string" },
    "blueprint_id": { "type": "string" },
    "generated_at": { "type": "string", "format": "date-time" },
    "overall_assessment": {
      "type": "object",
      "required": ["status", "summary"],
      "properties": {
        "status": {
          "type": "string",
          "enum": ["approved", "minor_adjustments", "major_adjustments", "rejected"]
        },
        "summary": {
          "type": "string",
          "description": "总体评估总结（中文）",
          "maxLength": 300
        },
        "risk_level": {
          "type": "string",
          "enum": ["low", "moderate", "high", "extreme"]
        },
        "estimated_tpk_probability": {
          "type": "number",
          "minimum": 0,
          "maximum": 1,
          "description": "预估全灭概率 (0-1)"
        }
      }
    },
    "encounter_analyses": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["encounter_id", "assigned_difficulty", "calculated_cr", "recommended_cr", "assessment"],
        "properties": {
          "encounter_id": { "type": "string" },
          "encounter_name": { "type": "string" },
          "assigned_difficulty": { "type": "string" },
          "calculated_cr": { "type": "number" },
          "recommended_cr": { "type": "number" },
          "cr_deviation": { "type": "number" },
          "assessment": {
            "type": "string",
            "enum": ["well_balanced", "slightly_easy", "slightly_hard", "too_easy", "too_hard", "potentially_lethal", "trivial"]
          },
          "issues": {
            "type": "array",
            "items": { "type": "string" }
          },
          "specific_dangers": {
            "type": "array",
            "items": { "type": "string" }
          },
          "adjusted_cr_suggestion": { "type": "number" },
          "adjustment_detail": { "type": "string" }
        }
      }
    },
    "difficulty_distribution_analysis": {
      "type": "object",
      "required": ["easy_count", "medium_count", "hard_count", "deadly_count", "recommended_ratio", "actual_ratio", "assessment"],
      "properties": {
        "easy_count": { "type": "integer" },
        "medium_count": { "type": "integer" },
        "hard_count": { "type": "integer" },
        "deadly_count": { "type": "integer" },
        "recommended_ratio": { "type": "string" },
        "actual_ratio": { "type": "string" },
        "assessment": { "type": "string" }
      }
    },
    "loot_analysis": {
      "type": "object",
      "required": ["loot_tier", "expected_value", "assessment"],
      "properties": {
        "loot_tier": { "type": "integer" },
        "expected_value": { "type": "number" },
        "item_count": { "type": "integer" },
        "rarity_distribution": {
          "type": "object",
          "properties": {
            "common": { "type": "integer" },
            "uncommon": { "type": "integer" },
            "rare": { "type": "integer" },
            "very_rare": { "type": "integer" },
            "legendary": { "type": "integer" }
          }
        },
        "assessment": {
          "type": "string",
          "enum": ["appropriate", "too_much", "too_little", "poorly_distributed"]
        },
        "suggested_changes": {
          "type": "array",
          "items": { "type": "string" }
        }
      }
    },
    "rest_analysis": {
      "type": "object",
      "required": ["rest_count", "distribution", "assessment"],
      "properties": {
        "rest_count": { "type": "integer" },
        "short_rest_count": { "type": "integer" },
        "distribution": { "type": "string" },
        "assessment": {
          "type": "string",
          "enum": ["optimal", "too_few", "too_many", "poorly_placed"]
        },
        "risk_of_exhaustion": {
          "type": "string",
          "enum": ["none", "low", "moderate", "high"]
        },
        "suggested_rest_nodes": {
          "type": "array",
          "items": { "type": "string" }
        }
      }
    },
    "adjustment_suggestions": {
      "type": "array",
      "maxItems": 10,
      "items": {
        "type": "object",
        "required": ["priority", "category", "description"],
        "properties": {
          "priority": {
            "type": "string",
            "enum": ["critical", "high", "medium", "low"]
          },
          "category": {
            "type": "string",
            "enum": ["encounter_cr", "enemy_composition", "loot_balance", "rest_placement", "special_condition", "puzzle_difficulty"]
          },
          "description": { "type": "string" },
          "target_id": { "type": "string" },
          "current_value": {},
          "suggested_value": {},
          "rationale": { "type": "string" }
        }
      }
    },
    "party_specific_considerations": {
      "type": "object",
      "properties": {
        "vulnerability_flags": {
          "type": "array",
          "items": { "type": "string" }
        },
        "strength_synergies": {
          "type": "array",
          "items": { "type": "string" }
        },
        "no_healer_warning": { "type": "boolean" }
      }
    }
  }
}
```

#### 3.4.5 示例输出

```json
{
  "report_id": "bal_003_forgotten_corridor",
  "blueprint_id": "adv_003_forgotten_corridor",
  "generated_at": "2026-05-04T15:30:00Z",
  "overall_assessment": {
    "status": "minor_adjustments",
    "summary": "冒险蓝图的整体平衡性良好。石棺伏击对3级队伍偏硬，建议将animated_coffin从3具减少到2具。Boss战的幽影领主CR 4对3级无治疗队伍风险偏高，建议降低CR到3并提供环境治疗手段。战利品等级合理，与冒险难度匹配。",
    "risk_level": "moderate",
    "estimated_tpk_probability": 0.15
  },
  "encounter_analyses": [
    {
      "encounter_id": "enc_shadow_ambush",
      "encounter_name": "石棺伏击",
      "assigned_difficulty": "medium",
      "calculated_cr": 2.0,
      "recommended_cr": 1.5,
      "cr_deviation": 0.5,
      "assessment": "slightly_hard",
      "issues": ["3具animated_coffin + 2个shadow_servant对3级无治疗者队伍可能导致资源过度消耗"],
      "specific_dangers": ["黑暗环境中的伏击会导致第一轮队伍处于劣势"],
      "adjusted_cr_suggestion": 1.5,
      "adjustment_detail": "建议减少1具animated_coffin(从3到2)，将1个shadow_servant替换为1个低CR的environmental_hazard"
    },
    {
      "encounter_id": "enc_boss_shadow_lord",
      "encounter_name": "幽影领主·被封印者",
      "assigned_difficulty": "hard",
      "calculated_cr": 4.5,
      "recommended_cr": 3.0,
      "cr_deviation": 1.5,
      "assessment": "potentially_lethal",
      "issues": ["CR 4对3级队伍为deadly遭遇而非hard", "队伍缺乏治疗者，幽影领主的持续性暗影伤害可能快速导致减员"],
      "specific_dangers": ["仪式地面每2轮释放负能量脉冲会造成AoE伤害", "shadow_servant可能分散队伍对Boss的火力"],
      "adjusted_cr_suggestion": 3.0,
      "adjustment_detail": "建议将幽影领主CR从4降低到3(减少HP和攻击加值)，在地图两侧放置古代治疗之泉作为环境元素"
    }
  ],
  "difficulty_distribution_analysis": {
    "easy_count": 0,
    "medium_count": 1,
    "hard_count": 1,
    "deadly_count": 1,
    "recommended_ratio": "1:1:1:0 for short adventure",
    "actual_ratio": "0:1:1:1",
    "assessment": "deadly遭遇过多(3级队伍建议deadly<=1，且应为Boss遭遇)。Boss遭遇实际为deadly但被标记为hard，需要进行CR调整。"
  },
  "loot_analysis": {
    "loot_tier": 2,
    "expected_value": 450,
    "item_count": 5,
    "rarity_distribution": {
      "common": 3,
      "uncommon": 2,
      "rare": 0,
      "very_rare": 0,
      "legendary": 0
    },
    "assessment": "appropriate",
    "suggested_changes": []
  },
  "rest_analysis": {
    "rest_count": 1,
    "short_rest_count": 1,
    "distribution": "一个长休点在封门后，短休点不明确——建议在side_library添加短休标记",
    "assessment": "optimal",
    "risk_of_exhaustion": "low"
  },
  "adjustment_suggestions": [
    {
      "priority": "high",
      "category": "encounter_cr",
      "description": "降低enc_shadow_ambush的难度",
      "target_id": "enc_shadow_ambush",
      "current_value": {"animated_coffin_count": 3, "shadow_servant_count": 2},
      "suggested_value": {"animated_coffin_count": 2, "shadow_servant_count": 2},
      "rationale": "减少1具石棺可将CR从2.0降至1.5，与队伍等级匹配"
    },
    {
      "priority": "critical",
      "category": "encounter_cr",
      "description": "降低enc_boss_shadow_lord的CR",
      "target_id": "enc_boss_shadow_lord",
      "current_value": {"boss_cr": 4},
      "suggested_value": {"boss_cr": 3},
      "rationale": "CR 4对3级无治疗队伍造成TPK风险。CR 3仍然具有挑战性但不会过于致命。建议将HP从75降至55，攻击加值从+7降至+6。"
    },
    {
      "priority": "medium",
      "category": "special_condition",
      "description": "在Boss房间添加治疗机制",
      "target_id": "ritual_chamber",
      "current_value": {},
      "suggested_value": {"environmental_healing": "古代治疗之泉 x 2"},
      "rationale": "弥补队伍无治疗者的缺陷，但要求玩家主动移动到指定位置激活"
    }
  ],
  "party_specific_considerations": {
    "vulnerability_flags": [
      "无治疗者：队伍在连续战斗中没有HP恢复手段，每个遭遇的难度实际提升20%",
      "感知豁免弱：暗影生物可能使用的恐惧/魅惑效果对队伍威胁更大"
    ],
    "strength_synergies": [
      "前线坦克(索林)：可以有效应对大部分物理类型敌人",
      "爆发输出(莉莉丝+艾尔登)：如果先手优势，可以在首轮快速解决1-2个目标"
    ],
    "no_healer_warning": true
  }
}
```

#### 3.4.6 平衡验证规则（程序侧）

除LLM分析外，程序侧也执行硬性验证:

```csharp
public static class BalanceHardValidator
{
    /// <summary>
    /// 硬性规则验证 — 如果LLM漏检，程序兜底
    /// </summary>
    public static List<string> Validate(AdventureBlueprint blueprint, float partyLevel, int memberCount)
    {
        var errors = new List<string>();

        // 规则1: TPK检查 — 单遭遇CR不能超过 队伍等级 x 人数 x 1.8
        var maxSafeCr = partyLevel * memberCount * 1.8f;
        foreach (var encounter in blueprint.Encounters)
        {
            if (encounter.CalculatedCr > maxSafeCr)
            {
                errors.Add(string.Format("遭遇 {0} (CR: {1:F1}) 超过安全阈值 ({2:F1})",
                    encounter.EncounterId, encounter.CalculatedCr, maxSafeCr));
            }
        }

        // 规则2: 连续危险遭遇检查 — 两个hard+遭遇之间必须有rest节点
        var lastHardIndex = -1;
        for (int i = 0; i < blueprint.PlotOutline.Nodes.Count; i++)
        {
            var node = blueprint.PlotOutline.Nodes[i];
            if (node.Type is "combat" or "elite_combat" or "boss")
            {
                var enc = FindEncounter(blueprint, node.EncounterId);
                if (enc != null && enc.Difficulty is "hard" or "deadly")
                {
                    if (lastHardIndex != -1)
                    {
                        var hasRestBetween = false;
                        for (int j = lastHardIndex + 1; j < i; j++)
                        {
                            if (blueprint.PlotOutline.Nodes[j].Type == "rest")
                            {
                                hasRestBetween = true;
                                break;
                            }
                        }
                        if (!hasRestBetween)
                        {
                            errors.Add(string.Format("连续危险遭遇: {0} 和 {1} 之间缺少休息点",
                                blueprint.PlotOutline.Nodes[lastHardIndex].NodeId, node.NodeId));
                        }
                    }
                    lastHardIndex = i;
                }
            }
        }

        // 规则3: 战利品等级上限检查
        var maxLootTier = blueprint.Meta.Tier switch
        {
            "short" => 2,
            "medium" => 4,
            "long" => 5,
            _ => 5
        };
        if (blueprint.LootProfile.Tier > maxLootTier)
        {
            errors.Add(string.Format("战利品等级({0})超出冒险等级({1})上限({2})",
                blueprint.LootProfile.Tier, blueprint.Meta.Tier, maxLootTier));
        }

        // 规则4: 最少遭遇数检查
        var minEncounters = blueprint.Meta.Tier switch
        {
            "short" => 2,
            "medium" => 5,
            "long" => 10,
            _ => 2
        };
        if (blueprint.Encounters.Count < minEncounters)
        {
            errors.Add(string.Format("遭遇数量({0})不足，{1}冒险至少需要{2}场",
                blueprint.Encounters.Count, blueprint.Meta.Tier, minEncounters));
        }

        // 规则5: Boss必然存在检查 (非短冒险)
        if (blueprint.Meta.Tier != "short")
        {
            var hasBoss = false;
            foreach (var enc in blueprint.Encounters)
            {
                if (enc.Type == "boss_fight")
                {
                    hasBoss = true;
                    break;
                }
            }
            if (!hasBoss)
            {
                errors.Add(string.Format("{0}冒险缺少Boss遭遇", blueprint.Meta.Tier));
            }
        }

        return errors;
    }

    private static Encounter? FindEncounter(AdventureBlueprint blueprint, string encounterId)
    {
        foreach (var enc in blueprint.Encounters)
        {
            if (enc.EncounterId == encounterId)
                return enc;
        }
        return null;
    }
}
```

#### 3.4.7 综合配置

| 参数 | 值 |
|------|-----|
| 调用频率 | 每次冒险蓝图生成后自动1次 |
| Token预算 | 3000 input / 1000 output |
| 模型等级 | High Quality (GPT-4o / Claude 3.5 Sonnet) |
| 重试次数 | 2次 |
| 超时时间 | 30秒 |
| 优先级 | MEDIUM |
| 缓存策略 | 不缓存(每次必须重新验证) |
| 失败降级 | 程序化硬性规则检查 (见3.4.6) |


---

## 4. Schema 验证系统

### 4.1 验证管线

```
+----------------------------------------------------+
|                Schema Validation Pipeline           |
+----------------------------------------------------+
|                                                    |
|  LLM Raw Response (可能是纯JSON或Markdown包裹)      |
|        |                                           |
|        v                                           |
|  Step 1: JSON Extraction                           |
|    - 检测是否为纯JSON                                |
|    - 检测是否为Markdown代码块 (```json ... ```)     |
|    - 提取JSON字符串, 去除注释和尾逗号                 |
|    - 失败 -> 返回 INVALID_JSON_OUTPUT, 触发重试      |
|        |                                           |
|        v                                           |
|  Step 2: JSON Parsing                              |
|    - JSON.parse() -> 如果失败, 尝试修复:              |
|      * 补全缺失的引号                               |
|      * 修复尾部截断 (补全最后的 } ] 等)              |
|      * 将单引号替换为双引号                          |
|    - 失败 -> 返回 INVALID_JSON_OUTPUT                |
|        |                                           |
|        v                                           |
|  Step 3: Schema Validation                         |
|    - 使用对应的JSON Schema进行完整验证               |
|    - 使用 Newtonsoft.Json / ajv-json-schema 验证库                  |
|    - 失败 -> 收集所有验证错误, 附加到重试prompt      |
|        |                                           |
|        v                                           |
|  Step 4: Business Logic Validation                 |
|    - CR范围检查                                     |
|    - 数值范围检查 (HP, 等级等)                       |
|    - 逻辑一致性检查 (如: 节点间引用完整性)           |
|    - 枚举值检查                                     |
|    - 必填字段完整性检查                              |
|    - 失败 -> 返回 SCHEMA_VALIDATION_FAILED           |
|        |                                           |
|        v                                           |
|  Step 5: Post-Processing                           |
|    - 填充默认值 (缺失的非必填字段)                   |
|    - 类型强制转换                                    |
|    - 标准化ID格式                                   |
|        |                                           |
|        v                                           |
|  Valid AgentResponse returned                      |
|                                                    |
+----------------------------------------------------+
```

### 4.2 JSON提取实现

```csharp
public static class JSONExtractor
{
    /// <summary>
    /// 从LLM的原始响应中提取JSON对象
    /// </summary>
    public static JsonExtractResult Extract(string rawText)
    {
        // 尝试1: 纯JSON
        var stripped = rawText.Trim();
        if (stripped.StartsWith('{') || stripped.StartsWith('['))
        {
            var parseResult = TryParse(stripped);
            if (parseResult.Success)
                return parseResult;
        }

        // 尝试2: Markdown代码块
        var regex = new Regex(@"```(?:json)?\s*\n([\s\S]*?)\n```");
        var matches = regex.Matches(rawText);
        foreach (Match match in matches)
        {
            var code = match.Groups[1].Value;
            var parseResult = TryParse(code);
            if (parseResult.Success)
                return parseResult;
        }

        // 尝试3: 查找最外层的 { ... }
        var firstBrace = rawText.IndexOf('{');
        var lastBrace = rawText.LastIndexOf('}');
        if (firstBrace != -1 && lastBrace > firstBrace)
        {
            var inner = rawText.Substring(firstBrace, lastBrace - firstBrace + 1);
            var parseResult = TryParse(inner);
            if (parseResult.Success)
                return parseResult;
        }

        return new JsonExtractResult(false, null!, "Could not extract valid JSON from response");
    }

    private static JsonExtractResult TryParse(string text)
    {
        try
        {
            var json = JsonSerializer.Deserialize<JsonElement>(text);
            return new JsonExtractResult(true, json, "");
        }
        catch (JsonException)
        {
            // 尝试修复常见问题
            // 移除尾逗号
            var fixed_ = Regex.Replace(text, @",(\s*[}\]])", "$1");

            try
            {
                var json = JsonSerializer.Deserialize<JsonElement>(fixed_);
                return new JsonExtractResult(true, json, "");
            }
            catch (JsonException ex)
            {
                return new JsonExtractResult(false, default, ex.Message);
            }
        }
    }
}

public record JsonExtractResult(bool Success, JsonElement Json, string Error);
```

### 4.3 Schema验证器实现

```csharp
public class SchemaValidator
{
    private readonly Dictionary<string, JsonSchema> _schemas = new();  // {agent_id: JSONSchemaObject}

    public SchemaValidator()
    {
        LoadSchemas();
    }

    private void LoadSchemas()
    {
        _schemas["screenwriter"] = ParseSchemaFile("schemas/adventure_blueprint.json");
        _schemas["dm_agent"] = ParseSchemaFile("schemas/dm_agent_output.json");
        _schemas["copywriter"] = ParseSchemaFile("schemas/copywriter_output.json");
        _schemas["balancer"] = ParseSchemaFile("schemas/balance_report.json");
    }

    /// <summary>
    /// 验证Agent输出是否符合Schema
    /// </summary>
    public ValidationResult Validate(string agentId, JsonElement data)
    {
        if (!_schemas.TryGetValue(agentId, out var schema))
            return new ValidationResult(false, new List<string> { "Unknown agent: " + agentId });

        var errors = new List<string>();

        // JSON Schema验证
        var schemaErrors = ValidateJsonSchema(schema, data);
        if (schemaErrors.Count > 0)
        {
            errors.AddRange(schemaErrors);
            // 收集前5个错误用于重试提示
            var retryInfo = FormatSchemaErrorsForRetry(schemaErrors.Take(5).ToList());
            return new ValidationResult(false, errors, retryInfo);
        }

        // Business Logic验证
        var bizErrors = ValidateBusinessRules(agentId, data);
        if (bizErrors.Count > 0)
        {
            errors.AddRange(bizErrors);
            return new ValidationResult(false, errors, "");
        }

        return new ValidationResult(true, new List<string>());
    }

    private static JsonSchema ParseSchemaFile(string path) { /* 从文件加载JSON Schema */ return null!; }
    private static List<string> ValidateJsonSchema(JsonSchema schema, JsonElement data) { return new(); }
    private static List<string> ValidateBusinessRules(string agentId, JsonElement data) { return new(); }
    private static string FormatSchemaErrorsForRetry(List<string> errors) { return string.Join("\n", errors); }
}

public record ValidationResult(bool IsValid, List<string> Errors, string RetryInfo = "");
```

### 4.4 业务逻辑验证规则

除JSON Schema外，以下规则由程序强制执行:

```
Screenwriter (adventure_blueprint):
  [check] 所有node_id唯一，无重复
  [check] 所有choice.target_node指向实际存在的node_id
  [check] 所有encounter_id引用存在于encounters数组中
  [check] CR范围: min <= max
  [check] encounter_count各难度之和 ~= total_encounters
  [check] 短冒险节点数: 5-8; 中冒险: 15-25; 长冒险: 30-50
  [check] 至少有一个节点type为"boss"或"climax"
  [check] 主题标签非空且>=2
  [check] 每个NPC至少关联一个节点
  [check] 冒险时长在tier对应的合理范围内

DM Agent:
  [check] narrative长度 <= 500字符
  [check] dialogue_options的数量 <= 4
  [check] 如果是npc_dialogue类型，必须有npc_dialogue字段
  [check] 如果是combat_narration类型，必须有combat_flourish字段
  [check] response_type与输出结构一致
  [check] flavor_text长度 <= 80字符

Copywriter:
  [check] flavor_text/ambient_text长度 <= 200字符
  [check] 必填字段完整
  [check] name长度 <= 20字符
  [check] mood/enum使用了有效枚举值

Balancer:
  [check] report_id格式正确
  [check] overall_assessment.status为有效枚举值
  [check] encounter_analyses覆盖所有遭遇
  [check] estimated_tpk_probability在0-1之间
  [check] 每个encounter_analysis有对应的调整建议(如果assessment不是well_balanced)
```

### 4.5 验证失败后的处理

```
流程:

1. 验证失败(Step 3 - Schema) ->
   +------------------------------------------+
   | 收集所有验证错误                          |
   | 格式化为:                                |
   | "Your previous response failed JSON      |
   |  Schema validation. Errors:              |
   |  - missing required property 'nodes'     |
   |  - 'cr_range.max' must be number         |
   |  Please fix and resubmit."              |
   | -> 追加到enhanced_retry_prompt            |
   | -> 重新调用LLM (消耗一次重试)              |
   +------------------------------------------+

2. 验证失败(Step 4 - Business Logic) ->
   +------------------------------------------+
   | 收集所有业务错误                          |
   | 如果是可自动修复的(如: node_id不唯一)     |
   |   -> 程序自动修正(重命名重复ID)           |
   | 如果是不可自动修复的(如: 缺少Boss遭遇)   |
   |   -> 格式化为retry prompt -> 重试          |
   | 如果重试耗尽 -> Fallback (见第8章)        |
   +------------------------------------------+

3. 重试耗尽 ->
   +------------------------------------------+
   | 尝试备用模型 (Fallback Chain)             |
   | 如果仍失败 -> 返回Cached结果              |
   | 如果无缓存 -> 返回Static Template         |
   | 在debug模式下标记: "此内容由模板生成"     |
   +------------------------------------------+
```

---

## 5. Token 预算管理

> **设计说明 (v1.1)**: Token预算限制暂时移除。在MVP阶段，不对LLM调用设置Token上限，以确保叙事体验的完整性。后续版本将根据实际使用数据重新引入合理的预算限制。

### 5.1 Per-Adventure Token Accounting

每次冒险维护一个Token Budget账本:

```csharp
public class TokenBudgetManager
{
    /// <summary>
    /// 每次冒险的Token预算分配与管理
    /// </summary>

    // 单次冒险的Token硬上限
    private readonly Dictionary<string, Dictionary<string, int>> _adventureBudgets = new()
    {
        ["short"] = new() { ["total"] = 20000, ["screenwriter"] = 6000, ["dm_agent"] = 8000, ["copywriter"] = 4000, ["balancer"] = 4000 },
        ["medium"] = new() { ["total"] = 60000, ["screenwriter"] = 7000, ["dm_agent"] = 35000, ["copywriter"] = 10000, ["balancer"] = 5000 },
        ["long"] = new() { ["total"] = 140000, ["screenwriter"] = 8000, ["dm_agent"] = 90000, ["copywriter"] = 25000, ["balancer"] = 6000 },
    };

    private readonly Dictionary<string, int> _currentBudgets = new();  // {agent_id: remaining_tokens}
    private int _totalConsumed;
    private int _totalBudget;

    /// <summary>
    /// 初始化冒险的Token预算
    /// </summary>
    public void InitAdventure(string tier)
    {
        var budget = _adventureBudgets[tier];
        _totalBudget = budget["total"];
        _currentBudgets.Clear();
        _currentBudgets["screenwriter"] = budget["screenwriter"];
        _currentBudgets["dm_agent"] = budget["dm_agent"];
        _currentBudgets["copywriter"] = budget["copywriter"];
        _currentBudgets["balancer"] = budget["balancer"];
    }

    /// <summary>
    /// 检查是否有足够Token，如果有则预留。
    /// 返回: BudgetResult {Allowed, Remaining, WarningLevel}
    /// </summary>
    public BudgetResult CheckAndReserve(string agentId, int estimatedTokens)
    {
        if (!_currentBudgets.ContainsKey(agentId))
            return new BudgetResult(false, 0, "unknown_agent");

        var remaining = _currentBudgets[agentId];
        var totalRemaining = _totalBudget - _totalConsumed;

        // 检查Agent预算
        if (estimatedTokens > remaining)
        {
            // 尝试从其他Agent借用(不超过10%)
            var borrowLimit = (int)(_currentBudgets[agentId] * 0.1);
            var borrowed = Math.Min(estimatedTokens - remaining, borrowLimit);
            if (borrowed > 0 && remaining + borrowed >= estimatedTokens)
                remaining += borrowed;
            else
                return new BudgetResult(false, remaining, "agent_budget_exceeded");
        }

        // 检查总预算
        if (estimatedTokens > totalRemaining)
            return new BudgetResult(false, totalRemaining, "total_budget_exceeded");

        // 预留Token
        _currentBudgets[agentId] -= estimatedTokens;
        _totalConsumed += estimatedTokens;

        // 警告级别
        var warning = "";
        var pct = (float)remaining / _adventureBudgets[GetCurrentTier()][agentId] * 100;
        if (pct < 10)
            warning = "critical";  // 剩余<10%
        else if (pct < 25)
            warning = "warning";   // 剩余<25%

        return new BudgetResult(true, _currentBudgets[agentId], warning);
    }

    private string GetCurrentTier() { return "short"; }  // 简化实现
}

public record BudgetResult(bool Allowed, int Remaining, string WarningLevel);
```

### 5.2 Token预估算法

在发送请求前，预估Token消耗:

```csharp
public static class TokenEstimator
{
    /// <summary>
    /// 基于prompt长度估算Token数
    /// </summary>

    // 粗略估算: 1 token ~ 0.75个中文字符 ~ 4个英文字符
    private const double CHINESE_CHARS_PER_TOKEN = 0.75;
    private const double ENGLISH_CHARS_PER_TOKEN = 4.0;

    /// <summary>
    /// 估算输入文本的Token数
    /// </summary>
    public static int EstimateInputTokens(string promptText)
    {
        var chineseCount = 0;
        var englishCount = 0;
        var otherCount = 0;

        foreach (var ch in promptText)
        {
            var code = (int)ch;
            if (code >= 0x4E00 && code <= 0x9FFF)           // CJK统一汉字
                chineseCount++;
            else if ((code >= 0x41 && code <= 0x5A) || (code >= 0x61 && code <= 0x7A))
                englishCount++;
            else
                otherCount++;
        }

        var estimated = (int)(chineseCount / CHINESE_CHARS_PER_TOKEN);
        estimated += (int)(englishCount / ENGLISH_CHARS_PER_TOKEN);
        estimated += (int)(otherCount / ENGLISH_CHARS_PER_TOKEN);
        return estimated;
    }

    /// <summary>
    /// 估算输出文本的Token数
    /// </summary>
    public static int EstimateOutputTokens(int expectedOutputLength)
    {
        return (int)(expectedOutputLength / CHINESE_CHARS_PER_TOKEN);
    }
}
```

### 5.3 动态预算调整

根据冒险中的实际消耗动态调整:

```
DM Agent的动态预算策略:
  每一步动作: 理论消耗2500 tokens
  实际消耗: 根据实时统计调整

  如果前5次调用平均消耗 < 2000 tokens:
    -> 放宽DM Agent的per-call预算到2500 tokens
    -> 允许更丰富的描述

  如果前5次调用平均消耗 > 2800 tokens:
    -> 收紧DM Agent的per-call预算到1800 tokens
    -> 减少上下文注入量 (最近动作从5->3个)
    -> 提高adventure_summary的压缩率

  预算溢出处理:
    DM Agent剩余预算 < 2000 tokens 时:
      -> 切换到"紧凑模式": 减少narrative长度上限从500->300
      -> 触发summary regeneration (压缩adventure_summary)
      -> 如果仍不够 -> 禁用DM Agent, 使用静态模板
```

### 5.4 成本估算公式

```
成本 = input_tokens x input_price_per_1k + output_tokens x output_price_per_1k

各模型价格 (2024年参考):

| 模型                    | Input ($/1K tokens) | Output ($/1K tokens) |
|------------------------|---------------------|----------------------|
| GPT-4o                 | $2.50              | $10.00              |
| GPT-4o-mini            | $0.15              | $0.60               |
| GPT-3.5-turbo          | $0.50              | $1.50               |
| Claude 3.5 Sonnet      | $3.00              | $15.00              |
| Claude 3 Haiku         | $0.25              | $1.25               |
| 国产模型 (DeepSeek V3) | $0.14              | $0.28               |

预估算例 (短冒险, 使用GPT-4o为主模型):

  编剧Agent:
    input: 4000 tokens x $2.50/1K = $0.0100
    output: 2000 tokens x $10.00/1K = $0.0200
    小计: $0.0300

  DM Agent (25次调用):
    input: 25 x 2000 x $2.50/1K = $0.1250
    output: 25 x 500 x $10.00/1K = $0.1250
    小计: $0.2500

  文案Agent (10次调用):
    input: 10 x 1000 x $2.50/1K = $0.0250
    output: 10 x 300 x $10.00/1K = $0.0300
    小计: $0.0550

  平衡Agent:
    input: 3000 x $2.50/1K = $0.0075
    output: 1000 x $10.00/1K = $0.0100
    小计: $0.0175

  总计: ~$0.35 / 短冒险

实际建议: 使用GPT-4o-mini作为DM/文案的默认模型，成本可降低到~$0.05/短冒险
```

### 5.5 总成本控制

```csharp
public class CostController
{
    private readonly float _hardCapPerSession = 5.00f;   // 单次游戏会话硬上限(美元)
    private readonly float _softWarningThreshold = 3.00f; // 软警告阈值

    private float _currentSessionCost;
    private readonly List<float> _adventureCosts = new();

    public event Action<float>? HardCapReached;
    public event Action<float>? SoftWarning;

    /// <summary>
    /// 记录一次API调用的成本
    /// </summary>
    public void RecordCost(string agentId, string model, int inputTokens, int outputTokens)
    {
        var cost = CalculateCost(model, inputTokens, outputTokens);
        _currentSessionCost += cost;
        _adventureCosts.Add(cost);

        if (_currentSessionCost >= _hardCapPerSession)
        {
            // 触发硬上限: 禁用所有LLM调用，强制使用离线模式
            LLMGateway.EnableFallbackMode();
            HardCapReached?.Invoke(_currentSessionCost);
        }
        else if (_currentSessionCost >= _softWarningThreshold)
        {
            // 触发软警告: 降低模型等级
            SoftWarning?.Invoke(_currentSessionCost);
            LLMGateway.DowngradeModels();
        }
    }

    private static float CalculateCost(string model, int inputTokens, int outputTokens)
    {
        var prices = new Dictionary<string, (double Input, double Output)>
        {
            ["gpt-4o"] = (2.50, 10.00),
            ["gpt-4o-mini"] = (0.15, 0.60),
            ["gpt-3.5-turbo"] = (0.50, 1.50),
            ["claude-3.5-sonnet"] = (3.00, 15.00),
            ["claude-3-haiku"] = (0.25, 1.25),
            ["deepseek-v3"] = (0.14, 0.28),
        };

        var p = prices.GetValueOrDefault(model, (1.0, 4.0));
        return (float)(inputTokens / 1000.0 * p.Input + outputTokens / 1000.0 * p.Output);
    }
}
```

---

## 6. 缓存系统

### 6.1 缓存架构

```
+-----------------------------------------------------+
|                    Cache Architecture                 |
+-----------------------------------------------------+
|                                                     |
|  Request Context                                    |
|       |                                             |
|       v                                             |
|  SemanticHashGenerator                              |
|    - 从请求中提取语义特征                             |
|    - 生成SHA-256哈希作为缓存Key                       |
|       |                                             |
|       v                                             |
|  SQLite Cache DB                                    |
|    +---------------------------------------+        |
|    | cache_entries TABLE:                  |        |
|    |                                       |        |
|    | hash_key      TEXT PRIMARY KEY        |        |
|    | agent_id      TEXT NOT NULL           |        |
|    | context_hash  TEXT NOT NULL           |        |
|    | response_json TEXT NOT NULL (压缩)     |        |
|    | created_at    INTEGER (unix ts)       |        |
|    | last_accessed INTEGER (unix ts)       |        |
|    | hit_count     INTEGER DEFAULT 1       |        |
|    | token_saved   INTEGER                 |        |
|    | expires_at    INTEGER                 |        |
|    +---------------------------------------+        |
|                                                     |
|  LRU Eviction Policy                                |
|    - 最大存储: 500MB                                 |
|    - 最大条目: 10000                                 |
|    - 过期清理: 每100次访问触发一次                     |
|    - 淘汰策略: 删除最久未访问的条目                     |
|                                                     |
+-----------------------------------------------------+
```

### 6.2 语义哈希算法

```csharp
public static class SemanticHashGenerator
{
    /// <summary>
    /// 从请求上下文中生成语义哈希作为缓存Key
    /// </summary>
    public static string Generate(string agentId, Dictionary<string, object> context)
    {
        var features = new List<string>();

        switch (agentId)
        {
            case "screenwriter":
                // 特征: tier + theme + avg_level + party_composition_hash
                var advConfig = GetValueOrDefault<Dictionary<string, object>>(context, "adventure_config");
                features.Add(GetValueOrDefault<string>(advConfig, "tier", ""));
                features.Add(GetValueOrDefault<string>(advConfig, "forced_theme", ""));
                var partyState = GetValueOrDefault<Dictionary<string, object>>(context, "party_state");
                features.Add(GetValueOrDefault<double>(partyState, "average_level", 0).ToString());
                // 队伍组成哈希 (忽略具体character_id，只关注组成模式)
                var comp = ExtractPartyComposition(partyState);
                features.Add(comp);
                break;

            case "dm_agent":
                // 特征: node_type + action_type + result_type
                var sceneCtx = GetValueOrDefault<Dictionary<string, object>>(context, "scene_context");
                var currentNode = GetValueOrDefault<Dictionary<string, object>>(sceneCtx, "current_node");
                features.Add(GetValueOrDefault<string>(currentNode, "type", ""));
                features.Add(GetValueOrDefault<string>(context, "request_type", ""));
                var actionCtx = GetValueOrDefault<Dictionary<string, object>>(context, "action_context");
                var rollResult = GetValueOrDefault<Dictionary<string, object>>(actionCtx, "roll_result");
                features.Add(GetValueOrDefault<bool>(rollResult, "success", false).ToString());
                features.Add(GetValueOrDefault<bool>(rollResult, "critical", false).ToString());
                break;

            case "copywriter":
                // 特征: content_type + item_type/rarity/mood
                features.Add(GetValueOrDefault<string>(context, "content_type", ""));
                var itemCtx = GetValueOrDefault<Dictionary<string, object>>(
                    GetValueOrDefault<Dictionary<string, object>>(context, "context"),
                    "item_context");
                features.Add(GetValueOrDefault<string>(itemCtx, "item_type", ""));
                features.Add(GetValueOrDefault<string>(itemCtx, "rarity", ""));
                break;

            case "balancer":
                // 不缓存
                return "";
        }

        // 组合特征并生成SHA-256
        var featureString = agentId + "||" + string.Join("|", features);
        return ComputeSha256Hash(featureString);
    }

    private static string ExtractPartyComposition(Dictionary<string, object>? partyState)
    {
        // 提取队伍组成模式 (忽略具体身份)
        var classes = new List<string>();
        var members = GetValueOrDefault<List<object>>(partyState, "members");
        if (members != null)
        {
            foreach (var m in members)
            {
                var member = m as Dictionary<string, object>;
                classes.Add(GetValueOrDefault<string>(member, "class", "unknown"));
            }
        }
        classes.Sort();
        return string.Join("|", classes);
    }

    private static T? GetValueOrDefault<T>(Dictionary<string, object>? dict, string key, T? defaultValue = default)
    {
        if (dict != null && dict.TryGetValue(key, out var val) && val is T typedVal)
            return typedVal;
        return defaultValue;
    }

    private static string ComputeSha256Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }
}
```

### 6.3 缓存失效规则

```
缓存失效条件:

1. 时间过期:
   - 冒险蓝图缓存: 72小时 (相同队伍+相同等级可能得到更好结果)
   - 战斗叙述缓存: 24小时
   - 物品描述缓存: 168小时 (7天，物品描述变化小)
   - NPC描述缓存: 168小时

2. 版本变更:
   - Schema版本变更 -> 所有缓存失效
   - System Prompt变更 -> 对应Agent的缓存失效
   - 游戏版本升级 -> 全部缓存失效

3. 手动清理:
   - 玩家在设置中选择"清除LLM缓存"
   - 开发者模式强制刷新
```

### 6.4 缓存命中率预估

| Agent | 缓存粒度 | 预估命中率 | 说明 |
|-------|---------|-----------|------|
| 编剧Agent (short) | tier+theme+level+comp | 40% | 短冒险模板组合有限 |
| 编剧Agent (medium/long) | tier+theme+level | 15% | 组合更多样 |
| DM Agent (combat) | node_type+action+result | 60% | 战斗模式相对固定 |
| DM Agent (dialogue) | npc_type+mood | 30% | NPC组合多变 |
| 文案Agent (item) | item_type+rarity | 75% | 物品描述模式化 |
| 文案Agent (environment) | env_type+mood | 50% | 环境组合有限 |
| 平衡Agent | - | 0% | 不缓存 |

### 6.5 缓存存储限制与淘汰策略

```
存储配置:
  最大磁盘占用: 500MB
  最大条目数: 10000
  单个条目最大: 100KB (超过此大小的响应不缓存)

淘汰策略 (LRU):
  1. 定时清理: 每100次缓存操作触发一次
  2. 过期检查: 删除所有 expires_at < now() 的条目
  3. 大小检查: 如果总大小 > 500MB 或 条目数 > 10000:
     - 按 last_accessed 升序排列
     - 删除最旧的条目，直到满足限制

SQLite实现:
  CREATE TABLE IF NOT EXISTS cache_entries (
    hash_key TEXT PRIMARY KEY,
    agent_id TEXT NOT NULL,
    context_hash TEXT NOT NULL,
    response_json TEXT NOT NULL,
    created_at INTEGER NOT NULL,
    last_accessed INTEGER NOT NULL,
    hit_count INTEGER DEFAULT 1,
    token_saved INTEGER DEFAULT 0,
    expires_at INTEGER NOT NULL
  );

  CREATE INDEX idx_agent_id ON cache_entries(agent_id);
  CREATE INDEX idx_expires_at ON cache_entries(expires_at);
  CREATE INDEX idx_last_accessed ON cache_entries(last_accessed);
```

---

## 7. 上下文窗口管理

### 7.1 滑动窗口策略 (DM Agent)

DM Agent使用滑动窗口管理上下文:

```
Sliding Window for DM Agent:

  Recent History (窗口大小: 5个动作)
  +---+---+---+---+---+
  |   |   |   |   |   |  <- 每个动作 (action + result_summary)
  +---+---+---+---+---+
    ^                   ^
    oldest              newest
    当新动作到来时，最旧的被移出窗口

  移出窗口的动作 -> 进入Summary Buffer
  +-------------------------------------------+
  | Adventure Summary (最大800字符)            |
  | 由SummaryGenerator定期从Buffer重新生成     |
  | 压缩率: 10:1 (10个动作 -> 1句摘要)         |
  +-------------------------------------------+
```

### 7.2 优先级上下文裁剪

当上下文超出Token预算时，按优先级裁剪:

```
裁剪优先级 (从低到高, 越低越先被裁):

  Priority 1 (最先被裁) — 历史动作详情
    - 超过5个的旧动作 -> 移入Summary Buffer
    - 如果超出预算，5个->3个

  Priority 2 — 队伍详细状态
    - 从详细HP -> HP百分比
    - 状态效果从完整列表 -> 只有显著效果(恐惧/眩晕/专注)

  Priority 3 — NPC信息
    - 超过3个NPC -> 只保留与当前节点关联的
    - NPC详情从完整 -> 只保留名字+当前情绪

  Priority 4 — 环境描述
    - 缩减环境字段: 保留lighting + weather, 删除tileset/ambient_sounds

  Priority 5 — Adventure Summary
    - 如果需要: 重新压缩为更短的版本 (400 -> 200字)

  Priority 6 (最后被裁) — System Prompt
    - 不可裁剪 (但可通过pre-compiled方式预注入)
```

### 7.3 摘要生成策略

```csharp
public static class SummaryGenerator
{
    /// <summary>
    /// 生成冒险进展摘要
    /// </summary>

    // 摘要触发阈值:
    //   DM Agent调用 > 15次 -> 生成初始摘要
    //   DM Agent调用 > 每15次 -> 重新生成(增量)
    //   上下文预算剩余 < 30% -> 强制重新生成

    /// <summary>
    /// 输入: 冒险至今的关键事件列表
    /// 输出: 200-400字的压缩摘要
    /// </summary>
    public static string GenerateSummary(List<Dictionary<string, object>> adventureHistory)
    {
        // 选择关键事件:
        //   - 剧情节点通过 (node_type != combat)
        //   - 重大战斗结果 (boss defeated, player downed)
        //   - NPC关系变化
        //   - 关键物品获得

        var keyEvents = new List<string>();
        foreach (var event_ in adventureHistory)
        {
            if (event_.GetValueOrDefault("significance", 0) is int sig && sig >= 3)  // 只保留重要事件
            {
                var summary = event_.GetValueOrDefault("summary", "") as string ?? "";
                keyEvents.Add(summary);
            }
        }

        // 构建摘要模板
        var summaryBuilder = new System.Text.StringBuilder("冒险进展: ");
        var sliced = keyEvents.Take(10);  // 最多10个关键事件
        foreach (var eventText in sliced)
        {
            summaryBuilder.Append(eventText);
            summaryBuilder.Append("; ");
        }
        summaryBuilder.Append("当前状态: ");
        summaryBuilder.Append(GetCurrentStatus());

        var summaryText = summaryBuilder.ToString();

        // 如果超出400字，使用最小化版本
        if (summaryText.Length > 400)
        {
            summaryText = summaryText[..397] + "...";
        }

        return summaryText;
    }

    private static string GetCurrentStatus()
    {
        // 从全局状态获取
        return "队伍正在探索中";
    }
}
```

### 7.4 各Agent上下文窗口

| Agent | 上下文窗口(input tokens) | 输出限制 | 上下文管理策略 |
|-------|------------------------|---------|--------------|
| 编剧Agent | 4000 | 2000 | 一次性注入全部上下文(队伍+世界状态+配置) |
| DM Agent | 2000 | 500 | 滑动窗口: 5个动作 + 冒险摘要 + 场景信息 |
| 文案Agent | 1000 | 300 | 最小化上下文: 只有类型 + 关键属性 |
| 平衡Agent | 3000 | 1000 | 一次性注入完整蓝图 + 队伍组成 |

---

## 8. 离线降级方案

### 8.1 模板目录结构

```
fallback_templates/
+-- scene_descriptions/
|   +-- dungeon_entrance.json
|   +-- dungeon_corridor.json
|   +-- dungeon_hall.json
|   +-- forest_path.json
|   +-- forest_clearing.json
|   +-- cave_entrance.json
|   +-- cave_depth.json
|   +-- ruined_temple.json
|   +-- town_square.json
|   +-- boss_chamber.json
|   +-- camp_site.json
|   +-- underground_river.json
|   +-- ancient_library.json
|   +-- throne_room.json
|   +-- graveyard.json       (15个)
|
+-- npc_dialogues/
|   +-- quest_giver/
|   |   +-- greeting.json
|   |   +-- accept.json
|   |   +-- refuse.json
|   |   +-- reward.json
|   +-- merchant/
|   |   +-- greeting.json
|   |   +-- browse.json
|   |   +-- purchase.json
|   |   +-- leave.json
|   +-- hostile/
|   |   +-- taunt.json
|   |   +-- threaten.json
|   |   +-- surrender.json
|   +-- ally/
|   |   +-- encouragement.json
|   |   +-- warning.json
|   +-- sage/
|   |   +-- hint.json
|   |   +-- exposition.json    (16个)
|
+-- combat_descriptions/
|   +-- melee_attack/
|   |   +-- hit.json
|   |   +-- miss.json
|   |   +-- critical_hit.json
|   |   +-- kill.json
|   +-- ranged_attack/
|   |   +-- hit.json
|   |   +-- miss.json
|   |   +-- critical_hit.json
|   +-- spell_cast/
|   |   +-- fire_damage.json
|   |   +-- ice_damage.json
|   |   +-- lightning_damage.json
|   |   +-- necrotic_damage.json
|   |   +-- healing.json
|   |   +-- buff.json
|   |   +-- debuff.json       (14个)
|
+-- item_descriptions/
|   +-- weapon/
|   |   +-- sword_common.json
|   |   +-- sword_uncommon.json
|   |   +-- axe_common.json
|   +-- armor/
|   |   +-- light_common.json
|   |   +-- medium_uncommon.json
|   +-- potion/
|   |   +-- healing.json
|   |   +-- mana.json
|   +-- scroll/
|   |   +-- common.json
|   +-- ring_amulet/
|       +-- common.json
|       +-- uncommon.json      (11个)
|
+-- event_outcomes/
|   +-- success/
|   |   +-- skill_check.json
|   |   +-- persuasion.json
|   |   +-- stealth.json
|   +-- failure/
|   |   +-- skill_check.json
|   |   +-- combat_defeat.json
|   |   +-- trap_triggered.json
|   +-- neutral/
|       +-- time_passes.json
|       +-- weather_change.json (8个)
```

总计: **64个模板** (超过50+的要求)

### 8.2 模板内容示例

**Template: combat_descriptions/melee_attack/critical_hit.json**

```json
{
  "template_id": "combat_melee_crit",
  "category": "combat",
  "subcategory": "melee_attack",
  "result_type": "critical_hit",
  "templates": [
    "{attacker_name}发出一声战吼，{weapon_name}携万钧之力砸向{target_name}——正中要害！",
    "{attacker_name}看准{target_name}的破绽，{weapon_name}划出一道致命弧线！",
    "精准一击！{attacker_name}的{weapon_name}刺穿了{target_name}的防御。",
    "{attacker_name}使出全力一击，{weapon_name}爆发出耀眼的光芒！"
  ],
  "variables": ["attacker_name", "weapon_name", "target_name"],
  "tone": "heroic",
  "min_player_level": 1
}
```

**Template: npc_dialogues/quest_giver/greeting.json**

```json
{
  "template_id": "npc_dialogue_quest_greeting",
  "category": "npc_dialogue",
  "subcategory": "quest_giver",
  "result_type": "greeting",
  "templates": [
    "「冒险者们，你们来了。」{npc_name}{expression}，{npc_greeting_action}。",
    "{npc_name}抬头看向你们，{expression_description}。「我有一件事需要你们的帮助。」",
    "「你们就是酒馆里能办事的人吧？」{npc_name}{expression_description}。",
    "{npc_name}从椅子上站起身，{expression_description}。「请坐，我们谈谈。」"
  ],
  "variables": ["npc_name", "expression", "np_greeting_action", "expression_description"],
  "tone": "neutral"
}
```

### 8.3 模板选择算法

```csharp
public class FallbackManager
{
    private readonly Dictionary<string, object> _templates = new();
    private readonly Dictionary<string, int> _templateWeights = new();  // 跟踪模板使用频率

    /// <summary>
    /// 根据上下文选择最合适的离线模板
    /// </summary>
    public Template SelectTemplate(string category, Dictionary<string, object> context)
    {
        var candidates = GetCandidates(category, context);
        if (candidates.Count == 0)
            return GetGenericTemplate(category);

        // 加权随机选择:
        //   - 最近使用过的模板权重降低 (避免重复)
        //   - 与上下文匹配度高的模板权重提高
        var weighted = new List<(Template Template, double Weight)>();
        foreach (var t in candidates)
        {
            var weight = 1.0;
            // 衰减最近使用的
            if (_templateWeights.ContainsKey(t.TemplateId))
                weight *= 0.5;
            // 匹配度加分
            if (t.Tone == context.GetValueOrDefault("tone", "") as string)
                weight *= 1.5;
            if (t.MinPlayerLevel <= (context.GetValueOrDefault("player_level", 1) is int level ? level : 1))
                weight *= 1.0;
            else
                weight *= 0.5;
            weighted.Add((t, weight));
        }

        // 加权随机选择
        var totalWeight = 0.0;
        foreach (var w in weighted)
            totalWeight += w.Weight;
        var roll = Random.Shared.NextDouble() * totalWeight;
        var current = 0.0;
        foreach (var w in weighted)
        {
            current += w.Weight;
            if (roll <= current)
            {
                RecordUsage(w.Template.TemplateId);
                return w.Template;
            }
        }

        return weighted[0].Template;
    }

    /// <summary>
    /// 将变量填充到模板中
    /// </summary>
    public string PopulateTemplate(Template template, Dictionary<string, string> variables)
    {
        // 随机选择一个模板变体
        var text = template.Templates[Random.Shared.Next(template.Templates.Count)];

        // 替换所有变量
        foreach (var varName in template.Variables)
        {
            var value = variables.GetValueOrDefault(varName, "???");
            text = text.Replace("{" + varName + "}", value);
        }

        return text;
    }

    private void RecordUsage(string templateId)
    {
        if (!_templateWeights.ContainsKey(templateId))
            _templateWeights[templateId] = 0;
        _templateWeights[templateId]++;
    }

    private List<Template> GetCandidates(string category, Dictionary<string, object> context) { return new(); }
    private Template GetGenericTemplate(string category) { return new Template(); }
}

public class Template
{
    public string TemplateId { get; init; } = "";
    public string Tone { get; init; } = "";
    public int MinPlayerLevel { get; init; }
    public List<string> Templates { get; init; } = new();
    public List<string> Variables { get; init; } = new();
}
```

### 8.4 质量对比: LLM vs 模板

| 维度 | LLM | 模板 |
|------|-----|------|
| 叙事连贯性 | 高 — 能根据上下文生成连贯叙事 | 中 — 模板之间可能有断裂感 |
| 文本多样性 | 高 — 几乎每次不同 | 低 — 模板池有限，会重复 |
| 上下文敏感度 | 高 — 能正确引用角色/地点 | 低 — 只能用通用变量替换 |
| 情感表达 | 高 — 能微妙地调整语气 | 中 — 模板预设了语气范围 |
| 响应速度 | 0.5-2秒 | <0.01秒 (即时) |
| API依赖 | 完全依赖 | 零依赖 |
| 成本 | $0.05-0.35/冒险 | $0 |
| 适用场景 | 核心叙事+首次体验 | 辅助描述+离线模式+重复场景 |

**离线模式下的体验影响**:

```
保留的核心体验:
  [check] DND 5e完整战斗规则
  [check] 骰子检定系统
  [check] 角色成长与装备
  [check] 冒险地图探索
  [check] 分支路线选择
  [check] NPC功能交互(商人/任务)

降级的体验:
  [x] 场景描述 -> 模板化 (但仍有氛围)
  [x] NPC对话 -> 预设台词+变量替换
  [x] 战斗叙述 -> 模板组合 (攻击/命中/暴击各有模板)
  [x] 物品描述 -> 模板化 (但物品数值/稀有度仍是真实的)
  [x] 冒险蓝图 -> 使用预定义的50+剧情模板

完全丢失:
  [x] 完全独特的冒险剧本 (离线时使用模板化的固定剧情)
  [x] 角色关系的细腻叙事 (关系数值变化仍发生，但缺乏叙事包装)
  [x] 世界状态演进的个性化描述
```

---

## 9. 多模型切换策略

> **设计说明 (v1.1) — MVP简化**:
>
> MVP阶段简化为 **1 tier × 2 fallback** 模式：
> - **Primary**: GPT-4o-mini（所有Agent共用）
> - **Fallback**: Claude 3 Haiku（不同供应商，确保供应商故障时有备选）
> - **最终Fallback**: 静态模板
>
> 原始的4 tier × 4 fallback设计保留为参考，待MVP验证后根据实际需求扩展。

### 9.1 品质等级定义

| 等级 | 标签 | 适用模型 | 适用Agent | 特点 |
|------|------|---------|-----------|------|
| High Quality | HQ | GPT-4o, Claude 3.5 Sonnet | 编剧, 平衡 | 最强理解力, 最贵, 最慢 |
| Medium Quality | MQ | GPT-4o-mini, Claude 3 Haiku | DM Agent | 性价比最优, 够快 |
| Lightweight | LW | GPT-3.5-turbo, DeepSeek V3 | 文案Agent | 最快, 最便宜 |

### 9.2 Per-Adventure-Type 模型映射

```
短冒险 (short):
  编剧: GPT-4o-mini (MQ) — 短剧本复杂度低，MQ足够
  DM:   GPT-4o-mini (MQ)
  文案: GPT-3.5-turbo (LW)
  平衡: GPT-4o-mini (MQ)

中冒险 (medium):
  编剧: GPT-4o (HQ) — 中剧本需要更深的叙事设计
  DM:   GPT-4o-mini (MQ)
  文案: GPT-4o-mini (MQ) — 更多物品需要更好描述
  平衡: GPT-4o (HQ)

长冒险 (long):
  编剧: Claude 3.5 Sonnet (HQ) — 长篇需要最强创意
  DM:   GPT-4o-mini (MQ)
  文案: GPT-4o-mini (MQ)
  平衡: GPT-4o (HQ)
```

### 9.3 模型切换逻辑

```
实时切换触发条件:

1. 成本触发:
   会话成本 > $3.00 (软阈值):
     编剧: HQ -> MQ
     平衡: HQ -> MQ
     文案: MQ -> LW

2. 性能触发:
   DM Agent平均响应 > 3秒:
     模型降级: GPT-4o-mini -> GPT-3.5-turbo
   DM Agent平均响应 > 5秒:
     模型降级: GPT-3.5-turbo -> DeepSeek V3

3. 可用性触发:
   API返回持续429/5xx:
     切换到备选提供商的同等级模型
     如果所有提供商不可用 -> 离线模式

4. 质量触发:
   连续3次Schema验证失败:
     当前模型升级一个等级 (MQ -> HQ)
     如果已经是HQ -> 增强prompt重试
```

### 9.4 Fallback序列

```
每个Tier的完整Fallback序列:

Tier HQ:
  Primary:    GPT-4o
  Fallback 1: Claude 3.5 Sonnet (同等级, 不同提供商)
  Fallback 2: GPT-4o-mini (降级)
  Fallback 3: DeepSeek V3 (国产备选)
  Fallback 4: 离线模板

Tier MQ:
  Primary:    GPT-4o-mini
  Fallback 1: Claude 3 Haiku (不同提供商)
  Fallback 2: GPT-3.5-turbo (轻量降级)
  Fallback 3: DeepSeek V3 (国产备选)
  Fallback 4: 离线模板

Tier LW:
  Primary:    GPT-3.5-turbo
  Fallback 1: DeepSeek V3 (国产备选)
  Fallback 2: Claude 3 Haiku
  Fallback 3: 离线模板
```

### 9.5 API提供商抽象

```csharp
/// <summary>
/// 抽象基类，所有LLM API提供商实现此接口
/// </summary>
public abstract class APIProvider
{
    public string ProviderName { get; protected init; } = "";
    public string BaseUrl { get; protected init; } = "";
    public string ApiKey { get; set; } = "";
    public List<string> AvailableModels { get; protected init; } = new();

    /// <summary>
    /// 子类实现具体API调用
    /// </summary>
    public abstract Task<object> SendRequestAsync(AgentRequest request);

    /// <summary>
    /// 子类实现响应解析 (不同提供商的JSON结构可能不同)
    /// </summary>
    public abstract Dictionary<string, object> ParseResponse(string raw);
}

public class OpenAIProvider : APIProvider
{
    public OpenAIProvider()
    {
        ProviderName = "openai";
        BaseUrl = "https://api.openai.com/v1";
        AvailableModels = new List<string> { "gpt-4o", "gpt-4o-mini", "gpt-3.5-turbo" };
    }

    public override async Task<object> SendRequestAsync(AgentRequest request)
    {
        using var http = new HttpClient();
        var body = new
        {
            model = request.Model,
            messages = new[]
            {
                new { role = "system", content = request.SystemPrompt },
                new { role = "user", content = request.UserPrompt }
            },
            temperature = request.Temperature,
            max_tokens = request.MaxTokens,
            response_format = new { type = "json_object" }
        };
        // ... HTTP call
        return null!;
    }

    public override Dictionary<string, object> ParseResponse(string raw) { return new(); }
}

public class AnthropicProvider : APIProvider
{
    public AnthropicProvider()
    {
        ProviderName = "anthropic";
        BaseUrl = "https://api.anthropic.com/v1";
        AvailableModels = new List<string> { "claude-3-5-sonnet", "claude-3-haiku" };
    }

    public override async Task<object> SendRequestAsync(AgentRequest request)
    {
        var body = new
        {
            model = request.Model,
            system = request.SystemPrompt,
            messages = new[]
            {
                new { role = "user", content = request.UserPrompt }
            },
            temperature = request.Temperature,
            max_tokens = request.MaxTokens,
        };
        // Anthropic使用不同的API格式
        // ...
        return null!;
    }

    public override Dictionary<string, object> ParseResponse(string raw) { return new(); }
}

public class DeepSeekProvider : APIProvider
{
    public DeepSeekProvider()
    {
        ProviderName = "deepseek";
        BaseUrl = "https://api.deepseek.com/v1";
        AvailableModels = new List<string> { "deepseek-chat", "deepseek-v3" };
    }
    // 兼容OpenAI格式的API

    public override Task<object> SendRequestAsync(AgentRequest request) { return Task.FromResult<object>(null!); }
    public override Dictionary<string, object> ParseResponse(string raw) { return new(); }
}
```

---

## 10. 测试规格

### 10.1 单元测试

#### Schema Validation Tests

```
Test: schema_validator_adventure_blueprint_valid
  Input: 有效的adventure_blueprint JSON (如3.1.6示例)
  Expected: ValidationResult.valid = true

Test: schema_validator_adventure_blueprint_missing_required
  Input: 缺少 "meta.title" 的蓝图JSON
  Expected: ValidationResult.valid = false, errors包含 "missing required property 'title'"

Test: schema_validator_adventure_blueprint_wrong_enum
  Input: node.type = "invalid_type" 的蓝图
  Expected: ValidationResult.valid = false, errors包含枚举错误

Test: schema_validator_adventure_blueprint_wrong_type
  Input: cr_range.min = "string_not_number" 的蓝图
  Expected: ValidationResult.valid = false, errors包含类型错误

Test: schema_validator_adventure_blueprint_range_violation
  Input: estimated_duration_minutes = 0 的蓝图
  Expected: ValidationResult.valid = false, errors包含范围错误

Test: json_extractor_pure_json
  Input: '{"key": "value"}'
  Expected: {"success": true, "json": {"key": "value"}}

Test: json_extractor_markdown_wrapped
  Input: 'Some text\n```json\n{"key": "value"}\n```\nMore text'
  Expected: {"success": true, "json": {"key": "value"}}

Test: json_extractor_trailing_comma_fix
  Input: '{"key": "value",}'
  Expected: {"success": true, "json": {"key": "value"}} (尾逗号被修复)

Test: json_extractor_invalid
  Input: 'Not JSON at all'
  Expected: {"success": false}
```

#### Retry Logic Tests

```
Test: retry_on_schema_failure
  Mock: LLM第一次返回无效JSON, 第二次返回有效JSON
  Expected: 总共2次API调用, 最终返回成功响应

Test: retry_max_exhausted
  Mock: LLM连续3次返回无效JSON
  Expected: 总共3次API调用, 最终触发fallback

Test: retry_exponential_backoff
  Mock: LLM返回可重试错误
  Expected: 重试间隔符合 1s, 2s, 4s 的指数退避模式

Test: retry_non_retryable
  Mock: LLM返回 HTTP 401 (认证错误)
  Expected: 不重试, 直接返回错误
```

#### Cache Tests

```
Test: cache_lookup_hit
  Setup: 在缓存中预存一个响应
  Input: 相同的agent_id + context_hash
  Expected: 返回缓存的响应 (cache_hit event emitted)

Test: cache_lookup_miss
  Input: 未在缓存中的agent_id + context_hash
  Expected: 返回null

Test: cache_store_and_retrieve
  Input: 存储一个新响应
  Expected: 后续相同hash的查询命中

Test: cache_expiry
  Setup: 存储一个expires_at为过去时间的缓存
  Expected: lookup返回null (已过期)

Test: cache_lru_eviction
  Setup: 存储超过10000个条目
  Expected: 最旧的条目被移除
```

#### Token Budget Tests

```
Test: budget_check_within_limit
  Setup: 初始化short冒险 (dm_agent budget = 8000)
  Input: estimated_tokens = 2000
  Expected: BudgetResult.allowed = true

Test: budget_check_exceeded
  Setup: dm_agent剩余budget = 1000
  Input: estimated_tokens = 2000
  Expected: BudgetResult.allowed = false, warning = "agent_budget_exceeded"

Test: budget_borrow_from_other_agent
  Setup: dm_agent剩余 = 1900, copywriter剩余 = 4000
  Input: dm_agent estimated = 2000 (超支100)
  Expected: 从copywriter借用100, BudgetResult.allowed = true

Test: budget_total_exceeded
  Setup: 总budget剩余 = 500, 当前消耗几乎满
  Input: estimated_tokens = 1000
  Expected: BudgetResult.allowed = false, warning = "total_budget_exceeded"
```

### 10.2 集成测试

```
Test: full_agent_request_lifecycle_screenwriter
  Setup: 完整的游戏场景 + Mock HTTP Server
  Steps:
    1. AdventureManager发起编剧Agent请求
    2. Gateway检查缓存 -> miss
    3. Gateway检查速率 -> OK
    4. Gateway发送HTTP请求到Mock Server
    5. Mock Server返回有效的adventure_blueprint JSON
    6. Gateway解析 -> Schema验证通过
    7. Gateway返回AgentResponse给AdventureManager
  Expected: status = SUCCESS, data包含完整蓝图

Test: full_agent_request_lifecycle_dm_agent
  Setup: 同上 + 战斗场景上下文
  Steps:
    1. CombatManager发起DM Agent请求 (combat_narration)
    2. Gateway发送请求
    3. Mock返回有效响应
    4. 通过Schema验证
  Expected: status = SUCCESS, data包含narrative文本

Test: offline_fallback_full
  Setup: LLM API完全不可达
  Steps:
    1. 请求编剧Agent
    2. 所有重试耗尽
    3. 所有备选模型失败
    4. FallbackManager被激活
    5. 从模板库生成冒险蓝图
  Expected: status = FALLBACK, adventure_blueprint由模板填充

Test: model_switching_on_429
  Setup: Primary模型返回HTTP 429
  Steps:
    1. 发送请求到GPT-4o
    2. 收到429 + Retry-After
    3. Gateway切换到Claude 3.5 Sonnet
  Expected: 第二次请求使用claude-3.5-sonnet模型

Test: concurrent_agent_calls
  Setup: 同时发起3个Agent请求 (1x DM + 2x Copywriter)
  Steps:
    1. DM Agent请求 (Priority HIGH)
    2. 两个文案Agent请求 (Priority LOW)
    3. 验证DM先执行，文案后执行
  Expected: DM先完成, 文案按顺序完成 (max_concurrent = 3)
```

### 10.3 边界情况测试

```
Test: large_context_overflow
  Setup: Adventure_summary = 50KB文本 (超长上下文)
  Input: DM Agent请求
  Expected: ContextManager自动裁剪到2000 tokens以内, 请求正常发送

Test: rapid_sequential_calls
  Setup: 在1秒内连续发起30个DM Agent请求
  Expected: RateLimiter触发, 大部分请求排队等待, 不丢失

Test: malformed_llm_output_unicode
  Input: LLM返回包含非法Unicode字符的JSON
  Expected: JSONExtractor优雅处理, 尝试修复, 失败则重试

Test: api_timeout_mid_response
  Setup: Mock HTTP Server在发送一半响应后超时
  Expected: Gateway识别为超时错误, 触发重试

Test: all_models_unavailable
  Setup: 所有API提供商都返回503
  Expected: 系统进入离线模式, 所有后续请求使用模板

Test: cache_corruption
  Setup: 缓存SQLite数据库文件损坏
  Expected: CacheManager检测到损坏 -> 删除旧缓存 -> 创建新数据库 -> 继续运行

Test: concurrent_cache_write
  Setup: 两个请求同时尝试写入相同hash_key
  Expected: SQLite "INSERT OR REPLACE" 处理冲突，最后一个写入的生效

Test: zero_token_budget
  Setup: Token预算完全耗尽
  Expected: 所有Agent请求被拒绝, 自动切换到离线模式
```

### 10.4 性能测试

```
Test: dm_agent_response_time_under_2s
  Setup: 典型DM Agent请求 (2000 tokens input, 500 tokens output)
  Metric: 从 request_started event 到 request_completed event 的时间
  Target: P95 < 2000ms, P50 < 1000ms

Test: cache_lookup_speed
  Setup: SQLite缓存中有10000个条目
  Metric: cache_manager.lookup() 执行时间
  Target: < 5ms (P99)

Test: schema_validation_speed
  Setup: 验证完整的adventure_blueprint JSON (约10KB)
  Metric: schema_validator.validate() 执行时间
  Target: < 10ms

Test: concurrent_load
  Setup: 同时10个Agent请求在队列中
  Metric: 所有请求完成的总时间
  Target: max_concurrent=3时, 10个请求应在5-10秒内全部完成

Test: token_estimation_accuracy
  Setup: 100个已知token数的prompt样本
  Metric: TokenEstimator.estimate_input_tokens()的误差
  Target: 平均误差 < 15%

Test: cache_hit_rate_realistic
  Setup: 模拟10次短冒险的完整LLM调用序列
  Metric: 总缓存命中数 / 总请求数
  Target: DM Agent > 40%, 文案Agent > 50%, 编剧Agent > 20%
```

---

## 11. 叙事状态管理 (Narrative State Management)

### 11.1 概述

叙事状态管理器（NarrativeStateManager）是连接程序骨骼层与 LLM 皮肤层的关键桥梁。每次 DM Agent 调用返回的叙事输出中，包含 `story_consequence` 和 `relationship_effects` 等影响后续叙事走向的信息——但这些信息在传统的"无状态 LLM 调用"模型中会丢失。叙事状态管理器的核心职责是：**在冒险生命周期内持久化追踪叙事上下文，使每次 DM Agent 调用都能感知此前发生的一切**。

核心原则:
- **程序持有叙事状态，LLM 消费叙事状态** — 状态的变更由程序根据蓝图数据决定，LLM 仅读取状态以生成连贯叙事
- **一次冒险一个状态实例** — 每次冒险创建独立的 NarrativeStateManager 实例，冒险结束时销毁
- **状态驱动冒险摘要** — 冒险结束后生成的摘要从叙事状态中提取，而非简单地拼接原始 LLM 输出
- **序列化友好** — 状态数据可完整序列化为 JSON，支持存档/读档

### 11.2 数据模型

#### 11.2.1 JSON Schema: `narrative_state.json`

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "NarrativeState",
  "description": "一次冒险中的完整叙事状态快照。由 NarrativeStateManager 维护。",
  "type": "object",
  "required": [
    "adventure_id",
    "revealed_secrets",
    "npc_emotional_arcs",
    "made_promises",
    "active_foreshadowing",
    "location_memories",
    "defeated_enemies",
    "relationship_changes",
    "consequence_queue"
  ],
  "properties": {
    "adventure_id": {
      "type": "string",
      "description": "关联的冒险唯一标识"
    },
    "revealed_secrets": {
      "type": "array",
      "description": "已揭示的秘密列表",
      "items": {
        "type": "object",
        "required": ["secret_id", "description", "revealed_at_node", "revealed_by"],
        "properties": {
          "secret_id": {
            "type": "string",
            "description": "秘密唯一标识，如 'archaeologist_is_traitor'"
          },
          "description": {
            "type": "string",
            "description": "秘密的叙事描述（中文）",
            "maxLength": 200
          },
          "revealed_at_node": {
            "type": "string",
            "description": "在哪个剧情节点揭示的"
          },
          "revealed_by": {
            "type": "string",
            "description": "由哪个 NPC/事件揭示，如 'npc_archaeologist_leader'"
          },
          "revealed_at_timestamp": {
            "type": "string",
            "format": "date-time"
          },
          "impact_level": {
            "type": "string",
            "enum": ["minor", "moderate", "major", "campaign_defining"],
            "description": "秘密对故事的影响级别"
          }
        }
      }
    },
    "npc_emotional_arcs": {
      "type": "object",
      "description": "NPC 对队伍的情感弧线。key = npc_id, value = 情感状态",
      "additionalProperties": {
        "type": "object",
        "required": ["current_attitude", "attitude_history"],
        "properties": {
          "npc_name": {
            "type": "string"
          },
          "current_attitude": {
            "type": "string",
            "enum": ["hostile", "suspicious", "hesitant", "neutral", "curious", "friendly", "trusting", "devoted"],
            "description": "NPC 当前对队伍的态度"
          },
          "attitude_history": {
            "type": "array",
            "description": "态度变化历史（按时间顺序）",
            "items": {
              "type": "object",
              "properties": {
                "from": { "type": "string" },
                "to": { "type": "string" },
                "trigger": { "type": "string" },
                "node_id": { "type": "string" }
              }
            }
          },
          "trust_level": {
            "type": "integer",
            "minimum": -100,
            "maximum": 100,
            "description": "信任度数值: -100 = 极度不信任, 0 = 中立, 100 = 绝对信任"
          },
          "last_interaction_summary": {
            "type": "string",
            "maxLength": 150
          }
        }
      }
    },
    "made_promises": {
      "type": "array",
      "description": "NPC 对队伍作出的承诺列表",
      "items": {
        "type": "object",
        "required": ["promise_id", "promiser_npc_id", "description", "status"],
        "properties": {
          "promise_id": {
            "type": "string",
            "description": "如 'blacksmith_promised_discount'"
          },
          "promiser_npc_id": {
            "type": "string"
          },
          "description": {
            "type": "string",
            "maxLength": 150
          },
          "status": {
            "type": "string",
            "enum": ["pending", "fulfilled", "broken"],
            "description": "承诺状态"
          },
          "promised_at_node": { "type": "string" },
          "fulfilled_at_node": { "type": "string" },
          "consequence_if_broken": {
            "type": "string",
            "description": "如果承诺未兑现的后果"
          }
        }
      }
    },
    "active_foreshadowing": {
      "type": "array",
      "description": "已埋设但尚未揭晓的伏笔",
      "items": {
        "type": "object",
        "required": ["foreshadow_id", "description", "planted_at_node", "expected_payoff"],
        "properties": {
          "foreshadow_id": {
            "type": "string"
          },
          "description": {
            "type": "string",
            "maxLength": 200
          },
          "planted_at_node": {
            "type": "string"
          },
          "expected_payoff": {
            "type": "string",
            "description": "预期的揭晓节点或条件"
          },
          "is_resolved": {
            "type": "boolean",
            "default": false
          },
          "resolved_at_node": {
            "type": "string"
          }
        }
      }
    },
    "location_memories": {
      "type": "object",
      "description": "已访问地点的记忆。key = node_id",
      "additionalProperties": {
        "type": "object",
        "required": ["node_name", "visited_at", "key_events"],
        "properties": {
          "node_name": {
            "type": "string"
          },
          "visited_at": {
            "type": "string",
            "format": "date-time"
          },
          "key_events": {
            "type": "array",
            "description": "在该地点发生的关键事件简述",
            "items": {
              "type": "string",
              "maxLength": 150
            }
          },
          "visited_count": {
            "type": "integer",
            "minimum": 1
          },
          "outcome": {
            "type": "string",
            "enum": ["explored", "cleared", "fled_from", "bypassed", "returned_later"]
          }
        }
      }
    },
    "defeated_enemies": {
      "type": "array",
      "description": "已击败的敌人及其叙事影响",
      "items": {
        "type": "object",
        "required": ["enemy_type", "encounter_id", "defeated_at"],
        "properties": {
          "enemy_type": {
            "type": "string"
          },
          "enemy_name": {
            "type": "string"
          },
          "encounter_id": {
            "type": "string"
          },
          "defeated_at": {
            "type": "string",
            "format": "date-time"
          },
          "narrative_impact": {
            "type": "string",
            "description": "击败此敌人的叙事影响（如 '打破了当地村民对古庙的恐惧'）",
            "maxLength": 150
          },
          "is_boss": {
            "type": "boolean"
          }
        }
      }
    },
    "relationship_changes": {
      "type": "array",
      "description": "角色之间的关系变化记录",
      "items": {
        "type": "object",
        "required": ["character_a", "character_b", "change_type", "delta", "trigger_event"],
        "properties": {
          "character_a": {
            "type": "string",
            "description": "角色 ID"
          },
          "character_b": {
            "type": "string",
            "description": "角色 ID"
          },
          "change_type": {
            "type": "string",
            "enum": ["trust_built", "trust_broken", "respect_gained", "resentment_formed", "bond_deepened", "rivalry_ignited"]
          },
          "delta": {
            "type": "integer",
            "description": "关系变化量: 正数=改善, 负数=恶化"
          },
          "trigger_event": {
            "type": "string",
            "maxLength": 200
          },
          "occurred_at_node": {
            "type": "string"
          }
        }
      }
    },
    "consequence_queue": {
      "type": "array",
      "description": "待处理的叙事后果队列",
      "items": {
        "type": "object",
        "required": ["consequence_id", "description", "source_choice_id"],
        "properties": {
          "consequence_id": {
            "type": "string"
          },
          "description": {
            "type": "string",
            "maxLength": 200
          },
          "source_choice_id": {
            "type": "string",
            "description": "触发此后果的玩家选择 ID"
          },
          "source_node_id": {
            "type": "string"
          },
          "target_node_id": {
            "type": "string",
            "description": "此后果应该在哪个节点触发"
          },
          "consequence_type": {
            "type": "string",
            "enum": ["dialogue_modifier", "encounter_change", "reward_change", "story_branch", "npc_appearance", "environment_change"]
          },
          "status": {
            "type": "string",
            "enum": ["pending", "triggered", "expired", "cancelled"]
          },
          "expires_after_node": {
            "type": "string",
            "description": "超过此节点后后果失效"
          }
        }
      }
    }
  }
}
```

#### 11.2.2 运行时数据版本（C# 记录类型）

基于上述 Schema 的精简 C# 运行时版本，省略 JSON 序列化相关属性（使用 System.Text.Json 的 `[JsonPropertyName]` 特性自动映射 snake_case）：

```csharp
namespace DndGame.Gateway.Narrative;

/// <summary>
/// 叙事状态管理器 — 跨 LLM 调用追踪冒险中的叙事上下文。
/// 在一次冒险的生命周期内维护所有影响叙事连贯性的状态数据。
/// 冒险结束时销毁实例，状态数据可序列化为存档。
/// </summary>
public class NarrativeStateManager
{
    private readonly string _adventureId;
    private readonly NarrativeState _state;
    private readonly IEventBus _eventBus;

    public NarrativeStateManager(string adventureId, IEventBus eventBus)
    {
        _adventureId = adventureId;
        _eventBus = eventBus;
        _state = new NarrativeState { AdventureId = adventureId };
    }

    /// <summary>
    /// 获取当前叙事状态快照（用于注入 DM Agent 输入）。
    /// </summary>
    public NarrativeStateSnapshot GetSnapshot() => _state.ToSnapshot();

    /// <summary>
    /// 写入一个已揭示的秘密。
    /// </summary>
    public void RevealSecret(string secretId, string description,
        string nodeId, string revealedBy, string impactLevel = "moderate")
    {
        _state.RevealedSecrets.Add(new RevealedSecret
        {
            SecretId = secretId,
            Description = description,
            RevealedAtNode = nodeId,
            RevealedBy = revealedBy,
            RevealedAtTimestamp = DateTime.UtcNow,
            ImpactLevel = impactLevel
        });
    }

    /// <summary>
    /// 更新 NPC 对队伍的情感态度。
    /// </summary>
    public void UpdateNpcAttitude(string npcId, string npcName,
        string newAttitude, int trustDelta, string trigger, string nodeId)
    {
        if (!_state.NpcEmotionalArcs.TryGetValue(npcId, out var arc))
        {
            arc = new NpcEmotionalArc
            {
                NpcName = npcName,
                CurrentAttitude = "neutral",
                TrustLevel = 0,
                AttitudeHistory = new List<AttitudeChange>()
            };
            _state.NpcEmotionalArcs[npcId] = arc;
        }

        var oldAttitude = arc.CurrentAttitude;
        arc.CurrentAttitude = newAttitude;
        arc.TrustLevel = Math.Clamp(arc.TrustLevel + trustDelta, -100, 100);
        arc.AttitudeHistory.Add(new AttitudeChange
        {
            From = oldAttitude,
            To = newAttitude,
            Trigger = trigger,
            NodeId = nodeId
        });
    }

    /// <summary>
    /// 记录 NPC 作出的承诺。
    /// </summary>
    public void RecordPromise(string promiseId, string promiserNpcId,
        string description, string nodeId, string? consequenceIfBroken = null)
    {
        _state.MadePromises.Add(new MadePromise
        {
            PromiseId = promiseId,
            PromiserNpcId = promiserNpcId,
            Description = description,
            Status = "pending",
            PromisedAtNode = nodeId,
            ConsequenceIfBroken = consequenceIfBroken
        });
    }

    /// <summary>
    /// 标记承诺状态变更。
    /// </summary>
    public void UpdatePromiseStatus(string promiseId, string newStatus,
        string? nodeId = null)
    {
        var promise = _state.MadePromises.Find(p => p.PromiseId == promiseId);
        if (promise != null)
        {
            promise.Status = newStatus;
            if (newStatus == "fulfilled" && nodeId != null)
                promise.FulfilledAtNode = nodeId;
        }
    }

    /// <summary>
    /// 埋设一个伏笔（尚未揭晓）。
    /// </summary>
    public void PlantForeshadowing(string foreshadowId, string description,
        string nodeId, string expectedPayoff)
    {
        _state.ActiveForeshadowing.Add(new ActiveForeshadowing
        {
            ForeshadowId = foreshadowId,
            Description = description,
            PlantedAtNode = nodeId,
            ExpectedPayoff = expectedPayoff,
            IsResolved = false
        });
    }

    /// <summary>
    /// 标记伏笔已揭晓。
    /// </summary>
    public void ResolveForeshadowing(string foreshadowId, string nodeId)
    {
        var fs = _state.ActiveForeshadowing.Find(f => f.ForeshadowId == foreshadowId);
        if (fs != null)
        {
            fs.IsResolved = true;
            fs.ResolvedAtNode = nodeId;
        }
    }

    /// <summary>
    /// 记录地点访问与关键事件。
    /// </summary>
    public void RecordLocationVisit(string nodeId, string nodeName,
        string keyEvent, string outcome = "explored")
    {
        if (!_state.LocationMemories.TryGetValue(nodeId, out var memory))
        {
            memory = new LocationMemory
            {
                NodeName = nodeName,
                VisitedAt = DateTime.UtcNow,
                KeyEvents = new List<string>(),
                VisitedCount = 0,
                Outcome = outcome
            };
            _state.LocationMemories[nodeId] = memory;
        }

        memory.VisitedCount++;
        memory.KeyEvents.Add(keyEvent);
        memory.Outcome = outcome;
    }

    /// <summary>
    /// 记录击败的敌人及其叙事影响。
    /// </summary>
    public void RecordDefeatedEnemy(string enemyType, string enemyName,
        string encounterId, string? narrativeImpact = null, bool isBoss = false)
    {
        _state.DefeatedEnemies.Add(new DefeatedEnemy
        {
            EnemyType = enemyType,
            EnemyName = enemyName,
            EncounterId = encounterId,
            DefeatedAt = DateTime.UtcNow,
            NarrativeImpact = narrativeImpact,
            IsBoss = isBoss
        });
    }

    /// <summary>
    /// 记录角色关系变化。
    /// </summary>
    public void RecordRelationshipChange(string characterA, string characterB,
        string changeType, int delta, string triggerEvent, string nodeId)
    {
        _state.RelationshipChanges.Add(new RelationshipChange
        {
            CharacterA = characterA,
            CharacterB = characterB,
            ChangeType = changeType,
            Delta = delta,
            TriggerEvent = triggerEvent,
            OccurredAtNode = nodeId
        });
    }

    /// <summary>
    /// 将 story_consequence 转换为待处理后果并加入队列。
    /// </summary>
    public void EnqueueConsequence(string consequenceId, string description,
        string sourceChoiceId, string sourceNodeId, string? targetNodeId = null,
        string consequenceType = "story_branch", string? expiresAfterNode = null)
    {
        _state.ConsequenceQueue.Add(new NarrativeConsequence
        {
            ConsequenceId = consequenceId,
            Description = description,
            SourceChoiceId = sourceChoiceId,
            SourceNodeId = sourceNodeId,
            TargetNodeId = targetNodeId,
            ConsequenceType = consequenceType,
            Status = "pending",
            ExpiresAfterNode = expiresAfterNode
        });
    }

    /// <summary>
    /// 处理指定节点上所有待处理的后果，返回应该在此节点触发的后果列表。
    /// </summary>
    public IReadOnlyList<NarrativeConsequence> ProcessConsequencesForNode(
        string currentNodeId)
    {
        var triggered = new List<NarrativeConsequence>();
        var expired = new List<NarrativeConsequence>();

        foreach (var c in _state.ConsequenceQueue.Where(c => c.Status == "pending"))
        {
            if (c.TargetNodeId == currentNodeId)
            {
                c.Status = "triggered";
                triggered.Add(c);
            }
            else if (c.ExpiresAfterNode != null &&
                     c.ExpiresAfterNode == currentNodeId)
            {
                c.Status = "expired";
                expired.Add(c);
            }
        }

        return triggered;
    }

    /// <summary>
    /// 从叙事状态生成冒险摘要（非 LLM 拼接，而是从结构化状态提取）。
    /// 生成的摘要将注入 DM Agent 的 adventure_summary 字段。
    /// </summary>
    public string GenerateAdventureSummary(int maxLength = 800)
    {
        var lines = new List<string>();

        // 阶段 1: 概述 — 地点 + 敌人
        var locationCount = _state.LocationMemories.Count;
        var enemyCount = _state.DefeatedEnemies.Count;
        var boss = _state.DefeatedEnemies.Find(e => e.IsBoss);
        lines.Add($"队伍已探索 {locationCount} 个区域，击败 {enemyCount} 批敌人。" +
            (boss != null ? $"Boss '{boss.EnemyName}' 已被击败。" : ""));

        // 阶段 2: 秘密揭示
        if (_state.RevealedSecrets.Count > 0)
        {
            var secretsSummary = string.Join("；",
                _state.RevealedSecrets.Select(s => $"[{s.SecretId}] {s.Description}"));
            lines.Add($"已揭示的秘密: {secretsSummary}");
        }

        // 阶段 3: NPC 情感状态
        if (_state.NpcEmotionalArcs.Count > 0)
        {
            var npcSummaries = _state.NpcEmotionalArcs
                .Select(kv => $"{kv.Value.NpcName}({kv.Value.CurrentAttitude}, 信任:{kv.Value.TrustLevel})");
            lines.Add($"NPC 状态: {string.Join("; ", npcSummaries)}");
        }

        // 阶段 4: 活跃伏笔
        var unresolved = _state.ActiveForeshadowing
            .Where(f => !f.IsResolved).ToList();
        if (unresolved.Count > 0)
        {
            lines.Add($"未揭晓伏笔: {string.Join("; ", unresolved.Select(f => f.Description))}");
        }

        // 阶段 5: 承诺状态
        var pendingPromises = _state.MadePromises
            .Where(p => p.Status == "pending").ToList();
        if (pendingPromises.Count > 0)
        {
            lines.Add($"待兑现承诺: {string.Join("; ", pendingPromises.Select(p => p.Description))}");
        }

        // 阶段 6: 角色关系
        if (_state.RelationshipChanges.Count > 0)
        {
            var relSummary = _state.RelationshipChanges
                .GroupBy(r => $"{r.CharacterA}<->{r.CharacterB}")
                .Select(g => $"{g.Key}: {g.Last().ChangeType}(Δ{g.Sum(r => r.Delta)})");
            lines.Add($"角色关系: {string.Join("; ", relSummary)}");
        }

        var summary = string.Join("\n", lines);
        return summary.Length <= maxLength
            ? summary
            : summary[..(maxLength - 3)] + "...";
    }
}

// ─── 数据记录类型 ───────────────────────────────────────────

/// <summary>
/// 完整叙事状态（运行时可变）。
/// </summary>
public class NarrativeState
{
    public string AdventureId { get; set; } = "";
    public List<RevealedSecret> RevealedSecrets { get; set; } = new();
    public Dictionary<string, NpcEmotionalArc> NpcEmotionalArcs { get; set; } = new();
    public List<MadePromise> MadePromises { get; set; } = new();
    public List<ActiveForeshadowing> ActiveForeshadowing { get; set; } = new();
    public Dictionary<string, LocationMemory> LocationMemories { get; set; } = new();
    public List<DefeatedEnemy> DefeatedEnemies { get; set; } = new();
    public List<RelationshipChange> RelationshipChanges { get; set; } = new();
    public List<NarrativeConsequence> ConsequenceQueue { get; set; } = new();

    /// <summary>
    /// 生成不可变快照（用于注入 DM Agent）。
    /// </summary>
    public NarrativeStateSnapshot ToSnapshot() => new()
    {
        AdventureId = AdventureId,
        RevealedSecrets = RevealedSecrets.Select(s => s.Description).ToList(),
        NpcAttitudes = NpcEmotionalArcs.ToDictionary(
            kv => kv.Key,
            kv => new NpcAttitudeSnapshot
            {
                NpcName = kv.Value.NpcName,
                CurrentAttitude = kv.Value.CurrentAttitude,
                TrustLevel = kv.Value.TrustLevel
            }),
        PendingPromises = MadePromises
            .Where(p => p.Status == "pending")
            .Select(p => p.Description).ToList(),
        UnresolvedForeshadowing = ActiveForeshadowing
            .Where(f => !f.IsResolved)
            .Select(f => f.Description).ToList(),
        RecentRelationshipChanges = RelationshipChanges
            .TakeLast(5).ToList(),
        PendingConsequences = ConsequenceQueue
            .Where(c => c.Status == "pending").ToList()
    };
}

/// <summary>
/// 叙事状态快照 — 只读版本，注入 DM Agent 输入。
/// 刻意简化以控制 Token 消耗，仅包含 LLM 生成叙事所需的关键信息。
/// </summary>
public record NarrativeStateSnapshot
{
    public string AdventureId { get; init; } = "";
    public List<string> RevealedSecrets { get; init; } = new();
    public Dictionary<string, NpcAttitudeSnapshot> NpcAttitudes { get; init; } = new();
    public List<string> PendingPromises { get; init; } = new();
    public List<string> UnresolvedForeshadowing { get; init; } = new();
    public List<RelationshipChange> RecentRelationshipChanges { get; init; } = new();
    public List<NarrativeConsequence> PendingConsequences { get; init; } = new();
}

public record NpcAttitudeSnapshot
{
    public string NpcName { get; init; } = "";
    public string CurrentAttitude { get; init; } = "neutral";
    public int TrustLevel { get; init; }
}

public record RevealedSecret
{
    public string SecretId { get; set; } = "";
    public string Description { get; set; } = "";
    public string RevealedAtNode { get; set; } = "";
    public string RevealedBy { get; set; } = "";
    public DateTime RevealedAtTimestamp { get; set; }
    public string ImpactLevel { get; set; } = "moderate";
}

public class NpcEmotionalArc
{
    public string NpcName { get; set; } = "";
    public string CurrentAttitude { get; set; } = "neutral";
    public int TrustLevel { get; set; }
    public List<AttitudeChange> AttitudeHistory { get; set; } = new();
    public string? LastInteractionSummary { get; set; }
}

public record AttitudeChange
{
    public string From { get; init; } = "";
    public string To { get; init; } = "";
    public string Trigger { get; init; } = "";
    public string NodeId { get; init; } = "";
}

public record MadePromise
{
    public string PromiseId { get; set; } = "";
    public string PromiserNpcId { get; set; } = "";
    public string Description { get; set; } = "";
    public string Status { get; set; } = "pending";
    public string PromisedAtNode { get; set; } = "";
    public string? FulfilledAtNode { get; set; }
    public string? ConsequenceIfBroken { get; set; }
}

public record ActiveForeshadowing
{
    public string ForeshadowId { get; set; } = "";
    public string Description { get; set; } = "";
    public string PlantedAtNode { get; set; } = "";
    public string ExpectedPayoff { get; set; } = "";
    public bool IsResolved { get; set; }
    public string? ResolvedAtNode { get; set; }
}

public record LocationMemory
{
    public string NodeName { get; set; } = "";
    public DateTime VisitedAt { get; set; }
    public List<string> KeyEvents { get; set; } = new();
    public int VisitedCount { get; set; }
    public string Outcome { get; set; } = "explored";
}

public record DefeatedEnemy
{
    public string EnemyType { get; set; } = "";
    public string? EnemyName { get; set; }
    public string EncounterId { get; set; } = "";
    public DateTime DefeatedAt { get; set; }
    public string? NarrativeImpact { get; set; }
    public bool IsBoss { get; set; }
}

public record RelationshipChange
{
    public string CharacterA { get; set; } = "";
    public string CharacterB { get; set; } = "";
    public string ChangeType { get; set; } = "";
    public int Delta { get; set; }
    public string TriggerEvent { get; set; } = "";
    public string OccurredAtNode { get; set; } = "";
}

public record NarrativeConsequence
{
    public string ConsequenceId { get; set; } = "";
    public string Description { get; set; } = "";
    public string SourceChoiceId { get; set; } = "";
    public string SourceNodeId { get; set; } = "";
    public string? TargetNodeId { get; set; }
    public string ConsequenceType { get; set; } = "story_branch";
    public string Status { get; set; } = "pending";
    public string? ExpiresAfterNode { get; set; }
}
```

### 11.3 状态序列化

叙事状态需要支持冒险中断后的存档恢复。使用 System.Text.Json 的源代码生成器实现高性能序列化：

```csharp
namespace DndGame.Gateway.Narrative;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// 叙事状态序列化器 — 将 NarrativeState 与 JSON 互相转换。
/// 使用 System.Text.Json 源代码生成器以支持 AOT。
/// </summary>
public static class NarrativeStateSerializer
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// 将 NarrativeState 序列化为 JSON 字符串。
    /// </summary>
    public static string Serialize(NarrativeState state)
    {
        return JsonSerializer.Serialize(state, _options);
    }

    /// <summary>
    /// 从 JSON 字符串反序列化为 NarrativeState。
    /// </summary>
    public static NarrativeState? Deserialize(string json)
    {
        return JsonSerializer.Deserialize<NarrativeState>(json, _options);
    }

    /// <summary>
    /// 生成用于 DM Agent 输入的压缩叙事上下文（精简版本，仅包含叙事必需字段）。
    /// </summary>
    public static NarrativeStateSnapshot GenerateSnapshot(NarrativeState state)
    {
        return state.ToSnapshot();
    }
}
```

### 11.4 与现有系统的集成

#### 11.4.1 系统架构中的位置

```
                          ┌──────────────────────────────────────┐
                          │             云端 LLM API              │
                          └────────────────┬─────────────────────┘
                                           │
                          ┌────────────────┴─────────────────────┐
                          │         DM Agent (实时调用)           │
                          │  输入: + narrative_state 字段         │
                          │  输出: story_consequence +            │
                          │         relationship_effects          │
                          └──┬─────────────────────┬─────────────┘
                             │                     │
                    ┌────────┴────────┐   ┌────────┴────────────┐
                    │  NarrativeState │◄──│  AdventureManager   │
                    │    Manager      │   │  (剧情节点管理器)    │
                    │                 │──►│                     │
                    │  持久化追踪:    │   │  每次进入节点时:     │
                    │  · 秘密         │   │  1. 注入 narrative_  │
                    │  · NPC情感      │   │     state 到 DM 输入 │
                    │  · 承诺         │   │  2. 解析 DM 输出    │
                    │  · 伏笔         │   │  3. 更新叙事状态    │
                    │  · 后果队列     │   │                     │
                    └─────────────────┘   └─────────────────────┘
```

#### 11.4.2 DM Agent 输入扩展

DM Agent 的输入 Schema（参见 Section 3.2.3）需要新增一个可选字段 `narrative_state`，用于传递当前叙事上下文。**此字段为可选字段，不在原有 required 列表中，不破坏现有 Schema 兼容性**：

```json
{
  "narrative_state": {
    "type": "object",
    "description": "当前冒险的叙事状态摘要。由 NarrativeStateManager 提供。",
    "properties": {
      "revealed_secrets": {
        "type": "array",
        "items": { "type": "string" },
        "description": "已揭示的秘密简述列表"
      },
      "npc_attitudes": {
        "type": "object",
        "description": "{npc_id: {name, attitude, trust_level}}"
      },
      "pending_promises": {
        "type": "array",
        "items": { "type": "string" },
        "description": "待兑现的承诺列表"
      },
      "unresolved_foreshadowing": {
        "type": "array",
        "items": { "type": "string" },
        "description": "尚未揭晓的伏笔"
      },
      "recent_relationship_changes": {
        "type": "array",
        "description": "最近的角色关系变化"
      },
      "summary_text": {
        "type": "string",
        "description": "叙事状态的自然语言摘要（由 GenerateAdventureSummary 生成）"
      }
    }
  }
}
```

#### 11.4.3 DM Agent 输出解析流程

每次 DM Agent 调用返回后，AdventureManager 需要解析 `story_consequence` 和 `relationship_effects`，并将其输入叙事状态管理器：

```csharp
/// <summary>
/// 从蓝图节点选择中提取叙事后果并更新状态。
/// 在玩家作出选择后、DM Agent 调用返回之后调用。
/// </summary>
public void ProcessChoiceOutcome(
    string nodeId,
    string choiceId,
    string? storyConsequence,
    Dictionary<string, int>? relationshipEffects)
{
    // 1. 处理故事后果 → 加入后果队列
    if (!string.IsNullOrEmpty(storyConsequence))
    {
        var consequenceId = $"cons_{nodeId}_{choiceId}";
        _narrativeState.EnqueueConsequence(
            consequenceId: consequenceId,
            description: storyConsequence,
            sourceChoiceId: choiceId,
            sourceNodeId: nodeId,
            consequenceType: InferConsequenceType(storyConsequence)
        );
    }

    // 2. 处理关系影响 → 更新关系变化
    if (relationshipEffects != null)
    {
        foreach (var (characterId, delta) in relationshipEffects)
        {
            _narrativeState.RecordRelationshipChange(
                characterA: "party",      // 队伍作为整体
                characterB: characterId,
                changeType: delta > 0 ? "trust_built" : "trust_broken",
                delta: delta,
                triggerEvent: $"在节点 {nodeId} 选择了 {choiceId}",
                nodeId: nodeId
            );
        }
    }

    // 3. 发布叙事状态变更事件
    _eventBus.Publish(new NarrativeStateUpdatedEvent(
        _adventureId, nodeId, choiceId));
}

private static string InferConsequenceType(string description)
{
    if (description.Contains("战斗") || description.Contains("遭遇"))
        return "encounter_change";
    if (description.Contains("奖励") || description.Contains("物品"))
        return "reward_change";
    if (description.Contains("对话") || description.Contains("态度"))
        return "dialogue_modifier";
    return "story_branch";
}
```

#### 11.4.4 冒险生命周期集成

```
冒险开始:
  ┌─────────────────────────────────────────────────────┐
  │ AdventureManager.InitializeAdventure(blueprint)     │
  │   → _narrativeState = new NarrativeStateManager(    │
  │       blueprint.Id, ServiceLocator.Get<IEventBus>())│
  │   → 在 ServiceLocator 中注册为 INarrativeStateManager│
  │     （冒险级作用域，覆盖全局注册）                    │
  └─────────────────────────────────────────────────────┘

每个剧情节点:
  ┌─────────────────────────────────────────────────────┐
  │ 1. 进入节点 → ProcessConsequencesForNode(nodeId)    │
  │    → 触发该节点的 pending 后果                       │
  │                                                     │
  │ 2. 构造 DM Agent 输入:                              │
  │    · scene_context.adventure_summary =               │
  │      _narrativeState.GenerateAdventureSummary()      │
  │    · narrative_state = _narrativeState.GetSnapshot() │
  │                                                     │
  │ 3. 调用 DM Agent                                    │
  │                                                     │
  │ 4. 解析 DM 输出 → ProcessChoiceOutcome(...)          │
  │    → 更新叙事状态                                    │
  │                                                     │
  │ 5. 记录地点访问:                                     │
  │    RecordLocationVisit(nodeId, nodeName, keyEvent)   │
  └─────────────────────────────────────────────────────┘

冒险结束:
  ┌─────────────────────────────────────────────────────┐
  │ AdventureManager.EndAdventure()                      │
  │   → var summary = _narrativeState.GenerateAdventure  │
  │       Summary(maxLength: 800)                        │
  │   → var json = NarrativeStateSerializer.Serialize(   │
  │       _narrativeState.GetFullState())                │
  │   → 存档到 DataPersistence                           │
  │   → 结算系统使用 summary 生成冒险结局叙事            │
  │   → 销毁 NarrativeStateManager 实例                  │
  └─────────────────────────────────────────────────────┘
```

### 11.5 NPC 对话预生成集成

叙事状态管理器与 Section 3 中定义的 NPC 对话预生成策略紧密协作（参见 [§1.3 调用关系矩阵](#13-调用关系矩阵) 的设计说明）：

1. **预生成阶段（酒馆中）**：
   - 编剧 Agent 生成冒险蓝图后，AdventureManager 遍历 `key_npcs` 数组
   - 为每个 NPC 的每种可能情绪状态（基于 `npc_emotional_arcs` 的枚举值）预生成 2-3 条对话变体
   - 预生成的对话存储到内存缓存：`Dictionary<string, List<DMNarrativeResponse>>`，key = `npcId + "|" + attitude + "|" + mood`

2. **实时使用阶段（冒险中）**：
   - 玩家与 NPC 交互时，NarrativeStateManager 提供当前 NPC 的 `current_attitude` 和 `trust_level`
   - AdventureManager 从预生成缓存中查找匹配的对话
   - 若 NPC 态度在冒险中发生改变（如 hostile → hesitant），优先回退到实时 DM Agent 调用

3. **状态联动**：
   ```csharp
   /// <summary>
   /// 根据叙事状态查找最匹配的预生成 NPC 对话。
   /// </summary>
   public DMNarrativeResponse? FindCachedNpcDialogue(
       string npcId,
       Dictionary<string, List<DMNarrativeResponse>> dialogueCache)
   {
       var attitude = _state.NpcEmotionalArcs
           .TryGetValue(npcId, out var arc)
               ? arc.CurrentAttitude
               : "neutral";

       var cacheKey = $"{npcId}|{attitude}|default";

       if (dialogueCache.TryGetValue(cacheKey, out var variants) &&
           variants.Count > 0)
       {
           // 使用最近最少使用的变体以避免重复
           return variants[Random.Shared.Next(variants.Count)];
       }

       return null; // 缓存未命中 → 触发实时 DM Agent 调用
   }
   ```

### 11.6 边缘情况处理

| 场景 | 处理策略 |
|------|----------|
| **冒险中断后恢复** | 从存档读取完整 NarrativeState JSON 反序列化；若存档损坏，使用 adventure_blueprint 重建最小状态（仅包含 NPC 初始态度和节点访问记录） |
| **玩家回到已访问节点** | `LocationMemory.VisitedCount` 递增；`LocationMemory.KeyEvents` 追加新事件；DM Agent 输入包含 "return_visit: true" 标记 |
| **后果队列溢出** | 上限 50 条；超出时自动将最早的非 `story_branch` 类型后果标记为 `expired` |
| **NPC 态度达到极值** | TrustLevel 锁定在 [-100, 100] 范围内；达到 ±100 时，DM Agent 的 system prompt 注入特殊提示："此 NPC 已对队伍不可挽回地信任/敌视" |
| **伏笔在冒险结束前未揭晓** | 未揭晓的伏笔记录到冒险摘要中，标记为 `state: "unresolved"`；通过 `world_state_hooks` 传播到世界状态，影响后续冒险 |
| **关系变化冲突** | 同节点内多个 relationship_effects 作用于同一对角色 → 按顺序累加 delta，最终在节点结束时统一写入 |

### 11.7 验收标准

| # | 验收标准 | 测试方法 | 通过条件 |
|---|----------|----------|----------|
| AC-N1 | NarrativeStateManager 正确初始化 | 单元测试 | 初始状态下所有集合为空，adventure_id 正确 |
| AC-N2 | 秘密揭示正确记录 | 单元测试 | RevealSecret() 后 RevealedSecrets 包含对应条目 |
| AC-N3 | NPC 态度变更正确追踪历史 | 单元测试 | 连续 3 次 UpdateNpcAttitude() 后 AttitudeHistory 包含 3 条记录 |
| AC-N4 | 后果队列正确触发 | 单元测试 | ProcessConsequencesForNode("node_5") 仅返回 TargetNodeId == "node_5" 的 pending 后果 |
| AC-N5 | 冒险摘要从状态生成（非拼接） | 单元测试 | GenerateAdventureSummary() 的输出仅包含结构化状态提取的文本 |
| AC-N6 | 状态序列化/反序列化往返一致 | 单元测试 | Serialize → Deserialize → 所有字段值与原状态一致 |
| AC-N7 | DM Agent 输入包含 narrative_state | 集成测试 | 构造的 DM Agent 请求 JSON 包含 narrative_state 字段 |
| AC-N8 | DM Agent 输出的 story_consequence 正确写入后果队列 | 集成测试 | ProcessChoiceOutcome() 后 ConsequenceQueue 包含对应条目 |
| AC-N9 | NPC 预生成对话根据叙事状态正确匹配 | 集成测试 | NPC 态度变更后 FindCachedNpcDialogue() 返回不同缓存 key 的对话 |
| AC-N10 | 冒险结束时状态正确序列化存档 | 端到端测试 | 存档文件包含完整的 narrative_state JSON |

---

## 12. 依赖关系 (Dependencies)

### 12.1 上游依赖（本系统依赖）

| 依赖系统 | 依赖内容 | 状态 | 风险 |
|----------|----------|:----:|:----:|
| **事件总线 (EventBus)** | Gateway信号发布（request_started/completed/failed） | ✅ 已实现 | 低 |
| **角色系统** | personality_tags、key_memories、relationship_summary注入DM Agent | ✅ 已审查 | 中 — 需确保DM Agent输入Schema包含这些字段 |
| **冒险生成系统** | 编剧Agent输出消费方 | ✅ 已设计 | 低 |
| **战斗系统** | DM Agent战斗叙述调用方 | ✅ 已设计 | 低 |
| **世界状态系统** | 世界状态hooks | ❌ 未设计 | 中 |

### 12.2 下游依赖（依赖本系统的系统）

| 依赖系统 | 依赖内容 | GDD状态 |
|----------|----------|:-------:|
| **冒险生成系统** | 编剧Agent生成冒险蓝图 | ✅ |
| **战斗系统** | DM Agent生成战斗叙述 | ✅ |
| **酒馆系统** | 文案Agent生成酒馆事件描述 | ✅ |
| **失败与成长系统** | 编剧Agent生成结算叙事 | ✅ |

---

## 13. 可调参数 (Tuning Knobs)

| 参数 | 当前值 | 安全范围 | 影响面 |
|------|:------:|:--------:|--------|
| **DM Agent缓存** | 禁用 | 启用/禁用 | 叙事独特性 vs Token消耗 |
| **Token预算** | 暂无限制 | 20K-350K/冒险 | 叙事完整性 vs 成本控制 |
| **全局RPM限制** | 30 | 30-100 | 叙事实时性 vs API成本 |
| **per-Agent RPM (DM)** | 20 | 10-50 | 战斗叙述频率 |
| **最大并发请求** | 3 | 2-10 | 叙事延迟 vs 资源消耗 |
| **重试次数** | 2-3 | 1-5 | 可靠性 vs 延迟 |
| **超时时间 (DM Agent)** | 8秒 | 3-15秒 | 叙事等待 vs 可靠性 |
| **熔断器冷却期** | 60秒 | 30-120秒 | 故障恢复速度 |
| **优先级老化阈值 (LOW→MEDIUM)** | 30秒 | 15-60秒 | 后台任务处理速度 |
| **预生成对话数量 (NPC)** | N个 | 3-10 | 预生成覆盖率 vs Token消耗 |
| **Primary模型** | GPT-4o-mini | 任意 | 叙事质量 vs 成本 |
| **Fallback模型** | Claude 3 Haiku | 任意 | 可靠性 vs 成本 |

---

## 14. 验收标准 (Acceptance Criteria)

| # | 验收标准 | 测试方法 | 通过条件 |
|---|----------|----------|----------|
| AC-1 | LLM Gateway正常启动 | 启动测试 | 所有组件初始化成功，无异常 |
| AC-2 | 编剧Agent生成有效蓝图 | 端到端测试 | 生成JSON通过Schema验证，包含所有必填字段 |
| AC-3 | DM Agent生成有效叙述 | 端到端测试 | 生成文本通过Schema验证，长度≤500字符 |
| AC-4 | 离线降级正常工作 | 断网测试 | API不可用时自动使用模板，游戏不崩溃 |
| AC-5 | 速率限制生效 | 负载测试 | 超过RPM限制时请求排队，不丢失 |
| AC-6 | 熔断器生效 | 故障注入测试 | 连续3次失败后模型被标记为不健康，冷却60秒 |
| AC-7 | 优先级老化生效 | 队列测试 | LOW优先级请求30秒后自动升级为MEDIUM |
| AC-8 | Rate Limiter正确记录 | 单元测试 | check_and_record()批准请求后正确写入跟踪数组 |
| AC-9 | 叙事状态跨调用持久化 | 集成测试 | NarrativeStateManager在冒险内保持状态 |
| AC-10 | NPC对话预生成工作 | 端到端测试 | 酒馆阶段预生成的对话在冒险中可正常使用 |
| AC-11 | DM Agent注入角色性格 | Schema验证 | DM Agent输入包含personality_tags、key_memories |
| AC-12 | 缓存策略符合支柱 | 代码审查 | DM Agent不使用缓存，编剧/文案Agent使用缓存 |
| AC-13 | GDScript代码已全部翻译 | 代码审查 | 文档中无GDScript代码块，全部为C# |
| AC-14 | Token预算无限制 | 配置验证 | MVP阶段不设置Token上限 |

---

*文档版本: v1.1*
*创建日期: 2026-05-04*
*最后更新: 2026-05-09*
*对应GDD: GDD-v1.md*
*状态: 设计评审修订完成，待复审*
