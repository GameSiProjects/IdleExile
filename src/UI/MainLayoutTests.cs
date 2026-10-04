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
            TestStatusCardEliminationAndDiagnostics(main);
            TestStickyDrawerLayout(main);
            TestActionButtonAndCandidateDrawerLayout(main);
            TestGemViewIntegration(main);
            TestAtlasViewIntegration(main);
            TestPassiveTreeViewIntegration(main);
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

    /// <summary>
    /// Verifies Task 1.3: Window Mode Display Controller in Main.cs.
    /// Tests transitions between Compact Mode (440x380) and Expanded Mode (440x780):
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
