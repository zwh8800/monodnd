using DndGame.Systems.Character;

namespace DndGame.Systems.Combat;

/// <summary>
/// AI 行为目标选择策略。
/// </summary>
public enum TargetStrategy
{
    Nearest,
    LowestHP,
    HighestThreat,
    Random
}

/// <summary>
/// 敌人 AI 类型。
/// </summary>
public enum EnemyType
{
    Melee,
    Ranged,
    Caster
}

/// <summary>
/// AI 行为系统，负责敌人决策：目标选择、行动选择、移动策略。
/// </summary>
public class AISystem
{
    private readonly Random _random = new();

    /// <summary>
    /// 选择攻击目标。按策略从可用目标中选择。
    /// </summary>
    public ICombatant? SelectTarget(ICombatant self, IReadOnlyList<ICombatant> enemies, TargetStrategy strategy)
    {
        if (enemies.Count == 0) return null;

        return strategy switch
        {
            TargetStrategy.Nearest => enemies.OrderBy(e => Distance(self, e)).First(),
            TargetStrategy.LowestHP => enemies.OrderBy(e => e.CurrentHp).First(),
            TargetStrategy.HighestThreat => enemies.OrderByDescending(e => e.CurrentHp).First(),
            TargetStrategy.Random => enemies[_random.Next(enemies.Count)],
            _ => enemies[0]
        };
    }

    /// <summary>
    /// 决定 AI 行动。返回行动类型：attack（攻击）、cast（施法）、retreat（撤退）、wait（等待）。
    /// </summary>
    public string DecideAction(ICombatant self, IReadOnlyList<ICombatant> enemies, EnemyType enemyType)
    {
        // HP 低于 20% 时有概率撤退
        if (self.CurrentHp < self.MaxHp * 0.2 && _random.NextDouble() < 0.4)
            return "retreat";

        if (enemies.Count == 0)
            return "wait";

        return enemyType switch
        {
            EnemyType.Melee => "attack",
            EnemyType.Ranged => ShouldKeepDistance(self, enemies) ? "attack" : "attack",
            EnemyType.Caster => _random.NextDouble() < 0.6 ? "cast" : "attack",
            _ => "attack"
        };
    }

    /// <summary>
    /// 计算移动方向。近战型向目标移动，远程型保持距离。
    /// </summary>
    public (int dx, int dy) CalculateMovement(ICombatant self, ICombatant target, EnemyType enemyType)
    {
        var (sx, sy) = GetPosition(self);
        var (tx, ty) = GetPosition(target);

        if (enemyType == EnemyType.Ranged && Distance(self, target) < 3)
        {
            // 远程型保持距离：远离目标
            return (Math.Sign(sx - tx), Math.Sign(sy - ty));
        }

        // 近战型/施法型：向目标移动
        return (Math.Sign(tx - sx), Math.Sign(ty - sy));
    }

    private static int Distance(ICombatant a, ICombatant b)
    {
        var (ax, ay) = GetPosition(a);
        var (bx, by) = GetPosition(b);
        return Math.Abs(ax - bx) + Math.Abs(ay - by);
    }

    private static (int x, int y) GetPosition(ICombatant combatant)
    {
        // ICombatant 没有位置信息，使用 CombatantId 的哈希作为伪位置
        // 实际实现应从 Entity.Position 获取
        var hash = combatant.CombatantId.GetHashCode();
        return (Math.Abs(hash % 20), Math.Abs((hash / 20) % 20));
    }

    private bool ShouldKeepDistance(ICombatant self, IReadOnlyList<ICombatant> enemies)
    {
        var nearest = enemies.MinBy(e => Distance(self, e));
        return nearest != null && Distance(self, nearest) >= 3;
    }
}
