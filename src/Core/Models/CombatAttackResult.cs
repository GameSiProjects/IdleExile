namespace IdleExile.Core.Models;

using IdleExile.Core.Services;

/// <summary>
/// Comprehensive outcome of a resolved combat hit between an attacker and target.
/// Symmetrically produced whether the initiator is Player or Monster.
/// </summary>
public sealed record CombatAttackResult(
    /// <summary>The combat entity that initiated the attack.</summary>
    ICombatEntity Attacker,

    /// <summary>The recipient combat entity.</summary>
    ICombatEntity Target,

    /// <summary>The offensive hit roll outcome (damage breakdown, crit status, inflicted ailments).</summary>
    HitResult Hit,

    /// <summary>The defensive evaluation outcome (evasion, block, suppression, mitigation, applied damage).</summary>
    DefenseResult Defense,

    /// <summary>Actual damage deducted from the target's resource pools.</summary>
    float DamageDealt,

    /// <summary>True if the target suffered fatal damage from this hit.</summary>
    bool TargetDied
)
{
    /// <summary>Convenience helper for an empty/inactive hit result.</summary>
    public static CombatAttackResult Empty(ICombatEntity attacker, ICombatEntity target)
    {
        var zeroBreakdown = new HitDamageBreakdown(0f, 0f, 0f, 0f, 0f);
        var emptyHit = new HitResult(
            Damage: zeroBreakdown,
            TotalDamage: 0f,
            IsCrit: false,
            CritMultiplier: 100f,
            InflictedAilments: System.Array.Empty<AilmentInstance>(),
            UnmitigatedDamage: zeroBreakdown
        );
        var emptyDefense = new DefenseResult
        {
            Evaded = true,
            TotalIncomingDamage = 0f,
            TotalMitigatedDamage = 0f,
            TotalDamageApplied = 0f,
            MitigatedBreakdown = zeroBreakdown,
            PlayerDied = false
        };

        return new CombatAttackResult(attacker, target, emptyHit, emptyDefense, 0f, !target.IsAlive);
    }
}
