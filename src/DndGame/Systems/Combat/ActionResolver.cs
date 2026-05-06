using DndGame.Systems.Character;

namespace DndGame.Systems.Combat;

/// <summary>
/// 抗性类型：普通、抗性（半伤）、免疫（零伤）、易伤（双倍）。
/// </summary>
public enum ResistanceType { Normal, Resistant, Immune, Vulnerable }

/// <summary>
/// 战斗参与者接口，定义 ActionResolver 所需的最小契约。
/// </summary>
public interface ICombatant
{
    string CombatantId { get; }
    int ArmorClass { get; }
    int MaxHp { get; }
    int CurrentHp { get; set; }
    int TempHp { get; set; }
    int ProficiencyBonus { get; }
    bool IsConcentrating { get; set; }
    int GetAbilityModifier(Ability ability);
    ResistanceType GetResistance(DamageType type);
}

/// <summary>
/// 武器数据，定义攻击的基础伤害骰和伤害类型。
/// </summary>
public record WeaponData
{
    public string Name { get; init; } = "";
    public string DamageDice { get; init; } = "1d6";
    public DamageType DamageType { get; init; } = DamageType.Slashing;
    public Ability ScalingAbility { get; init; } = Ability.Str;
    public int MagicalBonus { get; init; }
}

/// <summary>
/// 攻击结算结果。
/// </summary>
public record AttackResolution
{
    public bool IsHit { get; init; }
    public bool IsCritical { get; init; }
    public bool IsCriticalMiss { get; init; }
    public int AttackRoll { get; init; }
    public int DamageDealt { get; init; }
    public DamageType DamageType { get; init; }
    public bool ConcentrationBroken { get; init; }
    public List<string> LogEntries { get; init; } = new();
}

/// <summary>
/// 攻击结算器，实现 DND 5e 攻击检定 10 步管线。
/// 步骤：声明 → 检定 → 命中 → 优势/劣势 → 暴击 → 伤害 → 类型 → 专注 → 条件 → 日志。
/// </summary>
public class ActionResolver
{
    /// <summary>
    /// 结算一次攻击，返回完整的结果链。
    /// </summary>
    public AttackResolution ResolveAttack(ICombatant attacker, ICombatant target, WeaponData weapon)
    {
        var log = new List<string>();

        // 第 1 步：动作声明
        log.Add($"{attacker.CombatantId} 使用 {weapon.Name} 攻击 {target.CombatantId}");

        // 第 2 步：攻击检定
        var attackRoll = DiceRoller.RollAttack(
            attacker.GetAbilityModifier(weapon.ScalingAbility) + attacker.ProficiencyBonus + weapon.MagicalBonus,
            hasAdvantage: false,
            hasDisadvantage: false);

        log.Add($"攻击检定: d20={attackRoll.RawValue} + {attackRoll.Bonus} = {attackRoll.Total}");

        // 第 3 步：命中判定
        bool isHit;
        if (attackRoll.IsCriticalMiss)
        {
            isHit = false;
            log.Add("自然 1！自动未命中");
        }
        else if (attackRoll.IsCritical)
        {
            isHit = true;
            log.Add("自然 20！自动命中 + 暴击");
        }
        else
        {
            isHit = attackRoll.Total >= target.ArmorClass;
            if (!isHit)
                log.Add($"未命中（{attackRoll.Total} < AC {target.ArmorClass}）");
        }

        if (!isHit)
        {
            return new AttackResolution
            {
                IsHit = false,
                IsCritical = attackRoll.IsCritical,
                IsCriticalMiss = attackRoll.IsCriticalMiss,
                AttackRoll = attackRoll.Total,
                DamageDealt = 0,
                DamageType = weapon.DamageType,
                LogEntries = log
            };
        }

        // 第 5 步：暴击判定 — 伤害骰取最大值
        int damage;
        if (attackRoll.IsCritical)
        {
            damage = MaximizeDice(weapon.DamageDice) + attacker.GetAbilityModifier(weapon.ScalingAbility) + weapon.MagicalBonus;
            log.Add($"暴击！伤害骰取最大值: {damage}");
        }
        else
        {
            // 第 6 步：伤害计算
            var damageRoll = DiceRoller.RollDamage(weapon.DamageDice, attacker.GetAbilityModifier(weapon.ScalingAbility) + weapon.MagicalBonus);
            damage = damageRoll.Total;
            log.Add($"伤害: {string.Join("+", damageRoll.Rolls)} + {damageRoll.Bonus} = {damage}");
        }

        // 第 7 步：伤害类型 — 抗性/免疫/易伤
        var resistance = target.GetResistance(weapon.DamageType);
        switch (resistance)
        {
            case ResistanceType.Resistant:
                damage = damage / 2;
                log.Add($"抗性！伤害减半 → {damage}");
                break;
            case ResistanceType.Immune:
                damage = 0;
                log.Add("免疫！伤害降为 0");
                break;
            case ResistanceType.Vulnerable:
                damage = damage * 2;
                log.Add($"易伤！伤害翻倍 → {damage}");
                break;
        }

        // 应用伤害
        var oldHp = target.CurrentHp;
        if (target.TempHp > 0)
        {
            var absorbed = Math.Min(target.TempHp, damage);
            target.TempHp -= absorbed;
            damage -= absorbed;
        }
        target.CurrentHp = Math.Max(0, target.CurrentHp - damage);
        log.Add($"{target.CombatantId} HP: {oldHp} → {target.CurrentHp}");

        // 第 8 步：专注检定
        bool concentrationBroken = false;
        if (target.IsConcentrating)
        {
            var dc = Math.Max(10, (oldHp - target.CurrentHp) / 2);
            var conSave = DiceRoller.Roll(20) + target.GetAbilityModifier(Ability.Con);
            concentrationBroken = conSave < dc;
            if (concentrationBroken)
            {
                target.IsConcentrating = false;
                log.Add($"专注检定失败（{conSave} < DC {dc}）！专注中断");
            }
            else
            {
                log.Add($"专注检定成功（{conSave} >= DC {dc}）");
            }
        }

        return new AttackResolution
        {
            IsHit = true,
            IsCritical = attackRoll.IsCritical,
            IsCriticalMiss = false,
            AttackRoll = attackRoll.Total,
            DamageDealt = oldHp - target.CurrentHp,
            DamageType = weapon.DamageType,
            ConcentrationBroken = concentrationBroken,
            LogEntries = log
        };
    }

    /// <summary>
    /// 暴击时伤害骰取最大值。解析 "2d6" → 12。
    /// </summary>
    private static int MaximizeDice(string diceExpression)
    {
        var (count, sides, bonus) = DiceRoller.ParseDiceExpression(diceExpression);
        return count * sides + bonus;
    }
}
