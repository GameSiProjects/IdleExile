using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using IdleExile.Core.Models;

namespace IdleExile.Core.Services;

/// <summary>
/// Direction of a single stat delta inside the Smart Tooltip Sticky Drawer.
/// </summary>
public enum DeltaDirection
{
    Up,
    Down,
    Neutral
}

/// <summary>
/// Overall verdict badge for the inspected item (Sticky Drawer header).
/// </summary>
public enum DeltaVerdict
{
    Upgrade,
    Downgrade,
    TradeOff,
    Equipped,
    EmptySlot
}

/// <summary>
/// One of the two decisive headline rows (▲/▼ EHP and ▲/▼ DPS).
/// </summary>
public sealed record DeltaLine(DeltaDirection Direction, string Headline, string Detail);

/// <summary>
/// A single explicit affix row, pre-formatted for the drawer list.
/// </summary>
public sealed record AffixLine(AffixType Type, int Tier, string Text, bool IsHighTier);

/// <summary>
/// Aggregated affix overview: counts, best tier and the tier-sorted rows.
/// </summary>
public sealed record AffixSummary(
    int PrefixCount,
    int SuffixCount,
    int HighTierCount,
    int BestTier,
    IReadOnlyList<AffixLine> Lines
);

/// <summary>
/// Pure C# presentation formatter for Task 2.2 (Smart Tooltip Delta — Sticky Drawer 140px).
/// Converts <see cref="EquipmentComparisonResult"/> into skimmable strings that can be read
/// in under 1.5 seconds. Zero Godot dependencies → fully headless testable.
/// </summary>
public static class SmartDeltaFormatter
{
    /// <summary>Affixes at or below this tier number (T1, T2) are flagged as high tier.</summary>
    public const int HighTierThreshold = 2;

    /// <summary>EHP deltas with an absolute value below this are treated as unchanged.</summary>
    public const float EhpNeutralEpsilon = 0.5f;

    /// <summary>DPS percentage deltas with an absolute value below this are treated as unchanged.</summary>
    public const float DpsPercentNeutralEpsilon = 0.05f;

    /// <summary>Resistance deltas with an absolute value below this are omitted from the summary.</summary>
    public const float ResistanceNeutralEpsilon = 0.5f;

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // ─────────────────────────────────────────────
    //  Classification
    // ─────────────────────────────────────────────

    public static DeltaDirection Classify(float delta, float epsilon)
    {
        if (delta >= epsilon) return DeltaDirection.Up;
        if (delta <= -epsilon) return DeltaDirection.Down;
        return DeltaDirection.Neutral;
    }

    public static string Arrow(DeltaDirection direction) => direction switch
    {
        DeltaDirection.Up => "▲",
        DeltaDirection.Down => "▼",
        _ => "■"
    };

    public static DeltaVerdict ClassifyVerdict(EquipmentComparisonResult result)
    {
        if (result == null) throw new ArgumentNullException(nameof(result));

        var ehpDir = Classify(result.DeltaEhp, EhpNeutralEpsilon);
        var dpsDir = Classify(result.DeltaDpsPercent, DpsPercentNeutralEpsilon);

        if (ehpDir != DeltaDirection.Up && dpsDir != DeltaDirection.Up &&
            (ehpDir == DeltaDirection.Down || dpsDir == DeltaDirection.Down))
        {
            return DeltaVerdict.Downgrade;
        }

        if (result.IsUpgrade) return DeltaVerdict.Upgrade;

        return DeltaVerdict.TradeOff;
    }

    public static string VerdictText(DeltaVerdict verdict) => verdict switch
    {
        DeltaVerdict.Upgrade => "▲ NÂNG CẤP",
        DeltaVerdict.Downgrade => "▼ HẠ CẤP",
        DeltaVerdict.TradeOff => "◆ ĐÁNH ĐỔI",
        DeltaVerdict.Equipped => "● ĐANG MẶC",
        _ => "○ Ô TRỐNG"
    };

    // ─────────────────────────────────────────────
    //  Decisive Rows: ▲/▼ EHP and ▲/▼ DPS
    // ─────────────────────────────────────────────

    public static DeltaLine FormatEhpDelta(EquipmentComparisonResult result)
    {
        if (result == null) throw new ArgumentNullException(nameof(result));

        var dir = Classify(result.DeltaEhp, EhpNeutralEpsilon);
        string headline = dir == DeltaDirection.Neutral
            ? "■ ±0 EHP"
            : $"{Arrow(dir)} {Signed(result.DeltaEhp, "N0")} EHP";

        string percent = result.CurrentEhp > 0.001f
            ? $" ({Signed(result.DeltaEhp / result.CurrentEhp * 100f, "F1")}%)"
            : string.Empty;

        string detail = $"{result.CurrentEhp.ToString("N0", Inv)} → {result.ProjectedEhp.ToString("N0", Inv)}{percent}";
        return new DeltaLine(dir, headline, detail);
    }

