using DndGame.Core;

namespace DndGame.Systems.Combat;

// Condition 枚举已移至 Core/ConditionType.cs（CORE 层共享类型）

/// <summary>
/// 条件实例，记录条件类型、来源、剩余持续时间。
/// </summary>
public record ConditionInstance
{
    public Condition Condition { get; init; }
    public string SourceId { get; init; } = "";
    public int RemainingRounds { get; init; }
    public int StackCount { get; init; } = 1;
}

/// <summary>
/// 条件追踪系统，管理战斗中角色的状态条件。
/// 支持施加、移除、到期自动移除、回合结束时递减持续时间。
/// 同名条件不堆叠，同源刷新持续时间。
/// </summary>
public class ConditionSystem
{
    private readonly Dictionary<string, List<ConditionInstance>> _conditions = new();

    /// <summary>
    /// 为战斗角色施加条件。同名同源条件刷新持续时间，同名不同源取较长者。
    /// </summary>
    public void AddCondition(string combatantId, Condition condition, string sourceId, int durationRounds)
    {
        if (!_conditions.ContainsKey(combatantId))
            _conditions[combatantId] = new List<ConditionInstance>();

        var list = _conditions[combatantId];

        // 检查是否已有同名条件
        var existing = list.FirstOrDefault(c => c.Condition == condition);
        if (existing != null)
        {
            // 同源：刷新持续时间
            if (existing.SourceId == sourceId)
            {
                list.Remove(existing);
                list.Add(new ConditionInstance
                {
                    Condition = condition,
                    SourceId = sourceId,
                    RemainingRounds = durationRounds
                });
            }
            // 不同源：保留持续时间较长者
            else if (durationRounds > existing.RemainingRounds)
            {
                list.Remove(existing);
                list.Add(new ConditionInstance
                {
                    Condition = condition,
                    SourceId = sourceId,
                    RemainingRounds = durationRounds
                });
            }
            // 否则忽略（已有条件持续时间更长）
        }
        else
        {
            list.Add(new ConditionInstance
            {
                Condition = condition,
                SourceId = sourceId,
                RemainingRounds = durationRounds
            });
        }

        ConditionApplied?.Invoke(combatantId, condition);
    }

    /// <summary>
    /// 移除指定角色的指定条件。
    /// </summary>
    public void RemoveCondition(string combatantId, Condition condition)
    {
        if (!_conditions.TryGetValue(combatantId, out var list))
            return;

        var removed = list.RemoveAll(c => c.Condition == condition);
        if (removed > 0)
            ConditionRemoved?.Invoke(combatantId, condition);
    }

    /// <summary>
    /// 检查角色是否拥有指定条件。
    /// </summary>
    public bool HasCondition(string combatantId, Condition condition)
    {
        return _conditions.TryGetValue(combatantId, out var list)
            && list.Any(c => c.Condition == condition);
    }

    /// <summary>
    /// 获取角色的所有活跃条件。
    /// </summary>
    public IReadOnlyList<ConditionInstance> GetConditions(string combatantId)
    {
        return _conditions.TryGetValue(combatantId, out var list)
            ? list.AsReadOnly()
            : Array.Empty<ConditionInstance>();
    }

    /// <summary>
    /// 回合结束时调用，递减所有条件的持续时间，移除已到期的条件。
    /// 持续时间为 0 表示永久（不递减）。
    /// </summary>
    public void TickRound()
    {
        foreach (var (combatantId, list) in _conditions)
        {
            var expired = new List<ConditionInstance>();
            var remaining = new List<ConditionInstance>();

            foreach (var c in list)
            {
                if (c.RemainingRounds > 0 && c.RemainingRounds <= 1)
                {
                    expired.Add(c);
                }
                else if (c.RemainingRounds > 1)
                {
                    remaining.Add(c with { RemainingRounds = c.RemainingRounds - 1 });
                }
                else
                {
                    remaining.Add(c); // 持续时间为 0 = 永久
                }
            }

            list.Clear();
            list.AddRange(remaining);

            foreach (var c in expired)
                ConditionRemoved?.Invoke(combatantId, c.Condition);
        }
    }

    /// <summary>
    /// 条件施加事件。
    /// </summary>
    public event Action<string, Condition>? ConditionApplied;

    /// <summary>
    /// 条件移除事件。
    /// </summary>
    public event Action<string, Condition>? ConditionRemoved;
}
