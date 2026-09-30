namespace IdleExile.Core.Models;

using IdleExile.Core.Services;

/// <summary>
/// Encapsulates the offensive payload of an executed attack or spell action.
/// Shared by both Player and Monsters when executing attacks.
/// </summary>
public sealed class AttackPayload
{
    /// <summary>The entity executing this attack.</summary>
    public ICombatEntity Attacker { get; }

    /// <summary>Base damage ranges across the 5 PoE damage types before modifiers.</summary>
    public DamagePool BaseDamage { get; }

    /// <summary>Aggregated modifier context (Increased, More, Crit Chance, Crit Multiplier, Conversions, Gain As Extra).</summary>
    public DamageModifierContext Context { get; }

    /// <summary>True if this skill is an attack (checked by Evasion and Attack Block).</summary>
    public bool IsAttack { get; init; } = true;

    /// <summary>True if this skill is a spell (checked by Spell Block and Spell Suppression).</summary>
    public bool IsSpell { get; init; } = false;

    /// <summary>True if this attack is melee range.</summary>
    public bool IsMelee { get; init; } = true;

    /// <summary>Attacker's accuracy rating for evasion checks.</summary>
    public float Accuracy { get; init; } = 1000f;

    /// <summary>Primary damage type associated with this attack.</summary>
    public DamageType PrimaryDamageType { get; init; } = DamageType.Physical;

    /// <summary>Spatial geometry representation of the skill.</summary>
    public SkillGeometryType Geometry { get; init; } = SkillGeometryType.SingleTarget;

    /// <summary>Effective maximum range or radius of the attack.</summary>
    public float EffectiveRange { get; init; } = 35f;

    public AttackPayload(
        ICombatEntity attacker,
        DamagePool baseDamage,
        DamageModifierContext context)
    {
        Attacker = attacker;
        BaseDamage = baseDamage;
        Context = context;
    }
}
