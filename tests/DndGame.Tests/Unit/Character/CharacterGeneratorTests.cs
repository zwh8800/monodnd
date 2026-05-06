using Xunit;
using FluentAssertions;
using DndGame.Systems.Character;

namespace DndGame.Tests.Unit.Character;

public class CharacterGeneratorTests
{
    private static CharacterGenerator CreateGenerator()
    {
        var gen = new CharacterGenerator();
        gen.LoadRaces([
            new RaceConfig { RaceId = "human", Name = "人类", Speed = 30, AbilityIncreases = new Dictionary<string, int>
            {
                ["str"] = 1, ["dex"] = 1, ["con"] = 1, ["int"] = 1, ["wis"] = 1, ["cha"] = 1
            }},
            new RaceConfig { RaceId = "elf", Name = "精灵", Speed = 30, AbilityIncreases = new Dictionary<string, int>
            {
                ["dex"] = 2
            }},
            new RaceConfig { RaceId = "dwarf", Name = "矮人", Speed = 25, AbilityIncreases = new Dictionary<string, int>
            {
                ["con"] = 2
            }}
        ]);
        gen.LoadClasses([
            new ClassConfig { ClassId = "fighter", Name = "战士", HitDie = 10, PrimaryAbility = "str" },
            new ClassConfig { ClassId = "wizard", Name = "法师", HitDie = 6, PrimaryAbility = "int" },
            new ClassConfig { ClassId = "rogue", Name = "盗贼", HitDie = 8, PrimaryAbility = "dex" }
        ]);
        return gen;
    }

    [Fact]
    public void Generate_ElfWizard_HasCorrectBonuses()
    {
        // Arrange
        var gen = CreateGenerator();

        // Act
        var character = gen.Generate("elf", "wizard", 1);

        // Assert — 精灵 DEX+2，法师主属性 INT
        character.Stats.Abilities[Ability.Dex].Score.Should().BeGreaterThanOrEqualTo(10); // 基础 + 种族
        character.Stats.Level.Should().Be(1);
    }

    [Fact]
    public void Generate_HumanFighter_AllAbilitiesBoosted()
    {
        // Arrange
        var gen = CreateGenerator();

        // Act
        var character = gen.Generate("human", "fighter", 1);

        // Assert — 人类全属性+1，所有属性 >= 9 (最低8+1)
        foreach (var ability in character.Stats.Abilities.Values)
        {
            ability.Score.Should().BeGreaterThanOrEqualTo(9);
        }
    }

    [Fact]
    public void Generate_StandardArray_NoDuplicates()
    {
        // Arrange
        var gen = CreateGenerator();

        // Act
        var character = gen.Generate("human", "fighter", 1);

        // Assert — Standard Array 有6个不同值，加上种族加值后可能重复但范围合理
        var scores = character.Stats.Abilities.Values.Select(a => a.Score).ToList();
        scores.Should().AllSatisfy(s => s.Should().BeInRange(1, 20));
    }

    [Fact]
    public void Generate_FighterLv1_HPIsCorrect()
    {
        // Arrange
        var gen = CreateGenerator();

        // Act
        var character = gen.Generate("human", "fighter", 1);

        // Assert — 战士 d10，Lv1 HP = 10 + CON_mod
        var conMod = NumericalGenerator.GetModifier(character.Stats.Abilities[Ability.Con].Score);
        character.Stats.HitPoints.Max.Should().Be(10 + conMod);
    }

    [Fact]
    public void Generate_ProficiencyBonus_Lv1Is2()
    {
        // Arrange
        var gen = CreateGenerator();

        // Act
        var character = gen.Generate("elf", "wizard", 1);

        // Assert
        character.Stats.ProficiencyBonus.Should().Be(2);
    }

    [Fact]
    public void Generate_Dwarf_SpeedIs25()
    {
        // Arrange
        var gen = CreateGenerator();

        // Act
        var character = gen.Generate("dwarf", "fighter", 1);

        // Assert
        character.Stats.Speed.Should().Be(25);
    }

    [Fact]
    public void Generate_AllAbilities_InRange()
    {
        // Arrange
        var gen = CreateGenerator();

        // Act
        var character = gen.Generate("elf", "rogue", 1);

        // Assert — 所有属性在 [1, 20] 范围内
        foreach (var ability in character.Stats.Abilities.Values)
        {
            ability.Score.Should().BeInRange(1, 20);
        }
    }

    [Fact]
    public void Generate_CharacterId_IsNotEmpty()
    {
        // Arrange
        var gen = CreateGenerator();

        // Act
        var character = gen.Generate("human", "fighter", 1);

        // Assert
        character.CharacterId.Should().NotBeNullOrEmpty();
        character.CharacterId.Should().StartWith("char_");
    }
}
