using System;
using System.Collections.Generic;
using IdleExile.Core.Models;
using IdleExile.Core.Simulation;
using IdleExile.Core.State;

namespace IdleExile.Core.Tests;

/// <summary>
/// Unit tests for Task 2.5: Real-World Movement Controller (ExileMovementController).
/// Validates:
/// 1. Speed scaling formula: v_move = v_base * (1 + sum %Inc/100) * prod(1 + %More/100) * CC_mult.
/// 2. World-space corridor translation, distance traveled, bounds clamping, and map progress percentage.
/// 3. Zero-APM State Machine: Marching -> Engaging (stopping at optimal range) -> Advancing.
/// 4. Dynamic optimal engagement range calculation per skill geometry (Melee 35px vs Projectile 165px).
/// 5. Fog Gate threshold (85% L_map) and Boss Arena entry detection (93% L_map).
/// 6. Player death and respawn movement gating.
/// 7. Full 30Hz CombatEngine discrete tick integration.
/// </summary>
public static class ExileMovementControllerTests
{
    public static void RunAll()
    {
        Console.WriteLine("[Tests] Running ExileMovementControllerTests (Task 2.5)...");

        TestMovementSpeedFormulaScaling();
        TestWorldCoordinatesAndCorridorBounds();
        TestStateMachineMarchingToEngaging();
        TestOptimalRangePerSkillGeometry();
        TestStateMachineEngagingToAdvancing();
        TestFogGateThresholdAndBossArenaEntry();
        TestPlayerDeathHaltsMovement();
        TestCombatEngine30HzIntegration();

        Console.WriteLine("[Tests] All ExileMovementControllerTests PASSED successfully!");
    }

