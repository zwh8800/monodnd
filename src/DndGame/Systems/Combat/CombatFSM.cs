namespace DndGame.Systems.Combat;

/// <summary>
/// 战斗有限状态机，管理 16 个战斗状态之间的转换。
/// 每个转换可注册守卫条件（predicate），只有守卫返回 true 时才允许转换。
/// </summary>
public class CombatFSM
{
    /// <summary>
    /// 当前战斗状态。
    /// </summary>
    public CombatState CurrentState { get; private set; } = CombatState.Initialization;

    /// <summary>
    /// 守卫条件字典：键为 (源状态, 目标状态) 元组，值为守卫谓词。
    /// </summary>
    private readonly Dictionary<(CombatState, CombatState), Func<bool>> _guards = new();

    /// <summary>
    /// 注册状态转换的守卫条件。只有当 predicate 返回 true 时，转换才被允许。
    /// </summary>
    public void RegisterGuard(CombatState from, CombatState to, Func<bool> predicate)
    {
        _guards[(from, to)] = predicate;
    }

    /// <summary>
    /// 检查从当前状态到目标状态的转换是否可行（守卫存在且返回 true）。
    /// </summary>
    public bool CanTransition(CombatState to)
    {
        var key = (CurrentState, to);
        return _guards.TryGetValue(key, out var guard) && guard();
    }

    /// <summary>
    /// 执行状态转换。如果转换无效（无守卫或守卫返回 false），抛出异常。
    /// </summary>
    public void Transition(CombatState to)
    {
        if (!CanTransition(to))
            throw new InvalidOperationException(
                $"无法从 {CurrentState} 转换到 {to}：守卫条件不满足或未注册。");

        CurrentState = to;
        OnStateEntered?.Invoke(to);
    }

    /// <summary>
    /// 状态进入事件，每次成功转换后触发，参数为新状态。
    /// </summary>
    public event Action<CombatState>? OnStateEntered;
}
