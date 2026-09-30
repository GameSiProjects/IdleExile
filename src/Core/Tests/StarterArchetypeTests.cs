using System;
using System.Linq;
using IdleExile.Core.Models;
using IdleExile.Core.State;

namespace IdleExile.Core.Tests;

/// <summary>
/// Unit tests for Task 1.1: Starter Archetype Models & Initial Kits.
/// Validates archetypes definitions, attribute bindings, vitals, inventory kits, and PlayerState bootstrapping.
/// </summary>
public static class StarterArchetypeTests
{
    public static void RunAll()
    {
        Console.WriteLine("[Tests] Running StarterArchetypeTests (Task 1.1)...");

        TestAllArchetypesCatalogIntegrity();
        TestJuggernautKitSpecification();
        TestElementalistKitSpecification();
        TestDeadeyeKitSpecification();
        TestArchetypeLookupAndTryGet();
        TestPlayerStateBootstrapFromArchetype();
        TestStartingWeaponAssignment();

        Console.WriteLine("[Tests] All StarterArchetypeTests PASSED successfully!");
    }

    private static void AssertFloat(float expected, float actual, float tolerance = 0.001f, string message = "")
    {
        if (MathF.Abs(expected - actual) > tolerance)
        {
            throw new Exception($"Assertion failed: Expected {expected}, got {actual}. {message}");
        }
    }

    private static void AssertEqual<T>(T expected, T actual, string message = "")
    {
        if (!object.Equals(expected, actual))
        {
            throw new Exception($"Assertion failed: Expected {expected}, got {actual}. {message}");
        }
    }

    private static void AssertTrue(bool condition, string message = "")
    {
        if (!condition)
        {
            throw new Exception($"Assertion failed: Expected true. {message}");
        }
    }

    private static void TestAllArchetypesCatalogIntegrity()
    {
        var all = StarterArchetypeCatalog.All;
        AssertEqual(3, all.Count, "Should define exactly 3 starter archetypes.");

        var distinctIds = all.Select(a => a.Id).Distinct().Count();
        AssertEqual(3, distinctIds, "All archetypes must have unique IDs.");

        foreach (var arch in all)
        {
            AssertTrue(!string.IsNullOrWhiteSpace(arch.DisplayName), $"{arch.Id} must have display name.");
            AssertTrue(!string.IsNullOrWhiteSpace(arch.Subtitle), $"{arch.Id} must have subtitle.");
            AssertTrue(!string.IsNullOrWhiteSpace(arch.Description), $"{arch.Id} must have description.");
            AssertTrue(!string.IsNullOrWhiteSpace(arch.ActiveGemId), $"{arch.Id} must have active gem id.");
            AssertTrue(!string.IsNullOrWhiteSpace(arch.GuaranteedSupportGemId), $"{arch.Id} must have guaranteed support gem id.");
            AssertTrue(arch.InitialMaps.Count >= 3, $"{arch.Id} must start with at least 3 map slates for sustain.");
            AssertTrue(arch.InitialCurrencies.ContainsKey("scroll_of_wisdom"), $"{arch.Id} must include Wisdom scrolls.");
            AssertTrue(arch.InitialCurrencies.ContainsKey("orb_of_transmutation"), $"{arch.Id} must include Transmute orb.");
        }
    }

    private static void TestJuggernautKitSpecification()
    {
        var jugg = StarterArchetypeCatalog.Get(StarterArchetype.Juggernaut);

        AssertEqual("Juggernaut", jugg.DisplayName);
        AssertEqual("Strength", jugg.PrimaryAttribute);
        AssertEqual("#E74C3C", jugg.ColorHex);
        AssertEqual("cleave", jugg.ActiveGemId);
        AssertEqual("added_fire_damage", jugg.GuaranteedSupportGemId);
        AssertFloat(120f, jugg.BaseMaxLife);
        AssertFloat(0f, jugg.BaseMaxEnergyShield);
        AssertFloat(60f, jugg.BaseMaxMana);
        AssertFloat(25f, jugg.BaseDefenses.Armour);
        AssertEqual(3, jugg.InitialCurrencies["scroll_of_wisdom"]);
        AssertEqual(1, jugg.InitialCurrencies["orb_of_transmutation"]);
    }

    private static void TestElementalistKitSpecification()
    {
        var ele = StarterArchetypeCatalog.Get(StarterArchetype.Elementalist);

        AssertEqual("Elementalist", ele.DisplayName);
        AssertEqual("Intelligence", ele.PrimaryAttribute);
        AssertEqual("#3498DB", ele.ColorHex);
        AssertEqual("fireball", ele.ActiveGemId);
        AssertEqual("ignite_proliferation", ele.GuaranteedSupportGemId);
        AssertFloat(90f, ele.BaseMaxLife);
        AssertFloat(45f, ele.BaseMaxEnergyShield);
        AssertFloat(100f, ele.BaseMaxMana);
        AssertFloat(10f, ele.BaseDefenses.FireResistance);
        AssertEqual(3, ele.InitialCurrencies["scroll_of_wisdom"]);
        AssertEqual(1, ele.InitialCurrencies["orb_of_transmutation"]);
    }

