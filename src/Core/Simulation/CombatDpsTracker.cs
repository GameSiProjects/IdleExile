using System;
using System.Collections.Generic;
using IdleExile.Core.Models;

namespace IdleExile.Core.Simulation;

/// <summary>
/// Real-time in-combat DPS and telemetry tracker using a high-precision sliding time window (3.0s).
/// Measures actual effective damage dealt to monster life/ES after all mitigation (Armour, Resistances, Block, Suppression),
/// including both direct hits and status ailment damage-over-time (Bleed, Ignite, Poison).
///
/// Pure C# — zero Godot Node dependencies!
/// </summary>
public sealed class CombatDpsTracker
{
    public const float DefaultWindowDuration = 3.0f;

    private readonly struct DamageRecord
    {
        public readonly float Timestamp;
        public readonly float Damage;
        public readonly DamageType Type;
        public readonly bool IsHit;
        public readonly bool IsCrit;
        public readonly bool IsDoT;

        public DamageRecord(float timestamp, float damage, DamageType type, bool isHit, bool isCrit, bool isDoT)
        {
            Timestamp = timestamp;
            Damage = damage;
            Type = type;
            IsHit = isHit;
            IsCrit = isCrit;
            IsDoT = isDoT;
        }
    }

    private readonly struct AttackAttemptRecord
    {
        public readonly float Timestamp;
        public readonly bool WasHit;

        public AttackAttemptRecord(float timestamp, bool wasHit)
        {
            Timestamp = timestamp;
            WasHit = wasHit;
        }
    }

    private readonly Queue<DamageRecord> _damageEvents = new(128);
    private readonly Queue<AttackAttemptRecord> _attackAttempts = new(64);

    private readonly float[] _windowDamageByType = new float[5];
    private readonly float[] _totalDamageByType = new float[5];

    private float _windowTotalDamage;
    private int _windowHits;
    private int _windowCrits;
    private int _windowAttempts;
    private int _windowSuccessfulHits;

    public float WindowDuration { get; set; } = DefaultWindowDuration;

    public float CurrentDps { get; private set; }
    public float PeakDps { get; private set; }
    public float AverageDps { get; private set; }
    public float TotalDamageDealt { get; private set; }
    public int TotalHits { get; private set; }
    public int TotalCrits { get; private set; }
    public int TotalAttempts { get; private set; }
    public float CombatDuration { get; private set; }

    /// <summary>
    /// Records an arbitrary damage amount dealt at a given timestamp.
    /// </summary>
    public void RecordDamage(float timestamp, float damage, DamageType type, bool isHit = true, bool isCrit = false, bool isDoT = false)
    {
        if (damage <= 0f) return;

        var record = new DamageRecord(timestamp, damage, type, isHit, isCrit, isDoT);
        _damageEvents.Enqueue(record);

        _windowTotalDamage += damage;
        _windowDamageByType[(int)type] += damage;
        if (isHit)
        {
            _windowHits++;
            if (isCrit) _windowCrits++;
        }

        TotalDamageDealt += damage;
        _totalDamageByType[(int)type] += damage;
        if (isHit)
        {
            TotalHits++;
            if (isCrit) TotalCrits++;
        }
    }

    /// <summary>
    /// Records an executed direct combat hit, scaling and attributing each damage type proportionally.
    /// </summary>
    public void RecordHit(float timestamp, HitResult hit, float actualDamageDealt)
    {
        if (actualDamageDealt <= 0.001f) return;

        float totalHitDamage = hit.TotalDamage;
        if (totalHitDamage <= 0.001f)
        {
            RecordDamage(timestamp, actualDamageDealt, DamageType.Physical, isHit: true, isCrit: hit.IsCrit, isDoT: false);
            return;
        }

        float scale = actualDamageDealt / totalHitDamage;
        bool hasRecordedAny = false;

        if (hit.Damage.Physical > 0.001f)
        {
            RecordDamage(timestamp, hit.Damage.Physical * scale, DamageType.Physical, isHit: true, isCrit: hit.IsCrit, isDoT: false);
            hasRecordedAny = true;
        }
        if (hit.Damage.Fire > 0.001f)
        {
            RecordDamage(timestamp, hit.Damage.Fire * scale, DamageType.Fire, isHit: true, isCrit: hit.IsCrit, isDoT: false);
            hasRecordedAny = true;
        }
        if (hit.Damage.Cold > 0.001f)
        {
            RecordDamage(timestamp, hit.Damage.Cold * scale, DamageType.Cold, isHit: true, isCrit: hit.IsCrit, isDoT: false);
            hasRecordedAny = true;
        }
        if (hit.Damage.Lightning > 0.001f)
        {
            RecordDamage(timestamp, hit.Damage.Lightning * scale, DamageType.Lightning, isHit: true, isCrit: hit.IsCrit, isDoT: false);
            hasRecordedAny = true;
        }
        if (hit.Damage.Chaos > 0.001f)
        {
            RecordDamage(timestamp, hit.Damage.Chaos * scale, DamageType.Chaos, isHit: true, isCrit: hit.IsCrit, isDoT: false);
            hasRecordedAny = true;
        }

        if (!hasRecordedAny)
        {
            RecordDamage(timestamp, actualDamageDealt, DamageType.Physical, isHit: true, isCrit: hit.IsCrit, isDoT: false);
        }
    }

