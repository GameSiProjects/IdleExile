using System;
using System.IO;
using System.Linq;
using IdleExile.Core.Models;
using IdleExile.Core.State;

namespace IdleExile.Core.Tests;

/// <summary>
/// Unit tests for Task 5.1 & Phase 5: Passive Skill Web models, graph algorithms,
/// allocation rules, refund validation, BFS auto-pathing, and PoE stat arithmetic.
/// Pure C# — runs headlessly with 0 Godot dependencies.
/// </summary>
public static class PassiveTreeSystemTests
{
    public static void RunAll()
    {
        Console.WriteLine("[Tests] Running PassiveTreeSystemTests...");

        TestJsonParsingAndIntegrity();
        TestPointsBudgetAndLevelScaling();
        TestAllocationAndAdjacency();
        TestRefundAndConnectivity();
        TestShortestPathAndAutoPathing();
        TestStatAggregationAndPoeArithmetic();
        TestClassSwitchingAndRespec();

        Console.WriteLine("[Tests] All PassiveTreeSystemTests PASSED successfully!");
    }

    private static PassiveTreeDefinition LoadTestDefinition()
    {
        string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../data/passive_tree.json");
        if (!File.Exists(jsonPath))
        {
            jsonPath = Path.Combine(Directory.GetCurrentDirectory(), "data/passive_tree.json");
        }

        string json = File.ReadAllText(jsonPath);
        return PassiveTreeDefinition.Parse(json);
    }

    private static void TestJsonParsingAndIntegrity()
    {
        var def = LoadTestDefinition();
        AssertNotNull(def, "PassiveTreeDefinition should not be null.");

        var issues = def.Validate();
        AssertEqual(0, issues.Count, $"PassiveTree validation should have 0 issues, but found: {string.Join("; ", issues)}");

        AssertTrue(def.Nodes.Count >= 40, $"Expected >= 40 nodes, found {def.Nodes.Count}");
        AssertTrue(def.Clusters.Count >= 10, $"Expected >= 10 clusters, found {def.Clusters.Count}");
        AssertTrue(def.Connections.Count >= 40, $"Expected >= 40 connections, found {def.Connections.Count}");

        // Verify class start origins
        var marauderStart = def.Nodes.FirstOrDefault(n => n.Id == "start_marauder");
        var witchStart = def.Nodes.FirstOrDefault(n => n.Id == "start_witch");
        var rangerStart = def.Nodes.FirstOrDefault(n => n.Id == "start_ranger");

        AssertNotNull(marauderStart, "Marauder start node must exist");
        AssertNotNull(witchStart, "Witch start node must exist");
        AssertNotNull(rangerStart, "Ranger start node must exist");

        AssertEqual(PassiveNodeType.Start, marauderStart.Type, "Marauder start must be of type Start");
        AssertEqual(PassiveNodeType.Start, witchStart.Type, "Witch start must be of type Start");
        AssertEqual(PassiveNodeType.Start, rangerStart.Type, "Ranger start must be of type Start");

        // Verify iconic Keystones from docs/04_gems_and_passives.md §4.1
        var ci = def.Nodes.FirstOrDefault(n => n.Id == "ks_chaos_inoculation");
        var aof = def.Nodes.FirstOrDefault(n => n.Id == "ks_avatar_of_fire");
        var rt = def.Nodes.FirstOrDefault(n => n.Id == "ks_resolute_technique");
        var ir = def.Nodes.FirstOrDefault(n => n.Id == "ks_iron_reflexes");
        var pb = def.Nodes.FirstOrDefault(n => n.Id == "ks_point_blank");
        var mom = def.Nodes.FirstOrDefault(n => n.Id == "ks_mind_over_matter");

        AssertNotNull(ci, "Chaos Inoculation keystone must exist");
        AssertNotNull(aof, "Avatar of Fire keystone must exist");
        AssertNotNull(rt, "Resolute Technique keystone must exist");
        AssertNotNull(ir, "Iron Reflexes keystone must exist");
        AssertNotNull(pb, "Point Blank keystone must exist");
        AssertNotNull(mom, "Mind Over Matter keystone must exist");

        AssertEqual(PassiveNodeType.Keystone, ci.Type, "CI must be a Keystone");
        AssertEqual(PassiveNodeType.Keystone, aof.Type, "AoF must be a Keystone");
        AssertEqual(PassiveNodeType.Keystone, rt.Type, "RT must be a Keystone");
        AssertEqual(PassiveNodeType.Keystone, ir.Type, "IR must be a Keystone");
        AssertEqual(PassiveNodeType.Keystone, pb.Type, "PB must be a Keystone");
        AssertEqual(PassiveNodeType.Keystone, mom.Type, "MoM must be a Keystone");
    }

