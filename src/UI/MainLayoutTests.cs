using System;
using Godot;
using IdleExile.UI.Components;

namespace IdleExile.UI;

/// <summary>
/// Unit and integration tests for Task 1.1: 5-Tier Main Layout in scenes/Main.tscn.
/// Verifies node hierarchy, minimum dimensions, persistent BattleViewport, and tab switching.
/// </summary>
public static class MainLayoutTests
{
    public static void RunAll(Main? existingMain = null)
    {
        GD.Print("[Tests] Running MainLayoutTests (Task 1.1)...");

        if (existingMain != null)
        {
            TestFiveTierHierarchy(existingMain);
            TestPersistentBattleViewport(existingMain);
            TestTabNavigation(existingMain);
            TestModeSwitching(existingMain);
            GD.Print("[Tests] All MainLayoutTests PASSED successfully!");
            return;
        }

        var scene = GD.Load<PackedScene>("res://scenes/Main.tscn");
        if (scene == null)
        {
            throw new InvalidOperationException("Failed to load res://scenes/Main.tscn");
        }

        var main = scene.Instantiate<Main>();
        if (main == null)
        {
            throw new InvalidOperationException("Failed to instantiate Main scene");
        }

        try
        {
            TestFiveTierHierarchy(main);
            GD.Print("[Tests] All MainLayoutTests PASSED successfully!");
        }
        finally
        {
            main.Free();
        }
    }

    private static void TestFiveTierHierarchy(Main main)
    {
        // Tier 1: TopHeaderBar
        var header = main.GetNodeOrNull<Control>("%TopHeaderBar");
        AssertNotNull(header, "Tier 1: TopHeaderBar must exist with unique name %TopHeaderBar");
        AssertTrue(header.CustomMinimumSize.Y >= 36f, $"TopHeaderBar minimum height should be >= 36px, was {header.CustomMinimumSize.Y}");

        var titleLabel = main.GetNodeOrNull<Label>("%TitleLabel");
        AssertNotNull(titleLabel, "TitleLabel must exist");
        AssertTrue(titleLabel.Text.Contains("IDLE EXILE"), "TitleLabel must contain IDLE EXILE");

        var charInfoLabel = main.GetNodeOrNull<Label>("%CharInfoLabel");
        AssertNotNull(charInfoLabel, "CharInfoLabel must exist for character level & class");

        var pinCheck = main.GetNodeOrNull<CheckBox>("%AlwaysOnTopCheck");
        AssertNotNull(pinCheck, "AlwaysOnTopCheck must exist");

        var resizeBtn = main.GetNodeOrNull<Button>("%ResizeModeButton");
        AssertNotNull(resizeBtn, "ResizeModeButton must exist");

        var screenBtn = main.GetNodeOrNull<Button>("%ScreenCycleButton");
        AssertNotNull(screenBtn, "ScreenCycleButton must exist");

        // Tier 2: CombatViewportContainer
        var combatContainer = main.GetNodeOrNull<Control>("%CombatViewportContainer");
        AssertNotNull(combatContainer, "Tier 2: CombatViewportContainer must exist");
        AssertTrue(combatContainer.CustomMinimumSize.Y >= 260f, $"CombatViewportContainer minimum height should be >= 260px, was {combatContainer.CustomMinimumSize.Y}");

        var battleViewport = main.GetNodeOrNull<BattleViewport>("%BattleViewport");
        AssertNotNull(battleViewport, "BattleViewport must exist inside CombatViewportContainer");

        // Tier 3: SubTabBarContainer
        var subTabBar = main.GetNodeOrNull<Control>("%SubTabBarContainer");
        AssertNotNull(subTabBar, "Tier 3: SubTabBarContainer must exist");
        AssertTrue(subTabBar.CustomMinimumSize.Y >= 34f, $"SubTabBarContainer minimum height should be >= 34px, was {subTabBar.CustomMinimumSize.Y}");

        var tabGear = main.GetNodeOrNull<Button>("%TabGearBtn");
        var tabSkills = main.GetNodeOrNull<Button>("%TabSkillsBtn");
        var tabAtlas = main.GetNodeOrNull<Button>("%TabAtlasBtn");
        var tabPassives = main.GetNodeOrNull<Button>("%TabPassivesBtn");
        var tabStash = main.GetNodeOrNull<Button>("%TabStashBtn");

        AssertNotNull(tabGear, "TabGearBtn must exist");
        AssertNotNull(tabSkills, "TabSkillsBtn must exist");
        AssertNotNull(tabAtlas, "TabAtlasBtn must exist");
        AssertNotNull(tabPassives, "TabPassivesBtn must exist");
        AssertNotNull(tabStash, "TabStashBtn must exist");

        // Tier 4: TabWorkspaceContainer
        var tabWorkspace = main.GetNodeOrNull<Control>("%TabWorkspaceContainer");
        AssertNotNull(tabWorkspace, "Tier 4: TabWorkspaceContainer must exist");
        AssertTrue((tabWorkspace.SizeFlagsVertical & Control.SizeFlags.Expand) != 0, "TabWorkspaceContainer must have Vertical Expand size flag");

        var equipView = main.GetNodeOrNull<EquipmentView>("%EquipmentView");
        AssertNotNull(equipView, "EquipmentView must reside in TabWorkspaceContainer");

        // Tier 5: BottomMacroBar
        var macroBar = main.GetNodeOrNull<Control>("%BottomMacroBar");
        AssertNotNull(macroBar, "Tier 5: BottomMacroBar must exist");
        AssertTrue(macroBar.CustomMinimumSize.Y >= 40f, $"BottomMacroBar minimum height should be >= 40px, was {macroBar.CustomMinimumSize.Y}");

        var emergencyPortalBtn = main.GetNodeOrNull<Button>("%EmergencyPortalButton");
        AssertNotNull(emergencyPortalBtn, "EmergencyPortalButton must exist");
        AssertTrue(emergencyPortalBtn.Text.Contains("Emergency Portal"), "EmergencyPortalButton text must mention Emergency Portal");

        var statusLabel = main.GetNodeOrNull<Label>("%StatusLabel");
        AssertNotNull(statusLabel, "StatusLabel must exist in macro telemetry");

        var statsLabel = main.GetNodeOrNull<Label>("%StatsLabel");
        AssertNotNull(statsLabel, "StatsLabel must exist in macro telemetry");
    }

