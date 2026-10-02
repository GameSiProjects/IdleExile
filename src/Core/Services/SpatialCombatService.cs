using System;
using System.Collections.Generic;
using System.Linq;
using IdleExile.Core.Models;

namespace IdleExile.Core.Services;

/// <summary>
/// Skill geometry taxonomy matching PoE / Top-Down ARPG mechanics.
/// </summary>
public enum SkillGeometryType
{
    /// <summary>Direct single target hit (Strike skills, single arrows).</summary>
    SingleTarget = 0,

    /// <summary>360-degree radial explosion / pulse (Nova, Fireball impact explosion, RF aura).</summary>
    RadialAoE = 1,

    /// <summary>Directional frontal cone / arc sweep (Cleave, Ground Slam, Freezing Pulse).</summary>
    Cone = 2,

    /// <summary>Multi-directional projectile spread (GMP, Split Arrow, LMP).</summary>
    ProjectileSpread = 3
}

/// <summary>
/// Calculated trajectory ray for a multi-vector projectile in 2D space.
/// </summary>
public readonly record struct ProjectileVector(
    int Index,
    float AngleRadians,
    Vector2D Velocity,
    Vector2D Direction
);

/// <summary>
/// Pure C# Spatial Geometry & 2D Math service for Top-Down combat simulation.
/// Implements Section 4 of docs/03_combat_and_stats.md:
/// - 4.1 Euclidean Distance & Ranges (Melee 35px, Medium AoE 70px, Screen 220px).
/// - 4.2 Skill Geometries & Hit Testing:
///     1. Radial AoE with Increased AoE radius scaling: R_effective = R_base * sqrt(1 + sum %inc / 100).
///     2. Cones & Arcs: cos(delta_theta) = (V_facing . toTarget) / (|V_facing| * |toTarget|) >= cos(alpha / 2) && d <= L_range.
///     3. Multi-Vector Projectile Spread: theta_k = theta_aim + (k - (N-1)/2) * delta_phi.
///        Pierce & Chain to nearest target within R_chain <= 60px.
/// - 4.3 Autonomous AI Target Selection (Threat Scoring formula).
///
/// Strictly decoupled from Godot.Node to preserve headless testing and instant offline calculation.
/// </summary>
public static class SpatialCombatService
{
    // ─────────────────────────────────────────────
    //  Canonical Range Constants (Docs 4.1)
    // ─────────────────────────────────────────────

    /// <summary>Melee strike contact range (35px).</summary>
    public const float MeleeRange = 35.0f;

    /// <summary>Medium AoE / Aura radius (70px) — Righteous Fire, Blasphemy curses, Warcries.</summary>
    public const float MediumAoERange = 70.0f;

    /// <summary>Maximum ranged screen projectile distance (220px) spanning the 440 x 208 arena.</summary>
    public const float ScreenProjectileRange = 220.0f;

    /// <summary>Maximum chain jump search radius (60px).</summary>
    public const float ChainSearchRadius = 60.0f;

    /// <summary>Default cluster density search radius (70px) for threat scoring.</summary>
    public const float ClusterDensityRadius = 70.0f;

    /// <summary>Standard focal position of Exile in the 440 x 208 arena (220, 104).</summary>
    public static readonly Vector2D DefaultExilePosition = new(220.0f, 104.0f);

    // ─────────────────────────────────────────────
    //  4.1. Euclidean Distance & Range Queries
    // ─────────────────────────────────────────────

    /// <summary>
    /// Computes the exact Euclidean distance between two points in 2D space:
    /// d(P1, P2) = sqrt((x2 - x1)^2 + (y2 - y1)^2).
    /// </summary>
    public static float CalculateDistance(Vector2D p1, Vector2D p2) => p1.DistanceTo(p2);

    /// <summary>
    /// Checks whether target is within the specified maximum Euclidean distance.
    /// </summary>
    public static bool IsInRange(Vector2D origin, Vector2D target, float maxRange) =>
        origin.DistanceSquaredTo(target) <= maxRange * maxRange;

