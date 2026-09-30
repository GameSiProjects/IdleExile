using System;
using Godot;
using IdleExile.Core.Models;
using IdleExile.UI.Components;

namespace IdleExile.UI;

/// <summary>
/// Comprehensive verification test suite for Task 2.5: Floating Combat Text Pool.
/// Validates:
/// 1. Object pool pre-allocation (zero GC node churning).
/// 2. PoE standard 5-damage color coding: White (Phys), Red (Fire), Cold Blue (Cold), Yellow (Lightning), Purple (Chaos).
/// 3. Critical strike presentation: 16px font size, pop & shake animation, and golden outline.
/// 4. Pool recycling under saturation (LRU anti-overflow, fixed node count).
/// 5. Upward motion arc, spring pop scaling, shake decay, and alpha fade out.
/// 6. Damage formatting and HitResult primary damage type detection.
/// 7. Scene integration with BattleViewport.tscn and Arena2D.
/// </summary>
public static class FloatingTextManagerTests
{
    public static void RunAll()
    {
        GD.Print("[Tests] Running FloatingTextManagerTests (Task 2.5)...");

        TestPoolPreallocationAndZeroGC();
        TestPoEStandardDamageColors();
        TestCriticalStrikeVisualsAndShake();
        TestPoolSaturationAndLRURecycling();
        TestMotionArcAndAlphaFadeOut();
        TestPrimaryDamageDetectionAndHitResult();
        TestDamageFormatting();
        TestBattleViewportIntegration();

        GD.Print("[Tests] All FloatingTextManagerTests PASSED successfully!");
    }

    private static void TestPoolPreallocationAndZeroGC()
    {
        var fct = new FloatingTextManager();
        fct.InitializePool(16);

        Assert(fct.Capacity == 16, $"Expected pool capacity 16, got {fct.Capacity}");
        Assert(fct.ActiveCount == 0, $"Expected 0 active items initially, got {fct.ActiveCount}");
        Assert(fct.GetChildCount() == 16, $"Expected 16 pre-allocated label children, got {fct.GetChildCount()}");

        // Spawn one item
        int childCountBefore = fct.GetChildCount();
        var item = fct.SpawnDamageText(150f, new Vector2(100, 100), DamageType.Physical, isCrit: false);

        Assert(item != null, "Spawned item should not be null");
        Assert(item!.IsActive, "Spawned item should be active");
        Assert(item.Label.Visible, "Spawned item label should be visible");
        Assert(fct.ActiveCount == 1, $"Active count should be 1, got {fct.ActiveCount}");
        Assert(fct.GetChildCount() == childCountBefore, "Spawning must NOT allocate new child nodes (Anti-GC)");

        // Clear all
        fct.ClearAll();
        Assert(fct.ActiveCount == 0, "ClearAll should deactivate all items");
        Assert(!item.Label.Visible, "Label should be hidden after ClearAll");

        fct.QueueFree();
    }

    private static void TestPoEStandardDamageColors()
    {
        var fct = new FloatingTextManager();
        fct.InitializePool(8);

        // 1. Verify Color constants
        Assert(FloatingTextManager.GetDamageColor(DamageType.Physical) == FloatingTextManager.ColorPhysical, "Physical color mismatch");
        Assert(FloatingTextManager.GetDamageColor(DamageType.Fire) == FloatingTextManager.ColorFire, "Fire color mismatch");
        Assert(FloatingTextManager.GetDamageColor(DamageType.Cold) == FloatingTextManager.ColorCold, "Cold color mismatch");
        Assert(FloatingTextManager.GetDamageColor(DamageType.Lightning) == FloatingTextManager.ColorLightning, "Lightning color mismatch");
        Assert(FloatingTextManager.GetDamageColor(DamageType.Chaos) == FloatingTextManager.ColorChaos, "Chaos color mismatch");

        // 2. Test spawning each damage type and verifying label settings
        DamageType[] types = [DamageType.Physical, DamageType.Fire, DamageType.Cold, DamageType.Lightning, DamageType.Chaos];
        foreach (var type in types)
        {
            var item = fct.SpawnDamageText(300f, new Vector2(100, 100), type, isCrit: false);
            Assert(item != null, $"Failed to spawn damage text for {type}");
            Color expectedColor = FloatingTextManager.GetDamageColor(type);
            Assert(item!.Settings.FontColor == expectedColor, $"FontColor for {type} should be {expectedColor}, got {item.Settings.FontColor}");
            Assert(item.Settings.FontSize == FloatingTextManager.NormalFontSize, $"Normal font size should be {FloatingTextManager.NormalFontSize}");
            Assert(item.Settings.OutlineColor == FloatingTextManager.ColorBlackOutline, "Normal outline should be black");
            fct.ClearAll();
        }

        fct.QueueFree();
    }

