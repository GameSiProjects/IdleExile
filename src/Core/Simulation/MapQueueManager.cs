using System;
using System.Collections.Generic;
using IdleExile.Core.Models;

namespace IdleExile.Core.Simulation;

/// <summary>
/// Autonomous 10-Slot Map Queue Manager (Task 4.2 & GDD Section 4.1).
/// Enables True Zero-APM Endless Mapping by managing an ordered sequence of prepared maps,
/// coordinating a 3.0-second loot-settling countdown after map clearance, and automatically
/// activating subsequent maps or continuous fallback encounters.
/// Pure C# class decoupled from Godot Engine nodes.
/// </summary>
public sealed class MapQueueManager
{
    public const int MaxCapacity = 10;
    public const float DefaultDelaySeconds = 3.0f;

    private readonly List<MapQueueItem> _items = new(MaxCapacity);

    // ─────────────────────────────────────────────
    //  State Properties
    // ─────────────────────────────────────────────

    /// <summary>Total number of queued maps currently scheduled.</summary>
    public int Count => _items.Count;

    /// <summary>True if all 10 queue slots are occupied.</summary>
    public bool IsFull => _items.Count >= MaxCapacity;

    /// <summary>True if no maps are scheduled in the queue.</summary>
    public bool IsEmpty => _items.Count == 0;

    /// <summary>Read-only snapshot of all currently queued items in FIFO order.</summary>
    public IReadOnlyList<MapQueueItem> Items => _items;

    /// <summary>Toggles whether the queue automatically advances after map completion.</summary>
    public bool AutoAdvanceEnabled { get; set; } = true;

    /// <summary>
    /// When queue is depleted, automatically sustain mapping by generating or fetching
    /// maps from stash / inventory or falling back to The Endless Hollow (Tier 0).
    /// </summary>
    public bool ContinuousRunEnabled { get; set; } = false;

    /// <summary>Delay in seconds between map clearance and starting next map (loot collection window).</summary>
    public float AutoAdvanceDelaySeconds { get; set; } = DefaultDelaySeconds;

    /// <summary>True while the post-clearance auto-advance countdown is ticking down.</summary>
    public bool IsCountdownActive { get; private set; } = false;

    /// <summary>Remaining time in seconds for the current auto-advance delay countdown.</summary>
    public float CountdownRemaining { get; private set; } = 0f;

    /// <summary>Peeks at the next map item waiting at slot 0 without dequeuing.</summary>
    public MapQueueItem? NextPendingMap => _items.Count > 0 ? _items[0] : null;

    /// <summary>
    /// Optional delegate providing a fallback map slate when the queue is depleted
    /// and ContinuousRunEnabled is active (e.g. Endless Hollow T0 fail-safe).
    /// </summary>
    public Func<MapQueueItem?>? ContinuousRunFallbackProvider { get; set; }

    // ─────────────────────────────────────────────
    //  Events
    // ─────────────────────────────────────────────

    /// <summary>Fired whenever items are added, removed, swapped, or cleared from the queue.</summary>
    public event Action? OnQueueChanged;

    /// <summary>Fired when a queued map is dequeued and advanced for execution.</summary>
    public event Action<MapQueueItem>? OnMapQueueAdvanced;

    /// <summary>Fired when the 3-second auto-advance countdown begins.</summary>
    public event Action<float>? OnCountdownStarted;

    /// <summary>Fired every tick during the countdown with remaining seconds.</summary>
    public event Action<float>? OnCountdownTick;

    /// <summary>Fired when the player or system cancels the delay countdown.</summary>
    public event Action? OnCountdownCancelled;

    /// <summary>Fired when the queue becomes completely empty.</summary>
    public event Action? OnQueueDepleted;

    // ─────────────────────────────────────────────
    //  Queue Operations
    // ─────────────────────────────────────────────

