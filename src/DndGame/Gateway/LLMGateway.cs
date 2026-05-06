using DndGame.Gateway.Validation;
using DndGame.Gateway.Cache;
using DndGame.Gateway.Fallback;

namespace DndGame.Gateway;

/// <summary>
/// LLM Gateway — 所有 LLM 请求的唯一入口。
/// 管理 Agent 注册、Schema 验证、缓存和降级。
/// </summary>
public class LLMGateway
{
    private readonly Dictionary<string, LLMAgent> _agents = new();
    private readonly SchemaValidator _validator = new();
    private readonly CacheManager _cache = new();
    private readonly FallbackManager _fallback = new();

    /// <summary>
    /// 注册 Agent。
    /// </summary>
    public void RegisterAgent(LLMAgent agent)
    {
        _agents[agent.AgentId] = agent;
    }

    /// <summary>
    /// 调用 Agent。当前为基础实现（离线模式），后续集成 HttpClient。
    /// </summary>
    public async Task<T?> CallAgent<T>(string agentId, object context) where T : class
    {
        if (!_agents.TryGetValue(agentId, out var agent))
            return null;

        // 生成缓存键
        var contextJson = System.Text.Json.JsonSerializer.Serialize(context);
        var contextHash = CacheManager.GenerateHash(contextJson);

        // 检查缓存
        var cached = _cache.Lookup(agentId, contextHash);
        if (cached.IsHit && cached.Response != null)
        {
            return agent.ParseResponse<T>(cached.Response);
        }

        // 离线模式：使用降级模板
        var fallbackResponse = _fallback.GetFallback(agentId, "combat_narration");

        // 验证响应
        var validation = _validator.Validate(agentId, fallbackResponse);
        if (!validation.IsValid)
            return null;

        // 存储缓存
        _cache.Store(agentId, contextHash, fallbackResponse);

        // 解析响应
        return agent.ParseResponse<T>(fallbackResponse);
    }

    /// <summary>
    /// 获取已注册的 Agent 列表。
    /// </summary>
    public IReadOnlyDictionary<string, LLMAgent> GetAgents() => _agents;
}
