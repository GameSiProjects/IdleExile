using System;
using System.Collections.Generic;
using Godot;
using IdleExile.Core.Models;
using IdleExile.Core.State;
using IdleExile.UI.Components;

namespace IdleExile.UI;

/// <summary>
/// Comprehensive verification tests for Task 2.4: Player Vitometer (Bộ Đo Sinh Mệnh & Phòng Thủ).
/// Validates:
/// 1. Layered Health Bar: Life base, Energy Shield cyan overlay, Guard Skill golden shimmer.
/// 2. Mana & Mana Reservation Bar: Usable pool, reserved mana ratio, and reservation boundaries.
/// 3. Quick Flask Rack: 5 slots, charge gauge, threshold notch, active Auto-Instill glow, tooltips, and click/key drinking.
/// 4. Reactive integration with Core models: PlayerState and SkillLoadout events.
/// 5. Full Scene Tree integration inside BattleViewport.tscn.
/// </summary>
public static class VitometerViewTests
{
    public static void RunAll()
    {
        GD.Print("[Tests] Running VitometerViewTests (Task 2.4)...");

        TestLayeredHealthBarLogic();
        TestGuardSkillGoldenShimmer();
        TestManaAndReservationLogic();
        TestFlaskRackAndCharges();
        TestFlaskAutoInstillAndDurations();
        TestReactiveBindingWithPlayerStateAndLoadout();
        TestBattleViewportSceneIntegration();

        GD.Print("[Tests] All VitometerViewTests PASSED successfully!");
    }

    private static void TestLayeredHealthBarLogic()
    {
        var bar = new LayeredHealthBar();

        // 1. Initial defaults
        Assert(bar.CurrentLife == 100f, "Default CurrentLife should be 100");
        Assert(bar.MaxLife == 100f, "Default MaxLife should be 100");
        Assert(bar.LifePercent == 1.0f, "Default LifePercent should be 1.0 (100%)");
        Assert(bar.CurrentEnergyShield == 0f, "Default ES should be 0");
        Assert(!bar.IsGuardActive, "Guard skill should be inactive initially");

        // 2. Pure Life update
        bar.UpdateHealth(2500f, 3500f, 0f, 0f);
        Assert(bar.CurrentLife == 2500f, "CurrentLife should be 2500");
        Assert(bar.MaxLife == 3500f, "MaxLife should be 3500");
        Assert(Mathf.Abs(bar.LifePercent - (2500f / 3500f)) < 0.001f, "LifePercent should match 2500/3500");
        Assert(bar.MaxEnergyShield == 0f, "MaxES should be 0");

        // 3. Life + Energy Shield Hybrid
        bar.UpdateHealth(3500f, 3500f, 1200f, 1200f);
        Assert(bar.MaxEnergyShield == 1200f, "MaxES should be 1200");
        Assert(bar.CurrentEnergyShield == 1200f, "CurrentES should be 1200");
        Assert(bar.EsPercent == 1.0f, "EsPercent should be 100%");

        // 4. ES absorption: ES damaged while Life full
        bar.UpdateHealth(3500f, 3500f, 400f, 1200f);
        Assert(bar.CurrentLife == 3500f, "Life remains full while ES absorbs damage");
        Assert(bar.CurrentEnergyShield == 400f, "ES reduced to 400");
        Assert(Mathf.Abs(bar.EsPercent - (400f / 1200f)) < 0.001f, "EsPercent should be 33.3%");

        // 5. Chaos bypass: Life depleted while ES intact
        bar.UpdateHealth(1800f, 3500f, 1200f, 1200f);
        Assert(bar.CurrentLife == 1800f, "Life reduced by chaos bypass");
        Assert(bar.CurrentEnergyShield == 1200f, "ES remains intact during chaos bypass");

        bar.QueueFree();
    }

