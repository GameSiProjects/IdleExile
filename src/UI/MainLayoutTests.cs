using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using IdleExile.Core.Models;
using IdleExile.Core.Simulation;
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
            TestStatusCardEliminationAndDiagnostics(existingMain);
            TestOptimizedEquipmentDollLayout(existingMain);
            TestStickyDrawerLayout(existingMain);
            TestActionButtonAndCandidateDrawerLayout(existingMain);
            TestGemViewIntegration(existingMain);
            TestAtlasViewIntegration(existingMain);
            TestPassiveTreeViewIntegration(existingMain);
            TestStashViewIntegration(existingMain);
            TestTabTransitionMicroAnimations(existingMain);
            TestEdgeGlowAlerts(existingMain);
            TestInWindowShortcuts(existingMain);
            TestCameraLookAheadAndTransformations(existingMain);
            TestDualStateViewportAndCombatDpsMeter(existingMain);
            TestSanctuaryViewIntegration(existingMain);
            TestArenaAndBattleViewportResetForNewMap(existingMain);
            TestBattleViewportSanctuaryHeader(existingMain);
            TestBattleViewportMapHeaderDuringMapping(existingMain);
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
            TestPersistentBattleViewport(main);
            TestTabNavigation(main);
            TestModeSwitching(main);
            TestStatusCardEliminationAndDiagnostics(main);
            TestOptimizedEquipmentDollLayout(main);
            TestStickyDrawerLayout(main);
            TestActionButtonAndCandidateDrawerLayout(main);
            TestGemViewIntegration(main);
            TestAtlasViewIntegration(main);
            TestPassiveTreeViewIntegration(main);
            TestStashViewIntegration(main);
            TestTabTransitionMicroAnimations(main);
            TestEdgeGlowAlerts(main);
            TestInWindowShortcuts(main);
            TestCameraLookAheadAndTransformations(main);
            TestDualStateViewportAndCombatDpsMeter(main);
            TestSanctuaryViewIntegration(main);
            TestArenaAndBattleViewportResetForNewMap(main);
            TestBattleViewportSanctuaryHeader(main);
            TestBattleViewportMapHeaderDuringMapping(main);
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

        var closeBtn = main.GetNodeOrNull<Button>("%CloseButton");
        AssertNotNull(closeBtn, "CloseButton must exist in TopHeaderBar");
        AssertEqual("✕", closeBtn.Text, "CloseButton text must be '✕'");

        // Tier 2: TabWorkspaceContainer (Workspace expands above Combat Viewport)
        var tabWorkspace = main.GetNodeOrNull<Control>("%TabWorkspaceContainer");
        AssertNotNull(tabWorkspace, "Tier 2: TabWorkspaceContainer must exist");
        AssertTrue((tabWorkspace.SizeFlagsVertical & Control.SizeFlags.Expand) != 0, "TabWorkspaceContainer must have Vertical Expand size flag");

        var equipView = main.GetNodeOrNull<EquipmentView>("%EquipmentView");
        AssertNotNull(equipView, "EquipmentView must reside in TabWorkspaceContainer");

        // Tier 3: SubTabBarContainer (Hinge between Workspace and Combat Viewport)
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

        // Tier 4: CombatViewportContainer (Bottom-Anchored Persistent Viewport)
        var combatContainer = main.GetNodeOrNull<Control>("%CombatViewportContainer");
        AssertNotNull(combatContainer, "Tier 4: CombatViewportContainer must exist");
        AssertTrue(combatContainer.CustomMinimumSize.Y >= 240f, $"CombatViewportContainer minimum height should be >= 240px, was {combatContainer.CustomMinimumSize.Y}");

        var battleViewport = main.GetNodeOrNull<BattleViewport>("%BattleViewport");
        AssertNotNull(battleViewport, "BattleViewport must exist inside CombatViewportContainer");

        // Tier 5: BottomMacroBar (Anchored flush to taskbar)
        var macroBar = main.GetNodeOrNull<Control>("%BottomMacroBar");
        AssertNotNull(macroBar, "Tier 5: BottomMacroBar must exist");
        AssertTrue(macroBar.CustomMinimumSize.Y >= 40f, $"BottomMacroBar minimum height should be >= 40px, was {macroBar.CustomMinimumSize.Y}");

        var emergencyPortalBtn = main.GetNodeOrNull<Button>("%EmergencyPortalButton");
        AssertNotNull(emergencyPortalBtn, "EmergencyPortalButton must exist");
        AssertTrue(emergencyPortalBtn.Text.Contains("Emergency Portal") || emergencyPortalBtn.Text.Contains("Launch Map"), "EmergencyPortalButton text must mention Emergency Portal or Launch Map");

        var statusLabel = main.GetNodeOrNull<Label>("%StatusLabel");
        AssertNotNull(statusLabel, "StatusLabel must exist in macro telemetry");

        var statsLabel = main.GetNodeOrNull<Label>("%StatsLabel");
        AssertNotNull(statsLabel, "StatsLabel must exist in macro telemetry");

        // Verify exact child hierarchy order in MainLayout: TopHeaderBar -> TabWorkspaceContainer -> SubTabBarContainer -> CombatViewportContainer -> BottomMacroBar
        var mainLayout = main.GetNodeOrNull<VBoxContainer>("MainLayout");
        if (mainLayout != null)
        {
            AssertEqual(5, mainLayout.GetChildCount(), "MainLayout must contain exactly 5 child tiers");
            AssertTrue(mainLayout.GetChild(0) == header, "MainLayout child 0 must be TopHeaderBar (Tier 1: 36px)");
            AssertTrue(mainLayout.GetChild(1) == tabWorkspace, "MainLayout child 1 must be TabWorkspaceContainer (Tier 2: Upward Workspace Expansion)");
            AssertTrue(mainLayout.GetChild(2) == subTabBar, "MainLayout child 2 must be SubTabBarContainer (Tier 3: 36px Tab Hinge)");
            AssertTrue(mainLayout.GetChild(3) == combatContainer, "MainLayout child 3 must be CombatViewportContainer (Tier 4: Bottom 248px Combat Viewport)");
            AssertTrue(mainLayout.GetChild(4) == macroBar, "MainLayout child 4 must be BottomMacroBar (Tier 5: 40px Taskbar Dock)");
        }
    }

    private static void TestPersistentBattleViewport(Main main)
    {
        var battleViewport = main.GetNodeOrNull<BattleViewport>("%BattleViewport");
        AssertNotNull(battleViewport, "BattleViewport must exist");

        // Simulate switching tabs across all 5 workspace tabs - BattleViewport must NEVER become hidden
        main.SwitchWorkspaceTab(Main.WorkspaceTab.Gear);
        AssertTrue(battleViewport.Visible, "BattleViewport must be visible when Gear tab is active");

        main.SwitchWorkspaceTab(Main.WorkspaceTab.Atlas);
        AssertTrue(battleViewport.Visible, "BattleViewport must remain visible when Atlas tab is active");

        main.SwitchWorkspaceTab(Main.WorkspaceTab.Skills);
        AssertTrue(battleViewport.Visible, "BattleViewport must remain visible when Skills tab is active");

        main.SwitchWorkspaceTab(Main.WorkspaceTab.Passives);
        AssertTrue(battleViewport.Visible, "BattleViewport must remain visible when Passives tab is active");

        main.SwitchWorkspaceTab(Main.WorkspaceTab.Stash);
        AssertTrue(battleViewport.Visible, "BattleViewport must remain visible when Stash tab is active");

        // Verify BattleViewport remains persistent in Compact Mode
        var windowController = main.WindowController;
        if (windowController != null)
        {
            windowController.ShrinkWindow(keepBottom: true);
            AssertTrue(battleViewport.Visible, "BattleViewport must remain persistently visible in Compact Mode (Core Objective #1)");
            windowController.ExpandWindow(keepBottom: true);
            AssertTrue(battleViewport.Visible, "BattleViewport must remain visible after returning to Expanded Mode");
        }
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

    /// <summary>
    /// Verifies Task 1.3: Window Mode Display Controller in Main.cs.
    /// Tests transitions between Compact Mode (640x320) and Expanded Mode (640x780):
    /// 0. Verifies Compact (640x320) and Expanded (640x780) size constants and keepBottom math invariance.
    /// 1. Initial State (Expanded): SubTabBarContainer and TabWorkspaceContainer visible.
    /// 2. Active Tab selection (Atlas) preserved in Expanded Mode.
    /// 3. Compact Mode: SubTabBarContainer and TabWorkspaceContainer hidden.
    /// 4. BattleViewport is persistent and NEVER hidden in any mode (Core Objective #1).
    /// 5. Resize button reflects correct state (⤡ Min vs ⤢ Expand).
    /// 6. Re-expansion restores the previous active tab without data loss.
    /// 7. Selecting a tab while in Compact Mode automatically expands the window upward.
    /// 8. Closing EquipmentView shrinks back to Compact Mode.
    /// </summary>
    private static void TestModeSwitching(Main main)
    {
        var subTabBar = main.SubTabBarContainer;
        var tabWorkspace = main.TabWorkspaceContainer;
        var battleViewport = main.BattleViewport;
        var resizeBtn = main.ResizeModeButton;
        var windowController = main.WindowController;

        AssertNotNull(subTabBar, "SubTabBarContainer must exist");
        AssertNotNull(tabWorkspace, "TabWorkspaceContainer must exist");
        AssertNotNull(battleViewport, "BattleViewport must exist");
        AssertNotNull(resizeBtn, "ResizeModeButton must exist");
        AssertNotNull(windowController, "WindowController must exist");

        // 0. Verify Window Size Constants and Resizable Restrictions
        AssertEqual(new Vector2I(640, 320), WindowController.CompactWindowSize, "CompactWindowSize must be 640x320");
        AssertEqual(new Vector2I(640, 780), WindowController.ExpandedWindowSize, "ExpandedWindowSize must be 640x780");
        AssertFalse(windowController.Resizable, "WindowController must NOT allow free/manual window resizing by default");
        AssertFalse(windowController.DesktopWindow.Resizable, "DesktopWindow must have resizable=false configured");
        AssertTrue(windowController.Borderless, "WindowController must have Borderless=true by default for companion overlay");
        if (DisplayServer.GetName() != "headless" && windowController.DesktopWindow.Window != null)
        {
            AssertTrue(windowController.DesktopWindow.Window.Unresizable, "Window must be marked Unresizable in Godot Window settings");
            AssertTrue(windowController.DesktopWindow.Window.Borderless, "Window must be marked Borderless in Godot Window settings");
        }

        // Verify keepBottom=true bottom edge invariance math:
        // Bottom edge = Y + Height. When expanding by deltaY, new Y = current Y - deltaY.
        // Therefore new bottom = (current Y - deltaY) + (current Height + deltaY) = current Y + current Height.
        int simulatedCurrentY = 600;
        int compactH = WindowController.CompactWindowSize.Y; // 320
        int expandedH = WindowController.ExpandedWindowSize.Y; // 780
        int deltaYExpand = expandedH - compactH; // 460
        int expandedY = simulatedCurrentY - deltaYExpand; // 140
        AssertEqual(simulatedCurrentY + compactH, expandedY + expandedH, "keepBottom expand must keep bottom coordinate invariant");

        int deltaYShrink = compactH - expandedH; // -460
        int restoredY = expandedY - deltaYShrink; // 600
        AssertEqual(expandedY + expandedH, restoredY + compactH, "keepBottom shrink must keep bottom coordinate invariant");

        // 1. Initial State should be Expanded Mode
        AssertTrue(windowController.IsExpanded, "Initial window state should be Expanded");
        AssertTrue(subTabBar.Visible, "SubTabBarContainer should be visible in Expanded mode");
        AssertTrue(tabWorkspace.Visible, "TabWorkspaceContainer should be visible in Expanded mode");
        AssertTrue(battleViewport.Visible, "BattleViewport must be visible in Expanded mode");
        AssertTrue(resizeBtn.Text.Contains("Min"), $"Resize button should indicate Min/Collapse when expanded, was: {resizeBtn.Text}");

        // 2. Select Atlas Tab in Expanded Mode
        main.SwitchWorkspaceTab(Main.WorkspaceTab.Atlas);
        AssertTrue(main.ActiveTab == Main.WorkspaceTab.Atlas, "ActiveTab should be Atlas");
        AssertTrue(main.MapDeviceMargin?.Visible ?? false, "MapDeviceMargin should be visible for Atlas tab");
        AssertFalse(main.EquipmentView?.Visible ?? false, "EquipmentView should be hidden for Atlas tab");
        AssertTrue(battleViewport.Visible, "BattleViewport remains visible");

        // 3. Switch to Compact Mode
        windowController.ShrinkWindow(keepBottom: true);
        AssertFalse(windowController.IsExpanded, "WindowController should report not expanded (Compact)");
        AssertFalse(subTabBar.Visible, "SubTabBarContainer must be hidden in Compact mode");
        AssertFalse(tabWorkspace.Visible, "TabWorkspaceContainer must be hidden in Compact mode");
        AssertTrue(battleViewport.Visible, "BattleViewport must remain PERSISTENTLY visible in Compact mode!");
        AssertTrue(resizeBtn.Text.Contains("Expand"), $"Resize button should indicate Expand when compact, was: {resizeBtn.Text}");
        AssertTrue(main.ActiveTab == Main.WorkspaceTab.Atlas, "ActiveTab should remain preserved as Atlas in memory");
        AssertEqual(Window.ContentScaleModeEnum.Disabled, main.GetWindow().ContentScaleMode, "ContentScaleMode must be Disabled to prevent shrinking in compact mode");
        AssertEqual(new Vector2(640, 248), battleViewport.CustomMinimumSize, "BattleViewport custom minimum size must remain 640x248 in Compact mode");
        AssertEqual(Vector2.One, battleViewport.Scale, "BattleViewport must never be scaled down (Scale must be 1.0)");

        // 4. Re-expand to Expanded Mode -> Verifies active tab restoration
        windowController.ExpandWindow(keepBottom: true);
        AssertTrue(windowController.IsExpanded, "WindowController should report expanded");
        AssertTrue(subTabBar.Visible, "SubTabBarContainer must be restored to visible");
        AssertTrue(tabWorkspace.Visible, "TabWorkspaceContainer must be restored to visible");
        AssertTrue(battleViewport.Visible, "BattleViewport must remain visible");
        AssertTrue(resizeBtn.Text.Contains("Min"), $"Resize button should indicate Min/Collapse when expanded, was: {resizeBtn.Text}");
        AssertTrue(main.ActiveTab == Main.WorkspaceTab.Atlas, "ActiveTab should still be Atlas");
        AssertTrue(main.MapDeviceMargin?.Visible ?? false, "MapDeviceMargin should be restored to visible because Atlas was active");
        AssertFalse(main.EquipmentView?.Visible ?? false, "EquipmentView should remain hidden");

        // 5. Test Auto-Expand when switching tab while in Compact Mode
        windowController.ShrinkWindow(keepBottom: true);
        AssertFalse(windowController.IsExpanded, "Should be in Compact mode");
        AssertFalse(subTabBar.Visible, "SubTabBarContainer hidden in Compact mode");
        AssertFalse(tabWorkspace.Visible, "TabWorkspaceContainer hidden in Compact mode");

        // Switching to Gear Tab while compact -> should auto-expand upward!
        main.SwitchWorkspaceTab(Main.WorkspaceTab.Gear);
        AssertTrue(windowController.IsExpanded, "SwitchWorkspaceTab should auto-expand window when called in Compact mode");
        AssertTrue(subTabBar.Visible, "SubTabBarContainer should be visible after auto-expand");
        AssertTrue(tabWorkspace.Visible, "TabWorkspaceContainer should be visible after auto-expand");
        AssertTrue(main.EquipmentView?.Visible ?? false, "EquipmentView should be visible for Gear tab");
        AssertFalse(main.MapDeviceMargin?.Visible ?? false, "MapDeviceMargin should be hidden for Gear tab");
        AssertTrue(battleViewport.Visible, "BattleViewport must remain visible");

        // 6. Test EquipmentView close button -> Shrinks back to Compact Mode
        main.EquipmentView?.RequestClose();
        AssertFalse(windowController.IsExpanded, "Closing EquipmentView should shrink window to Compact Mode");
        AssertFalse(subTabBar.Visible, "SubTabBarContainer hidden in Compact mode");
        AssertFalse(tabWorkspace.Visible, "TabWorkspaceContainer hidden in Compact mode");
        AssertTrue(battleViewport.Visible, "BattleViewport remains visible in Compact mode");

        // Clean up state: return to Expanded Mode for subsequent tests/operations
        windowController.ExpandWindow(keepBottom: true);
        AssertTrue(windowController.IsExpanded, "Restored to Expanded mode for subsequent operations");

        // 7. Verify unauthorized external OS resize and maximization is reverted
        var testWindow = windowController.DesktopWindow.Window;
        if (testWindow != null)
        {
            Vector2I sizeBefore = windowController.WindowSize;
            // Simulate external OS resize event triggering OnNativeWindowSizeChanged
            testWindow.Size = new Vector2I(800, 600);
            AssertEqual(sizeBefore, windowController.WindowSize, "WindowController.WindowSize must remain unchanged when free resizing is blocked");
            AssertEqual(sizeBefore, testWindow.Size, "Native window.Size must be reverted back to in-game WindowSize");

            // Simulate external attempt to maximize window (only if non-headless display server)
            if (DisplayServer.GetName() != "headless")
            {
                testWindow.Mode = Window.ModeEnum.Maximized;
                windowController.EnforceWindowConstraints(testWindow);
                AssertEqual(Window.ModeEnum.Windowed, testWindow.Mode, "Native window.Mode must be reverted to Windowed when maximization is blocked");
                AssertEqual(sizeBefore, testWindow.Size, "Window size must remain invariant when maximization is blocked");
            }
        }

        // 8. Verify dragging support on WindowController
        AssertFalse(windowController.IsDragging, "Window should not be dragging initially");
        windowController.StartDragging();
        AssertTrue(windowController.IsDragging, "StartDragging must mark WindowController.IsDragging as true");
        windowController.StopDragging();
        AssertFalse(windowController.IsDragging, "StopDragging must mark WindowController.IsDragging as false");
    }

    /// <summary>
    /// Verifies Task 1.2: Elimination of Debug Card (StatusCard).
    /// Asserts that no StatusCard or StatusCardMargin exists to steal vertical gameplay space.
    /// Confirms database metrics are cleanly routed to footer tooltips and diagnostic modals.
    /// </summary>
    private static void TestStatusCardEliminationAndDiagnostics(Main main)
    {
        // 1. Confirm complete elimination of legacy StatusCard and StatusCardMargin
        var statusCard = main.FindChild("StatusCard", recursive: true, owned: false);
        AssertTrue(statusCard == null, "StatusCard must be completely eliminated (no vertical space consumed)");

        var statusCardMargin = main.FindChild("StatusCardMargin", recursive: true, owned: false);
        AssertTrue(statusCardMargin == null, "StatusCardMargin must be completely eliminated from the scene hierarchy");

        // 2. Confirm gameplay/workspace owns 100% of expandable vertical space
        var tabWorkspace = main.GetNodeOrNull<Control>("%TabWorkspaceContainer");
        AssertNotNull(tabWorkspace, "TabWorkspaceContainer must exist");
        AssertTrue((tabWorkspace.SizeFlagsVertical & Control.SizeFlags.Expand) != 0, "TabWorkspaceContainer must expand vertically");

        // 3. Confirm footer telemetry contains database information via tooltip/compact label
        var statsLabel = main.GetNodeOrNull<Label>("%StatsLabel");
        AssertNotNull(statsLabel, "StatsLabel must exist in macro telemetry footer");

#if DEBUG
        if (OS.IsDebugBuild())
        {
            AssertTrue(!string.IsNullOrEmpty(statsLabel.TooltipText), "StatsLabel must provide database registry info via tooltip");
            AssertTrue(statsLabel.TooltipText.Contains("Database Registry"), "StatsLabel tooltip must describe Database Registry");

            // 4. Verify DiagnosticModal instantiation and database binding
            var modalScene = GD.Load<PackedScene>("res://scenes/DiagnosticModal.tscn");
            AssertNotNull(modalScene, "DiagnosticModal.tscn must exist and load cleanly");

            var modal = modalScene.Instantiate<DiagnosticModal>();
            AssertNotNull(modal, "DiagnosticModal must instantiate successfully");

            try
            {
                modal.Setup(main.Db, main.Player, main.Engine, null);
                var affixesLabel = modal.GetNodeOrNull<Label>("%AffixesCountLabel");
                AssertNotNull(affixesLabel, "DiagnosticModal must have %AffixesCountLabel");
                AssertTrue(affixesLabel.Text.Contains("affixes"), $"Affixes label should mention affixes, was {affixesLabel.Text}");
            }
            finally
            {
                modal.Free();
            }
        }
#endif
    }

    /// <summary>
    /// Verifies Task 2.1: Optimized 10-Slot Paper Doll and Side-by-Side Survival Benchmark Card.
    /// Conforms to docs/UI_OVERHAUL_IMPLEMENTATION_PLAN.md Phase 2:
    /// - Doll Card height collapsed to 170px (from 230px).
    /// - Row 1 (Helmet): 52x30px
    /// - Row 2 (MainHand, BodyArmour, OffHand): 52x30px
    /// - Row 3 (Gloves, Boots): 52x28px
    /// - Row 4 (Amulet, Ring 1, Ring 2, Belt): 46x26px
    /// - DollVBox has exactly 4 rows (Belt integrated into Row 4).
    /// - Survival Benchmark card placed side-by-side with DollCard inside TopSectionHBox (height 170px).
    /// </summary>
    private static void TestOptimizedEquipmentDollLayout(Main main)
    {
        var equipView = main.GetNodeOrNull<EquipmentView>("%EquipmentView");
        AssertNotNull(equipView, "EquipmentView must exist in TabWorkspaceContainer");

        var topSection = equipView.GetNodeOrNull<HBoxContainer>("MainPanel/RootMargin/MainVBox/TopSectionHBox");
        AssertNotNull(topSection, "TopSectionHBox must exist");
        AssertTrue(Mathf.IsEqualApprox(topSection.CustomMinimumSize.Y, 170f), $"TopSectionHBox height must be 170px, was {topSection.CustomMinimumSize.Y}");

        var dollCard = equipView.GetNodeOrNull<Control>("MainPanel/RootMargin/MainVBox/TopSectionHBox/DollCard");
        AssertNotNull(dollCard, "DollCard must exist in TopSectionHBox");
        AssertTrue(Mathf.IsEqualApprox(dollCard.CustomMinimumSize.Y, 170f), $"DollCard height must be 170px, was {dollCard.CustomMinimumSize.Y}");
        AssertTrue(Mathf.IsEqualApprox(dollCard.CustomMinimumSize.X, 210f), $"DollCard width must be 210px, was {dollCard.CustomMinimumSize.X}");

        var dollVBox = equipView.GetNodeOrNull<VBoxContainer>("MainPanel/RootMargin/MainVBox/TopSectionHBox/DollCard/DollMargin/DollVBox");
        AssertNotNull(dollVBox, "DollVBox must exist");
        AssertTrue(dollVBox.GetChildCount() == 4, $"DollVBox must contain exactly 4 rows (Belt integrated into Row 4), had {dollVBox.GetChildCount()}");

        // Row 1: Helmet 52x30
        var helmBtn = equipView.GetNodeOrNull<Button>("%SlotHelmet");
        AssertNotNull(helmBtn, "SlotHelmet button must exist");
        AssertTrue(Mathf.IsEqualApprox(helmBtn.CustomMinimumSize.X, 52f) && Mathf.IsEqualApprox(helmBtn.CustomMinimumSize.Y, 30f),
            $"Helmet button must be 52x30, was {helmBtn.CustomMinimumSize}");

        // Row 2: MainHand, Body, OffHand 52x30
        var mainHandBtn = equipView.GetNodeOrNull<Button>("%SlotMainHand");
        AssertNotNull(mainHandBtn, "SlotMainHand button must exist");
        AssertTrue(Mathf.IsEqualApprox(mainHandBtn.CustomMinimumSize.X, 52f) && Mathf.IsEqualApprox(mainHandBtn.CustomMinimumSize.Y, 30f),
            $"MainHand button must be 52x30, was {mainHandBtn.CustomMinimumSize}");

        var bodyBtn = equipView.GetNodeOrNull<Button>("%SlotBodyArmour");
        AssertNotNull(bodyBtn, "SlotBodyArmour button must exist");
        AssertTrue(Mathf.IsEqualApprox(bodyBtn.CustomMinimumSize.X, 52f) && Mathf.IsEqualApprox(bodyBtn.CustomMinimumSize.Y, 30f),
            $"BodyArmour button must be 52x30, was {bodyBtn.CustomMinimumSize}");

        var offHandBtn = equipView.GetNodeOrNull<Button>("%SlotOffHand");
        AssertNotNull(offHandBtn, "SlotOffHand button must exist");
        AssertTrue(Mathf.IsEqualApprox(offHandBtn.CustomMinimumSize.X, 52f) && Mathf.IsEqualApprox(offHandBtn.CustomMinimumSize.Y, 30f),
            $"OffHand button must be 52x30, was {offHandBtn.CustomMinimumSize}");

        // Row 3: Gloves, Boots 52x28
        var glovesBtn = equipView.GetNodeOrNull<Button>("%SlotGloves");
        AssertNotNull(glovesBtn, "SlotGloves button must exist");
        AssertTrue(Mathf.IsEqualApprox(glovesBtn.CustomMinimumSize.X, 52f) && Mathf.IsEqualApprox(glovesBtn.CustomMinimumSize.Y, 28f),
            $"Gloves button must be 52x28, was {glovesBtn.CustomMinimumSize}");

        var bootsBtn = equipView.GetNodeOrNull<Button>("%SlotBoots");
        AssertNotNull(bootsBtn, "SlotBoots button must exist");
        AssertTrue(Mathf.IsEqualApprox(bootsBtn.CustomMinimumSize.X, 52f) && Mathf.IsEqualApprox(bootsBtn.CustomMinimumSize.Y, 28f),
            $"Boots button must be 52x28, was {bootsBtn.CustomMinimumSize}");

        // Row 4: Amulet, Ring 1, Ring 2, Belt 46x26
        var amuBtn = equipView.GetNodeOrNull<Button>("%SlotAmulet");
        AssertNotNull(amuBtn, "SlotAmulet button must exist");
        AssertTrue(Mathf.IsEqualApprox(amuBtn.CustomMinimumSize.X, 46f) && Mathf.IsEqualApprox(amuBtn.CustomMinimumSize.Y, 26f),
            $"Amulet button must be 46x26, was {amuBtn.CustomMinimumSize}");

        var ring1Btn = equipView.GetNodeOrNull<Button>("%SlotRing1");
        AssertNotNull(ring1Btn, "SlotRing1 button must exist");
        AssertTrue(Mathf.IsEqualApprox(ring1Btn.CustomMinimumSize.X, 46f) && Mathf.IsEqualApprox(ring1Btn.CustomMinimumSize.Y, 26f),
            $"Ring1 button must be 46x26, was {ring1Btn.CustomMinimumSize}");

        var ring2Btn = equipView.GetNodeOrNull<Button>("%SlotRing2");
        AssertNotNull(ring2Btn, "SlotRing2 button must exist");
        AssertTrue(Mathf.IsEqualApprox(ring2Btn.CustomMinimumSize.X, 46f) && Mathf.IsEqualApprox(ring2Btn.CustomMinimumSize.Y, 26f),
            $"Ring2 button must be 46x26, was {ring2Btn.CustomMinimumSize}");

        var beltBtn = equipView.GetNodeOrNull<Button>("%SlotBelt");
        AssertNotNull(beltBtn, "SlotBelt button must exist");
        AssertTrue(Mathf.IsEqualApprox(beltBtn.CustomMinimumSize.X, 46f) && Mathf.IsEqualApprox(beltBtn.CustomMinimumSize.Y, 26f),
            $"Belt button must be 46x26, was {beltBtn.CustomMinimumSize}");

        // Verify Belt is a sibling in Row 4
        var row4 = dollVBox.GetChild<HBoxContainer>(3);
        AssertNotNull(row4, "Row 4 must be an HBoxContainer");
        AssertTrue(row4.GetChildCount() == 4, $"Row 4 must contain 4 accessory slots (Amulet, Ring 1, Ring 2, Belt), had {row4.GetChildCount()}");

        // Verify Survival Benchmark Card
        var benchmarkCard = equipView.GetNodeOrNull<Control>("MainPanel/RootMargin/MainVBox/TopSectionHBox/BenchmarkCard");
        AssertNotNull(benchmarkCard, "BenchmarkCard must exist in TopSectionHBox side-by-side with DollCard");
        AssertTrue(Mathf.IsEqualApprox(benchmarkCard.CustomMinimumSize.Y, 170f), $"BenchmarkCard height must be 170px, was {benchmarkCard.CustomMinimumSize.Y}");

        var ehpBenchmark = equipView.GetNodeOrNull<Label>("%EhpBenchmarkLabel");
        AssertNotNull(ehpBenchmark, "EhpBenchmarkLabel must exist in BenchmarkCard");

        var dpsBenchmark = equipView.GetNodeOrNull<Label>("%TotalDpsBenchmarkLabel");
        AssertNotNull(dpsBenchmark, "TotalDpsBenchmarkLabel must exist in BenchmarkCard");

        var strBenchmark = equipView.GetNodeOrNull<Label>("%StrStatLabel");
        AssertNotNull(strBenchmark, "StrStatLabel must exist in BenchmarkCard");
    }

    /// <summary>
    /// Verifies Task 2.2: Smart Tooltip Delta Sticky Drawer (140px).
    /// Conforms to docs/UI_OVERHAUL_IMPLEMENTATION_PLAN.md:
    /// - Sticky Drawer positioned right under the Doll with fixed height 140px.
    /// - Clip contents enabled to prevent layout expansion.
    /// - 2 decisive headline rows: ▲/▼ EHP and ▲/▼ DPS with font size 14.
    /// - DeltaVerdictLabel shows instant verdict badge (▲ UPGRADE, ▼ DOWNGRADE, etc.).
    /// - AffixFilterToggle button toggles between full list and T1/T2 summary.
    /// - Smooth wheel scrolling capability in AffixScroll.
    /// - Live comparison updates when inspecting candidate items.
    /// </summary>
    private static void TestStickyDrawerLayout(Main main)
    {
        var equipView = main.GetNodeOrNull<EquipmentView>("%EquipmentView");
        AssertNotNull(equipView, "EquipmentView must exist");

        // 1. Sticky Drawer structure and 140px budget
        var drawer = equipView.GetNodeOrNull<Control>("%SmartDeltaDrawer");
        AssertNotNull(drawer, "SmartDeltaDrawer must exist with unique name %SmartDeltaDrawer");
        AssertTrue(Mathf.IsEqualApprox(drawer.CustomMinimumSize.Y, EquipmentView.StickyDrawerHeight),
            $"SmartDeltaDrawer height must be {EquipmentView.StickyDrawerHeight}px, was {drawer.CustomMinimumSize.Y}");
        AssertTrue(drawer.ClipContents, "SmartDeltaDrawer must have ClipContents enabled to maintain 140px budget");

        // 2. Decisive metrics (EHP & DPS) labels with large font size (14)
        var ehpLabel = equipView.GetNodeOrNull<Label>("%DeltaEhpLabel");
        AssertNotNull(ehpLabel, "DeltaEhpLabel must exist in drawer");
        int ehpFontSize = ehpLabel.GetThemeFontSize("font_size");
        AssertTrue(ehpFontSize >= 12, $"DeltaEhpLabel font size should be large (>=12, configured 14), was {ehpFontSize}");

        var dpsLabel = equipView.GetNodeOrNull<Label>("%DeltaDpsLabel");
        AssertNotNull(dpsLabel, "DeltaDpsLabel must exist in drawer");
        int dpsFontSize = dpsLabel.GetThemeFontSize("font_size");
        AssertTrue(dpsFontSize >= 12, $"DeltaDpsLabel font size should be large (>=12, configured 14), was {dpsFontSize}");

        // 3. Verdict badge and summary labels
        var verdictLabel = equipView.GetNodeOrNull<Label>("%DeltaVerdictLabel");
        AssertNotNull(verdictLabel, "DeltaVerdictLabel must exist for quick verdict badge");

        var summaryLabel = equipView.GetNodeOrNull<Label>("%DeltaSummaryLabel");
        AssertNotNull(summaryLabel, "DeltaSummaryLabel must exist for resistance summary");

        // 4. Affix column: header, toggle button, scroll container
        var affixHeader = equipView.GetNodeOrNull<Label>("%AffixHeaderLabel");
        AssertNotNull(affixHeader, "AffixHeaderLabel must exist");

        var filterToggle = equipView.GetNodeOrNull<Button>("%AffixFilterToggle");
        AssertNotNull(filterToggle, "AffixFilterToggle button must exist");

        var affixScroll = equipView.GetNodeOrNull<ScrollContainer>("%AffixScroll");
        AssertNotNull(affixScroll, "AffixScroll must exist");

        // 5. Test candidate inspection and Delta update
        if (equipView.CandidateItems.Count > 0)
        {
            var candidate = equipView.CandidateItems[0];
            equipView.InspectCandidate(candidate);

            AssertTrue(equipView.InspectedItem?.InstanceId == candidate.InstanceId, "InspectedItem should match candidate");
            AssertTrue(!string.IsNullOrEmpty(ehpLabel.Text), "DeltaEhpLabel must be populated");
            AssertTrue(!string.IsNullOrEmpty(dpsLabel.Text), "DeltaDpsLabel must be populated");
            AssertTrue(!string.IsNullOrEmpty(verdictLabel.Text), "DeltaVerdictLabel must be populated");

            // 6. Test T1/T2 affix filter toggle
            equipView.SetHighTierAffixFilter(true);
            AssertTrue(equipView.ShowHighTierAffixesOnly, "ShowHighTierAffixesOnly should be true");

            var affixList = equipView.GetNodeOrNull<VBoxContainer>("%TooltipAffixListVBox");
            AssertNotNull(affixList, "TooltipAffixListVBox must exist");

            equipView.SetHighTierAffixFilter(false);
            AssertFalse(equipView.ShowHighTierAffixesOnly, "ShowHighTierAffixesOnly should be false");
        }

        // 7. Test smooth scroll helper
        equipView.SmoothScrollAffixesBy(50f);
    }

    /// <summary>
    /// Verifies Task 2.3: Action Buttons & Candidate Drawer Mini (60px).
    /// Conforms to docs/UI_OVERHAUL_IMPLEMENTATION_PLAN.md:
    /// - ActionCandidateSection has fixed height budget 60px with ClipContents enabled.
    /// - 1-Click Action Buttons: [Equip] (Green), [Lock] (Amber), [Stash] (Blue).
    /// - Candidate Drawer provides horizontal smooth scrolling for items matching selected slot.
    /// - Fast 1.5s comparison candidate cards with dynamic EHP/DPS delta badges.
    /// - 1-click candidate inspection and 1-click / double-click equip actions.
    /// </summary>
    private static void TestActionButtonAndCandidateDrawerLayout(Main main)
    {
        var equipView = main.GetNodeOrNull<EquipmentView>("%EquipmentView");
        AssertNotNull(equipView, "EquipmentView must exist");

        // 1. 60px height constraint & clipping
        var section = equipView.GetNodeOrNull<Control>("%ActionCandidateSection");
        AssertNotNull(section, "ActionCandidateSection must exist with unique name %ActionCandidateSection");
        AssertTrue(Mathf.IsEqualApprox(section.CustomMinimumSize.Y, EquipmentView.ActionCandidateSectionHeight),
            $"ActionCandidateSection height must be {EquipmentView.ActionCandidateSectionHeight}px, was {section.CustomMinimumSize.Y}");
        AssertTrue(section.ClipContents, "ActionCandidateSection must clip contents to preserve 60px budget");

        // 2. Action Buttons existence
        var equipBtn = equipView.GetNodeOrNull<Button>("%EquipButton");
        AssertNotNull(equipBtn, "EquipButton must exist");

        var lockBtn = equipView.GetNodeOrNull<Button>("%LockButton");
        AssertNotNull(lockBtn, "LockButton must exist");

        var stashBtn = equipView.GetNodeOrNull<Button>("%StashButton");
        AssertNotNull(stashBtn, "StashButton must exist");

        // 3. Candidate Drawer Panel & ScrollContainer
        var drawerPanel = equipView.GetNodeOrNull<PanelContainer>("%CandidateDrawerPanel");
        AssertNotNull(drawerPanel, "CandidateDrawerPanel must exist");

        var candidateScroll = equipView.GetNodeOrNull<ScrollContainer>("%CandidateScroll");
        AssertNotNull(candidateScroll, "CandidateScroll must exist");
        AssertTrue(candidateScroll.ClipContents, "CandidateScroll must have ClipContents enabled");

        var candidateHBox = equipView.GetNodeOrNull<HBoxContainer>("%CandidateHBox");
        AssertNotNull(candidateHBox, "CandidateHBox must exist");

        // 4. Smooth horizontal scroll helpers
        equipView.SmoothScrollCandidatesBy(45f);
        equipView.ResetCandidateScroll();

        // 5. Candidate items card rendering and fast 1.5s comparison
        equipView.SelectSlot(GearSlot.BodyArmour);
        var matchingCandidates = equipView.CandidateItems
            .Where(i => i.TargetSlot == GearSlot.BodyArmour)
            .ToList();

        if (matchingCandidates.Count > 0)
        {
            var candidate = matchingCandidates[0];

            // Inspect candidate (1-click)
            equipView.InspectCandidate(candidate);
            AssertTrue(equipView.InspectedItem?.InstanceId == candidate.InstanceId,
                "Inspecting candidate must update InspectedItem");
            AssertFalse(equipBtn.Disabled, "EquipButton should be enabled when candidate is inspected");
            AssertTrue(equipBtn.Text == "Equip", "EquipButton text should say 'Equip'");

            // 1-Click Lock Toggle
            bool initialLock = candidate.IsLocked;
            lockBtn.EmitSignal(Button.SignalName.Pressed);
            AssertTrue(candidate.IsLocked != initialLock, "Clicking LockButton must toggle item lock state");
            AssertTrue(stashBtn.Disabled == candidate.IsLocked, "Locked candidate item must disable StashButton");

            // Toggle lock back to initial state
            lockBtn.EmitSignal(Button.SignalName.Pressed);
            AssertTrue(candidate.IsLocked == initialLock, "Toggling lock again must restore initial lock state");

            // 1-Click Equip
            equipBtn.EmitSignal(Button.SignalName.Pressed);
            AssertTrue(main.Player?.Gear.GetItem(GearSlot.BodyArmour)?.InstanceId == candidate.InstanceId,
                "EquipButton must equip the inspected candidate item into player gear");
            AssertTrue(equipBtn.Disabled, "EquipButton must be disabled when item is equipped");
            AssertTrue(equipBtn.Text == "Equipped", "EquipButton text must say 'Equipped'");
            AssertFalse(stashBtn.Disabled, "StashButton must be enabled for equipped item");
            AssertTrue(stashBtn.Text == "Unequip", "StashButton text must say 'Unequip' for equipped item");

            // 1-Click Unequip / Stash
            stashBtn.EmitSignal(Button.SignalName.Pressed);
            AssertTrue(main.Player?.Gear.GetItem(GearSlot.BodyArmour) == null,
                "StashButton must unequip the active item when clicked");
            AssertTrue(equipView.CandidateItems.Any(i => i.InstanceId == candidate.InstanceId),
                "Unequipped item must be returned to candidate items list");
        }
    }

    private static void TestGemViewIntegration(Main main)
    {
        GD.Print("[Tests] Verifying Task 3.1: GemView node hierarchy, layout, and tab integration...");

        // 1. Verify GemView exists in TabWorkspaceContainer
        var gemView = main.GetNodeOrNull<GemView>("%GemView");
        AssertNotNull(gemView, "GemView must exist inside TabWorkspaceContainer with unique name %GemView");
        AssertTrue(gemView.CustomMinimumSize.Y >= 390f, "GemView minimum height should be >= 390px");

        // 2. Verify Key Nodes within GemView
        var slotPrimary = gemView.GetNodeOrNull<Button>("%SlotPrimaryBtn");
        AssertNotNull(slotPrimary, "GemView must contain %SlotPrimaryBtn");

        var activeSocket = gemView.GetNodeOrNull<Button>("%ActiveGemSocket");
        AssertNotNull(activeSocket, "GemView must contain %ActiveGemSocket");

        var ringCanvas = gemView.GetNodeOrNull<SocketRingCanvas>("%SocketRingCanvas");
        AssertNotNull(ringCanvas, "GemView must contain %SocketRingCanvas for drawing auto-link conduits");

        for (int i = 0; i < 5; i++)
        {
            var supportSocket = gemView.GetNodeOrNull<Button>($"%SupportSocket{i}");
            AssertNotNull(supportSocket, $"GemView must contain %SupportSocket{i}");
        }

        var metricsBar = gemView.GetNodeOrNull<PanelContainer>("%GemMetricsBar");
        AssertNotNull(metricsBar, "GemView must contain %GemMetricsBar");

        var inspector = gemView.GetNodeOrNull<PanelContainer>("%GemInspectorDrawer");
        AssertNotNull(inspector, "GemView must contain %GemInspectorDrawer");

        var candidateScroll = gemView.GetNodeOrNull<ScrollContainer>("%CandidateScroll");
        AssertNotNull(candidateScroll, "GemView must contain %CandidateScroll");

        var socketActionBtn = gemView.GetNodeOrNull<Button>("%SocketActionBtn");
        AssertNotNull(socketActionBtn, "GemView must contain %SocketActionBtn");

        var unsocketActionBtn = gemView.GetNodeOrNull<Button>("%UnsocketActionBtn");
        AssertNotNull(unsocketActionBtn, "GemView must contain %UnsocketActionBtn");

        // 3. Verify Tab Switching & Persistent Combat Viewport
        main.SwitchWorkspaceTab(Main.WorkspaceTab.Skills);
        AssertTrue(gemView.Visible, "GemView must be visible when Skills tab is active");
        AssertFalse(main.EquipmentView?.Visible ?? true, "EquipmentView must be hidden when Skills tab is active");
        AssertTrue(main.BattleViewport?.Visible ?? false, "BattleViewport MUST remain persistent and visible!");

        // Switch back to Gear tab
        main.SwitchWorkspaceTab(Main.WorkspaceTab.Gear);
        AssertFalse(gemView.Visible, "GemView must be hidden when Gear tab is active");
        AssertTrue(main.EquipmentView?.Visible ?? false, "EquipmentView must be visible when Gear tab is active");
        AssertTrue(main.BattleViewport?.Visible ?? false, "BattleViewport MUST remain persistent and visible!");

        // 4. Verify Interactive Socketing on GemView
        main.SwitchWorkspaceTab(Main.WorkspaceTab.Skills);
        gemView.SelectSkillSlot(0);

        // Active Gem Socket test
        gemView.SelectSocket(-1);
        AssertTrue(gemView.SelectedSocketIndex == -1, "SelectedSocketIndex should be -1 for Active socket");

        var fireball = new GemDefinition(
            Id: "fireball_test",
            Name: "Fireball Test",
            Category: "Active",
            Color: "Blue",
            Tags: new[] { "spell", "projectile", "fire" },
            RequiredTags: null,
            BaseStats: new Dictionary<string, object>
            {
                ["manaCost"] = 12f,
                ["damageMultiplier"] = 100f
            },
            Modifiers: null,
            Description: "Test Fireball"
        );

        bool socketed = gemView.SocketGem(fireball);
        AssertTrue(socketed, "SocketGem must socket Active Gem");
        AssertEqual("Fireball Test", gemView.CurrentSetup.ActiveGem?.Name);

        // Support Gem Socket test
        var supportGem = new GemDefinition(
            Id: "sup_test",
            Name: "Test Support",
            Category: "Support",
            Color: "Green",
            Tags: null,
            RequiredTags: new[] { "projectile" },
            BaseStats: null,
            Modifiers: new Dictionary<string, object>
            {
                ["damageMultiplier"] = 25f,
                ["manaMultiplier"] = 120f
            },
            Description: "Test Support"
        );

        gemView.SelectSocket(0);
        bool supportSocketed = gemView.SocketGem(supportGem, 0);
        AssertTrue(supportSocketed, "SocketGem must socket Support Gem into slot 0");
        AssertEqual(1, gemView.CurrentSetup.SocketedSupports.Count);
        AssertTrue(gemView.CurrentSetup.IsSupportCompatible(supportGem), "Test Support must be compatible with Fireball");

        // Unsocket Support Gem
        bool unsocketed = gemView.UnsocketGem(0);
        AssertTrue(unsocketed, "UnsocketGem must remove support gem at slot 0");
        AssertEqual(0, gemView.CurrentSetup.SocketedSupports.Count);

        // 5. Task 3.2: Gem Tag Matching Validator Interactive Hover & Multiplier Tests
        var meleePhys = new GemDefinition(
            Id: "melee_phys_test",
            Name: "Melee Phys Test",
            Category: "Support",
            Color: "Red",
            Tags: null,
            RequiredTags: new[] { "melee", "physical", "attack" },
            BaseStats: null,
            Modifiers: new Dictionary<string, object> { ["morePhysicalDamagePercent"] = 35f },
            Description: "Melee Phys Test"
        );

        // Fireball is active: supportGem (projectile) is COMPATIBLE
        gemView.SetHoveredGemForTesting(supportGem);
        AssertTrue(gemView.IsActiveSocketHighlighted, "Active socket (Fireball) must light up when compatible Support (projectile) is hovered");
        AssertFalse(gemView.IsActiveSocketDimmed, "Active socket must not be dimmed when compatible Support is hovered");

        // Fireball is active: meleePhys (melee) is INCOMPATIBLE
        gemView.SetHoveredGemForTesting(meleePhys);
        AssertFalse(gemView.IsActiveSocketHighlighted, "Active socket (Fireball) must NOT light up when incompatible Support (melee) is hovered");
        AssertTrue(gemView.IsActiveSocketDimmed, "Active socket must be dimmed when incompatible Support is hovered");

        // Unhover: neutral state restored
        gemView.SetHoveredGemForTesting(null);
        AssertFalse(gemView.IsActiveSocketHighlighted, "Active socket highlight must clear when unhovered");
        AssertFalse(gemView.IsActiveSocketDimmed, "Active socket dimming must clear when unhovered");

        // Multi-slot highlight test: Slot 0 = Cleave, Slot 1 = Fireball
        var cleave = new GemDefinition("cleave_test", "Cleave Test", "Active", "Red", new[] { "attack", "melee", "aoe", "physical" }, null, null, null, "Cleave");
        main.Loadout.GetSetupAt(0).SetActiveGem(cleave);
        main.Loadout.GetSetupAt(1).SetActiveGem(fireball);

        gemView.SetHoveredGemForTesting(meleePhys);
        AssertTrue(gemView.IsSlotButtonHighlighted(0), "Slot 0 tab (Cleave) must be highlighted when Melee Phys is hovered");
        AssertFalse(gemView.IsSlotButtonHighlighted(1), "Slot 1 tab (Fireball) must NOT be highlighted when Melee Phys is hovered");

        gemView.SetHoveredGemForTesting(null);

        // Verify MetricDamageMultLabel displays compounded product ∏(1+More)
        var metricLabel = gemView.GetNodeOrNull<Label>("%MetricDamageMultLabel");
        AssertNotNull(metricLabel, "MetricDamageMultLabel must exist");
        AssertTrue(metricLabel.Text.Contains("∏"), $"MetricDamageMultLabel must display product symbol ∏, was '{metricLabel.Text}'");
    }

    private static void TestAtlasViewIntegration(Main main)
    {
        GD.Print("[Tests] Verifying Task 4.1: AtlasView node hierarchy, layout, and map activation...");

        var atlasView = main.AtlasView;
        AssertNotNull(atlasView, "AtlasView must be instantiated in Main.tscn");

        // 1. Verify Node Hierarchy
        var tierFilterBar = atlasView.GetNodeOrNull<Control>("%TierFilterHBox") ?? atlasView.GetNodeOrNull<Control>("%TierAllBtn")?.GetParent<Control>();
        AssertNotNull(tierFilterBar, "TierFilterHBox must exist in AtlasView");

        var runicCanvas = atlasView.GetNodeOrNull<RunicDeviceCanvas>("%RunicDeviceCanvas");
        AssertNotNull(runicCanvas, "RunicDeviceCanvas must exist in AtlasView for ancient runic geometry");

        var slottedTierBadge = atlasView.GetNodeOrNull<Label>("%SlottedTierBadge");
        AssertNotNull(slottedTierBadge, "SlottedTierBadge must exist");

        var slottedMapName = atlasView.GetNodeOrNull<Label>("%SlottedMapNameLabel");
        AssertNotNull(slottedMapName, "SlottedMapNameLabel must exist");

        var activateBtn = atlasView.GetNodeOrNull<Button>("%ActivateMapBtn");
        AssertNotNull(activateBtn, "ActivateMapBtn must exist");

        var emergencyBtn = atlasView.GetNodeOrNull<Button>("%EmergencyPortalBtn");
        AssertNotNull(emergencyBtn, "EmergencyPortalBtn must exist");

        var qtyLabel = atlasView.GetNodeOrNull<Label>("%QtyValueLabel");
        AssertNotNull(qtyLabel, "QtyValueLabel must exist");

        var threatBadges = atlasView.GetNodeOrNull<HBoxContainer>("%ThreatBadgesHBox");
        AssertNotNull(threatBadges, "ThreatBadgesHBox must exist");

        var mapSlatesHBox = atlasView.GetNodeOrNull<HBoxContainer>("%MapSlatesHBox");
        AssertNotNull(mapSlatesHBox, "MapSlatesHBox must exist in Available Maps drawer");

        // 2. Tab Navigation to Atlas
        main.SwitchWorkspaceTab(Main.WorkspaceTab.Atlas);
        AssertTrue(main.MapDeviceMargin?.Visible ?? false, "MapDeviceMargin must be visible on Atlas tab");
        AssertTrue(atlasView.Visible, "AtlasView must be visible on Atlas tab");

        // 3. Test Slotted Map and Modifiers Breakdown
        var testMap = new MapDefinition("strand_test", "Strand Test", 1, 1, false, 10, 100, "Tidal Behemoth", "Coast");
        var modDef = new MapModifierDefinition("test_reflect", "Monsters reflect {0}% Elemental Damage", 40, 15, 15f, 20f);
        var rolledMods = new List<RolledMapModifier> { new(modDef, 18f) };

        atlasView.SlotMap(testMap, rolledMods, "Rare");
        AssertEqual("strand_test", atlasView.SlottedMap?.Id, "Slotted map ID mismatch");
        AssertEqual("T1", slottedTierBadge.Text, "Tier badge text mismatch");
        AssertEqual("Strand Test", slottedMapName.Text, "Slotted map name mismatch");
        AssertTrue(qtyLabel.Text.Contains("+50%"), $"Total Quantity must be Base 10 + Bonus 40 = 50%, was '{qtyLabel.Text}'");

        // Threat badge must be displayed for elemental reflect
        AssertTrue(threatBadges.GetChildCount() > 0, "ThreatBadgesHBox must display badges for dangerous affixes");

        // 4. Test Normal Map Slate (0 Modifiers)
        atlasView.SlotMap(testMap, Array.Empty<RolledMapModifier>(), "Normal");
        var noModsLabel = atlasView.GetNodeOrNull<Label>("%NoModsLabel");
        AssertNotNull(noModsLabel, "NoModsLabel must exist");
        AssertTrue(noModsLabel.Visible, "NoModsLabel must be visible for Normal Map with 0 modifiers");

        // 5. Test Tier Filter Switching
        atlasView.SetTierFilter("T0");
        AssertEqual("T0", atlasView.CurrentTierFilter, "CurrentTierFilter mismatch");

        atlasView.SetTierFilter("White");
        AssertEqual("White", atlasView.CurrentTierFilter, "CurrentTierFilter mismatch");

        atlasView.SetTierFilter("All");
        AssertEqual("All", atlasView.CurrentTierFilter, "CurrentTierFilter mismatch");

        // 6. Test Golden Pulse
        atlasView.SetGoldenPulse(true);
        AssertTrue(atlasView.IsGoldenPulse, "IsGoldenPulse should be true");
        AssertTrue(runicCanvas.IsGoldenPulse, "RunicDeviceCanvas.IsGoldenPulse should be true");

        atlasView.SetGoldenPulse(false);
        AssertFalse(atlasView.IsGoldenPulse, "IsGoldenPulse should be false");
        AssertFalse(runicCanvas.IsGoldenPulse, "RunicDeviceCanvas.IsGoldenPulse should be false");

        // 7. Test Portal Activation & Emergency Portal Trigger
        atlasView.SetPortalActive(false);
        bool mapActivatedFired = false;
        atlasView.OnMapActivated += (m, mods, r) => mapActivatedFired = true;

        activateBtn.EmitSignal(Button.SignalName.Pressed);
        AssertTrue(mapActivatedFired, "OnMapActivated event must fire when Activate button is pressed");
        AssertTrue(atlasView.IsPortalActive, "AtlasView.IsPortalActive must be true after activation");
        AssertTrue(runicCanvas.IsPortalActive, "RunicDeviceCanvas.IsPortalActive must be true after activation");
        AssertTrue(activateBtn.Disabled, "Activate button must be disabled when portal is active");
        AssertFalse(emergencyBtn.Disabled, "Emergency portal button must be enabled when portal is active");

        bool emergencyFired = false;
        atlasView.OnEmergencyPortalTriggered += () => emergencyFired = true;
        emergencyBtn.EmitSignal(Button.SignalName.Pressed);
        AssertTrue(emergencyFired, "OnEmergencyPortalTriggered event must fire when Emergency button is pressed");
        AssertFalse(atlasView.IsPortalActive, "IsPortalActive must be false after emergency retreat");

        // 8. Test Task 4.2: Map Queue UI Integration (10 Slots & Automation)
        GD.Print("[Tests] Verifying Task 4.2: Map Queue 10-slot tray, countdown UI, and controls...");
        var queuePanel = atlasView.GetNodeOrNull<PanelContainer>("%MapQueuePanel");
        AssertNotNull(queuePanel, "MapQueuePanel must exist in AtlasView");

        var queueTitle = atlasView.GetNodeOrNull<Label>("%QueueTitleLabel");
        AssertNotNull(queueTitle, "QueueTitleLabel must exist");
        AssertTrue(queueTitle.Text.Contains("MAP QUEUE"), $"QueueTitleLabel must contain 'MAP QUEUE', was '{queueTitle.Text}'");

        var queueAutoAdvance = atlasView.GetNodeOrNull<CheckBox>("%QueueAutoAdvanceCheck");
        AssertNotNull(queueAutoAdvance, "QueueAutoAdvanceCheck must exist");
        AssertTrue(queueAutoAdvance.ButtonPressed, "QueueAutoAdvanceCheck must be enabled by default");

        var queueContinuous = atlasView.GetNodeOrNull<CheckBox>("%QueueContinuousCheck");
        AssertNotNull(queueContinuous, "QueueContinuousCheck must exist");

        var queueEnqueueBtn = atlasView.GetNodeOrNull<Button>("%QueueEnqueueBtn");
        AssertNotNull(queueEnqueueBtn, "QueueEnqueueBtn must exist");

        var queueClearBtn = atlasView.GetNodeOrNull<Button>("%QueueClearBtn");
        AssertNotNull(queueClearBtn, "QueueClearBtn must exist");

        var queueSlotsHBox = atlasView.GetNodeOrNull<HBoxContainer>("%QueueSlotsHBox");
        AssertNotNull(queueSlotsHBox, "QueueSlotsHBox must exist");
        AssertEqual(10, queueSlotsHBox.GetChildCount(), "QueueSlotsHBox must contain exactly 10 slot buttons");

        // Enqueue current map
        atlasView.SlotMap(testMap, rolledMods, "Rare");
        queueEnqueueBtn.EmitSignal(Button.SignalName.Pressed);
        AssertEqual(1, atlasView.QueueManager?.Count, "Queue count must be 1 after enqueuing slotted map");
        AssertTrue(queueTitle.Text.Contains("1/10"), $"QueueTitleLabel must update to 1/10, was '{queueTitle.Text}'");

        // Slot 0 should reflect Tier 1
        var slot0Btn = queueSlotsHBox.GetChild<Button>(0);
        var slot0TopLabel = slot0Btn.GetNodeOrNull<Label>("VBoxContainer/TopLabel") ?? slot0Btn.FindChild("TopLabel", recursive: true, owned: false) as Label;
        AssertNotNull(slot0TopLabel, "Slot 0 top label must exist");
        AssertEqual("T1", slot0TopLabel.Text, "Slot 0 top label must show 'T1'");

        // Test Countdown UI toggling
        var countdownLabel = atlasView.GetNodeOrNull<Label>("%QueueCountdownLabel");
        AssertNotNull(countdownLabel, "QueueCountdownLabel must exist");
        atlasView.ShowCountdownUI(3.0f);
        AssertTrue(countdownLabel.Visible, "QueueCountdownLabel must be visible during countdown");
        AssertTrue(countdownLabel.Text.Contains("3.0s"), $"Countdown text must display remaining seconds, was '{countdownLabel.Text}'");

        atlasView.HideCountdownUI();
        AssertFalse(countdownLabel.Visible, "QueueCountdownLabel must be hidden when countdown stops");

        // Clear Queue
        queueClearBtn.EmitSignal(Button.SignalName.Pressed);
        AssertEqual(0, atlasView.QueueManager?.Count, "Queue count must be 0 after clicking Clear");
        AssertTrue(queueTitle.Text.Contains("0/10"), $"QueueTitleLabel must update to 0/10 after clear, was '{queueTitle.Text}'");

        // Switch back to Gear tab
        main.SwitchWorkspaceTab(Main.WorkspaceTab.Gear);
    }

    private static void TestPassiveTreeViewIntegration(Main main)
    {
        GD.Print("[Tests] Verifying Task 5.1: PassiveTreeView node hierarchy, search filter, pan/zoom, auto-pathing, and respec...");

        var passiveTreeView = main.PassiveTreeView;
        AssertNotNull(passiveTreeView, "PassiveTreeView must be instantiated in Main.tscn");

        // 1. Verify Node Hierarchy
        var searchInput = passiveTreeView.GetNodeOrNull<LineEdit>("%SearchInput");
        AssertNotNull(searchInput, "SearchInput must exist in PassiveTreeView");

        var pointsBadge = passiveTreeView.GetNodeOrNull<Label>("%PointsBadgeLabel");
        AssertNotNull(pointsBadge, "PointsBadgeLabel must exist in PassiveTreeView");

        var zoomInBtn = passiveTreeView.GetNodeOrNull<Button>("%ZoomInBtn");
        var zoomOutBtn = passiveTreeView.GetNodeOrNull<Button>("%ZoomOutBtn");
        var resetViewBtn = passiveTreeView.GetNodeOrNull<Button>("%ResetViewBtn");
        var toggleStatsBtn = passiveTreeView.GetNodeOrNull<Button>("%ToggleStatsBtn");
        var respecBtn = passiveTreeView.GetNodeOrNull<Button>("%RespecBtn");

        AssertNotNull(zoomInBtn, "ZoomInBtn must exist");
        AssertNotNull(zoomOutBtn, "ZoomOutBtn must exist");
        AssertNotNull(resetViewBtn, "ResetViewBtn must exist");
        AssertNotNull(toggleStatsBtn, "ToggleStatsBtn must exist");
        AssertNotNull(respecBtn, "RespecBtn must exist");

        var canvas = passiveTreeView.Canvas;
        AssertNotNull(canvas, "PassiveTreeCanvas must exist in PassiveTreeView");

        var nodeNameLabel = passiveTreeView.GetNodeOrNull<Label>("%NodeNameLabel");
        AssertNotNull(nodeNameLabel, "NodeNameLabel must exist");

        var nodeTypeBadge = passiveTreeView.GetNodeOrNull<Label>("%NodeTypeBadge");
        AssertNotNull(nodeTypeBadge, "NodeTypeBadge must exist");

        var nodeActionBtn = passiveTreeView.GetNodeOrNull<Button>("%NodeActionBtn");
        AssertNotNull(nodeActionBtn, "NodeActionBtn must exist in NodeInspectorPanel");

        var statsDrawer = passiveTreeView.GetNodeOrNull<PanelContainer>("%StatsSummaryDrawer");
        AssertNotNull(statsDrawer, "StatsSummaryDrawer must exist");

        // 2. Tab Navigation to Passives
        main.SwitchWorkspaceTab(Main.WorkspaceTab.Passives);
        AssertTrue(passiveTreeView.Visible, "PassiveTreeView must be visible on Passives tab");
        AssertTrue(main.BattleViewport?.Visible ?? false, "BattleViewport must remain persistent and visible on Passives tab");
        AssertFalse(main.EquipmentView?.Visible ?? false, "EquipmentView must be hidden on Passives tab");
        AssertFalse(main.MapDeviceMargin?.Visible ?? false, "MapDeviceMargin must be hidden on Passives tab");

        // 3. Search Filter & Highlighting
        passiveTreeView.SetSearchQuery("Life");
        AssertEqual("Life", passiveTreeView.CurrentSearchQuery, "CurrentSearchQuery mismatch");
        var matchCountLabel = passiveTreeView.GetNodeOrNull<Label>("%MatchCountLabel");
        AssertNotNull(matchCountLabel, "MatchCountLabel must exist");
        AssertTrue(matchCountLabel.Text.Contains("matches") || matchCountLabel.Text.Contains("kết quả"),
            $"MatchCountLabel should show match count, was '{matchCountLabel.Text}'");

        passiveTreeView.SetSearchQuery("Resist");
        AssertTrue(!string.IsNullOrEmpty(matchCountLabel.Text), "Searching for 'Resist' should show matches");

        passiveTreeView.ClearSearch();
        AssertEqual(string.Empty, passiveTreeView.CurrentSearchQuery, "Search query should be cleared");
        AssertEqual(string.Empty, matchCountLabel.Text, "MatchCountLabel should be empty when search cleared");

        // 4. Node Selection and Inspector
        var treeState = passiveTreeView.TreeState;
        AssertNotNull(treeState, "TreeState must be initialized in PassiveTreeView");

        // Select Marauder start or Witch start
        treeState.SetStartingClass("start_witch");
        treeState.SetCharacterLevel(5); // 4 points

        passiveTreeView.SelectNodeById("witch_spell_1");
        AssertEqual("Spell Damage", nodeNameLabel.Text, "NodeNameLabel should show 'Spell Damage'");
        AssertTrue(nodeTypeBadge.Text.Contains("MINOR"), "NodeTypeBadge should show MINOR");
        AssertTrue(nodeActionBtn.Text.Contains("ALLOCATE"), $"Action button should show ALLOCATE, was '{nodeActionBtn.Text}'");
        AssertFalse(nodeActionBtn.Disabled, "Action button should be enabled for adjacent node");

        // Allocate node via UI
        passiveTreeView.AllocateSelectedNode();
        AssertTrue(treeState.IsAllocated("witch_spell_1"), "witch_spell_1 must now be allocated");
        AssertTrue(nodeActionBtn.Text.Contains("REFUND") || nodeActionBtn.Text.Contains("ALLOCATED"),
            $"Action button should update after allocation, was '{nodeActionBtn.Text}'");

        // 5. Auto-Pathing
        passiveTreeView.SelectNodeById("witch_notable_arcane");
        AssertTrue(nodeActionBtn.Text.Contains("PATH") || nodeActionBtn.Text.Contains("ALLOCATE"),
            $"Action button for notable should offer PATH TO NODE, was '{nodeActionBtn.Text}'");

        passiveTreeView.AutoPathToSelectedNode();
        AssertTrue(treeState.IsAllocated("witch_notable_arcane"), "witch_notable_arcane must be allocated via auto-pathing");

        // 6. Stats Summary Drawer
        passiveTreeView.SetStatsSummaryVisible(true);
        AssertTrue(statsDrawer.Visible, "StatsSummaryDrawer must be visible when toggled on");

        var statsListVBox = passiveTreeView.GetNodeOrNull<VBoxContainer>("%StatsListVBox");
        AssertNotNull(statsListVBox, "StatsListVBox must exist");
        AssertTrue(statsListVBox.GetChildCount() > 0, "StatsListVBox must contain passive stat lines");

        passiveTreeView.SetStatsSummaryVisible(false);
        AssertFalse(statsDrawer.Visible, "StatsSummaryDrawer must be hidden when closed");

        // 7. Respec Button
        respecBtn.EmitSignal(Button.SignalName.Pressed);
        AssertEqual(0, treeState.SpentPoints, "All allocated points should be refunded after respec");
        AssertFalse(treeState.IsAllocated("witch_spell_1"), "witch_spell_1 should be unallocated after respec");
        AssertEqual(4, treeState.AvailablePoints, "All 4 points must be available after respec");

        // Switch back to Gear tab
        main.SwitchWorkspaceTab(Main.WorkspaceTab.Gear);
    }

    private static void TestStashViewIntegration(Main main)
    {
        GD.Print("[Tests] Verifying Task 5.2: StashView node hierarchy, 4 affinity tabs, and 1-Click Dump...");

        var stashView = main.StashView;
        AssertNotNull(stashView, "StashView must exist in Main");

        var tabCurrencyBtn = stashView.GetNodeOrNull<Button>("%TabCurrencyBtn");
        var tabGearBtn = stashView.GetNodeOrNull<Button>("%TabGearBtn");
        var tabMapsBtn = stashView.GetNodeOrNull<Button>("%TabMapsBtn");
        var tabFossilsBtn = stashView.GetNodeOrNull<Button>("%TabFossilsBtn");
        var dumpAllBtn = stashView.GetNodeOrNull<Button>("%DumpAllButton");
        var dumpStatusLabel = stashView.GetNodeOrNull<Label>("%DumpStatusLabel");
        var summaryLabel = stashView.GetNodeOrNull<Label>("%SummaryLabel");

        AssertNotNull(tabCurrencyBtn, "TabCurrencyBtn must exist");
        AssertNotNull(tabGearBtn, "TabGearBtn must exist");
        AssertNotNull(tabMapsBtn, "TabMapsBtn must exist");
        AssertNotNull(tabFossilsBtn, "TabFossilsBtn must exist");
        AssertNotNull(dumpAllBtn, "DumpAllButton must exist");
        AssertNotNull(dumpStatusLabel, "DumpStatusLabel must exist");
        AssertNotNull(summaryLabel, "SummaryLabel must exist");

        var currencyPanel = stashView.GetNodeOrNull<Control>("%CurrencyPanel");
        var gearPanel = stashView.GetNodeOrNull<Control>("%GearPanel");
        var mapsPanel = stashView.GetNodeOrNull<Control>("%MapsPanel");
        var fossilsPanel = stashView.GetNodeOrNull<Control>("%FossilsPanel");

        AssertNotNull(currencyPanel, "CurrencyPanel must exist");
        AssertNotNull(gearPanel, "GearPanel must exist");
        AssertNotNull(mapsPanel, "MapsPanel must exist");
        AssertNotNull(fossilsPanel, "FossilsPanel must exist");

        // 2. Persistent Viewport check when switching to Stash Tab
        main.SwitchWorkspaceTab(Main.WorkspaceTab.Stash);
        AssertTrue(stashView.Visible, "StashView must be visible when on Stash tab");
        AssertTrue(main.BattleViewport?.Visible ?? false, "BattleViewport must remain 100% visible on Stash tab (Persistent Viewport)");
        AssertFalse(main.EquipmentView?.Visible ?? false, "EquipmentView must be hidden on Stash tab");
        AssertFalse(main.PassiveTreeView?.Visible ?? false, "PassiveTreeView must be hidden on Stash tab");

        // 3. Sub-Tab Switching
        stashView.SwitchTab(StashTabType.Currency);
        AssertTrue(currencyPanel.Visible, "CurrencyPanel must be visible");
        AssertFalse(gearPanel.Visible, "GearPanel must be hidden");

        stashView.SwitchTab(StashTabType.Gear);
        AssertTrue(gearPanel.Visible, "GearPanel must be visible");
        AssertFalse(currencyPanel.Visible, "CurrencyPanel must be hidden");

        stashView.SwitchTab(StashTabType.Maps);
        AssertTrue(mapsPanel.Visible, "MapsPanel must be visible");
        AssertFalse(gearPanel.Visible, "GearPanel must be hidden");

        stashView.SwitchTab(StashTabType.Fossils);
        AssertTrue(fossilsPanel.Visible, "FossilsPanel must be visible");
        AssertFalse(mapsPanel.Visible, "MapsPanel must be hidden");

        // 4. Currency Deposit & Summary
        main.Player.Stash.DepositCurrency("chaos_orb", 10);
        main.Player.Stash.DepositCurrency("divine_orb", 1);
        stashView.RefreshView();
        AssertTrue(summaryLabel.Text.Contains("Currencies") || summaryLabel.Text.Contains("Tiền tệ"),
            $"SummaryLabel must display currency count, was '{summaryLabel.Text}'");

        // 5. Gear Stash & Filtering
        stashView.SwitchTab(StashTabType.Gear);
        if (main.Db.WeaponBases.Count > 0)
        {
            var swordDef = main.Db.WeaponBases.Values.First();
            var weapon = new WeaponItemInstance(swordDef, "Rare", 0, 50) { CustomName = "Stashed Blade" };
            main.Player.Stash.DepositGear(weapon);
            stashView.RefreshView();
            stashView.SetGearFilter("Weapons");
            var gearItemList = stashView.GetNodeOrNull<VBoxContainer>("%GearItemList");
            AssertNotNull(gearItemList, "GearItemList must exist");
            AssertTrue(gearItemList.GetChildCount() > 0, "GearItemList must show deposited weapon");
        }

        // 6. Map Stash & Activation Hook
        stashView.SwitchTab(StashTabType.Maps);
        if (main.Db.Maps.TryGetValue("strand", out var strandDef))
        {
            main.Player.Stash.DepositMap(strandDef, 2, "Normal");
            stashView.RefreshView();
            var mapsList = stashView.GetNodeOrNull<VBoxContainer>("%MapsListContainer");
            AssertNotNull(mapsList, "MapsListContainer must exist");
            AssertTrue(mapsList.GetChildCount() > 0, "MapsListContainer must show Strand map");
        }

        // 7. Fossils Stash
        stashView.SwitchTab(StashTabType.Fossils);
        main.Player.Stash.DepositFossil("pristine_fossil", 4);
        stashView.RefreshView();
        var fossilsGrid = stashView.GetNodeOrNull<GridContainer>("%FossilsGrid");
        AssertNotNull(fossilsGrid, "FossilsGrid must exist");
        AssertTrue(fossilsGrid.GetChildCount() == 7, $"FossilsGrid must display all 7 Abyssal fossils, got {fossilsGrid.GetChildCount()}");

        // 8. 1-Click Dump Button
        main.Player.Currencies["scroll_of_wisdom"] = 25;
        main.Player.MapInventory.Add("strand");
        stashView.OnDumpAllPressed();

        AssertEqual(0, main.Player.Currencies.Count, "Player backpack currencies must be empty after 1-Click Dump");
        AssertEqual(0, main.Player.MapInventory.Count, "Player backpack maps must be empty after 1-Click Dump");
        AssertTrue(main.Player.Stash.GetCurrencyCount("scroll_of_wisdom") >= 25, "Stash must receive dumped scrolls");
        AssertTrue(dumpStatusLabel.Text.Contains("Deposited") || dumpStatusLabel.Text.Contains("cất"),
            $"DumpStatusLabel should show success message, was '{dumpStatusLabel.Text}'");

        // Switch back to Gear tab
        main.SwitchWorkspaceTab(Main.WorkspaceTab.Gear);
    }

    /// <summary>
    /// Verifies Task 6.1: Smooth Tab Switching (Micro-Animations).
    /// Tests:
    /// 1. Tab switching micro-animation parameters (duration 0.15s, slide offset).
    /// 2. Directional slide offset (positive when navigating right, negative when navigating left).
    /// 3. Immediate visibility state of target view, clean hiding of outgoing views.
    /// 4. Resting position accuracy and modulate reset.
    /// 5. Rapid switching resilience (no crash, previous tweens cancelled cleanly).
    /// 6. Persistent Combat Viewport (BattleViewport visible at all times).
    /// </summary>
    private static void TestTabTransitionMicroAnimations(Main main)
    {
        GD.Print("[Tests] Verifying Task 6.1: Smooth Tab Switching Micro-Animations...");

        var battleViewport = main.BattleViewport;
        AssertNotNull(battleViewport, "BattleViewport must exist");

        // 1. Verify animation constants
        AssertTrue(Mathf.IsEqualApprox(Main.TabTransitionDuration, 0.15f), $"TabTransitionDuration must be 0.15s, was {Main.TabTransitionDuration}");
        AssertTrue(Main.TabTransitionSlideOffset > 0f, "TabTransitionSlideOffset must be positive");

        Vector2 restingPos = main.GetTabRestingPosition();
        AssertTrue(restingPos.X >= 0f && restingPos.Y >= 0f, $"Tab resting position should be valid, was {restingPos}");

        // 2. Switch from Gear (0) to Atlas (2) -> Forward/Right navigation
        main.SwitchWorkspaceTab(Main.WorkspaceTab.Gear, animated: false);
        var gearView = main.EquipmentView;
        var atlasContainer = main.MapDeviceMargin;
        AssertNotNull(gearView, "EquipmentView must exist");
        AssertNotNull(atlasContainer, "MapDeviceMargin must exist");

        AssertTrue(gearView.Visible, "GearView must be visible when on Gear tab");
        AssertFalse(atlasContainer.Visible, "AtlasContainer must be hidden when on Gear tab");

        // Now switch to Atlas with animation
        main.SwitchWorkspaceTab(Main.WorkspaceTab.Atlas, animated: true);
        AssertTrue(atlasContainer.Visible, "AtlasContainer must immediately become visible upon tab switch");
        AssertFalse(gearView.Visible, "GearView must immediately be hidden upon tab switch");
        AssertTrue(battleViewport.Visible, "BattleViewport must remain persistently visible");

        if (main.IsInsideTree())
        {
            // Forward navigation (Gear 0 -> Atlas 2): slide from right (+offset)
            float expectedStartX = restingPos.X + Main.TabTransitionSlideOffset;
            AssertTrue(Mathf.IsEqualApprox(atlasContainer.Position.X, expectedStartX),
                $"Atlas incoming slide start X should be {expectedStartX}, was {atlasContainer.Position.X}");
            AssertTrue(Mathf.IsEqualApprox(atlasContainer.Modulate.A, 0.0f),
                $"Atlas incoming fade start alpha should be 0.0, was {atlasContainer.Modulate.A}");
        }

        // 3. Switch from Atlas (2) to Skills (1) -> Backward/Left navigation
        main.SwitchWorkspaceTab(Main.WorkspaceTab.Skills, animated: true);
        var gemView = main.GemView;
        AssertNotNull(gemView, "GemView must exist");
        AssertTrue(gemView.Visible, "GemView must be visible on Skills tab");
        AssertFalse(atlasContainer.Visible, "AtlasContainer must be hidden");

        if (main.IsInsideTree())
        {
            // Backward navigation (Atlas 2 -> Skills 1): slide from left (-offset)
            float expectedStartX = restingPos.X - Main.TabTransitionSlideOffset;
            AssertTrue(Mathf.IsEqualApprox(gemView.Position.X, expectedStartX),
                $"GemView incoming slide start X should be {expectedStartX}, was {gemView.Position.X}");
            AssertTrue(Mathf.IsEqualApprox(gemView.Modulate.A, 0.0f),
                $"GemView incoming fade start alpha should be 0.0, was {gemView.Modulate.A}");
        }

        // 4. Test rapid switching resilience (spam click tabs)
        main.SwitchWorkspaceTab(Main.WorkspaceTab.Passives, animated: true);
        main.SwitchWorkspaceTab(Main.WorkspaceTab.Stash, animated: true);
        main.SwitchWorkspaceTab(Main.WorkspaceTab.Gear, animated: true);

        AssertTrue(gearView.Visible, "Final active tab (Gear) must be visible");
        AssertFalse(gemView.Visible, "Skills tab must be hidden");
        AssertFalse(atlasContainer.Visible, "Atlas tab must be hidden");
        AssertFalse(main.PassiveTreeView?.Visible ?? false, "Passives tab must be hidden");
        AssertFalse(main.StashView?.Visible ?? false, "Stash tab must be hidden");
        AssertTrue(battleViewport.Visible, "BattleViewport must remain visible through rapid tab switches");

        // 5. Test Non-Animated / Direct layout application
        main.ApplyTabVisibility(Main.WorkspaceTab.Stash, animated: false);
        var stashView = main.StashView;
        AssertNotNull(stashView, "StashView must exist");
        AssertTrue(stashView.Visible, "StashView must be visible");
        AssertTrue(Mathf.IsEqualApprox(stashView.Position.X, restingPos.X),
            $"Non-animated transition must place view at resting X {restingPos.X}, was {stashView.Position.X}");
        AssertTrue(Mathf.IsEqualApprox(stashView.Modulate.A, 1.0f),
            $"Non-animated transition must leave alpha at 1.0, was {stashView.Modulate.A}");

        // Return to Gear tab for subsequent tests
        main.SwitchWorkspaceTab(Main.WorkspaceTab.Gear, animated: false);
        AssertTrue(gearView.Visible, "Gear tab restored for clean test state");
    }

    private static void TestEdgeGlowAlerts(Main main)
    {
        GD.Print("[Tests] Verifying Task 6.2: Edge Glow Alerts (Golden Border Glow, Ting Sound & Tab Tree Glow)...");

        // 1. Verify EdgeGlowAlert node binding & layout
        var glowAlert = main.EdgeGlowAlert;
        AssertNotNull(glowAlert, "EdgeGlowAlert must exist on Main");
        AssertTrue(glowAlert.CustomMinimumSize.Y >= 28f, $"EdgeGlowAlert height should be >= 28px, was {glowAlert.CustomMinimumSize.Y}");
        AssertEqual(Control.MouseFilterEnum.Ignore, glowAlert.MouseFilter, "EdgeGlowAlert must ignore mouse clicks to avoid obstructing header buttons");

        // 2. Verify PassiveTabGlowOverlay attached to TabPassivesBtn
        var tabPassivesBtn = main.GetNodeOrNull<Button>("%TabPassivesBtn");
        AssertNotNull(tabPassivesBtn, "TabPassivesBtn must exist");
        var tabGlow = tabPassivesBtn.GetNodeOrNull<PassiveTabGlowOverlay>("PassiveTabGlowOverlay");
        AssertNotNull(tabGlow, "PassiveTabGlowOverlay must be attached to TabPassivesBtn");
        AssertEqual(Control.MouseFilterEnum.Ignore, tabGlow.MouseFilter, "PassiveTabGlowOverlay must ignore mouse clicks");

        // 3. Test Tier 1 Drop Alert Trigger (Golden Edge Glow + Ting Sound)
        string? alertDropName = null;
        Action<string?> alertHandler = name => alertDropName = name;
        glowAlert.OnTier1DropAlertTriggered += alertHandler;

        try
        {
            main.TriggerTier1DropAlert("Divine Orb");
            AssertEqual("Divine Orb", alertDropName, "OnTier1DropAlertTriggered event must fire with dropped item name");
            AssertTrue(glowAlert.IsGlowActive, "Golden Edge Glow must become active upon Tier 1 drop alert");
            AssertTrue(glowAlert.CurrentGlowAlpha > 0.0f, "CurrentGlowAlpha must be greater than 0");

            // Test programmatic alpha clamp & redraw
            glowAlert.SetGlowAlpha(0.85f);
            AssertEqual(0.85f, glowAlert.CurrentGlowAlpha, "SetGlowAlpha must accurately update glow alpha");
            glowAlert.SetGlowAlpha(0.0f);
            AssertFalse(glowAlert.IsGlowActive, "Glow should become inactive when alpha is set to 0");
        }
        finally
        {
            glowAlert.OnTier1DropAlertTriggered -= alertHandler;
        }

        // 4. Test Passive Tree Tab Alert (Unspent Points & Tab Switching)
        var treeState = main.PassiveTreeView?.TreeState;
        AssertNotNull(treeState, "PassiveTreeView.TreeState must be initialized");

        // 4a. 0 unspent points -> Tab Tree glow inactive
        treeState.SetCharacterLevel(1);
        main.UpdatePassiveTreeAlert();
        AssertEqual(0, treeState.AvailablePoints, "Level 1 should have 0 unspent points");
        AssertFalse(glowAlert.IsPassiveTabGlowActive, "Passive Tab Glow must be inactive when 0 unspent points");
        AssertEqual("🌳 Tree", tabPassivesBtn.Text, "Tab button text should be default when 0 points");

        // 4b. Advance level -> unspent points > 0 -> Tab Tree glow active when on Gear tab
        main.SwitchWorkspaceTab(Main.WorkspaceTab.Gear, animated: false);
        treeState.SetCharacterLevel(4); // (4-1)*1 = 3 points
        main.UpdatePassiveTreeAlert();
        AssertEqual(3, treeState.AvailablePoints, "Level 4 should grant 3 passive points");
        AssertTrue(glowAlert.IsPassiveTabGlowActive, "Passive Tab Glow must be active on non-passive tab with unspent points");
        AssertTrue(tabPassivesBtn.Text.Contains("(+3)"), $"Tab button text should contain badge (+3), was '{tabPassivesBtn.Text}'");

        // 4c. Switch to Passives tab -> Tab glow calms down while active on tab
        main.SwitchWorkspaceTab(Main.WorkspaceTab.Passives, animated: false);
        AssertFalse(glowAlert.IsPassiveTabGlowActive, "Passive Tab Glow must be inactive while user is actively viewing Passives tab");

        // 4d. Switch away to Atlas tab -> Tab glow resumes to remind player
        main.SwitchWorkspaceTab(Main.WorkspaceTab.Atlas, animated: false);
        AssertTrue(glowAlert.IsPassiveTabGlowActive, "Passive Tab Glow must resume when navigating away with unspent points");

        // 4e. Test celebratory Level Up burst
        glowAlert.TriggerLevelUpPulse();
        AssertTrue(glowAlert.IsPassiveTabGlowActive, "Passive tab glow must be active during/after level up pulse");

        // 4f. Reset points back to level 1 for clean test state
        treeState.SetCharacterLevel(1);
        main.SwitchWorkspaceTab(Main.WorkspaceTab.Gear, animated: false);
        main.UpdatePassiveTreeAlert();
        AssertEqual(0, treeState.AvailablePoints, "Reset to Level 1 should restore 0 available points");
        AssertFalse(glowAlert.IsPassiveTabGlowActive, "Passive Tab Glow must be deactivated after resetting points");
        AssertEqual("🌳 Tree", tabPassivesBtn.Text, "Tab button text must be restored to '🌳 Tree'");
    }

    /// <summary>
    /// Verifies Task 6.3: In-Window Hotkeys / Shortcuts (No OS-level hooks).
    /// Tests Space (Emergency Portal), I/G (Gear), P (Passives), M (Atlas), S (Stash),
    /// K (Skills), Tab/F11 (Expand/Shrink), and Escape (Dismiss overlays / collapse window).
    /// </summary>
    private static void TestInWindowShortcuts(Main main)
    {
        GD.Print("[Tests] Testing Task 6.3: In-Window Hotkeys / Shortcuts...");

        var windowController = main.WindowController;

        // 1. Test Key.Space: Contextual (In Sanctuary -> Launches Map; In Combat -> Emergency Portal)
        // 1a. In Sanctuary: Space launches next map
        bool handledSpaceLaunch = main.HandleInWindowShortcut(Key.Space);
        AssertTrue(handledSpaceLaunch, "Space key in Sanctuary must launch map");
        AssertTrue(main.Engine is { IsInCombat: true }, "Engine must be in combat after launching map");

        // 1b. In Combat: Space triggers Emergency Portal
        bool handledSpacePortal = main.HandleInWindowShortcut(Key.Space);
        AssertTrue(handledSpacePortal, "Space key in combat must trigger Emergency Portal");
        var statusLabel = main.GetNodeOrNull<Label>("%StatusLabel");
        AssertNotNull(statusLabel, "StatusLabel must exist");
        AssertFalse(main.Engine?.IsInCombat ?? false, "Engine must no longer be in combat after Emergency Portal");

        // 2. Start in Compact Mode
        windowController.ShrinkWindow(keepBottom: true);
        AssertFalse(windowController.IsExpanded, "Window must start in Compact Mode for shortcut tests");

        // 3. Test Key.I and Key.G: Gear & Inventory Tab
        // 3a. Press Key.I in Compact mode -> auto-expands to Gear
        bool handledI = main.HandleInWindowShortcut(Key.I);
        AssertTrue(handledI, "Key.I must be handled");
        AssertEqual(Main.WorkspaceTab.Gear, main.ActiveTab, "Key.I must set ActiveTab to Gear");
        AssertTrue(windowController.IsExpanded, "Key.I must auto-expand window to full height");

        // 3b. Press Key.I again while already on Gear and expanded -> PoE-style toggle collapses to Compact
        bool handledIToggle = main.HandleInWindowShortcut(Key.I);
        AssertTrue(handledIToggle, "Key.I toggle must be handled");
        AssertFalse(windowController.IsExpanded, "Key.I when already on Gear should toggle window back to Compact");

        // 3c. Press Key.G in Compact mode -> auto-expands to Gear
        bool handledG = main.HandleInWindowShortcut(Key.G);
        AssertTrue(handledG, "Key.G must be handled");
        AssertEqual(Main.WorkspaceTab.Gear, main.ActiveTab, "Key.G must set ActiveTab to Gear");
        AssertTrue(windowController.IsExpanded, "Key.G must auto-expand window to full height");

        // 4. Test Key.P: Passive Skill Tree Tab
        // 4a. While expanded on Gear, press Key.P -> switches to Passives, stays expanded
        bool handledP = main.HandleInWindowShortcut(Key.P);
        AssertTrue(handledP, "Key.P must be handled");
        AssertEqual(Main.WorkspaceTab.Passives, main.ActiveTab, "Key.P must switch ActiveTab to Passives");
        AssertTrue(windowController.IsExpanded, "Key.P should maintain expanded window state when switching tabs");

        // 4b. Press Key.P again -> toggles back to Compact
        bool handledPToggle = main.HandleInWindowShortcut(Key.P);
        AssertTrue(handledPToggle, "Key.P toggle must be handled");
        AssertFalse(windowController.IsExpanded, "Key.P when already on Passives should toggle window back to Compact");

        // 5. Test Key.M: Map Device / Atlas Tab
        bool handledM = main.HandleInWindowShortcut(Key.M);
        AssertTrue(handledM, "Key.M must be handled");
        AssertEqual(Main.WorkspaceTab.Atlas, main.ActiveTab, "Key.M must switch ActiveTab to Atlas");
        AssertTrue(windowController.IsExpanded, "Key.M must auto-expand window to display Atlas");

        // 6. Test Key.S: Affinity Stash Tab
        bool handledS = main.HandleInWindowShortcut(Key.S);
        AssertTrue(handledS, "Key.S must be handled");
        AssertEqual(Main.WorkspaceTab.Stash, main.ActiveTab, "Key.S must switch ActiveTab to Stash");
        AssertTrue(windowController.IsExpanded, "Key.S must keep window expanded to display Stash");

        // 7. Test Key.K: Skills & Gems Tab
        bool handledK = main.HandleInWindowShortcut(Key.K);
        AssertTrue(handledK, "Key.K must be handled");
        AssertEqual(Main.WorkspaceTab.Skills, main.ActiveTab, "Key.K must switch ActiveTab to Skills");
        AssertTrue(windowController.IsExpanded, "Key.K must keep window expanded to display Skills");

        // 7b. Test Key.H: Crucible Sanctuary Tab (Prompt 5)
        bool handledH = main.HandleInWindowShortcut(Key.H);
        AssertTrue(handledH, "Key.H must be handled");
        AssertEqual(Main.WorkspaceTab.Sanctuary, main.ActiveTab, "Key.H must switch ActiveTab to Sanctuary");
        AssertTrue(windowController.IsExpanded, "Key.H must keep window expanded to display Sanctuary");

        // 7c. Test Key.A: Quick-Focus The Crucible Anvil (Prompt 5)
        windowController.ShrinkWindow(keepBottom: true);
        bool handledA = main.HandleInWindowShortcut(Key.A);
        AssertTrue(handledA, "Key.A must be handled");
        AssertEqual(Main.WorkspaceTab.Sanctuary, main.ActiveTab, "Key.A must auto-expand and switch to Sanctuary");
        AssertTrue(windowController.IsExpanded, "Key.A must auto-expand window to display Crucible Anvil");

        // 8. Test Key.Tab and Key.F11: Quick Window Mode Switch
        // 8a. Currently expanded, press Key.Tab -> collapses to Compact
        bool handledTab = main.HandleInWindowShortcut(Key.Tab);
        AssertTrue(handledTab, "Key.Tab must be handled");
        AssertFalse(windowController.IsExpanded, "Key.Tab must collapse expanded window to Compact");

        // 8b. Currently compact, press Key.Tab -> expands to full height
        bool handledTab2 = main.HandleInWindowShortcut(Key.Tab);
        AssertTrue(handledTab2, "Key.Tab must be handled");
        AssertTrue(windowController.IsExpanded, "Key.Tab must expand compact window to Expanded mode");

        // 8c. Test Key.F11: collapses and expands
        bool handledF11 = main.HandleInWindowShortcut(Key.F11);
        AssertTrue(handledF11, "Key.F11 must be handled");
        AssertFalse(windowController.IsExpanded, "Key.F11 must collapse window to Compact");
        bool handledF11_2 = main.HandleInWindowShortcut(Key.F11);
        AssertTrue(handledF11_2, "Key.F11 must be handled");
        AssertTrue(windowController.IsExpanded, "Key.F11 must expand window to Expanded mode");

        // 9. Test Key.Escape Hierarchy
        // 9a. Test Escape dismissing Equipment Help Overlay
        main.SwitchWorkspaceTab(Main.WorkspaceTab.Gear, animated: false);
        main.EquipmentView?.SetHelpOverlayVisible(true);
        AssertTrue(main.EquipmentView?.IsHelpOverlayVisible == true, "Equipment help overlay should be visible");
        bool handledEscOverlay = main.HandleInWindowShortcut(Key.Escape);
        AssertTrue(handledEscOverlay, "Escape must be handled");
        AssertFalse(main.EquipmentView?.IsHelpOverlayVisible == true, "Escape must close Equipment help overlay");
        AssertTrue(windowController.IsExpanded, "Closing an overlay with Escape should NOT collapse the window yet");

        // 9b. Test Escape dismissing Passive Tree Stats Drawer
        main.SwitchWorkspaceTab(Main.WorkspaceTab.Passives, animated: false);
        main.PassiveTreeView?.SetStatsSummaryVisible(true);
        AssertTrue(main.PassiveTreeView?.IsStatsSummaryVisible == true, "Stats summary drawer should be visible");
        bool handledEscDrawer = main.HandleInWindowShortcut(Key.Escape);
        AssertTrue(handledEscDrawer, "Escape must be handled");
        AssertFalse(main.PassiveTreeView?.IsStatsSummaryVisible == true, "Escape must close stats summary drawer");
        AssertTrue(windowController.IsExpanded, "Closing a stats drawer with Escape should NOT collapse the window yet");

        // 9c. Test Escape when no overlays are open -> collapses window to Compact Mode
        bool handledEscCollapse = main.HandleInWindowShortcut(Key.Escape);
        AssertTrue(handledEscCollapse, "Escape must be handled when expanded");
        AssertFalse(windowController.IsExpanded, "Escape must collapse window to Compact Mode when no modal/drawer is open");

        // 10. Test unhandled keys (e.g. Key.X, Key.Z)
        bool handledUnknown = main.HandleInWindowShortcut(Key.X);
        AssertFalse(handledUnknown, "Unmapped keys (like Key.X) must return false");

        // Reset to clean state
        main.SwitchWorkspaceTab(Main.WorkspaceTab.Gear, animated: false);
        GD.Print("[Tests] Task 6.3: In-Window Hotkeys / Shortcuts PASSED!");
    }

    private static void TestCameraLookAheadAndTransformations(Main main)
    {
        GD.Print("[Tests] Verifying Prompt 4: Camera Look-Ahead (+80px), BossArenaLock lerp, and 640px dimensions...");

        // 1. Verify 640px Corridor Dimensions
        AssertEqual(640f, Arena2D.ArenaWidth, "Arena2D.ArenaWidth must be 640px");
        AssertEqual(172f, Arena2D.ArenaHeight, "Arena2D.ArenaHeight must be 172px");
        AssertEqual(new Vector2(320f, 86f), Arena2D.CenterPosition, "Arena2D.CenterPosition must be (320, 86)");
        AssertEqual(640f, ExileMovementController.DefaultCorridorWidth, "ExileMovementController.DefaultCorridorWidth must be 640px");
        AssertEqual(260f, ExileMovementController.DefaultPerceptionRange, "Perception range must remain 260px");
        AssertEqual(320f, ExileMovementController.DefaultCenterX, "DefaultCenterX must be 320px");
        AssertEqual(600f, ExileMovementController.MaxCorridorX, "MaxCorridorX must be 600px");

        // 2. Verify Camera LookAheadOffset configuration
        var camera = new ArenaCamera();
        camera.Reset(new Vector2(40f, 104f));
        AssertEqual(new Vector2(80f, 0f), camera.LookAheadOffset, "LookAheadOffset must default to (80, 0)");
        AssertEqual(new Vector2(120f, 104f), camera.TargetPosition, "TargetPosition should be initialPos (40, 104) + LookAhead (80, 0)");

        // 3. Verify Bidirectional Coordinate Transformations (WorldToScreen <-> ScreenToWorld)
        var worldPt = new Vector2(450f, 110f);
        var screenPt = camera.WorldToScreen(worldPt, Arena2D.CenterPosition);
        var backPt = camera.ScreenToWorld(screenPt, Arena2D.CenterPosition);
        if (MathF.Abs(worldPt.X - backPt.X) > 0.001f || MathF.Abs(worldPt.Y - backPt.Y) > 0.001f)
        {
            throw new Exception($"Bidirectional transformation failed: Started at {worldPt}, roundtrip resulted in {backPt}");
        }

        // Test default viewportCenter overload
        var defaultScreenPt = camera.WorldToScreen(worldPt);
        AssertEqual(screenPt, defaultScreenPt, "WorldToScreen with omitted viewportCenter should default to Arena2D.CenterPosition");
        var defaultBackPt = camera.ScreenToWorld(defaultScreenPt);
        AssertEqual(worldPt, defaultBackPt, "ScreenToWorld with omitted viewportCenter should default to Arena2D.CenterPosition");

        // 4. Verify BossArenaLock mode and Lerp to center (X = 320px)
        Vector2 bossCenterWorld = new(2232f, 86f);
        camera.SetArenaLock(bossCenterWorld);
        AssertEqual(ArenaCameraMode.BossArenaLock, camera.Mode, "Mode should be BossArenaLock");
        AssertEqual(bossCenterWorld, camera.TargetPosition, "Camera target should snap to Boss Arena center");

        // Step camera updates to simulate convergence
        for (int i = 0; i < 30; i++)
        {
            camera.Update(0.05f, new Vector2(2040f, 104f), bossCenterWorld, shouldLockToArena: true);
        }
        AssertTrue(camera.IsLocked, "Camera should complete lerp and lock firmly to boss center");
        Vector2 lockedScreen = camera.WorldToScreen(bossCenterWorld);
        if (MathF.Abs(lockedScreen.X - 320f) > 1.5f || MathF.Abs(lockedScreen.Y - 86f) > 1.5f)
        {
            throw new Exception($"Boss arena center must project to screen center (320, 86), but was {lockedScreen}");
        }

        // 5. Verify CorridorFollow steady state projection
        camera.ReleaseLock();
        AssertEqual(ArenaCameraMode.CorridorFollow, camera.Mode, "Releasing lock must restore CorridorFollow mode");
        Vector2 exileWorld = new(300f, 104f);
        camera.TargetPosition = exileWorld + camera.LookAheadOffset;
        camera.CurrentPosition = camera.TargetPosition; // Simulate steady state
        Vector2 exileScreen = camera.WorldToScreen(exileWorld);
        float expectedScreenX = Arena2D.CenterPosition.X - camera.LookAheadOffset.X; // 320 - 80 = 240px
        if (MathF.Abs(exileScreen.X - expectedScreenX) > 0.01f)
        {
            throw new Exception($"Steady-state Exile screen X should be {expectedScreenX}, but was {exileScreen.X}");
        }
        float forwardBuffer = Arena2D.ArenaWidth - exileScreen.X;
        AssertTrue(forwardBuffer >= 400f, $"Forward buffer ({forwardBuffer}px) must be >= 400px, giving generous preview of incoming packs");

        // 6. Verify BattleViewport integrated arena camera
        if (main.BattleViewport?.Arena != null)
        {
            var arenaCam = main.BattleViewport.Arena.Camera;
            AssertNotNull(arenaCam, "BattleViewport.Arena.Camera must exist");
            AssertEqual(new Vector2(80f, 0f), arenaCam.LookAheadOffset, "Live arena camera LookAheadOffset must be (80, 0)");
        }

        camera.Free();
        GD.Print("[Tests] Camera Look-Ahead (+80px) & 640px dimensions PASSED!");
    }

    private static void TestDualStateViewportAndCombatDpsMeter(Main main)
    {
        var battleViewport = main.BattleViewport;
        AssertNotNull(battleViewport, "BattleViewport must exist");
        var arena = battleViewport.Arena;
        AssertNotNull(arena, "Arena2D must exist inside BattleViewport");

        // 1. Verify ViewportMode enum and initial Sanctuary state
        battleViewport.SetViewportMode(Arena2D.ViewportMode.SanctuaryIdle);
        AssertEqual(Arena2D.ViewportMode.SanctuaryIdle, battleViewport.CurrentMode, "CurrentMode should be SanctuaryIdle");
        AssertEqual(Arena2D.ViewportMode.SanctuaryIdle, arena.CurrentMode, "Arena2D.CurrentMode should be SanctuaryIdle");

        // 2. Process one step in Sanctuary mode
        arena._Process(0.033);
        AssertEqual(new Vector2(240f, Arena2D.GroundBaselineY), arena.ExilePosition, "Exile must be resting at (240, 86) in Sanctuary mode");
        AssertEqual(ExileMovementState.Idle, arena.MovementState, "MovementState must be Idle in Sanctuary mode");
        AssertEqual(0, arena.ActiveProjectileCount, "Projectiles must be 0 in Sanctuary mode");

        // 3. Verify Sanctuary status label and DPS label in Quick Footer
        battleViewport.SetSanctuaryStatus(main.Player.Sanctuary, main.MapQueue.Count);
        var statusLabel = main.GetNodeOrNull<Label>("%StatusLabel");
        AssertNotNull(statusLabel, "StatusLabel must exist in Quick Footer");
        AssertTrue(statusLabel.Text.Contains("Sanctuary Safe Haven (100% Recovered)"),
            $"StatusLabel should show Sanctuary Safe Haven message, got '{statusLabel.Text}'");

        var dpsLabel = main.GetNodeOrNull<Label>("%DpsLabel");
        AssertNotNull(dpsLabel, "DpsLabel must exist in Quick Footer (%DpsLabel)");
        AssertEqual("DPS: 0 • Peak: 0", dpsLabel.Text, "DpsLabel should show 0 in Sanctuary mode");

        // Verify Battle View Header displays Sanctuary safe haven telemetry
        AssertNotNull(battleViewport.HeaderBar, "HeaderBar must exist in BattleViewport");
        AssertTrue(battleViewport.HeaderBar.IsInSanctuary, "HeaderBar.IsInSanctuary must be true in Sanctuary mode");
        AssertTrue(battleViewport.HeaderBar.CurrentMapName.Contains("Sanctuary"), $"HeaderBar map name should contain 'Sanctuary', got '{battleViewport.HeaderBar.CurrentMapName}'");
        AssertEqual(100f, battleViewport.HeaderBar.CurrentProgress, "HeaderBar progress must be 100% (safe haven recovery)");

        // 4. Switch to ExpeditionCombat mode
        battleViewport.SetViewportMode(Arena2D.ViewportMode.ExpeditionCombat);
        AssertEqual(Arena2D.ViewportMode.ExpeditionCombat, battleViewport.CurrentMode, "CurrentMode should be ExpeditionCombat");
        AssertEqual(Arena2D.ViewportMode.ExpeditionCombat, arena.CurrentMode, "Arena2D.CurrentMode should be ExpeditionCombat");

        // 5. Feed in live combat DPS snapshot and verify telemetry updates
        var testSnapshot = new CombatDpsSnapshot(
            CurrentDps: 142580f,
            PeakDps: 389120f,
            AverageDps: 118400f,
            PhysicalPercent: 42.0f,
            FirePercent: 38.0f,
            ColdPercent: 0.0f,
            LightningPercent: 15.0f,
            ChaosPercent: 5.0f,
            HitRateAps: 3.45f,
            AccuracyPercent: 100f
        );

        battleViewport.UpdateDps(testSnapshot);
        AssertEqual("DPS: 142,580 • Peak: 389,120", dpsLabel.Text, "DpsLabel must display formatted Current and Peak DPS");

        // 6. Verify diagnostic tooltip breakdown
        AssertNotNull(dpsLabel.TooltipText, "DpsLabel must have diagnostic breakdown tooltip");
        AssertTrue(dpsLabel.TooltipText.Contains("COMBAT TELEMETRY & DPS BREAKDOWN"), "Tooltip must contain telemetry header");
        AssertTrue(dpsLabel.TooltipText.Contains("Physical:      42.0%"), "Tooltip must list physical damage percentage");
        AssertTrue(dpsLabel.TooltipText.Contains("Fire:          38.0%"), "Tooltip must list fire damage percentage");
        AssertTrue(dpsLabel.TooltipText.Contains("Lightning:     15.0%"), "Tooltip must list lightning damage percentage");
        AssertTrue(dpsLabel.TooltipText.Contains("Chaos:         5.0%"), "Tooltip must list chaos damage percentage");
        AssertTrue(dpsLabel.TooltipText.Contains("Hit Rate:       3.45 APS"), "Tooltip must list hit rate APS");

        // 7. Verify peak DPS gold modulation highlight
        var peakSnapshot = new CombatDpsSnapshot(
            CurrentDps: 400000f,
            PeakDps: 400000f,
            AverageDps: 200000f,
            PhysicalPercent: 100f,
            FirePercent: 0f,
            ColdPercent: 0f,
            LightningPercent: 0f,
            ChaosPercent: 0f,
            HitRateAps: 4.0f,
            AccuracyPercent: 100f
        );
        battleViewport.UpdateDps(peakSnapshot);
        AssertEqual("DPS: 400,000 • Peak: 400,000", dpsLabel.Text, "DpsLabel must update to peak value");
        AssertEqual(new Color(1.0f, 0.88f, 0.35f), dpsLabel.Modulate, "DpsLabel must be highlighted in gold on peak DPS");

        // Return to Sanctuary mode
        battleViewport.SetViewportMode(Arena2D.ViewportMode.SanctuaryIdle);
        battleViewport.SetSanctuaryStatus();

        GD.Print("[Tests] Prompt 4: Dual-State Viewport & Combat DPS Meter PASSED!");
    }

    private static void TestSanctuaryViewIntegration(Main main)
    {
        GD.Print("[Tests] Verifying Prompt 5: Sanctuary View Integration & 4-Panel Layout...");

        // 1. Verify Sanctuary Tab Button & SanctuaryView Node
        var tabSanctuary = main.TabSanctuaryBtn;
        AssertNotNull(tabSanctuary, "TabSanctuaryBtn must exist on sub-tab hinge bar");
        AssertTrue(tabSanctuary.Text.Contains("Sanctuary", StringComparison.OrdinalIgnoreCase), "TabSanctuaryBtn must contain 'Sanctuary' text");

        var sanctuaryView = main.SanctuaryView;
        AssertNotNull(sanctuaryView, "SanctuaryView must exist inside TabWorkspaceContainer");

        // 2. Tab Navigation to Sanctuary
        main.SwitchWorkspaceTab(Main.WorkspaceTab.Sanctuary, animated: false);
        var bgPanel = sanctuaryView.GetNodeOrNull<Control>("BgPanel");
        AssertNotNull(bgPanel, "BgPanel must exist inside SanctuaryView");
        AssertTrue(bgPanel.GetCombinedMinimumSize().X <= 640f,
            $"Sanctuary BgPanel minimum width ({bgPanel.GetCombinedMinimumSize().X}px) must fit within 640px window");

        var sanctuaryScroll = sanctuaryView.GetNodeOrNull<ScrollContainer>("BgPanel/ViewMargin/SanctuaryScroll");
        AssertNotNull(sanctuaryScroll, "SanctuaryScroll must exist to provide vertical scroll and zero horizontal overflow");
        AssertEqual((int)ScrollContainer.ScrollMode.Disabled, (int)sanctuaryScroll.HorizontalScrollMode, "Horizontal scroll must be disabled");

        AssertEqual(Main.WorkspaceTab.Sanctuary, main.ActiveTab, "ActiveTab must be Sanctuary");
        AssertTrue(sanctuaryView.Visible, "SanctuaryView must be visible when Sanctuary tab is active");
        AssertFalse(main.EquipmentView?.Visible ?? false, "EquipmentView must be hidden on Sanctuary tab");
        AssertFalse(main.GemView?.Visible ?? false, "GemView must be hidden on Sanctuary tab");
        AssertFalse(main.PassiveTreeView?.Visible ?? false, "PassiveTreeView must be hidden on Sanctuary tab");
        AssertFalse(main.StashView?.Visible ?? false, "StashView must be hidden on Sanctuary tab");

        // 3. Verify Crucible Anvil Service & Smelter Service
        AssertNotNull(sanctuaryView.AnvilService, "AnvilService must be initialized");
        AssertNotNull(sanctuaryView.SmelterService, "SmelterService must be initialized");

        // 4. Verify Anvil focus method
        sanctuaryView.FocusAnvil();

        // 5. Test Anvil currency slam on equipped item
        var bodyArmour = main.Player.Gear.BodyArmour;
        if (bodyArmour != null)
        {
            main.Player.Stash.DepositCurrency(IdleExile.Core.Services.TheCrucibleAnvilService.ChaosId, 5);
            var initialChaos = main.Player.Stash.GetCurrencyCount(IdleExile.Core.Services.TheCrucibleAnvilService.ChaosId);

            if (string.Equals(bodyArmour.Rarity, "Rare", StringComparison.OrdinalIgnoreCase))
            {
                var craftRes = sanctuaryView.AnvilService.SlamCurrency(bodyArmour, IdleExile.Core.Services.TheCrucibleAnvilService.ChaosId, main.Db, main.Player);
                AssertTrue(craftRes.Success, "Chaos slam via AnvilService on Rare body armour must succeed");
                AssertEqual(initialChaos - 1, main.Player.Stash.GetCurrencyCount(IdleExile.Core.Services.TheCrucibleAnvilService.ChaosId), "Stash chaos currency should decrease");
                AssertTrue(craftRes.NewAffixes.Count >= 4, "Chaos rolled item must have at least 4 affixes");
            }
        }

        // 6. Verify Voltaxic Sulphite & Abyssal Resonator State
        AssertTrue(main.Player.Sanctuary.VoltaxicSulphite >= 0, "Voltaxic Sulphite must be non-negative");
        AssertTrue(main.Player.Sanctuary.DeepestDepth >= 0, "Deepest Abyss depth must be non-negative");

        // 7. Verify Voidstones & Pinnacle Hall
        AssertTrue(main.Player.Sanctuary.VoidstonesCount >= 0 && main.Player.Sanctuary.VoidstonesCount <= 4, "Voidstones count must be between 0 and 4");

        // Reset back to Gear tab
        main.SwitchWorkspaceTab(Main.WorkspaceTab.Gear, animated: false);
        GD.Print("[Tests] Prompt 5: Sanctuary View Integration PASSED!");
    }

    private static void TestArenaAndBattleViewportResetForNewMap(Main main)
    {
        var battleViewport = main.BattleViewport;
        AssertNotNull(battleViewport, "BattleViewport must exist");
        var arena = battleViewport.Arena;
        AssertNotNull(arena, "Arena2D must exist");

        // 1. Simulate advancing into boss room during combat
        arena.UpdateExileWorldPosition(new Vector2(2232f, 104f), ExileMovementState.InBossArena);
        arena.SetFogGateSealed(true);
        arena.Camera.SetArenaLock(new Vector2(2232f, 86f));
        battleViewport.SetMapProgress(100f);

        AssertTrue(arena.IsFogGateSealed, "Fog gate should be sealed during boss phase");
        AssertEqual(ArenaCameraMode.BossArenaLock, arena.Camera.Mode, "Camera should be locked to BossArenaLock");

        // 2. Perform ResetForNewMap
        battleViewport.ResetForNewMap();

        // 3. Verify all presentation and camera state is restored for a new linear corridor run
        AssertFalse(arena.IsFogGateSealed, "Fog gate must be reset to false on ResetForNewMap");
        AssertEqual(ArenaCameraMode.CorridorFollow, arena.Camera.Mode, "Camera mode must be restored to CorridorFollow");
        AssertFalse(arena.Camera.IsLocked, "Camera lock must be released on ResetForNewMap");
        AssertEqual(new Vector2(40f, 104f), arena.ExileWorldPosition, "ExileWorldPosition must reset to corridor beginning (40, 104)");
        AssertEqual(ExileMovementState.Marching, arena.MovementState, "MovementState must reset to Marching");
        AssertEqual(0f, battleViewport.HeaderBar?.CurrentProgress ?? 0f, "Map progress must reset to 0%");

        GD.Print("[Tests] Arena & BattleViewport ResetForNewMap PASSED!");
    }

    private static void TestBattleViewportSanctuaryHeader(Main main)
    {
        GD.Print("[Tests] Verifying Battle View Header Sanctuary Telemetry upon leaving map...");

        var battleViewport = main.BattleViewport;
        AssertNotNull(battleViewport, "BattleViewport must exist");
        var header = battleViewport.HeaderBar;
        AssertNotNull(header, "HeaderBar must exist on BattleViewport");

        // 1. Initial / Default Sanctuary State
        battleViewport.SetSanctuaryStatus(main.Player.Sanctuary, main.MapQueue.Count);
        AssertTrue(header.IsInSanctuary, "Header must be in Sanctuary mode");
        AssertEqual(1, header.SanctuaryTier, "Initial sanctuary tier should be 1");
        AssertEqual("Hollow Crypt", header.SanctuaryTierName, "Tier 1 name should be Hollow Crypt");
        AssertEqual(100f, header.CurrentProgress, "Sanctuary progress bar must be 100%");
        AssertEqual(1, header.ActiveThreatCount, "Should have 1 Safe Haven badge");

        var tierBadgeLabel = header.GetNodeOrNull<Label>("%TierBadgeLabel");
        AssertNotNull(tierBadgeLabel, "TierBadgeLabel must exist");
        AssertEqual("S1", tierBadgeLabel.Text, "Tier badge text must be S1 in Sanctuary Tier 1");

        var mapNameLabel = header.GetNodeOrNull<Label>("%MapNameLabel");
        AssertNotNull(mapNameLabel, "MapNameLabel must exist");
        AssertTrue(mapNameLabel.Text.Contains("Sanctuary"), "MapNameLabel must display Sanctuary title");

        var bossIndicatorLabel = header.GetNodeOrNull<Label>("%BossIndicatorLabel");
        AssertNotNull(bossIndicatorLabel, "BossIndicatorLabel must exist");
        AssertTrue(bossIndicatorLabel.Text.Contains("SAFE"), "BossIndicatorLabel must show SAFE in sanctuary");

        var quantityLabel = header.GetNodeOrNull<Label>("%QuantityBonusLabel");
        AssertNotNull(quantityLabel, "QuantityBonusLabel must exist");
        AssertTrue(quantityLabel.Text.Contains("Dust:"), "Quantity label must display Thaumaturgic Dust");

        // 2. Custom Sanctuary State update (Tier 3 Eldritch Sanctum, 24,000 Dust, 6 Queued Maps, 10% Infused IIQ)
        header.SetSanctuary(
            tier: 3,
            tierName: "Eldritch Sanctum",
            thaumaturgicDust: 24000,
            queuedMaps: 6,
            infusedBonus: 10f,
            infusedMapsRemaining: 4,
            sulphite: 8500,
            maxSulphite: 15000,
            deepestDepth: 210,
            voidstones: 2
        );
        AssertTrue(header.IsInSanctuary, "Header must remain in Sanctuary mode");
        AssertEqual(3, header.SanctuaryTier, "Sanctuary tier must be 3");
        AssertEqual("Eldritch Sanctum", header.SanctuaryTierName, "Tier 3 name must be Eldritch Sanctum");
        AssertEqual(24000L, header.SanctuaryDust, "Sanctuary dust must be 24,000");
        AssertEqual(6, header.QueuedMapCount, "Queued maps must be 6");
        AssertEqual("S3", tierBadgeLabel.Text, "Tier badge must update to S3");
        AssertTrue(quantityLabel.Text.Contains("Dust: 24,000"), "Quantity label must display updated dust");
        AssertTrue(quantityLabel.Text.Contains("Queue: 6"), "Quantity label must display queued maps");
        AssertTrue(quantityLabel.Text.Contains("+10% IIQ"), "Quantity label must display active map infusion bonus");
        AssertNotNull(quantityLabel.TooltipText, "TooltipText must not be null");
        AssertTrue(quantityLabel.TooltipText.Contains("THE CRUCIBLE SANCTUARY"), "Tooltip must contain Sanctuary header");
        AssertTrue(quantityLabel.TooltipText.Contains("Eldritch Sanctum"), "Tooltip must list tier name");

        // 3. Transition to Combat Map (e.g. SetMap) -> verifies header reverts to combat telemetry
        var testMap = new MapDefinition("test_dunes", "Dunes", 5, 72, false, 10, 80, "The Dune Boss", "Sand");
        var testConfig = new MapRunConfig(testMap, Array.Empty<RolledMapModifier>());
        battleViewport.SetMap(testConfig, 35f, "Magic");
        AssertFalse(header.IsInSanctuary, "Header must NOT be in Sanctuary mode while mapping");
        AssertEqual(5, header.CurrentTier, "Current tier should be map tier 5");
        AssertEqual("T5", tierBadgeLabel.Text, "Tier badge should be T5 in combat map");
        AssertTrue(mapNameLabel.Text.Contains("Dunes"), "Map name should show Dunes");
        AssertEqual(35f, header.CurrentProgress, "Progress should be 35%");
        AssertTrue(bossIndicatorLabel.Text.Contains("BOSS"), "Boss indicator should be BOSS in combat map");

        // 4. Leaving map via Emergency Portal / SetSanctuaryStatus
        battleViewport.SetViewportMode(Arena2D.ViewportMode.SanctuaryIdle);
        battleViewport.SetSanctuaryStatus(main.Player.Sanctuary, main.MapQueue.Count);
        AssertTrue(header.IsInSanctuary, "Header must return to Sanctuary mode after leaving map");
        AssertTrue(mapNameLabel.Text.Contains("Sanctuary"), "Header map name must return to Sanctuary title");
        AssertEqual(100f, header.CurrentProgress, "Progress bar must return to 100%");
        AssertTrue(bossIndicatorLabel.Text.Contains("SAFE"), "Boss indicator must return to SAFE");

        GD.Print("[Tests] Battle View Header Sanctuary Telemetry PASSED!");
    }

    private static void TestBattleViewportMapHeaderDuringMapping(Main main)
    {
        GD.Print("[Tests] Verifying Battle View Header Map Telemetry during active mapping...");

        var battleViewport = main.BattleViewport;
        AssertNotNull(battleViewport, "BattleViewport must exist");
        var header = battleViewport.HeaderBar;
        AssertNotNull(header, "HeaderBar must exist on BattleViewport");

        // 1. Initial State: Sanctuary Safe Haven
        battleViewport.SetViewportMode(Arena2D.ViewportMode.SanctuaryIdle);
        battleViewport.SetSanctuaryStatus(main.Player.Sanctuary, main.MapQueue.Count);
        AssertTrue(header.IsInSanctuary, "Header must start in Sanctuary mode before mapping");

        // 2. Activate a map via Main.OnMapActivatedWithMods
        var mapDef = new MapDefinition("canyon", "Canyon Map", 8, 75, false, 15, 80, "Gore Beak", "A dusty red canyon.");
        main.OnMapActivatedWithMods(mapDef, Array.Empty<RolledMapModifier>(), "Rare");

        // 3. Verify Battle Viewport & Header are actively displaying the combat map
        AssertEqual(Arena2D.ViewportMode.ExpeditionCombat, battleViewport.CurrentMode, "Viewport mode must be ExpeditionCombat");
        AssertFalse(header.IsInSanctuary, "Header must NOT be in Sanctuary mode while actively mapping");
        AssertEqual("Canyon Map", header.CurrentMapName, "Header current map name must match activated map");
        AssertEqual(8, header.CurrentTier, "Header current tier must match activated map tier 8");
        AssertEqual("Rare", header.CurrentRarity, "Header current rarity must match activated map rarity");
        AssertEqual(0f, header.CurrentProgress, "Initial progress must be 0%");

        var tierBadgeLabel = header.GetNodeOrNull<Label>("%TierBadgeLabel");
        AssertNotNull(tierBadgeLabel, "TierBadgeLabel must exist");
        AssertEqual("T8", tierBadgeLabel.Text, "Tier badge label must display 'T8'");

        var mapNameLabel = header.GetNodeOrNull<Label>("%MapNameLabel");
        AssertNotNull(mapNameLabel, "MapNameLabel must exist");
        AssertTrue(mapNameLabel.Text.Contains("Canyon Map"), $"MapNameLabel should contain 'Canyon Map', got '{mapNameLabel.Text}'");
        AssertTrue(mapNameLabel.Text.Contains("Rare"), $"MapNameLabel should show 'Rare' rarity, got '{mapNameLabel.Text}'");

        var bossIndicatorLabel = header.GetNodeOrNull<Label>("%BossIndicatorLabel");
        AssertNotNull(bossIndicatorLabel, "BossIndicatorLabel must exist");
        AssertTrue(bossIndicatorLabel.Text.Contains("BOSS"), "Boss indicator must show 'BOSS' instead of 'SAFE'");

        // 4. Simulate combat tick progression (50% progress)
        main.Engine?.Update(1.0f);
        battleViewport.SetMapProgress(50f);
        AssertFalse(header.IsInSanctuary, "Header must remain in combat mode during combat progression");
        AssertEqual(50f, header.CurrentProgress, "Header progress must update to 50%");
        AssertEqual("T8", tierBadgeLabel.Text, "Tier badge label must stay T8");

        // 5. Trigger emergency portal escape to return to Sanctuary
        main.OnEmergencyPortalTriggered();
        AssertEqual(Arena2D.ViewportMode.SanctuaryIdle, battleViewport.CurrentMode, "Viewport mode must return to SanctuaryIdle");
        AssertTrue(header.IsInSanctuary, "Header must return to Sanctuary mode after emergency portal");
        AssertTrue(tierBadgeLabel.Text.StartsWith("S"), $"Tier badge label must start with 'S' in sanctuary, got '{tierBadgeLabel.Text}'");
        AssertTrue(mapNameLabel.Text.Contains("Sanctuary"), $"Map name label must display Sanctuary title, got '{mapNameLabel.Text}'");
        AssertTrue(bossIndicatorLabel.Text.Contains("SAFE"), "Boss indicator must return to SAFE");

        GD.Print("[Tests] Battle View Header Map Telemetry during mapping PASSED!");
    }

    private static void AssertEqual<T>(T expected, T actual, string? message = null)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"Assertion Failed: Expected '{expected}', but was '{actual}'. {message}");
    }

    private static void AssertNotNull([System.Diagnostics.CodeAnalysis.NotNull] object? obj, string message)
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
