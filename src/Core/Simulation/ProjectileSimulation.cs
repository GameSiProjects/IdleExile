using System;
using System.Collections.Generic;
using IdleExile.Core.Models;
using IdleExile.Core.Services;
using IdleExile.Core.State;

namespace IdleExile.Core.Simulation;

/// <summary>
/// Continuous 2D Projectile Physics & Collision Simulation Service.
/// Implements real in-flight projectile movement, continuous swept-circle collision detection (CCD),
/// AoE detonation, pierce, chain, and Path of Exile's non-shotgunning volley registry.
/// Pure C# — zero Godot Node dependencies. Pre-allocated object pool for zero GC allocations.
/// </summary>
public sealed class ProjectileSimulation
{
    public const int MaxPoolSize = 64;

    private readonly SimulatedProjectile[] _pool = new SimulatedProjectile[MaxPoolSize];
    private readonly List<SimulatedProjectile> _activeProjectiles = new(MaxPoolSize);
    private readonly List<(MonsterInstance Monster, float HitT, Vector2D ImpactPoint)> _stepCollisions = new(16);
    private readonly Dictionary<int, HashSet<long>> _volleyHits = new(32);
    private readonly Queue<HashSet<long>> _hashSetPool = new(32);

    private long _nextProjectileId = 1;

    /// <summary>Fired when a new projectile is launched into the arena.</summary>
    public event Action<SimulatedProjectile>? OnProjectileSpawned;

    /// <summary>Fired when a projectile collides, detonates, or reaches terminal destination.</summary>
    public event Action<ProjectileImpactEventArgs>? OnProjectileImpacted;

    /// <summary>ReadOnly view of all currently in-flight active projectiles.</summary>
    public IReadOnlyList<SimulatedProjectile> ActiveProjectiles => _activeProjectiles;

    public ProjectileSimulation()
    {
        for (int i = 0; i < MaxPoolSize; i++)
        {
            _pool[i] = new SimulatedProjectile();
        }
        for (int i = 0; i < 32; i++)
        {
            _hashSetPool.Enqueue(new HashSet<long>(8));
        }
    }

    /// <summary>
    /// Spawns a physical in-flight projectile into the simulation.
    /// Reuses a pooled SimulatedProjectile instance.
    /// </summary>
    public SimulatedProjectile? SpawnProjectile(
        ICombatEntity source,
        ICombatEntity? primaryTarget,
        AttackPayload payload,
        Vector2D origin,
        Vector2D direction,
        float speed,
        float maxRange,
        float radius = 6.0f,
        float aoeRadius = 0.0f,
        Vector2D? targetDestination = null,
        int pierce = 0,
        int chain = 0,
        bool canShotgun = false,
        bool isDefaultAttack = false,
        int volleyId = 0)
    {
        // Find inactive slot in pool
        SimulatedProjectile? p = null;
        for (int i = 0; i < MaxPoolSize; i++)
        {
            if (!_pool[i].IsActive)
            {
                p = _pool[i];
                break;
            }
        }

        if (p == null)
        {
            // Pool exhausted under extreme load; drop projectile gracefully to protect frame rate
            return null;
        }

        p.Reset();
        p.Id = _nextProjectileId++;
        p.VolleyId = volleyId;
        p.Source = source;
        p.PrimaryTarget = primaryTarget;
        p.Payload = payload;
        p.Position = origin;
        p.PreviousPosition = origin;
        p.Speed = speed > 0f ? speed : 520.0f;
        Vector2D normDir = direction.Normalized();
        if (normDir == Vector2D.Zero) normDir = Vector2D.UnitX;
        p.Velocity = normDir * p.Speed;
        p.Radius = radius > 0f ? radius : 6.0f;
        p.MaxRange = maxRange > 0f ? maxRange : 220.0f;
        p.DistanceTraveled = 0f;
        p.AoeRadius = aoeRadius;
        p.TargetDestination = targetDestination;
        p.PierceRemaining = pierce;
        p.ChainRemaining = chain;
        p.CanShotgun = canShotgun;
        p.IsDefaultAttack = isDefaultAttack;
        p.IsActive = true;

        if (volleyId != 0 && !_volleyHits.ContainsKey(volleyId))
        {
            var set = _hashSetPool.Count > 0 ? _hashSetPool.Dequeue() : new HashSet<long>(8);
            set.Clear();
            _volleyHits[volleyId] = set;
        }

        _activeProjectiles.Add(p);
        OnProjectileSpawned?.Invoke(p);
        return p;
    }

