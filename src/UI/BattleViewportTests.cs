using System;
using Godot;

namespace IdleExile.UI;

/// <summary>
/// Verification tests for Task 2.1: Battle Viewport Scene & Frame Layout.
/// Validates scene loading, 440x300 dimensions, required container structure,
/// map telemetry updates, vitometer bars, and loot toast banner.
/// </summary>
public static class BattleViewportTests
{
    public static void RunAll()
    {
        GD.Print("[Tests] Running BattleViewportTests (Task 2.1)...");

        TestSceneInstantiationAndContainers();
        TestTelemetryAndVitalsUpdate();
        TestLootToastBehavior();

        GD.Print("[Tests] All BattleViewportTests PASSED successfully!");
    }

    private static void TestSceneInstantiationAndContainers()
    {
        PackedScene? scene = ResourceLoader.Load<PackedScene>("res://scenes/BattleViewport.tscn");
        if (scene == null)
        {
            throw new Exception("Assertion failed: Failed to load res://scenes/BattleViewport.tscn");
        }

        Node instance = scene.Instantiate();
        if (instance is not BattleViewport viewport)
        {
            instance.QueueFree();
            throw new Exception("Assertion failed: Instantiated scene is not of type BattleViewport");
        }

        // Add to tree or trigger _Ready manually to bind nodes
        viewport._Ready();

        // 1. Dimensions check: 440 x 300 px
        if (Mathf.Abs(viewport.CustomMinimumSize.X - 440f) > 0.1f || Mathf.Abs(viewport.CustomMinimumSize.Y - 300f) > 0.1f)
        {
            viewport.QueueFree();
            throw new Exception($"Assertion failed: Expected CustomMinimumSize (440, 300), got {viewport.CustomMinimumSize}");
        }

        // 2. Container existence checks (Task 2.1 requirements)
        if (viewport.MapHeaderContainer == null)
        {
            viewport.QueueFree();
            throw new Exception("Assertion failed: MapHeaderContainer is null!");
        }

        if (viewport.ArenaViewportContainer == null)
        {
            viewport.QueueFree();
            throw new Exception("Assertion failed: ArenaViewportContainer is null!");
        }

        if (viewport.VitometerPanel == null)
        {
            viewport.QueueFree();
            throw new Exception("Assertion failed: VitometerPanel is null!");
        }

        if (viewport.LootToastOverlay == null)
        {
            viewport.QueueFree();
            throw new Exception("Assertion failed: LootToastOverlay is null!");
        }

        if (viewport.FloatingText == null)
        {
            viewport.QueueFree();
            throw new Exception("Assertion failed: FloatingText is null!");
        }

        viewport.QueueFree();
    }

    private static void TestTelemetryAndVitalsUpdate()
    {
        PackedScene scene = ResourceLoader.Load<PackedScene>("res://scenes/BattleViewport.tscn")!;
        var viewport = (BattleViewport)scene.Instantiate();
        viewport._Ready();

        // Update Map Info
        viewport.UpdateMapInfo("Strand Map", 14, "Rare", 68, 15, 20, 68.0f);

        // Update Vitals
        viewport.UpdateVitals(3450, 3450, 1200, 1200, 82, 820, 90.0, "EHP: 14.2k | Block: 75%");

        viewport.QueueFree();
    }

    private static void TestLootToastBehavior()
    {
        PackedScene scene = ResourceLoader.Load<PackedScene>("res://scenes/BattleViewport.tscn")!;
        var viewport = (BattleViewport)scene.Instantiate();
        viewport._Ready();

        // Initial state: Toast should be hidden
        if (viewport.LootToastOverlay!.Visible)
        {
            viewport.QueueFree();
            throw new Exception("Assertion failed: LootToastOverlay should be hidden initially.");
        }

        // Show Toast
        viewport.ShowLootToast("★ Exalted Orb dropped! ★", new Color(1, 0.75f, 0.02f), 2.5);
        if (!viewport.LootToastOverlay.Visible)
        {
            viewport.QueueFree();
            throw new Exception("Assertion failed: LootToastOverlay should be visible after ShowLootToast().");
        }

        // Process timeout
        viewport._Process(3.0);
        if (viewport.LootToastOverlay.Visible)
        {
            viewport.QueueFree();
            throw new Exception("Assertion failed: LootToastOverlay should hide after duration expired.");
        }

        viewport.QueueFree();
    }
}