    // ─────────────────────────────────────────────
    //  4.2.1. Radial AoE (360-Degree Nova / Aura / Explosions)
    // ─────────────────────────────────────────────

    /// <summary>
    /// Calculates the effective AoE radius according to Path of Exile's area scaling formula:
    /// R_effective = R_base * sqrt(1 + sum % Increased AoE / 100).
    /// </summary>
    /// <param name="baseRadius">Base skill AoE radius in pixels.</param>
    /// <param name="increasedAoEPercent">Sum of all % Increased Area of Effect modifiers (can be negative for reduced).</param>
    public static float CalculateEffectiveAoERadius(float baseRadius, float increasedAoEPercent)
    {
        if (baseRadius <= 0f) return 0f;
        float factor = 1.0f + (increasedAoEPercent / 100.0f);
        if (factor <= 0f) return 0f;
        return baseRadius * MathF.Sqrt(factor);
    }

    /// <summary>
    /// Tests whether a target point (and its hitbox radius) is inside a radial AoE circle.
    /// Condition: d(P_origin, P_target) &lt;= R_effective + R_target.
    /// </summary>
    public static bool IsInRadialAoE(Vector2D origin, Vector2D target, float effectiveRadius, float targetRadius = 0f)
    {
        float totalAllowedRadius = effectiveRadius + targetRadius;
        if (totalAllowedRadius <= 0f) return false;
        return origin.DistanceSquaredTo(target) <= (totalAllowedRadius * totalAllowedRadius);
    }

    /// <summary>
    /// Queries all alive monsters in the given candidate collection that fall within the radial AoE circle.
    /// </summary>
    public static IReadOnlyList<MonsterInstance> QueryRadialAoE(
        Vector2D origin,
        float effectiveRadius,
        IEnumerable<MonsterInstance> candidates)
    {
        if (effectiveRadius <= 0f)
            return Array.Empty<MonsterInstance>();

        var result = new List<MonsterInstance>();
        foreach (var m in candidates)
        {
            if (m.IsAlive && IsInRadialAoE(origin, m.Position, effectiveRadius, m.Radius))
            {
                result.Add(m);
            }
        }
        return result;
    }

    // ─────────────────────────────────────────────
    //  4.2.2. Cones & Arcs (Frontal Sweep / Cleave / Ground Slam)
    // ─────────────────────────────────────────────

    /// <summary>
    /// Evaluates whether a target point falls within a directional cone/arc.
    /// Formula from Docs 4.2.2:
    ///   cos(delta_theta) = (V_facing . (P_M - P_E)) / (|V_facing| * |P_M - P_E|) >= cos(alpha / 2) && d &lt;= L_range.
    /// </summary>
    /// <param name="origin">Apex of the cone (typically Exile's position).</param>
    /// <param name="facingDirection">Normalized direction vector Exile is aiming/facing.</param>
    /// <param name="target">Monster center position.</param>
    /// <param name="coneAngleDegrees">Total aperture angle alpha in degrees (e.g. 75 degrees for cone, 180 degrees for cleave).</param>
    /// <param name="maxRange">Maximum reach of the cone (L_range).</param>
    /// <param name="targetRadius">Monster hitbox radius.</param>
    public static bool IsInCone(
        Vector2D origin,
        Vector2D facingDirection,
        Vector2D target,
        float coneAngleDegrees,
        float maxRange,
        float targetRadius = 0f)
    {
        Vector2D toTarget = target - origin;
        float dist = toTarget.Length;

        // If target is directly on origin or inside its personal hitbox, consider it hit
        if (dist <= targetRadius)
            return true;

        // Range boundary test
        if (dist - targetRadius > maxRange)
            return false;

        // Facing direction normalization
        Vector2D normFacing = facingDirection.Normalized();
        if (normFacing == Vector2D.Zero)
            return false;

        Vector2D normToTarget = toTarget / dist;

        // Angle aperture test: cos(delta_theta) >= cos(alpha / 2)
        float cosHalfAlpha = MathF.Cos((coneAngleDegrees * 0.5f) * (MathF.PI / 180.0f));
        float cosDeltaTheta = normFacing.Dot(normToTarget);

        // Account for monster hitbox radius extending into the cone
        if (cosDeltaTheta >= cosHalfAlpha)
            return true;

        if (targetRadius > 0f && dist > 0f)
        {
            float angularTolerance = MathF.Asin(Math.Clamp(targetRadius / dist, 0f, 1f));
            float halfAlphaRad = (coneAngleDegrees * 0.5f) * (MathF.PI / 180.0f);
            float actualDeltaAngle = MathF.Acos(Math.Clamp(cosDeltaTheta, -1f, 1f));
            return (actualDeltaAngle - angularTolerance) <= halfAlphaRad;
        }

        return false;
    }

