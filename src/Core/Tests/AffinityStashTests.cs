using System;
using System.Collections.Generic;
using System.Linq;
using IdleExile.Core.Models;
using IdleExile.Core.State;

namespace IdleExile.Core.Tests;

/// <summary>
/// Headless unit tests for AffinityStashState and related models.
/// Validates 4 affinity tabs (Currency, Gear, Maps, Fossils) and 1-Click Dump operations.
/// Pure C# test suite — zero Godot dependencies.
/// </summary>
public static class AffinityStashTests
{
    public static void RunAllTests()
    {
        Console.WriteLine("[AffinityStashTests] Running Affinity Stash test suite...");

        TestCurrencyDepositWithdrawAndStacking();
        TestGearDepositWithdrawAndFiltering();
        TestMapDepositWithdrawAndToQueueItem();
        TestFossilDepositWithdrawAndBonus();
        TestAffinityDumpAll();

        Console.WriteLine("[AffinityStashTests] ✓ All 5 Affinity Stash test suites passed successfully!");
    }

    public static void TestCurrencyDepositWithdrawAndStacking()
    {
        var stash = new AffinityStashState();
        Assert(stash.TotalCurrencyCount == 0, "Initial stash should have 0 currencies");

        // Deposit currencies
        stash.DepositCurrency("chaos_orb", 5);
        stash.DepositCurrency("chaos_orb", 10);
        stash.DepositCurrency("divine_orb", 2);

        Assert(stash.GetCurrencyCount("chaos_orb") == 15, "Chaos Orb count should be 15 after stacking");
        Assert(stash.GetCurrencyCount("divine_orb") == 2, "Divine Orb count should be 2");
        Assert(stash.TotalCurrencyCount == 17, "Total currencies should be 17");

        // Withdraw currencies
        bool withdrawSuccess = stash.WithdrawCurrency("chaos_orb", 5);
        Assert(withdrawSuccess, "Withdrawing 5 Chaos Orbs should succeed");
        Assert(stash.GetCurrencyCount("chaos_orb") == 10, "Chaos Orb count should be 10 after withdrawal");

        // Withdraw failure (insufficient)
        bool overdraw = stash.WithdrawCurrency("divine_orb", 5);
        Assert(!overdraw, "Withdrawing more Divine Orbs than available must fail");
        Assert(stash.GetCurrencyCount("divine_orb") == 2, "Divine Orb count must remain unchanged");

        // Full withdrawal
        stash.WithdrawCurrency("divine_orb", 2);
        Assert(stash.GetCurrencyCount("divine_orb") == 0, "Divine Orb count should be 0");
        Assert(!stash.Currencies.ContainsKey("divine_orb"), "Empty currency entry should be pruned");

        Console.WriteLine("  ✓ TestCurrencyDepositWithdrawAndStacking passed.");
    }

