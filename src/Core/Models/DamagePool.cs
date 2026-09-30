using System;

namespace IdleExile.Core.Models;

/// <summary>
/// Container holding DamageRanges for all 5 damage types.
/// </summary>
public sealed class DamagePool
{
	private readonly DamageRange[] _damages = new DamageRange[5];

	public DamagePool()
	{
	}

	public DamagePool(DamageRange physical, DamageRange fire = default, DamageRange cold = default, DamageRange lightning = default, DamageRange chaos = default)
	{
		_damages[(int)DamageType.Physical] = physical;
		_damages[(int)DamageType.Fire] = fire;
		_damages[(int)DamageType.Cold] = cold;
		_damages[(int)DamageType.Lightning] = lightning;
		_damages[(int)DamageType.Chaos] = chaos;
	}

	public DamageRange this[DamageType type]
	{
		get => _damages[(int)type];
		set => _damages[(int)type] = value;
	}

	public DamageRange Physical
	{
		get => _damages[(int)DamageType.Physical];
		set => _damages[(int)DamageType.Physical] = value;
	}

	public DamageRange Fire
	{
		get => _damages[(int)DamageType.Fire];
		set => _damages[(int)DamageType.Fire] = value;
	}

	public DamageRange Cold
	{
		get => _damages[(int)DamageType.Cold];
		set => _damages[(int)DamageType.Cold] = value;
	}

	public DamageRange Lightning
	{
		get => _damages[(int)DamageType.Lightning];
		set => _damages[(int)DamageType.Lightning] = value;
	}

	public DamageRange Chaos
	{
		get => _damages[(int)DamageType.Chaos];
		set => _damages[(int)DamageType.Chaos] = value;
	}

	public float TotalMin =>
		_damages[0].Min + _damages[1].Min + _damages[2].Min + _damages[3].Min + _damages[4].Min;

	public float TotalMax =>
		_damages[0].Max + _damages[1].Max + _damages[2].Max + _damages[3].Max + _damages[4].Max;

	public float TotalAverage => (TotalMin + TotalMax) * 0.5f;

	public void Add(DamageType type, DamageRange range)
	{
		_damages[(int)type] += range;
	}

	public DamagePool Clone()
	{
		var copy = new DamagePool();
		Array.Copy(_damages, copy._damages, 5);
		return copy;
	}

	/// <summary>
	/// Resets all damage ranges to default(0, 0) for reuse in pooling.
	/// </summary>
	public void Reset()
	{
		for (int i = 0; i < 5; i++)
		{
			_damages[i] = default;
		}
	}

	/// <summary>
	/// Multiplies all 5 damage ranges by a scalar factor.
	/// </summary>
	public DamagePool Multiply(float factor) =>
		new(Physical * factor, Fire * factor, Cold * factor, Lightning * factor, Chaos * factor);

	/// <summary>
	/// Copies damage ranges from source pool without allocating.
	/// </summary>
	public void CopyFrom(DamagePool source)
	{
		Array.Copy(source._damages, _damages, 5);
	}
}
