using System;
using System.Collections.Generic;
using System.Linq;
using IdleExile.Core.Models;
using IdleExile.Core.Services;
using IdleExile.Core.Simulation;

namespace IdleExile.Core.Tests;

/// <summary>
/// Comprehensive unit tests for Section 4: Spatial Geometry & 2D Math (docs/03_combat_and_stats.md).
/// Validates:
/// 1. Section 4.1: Euclidean Distance and Range Boundaries (Melee 35px, Medium AoE 70px, Screen 220px).
/// 2. Section 4.2.1: Radial AoE Hit Testing & Area scaling formula: R_eff = R_base * sqrt(1 + sum %inc / 100).
/// 3. Section 4.2.2: Cones & Arcs: cos(delta_theta) >= cos(alpha / 2) and d <= L_range.
/// 4. Section 4.2.3: Multi-Vector Projectile Spread (theta_k = theta_aim + (k - (N-1)/2)*delta_phi) & Chain (R <= 60px).
/// 5. Section 4.3: Autonomous AI Target Selection & Threat Scoring formula.
/// </summary>
public static class SpatialGeometryTests
{
    public static void RunAll()
    {
        Console.WriteLine("[Tests] Running SpatialGeometryTests (Section 4)...");

        TestEuclideanDistanceAndRanges();
        TestRadialAoEAndAreaScaling();
        TestConeAndArcHitTesting();
        TestMultiVectorProjectileSpread();
        TestProjectileChainTargetFinding();
        TestThreatScoringFormula();
        TestTargetStickinessAndSwitching();
        TestPackFormationSpatialLayout();

        Console.WriteLine("[Tests] All SpatialGeometryTests PASSED successfully!");
    }

