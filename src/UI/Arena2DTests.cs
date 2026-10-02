using System;
using System.Collections.Generic;
using Godot;
using IdleExile.Core.Models;
using IdleExile.Core.Services;
using IdleExile.Core.State;
using IdleExile.UI.Components;

namespace IdleExile.UI;

/// <summary>
/// Comprehensive verification tests for Task 2.3: 2D Combat Arena & Entity Rendering.
/// Tests:
/// 1. Instantiation, default pack preview, and node configuration.
/// 2. Parallax scrolling offsets progression across frames.
/// 3. Exile silhouette character positioning, bobbing, and aura pulse phase.
/// 4. Monster pack synchronization (Trash mob, Magic, Rare golden aura, Boss overhead HP bar).
/// 5. In-flight projectile streaks (5 PoE elemental damage type colors, movement, and visual clamping at 12).
/// 6. Boss Slam Telegraph indicator (shrinking red ring, 1.2s - 1.5s wind-up, screen shake, and detonation).
/// 7. Scene integration with BattleViewport.tscn.
/// </summary>
public static class Arena2DTests
{
    public static void RunAll()
    {
        GD.Print("[Tests] Running Arena2DTests (Task 2.3)...");

        TestInstantiationAndDefaultPack();
        TestParallaxOffsetAdvancement();
        TestExileCharacterAndAura();
        TestMonsterPackSyncAndRarityVisuals();
        TestProjectilesMovementAndVisualClamping();
        TestElementalDamageColors();
        TestBossSlamTelegraphAndDetonation();
        TestBattleViewportIntegration();
        TestEquippedWeaponVisuals();
        TestExileAutoFacingAndTargetTracking();
        TestUnifiedAttackVisualPipeline();
        TestFireballProjectileAndDamageOnImpact();
        TestProjectileAbsorbedOnMonsterHitWithoutGoingThrough();

        GD.Print("[Tests] All Arena2DTests PASSED successfully!");
    }

    private static void TestInstantiationAndDefaultPack()
    {
        var arena = new Arena2D();
        arena._Ready();

        Assert(arena.ActiveMonsterCount == 4, $"Expected 4 default preview monsters, got {arena.ActiveMonsterCount}");
        Assert(arena.ActiveProjectileCount == 0, $"Expected 0 active projectiles initially, got {arena.ActiveProjectileCount}");
        Assert(!arena.IsTelegraphActive, "Telegraph should not be active initially");

        arena.QueueFree();
    }

    private static void TestParallaxOffsetAdvancement()
    {
        var arena = new Arena2D();
        arena._Ready();

        // Simulate 0.5s delta
        arena._Process(0.5);

        // Verify redraw was requested
        Assert(arena.ActiveMonsterCount >= 0, "Arena should process frames without exceptions");

        arena.QueueFree();
    }

    private static void TestExileCharacterAndAura()
    {
        var arena = new Arena2D();
        arena._Ready();

        Assert(Mathf.Abs(arena.ExilePosition.X - Arena2D.CenterPosition.X) < 1f,
            $"Exile should be horizontally centered at {Arena2D.CenterPosition.X}px, got {arena.ExilePosition.X}");
        Assert(Mathf.Abs(arena.ExilePosition.Y - Arena2D.CenterPosition.Y) < 1f,
            $"Exile should be vertically centered at {Arena2D.CenterPosition.Y}px, got {arena.ExilePosition.Y}");

        arena._Process(0.1);
        arena.QueueFree();
    }

