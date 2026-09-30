using System;

namespace IdleExile.Core.Simulation;

/// <summary>
/// High-precision 30Hz fixed timestep simulation manager.
/// Implements Path of Exile standard server tick resolution (~33.33ms per tick).
///
/// Features:
/// 1. Decouples simulation logic from variable rendering frame rates (10–60+ FPS).
/// 2. Accumulates elapsed real-world time and executes fixed discrete ticks.
/// 3. Prevents "Spiral of Death" lag cascades by capping catch-up iterations per frame.
/// 4. Provides deterministic stepping for headless unit testing and offline simulation.
/// 5. Pure C# — no Godot Node dependencies.
/// </summary>
public sealed class TickManager
{
    /// <summary>Standard PoE simulation tick rate (30 ticks per second).</summary>
    public const int DefaultTickRate = 30;

    /// <summary>Standard PoE simulation step duration (~0.03333333s = ~33.33ms).</summary>
    public const float DefaultFixedDeltaTime = 1.0f / DefaultTickRate;

    /// <summary>Maximum simulation ticks permitted to execute within a single update call (2.0s catchup window).</summary>
    public const int DefaultMaxTicksPerUpdate = 60;

    private float _timeAccumulator = 0f;

    /// <summary>Configured simulation tick frequency in Hz (default 30).</summary>
    public int TickRate { get; }

    /// <summary>Discrete duration of a single simulation tick in seconds (e.g. 1/30s = 0.03333s).</summary>
    public float FixedDeltaTime { get; }

    /// <summary>Maximum catch-up ticks allowed in one frame to prevent spiral-of-death freeze.</summary>
    public int MaxTicksPerUpdate { get; set; } = DefaultMaxTicksPerUpdate;

    /// <summary>Current unconsumed time fraction in the accumulator.</summary>
    public float TimeAccumulator => _timeAccumulator;

    /// <summary>Monotonic tick counter incremented on every discrete simulation step.</summary>
    public long CurrentTick { get; private set; } = 0;

    /// <summary>Cumulative simulated time in seconds (CurrentTick * FixedDeltaTime).</summary>
    public float TotalSimulationTime => CurrentTick * FixedDeltaTime;

    /// <summary>Speed multiplier applied to incoming real delta time (1.0 = normal speed).</summary>
    public float TimeScale { get; set; } = 1.0f;

    /// <summary>When true, updates accumulate no simulation progress.</summary>
    public bool IsPaused { get; set; } = false;

    /// <summary>Fired on every discrete tick step. Args: (CurrentTick, FixedDeltaTime).</summary>
    public event Action<long, float>? OnTickStep;

    public TickManager(int tickRate = DefaultTickRate, int maxTicksPerUpdate = DefaultMaxTicksPerUpdate)
    {
        TickRate = Math.Max(1, tickRate);
        FixedDeltaTime = 1.0f / TickRate;
        MaxTicksPerUpdate = Math.Max(1, maxTicksPerUpdate);
    }

    /// <summary>
    /// Advances the accumulator with elapsed real time, executing the provided tick callback
    /// as many times as necessary to maintain 30Hz simulation cadence.
    /// </summary>
    /// <param name="realDeltaTime">Elapsed real time since last update (in seconds).</param>
    /// <param name="onTick">Simulation step callback invoked for each discrete tick with FixedDeltaTime.</param>
    /// <returns>Number of discrete ticks processed in this update.</returns>
    public int Update(float realDeltaTime, Action<float> onTick)
    {
        if (IsPaused || realDeltaTime <= 0f || onTick == null)
            return 0;

        float effectiveDelta = realDeltaTime * MathF.Max(0f, TimeScale);
        _timeAccumulator += effectiveDelta;

        // Spiral of death prevention: clamp accumulator if it falls too far behind
        float maxAllowedAccumulator = FixedDeltaTime * MaxTicksPerUpdate;
        if (_timeAccumulator > maxAllowedAccumulator)
        {
            _timeAccumulator = maxAllowedAccumulator;
        }

        const float epsilon = 0.0001f;
        int ticksProcessed = 0;
        while (_timeAccumulator + epsilon >= FixedDeltaTime && ticksProcessed < MaxTicksPerUpdate)
        {
            CurrentTick++;
            _timeAccumulator -= FixedDeltaTime;
            if (_timeAccumulator < 0f)
            {
                _timeAccumulator = 0f;
            }
            ticksProcessed++;

            onTick(FixedDeltaTime);
            OnTickStep?.Invoke(CurrentTick, FixedDeltaTime);
        }

        return ticksProcessed;
    }

    /// <summary>
    /// Executes exactly one deterministic discrete tick.
    /// Used for unit tests, offline math replay, and debug frame-by-frame inspection.
    /// </summary>
    /// <param name="onTick">Simulation step callback.</param>
    public void StepSingleTick(Action<float> onTick)
    {
        if (onTick == null) return;

        CurrentTick++;
        onTick(FixedDeltaTime);
        OnTickStep?.Invoke(CurrentTick, FixedDeltaTime);
    }

    /// <summary>
    /// Resets the tick manager state to zero (resets counter and accumulator).
    /// </summary>
    public void Reset()
    {
        CurrentTick = 0;
        _timeAccumulator = 0f;
    }

    /// <summary>
    /// Pauses simulation.
    /// </summary>
    public void Pause() => IsPaused = true;

    /// <summary>
    /// Resumes simulation.
    /// </summary>
    public void Resume() => IsPaused = false;
}
