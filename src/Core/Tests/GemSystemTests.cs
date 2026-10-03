using System;
using System.Collections.Generic;
using System.Linq;
using IdleExile.Core.Models;
using IdleExile.Core.State;

namespace IdleExile.Core.Tests;

/// <summary>
/// Unit test suite verifying the PoE 2 Decoupled Gem System (Task 3.1 & Phase 3 Core):
/// - Active Gems contain up to 5 Support slots.
/// - 100% Auto-Linking based on tag compatibility.
/// - Compounded damage multiplier prod(1 + More) * prod(1 - Less).
/// - Compounded effective mana cost.
/// - Multi-slot loadout matrix (Primary, Secondary, Trigger, Auras).
/// Pure C# — zero Godot dependencies.
/// </summary>
public static class GemSystemTests
{
    public static void RunAll()
    {
        Console.WriteLine("[Tests] Running GemSystemTests (Task 3.1 PoE 2 Gem System)...");

        TestDecoupledGemModelSockets();
        TestAutoLinkingAndTagCompatibility();
        TestCompoundedDamageMultiplier();
        TestCompoundedEffectiveManaCost();
        TestMultiSlotLoadout();
        TestSocketGemAtAndReplacement();

        Console.WriteLine("[Tests] All GemSystemTests PASSED successfully!");
    }

    private static void TestDecoupledGemModelSockets()
    {
        var setup = new SkillSocketSetup();
        AssertNull(setup.ActiveGem, "Initial active gem should be null");
        AssertEqual(0, setup.SocketedSupports.Count, "Initial support gems should be empty");

        var fireball = new GemDefinition(
            Id: "fireball",
            Name: "Fireball",
            Category: "Active",
            Color: "Blue",
            Tags: new[] { "spell", "projectile", "fire", "aoe" },
            RequiredTags: null,
            BaseStats: new Dictionary<string, object>
            {
                ["baseDamageMin"] = 25f,
                ["baseDamageMax"] = 38f,
                ["manaCost"] = 8f,
                ["castTime"] = 0.75f
            },
            Modifiers: null,
            Description: "Unleashes a ball of fire."
        );

        // Cannot add Active gem as Support
        bool invalidSupport = setup.AddSupportGem(fireball);
        AssertFalse(invalidSupport, "Cannot add Active gem as Support gem");

        // Set Active Gem
        bool activeSet = setup.SetActiveGem(fireball);
        AssertTrue(activeSet, "Active gem should be set successfully");
        AssertEqual("Fireball", setup.ActiveGem?.Name);
        AssertTrue(setup.IsSpell, "Fireball should have IsSpell = true");
        AssertTrue(setup.IsProjectile, "Fireball should have IsProjectile = true");

        // Add up to 5 supports
        for (int i = 0; i < 5; i++)
        {
            var sup = new GemDefinition(
                Id: $"sup_{i}",
                Name: $"Support {i}",
                Category: "Support",
                Color: "Green",
                Tags: null,
                RequiredTags: new[] { "projectile" },
                BaseStats: null,
                Modifiers: new Dictionary<string, object> { ["damageMultiplier"] = 10f },
                Description: "Support gem"
            );
            bool added = setup.AddSupportGem(sup);
            AssertTrue(added, $"Support gem {i} should be added");
        }

        AssertEqual(5, setup.SocketedSupports.Count, "Setup should hold exactly 5 support gems");

        // 6th support should be rejected (PoE 2 max 5 supports on Active gem)
        var extraSup = new GemDefinition("sup_extra", "Extra Support", "Support", "Blue", null, null, null, null, "Extra");
        bool extraAdded = setup.AddSupportGem(extraSup);
        AssertFalse(extraAdded, "6th support gem must be rejected (max 5 supports)");
    }

