using System.Text.Json;
using System.Text.Json.Serialization;

namespace DndGame.Systems.Character;

/// <summary>
/// 种族配置数据。
/// </summary>
public record RaceConfig
{
    [JsonPropertyName("race_id")]
    public string RaceId { get; init; } = "";

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("speed")]
    public int Speed { get; init; } = 30;

    [JsonPropertyName("ability_increases")]
    public Dictionary<string, int> AbilityIncreases { get; init; } = new();

    [JsonPropertyName("traits")]
    public List<string> Traits { get; init; } = new();
}

/// <summary>
/// 职业配置数据。
/// </summary>
public record ClassConfig
{
    [JsonPropertyName("class_id")]
    public string ClassId { get; init; } = "";

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("hit_die")]
    public int HitDie { get; init; }

    [JsonPropertyName("primary_ability")]
    public string PrimaryAbility { get; init; } = "";

    [JsonPropertyName("saving_throw_proficiencies")]
    public List<string> SavingThrowProficiencies { get; init; } = new();

    [JsonPropertyName("skill_choices")]
    public int SkillChoices { get; init; }

    [JsonPropertyName("features_per_level")]
    public Dictionary<string, List<string>> FeaturesPerLevel { get; init; } = new();
}

/// <summary>
/// 角色生成器，组合种族和职业配置生成完整的 CharacterData。
/// </summary>
public class CharacterGenerator
{
    private readonly Dictionary<string, RaceConfig> _races = new();
    private readonly Dictionary<string, ClassConfig> _classes = new();

    /// <summary>
    /// 加载种族配置。
    /// </summary>
    public void LoadRaces(IEnumerable<RaceConfig> races)
    {
        foreach (var race in races)
            _races[race.RaceId] = race;
    }

    /// <summary>
    /// 加载职业配置。
    /// </summary>
    public void LoadClasses(IEnumerable<ClassConfig> classes)
    {
        foreach (var cls in classes)
            _classes[cls.ClassId] = cls;
    }

    /// <summary>
    /// 生成角色数值层。使用 Standard Array 分配属性，然后叠加种族加值。
    /// </summary>
    public CharacterData Generate(string raceId, string classId, int level = 1)
    {
        if (!_races.TryGetValue(raceId, out var race))
            throw new ArgumentException($"未知种族: {raceId}");
        if (!_classes.TryGetValue(classId, out var cls))
            throw new ArgumentException($"未知职业: {classId}");

        // 使用 Standard Array 分配属性
        var abilities = AllocateAbilities(race, cls);

        // 计算衍生值
        var conMod = NumericalGenerator.GetModifier(abilities[Ability.Con].Score);
        var dexMod = NumericalGenerator.GetModifier(abilities[Ability.Dex].Score);
        var hp = NumericalGenerator.CalculateHP(cls.HitDie, conMod, level);
        var ac = NumericalGenerator.CalculateAC(dexMod);
        var pb = NumericalGenerator.GetProficiencyBonus(level);

        return new CharacterData
        {
            CharacterId = $"char_{Guid.NewGuid():N}",
            Narrative = new CharacterNarrative
            {
                Name = $"{race.Name} {cls.Name}",
                Race = raceId
            },
            Stats = new CharacterStats
            {
                Level = level,
                ProficiencyBonus = pb,
                Speed = race.Speed,
                ArmorClass = ac,
                HitPoints = new HitPoints { Max = hp, Current = hp },
                Abilities = abilities
            }
        };
    }

    /// <summary>
    /// 分配 Standard Array 属性值，职业主属性优先，然后叠加种族加值。
    /// </summary>
    private Dictionary<Ability, AbilityScore> AllocateAbilities(RaceConfig race, ClassConfig cls)
    {
        var array = new List<int>(NumericalGenerator.StandardArray);
        var result = new Dictionary<Ability, AbilityScore>();

        // 职业主属性获得最高值
        var primary = ParseAbility(cls.PrimaryAbility);
        result[primary] = new AbilityScore { Score = array[0] };
        array.RemoveAt(0);

        // 剩余属性随机分配
        var remaining = Enum.GetValues<Ability>().Where(a => a != primary).ToList();
        var random = new Random();
        foreach (var ability in remaining)
        {
            var index = random.Next(array.Count);
            result[ability] = new AbilityScore { Score = array[index] };
            array.RemoveAt(index);
        }

        // 叠加种族加值
        foreach (var (abilityName, bonus) in race.AbilityIncreases)
        {
            var ability = ParseAbility(abilityName);
            var current = result[ability].Score;
            result[ability] = new AbilityScore { Score = Math.Min(20, current + bonus) };
        }

        return result;
    }

    private static Ability ParseAbility(string ability) => ability.ToLowerInvariant() switch
    {
        "str" => Ability.Str,
        "dex" => Ability.Dex,
        "con" => Ability.Con,
        "int" => Ability.Int,
        "wis" => Ability.Wis,
        "cha" => Ability.Cha,
        _ => throw new ArgumentException($"未知属性: {ability}")
    };
}
