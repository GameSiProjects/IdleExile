using System;
using System.Collections.Generic;
using IdleExile.Core.Models;
using IdleExile.Core.Services;

namespace IdleExile.Core.Tests;

/// <summary>
/// Unit tests for DamageCalculator service and supporting models.
/// Validates PoE mathematical accuracy according to GDD specifications.
/// </summary>
public static class DamageCalculatorTests
{
    public static void RunAll()
    {
        Console.WriteLine("[Tests] Running DamageCalculatorTests...");

        TestIncreasedVsMoreArithmetic();
        TestAllFiveDamageTypes();
        TestDamageConversionAndGainAsExtra();
        TestCriticalStrikeChanceAndMultiplier();
        TestIgniteAilment();
        TestChillAilment();
        TestFreezeAilment();
        TestShockAilment();
        TestBleedAilment();
        TestPoisonAilment();
        TestArmourReductionFormula();
        TestResistanceFormulas();
        TestShockDamageTakenAmplification();

        Console.WriteLine("[Tests] All DamageCalculatorTests PASSED successfully!");
    }

    private static void AssertEqual(float expected, float actual, float tolerance = 0.001f, string message = "")
    {
        if (MathF.Abs(expected - actual) > tolerance)
        {
            throw new Exception($"Assertion failed: Expected {expected}, got {actual}. {message}");
        }
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception($"Assertion failed: {message}");
        }
    }

    private static void TestIncreasedVsMoreArithmetic()
    {
        // 1. GDD Example: Two support gems: 30% More and 40% More = 1.30 * 1.40 = 1.82 (+82%)
        float multGdd = DamageCalculator.CalculateBucketMultiplier(
            increasedSum: 0f,
            reducedSum: 0f,
            morePercents: [30f, 40f]
        );
        AssertEqual(1.82f, multGdd, 0.001f, "GDD 30% more and 40% more should yield 1.82 multiplier");

        // 2. Additive: 50% increased and 50% increased = 2.0x
        float multAdd = DamageCalculator.CalculateBucketMultiplier(
            increasedSum: 100f,
            reducedSum: 0f
        );
        AssertEqual(2.0f, multAdd, 0.001f, "100% increased should double base damage");

        // 3. Combined: Base * (1 + 50% inc - 10% red) * (1 + 30% more) * (1 - 10% less)
        // = 1.40 * 1.30 * 0.90 = 1.638
        float multCombined = DamageCalculator.CalculateBucketMultiplier(
            increasedSum: 50f,
            reducedSum: 10f,
            morePercents: [30f],
            lessPercents: [10f]
        );
        AssertEqual(1.638f, multCombined, 0.001f, "Combined formula should equal 1.638");

        // 4. Overwhelming Reduced: sum(Reduced) > 1 + sum(Increased) clamps to 0
        float multNegative = DamageCalculator.CalculateBucketMultiplier(
            increasedSum: 20f,
            reducedSum: 200f
        );
        AssertEqual(0f, multNegative, 0.001f, "Multiplier cannot drop below 0");
    }

    private static void TestAllFiveDamageTypes()
    {
        var basePool = new DamagePool(
            physical: new DamageRange(10f, 10f),
            fire: new DamageRange(20f, 20f),
            cold: new DamageRange(30f, 30f),
            lightning: new DamageRange(40f, 40f),
            chaos: new DamageRange(50f, 50f)
        );

        var context = new DamageModifierContext();
        // Give 50% increased physical
        context.AddIncreased(DamageType.Physical, 50f);
        // Give 100% increased fire
        context.AddIncreased(DamageType.Fire, 100f);
        // Give 20% more cold
        context.AddMore(DamageType.Cold, 20f);
        // Give 50% less lightning
        context.AddLess(DamageType.Lightning, 50f);
        // Give 10% global more
        context.AddGlobalMore(10f);

        var modified = DamageCalculator.CalculateModifiedPool(basePool, context);

        // Physical: 10 * (1 + 0.5) * 1.1 = 16.5
        AssertEqual(16.5f, modified.Physical.Min, 0.01f, "Physical calculation");

        // Fire: 20 * (1 + 1.0) * 1.1 = 44.0
        AssertEqual(44.0f, modified.Fire.Min, 0.01f, "Fire calculation");

        // Cold: 30 * (1.0) * 1.2 * 1.1 = 39.6
        AssertEqual(39.6f, modified.Cold.Min, 0.01f, "Cold calculation");

        // Lightning: 40 * 1.0 * (1 - 0.5) * 1.1 = 22.0
        AssertEqual(22.0f, modified.Lightning.Min, 0.01f, "Lightning calculation");

        // Chaos: 50 * 1.0 * 1.1 = 55.0
        AssertEqual(55.0f, modified.Chaos.Min, 0.01f, "Chaos calculation");
    }

    private static void TestDamageConversionAndGainAsExtra()
    {
        // 100 Physical Damage base
        var basePool = new DamagePool(physical: new DamageRange(100f, 100f));
        var context = new DamageModifierContext();

        // 35% Added Physical as Extra Fire (like Added Fire Damage Support in gems.json)
        context.AddGainAsExtra(DamageType.Physical, DamageType.Fire, 35f);

        // 50% Physical converted to Fire (like Avatar of Fire in passives)
        context.AddConversion(DamageType.Physical, DamageType.Fire, 50f);

        var modified = DamageCalculator.CalculateModifiedPool(basePool, context);

        // Physical remaining unconverted: 50% of 100 = 50
        AssertEqual(50f, modified.Physical.Min, 0.01f, "Unconverted Physical");

        // Fire: 50 from conversion + 35 from extra = 85
        AssertEqual(85f, modified.Fire.Min, 0.01f, "Fire from conversion and extra");
    }

    private static void TestCriticalStrikeChanceAndMultiplier()
    {
        var context = new DamageModifierContext
        {
            BaseCritChance = 5.0f, // 5% base
            FlatCritChance = 1.5f  // +1.5% flat -> 6.5% base
        };
        // +100% Increased Critical Strike Chance -> 6.5 * 2.0 = 13.0%
        context.CritChanceBucket.AddIncreased(100f);

        float effectiveChance = DamageCalculator.CalculateEffectiveCritChance(context);
        AssertEqual(13.0f, effectiveChance, 0.01f, "Crit chance should be 13.0%");

        // Test cannot crit (Resolute Technique)
        context.CannotCrit = true;
        AssertEqual(0.0f, DamageCalculator.CalculateEffectiveCritChance(context), 0.001f, "CannotCrit should yield 0%");
        context.CannotCrit = false;

        // Test Crit Multiplier (Base 150% + 50% = 200%)
        context.AdditionalCritMultiplier = 50.0f;
        float effectiveMulti = DamageCalculator.CalculateEffectiveCritMultiplier(context);
        AssertEqual(200.0f, effectiveMulti, 0.01f, "Crit multiplier should be 200%");
    }

    private static void TestIgniteAilment()
    {
        var rng = new Random(1234);
        var damage = new HitDamageBreakdown(physical: 0f, fire: 100f);
        var context = new DamageModifierContext
        {
            ChanceToIgnite = 100f
        };

        var ailments = DamageCalculator.EvaluateAilments(damage, isCrit: false, context, targetMaxLife: 1000f, rng);
        AssertTrue(ailments.Exists(a => a.Type == AilmentType.Ignite), "Should inflict Ignite");

        var ignite = ailments.Find(a => a.Type == AilmentType.Ignite)!;
        // 90% of 100 Fire = 90 DPS, 4.0s duration
        AssertEqual(90f, ignite.DamagePerSecond, 0.01f, "Ignite DPS");
        AssertEqual(4.0f, ignite.DurationSeconds, 0.01f, "Ignite duration");
        AssertTrue(!ignite.IsStacking, "Ignite is non-stacking");
    }

    private static void TestChillAilment()
    {
        var rng = new Random(1234);
        // Cold damage = 100 vs 1000 life (10% ratio)
        // Slow = 50 * (0.1)^0.4 = 50 * 0.3981 = 19.9%
        var damage = new HitDamageBreakdown(physical: 0f, fire: 0f, cold: 100f);
        var context = new DamageModifierContext { ChanceToChill = 100f };

        var ailments = DamageCalculator.EvaluateAilments(damage, isCrit: false, context, targetMaxLife: 1000f, rng);
        AssertTrue(ailments.Exists(a => a.Type == AilmentType.Chill), "Should inflict Chill");

        var chill = ailments.Find(a => a.Type == AilmentType.Chill)!;
        AssertEqual(19.9f, chill.EffectPercent, 0.2f, "Chill slow percent");
        AssertEqual(2.0f, chill.DurationSeconds, 0.01f, "Chill duration");
    }

    private static void TestFreezeAilment()
    {
        var rng = new Random(1234);
        // Cold damage = 100 vs 1000 life (ratio = 0.1)
        // Duration = 6.0 * 0.1 = 0.6 seconds (above 0.3s min threshold)
        var damage = new HitDamageBreakdown(physical: 0f, fire: 0f, cold: 100f);
        var context = new DamageModifierContext { ChanceToFreeze = 100f };

        var ailments = DamageCalculator.EvaluateAilments(damage, isCrit: false, context, targetMaxLife: 1000f, rng);
        AssertTrue(ailments.Exists(a => a.Type == AilmentType.Freeze), "Should inflict Freeze");

        var freeze = ailments.Find(a => a.Type == AilmentType.Freeze)!;
        AssertEqual(0.6f, freeze.DurationSeconds, 0.01f, "Freeze duration");
        AssertTrue(freeze.PreventsAction, "Freeze prevents action");

        // Tiny damage (1 Cold vs 1000 life = 0.006s duration < 0.3s threshold) -> should NOT freeze
        var tinyDamage = new HitDamageBreakdown(physical: 0f, fire: 0f, cold: 1f);
        var tinyAilments = DamageCalculator.EvaluateAilments(tinyDamage, isCrit: false, context, targetMaxLife: 1000f, rng);
        AssertTrue(!tinyAilments.Exists(a => a.Type == AilmentType.Freeze), "Tiny cold hit should not freeze");
    }

    private static void TestShockAilment()
    {
        var rng = new Random(1234);
        // Lightning = 100 vs 1000 life (ratio = 0.1)
        // Shock = 100 * (0.1)^0.4 = 100 * 0.3981 = 39.81% increased damage taken (capped at 50%)
        var damage = new HitDamageBreakdown(physical: 0f, fire: 0f, cold: 0f, lightning: 100f);
        var context = new DamageModifierContext { ChanceToShock = 100f };

        var ailments = DamageCalculator.EvaluateAilments(damage, isCrit: false, context, targetMaxLife: 1000f, rng);
        AssertTrue(ailments.Exists(a => a.Type == AilmentType.Shock), "Should inflict Shock");

        var shock = ailments.Find(a => a.Type == AilmentType.Shock)!;
        AssertEqual(39.81f, shock.EffectPercent, 0.2f, "Shock magnitude");
        AssertEqual(2.0f, shock.DurationSeconds, 0.01f, "Shock duration");
    }

    private static void TestBleedAilment()
    {
        var rng = new Random(1234);
        var damage = new HitDamageBreakdown(physical: 200f);
        var context = new DamageModifierContext
        {
            IsAttack = true,
            ChanceToBleed = 100f,
            BleedTargetIsMoving = false
        };

        var ailments = DamageCalculator.EvaluateAilments(damage, isCrit: false, context, targetMaxLife: 1000f, rng);
        AssertTrue(ailments.Exists(a => a.Type == AilmentType.Bleed), "Should inflict Bleed");

        var bleed = ailments.Find(a => a.Type == AilmentType.Bleed)!;
        // 70% of 200 = 140 DPS, 5.0s duration
        AssertEqual(140f, bleed.DamagePerSecond, 0.01f, "Bleed DPS stationary");
        AssertEqual(5.0f, bleed.DurationSeconds, 0.01f, "Bleed duration");

        // Test moving bleed: 210% of 200 = 420 DPS
        context.BleedTargetIsMoving = true;
        var movingAilments = DamageCalculator.EvaluateAilments(damage, isCrit: false, context, targetMaxLife: 1000f, rng);
        var movingBleed = movingAilments.Find(a => a.Type == AilmentType.Bleed)!;
        AssertEqual(420f, movingBleed.DamagePerSecond, 0.01f, "Bleed DPS moving");
    }

    private static void TestPoisonAilment()
    {
        var rng = new Random(1234);
        var damage = new HitDamageBreakdown(physical: 100f, fire: 0f, cold: 0f, lightning: 0f, chaos: 50f);
        var context = new DamageModifierContext
        {
            ChanceToPoison = 100f
        };

        var ailments = DamageCalculator.EvaluateAilments(damage, isCrit: false, context, targetMaxLife: 1000f, rng);
        AssertTrue(ailments.Exists(a => a.Type == AilmentType.Poison), "Should inflict Poison");

        var poison = ailments.Find(a => a.Type == AilmentType.Poison)!;
        // Combined base: 100 (Phys) + 50 (Chaos) = 150. 30% of 150 = 45 DPS, 2.0s duration
        AssertEqual(45f, poison.DamagePerSecond, 0.01f, "Poison DPS");
        AssertEqual(2.0f, poison.DurationSeconds, 0.01f, "Poison duration");
        AssertTrue(poison.IsStacking, "Poison must be stacking");
    }

    private static void TestArmourReductionFormula()
    {
        // GDD Formula: % Reduction = Armour / (Armour + 5 * Damage)
        // Hit = 100, Armour = 1000:
        // Reduction = 1000 / (1000 + 500) = 1000 / 1500 = 66.67%
        // Final Damage = 100 * (1 - 0.6667) = 33.33
        float mitigatedSmall = DamageCalculator.ApplyArmourReduction(100f, 1000f);
        AssertEqual(33.333f, mitigatedSmall, 0.1f, "Small hit armour reduction");

        // Huge Hit = 2000, Armour = 1000:
        // Reduction = 1000 / (1000 + 10000) = 1000 / 11000 = 9.09%
        // Final Damage = 2000 * (1 - 0.0909) = 1818.18
        float mitigatedBig = DamageCalculator.ApplyArmourReduction(2000f, 1000f);
        AssertEqual(1818.18f, mitigatedBig, 1.0f, "Large hit armour reduction drop-off");
    }

    private static void TestResistanceFormulas()
    {
        // 100 Fire Damage against 75% resistance = 25 Damage
        float dmg75 = DamageCalculator.ApplyResistance(100f, 75f);
        AssertEqual(25f, dmg75, 0.01f, "75% resistance damage");

        // 100 Fire Damage against -20% resistance = 120 Damage
        float dmgMinus20 = DamageCalculator.ApplyResistance(100f, -20f);
        AssertEqual(120f, dmgMinus20, 0.01f, "-20% resistance damage");

        // 100 Fire Damage against 90% max resistance cap = 10 Damage
        float dmg90 = DamageCalculator.ApplyResistance(100f, 90f);
        AssertEqual(10f, dmg90, 0.01f, "90% resistance damage");
    }

    private static void TestShockDamageTakenAmplification()
    {
        // 100 Damage against 30% Shock = 100 * 1.30 = 130
        float shocked = DamageCalculator.ApplyDamageTaken(100f, shockPercent: 30f);
        AssertEqual(130f, shocked, 0.01f, "30% shock damage amplification");

        // 100 Damage against 50% Shock + 10% Increased Damage Taken = 160
        float shockedExtra = DamageCalculator.ApplyDamageTaken(100f, shockPercent: 50f, increasedDamageTaken: 10f);
        AssertEqual(160f, shockedExtra, 0.01f, "Shock + extra inc dmg taken");
    }
}
