using System;
using System.Linq;
using Godot;
using IdleExile.Core.Models;
using IdleExile.Core.State;
using IdleExile.UI.Components;

namespace IdleExile.UI;

/// <summary>
/// Unit tests for EquipmentView UI component, verifying 10-slot layout,
/// Smart Tooltip delta previews, and action buttons.
/// </summary>
public static class EquipmentViewTests
{
    public static void RunAll()
    {
        GD.Print("[Tests] Running EquipmentViewTests (Task 3.4)...");

        var scene = GD.Load<PackedScene>("res://scenes/EquipmentView.tscn");
        if (scene == null)
            throw new Exception("[EquipmentViewTests] Could not load res://scenes/EquipmentView.tscn");

        var view = scene.Instantiate<EquipmentView>();
        view._Ready();

        try
        {
            TestInitializationAndNodeBinding(view);
            TestPlayerAttachmentAndBenchmarks(view);
            TestSlotSelectionAndTooltip(view);
            TestCandidateInspectionAndDelta(view);
            TestEquipAction(view);
            TestLockToggle(view);
            TestTwoHanderOffHandLockout(view);
            TestWeaponDisplayAndPoEImplicits(view);
            TestOffHandDualWieldCandidateDrawer(view);

            GD.Print("[Tests] All EquipmentViewTests PASSED successfully!");
        }
        finally
        {
            view.Free();
        }
    }

    private static void TestInitializationAndNodeBinding(EquipmentView view)
    {
        AssertTrue(view != null, "View should instantiate successfully");
        AssertTrue(view != null && view.CandidateItems.Count > 0, "Default candidates should be populated for testing");
    }

    private static void TestPlayerAttachmentAndBenchmarks(EquipmentView view)
    {
        var player = new PlayerState();
        player.Initialize(1200f, 300f, 400f);

        view.SetPlayer(player);
        AssertEqual(player, view.Player, "PlayerState properly attached");

        var ehpLabel = view.GetNodeOrNull<Label>("%EhpBenchmarkLabel");
        AssertTrue(ehpLabel != null && !string.IsNullOrEmpty(ehpLabel.Text), "EHP benchmark should have a value");
    }

    private static void TestSlotSelectionAndTooltip(EquipmentView view)
    {
        view.SelectSlot(GearSlot.Helmet);
        AssertEqual(GearSlot.Helmet, view.SelectedSlot, "Selected slot should update to Helmet");

        var titleLabel = view.GetNodeOrNull<Label>("%TooltipTitleLabel");
        AssertTrue(titleLabel != null, "Tooltip title label exists");
    }

    private static void TestCandidateInspectionAndDelta(EquipmentView view)
    {
        var candidate = view.CandidateItems[0];
        view.InspectCandidate(candidate);

        AssertEqual(candidate.InstanceId, view.InspectedItem?.InstanceId, "Inspected item matches candidate");

        var deltaCard = view.GetNodeOrNull<PanelContainer>("%DeltaPreviewCard");
        AssertTrue(deltaCard != null && deltaCard.Visible, "DeltaPreviewCard should be visible for candidates");

        var deltaSummary = view.GetNodeOrNull<Label>("%DeltaSummaryLabel");
        AssertTrue(deltaSummary != null && !string.IsNullOrEmpty(deltaSummary.Text), "Delta summary should have notes");
    }

    private static void TestEquipAction(EquipmentView view)
    {
        var player = view.Player!;
        var candidate = view.CandidateItems[0];
        var targetSlot = candidate.TargetSlot;

        view.InspectCandidate(candidate);

        var equipBtn = view.GetNodeOrNull<Button>("%EquipButton");
        AssertTrue(equipBtn != null && !equipBtn.Disabled, "Equip button should be enabled for candidate item");

        // Simulate equip press
        equipBtn?.EmitSignal(Button.SignalName.Pressed);

        var equipped = player.Gear.GetItem(targetSlot);
        AssertEqual(candidate.InstanceId, equipped?.InstanceId, "Item should now be equipped in player gear");
        AssertTrue(!view.CandidateItems.Contains(candidate), "Candidate should be removed from candidate drawer upon equip");
    }

    private static void TestLockToggle(EquipmentView view)
    {
        var item = view.Player!.Gear.GetItem(view.SelectedSlot);
        if (item == null) return;

        bool initialLock = item.IsLocked;
        var lockBtn = view.GetNodeOrNull<Button>("%LockButton");
        AssertTrue(lockBtn != null && !lockBtn.Disabled, "Lock button enabled");

        lockBtn?.EmitSignal(Button.SignalName.Pressed);
        AssertEqual(!initialLock, item.IsLocked, "Item lock status should be toggled");
    }

    private static void TestTwoHanderOffHandLockout(EquipmentView view)
    {
        var player = view.Player!;
        var twoHandDef = new WeaponBaseDefinition("2h_axe", "Colossus Axe", WeaponClass.TwoHandedAxe, HandSlotRequirement.TwoHanded, 50, 90, 1.2f, 5f);
        player.Gear.Equip(GearSlot.MainHand, new WeaponItemInstance(twoHandDef));

        view.RefreshView();

        var offHandBtn = view.GetNodeOrNull<Button>("%SlotOffHand");
        AssertTrue(offHandBtn != null && offHandBtn.Disabled, "Offhand button must be disabled when 2H weapon is equipped");
    }

