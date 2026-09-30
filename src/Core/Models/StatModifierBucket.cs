using System;

namespace IdleExile.Core.Models;

/// <summary>
/// Encapsulates Path of Exile's modifier arithmetic:
/// Multiplier = Max(0, 1 + sum(Increased) - sum(Reduced)) * prod(1 + More) * prod(1 - Less).
/// </summary>
public sealed class StatModifierBucket
{
    public float IncreasedSum { get; private set; } = 0f;
    public float ReducedSum { get; private set; } = 0f;
    public float MoreProduct { get; private set; } = 1f;
    public float LessProduct { get; private set; } = 1f;

    public void AddIncreased(float percent)
    {
        if (percent != 0f)
            IncreasedSum += percent;
    }

    public void AddReduced(float percent)
    {
        if (percent != 0f)
            ReducedSum += percent;
    }

    public void AddMore(float percent)
    {
        if (percent != 0f)
            MoreProduct *= (1f + percent / 100f);
    }

    public void AddLess(float percent)
    {
        if (percent != 0f)
            LessProduct *= MathF.Max(0f, 1f - percent / 100f);
    }

    /// <summary>
    /// Computes the final multiplicative factor from additive (Increased/Reduced) and multiplicative (More/Less) modifiers.
    /// </summary>
    public float CalculateMultiplier()
    {
        float additive = MathF.Max(0f, 1f + (IncreasedSum - ReducedSum) / 100f);
        return additive * MoreProduct * LessProduct;
    }

    /// <summary>
    /// Combines another bucket into this one (adds Increased/Reduced, multiplies More/Less).
    /// </summary>
    public void Combine(StatModifierBucket other)
    {
        IncreasedSum += other.IncreasedSum;
        ReducedSum += other.ReducedSum;
        MoreProduct *= other.MoreProduct;
        LessProduct *= other.LessProduct;
    }

    public StatModifierBucket Clone()
    {
        var copy = new StatModifierBucket
        {
            IncreasedSum = IncreasedSum,
            ReducedSum = ReducedSum,
            MoreProduct = MoreProduct,
            LessProduct = LessProduct
        };
        return copy;
    }

    public void Reset()
    {
        IncreasedSum = 0f;
        ReducedSum = 0f;
        MoreProduct = 1f;
        LessProduct = 1f;
    }
}
