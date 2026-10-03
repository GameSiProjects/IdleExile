using System;
using System.Collections.Generic;
using System.Linq;
using IdleExile.Core.Models;
using IdleExile.Core.Services;
using IdleExile.Core.State;

namespace IdleExile.Core.Tests;

/// <summary>
/// Unit test suite verifying the 10-slot Equipment System, Local vs Global modifiers,
/// Soft Attributes scaling, and Smart Comparison Service.
/// Pure C# — zero Godot dependencies.
/// </summary>
public static class EquipmentSystemTests
{
    public static void RunAll()
    {
        Console.WriteLine("[Tests] Running EquipmentSystemTests (Task 3.4 & Phase 3 Core)...");

        TestTenSlotsEquipAndUnequip();
        TestTwoHandedLockoutRules();
        TestDualWieldingBonus();
        TestLocalArmourCalculation();
        TestSoftAttributesIntegration();
        TestSmartDeltaComparison();
        TestSmartDeltaFormatter();
        TestItemLocking();

        Console.WriteLine("[Tests] All EquipmentSystemTests PASSED successfully!");
    }

    private static void TestTenSlotsEquipAndUnequip()
    {
        var gear = new EquippedGearState();

        // 1. Create one item for each of the 10 slots
        var swordDef = new WeaponBaseDefinition("sword", "Broadsword", WeaponClass.OneHandedSword, HandSlotRequirement.OneHanded, 10, 20, 1.4f, 5f);
        var shieldDef = new ArmourBaseDefinition("shield", "Iron Shield", ArmourSlot.Shield, BaseArmour: 100, BaseBlockChance: 25);
        var helmDef = new ArmourBaseDefinition("helm", "Iron Helm", ArmourSlot.Helmet, BaseArmour: 80);
        var bodyDef = new ArmourBaseDefinition("body", "Plate Armor", ArmourSlot.BodyArmour, BaseArmour: 200);
        var gloveDef = new ArmourBaseDefinition("glove", "Gauntlets", ArmourSlot.Gloves, BaseArmour: 60);
        var bootDef = new ArmourBaseDefinition("boot", "Iron Boots", ArmourSlot.Boots, BaseArmour: 70);
        var beltDef = new AccessoryBaseDefinition("belt", "Leather Belt", AccessorySlot.Belt, ImplicitDescription: "+40 to Maximum Life", ImplicitValue: 40);
        var amuDef = new AccessoryBaseDefinition("amulet", "Onyx Amulet", AccessorySlot.Amulet, ImplicitDescription: "+16 to all Attributes", ImplicitValue: 16);
        var ring1Def = new AccessoryBaseDefinition("ring1", "Iron Ring", AccessorySlot.Ring);
        var ring2Def = new AccessoryBaseDefinition("ring2", "Diamond Ring", AccessorySlot.Ring);

        var sword = new WeaponItemInstance(swordDef);
        var shield = new ArmourItemInstance(shieldDef);
        var helm = new ArmourItemInstance(helmDef);
        var body = new ArmourItemInstance(bodyDef);
        var glove = new ArmourItemInstance(gloveDef);
        var boot = new ArmourItemInstance(bootDef);
        var belt = new AccessoryItemInstance(beltDef);
        var amu = new AccessoryItemInstance(amuDef);
        var ring1 = new AccessoryItemInstance(ring1Def);
        var ring2 = new AccessoryItemInstance(ring2Def);

        // Equip all 10
        AssertTrue(gear.Equip(GearSlot.MainHand, sword), "Equip MainHand");
        AssertTrue(gear.Equip(GearSlot.OffHand, shield), "Equip OffHand Shield");
        AssertTrue(gear.Equip(GearSlot.Helmet, helm), "Equip Helmet");
        AssertTrue(gear.Equip(GearSlot.BodyArmour, body), "Equip BodyArmour");
        AssertTrue(gear.Equip(GearSlot.Gloves, glove), "Equip Gloves");
        AssertTrue(gear.Equip(GearSlot.Boots, boot), "Equip Boots");
        AssertTrue(gear.Equip(GearSlot.Belt, belt), "Equip Belt");
        AssertTrue(gear.Equip(GearSlot.Amulet, amu), "Equip Amulet");
        AssertTrue(gear.Equip(GearSlot.Ring1, ring1), "Equip Ring1");
        AssertTrue(gear.Equip(GearSlot.Ring2, ring2), "Equip Ring2");

        // Verify all 10 are retrievable
        AssertEqual(sword.InstanceId, gear.GetItem(GearSlot.MainHand)?.InstanceId, "MainHand matches");
        AssertEqual(shield.InstanceId, gear.GetItem(GearSlot.OffHand)?.InstanceId, "OffHand matches");
        AssertEqual(helm.InstanceId, gear.GetItem(GearSlot.Helmet)?.InstanceId, "Helmet matches");
        AssertEqual(body.InstanceId, gear.GetItem(GearSlot.BodyArmour)?.InstanceId, "BodyArmour matches");
        AssertEqual(glove.InstanceId, gear.GetItem(GearSlot.Gloves)?.InstanceId, "Gloves matches");
        AssertEqual(boot.InstanceId, gear.GetItem(GearSlot.Boots)?.InstanceId, "Boots matches");
        AssertEqual(belt.InstanceId, gear.GetItem(GearSlot.Belt)?.InstanceId, "Belt matches");
        AssertEqual(amu.InstanceId, gear.GetItem(GearSlot.Amulet)?.InstanceId, "Amulet matches");
        AssertEqual(ring1.InstanceId, gear.GetItem(GearSlot.Ring1)?.InstanceId, "Ring1 matches");
        AssertEqual(ring2.InstanceId, gear.GetItem(GearSlot.Ring2)?.InstanceId, "Ring2 matches");

        // Verify Unequip
        var unequippedBody = gear.Unequip(GearSlot.BodyArmour);
        AssertEqual(body.InstanceId, unequippedBody?.InstanceId, "Unequipped body matches");
        AssertTrue(gear.GetItem(GearSlot.BodyArmour) == null, "Body slot now empty");
    }

