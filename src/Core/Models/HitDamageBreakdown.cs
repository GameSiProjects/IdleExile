using System;

namespace IdleExile.Core.Models;

/// <summary>
/// Concrete rolled damage amounts for each damage type in a single hit.
/// </summary>
public sealed class HitDamageBreakdown
{
    private readonly float[] _amounts = new float[5];

    public HitDamageBreakdown()
    {
    }

    public HitDamageBreakdown(float physical, float fire = 0f, float cold = 0f, float lightning = 0f, float chaos = 0f)
    {
        _amounts[(int)DamageType.Physical] = physical;
        _amounts[(int)DamageType.Fire] = fire;
        _amounts[(int)DamageType.Cold] = cold;
        _amounts[(int)DamageType.Lightning] = lightning;
        _amounts[(int)DamageType.Chaos] = chaos;
    }

    public float this[DamageType type]
    {
        get => _amounts[(int)type];
        set => _amounts[(int)type] = value;
    }

    public float Physical
    {
        get => _amounts[(int)DamageType.Physical];
        set => _amounts[(int)DamageType.Physical] = value;
    }

    public float Fire
    {
        get => _amounts[(int)DamageType.Fire];
        set => _amounts[(int)DamageType.Fire] = value;
    }

    public float Cold
    {
        get => _amounts[(int)DamageType.Cold];
        set => _amounts[(int)DamageType.Cold] = value;
    }

    public float Lightning
    {
        get => _amounts[(int)DamageType.Lightning];
        set => _amounts[(int)DamageType.Lightning] = value;
    }

    public float Chaos
    {
        get => _amounts[(int)DamageType.Chaos];
        set => _amounts[(int)DamageType.Chaos] = value;
    }

    public float Total =>
        _amounts[0] + _amounts[1] + _amounts[2] + _amounts[3] + _amounts[4];

    public HitDamageBreakdown Multiply(float factor)
    {
        return new HitDamageBreakdown(
            Physical * factor,
            Fire * factor,
            Cold * factor,
            Lightning * factor,
            Chaos * factor
        );
    }

    public HitDamageBreakdown Clone()
    {
        var copy = new HitDamageBreakdown();
        Array.Copy(_amounts, copy._amounts, 5);
        return copy;
    }
}
