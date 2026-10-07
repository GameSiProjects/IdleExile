using System;
using System.Collections.Generic;
using System.Linq;
using IdleExile.Core.Models;
using IdleExile.Core.Services;
using IdleExile.Core.State;

namespace IdleExile.Core.Simulation;

/// <summary>
/// Machine state of the autonomous Zero-APM Exile movement controller.
/// Conforms to Section 2.5 of docs/12_master_implementation_roadmap.md and docs/03_combat_and_stats.md:
/// Marching -> Engaging (holding optimal range to unleash skills) -> Advancing.
/// </summary>
public enum ExileMovementState
{
    /// <summary>Stationary before a map begins or while combat is paused.</summary>
    Idle = 0,

    /// <summary>Marching steadily forward along the linear corridor searching for hostiles or next encounter.</summary>
    Marching = 1,

    /// <summary>Target acquired and engaged. Exile halts forward travel to maintain optimal combat range and fire skills.</summary>
    Engaging = 2,

    /// <summary>Current target or pack slain; Exile presses forward along the corridor to the next encounter.</summary>
    Advancing = 3,

    /// <summary>Crossing through the Fog Gate / Sealed Threshold at 85% of map length into the boss chamber.</summary>
    EnteringBossRoom = 4,

    /// <summary>Inside the boss chamber arena engaging the final Map Boss.</summary>
    InBossArena = 5,

    /// <summary>Character has fallen in combat; all movement is halted.</summary>
    Dead = 6,

    /// <summary>Map Boss defeated and corridor cleared; map run complete.</summary>
    Completed = 7
}

/// <summary>
/// Pure C# Real-World Movement Controller for the player character (Exile).
/// Implements Section 2.5 of docs/12_master_implementation_roadmap.md:
/// - World-space position P_E(t) = (X_world, Y_world) traversing along a linear corridor of length L_map.
/// - Speed scaling conforming to PoE formula: v_move = v_base * (1 + sum %Inc/100) * prod(1 + %More/100) * CC_mult.
/// - Autonomous Zero-APM State Machine: Marching -> Engaging (holding optimal skill range) -> Advancing.
/// - Fog Gate threshold (85% L_map) and Boss Arena entry detection.
/// Strictly decoupled from Godot.Node to allow headless testing and instant simulation.
/// </summary>
public sealed class ExileMovementController
{
    // ─────────────────────────────────────────────
    //  Canonical Corridor & Movement Constants
    // ─────────────────────────────────────────────

    /// <summary>Default base movement velocity in pixels per second (v_base).</summary>
    public const float DefaultBaseSpeed = 120.0f;

    /// <summary>Default linear corridor length L_map in world pixels (~1800 - 3000px, Docs 07.A).</summary>
    public const float DefaultMapLength = 2400.0f;

    /// <summary>Default corridor width W_corridor in world pixels (640px, Prompt 4).</summary>
    public const float DefaultCorridorWidth = 640.0f;

    /// <summary>Default corridor height in world pixels (208px).</summary>
    public const float DefaultCorridorHeight = 208.0f;

    /// <summary>Playable lane minimum Y boundary inside horizontal corridor.</summary>
    public const float MinCorridorY = 32.0f;

    /// <summary>Playable lane maximum Y boundary inside horizontal corridor.</summary>
    public const float MaxCorridorY = 176.0f;

    /// <summary>Corridor central lane Y coordinate.</summary>
    public const float DefaultCenterY = 104.0f;

    /// <summary>Corridor default starting X coordinate.</summary>
    public const float DefaultStartX = 40.0f;

    /// <summary>Playable lane minimum X boundary inside vertical corridor (legacy).</summary>
    public const float MinCorridorX = 40.0f;

    /// <summary>Playable lane maximum X boundary inside vertical corridor (legacy).</summary>
    public const float MaxCorridorX = 600.0f;

    /// <summary>Corridor central lane X coordinate (legacy).</summary>
    public const float DefaultCenterX = 320.0f;

    /// <summary>Fog Gate / Sealed Threshold progress ratio along corridor length (85%).</summary>
    public const float FogGateRatio = 0.85f;

    /// <summary>Boss Arena center focal point progress ratio along corridor length (93%).</summary>
    public const float BossArenaRatio = 0.93f;

    /// <summary>Default look-ahead perception range for threat scanning along the corridor.</summary>
    public const float DefaultPerceptionRange = 260.0f;

