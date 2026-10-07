namespace IdleExile.Core.Models;

/// <summary>
/// Telemetry snapshot representing the exile's real-time in-combat damage performance.
/// Calculated via a sliding time window (3.0s) during mapping/delving.
/// Pure C# immutable record without Godot node dependencies.
/// </summary>
public sealed record CombatDpsSnapshot(
    float CurrentDps = 0f,
    float PeakDps = 0f,
    float AverageDps = 0f,
    float PhysicalPercent = 0f,
    float FirePercent = 0f,
    float ColdPercent = 0f,
    float LightningPercent = 0f,
    float ChaosPercent = 0f,
    float HitRateAps = 0f,
    float AccuracyPercent = 100f
)
{
    public static readonly CombatDpsSnapshot Empty = new();
}