    private static void TestAutoLinkingAndTagCompatibility()
    {
        var cleave = new GemDefinition(
            Id: "cleave",
            Name: "Cleave",
            Category: "Active",
            Color: "Red",
            Tags: new[] { "attack", "melee", "aoe", "physical" },
            RequiredTags: null,
            BaseStats: new Dictionary<string, object>
            {
                ["damageMultiplier"] = 125.0f,
                ["manaCost"] = 6.0f
            },
            Modifiers: null,
            Description: "Cleave"
        );

        var meleePhys = new GemDefinition(
            Id: "melee_phys",
            Name: "Melee Physical Damage Support",
            Category: "Support",
            Color: "Red",
            Tags: null,
            RequiredTags: new[] { "melee", "physical", "attack" },
            BaseStats: null,
            Modifiers: new Dictionary<string, object>
            {
                ["morePhysicalDamagePercent"] = 35.0f,
                ["manaMultiplier"] = 130f
            },
            Description: "35% more melee phys"
        );

        var gmp = new GemDefinition(
            Id: "gmp",
            Name: "Greater Multiple Projectiles Support",
            Category: "Support",
            Color: "Green",
            Tags: null,
            RequiredTags: new[] { "projectile" },
            BaseStats: null,
            Modifiers: new Dictionary<string, object>
            {
                ["extraProjectiles"] = 4,
                ["damageMultiplier"] = -25.0f,
                ["manaMultiplier"] = 150f
            },
            Description: "4 extra projectiles"
        );

        var setup = new SkillSocketSetup(cleave);
        setup.AddSupportGem(meleePhys);
        setup.AddSupportGem(gmp);

        // Cleave has ["attack", "melee", "aoe", "physical"]
        // meleePhys requires ["melee", "physical", "attack"] -> MATCH!
        AssertTrue(setup.IsSupportCompatible(meleePhys), "Melee Physical Damage must be compatible with Cleave");

        // gmp requires ["projectile"] -> Cleave DOES NOT have projectile -> MISMATCH!
        AssertFalse(setup.IsSupportCompatible(gmp), "GMP must NOT be compatible with Cleave");

        // Only valid linked supports should be counted
        var validSupports = setup.ValidLinkedSupports.ToList();
        AssertEqual(1, validSupports.Count, "Only meleePhys should be in ValidLinkedSupports");
        AssertEqual("Melee Physical Damage Support", validSupports[0].Name);
    }

    private static void TestCompoundedDamageMultiplier()
    {
        var fireball = new GemDefinition(
            Id: "fireball",
            Name: "Fireball",
            Category: "Active",
            Color: "Blue",
            Tags: new[] { "spell", "projectile", "fire", "aoe" },
            RequiredTags: null,
            BaseStats: new Dictionary<string, object>
            {
                ["damageMultiplier"] = 100f,
                ["manaCost"] = 10f
            },
            Modifiers: null,
            Description: "Fireball"
        );

        var igniteProlif = new GemDefinition(
            Id: "ignite_prolif",
            Name: "Ignite Proliferation Support",
            Category: "Support",
            Color: "Blue",
            Tags: null,
            RequiredTags: new[] { "fire" },
            BaseStats: null,
            Modifiers: new Dictionary<string, object>
            {
                ["moreFireDamagePercent"] = 20.0f // +20% More
            },
            Description: "20% More Fire"
        );

        var gmp = new GemDefinition(
            Id: "gmp",
            Name: "GMP Support",
            Category: "Support",
            Color: "Green",
            Tags: null,
            RequiredTags: new[] { "projectile" },
            BaseStats: null,
            Modifiers: new Dictionary<string, object>
            {
                ["damageMultiplier"] = -25.0f // -25% Less
            },
            Description: "25% Less"
        );

        var setup = new SkillSocketSetup(fireball);
        setup.AddSupportGem(igniteProlif);
        setup.AddSupportGem(gmp);

        // Expected multiplier: 1.0 * (1 + 0.20) * (1 - 0.25) = 1.20 * 0.75 = 0.90
        float totalMult = setup.CalculateTotalDamageMultiplier();
        AssertTrue(MathF.Abs(totalMult - 0.90f) < 0.001f, $"Damage multiplier should be 0.90, was {totalMult}");
    }

