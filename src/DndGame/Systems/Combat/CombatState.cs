namespace DndGame.Systems.Combat;

/// <summary>
/// 战斗状态枚举，定义战斗流程中的 16 种离散状态。
/// </summary>
public enum CombatState
{
    Initialization,
    RollInitiative,
    RoundStart,
    SimultaneousSelection,
    ActionPhase,
    BonusActionPhase,
    MovementPhase,
    ReactionWindow,
    TurnEnd,
    RoundEnd,
    Victory,
    Defeat,
    Retreat,
    WaitingForInput,
    AnimationPlaying,
    Reinforcement
}
