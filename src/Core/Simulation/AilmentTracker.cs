using System;
using System.Collections.Generic;
using System.Linq;
using IdleExile.Core.Models;

namespace IdleExile.Core.Simulation;

/// <summary>
/// Represents an active ailment ticking on an entity.
/// Wraps the immutable <see cref="AilmentInstance"/> with mutable duration tracking.
/// </summary>
public sealed class ActiveAilment
{
    public AilmentInstance Instance { get; }
    public float RemainingDuration { get; set; }
    public float ElapsedDuration { get; set; }

    public AilmentType Type => Instance.Type;
    public DamageType SourceDamageType => Instance.SourceDamageType;
    public float DamagePerSecond => Instance.DamagePerSecond;
    public float EffectPercent => Instance.EffectPercent;
    public bool IsStacking => Instance.IsStacking;
    public bool PreventsAction => Instance.PreventsAction;

    public ActiveAilment(AilmentInstance instance)
    {
        Instance = instance;
        RemainingDuration = MathF.Max(0f, instance.DurationSeconds);
        ElapsedDuration = 0f;
    }
}

/// <summary>
/// Breakdown of damage and debuffs produced during a single simulation tick of <see cref="AilmentTracker"/>.
/// </summary>
public sealed record AilmentTickResult(
    float TotalDoTDamage,
    float FireDoTDamage,
    float PhysicalDoTDamage,
    float ChaosDoTDamage,
    float ChillSlowPercent,
    bool IsFrozen,
    float ShockDamageTakenPercent,
    IReadOnlyList<AilmentType> ExpiredTypes
);

/// <summary>
/// Manages status ailments on a combat entity (Player or Monster).
/// Handles:
/// - 30Hz tick duration countdown and expiration.
/// - Non-stacking highest-effect retention (Ignite, Shock, Chill, Bleed, Freeze).
/// - Poison stacking damage calculation.
/// - Action speed modifiers (Chill slow and Freeze incapacitation).
/// - Shock damage taken amplification.
///
/// Pure C# — no Godot Node dependencies.
/// </summary>
public sealed class AilmentTracker
{
    private readonly List<ActiveAilment> _activeAilments = new();

    /// <summary>
    /// Current list of active ailments on this entity.
    /// </summary>
    public IReadOnlyList<ActiveAilment> ActiveAilments => _activeAilments;

    /// <summary>
    /// Fired when a new ailment is successfully applied or replaces a weaker one.
    /// </summary>
    public event Action<ActiveAilment>? OnAilmentApplied;

    /// <summary>
    /// Fired when an ailment expires naturally or is cleansed.
    /// </summary>
    public event Action<AilmentType>? OnAilmentExpired;

    // ─────────────────────────────────────────────
    //  Ailment Application & Refresh Rules
    // ─────────────────────────────────────────────

