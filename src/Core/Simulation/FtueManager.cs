using System;
using IdleExile.Core.Models;

namespace IdleExile.Core.Simulation;

/// <summary>
/// Granular progression steps during the First Time User Experience (FTUE).
/// Ordered sequentially from Step 0 to Step 7.
/// </summary>
public enum FtueStep
{
    /// <summary>Step 0: Player must select 1 of 3 starter archetypes.</summary>
    AwaitingArchetypeSelect = 0,

    /// <summary>Step 1: Archetype chosen, Strand Map T1 placed in Map Device, awaiting [ACTIVATE MAP].</summary>
    AwaitingFirstMapActivate = 1,

    /// <summary>Step 2: Map portal active, character auto-engaging initial beach monster waves.</summary>
    FirstCombatWave = 2,

    /// <summary>Step 3: 12th monster killed; guaranteed Magic item & Transmutation Orb dropped ("Ting!").</summary>
    FirstLootDropped = 3,

    /// <summary>Step 4: Character reached Level 2; Passive Tree tab highlighted for first point allocation.</summary>
    FirstLevelUpReached = 4,

    /// <summary>Step 5: Magic pack defeated; guaranteed Support Gem dropped, awaiting gem socket linking.</summary>
    SupportGemAcquired = 5,

    /// <summary>Step 6: Map progress reached 100%; Map Boss Tidal Behemoth engaged with slam telegraph.</summary>
    MapBossEncounter = 6,

    /// <summary>Step 7: Boss defeated, Map 1 completed; Companion Mode (Always-On-Top Pin & Auto-Map) unlocked.</summary>
    FtueCompleted = 7
}

/// <summary>
/// Pure C# state manager orchestrating the First Time User Experience (FTUE).
/// Tracks the active step, validates linear transitions, stores chosen archetype,
/// and emits events for UI guidance highlights, audio cues, and progressive disclosure.
/// </summary>
public sealed class FtueManager
{
    /// <summary>Current active step in the FTUE lifecycle.</summary>
    public FtueStep CurrentStep { get; private set; } = FtueStep.AwaitingArchetypeSelect;

    /// <summary>The starter archetype selected by the player, null if still at Step 0.</summary>
    public StarterArchetypeDefinition? SelectedArchetype { get; private set; }

    /// <summary>Whether the player has already socketed their guaranteed support gem.</summary>
    public bool HasSocketedSupportGem { get; private set; } = false;

    /// <summary>True once all 7 FTUE steps have been completed.</summary>
    public bool IsCompleted => CurrentStep >= FtueStep.FtueCompleted;

    /// <summary>True while FTUE is still ongoing.</summary>
    public bool IsActive => !IsCompleted;

    // ─────────────────────────────────────────────
    //  Events (Subscribed by UI & Audio Layers)
    // ─────────────────────────────────────────────

    /// <summary>Fired whenever the FTUE step advances. Arg: newStep.</summary>
    public event Action<FtueStep>? OnFtueStepChanged;

    /// <summary>Fired with both previous and new steps for smooth UI state transitions.</summary>
    public event Action<FtueStep, FtueStep>? OnFtueTransition;

    /// <summary>Fired when the player chooses an archetype at Step 0.</summary>
    public event Action<StarterArchetypeDefinition>? OnArchetypeSelected;

    /// <summary>Fired when the player successfully sockets their first Support Gem.</summary>
    public event Action? OnSupportGemSocketed;

    // ─────────────────────────────────────────────
    //  State Transition API
    // ─────────────────────────────────────────────

    /// <summary>
    /// Step 0 → Step 1: Selects an archetype, initializes kit, and prepares the first map.
    /// </summary>
    public bool SelectArchetype(StarterArchetype archetype)
    {
        return SelectArchetype(StarterArchetypeCatalog.Get(archetype));
    }

    /// <summary>
    /// Step 0 → Step 1: Selects an archetype via definition.
    /// </summary>
    public bool SelectArchetype(StarterArchetypeDefinition definition)
    {
        if (CurrentStep != FtueStep.AwaitingArchetypeSelect)
            return false;

        SelectedArchetype = definition ?? throw new ArgumentNullException(nameof(definition));
        OnArchetypeSelected?.Invoke(definition);

        AdvanceTo(FtueStep.AwaitingFirstMapActivate);
        return true;
    }

    /// <summary>
    /// Step 1 → Step 2: Player activates the first Tier 1 Map in the Map Device.
    /// </summary>
    public bool ActivateFirstMap()
    {
        if (CurrentStep != FtueStep.AwaitingFirstMapActivate)
            return false;

        AdvanceTo(FtueStep.FirstCombatWave);
        return true;
    }

