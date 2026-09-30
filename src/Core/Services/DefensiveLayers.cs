using System;
using IdleExile.Core.Models;
using IdleExile.Core.State;

namespace IdleExile.Core.Services;

/// <summary>
/// Processes incoming damage through the full Path of Exile defensive layer pipeline.
///
/// Layer order (matching GDD 03_combat_and_stats.md § 2):
///   1. Evasion (entropy-based, attacks only)
///   2. Block / Spell Block (attacks / spells respectively)
///   3. Spell Suppression (spells only, halves damage)
///   4. Armour (physical damage) & Resistances (elemental/chaos)
///   5. EHP absorption: Energy Shield → Mind over Matter (Mana) → Life
///
/// This class is a pure C# static service with no Godot dependency.
/// </summary>
public static class DefensiveLayers
{
    // ─────────────────────────────────────────────
    //  Full Pipeline Entry Point
    // ─────────────────────────────────────────────

    /// <summary>
    /// Processes a single incoming hit through all defensive layers and applies
    /// final mitigated damage to the target entity's resource pools.
    ///
    /// Symmetrically evaluates Player and Monster defenses.
    /// Returns a <see cref="DefenseResult"/> containing the outcome of each layer.
    /// </summary>
    /// <param name="incomingDamage">Per-type damage from the hit (post-attacker modifiers, pre-mitigation).</param>
    /// <param name="isAttack">True if the damage source is an attack (enables Evasion and Attack Block).</param>
    /// <param name="isSpell">True if the damage source is a spell (enables Spell Block and Suppression).</param>
    /// <param name="defender">Mutable combat entity that receives the final damage.</param>
    /// <param name="stats">Immutable defensive configuration snapshot.</param>
    /// <param name="random">RNG source for Block and Suppression rolls.</param>
    public static DefenseResult ProcessHit(
        HitDamageBreakdown incomingDamage,
        bool isAttack,
        bool isSpell,
        ICombatEntity defender,
        DefensiveStats stats,
        Random? random = null)
    {
        var rng = random ?? Random.Shared;
        float totalIncoming = incomingDamage.Total;

        // ── Layer 1: Evasion (attacks only, entropy-based) ──
        bool evaded = false;
        if (isAttack)
        {
            bool attackHit = defender.DoesAttackHit(stats.EvasionChance);
            evaded = !attackHit;
            if (evaded)
            {
                return new DefenseResult
                {
                    Evaded = true,
                    TotalIncomingDamage = totalIncoming,
                    TotalMitigatedDamage = totalIncoming,
                    TotalDamageApplied = 0f
                };
            }
        }

        // ── Layer 2: Block (attacks → AttackBlock, spells → SpellBlock) ──
        bool blocked = false;
        if (isAttack && stats.AttackBlockChance > 0f)
        {
            blocked = rng.NextDouble() * 100.0 < stats.AttackBlockChance;
        }
        else if (isSpell && stats.SpellBlockChance > 0f)
        {
            blocked = rng.NextDouble() * 100.0 < stats.SpellBlockChance;
        }

        if (blocked)
        {
            return new DefenseResult
            {
                Blocked = true,
                TotalIncomingDamage = totalIncoming,
                TotalMitigatedDamage = totalIncoming,
                TotalDamageApplied = 0f
            };
        }

        // ── Layer 3: Spell Suppression (spells only, 50% damage reduction) ──
        bool suppressed = false;
        float suppressionMultiplier = 1f;
        if (isSpell && stats.SpellSuppressionChance > 0f)
        {
            suppressed = rng.NextDouble() * 100.0 < stats.SpellSuppressionChance;
            if (suppressed)
            {
                suppressionMultiplier = 0.5f; // 50% damage taken
            }
        }

        // ── Layer 4: Armour & Resistances (per damage type) ──
        float mitigatedPhysical = ApplyArmourMitigation(
            incomingDamage.Physical * suppressionMultiplier, stats.Armour);

        float mitigatedFire = ApplyResistanceMitigation(
            incomingDamage.Fire * suppressionMultiplier,
            stats.GetEffectiveResistance(DamageType.Fire));

        float mitigatedCold = ApplyResistanceMitigation(
            incomingDamage.Cold * suppressionMultiplier,
            stats.GetEffectiveResistance(DamageType.Cold));

        float mitigatedLightning = ApplyResistanceMitigation(
            incomingDamage.Lightning * suppressionMultiplier,
            stats.GetEffectiveResistance(DamageType.Lightning));

        float mitigatedChaos = 0f;
        if (!stats.ChaosInoculation) // CI = immune to chaos
        {
            mitigatedChaos = ApplyResistanceMitigation(
                incomingDamage.Chaos * suppressionMultiplier,
                stats.GetEffectiveResistance(DamageType.Chaos));
        }

        // ── Layer 5: Apply to EHP pools ──
        // Non-chaos damage hits ES → MoM → Life
        float nonChaosDamage = mitigatedPhysical + mitigatedFire + mitigatedCold + mitigatedLightning;
        float totalApplied = 0f;

        if (nonChaosDamage > 0f)
        {
            totalApplied += defender.ApplyDamage(
                nonChaosDamage,
                isChaos: false,
                chaosDoesNotBypassEs: false,
                mindOverMatterPercent: stats.MindOverMatterPercent);
        }

        if (mitigatedChaos > 0f)
        {
            totalApplied += defender.ApplyDamage(
                mitigatedChaos,
                isChaos: true,
                chaosDoesNotBypassEs: stats.ChaosDoesNotBypassES,
                mindOverMatterPercent: stats.MindOverMatterPercent);
        }

        float totalAfterMitigation = mitigatedPhysical + mitigatedFire + mitigatedCold + mitigatedLightning + mitigatedChaos;

        return new DefenseResult
        {
            Evaded = false,
            Blocked = false,
            Suppressed = suppressed,
            TotalIncomingDamage = totalIncoming,
            TotalMitigatedDamage = totalIncoming - totalAfterMitigation,
            TotalDamageApplied = totalApplied,
            MitigatedBreakdown = new HitDamageBreakdown(
                mitigatedPhysical, mitigatedFire, mitigatedCold, mitigatedLightning, mitigatedChaos),
            PlayerDied = !defender.IsAlive
        };
    }

