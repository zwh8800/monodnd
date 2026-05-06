using Xunit;
using FluentAssertions;
using DndGame.Systems.Combat;

namespace DndGame.Tests.Unit.Combat;

public class CombatFSMTests
{
    private static CombatFSM CreateFSM()
    {
        var fsm = new CombatFSM();

        // 注册所有合法转换的守卫（默认返回 true）
        fsm.RegisterGuard(CombatState.Initialization, CombatState.RollInitiative, () => true);
        fsm.RegisterGuard(CombatState.RollInitiative, CombatState.RoundStart, () => true);
        fsm.RegisterGuard(CombatState.RoundStart, CombatState.SimultaneousSelection, () => true);
        fsm.RegisterGuard(CombatState.SimultaneousSelection, CombatState.ActionPhase, () => true);
        fsm.RegisterGuard(CombatState.ActionPhase, CombatState.BonusActionPhase, () => true);
        fsm.RegisterGuard(CombatState.ActionPhase, CombatState.TurnEnd, () => true);
        fsm.RegisterGuard(CombatState.BonusActionPhase, CombatState.MovementPhase, () => true);
        fsm.RegisterGuard(CombatState.BonusActionPhase, CombatState.TurnEnd, () => true);
        fsm.RegisterGuard(CombatState.MovementPhase, CombatState.ReactionWindow, () => true);
        fsm.RegisterGuard(CombatState.MovementPhase, CombatState.TurnEnd, () => true);
        fsm.RegisterGuard(CombatState.ReactionWindow, CombatState.TurnEnd, () => true);
        fsm.RegisterGuard(CombatState.TurnEnd, CombatState.RoundStart, () => true);
        fsm.RegisterGuard(CombatState.TurnEnd, CombatState.RoundEnd, () => true);
        fsm.RegisterGuard(CombatState.RoundEnd, CombatState.RollInitiative, () => true);
        fsm.RegisterGuard(CombatState.RoundEnd, CombatState.Victory, () => true);
        fsm.RegisterGuard(CombatState.RoundEnd, CombatState.Defeat, () => true);

        return fsm;
    }

    [Fact]
    public void Transition_ValidMove_Succeeds()
    {
        // Arrange
        var fsm = CreateFSM();

        // Act
        fsm.Transition(CombatState.RollInitiative);

        // Assert
        fsm.CurrentState.Should().Be(CombatState.RollInitiative);
    }

    [Fact]
    public void Transition_InvalidMove_ThrowsException()
    {
        // Arrange
        var fsm = CreateFSM();

        // Act
        var act = () => fsm.Transition(CombatState.Victory);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*无法从*");
    }

    [Fact]
    public void Transition_ValidPath_FollowsSequence()
    {
        // Arrange
        var fsm = CreateFSM();

        // Act
        fsm.Transition(CombatState.RollInitiative);
        fsm.Transition(CombatState.RoundStart);

        // Assert
        fsm.CurrentState.Should().Be(CombatState.RoundStart);
    }

    [Fact]
    public void Transition_WithGuard_PassesWhenTrue()
    {
        // Arrange
        var fsm = CreateFSM();
        fsm.RegisterGuard(CombatState.Initialization, CombatState.RollInitiative, () => true);

        // Act
        fsm.CanTransition(CombatState.RollInitiative).Should().BeTrue();
        fsm.Transition(CombatState.RollInitiative);

        // Assert
        fsm.CurrentState.Should().Be(CombatState.RollInitiative);
    }

    [Fact]
    public void Transition_WithGuard_FailsWhenFalse()
    {
        // Arrange
        var fsm = CreateFSM();
        fsm.RegisterGuard(CombatState.Initialization, CombatState.RollInitiative, () => false);

        // Act
        fsm.CanTransition(CombatState.RollInitiative).Should().BeFalse();
        var act = () => fsm.Transition(CombatState.RollInitiative);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void OnStateEntered_FiresOnTransition()
    {
        // Arrange
        var fsm = CreateFSM();
        CombatState? enteredState = null;
        fsm.OnStateEntered += state => enteredState = state;

        // Act
        fsm.Transition(CombatState.RollInitiative);

        // Assert
        enteredState.Should().Be(CombatState.RollInitiative);
    }

    [Fact]
    public void Transition_FullRound_CyclesBack()
    {
        // Arrange
        var fsm = CreateFSM();

        // Act — 完成一个完整回合
        fsm.Transition(CombatState.RollInitiative);
        fsm.Transition(CombatState.RoundStart);
        fsm.Transition(CombatState.SimultaneousSelection);
        fsm.Transition(CombatState.ActionPhase);
        fsm.Transition(CombatState.TurnEnd);
        fsm.Transition(CombatState.RoundEnd);
        fsm.Transition(CombatState.RollInitiative); // 下一轮

        // Assert
        fsm.CurrentState.Should().Be(CombatState.RollInitiative);
    }

    [Fact]
    public void Transition_TerminalStates_Reachable()
    {
        // Arrange
        var fsm = CreateFSM();

        // Act — 到达 Victory
        fsm.Transition(CombatState.RollInitiative);
        fsm.Transition(CombatState.RoundStart);
        fsm.Transition(CombatState.SimultaneousSelection);
        fsm.Transition(CombatState.ActionPhase);
        fsm.Transition(CombatState.TurnEnd);
        fsm.Transition(CombatState.RoundEnd);
        fsm.Transition(CombatState.Victory);

        // Assert
        fsm.CurrentState.Should().Be(CombatState.Victory);
    }
}