    /// <summary>
    /// Records a status ailment damage-over-time tick (Bleed, Ignite, Poison).
    /// </summary>
    public void RecordDoT(float timestamp, float damage, DamageType type)
    {
        RecordDamage(timestamp, damage, type, isHit: false, isCrit: false, isDoT: true);
    }

    /// <summary>
    /// Records an attack initiation attempt for accuracy calculation.
    /// </summary>
    public void RecordAttackAttempt(float timestamp, bool wasHit)
    {
        _attackAttempts.Enqueue(new AttackAttemptRecord(timestamp, wasHit));
        _windowAttempts++;
        if (wasHit) _windowSuccessfulHits++;

        TotalAttempts++;
    }

    /// <summary>
    /// Advances the sliding time window and updates rolling metrics.
    /// Called per discrete simulation tick in CombatEngine.
    /// </summary>
    public void Update(float currentTime, float delta)
    {
        CombatDuration += delta;

        // 1. Evict damage records older than sliding window duration
        while (_damageEvents.Count > 0 && (currentTime - _damageEvents.Peek().Timestamp) > WindowDuration)
        {
            var old = _damageEvents.Dequeue();
            _windowTotalDamage -= old.Damage;
            _windowDamageByType[(int)old.Type] -= old.Damage;
            if (old.IsHit)
            {
                _windowHits--;
                if (old.IsCrit) _windowCrits--;
            }
        }

        // Clamp rounding drift
        if (_windowTotalDamage < 0f) _windowTotalDamage = 0f;
        for (int i = 0; i < 5; i++)
        {
            if (_windowDamageByType[i] < 0f) _windowDamageByType[i] = 0f;
        }
        if (_windowHits < 0) _windowHits = 0;
        if (_windowCrits < 0) _windowCrits = 0;

        // 2. Evict attack attempt records older than sliding window duration
        while (_attackAttempts.Count > 0 && (currentTime - _attackAttempts.Peek().Timestamp) > WindowDuration)
        {
            var old = _attackAttempts.Dequeue();
            _windowAttempts--;
            if (old.WasHit) _windowSuccessfulHits--;
        }
        if (_windowAttempts < 0) _windowAttempts = 0;
        if (_windowSuccessfulHits < 0) _windowSuccessfulHits = 0;

        // 3. Compute Current DPS
        float effectiveWindow = Math.Max(0.2f, Math.Min(CombatDuration, WindowDuration));
        CurrentDps = effectiveWindow > 0f ? _windowTotalDamage / effectiveWindow : 0f;

        if (CurrentDps > PeakDps)
        {
            PeakDps = CurrentDps;
        }

        // 4. Compute Average Session DPS
        AverageDps = CombatDuration > 0.05f ? TotalDamageDealt / CombatDuration : 0f;
    }

    /// <summary>
    /// Generates an immutable telemetry snapshot for UI presentation and tooltips.
    /// </summary>
    public CombatDpsSnapshot GetSnapshot()
    {
        float physPct = 0f, firePct = 0f, coldPct = 0f, lightPct = 0f, chaosPct = 0f;
        float denom = _windowTotalDamage > 0.001f ? _windowTotalDamage : TotalDamageDealt;

        if (denom > 0.001f)
        {
            float[] source = _windowTotalDamage > 0.001f ? _windowDamageByType : _totalDamageByType;
            physPct = (source[(int)DamageType.Physical] / denom) * 100f;
            firePct = (source[(int)DamageType.Fire] / denom) * 100f;
            coldPct = (source[(int)DamageType.Cold] / denom) * 100f;
            lightPct = (source[(int)DamageType.Lightning] / denom) * 100f;
            chaosPct = (source[(int)DamageType.Chaos] / denom) * 100f;
        }

        float effectiveWindow = Math.Max(0.2f, Math.Min(CombatDuration, WindowDuration));
        float hitRateAps = effectiveWindow > 0f ? (float)_windowHits / effectiveWindow : 0f;

        float accuracy = 100f;
        if (_windowAttempts > 0)
        {
            accuracy = ((float)_windowSuccessfulHits / _windowAttempts) * 100f;
        }
        else if (TotalAttempts > 0)
        {
            accuracy = ((float)TotalHits / TotalAttempts) * 100f;
        }

        return new CombatDpsSnapshot(
            CurrentDps: CurrentDps,
            PeakDps: PeakDps,
            AverageDps: AverageDps,
            PhysicalPercent: physPct,
            FirePercent: firePct,
            ColdPercent: coldPct,
            LightningPercent: lightPct,
            ChaosPercent: chaosPct,
            HitRateAps: hitRateAps,
            AccuracyPercent: accuracy
        );
    }

    /// <summary>
    /// Resets all counters and queues when starting a new map encounter.
    /// </summary>
    public void ResetForNewMap()
    {
        _damageEvents.Clear();
        _attackAttempts.Clear();

        Array.Clear(_windowDamageByType, 0, _windowDamageByType.Length);
        Array.Clear(_totalDamageByType, 0, _totalDamageByType.Length);

        _windowTotalDamage = 0f;
        _windowHits = 0;
        _windowCrits = 0;
        _windowAttempts = 0;
        _windowSuccessfulHits = 0;

        CurrentDps = 0f;
        PeakDps = 0f;
        AverageDps = 0f;
        TotalDamageDealt = 0f;
        TotalHits = 0;
        TotalCrits = 0;
        TotalAttempts = 0;
        CombatDuration = 0f;
    }
}
