using System;
using System.Collections.Generic;
using System.Linq;
using IdleExile.Core.Models;
using IdleExile.Core.Simulation;
using IdleExile.UI.Components;

namespace IdleExile.Core.Tests;

/// <summary>
/// Unit tests for Task 4.1: Atlas System, Map Device mechanics,
/// Risk vs Reward calculations, and Threat Badge evaluation.
/// </summary>
public static class AtlasSystemTests
{
    public static void RunAll()
    {
        Console.WriteLine("[Tests] Running AtlasSystemTests (Task 4.1 Atlas & Map Device)...");

        TestMapDefinitionIntegrity();
        TestFailSafeEndlessHollow();
        TestRiskVsRewardCalculations();
        TestThreatBadgeEvaluations();
        TestTierCategorization();

        Console.WriteLine("[Tests] All AtlasSystemTests PASSED successfully!");
    }

    private static void TestMapDefinitionIntegrity()
    {
        var strand = new MapDefinition(
            Id: "strand",
            Name: "Strand Map",
            Tier: 1,
            MonsterLevel: 1,
            IsFailSafe: false,
            BaseQuantity: 10,
            BasePackSize: 100,
            BossName: "Tidal Behemoth",
            Description: "A windswept coastline."
        );

        AssertEqual("strand", strand.Id, "Map ID mismatch");
        AssertEqual("Strand Map", strand.Name, "Map Name mismatch");
        AssertEqual(1, strand.Tier, "Tier mismatch");
        AssertEqual(1, strand.MonsterLevel, "Monster level mismatch");
        AssertFalse(strand.IsFailSafe, "Strand should not be fail safe");
        AssertEqual("Tidal Behemoth", strand.BossName, "Boss name mismatch");
    }

    private static void TestFailSafeEndlessHollow()
    {
        var hollow = new MapDefinition(
            Id: "endless_hollow",
            Name: "The Endless Hollow",
            Tier: 0,
            MonsterLevel: 1,
            IsFailSafe: true,
            BaseQuantity: 0,
            BasePackSize: 100,
            BossName: null,
            Description: "A stable pocket dimension."
        );

        AssertTrue(hollow.IsFailSafe, "The Endless Hollow must be marked as FailSafe (Task 2 GDD)");
        AssertEqual(0, hollow.Tier, "The Endless Hollow must be Tier 0");
        AssertEqual(0, hollow.BaseQuantity, "Base quantity should be 0");
        AssertEqual(100, hollow.BasePackSize, "Base pack size should be 100%");

        // Test monster scaling: override level matches player level
        int playerLevel = 42;
        var config = new MapRunConfig(hollow, Array.Empty<RolledMapModifier>(), overrideMonsterLevel: playerLevel);
        AssertEqual(playerLevel, config.EffectiveMonsterLevel, "Effective monster level for T0 must scale with player level");
    }

    private static void TestRiskVsRewardCalculations()
    {
        var redMap = new MapDefinition(
            Id: "crimson_temple",
            Name: "Crimson Temple",
            Tier: 14,
            MonsterLevel: 81,
            IsFailSafe: false,
            BaseQuantity: 65,
            BasePackSize: 135,
            BossName: "Sanguine Priestess",
            Description: "Blood-soaked corridors."
        );

        var reflectModDef = new MapModifierDefinition(
            Id: "monster_reflect_elemental",
            NameTemplate: "Monsters reflect {0}% of Elemental Damage",
            BonusQuantity: 40,
            BonusPackSize: 15,
            Min: 15f,
            Max: 20f
        );

        var minusResModDef = new MapModifierDefinition(
            Id: "player_reduced_max_res",
            NameTemplate: "Players have -{0}% to Maximum Elemental Resistances",
            BonusQuantity: 35,
            BonusPackSize: 20,
            Min: 10f,
            Max: 15f
        );

        var extraColdModDef = new MapModifierDefinition(
            Id: "extra_damage_cold",
            NameTemplate: "Monsters deal {0}% extra damage as Cold",
            BonusQuantity: 15,
            BonusPackSize: 10,
            Min: 30f,
            Max: 50f
        );

        var rolledMods = new List<RolledMapModifier>
        {
            new(reflectModDef, 18f),
            new(minusResModDef, 12f),
            new(extraColdModDef, 45f)
        };

        var config = new MapRunConfig(redMap, rolledMods);

        // Expected totals:
        // Base Qty: 65 + 40 + 35 + 15 = 155%
        // Base Pack Size: 135 + 15 + 20 + 10 = 180%
        AssertEqual(155, config.TotalQuantity, "Total Quantity calculation mismatch");
        AssertEqual(180, config.TotalPackSize, "Total Pack Size calculation mismatch");
        AssertEqual(81, config.EffectiveMonsterLevel, "Effective Monster Level mismatch");

        // Verify formatted names
        AssertTrue(rolledMods[0].FormattedName.Contains("18%"), "Modifier format mismatch");
        AssertTrue(rolledMods[1].FormattedName.Contains("12%"), "Modifier format mismatch");
        AssertTrue(rolledMods[2].FormattedName.Contains("45%"), "Modifier format mismatch");
    }

