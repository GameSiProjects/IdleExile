using System;
using System.Collections.Generic;
using System.Linq;
using IdleExile.Core.Models;
using IdleExile.Core.Services;
using IdleExile.Core.State;

namespace IdleExile.Core.Simulation;

/// <summary>
/// Master 30Hz (~33.33ms fixed tick) Combat Simulation Engine for Idle Exile.
/// Faithful to Path of Exile's server tick order and mathematical architecture.
///
/// Executes the 5 standard PoE simulation phases per tick:
///   1. Regen & DoT Phase (Player Life/Mana regen, ES recharge, Ailment damage-over-time on all entities).
///   2. Timers & Buffs Phase (Guard Skill duration/cooldown, Flask duration/auto-instill, Death respawn timer).
///   3. Player Action Phase (APS action time accumulation, mana consumption, targeting, RollHit, damage reflect).
///   4. Monster Action Phase (Monster APS accumulation, damage rolls, map extra elemental modifiers, Boss slam telegraph, defensive layers).
///   5. Death & Drop Phase (Monster slay evaluation, EXP grant, flask charges, seeded drop evaluation, wave progression).
///
/// Provides C# events:
///   - <see cref="OnTickProcessed"/>
///   - <see cref="OnDamageDealt"/>
///   - <see cref="OnPlayerDamaged"/>
///   - <see cref="OnMonsterKilled"/>
///   - <see cref="OnLootDropped"/>
///   - <see cref="OnPlayerDied"/>
///
/// Pure C# — zero Godot Node dependencies!
/// </summary>
public sealed class CombatEngine
{
    private readonly Random _random;
    private readonly ActionTimeAccumulator _playerActionAccumulator = new();
    private readonly Dictionary<long, ActionTimeAccumulator> _monsterActionAccumulators = new();
    private readonly Dictionary<long, AilmentTracker> _monsterAilments = new();
    private readonly HashSet<long> _processedDeadMonsters = new();

    // ─────────────────────────────────────────────
    //  Subsystems & State
    // ─────────────────────────────────────────────

    /// <summary>Player character state (pools, level, EXP, inventory).</summary>
    public PlayerState Player { get; }

    /// <summary>Skill setup, guard skill, and 5 flask slots.</summary>
    public SkillLoadout Loadout { get; }

    /// <summary>Wave generator scaling monster packs by tier and map modifiers.</summary>
    public WaveSpawner Spawner { get; }

    /// <summary>Autonomous target selection strategy.</summary>
    public TargetingStrategy Targeting { get; }

    /// <summary>Death penalties, recap metrics, auto-respawn timer, and portal escape.</summary>
    public DeathHandler Death { get; }

    /// <summary>Seeded drop coordinator for FTUE and Tier 0 map sustain.</summary>
    public SeededDropCoordinator DropCoordinator { get; }

    /// <summary>30Hz discrete simulation tick manager.</summary>
    public TickManager TickManager { get; }

    /// <summary>Autonomous Zero-APM real-world corridor movement controller (Task 2.5).</summary>
    public ExileMovementController Movement { get; }

    /// <summary>Passive skill tree modifier bucket.</summary>
    public StatModifierBucket? PassiveStats { get; set; }

    /// <summary>Equipment modifier bucket.</summary>
    public StatModifierBucket? GearStats { get; set; }

    /// <summary>Ailment tracker for debuffs and DoTs currently on the player.</summary>
    public AilmentTracker PlayerAilments => Player.Ailments;

    // ─────────────────────────────────────────────
    //  Map Run Context
    // ─────────────────────────────────────────────

    /// <summary>Active map run configuration (MapDefinition + rolled MapModifiers).</summary>
    public MapRunConfig? CurrentConfig { get; private set; }

    /// <summary>Active runtime map modifier effects (defensive penalties, extra monster damage, reflect).</summary>
    public MapModifierRuntime? MapModifiers { get; private set; }

    /// <summary>Currently engaged monster wave.</summary>
    public MonsterWave? CurrentWave { get; private set; }

    /// <summary>Current primary focus target.</summary>
    public MonsterInstance? CurrentTarget { get; private set; }

    /// <summary>True while an active map encounter is executing.</summary>
    public bool IsInCombat { get; private set; } = false;

    /// <summary>
    /// True when the player is actively engaged in combat with an enemy in reach or when nearby enemies are aggroed.
    /// When false, the Exile is out of combat and automatically marches along the corridor towards the end of the map.
    /// </summary>
    public bool IsEngagedInCombat => IsInCombat && (Movement.CurrentState == ExileMovementState.Engaging || CurrentTarget != null || (CurrentWave?.Alive.Any(m => m.IsAggroed) == true));

    /// <summary>True once all waves including the boss wave have been defeated.</summary>
    public bool IsMapComplete { get; private set; } = false;

    /// <summary>Monsters killed during the current active map run.</summary>
    public int TotalMonstersKilledInMap { get; private set; } = 0;

    /// <summary>Elapsed simulation time in seconds for the current active map run.</summary>
    public float CurrentMapRunTime { get; private set; } = 0f;

    /// <summary>
    /// Current effective player defensive stats, incorporating any map modifier penalties (e.g. -Max Res).
    /// </summary>
    public DefensiveStats EffectiveDefenses => MapModifiers != null
        ? MapModifiers.ApplyToDefensiveStats(Player.Defenses)
        : Player.Defenses;

    // ─────────────────────────────────────────────
    //  Core C# Events (Task 1.5 Contract)
    // ─────────────────────────────────────────────

