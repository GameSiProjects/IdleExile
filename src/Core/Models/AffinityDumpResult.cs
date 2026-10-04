namespace IdleExile.Core.Models;

/// <summary>
/// Telemetry summary returned after a 1-click affinity stash dump operation.
/// Pure C# model — no Godot dependencies.
/// </summary>
public sealed record AffinityDumpResult
{
    public int CurrenciesDumped { get; init; }
    public int GearDumped { get; init; }
    public int MapsDumped { get; init; }
    public int FossilsDumped { get; init; }

    public int TotalItemsDumped => CurrenciesDumped + GearDumped + MapsDumped + FossilsDumped;
    public bool IsEmpty => TotalItemsDumped == 0;

    public string BuildSummaryText()
    {
        if (IsEmpty)
            return "No items to dump into Stash.";

        var parts = new System.Collections.Generic.List<string>();
        if (CurrenciesDumped > 0) parts.Add($"{CurrenciesDumped} {(CurrenciesDumped == 1 ? "Currency" : "Currencies")}");
        if (MapsDumped > 0) parts.Add($"{MapsDumped} {(MapsDumped == 1 ? "Map" : "Maps")}");
        if (GearDumped > 0) parts.Add($"{GearDumped} Gear");
        if (FossilsDumped > 0) parts.Add($"{FossilsDumped} {(FossilsDumped == 1 ? "Fossil" : "Fossils")}");

        return $"Stashed {string.Join(", ", parts)}.";
    }
}
