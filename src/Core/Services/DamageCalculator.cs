using System;
using System.Collections.Generic;
using IdleExile.Core.Models;

namespace IdleExile.Core.Services;

/// <summary>
/// Core mathematical engine for damage and ailment calculation in Idle Exile.
/// Faithful to Path of Exile's damage classification:
/// Final = Base * (1 + sum(Increased) - sum(Reduced)) * prod(1 + More) * prod(1 - Less) * CritMulti.
/// </summary>
public static class DamageCalculator
{

    /// <summary>
    /// Computes multiplier from additive (Increased/Reduced) and multiplicative (More/Less) modifiers.
    /// Multiplier = Max(0, 1 + sum(Increased) - sum(Reduced)) * prod(1 + More) * prod(1 - Less).
    /// </summary>
    public static float CalculateBucketMultiplier(
        float increasedSum,
        float reducedSum,
        IEnumerable<float>? morePercents = null,
        IEnumerable<float>? lessPercents = null)
    {
        float additive = MathF.Max(0f, 1f + (increasedSum - reducedSum) / 100f);

        float moreProduct = 1f;
        if (morePercents != null)
        {
            foreach (float more in morePercents)
            {
                moreProduct *= (1f + more / 100f);
            }
        }

        float lessProduct = 1f;
        if (lessPercents != null)
        {
            foreach (float less in lessPercents)
            {
                lessProduct *= MathF.Max(0f, 1f - less / 100f);
            }
        }

        return additive * moreProduct * lessProduct;
    }

    /// <summary>
    /// Computes the effective multiplier for a specific damage type, combining global and type-specific buckets.
    /// </summary>
    public static float GetDamageMultiplier(DamageType type, DamageModifierContext context)
    {
        var typeBucket = context.GetTypeBucket(type);
        float increased = context.Global.IncreasedSum + typeBucket.IncreasedSum;
        float reduced = context.Global.ReducedSum + typeBucket.ReducedSum;
        float moreProduct = context.Global.MoreProduct * typeBucket.MoreProduct;
        float lessProduct = context.Global.LessProduct * typeBucket.LessProduct;

        float additive = MathF.Max(0f, 1f + (increased - reduced) / 100f);
        return additive * moreProduct * lessProduct;
    }

    /// <summary>
    /// Computes combined multiplier for damage converting or gained from sourceType to targetType.
    /// In PoE, converted damage benefits from modifiers of both the original source and the converted destination.
    /// </summary>
    public static float GetConversionMultiplier(DamageType sourceType, DamageType targetType, DamageModifierContext context)
    {
        if (sourceType == targetType)
            return GetDamageMultiplier(sourceType, context);

        var sourceBucket = context.GetTypeBucket(sourceType);
        var targetBucket = context.GetTypeBucket(targetType);

        float increased = context.Global.IncreasedSum + sourceBucket.IncreasedSum + targetBucket.IncreasedSum;
        float reduced = context.Global.ReducedSum + sourceBucket.ReducedSum + targetBucket.ReducedSum;
        float moreProduct = context.Global.MoreProduct * sourceBucket.MoreProduct * targetBucket.MoreProduct;
        float lessProduct = context.Global.LessProduct * sourceBucket.LessProduct * targetBucket.LessProduct;

        float additive = MathF.Max(0f, 1f + (increased - reduced) / 100f);
        return additive * moreProduct * lessProduct;
    }

