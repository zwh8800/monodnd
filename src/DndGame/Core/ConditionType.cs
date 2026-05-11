namespace DndGame.Core;

/// <summary>
/// DND 5e 的 14 种基础状态条件。
/// 此枚举定义于 CORE 层，因为 ConditionTracker 是 CORE 层模块，
/// 且角色系统（CORE 层）需要引用条件类型。
/// </summary>
public enum Condition
{
    Blinded, Charmed, Deafened, Frightened, Grappled,
    Incapacitated, Invisible, Paralyzed, Petrified, Poisoned,
    Prone, Restrained, Stunned, Unconscious
}