    // ─────────────────────────────────────────────
    //  State & Position Properties
    // ─────────────────────────────────────────────

    /// <summary>Player character runtime state reference.</summary>
    public PlayerState Player { get; }

    /// <summary>Skill setup reference for determining optimal combat engagement range.</summary>
    public SkillLoadout? Loadout { get; set; }

    /// <summary>Current autonomous movement state.</summary>
    public ExileMovementState CurrentState { get; private set; } = ExileMovementState.Idle;

    /// <summary>Current real-world coordinate P_E(t) = (X_world, Y_world) along the corridor.</summary>
    public Vector2D Position { get; set; } = new Vector2D(DefaultStartX, DefaultCenterY);

    /// <summary>Current directional velocity vector in pixels per second.</summary>
    public Vector2D Velocity { get; private set; } = Vector2D.Zero;

    /// <summary>Corridor forward marching axis (default: +X rightward along the corridor).</summary>
    public Vector2D ForwardDirection { get; set; } = new Vector2D(1f, 0f);

    /// <summary>Base movement speed in pixels per second before gear, flask, and passive multipliers.</summary>
    public float BaseSpeed { get; set; } = DefaultBaseSpeed;

    /// <summary>Calculated effective movement speed in pixels per second during the last tick.</summary>
    public float CurrentSpeed { get; private set; } = 0f;

    /// <summary>Total corridor length L_map for the current map.</summary>
    public float MapLength { get; private set; } = DefaultMapLength;

    /// <summary>Corridor width W_corridor.</summary>
    public float CorridorWidth { get; set; } = DefaultCorridorWidth;

    /// <summary>Look-ahead threat detection distance in pixels.</summary>
    public float PerceptionRange { get; set; } = DefaultPerceptionRange;

    /// <summary>Optional override for optimal combat engagement range in pixels.</summary>
    public float? OptimalRangeOverride { get; set; } = null;

    /// <summary>Whether autonomous movement processing is enabled.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Currently engaged monster instance, if any.</summary>
    public MonsterInstance? CurrentEngagedTarget { get; private set; }

    /// <summary>Dedicated speed modifier bucket for buffs/flasks (e.g. Quicksilver Flask).</summary>
    public StatModifierBucket SpeedModifiers { get; } = new();

    // ─────────────────────────────────────────────
    //  Corridor Progress Telemetry
    // ─────────────────────────────────────────────

    private Vector2D _startPosition = new(DefaultStartX, DefaultCenterY);
    private float _accumulatedTravelDistance = 0f;

    /// <summary>Total distance in pixels traveled from starting point.</summary>
    public float DistanceTraveled => MathF.Max(_accumulatedTravelDistance, Vector2D.Dot(Position - _startPosition, ForwardDirection.Normalized()));

    /// <summary>Linear progress distance along the forward corridor axis from starting point.</summary>
    public float CorridorProgressDistance => MathF.Max(0f, Vector2D.Dot(Position - _startPosition, ForwardDirection.Normalized()));

    /// <summary>Normalized progression ratio along the corridor [0.0, 1.0].</summary>
    public float MapProgressRatio => MapLength > 0f ? Math.Clamp(DistanceTraveled / MapLength, 0f, 1f) : 0f;

    /// <summary>Corridor progression as percentage (0% to 100%).</summary>
    public float MapProgressPercent => MapProgressRatio * 100.0f;

    /// <summary>World distance to the Fog Gate threshold (85% of map length).</summary>
    public float FogGateDistance => MapLength * FogGateRatio;

    /// <summary>World distance to the center of the Boss Arena chamber (93% of map length).</summary>
    public float BossArenaDistance => MapLength * BossArenaRatio;

    /// <summary>True once the Exile has reached or crossed the Fog Gate threshold.</summary>
    public bool HasReachedFogGate { get; private set; } = false;

    /// <summary>True once the Fog Gate threshold has been crossed and sealed behind the Exile (Task 2.8).</summary>
    public bool IsFogGateSealed { get; private set; } = false;

    /// <summary>True once the Exile has arrived at the center of the Boss Arena.</summary>
    public bool HasEnteredBossRoom { get; private set; } = false;

    // ─────────────────────────────────────────────
    //  Events
    // ─────────────────────────────────────────────

    /// <summary>Fired when the movement state transitions. Args: (oldState, newState).</summary>
    public event Action<ExileMovementState, ExileMovementState>? OnStateChanged;

