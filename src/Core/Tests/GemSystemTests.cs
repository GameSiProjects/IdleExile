using System;
using System.Collections.Generic;
using System.Linq;
using IdleExile.Core.Models;
using IdleExile.Core.Services;
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
        Console.WriteLine("[Tests] Running GemSystemTests (Task 3.1 & Task 3.2 PoE 2 Gem System)...");

        TestDecoupledGemModelSockets();
        TestAutoLinkingAndTagCompatibility();
        TestCompoundedDamageMultiplier();
        TestCompoundedEffectiveManaCost();
        TestMultiSlotLoadout();
        TestSocketGemAtAndReplacement();
        TestGemCompatibilityValidatorArchetypeRules();
        TestCompoundedDamageMultiplierFormulaBreakdown();

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

    private static void TestGemCompatibilityValidatorArchetypeRules()
    {
        var cleave = new GemDefinition("cleave", "Cleave", "Active", "Red", new[] { "attack", "melee", "aoe", "physical" }, null, null, null, "Cleave");
        var fireball = new GemDefinition("fireball", "Fireball", "Active", "Blue", new[] { "spell", "projectile", "fire", "aoe" }, null, null, null, "Fireball");
        var splitArrow = new GemDefinition("split_arrow", "Split Arrow", "Active", "Green", new[] { "attack", "bow", "projectile", "physical" }, null, null, null, "Split Arrow");

        var meleePhys = new GemDefinition("melee_phys", "Melee Physical Damage Support", "Support", "Red", null, new[] { "melee", "physical", "attack" }, null, new Dictionary<string, object> { ["morePhysicalDamagePercent"] = 35f }, "Melee Phys");
        var gmp = new GemDefinition("gmp", "GMP Support", "Support", "Green", null, new[] { "projectile" }, null, new Dictionary<string, object> { ["damageMultiplier"] = -25f }, "GMP");
        var concEffect = new GemDefinition("conc", "Concentrated Effect Support", "Support", "Blue", null, new[] { "aoe" }, null, new Dictionary<string, object> { ["moreAoeDamagePercent"] = 30f }, "Conc Effect");
        var addedFire = new GemDefinition("added_fire", "Added Fire Support", "Support", "Red", null, new[] { "physical", "attack", "spell" }, null, new Dictionary<string, object> { ["manaMultiplier"] = 120f }, "Added Fire");

        // 1. Melee Physical Damage Support: ONLY compatible with Cleave!
        var cleaveMelee = GemCompatibilityValidator.ValidateCompatibility(cleave, meleePhys);
        AssertTrue(cleaveMelee.IsCompatible, "Melee Physical Damage must be compatible with Cleave");
        AssertTrue(cleaveMelee.MatchingTags.Contains("melee"), "Cleave matches melee tag");

        var fireballMelee = GemCompatibilityValidator.ValidateCompatibility(fireball, meleePhys);
        AssertFalse(fireballMelee.IsCompatible, "Melee Physical Damage must NOT be compatible with Fireball (Spell)");

        var bowMelee = GemCompatibilityValidator.ValidateCompatibility(splitArrow, meleePhys);
        AssertFalse(bowMelee.IsCompatible, "Melee Physical Damage must NOT be compatible with Split Arrow (Bow Attack lacks Melee)");

        // 2. GMP: Compatible with Fireball and Split Arrow, NOT with Cleave
        AssertTrue(GemCompatibilityValidator.IsCompatible(fireball, gmp), "GMP must be compatible with Fireball");
        AssertTrue(GemCompatibilityValidator.IsCompatible(splitArrow, gmp), "GMP must be compatible with Split Arrow");
        AssertFalse(GemCompatibilityValidator.IsCompatible(cleave, gmp), "GMP must NOT be compatible with Cleave (No Projectile)");

        // 3. Concentrated Effect: Compatible with Fireball and Cleave (both AoE), NOT with Split Arrow
        AssertTrue(GemCompatibilityValidator.IsCompatible(fireball, concEffect), "Conc Effect must be compatible with Fireball (AoE)");
        AssertTrue(GemCompatibilityValidator.IsCompatible(cleave, concEffect), "Conc Effect must be compatible with Cleave (AoE)");
        AssertFalse(GemCompatibilityValidator.IsCompatible(splitArrow, concEffect), "Conc Effect must NOT be compatible with Split Arrow (No AoE)");

        // 4. Added Fire: Compatible with all three
        AssertTrue(GemCompatibilityValidator.IsCompatible(fireball, addedFire), "Added Fire compatible with Fireball (Spell)");
        AssertTrue(GemCompatibilityValidator.IsCompatible(cleave, addedFire), "Added Fire compatible with Cleave (Attack/Phys)");
        AssertTrue(GemCompatibilityValidator.IsCompatible(splitArrow, addedFire), "Added Fire compatible with Split Arrow (Attack/Phys)");
    }

    private static void TestCompoundedDamageMultiplierFormulaBreakdown()
    {
        var cleave = new GemDefinition(
            Id: "cleave",
            Name: "Cleave",
            Category: "Active",
            Color: "Red",
            Tags: new[] { "attack", "melee", "aoe", "physical" },
            RequiredTags: null,
            BaseStats: new Dictionary<string, object> { ["damageMultiplier"] = 125f },
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
            Modifiers: new Dictionary<string, object> { ["morePhysicalDamagePercent"] = 35f },
            Description: "35% More Phys"
        );

        var concEffect = new GemDefinition(
            Id: "conc",
            Name: "Concentrated Effect Support",
            Category: "Support",
            Color: "Blue",
            Tags: null,
            RequiredTags: new[] { "aoe" },
            BaseStats: null,
            Modifiers: new Dictionary<string, object> { ["moreAoeDamagePercent"] = 30f },
            Description: "30% More AoE"
        );

        var gmp = new GemDefinition(
            Id: "gmp",
            Name: "GMP Support",
            Category: "Support",
            Color: "Green",
            Tags: null,
            RequiredTags: new[] { "projectile" },
            BaseStats: null,
            Modifiers: new Dictionary<string, object> { ["damageMultiplier"] = -25f },
            Description: "25% Less"
        );

        // 1. Cleave + Melee Phys: 1.25 * 1.35 = 1.6875 (~1.69x, +69% More)
        var setup = new SkillSocketSetup(cleave);
        setup.AddSupportGem(meleePhys);

        var breakdown1 = setup.GetDamageMultiplierBreakdown();
        AssertTrue(MathF.Abs(breakdown1.TotalMultiplier - 1.6875f) < 0.001f, $"Breakdown1 multiplier expected 1.6875, was {breakdown1.TotalMultiplier}");
        AssertTrue(breakdown1.FormulaString.Contains("1.25"), "Formula string should contain 1.25");
        AssertTrue(breakdown1.FormulaString.Contains("1.35"), "Formula string should contain 1.35");
        AssertTrue(breakdown1.MathRepresentation.Contains("∏(1 + More)"), "Math representation should contain ∏(1 + More)");

        // 2. Add Conc Effect: 1.25 * 1.35 * 1.30 = 2.19375 (~2.19x, +119% More)
        setup.AddSupportGem(concEffect);
        var breakdown2 = setup.GetDamageMultiplierBreakdown();
        AssertTrue(MathF.Abs(breakdown2.TotalMultiplier - 2.19375f) < 0.001f, $"Breakdown2 multiplier expected 2.19375, was {breakdown2.TotalMultiplier}");

        // 3. Add Incompatible GMP (Cleave lacks projectile): Incompatible support MUST NOT affect multiplier!
        setup.AddSupportGem(gmp);
        var breakdown3 = setup.GetDamageMultiplierBreakdown();
        AssertTrue(MathF.Abs(breakdown3.TotalMultiplier - 2.19375f) < 0.001f, "Incompatible GMP must not alter damage multiplier");

        // 4. Preview support impact
        var preview = GemCompatibilityValidator.PreviewSupportImpact(new SkillSocketSetup(cleave), meleePhys);
        AssertTrue(MathF.Abs(preview.CurrentBreakdown.TotalMultiplier - 1.25f) < 0.001f, "Current breakdown should be 1.25");
        AssertTrue(MathF.Abs(preview.ProjectedBreakdown.TotalMultiplier - 1.6875f) < 0.001f, "Projected breakdown should be 1.6875");
        AssertTrue(preview.IsIncrease, "Preview should be an increase");
        AssertTrue(MathF.Abs(preview.DeltaMultiplier - 0.4375f) < 0.001f, "Delta multiplier should be 0.4375");
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
