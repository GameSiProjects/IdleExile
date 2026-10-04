using System;
using System.Collections.Generic;
using IdleExile.Core.Models;
using IdleExile.Core.Simulation;
using IdleExile.Core.State;

namespace IdleExile.Core.Tests;

/// <summary>
/// Unit tests for Task 4.2: Automated 10-Slot Map Queue Manager (GDD Task 5.2).
/// Verifies FIFO queuing, capacity limits, slot re-indexing, countdown lifecycle,
/// modifier preservation, and Continuous Run fallback.
/// </summary>
public static class MapQueueTests
{
    public static void RunAll()
    {
        Console.WriteLine("[Tests] Running MapQueueTests (Task 4.2 Map Queue 10 Slots)...");

        TestEnqueueAndCapacity();
        TestFifoOrderAndDequeue();
        TestRemoveAtAndReindex();
        TestSwapSlots();
        TestClear();
        TestAutoAdvanceCountdown();
        TestCancelCountdown();
        TestContinuousRunFallback();
        TestRolledModifiersPreserved();
        TestFullCombatEngineQueueLoop();

        Console.WriteLine("[Tests] All MapQueueTests PASSED successfully!");
    }

    private static MapDefinition CreateDummyMap(string id, string name, int tier)
    {
        return new MapDefinition(
            Id: id,
            Name: name,
            Tier: tier,
            MonsterLevel: 68 + tier,
            IsFailSafe: tier == 0,
            BaseQuantity: 10 * tier,
            BasePackSize: 100,
            BossName: $"Boss of {name}",
            Description: $"Test map {name}"
        );
    }

    private static void TestEnqueueAndCapacity()
    {
        var manager = new MapQueueManager();
        AssertEqual(0, manager.Count, "Initial queue must be empty");
        AssertTrue(manager.IsEmpty, "IsEmpty should be true");
        AssertFalse(manager.IsFull, "IsFull should be false");

        // Enqueue exactly 10 maps
        for (int i = 0; i < MapQueueManager.MaxCapacity; i++)
        {
            var map = CreateDummyMap($"map_{i}", $"Map {i}", i + 1);
            bool enqueued = manager.Enqueue(map);
            AssertTrue(enqueued, $"Should enqueue map {i} within capacity");
            AssertEqual(i + 1, manager.Count, $"Queue count should be {i + 1}");
            AssertEqual(i, manager.Items[i].SlotIndex, $"Item at index {i} must have SlotIndex {i}");
        }

        AssertTrue(manager.IsFull, "Queue should be full at 10 items");
        AssertFalse(manager.IsEmpty, "Queue should not be empty");

        // Attempt to enqueue 11th map (must fail)
        var overflowMap = CreateDummyMap("overflow", "Overflow Map", 11);
        bool overflowEnqueued = manager.Enqueue(overflowMap);
        AssertFalse(overflowEnqueued, "11th map must be rejected when queue is full");
        AssertEqual(10, manager.Count, "Count must remain 10");
    }

    private static void TestFifoOrderAndDequeue()
    {
        var manager = new MapQueueManager();
        var mapA = CreateDummyMap("map_a", "Map A", 1);
        var mapB = CreateDummyMap("map_b", "Map B", 2);
        var mapC = CreateDummyMap("map_c", "Map C", 3);

        manager.Enqueue(mapA);
        manager.Enqueue(mapB);
        manager.Enqueue(mapC);

        AssertEqual("map_a", manager.NextPendingMap?.Map.Id, "NextPendingMap must be Map A");
        AssertEqual(0, manager.NextPendingMap?.SlotIndex, "NextPendingMap slot index must be 0");

        var dequeuedA = manager.DequeueNext();
        AssertNotNull(dequeuedA, "Dequeued item must not be null");
        AssertEqual("map_a", dequeuedA.Map.Id, "First dequeued must be Map A");
        AssertEqual(2, manager.Count, "Count should be 2 after dequeue");

        // Map B should now be at slot 0
        AssertEqual("map_b", manager.NextPendingMap?.Map.Id, "Map B must shift to slot 0");
        AssertEqual(0, manager.NextPendingMap?.SlotIndex, "Map B slot index must be updated to 0");
        AssertEqual("map_c", manager.Items[1].Map.Id, "Map C must shift to slot 1");
        AssertEqual(1, manager.Items[1].SlotIndex, "Map C slot index must be updated to 1");

        var dequeuedB = manager.DequeueNext();
        AssertEqual("map_b", dequeuedB?.Map.Id, "Second dequeued must be Map B");

        var dequeuedC = manager.DequeueNext();
        AssertEqual("map_c", dequeuedC?.Map.Id, "Third dequeued must be Map C");

        AssertTrue(manager.IsEmpty, "Queue should now be empty");
        AssertNull(manager.DequeueNext(), "Dequeuing empty queue must return null");
    }

