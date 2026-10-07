using System;

namespace IdleExile.Core.Models;

/// <summary>
/// Runtime state for The Crucible Sanctuary (Hub/Hideout).
/// Tracks hub tier, accumulated Thaumaturgic Dust, automation toggles, and map infusion bonuses.
/// Pure C# model with zero Godot dependencies.
/// </summary>
public sealed class SanctuaryState
{
    private int _currentTier = 1;
    private long _thaumaturgicDust;
    private bool _autoSmeltActive = true;
    private int _infusedMapsRemaining;
    private float _infusedQuantityBonus;

    public int CurrentTier
    {
        get => _currentTier;
        set
        {
            if (_currentTier != value)
            {
                _currentTier = Math.Clamp(value, 1, 4);
                OnSanctuaryUpdated?.Invoke();
            }
        }
    }

    public long ThaumaturgicDust
    {
        get => _thaumaturgicDust;
        set
        {
            if (_thaumaturgicDust != value)
            {
                _thaumaturgicDust = Math.Max(0, value);
                OnSanctuaryUpdated?.Invoke();
            }
        }
    }

    public bool AutoSmeltActive
    {
        get => _autoSmeltActive;
        set
        {
            if (_autoSmeltActive != value)
            {
                _autoSmeltActive = value;
                OnSanctuaryUpdated?.Invoke();
            }
        }
    }

    public int InfusedMapsRemaining
    {
        get => _infusedMapsRemaining;
        set
        {
            if (_infusedMapsRemaining != value)
            {
                _infusedMapsRemaining = Math.Max(0, value);
                if (_infusedMapsRemaining == 0)
                {
                    _infusedQuantityBonus = 0f;
                }
                OnSanctuaryUpdated?.Invoke();
            }
        }
    }

    public float InfusedQuantityBonus
    {
        get => _infusedQuantityBonus;
        set
        {
            if (Math.Abs(_infusedQuantityBonus - value) > 0.0001f)
            {
                _infusedQuantityBonus = Math.Max(0f, value);
                OnSanctuaryUpdated?.Invoke();
            }
        }
    }

    public event Action? OnSanctuaryUpdated;

    public void AddDust(long amount)
    {
        if (amount <= 0) return;
        _thaumaturgicDust += amount;
        OnSanctuaryUpdated?.Invoke();
    }

    public bool TryConsumeDust(long cost)
    {
        if (cost <= 0) return true;
        if (_thaumaturgicDust < cost) return false;

        _thaumaturgicDust -= cost;
        OnSanctuaryUpdated?.Invoke();
        return true;
    }

    public void InfuseMaps(int count, float bonusQuantity)
    {
        _infusedMapsRemaining = count;
        _infusedQuantityBonus = bonusQuantity;
        OnSanctuaryUpdated?.Invoke();
    }

    public void ConsumeInfusedMap()
    {
        if (_infusedMapsRemaining > 0)
        {
            _infusedMapsRemaining--;
            if (_infusedMapsRemaining == 0)
            {
                _infusedQuantityBonus = 0f;
            }
            OnSanctuaryUpdated?.Invoke();
        }
    }
}