    /// <summary>Fired when the real-world position updates. Arg: newPosition.</summary>
    public event Action<Vector2D>? OnPositionChanged;

    /// <summary>Fired when the velocity vector changes. Arg: newVelocity.</summary>
    public event Action<Vector2D>? OnVelocityChanged;

    /// <summary>Fired when the Exile crosses the Fog Gate threshold at 85% map progress.</summary>
    public event Action? OnFogGateCrossed;

    /// <summary>Fired when the Fog Gate is sealed shut behind the Exile (Task 2.8).</summary>
    public event Action? OnFogGateSealed;

    /// <summary>Fired when the Exile steps into the center of the Boss Arena chamber.</summary>
    public event Action? OnBossArenaEntered;

    /// <summary>Fired when all alive monsters in an engaged pack are cleared.</summary>
    public event Action? OnPackCleared;

    // ─────────────────────────────────────────────
    //  Constructor
    // ─────────────────────────────────────────────

    public ExileMovementController(
        PlayerState player,
        SkillLoadout? loadout = null,
        float mapLength = DefaultMapLength,
        float baseSpeed = DefaultBaseSpeed,
        Vector2D? forwardDirection = null)
    {
        Player = player ?? throw new ArgumentNullException(nameof(player));
        Loadout = loadout;
        MapLength = mapLength > 0f ? mapLength : DefaultMapLength;
        BaseSpeed = baseSpeed > 0f ? baseSpeed : DefaultBaseSpeed;
        ForwardDirection = (forwardDirection ?? new Vector2D(1f, 0f)).Normalized();

        _startPosition = player.Position;
        Position = player.Position;
    }

    // ─────────────────────────────────────────────
    //  Speed Scaling Calculations (PoE Precision)
    // ─────────────────────────────────────────────

    /// <summary>
    /// Computes the effective movement speed in pixels per second according to docs/03 section 4.1:
    /// v_move = v_base * (1 + sum %Inc / 100 - sum %Red / 100) * prod(1 + %More / 100) * prod(1 - %Less / 100) * CC_mult.
    /// Incorporates gear modifiers (boots implicit/explicit), passive tree, active ailments, and dedicated speed bucket.
    /// </summary>
    public float CalculateCurrentSpeed(
        PlayerState player,
        StatModifierBucket? gearStats = null,
        StatModifierBucket? passiveStats = null)
    {
        if (!player.IsAlive)
            return 0f;

        // 1. Crowd control & action speed multipliers
        float ccSpeedMult = 1.0f;
        if (player.Ailments.ActiveAilments.Any(a => a.Type == AilmentType.Freeze))
        {
            return 0f; // Frozen: completely immobile
        }

        if (player.Ailments.ActiveAilments.FirstOrDefault(a => a.Type == AilmentType.Chill) is { } chill)
        {
            ccSpeedMult = MathF.Max(0.1f, 1.0f - chill.EffectPercent / 100f);
        }

        // 2. Additive % Increased / Reduced Movement Speed
        float increasedSum = SpeedModifiers.IncreasedSum;
        float reducedSum = SpeedModifiers.ReducedSum;

        // Gear bonus (e.g. boots +3% implicit, rolled affixes)
        increasedSum += player.TotalMovementSpeedBonus;

        if (gearStats != null)
        {
            increasedSum += gearStats.IncreasedSum;
            reducedSum += gearStats.ReducedSum;
        }

        if (passiveStats != null)
        {
            increasedSum += passiveStats.IncreasedSum;
            reducedSum += passiveStats.ReducedSum;
        }

        float additiveFactor = MathF.Max(0f, 1.0f + (increasedSum - reducedSum) / 100f);

        // 3. Multiplicative % More / Less Movement Speed
        float moreFactor = SpeedModifiers.MoreProduct;
        float lessFactor = SpeedModifiers.LessProduct;

        if (gearStats != null)
        {
            moreFactor *= gearStats.MoreProduct;
            lessFactor *= gearStats.LessProduct;
        }

        if (passiveStats != null)
        {
            moreFactor *= passiveStats.MoreProduct;
            lessFactor *= passiveStats.LessProduct;
        }

        float effectiveSpeed = BaseSpeed * additiveFactor * moreFactor * lessFactor * ccSpeedMult;
        return MathF.Max(0f, effectiveSpeed);
    }

