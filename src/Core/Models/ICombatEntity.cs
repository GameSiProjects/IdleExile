namespace IdleExile.Core.Models;

using IdleExile.Core.Simulation;

/// <summary>
/// Common contract for all combatants (Player character, Monsters, Minions, Bosses) in Idle Exile.
/// Unifies offensive and defensive capabilities so combat interactions are symmetrical and PoE-accurate.
/// </summary>
public interface ICombatEntity
{
    /// <summary>Unique entity ID for targeting and event correlation.</summary>
    long Id { get; }

    /// <summary>Display name of the entity.</summary>
    string Name { get; }

    /// <summary>2D coordinate (X, Y) within the arena.</summary>
    Vector2D Position { get; set; }

    /// <summary>Effective collision/hitbox radius in pixels.</summary>
    float Radius { get; }

    /// <summary>True while CurrentLife > 0.</summary>
    bool IsAlive { get; }

    /// <summary>Current life pool.</summary>
    float CurrentLife { get; }

    /// <summary>Maximum life pool.</summary>
    float MaxLife { get; }

    /// <summary>Current energy shield pool.</summary>
    float CurrentEnergyShield { get; }

    /// <summary>Maximum energy shield pool.</summary>
    float MaxEnergyShield { get; }

    /// <summary>Snapshot of defensive stats (Armour, Evasion, Block, Suppression, Resistances).</summary>
    DefensiveStats Defenses { get; }

    /// <summary>Active status ailments tracker (Ignite, Chill, Freeze, Shock, Bleed, Poison).</summary>
    AilmentTracker Ailments { get; }

    /// <summary>
    /// Evaluates whether an incoming attack lands on this entity using deterministic Evasion Entropy.
    /// </summary>
    bool DoesAttackHit(float evasionChance);

    /// <summary>
    /// Applies unmitigated or mitigated damage directly to the entity's health/resource pools.
    /// Returns the actual amount of damage subtracted.
    /// </summary>
    float ApplyDamage(float amount, bool isChaos, bool chaosDoesNotBypassEs = false, float mindOverMatterPercent = 0f);
}