    /// <summary>
    /// Applies an ailment instance to this entity according to PoE stacking and refresh rules.
    /// - Stacking ailments (Poison): Appended as separate instances, all deal damage concurrently.
    /// - Non-stacking ailments (Ignite, Bleed, Chill, Freeze, Shock):
    ///   Replaces any existing instance if the new one is stronger or equal with longer duration.
    /// </summary>
    public void ApplyAilment(AilmentInstance ailment)
    {
        if (ailment.DurationSeconds <= 0f)
            return;

        if (ailment.IsStacking)
        {
            // Poison stacks indefinitely in PoE
            var active = new ActiveAilment(ailment);
            _activeAilments.Add(active);
            OnAilmentApplied?.Invoke(active);
            return;
        }

        // Non-stacking ailments: Find any existing instances of this type
        var existing = _activeAilments.FirstOrDefault(a => a.Type == ailment.Type);

        if (existing == null)
        {
            var active = new ActiveAilment(ailment);
            _activeAilments.Add(active);
            OnAilmentApplied?.Invoke(active);
            return;
        }

        // Evaluate whether new instance supersedes existing instance
        bool shouldReplace = false;

        switch (ailment.Type)
        {
            case AilmentType.Ignite:
            case AilmentType.Bleed:
                // Compare DPS: Higher DPS wins; if equal DPS, longer duration wins
                if (ailment.DamagePerSecond > existing.DamagePerSecond)
                {
                    shouldReplace = true;
                }
                else if (MathF.Abs(ailment.DamagePerSecond - existing.DamagePerSecond) < 0.001f
                         && ailment.DurationSeconds > existing.RemainingDuration)
                {
                    shouldReplace = true;
                }
                break;

            case AilmentType.Chill:
            case AilmentType.Shock:
                // Compare EffectPercent: Higher slow / shock multiplier wins
                if (ailment.EffectPercent > existing.EffectPercent)
                {
                    shouldReplace = true;
                }
                else if (MathF.Abs(ailment.EffectPercent - existing.EffectPercent) < 0.001f
                         && ailment.DurationSeconds > existing.RemainingDuration)
                {
                    shouldReplace = true;
                }
                break;

            case AilmentType.Freeze:
                // Compare Duration: Longer freeze wins
                if (ailment.DurationSeconds > existing.RemainingDuration)
                {
                    shouldReplace = true;
                }
                break;
        }

        if (shouldReplace)
        {
            _activeAilments.Remove(existing);
            var active = new ActiveAilment(ailment);
            _activeAilments.Add(active);
            OnAilmentApplied?.Invoke(active);
        }
    }

    // ─────────────────────────────────────────────
    //  Tick Simulation
    // ─────────────────────────────────────────────

    /// <summary>
    /// Advances duration of all active ailments by deltaTime and calculates DoT damage.
    /// Returns an <see cref="AilmentTickResult"/> containing damage to apply and current debuffs.
    /// </summary>
    public AilmentTickResult Tick(float deltaTime)
    {
        if (deltaTime <= 0f || _activeAilments.Count == 0)
        {
            return new AilmentTickResult(
                TotalDoTDamage: 0f,
                FireDoTDamage: 0f,
                PhysicalDoTDamage: 0f,
                ChaosDoTDamage: 0f,
                ChillSlowPercent: 0f,
                IsFrozen: false,
                ShockDamageTakenPercent: 0f,
                ExpiredTypes: Array.Empty<AilmentType>()
            );
        }

        // 1. Calculate DoT Damage for this tick
        float fireDoT = 0f;
        float physicalDoT = 0f;
        float chaosDoT = 0f;

        // Ignite: Non-stacking, highest DPS deals damage
        var ignite = _activeAilments.Where(a => a.Type == AilmentType.Ignite)
                                   .OrderByDescending(a => a.DamagePerSecond)
                                   .FirstOrDefault();
        if (ignite != null)
        {
            float activeDt = MathF.Min(deltaTime, MathF.Max(0f, ignite.RemainingDuration));
            fireDoT = ignite.DamagePerSecond * activeDt;
        }

        // Bleed: Non-stacking, highest DPS deals damage
        var bleed = _activeAilments.Where(a => a.Type == AilmentType.Bleed)
                                  .OrderByDescending(a => a.DamagePerSecond)
                                  .FirstOrDefault();
        if (bleed != null)
        {
            float activeDt = MathF.Min(deltaTime, MathF.Max(0f, bleed.RemainingDuration));
            physicalDoT = bleed.DamagePerSecond * activeDt;
        }

        // Poison: Stacking, all active instances deal damage
        foreach (var p in _activeAilments)
        {
            if (p.Type == AilmentType.Poison)
            {
                float activeDt = MathF.Min(deltaTime, MathF.Max(0f, p.RemainingDuration));
                chaosDoT += p.DamagePerSecond * activeDt;
            }
        }

        float totalDoT = fireDoT + physicalDoT + chaosDoT;

        // 2. Tick-down durations & collect expirations
        List<AilmentType>? expired = null;
        for (int i = _activeAilments.Count - 1; i >= 0; i--)
        {
            var ailment = _activeAilments[i];
            ailment.RemainingDuration -= deltaTime;
            ailment.ElapsedDuration += deltaTime;

            if (ailment.RemainingDuration <= 0f)
            {
                expired ??= new List<AilmentType>();
                expired.Add(ailment.Type);
                _activeAilments.RemoveAt(i);
                OnAilmentExpired?.Invoke(ailment.Type);
            }
        }

        // 3. Compute CC & Damage Debuffs
        float chillSlow = CurrentChillSlowPercent;
        bool isFrozen = IsFrozen;
        float shockTaken = CurrentShockDamageTakenPercent;

        return new AilmentTickResult(
            TotalDoTDamage: totalDoT,
            FireDoTDamage: fireDoT,
            PhysicalDoTDamage: physicalDoT,
            ChaosDoTDamage: chaosDoT,
            ChillSlowPercent: chillSlow,
            IsFrozen: isFrozen,
            ShockDamageTakenPercent: shockTaken,
            ExpiredTypes: expired ?? (IReadOnlyList<AilmentType>)Array.Empty<AilmentType>()
        );
    }