    // ─────────────────────────────────────────────
    //  Optimal Range Determination
    // ─────────────────────────────────────────────

    /// <summary>
    /// Evaluates the optimal combat engagement range in pixels based on the active primary skill geometry.
    /// - Melee skills: 35px (contact range).
    /// - Cones & frontal sweeps: 55-70px.
    /// - Radial AoE: 65-70px.
    /// - Multi-vector Projectiles & Ranged: 160-180px.
    /// </summary>
    public float GetOptimalEngagementRange()
    {
        if (OptimalRangeOverride.HasValue)
            return OptimalRangeOverride.Value;

        if (Loadout?.PrimarySkill?.ActiveGem != null)
        {
            var primary = Loadout.PrimarySkill;
            bool isMelee = primary.ActiveGem.Tags != null && primary.ActiveGem.Tags.Any(t => string.Equals(t, "Melee", StringComparison.OrdinalIgnoreCase));
            var geometry = primary.GetGeometryType();

            if (isMelee)
            {
                return SpatialCombatService.MeleeRange; // 35px
            }

            // Projectile skills (Fireball, Spark, Split Arrow) maintain ranged spacing
            if (primary.IsProjectile)
            {
                return 165.0f;
            }

            return geometry switch
            {
                SkillGeometryType.Cone => MathF.Min(primary.GetBaseRadiusOrRange() * 0.8f, 65.0f),
                SkillGeometryType.RadialAoE => MathF.Min(primary.GetBaseRadiusOrRange() * 0.9f, 70.0f),
                SkillGeometryType.ProjectileSpread => 165.0f,
                _ => 160.0f
            };
        }

        // Fallback based on equipped weapon class
        if (Player.MainHand != null)
        {
            return Player.MainHand.BaseDefinition.WeaponClass switch
            {
                WeaponClass.Bow or WeaponClass.Wand or WeaponClass.Staff => 165.0f,
                _ => SpatialCombatService.MeleeRange
            };
        }

        return SpatialCombatService.MeleeRange;
    }

    // ─────────────────────────────────────────────
    //  Map Lifecycle Control
    // ─────────────────────────────────────────────

    /// <summary>
    /// Starts a new map run along the corridor of specified length, setting initial position and Marching state.
    /// </summary>
    public void StartMap(float mapLength = DefaultMapLength, Vector2D? startPosition = null)
    {
        MapLength = mapLength > 0f ? mapLength : DefaultMapLength;
        _startPosition = startPosition ?? new Vector2D(DefaultStartX, DefaultCenterY);
        Position = _startPosition;
        _accumulatedTravelDistance = 0f;
        Velocity = Vector2D.Zero;
        CurrentSpeed = 0f;
        HasReachedFogGate = false;
        IsFogGateSealed = false;
        HasEnteredBossRoom = false;
        CurrentEngagedTarget = null;

        SetState(ExileMovementState.Marching);
        Player.Position = Position;
        OnPositionChanged?.Invoke(Position);
    }

    /// <summary>
    /// Resets the movement controller to default idle state.
    /// </summary>
    public void Reset(Vector2D? position = null, float? mapLength = null)
    {
        if (mapLength.HasValue && mapLength.Value > 0f)
        {
            MapLength = mapLength.Value;
        }

        _startPosition = position ?? new Vector2D(DefaultStartX, DefaultCenterY);
        Position = _startPosition;
        _accumulatedTravelDistance = 0f;
        Velocity = Vector2D.Zero;
        HasReachedFogGate = false;
        IsFogGateSealed = false;
        HasEnteredBossRoom = false;
        CurrentEngagedTarget = null;
        SetState(ExileMovementState.Idle);

        Player.Position = Position;
    }

    /// <summary>
    /// Immediately halts movement (e.g. paused or out of combat).
    /// </summary>
    public void Stop()
    {
        Velocity = Vector2D.Zero;
        CurrentSpeed = 0f;
        if (CurrentState == ExileMovementState.Marching || CurrentState == ExileMovementState.Advancing)
        {
            SetState(ExileMovementState.Idle);
        }
    }

    /// <summary>
    /// Sets state to Dead when player dies.
    /// </summary>
    public void SetDead()
    {
        Velocity = Vector2D.Zero;
        CurrentSpeed = 0f;
        CurrentEngagedTarget = null;
        SetState(ExileMovementState.Dead);
    }

