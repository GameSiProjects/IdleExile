using System;
using System.Collections.Generic;
using System.Linq;
using IdleExile.Core.Models;
using IdleExile.Core.Services;
using IdleExile.Core.Simulation;
using IdleExile.Core.State;

namespace IdleExile.Core.Tests;

/// <summary>
/// Unit tests validating the Unified Combat & Attack System.
/// Verifies:
/// 1. Symmetrical combatant interface: Both Player and Monster implement <see cref="ICombatEntity"/>.
/// 2. Symmetrical hit execution: Both use <see cref="CombatAttackService.ExecuteHit"/> with <see cref="AttackPayload"/>.
/// 3. Symmetrical status ailments: Monsters can inflict ailments on Player and vice versa.
/// 4. Symmetrical defensive pipeline: Both utilize <see cref="DefensiveLayers.ProcessHit"/>.
/// 5. Symmetrical debuff amplification: Shock on either entity amplifies incoming damage.
/// </summary>
public static class UnifiedCombatSystemTests
{
    public static void RunAll()
    {
        Console.WriteLine("[Tests] Running UnifiedCombatSystemTests...");

        TestCombatEntityInterfaceContract();
        TestPlayerAttackingMonsterThroughUnifiedPipeline();
        TestMonsterAttackingPlayerThroughUnifiedPipeline();
        TestSymmetricalShockAmplification();
        TestSymmetricalEvasionEntropy();
        TestMonsterInflictingAilmentsOnPlayer();

        Console.WriteLine("[Tests] All UnifiedCombatSystemTests PASSED successfully!");
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
    //  1. Contract Verification
    // ─────────────────────────────────────────────

    private static void TestCombatEntityInterfaceContract()
    {
        var player = new PlayerState();
        player.Initialize(maxLife: 500f, maxEnergyShield: 100f, maxMana: 200f);

        var monster = new MonsterInstance(
            instanceId: 42,
            name: "Flame Archer",
            rarity: MonsterRarity.Magic,
            monsterLevel: 10,
            maxLife: 300f,
            primaryDamageType: DamageType.Fire,
            damageMin: 30f,
            damageMax: 45f,
            attacksPerSecond: 1.5f,
            critChance: 10f,
            critMultiplier: 150f,
            accuracy: 400f,
            baseExperience: 50f,
            itemQuantityMultiplier: 1.5f,
            itemRarityMultiplier: 2.0f
        );

        // Verify both implement ICombatEntity
        ICombatEntity playerEntity = player;
        ICombatEntity monsterEntity = monster;

        AssertEqual(0, playerEntity.Id, 0.001f, "Player ID should be 0");
        AssertEqual(42, monsterEntity.Id, 0.001f, "Monster ID should match instance ID (42)");

        AssertEqual(500f, playerEntity.MaxLife, 0.001f, "Player max life should be 500");
        AssertEqual(300f, monsterEntity.MaxLife, 0.001f, "Monster max life should be 300");

        AssertTrue(playerEntity.IsAlive, "Player should be alive");
        AssertTrue(monsterEntity.IsAlive, "Monster should be alive");

        AssertTrue(playerEntity.Defenses != null, "Player Defenses should be initialized");
        AssertTrue(monsterEntity.Defenses != null, "Monster Defenses should be initialized");

        AssertTrue(playerEntity.Ailments != null, "Player Ailments should be initialized");
        AssertTrue(monsterEntity.Ailments != null, "Monster Ailments should be initialized");
    }

    // ─────────────────────────────────────────────
    //  2. Player -> Monster Unified Attack
    // ─────────────────────────────────────────────

    private static void TestPlayerAttackingMonsterThroughUnifiedPipeline()
    {
        var player = new PlayerState();
        player.Initialize(maxLife: 500f, maxEnergyShield: 0f, maxMana: 100f);

        var monster = new MonsterInstance(
            instanceId: 101,
            name: "Fire Golem",
            rarity: MonsterRarity.Rare,
            monsterLevel: 15,
            maxLife: 1000f,
            primaryDamageType: DamageType.Fire,
            damageMin: 20f,
            damageMax: 30f,
            attacksPerSecond: 1.0f,
            critChance: 5f,
            critMultiplier: 130f,
            accuracy: 300f,
            baseExperience: 100f,
            itemQuantityMultiplier: 2f,
            itemRarityMultiplier: 3f
        )
        {
            FireResistance = 50f // 50% Fire resistance
        };

        // Player attacks with 100 Fire damage
        var basePool = new DamagePool { Fire = new DamageRange(100f, 100f) };
        var context = new DamageModifierContext
        {
            BaseCritChance = 0f,
            IsAttack = true
        };
        var attack = new AttackPayload(player, basePool, context)
        {
            IsAttack = true,
            PrimaryDamageType = DamageType.Fire
        };

        var result = CombatAttackService.ExecuteHit(player, monster, attack, monster.Defenses);

        // 100 Fire mitigated by 50% Fire Res -> 50 Damage applied
        AssertEqual(50f, result.DamageDealt, 0.5f, "100 Fire damage against 50% Fire Res should deal 50 damage");
        AssertEqual(950f, monster.CurrentLife, 0.5f, "Monster life should drop from 1000 to 950");
        AssertTrue(!result.TargetDied, "Monster should still be alive");
    }

    // ─────────────────────────────────────────────
    //  3. Monster -> Player Unified Attack
    // ─────────────────────────────────────────────

    private static void TestMonsterAttackingPlayerThroughUnifiedPipeline()
    {
        var player = new PlayerState();
        player.Initialize(maxLife: 200f, maxEnergyShield: 0f, maxMana: 100f);
        player.Defenses = new DefensiveStats
        {
            FireResistance = 75f, // 75% Fire resistance
            MaxFireResistance = 75f
        };

        var monster = new MonsterInstance(
            instanceId: 202,
            name: "Infernal Shaman",
            rarity: MonsterRarity.Normal,
            monsterLevel: 1,
            maxLife: 200f,
            primaryDamageType: DamageType.Fire,
            damageMin: 100f,
            damageMax: 100f,
            attacksPerSecond: 1.0f,
            critChance: 0f,
            critMultiplier: 100f,
            accuracy: 500f,
            baseExperience: 20f,
            itemQuantityMultiplier: 1f,
            itemRarityMultiplier: 1f
        );

        var attack = CombatAttackService.CreateMonsterAttack(monster, mapModifiers: null);

        var result = CombatAttackService.ExecuteHit(monster, player, attack, player.Defenses);

        // 100 Fire against 75% Fire Res -> 25 Damage applied
        AssertEqual(25f, result.DamageDealt, 0.5f, "100 Fire damage against 75% Fire Res should deal 25 damage");
        AssertEqual(175f, player.CurrentLife, 0.5f, "Player life should drop from 200 to 175");
        AssertTrue(!result.TargetDied, "Player should still be alive");
    }

    // ─────────────────────────────────────────────
    //  4. Symmetrical Shock Amplification
    // ─────────────────────────────────────────────

    private static void TestSymmetricalShockAmplification()
    {
        var player = new PlayerState();
        player.Initialize(maxLife: 300f, maxEnergyShield: 0f, maxMana: 100f);
        player.Defenses = new DefensiveStats { Armour = 0f };

        var monster = new MonsterInstance(
            instanceId: 303,
            name: "Training Dummy",
            rarity: MonsterRarity.Normal,
            monsterLevel: 1,
            maxLife: 1000f,
            primaryDamageType: DamageType.Physical,
            damageMin: 100f,
            damageMax: 100f,
            attacksPerSecond: 1.0f,
            critChance: 0f,
            critMultiplier: 100f,
            accuracy: 500f,
            baseExperience: 10f,
            itemQuantityMultiplier: 1f,
            itemRarityMultiplier: 1f
        );

        // A. Shock on Monster amplifies Player damage:
        // Apply 30% Shock to Monster
        monster.Ailments.ApplyAilment(new AilmentInstance(
            Type: AilmentType.Shock,
            SourceDamageType: DamageType.Lightning,
            DamagePerSecond: 0f,
            EffectPercent: 30f,
            DurationSeconds: 4f,
            IsStacking: false
        ));

        var playerAttack = new AttackPayload(player, new DamagePool { Physical = new DamageRange(100f, 100f) }, new DamageModifierContext { BaseCritChance = 0f });
        var pResult = CombatAttackService.ExecuteHit(player, monster, playerAttack, monster.Defenses);
        // 100 base * 1.30 shock = 130 damage
        AssertEqual(130f, pResult.DamageDealt, 0.5f, "Player attack should deal +30% damage to Shocked monster (130)");

        // B. Shock on Player amplifies Monster damage:
        // Apply 40% Shock to Player
        player.Ailments.ApplyAilment(new AilmentInstance(
            Type: AilmentType.Shock,
            SourceDamageType: DamageType.Lightning,
            DamagePerSecond: 0f,
            EffectPercent: 40f,
            DurationSeconds: 4f,
            IsStacking: false
        ));

        var monsterAttack = CombatAttackService.CreateMonsterAttack(monster, null);
        var mResult = CombatAttackService.ExecuteHit(monster, player, monsterAttack, player.Defenses);
        // 100 base * 1.40 shock = 140 damage
        AssertEqual(140f, mResult.DamageDealt, 0.5f, "Monster attack should deal +40% damage to Shocked player (140)");
    }

    // ─────────────────────────────────────────────
    //  5. Symmetrical Evasion Entropy
    // ─────────────────────────────────────────────

    private static void TestSymmetricalEvasionEntropy()
    {
        var player = new PlayerState();
        player.Initialize(maxLife: 200f, maxEnergyShield: 0f, maxMana: 100f);
        // 50% Evasion -> Chance to hit = 50% -> lands every 2 hits
        player.Defenses = new DefensiveStats { EvasionChance = 50f, Armour = 0f };

        var monster = new MonsterInstance(
            instanceId: 404,
            name: "Bandit Striker",
            rarity: MonsterRarity.Normal,
            monsterLevel: 1,
            maxLife: 500f,
            primaryDamageType: DamageType.Physical,
            damageMin: 10f,
            damageMax: 10f,
            attacksPerSecond: 1.0f,
            critChance: 0f,
            critMultiplier: 100f,
            accuracy: 100f,
            baseExperience: 10f,
            itemQuantityMultiplier: 1f,
            itemRarityMultiplier: 1f
        );

        var attack = CombatAttackService.CreateMonsterAttack(monster, null);

        // Hit 1: Entropy += 50 (50 < 100) -> EVADED
        var hit1 = CombatAttackService.ExecuteHit(monster, player, attack, player.Defenses);
        AssertTrue(hit1.Defense.Evaded, "Hit 1 must be evaded by Evasion Entropy");
        AssertEqual(0f, hit1.DamageDealt, 0.001f, "Evaded hit must deal 0 damage");

        // Hit 2: Entropy += 50 (100 >= 100) -> HITS
        var hit2 = CombatAttackService.ExecuteHit(monster, player, attack, player.Defenses);
        AssertTrue(!hit2.Defense.Evaded, "Hit 2 must land according to Evasion Entropy");
        AssertEqual(10f, hit2.DamageDealt, 0.5f, "Hit 2 must deal 10 damage");
    }

    // ─────────────────────────────────────────────
    //  6. Monster Inflicting Ailments On Player
    // ─────────────────────────────────────────────

    private static void TestMonsterInflictingAilmentsOnPlayer()
    {
        var player = new PlayerState();
        player.Initialize(maxLife: 500f, maxEnergyShield: 0f, maxMana: 100f);
        player.Defenses = new DefensiveStats { FireResistance = 0f };

        // Monster with 100% crit chance dealing Fire damage -> guaranteed Ignite on hit!
        var fireBoss = new MonsterInstance(
            instanceId: 505,
            name: "Fire Lord",
            rarity: MonsterRarity.Boss,
            monsterLevel: 10,
            maxLife: 5000f,
            primaryDamageType: DamageType.Fire,
            damageMin: 50f,
            damageMax: 50f,
            attacksPerSecond: 1.0f,
            critChance: 100f, // 100% crit -> inherently ignites
            critMultiplier: 150f,
            accuracy: 1000f,
            baseExperience: 500f,
            itemQuantityMultiplier: 5f,
            itemRarityMultiplier: 5f
        );

        var attack = CombatAttackService.CreateMonsterAttack(fireBoss, null);
        var result = CombatAttackService.ExecuteHit(fireBoss, player, attack, player.Defenses);

        AssertTrue(result.Hit.IsCrit, "Monster hit should be a critical strike");
        AssertTrue(result.Hit.InflictedAilments.Any(a => a.Type == AilmentType.Ignite), "Critical fire hit should generate Ignite ailment");
        AssertTrue(player.Ailments.HasAilment(AilmentType.Ignite), "Player should now have active Ignite status ailment from monster hit!");
    }
}