    /// <summary>Fired at the conclusion of each 30Hz simulation tick.</summary>
    public event Action<TickProcessedEventArgs>? OnTickProcessed;

    /// <summary>Fired when damage is dealt to a monster (hit or DoT).</summary>
    public event Action<DamageDealtEventArgs>? OnDamageDealt;

    /// <summary>Fired when the player takes damage (monster attack, spell, reflect, or DoT).</summary>
    public event Action<PlayerDamagedEventArgs>? OnPlayerDamaged;

    /// <summary>Fired when a monster is slain.</summary>
    public event Action<MonsterKilledEventArgs>? OnMonsterKilled;

    /// <summary>Fired for each item dropped.</summary>
    public event Action<LootDrop>? OnLootDropped;

    /// <summary>Fired when the player suffers a fatal blow.</summary>
    public event Action<DeathRecap>? OnPlayerDied;

    // ─────────────────────────────────────────────
    //  Supplemental Combat Events
    // ─────────────────────────────────────────────

    /// <summary>Fired when a batch of items drops simultaneously (e.g. boss explosion).</summary>
    public event Action<IReadOnlyList<LootDrop>>? OnLootBatchDropped;

    /// <summary>Fired when a new monster wave enters the arena.</summary>
    public event Action<MonsterWave>? OnWaveSpawned;

    /// <summary>Fired when the final boss wave spawns.</summary>
    public event Action<MonsterWave>? OnBossWaveSpawned;

    /// <summary>Fired when an active map run begins.</summary>
    public event Action<MapRunConfig>? OnMapStarted;

    /// <summary>Fired when an active map run is fully cleared.</summary>
    public event Action<MapCompletedEventArgs>? OnMapCompleted;

    /// <summary>Fired when the player successfully auto-respawns after the 2s delay.</summary>
    public event Action? OnPlayerRespawned;

    /// <summary>Fired when a Boss winds up a heavy slam telegraph attack (>70% player EHP).</summary>
    public event Action<BossSlamTelegraphEventArgs>? OnBossSlamTelegraphed;

    /// <summary>Fired when the player attacks or casts an active skill with 2D spatial trajectory metadata.</summary>
    public event Action<PlayerSkillFiredEventArgs>? OnPlayerSkillFired;

    /// <summary>Fired when a monster executes a melee attack, providing 2D spatial data for the AoE strike indicator.</summary>
    public event Action<MonsterAttackEventArgs>? OnMonsterAttacked;

    /// <summary>Fired when the Exile's autonomous movement state changes.</summary>
    public event Action<ExileMovementState, ExileMovementState>? OnMovementStateChanged;

    /// <summary>Fired when the Exile moves along the corridor.</summary>
    public event Action<Vector2D>? OnExileMoved;

    /// <summary>Fired when the Exile crosses the Fog Gate threshold at 85% of map length.</summary>
    public event Action? OnFogGateCrossed;

    /// <summary>Fired when the Exile arrives in the center of the Boss Arena.</summary>
    public event Action? OnBossArenaEntered;

    /// <summary>Fired when a roadside League Encounter is triggered along the corridor (Task 2.7).</summary>
    public event Action<LeagueEncounterEventArgs>? OnLeagueEncounterTriggered;

    /// <summary>Fired when a roadside League Encounter is completed (Task 2.7).</summary>
    public event Action<LeagueEncounterEventArgs>? OnLeagueEncounterCompleted;

    /// <summary>Active structured corridor layout for the current map run (Task 2.7).</summary>
    public CorridorMonsterLayout? CurrentLayout => Spawner.CurrentLayout;

    /// <summary>Current active corridor pack metadata and spatial formation (Task 2.7).</summary>
    public CorridorPack? CurrentPack => CurrentWave?.Pack;

    /// <summary>2D spatial position of the Exile within the world corridor.</summary>
    public Vector2D ExilePosition
    {
        get => Movement?.Position ?? Player.Position;
        set
        {
            Player.Position = value;
            if (Movement != null)
            {
                Movement.Position = value;
            }
        }
    }

    // ─────────────────────────────────────────────
    //  Constructor
    // ─────────────────────────────────────────────

    public CombatEngine(
        PlayerState player,
        SkillLoadout loadout,
        WaveSpawner? spawner = null,
        TargetingStrategy? targeting = null,
        DeathHandler? death = null,
        SeededDropCoordinator? dropCoordinator = null,
        TickManager? tickManager = null,
        Random? random = null)
    {
        Player = player ?? throw new ArgumentNullException(nameof(player));
        Loadout = loadout ?? throw new ArgumentNullException(nameof(loadout));
        Spawner = spawner ?? new WaveSpawner();
        Targeting = targeting ?? new TargetingStrategy();
        Death = death ?? new DeathHandler();
        DropCoordinator = dropCoordinator ?? new SeededDropCoordinator();
        TickManager = tickManager ?? new TickManager();
        _random = random ?? Random.Shared;

        Movement = new ExileMovementController(Player, Loadout);
        Movement.OnStateChanged += (oldS, newS) => OnMovementStateChanged?.Invoke(oldS, newS);
        Movement.OnPositionChanged += pos => OnExileMoved?.Invoke(pos);
        Movement.OnFogGateCrossed += () => OnFogGateCrossed?.Invoke();
        Movement.OnBossArenaEntered += () => OnBossArenaEntered?.Invoke();

        // Forward internal events
        Death.OnPlayerDied += recap =>
        {
            Movement.SetDead();
            OnPlayerDied?.Invoke(recap);
        };
        Death.OnPlayerRespawned += () =>
        {
            Movement.SetAlive();
            OnPlayerRespawned?.Invoke();
        };
    }

