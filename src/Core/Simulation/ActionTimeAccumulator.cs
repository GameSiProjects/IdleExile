using System;

namespace IdleExile.Core.Simulation;

/// <summary>
/// High-precision action time accumulator designed for the 30Hz (~33.33ms) fixed simulation loop.
/// Eliminates quantization error across fractional attack/cast speed frequencies (e.g. 1.75 APS, 4.33 APS).
///
/// Features:
/// - Accumulates elapsed time multiplied by entity action speed (e.g. Chill slows, Freeze halts).
/// - Triggers attacks/casts when accumulator reaches or exceeds the action interval (1.0 / APS).
/// - Preserves fractional remainder across ticks to prevent animation/action drift.
/// - Clamps accumulator to prevent action runaway bursts following lag or pause.
/// - Exposes normalized progress (0.0 to 1.0) for smooth UI attack bar interpolation.
///
/// Pure C# — no Godot Node dependencies.
/// </summary>
public sealed class ActionTimeAccumulator
{
    private float _accumulatedTime = 0f;

    /// <summary>
    /// Current time accumulated toward the next action in seconds.
    /// </summary>
    public float AccumulatedTime => _accumulatedTime;

    /// <summary>
    /// Duration in seconds required to complete one action at the current rate (1.0 / effectiveAPS).
    /// </summary>
    public float CurrentActionInterval { get; private set; } = 1.0f;

    /// <summary>
    /// Normalized progress towards the next action [0.0, 1.0].
    /// Ideal for UI attack charge / swing progress indicators.
    /// </summary>
    public float ProgressPercent => CurrentActionInterval > 0f
        ? Math.Clamp(_accumulatedTime / CurrentActionInterval, 0f, 1f)
        : 0f;

    /// <summary>
    /// Maximum action charges that can be accumulated without being consumed (prevents burst lag).
    /// </summary>
    public float MaxAccumulatedIntervals { get; set; } = 1.5f;

    /// <summary>
    /// Advances the accumulator by deltaTime for an entity with the specified actions-per-second rate.
    /// </summary>
    /// <param name="deltaTime">Time elapsed in seconds (e.g. 1/30f = 0.0333s).</param>
    /// <param name="actionsPerSecond">Base attacks or casts per second.</param>
    /// <param name="actionSpeedMultiplier">Multiplier on action speed (e.g. 0.8 for 20% chill, 0 for freeze).</param>
    public void Tick(float deltaTime, float actionsPerSecond, float actionSpeedMultiplier = 1.0f)
    {
        if (actionsPerSecond <= 0f || deltaTime <= 0f)
            return;

        // Frozen entities or incapacitated state: action speed is 0
        if (actionSpeedMultiplier <= 0f)
            return;

        float effectiveSpeed = actionsPerSecond * actionSpeedMultiplier;
        CurrentActionInterval = 1.0f / effectiveSpeed;

        _accumulatedTime += deltaTime;

        // Prevent accumulation beyond the max allowed intervals to avoid burst exploits on resume
        float maxAllowedTime = CurrentActionInterval * MaxAccumulatedIntervals;
        if (_accumulatedTime > maxAllowedTime)
        {
            _accumulatedTime = maxAllowedTime;
        }
    }

    /// <summary>
    /// Returns true if enough time has accumulated to perform an action.
    /// </summary>
    public bool IsActionReady => CurrentActionInterval > 0f && _accumulatedTime >= CurrentActionInterval;

    /// <summary>
    /// Attempts to consume one action interval.
    /// If ready, deducts CurrentActionInterval from AccumulatedTime (retaining the remainder) and returns true.
    /// </summary>
    public bool TryConsumeAction()
    {
        if (CurrentActionInterval > 0f && _accumulatedTime >= CurrentActionInterval)
        {
            _accumulatedTime -= CurrentActionInterval;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Consumes all ready actions (e.g. if multiple ticks elapsed or high attack speed).
    /// Returns the number of actions executed.
    /// </summary>
    public int ConsumeReadyActions(int maxActionsPerTick = 2)
    {
        if (CurrentActionInterval <= 0f || _accumulatedTime < CurrentActionInterval)
            return 0;

        int consumed = 0;
        while (_accumulatedTime >= CurrentActionInterval && consumed < maxActionsPerTick)
        {
            _accumulatedTime -= CurrentActionInterval;
            consumed++;
        }

        return consumed;
    }

    /// <summary>
    /// Resets the accumulator (e.g. on weapon swap, new wave, death, or interruption).
    /// </summary>
    public void Reset()
    {
        _accumulatedTime = 0f;
    }
}
