using System;
using IdleExile.Core.State;

namespace IdleExile.Core.Simulation;

/// <summary>
/// Detailed metadata recorded upon character demise.
/// Used for death recaps, combat telemetry, and offline benchmark estimation.
/// </summary>
public sealed record DeathRecap(
    string KillerName,
    int PlayerLevel,
    float ExpLost,
    float RemainingExpPercent,
    int TotalDeaths,
    int MapDeaths,
    DateTime Timestamp
);

/// <summary>
/// Coordinates player death penalties, death tracking, auto-respawn sequencing,
/// and emergency portal escapes according to PoE rules.
///
/// Features:
/// 1. PoE-standard XP Penalty: -10% of current level experience on death (capped at 0%, cannot de-level).
/// 2. Death Metrics: Career total deaths and current map deaths for death-rate benchmarking.
/// 3. Auto-Respawn: 2.0-second delay countdown that automatically refills all EHP pools
///    and cleanses all active ailments.
/// 4. Emergency Portal (Spacebar shortcut): Instantly escapes back to Town/Hideout,
///    preserving all loot and avoiding death penalties if used prior to fatal damage.
///
/// Pure C# — no Godot Node dependencies.
/// </summary>
public sealed class DeathHandler
{
    /// <summary>
    /// Default respawn delay in seconds (PoE standard ~2.0s).
    /// </summary>
    public const float DefaultRespawnDelay = 2.0f;

    /// <summary>
    /// Percentage of current level EXP lost on death (10% standard PoE).
    /// </summary>
    public const float DefaultExpPenaltyPercent = 10.0f;

    public float RespawnDelay { get; set; } = DefaultRespawnDelay;
    public float ExpPenaltyPercent { get; set; } = DefaultExpPenaltyPercent;

    /// <summary>Lifetime cumulative deaths across all map runs.</summary>
    public int TotalDeaths { get; private set; } = 0;

    /// <summary>Deaths occurred during the current active map run.</summary>
    public int MapDeaths { get; private set; } = 0;

    /// <summary>True while the character is dead and counting down to respawn.</summary>
    public bool IsAwaitingRespawn { get; private set; } = false;

    /// <summary>Seconds elapsed since the fatal hit occurred.</summary>
    public float RespawnElapsedSeconds { get; private set; } = 0f;

    /// <summary>Normalized progress towards respawn [0.0, 1.0].</summary>
    public float RespawnProgress => RespawnDelay > 0f
        ? Math.Clamp(RespawnElapsedSeconds / RespawnDelay, 0f, 1f)
        : 1f;

    /// <summary>Metadata of the most recent death.</summary>
    public DeathRecap? LastDeathRecap { get; private set; }

    // ─────────────────────────────────────────────
    //  Events
    // ─────────────────────────────────────────────

    /// <summary>Fired when the player suffers a fatal blow. Arg: DeathRecap.</summary>
    public event Action<DeathRecap>? OnPlayerDied;

    /// <summary>Fired when the player successfully respawns after the 2s delay.</summary>
    public event Action? OnPlayerRespawned;

    /// <summary>Fired when the player activates an Emergency Portal escape.</summary>
    public event Action? OnEmergencyPortalUsed;

    // ─────────────────────────────────────────────
    //  Death Processing
    // ─────────────────────────────────────────────

    /// <summary>
    /// Triggers player death processing:
    /// 1. Increments death metrics.
    /// 2. Applies the -10% EXP death penalty (cannot de-level).
    /// 3. Begins the 2.0s auto-respawn countdown.
    /// </summary>
    /// <param name="player">The player character state.</param>
    /// <param name="killerName">Name of the monster or effect that delivered the lethal hit.</param>
    /// <returns>The generated death recap.</returns>
    public DeathRecap HandleDeath(PlayerState player, string killerName = "Crucible Monster")
    {
        TotalDeaths++;
        MapDeaths++;
        IsAwaitingRespawn = true;
        RespawnElapsedSeconds = 0f;

        // Apply 10% EXP penalty
        float expLost = player.DeductDeathPenalty(ExpPenaltyPercent);

        var recap = new DeathRecap(
            KillerName: killerName,
            PlayerLevel: player.Level,
            ExpLost: expLost,
            RemainingExpPercent: player.ExperiencePercent,
            TotalDeaths: TotalDeaths,
            MapDeaths: MapDeaths,
            Timestamp: DateTime.UtcNow
        );

        LastDeathRecap = recap;
        OnPlayerDied?.Invoke(recap);
        return recap;
    }

    // ─────────────────────────────────────────────
    //  Tick & Auto-Respawn
    // ─────────────────────────────────────────────

    /// <summary>
    /// Ticks the respawn countdown during the 30Hz combat simulation.
    /// Once 2.0s elapses, automatically restores all resource pools and cleanses ailments.
    /// </summary>
    /// <param name="deltaTime">Time step in seconds (e.g. 1/30f).</param>
    /// <param name="player">Player state to restore upon respawn.</param>
    /// <param name="ailments">Ailment tracker to cleanse upon respawn.</param>
    /// <returns>True if respawn occurred this tick.</returns>
    public bool Tick(float deltaTime, PlayerState player, AilmentTracker? ailments = null)
    {
        if (!IsAwaitingRespawn)
            return false;

        RespawnElapsedSeconds += deltaTime;

        if (RespawnElapsedSeconds >= RespawnDelay)
        {
            Respawn(player, ailments);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Immediately completes the respawn sequence:
    /// - Refills Life, Energy Shield, and Mana.
    /// - Cleanses all active ailments.
    /// - Resets respawn flags.
    /// </summary>
    public void Respawn(PlayerState player, AilmentTracker? ailments = null)
    {
        IsAwaitingRespawn = false;
        RespawnElapsedSeconds = 0f;

        // Refill pools & reset entropy
        player.RestoreAllPools();

        // Cleanse ailments
        ailments?.Clear();

        OnPlayerRespawned?.Invoke();
    }

    // ─────────────────────────────────────────────
    //  Emergency Escape & Map Reset
    // ─────────────────────────────────────────────

    /// <summary>
    /// Triggers an immediate emergency portal escape back to Town/Hideout.
    /// Only accessible while the character is alive (prevents escaping mid-death).
    /// Preserves all loot accumulated in the map run and avoids EXP penalties.
    /// </summary>
    /// <param name="player">Player state to verify alive status.</param>
    /// <returns>True if escape succeeded.</returns>
    public bool TryTriggerEmergencyPortal(PlayerState player)
    {
        if (!player.IsAlive || IsAwaitingRespawn)
            return false;

        OnEmergencyPortalUsed?.Invoke();
        return true;
    }

    /// <summary>
    /// Resets per-map death counter when embarking on a new map run.
    /// </summary>
    public void ResetForNewMap()
    {
        MapDeaths = 0;
        IsAwaitingRespawn = false;
        RespawnElapsedSeconds = 0f;
    }
}
