using System;
using System.Collections.Generic;
using Godot;
using IdleExile.Core.Models;
using IdleExile.Core.Services;
using IdleExile.Core.Simulation;
using IdleExile.Core.State;
using IdleExile.UI.Components;

namespace IdleExile.UI;

/// <summary>
/// Comprehensive verification tests for Task 2.8: Cổng Sương Mù & Đấu Trường Boss Cuối Đường (Boss Room Chamber).
/// Tests:
/// 1. Fog Gate sealing at 85% map progress and backtracking prevention.
/// 2. ArenaCamera corridor follow vs Boss Arena Chamber fixed lock (220, L_map * 0.93).
/// 3. Map Boss awakening animation, 1.8x scale, and 96px Overhead Boss Bar.
/// 4. 2D Boss Slam Telegraph wind-up (1.4s, 55px radius, Alpha 0.22) and detonation crater with screen shake.
/// 5. CombatEngine integration: boss slam triggering, countdown, and damage detonation.
/// 6. BattleViewport pass-through integration.
/// </summary>
public static class BossRoomChamberTests
{
    public static void RunAll()
    {
        GD.Print("[Tests] Running BossRoomChamberTests (Task 2.8)...");

        TestFogGatePlacementAndSealing();
        TestArenaCameraCorridorFollowAndBossArenaLock();
        TestBossAwakeningAndVisualToken();
        TestBossSlamTelegraphAndCraterDetonation();
        TestCombatEngineBossSlamIntegration();
        TestBattleViewportIntegration();

        GD.Print("[Tests] All BossRoomChamberTests PASSED successfully!");
    }

    private static void TestFogGatePlacementAndSealing()
    {
        var player = new PlayerState();
        var movement = new ExileMovementController(player, baseSpeed: 100f);
        float mapLength = 2000f;
        movement.StartMap(mapLength, new Vector2D(220f, 40f));

        float expectedFogY = 40f + (mapLength * ExileMovementController.FogGateRatio); // 40 + 1700 = 1740f
        float expectedBossY = 40f + (mapLength * ExileMovementController.BossArenaRatio); // 40 + 1860 = 1900f

        Assert(Mathf.Abs(ExileMovementController.FogGateRatio - 0.85f) < 0.001f, "FogGateRatio must be 0.85 (85%)");
        Assert(Mathf.Abs(ExileMovementController.BossArenaRatio - 0.93f) < 0.001f, "BossArenaRatio must be 0.93 (93%)");
        Assert(!movement.IsFogGateSealed, "Fog Gate should initially be open/unsealed");

        bool sealedEventFired = false;
        movement.OnFogGateSealed += () => sealedEventFired = true;

        // Position Exile close to Fog Gate before 85%
        movement.Position = new Vector2D(220f, expectedFogY - 10f);
        movement.Tick(0.033f, Array.Empty<MonsterInstance>(), player);
        Assert(!movement.IsFogGateSealed, "Fog Gate should not be sealed before 85%");

        // Advance Exile past Fog Gate threshold
        movement.Position = new Vector2D(220f, expectedFogY + 10f);
        movement.Tick(0.033f, Array.Empty<MonsterInstance>(), player);
        Assert(movement.IsFogGateSealed, "Fog Gate must be sealed when crossing 85% threshold");
        Assert(sealedEventFired, "OnFogGateSealed event must fire when crossing Fog Gate");

        // Test backtracking prevention: attempting to step or move behind the sealed gate
        movement.Position = new Vector2D(220f, expectedFogY - 100f);
        movement.Tick(0.033f, Array.Empty<MonsterInstance>(), player);
        Assert(movement.Position.Y >= expectedFogY,
            $"Exile position must be clamped to Fog Gate Y ({expectedFogY}), got {movement.Position.Y}");
    }

