using System;
using Godot;

namespace IdleExile.UI.Components;

/// <summary>
/// Operational camera modes for the 2D combat viewport.
/// - CorridorFollow: Smoothly follows Exile world position with look-ahead along corridor.
/// - BossArenaLock: Freezes camera focal point firmly to the center of the Boss Chamber.
/// </summary>
public enum ArenaCameraMode
{
    /// <summary>Dynamic tracking following the Exile character along the linear corridor.</summary>
    CorridorFollow = 0,

    /// <summary>Fixed focal lock anchored to the center of the Boss Room Chamber (Task 2.8).</summary>
    BossArenaLock = 1
}

/// <summary>
/// 2D Camera Controller for the Combat Viewport (Task 2.6 & Task 2.8).
/// - Corridor Follow: Smooth Lerp tracking of Exile's real-world coordinates with look-ahead offset.
/// - Boss Arena Lock: When Exile crosses the Fog Gate threshold at 85% map progress,
///   the camera smoothly transitions to and locks firmly onto the center of the Boss Arena Chamber (93%).
/// - Provides exact bidirectional coordinate transformations: WorldToScreen and ScreenToWorld.
/// </summary>
public partial class ArenaCamera : Node2D
{
    // ─────────────────────────────────────────────
    //  Configuration & Tuning
    // ─────────────────────────────────────────────

    /// <summary>Current operational tracking mode of the camera.</summary>
    public ArenaCameraMode Mode { get; private set; } = ArenaCameraMode.CorridorFollow;

    /// <summary>Current world-space focal coordinate of the camera.</summary>
    public Vector2 CurrentPosition { get; set; } = Arena2D.CenterPosition;

    /// <summary>Target world-space focal coordinate that the camera is interpolating toward.</summary>
    public Vector2 TargetPosition { get; set; } = Arena2D.CenterPosition;

    /// <summary>Look-ahead offset applied in forward marching direction (+X) during corridor travel.</summary>
    public Vector2 LookAheadOffset { get; set; } = new(24f, 0f);

    /// <summary>Lerp smoothing coefficient when following the Exile along the corridor.</summary>
    public float FollowLerpSpeed { get; set; } = 8.0f;

    /// <summary>Lerp smoothing coefficient when transitioning from corridor to boss arena center.</summary>
    public float LockLerpSpeed { get; set; } = 5.0f;

    /// <summary>True once the camera has completed transition and is firmly locked to the boss chamber center.</summary>
    public bool IsLocked { get; private set; } = false;

    /// <summary>World coordinates of the Boss Chamber center anchor point.</summary>
    public Vector2 BossArenaCenterWorld { get; private set; } = Vector2.Zero;

    // ─────────────────────────────────────────────
    //  Events
    // ─────────────────────────────────────────────

    /// <summary>Fired when camera transitions to or from Arena Lock mode.</summary>
    public event Action<ArenaCameraMode>? OnModeChanged;

    /// <summary>Fired when the camera has completed its transition and is locked to the arena center.</summary>
    public event Action? OnArenaLockEngaged;

    // ─────────────────────────────────────────────
    //  Lifecycle & Control API
    // ─────────────────────────────────────────────

    /// <summary>
    /// Resets the camera to default CorridorFollow state initialized at the given world position.
    /// </summary>
    public void Reset(Vector2 initialWorldPos)
    {
        Mode = ArenaCameraMode.CorridorFollow;
        CurrentPosition = initialWorldPos;
        TargetPosition = initialWorldPos + LookAheadOffset;
        IsLocked = false;
        BossArenaCenterWorld = Vector2.Zero;
    }

    /// <summary>
    /// Commands the camera to transition to and lock onto the specified Boss Arena center in world coordinates.
    /// </summary>
    public void SetArenaLock(Vector2 bossArenaCenterWorld)
    {
        BossArenaCenterWorld = bossArenaCenterWorld;
        TargetPosition = bossArenaCenterWorld;

        if (Mode != ArenaCameraMode.BossArenaLock)
        {
            Mode = ArenaCameraMode.BossArenaLock;
            IsLocked = false;
            OnModeChanged?.Invoke(Mode);
        }
    }

    /// <summary>
    /// Releases the Boss Arena camera lock, resuming dynamic Exile corridor tracking.
    /// </summary>
    public void ReleaseLock()
    {
        if (Mode != ArenaCameraMode.CorridorFollow)
        {
            Mode = ArenaCameraMode.CorridorFollow;
            IsLocked = false;
            OnModeChanged?.Invoke(Mode);
        }
    }

    /// <summary>
    /// Frame update method advancing camera position and smoothing.
    /// </summary>
    public void Update(float deltaTime, Vector2 exileWorldPos, Vector2 bossArenaCenterWorld, bool shouldLockToArena)
    {
        if (deltaTime <= 0f) return;

        if (shouldLockToArena)
        {
            if (Mode != ArenaCameraMode.BossArenaLock)
            {
                SetArenaLock(bossArenaCenterWorld);
            }

            TargetPosition = bossArenaCenterWorld;

            // Interpolate toward the boss arena center
            float t = Mathf.Clamp(deltaTime * LockLerpSpeed, 0f, 1f);
            CurrentPosition = CurrentPosition.Lerp(TargetPosition, t);

            if (!IsLocked && CurrentPosition.DistanceTo(TargetPosition) <= 1.5f)
            {
                CurrentPosition = TargetPosition;
                IsLocked = true;
                OnArenaLockEngaged?.Invoke();
            }
        }
        else
        {
            if (Mode != ArenaCameraMode.CorridorFollow)
            {
                ReleaseLock();
            }

            TargetPosition = exileWorldPos + LookAheadOffset;
            float t = Mathf.Clamp(deltaTime * FollowLerpSpeed, 0f, 1f);
            CurrentPosition = CurrentPosition.Lerp(TargetPosition, t);
            IsLocked = false;
        }
    }

    // ─────────────────────────────────────────────
    //  Bidirectional Coordinate Transformations
    // ─────────────────────────────────────────────

    /// <summary>
    /// Projects a 2D world space coordinate into 2D viewport screen coordinates relative to the camera focal point.
    /// </summary>
    public Vector2 WorldToScreen(Vector2 worldPos, Vector2 viewportCenter)
    {
        return viewportCenter + (worldPos - CurrentPosition);
    }

    /// <summary>
    /// Unprojects a 2D screen coordinate back to world coordinates.
    /// </summary>
    public Vector2 ScreenToWorld(Vector2 screenPos, Vector2 viewportCenter)
    {
        return CurrentPosition + (screenPos - viewportCenter);
    }
}