    /// <summary>
    /// Restores state to Marching/Advancing when player respawns.
    /// </summary>
    public void SetAlive()
    {
        if (CurrentState == ExileMovementState.Dead)
        {
            SetState(ExileMovementState.Marching);
        }
    }

    /// <summary>
    /// Sets state to Completed when map boss is slain.
    /// </summary>
    public void SetCompleted()
    {
        Velocity = Vector2D.Zero;
        CurrentSpeed = 0f;
        CurrentEngagedTarget = null;
        SetState(ExileMovementState.Completed);
    }

    /// <summary>
    /// Explicitly seals the Fog Gate behind the Exile, locking them inside the boss chamber (Task 2.8).
    /// </summary>
    public void SealFogGate()
    {
        if (!IsFogGateSealed)
        {
            IsFogGateSealed = true;
            OnFogGateSealed?.Invoke();
        }
    }

    /// <summary>
    /// Explicitly notifies the movement controller that the currently engaged monster pack has been slain.
    /// Triggers immediate transition from Engaging to Advancing.
    /// </summary>
    public void NotifyPackCleared()
    {
        CurrentEngagedTarget = null;
        if (CurrentState == ExileMovementState.Engaging)
        {
            OnPackCleared?.Invoke();
            SetState(ExileMovementState.Advancing);
        }
    }

    // ─────────────────────────────────────────────
    //  30Hz Discrete Simulation Tick (Autonomous Zero-APM State Machine)
    // ─────────────────────────────────────────────

    /// <summary>
    /// Advances the movement simulation by fixed deltaTime.
    /// Evaluates current speed, scans for monsters along corridor, transitions movement states, and updates world coordinates.
    /// </summary>
    public void Tick(
        float deltaTime,
        IReadOnlyList<MonsterInstance> monsters,
        PlayerState player,
        StatModifierBucket? gearStats = null,
        StatModifierBucket? passiveStats = null)
    {
        if (!IsEnabled || deltaTime <= 0f)
            return;

        // 1. Guard Death & Completed states
        if (!player.IsAlive || CurrentState == ExileMovementState.Dead)
        {
            if (CurrentState != ExileMovementState.Dead)
            {
                SetDead();
            }
            Velocity = Vector2D.Zero;
            CurrentSpeed = 0f;
            return;
        }

        if (CurrentState == ExileMovementState.Completed || CurrentState == ExileMovementState.Idle)
        {
            Velocity = Vector2D.Zero;
            CurrentSpeed = 0f;
            return;
        }

        // 2. Compute effective movement speed
        CurrentSpeed = CalculateCurrentSpeed(player, gearStats, passiveStats);
        float optimalRange = GetOptimalEngagementRange();

        // 3. Scan alive monsters in perception range
        var aliveMonsters = monsters != null ? monsters.Where(m => m.IsAlive).ToList() : new List<MonsterInstance>();

        // Check if current engaged target is still alive
        if (CurrentEngagedTarget != null && !CurrentEngagedTarget.IsAlive)
        {
            CurrentEngagedTarget = null;
        }

        // Pick priority target if we don't have an alive target
        if (CurrentEngagedTarget == null && aliveMonsters.Count > 0)
        {
            CurrentEngagedTarget = SelectThreatTarget(aliveMonsters);
        }

        // 4. Autonomous State Machine Execution
        switch (CurrentState)
        {
            case ExileMovementState.Marching:
            case ExileMovementState.Advancing:
                ExecuteMarchingOrAdvancing(deltaTime, CurrentSpeed, optimalRange, aliveMonsters);
                break;

            case ExileMovementState.Engaging:
                ExecuteEngaging(deltaTime, CurrentSpeed, optimalRange, aliveMonsters);
                break;

            case ExileMovementState.EnteringBossRoom:
                ExecuteEnteringBossRoom(deltaTime, CurrentSpeed);
                break;

            case ExileMovementState.InBossArena:
                ExecuteInBossArena(deltaTime, CurrentSpeed, optimalRange, aliveMonsters);
                break;
        }

        // 5. Check Fog Gate crossing
        float distAlongForward = DistanceTraveled;
        if (!HasReachedFogGate && distAlongForward >= FogGateDistance)
        {
            HasReachedFogGate = true;
            IsFogGateSealed = true;
            OnFogGateCrossed?.Invoke();
            OnFogGateSealed?.Invoke();
            if (CurrentState != ExileMovementState.InBossArena && CurrentState != ExileMovementState.EnteringBossRoom)
            {
                SetState(ExileMovementState.EnteringBossRoom);
            }
        }

        // 6. Clamp Position inside corridor boundaries
        ClampPositionToCorridor();

        // 7. Synchronize back to player entity
        Player.Position = Position;
    }

