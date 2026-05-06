using DndGame.Systems.Character;

namespace DndGame.Systems.Character;

/// <summary>
/// 角色数值生成器，使用 Standard Array + 种族加值 + 职业进阶表生成角色数值。
/// </summary>
public static class NumericalGenerator
{
    /// <summary>
    /// DND 5e Standard Array。
    /// </summary>
    public static readonly int[] StandardArray = [15, 14, 13, 12, 10, 8];

    /// <summary>
    /// 计算属性调整值 = floor((score - 10) / 2.0)。
    /// </summary>
    public static int GetModifier(int score) => (int)Math.Floor((score - 10) / 2.0);

    /// <summary>
    /// 计算熟练加值 = floor((level - 1) / 4) + 2。
    /// </summary>
    public static int GetProficiencyBonus(int level) => (level - 1) / 4 + 2;

    /// <summary>
    /// 计算生命值。Lv1 = 生命骰最大值 + CON调整值；Lv2+ = 期望值 + CON调整值。
    /// </summary>
    public static int CalculateHP(int hitDieMax, int conModifier, int level)
    {
        if (level < 1) throw new ArgumentOutOfRangeException(nameof(level));

        // Lv1: 生命骰最大值
        int hp = hitDieMax + conModifier;

        // Lv2+: 每级增加期望值（向上取整）
        int expectedGain = (hitDieMax / 2) + 1;
        hp += (expectedGain + conModifier) * (level - 1);

        return Math.Max(1, hp); // 最少 1 HP
    }

    /// <summary>
    /// 计算 AC（无甲）。
    /// </summary>
    public static int CalculateAC(int dexModifier) => 10 + dexModifier;
}