    private static void TestGuardSkillGoldenShimmer()
    {
        var bar = new LayeredHealthBar();

        bar.UpdateHealth(3000f, 3000f, 500f, 500f);
        Assert(!bar.IsGuardActive, "Guard skill should initially be inactive");

        // Activate Guard Skill (e.g. Steelskin)
        bar.SetGuardSkill(true, 500f, 500f, "Steelskin");
        Assert(bar.IsGuardActive, "Guard skill should be active");
        Assert(bar.GuardAbsorbPool == 500f, "Guard absorb pool should be 500");
        Assert(bar.GuardMaxAbsorb == 500f, "Guard max absorb should be 500");
        Assert(bar.GuardName == "Steelskin", "Guard name should be Steelskin");

        // Partial absorb
        bar.SetGuardSkill(true, 180f, 500f, "Steelskin");
        Assert(bar.GuardAbsorbPool == 180f, "Guard pool updated to 180");
        Assert(bar.IsGuardActive, "Guard remains active while absorb pool > 0");

        // Guard expiration / depletion
        bar.SetGuardSkill(false, 0f, 500f, "Steelskin");
        Assert(!bar.IsGuardActive, "Guard skill should be inactive after expiration");
        Assert(bar.GuardAbsorbPool == 0f, "Guard pool should be 0");

        bar.QueueFree();
    }

    private static void TestManaAndReservationLogic()
    {
        var bar = new ManaReservationBar();

        // 1. Initial defaults
        Assert(bar.CurrentMana == 50f, "Default CurrentMana should be 50");
        Assert(bar.MaxMana == 50f, "Default MaxMana should be 50");
        Assert(bar.ReservedMana == 0f, "Default ReservedMana should be 0");
        Assert(bar.ReservedPercent == 0f, "Default ReservedPercent should be 0%");
        Assert(bar.UnreservedMaxMana == 50f, "Default UnreservedMaxMana should equal MaxMana");

        // 2. High reservation (Aura loadout: Hatred + Herald of Ash = 738/820 reserved = 90%)
        bar.UpdateMana(82f, 820f, 738f);
        Assert(bar.MaxMana == 820f, "MaxMana should be 820");
        Assert(bar.ReservedMana == 738f, "ReservedMana should be 738");
        Assert(Mathf.Abs(bar.ReservedPercent - 90f) < 0.1f, "ReservedPercent should be 90%");
        Assert(Mathf.Abs(bar.UnreservedMaxMana - 82f) < 0.1f, "UnreservedMaxMana should be 82");
        Assert(Mathf.Abs(bar.CurrentMana - 82f) < 0.1f, "CurrentMana should be 82");
        Assert(bar.ManaPercent == 1.0f, "ManaPercent relative to unreserved pool should be 100%");

        // 3. Spending mana from unreserved pool
        bar.UpdateMana(30f, 820f, 738f);
        Assert(bar.CurrentMana == 30f, "CurrentMana should be 30");
        Assert(Mathf.Abs(bar.ManaPercent - (30f / 82f)) < 0.01f, "ManaPercent should be 30/82");

        // 4. Zero mana remaining (OOM)
        bar.UpdateMana(0f, 820f, 738f);
        Assert(bar.CurrentMana == 0f, "CurrentMana should be 0");
        Assert(bar.ManaPercent == 0f, "ManaPercent should be 0");

        // 5. Clamping test: mana cannot exceed unreserved pool
        bar.UpdateMana(500f, 820f, 738f);
        Assert(bar.CurrentMana == 82f, "CurrentMana must be clamped to UnreservedMaxMana (82)");

        bar.QueueFree();
    }