    // ─────────────────────────────────────────────
    //  Map Control Lifecycle
    // ─────────────────────────────────────────────

    /// <summary>
    /// Initializes and launches an active map run using the specified configuration.
    /// Spawns the initial wave and initiates the 30Hz simulation loop.
    /// </summary>
    public void StartMap(MapRunConfig config)
    {
        CurrentConfig = config ?? throw new ArgumentNullException(nameof(config));
        MapModifiers = new MapModifierRuntime(config.Modifiers);

        Spawner.BeginMap(config);
        Death.ResetForNewMap();

        TotalMonstersKilledInMap = 0;
        CurrentMapRunTime = 0f;
        IsMapComplete = false;
        IsInCombat = true;

        _playerActionAccumulator.Reset();
        _monsterActionAccumulators.Clear();
        _monsterAilments.Clear();
        _processedDeadMonsters.Clear();
        PlayerAilments.Clear();

        OnMapStarted?.Invoke(config);

        Movement.StartMap(2400f, ExilePosition);
        Spawner.BeginMap(config, Movement.MapLength);

        var firstWave = Spawner.SpawnNextWave();
        if (firstWave != null)
        {
            SetCurrentWave(firstWave);
        }
    }

    /// <summary>
    /// Halts the current combat simulation (e.g. on return to hideout or window pause).
    /// </summary>
    public void StopMap()
    {
        IsInCombat = false;
        Movement.Stop();
    }

    /// <summary>
    /// Triggers an immediate emergency portal escape back to town/hideout.
    /// Halts combat, preserves accumulated loot, and avoids EXP penalties.
    /// </summary>
    public bool TriggerEmergencyPortal()
    {
        if (!IsInCombat || !Player.IsAlive)
            return false;

        Movement.Stop();
        bool escaped = Death.TryTriggerEmergencyPortal(Player);
        if (escaped)
        {
            IsInCombat = false;
        }
        return escaped;
    }

    /// <summary>
    /// Sets the active monster wave, acquires initial target, and resets wave-specific trackers.
    /// </summary>
    public void SetCurrentWave(MonsterWave wave)
    {
        CurrentWave = wave;

        // If monsters are within perception range of Exile, acquire initial target; otherwise start null until in range
        var inPerception = wave.Alive
            .Where(m => m.IsAggroed || ExilePosition.DistanceTo(m.Position) <= Movement.PerceptionRange)
            .ToList();
        CurrentTarget = inPerception.Count > 0 ? Targeting.SelectTarget(inPerception, ExilePosition) : null;

        _monsterActionAccumulators.Clear();
        _monsterAilments.Clear();

        OnWaveSpawned?.Invoke(wave);
        if (wave.IsBossWave)
        {
            OnBossWaveSpawned?.Invoke(wave);
        }
    }

    // ─────────────────────────────────────────────
    //  Simulation Frame Advance
    // ─────────────────────────────────────────────

    /// <summary>
    /// Advances combat simulation by the elapsed real-world time.
    /// Translates variable frame rate into discrete 30Hz (~33.33ms) fixed ticks via <see cref="TickManager"/>.
    /// </summary>
    /// <param name="realDeltaTime">Elapsed real time in seconds.</param>
    /// <returns>Number of discrete simulation ticks executed.</returns>
    public int Update(float realDeltaTime)
    {
        if (!IsInCombat || TickManager.IsPaused || realDeltaTime <= 0f)
            return 0;

        return TickManager.Update(realDeltaTime, ProcessTick);
    }

    // ─────────────────────────────────────────────
    //  PoE 5-Phase 30Hz Simulation Tick Loop
    // ─────────────────────────────────────────────

    /// <summary>
    /// Executes exactly one discrete 30Hz (~33.33ms) combat simulation tick.
    /// Strictly processes the 5 canonical phases in sequence.
    /// </summary>
    /// <param name="deltaTime">Fixed discrete tick duration (1.0 / 30.0s = ~0.03333s).</param>
    public void ProcessTick(float deltaTime)
    {
        var defenses = EffectiveDefenses;

        // ═════════════════════════════════════════════
        //  Phase 1: Regen & DoT Phase
        // ═════════════════════════════════════════════
        ExecutePhase1_RegenAndDoT(deltaTime, defenses);

        // ═════════════════════════════════════════════
        //  Phase 2: Timers & Buffs Phase
        // ═════════════════════════════════════════════
        ExecutePhase2_TimersAndBuffs(deltaTime);

        // ═════════════════════════════════════════════
        //  Phase 2.5: Real-World Exile Movement (Task 2.5)
        // ═════════════════════════════════════════════
        ExecutePhase_ExileMovement(deltaTime);

        // ═════════════════════════════════════════════
        //  Phase 3: Player Action Phase
        // ═════════════════════════════════════════════
        ExecutePhase3_PlayerAction(deltaTime, defenses);

        // ═════════════════════════════════════════════
        //  Phase 4: Monster Action Phase
        // ═════════════════════════════════════════════
        ExecutePhase4_MonsterAction(deltaTime, defenses);

        // ═════════════════════════════════════════════
        //  Phase 5: Death & Drop Phase
        // ═════════════════════════════════════════════
        ExecutePhase5_DeathAndDrop(deltaTime);
    }

    // ─────────────────────────────────────────────
    //  Phase 1: Regen & DoT
    // ─────────────────────────────────────────────