    /// <summary>
    /// Transforms base damage ranges through flat additions, conversions, gained as extra,
    /// and type-specific Increased/More multipliers into a final DamagePool.
    /// </summary>
    public static DamagePool CalculateModifiedPool(DamagePool baseDamage, DamageModifierContext context)
    {
        // 1. Initial pool = base damage + flat added damage
        var initial = new DamagePool();
        for (int i = 0; i < 5; i++)
        {
            var type = (DamageType)i;
            initial[type] = baseDamage[type] + context.AddedFlatDamage[type];
        }

        var result = new DamagePool();

        // 2. Process conversions and gained as extra for each source damage type
        for (int s = 0; s < 5; s++)
        {
            var sourceType = (DamageType)s;
            var sourceRange = initial[sourceType];
            if (sourceRange.IsZero)
                continue;

            // Calculate total conversion percent from this source
            float totalConvertPercent = 0f;
            for (int d = 0; d < 5; d++)
            {
                if (s != d)
                    totalConvertPercent += context.GetConversion(sourceType, (DamageType)d);
            }

            // Normalization if total conversion exceeds 100%
            float convertScale = totalConvertPercent > 100f ? (100f / totalConvertPercent) : 1.0f;
            float actualConvertTotal = MathF.Min(100f, totalConvertPercent);
            float unconvertedPercent = MathF.Max(0f, 100f - actualConvertTotal);

            // 2a. Add unconverted portion of source type
            if (unconvertedPercent > 0f)
            {
                float mult = GetDamageMultiplier(sourceType, context);
                var unconvertedRange = sourceRange * (unconvertedPercent / 100f) * mult;
                result.Add(sourceType, unconvertedRange);
            }

            // 2b. Add converted portions to destinations
            for (int d = 0; d < 5; d++)
            {
                if (s == d) continue;
                var destType = (DamageType)d;
                float rawConvert = context.GetConversion(sourceType, destType);
                if (rawConvert > 0f)
                {
                    float effectiveConvert = (rawConvert * convertScale) / 100f;
                    float mult = GetConversionMultiplier(sourceType, destType, context);
                    var convertedRange = sourceRange * effectiveConvert * mult;
                    result.Add(destType, convertedRange);
                }
            }

            // 2c. Add "Gain As Extra" portions (calculated from unconverted source base)
            for (int d = 0; d < 5; d++)
            {
                var destType = (DamageType)d;
                float extraPercent = context.GetGainAsExtra(sourceType, destType);
                if (extraPercent > 0f)
                {
                    float mult = GetConversionMultiplier(sourceType, destType, context);
                    var extraRange = sourceRange * (extraPercent / 100f) * mult;
                    result.Add(destType, extraRange);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Computes final critical strike chance clamped between [0.0%, 100.0%].
    /// </summary>
    public static float CalculateEffectiveCritChance(DamageModifierContext context)
    {
        if (context.CannotCrit)
            return 0f;

        float baseChance = context.BaseCritChance + context.FlatCritChance;
        if (baseChance <= 0f)
            return 0f;

        float multiplier = context.CritChanceBucket.CalculateMultiplier();
        float effective = baseChance * multiplier;

        return Math.Clamp(effective, 0f, 100f);
    }

    /// <summary>
    /// Computes effective critical strike multiplier (e.g. 150% + 50% = 200%).
    /// </summary>
    public static float CalculateEffectiveCritMultiplier(DamageModifierContext context)
    {
        return MathF.Max(100f, context.BaseCritMultiplier + context.AdditionalCritMultiplier);
    }

    /// <summary>
    /// Rolls and evaluates a single combat hit, including damage rolls, critical strike check,
    /// and status ailments application.
    /// </summary>
    public static HitResult RollHit(
        DamagePool baseDamage,
        DamageModifierContext context,
        float? targetMaxLife = null,
        Random? random = null,
        List<AilmentInstance>? ailmentsBuffer = null)
    {
        var rng = random ?? Random.Shared;

        // 1. Calculate modified damage ranges
        var modifiedPool = CalculateModifiedPool(baseDamage, context);

        // 2. Roll random damage values within range for each type
        var rolledDamage = new HitDamageBreakdown(
            modifiedPool.Physical.Roll(rng),
            modifiedPool.Fire.Roll(rng),
            modifiedPool.Cold.Roll(rng),
            modifiedPool.Lightning.Roll(rng),
            modifiedPool.Chaos.Roll(rng)
        );

        // 3. Roll Critical Strike check
        float critChance = CalculateEffectiveCritChance(context);
        float critMultiplier = CalculateEffectiveCritMultiplier(context);
        bool isCrit = critChance > 0f && (rng.NextDouble() * 100.0 < critChance);

        // 4. Apply crit multiplier if triggered
        HitDamageBreakdown finalDamage;
        if (isCrit)
        {
            float critFactor = critMultiplier / 100f;
            finalDamage = rolledDamage.Multiply(critFactor);
        }
        else
        {
            finalDamage = rolledDamage;
        }

        // 5. Evaluate status ailments
        float effectiveTargetLife = targetMaxLife.HasValue && targetMaxLife.Value > 0f
            ? targetMaxLife.Value
            : 1000f; // Standard default baseline if unknown

        List<AilmentInstance> ailments;
        if (ailmentsBuffer != null)
        {
            EvaluateAilments(finalDamage, isCrit, context, effectiveTargetLife, rng, ailmentsBuffer);
            ailments = ailmentsBuffer;
        }
        else
        {
            ailments = EvaluateAilments(finalDamage, isCrit, context, effectiveTargetLife, rng);
        }

        return new HitResult(
            Damage: finalDamage,
            TotalDamage: finalDamage.Total,
            IsCrit: isCrit,
            CritMultiplier: critMultiplier,
            InflictedAilments: ailments,
            UnmitigatedDamage: finalDamage.Clone()
        );
    }

    /// <summary>
    /// Evaluates all ailments (Ignite, Chill, Freeze, Shock, Bleed, Poison) inflicted by the hit.
    /// Allocates and returns a new list of ailments.
    /// </summary>
    public static List<AilmentInstance> EvaluateAilments(
        HitDamageBreakdown damage,
        bool isCrit,
        DamageModifierContext context,
        float targetMaxLife,
        Random rng)
    {
        var ailments = new List<AilmentInstance>();
        EvaluateAilments(damage, isCrit, context, targetMaxLife, rng, ailments);
        return ailments;
    }

    /// <summary>
    /// Evaluates all ailments (Ignite, Chill, Freeze, Shock, Bleed, Poison) inflicted by the hit,
    /// populating the provided list to avoid GC allocations in the 30Hz combat loop.
    /// </summary>
    public static void EvaluateAilments(
        HitDamageBreakdown damage,
        bool isCrit,
        DamageModifierContext context,
        float targetMaxLife,
        Random rng,
        List<AilmentInstance> results)
    {
        results.Clear();
        float dotMultiplier = context.DamageOverTimeBucket.CalculateMultiplier();

        // 1. Ignite (Fire DoT)
        // Crits with Fire damage inherently have 100% chance to Ignite
        if (damage.Fire > 0f && (isCrit || rng.NextDouble() * 100.0 < context.ChanceToIgnite))
        {
            float igniteDps = damage.Fire * AilmentConstants.IgniteBaseDamageRatio * dotMultiplier;
            float duration = AilmentConstants.IgniteBaseDuration * context.IgniteDurationMultiplier;
            results.Add(new AilmentInstance(
                Type: AilmentType.Ignite,
                SourceDamageType: DamageType.Fire,
                DamagePerSecond: igniteDps,
                EffectPercent: 0f,
                DurationSeconds: duration,
                IsStacking: false
            ));
        }

        // 2. Chill (Cold Slow)
        // Crits with Cold inherently have 100% chance to Chill
        if (damage.Cold > 0f && (isCrit || rng.NextDouble() * 100.0 < context.ChanceToChill))
        {
            float ratio = damage.Cold / targetMaxLife;
            float slowPercent = 50f * MathF.Pow(ratio, 0.4f) * context.ChillEffectMultiplier;

            if (slowPercent >= AilmentConstants.ChillMinThreshold)
            {
                slowPercent = MathF.Min(slowPercent, AilmentConstants.ChillMaxCap);
                results.Add(new AilmentInstance(
                    Type: AilmentType.Chill,
                    SourceDamageType: DamageType.Cold,
                    DamagePerSecond: 0f,
                    EffectPercent: slowPercent,
                    DurationSeconds: AilmentConstants.ChillBaseDuration,
                    IsStacking: false
                ));
            }
        }

        // 3. Freeze (Cold Incapacitate)
        // Crits with Cold inherently have 100% chance to Freeze
        if (damage.Cold > 0f && (isCrit || rng.NextDouble() * 100.0 < context.ChanceToFreeze))
        {
            float ratio = damage.Cold / targetMaxLife;
            float duration = 6.0f * ratio; // 60ms per 1% life = 6.0s * (damage/life)

            if (duration >= AilmentConstants.FreezeMinDuration)
            {
                duration = MathF.Min(duration, AilmentConstants.FreezeMaxDuration);
                results.Add(new AilmentInstance(
                    Type: AilmentType.Freeze,
                    SourceDamageType: DamageType.Cold,
                    DamagePerSecond: 0f,
                    EffectPercent: 100f,
                    DurationSeconds: duration,
                    IsStacking: false,
                    PreventsAction: true
                ));
            }
        }

        // 4. Shock (Lightning +% Damage Taken)
        // Crits with Lightning inherently have 100% chance to Shock
        if (damage.Lightning > 0f && (isCrit || rng.NextDouble() * 100.0 < context.ChanceToShock))
        {
            float ratio = damage.Lightning / targetMaxLife;
            float shockPercent = 100f * MathF.Pow(ratio, 0.4f) * context.ShockEffectMultiplier;

            if (shockPercent >= AilmentConstants.ShockMinThreshold)
            {
                shockPercent = MathF.Min(shockPercent, AilmentConstants.ShockMaxCap);
                results.Add(new AilmentInstance(
                    Type: AilmentType.Shock,
                    SourceDamageType: DamageType.Lightning,
                    DamagePerSecond: 0f,
                    EffectPercent: shockPercent,
                    DurationSeconds: AilmentConstants.ShockBaseDuration,
                    IsStacking: false
                ));
            }
        }

        // 5. Bleed (Physical DoT, Attacks only)
        // In PoE, crits do NOT automatically bleed without 100% chance or modifier
        if (context.IsAttack && damage.Physical > 0f && (rng.NextDouble() * 100.0 < context.ChanceToBleed))
        {
            float bleedRatio = context.BleedTargetIsMoving
                ? AilmentConstants.BleedMovingDamageRatio
                : AilmentConstants.BleedBaseDamageRatio;

            float bleedDps = damage.Physical * bleedRatio * dotMultiplier;
            float duration = AilmentConstants.BleedBaseDuration * context.BleedDurationMultiplier;

            results.Add(new AilmentInstance(
                Type: AilmentType.Bleed,
                SourceDamageType: DamageType.Physical,
                DamagePerSecond: bleedDps,
                EffectPercent: 0f,
                DurationSeconds: duration,
                IsStacking: false
            ));
        }

        // 6. Poison (Chaos DoT, Stacking, from Physical & Chaos)
        if ((damage.Physical > 0f || damage.Chaos > 0f) && (rng.NextDouble() * 100.0 < context.ChanceToPoison))
        {
            float poisonBaseDamage = damage.Physical + damage.Chaos;
            float poisonDps = poisonBaseDamage * AilmentConstants.PoisonBaseDamageRatio * dotMultiplier;
            float duration = AilmentConstants.PoisonBaseDuration * context.PoisonDurationMultiplier;

            results.Add(new AilmentInstance(
                Type: AilmentType.Poison,
                SourceDamageType: DamageType.Chaos,
                DamagePerSecond: poisonDps,
                EffectPercent: 0f,
                DurationSeconds: duration,
                IsStacking: true
            ));
        }
    }

    /// <summary>
    /// Deterministically calculates average hit damage (for tooltips, offline estimation, character sheet).
    /// </summary>
    public static HitResult CalculateAverageHit(
        DamagePool baseDamage,
        DamageModifierContext context,
        float? targetMaxLife = null)
    {
        var pool = CalculateModifiedPool(baseDamage, context);

        float critChance = CalculateEffectiveCritChance(context);
        float critMulti = CalculateEffectiveCritMultiplier(context);

        // Expected damage factor = (1 - CritChance) * 1 + CritChance * (CritMulti / 100)
        float critFactor = (1f - critChance / 100f) + (critChance / 100f) * (critMulti / 100f);

        var averageDamage = new HitDamageBreakdown(
            pool.Physical.Average * critFactor,
            pool.Fire.Average * critFactor,
            pool.Cold.Average * critFactor,
            pool.Lightning.Average * critFactor,
            pool.Chaos.Average * critFactor
        );

        float effectiveTargetLife = targetMaxLife.HasValue && targetMaxLife.Value > 0f
            ? targetMaxLife.Value
            : 1000f;

        // For average ailments, deterministic evaluation with rng that rolls 50
        var ailments = EvaluateAilments(averageDamage, isCrit: critChance >= 100f, context, effectiveTargetLife, new Random(42));

        return new HitResult(
            Damage: averageDamage,
            TotalDamage: averageDamage.Total,
            IsCrit: false,
            CritMultiplier: critMulti,
            InflictedAilments: ailments,
            UnmitigatedDamage: averageDamage.Clone()
        );
    }

    // ─────────────────────────────────────────────
    //  Mitigation Convenience Methods
    //  Delegates to DefensiveLayers as Single Source of Truth.
    // ─────────────────────────────────────────────

    /// <summary>
    /// Applies Path of Exile's non-linear Armour reduction formula.
    /// Delegates to <see cref="DefensiveLayers.ApplyArmourMitigation"/>.
    /// </summary>
    public static float ApplyArmourReduction(float physicalDamage, float armour) =>
        DefensiveLayers.ApplyArmourMitigation(physicalDamage, armour);

    /// <summary>
    /// Applies elemental or chaos resistance to damage.
    /// Delegates to <see cref="DefensiveLayers.ApplyResistanceMitigation"/>.
    /// </summary>
    public static float ApplyResistance(float damage, float resistancePercent) =>
        DefensiveLayers.ApplyResistanceMitigation(damage, resistancePercent);

    /// <summary>
    /// Applies damage taken multiplier, including Shock (+% Damage Taken)
    /// and any other increased/reduced damage taken.
    /// </summary>
    public static float ApplyDamageTaken(float damage, float shockPercent, float increasedDamageTaken = 0f)
    {
        if (damage <= 0f) return 0f;

        float multiplier = MathF.Max(0f, 1f + (shockPercent + increasedDamageTaken) / 100f);
        return damage * multiplier;
    }
}