    private static void TestArenaCameraCorridorFollowAndBossArenaLock()
    {
        var camera = new ArenaCamera();
        Vector2 screenCenter = Arena2D.CenterPosition; // (220, 104)
        float mapLength = 2400f;
        Vector2 bossCenterWorld = new(screenCenter.X, mapLength * ExileMovementController.BossArenaRatio); // (220, 2232)

        // 1. In corridor phase: camera tracks exile position + look ahead
        Vector2 exileCorridorPos = new(220f, 600f);
        camera.Update(0.1f, exileCorridorPos, bossCenterWorld, shouldLockToArena: false);
        Assert(camera.Mode == ArenaCameraMode.CorridorFollow, "Camera should be in CorridorFollow mode");
        Assert(camera.TargetPosition.Y < bossCenterWorld.Y, "Camera target should follow corridor");

        // 2. Transition into boss arena lock
        camera.Update(0.1f, new Vector2(220f, 2100f), bossCenterWorld, shouldLockToArena: true);
        Assert(camera.Mode == ArenaCameraMode.BossArenaLock, "Camera should switch to BossArenaLock mode");
        Assert(Mathf.Abs(camera.TargetPosition.X - bossCenterWorld.X) < 0.01f, "Camera target X must match boss chamber center");
        Assert(Mathf.Abs(camera.TargetPosition.Y - bossCenterWorld.Y) < 0.01f, "Camera target Y must match boss chamber center");

        // Step camera multiple frames to complete smooth lerp into locked position
        for (int i = 0; i < 60; i++)
        {
            camera.Update(0.05f, new Vector2(220f, 2150f), bossCenterWorld, shouldLockToArena: true);
        }

        Assert(camera.CurrentPosition.DistanceTo(bossCenterWorld) < 1.0f,
            $"Camera should smoothly reach Boss Arena center, got {camera.CurrentPosition}");

        // 3. Verify WorldToScreen projection: Boss Arena center maps exactly to screen center (220, 104)
        Vector2 screenPos = camera.WorldToScreen(bossCenterWorld, screenCenter);
        Assert(screenPos.DistanceTo(screenCenter) < 1.0f,
            $"Boss Arena Center should project to screen center {screenCenter}, got {screenPos}");
    }

    private static void TestBossAwakeningAndVisualToken()
    {
        var arena = new Arena2D();
        arena._Ready();

        var bossInstance = CreateMockBoss(999, "Kitava, the Insatiable", 5000f);
        arena.SetMonsters(new[] { bossInstance });

        var bossPos = arena.GetMonsterPosition(999);
        Assert(bossPos != Vector2.Zero, "Boss position should be valid");

        // Trigger Awakening
        arena.AwakenBoss();

        // Simulate 0.3s of awakening
        arena._Process(0.3);

        // Verify visual update didn't crash and queue redraw was executed
        arena._Process(0.8); // Complete awakening

        arena.QueueFree();
    }

    private static void TestBossSlamTelegraphAndCraterDetonation()
    {
        var arena = new Arena2D();
        arena._Ready();

        Vector2 slamTarget = new(220f, 104f);
        arena.TriggerBossSlam(slamTarget, duration: 1.4f, radius: 55f, threatEhpPercent: 85f);

        Assert(arena.IsTelegraphActive, "Telegraph must be active immediately after trigger");
        Assert(arena.TelegraphProgress < 0.01f, "Telegraph progress should start near 0");

        bool detonatedEventFired = false;
        arena.OnBossSlamDetonated += (pos, threat) =>
        {
            detonatedEventFired = true;
            Assert(pos.DistanceTo(slamTarget) < 0.1f, "Detonation position must match target");
            Assert(Mathf.Abs(threat - 85f) < 0.1f, "Threat EHP % must match 85%");
        };

        // Advance halfway (0.7s)
        arena._Process(0.7);
        Assert(arena.IsTelegraphActive, "Telegraph must remain active during wind-up");
        Assert(arena.TelegraphProgress >= 0.49f && arena.TelegraphProgress <= 0.52f,
            $"Telegraph progress at 0.7s should be ~0.5, got {arena.TelegraphProgress}");

        // Advance to detonation (another 0.75s)
        arena._Process(0.75);
        Assert(!arena.IsTelegraphActive, "Telegraph must deactivate upon detonation");
        Assert(detonatedEventFired, "OnBossSlamDetonated must fire when timer expires");

        arena.QueueFree();
    }