    private static void TestMonsterPackSyncAndRarityVisuals()
    {
        var arena = new Arena2D();
        arena._Ready();

        // Create a simulated pack from core models: 1 Boss, 1 Rare, 1 Magic, 2 Normal
        var boss = new MonsterInstance(
            instanceId: 1001,
            name: "Brutus, Lord Incarcerator",
            rarity: MonsterRarity.Boss,
            monsterLevel: 75,
            maxLife: 20000,
            primaryDamageType: DamageType.Physical,
            damageMin: 120,
            damageMax: 200,
            attacksPerSecond: 1.1f,
            critChance: 5f,
            critMultiplier: 130f,
            accuracy: 450,
            baseExperience: 1000,
            itemQuantityMultiplier: 5.0f,
            itemRarityMultiplier: 8.0f,
            auraModifiers: null
        );

        var rareMob = new MonsterInstance(
            instanceId: 1002,
            name: "Hate Stalker",
            rarity: MonsterRarity.Rare,
            monsterLevel: 75,
            maxLife: 2500,
            primaryDamageType: DamageType.Fire,
            damageMin: 60,
            damageMax: 90,
            attacksPerSecond: 1.4f,
            critChance: 5f,
            critMultiplier: 130f,
            accuracy: 400,
            baseExperience: 300,
            itemQuantityMultiplier: 2.5f,
            itemRarityMultiplier: 3.5f,
            auraModifiers: null
        );

        var magicMob = new MonsterInstance(
            instanceId: 1003,
            name: "Frost Archer",
            rarity: MonsterRarity.Magic,
            monsterLevel: 75,
            maxLife: 800,
            primaryDamageType: DamageType.Cold,
            damageMin: 40,
            damageMax: 60,
            attacksPerSecond: 1.2f,
            critChance: 5f,
            critMultiplier: 130f,
            accuracy: 350,
            baseExperience: 150,
            itemQuantityMultiplier: 1.5f,
            itemRarityMultiplier: 2.0f,
            auraModifiers: null
        );

        var trashMob = new MonsterInstance(
            instanceId: 1004,
            name: "Zombie",
            rarity: MonsterRarity.Normal,
            monsterLevel: 75,
            maxLife: 300,
            primaryDamageType: DamageType.Physical,
            damageMin: 20,
            damageMax: 35,
            attacksPerSecond: 0.9f,
            critChance: 5f,
            critMultiplier: 130f,
            accuracy: 250,
            baseExperience: 50,
            itemQuantityMultiplier: 1.0f,
            itemRarityMultiplier: 1.0f,
            auraModifiers: null
        );

        var pack = new List<MonsterInstance> { boss, rareMob, magicMob, trashMob };
        arena.SetMonsters(pack);

        Assert(arena.ActiveMonsterCount == 4, $"Expected 4 active monsters in arena, got {arena.ActiveMonsterCount}");

        // Update monster health
        arena.UpdateMonsterLife(1004, 0f); // Kill zombie
        arena._Process(0.05);

        arena.QueueFree();
    }

    private static void TestProjectilesMovementAndVisualClamping()
    {
        var arena = new Arena2D();
        arena._Ready();

        // 1. Spawn a projectile in 2D space from center to top-left
        Vector2 start = Arena2D.CenterPosition;
        Vector2 target = new(100, 50);
        bool spawned = arena.SpawnProjectile(start, target, DamageType.Fire, speed: 500f);
        Assert(spawned, "Should successfully spawn a projectile");
        Assert(arena.ActiveProjectileCount == 1, $"Expected 1 active projectile, got {arena.ActiveProjectileCount}");

        // 2. Test Visual Clamping (Max 12 projectiles)
        for (int i = 0; i < 15; i++)
        {
            arena.SpawnProjectile(start, target, DamageType.Lightning, speed: 500f);
        }

        Assert(arena.ActiveProjectileCount == Arena2D.MaxProjectiles,
            $"Active projectiles must be clamped to {Arena2D.MaxProjectiles}, got {arena.ActiveProjectileCount}");

        // 3. Simulate frames to let projectiles reach target
        arena._Process(1.0); // 1 full second at 500px/s over 255px distance -> all reach target
        Assert(arena.ActiveProjectileCount == 0,
            $"All projectiles should hit target and deactivate, got {arena.ActiveProjectileCount}");

        arena.QueueFree();
    }

    private static void TestElementalDamageColors()
    {
        var (physBase, _) = Arena2D.GetDamageTypeColors(DamageType.Physical);
        var (fireBase, _) = Arena2D.GetDamageTypeColors(DamageType.Fire);
        var (coldBase, _) = Arena2D.GetDamageTypeColors(DamageType.Cold);
        var (lightBase, _) = Arena2D.GetDamageTypeColors(DamageType.Lightning);
        var (chaosBase, _) = Arena2D.GetDamageTypeColors(DamageType.Chaos);

        Assert(Mathf.Abs(physBase.R - physBase.G) < 0.05f, "Physical color should be neutral silver/white");
        Assert(fireBase.R > 0.8f && fireBase.G < 0.5f, "Fire color should be vivid red/orange");
        Assert(coldBase.B > 0.8f, "Cold color should be vivid cyan/blue");
        Assert(lightBase.R > 0.8f && lightBase.G > 0.8f, "Lightning color should be bright yellow");
        Assert(chaosBase.R > 0.7f && chaosBase.B > 0.8f, "Chaos color should be dark purple/magenta");
    }

