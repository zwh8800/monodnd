namespace DndGame.Systems.Character;

/// <summary>
/// DND 5e 六项基本属性。
/// </summary>
public enum Ability { Str, Dex, Con, Int, Wis, Cha }

/// <summary>
/// DND 5e 18 项技能。
/// </summary>
public enum Skill
{
    Acrobatics, AnimalHandling, Arcana, Athletics,
    Deception, History, Insight, Intimidation,
    Investigation, Medicine, Nature, Perception,
    Performance, Persuasion, Religion, SleightOfHand,
    Stealth, Survival
}

/// <summary>
/// 角色生存状态。
/// </summary>
public enum CharacterStatus { Alive, Dead, Retired }

/// <summary>
/// DND 5e 13 种伤害类型。
/// </summary>
public enum DamageType
{
    Bludgeoning, Slashing, Piercing,
    Acid, Cold, Fire, Force, Lightning, Necrotic,
    Poison, Psychic, Radiant, Thunder
}