    public static void TestGearDepositWithdrawAndFiltering()
    {
        var stash = new AffinityStashState(maxGearSlots: 5);

        var swordDef = new WeaponBaseDefinition("rusted_sword", "Rusted Sword", WeaponClass.OneHandedSword, HandSlotRequirement.OneHanded, 5, 10, 1.45f, 5f);
        var sword = new WeaponItemInstance(swordDef, "Rare", quality: 20, itemLevel: 50) { CustomName = "Doom Edge" };

        var armourDef = new ArmourBaseDefinition("astral_plate", "Astral Plate", ArmourSlot.BodyArmour, 500, 0, 0);
        var chest = new ArmourItemInstance(armourDef, "Rare", quality: 20, itemLevel: 84) { CustomName = "Beast Carapace", IsLocked = true };

        var accessoryDef = new AccessoryBaseDefinition("amber_amulet", "Amber Amulet", AccessorySlot.Amulet, "+30 to Strength", "amulet_implicit_str", 30);
        var amulet = new AccessoryItemInstance(accessoryDef, "Magic", itemLevel: 60) { CustomName = "Solar Choker" };

        // Deposit items
        Assert(stash.DepositGear(sword), "Depositing sword should succeed");
        Assert(stash.DepositGear(chest), "Depositing chest should succeed");
        Assert(stash.DepositGear(amulet), "Depositing amulet should succeed");
        Assert(stash.TotalGearCount == 3, "Total gear count should be 3");

        // Filtering by Slot category
        var weapons = stash.GetFilteredGear("Weapons");
        Assert(weapons.Count == 1 && weapons[0].Name.Contains("Doom Edge"), "Filtering by Weapons should return 1 sword");

        var armours = stash.GetFilteredGear("Armour");
        Assert(armours.Count == 1 && armours[0].Name.Contains("Beast Carapace"), "Filtering by Armour should return 1 chest");

        var accessories = stash.GetFilteredGear("Jewellery");
        Assert(accessories.Count == 1 && accessories[0].Name.Contains("Solar Choker"), "Filtering by Jewellery should return 1 amulet");

        // Filtering by Locked
        var lockedItems = stash.GetFilteredGear("Locked");
        Assert(lockedItems.Count == 1 && lockedItems[0].IsLocked, "Filtering by Locked should return 1 locked item");

        // Search filtering
        var searchResults = stash.GetFilteredGear(search: "Carapace");
        Assert(searchResults.Count == 1 && searchResults[0].Name.Contains("Beast Carapace"), "Searching for 'Carapace' should return chest");

        // Withdraw gear
        bool withdrawSuccess = stash.WithdrawGear(sword.InstanceId, out var withdrawn);
        Assert(withdrawSuccess && withdrawn != null, "Withdrawing sword should succeed");
        Assert(stash.TotalGearCount == 2, "Stash should have 2 gear items remaining");

        Console.WriteLine("  ✓ TestGearDepositWithdrawAndFiltering passed.");
    }

    public static void TestMapDepositWithdrawAndToQueueItem()
    {
        var stash = new AffinityStashState();

        var strandDef = new MapDefinition("strand", "Strand Map", 1, 1, false, 10, 100, "Tidal Behemoth", "Coastal map.");
        var canyonDef = new MapDefinition("canyon", "Canyon Map", 7, 58, false, 35, 120, "Gorgon Queen", "Gorge map.");

        // Deposit maps
        stash.DepositMap(strandDef, 3, "Normal");
        stash.DepositMap(strandDef, 2, "Normal");
        stash.DepositMap(canyonDef, 1, "Rare");

        Assert(stash.TotalMapCount == 6, "Total maps should be 6");

        var strandSlate = stash.FindMap("strand", 1, "Normal");
        Assert(strandSlate != null, "Strand slate must exist");
        Assert(strandSlate!.Quantity == 5, "Strand quantity should be stacked to 5");

        // Convert to MapQueueItem
        var queueItem = strandSlate.ToQueueItem(slotIndex: 2);
        Assert(queueItem.SlotIndex == 2, "SlotIndex should be 2");
        Assert(queueItem.Tier == 1, "QueueItem tier should be 1");
        Assert(queueItem.Map.Id == "strand", "QueueItem map ID should be strand");

        // Filter maps by tier
        var whiteMaps = stash.GetFilteredMaps(minTier: 1, maxTier: 5);
        Assert(whiteMaps.Count == 1 && whiteMaps[0].MapId == "strand", "White maps filter should return Strand");

        var yellowMaps = stash.GetFilteredMaps(minTier: 6, maxTier: 10);
        Assert(yellowMaps.Count == 1 && yellowMaps[0].MapId == "canyon", "Yellow maps filter should return Canyon");

        // Withdraw map
        bool withdrawSuccess = stash.WithdrawMap("strand", 1, "Normal", 2);
        Assert(withdrawSuccess, "Withdrawing 2 strand maps should succeed");
        Assert(strandSlate.Quantity == 3, "Strand quantity should now be 3");

        Console.WriteLine("  ✓ TestMapDepositWithdrawAndToQueueItem passed.");
    }

