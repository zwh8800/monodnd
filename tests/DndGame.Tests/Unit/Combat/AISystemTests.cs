using Xunit;
using FluentAssertions;
using DndGame.Systems.Combat;
using DndGame.Systems.Character;

namespace DndGame.Tests.Unit.Combat;

public class AISystemTests
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
        public int GetAbilityModifier(Ability ability) => 0;
        public ResistanceType GetResistance(DamageType type) => ResistanceType.Normal;
    }

    [Fact]
    public void SelectTarget_Nearest_SelectsOneEnemy()
    {
        var ai = new AISystem();
        var self = new TestCombatant { CombatantId = "ai" };
        var enemies = new List<ICombatant>
        {
            new TestCombatant { CombatantId = "enemy_a" },
            new TestCombatant { CombatantId = "enemy_b" },
            new TestCombatant { CombatantId = "enemy_c" }
        };

        var target = ai.SelectTarget(self, enemies, TargetStrategy.Nearest);

        target.Should().NotBeNull();
        enemies.Should().Contain(target!);
    }

    [Fact]
    public void SelectTarget_LowestHP_SelectsWeakest()
    {
        var ai = new AISystem();
        var self = new TestCombatant { CombatantId = "ai" };
        var enemies = new List<ICombatant>
        {
            new TestCombatant { CombatantId = "tank", CurrentHp = 50 },
            new TestCombatant { CombatantId = "weak", CurrentHp = 5 },
            new TestCombatant { CombatantId = "mid", CurrentHp = 25 }
        };

        var target = ai.SelectTarget(self, enemies, TargetStrategy.LowestHP);

        target.Should().NotBeNull();
        target!.CombatantId.Should().Be("weak");
    }

    [Fact]
    public void DecideAction_LowHP_ProbabilityRetreat()
    {
        var ai = new AISystem();
        var self = new TestCombatant { CombatantId = "ai", CurrentHp = 3, MaxHp = 20 }; // 15% HP
        var enemies = new List<ICombatant> { new TestCombatant { CombatantId = "enemy" } };

        // 运行多次，至少应该有一次撤退
        var actions = Enumerable.Range(0, 100)
            .Select(_ => ai.DecideAction(self, enemies, EnemyType.Melee))
            .ToList();

        actions.Should().Contain("retreat");
    }

    [Fact]
    public void DecideAction_Ranged_PrioritizesAttack()
    {
        var ai = new AISystem();
        var self = new TestCombatant { CombatantId = "archer", CurrentHp = 20, MaxHp = 20 };
        var enemies = new List<ICombatant> { new TestCombatant { CombatantId = "enemy" } };

        var action = ai.DecideAction(self, enemies, EnemyType.Ranged);

        action.Should().Be("attack");
    }

    [Fact]
    public void DecideAction_Caster_SometimesCasts()
    {
        var ai = new AISystem();
        var self = new TestCombatant { CombatantId = "mage", CurrentHp = 20, MaxHp = 20 };
        var enemies = new List<ICombatant> { new TestCombatant { CombatantId = "enemy" } };

        var actions = Enumerable.Range(0, 100)
            .Select(_ => ai.DecideAction(self, enemies, EnemyType.Caster))
            .ToList();

        actions.Should().Contain("cast");
    }

    [Fact]
    public void DecideAction_NoEnemies_ReturnsWait()
    {
        var ai = new AISystem();
        var self = new TestCombatant { CombatantId = "ai", CurrentHp = 20, MaxHp = 20 };
        var enemies = new List<ICombatant>();

        var action = ai.DecideAction(self, enemies, EnemyType.Melee);

        action.Should().Be("wait");
    }
}
