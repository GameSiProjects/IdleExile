using System;

namespace IdleExile.Core.Models;

/// <summary>
/// Aggregates all damage modifiers, added damages, conversions, critical strike stats,
/// and ailment chances for a damage evaluation.
/// </summary>
public sealed class DamageModifierContext
{
    /// <summary>
    /// Global modifier bucket that affects all damage types and ailments.
    /// </summary>
    public StatModifierBucket Global { get; } = new();

    /// <summary>
    /// Per-damage-type modifier buckets (Physical, Fire, Cold, Lightning, Chaos).
    /// </summary>
    private readonly StatModifierBucket[] _typeBuckets = new StatModifierBucket[5];

    /// <summary>
    /// Flat damage added directly to base damage before conversions and multipliers.
    /// </summary>
    public DamagePool AddedFlatDamage { get; } = new();

    /// <summary>
    /// Percentage of damage gained as an extra damage type [from, to].
    /// E.g. [Physical, Fire] = 35 means "Gain 35% of Physical Damage as Extra Fire Damage".
    /// </summary>
    private readonly float[,] _gainExtraPercent = new float[5, 5];

    /// <summary>
    /// Percentage of damage converted to another type [from, to].
    /// E.g. [Physical, Fire] = 50 means "50% of Physical Damage Converted to Fire Damage".
    /// </summary>
    private readonly float[,] _convertPercent = new float[5, 5];

    // --- Critical Strike Configuration ---
    public float BaseCritChance { get; set; } = 5.0f; // Default 5.0%
    public float FlatCritChance { get; set; } = 0.0f;
    public StatModifierBucket CritChanceBucket { get; } = new();
    public float BaseCritMultiplier { get; set; } = 150.0f; // PoE default 150.0%
    public float AdditionalCritMultiplier { get; set; } = 0.0f; // e.g. +50.0% -> 200.0%
    public bool CannotCrit { get; set; } = false; // E.g. Resolute Technique

    // --- Ailment Configuration ---
    public float ChanceToIgnite { get; set; } = 0.0f;
    public float ChanceToChill { get; set; } = 0.0f;
    public float ChanceToFreeze { get; set; } = 0.0f;
    public float ChanceToShock { get; set; } = 0.0f;
    public float ChanceToBleed { get; set; } = 0.0f;
    public float ChanceToPoison { get; set; } = 0.0f;
    public bool IsAttack { get; set; } = false; // Bleed requires attack damage
    public bool BleedTargetIsMoving { get; set; } = false;

    // Ailment scalers
    public StatModifierBucket DamageOverTimeBucket { get; } = new();
    public float IgniteDurationMultiplier { get; set; } = 1.0f;
    public float BleedDurationMultiplier { get; set; } = 1.0f;
    public float PoisonDurationMultiplier { get; set; } = 1.0f;
    public float ShockEffectMultiplier { get; set; } = 1.0f;
    public float ChillEffectMultiplier { get; set; } = 1.0f;

    public DamageModifierContext()
    {
        for (int i = 0; i < 5; i++)
        {
            _typeBuckets[i] = new StatModifierBucket();
        }
    }

    public StatModifierBucket GetTypeBucket(DamageType type) => _typeBuckets[(int)type];

    public void AddIncreased(DamageType type, float percent) => _typeBuckets[(int)type].AddIncreased(percent);
    public void AddReduced(DamageType type, float percent) => _typeBuckets[(int)type].AddReduced(percent);
    public void AddMore(DamageType type, float percent) => _typeBuckets[(int)type].AddMore(percent);
    public void AddLess(DamageType type, float percent) => _typeBuckets[(int)type].AddLess(percent);

    public void AddGlobalIncreased(float percent) => Global.AddIncreased(percent);
    public void AddGlobalReduced(float percent) => Global.AddReduced(percent);
    public void AddGlobalMore(float percent) => Global.AddMore(percent);
    public void AddGlobalLess(float percent) => Global.AddLess(percent);

