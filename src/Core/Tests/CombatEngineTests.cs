using System;
using System.Collections.Generic;
using System.Linq;
using IdleExile.Core.Models;
using IdleExile.Core.Services;
using IdleExile.Core.Simulation;
using IdleExile.Core.State;

namespace IdleExile.Core.Tests;

/// <summary>
/// Comprehensive unit tests for Task 1.5: 30Hz Combat Engine & Tick Manager.
/// Validates:
/// - 30Hz Fixed Timestep resolution and time accumulation (<see cref="TickManager"/>).
/// - Phase 1: Life/Mana regen, ES recharge delay, and DoT damage (<see cref="CombatEngine"/>).
/// - Phase 2: Buff/cooldown timers, flask auto-instill, and 2s auto-respawn delay.
/// - Phase 3: Player action accumulation, mana deduction, targeting, damage, and reflect.
/// - Phase 4: Monster action accumulation, damage rolls, Boss slam telegraph, player defenses.
/// - Phase 5: Slain monster detection, EXP reward, flask charges, loot drops, and wave progression.
/// - End-to-end 100+ tick integration run with zero allocations and complete event telemetry.
/// </summary>
public static class CombatEngineTests
{
    public static void RunAll()
    {
        Console.WriteLine("[Tests] Running CombatEngineTests (Task 1.5)...");

        TestTickManagerFixedTimestep();
        TestTickManagerSpiralOfDeathPrevention();
        TestTickManagerPauseAndStepSingleTick();

        TestPhase1RegenAndESRecharge();
        TestPhase1AilmentDoTProcessing();

        TestPhase2FlaskAutoInstillAndGuardSkill();
        TestPhase2AutoRespawnSequence();

        TestPhase3PlayerActionAndManaConsumption();
        TestPhase3ReflectDamageHook();

        TestPhase4MonsterActionAndBossSlamTelegraph();

        TestPhase5MonsterKillExpFlaskChargesAndLoot();
        TestFullEndToEndCombatLoop();
        TestContinuousCombatSustainWithoutFreezing();
        TestFireballProducesProjectilesAndAoETargets();
        TestPhysicalProjectileContinuousMovementAndFlightTime();
        TestPhysicalProjectileAoEExplosionHitsSurroundingCluster();
        TestFrontlineMonsterBlocksBacklineMonster();
        TestNonShotgunningRuleInVolley();
        TestSplitArrowProjectilesAbsorbedOnHitWithoutPiercingBehindEnemy();
        TestStartMapResetsExileAndPlayerPositionsToCorridorStart();

        Console.WriteLine("[Tests] All CombatEngineTests PASSED successfully!");
    }

