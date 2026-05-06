namespace DndGame.Gateway;

/// <summary>
/// LLM Agent 抽象基类，定义 Agent 的基本契约。
/// </summary>
public abstract class LLMAgent
{
    /// <summary>
    /// Agent 唯一标识。
    /// </summary>
    public abstract string AgentId { get; }

    /// <summary>
    /// System Prompt 模板。
    /// </summary>
    public abstract string SystemPrompt { get; }

    /// <summary>
    /// 构建请求 payload。
    /// </summary>
    public abstract object BuildPayload(object context);

    /// <summary>
    /// 解析响应 JSON。
    /// </summary>
    public abstract T? ParseResponse<T>(string jsonResponse) where T : class;
}

/// <summary>
/// DM Agent — 战斗叙述生成器。接收战斗日志，生成中文叙事文本。
/// </summary>
public class DMAgent : LLMAgent
{
    public override string AgentId => "dm_agent";

    public override string SystemPrompt =>
        "你是一位经验丰富的地下城主，为《酒馆与命运》DND 5e 游戏生成沉浸式中文叙事。" +
        "基于战斗日志和角色动作，生成生动的战斗描述。" +
        "保持 TRPG DM 风格：简洁、感官丰富（视觉/听觉/触觉）。" +
        "永远不要决定数值结果或故事方向。只生成叙事文本。";

    public override object BuildPayload(object context)
    {
        return new
        {
            system = SystemPrompt,
            context = context,
            max_tokens = 500,
            temperature = 0.7
        };
    }

    public override T? ParseResponse<T>(string jsonResponse) where T : class
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<T>(jsonResponse);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// DM Agent 输出结构。
/// </summary>
public record DMNarrativeResponse
{
    public string Narrative { get; init; } = "";
    public string ResponseType { get; init; } = "combat_narration";
}