    private void ExecutePhase1_RegenAndDoT(float deltaTime, DefensiveStats defenses)
    {
        // 1. Player Life & Mana Regeneration
        Player.ApplyRegeneration(deltaTime, defenses.LifeRegenPerSecond, defenses.ManaRegenPerSecond);

        // 2. Player Energy Shield Recharge (ticks after delay without damage)
        Player.UpdateEsRecharge(deltaTime, defenses.EnergyShieldRechargeDelay, defenses.EnergyShieldRechargeRate);

        // 3. Player Ailment DoT countdown & application
        var playerAilmentResult = PlayerAilments.Tick(deltaTime);
        if (playerAilmentResult.TotalDoTDamage > 0f && Player.IsAlive)
        {
            float totalDoTApplied = 0f;

            // Ignite (Fire DoT)
            if (playerAilmentResult.FireDoTDamage > 0f)
            {
                totalDoTApplied += Player.ApplyDamage(
                    playerAilmentResult.FireDoTDamage,
                    isChaos: false,
                    chaosDoesNotBypassEs: false,
                    defenses.MindOverMatterPercent);
            }

            // Bleed (Physical DoT)
            if (playerAilmentResult.PhysicalDoTDamage > 0f)
            {
                totalDoTApplied += Player.ApplyDamage(
                    playerAilmentResult.PhysicalDoTDamage,
                    isChaos: false,
                    chaosDoesNotBypassEs: false,
                    defenses.MindOverMatterPercent);
            }

            // Poison (Chaos DoT)
            if (playerAilmentResult.ChaosDoTDamage > 0f)
            {
                totalDoTApplied += Player.ApplyDamage(
                    playerAilmentResult.ChaosDoTDamage,
                    isChaos: true,
                    chaosDoesNotBypassEs: defenses.ChaosDoesNotBypassES,
                    defenses.MindOverMatterPercent);
            }

            var defenseResult = new DefenseResult
            {
                Evaded = false,
                Blocked = false,
                Suppressed = false,
                TotalIncomingDamage = playerAilmentResult.TotalDoTDamage,
                TotalMitigatedDamage = 0f,
                TotalDamageApplied = totalDoTApplied,
                PlayerDied = !Player.IsAlive
            };

            OnPlayerDamaged?.Invoke(new PlayerDamagedEventArgs(
                Attacker: null,
                DefenseResult: defenseResult,
                DamageApplied: totalDoTApplied,
                MitigatedBreakdown: null,
                Evaded: false,
                Blocked: false,
                Suppressed: false,
                IsReflect: false,
                IsDoT: true
            ));

            if (!Player.IsAlive)
            {
                Death.HandleDeath(Player, "Ailment Degeneration");
            }
        }

        // 4. Monster Ailment DoTs (Ignite, Bleed, Poison on living enemies)
        if (CurrentWave != null)
        {
            foreach (var monster in CurrentWave.Alive)
            {
                if (_monsterAilments.TryGetValue(monster.InstanceId, out var mTracker))
                {
                    var dotResult = mTracker.Tick(deltaTime);
                    if (dotResult.TotalDoTDamage > 0f)
                    {
                        float dealt = monster.TakeRawDamage(dotResult.TotalDoTDamage);
                        OnDamageDealt?.Invoke(new DamageDealtEventArgs(
                            Target: monster,
                            Hit: null,
                            TotalDamageDealt: dealt,
                            IsCrit: false,
                            InflictedAilments: Array.Empty<AilmentInstance>(),
                            IsDoT: true
                        ));
                    }
                }
            }
        }
    }

    // ─────────────────────────────────────────────
    //  Phase 2: Timers & Buffs
    // ─────────────────────────────────────────────

    private void ExecutePhase2_TimersAndBuffs(float deltaTime)
    {
        // 1. Guard Skill & Flasks cooldowns and auto-instill triggers
        Loadout.Tick(deltaTime, Player);

        // 2. Auto-respawn countdown if awaiting respawn
        if (Death.IsAwaitingRespawn)
        {
            bool respawned = Death.Tick(deltaTime, Player, PlayerAilments);
            if (respawned)
            {
                _playerActionAccumulator.Reset();
            }
        }
    }

    // ─────────────────────────────────────────────
    //  Phase 2.5: Real-World Exile Movement (Task 2.5)
    // ─────────────────────────────────────────────

    private void ExecutePhase_ExileMovement(float deltaTime)
    {
        if (!Player.IsAlive || Death.IsAwaitingRespawn || !IsInCombat)
        {
            Movement.Stop();
            return;
        }

        var aliveMonsters = CurrentWave?.Alive.ToList() ?? new List<MonsterInstance>();
        Movement.Tick(deltaTime, aliveMonsters, Player, GearStats, PassiveStats);
        Player.Position = Movement.Position;

        // Task 2.7: Check roadside League Encounter proximity trigger
        if (CurrentWave?.Pack?.Encounter is { State: LeagueEncounterState.Dormant } encounter)
        {
            if (encounter.TryTrigger(ExilePosition))
            {
                CurrentWave.Pack.WakeUp();

                // Sulphite bonus: instantaneous flask charges refill
                if (encounter.Type == LeagueEncounterType.SulphiteVein)
                {
                    Loadout.RefillAllFlaskCharges();
                }

                OnLeagueEncounterTriggered?.Invoke(new LeagueEncounterEventArgs(encounter));
            }
        }
    }

    // ─────────────────────────────────────────────
    //  Phase 3: Player Action
    // ─────────────────────────────────────────────

