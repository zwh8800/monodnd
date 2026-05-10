namespace DndGame.Core;

/// <summary>
/// LLM 集成网关接口 — 所有 LLM Agent 调用的统一入口。
/// 负责请求路由、响应解析、缓存管理和降级回退。
/// 遵循"LLM = 皮肤层"原则：只生成叙事文本，不决策数值。
/// </summary>
public interface ILLMGateway
{
    /// <summary>
    /// 异步调用指定 Agent 并返回结构化结果。
    /// 内部流程：查缓存 → API 调用 → Schema 验证 → 降级回退。
    /// </summary>
    /// <typeparam name="T">期望的响应类型。</typeparam>
    /// <param name="agentId">Agent 唯一标识。</param>
    /// <param name="context">传递给 Agent 的上下文对象。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>Agent 的结构化响应；失败或降级时返回 null。</returns>
    Task<T?> CallAgentAsync<T>(string agentId, object context, CancellationToken ct = default) where T : class;

    /// <summary>
    /// 检查指定 Agent 是否已注册并处于可用状态。
    /// </summary>
    /// <param name="agentId">Agent 唯一标识。</param>
    /// <returns>若 Agent 已注册且可用则返回 true。</returns>
    bool IsAgentReady(string agentId);

    /// <summary>
    /// 获取当前模式下所有已注册 Agent 的 ID 列表。
    /// </summary>
    IReadOnlyList<string> GetRegisteredAgentIds();
}