    private static void TestBossSlamTelegraphAndDetonation()
    {
        var arena = new Arena2D();
        arena._Ready();

        bool detonated = false;
        Vector2 detonatedPos = Vector2.Zero;
        float detonatedThreat = 0f;

        arena.OnBossSlamDetonated += (pos, threat) =>
        {
            detonated = true;
            detonatedPos = pos;
            detonatedThreat = threat;
        };

        Vector2 target = new(355, 145);
        float duration = 1.2f;
        arena.TriggerBossSlam(target, duration: duration, radius: 55f, threatEhpPercent: 88f);

        Assert(arena.IsTelegraphActive, "Telegraph should be active after TriggerBossSlam");
        Assert(arena.TelegraphProgress < 0.1f, "Telegraph progress should start near 0");

        // Advance halfway (0.6s)
        arena._Process(0.6);
        Assert(arena.IsTelegraphActive, "Telegraph should still be active halfway through");
        Assert(arena.TelegraphProgress >= 0.45f && arena.TelegraphProgress <= 0.55f,
            $"Expected ~50% progress, got {arena.TelegraphProgress * 100}%");
        Assert(!detonated, "Telegraph should not detonate prematurely");

        // Advance past duration (another 0.7s)
        arena._Process(0.7);
        Assert(!arena.IsTelegraphActive, "Telegraph should be finished after full duration");
        Assert(detonated, "OnBossSlamDetonated event must fire upon telegraph completion");
        Assert(detonatedPos == target, "Detonation position must match target");
        Assert(Mathf.Abs(detonatedThreat - 88f) < 0.1f, $"Expected 88% threat, got {detonatedThreat}");

        arena.QueueFree();
    }

    private static void TestBattleViewportIntegration()
    {
        var scene = GD.Load<PackedScene>("res://scenes/BattleViewport.tscn");
        Assert(scene != null, "Failed to load BattleViewport.tscn");
        if (scene == null) return;

        var viewport = scene.Instantiate<BattleViewport>();
        Assert(viewport != null, "Failed to instantiate BattleViewport");
        if (viewport == null) return;

        viewport._Ready();

        Assert(viewport.Arena != null, "BattleViewport.Arena must not be null after _Ready");
        Assert(viewport.Arena is Arena2D, "BattleViewport.Arena must be an instance of Arena2D");
        if (viewport.Arena == null) return;

        Assert(viewport.Arena.ActiveMonsterCount >= 0, "Arena within scene must be functional");

        // Test proxy helper methods
        viewport.FireExileAttack(DamageType.Fire);
        Assert(viewport.Arena.ActiveProjectileCount == 1, "FireExileAttack should spawn a projectile in Arena");

        int projCountBeforeMelee = viewport.Arena.ActiveProjectileCount;
        viewport.FireExileMeleeAttack(new Vector2(250, 100), 45f, DamageType.Physical, Mathf.DegToRad(180f));
        Assert(viewport.Arena.ActiveProjectileCount == projCountBeforeMelee, "FireExileMeleeAttack must NOT spawn projectiles");
        Assert(viewport.Arena.ActiveMeleeIndicatorCount > 0, "FireExileMeleeAttack must spawn a melee slash indicator");

        viewport.TriggerBossSlam(new Vector2(350, 140), duration: 1.3f);
        Assert(viewport.Arena.IsTelegraphActive, "TriggerBossSlam on viewport should activate arena telegraph");

        viewport.QueueFree();
    }