    /// <summary>
    /// Advances all in-flight projectiles by dt, performs Continuous Collision Detection (CCD)
    /// against valid target hitboxes, resolves impacts/detonations, and cleans up expired projectiles.
    /// </summary>
    public void Tick(
        float dt,
        IReadOnlyList<MonsterInstance> monsters,
        PlayerState player,
        Action<SimulatedProjectile, MonsterInstance, Vector2D, bool> onMonsterHit,
        Action<SimulatedProjectile, PlayerState, Vector2D>? onPlayerHit = null)
    {
        if (_activeProjectiles.Count == 0)
            return;

        for (int i = _activeProjectiles.Count - 1; i >= 0; i--)
        {
            var p = _activeProjectiles[i];
            if (!p.IsActive)
            {
                _activeProjectiles.RemoveAt(i);
                continue;
            }

            // 1. Advance Trajectory
            p.PreviousPosition = p.Position;
            Vector2D step = p.Velocity * dt;
            float stepLength = step.Length;
            p.Position = p.Position + step;
            p.DistanceTraveled += stepLength;

            bool impactedThisTick = false;

            // 2. Continuous Collision Detection (CCD)
            if (p.Source is PlayerState)
            {
                _stepCollisions.Clear();

                // Test swept segment against all alive monsters
                for (int m = 0; m < monsters.Count; m++)
                {
                    var monster = monsters[m];
                    if (!monster.IsAlive) continue;

                    // Check pierce exclusion (already hit by this projectile)
                    if (p.HitEntityIds.Contains(monster.InstanceId)) continue;

                    float combinedRadius = monster.Radius + p.Radius;
                    if (SpatialCombatService.SegmentIntersectsCircle(
                        p.PreviousPosition,
                        p.Position,
                        monster.Position,
                        combinedRadius,
                        out float hitT,
                        out Vector2D impactPoint))
                    {
                        _stepCollisions.Add((monster, hitT, impactPoint));
                    }
                }

                if (_stepCollisions.Count > 0)
                {
                    // Sort by earliest intersection along segment (frontline monster shields backline)
                    _stepCollisions.Sort((a, b) => a.HitT.CompareTo(b.HitT));
                    var earliest = _stepCollisions[0];
                    Vector2D impactPoint = earliest.ImpactPoint;

                    if (p.AoeRadius > 0f)
                    {
                        // ═══ Area of Effect Detonation (e.g. Fireball) ═══
                        p.IsActive = false;
                        impactedThisTick = true;

                        var hitEntities = new List<ICombatEntity>();
                        for (int m = 0; m < monsters.Count; m++)
                        {
                            var target = monsters[m];
                            if (!target.IsAlive) continue;

                            if (SpatialCombatService.IsInRadialAoE(impactPoint, target.Position, p.AoeRadius, target.Radius))
                            {
                                bool canDealDamage = p.CanShotgun || p.VolleyId == 0 ||
                                    !_volleyHits.TryGetValue(p.VolleyId, out var vSet) ||
                                    !vSet.Contains(target.InstanceId);

                                if (canDealDamage)
                                {
                                    if (!p.CanShotgun && p.VolleyId != 0 && _volleyHits.TryGetValue(p.VolleyId, out var volleySet))
                                    {
                                        volleySet.Add(target.InstanceId);
                                    }
                                    hitEntities.Add(target);
                                    onMonsterHit(p, target, impactPoint, true);
                                }
                            }
                        }

                        OnProjectileImpacted?.Invoke(new ProjectileImpactEventArgs(
                            p.Id,
                            p.VolleyId,
                            p.Source,
                            impactPoint,
                            p.DamageType,
                            p.AoeRadius,
                            false,
                            hitEntities
                        ));
                    }
                    else
                    {
                        // ═══ Single Target Projectile Hit (e.g. Split Arrow, Default Attack) ═══
                        var target = earliest.Monster;
                        p.HitEntityIds.Add(target.InstanceId);

                        bool alreadyHitByVolley = !p.CanShotgun && p.VolleyId != 0 &&
                            _volleyHits.TryGetValue(p.VolleyId, out var vSet) &&
                            vSet.Contains(target.InstanceId);

                        if (!alreadyHitByVolley)
                        {
                            if (!p.CanShotgun && p.VolleyId != 0 && _volleyHits.TryGetValue(p.VolleyId, out var volleySet))
                            {
                                volleySet.Add(target.InstanceId);
                            }

                            onMonsterHit(p, target, impactPoint, false);
                        }

                        impactedThisTick = true;

                        OnProjectileImpacted?.Invoke(new ProjectileImpactEventArgs(
                            p.Id,
                            p.VolleyId,
                            p.Source,
                            impactPoint,
                            p.DamageType,
                            0f,
                            false,
                            alreadyHitByVolley ? Array.Empty<ICombatEntity>() : new[] { target }
                        ));

                        // Pierce Evaluation
                        if (p.PierceRemaining > 0)
                        {
                            p.PierceRemaining--;
                            // Projectile continues flying forward
                        }
                        // Chain Evaluation
                        else if (p.ChainRemaining > 0 && !alreadyHitByVolley)
                        {
                            p.ChainRemaining--;
                            var chainTarget = SpatialCombatService.FindChainTarget(
                                impactPoint,
                                target.InstanceId,
                                monsters,
                                SpatialCombatService.ChainSearchRadius,
                                p.HitEntityIds
                            );

                            if (chainTarget != null)
                            {
                                Vector2D chainDir = (chainTarget.Position - impactPoint).Normalized();
                                if (chainDir != Vector2D.Zero)
                                {
                                    p.Position = impactPoint;
                                    p.Velocity = chainDir * p.Speed;
                                }
                                else
                                {
                                    p.IsActive = false;
                                }
                            }
                            else
                            {
                                p.IsActive = false;
                            }
                        }
                        else
                        {
                            p.IsActive = false;
                        }
                    }
                }
            }
            else if (p.Source is MonsterInstance && player.IsAlive && onPlayerHit != null)
            {
                // Monster projectile targeting Player
                float combinedRadius = player.Radius + p.Radius;
                if (SpatialCombatService.SegmentIntersectsCircle(
                    p.PreviousPosition,
                    p.Position,
                    player.Position,
                    combinedRadius,
                    out _,
                    out Vector2D impactPoint))
                {
                    p.IsActive = false;
                    impactedThisTick = true;
                    onPlayerHit(p, player, impactPoint);

                    OnProjectileImpacted?.Invoke(new ProjectileImpactEventArgs(
                        p.Id,
                        p.VolleyId,
                        p.Source,
                        impactPoint,
                        p.DamageType,
                        p.AoeRadius,
                        false,
                        new[] { player }
                    ));
                }
            }

            // 3. Terminal Destination & Max Range Expiration
            if (p.IsActive && !impactedThisTick)
            {
                bool reachedDestination = p.TargetDestination.HasValue &&
                    p.Position.DistanceSquaredTo(p.TargetDestination.Value) <= (p.Speed * dt * p.Speed * dt + 16f);
                bool reachedMaxRange = p.DistanceTraveled >= p.MaxRange;

                if (p.AoeRadius > 0f && (reachedDestination || reachedMaxRange))
                {
                    // Terminal AoE Ground Detonation (e.g. Fireball reaching targeted spot or max range)
                    p.IsActive = false;
                    Vector2D detonatePoint = p.TargetDestination ?? p.Position;
                    var hitEntities = new List<ICombatEntity>();

                    for (int m = 0; m < monsters.Count; m++)
                    {
                        var target = monsters[m];
                        if (!target.IsAlive) continue;

                        if (SpatialCombatService.IsInRadialAoE(detonatePoint, target.Position, p.AoeRadius, target.Radius))
                        {
                            bool canDealDamage = p.CanShotgun || p.VolleyId == 0 ||
                                !_volleyHits.TryGetValue(p.VolleyId, out var vSet) ||
                                !vSet.Contains(target.InstanceId);

                            if (canDealDamage)
                            {
                                if (!p.CanShotgun && p.VolleyId != 0 && _volleyHits.TryGetValue(p.VolleyId, out var volleySet))
                                {
                                    volleySet.Add(target.InstanceId);
                                }
                                hitEntities.Add(target);
                                onMonsterHit(p, target, detonatePoint, true);
                            }
                        }
                    }

                    OnProjectileImpacted?.Invoke(new ProjectileImpactEventArgs(
                        p.Id,
                        p.VolleyId,
                        p.Source,
                        detonatePoint,
                        p.DamageType,
                        p.AoeRadius,
                        false,
                        hitEntities
                    ));
                }
                else if (reachedMaxRange)
                {
                    // Non-AoE projectile expires in thin air
                    p.IsActive = false;
                }
            }

            if (!p.IsActive)
            {
                _activeProjectiles.RemoveAt(i);
            }
        }

        // 4. Recycle unused Volley registries
        RecycleVolleys();
    }