    private static void TestRemoveAtAndReindex()
    {
        var manager = new MapQueueManager();
        var map0 = CreateDummyMap("map_0", "Map 0", 1);
        var map1 = CreateDummyMap("map_1", "Map 1", 2);
        var map2 = CreateDummyMap("map_2", "Map 2", 3);

        manager.Enqueue(map0);
        manager.Enqueue(map1);
        manager.Enqueue(map2);

        // Remove middle item (index 1 = map1)
        bool removed = manager.RemoveAt(1);
        AssertTrue(removed, "RemoveAt(1) must succeed");
        AssertEqual(2, manager.Count, "Queue count must be 2 after removal");
        AssertEqual("map_0", manager.Items[0].Map.Id, "Slot 0 must still be map_0");
        AssertEqual(0, manager.Items[0].SlotIndex, "Slot 0 index must be 0");
        AssertEqual("map_2", manager.Items[1].Map.Id, "Slot 1 must now be map_2");
        AssertEqual(1, manager.Items[1].SlotIndex, "Slot 1 index must be reindexed to 1");

        // Invalid index removal
        AssertFalse(manager.RemoveAt(5), "RemoveAt out of bounds must return false");
        AssertFalse(manager.RemoveAt(-1), "RemoveAt negative index must return false");
    }

    private static void TestSwapSlots()
    {
        var manager = new MapQueueManager();
        manager.Enqueue(CreateDummyMap("map_0", "Map 0", 1));
        manager.Enqueue(CreateDummyMap("map_1", "Map 1", 2));

        bool swapped = manager.Swap(0, 1);
        AssertTrue(swapped, "Swap(0, 1) must succeed");
        AssertEqual("map_1", manager.Items[0].Map.Id, "Slot 0 should now be map_1");
        AssertEqual(0, manager.Items[0].SlotIndex, "Slot 0 index must be 0");
        AssertEqual("map_0", manager.Items[1].Map.Id, "Slot 1 should now be map_0");
        AssertEqual(1, manager.Items[1].SlotIndex, "Slot 1 index must be 1");

        AssertFalse(manager.Swap(0, 0), "Swap same index should return false");
        AssertFalse(manager.Swap(0, 5), "Swap invalid index should return false");
    }

    private static void TestClear()
    {
        var manager = new MapQueueManager();
        for (int i = 0; i < 5; i++)
        {
            manager.Enqueue(CreateDummyMap($"m_{i}", $"M {i}", i + 1));
        }

        AssertEqual(5, manager.Count, "Count must be 5 before clear");
        manager.Clear();
        AssertEqual(0, manager.Count, "Count must be 0 after clear");
        AssertTrue(manager.IsEmpty, "Queue must be empty after clear");
    }

    private static void TestAutoAdvanceCountdown()
    {
        var manager = new MapQueueManager
        {
            AutoAdvanceEnabled = true,
            AutoAdvanceDelaySeconds = 3.0f
        };

        var map = CreateDummyMap("test_map", "Test Map", 1);
        manager.Enqueue(map);

        manager.StartCountdown();
        AssertTrue(manager.IsCountdownActive, "Countdown should be active after StartCountdown");
        AssertEqual(3.0f, manager.CountdownRemaining, "Countdown remaining should start at 3.0s");

        // Tick 1.0s -> remaining 2.0s, no map launched
        bool launched = manager.UpdateTick(1.0f, out var launchedItem);
        AssertFalse(launched, "Map should not launch while countdown is active");
        AssertNull(launchedItem, "Launched item should be null");
        AssertTrue(manager.IsCountdownActive, "Countdown should still be active");
        AssertEqual(2.0f, manager.CountdownRemaining, "Remaining time should be 2.0s");

        // Tick 2.0s -> countdown expires, launches map
        launched = manager.UpdateTick(2.0f, out launchedItem);
        AssertTrue(launched, "Map must launch when countdown completes");
        AssertNotNull(launchedItem, "Launched item must not be null");
        AssertEqual("test_map", launchedItem.Map.Id, "Launched map ID mismatch");
        AssertFalse(manager.IsCountdownActive, "Countdown should no longer be active");
        AssertEqual(0, manager.Count, "Queue count should be 0 after auto-launch");
    }

    private static void TestCancelCountdown()
    {
        var manager = new MapQueueManager
        {
            AutoAdvanceEnabled = true
        };
        manager.Enqueue(CreateDummyMap("m1", "M1", 1));

        manager.StartCountdown(3.0f);
        AssertTrue(manager.IsCountdownActive, "Countdown should be active");

        manager.CancelCountdown();
        AssertFalse(manager.IsCountdownActive, "Countdown must be inactive after cancel");
        AssertEqual(0f, manager.CountdownRemaining, "Countdown remaining must be 0");

        // Tick past countdown time -> should not launch
        bool launched = manager.UpdateTick(5.0f, out var launchedItem);
        AssertFalse(launched, "Should not launch when cancelled");
        AssertNull(launchedItem, "Launched item must be null");
        AssertEqual(1, manager.Count, "Map must remain in queue");
    }

