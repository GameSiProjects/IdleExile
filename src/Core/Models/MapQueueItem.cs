using System;
using System.Collections.Generic;
using System.Linq;
using IdleExile.Core.Simulation;

namespace IdleExile.Core.Models;

/// <summary>
/// Represents an itemized map slate waiting in the automated Map Device Queue (Task 4.2 & GDD 4.1).
/// Stores the immutable map definition, rolled affixes, rarity classification, and computed yields.
/// </summary>
public sealed record MapQueueItem(
    int SlotIndex,
    MapDefinition Map,
    IReadOnlyList<RolledMapModifier> Modifiers,
    string Rarity = "Normal"
)
{
    /// <summary>Total computed item quantity (+base map quantity + affix bonuses).</summary>
    public int TotalQuantity => Map.BaseQuantity + Modifiers.Sum(m => m.Definition.BonusQuantity);

    /// <summary>Total computed monster pack size (+base map pack size + affix bonuses).</summary>
    public int TotalPackSize => Map.BasePackSize + Modifiers.Sum(m => m.Definition.BonusPackSize);

    /// <summary>Monster level of the map.</summary>
    public int MonsterLevel => Map.MonsterLevel;

    /// <summary>Tier of the map (T0 to T16+).</summary>
    public int Tier => Map.Tier;

    /// <summary>Returns true if the map has dangerous reflect or no-regen modifiers.</summary>
    public bool HasDangerousModifiers => Modifiers.Any(m =>
        m.Definition.Id.Contains("reflect", StringComparison.OrdinalIgnoreCase) ||
        m.Definition.Id.Contains("no_regen", StringComparison.OrdinalIgnoreCase) ||
        m.Definition.Id.Contains("reduced_max_res", StringComparison.OrdinalIgnoreCase)
    );
}