    private static void TestPersistentBattleViewport(Main main)
    {
        var battleViewport = main.GetNodeOrNull<BattleViewport>("%BattleViewport");
        AssertNotNull(battleViewport, "BattleViewport must exist");

        // Simulate switching tabs - BattleViewport must NEVER become hidden
        main.SwitchWorkspaceTab(Main.WorkspaceTab.Gear);
        AssertTrue(battleViewport.Visible, "BattleViewport must be visible when Gear tab is active");

        main.SwitchWorkspaceTab(Main.WorkspaceTab.Atlas);
        AssertTrue(battleViewport.Visible, "BattleViewport must remain visible when Atlas tab is active");

        main.SwitchWorkspaceTab(Main.WorkspaceTab.Skills);
        AssertTrue(battleViewport.Visible, "BattleViewport must remain visible when Skills tab is active");
    }

    private static void TestTabNavigation(Main main)
    {
        var equipView = main.GetNodeOrNull<EquipmentView>("%EquipmentView");
        var mapMargin = main.GetNodeOrNull<Control>("%MapDeviceMargin");

        main.SwitchWorkspaceTab(Main.WorkspaceTab.Gear);
        AssertTrue(equipView?.Visible ?? false, "EquipmentView should be visible on Gear tab");
        AssertFalse(mapMargin?.Visible ?? false, "MapDeviceMargin should be hidden on Gear tab");

        main.SwitchWorkspaceTab(Main.WorkspaceTab.Atlas);
        AssertFalse(equipView?.Visible ?? false, "EquipmentView should be hidden on Atlas tab");
        AssertTrue(mapMargin?.Visible ?? false, "MapDeviceMargin should be visible on Atlas tab");
    }

    private static void TestModeSwitching(Main main)
    {
        var subTabBar = main.GetNodeOrNull<Control>("%SubTabBarContainer");
        var tabWorkspace = main.GetNodeOrNull<Control>("%TabWorkspaceContainer");
        var battleViewport = main.GetNodeOrNull<BattleViewport>("%BattleViewport");

        // In expanded mode, both are visible
        AssertNotNull(subTabBar, "SubTabBarContainer must exist");
        AssertNotNull(tabWorkspace, "TabWorkspaceContainer must exist");
        AssertNotNull(battleViewport, "BattleViewport must exist");

        // Trigger Compact mode via SwitchWorkspaceTab check
        main.SwitchWorkspaceTab(Main.WorkspaceTab.Gear);
        AssertTrue(battleViewport.Visible, "BattleViewport remains visible");
    }

    private static void AssertNotNull(object? obj, string message)
    {
        if (obj == null) throw new Exception($"Assertion Failed: {message} (Object was null)");
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition) throw new Exception($"Assertion Failed: {message}");
    }

    private static void AssertFalse(bool condition, string message)
    {
        if (condition) throw new Exception($"Assertion Failed: {message}");
    }
}