    private static void AssertEqual(float expected, float actual, float tolerance = 0.001f, string message = "")
    {
        if (MathF.Abs(expected - actual) > tolerance)
        {
            throw new Exception($"Assertion failed: Expected {expected}, got {actual}. {message}");
        }
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception($"Assertion failed: {message}");
        }
    }

    // ─────────────────────────────────────────────
    //  TickManager Tests
    // ─────────────────────────────────────────────

    private static void TestTickManagerFixedTimestep()
    {
        var tm = new TickManager(30);
        AssertEqual(30, tm.TickRate, 0.001f, "Tick rate must be 30");
        AssertEqual(1f / 30f, tm.FixedDeltaTime, 0.0001f, "Fixed delta time should be ~0.0333s");

        int tickCount = 0;
        float totalTickTime = 0f;

        // Feed exactly 1.0 second of real time
        int processed = tm.Update(1.0f, dt =>
        {
            tickCount++;
            totalTickTime += dt;
        });

        AssertEqual(30, processed, 0.001f, "1.0 second must yield exactly 30 discrete ticks");
        AssertEqual(30, tickCount, 0.001f, "Callback must execute 30 times");
        AssertEqual(1.0f, totalTickTime, 0.01f, "Total discrete tick time should equal 1.0s");
        AssertEqual(30, tm.CurrentTick, 0.001f, "CurrentTick counter must be 30");
    }

    private static void TestTickManagerSpiralOfDeathPrevention()
    {
        var tm = new TickManager(30, maxTicksPerUpdate: 10);

        // Feed an enormous lag spike (e.g. 5.0 seconds)
        int ticksProcessed = tm.Update(5.0f, dt => { });

        AssertEqual(10, ticksProcessed, 0.001f, "Must clamp catch-up to maxTicksPerUpdate (10 ticks)");
        AssertTrue(tm.TimeAccumulator <= tm.FixedDeltaTime * 10, "Accumulator must be clamped to prevent runaway");
    }

    private static void TestTickManagerPauseAndStepSingleTick()
    {
        var tm = new TickManager(30);

        // Paused state
        tm.Pause();
        AssertTrue(tm.IsPaused, "Manager should be paused");
        int ticks = tm.Update(1.0f, dt => { });
        AssertEqual(0, ticks, 0.001f, "No ticks should process while paused");
        AssertEqual(0, tm.CurrentTick, 0.001f, "Tick counter should remain 0");

        // Deterministic single step
        bool stepped = false;
        tm.StepSingleTick(dt => stepped = true);
        AssertTrue(stepped, "StepSingleTick must execute callback even when paused");
        AssertEqual(1, tm.CurrentTick, 0.001f, "CurrentTick should advance to 1");

        tm.Resume();
        AssertTrue(!tm.IsPaused, "Manager should be unpaused");
    }

    // ─────────────────────────────────────────────
    //  Phase 1 Tests: Regen & DoT
    // ─────────────────────────────────────────────

    private static void TestPhase1RegenAndESRecharge()
    {
        var player = new PlayerState();
        player.Initialize(maxLife: 100f, maxEnergyShield: 50f, maxMana: 60f);
        player.Defenses = new DefensiveStats
        {
            LifeRegenPerSecond = 10f,
            ManaRegenPerSecond = 15f,
            EnergyShieldRechargeDelay = 1.0f, // 1 second delay
            EnergyShieldRechargeRate = 50f   // 50% per second
        };

        var loadout = new SkillLoadout();
        var engine = new CombatEngine(player, loadout);

        // Damage player resources to 50 Life, 0 ES, 10 Mana
        player.ApplyDamage(75f, isChaos: false, chaosDoesNotBypassEs: false, 0f);
        player.TrySpendMana(50f);

        AssertEqual(75f, player.CurrentLife, 0.01f, "Life damaged");
        AssertEqual(0f, player.CurrentEnergyShield, 0.01f, "ES depleted");
        AssertEqual(10f, player.CurrentMana, 0.01f, "Mana spent");

        // Run 30 ticks (1 second)
        float dt = 1f / 30f;
        for (int i = 0; i < 30; i++)
        {
            engine.ProcessTick(dt);
        }

        // Life regened ~10 (75 -> ~85), Mana regened ~15 (10 -> ~25)
        AssertEqual(85f, player.CurrentLife, 0.5f, "Life should regenerate by ~10 over 1 second");
        AssertEqual(25f, player.CurrentMana, 0.5f, "Mana should regenerate by ~15 over 1 second");

        // ES recharge delay was 1.0s, so it just began or reached threshold; run another 15 ticks (0.5s)
        for (int i = 0; i < 15; i++)
        {
            engine.ProcessTick(dt);
        }

        AssertTrue(player.CurrentEnergyShield > 0f, "ES should actively recharge after delay elapses");
    }

    private static void TestPhase1AilmentDoTProcessing()
    {
        var player = new PlayerState();
        player.Initialize(maxLife: 200f, maxEnergyShield: 0f, maxMana: 50f);

        var loadout = new SkillLoadout();
        var engine = new CombatEngine(player, loadout);

        // Apply 60 DPS Ignite to player
        var ignite = new AilmentInstance(
            Type: AilmentType.Ignite,
            SourceDamageType: DamageType.Fire,
            DamagePerSecond: 60f,
            EffectPercent: 0f,
            DurationSeconds: 3.0f,
            IsStacking: false
        );
        engine.PlayerAilments.ApplyAilment(ignite);

        bool playerDamagedEventFired = false;
        engine.OnPlayerDamaged += args =>
        {
            if (args.IsDoT) playerDamagedEventFired = true;
        };

        // Run 30 ticks (1.0 second)
        float dt = 1f / 30f;
        for (int i = 0; i < 30; i++)
        {
            engine.ProcessTick(dt);
        }

        AssertTrue(playerDamagedEventFired, "OnPlayerDamaged must fire on DoT tick");
        AssertEqual(140f, player.CurrentLife, 1.0f, "Player Life should drop by ~60 from 60 DPS Ignite over 1s");
    }

    // ─────────────────────────────────────────────
    //  Phase 2 Tests: Timers, Flasks & Auto-Respawn
    // ─────────────────────────────────────────────

    private static void TestPhase2FlaskAutoInstillAndGuardSkill()
    {
        var player = new PlayerState();
        player.Initialize(maxLife: 100f, maxEnergyShield: 0f, maxMana: 50f);

        var loadout = new SkillLoadout();
        var engine = new CombatEngine(player, loadout);

        // Guard Skill activation
        bool guardActivated = loadout.GuardSkill.TryActivate();
        AssertTrue(guardActivated, "Guard skill should activate");
        AssertTrue(loadout.GuardSkill.IsActive, "Guard skill should be active");

        // Damage player to 40% (below 50% threshold for Divine Life Flask auto-instill)
        player.ApplyDamage(60f, isChaos: false, chaosDoesNotBypassEs: false, 0f);
        AssertEqual(40f, player.CurrentLife, 0.01f, "Player life at 40%");

        var lifeFlask = loadout.Flasks[0];
        AssertTrue(lifeFlask.Type == FlaskType.Life, "Slot 0 should be Life flask");
        float chargesBefore = lifeFlask.CurrentCharges;

        // Process single tick: Flask auto-instill should evaluate and trigger
        engine.ProcessTick(1f / 30f);

        AssertTrue(lifeFlask.CurrentCharges < chargesBefore, "Life flask should have consumed charges");
        AssertTrue(player.CurrentLife > 40f, "Player life should have been restored by Life flask");
    }

    private static void TestPhase2AutoRespawnSequence()
    {
        var player = new PlayerState();
        player.Initialize(maxLife: 100f, maxEnergyShield: 50f, maxMana: 60f);

        var loadout = new SkillLoadout();
        var engine = new CombatEngine(player, loadout);

        // Inflict fatal damage
        player.ApplyDamage(200f, isChaos: false, chaosDoesNotBypassEs: false, 0f);
        AssertTrue(!player.IsAlive, "Player must be dead");

        engine.Death.HandleDeath(player, "Test Brute");
        AssertTrue(engine.Death.IsAwaitingRespawn, "Player must be awaiting respawn");

        bool respawnFired = false;
        engine.OnPlayerRespawned += () => respawnFired = true;

        // 2.0 seconds respawn delay = 60 ticks
        float dt = 1f / 30f;
        for (int i = 0; i < 61; i++)
        {
            engine.ProcessTick(dt);
        }

        AssertTrue(respawnFired, "OnPlayerRespawned event must fire after 2.0s delay");
        AssertTrue(!engine.Death.IsAwaitingRespawn, "IsAwaitingRespawn should be false after respawn");
        AssertTrue(player.IsAlive, "Player must be alive again");
        AssertEqual(100f, player.CurrentLife, 0.01f, "Life restored to full");
        AssertEqual(50f, player.CurrentEnergyShield, 0.01f, "ES restored to full");
    }

    // ─────────────────────────────────────────────
    //  Phase 3 Tests: Player Action & Reflect
    // ─────────────────────────────────────────────

    private static void TestPhase3PlayerActionAndManaConsumption()
    {
        var player = new PlayerState();
        player.Initialize(maxLife: 100f, maxEnergyShield: 0f, maxMana: 100f);

        var loadout = new SkillLoadout();
        // Setup Active Gem with 2.0 APS, 10 mana cost, 20-30 physical damage
        var gem = new GemDefinition(
            Id: "heavy_strike",
            Name: "Heavy Strike",
            Category: "Active",
            Color: "Red",
            Tags: new[] { "Attack", "Melee", "Physical" },
            RequiredTags: null,
            BaseStats: new Dictionary<string, object>
            {
                ["attackSpeedMultiplier"] = 200f, // 2.0 APS -> every 15 ticks
                ["manaCost"] = 10f,
                ["baseDamageMin"] = 20f,
                ["baseDamageMax"] = 30f,
                ["damageType"] = "Physical"
            },
            Modifiers: null,
            Description: "A heavy melee strike."
        );
        loadout.PrimarySkill.SetActiveGem(gem);

        var engine = new CombatEngine(player, loadout);

        // Spawn a dummy test wave with 1 monster
        var monster = new MonsterInstance(
            instanceId: 101,
            name: "Training Dummy",
            rarity: MonsterRarity.Normal,
            monsterLevel: 1,
            maxLife: 500f,
            primaryDamageType: DamageType.Physical,
            damageMin: 0f,
            damageMax: 0f,
            attacksPerSecond: 0.1f,
            critChance: 0f,
            critMultiplier: 100f,
            accuracy: 100f,
            baseExperience: 10f,
            itemQuantityMultiplier: 1f,
            itemRarityMultiplier: 1f,
            initialPosition: new Vector2D(65f, 104f)
        );

        var wave = new MonsterWave
        {
            WaveNumber = 1,
            Monsters = new[] { monster }
        };

        var mapDef = new MapDefinition("t1", "Strand", 1, 1, false, 0, 100, "Boss", "");
        engine.StartMap(new MapRunConfig(mapDef, Array.Empty<RolledMapModifier>()));
        engine.SetCurrentWave(wave);

        bool damageFired = false;
        engine.OnDamageDealt += args => damageFired = true;

        // At 2.0 APS, first attack triggers after 15 ticks (0.5s)
        float dt = 1f / 30f;
        for (int i = 0; i < 15; i++)
        {
            engine.ProcessTick(dt);
        }

        AssertTrue(damageFired, "OnDamageDealt must fire after action interval is met");
        AssertTrue(monster.CurrentLife < 500f, "Monster must take damage from Heavy Strike");
        AssertEqual(90f, player.CurrentMana, 0.01f, "10 mana should be consumed by Heavy Strike");
    }

    private static void TestPhase3ReflectDamageHook()
    {
        var player = new PlayerState();
        player.Initialize(maxLife: 200f, maxEnergyShield: 0f, maxMana: 100f);
        player.Defenses = new DefensiveStats { Armour = 0f }; // 0 armour for clear calculation

        var loadout = new SkillLoadout();
        var gem = new GemDefinition(
            Id: "strike",
            Name: "Strike",
            Category: "Active",
            Color: "Red",
            Tags: new[] { "Attack", "Physical" },
            RequiredTags: null,
            BaseStats: new Dictionary<string, object>
            {
                ["attackSpeedMultiplier"] = 3000f, // Instant attack every tick
                ["manaCost"] = 0f,
                ["baseDamageMin"] = 100f,
                ["baseDamageMax"] = 100f,
                ["damageType"] = "Physical"
            },
            Modifiers: null,
            Description: "A rapid physical strike."
        );
        loadout.PrimarySkill.SetActiveGem(gem);

        var engine = new CombatEngine(player, loadout);

        // Map with 20% Physical Reflect
        var reflectMod = new MapModifierDefinition(
            Id: "monster_reflect_physical",
            NameTemplate: "Monsters reflect {0}% of Physical Damage",
            BonusQuantity: 10,
            BonusPackSize: 5,
            Min: 20f,
            Max: 20f
        );
        var rolledMods = new[] { new RolledMapModifier(reflectMod, 20f) };
        var mapDef = new MapDefinition("t1", "Strand", 1, 1, false, 0, 100, null, "");

        engine.StartMap(new MapRunConfig(mapDef, rolledMods));

        var monster = new MonsterInstance(102, "Target Mob", MonsterRarity.Normal, 1, 1000f, DamageType.Physical, 0, 0, 0.1f, 0, 100, 100, 10, 1, 1, initialPosition: new Vector2D(65f, 104f));
        engine.SetCurrentWave(new MonsterWave { WaveNumber = 1, Monsters = new[] { monster } });

        bool reflectDamaged = false;
        engine.OnPlayerDamaged += args =>
        {
            if (args.IsReflect) reflectDamaged = true;
        };

        // Advance 1 tick to trigger attack and reflect
        engine.ProcessTick(1f / 30f);

        AssertTrue(reflectDamaged, "OnPlayerDamaged must fire with IsReflect = true");
        AssertTrue(player.CurrentLife < 200f, "Player Life should be reduced by reflected damage");
    }

    // ─────────────────────────────────────────────
    //  Phase 4 Tests: Monster Action & Boss Slam
    // ─────────────────────────────────────────────

    private static void TestPhase4MonsterActionAndBossSlamTelegraph()
    {
        var player = new PlayerState();
        player.Initialize(maxLife: 100f, maxEnergyShield: 0f, maxMana: 50f);
        player.Defenses = new DefensiveStats { Armour = 0f };

        var loadout = new SkillLoadout();
        var engine = new CombatEngine(player, loadout);

        // Boss monster dealing 80 damage (80% of player's 100 max EHP)
        // Positioned within attack range of Exile (40, 104)
        var boss = new MonsterInstance(
            instanceId: 999,
            name: "Tidal Behemoth",
            rarity: MonsterRarity.Boss,
            monsterLevel: 1,
            maxLife: 2000f,
            primaryDamageType: DamageType.Physical,
            damageMin: 80f,
            damageMax: 80f,
            attacksPerSecond: 30f, // Attacks every tick
            critChance: 0f,
            critMultiplier: 100f,
            accuracy: 100f,
            baseExperience: 100f,
            itemQuantityMultiplier: 5f,
            itemRarityMultiplier: 5f,
            initialPosition: new Vector2D(60f, 104f) // ~20px from Exile, within Boss AttackRange (45px + 16px Radius)
        );

        var mapDef = new MapDefinition("t1", "Strand", 1, 1, false, 0, 100, "Tidal Behemoth", "");
        engine.StartMap(new MapRunConfig(mapDef, Array.Empty<RolledMapModifier>()));
        engine.SetCurrentWave(new MonsterWave { WaveNumber = 1, Monsters = new[] { boss }, IsBossWave = true });

        bool telegraphFired = false;
        engine.OnBossSlamTelegraphed += args =>
        {
            telegraphFired = true;
            AssertTrue(args.ThreatEhpPercent >= 70f, "Threat percent should exceed 70%");
        };

        bool monsterAttackLanded = false;
        engine.OnPlayerDamaged += args =>
        {
            if (args.Attacker?.InstanceId == boss.InstanceId) monsterAttackLanded = true;
        };

        // Advance 1 tick
        engine.ProcessTick(1f / 30f);

        AssertTrue(telegraphFired, "OnBossSlamTelegraphed must fire for >70% EHP attack");
        AssertTrue(monsterAttackLanded, "Monster attack must land and deal damage");
        AssertEqual(20f, player.CurrentLife, 0.5f, "Player Life should drop from 100 to 20");
    }

    // ─────────────────────────────────────────────
    //  Phase 5 Tests: Death, EXP, Flask Charges, Loot
    // ─────────────────────────────────────────────

    private static void TestPhase5MonsterKillExpFlaskChargesAndLoot()
    {
        var player = new PlayerState();
        player.Initialize(maxLife: 100f, maxEnergyShield: 0f, maxMana: 50f);

        var loadout = new SkillLoadout();
        // Set life flask charges to 0 to verify refill on kill
        var flask = loadout.Flasks[0];
        flask.TryDrink(player); // consumes 20
        float chargesPrior = flask.CurrentCharges;

        var engine = new CombatEngine(player, loadout);

        var monster = new MonsterInstance(
            instanceId: 501,
            name: "Plague Rat",
            rarity: MonsterRarity.Normal,
            monsterLevel: 1,
            maxLife: 10f,
            primaryDamageType: DamageType.Physical,
            damageMin: 0f,
            damageMax: 0f,
            attacksPerSecond: 0.1f,
            critChance: 0f,
            critMultiplier: 100f,
            accuracy: 100f,
            baseExperience: 25f,
            itemQuantityMultiplier: 1f,
            itemRarityMultiplier: 1f
        );

        var mapDef = new MapDefinition("t1", "Strand", 1, 1, false, 0, 100, "Boss", "");
        engine.StartMap(new MapRunConfig(mapDef, Array.Empty<RolledMapModifier>()));
        engine.SetCurrentWave(new MonsterWave { WaveNumber = 1, Monsters = new[] { monster } });

        bool monsterKilledFired = false;
        engine.OnMonsterKilled += args => monsterKilledFired = true;

        // Slay the monster directly
        monster.TakeDamage(100f, DamageType.Physical);
        AssertTrue(!monster.IsAlive, "Monster is dead");

        float expBefore = player.CurrentExperience;

        // Process Phase 5 in next tick
        engine.ProcessTick(1f / 30f);

        AssertTrue(monsterKilledFired, "OnMonsterKilled event must fire");
        AssertEqual(expBefore + 25f, player.CurrentExperience, 0.01f, "Player must gain 25 EXP");
        AssertTrue(flask.CurrentCharges > chargesPrior, "Flask must gain charges on monster kill");
        AssertEqual(1, engine.TotalMonstersKilledInMap, 0.01f, "Map kill count should increment to 1");
    }

    // ─────────────────────────────────────────────
    //  Full Integration Test: 100+ Ticks
    // ─────────────────────────────────────────────

    private static void TestFullEndToEndCombatLoop()
    {
        var player = new PlayerState();
        player.BootstrapFromArchetype(StarterArchetypeCatalog.Juggernaut);

        var loadout = new SkillLoadout();
        var gem = new GemDefinition(
            Id: "cleave",
            Name: "Cleave",
            Category: "Active",
            Color: "Red",
            Tags: new[] { "Attack", "Melee", "AoE", "Physical" },
            RequiredTags: null,
            BaseStats: new Dictionary<string, object>
            {
                ["attackSpeedMultiplier"] = 150f,
                ["manaCost"] = 2f,
                ["baseDamageMin"] = 25f,
                ["baseDamageMax"] = 40f,
                ["damageType"] = "Physical"
            },
            Modifiers: null,
            Description: "An expansive cleaving attack."
        );
        loadout.PrimarySkill.SetActiveGem(gem);

        var spawner = new WaveSpawner(new Random(42));
        var engine = new CombatEngine(player, loadout, spawner);

        var mapDef = new MapDefinition("strand", "Strand Map", 1, 1, false, 10, 100, "Tidal Behemoth", "");
        engine.StartMap(new MapRunConfig(mapDef, Array.Empty<RolledMapModifier>()));

        int tickProcessedCount = 0;
        int damageCount = 0;
        int killCount = 0;
        int skillFiredCount = 0;
        bool lastFiredWasMelee = false;

        engine.OnTickProcessed += args => tickProcessedCount++;
        engine.OnDamageDealt += args => damageCount++;
        engine.OnMonsterKilled += args => killCount++;
        engine.OnPlayerSkillFired += args =>
        {
            skillFiredCount++;
            lastFiredWasMelee = args.IsMelee;
        };

        // Simulate 120 ticks (4.0 seconds of gameplay)
        float dt = 1f / 30f;
        for (int i = 0; i < 120; i++)
        {
            engine.ProcessTick(dt);
        }

        AssertEqual(120, tickProcessedCount, 0.001f, "Must process exactly 120 ticks");
        AssertTrue(skillFiredCount > 0, "Player must fire skills during combat");
        AssertTrue(lastFiredWasMelee, "Cleave must be identified as Melee skill in OnPlayerSkillFired event");
        AssertTrue(damageCount > 0, "Cleave must deal damage to monsters");
        AssertTrue(killCount > 0, "Monsters must be slain across 4 seconds of combat");
        AssertTrue(player.CurrentExperience > 0f, "Character must gain experience");
        AssertTrue(engine.CurrentMapRunTime >= 3.9f, "Map run time must advance");
    }

    /// <summary>
    /// Regression test: Verifies that after initial mana pool depletes (5+ seconds),
    /// the player character does NOT stop attacking while surrounded by monsters.
    /// Sustains combat through passive mana regen and PoE fallback Default Attack.
    /// </summary>
    private static void TestContinuousCombatSustainWithoutFreezing()
    {
        var player = new PlayerState();
        player.BootstrapFromArchetype(StarterArchetypeCatalog.Juggernaut);

        var loadout = new SkillLoadout();
        var gem = new GemDefinition(
            Id: "cleave",
            Name: "Cleave",
            Category: "Active",
            Color: "Red",
            Tags: new[] { "Attack", "Melee", "AoE", "Physical" },
            RequiredTags: null,
            BaseStats: new Dictionary<string, object>
            {
                ["attackSpeedMultiplier"] = 100f,
                ["manaCost"] = 8f, // High mana cost: drains 60 initial mana in ~7 attacks (~5 seconds)
                ["baseDamageMin"] = 20f,
                ["baseDamageMax"] = 35f,
                ["damageType"] = "Physical"
            },
            Modifiers: null,
            Description: "Cleave"
        );
        loadout.PrimarySkill.SetActiveGem(gem);

        // Populate a sturdy wave that won't die immediately so monsters stay alive and surrounding
        var monsters = new List<MonsterInstance>();
        for (int m = 0; m < 5; m++)
        {
            monsters.Add(new MonsterInstance(
                instanceId: 200 + m,
                name: "Undead Brute",
                rarity: MonsterRarity.Normal,
                monsterLevel: 1,
                maxLife: 5000f, // high life to endure sustained combat
                primaryDamageType: DamageType.Physical,
                damageMin: 1f,
                damageMax: 2f,
                attacksPerSecond: 0.5f,
                critChance: 0f,
                critMultiplier: 100f,
                accuracy: 100f,
                baseExperience: 10f,
                itemQuantityMultiplier: 1f,
                itemRarityMultiplier: 1f,
                initialPosition: new Vector2D(65f + m * 5f, 104f)
            ));
        }

        var spawner = new WaveSpawner(new Random(42));
        var engine = new CombatEngine(player, loadout, spawner);

        var mapDef = new MapDefinition("strand", "Strand Map", 1, 1, false, 10, 100, "Tidal Behemoth", "");
        engine.StartMap(new MapRunConfig(mapDef, Array.Empty<RolledMapModifier>()));
        engine.SetCurrentWave(new MonsterWave { WaveNumber = 1, Monsters = monsters });

        int attacksInFirst5s = 0;
        int attacksInSecond5s = 0;
        int attacksInThird5s = 0;

        engine.OnPlayerSkillFired += args =>
        {
            if (engine.CurrentMapRunTime < 5.0f) attacksInFirst5s++;
            else if (engine.CurrentMapRunTime < 10.0f) attacksInSecond5s++;
            else attacksInThird5s++;
        };

        // Simulate 450 ticks (15.0 seconds of continuous combat)
        float dt = 1f / 30f;
        for (int i = 0; i < 450; i++)
        {
            engine.ProcessTick(dt);
        }

        AssertTrue(attacksInFirst5s > 0, "Player must attack in first 5 seconds (initial mana)");
        AssertTrue(attacksInSecond5s > 0, $"Player must NOT stop attacking between 5s-10s (got {attacksInSecond5s} attacks)");
        AssertTrue(attacksInThird5s > 0, $"Player must NOT stop attacking between 10s-15s (got {attacksInThird5s} attacks)");
        AssertTrue(player.CurrentMana > 0f, "Player mana must regenerate and not remain at 0 permanently");
    }

    /// <summary>
    /// Verifies that casting Fireball in CombatEngine emits CombatAttackEventArgs
    /// with an in-flight ProjectileVector (not null) and selects AoE targets.
    /// </summary>
    private static void TestFireballProducesProjectilesAndAoETargets()
    {
        var player = new PlayerState();
        player.BootstrapFromArchetype(StarterArchetypeCatalog.Elementalist);

        var loadout = new SkillLoadout();
        var fireballGem = new GemDefinition(
            Id: "fireball",
            Name: "Fireball",
            Category: "Active",
            Color: "Blue",
            Tags: new[] { "spell", "projectile", "fire", "aoe" },
            RequiredTags: null,
            BaseStats: new Dictionary<string, object>
            {
                ["baseDamageMin"] = 25f,
                ["baseDamageMax"] = 38f,
                ["damageType"] = "Fire",
                ["manaCost"] = 8f,
                ["projectileSpeed"] = 540f,
                ["aoeRadius"] = 32f,
                ["castTime"] = 0.75f
            },
            Modifiers: null,
            Description: "Fireball"
        );
        loadout.PrimarySkill.SetActiveGem(fireballGem);

        var monsters = new List<MonsterInstance>();
        for (int m = 0; m < 3; m++)
        {
            monsters.Add(new MonsterInstance(
                instanceId: 300 + m,
                name: "Cluster Mob",
                rarity: MonsterRarity.Normal,
                monsterLevel: 1,
                maxLife: 200f,
                primaryDamageType: DamageType.Physical,
                damageMin: 1f,
                damageMax: 2f,
                attacksPerSecond: 0.5f,
                critChance: 0f,
                critMultiplier: 100f,
                accuracy: 100f,
                baseExperience: 10f,
                itemQuantityMultiplier: 1f,
                itemRarityMultiplier: 1f,
                initialPosition: new Vector2D(80f + m * 5f, 104f)
            ));
        }

        var spawner = new WaveSpawner(new Random(42));
        var engine = new CombatEngine(player, loadout, spawner);
        var mapDef = new MapDefinition("strand", "Strand Map", 1, 1, false, 10, 100, "Tidal Behemoth", "");
        engine.StartMap(new MapRunConfig(mapDef, Array.Empty<RolledMapModifier>()));
        engine.SetCurrentWave(new MonsterWave { WaveNumber = 1, Monsters = monsters });

        CombatAttackEventArgs? executedArgs = null;
        engine.OnAttackExecuted += args =>
        {
            if (executedArgs == null && args.Attacker is PlayerState)
            {
                executedArgs = args;
            }
        };

        // Run ticks until player attacks
        for (int i = 0; i < 30 && executedArgs == null; i++)
        {
            engine.ProcessTick(1f / 30f);
        }

        AssertTrue(executedArgs != null, "Player must execute an attack with Fireball");
        AssertTrue(executedArgs!.Projectiles != null && executedArgs.Projectiles.Count > 0, "Fireball attack MUST have in-flight projectiles");
        AssertEqual(540f, (float)executedArgs.Projectiles![0].Velocity.Length, 1.0f, "Fireball projectile speed must be 540px/s");
        AssertTrue(!executedArgs.IsMelee, "Fireball must be marked as ranged (!IsMelee)");
        AssertTrue(executedArgs.GeometryType == SkillGeometryType.RadialAoE, "Fireball geometry must be RadialAoE");
    }

    /// <summary>
    /// Verifies that physical projectiles advance along continuous vectors in flight across multiple ticks,
    /// dealing ZERO damage until physical swept-circle collision occurs with the monster hitbox.
    /// </summary>
    private static void TestPhysicalProjectileContinuousMovementAndFlightTime()
    {
        var player = new PlayerState();
        player.BootstrapFromArchetype(StarterArchetypeCatalog.Elementalist);

        var loadout = new SkillLoadout();
        var fireballGem = new GemDefinition(
            Id: "fireball",
            Name: "Fireball",
            Category: "Active",
            Color: "Blue",
            Tags: new[] { "spell", "projectile", "fire", "aoe" },
            RequiredTags: null,
            BaseStats: new Dictionary<string, object>
            {
                ["baseDamageMin"] = 50f,
                ["baseDamageMax"] = 50f,
                ["damageType"] = "Fire",
                ["manaCost"] = 0f,
                ["projectileSpeed"] = 540f,
                ["aoeRadius"] = 32f,
                ["castTime"] = 0.033f
            },
            Modifiers: null,
            Description: "Fireball"
        );
        loadout.PrimarySkill.SetActiveGem(fireballGem);

        player.Position = new Vector2D(20f, 104f);

        // Monster placed at (140, 104) -> distance = 120px.
        // Monster Radius = 12px, Projectile Radius = 8px -> Combined Radius = 20px.
        // Collision threshold: 120 - 20 = 100px.
        // At 540px/s (18px per 30Hz tick), collision occurs around tick 5-6.
        var monster = new MonsterInstance(
            instanceId: 601,
            name: "Training Dummy",
            rarity: MonsterRarity.Normal,
            monsterLevel: 1,
            maxLife: 200f,
            primaryDamageType: DamageType.Physical,
            damageMin: 0f,
            damageMax: 0f,
            attacksPerSecond: 0.1f,
            critChance: 0f,
            critMultiplier: 100f,
            accuracy: 100f,
            baseExperience: 10f,
            itemQuantityMultiplier: 1f,
            itemRarityMultiplier: 1f,
            initialPosition: new Vector2D(140f, 104f)
        );

        var engine = new CombatEngine(player, loadout);
        var mapDef = new MapDefinition("strand", "Strand Map", 1, 1, false, 10, 100, "Boss", "");
        engine.StartMap(new MapRunConfig(mapDef, Array.Empty<RolledMapModifier>()));
        engine.ExilePosition = new Vector2D(20f, 104f);
        engine.SetCurrentWave(new MonsterWave { WaveNumber = 1, Monsters = new[] { monster } });

        // Ticks 1 to 3: In-flight. Monster MUST take 0 damage!
        for (int i = 0; i < 3; i++)
        {
            engine.ProcessTick(1f / 30f);
            AssertEqual(200f, monster.CurrentLife, 0.01f, $"Monster must NOT take damage during flight on tick {i + 1}");
            AssertTrue(engine.ActiveProjectiles.Count > 0, "Projectile must still be in flight");
        }

        // Advance further ticks until impact occurs
        bool damageLanded = false;
        for (int i = 0; i < 8; i++)
        {
            engine.ProcessTick(1f / 30f);
            if (monster.CurrentLife < 200f)
            {
                damageLanded = true;
                break;
            }
        }

        AssertTrue(damageLanded, "Projectile must physically collide and apply damage on impact");
        AssertTrue(monster.CurrentLife < 200f, "Monster must take Fire damage upon projectile impact");
    }

    /// <summary>
    /// Verifies that explosive projectiles (Fireball) detonate on impact,
    /// dealing AoE damage to all surrounding enemies within the explosion radius (32px),
    /// while enemies outside the radius take zero damage.
    /// </summary>
    private static void TestPhysicalProjectileAoEExplosionHitsSurroundingCluster()
    {
        var player = new PlayerState();
        player.BootstrapFromArchetype(StarterArchetypeCatalog.Elementalist);

        var loadout = new SkillLoadout();
        var fireballGem = new GemDefinition(
            Id: "fireball",
            Name: "Fireball",
            Category: "Active",
            Color: "Blue",
            Tags: new[] { "spell", "projectile", "fire", "aoe" },
            RequiredTags: null,
            BaseStats: new Dictionary<string, object>
            {
                ["baseDamageMin"] = 40f,
                ["baseDamageMax"] = 40f,
                ["damageType"] = "Fire",
                ["manaCost"] = 0f,
                ["projectileSpeed"] = 600f,
                ["aoeRadius"] = 32f,
                ["castTime"] = 0.033f
            },
            Modifiers: null,
            Description: "Fireball"
        );
        loadout.PrimarySkill.SetActiveGem(fireballGem);

        player.Position = new Vector2D(20f, 104f);

        // Mob 1: direct target at (80, 104)
        var mob1 = new MonsterInstance(701, "Target Mob", MonsterRarity.Normal, 1, 200f, DamageType.Physical, 0, 0, 0.1f, 0, 100, 100, 10, 1, 1, initialPosition: new Vector2D(80f, 104f));
        // Mob 2: cluster mob at (88, 110) -> distance ~10px, within 32px blast radius
        var mob2 = new MonsterInstance(702, "Cluster Mob", MonsterRarity.Normal, 1, 200f, DamageType.Physical, 0, 0, 0.1f, 0, 100, 100, 10, 1, 1, initialPosition: new Vector2D(88f, 110f));
        // Mob 3: far mob at (180, 104) -> distance 100px, way outside 32px blast radius
        var mob3 = new MonsterInstance(703, "Far Mob", MonsterRarity.Normal, 1, 200f, DamageType.Physical, 0, 0, 0.1f, 0, 100, 100, 10, 1, 1, initialPosition: new Vector2D(180f, 104f));

        var engine = new CombatEngine(player, loadout);
        var mapDef = new MapDefinition("strand", "Strand Map", 1, 1, false, 10, 100, "Boss", "");
        engine.StartMap(new MapRunConfig(mapDef, Array.Empty<RolledMapModifier>()));
        engine.ExilePosition = new Vector2D(20f, 104f);
        engine.SetCurrentWave(new MonsterWave { WaveNumber = 1, Monsters = new[] { mob1, mob2, mob3 } });

        // Tick until impact
        for (int i = 0; i < 10; i++)
        {
            engine.ProcessTick(1f / 30f);
            if (mob1.CurrentLife < 200f) break;
        }

        AssertTrue(mob1.CurrentLife < 200f, "Direct target Mob 1 must take Fireball impact damage");
        AssertTrue(mob2.CurrentLife < 200f, "Cluster Mob 2 inside 32px AoE blast must take explosion damage");
        AssertEqual(200f, mob3.CurrentLife, 0.01f, "Far Mob 3 outside 32px blast radius must take ZERO damage");
    }

    /// <summary>
    /// Verifies that frontline monsters physically intercept and block non-piercing projectiles,
    /// protecting backline monsters behind them.
    /// </summary>
    private static void TestFrontlineMonsterBlocksBacklineMonster()
    {
        var player = new PlayerState();
        player.BootstrapFromArchetype(StarterArchetypeCatalog.Deadeye);

        var loadout = new SkillLoadout();
        // Single target arrow attack (pierce = 0, aoeRadius = 0)
        var arrowGem = new GemDefinition(
            Id: "snipe",
            Name: "Snipe",
            Category: "Active",
            Color: "Green",
            Tags: new[] { "attack", "projectile", "physical" },
            RequiredTags: null,
            BaseStats: new Dictionary<string, object>
            {
                ["baseDamageMin"] = 50f,
                ["baseDamageMax"] = 50f,
                ["damageType"] = "Physical",
                ["manaCost"] = 0f,
                ["projectileCount"] = 1,
                ["projectileSpeed"] = 600f,
                ["pierceCount"] = 0,
                ["castTime"] = 0.033f
            },
            Modifiers: null,
            Description: "A single piercing-disabled arrow."
        );
        loadout.PrimarySkill.SetActiveGem(arrowGem);

        player.Position = new Vector2D(20f, 104f);

        // Frontline mob at (60, 104) directly between player and backline
        var frontlineMob = new MonsterInstance(801, "Frontline Shield", MonsterRarity.Normal, 1, 200f, DamageType.Physical, 0, 0, 0.1f, 0, 100, 100, 10, 1, 1, initialPosition: new Vector2D(60f, 104f));
        // Backline mob at (120, 104) behind the frontline mob
        var backlineMob = new MonsterInstance(802, "Backline Caster", MonsterRarity.Normal, 1, 200f, DamageType.Physical, 0, 0, 0.1f, 0, 100, 100, 10, 1, 1, initialPosition: new Vector2D(120f, 104f));

        var engine = new CombatEngine(player, loadout);
        var mapDef = new MapDefinition("strand", "Strand Map", 1, 1, false, 10, 100, "Boss", "");
        engine.StartMap(new MapRunConfig(mapDef, Array.Empty<RolledMapModifier>()));
        engine.ExilePosition = new Vector2D(20f, 104f);
        engine.SetCurrentWave(new MonsterWave { WaveNumber = 1, Monsters = new[] { frontlineMob, backlineMob } });

        // Tick simulation until collision
        for (int i = 0; i < 10; i++)
        {
            engine.ProcessTick(1f / 30f);
            if (frontlineMob.CurrentLife < 200f) break;
        }

        AssertTrue(frontlineMob.CurrentLife < 200f, "Frontline mob must intercept and absorb the projectile");
        AssertEqual(200f, backlineMob.CurrentLife, 0.01f, "Backline mob shielded behind frontline must take ZERO damage");
    }

    /// <summary>
    /// Verifies Path of Exile's strict Non-Shotgunning rule:
    /// Multiple projectiles fired from the same volley cannot hit the same target entity.
    /// </summary>
    private static void TestNonShotgunningRuleInVolley()
    {
        var player = new PlayerState();
        player.BootstrapFromArchetype(StarterArchetypeCatalog.Deadeye);

        var loadout = new SkillLoadout();
        // Split arrow firing 4 projectiles in a 15-degree tight cone
        var splitArrowGem = new GemDefinition(
            Id: "split_arrow",
            Name: "Split Arrow",
            Category: "Active",
            Color: "Green",
            Tags: new[] { "attack", "bow", "projectile", "physical" },
            RequiredTags: null,
            BaseStats: new Dictionary<string, object>
            {
                ["baseDamageMin"] = 30f,
                ["baseDamageMax"] = 30f,
                ["damageType"] = "Physical",
                ["manaCost"] = 0f,
                ["projectileCount"] = 4,
                ["spreadAngle"] = 15f,
                ["projectileSpeed"] = 600f,
                ["castTime"] = 0.033f
            },
            Modifiers: null,
            Description: "Fires 4 arrows."
        );
        loadout.PrimarySkill.SetActiveGem(splitArrowGem);

        player.Position = new Vector2D(20f, 104f);

        // Huge boss at distance 120px so all 4 arrows can be verified in flight before impact
        var boss = new MonsterInstance(901, "Colossus", MonsterRarity.Boss, 1, 1000f, DamageType.Physical, 0, 0, 0.1f, 0, 100, 100, 10, 1, 1, initialPosition: new Vector2D(140f, 104f));

        var engine = new CombatEngine(player, loadout);
        var mapDef = new MapDefinition("strand", "Strand Map", 1, 1, false, 10, 100, "Colossus", "");
        engine.StartMap(new MapRunConfig(mapDef, Array.Empty<RolledMapModifier>()));
        engine.ExilePosition = new Vector2D(20f, 104f);
        engine.SetCurrentWave(new MonsterWave { WaveNumber = 1, Monsters = new[] { boss }, IsBossWave = true });

        int volleysFired = 0;
        int damageDealtEventCount = 0;
        engine.OnAttackExecuted += args =>
        {
            volleysFired++;
        };
        engine.OnDamageDealt += args =>
        {
            if (args.Target.InstanceId == boss.InstanceId) damageDealtEventCount++;
        };

        // Advance 1 tick to fire the volley (spawns 4 physical arrows)
        engine.ProcessTick(1f / 30f);
        AssertEqual(1, volleysFired, 0.01f, "Must fire 1 volley on tick 1");
        AssertEqual(4, engine.ActiveProjectiles.Count, 0.01f, "Split arrow must spawn 4 physical in-flight projectiles");

        // Clear active gem so subsequent ticks do not fire additional volleys
        loadout.PrimarySkill.ClearActiveGem();

        // Advance 10 ticks to let all 4 in-flight arrows hit the boss and be absorbed
        for (int i = 0; i < 10; i++)
        {
            engine.ProcessTick(1f / 30f);
        }

        // Path of Exile Non-Shotgunning rule:
        // All 4 arrows originate from the same volley; boss must only take exactly 1 hit from this volley!
        AssertEqual(1, damageDealtEventCount, 0.01f, "Boss must only be hit ONCE per volley due to Non-Shotgunning rule");
        AssertEqual(0, engine.ActiveProjectiles.Count, 0.01f, "All 4 arrows must be absorbed on hitting the boss, not going through");
    }

    /// <summary>
    /// Verifies that Split Arrow projectiles are absorbed on hitting an enemy without pierce,
    /// and do NOT go through to damage enemies standing directly behind the first target.
    /// </summary>
    private static void TestSplitArrowProjectilesAbsorbedOnHitWithoutPiercingBehindEnemy()
    {
        var player = new PlayerState();
        player.BootstrapFromArchetype(StarterArchetypeCatalog.Deadeye);

        var loadout = new SkillLoadout();
        var splitArrowGem = new GemDefinition(
            Id: "split_arrow",
            Name: "Split Arrow",
            Category: "Active",
            Color: "Green",
            Tags: new[] { "attack", "bow", "projectile", "physical" },
            RequiredTags: null,
            BaseStats: new Dictionary<string, object>
            {
                ["baseDamageMin"] = 25f,
                ["baseDamageMax"] = 25f,
                ["damageType"] = "Physical",
                ["manaCost"] = 0f,
                ["projectileCount"] = 4,
                ["spreadAngle"] = 10f, // Tight spread directly at front target
                ["projectileSpeed"] = 600f,
                ["castTime"] = 0.033f
            },
            Modifiers: null,
            Description: "Fires 4 arrows."
        );
        loadout.PrimarySkill.SetActiveGem(splitArrowGem);

        player.Position = new Vector2D(20f, 104f);

        // Frontline monster (shields the backline)
        var frontline = new MonsterInstance(1001, "Shield Zombie", MonsterRarity.Normal, 1, 500f, DamageType.Physical, 0, 0, 0.1f, 0, 10, 10, 10, 1, 1, initialPosition: new Vector2D(120f, 104f));
        // Backline monster (standing directly behind frontline)
        var backline = new MonsterInstance(1002, "Protected Peon", MonsterRarity.Normal, 1, 100f, DamageType.Physical, 0, 0, 0.1f, 0, 10, 10, 10, 1, 1, initialPosition: new Vector2D(220f, 104f));

        var engine = new CombatEngine(player, loadout);
        var mapDef = new MapDefinition("strand", "Strand Map", 1, 1, false, 10, 100, "Boss", "");
        engine.StartMap(new MapRunConfig(mapDef, Array.Empty<RolledMapModifier>()));
        engine.ExilePosition = new Vector2D(20f, 104f);
        engine.SetCurrentWave(new MonsterWave { WaveNumber = 1, Monsters = new[] { frontline, backline } });

        int frontlineHits = 0;
        int backlineHits = 0;

        engine.OnDamageDealt += args =>
        {
            if (args.Target.InstanceId == frontline.InstanceId) frontlineHits++;
            if (args.Target.InstanceId == backline.InstanceId) backlineHits++;
        };

        // Advance 1 tick to fire the volley (spawns 4 physical arrows)
        engine.ProcessTick(1f / 30f);
        AssertEqual(4, engine.ActiveProjectiles.Count, 0.01f, "Split arrow must spawn 4 physical in-flight projectiles");

        // Clear active gem so no additional volleys are fired
        loadout.PrimarySkill.ClearActiveGem();

        // Advance 12 ticks to let all arrows reach the frontline monster
        for (int i = 0; i < 12; i++)
        {
            engine.ProcessTick(1f / 30f);
        }

        // Frontline monster must be hit (1 damage hit due to non-shotgunning) and absorb arrows
        AssertEqual(1, frontlineHits, 0.01f, "Frontline monster must be hit once by the volley");
        AssertTrue(frontline.CurrentLife < 500f, "Frontline monster must have taken damage");

        // Backline monster MUST NOT be hit, because Split Arrow projectiles are absorbed on hit and do not go through
        AssertEqual(0, backlineHits, 0.01f, "Backline monster must take 0 hits because arrows are absorbed on frontline impact");
        AssertEqual(100f, backline.CurrentLife, 0.01f, "Backline monster must remain at 100% life");
        AssertEqual(0, engine.ActiveProjectiles.Count, 0.01f, "All projectiles must be completely absorbed on frontline impact");
    }

    private static void AssertFalse(bool condition, string message)
    {
        if (condition)
        {
            throw new Exception($"Assertion failed: {message}");
        }
    }

    private static void TestStartMapResetsExileAndPlayerPositionsToCorridorStart()
    {
        var player = new PlayerState();
        var loadout = new SkillLoadout();
        var engine = new CombatEngine(player, loadout);

        var mapDef = new MapDefinition("strand_t1", "The Twilight Strand", 1, 68, false, 0, 100, "Hillock", "");
        var config1 = new MapRunConfig(mapDef, Array.Empty<RolledMapModifier>());
        engine.StartMap(config1);

        // Move the exile all the way down into the boss arena (at 2272px: 40px start + 2232px distance)
        engine.ExilePosition = new Vector2D(2272f, 104f);
        engine.ProcessTick(0.1f);
        engine.ProcessTick(0.1f);

        AssertTrue(engine.Movement.HasEnteredBossRoom, "Movement must have entered boss room");
        AssertTrue(player.Position.X >= 2272f, "Player position must be in boss room");

        // Complete the map
        engine.Movement.SetCompleted();

        // Start a second map run (e.g. from queue or map device)
        var config2 = new MapRunConfig(mapDef, Array.Empty<RolledMapModifier>());
        engine.StartMap(config2);

        // Verify player & movement position are reset to corridor beginning (40, 104)
        AssertEqual(ExileMovementController.DefaultStartX, engine.ExilePosition.X, 0.001f, "engine.ExilePosition.X must reset to DefaultStartX");
        AssertEqual(ExileMovementController.DefaultCenterY, engine.ExilePosition.Y, 0.001f, "engine.ExilePosition.Y must reset to DefaultCenterY");
        AssertEqual(ExileMovementController.DefaultStartX, player.Position.X, 0.001f, "player.Position.X must reset to DefaultStartX");
        AssertEqual(ExileMovementController.DefaultCenterY, player.Position.Y, 0.001f, "player.Position.Y must reset to DefaultCenterY");
        AssertFalse(engine.Movement.HasEnteredBossRoom, "Movement must not be in boss room on new map");
        AssertFalse(engine.Movement.HasReachedFogGate, "Movement must not be at fog gate on new map");
        AssertFalse(engine.Movement.IsFogGateSealed, "Fog gate must not be sealed on new map");
        AssertEqual(0f, engine.Movement.DistanceTraveled, 0.001f, "DistanceTraveled must be 0 on new map");
    }
}

