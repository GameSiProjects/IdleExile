using System;
using System.Collections.Generic;
using IdleExile.Core.Models;
using IdleExile.Core.Simulation;

namespace IdleExile.Core.Tests;

/// <summary>
/// Unit tests for Task 1.2: FTUE State Tracker & Step Transitions.
/// Validates:
/// - State machine initialization & linear forward transitions
/// - Monotonic progression guards & invalid transition rejection
/// - Event dispatching for step changes, transitions, and archetype selection
/// - Reset behavior and guidance instruction text resolution
/// </summary>
public static class FtueManagerTests
{
    public static void RunAll()
    {
        Console.WriteLine("[Tests] Running FtueManagerTests (Task 1.2)...");

        TestInitialState();
        TestArchetypeSelectionAndGuards();
        TestFullLinearFtueLifecycle();
        TestEventDispatching();
        TestGuidanceInstructions();
        TestResetFunctionality();

        Console.WriteLine("[Tests] All FtueManagerTests PASSED successfully!");
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

    private static void AssertFalse(bool condition, string message = "")
    {
        if (condition)
        {
            throw new Exception($"Assertion failed: Expected false. {message}");
        }
    }

    private static void TestInitialState()
    {
        var ftue = new FtueManager();

        AssertEqual(FtueStep.AwaitingArchetypeSelect, ftue.CurrentStep, "Must start at AwaitingArchetypeSelect.");
        AssertTrue(ftue.SelectedArchetype == null, "Initial selected archetype must be null.");
        AssertFalse(ftue.HasSocketedSupportGem, "HasSocketedSupportGem must start false.");
        AssertTrue(ftue.IsActive, "FTUE must be active initially.");
        AssertFalse(ftue.IsCompleted, "FTUE must not be completed initially.");
    }

    private static void TestArchetypeSelectionAndGuards()
    {
        var ftue = new FtueManager();

        bool selected = ftue.SelectArchetype(StarterArchetype.Elementalist);
        AssertTrue(selected, "Selecting archetype at step 0 must succeed.");
        AssertEqual(FtueStep.AwaitingFirstMapActivate, ftue.CurrentStep, "Step must advance to AwaitingFirstMapActivate.");
        AssertEqual("Elementalist", ftue.SelectedArchetype?.DisplayName);

        // Cannot re-select after advancing
        bool reselected = ftue.SelectArchetype(StarterArchetype.Juggernaut);
        AssertFalse(reselected, "Cannot re-select archetype once past Step 0.");
        AssertEqual(FtueStep.AwaitingFirstMapActivate, ftue.CurrentStep);
    }

    private static void TestFullLinearFtueLifecycle()
    {
        var ftue = new FtueManager();

        // Step 0 -> Step 1
        AssertTrue(ftue.SelectArchetype(StarterArchetype.Juggernaut));
        AssertEqual(FtueStep.AwaitingFirstMapActivate, ftue.CurrentStep);

        // Step 1 -> Step 2
        AssertTrue(ftue.ActivateFirstMap());
        AssertEqual(FtueStep.FirstCombatWave, ftue.CurrentStep);

        // Step 2 -> Step 3
        AssertTrue(ftue.NotifyFirstLootDropped());
        AssertEqual(FtueStep.FirstLootDropped, ftue.CurrentStep);

        // Step 3 -> Step 4
        AssertFalse(ftue.NotifyLevelUp(1), "Level 1 should not trigger step 4.");
        AssertTrue(ftue.NotifyLevelUp(2), "Level 2 should trigger step 4.");
        AssertEqual(FtueStep.FirstLevelUpReached, ftue.CurrentStep);

        // Step 4 -> Step 5
        AssertTrue(ftue.NotifySupportGemAcquired("added_fire_damage"));
        AssertEqual(FtueStep.SupportGemAcquired, ftue.CurrentStep);

        // Socketing gem
        AssertFalse(ftue.HasSocketedSupportGem);
        ftue.NotifySupportGemSocketed();
        AssertTrue(ftue.HasSocketedSupportGem);

        // Step 5 -> Step 6
        AssertTrue(ftue.NotifyMapBossEncounter());
        AssertEqual(FtueStep.MapBossEncounter, ftue.CurrentStep);

        // Step 6 -> Step 7
        AssertTrue(ftue.NotifyFirstMapCompleted());
        AssertEqual(FtueStep.FtueCompleted, ftue.CurrentStep);

        AssertTrue(ftue.IsCompleted, "Must be completed at step 7.");
        AssertFalse(ftue.IsActive, "Must not be active once completed.");
    }

    private static void TestEventDispatching()
    {
        var ftue = new FtueManager();
        var stepChanges = new List<FtueStep>();
        var transitions = new List<(FtueStep from, FtueStep to)>();
        StarterArchetypeDefinition? chosenArch = null;
        bool socketEventFired = false;

        ftue.OnFtueStepChanged += s => stepChanges.Add(s);
        ftue.OnFtueTransition += (from, to) => transitions.Add((from, to));
        ftue.OnArchetypeSelected += a => chosenArch = a;
        ftue.OnSupportGemSocketed += () => socketEventFired = true;

        ftue.SelectArchetype(StarterArchetype.Deadeye);
        ftue.ActivateFirstMap();
        ftue.NotifySupportGemSocketed();

        AssertEqual("Deadeye", chosenArch?.DisplayName);
        AssertEqual(2, stepChanges.Count);
        AssertEqual(FtueStep.AwaitingFirstMapActivate, stepChanges[0]);
        AssertEqual(FtueStep.FirstCombatWave, stepChanges[1]);

        AssertEqual(2, transitions.Count);
        AssertEqual((FtueStep.AwaitingArchetypeSelect, FtueStep.AwaitingFirstMapActivate), transitions[0]);
        AssertEqual((FtueStep.AwaitingFirstMapActivate, FtueStep.FirstCombatWave), transitions[1]);

        AssertTrue(socketEventFired, "Socket event should fire when gem is linked.");
    }

    private static void TestGuidanceInstructions()
    {
        var ftue = new FtueManager();

        foreach (FtueStep step in Enum.GetValues<FtueStep>())
        {
            ftue.TryAdvanceTo(step);
            string instruction = ftue.GetCurrentInstruction();
            AssertTrue(!string.IsNullOrWhiteSpace(instruction), $"Step {step} must have a non-empty guidance instruction.");
        }
    }

    private static void TestResetFunctionality()
    {
        var ftue = new FtueManager();
        ftue.SelectArchetype(StarterArchetype.Juggernaut);
        ftue.ActivateFirstMap();
        ftue.NotifySupportGemSocketed();

        AssertEqual(FtueStep.FirstCombatWave, ftue.CurrentStep);
        AssertTrue(ftue.HasSocketedSupportGem);

        ftue.Reset();

        AssertEqual(FtueStep.AwaitingArchetypeSelect, ftue.CurrentStep);
        AssertTrue(ftue.SelectedArchetype == null);
        AssertFalse(ftue.HasSocketedSupportGem);
        AssertTrue(ftue.IsActive);
    }
}
