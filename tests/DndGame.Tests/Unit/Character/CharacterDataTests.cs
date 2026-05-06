using Xunit;
using FluentAssertions;
using DndGame.Systems.Character;

namespace DndGame.Tests.Unit.Character;

public class CharacterDataTests
{
    [Fact]
    public void AbilityScore_Score10_ModifierZero()
    {
        // Arrange & Act
        var score = new AbilityScore { Score = 10 };

        // Assert
        score.Modifier.Should().Be(0);
    }

    [Fact]
    public void AbilityScore_Score14_ModifierPlus2()
    {
        // Arrange & Act
        var score = new AbilityScore { Score = 14 };

        // Assert
        score.Modifier.Should().Be(2);
    }

    [Fact]
    public void AbilityScore_Score8_ModifierMinus1()
    {
        // Arrange & Act
        var score = new AbilityScore { Score = 8 };

        // Assert
        score.Modifier.Should().Be(-1);
    }

    [Fact]
    public void AbilityScore_Score20_ModifierPlus5()
    {
        // Arrange & Act
        var score = new AbilityScore { Score = 20 };

        // Assert
        score.Modifier.Should().Be(5);
    }

    [Fact]
    public void AbilityScore_Score1_ModifierMinus5()
    {
        // Arrange & Act
        var score = new AbilityScore { Score = 1 };

        // Assert
        score.Modifier.Should().Be(-5);
    }

    [Fact]
    public void CharacterData_DefaultValues_HasExpectedInitials()
    {
        // Arrange & Act
        var data = new CharacterData();

        // Assert
        data.Status.Should().Be(CharacterStatus.Alive);
        data.Stats.Level.Should().Be(1);
        data.Stats.ProficiencyBonus.Should().Be(2);
        data.Narrative.Name.Should().Be("");
    }

    [Fact]
    public void CharacterData_InitOnlyProperties_Immutable()
    {
        // Arrange
        var data = new CharacterData
        {
            CharacterId = "char_01",
            Narrative = new CharacterNarrative { Name = "测试角色" }
        };

        // Act & Assert — record 的 with 表达式可以创建修改副本
        var modified = data with { CharacterId = "char_02" };
        data.CharacterId.Should().Be("char_01");
        modified.CharacterId.Should().Be("char_02");
    }
}
