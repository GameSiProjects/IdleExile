using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleExile.Core.Models;

/// <summary>
/// Formations for monster packs deployed sequentially along the linear world corridor.
/// Conforms to docs/07_combat_ui_and_viewport.md Section 3.2.D and Roadmap Task 2.7.
/// </summary>
public enum CorridorPackFormationType
{
    /// <summary>Pack 1 variant: Line of trash mobs spread horizontally across the corridor blocking the way.</summary>
    VanguardLine = 0,

    /// <summary>Pack 1 variant: V-shaped wedge with apex pointed toward the advancing Exile.</summary>
    VanguardVee = 1,

    /// <summary>Pack 2: Rare Commander with golden aura at center-rear protected by a defensive perimeter of minions.</summary>
    CommanderPhalanx = 2,

    /// <summary>Pack 3: Highly mobile ambushers hugging the left and right walls to pinch the Exile in a pincer trap.</summary>
    FlankerPincer = 3,

    /// <summary>Pack 4: Elite shield wall guarding the corridor right before the Fog Gate (85% map length).</summary>
    GatePhalanx = 4,

    /// <summary>Special League Encounter: Monsters positioned around roadside monoliths, breach crevices, or ore veins.</summary>
    EncounterAmbush = 5,

    /// <summary>Final Chamber: Map Boss with flanking minion retinue in the end-of-map arena.</summary>
    BossChamber = 6
}

/// <summary>
/// Types of League Encounters that appear along the roadside during corridor mapping.
/// </summary>
public enum LeagueEncounterType
{
    None = 0,

    /// <summary>
    /// Essence Monolith: A Rare monster encased in shimmering ice/crystal alongside minions.
    /// Dormant until the Exile approaches within trigger range (120px), then shatters and attacks with Essence mods.
    /// </summary>
    EssenceMonolith = 1,

    /// <summary>
    /// Breach Crevice: A clasped void hand emerging from the ground.
    /// When triggered within 130px, an expanding purple ring opens and unleashes ravenous Breachlings.
    /// </summary>
    BreachCrevice = 2,

    /// <summary>
    /// Voltaxic Sulphite Vein: Rich golden ore deposit mined on approach, granting buffs/charges and calling subterranean beasts.
    /// </summary>
    SulphiteVein = 3
}

/// <summary>
/// Lifecycle state of a roadside League Encounter.
/// </summary>
public enum LeagueEncounterState
{
    /// <summary>Sealed / imprisoned / waiting for Exile to approach.</summary>
    Dormant = 0,

    /// <summary>Triggered! Pack awakened, active combat underway.</summary>
    Active = 1,

    /// <summary>Defeated or collected; bonus rewards granted.</summary>
    Completed = 2
}

/// <summary>
/// Runtime instance of a roadside League Encounter situated along the corridor.
/// Pure C# model — zero Godot dependencies.
/// </summary>
public sealed class LeagueEncounterInstance
{
    /// <summary>Type of the league mechanic.</summary>
    public LeagueEncounterType Type { get; }

    /// <summary>Display name (e.g. "Weeping Essence of Hatred", "Clasped Breach Hand", "Voltaxic Sulphite Deposit").</summary>
    public string Name { get; }

    /// <summary>World position along the corridor roadside.</summary>
    public Vector2D Position { get; }

    /// <summary>Activation proximity radius in world pixels (Exile triggers encounter when dist &lt;= TriggerRadius).</summary>
    public float TriggerRadius { get; }

    /// <summary>Current state of the encounter.</summary>
    public LeagueEncounterState State { get; private set; } = LeagueEncounterState.Dormant;

    /// <summary>Special mod name if applicable (e.g. "Essence of Hatred: +35% Extra Cold Damage").</summary>
    public string? EssenceModifierName { get; }

    /// <summary>Description of the bonus reward granted upon activation or clear.</summary>
    public string RewardDescription { get; }

    /// <summary>Fired when the encounter transitions from Dormant to Active.</summary>
    public event Action<LeagueEncounterInstance>? OnTriggered;

    /// <summary>Fired when the encounter is cleared.</summary>
    public event Action<LeagueEncounterInstance>? OnCompleted;

