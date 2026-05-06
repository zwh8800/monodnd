namespace DndGame.Systems.Combat;

/// <summary>
/// 纯函数骰子系统，支持 d20 检定（优势/劣势）、伤害骰表达式解析、暴击/失误判定。
/// 所有方法为静态纯函数，无外部状态依赖。
/// </summary>
public static class DiceRoller
{
    private static readonly Random _shared = new();

    /// <summary>
    /// 掷 N 面骰子，返回 [1, sides] 范围内的随机值。
    /// </summary>
    public static int Roll(int sides) => _shared.Next(1, sides + 1);

    /// <summary>
    /// 掷 D20 攻击检定，支持优势/劣势机制。
    /// 优势：掷两次取高值；劣势：掷两次取低值；同时有优势和劣势则退化为单骰。
    /// </summary>
    public static AttackRollResult RollAttack(int bonus, bool hasAdvantage, bool hasDisadvantage)
    {
        var hasBoth = hasAdvantage && hasDisadvantage;
        int[] rolls;

        if (hasBoth)
        {
            rolls = new[] { Roll(20) };
        }
        else if (hasAdvantage)
        {
            rolls = new[] { Roll(20), Roll(20) };
        }
        else if (hasDisadvantage)
        {
            rolls = new[] { Roll(20), Roll(20) };
        }
        else
        {
            rolls = new[] { Roll(20) };
        }

        var rawValue = hasAdvantage && !hasBoth ? Math.Max(rolls[0], rolls[1]) :
                       hasDisadvantage && !hasBoth ? Math.Min(rolls[0], rolls[1]) :
                       rolls[0];

        return new AttackRollResult
        {
            RawValue = rawValue,
            Total = rawValue + bonus,
            IsCritical = Array.Exists(rolls, r => r == 20),
            IsCriticalMiss = Array.Exists(rolls, r => r == 1),
            Rolls = rolls,
            Bonus = bonus,
            Advantage = hasAdvantage,
            Disadvantage = hasDisadvantage
        };
    }

    /// <summary>
    /// 掷伤害骰，解析 "2d6+3" 格式的骰子表达式。
    /// </summary>
    public static DamageRollResult RollDamage(string diceExpression, int bonus)
    {
        var (count, sides, extraBonus) = ParseDiceExpression(diceExpression);
        var rolls = new int[count];
        for (int i = 0; i < count; i++)
            rolls[i] = Roll(sides);

        var totalBonus = bonus + extraBonus;
        return new DamageRollResult
        {
            Rolls = rolls,
            Bonus = totalBonus,
            Total = rolls.Sum() + totalBonus
        };
    }

    /// <summary>
    /// 解析骰子表达式，如 "2d6+3" → (Count=2, Sides=6, Bonus=3)。
    /// </summary>
    public static (int Count, int Sides, int Bonus) ParseDiceExpression(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            throw new ArgumentException("骰子表达式不能为空。", nameof(expression));

        var expr = expression.Trim().ToLowerInvariant();
        var dIndex = expr.IndexOf('d');
        if (dIndex < 0)
            throw new FormatException($"无效的骰子表达式格式：'{expression}'。期望格式如 '2d6+3'。");

        var countStr = expr[..dIndex];
        if (!int.TryParse(countStr, out var count) || count <= 0)
            throw new FormatException($"骰子数量无效：'{countStr}'。");

        var afterD = expr[(dIndex + 1)..];
        var bonus = 0;
        var sidesStr = afterD;

        var plusIndex = afterD.IndexOf('+');
        var minusIndex = afterD.IndexOf('-');

        if (plusIndex >= 0)
        {
            sidesStr = afterD[..plusIndex];
            if (int.TryParse(afterD[(plusIndex + 1)..], out var b))
                bonus = b;
        }
        else if (minusIndex >= 0)
        {
            sidesStr = afterD[..minusIndex];
            if (int.TryParse(afterD[(minusIndex + 1)..], out var b))
                bonus = -b;
        }

        if (!int.TryParse(sidesStr, out var sides) || sides <= 0)
            throw new FormatException($"骰子面数无效：'{sidesStr}'。");

        return (count, sides, bonus);
    }
}

/// <summary>
/// 攻击检定结果，包含原始值、最终值、暴击/失误标记、所有骰子结果。
/// </summary>
public record AttackRollResult
{
    public int RawValue { get; init; }
    public int Total { get; init; }
    public bool IsCritical { get; init; }
    public bool IsCriticalMiss { get; init; }
    public int[] Rolls { get; init; } = Array.Empty<int>();
    public int Bonus { get; init; }
    public bool Advantage { get; init; }
    public bool Disadvantage { get; init; }
}

/// <summary>
/// 伤害骰结果，包含所有骰子结果、加值、总伤害。
/// </summary>
public record DamageRollResult
{
    public int[] Rolls { get; init; } = Array.Empty<int>();
    public int Bonus { get; init; }
    public int Total { get; init; }
}
