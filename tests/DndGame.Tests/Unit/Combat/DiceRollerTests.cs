using Xunit;
using FluentAssertions;
using DndGame.Systems.Combat;

namespace DndGame.Tests.Unit.Combat;

public class DiceRollerTests
{
    [Fact]
    public void Roll_ReturnsValueInRange()
    {
        // Arrange & Act
        var results = Enumerable.Range(0, 1000).Select(_ => DiceRoller.Roll(20)).ToArray();

        // Assert
        results.Should().AllSatisfy(r => r.Should().BeInRange(1, 20));
    }

    [Fact]
    public void RollAttack_Advantage_TakesHigherOfTwoDice()
    {
        // Arrange & Act
        var result = DiceRoller.RollAttack(0, hasAdvantage: true, hasDisadvantage: false);

        // Assert
        result.Rolls.Should().HaveCount(2);
        result.RawValue.Should().Be(Math.Max(result.Rolls[0], result.Rolls[1]));
        result.Advantage.Should().BeTrue();
    }

    [Fact]
    public void RollAttack_Disadvantage_TakesLowerOfTwoDice()
    {
        // Arrange & Act
        var result = DiceRoller.RollAttack(0, hasAdvantage: false, hasDisadvantage: true);

        // Assert
        result.Rolls.Should().HaveCount(2);
        result.RawValue.Should().Be(Math.Min(result.Rolls[0], result.Rolls[1]));
        result.Disadvantage.Should().BeTrue();
    }

    [Fact]
    public void RollAttack_BothAdvantageAndDisadvantage_SingleDie()
    {
        // Arrange & Act
        var result = DiceRoller.RollAttack(0, hasAdvantage: true, hasDisadvantage: true);

        // Assert
        result.Rolls.Should().HaveCount(1);
        result.RawValue.Should().Be(result.Rolls[0]);
    }

    [Fact]
    public void RollAttack_Natural20_IsCritical()
    {
        // Arrange — 掷足够多次以确保出现自然20
        for (int i = 0; i < 1000; i++)
        {
            var result = DiceRoller.RollAttack(0, false, false);
            if (result.RawValue == 20)
            {
                // Assert
                result.IsCritical.Should().BeTrue();
                result.IsCriticalMiss.Should().BeFalse();
                return;
            }
        }
        // 如果1000次都没掷出20，测试失败（概率极低）
        Assert.Fail("1000次投掷未出现自然20");
    }

    [Fact]
    public void RollAttack_Natural1_IsCriticalMiss()
    {
        // Arrange — 掷足够多次以确保出现自然1
        for (int i = 0; i < 1000; i++)
        {
            var result = DiceRoller.RollAttack(0, false, false);
            if (result.RawValue == 1)
            {
                // Assert
                result.IsCriticalMiss.Should().BeTrue();
                result.IsCritical.Should().BeFalse();
                return;
            }
        }
        Assert.Fail("1000次投掷未出现自然1");
    }

    [Fact]
    public void RollDamage_TwoD6Plus3_ReturnsValidRange()
    {
        // Arrange & Act
        var results = Enumerable.Range(0, 1000)
            .Select(_ => DiceRoller.RollDamage("2d6+3", 0))
            .ToArray();

        // Assert — 2d6+3 范围: [2+3, 12+3] = [5, 15]
        results.Should().AllSatisfy(r =>
        {
            r.Total.Should().BeInRange(5, 15);
            r.Rolls.Should().HaveCount(2);
            r.Rolls.Should().AllSatisfy(d => d.Should().BeInRange(1, 6));
            r.Bonus.Should().Be(3);
        });
    }

    [Fact]
    public void RollDamage_OneD8Plus2_ReturnsValidRange()
    {
        // Arrange & Act
        var results = Enumerable.Range(0, 1000)
            .Select(_ => DiceRoller.RollDamage("1d8", 2))
            .ToArray();

        // Assert — 1d8+2 范围: [1+2, 8+2] = [3, 10]
        results.Should().AllSatisfy(r =>
        {
            r.Total.Should().BeInRange(3, 10);
            r.Rolls.Should().HaveCount(1);
            r.Rolls[0].Should().BeInRange(1, 8);
            r.Bonus.Should().Be(2);
        });
    }

    [Fact]
    public void ParseDiceExpression_ValidFormat_ParsesCorrectly()
    {
        // Arrange & Act
        var (count, sides, bonus) = DiceRoller.ParseDiceExpression("2d6+3");

        // Assert
        count.Should().Be(2);
        sides.Should().Be(6);
        bonus.Should().Be(3);
    }

    [Fact]
    public void ParseDiceExpression_NoBonus_ParsesCorrectly()
    {
        // Arrange & Act
        var (count, sides, bonus) = DiceRoller.ParseDiceExpression("1d20");

        // Assert
        count.Should().Be(1);
        sides.Should().Be(20);
        bonus.Should().Be(0);
    }
}
