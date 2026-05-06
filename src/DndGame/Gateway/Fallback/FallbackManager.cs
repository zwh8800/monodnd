namespace DndGame.Gateway.Fallback;

/// <summary>
/// 降级管理器，当 LLM API 不可用时提供静态模板回退。
/// </summary>
public class FallbackManager
{
    private static readonly Dictionary<string, List<string>> _templates = new()
    {
        ["combat_narration"] = new()
        {
            "挥舞武器，向敌人发起猛烈攻击！",
            "箭矢划破空气，精准命中目标。",
            "法术能量在手中凝聚，释放出耀眼光芒。",
            "敌人倒下了，战斗胜利！"
        },
        ["scene_atmosphere"] = new()
        {
            "阴暗的地牢中弥漫着潮湿的气息。",
            "火把的光芒在墙壁上投下摇曳的影子。",
            "远处传来低沉的咆哮声。",
            "空气中弥漫着腐朽的味道。"
        }
    };

    /// <summary>
    /// 获取降级模板。
    /// </summary>
    public string GetFallback(string agentId, string responseType)
    {
        if (_templates.TryGetValue(responseType, out var templates))
        {
            var index = Random.Shared.Next(templates.Count);
            return templates[index];
        }
        return "战斗继续进行...";
    }
}