    private static void TestWeaponDisplayAndPoEImplicits(EquipmentView view)
    {
        var player = view.Player!;
        var swordDef = new WeaponBaseDefinition("test_foil", "Jewelled Foil", WeaponClass.OneHandedSword, HandSlotRequirement.OneHanded, 30, 60, 1.6f, 5.5f, ImplicitAffixId: "foil_implicit_crit_multi", ImplicitValue: 25, Description: "Thrusting foil");
        var weapon = new WeaponItemInstance(swordDef, "Rare", quality: 20, itemLevel: 80);

        player.Gear.Equip(GearSlot.MainHand, weapon);
        view.SelectSlot(GearSlot.MainHand);

        // 1. Verify button displays weapon icon
        var mainBtn = view.GetNodeOrNull<Button>("%SlotMainHand");
        AssertTrue(mainBtn != null && mainBtn.Text.Contains("⚔️"), $"MainHand button should display sword icon, got '{mainBtn?.Text}'");

        // 2. Verify tooltip displays Weapon Class and Hand Requirement
        var subtitleLabel = view.GetNodeOrNull<Label>("%TooltipSubtitleLabel");
        AssertTrue(subtitleLabel != null && subtitleLabel.Text.Contains("Kiếm Một Tay"), $"Subtitle should contain weapon class, got '{subtitleLabel?.Text}'");

        // 3. Verify base stats line has Physical damage, Crit Chance, and APS
        var baseStatsLabel = view.GetNodeOrNull<Label>("%TooltipBaseStatsLabel");
        AssertTrue(baseStatsLabel != null && baseStatsLabel.Text.Contains("Sát thương vật lý"), "Base stats label should contain physical damage");
        AssertTrue(baseStatsLabel != null && baseStatsLabel.Text.Contains("Tỷ lệ chí mạng: 5.50%"), $"Base stats label should display crit chance, got '{baseStatsLabel?.Text}'");
        AssertTrue(baseStatsLabel != null && baseStatsLabel.Text.Contains("1.60 APS"), "Base stats label should display APS");

        // 4. Verify PoE Implicit modifier is formatted accurately
        var implicitLabel = view.GetNodeOrNull<Label>("%TooltipImplicitLabel");
        AssertTrue(implicitLabel != null && implicitLabel.Visible, "Implicit label should be visible");
        AssertTrue(implicitLabel != null && implicitLabel.Text.Contains("+25% to Global Critical Strike Multiplier"), $"Implicit should format correctly, got '{implicitLabel?.Text}'");
    }

    private static void TestOffHandDualWieldCandidateDrawer(EquipmentView view)
    {
        var player = view.Player!;
        // Equip 1H sword in MainHand
        var sword1Def = new WeaponBaseDefinition("s1", "Main Sword", WeaponClass.OneHandedSword, HandSlotRequirement.OneHanded, 10, 20, 1.4f, 5f);
        player.Gear.Equip(GearSlot.MainHand, new WeaponItemInstance(sword1Def));
        player.Gear.UnequipOffHand();

        // Select OffHand slot
        view.SelectSlot(GearSlot.OffHand);

        // Verify candidate items include 1H weapons (e.g. Boarding Axe candidate)
        var candidateHBox = view.GetNodeOrNull<HBoxContainer>("%CandidateHBox");
        AssertTrue(candidateHBox != null && candidateHBox.GetChildCount() > 0, "OffHand slot should show 1H weapon and shield candidates in drawer");

        // Find a 1H weapon candidate and inspect it
        var axeCandidate = view.CandidateItems.FirstOrDefault(i => i is WeaponItemInstance w && w.BaseDefinition.HandRequirement == HandSlotRequirement.OneHanded);
        AssertTrue(axeCandidate != null, "Should have a 1H candidate in drawer");

        view.InspectCandidate(axeCandidate!);
        AssertEqual(GearSlot.OffHand, view.SelectedSlot, "Selected slot should remain OffHand when inspecting a 1H candidate for dual wielding");

        // Simulate equip press into OffHand
        var equipBtn = view.GetNodeOrNull<Button>("%EquipButton");
        equipBtn?.EmitSignal(Button.SignalName.Pressed);

        AssertTrue(player.Gear.IsDualWielding, "Equipping 1H weapon into OffHand should trigger Dual Wielding");
        AssertEqual(axeCandidate!.InstanceId, player.Gear.OffHand?.InstanceId, "OffHand should now have the inspected candidate");

        var offBtn = view.GetNodeOrNull<Button>("%SlotOffHand");
        AssertTrue(offBtn != null && offBtn.Text.Contains("OFF"), "OffHand button should reflect equipped item");
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[EquipmentViewTests Assertion Failed] Expected TRUE, but got FALSE: {message}");
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!System.Collections.Generic.EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"[EquipmentViewTests Assertion Failed] Expected: {expected}, Actual: {actual} — {message}");
    }
}
