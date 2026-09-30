using System;
using System.Collections.Generic;
using System.Linq;
using IdleExile.Core.Models;
using IdleExile.Core.Simulation;

namespace IdleExile.Core.Tests;

/// <summary>
/// Unit tests for Task 1.3: Deterministic Seeded Drop Rules.
/// Validates:
/// - 12th monster kill rule (Magic weapon + Orb of Transmutation)
/// - First Magic pack leader rule (Guaranteed Support Gem tailored to archetype)
/// - Map 1 Boss kill rule (2x Maps, Chaos Orb, Rare armor)
/// - Tier 0 Fail-Safe rule (Guaranteed T1 map drop every 50 kills in Endless Hollow)
/// - Event dispatching for loot drops and loot batches
/// </summary>
public static class SeededDropTests
{
    public static void RunAll()
    {
        Console.WriteLine("[Tests] Running SeededDropTests (Task 1.3)...");

        TestTwelfthKillDropRule();
        TestMagicSupportGemArchetypeMatching();
        TestMap1BossLootExplosion();
        TestFailSafeEndlessHollowRule();
        TestLootEventsDispatching();

        Console.WriteLine("[Tests] All SeededDropTests PASSED successfully!");
    }

    private static void AssertEqual<T>(T expected, T actual, string message = "")
    {
        if (!object.Equals(expected, actual))
        {
            throw new Exception($"Assertion failed: Expected {expected}, got {actual}. {message}");
        }
    }

    private static void AssertTrue(bool condition, string message = "")
    {
        if (!condition)
        {
            throw new Exception($"Assertion failed: Expected true. {message}");
        }
    }

    private static MonsterInstance CreateDummyMonster(MonsterRarity rarity, string name = "Zombie")
    {
        return new MonsterInstance(
            instanceId: 1L,
            name: name,
            rarity: rarity,
            monsterLevel: 1,
            maxLife: 20f,
            primaryDamageType: DamageType.Physical,
            damageMin: 2f,
            damageMax: 4f,
            attacksPerSecond: 1.0f,
            critChance: 5f,
            critMultiplier: 130f,
            accuracy: 30f,
            baseExperience: 5f,
            itemQuantityMultiplier: 1.0f,
            itemRarityMultiplier: 1.0f
        );
    }

    private static void TestTwelfthKillDropRule()
    {
        var ftue = new FtueManager();
        ftue.SelectArchetype(StarterArchetype.Juggernaut);
        ftue.ActivateFirstMap();

        var coordinator = new SeededDropCoordinator(ftue);

        // Kills 1 through 11 should not trigger the 12th kill seeded drop
        for (int i = 1; i <= 11; i++)
        {
            var drops = coordinator.ProcessMonsterKill(CreateDummyMonster(MonsterRarity.Normal));
            AssertEqual(0, drops.Count, $"Kill {i} should yield no seeded drops.");
        }

        AssertEqual(11, coordinator.KillCountInMap);
        AssertEqual(FtueStep.FirstCombatWave, ftue.CurrentStep);

        // Kill 12 MUST trigger the guaranteed drop
        var twelfthDrops = coordinator.ProcessMonsterKill(CreateDummyMonster(MonsterRarity.Normal));
        AssertEqual(2, twelfthDrops.Count, "12th kill must drop exactly 2 seeded items.");

        var equip = twelfthDrops.FirstOrDefault(d => d.Type == LootDropType.Equipment);
        var currency = twelfthDrops.FirstOrDefault(d => d.Type == LootDropType.Currency);

        AssertTrue(equip != null, "Must include Magic equipment.");
        AssertEqual(LootRarity.Magic, equip!.Rarity);
        AssertEqual("rusted_sword", equip.Id);

        AssertTrue(currency != null, "Must include Orb of Transmutation.");
        AssertEqual("orb_of_transmutation", currency!.Id);
        AssertEqual(1, currency.Quantity);

        // FTUE step must advance to FirstLootDropped
        AssertEqual(FtueStep.FirstLootDropped, ftue.CurrentStep);

        // Kill 13 must not duplicate the 12th kill drop
        var thirteenthDrops = coordinator.ProcessMonsterKill(CreateDummyMonster(MonsterRarity.Normal));
        AssertEqual(0, thirteenthDrops.Count, "Kill 13 should yield no seeded drops.");
    }

