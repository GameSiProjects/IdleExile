using System;
using System.Collections.Generic;
using System.Linq;
using IdleExile.Core.Models;
using IdleExile.Core.Services;

namespace IdleExile.Core.Simulation;

/// <summary>
/// Autonomous targeting system for zero-APM idle combat simulation.
/// Implements Section 4.3 Autonomous AI Target Selection (Threat Scoring)
/// and spatial geometry hit selection (Radial AoE, Cone, Spread).
///
/// Threat Scoring Formula (Docs 4.3):
///   Threat Score(M) = W_Rarity * 1000 + W_Boss * 5000 + (500 / (d(P_E, P_M) + 1)) + ClusterBonus(M)
///
/// Pure C# — no Godot Node dependencies.
/// </summary>
public sealed class TargetingStrategy
{
    /// <summary>
    /// Selects the optimal target from living candidates using Autonomous AI Threat Scoring.
    /// Honors target stickiness: only switches away from an alive current target if a candidate has a
    /// strictly higher rarity or significantly higher threat score.
    /// </summary>
    /// <param name="candidates">All candidate monsters in the encounter.</param>
    /// <param name="exilePos">Current 2D position of the player Exile.</param>
    /// <param name="currentTarget">The monster currently being attacked (if any).</param>
    /// <param name="isAoESkill">True if currently using an AoE skill (prioritizes dense packs).</param>
    /// <returns>The chosen monster to attack, or null if no alive enemies remain.</returns>
    public MonsterInstance? SelectTarget(
        IEnumerable<MonsterInstance> candidates,
        Vector2D exilePos,
        MonsterInstance? currentTarget = null,
        bool isAoESkill = false)
    {
        return SpatialCombatService.SelectTargetByThreat(candidates, exilePos, currentTarget, isAoESkill);
    }

    /// <summary>
    /// Backward-compatible overload assuming default center Exile position (220, 104).
    /// </summary>
    public MonsterInstance? SelectTarget(
        IEnumerable<MonsterInstance> candidates,
        MonsterInstance? currentTarget = null)
    {
        return SelectTarget(candidates, SpatialCombatService.DefaultExilePosition, currentTarget, isAoESkill: false);
    }

    /// <summary>
    /// Spatial geometry hit query for AoE skills:
    /// - RadialAoE: 360-degree circle centered on primary target (impact) or Exile (nova/aura).
    /// - Cone: Directional frontal sweep from Exile towards primary target.
    /// - SingleTarget: Hits only primary target.
    /// </summary>
    public IReadOnlyList<MonsterInstance> SelectSpatialAoETargets(
        IEnumerable<MonsterInstance> candidates,
        Vector2D exilePos,
        MonsterInstance? primaryTarget,
        SkillGeometryType geometryType,
        float baseRadiusOrRange,
        float coneAngleDegrees = 75f,
        int maxTargets = 5,
        float increasedAoE = 0f,
        bool isOriginAtTarget = true)
    {
        if (maxTargets <= 0)
            return Array.Empty<MonsterInstance>();

        var alive = candidates.Where(m => m.IsAlive).ToList();
        if (alive.Count == 0)
            return Array.Empty<MonsterInstance>();

        // Ensure we have a valid primary target
        var target = primaryTarget != null && primaryTarget.IsAlive && alive.Contains(primaryTarget)
            ? primaryTarget
            : SelectTarget(alive, exilePos, primaryTarget, isAoESkill: true);

        if (target == null)
            return Array.Empty<MonsterInstance>();

        switch (geometryType)
        {
            case SkillGeometryType.Cone:
            {
                // Facing direction towards primary target
                Vector2D toTarget = target.Position - exilePos;
                Vector2D facing = toTarget.LengthSquared > 0.001f ? toTarget.Normalized() : Vector2D.UnitX;
                float effectiveRange = baseRadiusOrRange > 0f ? baseRadiusOrRange : SpatialCombatService.MeleeRange + 25f;

                var inCone = SpatialCombatService.QueryCone(exilePos, facing, coneAngleDegrees, effectiveRange, alive);

                // Ensure primary target is first in list
                var ordered = new List<MonsterInstance>(maxTargets) { target };
                foreach (var m in inCone)
                {
                    if (m.InstanceId != target.InstanceId && ordered.Count < maxTargets)
                    {
                        ordered.Add(m);
                    }
                }
                return ordered;
            }

            case SkillGeometryType.RadialAoE:
            {
                float effectiveRadius = SpatialCombatService.CalculateEffectiveAoERadius(baseRadiusOrRange, increasedAoE);
                Vector2D center = isOriginAtTarget ? target.Position : exilePos;

                var inRadius = SpatialCombatService.QueryRadialAoE(center, effectiveRadius, alive);

                var ordered = new List<MonsterInstance>(maxTargets) { target };
                foreach (var m in inRadius.OrderBy(m => center.DistanceSquaredTo(m.Position)))
                {
                    if (m.InstanceId != target.InstanceId && ordered.Count < maxTargets)
                    {
                        ordered.Add(m);
                    }
                }
                return ordered;
            }

            default:
            {
                // Single target or generic proximity fallback
                return SelectAoETargets(alive, target, maxTargets);
            }
        }
    }

    /// <summary>
    /// Legacy fallback method for AoE selection:
    /// Primary target is index 0, followed by nearest living enemies.
    /// </summary>
    public IReadOnlyList<MonsterInstance> SelectAoETargets(
        IEnumerable<MonsterInstance> candidates,
        MonsterInstance? primaryTarget,
        int maxTargets)
    {
        if (maxTargets <= 0)
            return Array.Empty<MonsterInstance>();

        var alive = candidates.Where(m => m.IsAlive).ToList();
        if (alive.Count == 0)
            return Array.Empty<MonsterInstance>();

        // Ensure we have a valid primary target
        var target = primaryTarget != null && primaryTarget.IsAlive && alive.Contains(primaryTarget)
            ? primaryTarget
            : SelectTarget(alive);

        if (target == null)
            return Array.Empty<MonsterInstance>();

        var result = new List<MonsterInstance>(maxTargets) { target };

        if (maxTargets == 1)
            return result;

        // Fill remaining slots with other alive monsters sorted by proximity to target, then rarity and lowest health
        var secondaryTargets = alive
            .Where(m => m.InstanceId != target.InstanceId)
            .OrderBy(m => target.Position.DistanceSquaredTo(m.Position))
            .ThenByDescending(m => (int)m.Rarity)
            .ThenBy(m => m.CurrentLife)
            .Take(maxTargets - 1);

        result.AddRange(secondaryTargets);
        return result;
    }

    /// <summary>
    /// Evaluates whether the player should switch from current target to a new candidate.
    /// Returns true if candidate has strictly higher rarity or current is dead.
    /// </summary>
    public bool ShouldSwitchTarget(MonsterInstance? currentTarget, MonsterInstance newCandidate)
    {
        if (currentTarget == null || !currentTarget.IsAlive)
            return true;

        if (!newCandidate.IsAlive)
            return false;

        return (int)newCandidate.Rarity > (int)currentTarget.Rarity;
    }
}