    private static void TestCriticalStrikeVisualsAndShake()
    {
        var fct = new FloatingTextManager();
        fct.InitializePool(8);

        // Spawn a critical strike hit
        var critItem = fct.SpawnDamageText(8420f, new Vector2(200, 100), DamageType.Cold, isCrit: true);
        Assert(critItem != null, "Crit item should not be null");

        // 1. Chữ to 16px
        Assert(critItem!.Settings.FontSize == FloatingTextManager.CritFontSize,
            $"Crit font size should be {FloatingTextManager.CritFontSize}, got {critItem.Settings.FontSize}");

        // 2. Viền vàng
        Assert(critItem.Settings.OutlineColor == FloatingTextManager.ColorGoldOutline,
            "Crit outline color should be golden (#FFD700)");
        Assert(critItem.Settings.OutlineSize == FloatingTextManager.CritOutlineSize,
            $"Crit outline size should be {FloatingTextManager.CritOutlineSize}");

        // 3. Pop & Shake parameters
        Assert(critItem.IsCrit, "Item should have IsCrit = true");
        Assert(critItem.PopScale == FloatingTextManager.CritPopScale, "Crit pop scale should be 1.50");
        Assert(critItem.ShakeMagnitude == FloatingTextManager.CritShakeMagnitude, "Crit shake magnitude should be 3.5");
        Assert(critItem.TotalDuration == FloatingTextManager.CritDuration, "Crit duration should be 0.90s");

        // Simulate frame update during shake phase (0.05s)
        Vector2 posBefore = critItem.Label.Position;
        fct._Process(0.05);

        Assert(critItem.IsActive, "Crit item should remain active");
        Assert(critItem.Label.Scale.X > 1.0f, "Crit scale should be enlarged during initial pop");

        fct.QueueFree();
    }

    private static void TestPoolSaturationAndLRURecycling()
    {
        var fct = new FloatingTextManager();
        const int smallCapacity = 4;
        fct.InitializePool(smallCapacity);

        // Fill pool to capacity
        var item1 = fct.SpawnDamageText(100f, Vector2.Zero, DamageType.Physical);
        var item2 = fct.SpawnDamageText(200f, Vector2.Zero, DamageType.Fire);
        var item3 = fct.SpawnDamageText(300f, Vector2.Zero, DamageType.Cold);
        var item4 = fct.SpawnDamageText(400f, Vector2.Zero, DamageType.Lightning);

        Assert(fct.ActiveCount == smallCapacity, $"Expected pool full at {smallCapacity}, got {fct.ActiveCount}");
        Assert(fct.GetChildCount() == smallCapacity, "Child count should equal capacity");

        // Sequence of item1 is oldest
        int seq1 = item1!.SpawnSequence;
        int seq4 = item4!.SpawnSequence;
        Assert(seq1 < seq4, "Item 1 should have older sequence than item 4");

        // Spawn 5th item when pool is saturated -> Should recycle item 1 (oldest)
        var recycledItem = fct.SpawnDamageText(9999f, Vector2.Zero, DamageType.Chaos);

        Assert(recycledItem != null, "Recycled item should not be null");
        Assert(recycledItem == item1, "Oldest item should be recycled first (LRU)");
        Assert(recycledItem!.Label.Text == "9,999", $"Recycled item should show new text '9,999', got {recycledItem.Label.Text}");
        Assert(fct.ActiveCount == smallCapacity, "Active count must remain clamped to pool capacity");
        Assert(fct.GetChildCount() == smallCapacity, "No extra nodes should ever be allocated on saturation");

        fct.QueueFree();
    }

    private static void TestMotionArcAndAlphaFadeOut()
    {
        var fct = new FloatingTextManager();
        fct.InitializePool(4);

        Vector2 startPos = new(100, 150);
        var item = fct.SpawnDamageText(500f, startPos, DamageType.Physical, isCrit: false);
        Assert(item != null, "Item should not be null");

        // Step 1: At t = 0.2s (opaque phase)
        fct._Process(0.2);
        Assert(item!.IsActive, "Item should be active at t=0.2s");
        Assert(item.BasePosition.Y < startPos.Y, "Item should drift upward over time");
        Assert(Mathf.Abs(item.Label.Modulate.A - 1.0f) < 0.01f, "Item should be fully opaque in first half of life");

        // Step 2: At t = 0.6s (fade-out phase)
        fct._Process(0.4);
        Assert(item.IsActive, "Item should still be active at t=0.6s");
        Assert(item.Label.Modulate.A < 1.0f, $"Item should fade out (alpha < 1.0), got {item.Label.Modulate.A}");

        // Step 3: At t = 0.8s (exceeded duration -> returned to pool)
        fct._Process(0.2);
        Assert(!item.IsActive, "Item should be expired and inactive");
        Assert(!item.Label.Visible, "Label should be invisible after expiration");
        Assert(fct.ActiveCount == 0, "Active count should drop to 0");

        fct.QueueFree();
    }