    private void ExecutePhase3_PlayerAction(float deltaTime, DefensiveStats defenses)
    {
        // Player cannot act while awaiting respawn, dead, or when no enemies remain
        if (!Player.IsAlive || Death.IsAwaitingRespawn || !IsInCombat)
            return;

        if (CurrentWave == null || !CurrentWave.Alive.Any())
            return;

        // 1. Identify engageable monsters: must be either actively aggroed or within Exile's perception range
        var engageable = CurrentWave.Alive
            .Where(m => m.IsAggroed || ExilePosition.DistanceTo(m.Position) <= Movement.PerceptionRange)
            .ToList();

        if (engageable.Count == 0)
        {
            // Out of combat: no enemies in range to engage!
            CurrentTarget = null;
            while (_playerActionAccumulator.TryConsumeAction()) { }
            return;
        }

        // CC Action Speed Multipliers from Player Ailments
        float actionSpeedMultiplier = 1.0f;
        if (PlayerAilments.ActiveAilments.Any(a => a.Type == AilmentType.Freeze))
        {
            actionSpeedMultiplier = 0f; // Frozen: completely incapacitated
        }
        else if (PlayerAilments.ActiveAilments.FirstOrDefault(a => a.Type == AilmentType.Chill) is { } chill)
        {
            actionSpeedMultiplier = MathF.Max(0.1f, 1.0f - chill.EffectPercent / 100f);
        }

        // Calculate Effective APS
        float baseAps = Loadout.PrimarySkill.CalculateBaseActionSpeed(Player.Gear);
        if (baseAps <= 0f) baseAps = 1.0f;

        // Advance high-precision accumulator
        _playerActionAccumulator.Tick(deltaTime, baseAps, actionSpeedMultiplier);

        // Trigger attacks when action interval is reached
        while (_playerActionAccumulator.TryConsumeAction())
        {
            // 1. Skill Geometry & Autonomous Target Selection (Threat Scoring - Docs 4.3)
            var geometry = Loadout.PrimarySkill.GetGeometryType();
            bool isAoE = geometry != SkillGeometryType.SingleTarget;

            CurrentTarget = (Movement.CurrentEngagedTarget != null && Movement.CurrentEngagedTarget.IsAlive && engageable.Contains(Movement.CurrentEngagedTarget))
                ? Movement.CurrentEngagedTarget
                : Targeting.SelectTarget(engageable, ExilePosition, CurrentTarget, isAoESkill: isAoE);

            if (CurrentTarget == null)
            {
                break;
            }

            // 2. Attack Reach Check: ensure target is within weapon/skill reach
            bool isMeleeSkill = (Loadout.PrimarySkill.ActiveGem?.Tags?.Contains("Melee", StringComparer.OrdinalIgnoreCase) ?? false)
                || (Loadout.PrimarySkill.ActiveGem == null && (Player.MainHand == null || Player.MainHand.BaseDefinition.IsMelee));
            float skillBaseRange = Loadout.PrimarySkill.GetBaseRadiusOrRange();
            float effectiveAttackReach = isMeleeSkill
                ? SpatialCombatService.MeleeRange + 25f + CurrentTarget.Radius
                : MathF.Max(skillBaseRange + 25f + CurrentTarget.Radius, 175f);

            float distToTarget = ExilePosition.DistanceTo(CurrentTarget.Position);
            if (distToTarget > effectiveAttackReach)
            {
                // Target is perceived ahead but out of weapon reach: Exile is closing distance
                break;
            }

            // 3. In reach! Check & Spend Mana (PoE Rules: If insufficient mana, fall back to Default Attack)
            float manaCost = Loadout.PrimarySkill.CalculateEffectiveManaCost();
            bool isDefaultAttack = !Player.TrySpendMana(manaCost);
            if (isDefaultAttack)
            {
                geometry = SkillGeometryType.SingleTarget;
            }

            // Target aggros upon being engaged
            var targetEntity = CurrentTarget!;
            targetEntity.Aggro();

            // 3. Spatial Hit Testing (Docs 4.2: Radial AoE, Cones/Arcs, Multi-Vector Projectile Spread)
            float baseRadiusOrRange = isDefaultAttack ? SpatialCombatService.MeleeRange : Loadout.PrimarySkill.GetBaseRadiusOrRange();
            float coneAngle = isDefaultAttack ? 0f : Loadout.PrimarySkill.GetConeAngle();
            int projCount = isDefaultAttack ? 1 : Loadout.PrimarySkill.GetProjectileCount();
            int chainCount = isDefaultAttack ? 0 : Loadout.PrimarySkill.GetChainCount();
            float spreadAngle = isDefaultAttack ? 0f : Loadout.PrimarySkill.GetSpreadAngle();
            float projSpeed = isDefaultAttack ? 520f : Loadout.PrimarySkill.GetProjectileSpeed();

            IReadOnlyList<MonsterInstance> targets;
            IReadOnlyList<ProjectileVector>? projectileVectors = null;

            if (geometry == SkillGeometryType.Cone)
            {
                targets = Targeting.SelectSpatialAoETargets(
                    CurrentWave!.Alive, ExilePosition, targetEntity, SkillGeometryType.Cone,
                    baseRadiusOrRange, coneAngle, maxTargets: 8);
            }
            else if (geometry == SkillGeometryType.RadialAoE)
            {
                targets = Targeting.SelectSpatialAoETargets(
                    CurrentWave!.Alive, ExilePosition, targetEntity, SkillGeometryType.RadialAoE,
                    baseRadiusOrRange, coneAngleDegrees: 0f, maxTargets: 8);
            }
            else if (geometry == SkillGeometryType.ProjectileSpread)
            {
                Vector2D toTarget = targetEntity.Position - ExilePosition;
                float aimAngle = toTarget.LengthSquared > 0.001f ? toTarget.Angle() : 0f;
                float spreadStepRad = (spreadAngle / Math.Max(1, projCount - 1)) * (MathF.PI / 180.0f);
                if (projCount == 1) spreadStepRad = 0f;

                projectileVectors = SpatialCombatService.CalculateSpread(aimAngle, projCount, spreadStepRad, projSpeed);
                targets = Targeting.SelectAoETargets(CurrentWave!.Alive, targetEntity, Math.Min(projCount, 8));
            }
            else
            {
                targets = new[] { targetEntity };
            }

            // 4. Build Unified Attack Payload for Player (Primary Skill or Default Attack)
            var playerAttack = isDefaultAttack
                ? CombatAttackService.CreatePlayerDefaultAttack(Player, PassiveStats, GearStats)
                : CombatAttackService.CreatePlayerAttack(Player, Loadout, PassiveStats, GearStats);
            var basePool = playerAttack.BaseDamage;

            // 5. Fire Skill Event with Spatial Trajectories
            OnPlayerSkillFired?.Invoke(new PlayerSkillFiredEventArgs(
                ExilePosition,
                targetEntity,
                geometry,
                playerAttack.PrimaryDamageType,
                projectileVectors,
                coneAngle,
                baseRadiusOrRange,
                playerAttack.IsMelee
            ));

            // 6. Execute Hit on each selected target via Unified Pipeline
            foreach (var target in targets)
            {
                if (!target.IsAlive) continue;

                var result = CombatAttackService.ExecuteHit(Player, target, playerAttack, target.Defenses, _random);

                // If using fallback Default Attack, gain mana on hit to assist recovery
                if (isDefaultAttack)
                {
                    Player.RestoreManaOnHit(2.0f);
                }

                OnDamageDealt?.Invoke(new DamageDealtEventArgs(
                    Target: target,
                    Hit: result.Hit,
                    TotalDamageDealt: result.DamageDealt,
                    IsCrit: result.Hit.IsCrit,
                    InflictedAilments: result.Hit.InflictedAilments,
                    IsDoT: false
                ));

                // 7. Damage Reflect Pipeline Hook
                if (MapModifiers != null && Player.IsAlive)
                {
                    var reflect = MapModifiers.CalculateReflectedDamage(result.Hit.Damage);
                    if (reflect.HasReflectedDamage)
                    {
                        var reflectBreakdown = new HitDamageBreakdown(
                            reflect.ReflectedPhysical,
                            reflect.ReflectedFire,
                            reflect.ReflectedCold,
                            reflect.ReflectedLightning,
                            0f
                        );

                        // Absorb through Guard skill if active
                        if (Loadout.GuardSkill.IsActive)
                        {
                            float unabsorbed = Loadout.GuardSkill.AbsorbDamage(reflectBreakdown.Total);
                            if (reflectBreakdown.Total > 0f)
                            {
                                reflectBreakdown = reflectBreakdown.Multiply(unabsorbed / reflectBreakdown.Total);
                            }
                        }

                        var defense = DefensiveLayers.ProcessHit(
                            reflectBreakdown,
                            isAttack: false,
                            isSpell: false,
                            Player,
                            defenses,
                            _random
                        );

                        OnPlayerDamaged?.Invoke(new PlayerDamagedEventArgs(
                            Attacker: null,
                            DefenseResult: defense,
                            DamageApplied: defense.TotalDamageApplied,
                            MitigatedBreakdown: defense.MitigatedBreakdown,
                            Evaded: defense.Evaded,
                            Blocked: defense.Blocked,
                            Suppressed: defense.Suppressed,
                            IsReflect: true,
                            IsDoT: false
                        ));

                        if (defense.PlayerDied || !Player.IsAlive)
                        {
                            Death.HandleDeath(Player, "Reflected Damage");
                            break;
                        }
                    }
                }
            }

            // 8. Multi-Vector Chain Bounces (Docs 4.2.3: R_chain <= 60px)
            if (chainCount > 0 && targets.Count > 0 && CurrentWave != null)
            {
                var hitIds = new HashSet<long>(targets.Select(t => t.InstanceId));
                foreach (var primaryHitTarget in targets)
                {
                    var currentChainOrigin = primaryHitTarget.Position;
                    long lastTargetId = primaryHitTarget.InstanceId;

                    for (int c = 0; c < chainCount; c++)
                    {
                        var chainTarget = SpatialCombatService.FindChainTarget(
                            currentChainOrigin,
                            lastTargetId,
                            CurrentWave.Alive,
                            SpatialCombatService.ChainSearchRadius,
                            hitIds);

                        if (chainTarget == null || !chainTarget.IsAlive)
                            break;

                        hitIds.Add(chainTarget.InstanceId);
                        currentChainOrigin = chainTarget.Position;
                        lastTargetId = chainTarget.InstanceId;

                        // Chain hit deals -30% less damage per bounce
                        float chainFactor = MathF.Pow(0.70f, c + 1);
                        var chainAttack = new AttackPayload(Player, basePool.Multiply(chainFactor), playerAttack.Context)
                        {
                            IsAttack = playerAttack.IsAttack,
                            IsSpell = playerAttack.IsSpell,
                            IsMelee = playerAttack.IsMelee,
                            Accuracy = playerAttack.Accuracy,
                            Geometry = SkillGeometryType.SingleTarget,
                            PrimaryDamageType = playerAttack.PrimaryDamageType
                        };

                        var chainResult = CombatAttackService.ExecuteHit(Player, chainTarget, chainAttack, chainTarget.Defenses, _random);

                        OnDamageDealt?.Invoke(new DamageDealtEventArgs(
                            Target: chainTarget,
                            Hit: chainResult.Hit,
                            TotalDamageDealt: chainResult.DamageDealt,
                            IsCrit: chainResult.Hit.IsCrit,
                            InflictedAilments: chainResult.Hit.InflictedAilments,
                            IsDoT: false
                        ));
                    }
                }
            }
        }
    }