    public LeagueEncounterInstance(
        LeagueEncounterType type,
        string name,
        Vector2D position,
        float triggerRadius = 120f,
        string? essenceModifierName = null,
        string rewardDescription = "")
    {
        Type = type;
        Name = name;
        Position = position;
        TriggerRadius = triggerRadius;
        EssenceModifierName = essenceModifierName;
        RewardDescription = rewardDescription;
    }

    /// <summary>
    /// Checks proximity to the Exile. If within TriggerRadius and currently Dormant, transitions to Active.
    /// </summary>
    public bool TryTrigger(Vector2D exilePos)
    {
        if (State != LeagueEncounterState.Dormant)
            return false;

        float dist = Position.DistanceTo(exilePos);
        if (dist <= TriggerRadius)
        {
            State = LeagueEncounterState.Active;
            OnTriggered?.Invoke(this);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Forces immediate trigger (e.g. for testing or explicit player click).
    /// </summary>
    public void ForceTrigger()
    {
        if (State == LeagueEncounterState.Dormant)
        {
            State = LeagueEncounterState.Active;
            OnTriggered?.Invoke(this);
        }
    }

    /// <summary>
    /// Marks the encounter complete once all associated monsters are slain or ore collected.
    /// </summary>
    public void Complete()
    {
        if (State != LeagueEncounterState.Completed)
        {
            State = LeagueEncounterState.Completed;
            OnCompleted?.Invoke(this);
        }
    }
}

/// <summary>
/// A structured pack of monsters arranged in a 2D formation at a fixed world coordinate along the corridor.
/// Conforms to Roadmap Task 2.7.
/// </summary>
public sealed class CorridorPack
{
    /// <summary>Sequential 1-based pack index along the corridor (1: Vanguard, 2: Commander, ...).</summary>
    public int PackIndex { get; }

    /// <summary>The spatial formation geometry of this pack.</summary>
    public CorridorPackFormationType FormationType { get; }

    /// <summary>Focal centroid world coordinate of this pack along the corridor.</summary>
    public Vector2D CenterPosition { get; }

    /// <summary>Progress ratio along the total corridor length (e.g. 0.17 for 17%).</summary>
    public float CorridorProgressRatio { get; }

    /// <summary>Monsters comprising this pack with their pre-calculated world coordinates.</summary>
    public IReadOnlyList<MonsterInstance> Monsters { get; }

    /// <summary>True if this pack belongs to a roadside League Encounter.</summary>
    public bool IsEncounterPack => Encounter != null;

    /// <summary>Reference to the associated roadside League Encounter, if any.</summary>
    public LeagueEncounterInstance? Encounter { get; }

    /// <summary>True if monsters are sealed / dormant (e.g. frozen in Essence) and should not act until triggered.</summary>
    public bool IsDormant { get; private set; }

    /// <summary>True once all monsters in this pack have been defeated.</summary>
    public bool IsCleared => !Monsters.Any(m => m.IsAlive);

    /// <summary>Returns living monsters in this pack.</summary>
    public IEnumerable<MonsterInstance> AliveMonsters => Monsters.Where(m => m.IsAlive);

    /// <summary>Total monster count in this pack.</summary>
    public int Count => Monsters.Count;

    public CorridorPack(
        int packIndex,
        CorridorPackFormationType formationType,
        Vector2D centerPosition,
        float progressRatio,
        IReadOnlyList<MonsterInstance> monsters,
        LeagueEncounterInstance? encounter = null,
        bool isDormant = false)
    {
        PackIndex = packIndex;
        FormationType = formationType;
        CenterPosition = centerPosition;
        CorridorProgressRatio = progressRatio;
        Monsters = monsters;
        Encounter = encounter;
        IsDormant = isDormant;
    }

    /// <summary>
    /// Awakens dormant monsters in this pack (e.g. when Essence Monolith shatters) and marks them aggroed.
    /// </summary>
    public void WakeUp()
    {
        IsDormant = false;
        foreach (var m in Monsters)
        {
            m.Aggro();
        }
    }
}