    private static void TestTwoHandedLockoutRules()
    {
        var gear = new EquippedGearState();

        var twoHandSwordDef = new WeaponBaseDefinition("colossus_sword", "Colossus Sword", WeaponClass.TwoHandedSword, HandSlotRequirement.TwoHanded, 50, 100, 1.2f, 5f);
        var oneHandSwordDef = new WeaponBaseDefinition("broadsword", "Broadsword", WeaponClass.OneHandedSword, HandSlotRequirement.OneHanded, 10, 20, 1.4f, 5f);
        var shieldDef = new ArmourBaseDefinition("shield", "Shield", ArmourSlot.Shield, BaseArmour: 100);

        var twoHandSword = new WeaponItemInstance(twoHandSwordDef);
        var oneHandSword = new WeaponItemInstance(oneHandSwordDef);
        var shield = new ArmourItemInstance(shieldDef);

        // Equip 1H sword and shield first
        gear.Equip(GearSlot.MainHand, oneHandSword);
        gear.Equip(GearSlot.OffHand, shield);
        AssertTrue(gear.OffHand != null, "Offhand equipped");

        // Equipping 2H weapon must clear off-hand and flag off-hand as disabled
        gear.Equip(GearSlot.MainHand, twoHandSword);
        AssertTrue(gear.OffHand == null, "2H weapon must automatically clear off-hand");
        AssertTrue(gear.IsOffHandDisabledByTwoHander, "IsOffHandDisabledByTwoHander must be true");

        // Attempting to equip shield into off-hand while 2H weapon is held must fail
        bool equippedShield = gear.Equip(GearSlot.OffHand, shield);
        AssertFalse(equippedShield, "Equipping offhand with 2H weapon must be blocked");
    }

    private static void TestDualWieldingBonus()
    {
        var gear = new EquippedGearState();
        var swordDef1 = new WeaponBaseDefinition("s1", "Sword 1", WeaponClass.OneHandedSword, HandSlotRequirement.OneHanded, 10, 20, 1.50f, 5f);
        var swordDef2 = new WeaponBaseDefinition("s2", "Sword 2", WeaponClass.OneHandedSword, HandSlotRequirement.OneHanded, 12, 22, 1.30f, 5f);

        gear.EquipMainHand(new WeaponItemInstance(swordDef1));
        gear.EquipOffHand(new WeaponItemInstance(swordDef2));

        AssertTrue(gear.IsDualWielding, "Two 1H weapons trigger Dual-Wielding");
        AssertEqual(1.10f, gear.DualWieldMoreAttackSpeed, "Dual-Wield grants +10% More APS factor");
        AssertEqual(15f, gear.DualWieldBlockBonus, "Dual-Wield grants +15% Block Chance");

        // Average APS: (1.50 + 1.30) / 2 = 1.40 * 1.10 = 1.54
        float effAps = gear.GetEffectiveBaseAps();
        AssertTrue(MathF.Abs(effAps - 1.54f) < 0.05f, $"Dual-Wield effective APS should be ~1.54, got {effAps}");
    }

