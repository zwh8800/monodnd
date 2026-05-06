using System.Text.Json.Serialization;

namespace DndGame.UI.Widgets;

/// <summary>
/// 冒险模板数据。
/// </summary>
public record AdventureTemplate
{
    [JsonPropertyName("adventure_id")]
    public string AdventureId { get; init; } = "";

    [JsonPropertyName("title")]
    public string Title { get; init; } = "";

    [JsonPropertyName("description")]
    public string Description { get; init; } = "";

    [JsonPropertyName("recommended_level")]
    public int RecommendedLevel { get; init; } = 1;

    [JsonPropertyName("estimated_duration")]
    public string EstimatedDuration { get; init; } = "";

    [JsonPropertyName("enemy_types")]
    public List<string> EnemyTypes { get; init; } = new();

    [JsonPropertyName("node_count")]
    public int NodeCount { get; init; }
}

/// <summary>
/// 任务板管理器，管理可接任务列表。
/// </summary>
public class QuestBoardManager
{
    private readonly List<AdventureTemplate> _adventures = new();

    public IReadOnlyList<AdventureTemplate> Adventures => _adventures;

    /// <summary>
    /// 加载冒险模板。
    /// </summary>
    public void LoadAdventures(IEnumerable<AdventureTemplate> adventures)
    {
        _adventures.Clear();
        _adventures.AddRange(adventures);
    }

    /// <summary>
    /// 获取指定等级范围的冒险。
    /// </summary>
    public IReadOnlyList<AdventureTemplate> GetAdventuresForLevel(int level)
    {
        return _adventures.Where(a => a.RecommendedLevel <= level + 1 && a.RecommendedLevel >= level - 1).ToList();
    }
}