    /// <summary>
    /// Queries all alive monsters in the given candidate collection that fall within the directional cone/arc.
    /// </summary>
    public static IReadOnlyList<MonsterInstance> QueryCone(
        Vector2D origin,
        Vector2D facingDirection,
        float coneAngleDegrees,
        float maxRange,
        IEnumerable<MonsterInstance> candidates)
    {
        var result = new List<MonsterInstance>();
        foreach (var m in candidates)
        {
            if (m.IsAlive && IsInCone(origin, facingDirection, m.Position, coneAngleDegrees, maxRange, m.Radius))
            {
                result.Add(m);
            }
        }
        return result;
    }

    // ─────────────────────────────────────────────
    //  4.2.3. Multi-Vector Projectile Spread (GMP / Split Arrow)
    // ─────────────────────────────────────────────

    /// <summary>
    /// Calculates angular trajectories for N projectiles in a multi-vector volley.
    /// Formula from Docs 4.2.3:
    ///   theta_k = theta_aim + (k - (N - 1) / 2) * delta_phi  (k = 0, ..., N-1).
    /// </summary>
    /// <param name="aimAngleRadians">Central aiming direction in radians.</param>
    /// <param name="projectileCount">Total number of projectiles (N).</param>
    /// <param name="spreadStepRadians">Angular divergence step between adjacent projectiles (delta_phi).</param>
    /// <param name="speed">Flight velocity magnitude S in pixels/sec.</param>
    public static IReadOnlyList<ProjectileVector> CalculateSpread(
        float aimAngleRadians,
        int projectileCount,
        float spreadStepRadians,
        float speed = 520.0f)
    {
        if (projectileCount <= 0)
            return Array.Empty<ProjectileVector>();

        var vectors = new List<ProjectileVector>(projectileCount);
        float halfN = (projectileCount - 1) * 0.5f;

        for (int k = 0; k < projectileCount; k++)
        {
            float thetaK = aimAngleRadians + (k - halfN) * spreadStepRadians;
            var dir = Vector2D.FromAngle(thetaK);
            var vel = dir * speed;
            vectors.Add(new ProjectileVector(k, thetaK, vel, dir));
        }

        return vectors;
    }

    /// <summary>
    /// Finds the optimal Chain target for an in-flight projectile:
    /// Searches for the closest alive monster within R_chain &lt;= 60px that hasn't already been chained to.
    /// </summary>
    /// <param name="currentImpactPos">Position of current hit.</param>
    /// <param name="currentTargetId">Instance ID of currently impacted entity (cannot chain to itself).</param>
    /// <param name="candidates">All monsters in current wave.</param>
    /// <param name="chainRadius">Search radius limit (default 60px).</param>
    /// <param name="visitedTargetIds">Set of already impacted monster IDs to prevent infinite looping.</param>
    public static MonsterInstance? FindChainTarget(
        Vector2D currentImpactPos,
        long currentTargetId,
        IEnumerable<MonsterInstance> candidates,
        float chainRadius = ChainSearchRadius,
        ISet<long>? visitedTargetIds = null)
    {
        MonsterInstance? bestTarget = null;
        float bestDistSq = chainRadius * chainRadius;

        foreach (var m in candidates)
        {
            if (!m.IsAlive || m.InstanceId == currentTargetId)
                continue;

            if (visitedTargetIds != null && visitedTargetIds.Contains(m.InstanceId))
                continue;

            float dSq = currentImpactPos.DistanceSquaredTo(m.Position);
            if (dSq <= bestDistSq)
            {
                bestDistSq = dSq;
                bestTarget = m;
            }
        }

        return bestTarget;
    }