    private static void TestPointsBudgetAndLevelScaling()
    {
        var def = LoadTestDefinition();
        var tree = new PassiveTreeState(def);

        AssertEqual(1, tree.CharacterLevel, "Default character level should be 1");
        AssertEqual(0, tree.TotalPoints, "Level 1 should yield 0 spendable points");
        AssertEqual(0, tree.SpentPoints, "Start node is free, so 0 spent points");
        AssertEqual(0, tree.AvailablePoints, "0 available points at Level 1");

        tree.SetCharacterLevel(2);
        AssertEqual(1, tree.TotalPoints, "Level 2 should yield 1 point");
        AssertEqual(1, tree.AvailablePoints, "Level 2 with 0 spent should have 1 available point");

        tree.SetCharacterLevel(72);
        AssertEqual(71, tree.TotalPoints, "Level 72 should yield 71 points");
        AssertEqual(71, tree.AvailablePoints, "71 available points");
    }

    private static void TestAllocationAndAdjacency()
    {
        var def = LoadTestDefinition();
        var tree = new PassiveTreeState(def);
        tree.SetStartingClass("start_witch");
        tree.SetCharacterLevel(1);

        // Cannot allocate because available points == 0
        AssertFalse(tree.CanAllocate("witch_spell_1"), "Cannot allocate without points");

        tree.SetCharacterLevel(3); // 2 points
        AssertTrue(tree.CanAllocate("witch_spell_1"), "Should be able to allocate adjacent node with available points");

        // Non-adjacent node cannot be allocated
        AssertFalse(tree.CanAllocate("witch_notable_arcane"), "Cannot allocate non-adjacent node");
        AssertFalse(tree.CanAllocate("start_marauder"), "Cannot allocate a Start node");

        // Allocate witch_spell_1
        bool success = tree.Allocate("witch_spell_1");
        AssertTrue(success, "Allocation of witch_spell_1 should succeed");
        AssertTrue(tree.IsAllocated("witch_spell_1"), "witch_spell_1 must be marked allocated");
        AssertEqual(1, tree.SpentPoints, "Spent points should be 1");
        AssertEqual(1, tree.AvailablePoints, "Available points should be 1");

        // Now witch_spell_2 and witch_es_1 become adjacent
        AssertTrue(tree.CanAllocate("witch_spell_2"), "witch_spell_2 is now adjacent and can be allocated");
        AssertTrue(tree.CanAllocate("witch_es_1"), "witch_es_1 is now adjacent and can be allocated");

        // Allocate witch_spell_2
        tree.Allocate("witch_spell_2");
        AssertEqual(2, tree.SpentPoints, "Spent points should be 2");
        AssertEqual(0, tree.AvailablePoints, "Available points should be 0");

        // No more points
        AssertFalse(tree.CanAllocate("witch_notable_arcane"), "Cannot allocate notable without points");
    }

    private static void TestRefundAndConnectivity()
    {
        var def = LoadTestDefinition();
        var tree = new PassiveTreeState(def);
        tree.SetStartingClass("start_witch");
        tree.SetCharacterLevel(10); // Plenty of points

        tree.Allocate("witch_spell_1");
        tree.Allocate("witch_spell_2");
        tree.Allocate("witch_notable_arcane");

        // Start node cannot be refunded
        AssertFalse(tree.CanRefund("start_witch"), "Start node cannot be refunded");

        // Leaf node can be refunded
        AssertTrue(tree.CanRefund("witch_notable_arcane"), "Leaf node can be refunded");

        // Intermediary node cannot be refunded if it breaks connection
        AssertFalse(tree.CanRefund("witch_spell_1"), "witch_spell_1 is an articulation point and cannot be refunded");
        AssertFalse(tree.CanRefund("witch_spell_2"), "witch_spell_2 connects notable to spell_1 and cannot be refunded");

        // Now provide an alternate path: witch_spell_1 -> witch_es_1 -> witch_notable_arcane
        tree.Allocate("witch_es_1");
        // Now witch_notable_arcane is connected via BOTH spell_2 and es_1!
        // So witch_spell_2 CAN now be refunded without disconnecting the notable!
        AssertTrue(tree.CanRefund("witch_spell_2"), "witch_spell_2 should be refundable because witch_es_1 maintains connectivity");

        bool refunded = tree.Refund("witch_spell_2");
        AssertTrue(refunded, "Refunding witch_spell_2 should succeed");
        AssertFalse(tree.IsAllocated("witch_spell_2"), "witch_spell_2 should no longer be allocated");
        AssertTrue(tree.IsAllocated("witch_notable_arcane"), "witch_notable_arcane should still be allocated via witch_es_1");
    }