    // ─────────────────────────────────────────────
    //  Phase 4: Monster Action
    // ─────────────────────────────────────────────

    private void ExecutePhase4_MonsterAction(float deltaTime, DefensiveStats defenses)
    {
        if (!Player.IsAlive || Death.IsAwaitingRespawn || !IsInCombat || CurrentWave == null)
            return;

        // Check for Rare Monster Auras in the wave (e.g. Haste)
        float waveHasteBonus = CurrentWave.Alive
            .Where(m => m.Rarity == MonsterRarity.Rare)
            .SelectMany(m => m.AuraModifiers)
            .Where(a => a.HastePercent > 0f)
            .Sum(a => a.HastePercent);

        foreach (var monster in CurrentWave.Alive.ToList())
        {
            if (!Player.IsAlive) break;

            // Task 2.7: Dormant monsters (e.g. sealed in Essence Monolith) cannot act until triggered
            if (CurrentWave?.Pack?.IsDormant == true)
            {
                continue;
            }

            // Monster Action Speed & Crowd Control
            float monsterSpeedMult = 1.0f;
            if (_monsterAilments.TryGetValue(monster.InstanceId, out var mTracker))
            {
                if (mTracker.ActiveAilments.Any(a => a.Type == AilmentType.Freeze))
                {
                    monsterSpeedMult = 0f; // Frozen: cannot move or attack
                }
                else if (mTracker.ActiveAilments.FirstOrDefault(a => a.Type == AilmentType.Chill) is { } c)
                {
                    monsterSpeedMult = MathF.Max(0.1f, 1.0f - c.EffectPercent / 100f);
                }
            }

            if (waveHasteBonus > 0f)
            {
                monsterSpeedMult *= (1.0f + waveHasteBonus / 100f);
            }

            float distToExile = ExilePosition.DistanceTo(monster.Position);
            float effectiveAttackRange = monster.AttackRange + monster.Radius;

            // ═══ Aggro Check: Monsters only aggro/advance when Exile moves close or when damaged ═══
            if (!monster.IsAggroed)
            {
                if (distToExile <= monster.AggroRange)
                {
                    monster.Aggro();
                }
                else
                {
                    // Out of range & unaggroed: hold formation position, do not advance or attack
                    if (_monsterActionAccumulators.TryGetValue(monster.InstanceId, out var idleAcc))
                    {
                        while (idleAcc.TryConsumeAction()) { }
                    }
                    continue;
                }
            }

            // Alert nearby pack members within 120px
            if (monster.IsAggroed && CurrentWave != null)
            {
                foreach (var ally in CurrentWave.Alive)
                {
                    if (!ally.IsAggroed && monster.Position.DistanceTo(ally.Position) <= 120f)
                    {
                        ally.Aggro();
                    }
                }
            }

            // ═══ Monster Movement Phase (Docs 4.1: Spatial Range Enforcement) ═══
            // Aggroed monsters advance toward the Exile at their MovementSpeed each tick.
            // Movement is affected by CC multipliers (Chill slows, Freeze stops).
            if (distToExile > effectiveAttackRange && monsterSpeedMult > 0f)
            {
                float moveDistance = monster.MovementSpeed * monsterSpeedMult * deltaTime;

                // Don't overshoot into melee range
                float maxMove = distToExile - effectiveAttackRange + 1f;
                moveDistance = MathF.Min(moveDistance, maxMove);

                if (moveDistance > 0.01f)
                {
                    Vector2D toExile = (ExilePosition - monster.Position).Normalized();
                    Vector2D newPos = monster.Position + toExile * moveDistance;

                    // Clamp to world corridor bounds (Task 2.7)
                    float maxCorridorY = Movement.MapLength > 0f ? Movement.MapLength : ExileMovementController.DefaultMapLength;
                    newPos = new Vector2D(
                        Math.Clamp(newPos.X, ExileMovementController.MinCorridorX, ExileMovementController.MaxCorridorX),
                        Math.Clamp(newPos.Y, 0f, maxCorridorY)
                    );

                    monster.Position = newPos;
                }
            }

            // ═══ Range-Gated Attack: Only attack when within AttackRange ═══
            distToExile = ExilePosition.DistanceTo(monster.Position);
            bool isInAttackRange = distToExile <= effectiveAttackRange;

            // Monster Action Time Accumulator — always ticks (action time builds while closing)
            if (!_monsterActionAccumulators.TryGetValue(monster.InstanceId, out var mAccumulator))
            {
                mAccumulator = new ActionTimeAccumulator();
                _monsterActionAccumulators[monster.InstanceId] = mAccumulator;
            }

            mAccumulator.Tick(deltaTime, monster.AttacksPerSecond, monsterSpeedMult);

            // Execute Monster Attack(s) — ONLY if within attack range
            if (!isInAttackRange)
            {
                // Drain accumulated actions while out of range (prevent burst on arrival)
                while (mAccumulator.TryConsumeAction()) { }
                continue;
            }

            while (mAccumulator.TryConsumeAction() && Player.IsAlive)
            {
                var monsterAttack = CombatAttackService.CreateMonsterAttack(monster, MapModifiers);

                // ═══ Melee Attack AoE Indicator Event ═══
                Vector2D attackDir = (ExilePosition - monster.Position).Normalized();
                if (attackDir == Vector2D.Zero) attackDir = Vector2D.UnitX;

                // Execute Hit via Unified Pipeline
                var result = CombatAttackService.ExecuteHit(
                    monster,
                    Player,
                    monsterAttack,
                    defenses,
                    _random,
                    Loadout.GuardSkill
                );

                OnMonsterAttacked?.Invoke(new MonsterAttackEventArgs(
                    Attacker: monster,
                    AttackOrigin: monster.Position,
                    AttackDirection: attackDir,
                    EffectiveRange: effectiveAttackRange,
                    DamageType: monster.PrimaryDamageType,
                    IsCrit: result.Hit.IsCrit,
                    RawDamage: result.Hit.Damage.Total
                ));

                // Boss Slam Telegraph Warning check (>70% player EHP)
                float playerMaxEhp = MathF.Max(1f, Player.MaxLife + Player.MaxEnergyShield);
                float threatPercent = (result.Hit.Damage.Total / playerMaxEhp) * 100f;
                if ((monster.Rarity == MonsterRarity.Boss || monster.Rarity == MonsterRarity.Rare) && threatPercent >= 70f)
                {
                    OnBossSlamTelegraphed?.Invoke(new BossSlamTelegraphEventArgs(monster, threatPercent));
                }

                OnPlayerDamaged?.Invoke(new PlayerDamagedEventArgs(
                    Attacker: monster,
                    DefenseResult: result.Defense,
                    DamageApplied: result.DamageDealt,
                    MitigatedBreakdown: result.Defense.MitigatedBreakdown,
                    Evaded: result.Defense.Evaded,
                    Blocked: result.Defense.Blocked,
                    Suppressed: result.Defense.Suppressed,
                    IsReflect: false,
                    IsDoT: false
                ));

                if (result.TargetDied || !Player.IsAlive)
                {
                    Death.HandleDeath(Player, monster.Name);
                    break;
                }
            }
        }
    }