    /// <summary>
    /// Continuous Collision Detection (CCD) for in-flight projectiles:
    /// Evaluates whether the line segment from P0 to P1 intersects a circular entity hitbox (center C, radius R).
    /// If true, outputs hitT in [0, 1] representing the earliest point of entry along the segment,
    /// and the exact impact point P_impact = P0 + hitT * (P1 - P0).
    /// Prevents projectile tunneling at any flight velocity or frame rate.
    /// </summary>
    /// <param name="p0">Segment starting position (previous tick position).</param>
    /// <param name="p1">Segment ending position (current tick position).</param>
    /// <param name="circleCenter">Center coordinates of candidate entity hitbox.</param>
    /// <param name="combinedRadius">Combined collision radius (Entity Radius + Projectile Radius).</param>
    /// <param name="hitT">Output normalized progress [0, 1] along segment at earliest contact.</param>
    /// <param name="impactPoint">Output coordinates of impact in 2D space.</param>
    /// <returns>True if segment enters or starts inside the circle; false otherwise.</returns>
    public static bool SegmentIntersectsCircle(
        Vector2D p0,
        Vector2D p1,
        Vector2D circleCenter,
        float combinedRadius,
        out float hitT,
        out Vector2D impactPoint)
    {
        hitT = 1f;
        impactPoint = p1;

        if (combinedRadius <= 0f)
            return false;

        float rSq = combinedRadius * combinedRadius;
        Vector2D toCenter = circleCenter - p0;
        float distSqStart = toCenter.LengthSquared;

        // Case 1: Start point P0 is already inside the circle
        if (distSqStart <= rSq)
        {
            hitT = 0f;
            impactPoint = p0;
            return true;
        }

        Vector2D seg = p1 - p0;
        float segLenSq = seg.LengthSquared;

        // Case 2: Stationary / zero-length step
        if (segLenSq < 1e-6f)
        {
            return false;
        }

        // Project circle center onto segment ray: t_proj = (toCenter . seg) / |seg|^2
        float proj = toCenter.Dot(seg);

        // If projection is negative or zero, circle center is behind P0 and P0 is outside -> no collision
        if (proj <= 0f)
        {
            return false;
        }

        // Quadratic intersection: a*t^2 - 2*b'*t + c = 0
        // a = segLenSq, b' = proj, c = distSqStart - rSq
        float c = distSqStart - rSq;
        float discriminantQuarter = (proj * proj) - (segLenSq * c);

        if (discriminantQuarter < 0f)
        {
            // Ray does not intersect the circle
            return false;
        }

        float sqrtDisc = MathF.Sqrt(discriminantQuarter);
        float tEntry = (proj - sqrtDisc) / segLenSq;

        if (tEntry >= 0f && tEntry <= 1.0f)
        {
            hitT = tEntry;
            impactPoint = p0 + seg * tEntry;
            return true;
        }

        // If tEntry > 1.0, the segment didn't reach the circle yet this step
        return false;
    }

    // ─────────────────────────────────────────────
    //  4.3. Autonomous AI Target Selection & Threat Scoring
    // ─────────────────────────────────────────────