    private static void TestDeadeyeKitSpecification()
    {
        var dead = StarterArchetypeCatalog.Get(StarterArchetype.Deadeye);

        AssertEqual("Deadeye", dead.DisplayName);
        AssertEqual("Dexterity", dead.PrimaryAttribute);
        AssertEqual("#2ECC71", dead.ColorHex);
        AssertEqual("split_arrow", dead.ActiveGemId);
        AssertEqual("multiple_projectiles", dead.GuaranteedSupportGemId);
        AssertFloat(105f, dead.BaseMaxLife);
        AssertFloat(0f, dead.BaseMaxEnergyShield);
        AssertFloat(75f, dead.BaseMaxMana);
        AssertFloat(25f, dead.BaseDefenses.EvasionChance);
        AssertEqual(3, dead.InitialCurrencies["scroll_of_wisdom"]);
        AssertEqual(1, dead.InitialCurrencies["orb_of_transmutation"]);
    }

    private static void TestArchetypeLookupAndTryGet()
    {
        AssertTrue(StarterArchetypeCatalog.TryGet("Juggernaut", out var j) && j?.Id == StarterArchetype.Juggernaut);
        AssertTrue(StarterArchetypeCatalog.TryGet("elementalist", out var e) && e?.Id == StarterArchetype.Elementalist);
        AssertTrue(StarterArchetypeCatalog.TryGet("DEADEYE", out var d) && d?.Id == StarterArchetype.Deadeye);

        AssertTrue(!StarterArchetypeCatalog.TryGet("InvalidClass", out _), "Invalid name should return false.");
        AssertTrue(!StarterArchetypeCatalog.TryGet(null, out _), "Null should return false.");
    }

    private static void TestPlayerStateBootstrapFromArchetype()
    {
        var arch = StarterArchetypeCatalog.Get(StarterArchetype.Elementalist);
        var player = new PlayerState();

        player.Initialize(arch.BaseMaxLife, arch.BaseMaxEnergyShield, arch.BaseMaxMana);

        AssertFloat(90f, player.CurrentLife, 0.01f, "Current Life matches definition.");
        AssertFloat(90f, player.MaxLife, 0.01f, "Max Life matches definition.");
        AssertFloat(45f, player.CurrentEnergyShield, 0.01f, "Current ES matches definition.");
        AssertFloat(45f, player.MaxEnergyShield, 0.01f, "Max ES matches definition.");
        AssertFloat(100f, player.CurrentMana, 0.01f, "Current Mana matches definition.");
        AssertFloat(100f, player.MaxMana, 0.01f, "Max Mana matches definition.");
        AssertTrue(player.IsAlive, "Player must be alive on bootstrap.");
    }

    private static void TestStartingWeaponAssignment()
    {
        // 1. Juggernaut -> Cleave -> Dual-Wield Rusted Swords
        var juggDef = StarterArchetypeCatalog.Get(StarterArchetype.Juggernaut);
        var juggPlayer = new PlayerState();
        juggPlayer.BootstrapFromArchetype(juggDef);

        AssertTrue(juggPlayer.MainHand != null, "Juggernaut must start with MainHand weapon.");
        AssertEqual("rusted_sword", juggPlayer.MainHand?.BaseDefinition.Id, "Juggernaut main weapon must be rusted_sword.");
        AssertTrue(juggPlayer.OffHand != null, "Juggernaut must start with OffHand weapon.");
        AssertEqual("rusted_sword", juggPlayer.OffHand?.BaseDefinition.Id, "Juggernaut offhand must be rusted_sword.");
        AssertTrue(juggPlayer.IsDualWielding, "Juggernaut wielding two 1H swords must be dual-wielding.");
        AssertFloat(4f, juggPlayer.MainHand!.ComputedMinDamage, 0.01f);
        AssertFloat(9f, juggPlayer.MainHand!.ComputedMaxDamage, 0.01f);
        AssertFloat(1.45f, juggPlayer.MainHand!.ComputedAps, 0.01f);

        // 2. Elementalist -> Fireball -> Driftwood Wand
        var eleDef = StarterArchetypeCatalog.Get(StarterArchetype.Elementalist);
        var elePlayer = new PlayerState();
        elePlayer.BootstrapFromArchetype(eleDef);

        AssertTrue(elePlayer.MainHand != null, "Elementalist must start with MainHand weapon.");
        AssertEqual("driftwood_wand", elePlayer.MainHand?.BaseDefinition.Id, "Elementalist main weapon must be driftwood_wand.");
        AssertTrue(elePlayer.OffHand == null, "Elementalist off-hand should be empty initially.");
        AssertTrue(!elePlayer.IsDualWielding, "Elementalist must not be dual-wielding.");
        AssertEqual(WeaponClass.Wand, elePlayer.MainHand!.BaseDefinition.WeaponClass);

        // 3. Deadeye -> Split Arrow -> Crude Bow (Two-Handed)
        var deadDef = StarterArchetypeCatalog.Get(StarterArchetype.Deadeye);
        var deadPlayer = new PlayerState();
        deadPlayer.BootstrapFromArchetype(deadDef);

        AssertTrue(deadPlayer.MainHand != null, "Deadeye must start with MainHand weapon.");
        AssertEqual("crude_bow", deadPlayer.MainHand?.BaseDefinition.Id, "Deadeye main weapon must be crude_bow.");
        AssertTrue(deadPlayer.OffHand == null, "Deadeye off-hand should be empty initially.");
        AssertEqual(WeaponClass.Bow, deadPlayer.MainHand!.BaseDefinition.WeaponClass);
        AssertEqual(HandSlotRequirement.TwoHanded, deadPlayer.MainHand!.BaseDefinition.HandRequirement);
    }
}