    private static void TestLocalArmourCalculation()
    {
        // Astral Plate base: 780 Armour, Quality +20%
        var def = new ArmourBaseDefinition("astral_plate", "Astral Plate", ArmourSlot.BodyArmour, BaseArmour: 780);
        var item = new ArmourItemInstance(def, "Rare", quality: 20);

        // Add local % increased armour affix (+80%)
        item.Affixes.Add(new AffixDefinition("local_armour_t1", "{0}% Increased Armour", AffixType.Prefix, 1, 80, 80, 80, new[] { "armour", "local" }));
        item.RecalculateStats();

        // Formula: 780 * (1 + (20 + 80)/100) = 780 * 2.0 = 1560
        AssertEqual(1560f, item.LocalArmour, "Local armour calculation incorporating Quality and Local % Increased Armour");
    }

    private static void TestSoftAttributesIntegration()
    {
        var player = new PlayerState();
        player.Initialize(100f, 0f, 50f);

        player.BaseStrength = 50;     // 50 Str -> +25 Life, +10% Armour
        player.BaseDexterity = 40;    // 40 Dex -> +80 Acc, +8% Evasion
        player.BaseIntelligence = 30; // 30 Int -> +15 Mana, +6% ES

        player.RecalculateStatsFromGear();

        // MaxLife = 100 + 25 = 125
        AssertEqual(125f, player.MaxLife, "Player MaxLife incorporates Soft Attribute Strength bonus (+5 per 10 Str)");
        // MaxMana = 50 + 15 = 65
        AssertEqual(65f, player.MaxMana, "Player MaxMana incorporates Soft Attribute Intelligence bonus (+5 per 10 Int)");
    }

    private static void TestSmartDeltaComparison()
    {
        var player = new PlayerState();
        player.Initialize(1000f, 200f, 200f);

        var currentBodyDef = new ArmourBaseDefinition("vest", "Plate Vest", ArmourSlot.BodyArmour, BaseArmour: 100);
        var currentBody = new ArmourItemInstance(currentBodyDef);
        player.Gear.Equip(GearSlot.BodyArmour, currentBody);

        // Candidate: Astral Plate (+780 Armour, +115 Life, +45% Fire Res)
        var candidateDef = new ArmourBaseDefinition("astral", "Astral Plate", ArmourSlot.BodyArmour, BaseArmour: 780);
        var candidate = new ArmourItemInstance(candidateDef, "Rare", quality: 20);
        candidate.Affixes.Add(new AffixDefinition("life", "+{0} to Maximum Life", AffixType.Prefix, 1, 80, 115, 115, new[] { "life" }));
        candidate.Affixes.Add(new AffixDefinition("fire", "+{0}% to Fire Resistance", AffixType.Suffix, 1, 80, 45, 45, new[] { "fire", "resistance" }));
        candidate.RecalculateStats();

        var delta = EquipmentComparisonService.Compare(player, GearSlot.BodyArmour, candidate);

        AssertTrue(delta.DeltaEhp > 0f, "Candidate item must show positive Delta EHP");
        AssertTrue(delta.IsUpgrade, "Candidate item with strictly higher stats is marked as IsUpgrade");
        AssertTrue(delta.HighlightNotes.Count > 0, "Highlight notes generated for quick skimmability");
        AssertEqual(45f, delta.DeltaFireRes, "Delta Fire Resistance matches candidate affix bonus");
    }

