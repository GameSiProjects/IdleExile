using System;

namespace IdleExile.Core.Models;

/// <summary>
/// Represents a range of damage [Min, Max] for a specific damage source.
/// </summary>
public readonly record struct DamageRange(float Min, float Max)
{
    public static DamageRange Zero => new(0f, 0f);

    public float Average => (Min + Max) * 0.5f;

    public bool IsZero => Min <= 0f && Max <= 0f;

    /// <summary>
    /// Uniformly rolls a random damage value between Min and Max.
    /// </summary>
    public float Roll(Random random)
    {
        if (Min >= Max)
            return Min;

        return Min + (float)random.NextDouble() * (Max - Min);
    }

    public static DamageRange operator +(DamageRange a, DamageRange b) =>
        new(a.Min + b.Min, a.Max + b.Max);

    public static DamageRange operator *(DamageRange range, float multiplier) =>
        new(range.Min * multiplier, range.Max * multiplier);

    public static DamageRange operator *(float multiplier, DamageRange range) =>
        new(range.Min * multiplier, range.Max * multiplier);
}