    private static void AssertEqual(float expected, float actual, float tolerance = 0.001f, string message = "")
    {
        if (MathF.Abs(expected - actual) > tolerance)
        {
            throw new Exception($"Assertion failed: Expected {expected:F4}, got {actual:F4}. {message}");
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
    //  1. Speed Scaling Formula Tests (PoE Rules)
    // ─────────────────────────────────────────────

    private static void TestMovementSpeedFormulaScaling()
    {
        var player = new PlayerState();
        var controller = new ExileMovementController(player, baseSpeed: 120.0f);

        // Baseline speed: 120 px/s
        float baseSpeed = controller.CalculateCurrentSpeed(player);
        AssertEqual(120.0f, baseSpeed, 0.01f, "Baseline speed must be 120 px/s");

        // Add 25% Increased Movement Speed
        controller.SpeedModifiers.AddIncreased(25.0f);
        float incSpeed = controller.CalculateCurrentSpeed(player);
        AssertEqual(150.0f, incSpeed, 0.01f, "120 * 1.25 = 150 px/s");

        // Add 10% More Movement Speed
        controller.SpeedModifiers.AddMore(10.0f);
        float moreSpeed = controller.CalculateCurrentSpeed(player);
        AssertEqual(165.0f, moreSpeed, 0.01f, "120 * 1.25 * 1.10 = 165 px/s");

        // Add 15% Reduced Movement Speed
        controller.SpeedModifiers.AddReduced(15.0f);
        float redSpeed = controller.CalculateCurrentSpeed(player);
        // (1 + (25 - 15) / 100) * 1.10 = 1.10 * 1.10 = 1.21 * 120 = 145.2
        AssertEqual(145.2f, redSpeed, 0.01f, "120 * 1.10 * 1.10 = 145.2 px/s");

        // Crowd Control: Chill (30% Chill Effect -> 70% speed multiplier)
        player.Ailments.ApplyAilment(new AilmentInstance(AilmentType.Chill, DamageType.Cold, 0f, 30.0f, 3.0f, false));
        float chilledSpeed = controller.CalculateCurrentSpeed(player);
        AssertEqual(145.2f * 0.70f, chilledSpeed, 0.05f, "Chill must reduce speed by 30%");

        // Crowd Control: Freeze -> Speed becomes exactly 0
        player.Ailments.ApplyAilment(new AilmentInstance(AilmentType.Freeze, DamageType.Cold, 0f, 100.0f, 2.0f, false, PreventsAction: true));
        float frozenSpeed = controller.CalculateCurrentSpeed(player);
        AssertEqual(0.0f, frozenSpeed, 0.001f, "Freeze must reduce movement speed to exactly 0");
    }

    // ─────────────────────────────────────────────
    //  2. World Coordinates & Corridor Bounds Tests
    // ─────────────────────────────────────────────

    private static void TestWorldCoordinatesAndCorridorBounds()
    {
        var player = new PlayerState();
        var controller = new ExileMovementController(player, mapLength: 2400.0f, baseSpeed: 100.0f);
        controller.StartMap(2400.0f, new Vector2D(220.0f, 40.0f));

        AssertEqual(220.0f, controller.Position.X, 0.01f, "Initial X should be 220");
        AssertEqual(40.0f, controller.Position.Y, 0.01f, "Initial Y should be 40");
        AssertEqual(0.0f, controller.DistanceTraveled, 0.01f, "Initial distance traveled should be 0");
        AssertEqual(0.0f, controller.MapProgressPercent, 0.01f, "Initial progress percent should be 0%");

        // Simulate 2.0 seconds of marching without monsters (speed 100 px/s -> advances 200px)
        controller.Tick(2.0f, Array.Empty<MonsterInstance>(), player);

        AssertEqual(240.0f, controller.Position.Y, 0.5f, "Y position should advance by 200px to 240px");
        AssertEqual(200.0f, controller.DistanceTraveled, 0.5f, "Distance traveled should be 200px");
        AssertEqual((200f / 2400f) * 100f, controller.MapProgressPercent, 0.1f, "Progress percent should reflect distance / L_map");

        // Verify lateral X clamping: attempt to push position beyond bounds
        controller.Position = new Vector2D(10.0f, 500.0f); // X < MinCorridorX (40)
        controller.Tick(0.033f, Array.Empty<MonsterInstance>(), player);
        AssertTrue(controller.Position.X >= ExileMovementController.MinCorridorX, "X must not be less than MinCorridorX (40)");

        controller.Position = new Vector2D(500.0f, 500.0f); // X > MaxCorridorX (400)
        controller.Tick(0.033f, Array.Empty<MonsterInstance>(), player);
        AssertTrue(controller.Position.X <= ExileMovementController.MaxCorridorX, "X must not exceed MaxCorridorX (400)");
    }

    // ─────────────────────────────────────────────
    //  3. State Machine: Marching -> Engaging Tests
    // ─────────────────────────────────────────────

    private static void TestStateMachineMarchingToEngaging()
    {
        var player = new PlayerState();
        var controller = new ExileMovementController(player, baseSpeed: 100.0f);
        controller.StartMap(2400.0f, new Vector2D(220.0f, 40.0f));

        AssertTrue(controller.CurrentState == ExileMovementState.Marching, "Should start in Marching state");

        // Setup a normal monster 150px ahead at (220, 190)
        var monster = new MonsterInstance(
            instanceId: 101,
            name: "Roa Scavenger",
            rarity: MonsterRarity.Normal,
            monsterLevel: 1,
            maxLife: 100.0f,
            primaryDamageType: DamageType.Physical,
            damageMin: 2f,
            damageMax: 4f,
            attacksPerSecond: 1.0f,
            critChance: 5f,
            critMultiplier: 130f,
            accuracy: 90f,
            baseExperience: 10f,
            itemQuantityMultiplier: 1f,
            itemRarityMultiplier: 1f
        )
        {
            Position = new Vector2D(220.0f, 190.0f)
        };

        var monsters = new[] { monster };
        controller.OptimalRangeOverride = 35.0f; // Melee strike range

        bool stateChangedToEngaging = false;
        controller.OnStateChanged += (oldS, newS) =>
        {
            if (newS == ExileMovementState.Engaging)
                stateChangedToEngaging = true;
        };

        // Tick 1: Monster is 150px away (> 35px), Exile should close distance
        controller.Tick(0.5f, monsters, player); // moves 50px forward
        AssertTrue(controller.CurrentState == ExileMovementState.Marching, "Still closing distance");
        AssertTrue(controller.Position.Y > 40.0f, "Should have advanced forward");

        // Tick 2: Step until within 35px
        // Distance remaining: (190 - 90) = 100px. At 100px/s, in 0.7s Exile closes 70px -> distance 30px <= 35px.
        controller.Tick(0.7f, monsters, player);

        AssertTrue(stateChangedToEngaging, "Must transition to Engaging once within optimal range");
        AssertTrue(controller.CurrentState == ExileMovementState.Engaging, "CurrentState must be Engaging");
        AssertEqual(0.0f, controller.Velocity.Length, 0.001f, "Velocity must drop to 0 when Engaging");
    }

    // ─────────────────────────────────────────────
    //  4. Optimal Range Per Skill Geometry
    // ─────────────────────────────────────────────

    private static void TestOptimalRangePerSkillGeometry()
    {
        var player = new PlayerState();
        var loadout = new SkillLoadout();
        var controller = new ExileMovementController(player, loadout);

        // 1. Melee Skill (e.g. Strike / Cleave)
        var meleeGem = new GemDefinition(
            Id: "heavy_strike",
            Name: "Heavy Strike",
            Category: "Active",
            Color: "Red",
            Tags: new[] { "Attack", "Melee", "Strike", "Physical" },
            RequiredTags: null,
            BaseStats: new Dictionary<string, object> { ["damageType"] = "Physical" },
            Modifiers: null,
            Description: "Heavy Strike"
        );
        loadout.PrimarySkill.SetActiveGem(meleeGem);
        AssertEqual(35.0f, controller.GetOptimalEngagementRange(), 0.1f, "Melee skill optimal range must be 35px");

        // 2. Projectile / Ranged Skill (e.g. Split Arrow / Spark)
        var rangedGem = new GemDefinition(
            Id: "spark",
            Name: "Spark",
            Category: "Active",
            Color: "Blue",
            Tags: new[] { "Spell", "Projectile", "Lightning" },
            RequiredTags: null,
            BaseStats: new Dictionary<string, object> { ["projectileCount"] = 3f, ["spreadAngle"] = 30f, ["damageType"] = "Lightning" },
            Modifiers: null,
            Description: "Spark"
        );
        loadout.PrimarySkill.SetActiveGem(rangedGem);
        AssertEqual(165.0f, controller.GetOptimalEngagementRange(), 0.1f, "Ranged projectile skill optimal range should be ~165px");
    }

    // ─────────────────────────────────────────────
    //  5. State Machine: Engaging -> Advancing Tests
    // ─────────────────────────────────────────────

    private static void TestStateMachineEngagingToAdvancing()
    {
        var player = new PlayerState();
        var controller = new ExileMovementController(player, baseSpeed: 100.0f);
        controller.StartMap(2400.0f, new Vector2D(220.0f, 40.0f));

        var monster = new MonsterInstance(
            instanceId: 102,
            name: "Dune Swarmer",
            rarity: MonsterRarity.Normal,
            monsterLevel: 1,
            maxLife: 50.0f,
            primaryDamageType: DamageType.Physical,
            damageMin: 1f,
            damageMax: 2f,
            attacksPerSecond: 1.0f,
            critChance: 0f,
            critMultiplier: 100f,
            accuracy: 80f,
            baseExperience: 10f,
            itemQuantityMultiplier: 1f,
            itemRarityMultiplier: 1f
        )
        {
            Position = new Vector2D(220.0f, 70.0f) // exactly 30px away <= 35px
        };

        controller.OptimalRangeOverride = 35.0f;

        // Transition to Engaging
        controller.Tick(0.033f, new[] { monster }, player);
        AssertTrue(controller.CurrentState == ExileMovementState.Engaging, "Must be Engaging target");

        bool packClearedFired = false;
        controller.OnPackCleared += () => packClearedFired = true;

        // Slay monster (deal lethal damage)
        monster.TakeRawDamage(100.0f);
        AssertTrue(!monster.IsAlive, "Monster must be dead");

        // Next tick: Exile detects no living hostiles in pack -> transitions to Advancing
        controller.Tick(0.033f, new[] { monster }, player);

        AssertTrue(packClearedFired, "OnPackCleared event must fire when pack is slain");
        AssertTrue(controller.CurrentState == ExileMovementState.Advancing, "Must transition from Engaging to Advancing");

        // Subsequent ticks with no hostiles: transitions smoothly to Marching down the corridor
        controller.Tick(0.033f, Array.Empty<MonsterInstance>(), player);
        AssertTrue(controller.CurrentState == ExileMovementState.Marching, "Must resume Marching when path is clear");
    }

    // ─────────────────────────────────────────────
    //  6. Fog Gate Threshold & Boss Arena Entry
    // ─────────────────────────────────────────────

    private static void TestFogGateThresholdAndBossArenaEntry()
    {
        var player = new PlayerState();
        var controller = new ExileMovementController(player, mapLength: 2000.0f, baseSpeed: 100.0f);
        controller.StartMap(2000.0f, new Vector2D(220.0f, 0.0f));

        // Fog Gate is at 85% of 2000px = 1700px
        AssertEqual(1700.0f, controller.FogGateDistance, 0.1f, "Fog Gate distance should be 1700px");
        // Boss Arena is at 93% of 2000px = 1860px
        AssertEqual(1860.0f, controller.BossArenaDistance, 0.1f, "Boss Arena distance should be 1860px");

        bool fogGateCrossed = false;
        controller.OnFogGateCrossed += () => fogGateCrossed = true;

        bool bossArenaEntered = false;
        controller.OnBossArenaEntered += () => bossArenaEntered = true;

        // Fast-forward position to just before Fog Gate (1690px)
        controller.Position = new Vector2D(220.0f, 1690.0f);
        controller.Tick(0.05f, Array.Empty<MonsterInstance>(), player); // advances 5px to 1695px
        AssertTrue(!fogGateCrossed, "Fog Gate should not be crossed yet at 1695px");

        // Step past 1700px
        controller.Tick(0.1f, Array.Empty<MonsterInstance>(), player); // advances 10px to 1705px
        AssertTrue(fogGateCrossed, "OnFogGateCrossed must fire when reaching 85% map progress");
        AssertTrue(controller.HasReachedFogGate, "HasReachedFogGate must be true");
        AssertTrue(controller.CurrentState == ExileMovementState.EnteringBossRoom, "State should be EnteringBossRoom");

        // Fast-forward towards Boss Arena center (1860px)
        controller.Position = new Vector2D(220.0f, 1850.0f); // 10px from center
        controller.Tick(0.05f, Array.Empty<MonsterInstance>(), player);

        AssertTrue(bossArenaEntered, "OnBossArenaEntered must fire when entering boss arena center");
        AssertTrue(controller.HasEnteredBossRoom, "HasEnteredBossRoom must be true");
        AssertTrue(controller.CurrentState == ExileMovementState.InBossArena, "State should be InBossArena");
    }

    // ─────────────────────────────────────────────
    //  7. Player Death Halts Movement
    // ─────────────────────────────────────────────

    private static void TestPlayerDeathHaltsMovement()
    {
        var player = new PlayerState();
        var controller = new ExileMovementController(player, baseSpeed: 120.0f);
        controller.StartMap(2400.0f, new Vector2D(220.0f, 40.0f));

        AssertTrue(controller.CurrentState == ExileMovementState.Marching, "Should be Marching");

        // Player dies
        player.ApplyDamage(1000.0f, isChaos: false, chaosDoesNotBypassEs: false, mindOverMatterPercent: 0f);
        AssertTrue(!player.IsAlive, "Player must be dead");

        controller.Tick(0.033f, Array.Empty<MonsterInstance>(), player);

        AssertTrue(controller.CurrentState == ExileMovementState.Dead, "State must be Dead");
        AssertEqual(0.0f, controller.Velocity.Length, 0.001f, "Velocity must be 0 when Dead");
        AssertEqual(0.0f, controller.CurrentSpeed, 0.001f, "CurrentSpeed must be 0 when Dead");

        // When player respawns
        player.RestoreAllPools();
        controller.SetAlive();
        AssertTrue(controller.CurrentState == ExileMovementState.Marching, "Must resume Marching upon respawn");
    }

    // ─────────────────────────────────────────────
    //  8. Full 30Hz CombatEngine Integration
    // ─────────────────────────────────────────────

    private static void TestCombatEngine30HzIntegration()
    {
        var player = new PlayerState();
        var loadout = new SkillLoadout();
        var engine = new CombatEngine(player, loadout);

        AssertTrue(engine.Movement != null, "CombatEngine must expose Movement controller");
        AssertEqual(engine.ExilePosition.X, engine.Movement!.Position.X, 0.001f, "ExilePosition and Movement.Position must match");

        var mapDef = new MapDefinition("strand", "Strand Map", 1, 1, false, 10, 100, "Tidal Behemoth", "");
        engine.StartMap(new MapRunConfig(mapDef, Array.Empty<RolledMapModifier>()));
        engine.ExilePosition = new Vector2D(220f, 40f);

        // Position a monster pack ahead along the corridor (downwards at Y = 280)
        var waveMonsters = new List<MonsterInstance>
        {
            new MonsterInstance(
                instanceId: 501,
                name: "Strand Roa",
                rarity: MonsterRarity.Normal,
                monsterLevel: 1,
                maxLife: 200f,
                primaryDamageType: DamageType.Physical,
                damageMin: 1f,
                damageMax: 2f,
                attacksPerSecond: 1f,
                critChance: 0f,
                critMultiplier: 100f,
                accuracy: 90f,
                baseExperience: 10f,
                itemQuantityMultiplier: 1f,
                itemRarityMultiplier: 1f
            )
            {
                Position = new Vector2D(220f, 280f)
            }
        };
        engine.SetCurrentWave(new MonsterWave { WaveNumber = 1, Monsters = waveMonsters });

        AssertTrue(engine.Movement!.CurrentState == ExileMovementState.Marching, "Movement must start Marching on map start");

        // Run 30 ticks (1.0 second of simulation)
        float dt = 1f / 30f;
        for (int i = 0; i < 30; i++)
        {
            engine.ProcessTick(dt);
        }

        // Verify movement advanced forward along corridor
        AssertTrue(engine.Movement.DistanceTraveled > 0f, "Exile must have traveled distance along corridor");
        AssertTrue(engine.ExilePosition.Y > 40.0f, "ExilePosition must reflect movement along corridor");
        AssertEqual(player.Position.Y, engine.Movement.Position.Y, 0.001f, "Player.Position must synchronize with Movement.Position");
    }
}