    /// <summary>
    /// Step 2 → Step 3: Triggered when the 12th monster drops guaranteed initial loot & currency.
    /// </summary>
    public bool NotifyFirstLootDropped()
    {
        if (CurrentStep != FtueStep.FirstCombatWave)
            return false;

        AdvanceTo(FtueStep.FirstLootDropped);
        return true;
    }

    /// <summary>
    /// Step 3 (or 2) → Step 4: Triggered when character levels up to Level 2.
    /// </summary>
    public bool NotifyLevelUp(int newLevel)
    {
        if (newLevel >= 2 && CurrentStep < FtueStep.FirstLevelUpReached && CurrentStep >= FtueStep.FirstCombatWave)
        {
            AdvanceTo(FtueStep.FirstLevelUpReached);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Step 4 (or prior) → Step 5: Triggered when Magic monster pack drops guaranteed Support Gem.
    /// </summary>
    public bool NotifySupportGemAcquired(string gemId)
    {
        if (CurrentStep < FtueStep.SupportGemAcquired && CurrentStep >= FtueStep.FirstCombatWave)
        {
            AdvanceTo(FtueStep.SupportGemAcquired);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Player links the Support Gem into the Active Gem socket.
    /// </summary>
    public void NotifySupportGemSocketed()
    {
        if (!HasSocketedSupportGem)
        {
            HasSocketedSupportGem = true;
            OnSupportGemSocketed?.Invoke();
        }
    }

    /// <summary>
    /// Step 5 (or prior) → Step 6: Triggered when Map progress reaches 100% and Boss spawns.
    /// </summary>
    public bool NotifyMapBossEncounter()
    {
        if (CurrentStep < FtueStep.MapBossEncounter && CurrentStep >= FtueStep.FirstCombatWave)
        {
            AdvanceTo(FtueStep.MapBossEncounter);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Step 6 → Step 7: Triggered when Map Boss is slain and Map 1 is completed.
    /// </summary>
    public bool NotifyFirstMapCompleted()
    {
        if (CurrentStep < FtueStep.FtueCompleted && CurrentStep >= FtueStep.FirstCombatWave)
        {
            AdvanceTo(FtueStep.FtueCompleted);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Direct step advancement. Ensures monotonic progression (never moves backwards).
    /// </summary>
    public bool TryAdvanceTo(FtueStep targetStep)
    {
        if (targetStep > CurrentStep)
        {
            AdvanceTo(targetStep);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Resets FTUE back to the initial Step 0 state.
    /// </summary>
    public void Reset()
    {
        var prev = CurrentStep;
        CurrentStep = FtueStep.AwaitingArchetypeSelect;
        SelectedArchetype = null;
        HasSocketedSupportGem = false;

        if (prev != CurrentStep)
        {
            OnFtueTransition?.Invoke(prev, CurrentStep);
            OnFtueStepChanged?.Invoke(CurrentStep);
        }
    }

    // ─────────────────────────────────────────────
    //  Guidance & Query Helpers
    // ─────────────────────────────────────────────

    /// <summary>True if the specified step has been reached or surpassed.</summary>
    public bool IsStepReached(FtueStep step) => CurrentStep >= step;

    /// <summary>True if exactly on the specified step.</summary>
    public bool IsCurrent(FtueStep step) => CurrentStep == step;

    /// <summary>
    /// Returns localized, concise contextual guidance text for the current step.
    /// </summary>
    public string GetCurrentInstruction() => CurrentStep switch
    {
        FtueStep.AwaitingArchetypeSelect => "Select a Starter Archetype to awaken your Exile.",
        FtueStep.AwaitingFirstMapActivate => "Click [ACTIVATE MAP] to tear open the Crucible portal.",
        FtueStep.FirstCombatWave => "Watch your character engage monsters automatically. Zero APM combat.",
        FtueStep.FirstLootDropped => "Loot dropped! Currency and gear automatically filter into your bag.",
        FtueStep.FirstLevelUpReached => "Level Up! Open the [Passive Tree] to claim your first power boost.",
        FtueStep.SupportGemAcquired => "Support Gem acquired! Open [Skills] and slot it directly into your Active Gem.",
        FtueStep.MapBossEncounter => "Boss Ahead! Watch for the red slam telegraph circle and guard skills.",
        FtueStep.FtueCompleted => "First Map Cleared! Pin the window and let Idle Exile run as your companion.",
        _ => string.Empty
    };

    private void AdvanceTo(FtueStep newStep)
    {
        var prev = CurrentStep;
        CurrentStep = newStep;

        OnFtueTransition?.Invoke(prev, newStep);
        OnFtueStepChanged?.Invoke(newStep);
    }
}
