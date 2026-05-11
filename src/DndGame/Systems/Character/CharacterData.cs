using DndGame.Core;

namespace DndGame.Systems.Character;

/// <summary>
/// 属性值与调整值。调整值由公式 floor((score - 10) / 2.0) 计算，不存储。
/// </summary>
public record AbilityScore
{
    public int Score { get; init; }

    /// <summary>
    /// 属性调整值 = floor((Score - 10) / 2.0)。计算属性，非存储属性。
    /// </summary>
    public int Modifier => (int)Math.Floor((Score - 10) / 2.0);
}

/// <summary>
/// 生命值：最大值、当前值、临时生命值。
/// </summary>
public record HitPoints
{
    public int Max { get; init; }
    public int Current { get; init; }
    public int Temporary { get; init; }
}

/// <summary>
/// 角色核心数值属性。
/// </summary>
public record CharacterStats
{
    public int Level { get; init; } = 1;
    public int Xp { get; init; }
    public int XpToNext { get; init; }
    public int ProficiencyBonus { get; init; } = 2;
    public int Speed { get; init; } = 30;
    public int ArmorClass { get; init; }
    public HitPoints HitPoints { get; init; } = new();
    public Dictionary<Ability, AbilityScore> Abilities { get; init; } = new();
    public int ExhaustionLevel { get; init; }
}

/// <summary>
/// 角色叙事层数据：名称、种族、背景故事等。由 LLM 或模板生成。
/// </summary>
public record CharacterNarrative
{
    public string Name { get; init; } = "";
    public string Race { get; init; } = "";
    public string Gender { get; init; } = "";
    public int Age { get; init; }
    public List<string> PersonalityTags { get; init; } = new();
    public string Backstory { get; init; } = "";
}

/// <summary>
/// 角色完整数据模型，包含数值层和叙事层。
/// 所有属性为 init-only，保证不可变性。
/// </summary>
public record CharacterData
{
    public string CharacterId { get; init; } = "";
    public CharacterStatus Status { get; init; } = CharacterStatus.Alive;
    public CharacterNarrative Narrative { get; init; } = new();
    public CharacterStats Stats { get; init; } = new();
    public List<Condition> Conditions { get; init; } = new();
}