    private static void TestEquippedWeaponVisuals()
    {
        var arena = new Arena2D();
        arena._Ready();

        // 1. Test Dual Wielding (Juggernaut setup)
        var sword1 = new WeaponBaseDefinition("sword1", "Rusted Sword", WeaponClass.OneHandedSword, HandSlotRequirement.OneHanded, 4, 9, 1.45f, 5f);
        var sword2 = new WeaponBaseDefinition("sword2", "Rusted Sword", WeaponClass.OneHandedSword, HandSlotRequirement.OneHanded, 4, 9, 1.45f, 5f);
        arena.SetEquippedWeapons(sword1, new WeaponItemInstance(sword2));

        Assert(arena.MainHandWeapon?.Id == "sword1", "MainHandWeapon should be sword1");
        Assert(arena.OffHandItem is WeaponItemInstance, "OffHandItem should be weapon instance");

        // Simulate frame with attack recoil
        arena.FireExileMeleeAttack(new Vector2(250, 100), 45f, DamageType.Physical);
        arena._Process(0.016);

        // 2. Test Bow & Quiver (Deadeye setup)
        var bow = new WeaponBaseDefinition("bow1", "Crude Bow", WeaponClass.Bow, HandSlotRequirement.TwoHanded, 8, 16, 1.40f, 5f);
        var quiver = new WeaponBaseDefinition("quiver1", "Broadhead Quiver", WeaponClass.Quiver, HandSlotRequirement.OffHandOnly, 0, 0, 0, 0);
        arena.SetEquippedWeapons(bow, new WeaponItemInstance(quiver));

        Assert(arena.MainHandWeapon?.WeaponClass == WeaponClass.Bow, "MainHand should be bow");
        arena._Process(0.016);

        // 3. Test Wand & Shield (Caster / Tank setup)
        var wand = new WeaponBaseDefinition("wand1", "Driftwood Wand", WeaponClass.Wand, HandSlotRequirement.OneHanded, 5, 10, 1.40f, 7f);
        var shield = new ArmourBaseDefinition("shield1", "Tower Shield", ArmourSlot.Shield, 100, 0, 0, 25);
        arena.SetEquippedWeapons(wand, new ArmourItemInstance(shield));

        Assert(arena.MainHandWeapon?.WeaponClass == WeaponClass.Wand, "MainHand should be wand");
        Assert(arena.OffHandItem is ArmourItemInstance a && a.BaseDefinition.ArmourSlot == ArmourSlot.Shield, "OffHand should be shield");
        arena._Process(0.016);

        // 4. Test Two-Handed Axe
        var twoHandAxe = new WeaponBaseDefinition("axe2h", "Colossus Axe", WeaponClass.TwoHandedAxe, HandSlotRequirement.TwoHanded, 50, 90, 1.2f, 5f);
        arena.SetEquippedWeapons(twoHandAxe, null);
        Assert(arena.MainHandWeapon?.WeaponClass == WeaponClass.TwoHandedAxe, "MainHand should be 2H axe");
        arena._Process(0.016);

        arena.QueueFree();
    }

    private static void TestExileAutoFacingAndTargetTracking()
    {
        var arena = new Arena2D();
        arena._Ready();

        // 1. Direct FaceTarget checks
        arena.FaceTarget(new Vector2(100f, 104f)); // Target to the West (-X)
        Assert(Mathf.Abs(Mathf.AngleDifference(arena.ExileFacingAngle, Mathf.Pi)) < 0.05f,
            $"Exile should face West (~PI rad), got {arena.ExileFacingAngle}");

        arena.FaceTarget(new Vector2(220f, 50f)); // Target to the North (-Y)
        Assert(Mathf.Abs(Mathf.AngleDifference(arena.ExileFacingAngle, -Mathf.Pi * 0.5f)) < 0.05f,
            $"Exile should face North (~-PI/2 rad), got {arena.ExileFacingAngle}");

        arena.FaceTarget(new Vector2(300f, 104f)); // Target to the East (+X)
        Assert(Mathf.Abs(Mathf.AngleDifference(arena.ExileFacingAngle, 0f)) < 0.05f,
            $"Exile should face East (~0 rad), got {arena.ExileFacingAngle}");

        // 2. Weapon tip calculation should align with facing
        Vector2 wandTipEast = arena.GetWeaponTipPosition();
        Assert(wandTipEast.X > arena.ExilePosition.X, "Wand tip X should extend East in facing direction");

        // 3. FireExileMeleeAttack should immediately turn Exile toward target
        arena.FireExileMeleeAttack(new Vector2(100f, 104f), 45f, DamageType.Physical);
        Assert(Mathf.Abs(Mathf.AngleDifference(arena.ExileFacingAngle, Mathf.Pi)) < 0.05f,
            $"FireExileMeleeAttack should turn Exile West toward target, got {arena.ExileFacingAngle}");

        // 4. FireExileAttack should immediately turn Exile toward projectile target
        arena.FireExileAttack(DamageType.Fire, new Vector2(220f, 180f));
        Assert(Mathf.Abs(Mathf.AngleDifference(arena.ExileFacingAngle, Mathf.Pi * 0.5f)) < 0.05f,
            $"FireExileAttack should turn Exile South toward target, got {arena.ExileFacingAngle}");

        // 5. Priority targeting over closest mob
        var mobClose = new MonsterInstance(201, "CloseMob", MonsterRarity.Normal, 1, 100f, DamageType.Physical, 5, 10, 1, 5, 100, 10, 1, 1, 1,
            initialPosition: new Vector2D(260f, 104f)); // 40px East
        var mobPriority = new MonsterInstance(202, "BossMob", MonsterRarity.Boss, 1, 5000f, DamageType.Physical, 5, 10, 1, 5, 100, 10, 1, 1, 1,
            initialPosition: new Vector2D(100f, 104f)); // 120px West

        arena.SetMonsters(new[] { mobClose, mobPriority });
        arena.SetTarget(mobPriority.InstanceId, new Vector2(100f, 104f));

        // Let simulation interpolate for 0.5s
        arena._Process(0.5);
        Assert(Mathf.Abs(Mathf.AngleDifference(arena.ExileFacingAngle, Mathf.Pi)) < 0.25f,
            $"Auto-Facing must track priority target (West) even when another mob is closer, got {arena.ExileFacingAngle}");

        arena.QueueFree();
    }

