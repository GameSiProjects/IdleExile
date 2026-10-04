using System;
using System.Collections.Generic;
using IdleExile.Core.Simulation;

namespace IdleExile.Core.Models;

/// <summary>
/// Represents an itemized Map Slate stored in the Affinity Map Tab.
/// Tracks tier, quantity count, rolled modifiers, and converts cleanly into MapQueueItem.
/// Pure C# model — no Godot dependencies.
/// </summary>
public sealed class StashedMapSlate
{
    public MapDefinition Map { get; set; }
    public int Quantity { get; set; } = 1;
    public string Rarity { get; set; } = "Normal";
    public List<RolledMapModifier> Modifiers { get; set; } = new();

    public string MapId => Map.Id;
    public string MapName => Map.Name;
    public int Tier => Map.Tier;
    public int MonsterLevel => Map.MonsterLevel;
    public string? BossName => Map.BossName;
    public string Description => Map.Description;

    public StashedMapSlate(MapDefinition mapDef, int quantity = 1, string rarity = "Normal")
    {
        Map = mapDef ?? throw new ArgumentNullException(nameof(mapDef));
        Quantity = quantity;
        Rarity = rarity;
    }

    /// <summary>
    /// Converts this stashed map slate to a MapQueueItem ready for MapQueueManager.
    /// </summary>
    public MapQueueItem ToQueueItem(int slotIndex = 0)
    {
        return new MapQueueItem(slotIndex, Map, Modifiers, Rarity);
    }
}