    private static void TestThreatBadgeEvaluations()
    {
        var reflectDef = new MapModifierDefinition("monster_reflect_elemental", "{0}% ele reflect", 40, 15, 15, 20);
        var minusResDef = new MapModifierDefinition("player_reduced_max_res", "-{0}% max res", 35, 20, 10, 15);
        var noLeechDef = new MapModifierDefinition("cannot_leech_life_mana", "No leech", 25, 15, 0, 0);
        var extraColdDef = new MapModifierDefinition("extra_damage_cold", "+{0}% cold", 15, 10, 30, 50);
        var harmlessDef = new MapModifierDefinition("harmless_bonus_quant", "+{0}% quant", 10, 5, 5, 10);

        var badgeReflect = ThreatBadgeInfo.FromModifier(new RolledMapModifier(reflectDef, 18f));
        AssertNotNull(badgeReflect, "Elemental reflect must generate a threat badge");
        AssertEqual("reflect_ele", badgeReflect.Key, "Badge key mismatch");
        AssertTrue(badgeReflect.TooltipDescription.Contains("DANGER"), "Threat badge must warn player in tooltip");

        var badgeMinusRes = ThreatBadgeInfo.FromModifier(new RolledMapModifier(minusResDef, 12f));
        AssertNotNull(badgeMinusRes, "Minus max res must generate a threat badge");
        AssertEqual("minus_max_res", badgeMinusRes.Key, "Badge key mismatch");

        var badgeNoLeech = ThreatBadgeInfo.FromModifier(new RolledMapModifier(noLeechDef, 0f));
        AssertNotNull(badgeNoLeech, "No leech must generate a threat badge");
        AssertEqual("no_leech", badgeNoLeech.Key, "Badge key mismatch");

        var badgeCold = ThreatBadgeInfo.FromModifier(new RolledMapModifier(extraColdDef, 40f));
        AssertNotNull(badgeCold, "Extra cold damage must generate an elemental threat badge");
        AssertEqual("extra_cold", badgeCold.Key, "Badge key mismatch");

        var badgeHarmless = ThreatBadgeInfo.FromModifier(new RolledMapModifier(harmlessDef, 8f));
        AssertTrue(badgeHarmless == null, "Non-threatening modifier should not generate a threat badge");
    }

    private static void TestTierCategorization()
    {
        var t0 = new MapDefinition("t0", "Hollow", 0, 1, true, 0, 100, null, "");
        var t1 = new MapDefinition("t1", "Beach", 1, 1, false, 10, 100, null, "");
        var t5 = new MapDefinition("t5", "Courtyard", 5, 45, false, 25, 115, null, "");
        var t6 = new MapDefinition("t6", "Canyon", 6, 50, false, 30, 120, null, "");
        var t10 = new MapDefinition("t10", "Promenade", 10, 70, false, 50, 130, null, "");
        var t11 = new MapDefinition("t11", "Temple", 11, 72, false, 55, 135, null, "");
        var t15 = new MapDefinition("t15", "Burial", 15, 82, false, 70, 140, null, "");
        var t16 = new MapDefinition("t16", "Apex", 16, 85, false, 80, 150, null, "");

        AssertEqual("T0", CategorizeTier(t0.Tier), "T0 categorization mismatch");
        AssertEqual("White", CategorizeTier(t1.Tier), "T1 should be White");
        AssertEqual("White", CategorizeTier(t5.Tier), "T5 should be White");
        AssertEqual("Yellow", CategorizeTier(t6.Tier), "T6 should be Yellow");
        AssertEqual("Yellow", CategorizeTier(t10.Tier), "T10 should be Yellow");
        AssertEqual("Red", CategorizeTier(t11.Tier), "T11 should be Red");
        AssertEqual("Red", CategorizeTier(t15.Tier), "T15 should be Red");
        AssertEqual("Pinnacle", CategorizeTier(t16.Tier), "T16 should be Pinnacle");
    }

    private static string CategorizeTier(int tier)
    {
        return tier switch
        {
            0 => "T0",
            >= 1 and <= 5 => "White",
            >= 6 and <= 10 => "Yellow",
            >= 11 and <= 15 => "Red",
            _ => "Pinnacle"
        };
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"Assertion Failed: Expected '{expected}', but was '{actual}'. {message}");
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition) throw new Exception($"Assertion Failed: {message}");
    }

    private static void AssertFalse(bool condition, string message)
    {
        if (condition) throw new Exception($"Assertion Failed: {message}");
    }

    private static void AssertNotNull([System.Diagnostics.CodeAnalysis.NotNull] object? obj, string message)
    {
        if (obj == null) throw new Exception($"Assertion Failed: {message} (Object was null)");
    }
}
