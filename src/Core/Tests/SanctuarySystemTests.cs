using System;
using System.Collections.Generic;
using System.Text.Json;
using IdleExile.Core.Models;
using IdleExile.Core.Simulation;

namespace IdleExile.Core.Tests;

/// <summary>
/// Unit tests for Prompt 1: The Crucible Sanctuary & In-Combat DPS Snapshot models and state logic.
/// </summary>
public static class SanctuarySystemTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static void RunAll()
    {
        Console.WriteLine("[Tests] Running SanctuarySystemTests (Prompt 1 & 2: Sanctuary & Combat DPS Meter)...");

        TestSanctuaryTierDefinitionModel();
        TestSanctuaryStateMutationsAndEvents();
        TestCombatDpsSnapshotIntegrity();
        TestSanctuaryTiersJsonDeserialization();
        TestCombatDpsTrackerSlidingWindow();
        TestCombatDpsTrackerBreakdownAndAccuracy();
        TestCombatDpsTrackerReset();

        Console.WriteLine("[Tests] All SanctuarySystemTests PASSED successfully!");
    }

    private static void TestSanctuaryTierDefinitionModel()
    {
        var tier = new SanctuaryTierDefinition(
            Id: "tier_2",
            Tier: 2,
            Name: "Awakened Foundry",
            RequiredMapTier: 6,
            MapSlots: 4,
            QueueSlots: 10,
            SmelterUnlocked: true,
            AbyssUnlocked: true,
            SmelterSpeedBonus: 0.0f,
            PinnacleTrophiesUnlocked: false,
            Description: "Restored flagstones with glowing runic conduits."
        );

        AssertEqual("tier_2", tier.Id, "Tier ID mismatch");
        AssertEqual(2, tier.Tier, "Tier number mismatch");
        AssertEqual("Awakened Foundry", tier.Name, "Name mismatch");
        AssertEqual(6, tier.RequiredMapTier, "RequiredMapTier mismatch");
        AssertEqual(4, tier.MapSlots, "MapSlots mismatch");
        AssertEqual(10, tier.QueueSlots, "QueueSlots mismatch");
        AssertTrue(tier.SmelterUnlocked, "Smelter should be unlocked");
        AssertTrue(tier.AbyssUnlocked, "Abyss should be unlocked");
        AssertFalse(tier.PinnacleTrophiesUnlocked, "Pinnacle trophies should not be unlocked at tier 2");
    }

    private static void TestSanctuaryStateMutationsAndEvents()
    {
        var state = new SanctuaryState();
        AssertEqual(1, state.CurrentTier, "Default tier should be 1");
        AssertEqual(0L, state.ThaumaturgicDust, "Default dust should be 0");
        AssertTrue(state.AutoSmeltActive, "Default auto smelt should be active");
        AssertEqual(0, state.InfusedMapsRemaining, "Default infused maps should be 0");
        AssertEqual(0f, state.InfusedQuantityBonus, "Default quantity bonus should be 0");

        int eventCount = 0;
        state.OnSanctuaryUpdated += () => eventCount++;

        // Test tier clamp & event
        state.CurrentTier = 3;
        AssertEqual(3, state.CurrentTier, "Tier should be 3");
        AssertEqual(1, eventCount, "Event should have fired for tier change");

        state.CurrentTier = 10;
        AssertEqual(4, state.CurrentTier, "Tier should clamp to 4");

        // Test Dust addition and consumption
        state.AddDust(5000);
        AssertEqual(5000L, state.ThaumaturgicDust, "Dust should be 5000");

        bool consumeFail = state.TryConsumeDust(10000);
        AssertFalse(consumeFail, "Consuming 10000 dust with only 5000 should fail");
        AssertEqual(5000L, state.ThaumaturgicDust, "Dust should remain unchanged on failed consumption");

        bool consumeSuccess = state.TryConsumeDust(2500);
        AssertTrue(consumeSuccess, "Consuming 2500 dust should succeed");
        AssertEqual(2500L, state.ThaumaturgicDust, "Dust should be 2500 after consumption");

        // Test Map Infusion
        state.InfuseMaps(5, 0.10f);
        AssertEqual(5, state.InfusedMapsRemaining, "Infused maps should be 5");
        AssertEqual(0.10f, state.InfusedQuantityBonus, "Infused quantity bonus should be 0.10f");

        state.ConsumeInfusedMap();
        AssertEqual(4, state.InfusedMapsRemaining, "Remaining maps should decrement to 4");
        AssertEqual(0.10f, state.InfusedQuantityBonus, "Quantity bonus should remain active");

        // Consume remaining 4 maps
        state.ConsumeInfusedMap();
        state.ConsumeInfusedMap();
        state.ConsumeInfusedMap();
        state.ConsumeInfusedMap();
        AssertEqual(0, state.InfusedMapsRemaining, "Remaining maps should be 0");
        AssertEqual(0f, state.InfusedQuantityBonus, "Quantity bonus should reset to 0");
    }

    private static void TestCombatDpsSnapshotIntegrity()
    {
        var empty = CombatDpsSnapshot.Empty;
        AssertEqual(0f, empty.CurrentDps, "Empty CurrentDps should be 0");
        AssertEqual(0f, empty.PeakDps, "Empty PeakDps should be 0");
        AssertEqual(100f, empty.AccuracyPercent, "Default AccuracyPercent should be 100%");

        var snapshot = new CombatDpsSnapshot(
            CurrentDps: 154200f,
            PeakDps: 289500f,
            AverageDps: 120000f,
            PhysicalPercent: 40f,
            FirePercent: 35f,
            ColdPercent: 0f,
            LightningPercent: 15f,
            ChaosPercent: 10f,
            HitRateAps: 4.2f,
            AccuracyPercent: 98.5f
        );

        AssertEqual(154200f, snapshot.CurrentDps, "CurrentDps mismatch");
        AssertEqual(289500f, snapshot.PeakDps, "PeakDps mismatch");
        AssertEqual(40f, snapshot.PhysicalPercent, "PhysicalPercent mismatch");
        AssertEqual(4.2f, snapshot.HitRateAps, "HitRateAps mismatch");
    }

    private static void TestSanctuaryTiersJsonDeserialization()
    {
        string sampleJson = @"[
            {
                ""id"": ""tier_1"",
                ""tier"": 1,
                ""name"": ""Hollow Crypt"",
                ""requiredMapTier"": 1,
                ""mapSlots"": 4,
                ""queueSlots"": 10,
                ""smelterUnlocked"": false,
                ""abyssUnlocked"": false,
                ""smelterSpeedBonus"": 0.0,
                ""pinnacleTrophiesUnlocked"": false,
                ""description"": ""A decaying crypt.""
            }
        ]";

        var list = JsonSerializer.Deserialize<List<SanctuaryTierDefinition>>(sampleJson, JsonOptions);
        AssertNotNull(list, "Deserialized list should not be null");
        AssertEqual(1, list!.Count, "List count should be 1");
        AssertEqual("tier_1", list[0].Id, "Id mismatch in deserialized tier");
        AssertEqual("Hollow Crypt", list[0].Name, "Name mismatch in deserialized tier");
    }

    private static void TestCombatDpsTrackerSlidingWindow()
    {
        var tracker = new CombatDpsTracker { WindowDuration = 3.0f };

        tracker.RecordDamage(0.5f, 150f, DamageType.Fire);
        tracker.RecordDamage(1.0f, 150f, DamageType.Physical);
        tracker.RecordDamage(2.0f, 300f, DamageType.Cold);

        tracker.Update(2.0f, 2.0f);

        // Within 2.0s effective window: 600 total / 2.0s = 300 DPS
        AssertEqual(300f, tracker.CurrentDps, "CurrentDps at 2s should be 300");
        AssertEqual(300f, tracker.PeakDps, "PeakDps at 2s should be 300");
        AssertEqual(600f, tracker.TotalDamageDealt, "TotalDamageDealt should be 600");

        // Advance to t = 4.0s (delta = 2.0s). Event at 0.5s is evicted (4.0 - 0.5 = 3.5 > 3.0)
        tracker.Update(4.0f, 2.0f);

        // Remaining events: 150 at 1.0s + 300 at 2.0s = 450 total. Over full 3.0s window = 150 DPS
        AssertEqual(150f, tracker.CurrentDps, "CurrentDps at 4s should be 150");
        AssertEqual(300f, tracker.PeakDps, "PeakDps should remain at peak 300");

        // Advance to t = 6.0s. All earlier events evicted.
        tracker.Update(6.0f, 2.0f);
        AssertEqual(0f, tracker.CurrentDps, "CurrentDps at 6s should drop to 0");
        AssertEqual(300f, tracker.PeakDps, "PeakDps should remain at peak 300 even when quiet");
        AssertEqual(600f, tracker.TotalDamageDealt, "TotalDamageDealt should remain 600");
    }

    private static void TestCombatDpsTrackerBreakdownAndAccuracy()
    {
        var tracker = new CombatDpsTracker { WindowDuration = 3.0f };

        var hit = new HitResult(
            Damage: new HitDamageBreakdown(100f, 100f, 0f, 0f, 0f),
            TotalDamage: 200f,
            IsCrit: true,
            CritMultiplier: 150f,
            InflictedAilments: Array.Empty<AilmentInstance>()
        );

        tracker.RecordHit(1.0f, hit, 200f);
        tracker.RecordDoT(1.5f, 50f, DamageType.Chaos);

        tracker.RecordAttackAttempt(1.0f, wasHit: true);
        tracker.RecordAttackAttempt(1.2f, wasHit: false);

        tracker.Update(2.0f, 2.0f);

        var snapshot = tracker.GetSnapshot();

        // Total = 250: 100 Phys (40%), 100 Fire (40%), 50 Chaos (20%)
        AssertEqual(40f, snapshot.PhysicalPercent, "PhysicalPercent mismatch");
        AssertEqual(40f, snapshot.FirePercent, "FirePercent mismatch");
        AssertEqual(20f, snapshot.ChaosPercent, "ChaosPercent mismatch");
        AssertEqual(0f, snapshot.ColdPercent, "ColdPercent mismatch");

        // Accuracy: 1 hit out of 2 attempts = 50%
        AssertEqual(50f, snapshot.AccuracyPercent, "AccuracyPercent mismatch");

        // Total damage = 250, effective window = 2.0s -> 125 DPS
        AssertEqual(125f, snapshot.CurrentDps, "CurrentDps mismatch");
        AssertEqual(125f, snapshot.PeakDps, "PeakDps mismatch");
    }

    private static void TestCombatDpsTrackerReset()
    {
        var tracker = new CombatDpsTracker();
        tracker.RecordDamage(1.0f, 500f, DamageType.Fire);
        tracker.Update(1.0f, 1.0f);

        AssertTrue(tracker.CurrentDps > 0f, "CurrentDps should be positive before reset");

        tracker.ResetForNewMap();

        AssertEqual(0f, tracker.CurrentDps, "CurrentDps should be 0 after reset");
        AssertEqual(0f, tracker.PeakDps, "PeakDps should be 0 after reset");
        AssertEqual(0f, tracker.TotalDamageDealt, "TotalDamageDealt should be 0 after reset");
        AssertEqual(0f, tracker.CombatDuration, "CombatDuration should be 0 after reset");
        AssertEqual(0, tracker.TotalHits, "TotalHits should be 0 after reset");
    }

    private static void AssertEqual<T>(T expected, T actual, string message)

    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"[FAIL] {message}. Expected: {expected}, Actual: {actual}");
        }
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception($"[FAIL] {message}. Expected true, got false.");
        }
    }

    private static void AssertFalse(bool condition, string message)
    {
        if (condition)
        {
            throw new Exception($"[FAIL] {message}. Expected false, got true.");
        }
    }

    private static void AssertNotNull(object? obj, string message)
    {
        if (obj == null)
        {
            throw new Exception($"[FAIL] {message}. Expected non-null object.");
        }
    }
}