    private static void TestSmartDeltaFormatter()
    {
        var player = new PlayerState();
        player.Initialize(1000f, 200f, 200f);

        var currentBodyDef = new ArmourBaseDefinition("vest", "Plate Vest", ArmourSlot.BodyArmour, BaseArmour: 100);
        var currentBody = new ArmourItemInstance(currentBodyDef);
        player.Gear.Equip(GearSlot.BodyArmour, currentBody);

        var candidateDef = new ArmourBaseDefinition("astral", "Astral Plate", ArmourSlot.BodyArmour, BaseArmour: 780);
        var candidate = new ArmourItemInstance(candidateDef, "Rare", quality: 20);
        candidate.Affixes.Add(new AffixDefinition("life_t1", "+{0} to Maximum Life", AffixType.Prefix, 1, 80, 115, 115, new[] { "life" }));
        candidate.Affixes.Add(new AffixDefinition("armour_t2", "{0}% Increased Armour", AffixType.Prefix, 2, 70, 80, 80, new[] { "armour" }));
        candidate.Affixes.Add(new AffixDefinition("fire_t1", "+{0}% to Fire Resistance", AffixType.Suffix, 1, 80, 45, 45, new[] { "fire", "resistance" }));
        candidate.Affixes.Add(new AffixDefinition("cold_t4", "+{0}% to Cold Resistance", AffixType.Suffix, 4, 30, 20, 20, new[] { "cold", "resistance" }));
        candidate.RecalculateStats();

        var delta = EquipmentComparisonService.Compare(player, GearSlot.BodyArmour, candidate);

        // 1. Decisive Lines (EHP and DPS)
        var ehpLine = SmartDeltaFormatter.FormatEhpDelta(delta);
        AssertEqual(DeltaDirection.Up, ehpLine.Direction, "EHP direction should be Up");
        AssertTrue(ehpLine.Headline.StartsWith("▲ +"), $"EHP Headline should start with '▲ +', was '{ehpLine.Headline}'");
        AssertTrue(ehpLine.Headline.Contains("EHP"), "EHP Headline should mention EHP");
        AssertTrue(ehpLine.Detail.Contains("→"), "EHP Detail should contain arrow indicator");

        var dpsLine = SmartDeltaFormatter.FormatDpsDelta(delta);
        AssertTrue(dpsLine.Headline.Contains("DPS"), "DPS Headline should contain DPS");

        // 2. Verdict Classification
        var verdict = SmartDeltaFormatter.ClassifyVerdict(delta);
        AssertEqual(DeltaVerdict.Upgrade, verdict, "Strictly better item should be classified as Upgrade");
        AssertEqual("▲ NÂNG CẤP", SmartDeltaFormatter.VerdictText(verdict), "Verdict text should be ▲ NÂNG CẤP");

        // 3. Resistance Summary
        string resSummary = SmartDeltaFormatter.FormatResistanceSummary(delta);
        AssertTrue(resSummary.Contains("Fire +45"), $"Res summary should contain 'Fire +45', was '{resSummary}'");
        AssertTrue(resSummary.Contains("Cold +20"), $"Res summary should contain 'Cold +20', was '{resSummary}'");

        // 4. Affix Summary (T1/T2 filtering)
        var fullSummary = SmartDeltaFormatter.SummarizeAffixes(candidate.Affixes, highTierOnly: false);
        AssertEqual(2, fullSummary.PrefixCount, "Should have 2 Prefixes");
        AssertEqual(2, fullSummary.SuffixCount, "Should have 2 Suffixes");
        AssertEqual(3, fullSummary.HighTierCount, "Should have 3 high tier (T1/T2) affixes");
        AssertEqual(1, fullSummary.BestTier, "Best tier should be 1");
        AssertEqual(4, fullSummary.Lines.Count, "All 4 lines included when highTierOnly is false");

        var highTierSummary = SmartDeltaFormatter.SummarizeAffixes(candidate.Affixes, highTierOnly: true);
        AssertEqual(3, highTierSummary.Lines.Count, "Only 3 high tier lines included when highTierOnly is true");
        AssertTrue(highTierSummary.Lines.All(l => l.Tier <= 2), "All lines must be Tier <= 2");

        string headerText = SmartDeltaFormatter.FormatAffixHeader(fullSummary);
        AssertTrue(headerText.Contains("4 MODS"), "Header must mention 4 MODS");
        AssertTrue(headerText.Contains("2P/2S"), "Header must mention 2P/2S");
        AssertTrue(headerText.Contains("★ T1-T2 ×3"), "Header must mention ★ T1-T2 ×3");
    }

    private static void TestItemLocking()
    {
        var def = new AccessoryBaseDefinition("belt", "Leather Belt", AccessorySlot.Belt);
        var item = new AccessoryItemInstance(def);

        AssertFalse(item.IsLocked, "Item initially unlocked");
        item.IsLocked = true;
        AssertTrue(item.IsLocked, "Item successfully locked [Khóa Đồ]");
    }

    // ─────────────────────────────────────────────
    //  Assertions
    // ─────────────────────────────────────────────

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[Assertion Failed] Expected TRUE, but got FALSE: {message}");
    }

    private static void AssertFalse(bool condition, string message)
    {
        if (condition)
            throw new Exception($"[Assertion Failed] Expected FALSE, but got TRUE: {message}");
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"[Assertion Failed] Expected: {expected}, Actual: {actual} — {message}");
    }
}