    public static void TestFossilDepositWithdrawAndBonus()
    {
        var stash = new AffinityStashState();

        stash.DepositFossil("pristine_fossil", 10);
        stash.DepositFossil("dense_fossil", 5);
        stash.DepositFossil("sanctified_fossil", 2);

        Assert(stash.GetFossilCount("pristine_fossil") == 10, "Pristine Fossil count should be 10");
        Assert(stash.GetFossilCount("dense_fossil") == 5, "Dense Fossil count should be 5");
        Assert(stash.TotalFossilCount == 17, "Total fossils should be 17");

        bool withdrawSuccess = stash.WithdrawFossil("pristine_fossil", 4);
        Assert(withdrawSuccess, "Withdrawing 4 pristine fossils should succeed");
        Assert(stash.GetFossilCount("pristine_fossil") == 6, "Remaining pristine fossils should be 6");

        Console.WriteLine("  ✓ TestFossilDepositWithdrawAndBonus passed.");
    }

    public static void TestAffinityDumpAll()
    {
        var player = new PlayerState();
        var stash = player.Stash;

        // Populate player backpack
        player.Currencies["chaos_orb"] = 5;
        player.Currencies["divine_orb"] = 1;
        player.Currencies["pristine_fossil"] = 3; // Fossil in currency dictionary

        player.MapInventory.Add("strand");
        player.MapInventory.Add("strand");
        player.MapInventory.Add("canyon");

        var swordDef = new WeaponBaseDefinition("rusted_sword", "Rusted Sword", WeaponClass.OneHandedSword, HandSlotRequirement.OneHanded, 5, 10, 1.45f, 5f);
        var droppedWeapon = new WeaponItemInstance(swordDef, "Rare", 0, 20) { CustomName = "Loot Sword", IsLocked = false };
        var lockedWeapon = new WeaponItemInstance(swordDef, "Rare", 0, 20) { CustomName = "Locked Sword", IsLocked = true };

        var candidates = new List<IGearItem> { droppedWeapon, lockedWeapon };

        var mapCatalog = new Dictionary<string, MapDefinition>
        {
            ["strand"] = new MapDefinition("strand", "Strand Map", 1, 1, false, 10, 100, null, ""),
            ["canyon"] = new MapDefinition("canyon", "Canyon Map", 7, 58, false, 35, 120, null, "")
        };

        // Execute 1-Click Affinity Dump
        var result = stash.DumpAll(player, mapCatalog, candidates);

        Assert(!result.IsEmpty, "AffinityDumpResult should not be empty");
        Assert(result.CurrenciesDumped == 6, $"Expected 6 currencies dumped, got {result.CurrenciesDumped}");
        Assert(result.FossilsDumped == 3, $"Expected 3 fossils dumped, got {result.FossilsDumped}");
        Assert(result.MapsDumped == 3, $"Expected 3 maps dumped, got {result.MapsDumped}");
        Assert(result.GearDumped == 1, $"Expected 1 gear item dumped (excluding locked), got {result.GearDumped}");

        // Player bags should be cleared
        Assert(player.Currencies.Count == 0, "Player backpack currencies should be empty after dump");
        Assert(player.MapInventory.Count == 0, "Player backpack maps should be empty after dump");

        // Stash should hold deposited items
        Assert(stash.GetCurrencyCount("chaos_orb") == 5, "Stash should have 5 Chaos Orbs");
        Assert(stash.GetCurrencyCount("divine_orb") == 1, "Stash should have 1 Divine Orb");
        Assert(stash.GetFossilCount("pristine_fossil") == 3, "Stash should have 3 Pristine Fossils");
        Assert(stash.TotalMapCount == 3, "Stash should have 3 maps total");
        Assert(stash.TotalGearCount == 1, "Stash should have 1 gear item total");

        string summary = result.BuildSummaryText();
        Assert(!string.IsNullOrWhiteSpace(summary), "Summary text must not be empty");
        Console.WriteLine($"  Dump Summary: {summary}");

        Console.WriteLine("  ✓ TestAffinityDumpAll passed.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception($"[AffinityStashTests Assertion Failed] {message}");
        }
    }
}
