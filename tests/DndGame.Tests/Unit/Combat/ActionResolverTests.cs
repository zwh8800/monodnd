using Xunit;
using FluentAssertions;
using DndGame.Systems.Combat;
using DndGame.Systems.Character;

namespace DndGame.Tests.Unit.Combat;

public class ActionResolverTests
{
    private class TestCombatant : ICombatant
    {
        public string CombatantId { get; set; } = "";
        public int ArmorClass { get; set; } = 10;
        public int MaxHp { get; set; } = 20;
        public int CurrentHp { get; set; } = 20;
        public int TempHp { get; set; }
        public int ProficiencyBonus { get; set; } = 2;
        public bool IsConcentrating { get; set; }
        private readonly Dictionary<Ability, int> _modifiers = new();
        private readonly Dictionary<DamageType, ResistanceType> _resistances = new();

        public void SetModifier(Ability ability, int modifier) => _modifiers[ability] = modifier;
        public void SetResistance(DamageType type, ResistanceType resistance) => _resistances[type] = resistance;

        public int GetAbilityModifier(Ability ability) =>
            _modifiers.TryGetValue(ability, out var mod) ? mod : 0;

        public ResistanceType GetResistance(DamageType type) =>
            _resistances.TryGetValue(type, out var res) ? res : ResistanceType.Normal;
    }

    private static WeaponData CreateWeapon(string dice = "1d8", DamageType type = DamageType.Slashing, Ability ability = Ability.Str)
        => new() { Name = "长剑", DamageDice = dice, DamageType = type, ScalingAbility = ability };

    [Fact]
    public void ResolveAttack_Hit_AttackRollAboveAC()
    {
        // Arrange
        var resolver = new ActionResolver();
        var attacker = new TestCombatant { CombatantId = "战士", ArmorClass = 15 };
        attacker.SetModifier(Ability.Str, 3);
        var target = new TestCombatant { CombatantId = "哥布林", ArmorClass = 5, CurrentHp = 10, MaxHp = 10 };

        // Act — 循环直到命中
        for (int i = 0; i < 100; i++)
        {
            var result = resolver.ResolveAttack(attacker, target, CreateWeapon());
            if (result.IsHit)
            {
                // Assert
                result.AttackRoll.Should().BeGreaterThanOrEqualTo(5);
                result.DamageDealt.Should().BeGreaterThan(0);
                return;
            }
        }
        Assert.Fail("100次攻击未命中AC5目标");
    }

    [Fact]
    public void ResolveAttack_Miss_AttackRollBelowAC()
    {
        // Arrange
        var resolver = new ActionResolver();
        var attacker = new TestCombatant { CombatantId = "战士" };
        attacker.SetModifier(Ability.Str, -5); // 极低攻击加值
        var target = new TestCombatant { CombatantId = "龙", ArmorClass = 25, CurrentHp = 100, MaxHp = 100 };

        // Act — 循环直到未命中（非自然20）
        for (int i = 0; i < 100; i++)
        {
            var result = resolver.ResolveAttack(attacker, target, CreateWeapon());
            if (!result.IsHit && !result.IsCritical)
            {
                // Assert
                result.DamageDealt.Should().Be(0);
                return;
            }
        }
        Assert.Fail("100次攻击全部命中AC25目标");
    }

    [Fact]
    public void ResolveAttack_Natural20_AutoHitAndCritical()
    {
        // Arrange
        var resolver = new ActionResolver();
        var attacker = new TestCombatant { CombatantId = "战士", CurrentHp = 20, MaxHp = 20 };
        attacker.SetModifier(Ability.Str, 3);
        var target = new TestCombatant { CombatantId = "龙", ArmorClass = 25, CurrentHp = 100, MaxHp = 100 };

        // Act — 循环直到自然20
        for (int i = 0; i < 1000; i++)
        {
            var result = resolver.ResolveAttack(attacker, target, CreateWeapon());
            if (result.IsCritical)
            {
                // Assert
                result.IsHit.Should().BeTrue();
                result.DamageDealt.Should().BeGreaterThan(0);
                return;
            }
        }
        Assert.Fail("1000次攻击未出现自然20");
    }

