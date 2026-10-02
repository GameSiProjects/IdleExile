using System.Collections.Generic;

namespace IdleExile.Core.Models;

/// <summary>
/// Physical simulation state of an in-flight projectile in the 2D combat arena.
/// Represents real continuous movement, swept-circle collisions, and detonation rules.
/// Pure C# — zero Godot Node dependencies. Pre-allocated and pooled for zero GC allocations.
/// </summary>
public sealed class SimulatedProjectile
{
    /// <summary>Unique identifier for this specific projectile instance.</summary>
    public long Id { get; set; }

    /// <summary>
    /// Volley group ID. All projectiles spawned from the same attack/cast share the same VolleyId
    /// to enforce Path of Exile's non-shotgunning rule.
    /// </summary>
    public int VolleyId { get; set; }

    /// <summary>Attacking entity that fired the projectile (PlayerState or MonsterInstance).</summary>
    public ICombatEntity Source { get; set; } = null!;

    /// <summary>Original primary targeted entity, if any.</summary>
    public ICombatEntity? PrimaryTarget { get; set; }

    /// <summary>Calculated offensive damage payload and modifiers.</summary>
    public AttackPayload Payload { get; set; } = null!;

    /// <summary>Current world position in 2D space.</summary>
    public Vector2D Position { get; set; }

    /// <summary>Previous tick world position for swept-segment collision detection.</summary>
    public Vector2D PreviousPosition { get; set; }

    /// <summary>Current flight velocity vector in pixels per second.</summary>
    public Vector2D Velocity { get; set; }

    /// <summary>Scalar flight speed in pixels per second.</summary>
    public float Speed { get; set; }

    /// <summary>Physical collision radius in pixels (e.g. 6px for Fireball, 4px for arrows).</summary>
    public float Radius { get; set; }

    /// <summary>Maximum flight distance in pixels before expiration or ground detonation.</summary>
    public float MaxRange { get; set; }

    /// <summary>Total distance traveled so far in pixels.</summary>
    public float DistanceTraveled { get; set; }

    /// <summary>
    /// Area of Effect explosion radius upon collision or terminal detonation.
    /// 0 for single-target projectiles (arrows); > 0 for explosive projectiles (Fireball: 32px).
    /// </summary>
    public float AoeRadius { get; set; }

    /// <summary>Optional targeted ground destination coordinate (for ground-targeted skills).</summary>
    public Vector2D? TargetDestination { get; set; }

    /// <summary>Remaining enemies this projectile can pierce through before impact consumption.</summary>
    public int PierceRemaining { get; set; }

    /// <summary>Remaining chain bounces to nearby unhit targets.</summary>
    public int ChainRemaining { get; set; }

    /// <summary>
    /// If false (PoE standard for Split Arrow / multiple projectiles), an enemy can only be hit
    /// once per volley from direct projectile collisions.
    /// </summary>
    public bool CanShotgun { get; set; }

    /// <summary>True if this projectile was fired as a fallback Default Attack (restores mana on hit).</summary>
    public bool IsDefaultAttack { get; set; }

    /// <summary>True while in flight; false when inactive/pooled or consumed.</summary>
    public bool IsActive { get; set; }

    /// <summary>Track entity IDs already pierced or hit by this projectile to prevent double-hitting.</summary>
    public HashSet<long> HitEntityIds { get; } = new(8);

    /// <summary>Primary elemental or physical damage type of this projectile.</summary>
    public DamageType DamageType => Payload?.PrimaryDamageType ?? DamageType.Physical;

    /// <summary>
    /// Resets this pooled projectile instance for reuse without allocating heap memory.
    /// </summary>
    public void Reset()
    {
        Id = 0;
        VolleyId = 0;
        Source = null!;
        PrimaryTarget = null;
        Payload = null!;
        Position = Vector2D.Zero;
        PreviousPosition = Vector2D.Zero;
        Velocity = Vector2D.Zero;
        Speed = 0f;
        Radius = 6f;
        MaxRange = 220f;
        DistanceTraveled = 0f;
        AoeRadius = 0f;
        TargetDestination = null;
        PierceRemaining = 0;
        ChainRemaining = 0;
        CanShotgun = false;
        IsDefaultAttack = false;
        IsActive = false;
        HitEntityIds.Clear();
    }
}