    // ─────────────────────────────────────────────
    //  Phase 5: Death & Drop
    // ─────────────────────────────────────────────

    private void ExecutePhase5_DeathAndDrop(float deltaTime)
    {
        // 1. Identify and process freshly slain monsters
        if (CurrentWave != null)
        {
            var deadMonsters = CurrentWave.Monsters
                .Where(m => !m.IsAlive && !_processedDeadMonsters.Contains(m.InstanceId))
                .ToList();

            foreach (var dead in deadMonsters)
            {
                _processedDeadMonsters.Add(dead.InstanceId);
                TotalMonstersKilledInMap++;

                // Grant character experience
                Player.AddExperience(dead.BaseExperience);

                // Add flask charges
                Loadout.AddChargesOnKill(dead.Rarity);

                // Seeded & Loot Drop Evaluation
                bool isFailSafe = CurrentConfig?.Map.IsFailSafe ?? false;
                var drops = DropCoordinator.ProcessMonsterKill(dead, isFailSafe);

                if (drops != null && drops.Count > 0)
                {
                    foreach (var drop in drops)
                    {
                        OnLootDropped?.Invoke(drop);
                    }
                    OnLootBatchDropped?.Invoke(drops);
                }

                // Fire Monster Killed Event
                OnMonsterKilled?.Invoke(new MonsterKilledEventArgs(
                    Monster: dead,
                    ExpGranted: dead.BaseExperience,
                    Drops: drops ?? Array.Empty<LootDrop>()
                ));

                // Clean up monster runtime resources
                dead.ClearEventSubscribers();
                _monsterAilments.Remove(dead.InstanceId);
                _monsterActionAccumulators.Remove(dead.InstanceId);
            }
        }

        // 2. Wave Progression & Map Completion
        if (CurrentWave != null && CurrentWave.IsCleared)
        {
            // Task 2.7: Mark League Encounter completed if active wave was an encounter
            if (CurrentWave.Pack?.Encounter is { State: LeagueEncounterState.Active } activeEnc)
            {
                activeEnc.Complete();
                OnLeagueEncounterCompleted?.Invoke(new LeagueEncounterEventArgs(activeEnc));
            }

            Movement.NotifyPackCleared();

            if (CurrentWave.IsBossWave || Spawner.IsMapComplete)
            {
                IsMapComplete = true;
                IsInCombat = false;
                Movement.SetCompleted();

                OnMapCompleted?.Invoke(new MapCompletedEventArgs(
                    Config: CurrentConfig!,
                    TotalMonstersKilled: TotalMonstersKilledInMap,
                    ClearTimeSeconds: CurrentMapRunTime,
                    MapDeaths: Death.MapDeaths
                ));
            }
            else
            {
                var nextWave = Spawner.SpawnNextWave();
                if (nextWave != null)
                {
                    SetCurrentWave(nextWave);
                }
            }
        }

        // 3. Tick Telemetry Event
        CurrentMapRunTime += deltaTime;
        float progress = Spawner.GetClearProgress();
        int aliveCount = CurrentWave?.Alive.Count() ?? 0;
        bool isBoss = CurrentWave?.IsBossWave ?? false;

        OnTickProcessed?.Invoke(new TickProcessedEventArgs(
            TickIndex: TickManager.CurrentTick,
            DeltaTime: deltaTime,
            MapSimulationTime: CurrentMapRunTime,
            AliveMonstersCount: aliveCount,
            ClearProgress: progress,
            IsBossWave: isBoss
        ));
    }
}