    private static void TestShortestPathAndAutoPathing()
    {
        var def = LoadTestDefinition();
        var tree = new PassiveTreeState(def);
        tree.SetStartingClass("start_witch");
        tree.SetCharacterLevel(10);

        // Find path to CI
        var path = tree.FindPath("ks_chaos_inoculation");
        AssertNotNull(path, "Path to CI from Witch start must exist");
        AssertTrue(path.Count >= 3, $"Path to CI should take at least 3 steps, was {path.Count}");
        AssertEqual("witch_spell_1", path[0], "First step towards CI should be witch_spell_1");
        AssertEqual("ks_chaos_inoculation", path[^1], "Target must be the last step in path");

        // Path to already allocated node returns empty list
        var pathSelf = tree.FindPath("start_witch");
        AssertNotNull(pathSelf, "Path to self should not be null");
        AssertEqual(0, pathSelf.Count, "Path to already allocated node must be empty");

        // Path cost test
        int cost = tree.GetPathCost("ks_chaos_inoculation");
        AssertEqual(path.Count, cost, "GetPathCost must match path.Count");

        // Auto-pathing: AllocatePath
        tree.SetCharacterLevel(path.Count + 1); // Exact points needed (Level - 1 = path.Count)
        bool allocated = tree.AllocatePath("ks_chaos_inoculation");
        AssertTrue(allocated, "AllocatePath should succeed when points are sufficient");
        AssertTrue(tree.IsAllocated("ks_chaos_inoculation"), "CI must be allocated");
        AssertEqual(0, tree.AvailablePoints, "All available points should have been spent");

        // Try allocating unreachable or unaffordable target
        tree.SetStartingClass("start_witch");
        tree.SetCharacterLevel(2); // Only 1 point
        bool failedAfford = tree.AllocatePath("ks_chaos_inoculation");
        AssertFalse(failedAfford, "AllocatePath should fail when points are insufficient");
        AssertFalse(tree.IsAllocated("witch_spell_1"), "AllocatePath should make no partial changes on failure");
    }

    private static void TestStatAggregationAndPoeArithmetic()
    {
        var def = LoadTestDefinition();
        var tree = new PassiveTreeState(def);
        tree.SetStartingClass("start_witch");
        tree.SetCharacterLevel(10);

        // witch_spell_1: 10% inc spell damage
        // witch_spell_2: 10% inc spell damage
        // witch_notable_arcane: 20% inc spell damage, 12% inc max ES
        tree.Allocate("witch_spell_1");
        tree.Allocate("witch_spell_2");
        tree.Allocate("witch_notable_arcane");

        var totals = tree.GetStatTotals();
        var spellDmgTotal = totals.FirstOrDefault(t => t.Stat == "spell_damage" && t.Mod == PassiveModType.Increased);
        var esTotal = totals.FirstOrDefault(t => t.Stat == "energy_shield" && t.Mod == PassiveModType.Increased);

        AssertNotNull(spellDmgTotal, "Spell damage total should exist");
        AssertNotNull(esTotal, "ES total should exist");

        AssertEqual(40f, spellDmgTotal.Value, "Spell damage should be 10 + 10 + 20 = 40% increased");
        AssertEqual(12f, esTotal.Value, "ES should be 12% increased");

        // Verify StatModifierBucket calculation
        var spellBucket = tree.BuildStatBucket("spell_damage");
        AssertEqual(40f, spellBucket.IncreasedSum, "Increased sum should be 40%");
        AssertEqual(1.40f, spellBucket.CalculateMultiplier(), "Multiplier should be 1 + 0.40 = 1.40x");

        // Verify formatting
        AssertTrue(spellDmgTotal.DisplayText.Contains("40% increased"), $"Formatted text should contain '40% increased', was '{spellDmgTotal.DisplayText}'");
    }

    private static void TestClassSwitchingAndRespec()
    {
        var def = LoadTestDefinition();
        var tree = new PassiveTreeState(def);
        tree.SetStartingClass("start_marauder");
        tree.SetCharacterLevel(5);

        tree.Allocate("mar_life_1");
        tree.Allocate("mar_armour_1");
        AssertEqual(2, tree.SpentPoints, "2 points spent on Marauder");

        // Switch class to Ranger
        bool switched = tree.SetStartingClassByAttribute("Dexterity");
        AssertTrue(switched, "SetStartingClassByAttribute Dexterity should switch to Ranger");
        AssertEqual("start_ranger", tree.StartNodeId, "StartNodeId should now be start_ranger");
        AssertEqual(0, tree.SpentPoints, "Switching class should reset all allocated passives");
        AssertTrue(tree.IsAllocated("start_ranger"), "Ranger start must be allocated");
        AssertFalse(tree.IsAllocated("mar_life_1"), "Previous Marauder nodes must be wiped");
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!System.Collections.Generic.EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"Assertion Failed: Expected '{expected}', but was '{actual}'. {message}");
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition) throw new Exception($"Assertion Failed: {message}");
    }

    private static void AssertFalse(bool condition, string message)
    {
        if (condition) throw new Exception($"Assertion Failed: {message}");
    }

    private static void AssertNotNull([System.Diagnostics.CodeAnalysis.NotNull] object? obj, string message)
    {
        if (obj == null) throw new Exception($"Assertion Failed: {message} (Object was null)");
    }
}
