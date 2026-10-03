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
            TestStatusCardEliminationAndDiagnostics(existingMain);
            TestOptimizedEquipmentDollLayout(existingMain);
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
    /// Verifies Task 1.2: Triệt tiêu Card Debug (StatusCard).
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