    private static void TestContinuousRunFallback()
    {
        var manager = new MapQueueManager
        {
            AutoAdvanceEnabled = true,
            ContinuousRunEnabled = true
        };

        var hollowDef = new MapDefinition(
            Id: "endless_hollow",
            Name: "The Endless Hollow",
            Tier: 0,
            MonsterLevel: 1,
            IsFailSafe: true,
            BaseQuantity: 0,
            BasePackSize: 100,
            BossName: null,
            Description: "A pocket dimension."
        );

        manager.ContinuousRunFallbackProvider = () =>
            new MapQueueItem(0, hollowDef, Array.Empty<RolledMapModifier>(), "Normal");

        // Queue is empty, but ContinuousRunEnabled is true
        AssertTrue(manager.IsEmpty, "Queue is empty");
        manager.StartCountdown(3.0f);
        AssertTrue(manager.IsCountdownActive, "Countdown should start for continuous run");

        bool launched = manager.UpdateTick(3.0f, out var fallbackItem);
        AssertTrue(launched, "Continuous run must launch fallback map when queue is empty");
        AssertNotNull(fallbackItem, "Fallback item must not be null");
        AssertEqual("endless_hollow", fallbackItem.Map.Id, "Fallback map ID mismatch");
        AssertEqual(0, fallbackItem.Tier, "Fallback tier must be 0");
    }

    private static void TestRolledModifiersPreserved()
    {
        var manager = new MapQueueManager();
        var map = CreateDummyMap("crimson", "Crimson Temple", 14);

        var mod1 = new MapModifierDefinition("mod_pack", "+{0}% Monster Pack Size", 10, 20, 15f, 25f);
        var mod2 = new MapModifierDefinition("mod_reflect", "Reflects {0}% Physical Damage", 30, 10, 10f, 15f);

        var rolledList = new List<RolledMapModifier>
        {
            new(mod1, 20f),
            new(mod2, 12f)
        };

        manager.Enqueue(map, rolledList, "Rare");
        var queued = manager.NextPendingMap;
        AssertNotNull(queued, "Queued item must not be null");
        AssertEqual("Rare", queued.Rarity, "Rarity must be Rare");
        AssertEqual(2, queued.Modifiers.Count, "Modifiers count must be 2");
        AssertEqual(140 + 10 + 30, queued.TotalQuantity, "Total quantity must match base + mods");
        AssertEqual(100 + 20 + 10, queued.TotalPackSize, "Total pack size must match base + mods");
        AssertTrue(queued.HasDangerousModifiers, "Map must flag dangerous reflect modifier");

        var dequeued = manager.DequeueNext();
        AssertNotNull(dequeued, "Dequeued must not be null");
        AssertEqual("Rare", dequeued.Rarity, "Dequeued rarity mismatch");
        AssertEqual(2, dequeued.Modifiers.Count, "Dequeued modifiers count mismatch");
    }

    private static void TestFullCombatEngineQueueLoop()
    {
        var player = new PlayerState();
        player.Initialize(500f, 100f, 200f);
        var loadout = new SkillLoadout();
        var engine = new CombatEngine(player, loadout);

        var queue = new MapQueueManager
        {
            AutoAdvanceEnabled = true,
            AutoAdvanceDelaySeconds = 3.0f
        };

        var map1 = CreateDummyMap("map1", "First Map", 1);
        var map2 = CreateDummyMap("map2", "Second Map", 2);

        queue.Enqueue(map2);

        // When map1 completes, queue should start countdown
        engine.OnMapCompleted += args =>
        {
            if (queue.AutoAdvanceEnabled && (queue.Count > 0 || queue.ContinuousRunEnabled))
            {
                queue.StartCountdown(3.0f);
            }
        };

        engine.StartMap(new MapRunConfig(map1, Array.Empty<RolledMapModifier>()));

        // Simulate killing all waves and completing map1
        while (!engine.IsMapComplete)
        {
            if (engine.CurrentTarget != null && engine.CurrentTarget.IsAlive)
            {
                engine.CurrentTarget.TakeDamage(10000f, DamageType.Physical);
            }
            engine.Update(0.1f);
        }

        AssertTrue(engine.IsMapComplete, "First map must be cleared");
        AssertTrue(queue.IsCountdownActive, "Queue countdown must be active following map completion");

        // Advance countdown by 3.0s
        bool nextLaunched = queue.UpdateTick(3.0f, out var nextMap);
        AssertTrue(nextLaunched, "Next map must be launched after 3.0s delay");
        AssertNotNull(nextMap, "Next map must not be null");
        AssertEqual("map2", nextMap.Map.Id, "Second map must be launched from queue");
        AssertEqual(0, queue.Count, "Queue count should now be 0");
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"Assertion Failed: Expected '{expected}', but was '{actual}'. {message}");
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition) throw new Exception($"Assertion Failed: {message}");
    }

    private static void AssertFalse(bool condition, string message)
    {
        if (condition) throw new Exception($"Assertion Failed: {message}");
    }

    private static void AssertNotNull([System.Diagnostics.CodeAnalysis.NotNull] object? obj, string message)
    {
        if (obj == null) throw new Exception($"Assertion Failed: {message} (Object was null)");
    }

    private static void AssertNull(object? obj, string message)
    {
        if (obj != null) throw new Exception($"Assertion Failed: {message} (Expected null)");
    }
}
