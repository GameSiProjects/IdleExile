namespace IdleExile.Core.Models;

using System.Collections.Generic;
using IdleExile.Core.Services;
using IdleExile.Core.Simulation;

/// <summary>
/// Unified event payload emitted whenever any combatant (Player, Monster, Minion, Boss)
/// executes an attack or casts a combat skill.
/// Ensures 100% symmetry across the simulation and visual presentation pipelines.
/// </summary>
public sealed record CombatAttackEventArgs(
    /// <summary>The attacking entity initiating the combat action (PlayerState or MonsterInstance).</summary>
    ICombatEntity Attacker,

    /// <summary>Primary target entity, if any.</summary>
    ICombatEntity? Target,

    /// <summary>Real-world origin coordinate P_origin(X, Y) where the attack originates in the corridor.</summary>
    Vector2D Origin,

    /// <summary>Real-world target coordinate P_target(X, Y), if available.</summary>
    Vector2D? TargetPosition,

    /// <summary>Spatial geometry type (SingleTarget, Cone, RadialAoE, ProjectileSpread).</summary>
    SkillGeometryType GeometryType,

    /// <summary>Primary elemental/physical damage type of the attack.</summary>
    DamageType DamageType,

    /// <summary>Effective reach or radius in pixels.</summary>
    float Range,

    /// <summary>True if this is a melee attack or close-quarters strike.</summary>
    bool IsMelee,

    /// <summary>True if this attack rolled a critical strike.</summary>
    bool IsCrit,

    /// <summary>Cone/arc angle in degrees for sweep or cleave attacks.</summary>
    float ConeAngle = 0f,

    /// <summary>Optional pre-computed projectile trajectory vectors for projectile skills.</summary>
    IReadOnlyList<ProjectileVector>? Projectiles = null,

    /// <summary>Number of pierces allowed for projectiles launched by this attack.</summary>
    int PierceCount = 0
);