    /// <summary>
    /// Enqueues a new map slate into the next available slot.
    /// Returns false if the queue is already full (10 slots).
    /// </summary>
    public bool Enqueue(MapDefinition map, IReadOnlyList<RolledMapModifier>? modifiers = null, string rarity = "Normal")
    {
        if (map == null || IsFull)
            return false;

        var item = new MapQueueItem(
            SlotIndex: _items.Count,
            Map: map,
            Modifiers: modifiers ?? Array.Empty<RolledMapModifier>(),
            Rarity: rarity
        );

        _items.Add(item);
        OnQueueChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Enqueues an existing MapQueueItem instance.
    /// Returns false if queue is full.
    /// </summary>
    public bool Enqueue(MapQueueItem item)
    {
        if (item == null || IsFull)
            return false;

        var reindexed = item with { SlotIndex = _items.Count };
        _items.Add(reindexed);
        OnQueueChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Inserts a map slate at a specific slot index, shifting subsequent items right.
    /// Returns false if queue is full or index is out of bounds.
    /// </summary>
    public bool InsertAt(int slotIndex, MapDefinition map, IReadOnlyList<RolledMapModifier>? modifiers = null, string rarity = "Normal")
    {
        if (map == null || IsFull || slotIndex < 0 || slotIndex > _items.Count)
            return false;

        var item = new MapQueueItem(
            SlotIndex: slotIndex,
            Map: map,
            Modifiers: modifiers ?? Array.Empty<RolledMapModifier>(),
            Rarity: rarity
        );

        _items.Insert(slotIndex, item);
        ReindexSlots();
        OnQueueChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Removes the item at the specified slot index, shifting subsequent items left.
    /// Returns false if index is out of bounds.
    /// </summary>
    public bool RemoveAt(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _items.Count)
            return false;

        _items.RemoveAt(slotIndex);
        ReindexSlots();
        OnQueueChanged?.Invoke();

        if (_items.Count == 0)
        {
            OnQueueDepleted?.Invoke();
        }

        return true;
    }

    /// <summary>
    /// Swaps the position of two queued items.
    /// Returns false if either index is out of bounds.
    /// </summary>
    public bool Swap(int indexA, int indexB)
    {
        if (indexA < 0 || indexA >= _items.Count || indexB < 0 || indexB >= _items.Count || indexA == indexB)
            return false;

        (_items[indexA], _items[indexB]) = (_items[indexB], _items[indexA]);
        ReindexSlots();
        OnQueueChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Clears all scheduled maps and cancels any pending auto-advance countdown.
    /// </summary>
    public void Clear()
    {
        if (_items.Count == 0 && !IsCountdownActive)
            return;

        _items.Clear();
        CancelCountdown();
        OnQueueChanged?.Invoke();
        OnQueueDepleted?.Invoke();
    }

    /// <summary>
    /// Dequeues and returns the next pending map (Slot 0), shifting remaining items forward.
    /// Returns null if the queue is empty.
    /// </summary>
    public MapQueueItem? DequeueNext()
    {
        if (_items.Count == 0)
            return null;

        var next = _items[0];
        _items.RemoveAt(0);
        ReindexSlots();

        OnQueueChanged?.Invoke();
        OnMapQueueAdvanced?.Invoke(next);

        if (_items.Count == 0)
        {
            OnQueueDepleted?.Invoke();
        }

        return next;
    }

    /// <summary>
    /// Gets the item currently occupying the given slot index, or null if vacant.
    /// </summary>
    public MapQueueItem? GetSlot(int index)
    {
        return (index >= 0 && index < _items.Count) ? _items[index] : null;
    }

    // ─────────────────────────────────────────────
    //  Auto-Advance & Countdown Logic
    // ─────────────────────────────────────────────

    /// <summary>
    /// Initiates the post-map clearance countdown before starting the next map.
    /// </summary>
    public void StartCountdown(float? customDelay = null)
    {
        if (!AutoAdvanceEnabled)
            return;

        // Only start countdown if we have an item in queue or continuous run is enabled
        if (_items.Count == 0 && !ContinuousRunEnabled)
            return;

        CountdownRemaining = customDelay ?? AutoAdvanceDelaySeconds;
        IsCountdownActive = true;
        OnCountdownStarted?.Invoke(CountdownRemaining);
    }

    /// <summary>
    /// Cancels any active delay countdown without advancing the queue.
    /// </summary>
    public void CancelCountdown()
    {
        if (!IsCountdownActive)
            return;

        IsCountdownActive = false;
        CountdownRemaining = 0f;
        OnCountdownCancelled?.Invoke();
    }

    /// <summary>
    /// Immediately finishes the delay countdown, forcing an advance on the next tick.
    /// </summary>
    public void FastForwardCountdown()
    {
        if (IsCountdownActive)
        {
            CountdownRemaining = 0f;
        }
    }

    /// <summary>
    /// Advances the countdown timer by deltaTime. When countdown expires,
    /// dequeues the next queued map or triggers the continuous run fallback.
    /// Returns true and outputs mapToLaunch if a map should be launched this tick.
    /// </summary>
    public bool UpdateTick(float deltaTime, out MapQueueItem? mapToLaunch)
    {
        mapToLaunch = null;

        if (!IsCountdownActive)
            return false;

        CountdownRemaining -= deltaTime;
        OnCountdownTick?.Invoke(MathF.Max(0f, CountdownRemaining));

        if (CountdownRemaining <= 0f)
        {
            IsCountdownActive = false;
            CountdownRemaining = 0f;

            if (_items.Count > 0)
            {
                mapToLaunch = DequeueNext();
                return mapToLaunch != null;
            }

            if (ContinuousRunEnabled && ContinuousRunFallbackProvider != null)
            {
                mapToLaunch = ContinuousRunFallbackProvider.Invoke();
                return mapToLaunch != null;
            }
        }

        return false;
    }

    private void ReindexSlots()
    {
        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i].SlotIndex != i)
            {
                _items[i] = _items[i] with { SlotIndex = i };
            }
        }
    }
}
