using System.Collections.Generic;

namespace IdleExile.Core.Models;

/// <summary>
/// Event arguments emitted when an in-flight projectile physically collides with a target,
/// detonates in an AoE explosion, or reaches terminal destination.
/// </summary>
public sealed record ProjectileImpactEventArgs(
    long ProjectileId,
    int VolleyId,
    ICombatEntity Source,
    Vector2D ImpactPoint,
    DamageType DamageType,
    float AoeRadius,
    bool IsCrit,
    IReadOnlyList<ICombatEntity> HitEntities
);