    private static void TestPrimaryDamageDetectionAndHitResult()
    {
        // 1. Physical dominant hit
        var physBreakdown = new HitDamageBreakdown(physical: 500f, fire: 50f, cold: 20f);
        Assert(FloatingTextManager.DeterminePrimaryDamageType(physBreakdown) == DamageType.Physical, "Primary should be Physical");

        // 2. Fire dominant hit
        var fireBreakdown = new HitDamageBreakdown(physical: 100f, fire: 850f, cold: 120f);
        Assert(FloatingTextManager.DeterminePrimaryDamageType(fireBreakdown) == DamageType.Fire, "Primary should be Fire");

        // 3. Chaos dominant hit
        var chaosBreakdown = new HitDamageBreakdown(physical: 50f, fire: 0f, cold: 0f, lightning: 0f, chaos: 600f);
        Assert(FloatingTextManager.DeterminePrimaryDamageType(chaosBreakdown) == DamageType.Chaos, "Primary should be Chaos");

        // 4. Test SpawnHitResult
        var fct = new FloatingTextManager();
        fct.InitializePool(4);

        var hit = new HitResult(
            Damage: fireBreakdown,
            TotalDamage: 1070f,
            IsCrit: true,
            CritMultiplier: 2.0f,
            InflictedAilments: []
        );

        var item = fct.SpawnHitResult(hit, new Vector2(150, 80));
        Assert(item != null, "SpawnHitResult item should not be null");
        Assert(item!.Settings.FontColor == FloatingTextManager.ColorFire, "Hit result should pick Fire color");
        Assert(item.IsCrit, "Hit result crit flag should transfer to floating text");
        Assert(item.Settings.FontSize == FloatingTextManager.CritFontSize, "Crit hit result should use 16px font");

        fct.QueueFree();
    }

    private static void TestDamageFormatting()
    {
        Assert(FloatingTextManager.FormatDamageText(45f, isCrit: false) == "45", "Format 45");
        Assert(FloatingTextManager.FormatDamageText(450.4f, isCrit: false) == "450", "Format 450");
        Assert(FloatingTextManager.FormatDamageText(1250f, isCrit: false) == "1,250", "Format 1,250");
        Assert(FloatingTextManager.FormatDamageText(8420f, isCrit: true) == "8,420", "Format 8,420 crit");
        Assert(FloatingTextManager.FormatDamageText(142500f, isCrit: false) == "142.5k", "Format 142.5k");
    }

    private static void TestBattleViewportIntegration()
    {
        PackedScene scene = ResourceLoader.Load<PackedScene>("res://scenes/BattleViewport.tscn")!;
        var viewport = (BattleViewport)scene.Instantiate();
        viewport._Ready();

        // 1. Verify FloatingText component reference on BattleViewport
        Assert(viewport.FloatingText != null, "BattleViewport.FloatingText must not be null");
        Assert(viewport.FloatingText!.Capacity >= FloatingTextManager.DefaultPoolSize,
            $"Expected pool capacity >= {FloatingTextManager.DefaultPoolSize}, got {viewport.FloatingText.Capacity}");

        // 2. Verify FloatingText component reference on Arena2D
        Assert(viewport.Arena != null, "BattleViewport.Arena must not be null");
        Assert(viewport.Arena!.FloatingText != null, "Arena2D.FloatingText must not be null");

        // 3. Test spawning damage via BattleViewport API
        viewport.SpawnFloatingDamage(8420f, new Vector2(165, 120), DamageType.Cold, isCrit: true);
        Assert(viewport.FloatingText.ActiveCount == 1, $"Expected 1 active floating text, got {viewport.FloatingText.ActiveCount}");

        // 4. Test spawning custom text via BattleViewport API
        viewport.SpawnFloatingText("EVADED", new Vector2(355, 120), Colors.LightGreen, isCrit: false);
        Assert(viewport.FloatingText.ActiveCount == 2, $"Expected 2 active floating texts, got {viewport.FloatingText.ActiveCount}");

        viewport.QueueFree();
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception($"[Assertion Failed] {message}");
        }
    }
}