    /// <summary>
    /// Resets and recycles volley tracking sets that have no more in-flight active projectiles.
    /// </summary>
    private void RecycleVolleys()
    {
        if (_volleyHits.Count == 0) return;

        var activeVolleyIds = new HashSet<int>();
        for (int i = 0; i < _activeProjectiles.Count; i++)
        {
            if (_activeProjectiles[i].VolleyId != 0)
            {
                activeVolleyIds.Add(_activeProjectiles[i].VolleyId);
            }
        }

        var deadVolleys = new List<int>();
        foreach (var kvp in _volleyHits)
        {
            if (!activeVolleyIds.Contains(kvp.Key))
            {
                deadVolleys.Add(kvp.Key);
            }
        }

        for (int i = 0; i < deadVolleys.Count; i++)
        {
            int deadId = deadVolleys[i];
            if (_volleyHits.Remove(deadId, out var set))
            {
                set.Clear();
                _hashSetPool.Enqueue(set);
            }
        }
    }

    /// <summary>
    /// Clears and deactivates all projectiles and recycled volley tracking.
    /// </summary>
    public void Clear()
    {
        for (int i = 0; i < MaxPoolSize; i++)
        {
            _pool[i].Reset();
        }
        _activeProjectiles.Clear();

        foreach (var set in _volleyHits.Values)
        {
            set.Clear();
            _hashSetPool.Enqueue(set);
        }
        _volleyHits.Clear();
    }
}