    /// <summary>Backwards compatible overload for PlayerState defender.</summary>
    public static DefenseResult ProcessHit(
        HitDamageBreakdown incomingDamage,
        bool isAttack,
        bool isSpell,
        PlayerState player,
        DefensiveStats stats,
        Random? random = null) => ProcessHit(incomingDamage, isAttack, isSpell, (ICombatEntity)player, stats, random);

    // ─────────────────────────────────────────────
    //  Individual Mitigation Functions
    // ─────────────────────────────────────────────

    /// <summary>
    /// Applies the PoE Armour formula to physical damage:
    /// % Reduction = Armour / (Armour + 5 * Damage), capped at 90%.
    /// Returns the damage after reduction.
    /// </summary>
    public static float ApplyArmourMitigation(float physicalDamage, float armour)
    {
        if (physicalDamage <= 0f) return 0f;
        if (armour <= 0f) return physicalDamage;

        float reductionPercent = armour / (armour + 5f * physicalDamage);
        reductionPercent = Math.Clamp(reductionPercent, 0f, 0.90f);
        return physicalDamage * (1f - reductionPercent);
    }

    /// <summary>
    /// Applies elemental or chaos resistance to damage.
    /// Resistance is clamped between -100% (amplified) and the effective max (up to 90%).
    /// Returns the damage after resistance.
    /// </summary>
    public static float ApplyResistanceMitigation(float damage, float effectiveResistance)
    {
        if (damage <= 0f) return 0f;
        float clampedRes = Math.Clamp(effectiveResistance, -100f, 90f);
        return damage * (1f - clampedRes / 100f);
    }

    /// <summary>
    /// Calculates Evasion chance from Evasion Rating vs monster Accuracy Rating.
    /// Uses PoE formula: Chance to Evade = 1 - Accuracy / (Accuracy + (Evasion/4)^0.8).
    /// Clamped to [5%, 95%].
    /// </summary>
    public static float CalculateEvasionChance(float evasionRating, float monsterAccuracy)
    {
        if (evasionRating <= 0f) return 5f;
        if (monsterAccuracy <= 0f) return 95f;

        float evasionTerm = MathF.Pow(evasionRating / 4f, 0.8f);
        float chanceToEvade = 1f - monsterAccuracy / (monsterAccuracy + evasionTerm);
        float percent = chanceToEvade * 100f;
        return Math.Clamp(percent, 5f, 95f);
    }
}

/// <summary>
/// Outcome of processing a single hit through the defensive pipeline.
/// </summary>
public sealed class DefenseResult
{
    /// <summary>True if the attack was evaded (entropy check). No damage applied.</summary>
    public bool Evaded { get; init; }

    /// <summary>True if the hit was blocked. No damage applied.</summary>
    public bool Blocked { get; init; }

    /// <summary>True if the spell was suppressed (50% less damage).</summary>
    public bool Suppressed { get; init; }

    /// <summary>Total raw incoming damage before any mitigation.</summary>
    public float TotalIncomingDamage { get; init; }

    /// <summary>Total damage prevented by all defensive layers combined.</summary>
    public float TotalMitigatedDamage { get; init; }

    /// <summary>Total damage actually applied to the player's health pools.</summary>
    public float TotalDamageApplied { get; init; }

    /// <summary>Per-type damage after mitigation (Armour/Resistances/Suppression).</summary>
    public HitDamageBreakdown? MitigatedBreakdown { get; init; }

    /// <summary>True if the target entity died as a result of this hit.</summary>
    public bool PlayerDied { get; init; }

    /// <summary>True if the target entity died as a result of this hit (alias for PlayerDied).</summary>
    public bool TargetDied => PlayerDied;

    /// <summary>True if no damage was dealt (evaded or blocked).</summary>
    public bool FullyAvoided => Evaded || Blocked;
}