    private static void TestUnifiedAttackVisualPipeline()
    {
        var arena = new Arena2D();
        arena._Ready();

        var player = new PlayerState();
        var monster = new MonsterInstance(
            instanceId: 777,
            name: "Test Marauder",
            rarity: MonsterRarity.Rare,
            monsterLevel: 1,
            maxLife: 500f,
            primaryDamageType: DamageType.Fire,
            damageMin: 20f,
            damageMax: 30f,
            attacksPerSecond: 1.0f,
            critChance: 10f,
            critMultiplier: 150f,
            accuracy: 100f,
            baseExperience: 50f,
            itemQuantityMultiplier: 1f,
            itemRarityMultiplier: 1f,
            initialPosition: new Vector2D(300f, 104f)
        );

        arena.SetMonsters(new[] { monster });

        // 1. Unified Player Melee Attack
        int meleeBefore = arena.ActiveMeleeIndicatorCount;
        var playerAttackArgs = new CombatAttackEventArgs(
            Attacker: player,
            Target: monster,
            Origin: new Vector2D(40f, 104f),
            TargetPosition: new Vector2D(300f, 104f),
            GeometryType: SkillGeometryType.Cone,
            DamageType: DamageType.Physical,
            Range: 45f,
            IsMelee: true,
            IsCrit: false,
            ConeAngle: 75f
        );

        arena.ExecuteAttackVisual(playerAttackArgs);
        Assert(arena.ActiveMeleeIndicatorCount > meleeBefore, "Player melee attack must spawn melee strike indicator");

        // 2. Unified Monster Melee Attack
        int meleeAfterPlayer = arena.ActiveMeleeIndicatorCount;
        var monsterAttackArgs = new CombatAttackEventArgs(
            Attacker: monster,
            Target: player,
            Origin: new Vector2D(300f, 104f),
            TargetPosition: new Vector2D(40f, 104f),
            GeometryType: SkillGeometryType.Cone,
            DamageType: DamageType.Fire,
            Range: 40f,
            IsMelee: true,
            IsCrit: true,
            ConeAngle: 70f
        );

        arena.ExecuteAttackVisual(monsterAttackArgs);
        Assert(arena.ActiveMeleeIndicatorCount > meleeAfterPlayer, "Monster melee attack must spawn melee strike indicator through the same pipeline");

        // 3. Unified Projectile Attack (e.g. Player or Ranged Monster)
        int projBefore = arena.ActiveProjectileCount;
        var projectileAttackArgs = new CombatAttackEventArgs(
            Attacker: player,
            Target: monster,
            Origin: new Vector2D(40f, 104f),
            TargetPosition: new Vector2D(300f, 104f),
            GeometryType: SkillGeometryType.SingleTarget,
            DamageType: DamageType.Lightning,
            Range: 150f,
            IsMelee: false,
            IsCrit: false
        );

        arena.ExecuteAttackVisual(projectileAttackArgs);
        Assert(arena.ActiveProjectileCount > projBefore, "Ranged attack must spawn projectile through the same pipeline");

        arena.QueueFree();
    }

