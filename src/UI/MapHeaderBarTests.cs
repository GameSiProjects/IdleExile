using System;
using System.Collections.Generic;
using Godot;
using IdleExile.Core.Models;
using IdleExile.Core.Simulation;
using IdleExile.UI.Components;

namespace IdleExile.UI;

/// <summary>
/// Comprehensive verification tests for Task 2.2: Map Header & Threat Telemetry Component.
/// Validates:
/// 1. Instantiation and UI node bindings (standalone and inside BattleViewport scene).
/// 2. Tier badge coloring brackets (White T1-T5, Yellow T6-T10, Red T11-T15, Purple T16+).
/// 3. Rarity name coloring brackets (Normal, Magic, Rare, Unique, Corrupted).
/// 4. Threat badge generation and categorization from rolled map modifiers.
/// 5. Monster Pack progress bar scaling, clamping, and Boss spawn activation at 100%.
/// 6. End-to-end integration with MapRunConfig from the Core simulation.
/// </summary>
public static class MapHeaderBarTests
{
    public static void RunAll()
    {
        GD.Print("[Tests] Running MapHeaderBarTests (Task 2.2)...");

        TestMapHeaderBarInstantiationAndBinding();
        TestTierColors();
        TestRarityColors();
        TestThreatBadgesDetection();
        TestMonsterPackProgressAndBossSpawn();
        TestSetMapWithMapRunConfig();
        TestSceneIntegrationWithBattleViewport();

        GD.Print("[Tests] All MapHeaderBarTests PASSED successfully!");
    }

    private static void TestMapHeaderBarInstantiationAndBinding()
    {
        var bar = new MapHeaderBar();
        bar._Ready();

        Assert(bar.CurrentMapName == "The Twilight Strand", "Expected default map name 'The Twilight Strand'");
        Assert(bar.CurrentTier == 1, "Expected default tier 1");
        Assert(bar.CurrentRarity == "Normal", "Expected default rarity 'Normal'");
        Assert(bar.CurrentProgress == 0f, "Expected default progress 0%");
        Assert(!bar.IsBossActive, "Boss should not be active initially");

        bar.QueueFree();
    }

    private static void TestTierColors()
    {
        // White Maps: T0 - T5
        Color t1Color = MapHeaderBar.GetTierColor(1);
        Color t5Color = MapHeaderBar.GetTierColor(5);
        Assert(Mathf.Abs(t1Color.R - t5Color.R) < 0.01f, "T1 and T5 should share white map tier color");

        // Yellow Maps: T6 - T10
        Color t6Color = MapHeaderBar.GetTierColor(6);
        Color t10Color = MapHeaderBar.GetTierColor(10);
        Assert(t6Color.R > 0.9f && t6Color.G > 0.8f, "T6 should be in gold/yellow spectrum");
        Assert(t6Color == t10Color, "T6 and T10 should share yellow map tier color");

        // Red Maps: T11 - T15
        Color t11Color = MapHeaderBar.GetTierColor(11);
        Color t15Color = MapHeaderBar.GetTierColor(15);
        Assert(t11Color.R > 0.9f && t11Color.G < 0.4f, "T11 should be in red/crimson spectrum");
        Assert(t11Color == t15Color, "T11 and T15 should share red map tier color");

        // Pinnacle / Void Maps: T16+
        Color t16Color = MapHeaderBar.GetTierColor(16);
        Color t17Color = MapHeaderBar.GetTierColor(17);
        Assert(t16Color.B > 0.9f && t16Color.R > 0.7f, "T16 should be in purple/void spectrum");
        Assert(t16Color == t17Color, "T16 and T17 should share purple tier color");
    }

    private static void TestRarityColors()
    {
        Color normal = MapHeaderBar.GetRarityColor("Normal");
        Color magic = MapHeaderBar.GetRarityColor("Magic");
        Color rare = MapHeaderBar.GetRarityColor("Rare");
        Color unique = MapHeaderBar.GetRarityColor("Unique");
        Color corrupted = MapHeaderBar.GetRarityColor("Corrupted");

        Assert(magic.B > magic.R, "Magic rarity should be blue-dominant");
        Assert(rare.R > 0.9f && rare.G > 0.9f && rare.B < 0.6f, "Rare rarity should be yellow");
        Assert(unique.R > 0.6f && unique.B < 0.3f, "Unique rarity should be orange/brown");
        Assert(corrupted.R > 0.9f && corrupted.G < 0.3f, "Corrupted rarity should be red");
        Assert(normal.R > 0.9f && normal.G > 0.9f && normal.B > 0.9f, "Normal rarity should be white/grey");
    }

