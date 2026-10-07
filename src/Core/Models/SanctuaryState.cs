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
    private long _voltaxicSulphite = 6850;
    private long _maxVoltaxicSulphite = 10000;
    private int _deepestDepth = 185;
    private bool _eaterOfWorldsDefeated;
    private bool _searingExarchDefeated;
    private bool _mavenDefeated;
    private bool _uberElderDefeated;

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

    public long VoltaxicSulphite
    {
        get => _voltaxicSulphite;
        set
        {
            long clamped = Math.Clamp(value, 0, _maxVoltaxicSulphite);
            if (_voltaxicSulphite != clamped)
            {
                _voltaxicSulphite = clamped;
                OnSanctuaryUpdated?.Invoke();
            }
        }
    }

    public long MaxVoltaxicSulphite
    {
        get => _maxVoltaxicSulphite;
        set
        {
            if (_maxVoltaxicSulphite != value)
            {
                _maxVoltaxicSulphite = Math.Max(1000, value);
                if (_voltaxicSulphite > _maxVoltaxicSulphite)
                {
                    _voltaxicSulphite = _maxVoltaxicSulphite;
                }
                OnSanctuaryUpdated?.Invoke();
            }
        }
    }

    public int DeepestDepth
    {
        get => _deepestDepth;
        set
        {
            if (_deepestDepth != value)
            {
                _deepestDepth = Math.Max(1, value);
                OnSanctuaryUpdated?.Invoke();
            }
        }
    }

    public bool EaterOfWorldsDefeated
    {
        get => _eaterOfWorldsDefeated;
        set
        {
            if (_eaterOfWorldsDefeated != value)
            {
                _eaterOfWorldsDefeated = value;
                OnSanctuaryUpdated?.Invoke();
            }
        }
    }

    public bool SearingExarchDefeated
    {
        get => _searingExarchDefeated;
        set
        {
            if (_searingExarchDefeated != value)
            {
                _searingExarchDefeated = value;
                OnSanctuaryUpdated?.Invoke();
            }
        }
    }

    public bool MavenDefeated
    {
        get => _mavenDefeated;
        set
        {
            if (_mavenDefeated != value)
            {
                _mavenDefeated = value;
                OnSanctuaryUpdated?.Invoke();
            }
        }
    }

    public bool UberElderDefeated
    {
        get => _uberElderDefeated;
        set
        {
            if (_uberElderDefeated != value)
            {
                _uberElderDefeated = value;
                OnSanctuaryUpdated?.Invoke();
            }
        }
    }

    public int VoidstonesCount =>
        (_eaterOfWorldsDefeated ? 1 : 0) +
        (_searingExarchDefeated ? 1 : 0) +
        (_mavenDefeated ? 1 : 0) +
        (_uberElderDefeated ? 1 : 0);

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

    public void AddSulphite(long amount)
    {
        if (amount <= 0) return;
        VoltaxicSulphite = Math.Min(_maxVoltaxicSulphite, _voltaxicSulphite + amount);
    }

    public bool TryConsumeSulphite(long cost)
    {
        if (cost <= 0) return true;
        if (_voltaxicSulphite < cost) return false;
        VoltaxicSulphite -= cost;
        return true;
    }

    public bool DescendAbyss(int depthIncrease = 1, long cost = 250)
    {
        if (!TryConsumeSulphite(cost)) return false;
        DeepestDepth += Math.Max(1, depthIncrease);
        return true;
    }
}
