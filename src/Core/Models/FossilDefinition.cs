namespace IdleExile.Core.Models;

/// <summary>
/// Data definition for an Abyssal fossil used in deterministic crafting.
/// Pure C# immutable record deserialized from data/fossils.json.
/// </summary>
public sealed record FossilDefinition(
    string Id,
    string Name,
    string IconGlyph,
    string ColorHex,
    string PrimaryBonus,
    string BlockedGroup,
    string Description,
    int MaxStackSize
);
