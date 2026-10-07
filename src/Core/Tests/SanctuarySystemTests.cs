using System;
using System.Collections.Generic;
using System.Text.Json;
using IdleExile.Core.Models;

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
        Console.WriteLine("[Tests] Running SanctuarySystemTests (Prompt 1: Data Architecture & Core Models)...");

        TestSanctuaryTierDefinitionModel();
        TestSanctuaryStateMutationsAndEvents();
        TestCombatDpsSnapshotIntegrity();
        TestSanctuaryTiersJsonDeserialization();

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