    public static DeltaLine FormatDpsDelta(EquipmentComparisonResult result)
    {
        if (result == null) throw new ArgumentNullException(nameof(result));

        var dir = Classify(result.DeltaDpsPercent, DpsPercentNeutralEpsilon);
        string headline = dir == DeltaDirection.Neutral
            ? "■ ±0% DPS"
            : $"{Arrow(dir)} {Signed(result.DeltaDpsPercent, "F1")}% DPS";

        string detail = $"{result.CurrentDps.ToString("N1", Inv)} → {result.ProjectedDps.ToString("N1", Inv)} ({Signed(result.DeltaDps, "N1")})";
        return new DeltaLine(dir, headline, detail);
    }

    /// <summary>
    /// Baseline rows shown when the inspected item is the one already equipped.
    /// </summary>
    public static (DeltaLine Ehp, DeltaLine Dps) FormatEquippedBaseline(float currentEhp, float currentDps)
    {
        var ehp = new DeltaLine(DeltaDirection.Neutral, $"● {currentEhp.ToString("N0", Inv)} EHP", "Mốc so sánh hiện tại");
        var dps = new DeltaLine(DeltaDirection.Neutral, $"● {currentDps.ToString("N1", Inv)} DPS", "Chọn đồ dự phòng để so sánh");
        return (ehp, dps);
    }

    /// <summary>
    /// Compact one-line resistance summary, e.g. "Fire +45 • Cold -5 • Chaos -15".
    /// </summary>
    public static string FormatResistanceSummary(EquipmentComparisonResult result)
    {
        if (result == null) throw new ArgumentNullException(nameof(result));

        var parts = new List<string>(4);
        AppendRes(parts, "Fire", result.DeltaFireRes);
        AppendRes(parts, "Cold", result.DeltaColdRes);
        AppendRes(parts, "Light", result.DeltaLightningRes);
        AppendRes(parts, "Chaos", result.DeltaChaosRes);

        return parts.Count > 0 ? "Res: " + string.Join(" • ", parts) : "Res: không đổi";
    }

    // ─────────────────────────────────────────────
    //  Affix List (Scroll or T1/T2 summary)
    // ─────────────────────────────────────────────

    /// <summary>
    /// Builds the affix overview. Rows are sorted best-tier first (T1 → T8), Prefix before Suffix.
    /// When <paramref name="highTierOnly"/> is true, only T1/T2 rows are returned (counts stay global).
    /// </summary>
    public static AffixSummary SummarizeAffixes(IEnumerable<AffixDefinition> affixes, bool highTierOnly = false)
    {
        if (affixes == null) throw new ArgumentNullException(nameof(affixes));

        var explicitAffixes = affixes.Where(a => a.Type != AffixType.Implicit).ToList();

        int prefixes = explicitAffixes.Count(a => a.Type == AffixType.Prefix);
        int suffixes = explicitAffixes.Count(a => a.Type == AffixType.Suffix);
        int highTier = explicitAffixes.Count(IsHighTier);
        int bestTier = explicitAffixes.Count > 0 ? explicitAffixes.Min(a => a.Tier) : 0;

        var lines = explicitAffixes
            .Where(a => !highTierOnly || IsHighTier(a))
            .OrderBy(a => a.Tier)
            .ThenBy(a => a.Type)
            .Select(a => new AffixLine(a.Type, a.Tier, FormatAffixText(a), IsHighTier(a)))
            .ToList();

        return new AffixSummary(prefixes, suffixes, highTier, bestTier, lines);
    }

    /// <summary>
    /// Header line, e.g. "4 MODS • 2P/2S • ★ T1-T2 ×3".
    /// </summary>
    public static string FormatAffixHeader(AffixSummary summary)
    {
        if (summary == null) throw new ArgumentNullException(nameof(summary));

        int total = summary.PrefixCount + summary.SuffixCount;
        if (total == 0) return "KHÔNG CÓ AFFIX";

        string header = $"{total} MODS • {summary.PrefixCount}P/{summary.SuffixCount}S";
        return summary.HighTierCount > 0 ? $"{header} • ★ T1-T2 ×{summary.HighTierCount}" : header;
    }

    /// <summary>
    /// Formats a single affix row, e.g. "★ (S) +47% to Fire Resistance [T1]".
    /// </summary>
    public static string FormatAffixRow(AffixLine line)
    {
        if (line == null) throw new ArgumentNullException(nameof(line));
        string mark = line.IsHighTier ? "★" : "•";
        string type = line.Type == AffixType.Prefix ? "(P)" : "(S)";
        return $"{mark} {type} {line.Text} [T{line.Tier}]";
    }

    public static bool IsHighTier(AffixDefinition affix) => affix.Tier > 0 && affix.Tier <= HighTierThreshold;

    private static string FormatAffixText(AffixDefinition affix)
    {
        float avg = MathF.Round((affix.MinValue + affix.MaxValue) / 2f);
        return string.Format(Inv, affix.NameTemplate, avg);
    }

    // ─────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────

    private static void AppendRes(List<string> parts, string name, float delta)
    {
        if (MathF.Abs(delta) < ResistanceNeutralEpsilon) return;
        parts.Add($"{name} {Signed(delta, "F0")}");
    }

    private static string Signed(float value, string format)
    {
        string body = MathF.Abs(value).ToString(format, Inv);
        return value < 0f ? $"-{body}" : $"+{body}";
    }
}