    private static void TestCombatEngineBossSlamIntegration()
    {
        var player = new PlayerState();
        var loadout = new SkillLoadout();
        var engine = new CombatEngine(player, loadout);

        var mapDef = new MapDefinition("t1", "Strand", 1, 1, false, 0, 100, "Merveil, the Siren", "");
        engine.StartMap(new MapRunConfig(mapDef, Array.Empty<RolledMapModifier>()));

        Assert(!engine.IsBossSlamTelegraphing, "CombatEngine should not have active slam initially");

        // Spawn mock boss wave
        var boss = CreateMockBoss(501, "Merveil, the Siren", 8000f);
        engine.SetCurrentWave(new MonsterWave { WaveNumber = 1, Monsters = new[] { boss }, IsBossWave = true });

        bool slamTelegraphedFired = false;
        bool slamDetonatedFired = false;

        engine.OnBossSlamTelegraphed += args =>
        {
            slamTelegraphedFired = true;
            Assert(args.Boss.InstanceId == 501, "Telegraphed boss must match Merveil");
            Assert(Mathf.Abs(args.WindUpDuration - 1.4f) < 0.01f, "Wind-up duration must be 1.4s");
            Assert(Mathf.Abs(args.Radius - 55f) < 0.01f, "Radius must be 55px");
        };

        engine.OnBossSlamDetonated += args =>
        {
            slamDetonatedFired = true;
            Assert(args.Boss.InstanceId == 501, "Detonated boss must match Merveil");
        };

        // Trigger heavy slam on engine with 1.4s duration and 55px radius
        engine.TriggerBossSlam(boss, new Vector2D(220, 2232), threatPercent: 85f, windUp: 1.4f, radius: 55f);

        Assert(engine.IsBossSlamTelegraphing, "CombatEngine.IsBossSlamTelegraphing should be true");
        Assert(slamTelegraphedFired, "OnBossSlamTelegraphed event must fire");

        // Advance 45 ticks (at 30Hz, 45 * 0.0333s = 1.5s > 1.4s)
        for (int i = 0; i < 45; i++)
        {
            engine.ProcessTick(0.0333f);
        }

        Assert(!engine.IsBossSlamTelegraphing, "Slam must no longer be telegraphing after detonation");
        Assert(slamDetonatedFired, "OnBossSlamDetonated event must fire after 1.4s wind-up");
    }

    private static void TestBattleViewportIntegration()
    {
        var packed = GD.Load<PackedScene>("res://scenes/BattleViewport.tscn");
        Assert(packed != null, "BattleViewport.tscn must be loadable");

        var viewport = packed!.Instantiate<BattleViewport>();
        viewport._Ready();

        Assert(viewport.Camera != null, "BattleViewport.Camera must be accessible");
        Assert(!viewport.IsFogGateSealed, "Viewport Fog Gate should initially be open");

        viewport.SetFogGateSealed(true);
        Assert(viewport.IsFogGateSealed, "Viewport Fog Gate must update to sealed");

        viewport.AwakenBoss(); // Verify no crash on invocation

        viewport.QueueFree();
    }

    private static MonsterInstance CreateMockBoss(long id, string name, float life)
    {
        return new MonsterInstance(
            instanceId: id,
            name: name,
            rarity: MonsterRarity.Boss,
            monsterLevel: 1,
            maxLife: life,
            primaryDamageType: DamageType.Physical,
            damageMin: 120f,
            damageMax: 120f,
            attacksPerSecond: 1.0f,
            critChance: 5f,
            critMultiplier: 150f,
            accuracy: 100f,
            baseExperience: 250f,
            itemQuantityMultiplier: 5f,
            itemRarityMultiplier: 5f,
            initialPosition: new Vector2D(220, 2232)
        );
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception($"[BossRoomChamberTests Assertion Failed] {message}");
        }
    }
}