    private static void AssertEqual(float expected, float actual, float tolerance = 0.001f, string message = "")
    {
        if (MathF.Abs(expected - actual) > tolerance)
        {
            throw new Exception($"Assertion failed: Expected {expected:F4}, got {actual:F4}. {message}");
        }
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception($"Assertion failed: {message}");
        }
    }

    private static void AssertFalse(bool condition, string message)
    {
        if (condition)
        {
            throw new Exception($"Assertion failed (expected false): {message}");
        }
    }

    // ─────────────────────────────────────────────
    //  4.1. Euclidean Distance & Range Boundaries
    // ─────────────────────────────────────────────

    private static void TestEuclideanDistanceAndRanges()
    {
        var p1 = new Vector2D(0f, 0f);
        var p2 = new Vector2D(30f, 40f);

        // 3-4-5 right triangle -> distance must be exactly 50
        float dist = SpatialCombatService.CalculateDistance(p1, p2);
        AssertEqual(50.0f, dist, 0.001f, "Euclidean distance formula d = sqrt(dx^2 + dy^2)");

        // Melee Range boundary: d <= 35px
        var exile = new Vector2D(220f, 104f);
        var inMelee = new Vector2D(220f + 30f, 104f);
        var outsideMelee = new Vector2D(220f + 40f, 104f);

        AssertTrue(SpatialCombatService.IsInRange(exile, inMelee, SpatialCombatService.MeleeRange), "Point at 30px is within Melee range (<= 35px)");
        AssertFalse(SpatialCombatService.IsInRange(exile, outsideMelee, SpatialCombatService.MeleeRange), "Point at 40px is outside Melee range (> 35px)");

        // Medium AoE / Aura boundary: d <= 70px
        var inMediumAoE = new Vector2D(220f, 104f + 65f);
        var outsideMediumAoE = new Vector2D(220f, 104f + 75f);
        AssertTrue(SpatialCombatService.IsInRange(exile, inMediumAoE, SpatialCombatService.MediumAoERange), "Point at 65px is within Medium AoE range (<= 70px)");
        AssertFalse(SpatialCombatService.IsInRange(exile, outsideMediumAoE, SpatialCombatService.MediumAoERange), "Point at 75px is outside Medium AoE range (> 70px)");

        // Screen Projectile Range boundary: d <= 220px
        var inScreenRange = new Vector2D(220f + 200f, 104f);
        var outsideScreenRange = new Vector2D(220f + 230f, 104f);
        AssertTrue(SpatialCombatService.IsInRange(exile, inScreenRange, SpatialCombatService.ScreenProjectileRange), "Point at 200px is within Screen range (<= 220px)");
        AssertFalse(SpatialCombatService.IsInRange(exile, outsideScreenRange, SpatialCombatService.ScreenProjectileRange), "Point at 230px is outside Screen range (> 220px)");
    }

    // ─────────────────────────────────────────────
    //  4.2.1. Radial AoE & Effective Area Scaling
    // ─────────────────────────────────────────────

    private static void TestRadialAoEAndAreaScaling()
    {
        float baseRadius = 50.0f;

        // 1. Zero increased AoE -> R_eff = R_base
        float rEff0 = SpatialCombatService.CalculateEffectiveAoERadius(baseRadius, 0f);
        AssertEqual(50.0f, rEff0, 0.001f, "0% Increased AoE must leave radius unchanged");

        // 2. +44% Increased AoE -> sqrt(1 + 0.44) = sqrt(1.44) = 1.2 -> R_eff = 50 * 1.2 = 60
        float rEff44 = SpatialCombatService.CalculateEffectiveAoERadius(baseRadius, 44f);
        AssertEqual(60.0f, rEff44, 0.001f, "+44% Increased AoE must increase radius to 60px (1.2x)");

        // 3. +100% Increased AoE -> sqrt(2) * 50 = ~70.7107px
        float rEff100 = SpatialCombatService.CalculateEffectiveAoERadius(baseRadius, 100f);
        AssertEqual(50f * MathF.Sqrt(2f), rEff100, 0.001f, "+100% Increased AoE must scale radius by sqrt(2)");

        // 4. Hit testing with monster radius
        var origin = new Vector2D(100f, 100f);
        var monsterPos = new Vector2D(100f + 55f, 100f); // distance = 55px
        float monsterHitbox = 8.5f;

        // With base radius 50px: 55px > 50px, but 55px <= (50px + 8.5px = 58.5px) -> Hit!
        AssertTrue(SpatialCombatService.IsInRadialAoE(origin, monsterPos, 50f, monsterHitbox),
            "Monster edge overlapping with AoE circle must be registered as hit");

        // Point at 60px is outside 50px + 8.5px (58.5px) -> Miss!
        var farMonsterPos = new Vector2D(100f + 60f, 100f);
        AssertFalse(SpatialCombatService.IsInRadialAoE(origin, farMonsterPos, 50f, monsterHitbox),
            "Monster beyond AoE radius + hitbox must miss");

        // Query collections
        var m1 = new MonsterInstance(1, "Mob1", MonsterRarity.Normal, 1, 100f, DamageType.Physical, 5, 10, 1, 5, 130, 50, 10, 1, 1, initialPosition: new Vector2D(120f, 100f));
        var m2 = new MonsterInstance(2, "Mob2", MonsterRarity.Normal, 1, 100f, DamageType.Physical, 5, 10, 1, 5, 130, 50, 10, 1, 1, initialPosition: new Vector2D(190f, 100f)); // 90px away

        var hits = SpatialCombatService.QueryRadialAoE(origin, 50f, new[] { m1, m2 });
        AssertEqual(1, hits.Count, 0.001f, "Only mob within 50px effective radius should be hit");
        AssertTrue(hits[0].InstanceId == 1, "m1 should be the hit monster");
    }

    // ─────────────────────────────────────────────
    //  4.2.2. Cones & Arcs Hit Testing
    // ─────────────────────────────────────────────

    private static void TestConeAndArcHitTesting()
    {
        var origin = new Vector2D(100f, 100f);
        var facingRight = new Vector2D(1f, 0f); // 0 degrees
        float coneAngle = 75.0f; // Half-aperture = 37.5 degrees
        float maxRange = 80.0f;

        // 1. Direct frontal hit (0 degrees, 50px distance) -> Hit
        var frontTarget = new Vector2D(150f, 100f);
        AssertTrue(SpatialCombatService.IsInCone(origin, facingRight, frontTarget, coneAngle, maxRange),
            "Target directly in facing line must hit");

        // 2. Target at 30 degrees angle (inside 37.5 degree half-angle) -> Hit
        float rad30 = 30f * (MathF.PI / 180f);
        var angledTarget = origin + new Vector2D(MathF.Cos(rad30), MathF.Sin(rad30)) * 50f;
        AssertTrue(SpatialCombatService.IsInCone(origin, facingRight, angledTarget, coneAngle, maxRange),
            "Target at 30 degrees must hit within 75 degree cone");

        // 3. Target at 60 degrees angle (outside 37.5 degree half-angle) -> Miss
        float rad60 = 60f * (MathF.PI / 180f);
        var outsideAngleTarget = origin + new Vector2D(MathF.Cos(rad60), MathF.Sin(rad60)) * 50f;
        AssertFalse(SpatialCombatService.IsInCone(origin, facingRight, outsideAngleTarget, coneAngle, maxRange),
            "Target at 60 degrees must miss 75 degree cone");

        // 4. Target behind Exile (180 degrees) -> Miss
        var behindTarget = new Vector2D(50f, 100f);
        AssertFalse(SpatialCombatService.IsInCone(origin, facingRight, behindTarget, coneAngle, maxRange),
            "Target behind origin must miss frontal cone");

        // 5. Target within angle but out of range (100px > 80px) -> Miss
        var outOfRangeTarget = new Vector2D(200f, 100f);
        AssertFalse(SpatialCombatService.IsInCone(origin, facingRight, outOfRangeTarget, coneAngle, maxRange),
            "Target beyond maxRange must miss");

        // 6. Cleave 180-degree sweep test:
        // Angle aperture = 180 degrees (half-angle = 90 degrees) -> Any target in positive X direction hits!
        float sweepAngle = 180.0f;
        var topTarget = new Vector2D(100f, 160f); // Exactly 90 degrees
        AssertTrue(SpatialCombatService.IsInCone(origin, facingRight, topTarget, sweepAngle, maxRange),
            "Cleave 180-degree sweep hits perpendicular target in front half-plane");
    }

    // ─────────────────────────────────────────────
    //  4.2.3. Multi-Vector Projectile Spread
    // ─────────────────────────────────────────────

    private static void TestMultiVectorProjectileSpread()
    {
        // Greater Multiple Projectiles (GMP): N = 5 projectiles
        int count = 5;
        float aimAngle = 0f; // Aiming right along +X
        float spreadStep = 10f * (MathF.PI / 180f); // 10 degrees step
        float speed = 600f;

        var rays = SpatialCombatService.CalculateSpread(aimAngle, count, spreadStep, speed);
        AssertEqual(5, rays.Count, 0.001f, "GMP must produce exactly 5 projectile rays");

        // Formula: theta_k = theta_aim + (k - (N-1)/2) * delta_phi
        // For N=5, (N-1)/2 = 2.
        // k=0: theta = -2 * 10 deg = -20 deg
        // k=1: theta = -1 * 10 deg = -10 deg
        // k=2: theta =  0 * 10 deg = 0 deg (center)
        // k=3: theta =  1 * 10 deg = +10 deg
        // k=4: theta =  2 * 10 deg = +20 deg
        float expectedRad0 = -20f * (MathF.PI / 180f);
        float expectedRad2 = 0f;
        float expectedRad4 = 20f * (MathF.PI / 180f);

        AssertEqual(expectedRad0, rays[0].AngleRadians, 0.001f, "Ray 0 angle must be -20 degrees");
        AssertEqual(expectedRad2, rays[2].AngleRadians, 0.001f, "Ray 2 (central) angle must be 0 degrees");
        AssertEqual(expectedRad4, rays[4].AngleRadians, 0.001f, "Ray 4 angle must be +20 degrees");

        // Check velocity magnitudes
        foreach (var ray in rays)
        {
            AssertEqual(speed, ray.Velocity.Length, 0.01f, "Each projectile velocity must match speed S");
            AssertEqual(1.0f, ray.Direction.Length, 0.001f, "Direction vector must be normalized");
        }
    }

    // ─────────────────────────────────────────────
    //  4.2.3. Chain Target Finding (R_chain <= 60px)
    // ─────────────────────────────────────────────

    private static void TestProjectileChainTargetFinding()
    {
        var hitPos = new Vector2D(200f, 100f);
        long hitMonsterId = 10;

        var mHit = new MonsterInstance(10, "HitMob", MonsterRarity.Normal, 1, 100f, DamageType.Physical, 5, 10, 1, 5, 130, 50, 10, 1, 1, initialPosition: hitPos);
        var mNear = new MonsterInstance(11, "NearMob", MonsterRarity.Normal, 1, 100f, DamageType.Physical, 5, 10, 1, 5, 130, 50, 10, 1, 1, initialPosition: new Vector2D(230f, 100f)); // 30px away (<= 60px)
        var mFar = new MonsterInstance(12, "FarMob", MonsterRarity.Normal, 1, 100f, DamageType.Physical, 5, 10, 1, 5, 130, 50, 10, 1, 1, initialPosition: new Vector2D(280f, 100f)); // 80px away (> 60px)
        var mDead = new MonsterInstance(13, "DeadMob", MonsterRarity.Normal, 1, 100f, DamageType.Physical, 5, 10, 1, 5, 130, 50, 10, 1, 1, initialPosition: new Vector2D(210f, 100f));
        mDead.TakeRawDamage(200f); // Kill it

        var allMonsters = new[] { mHit, mNear, mFar, mDead };

        // Should chain to mNear (30px away, alive, not self)
        var chainTarget = SpatialCombatService.FindChainTarget(hitPos, hitMonsterId, allMonsters, SpatialCombatService.ChainSearchRadius);
        AssertTrue(chainTarget?.InstanceId == mNear.InstanceId, "Chain must jump to closest alive candidate within 60px");

        // If mNear is already in visited set, it should not chain to it
        var visited = new HashSet<long> { mNear.InstanceId };
        var nextTarget = SpatialCombatService.FindChainTarget(hitPos, hitMonsterId, allMonsters, SpatialCombatService.ChainSearchRadius, visited);
        AssertTrue(nextTarget == null, "Must return null when no other unvisited alive monsters are within 60px");
    }

    // ─────────────────────────────────────────────
    //  4.3. Autonomous AI Target Selection & Threat Scoring
    // ─────────────────────────────────────────────

    private static void TestThreatScoringFormula()
    {
        var exilePos = new Vector2D(220f, 104f);

        // 1. Calculate Threat Score components:
        // Threat Score(M) = W_Rarity * 1000 + W_Boss * 5000 + (500 / (d + 1)) + ClusterBonus(M)
        var trash = new MonsterInstance(1, "Trash", MonsterRarity.Normal, 1, 100f, DamageType.Physical, 5, 10, 1, 5, 130, 50, 10, 1, 1, initialPosition: new Vector2D(260f, 104f)); // d = 40px
        var rare = new MonsterInstance(2, "Rare", MonsterRarity.Rare, 1, 500f, DamageType.Physical, 5, 10, 1, 5, 130, 50, 50, 1, 1, initialPosition: new Vector2D(320f, 104f)); // d = 100px
        var boss = new MonsterInstance(3, "Boss", MonsterRarity.Boss, 1, 2000f, DamageType.Physical, 5, 10, 1, 5, 130, 50, 200, 1, 1, initialPosition: new Vector2D(320f, 95f));

        var monsters = new[] { trash, rare, boss };

        float trashScore = SpatialCombatService.CalculateThreatScore(trash, exilePos, monsters);
        float rareScore = SpatialCombatService.CalculateThreatScore(rare, exilePos, monsters);
        float bossScore = SpatialCombatService.CalculateThreatScore(boss, exilePos, monsters);

        // Trash: W_Rarity = 0, W_Boss = 0. Proximity: 500 / 41 = ~12.2. Total ~12.2 (plus any cluster)
        AssertTrue(trashScore < 500f, "Trash mob threat score must be lower than Magic/Rare/Boss");

        // Rare: W_Rarity = 2 -> 2000. Total >= 2000
        AssertTrue(rareScore >= 2000f, "Rare monster threat score must include W_Rarity * 1000 = 2000");

        // Boss: W_Rarity = 3 (3000) + W_Boss = 1 (5000) -> Total >= 8000
        AssertTrue(bossScore >= 8000f, "Boss threat score must be at least 8000 (3000 rarity + 5000 boss)");

        // 2. Rare with Aura Modifiers receives extra +500 threat
        var auraMod = new MonsterAuraModifier("haste", "Haste", 0f, 25f, 0f);
        var rareWithAura = new MonsterInstance(4, "RareAura", MonsterRarity.Rare, 1, 500f, DamageType.Physical, 5, 10, 1, 5, 130, 50, 50, 1, 1,
            auraModifiers: new[] { auraMod },
            initialPosition: new Vector2D(320f, 104f));

        float rareWithAuraScore = SpatialCombatService.CalculateThreatScore(rareWithAura, exilePos, new[] { rareWithAura });
        float baseRareScore = SpatialCombatService.CalculateThreatScore(rare, exilePos, new[] { rare });
        AssertEqual(500.0f, rareWithAuraScore - baseRareScore, 1.0f, "Rare monster with Aura Modifiers must gain +500 threat bonus");

        // 3. Cluster Bonus for AoE skills:
        // Create a dense cluster of 3 trash mobs near each other
        var c1 = new MonsterInstance(20, "C1", MonsterRarity.Normal, 1, 100f, DamageType.Physical, 5, 10, 1, 5, 130, 50, 10, 1, 1, initialPosition: new Vector2D(250f, 100f));
        var c2 = new MonsterInstance(21, "C2", MonsterRarity.Normal, 1, 100f, DamageType.Physical, 5, 10, 1, 5, 130, 50, 10, 1, 1, initialPosition: new Vector2D(255f, 105f));
        var c3 = new MonsterInstance(22, "C3", MonsterRarity.Normal, 1, 100f, DamageType.Physical, 5, 10, 1, 5, 130, 50, 10, 1, 1, initialPosition: new Vector2D(252f, 98f));
        var isolated = new MonsterInstance(23, "Iso", MonsterRarity.Normal, 1, 100f, DamageType.Physical, 5, 10, 1, 5, 130, 50, 10, 1, 1, initialPosition: new Vector2D(100f, 50f));

        var clusterPacks = new[] { c1, c2, c3, isolated };

        float aoeScoreC1 = SpatialCombatService.CalculateThreatScore(c1, exilePos, clusterPacks, isAoESkill: true);
        float aoeScoreIso = SpatialCombatService.CalculateThreatScore(isolated, exilePos, clusterPacks, isAoESkill: true);

        // c1 has 2 nearby neighbors -> gains +300 cluster bonus (150 * 2)
        AssertTrue(aoeScoreC1 > aoeScoreIso + 200f, "AoE skills must heavily prioritize clustered enemies via ClusterBonus");
    }

    // ─────────────────────────────────────────────
    //  Target Stickiness & Rarity Switching
    // ─────────────────────────────────────────────

    private static void TestTargetStickinessAndSwitching()
    {
        var targeting = new TargetingStrategy();
        var exilePos = new Vector2D(220f, 104f);

        var trash1 = new MonsterInstance(1, "Trash1", MonsterRarity.Normal, 1, 100f, DamageType.Physical, 5, 10, 1, 5, 130, 50, 10, 1, 1, initialPosition: new Vector2D(250f, 104f));
        var trash2 = new MonsterInstance(2, "Trash2", MonsterRarity.Normal, 1, 100f, DamageType.Physical, 5, 10, 1, 5, 130, 50, 10, 1, 1, initialPosition: new Vector2D(245f, 104f));

        var candidates = new List<MonsterInstance> { trash1, trash2 };

        // Currently attacking trash1 -> stickiness keeps focus on trash1 even if trash2 is slightly closer
        var target = targeting.SelectTarget(candidates, exilePos, currentTarget: trash1);
        AssertTrue(target?.InstanceId == trash1.InstanceId, "Target stickiness must preserve current target on same rarity tier");

        // When a Rare monster enters -> switches to Rare!
        var rare = new MonsterInstance(3, "Rare", MonsterRarity.Rare, 1, 500f, DamageType.Physical, 5, 10, 1, 5, 130, 50, 50, 1, 1, initialPosition: new Vector2D(310f, 95f));
        candidates.Add(rare);

        target = targeting.SelectTarget(candidates, exilePos, currentTarget: trash1);
        AssertTrue(target?.InstanceId == rare.InstanceId, "Must switch target when strictly higher rarity monster enters");

        // When Boss enters -> switches to Boss!
        var boss = new MonsterInstance(4, "Boss", MonsterRarity.Boss, 1, 3000f, DamageType.Physical, 5, 10, 1, 5, 130, 50, 200, 1, 1, initialPosition: new Vector2D(320f, 95f));
        candidates.Add(boss);

        target = targeting.SelectTarget(candidates, exilePos, currentTarget: rare);
        AssertTrue(target?.InstanceId == boss.InstanceId, "Must switch target from Rare to Boss immediately");
    }

    // ─────────────────────────────────────────────
    //  Wave Spawner 2D Spatial Layout
    // ─────────────────────────────────────────────

    private static void TestPackFormationSpatialLayout()
    {
        var spawner = new WaveSpawner(new Random(42));
        var mapDef = new MapDefinition("strand", "Strand", 1, 1, false, 0, 100, "Tidal Behemoth", "Coast");
        var config = new MapRunConfig(mapDef, Array.Empty<RolledMapModifier>());

        spawner.BeginMap(config);

        // Spawn trash wave
        var wave = spawner.SpawnNextWave();
        AssertTrue(wave != null && wave.Monsters.Count > 0, "Spawner must spawn a valid wave");

        foreach (var m in wave!.Monsters)
        {
            // All monsters must have valid 2D coordinates inside the 440 x 208 arena bounds
            AssertTrue(m.Position.X >= 20f && m.Position.X <= 420f, $"Monster X ({m.Position.X}) must be within arena");
            AssertTrue(m.Position.Y >= 15f && m.Position.Y <= 195f, $"Monster Y ({m.Position.Y}) must be within arena");
            AssertTrue(m.Radius >= 8f, $"Monster Radius ({m.Radius}) must be valid");
        }
    }
}
