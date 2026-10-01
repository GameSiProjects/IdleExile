using System;
using System.Collections.Generic;
using IdleExile.Core.Models;
using IdleExile.Core.Services;

namespace IdleExile.Core.Simulation;

/// <summary>
/// Event arguments emitted when damage is dealt to a monster (from direct attack, spell, or DoT).
/// Subscribed by UI floating text, damage meters, and visual hit feedback.
/// </summary>
public sealed record DamageDealtEventArgs(
    MonsterInstance Target,
    HitResult? Hit,
    float TotalDamageDealt,
    bool IsCrit,
    IReadOnlyList<AilmentInstance> InflictedAilments,
    bool IsDoT = false
);

/// <summary>
/// Event arguments emitted when the player takes damage (from a monster attack, spell, reflect, or DoT).
/// Subscribed by UI vitometer, screen shake, and damage indicators.
/// </summary>
public sealed record PlayerDamagedEventArgs(
    MonsterInstance? Attacker,
    DefenseResult DefenseResult,
    float DamageApplied,
    HitDamageBreakdown? MitigatedBreakdown,
    bool Evaded,
    bool Blocked,
    bool Suppressed,
    bool IsReflect = false,
    bool IsDoT = false
);

/// <summary>
/// Event arguments emitted when a monster is slain during combat.
/// Subscribed by kill counters, audio triggers, and combat telemetry.
/// </summary>
public sealed record MonsterKilledEventArgs(
    MonsterInstance Monster,
    float ExpGranted,
    IReadOnlyList<LootDrop> Drops
);

/// <summary>
/// Event arguments emitted at the conclusion of every discrete 30Hz combat simulation tick.
/// Subscribed by UI telemetry progress bars and HUD monitors.
/// </summary>
public sealed record TickProcessedEventArgs(
    long TickIndex,
    float DeltaTime,
    float MapSimulationTime,
    int AliveMonstersCount,
    float ClearProgress,
    bool IsBossWave
);

/// <summary>
/// Event arguments emitted when a map run is fully cleared (all trash waves + boss defeated).
/// </summary>
public sealed record MapCompletedEventArgs(
    MapRunConfig Config,
    int TotalMonstersKilled,
    float ClearTimeSeconds,
    int MapDeaths
);

/// <summary>
/// Event arguments emitted when a Boss initiates a heavy slam attack (>70% player EHP threat).
/// Subscribed by Arena2D telegraph circle indicator and Emergency Portal warning hints.
/// </summary>
public sealed record BossSlamTelegraphEventArgs(
    MonsterInstance Boss,
    float ThreatEhpPercent,
    float WindUpDuration = 1.35f
);

/// <summary>
/// Event arguments emitted when the player fires an active skill.
/// Contains 2D spatial metadata: geometry type, aim angle, projectile trajectories or cone/AoE shapes.
/// </summary>
public sealed record PlayerSkillFiredEventArgs(
    Vector2D ExilePosition,
    MonsterInstance? PrimaryTarget,
    SkillGeometryType GeometryType,
    DamageType PrimaryDamageType,
    IReadOnlyList<ProjectileVector>? Projectiles = null,
    float ConeAngleDegrees = 0f,
    float RadiusOrRange = 0f,
    bool IsMelee = false
);

/// <summary>
/// Event arguments emitted when a monster executes a melee attack on the player.
/// Subscribed by Arena2D to render a strike AoE indicator (expanding arc/ring) at the monster's position.
/// </summary>
public sealed record MonsterAttackEventArgs(
    /// <summary>The attacking monster instance.</summary>
    MonsterInstance Attacker,
    /// <summary>Monster's 2D position at time of attack.</summary>
    Vector2D AttackOrigin,
    /// <summary>Direction vector from monster toward the Exile (normalized).</summary>
    Vector2D AttackDirection,
    /// <summary>Effective attack range in pixels (AttackRange + Radius).</summary>
    float EffectiveRange,
    /// <summary>Damage type for color coding the indicator.</summary>
    DamageType DamageType,
    /// <summary>True if this attack was a critical strike (bigger/brighter indicator).</summary>
    bool IsCrit,
    /// <summary>Raw total damage of the hit (used for indicator intensity scaling).</summary>
    float RawDamage
);

/// <summary>
/// Event arguments emitted when a roadside League Encounter transitions state (Task 2.7).
/// </summary>
public sealed record LeagueEncounterEventArgs(
    LeagueEncounterInstance Encounter
);
