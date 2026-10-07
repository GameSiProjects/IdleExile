using System.Collections.Generic;

namespace IdleExile.Core.Models;

/// <summary>
/// Configuration model for Thaumaturgic Smelter recipes, transmutation exchange rates, and dust multipliers.
/// Deserialized from data/smelter_recipes.json.
/// Pure C# model — zero Godot dependencies.
/// </summary>
public sealed record SmelterRecipeConfig(
    Dictionary<string, float> RarityMultipliers,
    int DefaultInfuseCost,
    int DefaultInfuseMapCount,
    float DefaultInfuseBonusQuantity,
    List<SmelterTransmutationOption> TransmutationOptions
);

/// <summary>
/// Definition of a currency transmutation option available at the Thaumaturgic Smelter.
/// </summary>
public sealed record SmelterTransmutationOption(
    string CurrencyId,
    string Name,
    int DustCost,
    int Quantity
);