    private static void TestFlaskRackAndCharges()
    {
        var vitometer = new VitometerView();
        vitometer._Ready();

        Assert(vitometer.FlaskSlots.Count == 5, "Vitometer must have exactly 5 flask slots");

        // Verify flask types match PoE standard order
        Assert(vitometer.FlaskSlots[0].Type == FlaskType.Life, "Slot 0 should be Life Flask");
        Assert(vitometer.FlaskSlots[1].Type == FlaskType.Quicksilver, "Slot 1 should be Quicksilver Flask");
        Assert(vitometer.FlaskSlots[2].Type == FlaskType.Granite, "Slot 2 should be Granite Flask");
        Assert(vitometer.FlaskSlots[3].Type == FlaskType.Diamond, "Slot 3 should be Diamond Flask");
        Assert(vitometer.FlaskSlots[4].Type == FlaskType.Quartz, "Slot 4 should be Quartz Flask");

        // Test slot 1 (Quicksilver: 60 max, 20 per use)
        var qsSlot = vitometer.FlaskSlots[1];
        Assert(qsSlot.MaxCharges == 60f, "Quicksilver MaxCharges should be 60");
        Assert(qsSlot.CurrentCharges == 60f, "Quicksilver starts full");
        Assert(qsSlot.ChargesPerUse == 20f, "ChargesPerUse should be 20");
        Assert(qsSlot.CanUse, "Quicksilver should be ready to use");
        Assert(!qsSlot.IsActive, "Quicksilver should not be active initially");

        // Drink once
        bool drank = qsSlot.TryDrink();
        Assert(drank, "Drinking full flask should succeed");
        Assert(qsSlot.CurrentCharges == 40f, "Charges reduced by 20 -> 40");
        Assert(qsSlot.RemainingDuration == qsSlot.DurationSeconds, "Active duration started");
        Assert(qsSlot.IsActive, "Flask is now active");
        Assert(qsSlot.CanUse, "Flask still has 40 charges (can drink again)");

        // Drink second time
        drank = qsSlot.TryDrink();
        Assert(drank, "Drinking second time should succeed");
        Assert(qsSlot.CurrentCharges == 20f, "Charges reduced to 20");

        // Drink third time
        drank = qsSlot.TryDrink();
        Assert(drank, "Drinking third time should succeed");
        Assert(qsSlot.CurrentCharges == 0f, "Charges reduced to 0");
        Assert(!qsSlot.CanUse, "Cannot drink with 0 charges");

        // Try drink when empty
        drank = qsSlot.TryDrink();
        Assert(!drank, "Drinking empty flask must return false");

        // Add charges gained on kill
        qsSlot.UpdateState(25f, 60f, qsSlot.IsActive, qsSlot.RemainingDuration);
        Assert(qsSlot.CurrentCharges == 25f, "Charges updated to 25");
        Assert(qsSlot.CanUse, "CanUse is true again (25 >= 20)");

        vitometer.QueueFree();
    }

    private static void TestFlaskAutoInstillAndDurations()
    {
        var slot = new FlaskSlotView(FlaskType.Life, "Divine Life Flask", 60f, 20f, 4.0f, "1");
        slot.AutoInstillCondition = FlaskAutoInstillCondition.LifeBelowPercent;
        slot.AutoInstillThreshold = 50f;

        Assert(slot.KeyShortcut == "1", "Shortcut should be '1'");
        Assert(slot.TooltipText.Contains("Divine Life Flask"), "Tooltip must contain flask name");
        Assert(slot.TooltipText.Contains("Life < 50%"), "Tooltip must describe Auto-Instill condition");

        // Trigger manual drink
        bool drank = slot.TryDrink();
        Assert(drank, "TryDrink succeeded");
        Assert(slot.IsActive, "Flask is active");
        Assert(slot.RemainingDuration == 4.0f, "Duration is 4.0s");

        // Advance time
        slot._Process(1.5);
        Assert(Mathf.Abs(slot.RemainingDuration - 2.5f) < 0.05f, "Remaining duration ticked to ~2.5s");
        Assert(slot.IsActive, "Flask still active");

        // Advance time past expiration
        slot._Process(3.0);
        Assert(slot.RemainingDuration == 0f, "Remaining duration expired to 0");
        Assert(!slot.IsActive, "Flask is no longer active");

        slot.QueueFree();
    }