    /// <summary>
    /// Calculates monster cluster density: counts how many other living monsters are within clusterRadius.
    /// Used by AoE skills to prioritize dense monster packs for maximum DPS efficiency.
    /// </summary>
    public static int CountNearbyMonsters(
        Vector2D targetPos,
        long targetInstanceId,
        IEnumerable<MonsterInstance> allMonsters,
        float clusterRadius = ClusterDensityRadius)
    {
        float rSq = clusterRadius * clusterRadius;
        int count = 0;

        foreach (var m in allMonsters)
        {
            if (m.IsAlive && m.InstanceId != targetInstanceId)
            {
                if (targetPos.DistanceSquaredTo(m.Position) <= rSq)
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>
    /// Computes the Autonomous AI Threat Score for a given monster.
    /// Canonical formula from Docs 4.3:
    ///   Threat Score(M) = W_Rarity * 1000 + W_Boss * 5000 + (500 / (d(P_E, P_M) + 1)) + ClusterBonus(M).
    ///
    /// Additional rules:
    /// - Rare monsters carrying dangerous Aura Modifiers receive an extra +500 threat.
    /// - ClusterBonus scales based on skill type: AoE skills gain +150 per clustered monster; single-target +40.
    /// </summary>
    public static float CalculateThreatScore(
        MonsterInstance monster,
        Vector2D exilePos,
        IEnumerable<MonsterInstance> allLivingMonsters,
        bool isAoESkill = false)
    {
        if (!monster.IsAlive)
            return 0f;

        // 1. Rarity & Boss weights
        float wRarity = (int)monster.Rarity; // Normal: 0, Magic: 1, Rare: 2, Boss: 3
        float wBoss = monster.Rarity == MonsterRarity.Boss ? 1.0f : 0.0f;

        float rarityScore = wRarity * 1000.0f;
        float bossScore = wBoss * 5000.0f;

        // Rare monsters with aura modifiers are high priority targets
        float auraBonus = (monster.Rarity == MonsterRarity.Rare && monster.AuraModifiers.Count > 0) ? 500.0f : 0.0f;

        // 2. Proximity term: 500 / (d(P_E, P_M) + 1)
        float distance = exilePos.DistanceTo(monster.Position);
        float proximityScore = 500.0f / (distance + 1.0f);

        // 3. Cluster Density Bonus
        int nearbyCount = CountNearbyMonsters(monster.Position, monster.InstanceId, allLivingMonsters, ClusterDensityRadius);
        float clusterBonusMultiplier = isAoESkill ? 150.0f : 40.0f;
        float clusterBonus = nearbyCount * clusterBonusMultiplier;

        return rarityScore + bossScore + auraBonus + proximityScore + clusterBonus;
    }

    /// <summary>
    /// Selects the best target from a collection of candidate monsters using the Threat Scoring algorithm.
    /// Honors target stickiness: only switches away from an alive current target if a candidate has a
    /// significantly higher threat score (or strictly higher rarity tier).
    /// </summary>
    public static MonsterInstance? SelectTargetByThreat(
        IEnumerable<MonsterInstance> candidates,
        Vector2D exilePos,
        MonsterInstance? currentTarget = null,
        bool isAoESkill = false,
        float stickinessThreshold = 250.0f)
    {
        var living = candidates.Where(m => m.IsAlive).ToList();
        if (living.Count == 0)
            return null;

        MonsterInstance? bestCandidate = null;
        float highestScore = float.MinValue;

        foreach (var m in living)
        {
            float score = CalculateThreatScore(m, exilePos, living, isAoESkill);
            if (score > highestScore)
            {
                highestScore = score;
                bestCandidate = m;
            }
        }

        // Stickiness check if current target is still alive and in the candidate pool
        if (currentTarget != null && currentTarget.IsAlive && living.Contains(currentTarget))
        {
            // Always switch if a strictly higher rarity monster exists (e.g. Boss or Rare spawned)
            if ((int)bestCandidate!.Rarity > (int)currentTarget.Rarity)
            {
                return bestCandidate;
            }

            // Otherwise, evaluate threat score difference against stickiness threshold
            float currentScore = CalculateThreatScore(currentTarget, exilePos, living, isAoESkill);
            if (highestScore > currentScore + stickinessThreshold)
            {
                return bestCandidate;
            }

            return currentTarget;
        }

        return bestCandidate;
    }
}