    // ─────────────────────────────────────────────
    //  State Handlers
    // ─────────────────────────────────────────────

    private void ExecuteMarchingOrAdvancing(
        float deltaTime,
        float speed,
        float optimalRange,
        IReadOnlyList<MonsterInstance> aliveMonsters)
    {
        if (CurrentEngagedTarget != null)
        {
            float distToTarget = Position.DistanceTo(CurrentEngagedTarget.Position);

            if (distToTarget <= optimalRange)
            {
                // In range! Transition to Engaging and halt
                Velocity = Vector2D.Zero;
                OnVelocityChanged?.Invoke(Velocity);
                SetState(ExileMovementState.Engaging);
                return;
            }

            // Target is ahead: advance towards it to close into optimal range
            if (speed > 0f)
            {
                Vector2D toTarget = (CurrentEngagedTarget.Position - Position).Normalized();
                float step = MathF.Min(speed * deltaTime, distToTarget - optimalRange);
                if (step < 0f) step = speed * deltaTime;

                Velocity = toTarget * speed;
                Vector2D stepVector = toTarget * step;
                Position += stepVector;
                _accumulatedTravelDistance += stepVector.Length;
                OnVelocityChanged?.Invoke(Velocity);
                OnPositionChanged?.Invoke(Position);

                // If step brought Exile into optimal range, transition to Engaging immediately
                float newDist = Position.DistanceTo(CurrentEngagedTarget.Position);
                if (newDist <= optimalRange + 0.01f)
                {
                    Velocity = Vector2D.Zero;
                    OnVelocityChanged?.Invoke(Velocity);
                    SetState(ExileMovementState.Engaging);
                }
            }
            return;
        }

        // No monsters detected: advance along forward corridor axis
        if (speed > 0f)
        {
            // Soft lane centering towards corridor central axis
            Vector2D lateralDir;
            if (ForwardDirection.X > 0.5f)
            {
                float lateralCorrection = (DefaultCenterY - Position.Y) * 0.5f * deltaTime;
                lateralDir = new Vector2D(0f, lateralCorrection);
            }
            else
            {
                float lateralCorrection = (DefaultCenterX - Position.X) * 0.5f * deltaTime;
                lateralDir = new Vector2D(lateralCorrection, 0f);
            }

            Velocity = ForwardDirection * speed;
            Vector2D stepVector = ForwardDirection * (speed * deltaTime) + lateralDir;
            Position += stepVector;
            _accumulatedTravelDistance += stepVector.Length;

            OnVelocityChanged?.Invoke(Velocity);
            OnPositionChanged?.Invoke(Position);
        }
        else
        {
            Velocity = Vector2D.Zero;
        }

        // If Advancing without an engaged target, transition back to regular Marching
        if (CurrentState == ExileMovementState.Advancing && CurrentEngagedTarget == null)
        {
            SetState(ExileMovementState.Marching);
        }
    }

    private void ExecuteEngaging(
        float deltaTime,
        float speed,
        float optimalRange,
        IReadOnlyList<MonsterInstance> aliveMonsters)
    {
        if (CurrentEngagedTarget == null || !CurrentEngagedTarget.IsAlive)
        {
            // Pick next target if available
            CurrentEngagedTarget = SelectThreatTarget(aliveMonsters);

            if (CurrentEngagedTarget == null)
            {
                // All targets cleared: pack is slain!
                NotifyPackCleared();
                return;
            }
        }

        float dist = Position.DistanceTo(CurrentEngagedTarget.Position);

        // Allow a small hysteresis tolerance (+20px) before disengaging to chase
        if (dist > optimalRange + 20.0f)
        {
            // Monster has moved away or is out of reach; transition to Advancing to close in
            SetState(ExileMovementState.Advancing);
            return;
        }

        // Within optimal range: hold ground
        Velocity = Vector2D.Zero;
    }