    public void AddGainAsExtra(DamageType from, DamageType to, float percent)
    {
        _gainExtraPercent[(int)from, (int)to] += percent;
    }

    public float GetGainAsExtra(DamageType from, DamageType to) =>
        _gainExtraPercent[(int)from, (int)to];

    public void AddConversion(DamageType from, DamageType to, float percent)
    {
        _convertPercent[(int)from, (int)to] += percent;
    }

    public float GetConversion(DamageType from, DamageType to) =>
        _convertPercent[(int)from, (int)to];

    /// <summary>
    /// Resets all modifier state for reuse (object pool pattern).
    /// Avoids GC pressure from allocating new contexts every hit in the 30Hz combat loop.
    /// </summary>
    public void Reset()
    {
        Global.Reset();
        for (int i = 0; i < 5; i++)
        {
            _typeBuckets[i].Reset();
        }
        AddedFlatDamage.Physical = default;
        AddedFlatDamage.Fire = default;
        AddedFlatDamage.Cold = default;
        AddedFlatDamage.Lightning = default;
        AddedFlatDamage.Chaos = default;
        Array.Clear(_gainExtraPercent);
        Array.Clear(_convertPercent);

        BaseCritChance = 5.0f;
        FlatCritChance = 0.0f;
        CritChanceBucket.Reset();
        BaseCritMultiplier = 150.0f;
        AdditionalCritMultiplier = 0.0f;
        CannotCrit = false;

        ChanceToIgnite = 0.0f;
        ChanceToChill = 0.0f;
        ChanceToFreeze = 0.0f;
        ChanceToShock = 0.0f;
        ChanceToBleed = 0.0f;
        ChanceToPoison = 0.0f;
        IsAttack = false;
        BleedTargetIsMoving = false;

        DamageOverTimeBucket.Reset();
        IgniteDurationMultiplier = 1.0f;
        BleedDurationMultiplier = 1.0f;
        PoisonDurationMultiplier = 1.0f;
        ShockEffectMultiplier = 1.0f;
        ChillEffectMultiplier = 1.0f;
    }

    /// <summary>
    /// Creates a deep copy of this modifier context.
    /// </summary>
    public DamageModifierContext Clone()
    {
        var copy = new DamageModifierContext
        {
            BaseCritChance = BaseCritChance,
            FlatCritChance = FlatCritChance,
            BaseCritMultiplier = BaseCritMultiplier,
            AdditionalCritMultiplier = AdditionalCritMultiplier,
            CannotCrit = CannotCrit,
            ChanceToIgnite = ChanceToIgnite,
            ChanceToChill = ChanceToChill,
            ChanceToFreeze = ChanceToFreeze,
            ChanceToShock = ChanceToShock,
            ChanceToBleed = ChanceToBleed,
            ChanceToPoison = ChanceToPoison,
            IsAttack = IsAttack,
            BleedTargetIsMoving = BleedTargetIsMoving,
            IgniteDurationMultiplier = IgniteDurationMultiplier,
            BleedDurationMultiplier = BleedDurationMultiplier,
            PoisonDurationMultiplier = PoisonDurationMultiplier,
            ShockEffectMultiplier = ShockEffectMultiplier,
            ChillEffectMultiplier = ChillEffectMultiplier
        };

        copy.Global.Combine(Global);
        for (int i = 0; i < 5; i++)
        {
            copy._typeBuckets[i].Combine(_typeBuckets[i]);
        }
        copy.CritChanceBucket.Combine(CritChanceBucket);
        copy.DamageOverTimeBucket.Combine(DamageOverTimeBucket);

        for (int i = 0; i < 5; i++)
        {
            var dt = (DamageType)i;
            copy.AddedFlatDamage[dt] = AddedFlatDamage[dt];
            for (int j = 0; j < 5; j++)
            {
                copy._gainExtraPercent[i, j] = _gainExtraPercent[i, j];
                copy._convertPercent[i, j] = _convertPercent[i, j];
            }
        }

        return copy;
    }
}