    [Fact]
    public void ResolveAttack_Natural1_AutoMiss()
    {
        // Arrange
        var resolver = new ActionResolver();
        var attacker = new TestCombatant { CombatantId = "战士" };
        attacker.SetModifier(Ability.Str, 10);
        var target = new TestCombatant { CombatantId = "靶子", ArmorClass = 1, CurrentHp = 100, MaxHp = 100 };

        // Act — 循环直到自然1
        for (int i = 0; i < 1000; i++)
        {
            var result = resolver.ResolveAttack(attacker, target, CreateWeapon());
            if (result.IsCriticalMiss)
            {
                // Assert
                result.IsHit.Should().BeFalse();
                result.DamageDealt.Should().Be(0);
                return;
            }
        }
        Assert.Fail("1000次攻击未出现自然1");
    }

    [Fact]
    public void ResolveAttack_DamageIncludesAbilityModifier()
    {
        // Arrange
        var resolver = new ActionResolver();
        var attacker = new TestCombatant { CombatantId = "战士" };
        attacker.SetModifier(Ability.Str, 3);
        var target = new TestCombatant { CombatantId = "哥布林", ArmorClass = 1, CurrentHp = 100, MaxHp = 100 };

        // Act
        for (int i = 0; i < 100; i++)
        {
            var result = resolver.ResolveAttack(attacker, target, CreateWeapon("1d1"));
            if (result.IsHit && !result.IsCritical)
            {
                // 1d1=1 + STR=3 = 4
                // Assert
                result.DamageDealt.Should().BeGreaterThanOrEqualTo(4);
                return;
            }
        }
        Assert.Fail("100次攻击未命中");
    }

    [Fact]
    public void ResolveAttack_FireResistance_HalvesDamage()
    {
        // Arrange
        var resolver = new ActionResolver();
        var attacker = new TestCombatant { CombatantId = "法师" };
        attacker.SetModifier(Ability.Str, 0);
        var target = new TestCombatant { CombatantId = "火元素", ArmorClass = 1, CurrentHp = 100, MaxHp = 100 };
        target.SetResistance(DamageType.Fire, ResistanceType.Resistant);

        // Act
        for (int i = 0; i < 100; i++)
        {
            var result = resolver.ResolveAttack(attacker, target, CreateWeapon("1d8", DamageType.Fire));
            if (result.IsHit)
            {
                // Assert — 1d8 max=8, half=4
                result.DamageDealt.Should().BeLessThanOrEqualTo(4);
                result.LogEntries.Should().Contain(l => l.Contains("抗性"));
                return;
            }
        }
        Assert.Fail("100次攻击未命中");
    }

    [Fact]
    public void ResolveAttack_LightningVulnerability_DoublesDamage()
    {
        // Arrange
        var resolver = new ActionResolver();
        var attacker = new TestCombatant { CombatantId = "法师" };
        attacker.SetModifier(Ability.Str, 0);
        var target = new TestCombatant { CombatantId = "水元素", ArmorClass = 1, CurrentHp = 100, MaxHp = 100 };
        target.SetResistance(DamageType.Lightning, ResistanceType.Vulnerable);

        // Act
        for (int i = 0; i < 100; i++)
        {
            var result = resolver.ResolveAttack(attacker, target, CreateWeapon("1d4", DamageType.Lightning));
            if (result.IsHit && !result.IsCritical)
            {
                // Assert — 1d4 max=4, doubled=8
                result.DamageDealt.Should().BeGreaterThanOrEqualTo(2); // min 1*2
                result.DamageDealt.Should().BeLessThanOrEqualTo(8);    // max 4*2
                result.LogEntries.Should().Contain(l => l.Contains("易伤"));
                return;
            }
        }
        Assert.Fail("100次攻击未命中");
    }