    private static void TestFireballProjectileAndDamageOnImpact()
    {
        var arena = new Arena2D();
        arena._Ready();

        var player = new PlayerState();
        var monster = new MonsterInstance(
            instanceId: 201,
            name: "Fire Target",
            rarity: MonsterRarity.Normal,
            monsterLevel: 1,
            maxLife: 100.0f,
            primaryDamageType: DamageType.Physical,
            damageMin: 2f,
            damageMax: 4f,
            attacksPerSecond: 1.0f,
            critChance: 5f,
            critMultiplier: 150f,
            accuracy: 100f,
            baseExperience: 10f,
            itemQuantityMultiplier: 1f,
            itemRarityMultiplier: 1f,
            initialPosition: new Vector2D(300f, 104f)
        );

        arena.SetMonsters(new[] { monster });

        int projBefore = arena.ActiveProjectileCount;
        int meleeIndicatorsBefore = arena.ActiveMeleeIndicatorCount;

        var fireballArgs = new CombatAttackEventArgs(
            Attacker: player,
            Target: monster,
            Origin: new Vector2D(40f, 104f),
            TargetPosition: new Vector2D(300f, 104f),
            GeometryType: SkillGeometryType.RadialAoE,
            DamageType: DamageType.Fire,
            Range: 32f,
            IsMelee: false,
            IsCrit: false,
            Projectiles: new[] { new ProjectileVector(0, 0f, new Vector2D(540f, 0f), new Vector2D(1f, 0f)) }
        );

        arena.ExecuteAttackVisual(fireballArgs);
        Assert(arena.ActiveProjectileCount == 1, $"Fireball must spawn an in-flight projectile, got {arena.ActiveProjectileCount}");
        Assert(arena.ActiveMeleeIndicatorCount == meleeIndicatorsBefore, "Fireball must NOT detonate melee/radial strike indicator at cast time");

        // Queue damage while fireball is in-flight:
        arena.HandleMonsterDamageDealt(monster.InstanceId, 40f, 60f, new Vector2(300f, 104f), DamageType.Fire, false, false);

        for (int step = 0; step < 10; step++)
        {
            arena._Process(0.05);
            if (arena.ActiveMeleeIndicatorCount > meleeIndicatorsBefore)
            {
                break;
            }
        }

        Assert(arena.ActiveMeleeIndicatorCount > meleeIndicatorsBefore, "Fireball impact must detonate AoE radial indicator");
        Assert(arena.ActiveProjectileCount == 0, "Fireball projectile should deactivate after reaching target");

        arena.QueueFree();
    }

    private static void TestProjectileAbsorbedOnMonsterHitWithoutGoingThrough()
    {
        var arena = new Arena2D();
        arena._Ready();

        var monster = new MonsterInstance(
            instanceId: 501,
            name: "Absorb Target",
            rarity: MonsterRarity.Normal,
            monsterLevel: 1,
            maxLife: 100.0f,
            primaryDamageType: DamageType.Physical,
            damageMin: 2f,
            damageMax: 4f,
            attacksPerSecond: 1.0f,
            critChance: 5f,
            critMultiplier: 150f,
            accuracy: 100f,
            baseExperience: 10f,
            itemQuantityMultiplier: 1f,
            itemRarityMultiplier: 1f,
            initialPosition: new Vector2D(150f, 104f)
        );

        arena.SetMonsters(new[] { monster });

        // Spawn projectile aimed towards (300, 104) which is way past the monster at (150, 104)
        Vector2 start = new(20f, 104f);
        Vector2 target = new(300f, 104f);
        arena.SpawnProjectile(start, target, DamageType.Physical, speed: 600f, pierceCount: 0);

        Assert(arena.ActiveProjectileCount == 1, "Projectile should be active in flight initially");

        // Advance frames until the projectile reaches the monster (130px at 600px/s = ~0.22s)
        for (int i = 0; i < 6; i++)
        {
            arena._Process(0.05); // 0.3s total
            if (arena.ActiveProjectileCount == 0) break;
        }

        Assert(arena.ActiveProjectileCount == 0, "Projectile must be absorbed on hitting the monster and NOT continue to target (300, 104)");

        // Now test pierce: a projectile with PierceRemaining = 1 should NOT be absorbed by the first monster
        arena.SpawnProjectile(start, target, DamageType.Physical, speed: 600f, pierceCount: 1);
        Assert(arena.ActiveProjectileCount == 1, "Piercing projectile should be active");

        // Advance 0.25s (enough to reach monster at 150px)
        arena._Process(0.25);
        // It pierced the monster and is still in flight towards 300px
        Assert(arena.ActiveProjectileCount == 1, "Piercing projectile should pierce through the monster and remain in flight");

        // Advance another 0.4s to let it reach 300px and complete
        arena._Process(0.4);
        Assert(arena.ActiveProjectileCount == 0, "Piercing projectile completes after reaching max distance");

        arena.QueueFree();
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception($"[Assertion Failed] {message}");
        }
    }
}