    private static void TestReactiveBindingWithPlayerStateAndLoadout()
    {
        var player = new PlayerState();
        player.Initialize(maxLife: 3000f, maxEnergyShield: 1000f, maxMana: 500f, reservedMana: 250f);

        var loadout = new SkillLoadout();
        loadout.GuardSkill.Name = "Molten Shell";
        loadout.GuardSkill.MaxAbsorbPool = 800f;
        loadout.GuardSkill.BuffDuration = 3.0f;

        var vitometer = new VitometerView();
        vitometer._Ready();

        // Bind models to Vitometer
        vitometer.Bind(player, loadout);

        // 1. Initial state sync verification
        Assert(vitometer.HealthBar.CurrentLife == 3000f, "HealthBar Life synced");
        Assert(vitometer.HealthBar.CurrentEnergyShield == 1000f, "HealthBar ES synced");
        Assert(vitometer.ManaBar.CurrentMana == 250f, "ManaBar usable mana synced (500 - 250)");
        Assert(vitometer.ManaBar.ReservedMana == 250f, "ManaBar reserved mana synced");
        Assert(!vitometer.HealthBar.IsGuardActive, "Guard skill starts inactive");

        // 2. Reactivity: Damage to player ES
        player.ApplyDamage(400f, isChaos: false, chaosDoesNotBypassEs: false, mindOverMatterPercent: 0f);
        Assert(vitometer.HealthBar.CurrentEnergyShield == 600f, "Vitometer reacted to ES loss");
        Assert(vitometer.HealthBar.CurrentLife == 3000f, "Life remained untouched");

        // 3. Reactivity: Guard Skill Activation
        bool guardActivated = loadout.GuardSkill.TryActivate();
        Assert(guardActivated, "Guard skill activated in loadout");
        Assert(vitometer.HealthBar.IsGuardActive, "Vitometer HealthBar activated golden shimmer");
        Assert(vitometer.HealthBar.GuardAbsorbPool == 800f, "Vitometer synced guard absorb pool");
        Assert(vitometer.HealthBar.GuardName == "Molten Shell", "Vitometer synced guard name");

        // 4. Reactivity: Flask Drink
        bool flaskDrank = vitometer.TryDrinkFlask(1); // Drink Quicksilver
        Assert(flaskDrank, "Drank flask through Vitometer API");
        Assert(loadout.Flasks[1].IsActive, "Loadout Quicksilver is active");
        Assert(vitometer.FlaskSlots[1].IsActive, "Vitometer Quicksilver slot reflects active status");
        Assert(vitometer.FlaskSlots[1].CurrentCharges == 40f, "Charges reduced to 40 in Vitometer");

        // 5. Reactivity: Guard Skill Tick & Expiry
        loadout.GuardSkill.Tick(3.5f);
        Assert(!loadout.GuardSkill.IsActive, "Guard skill expired in loadout");
        Assert(!vitometer.HealthBar.IsGuardActive, "Vitometer HealthBar deactivated golden shimmer");

        vitometer.QueueFree();
    }

    private static void TestBattleViewportSceneIntegration()
    {
        PackedScene scene = ResourceLoader.Load<PackedScene>("res://scenes/BattleViewport.tscn")!;
        var viewport = (BattleViewport)scene.Instantiate();
        viewport._Ready();

        Assert(viewport.VitometerPanel != null, "VitometerPanel exists in BattleViewport");
        Assert(viewport.Vitometer != null, "VitometerView is bound in BattleViewport");

        var vitometer = viewport.Vitometer;
        Assert(vitometer != null, "Vitometer must not be null");
        Assert(vitometer.FlaskSlots.Count == 5, "Vitometer has 5 FlaskSlots");

        // Test UpdateVitals through BattleViewport facade
        viewport.UpdateVitals(3200, 4000, 800, 1000, 120, 600, 75.0, "EHP: 16.5k | Armour: 10.2k");

        Assert(vitometer.HealthBar.CurrentLife == 3200f, "Facade updated HealthBar Life");
        Assert(vitometer.HealthBar.MaxLife == 4000f, "Facade updated HealthBar MaxLife");
        Assert(vitometer.HealthBar.CurrentEnergyShield == 800f, "Facade updated HealthBar ES");
        Assert(vitometer.HealthBar.MaxEnergyShield == 1000f, "Facade updated HealthBar MaxES");
        Assert(vitometer.ManaBar.CurrentMana == 120f, "Facade updated ManaBar CurrentMana");
        Assert(vitometer.ManaBar.MaxMana == 600f, "Facade updated ManaBar MaxMana");
        Assert(Mathf.Abs(vitometer.ManaBar.ReservedPercent - 75f) < 0.1f, "Facade updated ManaBar ReservedPercent");
        Assert(vitometer.DefensesSummaryLabel?.Text == "EHP: 16.5k | Armour: 10.2k", "Defenses summary label updated");

        // Test Guard Skill through BattleViewport facade
        viewport.SetGuardSkill(true, 450f, 500f, "Steelskin");
        Assert(vitometer.HealthBar.IsGuardActive, "Facade activated Guard Skill");
        Assert(vitometer.HealthBar.GuardAbsorbPool == 450f, "Facade set guard absorb pool");

        // Test Drink Flask through BattleViewport facade
        bool drank = viewport.TryDrinkFlask(0);
        Assert(drank, "Drank slot 0 flask through BattleViewport facade");

        viewport.QueueFree();
    }

    private static void Assert([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception($"[VitometerViewTests] Assertion failed: {message}");
        }
    }
}
