using DndGame.Systems.Character;
using DndGame.Systems.Combat;

namespace DndGame.UI.Widgets;

/// <summary>
/// 角色面板数据视图，提供格式化的角色信息供 UI 显示。
/// </summary>
public class CharacterPanelViewModel
{
    private readonly CharacterData _data;

    public CharacterPanelViewModel(CharacterData data)
    {
        _data = data;
    }

    public string Name => _data.Narrative.Name;
    public string Race => _data.Narrative.Race;
    public int Level => _data.Stats.Level;
    public string Status => _data.Status.ToString();

    public int HP => _data.Stats.HitPoints.Current;
    public int MaxHP => _data.Stats.HitPoints.Max;
    public int AC => _data.Stats.ArmorClass;
    public int ProficiencyBonus => _data.Stats.ProficiencyBonus;
    public int Speed => _data.Stats.Speed;

    /// <summary>
    /// 获取属性调整值。
    /// </summary>
    public int GetModifier(Ability ability)
    {
        return _data.Stats.Abilities.TryGetValue(ability, out var score) ? score.Modifier : 0;
    }

    /// <summary>
    /// 获取属性值。
    /// </summary>
    public int GetScore(Ability ability)
    {
        return _data.Stats.Abilities.TryGetValue(ability, out var score) ? score.Score : 10;
    }

    /// <summary>
    /// 格式化属性字符串，如 "STR 16 (+3)"。
    /// </summary>
    public string FormatAbility(Ability ability)
    {
        var score = GetScore(ability);
        var mod = GetModifier(ability);
        var sign = mod >= 0 ? "+" : "";
        return $"{ability} {score} ({sign}{mod})";
    }

    /// <summary>
    /// 格式化 HP 字符串，如 "28/30"。
    /// </summary>
    public string FormatHP() => $"{HP}/{MaxHP}";
}