    private static void TestThreatBadgesDetection()
    {
        var bar = new MapHeaderBar();
        bar._Ready();

        var defReflectEle = new MapModifierDefinition("monster_reflect_elemental", "Monsters reflect {0}% Elemental Damage", 40, 15, 15, 20);
        var defReducedRes = new MapModifierDefinition("player_reduced_max_res", "Players have -{0}% to Maximum Elemental Resistances", 35, 20, 10, 15);
        var defColdExtra = new MapModifierDefinition("extra_damage_cold", "Monsters deal {0}% extra damage as Cold", 15, 10, 30, 50);

        var rolledMods = new List<RolledMapModifier>
        {
            new(defReflectEle, 18f),
            new(defReducedRes, 12f),
            new(defColdExtra, 42f)
        };

        var mapDef = new MapDefinition(
            Id: "strand",
            Name: "Strand Map",
            Tier: 14,
            MonsterLevel: 81,
            IsFailSafe: false,
            BaseQuantity: 20,
            BasePackSize: 100,
            BossName: "Tidal Behemoth",
            Description: "A windswept coastline."
        );

        bar.SetMap(mapDef, rolledMods, 45f, "Rare");

        Assert(bar.ActiveThreatCount == 3, $"Expected 3 active threat badges, got {bar.ActiveThreatCount}");
        Assert(bar.CurrentMapName == "Strand Map", "Expected map name 'Strand Map'");
        Assert(bar.CurrentTier == 14, "Expected tier 14");
        Assert(bar.CurrentRarity == "Rare", "Expected rarity 'Rare'");
        Assert(bar.CurrentBossName == "Tidal Behemoth", "Expected boss name 'Tidal Behemoth'");

        bar.QueueFree();
    }

    private static void TestMonsterPackProgressAndBossSpawn()
    {
        var bar = new MapHeaderBar();
        bar._Ready();

        // 1. Initial / partial progress
        bar.SetProgress(0f);
        Assert(bar.CurrentProgress == 0f, "Progress should be 0%");
        Assert(!bar.IsBossActive, "Boss should not be active at 0%");

        bar.SetProgress(50f);
        Assert(bar.CurrentProgress == 50f, "Progress should be 50%");
        Assert(!bar.IsBossActive, "Boss should not be active at 50%");

        bar.SetProgress(99.9f);
        Assert(!bar.IsBossActive, "Boss should not be active at 99.9%");

        // 2. Boss threshold triggered at 100%
        bar.SetProgress(100f);
        Assert(bar.CurrentProgress == 100f, "Progress should be 100%");
        Assert(bar.IsBossActive, "Boss should be active at 100%");

        // 3. Clamping bounds check
        bar.SetProgress(150f);
        Assert(bar.CurrentProgress == 100f, "Progress should clamp to 100%");
        Assert(bar.IsBossActive, "Boss should remain active when clamped to 100%");

        bar.SetProgress(-25f);
        Assert(bar.CurrentProgress == 0f, "Progress should clamp to 0%");
        Assert(!bar.IsBossActive, "Boss should not be active at 0%");

        bar.QueueFree();
    }

    private static void TestSetMapWithMapRunConfig()
    {
        var bar = new MapHeaderBar();
        bar._Ready();

        var mapDef = new MapDefinition(
            Id: "ruined_courtyard",
            Name: "Ruined Courtyard",
            Tier: 5,
            MonsterLevel: 45,
            IsFailSafe: false,
            BaseQuantity: 25,
            BasePackSize: 115,
            BossName: "Statue Sentinel",
            Description: "Crumbled imperial pillars."
        );

        var defCold = new MapModifierDefinition("extra_damage_cold", "Monsters deal {0}% extra damage as Cold", 15, 10, 30, 50);
        var rolledMods = new List<RolledMapModifier> { new(defCold, 35f) };

        var config = new MapRunConfig(mapDef, rolledMods);

        bar.SetMap(config, progressPercent: 80f, rarity: "Magic", leagueEncounter: "Breach");

        Assert(bar.CurrentMapName == "Ruined Courtyard", "Map name should match config");
        Assert(bar.CurrentTier == 5, "Tier should be 5");
        Assert(bar.CurrentRarity == "Magic", "Rarity should be Magic");
        Assert(bar.CurrentIIQ == 40, "IIQ should be base 25 + bonus 15 = 40");
        Assert(bar.CurrentPackSize == 10, "Pack size bonus should be 10");
        Assert(bar.CurrentProgress == 80f, "Progress should be 80%");
        Assert(bar.CurrentBossName == "Statue Sentinel", "Boss name should be Statue Sentinel");
        Assert(bar.ActiveThreatCount == 1, "Should have 1 threat badge (+Cold)");

        bar.QueueFree();
    }

    private static void TestSceneIntegrationWithBattleViewport()
    {
        PackedScene scene = ResourceLoader.Load<PackedScene>("res://scenes/BattleViewport.tscn")!;
        var viewport = (BattleViewport)scene.Instantiate();
        viewport._Ready();

        var headerBar = viewport.HeaderBar;
        if (headerBar == null)
        {
            viewport.QueueFree();
            throw new Exception("Assertion failed: BattleViewport.HeaderBar should be bound from scene");
        }

        // Update via high-level API
        viewport.UpdateMapInfo("Twilight Strand", 1, "Normal", 0, 0, 0, 30f);
        Assert(headerBar.CurrentMapName == "Twilight Strand", "HeaderBar map name should be updated");
        Assert(headerBar.CurrentTier == 1, "HeaderBar tier should be 1");
        Assert(headerBar.CurrentProgress == 30f, "HeaderBar progress should be 30%");

        // Update progress directly
        viewport.SetMapProgress(100f);
        Assert(headerBar.IsBossActive, "HeaderBar boss should activate when progress reaches 100%");

        viewport.QueueFree();
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception($"Assertion failed: {message}");
        }
    }
}
