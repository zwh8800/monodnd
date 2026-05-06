using Xunit;
using FluentAssertions;
using DndGame.Systems.Combat;

namespace DndGame.Tests.Unit.Combat;

public class ConditionSystemTests
{
    [Fact]
    public void AddCondition_NewCondition_Applies()
    {
        // Arrange
        var system = new ConditionSystem();

        // Act
        system.AddCondition("char_01", Condition.Poisoned, "spell_poison", 3);

        // Assert
        system.HasCondition("char_01", Condition.Poisoned).Should().BeTrue();
    }

    [Fact]
    public void RemoveCondition_Existing_Removes()
    {
        // Arrange
        var system = new ConditionSystem();
        system.AddCondition("char_01", Condition.Poisoned, "spell_poison", 3);

        // Act
        system.RemoveCondition("char_01", Condition.Poisoned);

        // Assert
        system.HasCondition("char_01", Condition.Poisoned).Should().BeFalse();
    }

    [Fact]
    public void AddCondition_ExpiredByTick_Removed()
    {
        // Arrange
        var system = new ConditionSystem();
        system.AddCondition("char_01", Condition.Poisoned, "spell_poison", 1);

        // Act
        system.TickRound();

        // Assert
        system.HasCondition("char_01", Condition.Poisoned).Should().BeFalse();
    }

    [Fact]
    public void AddCondition_SameConditionSameSource_RefreshesDuration()
    {
        // Arrange
        var system = new ConditionSystem();
        system.AddCondition("char_01", Condition.Poisoned, "spell_poison", 2);

        // Act — 同源重新施加，刷新为 5 轮
        system.AddCondition("char_01", Condition.Poisoned, "spell_poison", 5);

        // Assert
        var conditions = system.GetConditions("char_01");
        conditions.Should().HaveCount(1);
        conditions[0].RemainingRounds.Should().Be(5);
    }

    [Fact]
    public void TickRound_DecrementsDuration()
    {
        // Arrange
        var system = new ConditionSystem();
        system.AddCondition("char_01", Condition.Frightened, "dragon_roar", 3);

        // Act
        system.TickRound();

        // Assert
        var conditions = system.GetConditions("char_01");
        conditions.Should().HaveCount(1);
        conditions[0].RemainingRounds.Should().Be(2);
    }

    [Fact]
    public void HasCondition_DifferentConditions_Independent()
    {
        // Arrange
        var system = new ConditionSystem();

        // Act
        system.AddCondition("char_01", Condition.Poisoned, "spell", 3);
        system.AddCondition("char_01", Condition.Frightened, "dragon", 2);

        // Assert
        system.HasCondition("char_01", Condition.Poisoned).Should().BeTrue();
        system.HasCondition("char_01", Condition.Frightened).Should().BeTrue();
        system.HasCondition("char_01", Condition.Blinded).Should().BeFalse();
    }

    [Fact]
    public void GetConditions_ReturnsAllActive()
    {
        // Arrange
        var system = new ConditionSystem();
        system.AddCondition("char_01", Condition.Poisoned, "s1", 3);
        system.AddCondition("char_01", Condition.Frightened, "s2", 2);
        system.AddCondition("char_01", Condition.Prone, "s3", 1);

        // Act
        var conditions = system.GetConditions("char_01");

        // Assert
        conditions.Should().HaveCount(3);
        conditions.Should().Contain(c => c.Condition == Condition.Poisoned);
        conditions.Should().Contain(c => c.Condition == Condition.Frightened);
        conditions.Should().Contain(c => c.Condition == Condition.Prone);
    }

    [Fact]
    public void ConditionApplied_EventFires()
    {
        // Arrange
        var system = new ConditionSystem();
        string? firedCombatant = null;
        Condition? firedCondition = null;
        system.ConditionApplied += (id, cond) => { firedCombatant = id; firedCondition = cond; };

        // Act
        system.AddCondition("char_01", Condition.Stunned, "spell", 2);

        // Assert
        firedCombatant.Should().Be("char_01");
        firedCondition.Should().Be(Condition.Stunned);
    }
}
