using DndGame.Systems.Character;

namespace DndGame.Systems.Adventure;

/// <summary>
/// 冒险结算结果。
/// </summary>
public record SettlementResult
{
    public int XPGained { get; init; }
    public int GoldGained { get; init; }
    public List<string> LootItems { get; init; } = new();
    public bool IsSuccess { get; init; }
}

/// <summary>
/// 结算系统，处理冒险完成后的 XP、战利品和金币分配。
/// </summary>
public class SettlementSystem
{
    /// <summary>
    /// 计算胜利结算。
    /// </summary>
    public SettlementResult CalculateVictory(int enemyCR, int partySize, int goldReward)
    {
        var xp = CalculateXP(enemyCR);
        var xpPerMember = partySize > 0 ? xp / partySize : xp;

        return new SettlementResult
        {
            XPGained = xpPerMember,
            GoldGained = goldReward,
            IsSuccess = true
        };
    }

    /// <summary>
    /// 计算失败结算（损失 30% 金币）。
    /// </summary>
    public SettlementResult CalculateDefeat(int currentGold)
    {
        var goldLoss = (int)(currentGold * 0.3);

        return new SettlementResult
        {
            XPGained = 0,
            GoldGained = -goldLoss,
            IsSuccess = false
        };
    }

    /// <summary>
    /// 计算 XP 奖励（基于 CR）。
    /// </summary>
    private static int CalculateXP(int cr) => cr switch
    {
        0 => 10,
        1 => 200,
        2 => 450,
        3 => 700,
        4 => 1100,
        5 => 1800,
        _ => cr * 400
    };
}