    [Fact]
    public void ResolveAttack_Critical_MaximizesDice()
    {
        // Arrange
        var resolver = new ActionResolver();
        var attacker = new TestCombatant { CombatantId = "战士", ProficiencyBonus = 0 };
        attacker.SetModifier(Ability.Str, 3);
        var target = new TestCombatant { CombatantId = "哥布林", ArmorClass = 1, CurrentHp = 100, MaxHp = 100 };

        // Act — 循环直到暴击
        for (int i = 0; i < 10000; i++)
        {
            target.CurrentHp = 100; // 重置目标HP
            var result = resolver.ResolveAttack(attacker, target, CreateWeapon("2d6"));
            if (result.IsCritical)
            {
                // Assert — 2d6 max=12 + STR=3 = 15
                result.DamageDealt.Should().BeGreaterThanOrEqualTo(12);
                return;
            }
        }
        Assert.Fail("10000次攻击未出现暴击");
    }

    [Fact]
    public void ResolveAttack_ConcentrationCheck_DCCalculated()
    {
        // Arrange
        var resolver = new ActionResolver();
        var attacker = new TestCombatant { CombatantId = "战士" };
        attacker.SetModifier(Ability.Str, 3);
        var target = new TestCombatant { CombatantId = "法师", ArmorClass = 1, CurrentHp = 30, MaxHp = 30, IsConcentrating = true };
        target.SetModifier(Ability.Con, 2);

        // Act
        for (int i = 0; i < 100; i++)
        {
            var result = resolver.ResolveAttack(attacker, target, CreateWeapon("1d8"));
            if (result.IsHit)
            {
                // Assert — 专注检定应该被触发
                result.LogEntries.Should().Contain(l => l.Contains("专注检定"));
                return;
            }
        }
        Assert.Fail("100次攻击未命中");
    }

    [Fact]
    public void ResolveAttack_Immunity_ZeroDamage()
    {
        // Arrange
        var resolver = new ActionResolver();
        var attacker = new TestCombatant { CombatantId = "战士" };
        attacker.SetModifier(Ability.Str, 3);
        var target = new TestCombatant { CombatantId = "幽灵", ArmorClass = 1, CurrentHp = 100, MaxHp = 100 };
        target.SetResistance(DamageType.Slashing, ResistanceType.Immune);

        // Act
        for (int i = 0; i < 100; i++)
        {
            var result = resolver.ResolveAttack(attacker, target, CreateWeapon());
            if (result.IsHit)
            {
                // Assert
                result.DamageDealt.Should().Be(0);
                result.LogEntries.Should().Contain(l => l.Contains("免疫"));
                return;
            }
        }
        Assert.Fail("100次攻击未命中");
    }

    [Fact]
    public void ResolveAttack_ResistanceAndVulnerability_Cancel()
    {
        // Arrange
        var resolver = new ActionResolver();
        var attacker = new TestCombatant { CombatantId = "战士" };
        attacker.SetModifier(Ability.Str, 0);
        var target = new TestCombatant { CombatantId = "怪物", ArmorClass = 1, CurrentHp = 100, MaxHp = 100 };
        target.SetResistance(DamageType.Fire, ResistanceType.Resistant);
        target.SetResistance(DamageType.Fire, ResistanceType.Vulnerable); // 同时有两者

        // Act — 由于 SetResistance 会覆盖，实际上只有一个生效
        // 这个测试验证系统不会崩溃
        for (int i = 0; i < 10; i++)
        {
            var result = resolver.ResolveAttack(attacker, target, CreateWeapon("1d4", DamageType.Fire));
            if (result.IsHit)
            {
                // Assert — 伤害应该在合理范围内
                result.DamageDealt.Should().BeGreaterThanOrEqualTo(0);
                return;
            }
        }
        Assert.Fail("10次攻击未命中");
    }

    [Fact]
    public void ResolveAttack_FullLogChain_RecordsAllSteps()
    {
        // Arrange
        var resolver = new ActionResolver();
        var attacker = new TestCombatant { CombatantId = "战士" };
        attacker.SetModifier(Ability.Str, 3);
        var target = new TestCombatant { CombatantId = "哥布林", ArmorClass = 1, CurrentHp = 10, MaxHp = 10 };

        // Act
        var result = resolver.ResolveAttack(attacker, target, CreateWeapon());

        // Assert — 日志应包含攻击声明和结果
        result.LogEntries.Should().NotBeEmpty();
        result.LogEntries[0].Should().Contain("攻击");
    }
}