    // ─────────────────────────────────────────────
    //  State Queries
    // ─────────────────────────────────────────────

    /// <summary>
    /// Highest active Chill slow percentage (capped at 30%).
    /// </summary>
    public float CurrentChillSlowPercent
    {
        get
        {
            float maxSlow = 0f;
            foreach (var a in _activeAilments)
            {
                if (a.Type == AilmentType.Chill && a.EffectPercent > maxSlow)
                {
                    maxSlow = a.EffectPercent;
                }
            }
            return Math.Clamp(maxSlow, 0f, AilmentConstants.ChillMaxCap);
        }
    }

    /// <summary>
    /// True if any active Freeze ailment prevents actions.
    /// </summary>
    public bool IsFrozen => _activeAilments.Any(a => a.Type == AilmentType.Freeze && a.PreventsAction && a.RemainingDuration > 0f);

    /// <summary>
    /// Highest active Shock increased damage taken percentage (capped at 50%).
    /// </summary>
    public float CurrentShockDamageTakenPercent
    {
        get
        {
            float maxShock = 0f;
            foreach (var a in _activeAilments)
            {
                if (a.Type == AilmentType.Shock && a.EffectPercent > maxShock)
                {
                    maxShock = a.EffectPercent;
                }
            }
            return Math.Clamp(maxShock, 0f, AilmentConstants.ShockMaxCap);
        }
    }

    /// <summary>
    /// Effective action speed multiplier based on Chill slow and Freeze.
    /// If Frozen: 0.0. Otherwise: 1.0 - (ChillSlowPercent / 100).
    /// </summary>
    public float ActionSpeedMultiplier
    {
        get
        {
            if (IsFrozen)
                return 0f;

            float slow = CurrentChillSlowPercent;
            return Math.Clamp(1f - slow / 100f, 0.1f, 1f);
        }
    }

    /// <summary>
    /// Returns total active DoT DPS across all active ailments (Ignite + Bleed + sum of Poisons).
    /// </summary>
    public float TotalDoTDps
    {
        get
        {
            float dps = 0f;

            var ignite = _activeAilments.Where(a => a.Type == AilmentType.Ignite)
                                       .OrderByDescending(a => a.DamagePerSecond)
                                       .FirstOrDefault();
            if (ignite != null) dps += ignite.DamagePerSecond;

            var bleed = _activeAilments.Where(a => a.Type == AilmentType.Bleed)
                                      .OrderByDescending(a => a.DamagePerSecond)
                                      .FirstOrDefault();
            if (bleed != null) dps += bleed.DamagePerSecond;

            foreach (var a in _activeAilments)
            {
                if (a.Type == AilmentType.Poison)
                {
                    dps += a.DamagePerSecond;
                }
            }

            return dps;
        }
    }

    /// <summary>
    /// Checks whether an ailment of the specified type is currently active.
    /// </summary>
    public bool HasAilment(AilmentType type) => _activeAilments.Any(a => a.Type == type);

    /// <summary>
    /// Clears all active ailments (e.g. on death, respawn, or flask cleanse).
    /// </summary>
    public void Clear()
    {
        if (_activeAilments.Count > 0)
        {
            _activeAilments.Clear();
        }
    }
}
