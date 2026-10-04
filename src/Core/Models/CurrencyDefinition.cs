namespace IdleExile.Core.Models;

/// <summary>
/// Data definition for a Path of Exile-inspired currency orb.
/// Pure C# immutable record deserialized from data/currencies.json.
/// </summary>
public sealed record CurrencyDefinition(
    string Id,
    string Name,
    string IconGlyph,
    string ColorHex,
    int Tier,
    int MaxStackSize,
    string Description,
    string Category
);
