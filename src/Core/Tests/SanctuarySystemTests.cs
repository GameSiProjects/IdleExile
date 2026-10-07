using System;
using System.Collections.Generic;
using System.Text.Json;
using IdleExile.Core.Models;
using IdleExile.Core.Services;
using IdleExile.Core.Simulation;
using IdleExile.Core.State;

namespace IdleExile.Core.Tests;

/// <summary>
/// Unit tests for The Crucible Sanctuary, In-Combat DPS Snapshot, and Thaumaturgic Smelter systems.
/// </summary>
public static class SanctuarySystemTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static void RunAll()
    {
        Console.WriteLine("[Tests] Running SanctuarySystemTests (Prompts 1, 2 & 3: Sanctuary, DPS Meter, Smelter & Dust Economy)...");

        TestSanctuaryTierDefinitionModel();
        TestSanctuaryStateMutationsAndEvents();
        TestCombatDpsSnapshotIntegrity();
        TestSanctuaryTiersJsonDeserialization();
        TestCombatDpsTrackerSlidingWindow();
        TestCombatDpsTrackerBreakdownAndAccuracy();
        TestCombatDpsTrackerReset();

        // Prompt 3: Thaumaturgic Smelter & Dust Economy
        TestSmelterCalculateDustYield();
        TestSmelterProcessFilteredDrop();
        TestSmelterTryInfuseNextMaps();
        TestSmelterTryTransmuteToCurrency();
        TestSmelterLootFilterEvaluation();
        TestSmelterRecipesJsonDeserialization();
        TestCombatEngineSmelterIntegration();

        // Prompt 5: The Crucible Anvil, Sulphite, Abyss & Pinnacle Hall
        TestSanctuaryStateSulphiteAndAbyss();
        TestSanctuaryStateVoidstonesAndBossTrophies();
        TestCrucibleAnvilCurrencySlamTransmuteAndChaos();
        TestCrucibleAnvilExaltAnnulAndDivine();
        TestCrucibleAnvilVaalCorruption();
        TestCrucibleAnvilFossilResonatorSlam();

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

    private static void TestSmelterCalculateDustYield()
    {
        var smelter = new SmelterService();

        // 1. Normal Item: ilvl 10, Normal (mult 1x), 0 affixes -> 10 * 1 * 1 = 10
        var normalItem = new EquipmentItem("rusted_sword", "Rusted Sword", LootRarity.Normal, 10, AffixCount: 0);
        AssertEqual(10, smelter.CalculateDustYield(normalItem), "Normal item yield mismatch");

        // 2. Magic Item: ilvl 50, Magic (mult 3x), 2 affixes -> 50 * 3 * 2 = 300
        var magicItem = new EquipmentItem("driftwood_wand", "Gleaming Wand", LootRarity.Magic, 50, AffixCount: 2);
        AssertEqual(300, smelter.CalculateDustYield(magicItem), "Magic item yield mismatch");

        // 3. Rare Item: ilvl 80, Rare (mult 10x), 6 affixes -> 80 * 10 * 6 = 4800
        var rareItem = new EquipmentItem("astral_plate", "Glory Carapace", LootRarity.Rare, 80, AffixCount: 6);
        AssertEqual(4800, smelter.CalculateDustYield(rareItem), "Rare item yield mismatch");

        // 4. Unique Item: ilvl 84, Unique (mult 25x), 4 affixes -> 84 * 25 * 4 = 8400
        var uniqueItem = new EquipmentItem("tabula_rasa", "Tabula Rasa", LootRarity.Unique, 84, AffixCount: 4);
        AssertEqual(8400, smelter.CalculateDustYield(uniqueItem), "Unique item yield mismatch");

        // 5. Tier speed/yield bonus (Tier 3: +50% -> 300 * 1.5 = 450; Tier 4: +100% -> 300 * 2.0 = 600)
        AssertEqual(450, smelter.CalculateDustYield(magicItem, speedOrTierBonus: 0.5f), "Tier 3 yield bonus mismatch");
        AssertEqual(600, smelter.CalculateDustYield(magicItem, speedOrTierBonus: 1.0f), "Tier 4 yield bonus mismatch");

        // 6. LootDrop overload
        var drop = LootDrop.Equipment("iron_cuirass", "Iron Cuirass", LootRarity.Magic);
        int dropYield = smelter.CalculateDustYield(drop, itemLevel: 20);
        // Magic estimate affix count = 1 -> 20 * 3 * 1 = 60
        AssertEqual(60, dropYield, "LootDrop overload yield mismatch");
    }

    private static void TestSmelterProcessFilteredDrop()
    {
        var smelter = new SmelterService();
        var state = new SanctuaryState();

        int eventFiredCount = 0;
        int lastDustGained = 0;
        string? lastItemName = null;

        smelter.OnItemSmelted += (dust, name) =>
        {
            eventFiredCount++;
            lastDustGained = dust;
            lastItemName = name;
        };

        var magicItem = new EquipmentItem("crude_bow", "Swift Bow", LootRarity.Magic, 40, AffixCount: 2);
        // 40 * 3 * 2 = 240 dust

        // When AutoSmeltActive is true
        state.AutoSmeltActive = true;
        int gained = smelter.ProcessFilteredDrop(magicItem, state);
        AssertEqual(240, gained, "Gained dust mismatch");
        AssertEqual(240L, state.ThaumaturgicDust, "State dust mismatch");
        AssertEqual(1, eventFiredCount, "Event should have fired once");
        AssertEqual(240, lastDustGained, "Event dust mismatch");
        AssertEqual("Swift Bow", lastItemName, "Event item name mismatch");

        // When AutoSmeltActive is false
        state.AutoSmeltActive = false;
        int disabledGained = smelter.ProcessFilteredDrop(magicItem, state);
        AssertEqual(0, disabledGained, "Disabled auto-smelt should return 0 dust");
        AssertEqual(240L, state.ThaumaturgicDust, "Dust should not change when auto-smelt is disabled");
        AssertEqual(1, eventFiredCount, "Event should not fire when auto-smelt is disabled");
    }

    private static void TestSmelterTryInfuseNextMaps()
    {
        var smelter = new SmelterService();
        var state = new SanctuaryState();

        state.ThaumaturgicDust = 1000;
        bool fail = smelter.TryInfuseNextMaps(state, cost: 2500);
        AssertFalse(fail, "Should fail when dust is insufficient");
        AssertEqual(1000L, state.ThaumaturgicDust, "Dust should remain unchanged on failure");

        state.ThaumaturgicDust = 5000;
        bool success = smelter.TryInfuseNextMaps(state, cost: 2500, mapCount: 5, bonusQuantity: 0.10f);
        AssertTrue(success, "Should succeed when dust is sufficient");
        AssertEqual(2500L, state.ThaumaturgicDust, "Dust should be deducted");
        AssertEqual(5, state.InfusedMapsRemaining, "InfusedMapsRemaining should be 5");
        AssertEqual(0.10f, state.InfusedQuantityBonus, "InfusedQuantityBonus should be 0.10f");
    }

    private static void TestSmelterTryTransmuteToCurrency()
    {
        var smelter = new SmelterService();
        var state = new SanctuaryState();
        var stash = new AffinityStashState();

        state.ThaumaturgicDust = 5000;
        bool fail = smelter.TryTransmuteToUnmaking(state, stash, cost: 10000);
        AssertFalse(fail, "Should fail when dust is insufficient");
        AssertEqual(0, stash.GetCurrencyCount(SmelterService.CurrencyOrbOfUnmaking), "No currency should be deposited on failure");

        state.ThaumaturgicDust = 25000;
        bool unmakingSuccess = smelter.TryTransmuteToUnmaking(state, stash, cost: 10000);
        AssertTrue(unmakingSuccess, "Should succeed to transmute Orb of Unmaking");
        AssertEqual(15000L, state.ThaumaturgicDust, "Dust should be deducted by 10000");
        AssertEqual(1, stash.GetCurrencyCount(SmelterService.CurrencyOrbOfUnmaking), "Stash should have 1 Orb of Unmaking");

        bool chaosSuccess = smelter.TryTransmuteToChaos(state, stash, cost: 10000);
        AssertTrue(chaosSuccess, "Should succeed to transmute Chaos Orb");
        AssertEqual(5000L, state.ThaumaturgicDust, "Dust should be deducted by 10000");
        AssertEqual(1, stash.GetCurrencyCount(SmelterService.CurrencyChaosOrb), "Stash should have 1 Chaos Orb");
    }

    private static void TestSmelterLootFilterEvaluation()
    {
        // 1. Seeded items are never filtered
        var seeded = LootDrop.Equipment("seeded_sword", "Starter Sword", LootRarity.Normal, isSeeded: true);
        AssertFalse(SmelterService.IsFilteredOut(seeded), "Seeded items must not be filtered out");

        // 2. Tier 1 drops (6-link, unique) are never filtered
        var sixLink = LootDrop.Equipment("astral_plate_6l", "Astral Plate (6-Link)", LootRarity.Rare);
        AssertFalse(SmelterService.IsFilteredOut(sixLink), "6-Link items must not be filtered out");

        var unique = LootDrop.Equipment("tabula_rasa", "Tabula Rasa", LootRarity.Unique);
        AssertFalse(SmelterService.IsFilteredOut(unique), "Unique items must not be filtered out");

        // 3. Regular Rare is not filtered by default
        var rare = LootDrop.Equipment("rare_plate", "Glorious Plate", LootRarity.Rare);
        AssertFalse(SmelterService.IsFilteredOut(rare), "Standard rare equipment should not be filtered by default");

        // 4. White / Normal equipment is filtered out
        var normal = LootDrop.Equipment("iron_cuirass", "Iron Cuirass", LootRarity.Normal);
        AssertTrue(SmelterService.IsFilteredOut(normal), "Normal equipment must be filtered out for auto-smelting");

        // 5. Standard Magic equipment is filtered out
        var magic = LootDrop.Equipment("magic_wand", "Driftwood Wand", LootRarity.Magic);
        AssertTrue(SmelterService.IsFilteredOut(magic), "Standard magic equipment must be filtered out for auto-smelting");

        // 6. Currency, Gem, Map drops are not equipment and must not be filtered out
        var currency = LootDrop.Currency("chaos_orb", "Chaos Orb");
        AssertFalse(SmelterService.IsFilteredOut(currency), "Currency drops must not be filtered out");
    }

    private static void TestSmelterRecipesJsonDeserialization()
    {
        string sampleJson = @"{
            ""rarityMultipliers"": {
                ""Normal"": 1.0,
                ""Magic"": 3.0,
                ""Rare"": 10.0,
                ""Unique"": 25.0
            },
            ""defaultInfuseCost"": 2500,
            ""defaultInfuseMapCount"": 5,
            ""defaultInfuseBonusQuantity"": 0.10,
            ""transmutationOptions"": [
                {
                    ""currencyId"": ""orb_of_unmaking"",
                    ""name"": ""Orb of Unmaking"",
                    ""dustCost"": 10000,
                    ""quantity"": 1
                }
            ]
        }";

        var config = JsonSerializer.Deserialize<SmelterRecipeConfig>(sampleJson, JsonOptions);
        AssertNotNull(config, "SmelterRecipeConfig should not be null");
        AssertEqual(2500, config!.DefaultInfuseCost, "DefaultInfuseCost mismatch");
        AssertEqual(1.0f, config.RarityMultipliers["Normal"], "Normal multiplier mismatch");
        AssertEqual(1, config.TransmutationOptions.Count, "TransmutationOptions count mismatch");
        AssertEqual("orb_of_unmaking", config.TransmutationOptions[0].CurrencyId, "CurrencyId mismatch");
    }

    private static void TestCombatEngineSmelterIntegration()
    {
        var player = new PlayerState();
        var loadout = new SkillLoadout();
        var engine = new CombatEngine(player, loadout);

        // Verify PlayerState has Sanctuary
        AssertNotNull(player.Sanctuary, "PlayerState must have a SanctuaryState instance");
        AssertNotNull(engine.Smelter, "CombatEngine must have a SmelterService instance");

        // Setup Infused Maps
        player.Sanctuary.AddDust(5000);
        bool infused = engine.Smelter.TryInfuseNextMaps(player.Sanctuary);
        AssertTrue(infused, "TryInfuseNextMaps should succeed");
        AssertEqual(5, player.Sanctuary.InfusedMapsRemaining, "InfusedMapsRemaining should be 5");

        // Start a map: verify that an infused map is consumed
        var mapDef = new MapDefinition("strand", "Strand Map", 1, 68, false, 10, 0, "Brine King", "A coastal strand.");
        var config = new MapRunConfig(mapDef, Array.Empty<RolledMapModifier>());
        engine.StartMap(config);

        AssertEqual(4, player.Sanctuary.InfusedMapsRemaining, "Starting map must consume 1 infused map");
    }

    private static void TestSanctuaryStateSulphiteAndAbyss()
    {
        var state = new SanctuaryState();
        AssertEqual(6850, state.VoltaxicSulphite, "Default sulphite should be 6,850");
        AssertEqual(10000, state.MaxVoltaxicSulphite, "Max sulphite should be 10,000");
        AssertEqual(185, state.DeepestDepth, "Default deepest depth should be 185m");

        state.AddSulphite(2000);
        AssertEqual(8850, state.VoltaxicSulphite, "Sulphite should increase to 8,850");

        state.AddSulphite(5000);
        AssertEqual(10000, state.VoltaxicSulphite, "Sulphite should clamp to MaxVoltaxicSulphite");

        bool consumeFail = state.TryConsumeSulphite(15000);
        AssertFalse(consumeFail, "Cannot consume more sulphite than available");

        bool consumeSuccess = state.TryConsumeSulphite(350);
        AssertTrue(consumeSuccess, "Consuming 350 sulphite should succeed");
        AssertEqual(9650, state.VoltaxicSulphite, "Sulphite should decrease to 9,650");

        state.DescendAbyss(500);
        AssertEqual(685, state.DeepestDepth, "Deepest depth should update to 185 + 500 = 685m");
    }

    private static void TestSanctuaryStateVoidstonesAndBossTrophies()
    {
        var state = new SanctuaryState();
        AssertEqual(0, state.VoidstonesCount, "Default Voidstones count should be 0");
        AssertFalse(state.EaterOfWorldsDefeated, "Default Eater of Worlds should be undefeated");
        AssertFalse(state.SearingExarchDefeated, "Default Searing Exarch should be undefeated");
        AssertFalse(state.MavenDefeated, "Default Maven should be undefeated");
        AssertFalse(state.UberElderDefeated, "Default Uber Elder should be undefeated");

        state.EaterOfWorldsDefeated = true;
        AssertEqual(1, state.VoidstonesCount, "VoidstonesCount should be 1 after defeating Eater");

        state.SearingExarchDefeated = true;
        AssertEqual(2, state.VoidstonesCount, "VoidstonesCount should be 2 after defeating Exarch");

        state.MavenDefeated = true;
        state.UberElderDefeated = true;
        AssertEqual(4, state.VoidstonesCount, "VoidstonesCount should be 4 after defeating all Pinnacle Bosses");
        AssertTrue(state.MavenDefeated, "Maven should be defeated");
        AssertTrue(state.UberElderDefeated, "Uber Elder should be defeated");
    }

    private static void TestCrucibleAnvilCurrencySlamTransmuteAndChaos()
    {
        var db = new DatabaseLoader();
        db.LoadAll();
        var player = new PlayerState();
        var anvil = new TheCrucibleAnvilService(new Random(42));
        var basePlate = new ArmourBaseDefinition("astral_plate", "Astral Plate", ArmourSlot.BodyArmour, 500, 0, 0, 0, 68, 180, 0);
        var item = new ArmourItemInstance(basePlate, "Normal", quality: 20, itemLevel: 85);
        player.Gear.Equip(GearSlot.BodyArmour, item);

        // Deposit currencies
        player.Stash.DepositCurrency(TheCrucibleAnvilService.TransmuteId, 10);
        player.Stash.DepositCurrency(TheCrucibleAnvilService.ChaosId, 10);

        AssertTrue(anvil.CanSlamCurrency(item, TheCrucibleAnvilService.TransmuteId), "Normal item can be slammed with Transmute");
        AssertFalse(anvil.CanSlamCurrency(item, TheCrucibleAnvilService.ChaosId), "Normal item cannot be slammed with Chaos");

        // 1. Slam Transmute: Normal -> Magic
        var transmuteResult = anvil.SlamCurrency(item, TheCrucibleAnvilService.TransmuteId, db, player);
        AssertTrue(transmuteResult.Success, "Transmute slam must succeed");
        AssertEqual("Magic", item.Rarity, "Item rarity must become Magic");
        AssertTrue(item.Affixes.Count >= 1 && item.Affixes.Count <= 2, "Magic item must have 1-2 affixes");
        AssertEqual(9, player.Stash.GetCurrencyCount(TheCrucibleAnvilService.TransmuteId), "Transmute count must decrease by 1");

        // 2. Make item Rare and slam Chaos
        item.Rarity = "Rare";
        item.Affixes.Clear();
        item.Affixes.Add(new AffixDefinition("life_t1", "+{0} to Maximum Life", AffixType.Prefix, 1, 80, 100, 100, new[] { "life" }));
        item.RecalculateStats();

        AssertTrue(anvil.CanSlamCurrency(item, TheCrucibleAnvilService.ChaosId), "Rare item can be slammed with Chaos");
        var chaosResult = anvil.SlamCurrency(item, TheCrucibleAnvilService.ChaosId, db, player);
        AssertTrue(chaosResult.Success, "Chaos slam must succeed");
        AssertEqual("Rare", item.Rarity, "Item must remain Rare");
        AssertTrue(item.Affixes.Count >= 4 && item.Affixes.Count <= 6, "Chaos slam must generate 4-6 affixes");
        AssertEqual(9, player.Stash.GetCurrencyCount(TheCrucibleAnvilService.ChaosId), "Chaos count must decrease by 1");
    }

    private static void TestCrucibleAnvilExaltAnnulAndDivine()
    {
        var db = new DatabaseLoader();
        db.LoadAll();
        var player = new PlayerState();
        var anvil = new TheCrucibleAnvilService(new Random(123));
        var basePlate = new ArmourBaseDefinition("astral_plate", "Astral Plate", ArmourSlot.BodyArmour, 500, 0, 0, 0, 68, 180, 0);
        var item = new ArmourItemInstance(basePlate, "Rare", quality: 20, itemLevel: 85);
        item.Affixes.Add(new AffixDefinition("life_t1", "+{0} to Maximum Life", AffixType.Prefix, 1, 80, 100, 100, new[] { "life" }));
        item.Affixes.Add(new AffixDefinition("fire_res_t1", "+{0}% to Fire Resistance", AffixType.Suffix, 1, 80, 45, 45, new[] { "fire", "resistance" }));
        item.RecalculateStats();
        player.Gear.Equip(GearSlot.BodyArmour, item);

        player.Stash.DepositCurrency(TheCrucibleAnvilService.ExaltedId, 5);
        player.Stash.DepositCurrency(TheCrucibleAnvilService.AnnulmentId, 5);
        player.Stash.DepositCurrency(TheCrucibleAnvilService.DivineId, 5);

        // Exalt slam: adds 1 mod
        int countBeforeExalt = item.Affixes.Count;
        var exaltResult = anvil.SlamCurrency(item, TheCrucibleAnvilService.ExaltedId, db, player);
        AssertTrue(exaltResult.Success, "Exalt slam must succeed");
        AssertEqual(countBeforeExalt + 1, item.Affixes.Count, "Exalt must add 1 affix");
        AssertEqual(4, player.Stash.GetCurrencyCount(TheCrucibleAnvilService.ExaltedId), "Exalted count must decrease");

        // Annul slam: removes 1 mod
        int countBeforeAnnul = item.Affixes.Count;
        var annulResult = anvil.SlamCurrency(item, TheCrucibleAnvilService.AnnulmentId, db, player);
        AssertTrue(annulResult.Success, "Annul slam must succeed");
        AssertEqual(countBeforeAnnul - 1, item.Affixes.Count, "Annul must remove 1 affix");
        AssertEqual(4, player.Stash.GetCurrencyCount(TheCrucibleAnvilService.AnnulmentId), "Annul count must decrease");

        // Divine slam: rerolls values
        var divineResult = anvil.SlamCurrency(item, TheCrucibleAnvilService.DivineId, db, player);
        AssertTrue(divineResult.Success, "Divine slam must succeed");
        AssertEqual(4, player.Stash.GetCurrencyCount(TheCrucibleAnvilService.DivineId), "Divine count must decrease");
    }

    private static void TestCrucibleAnvilVaalCorruption()
    {
        var db = new DatabaseLoader();
        db.LoadAll();
        var player = new PlayerState();
        var anvil = new TheCrucibleAnvilService(new Random(999));
        var basePlate = new ArmourBaseDefinition("astral_plate", "Astral Plate", ArmourSlot.BodyArmour, 500, 0, 0, 0, 68, 180, 0);
        var item = new ArmourItemInstance(basePlate, "Rare", quality: 20, itemLevel: 85);
        player.Stash.DepositCurrency(TheCrucibleAnvilService.VaalId, 5);
        player.Stash.DepositCurrency(TheCrucibleAnvilService.ChaosId, 5);

        AssertFalse(TheCrucibleAnvilService.IsCorrupted(item), "Item should not be corrupted initially");
        AssertTrue(anvil.CanSlamCurrency(item, TheCrucibleAnvilService.VaalId), "Item can be slammed with Vaal Orb");

        var vaalResult = anvil.SlamCurrency(item, TheCrucibleAnvilService.VaalId, db, player);
        AssertTrue(vaalResult.Success, "Vaal slam must succeed");
        AssertTrue(TheCrucibleAnvilService.IsCorrupted(item), "Item must become Corrupted");

        // Corrupted item cannot receive any further currency slams
        AssertFalse(anvil.CanSlamCurrency(item, TheCrucibleAnvilService.ChaosId), "Corrupted item cannot receive Chaos slam");
        AssertFalse(anvil.CanSlamCurrency(item, TheCrucibleAnvilService.VaalId), "Corrupted item cannot be corrupted again");

        var failChaosResult = anvil.SlamCurrency(item, TheCrucibleAnvilService.ChaosId, db, player);
        AssertFalse(failChaosResult.Success, "Chaos slam on corrupted item must fail");
    }

    private static void TestCrucibleAnvilFossilResonatorSlam()
    {
        var db = new DatabaseLoader();
        db.LoadAll();
        var player = new PlayerState();
        var anvil = new TheCrucibleAnvilService(new Random(777));
        var basePlate = new ArmourBaseDefinition("astral_plate", "Astral Plate", ArmourSlot.BodyArmour, 500, 0, 0, 0, 68, 180, 0);
        var item = new ArmourItemInstance(basePlate, "Normal", quality: 20, itemLevel: 85);
        player.Gear.Equip(GearSlot.BodyArmour, item);

        player.Stash.DepositFossil("pristine_fossil", 5);
        player.Stash.DepositFossil("metallic_fossil", 5);

        var fossils = new[] { "pristine_fossil", "metallic_fossil" };
        AssertTrue(anvil.CanSlamFossils(item, fossils), "Can slam pristine and metallic fossils");

        var resonatorResult = anvil.SlamFossils(item, fossils, db, player);
        AssertTrue(resonatorResult.Success, "Resonator fossil slam must succeed");
        AssertEqual("Rare", item.Rarity, "Resonator slam must make item Rare");
        AssertTrue(item.Affixes.Count >= 4 && item.Affixes.Count <= 6, "Resonator slam must generate 4-6 affixes");
        AssertEqual(4, player.Stash.GetFossilCount("pristine_fossil"), "Pristine fossil count must decrease");
        AssertEqual(4, player.Stash.GetFossilCount("metallic_fossil"), "Metallic fossil count must decrease");
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