    private static void TestCompoundedEffectiveManaCost()
    {
        var fireball = new GemDefinition(
            Id: "fireball",
            Name: "Fireball",
            Category: "Active",
            Color: "Blue",
            Tags: new[] { "spell", "projectile", "fire" },
            RequiredTags: null,
            BaseStats: new Dictionary<string, object>
            {
                ["manaCost"] = 10.0f
            },
            Modifiers: null,
            Description: "Fireball"
        );

        var sup1 = new GemDefinition(
            Id: "sup1",
            Name: "Support 1",
            Category: "Support",
            Color: "Green",
            Tags: null,
            RequiredTags: new[] { "projectile" },
            BaseStats: null,
            Modifiers: new Dictionary<string, object> { ["manaMultiplier"] = 140f },
            Description: "140% Mana"
        );

        var sup2 = new GemDefinition(
            Id: "sup2",
            Name: "Support 2",
            Category: "Support",
            Color: "Blue",
            Tags: null,
            RequiredTags: new[] { "spell" },
            BaseStats: null,
            Modifiers: new Dictionary<string, object> { ["manaMultiplier"] = 130f },
            Description: "130% Mana"
        );

        var setup = new SkillSocketSetup(fireball);
        setup.AddSupportGem(sup1);
        setup.AddSupportGem(sup2);

        // Formula: 10 * 1.40 * 1.30 = 18.2
        float effectiveMana = setup.CalculateEffectiveManaCost();
        AssertTrue(MathF.Abs(effectiveMana - 18.2f) < 0.01f, $"Effective mana cost should be 18.2, was {effectiveMana}");
    }

    private static void TestMultiSlotLoadout()
    {
        var loadout = new SkillLoadout();

        var cleave = new GemDefinition("cleave", "Cleave", "Active", "Red", new[] { "attack", "melee" }, null, null, null, "Cleave");
        var fireball = new GemDefinition("fireball", "Fireball", "Active", "Blue", new[] { "spell", "projectile" }, null, null, null, "Fireball");

        loadout.GetSetupAt(0).SetActiveGem(cleave);
        loadout.GetSetupAt(1).SetActiveGem(fireball);

        AssertEqual("Cleave", loadout.PrimarySkill.ActiveGem?.Name);
        AssertEqual("Fireball", loadout.SecondarySkill.ActiveGem?.Name);
        AssertNull(loadout.TriggerSkill.ActiveGem, "Trigger skill active gem should be null");

        AssertEqual("Primary DPS", loadout.GetSlotName(0));
        AssertEqual("Secondary / Cooldown", loadout.GetSlotName(1));
        AssertEqual("Trigger / Guard", loadout.GetSlotName(2));
    }

    private static void TestSocketGemAtAndReplacement()
    {
        var setup = new SkillSocketSetup();
        var sup0 = new GemDefinition("s0", "Support 0", "Support", "Red", null, null, null, null, "S0");
        var sup1 = new GemDefinition("s1", "Support 1", "Support", "Green", null, null, null, null, "S1");
        var supReplacement = new GemDefinition("s_rep", "Replacement", "Support", "Blue", null, null, null, null, "Rep");

        setup.AddSupportGem(sup0);
        setup.AddSupportGem(sup1);

        AssertEqual(2, setup.SocketedSupports.Count);
        AssertEqual("Support 0", setup.SocketedSupports[0].Name);

        // Replace support at slot 0
        bool replaced = setup.SetSupportGemAt(0, supReplacement);
        AssertTrue(replaced, "Support gem at index 0 should be replaced");
        AssertEqual("Replacement", setup.SocketedSupports[0].Name);
        AssertEqual("Support 1", setup.SocketedSupports[1].Name);
    }

    // ─── Simple Assertion Helpers ───

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"[Assertion Failed] Expected true: {message}");
    }

    private static void AssertFalse(bool condition, string message)
    {
        if (condition) throw new InvalidOperationException($"[Assertion Failed] Expected false: {message}");
    }

    private static void AssertEqual<T>(T expected, T actual, string? message = null)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"[Assertion Failed] Expected '{expected}', but was '{actual}'. {message}");
    }

    private static void AssertNull(object? obj, string message)
    {
        if (obj != null) throw new InvalidOperationException($"[Assertion Failed] Expected null: {message}");
    }
}