    private static void TestMagicSupportGemArchetypeMatching()
    {
        // 1. Juggernaut -> Added Fire Damage
        {
            var ftue = new FtueManager();
            ftue.SelectArchetype(StarterArchetype.Juggernaut);
            ftue.ActivateFirstMap();

            var coordinator = new SeededDropCoordinator(ftue);
            var drops = coordinator.ProcessMonsterKill(CreateDummyMonster(MonsterRarity.Magic));

            AssertEqual(1, drops.Count);
            AssertEqual(LootDropType.Gem, drops[0].Type);
            AssertEqual("added_fire_damage", drops[0].Id);
            AssertEqual(FtueStep.SupportGemAcquired, ftue.CurrentStep);
        }

        // 2. Elementalist -> Ignite Proliferation
        {
            var ftue = new FtueManager();
            ftue.SelectArchetype(StarterArchetype.Elementalist);
            ftue.ActivateFirstMap();

            var coordinator = new SeededDropCoordinator(ftue);
            var drops = coordinator.ProcessMonsterKill(CreateDummyMonster(MonsterRarity.Magic));

            AssertEqual(1, drops.Count);
            AssertEqual(LootDropType.Gem, drops[0].Type);
            AssertEqual("ignite_proliferation", drops[0].Id);
            AssertEqual(FtueStep.SupportGemAcquired, ftue.CurrentStep);
        }

        // 3. Deadeye -> Multiple Projectiles
        {
            var ftue = new FtueManager();
            ftue.SelectArchetype(StarterArchetype.Deadeye);
            ftue.ActivateFirstMap();

            var coordinator = new SeededDropCoordinator(ftue);
            var drops = coordinator.ProcessMonsterKill(CreateDummyMonster(MonsterRarity.Magic));

            AssertEqual(1, drops.Count);
            AssertEqual(LootDropType.Gem, drops[0].Type);
            AssertEqual("multiple_projectiles", drops[0].Id);
            AssertEqual(FtueStep.SupportGemAcquired, ftue.CurrentStep);
        }
    }

    private static void TestMap1BossLootExplosion()
    {
        var ftue = new FtueManager();
        ftue.SelectArchetype(StarterArchetype.Juggernaut);
        ftue.ActivateFirstMap();

        var coordinator = new SeededDropCoordinator(ftue);
        var drops = coordinator.ProcessMonsterKill(CreateDummyMonster(MonsterRarity.Boss, "Tidal Behemoth"));

        AssertEqual(4, drops.Count, "Boss must drop 4 seeded items.");

        var strandMap = drops.FirstOrDefault(d => d.Id == "strand");
        var aridLakeMap = drops.FirstOrDefault(d => d.Id == "arid_lake");
        var chaos = drops.FirstOrDefault(d => d.Id == "chaos_orb");
        var rareEquip = drops.FirstOrDefault(d => d.Type == LootDropType.Equipment);

        AssertTrue(strandMap != null && strandMap.Type == LootDropType.Map, "Must drop Strand T1 map.");
        AssertTrue(aridLakeMap != null && aridLakeMap.Type == LootDropType.Map, "Must drop Arid Lake T2 map.");
        AssertTrue(chaos != null && chaos.Type == LootDropType.Currency, "Must drop Chaos Orb.");
        AssertTrue(rareEquip != null && rareEquip.Rarity == LootRarity.Rare, "Must drop Rare armor.");

        AssertEqual(FtueStep.FtueCompleted, ftue.CurrentStep);
        AssertTrue(!coordinator.IsFirstMapRun, "First map run flag must clear after boss kill.");
    }

    private static void TestFailSafeEndlessHollowRule()
    {
        var coordinator = new SeededDropCoordinator();
        coordinator.IsFirstMapRun = false; // Not FTUE, running T0 fail-safe

        // Kills 1 through 49 in T0
        for (int i = 1; i <= 49; i++)
        {
            var drops = coordinator.ProcessMonsterKill(CreateDummyMonster(MonsterRarity.Normal), isFailSafeMap: true);
            AssertEqual(0, drops.Count);
        }

        // Kill 50 MUST drop a guaranteed Strand T1 map
        var drop50 = coordinator.ProcessMonsterKill(CreateDummyMonster(MonsterRarity.Normal), isFailSafeMap: true);
        AssertEqual(1, drop50.Count);
        AssertEqual("strand", drop50[0].Id);
        AssertEqual(LootDropType.Map, drop50[0].Type);

        // Next 49 kills
        for (int i = 51; i <= 99; i++)
        {
            var drops = coordinator.ProcessMonsterKill(CreateDummyMonster(MonsterRarity.Normal), isFailSafeMap: true);
            AssertEqual(0, drops.Count);
        }

        // Kill 100 MUST drop another map
        var drop100 = coordinator.ProcessMonsterKill(CreateDummyMonster(MonsterRarity.Normal), isFailSafeMap: true);
        AssertEqual(1, drop100.Count);
        AssertEqual("strand", drop100[0].Id);
    }

    private static void TestLootEventsDispatching()
    {
        var ftue = new FtueManager();
        ftue.SelectArchetype(StarterArchetype.Elementalist);
        ftue.ActivateFirstMap();

        var coordinator = new SeededDropCoordinator(ftue);
        var singleDrops = new List<LootDrop>();
        var batchDrops = new List<IReadOnlyList<LootDrop>>();

        coordinator.OnLootDropped += item => singleDrops.Add(item);
        coordinator.OnLootBatchDropped += batch => batchDrops.Add(batch);

        coordinator.ProcessMonsterKill(CreateDummyMonster(MonsterRarity.Magic));

        AssertEqual(1, singleDrops.Count);
        AssertEqual("ignite_proliferation", singleDrops[0].Id);
        AssertEqual(1, batchDrops.Count);
        AssertEqual(1, batchDrops[0].Count);
    }
}