    private void ExecuteEnteringBossRoom(float deltaTime, float speed)
    {
        // Destination is center of the Boss Arena chamber
        Vector2D bossArenaCenter = _startPosition + ForwardDirection * BossArenaDistance;
        float distToArena = Position.DistanceTo(bossArenaCenter);

        if (distToArena <= 25.0f)
        {
            // Arrived in boss room!
            HasEnteredBossRoom = true;
            Velocity = Vector2D.Zero;
            OnBossArenaEntered?.Invoke();
            SetState(ExileMovementState.InBossArena);
            return;
        }

        if (speed > 0f)
        {
            Vector2D toCenter = (bossArenaCenter - Position).Normalized();
            Velocity = toCenter * speed;
            Vector2D stepVector = toCenter * (speed * deltaTime);
            Position += stepVector;
            _accumulatedTravelDistance += stepVector.Length;

            OnVelocityChanged?.Invoke(Velocity);
            OnPositionChanged?.Invoke(Position);
        }
    }

    private void ExecuteInBossArena(
        float deltaTime,
        float speed,
        float optimalRange,
        IReadOnlyList<MonsterInstance> aliveMonsters)
    {
        if (CurrentEngagedTarget == null || !CurrentEngagedTarget.IsAlive)
        {
            // Focus boss first if available
            CurrentEngagedTarget = aliveMonsters.FirstOrDefault(m => m.Rarity == MonsterRarity.Boss)
                                   ?? SelectThreatTarget(aliveMonsters);
        }

        if (CurrentEngagedTarget != null)
        {
            float dist = Position.DistanceTo(CurrentEngagedTarget.Position);
            if (dist > optimalRange + 15.0f && speed > 0f)
            {
                // Close distance towards boss inside arena
                Vector2D toTarget = (CurrentEngagedTarget.Position - Position).Normalized();
                Velocity = toTarget * speed;
                Vector2D stepVector = toTarget * (speed * deltaTime);
                Position += stepVector;
                _accumulatedTravelDistance += stepVector.Length;
                OnVelocityChanged?.Invoke(Velocity);
                OnPositionChanged?.Invoke(Position);
            }
            else
            {
                Velocity = Vector2D.Zero;
            }
        }
        else
        {
            Velocity = Vector2D.Zero;
        }
    }

    // ─────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────

    private MonsterInstance? SelectThreatTarget(IReadOnlyList<MonsterInstance> candidates)
    {
        if (candidates.Count == 0) return null;

        // Filter to candidates that are aggroed or within perception range
        var inRange = candidates
            .Where(m => m.IsAlive && (m.IsAggroed || Position.DistanceTo(m.Position) <= PerceptionRange))
            .ToList();

        if (inRange.Count == 0) return null;

        // Prioritize aggroed enemies first, then Boss > Rare > Magic > Closest
        return inRange
            .OrderByDescending(m => m.IsAggroed ? 1 : 0)
            .ThenByDescending(m => m.Rarity switch
            {
                MonsterRarity.Boss => 1000f,
                MonsterRarity.Rare => 500f,
                MonsterRarity.Magic => 200f,
                _ => 100f
            })
            .ThenBy(m => Position.DistanceSquaredTo(m.Position))
            .FirstOrDefault();
    }

    private void SetState(ExileMovementState newState)
    {
        if (CurrentState == newState)
            return;

        var oldState = CurrentState;
        CurrentState = newState;
        OnStateChanged?.Invoke(oldState, newState);
    }

    private void ClampPositionToCorridor()
    {
        float clampedX = Position.X;
        float clampedY = Position.Y;

        // If moving along +X, clamp Y to horizontal corridor lane, and X between start and MapLength
        if (ForwardDirection.X > 0.5f)
        {
            clampedY = Math.Clamp(Position.Y, MinCorridorY, MaxCorridorY);
            float minX = IsFogGateSealed ? (_startPosition.X + FogGateDistance) : _startPosition.X;
            clampedX = Math.Clamp(Position.X, minX, _startPosition.X + MapLength);
        }
        else
        {
            clampedX = Math.Clamp(Position.X, MinCorridorX, MaxCorridorX);
            if (ForwardDirection.Y > 0.5f)
            {
                float minY = IsFogGateSealed ? (_startPosition.Y + FogGateDistance) : _startPosition.Y;
                clampedY = Math.Clamp(Position.Y, minY, _startPosition.Y + MapLength);
            }
        }

        if (MathF.Abs(clampedX - Position.X) > 0.001f || MathF.Abs(clampedY - Position.Y) > 0.001f)
        {
            Position = new Vector2D(clampedX, clampedY);
        }
    }
}
