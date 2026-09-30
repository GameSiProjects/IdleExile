using System;
using System.Collections.Generic;
using System.Linq;
using IdleExile.Core.Models;
using IdleExile.Core.Simulation;
using IdleExile.Core.State;

namespace IdleExile.Core.Tests;

/// <summary>
/// Unit tests for Task 1.4: Combat Infrastructure Prerequisites.
/// Validates:
/// - Task 1.4a: AilmentTracker (PoE refresh rules, stacking poison, DoT, CC slow/freeze)
/// - Task 1.4b: SkillLoadout & ActionTimeAccumulator (PoE 2 decoupled gem linking, tags, mana cost, 30Hz accumulator)
/// - Task 1.4c: MapModifierRuntime (-Max Res, extra monster damage, immunities, reflect)
/// - Task 1.4d: TargetingStrategy (Boss > Rare > Magic > Normal, stickiness, AoE cleave)
/// - Task 1.4e: DeathHandler (-10% EXP penalty, death metrics, 2s auto-respawn, emergency portal)
/// </summary>
public static class CombatInfrastructureTests
{
    public static void RunAll()
    {
        Console.WriteLine("[Tests] Running CombatInfrastructureTests (Task 1.4)...");

        // 1.4a: Ailment Tracker Tests
        TestAilmentTrackerNonStackingRefresh();
        TestAilmentTrackerPoisonStacking();
        TestAilmentTrackerChillAndFreeze();
        TestAilmentTrackerShockEffect();
        TestAilmentTrackerTickAndExpiration();

        // 1.4b: Skill Loadout & Action Time Tests
        TestSkillSocketSetupAndTagMatching();
        TestSkillLoadoutDamageContextAndManaCost();
        TestActionTimeAccumulatorHighResolution();

        // 1.4c: Map Modifier Runtime Tests
        TestMapModifierRuntimeDefenses();
        TestMapModifierRuntimeMonsterDamage();
        TestMapModifierRuntimeAilmentImmunities();
        TestMapModifierRuntimeDamageReflect();

        // 1.4d: Targeting Strategy Tests
        TestTargetingPriorityHierarchy();
        TestTargetingStickiness();
        TestTargetingAoECleave();

        // 1.4e: Death & Recovery Flow Tests
        TestDeathExpPenaltyAndMetrics();
        TestAutoRespawnAfterDelay();
        TestEmergencyPortalEscape();

        Console.WriteLine("[Tests] All CombatInfrastructureTests PASSED successfully!");
    }

    private static void AssertEqual(float expected, float actual, float tolerance = 0.001f, string message = "")
    {
        if (MathF.Abs(expected - actual) > tolerance)
        {
            throw new Exception($"Assertion failed: Expected {expected}, got {actual}. {message}");
        }
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception($"Assertion failed: {message}");
        }
    }

    // ─────────────────────────────────────────────
    //  1.4a Tests: Ailment Tracker
    // ─────────────────────────────────────────────

    private static void TestAilmentTrackerNonStackingRefresh()
    {
        var tracker = new AilmentTracker();

        // Apply initial Ignite (100 DPS, 4s)
        var ignite1 = new AilmentInstance(
            Type: AilmentType.Ignite,
            SourceDamageType: DamageType.Fire,
            DamagePerSecond: 100f,
            EffectPercent: 0f,
            DurationSeconds: 4.0f,
            IsStacking: false
        );
        tracker.ApplyAilment(ignite1);
        AssertEqual(1, tracker.ActiveAilments.Count, 0.001f, "Should have 1 active ignite");
        AssertEqual(100f, tracker.TotalDoTDps, 0.001f, "DoT DPS should be 100");

        // Apply weaker Ignite (60 DPS, 4s) -> should be discarded/ignored, keeping 100 DPS
        var weakerIgnite = new AilmentInstance(
            Type: AilmentType.Ignite,
            SourceDamageType: DamageType.Fire,
            DamagePerSecond: 60f,
            EffectPercent: 0f,
            DurationSeconds: 4.0f,
            IsStacking: false
        );
        tracker.ApplyAilment(weakerIgnite);
        AssertEqual(1, tracker.ActiveAilments.Count, 0.001f, "Should still have 1 active ignite");
        AssertEqual(100f, tracker.TotalDoTDps, 0.001f, "Weaker ignite must not replace stronger one");

        // Apply stronger Ignite (150 DPS, 4s) -> should replace the weaker one
        var strongerIgnite = new AilmentInstance(
            Type: AilmentType.Ignite,
            SourceDamageType: DamageType.Fire,
            DamagePerSecond: 150f,
            EffectPercent: 0f,
            DurationSeconds: 4.0f,
            IsStacking: false
        );
        tracker.ApplyAilment(strongerIgnite);
        AssertEqual(1, tracker.ActiveAilments.Count, 0.001f, "Should still have 1 active ignite");
        AssertEqual(150f, tracker.TotalDoTDps, 0.001f, "Stronger ignite must replace existing instance");
    }

    private static void TestAilmentTrackerPoisonStacking()
    {
        var tracker = new AilmentTracker();

        // Apply 3 Poison instances (each 40 DPS, 2s)
        for (int i = 0; i < 3; i++)
        {
            tracker.ApplyAilment(new AilmentInstance(
                Type: AilmentType.Poison,
                SourceDamageType: DamageType.Chaos,
                DamagePerSecond: 40f,
                EffectPercent: 0f,
                DurationSeconds: 2.0f,
                IsStacking: true
            ));
        }

        AssertEqual(3, tracker.ActiveAilments.Count, 0.001f, "Should have 3 poison stacks");
        AssertEqual(120f, tracker.TotalDoTDps, 0.001f, "Poison stacks must combine DPS (3 * 40 = 120)");

        // Tick 1 second (30 ticks of ~0.0333s or 1.0s direct)
        var result = tracker.Tick(1.0f);
        AssertEqual(120f, result.TotalDoTDamage, 0.01f, "1 second of 120 DPS should deal 120 damage");
        AssertEqual(120f, result.ChaosDoTDamage, 0.01f, "Damage must be Chaos type");
        AssertEqual(3, tracker.ActiveAilments.Count, 0.001f, "Stacks should still be active at 1.0s remaining");

        // Tick another 1.1s -> all 3 poisons should expire
        var result2 = tracker.Tick(1.1f);
        AssertEqual(0, tracker.ActiveAilments.Count, 0.001f, "Poisons must expire after 2s duration");
        AssertEqual(3, result2.ExpiredTypes.Count, 0.001f, "Must report 3 expired ailments");
    }

    private static void TestAilmentTrackerChillAndFreeze()
    {
        var tracker = new AilmentTracker();

        // 1. Chill: 20% slow
        tracker.ApplyAilment(new AilmentInstance(
            Type: AilmentType.Chill,
            SourceDamageType: DamageType.Cold,
            DamagePerSecond: 0f,
            EffectPercent: 20f,
            DurationSeconds: 2.0f,
            IsStacking: false
        ));

        AssertEqual(20f, tracker.CurrentChillSlowPercent, 0.001f, "Chill slow should be 20%");
        AssertEqual(0.8f, tracker.ActionSpeedMultiplier, 0.001f, "Action speed multiplier should be 1 - 0.20 = 0.80");
        AssertTrue(!tracker.IsFrozen, "Target should not be frozen");

        // 2. Freeze: Prevents action
        tracker.ApplyAilment(new AilmentInstance(
            Type: AilmentType.Freeze,
            SourceDamageType: DamageType.Cold,
            DamagePerSecond: 0f,
            EffectPercent: 100f,
            DurationSeconds: 1.0f,
            IsStacking: false,
            PreventsAction: true
        ));

        AssertTrue(tracker.IsFrozen, "Target should be frozen");
        AssertEqual(0f, tracker.ActionSpeedMultiplier, 0.001f, "Frozen target must have 0 action speed");

        // Tick 1.1s: Freeze expires, Chill still has 0.9s
        tracker.Tick(1.1f);
        AssertTrue(!tracker.IsFrozen, "Freeze should have expired");
        AssertEqual(20f, tracker.CurrentChillSlowPercent, 0.001f, "Chill should still be active");
        AssertEqual(0.8f, tracker.ActionSpeedMultiplier, 0.001f, "Action speed should return to chilled rate");
    }

    private static void TestAilmentTrackerShockEffect()
    {
        var tracker = new AilmentTracker();

        tracker.ApplyAilment(new AilmentInstance(
            Type: AilmentType.Shock,
            SourceDamageType: DamageType.Lightning,
            DamagePerSecond: 0f,
            EffectPercent: 25f,
            DurationSeconds: 2.0f,
            IsStacking: false
        ));

        AssertEqual(25f, tracker.CurrentShockDamageTakenPercent, 0.001f, "Shock should provide 25% damage taken");

        // Test cap: Apply 60% shock -> capped at 50%
        tracker.ApplyAilment(new AilmentInstance(
            Type: AilmentType.Shock,
            SourceDamageType: DamageType.Lightning,
            DamagePerSecond: 0f,
            EffectPercent: 60f,
            DurationSeconds: 2.0f,
            IsStacking: false
        ));

        AssertEqual(50f, tracker.CurrentShockDamageTakenPercent, 0.001f, "Shock effect must be capped at 50%");
    }

    private static void TestAilmentTrackerTickAndExpiration()
    {
        var tracker = new AilmentTracker();
        tracker.ApplyAilment(new AilmentInstance(
            Type: AilmentType.Ignite,
            SourceDamageType: DamageType.Fire,
            DamagePerSecond: 50f,
            EffectPercent: 0f,
            DurationSeconds: 1.0f,
            IsStacking: false
        ));

        // 30 ticks of ~33.33ms (total 1.0s)
        float dt = 1f / 30f;
        float totalDamage = 0f;

        for (int i = 0; i < 30; i++)
        {
            var res = tracker.Tick(dt);
            totalDamage += res.TotalDoTDamage;
        }

        AssertEqual(50f, totalDamage, 0.5f, "Total damage over 1 second should equal 50");
        AssertTrue(!tracker.HasAilment(AilmentType.Ignite), "Ignite should have expired after 1 second");
    }

    // ─────────────────────────────────────────────
    //  1.4b Tests: Skill Loadout & Action Time
    // ─────────────────────────────────────────────

    private static void TestSkillSocketSetupAndTagMatching()
    {
        var fireball = new GemDefinition(
            Id: "fireball",
            Name: "Fireball",
            Category: "Active",
            Color: "Blue",
            Tags: ["spell", "projectile", "fire", "aoe"],
            RequiredTags: null,
            BaseStats: new Dictionary<string, object>
            {
                ["baseDamageMin"] = 25f,
                ["baseDamageMax"] = 38f,
                ["damageType"] = "Fire",
                ["castTime"] = 0.75f,
                ["criticalStrikeChance"] = 6.0f,
                ["manaCost"] = 8f
            },
            Modifiers: null,
            Description: "Fireball"
        );

        var cleave = new GemDefinition(
            Id: "cleave",
            Name: "Cleave",
            Category: "Active",
            Color: "Red",
            Tags: ["attack", "melee", "aoe", "physical"],
            RequiredTags: null,
            BaseStats: new Dictionary<string, object>
            {
                ["baseDamageMin"] = 10f,
                ["baseDamageMax"] = 20f,
                ["damageType"] = "Physical",
                ["attackSpeedMultiplier"] = 100f,
                ["manaCost"] = 6f
            },
            Modifiers: null,
            Description: "Cleave"
        );

        var gmp = new GemDefinition(
            Id: "greater_multiple_projectiles",
            Name: "Greater Multiple Projectiles Support",
            Category: "Support",
            Color: "Green",
            Tags: null,
            RequiredTags: ["projectile"],
            BaseStats: null,
            Modifiers: new Dictionary<string, object>
            {
                ["damageMultiplier"] = -25f,
                ["manaMultiplier"] = 150f
            },
            Description: "GMP Support"
        );

        var setup = new SkillSocketSetup();
        AssertTrue(setup.SetActiveGem(fireball), "Setting Fireball should succeed");
        AssertTrue(setup.AddSupportGem(gmp), "Adding GMP should succeed");

        // Fireball has 'projectile', so GMP is compatible
        AssertTrue(setup.IsSupportCompatible(gmp), "GMP should be compatible with Fireball (projectile tag)");
        AssertEqual(1, setup.ValidLinkedSupports.Count(), 0.001f, "GMP should be validly linked to Fireball");

        // Swap active gem to Cleave (no projectile tag)
        setup.SetActiveGem(cleave);
        AssertTrue(!setup.IsSupportCompatible(gmp), "GMP should NOT be compatible with Cleave (no projectile tag)");
        AssertEqual(0, setup.ValidLinkedSupports.Count(), 0.001f, "GMP should not link to Cleave");
    }

    private static void TestSkillLoadoutDamageContextAndManaCost()
    {
        var fireball = new GemDefinition(
            Id: "fireball",
            Name: "Fireball",
            Category: "Active",
            Color: "Blue",
            Tags: ["spell", "projectile", "fire"],
            RequiredTags: null,
            BaseStats: new Dictionary<string, object>
            {
                ["baseDamageMin"] = 100f,
                ["baseDamageMax"] = 150f,
                ["damageType"] = "Fire",
                ["criticalStrikeChance"] = 7.0f,
                ["manaCost"] = 10f
            },
            Modifiers: null,
            Description: "Fireball"
        );

        var support1 = new GemDefinition(
            Id: "support1",
            Name: "Support 1",
            Category: "Support",
            Color: "Blue",
            Tags: null,
            RequiredTags: ["spell"],
            BaseStats: null,
            Modifiers: new Dictionary<string, object>
            {
                ["damageMultiplier"] = 30f, // 30% More
                ["manaMultiplier"] = 130f
            },
            Description: "30% More Damage"
        );

        var support2 = new GemDefinition(
            Id: "support2",
            Name: "Support 2",
            Category: "Support",
            Color: "Blue",
            Tags: null,
            RequiredTags: ["spell"],
            BaseStats: null,
            Modifiers: new Dictionary<string, object>
            {
                ["damageMultiplier"] = 40f, // 40% More
                ["manaMultiplier"] = 120f
            },
            Description: "40% More Damage"
        );

        var setup = new SkillSocketSetup(fireball);
        setup.AddSupportGem(support1);
        setup.AddSupportGem(support2);

        // Effective mana cost: 10 * 1.30 * 1.20 = 15.6
        float manaCost = setup.CalculateEffectiveManaCost();
        AssertEqual(15.6f, manaCost, 0.01f, "Mana cost should compound support multipliers (10 * 1.30 * 1.20 = 15.6)");

        // Damage modifier context
        var context = setup.BuildDamageContext();
        AssertEqual(7.0f, context.BaseCritChance, 0.001f, "Base crit chance should match gem (7.0%)");
        AssertTrue(!context.IsAttack, "Spell skill must set IsAttack = false");

        // Check global More product: 1.30 * 1.40 = 1.82
        AssertEqual(1.82f, context.Global.MoreProduct, 0.001f, "Two support gems (30% more and 40% more) should multiply to 1.82");
    }

    private static void TestActionTimeAccumulatorHighResolution()
    {
        var accumulator = new ActionTimeAccumulator();

        // 2.0 Attacks per second -> Interval = 0.5s (15 ticks at 30Hz)
        float aps = 2.0f;
        float dt = 1f / 30f; // ~0.0333s

        int actionCount = 0;
        for (int tick = 0; tick < 30; tick++) // 1 second total
        {
            accumulator.Tick(dt, aps);
            if (accumulator.TryConsumeAction())
            {
                actionCount++;
            }
        }

        AssertEqual(2, actionCount, 0.001f, "At 2.0 APS, exactly 2 actions should trigger in 1 second (30 ticks)");

        // Test Chill slow: 50% slow on 2.0 APS -> Effective 1.0 APS -> 1 action in 30 ticks
        accumulator.Reset();
        actionCount = 0;
        for (int tick = 0; tick < 30; tick++)
        {
            accumulator.Tick(dt, aps, actionSpeedMultiplier: 0.5f);
            if (accumulator.TryConsumeAction())
            {
                actionCount++;
            }
        }
        AssertEqual(1, actionCount, 0.001f, "With 50% action slow, exactly 1 action should trigger in 30 ticks");

        // Test Freeze: 0 action speed -> 0 actions
        accumulator.Reset();
        actionCount = 0;
        for (int tick = 0; tick < 30; tick++)
        {
            accumulator.Tick(dt, aps, actionSpeedMultiplier: 0f);
            if (accumulator.TryConsumeAction()) actionCount++;
        }
        AssertEqual(0, actionCount, 0.001f, "When frozen (0 action speed), 0 actions should trigger");
    }

    // ─────────────────────────────────────────────
    //  1.4c Tests: Map Modifier Runtime
    // ─────────────────────────────────────────────

    private static void TestMapModifierRuntimeDefenses()
    {
        var rolledMods = new List<RolledMapModifier>
        {
            new(new MapModifierDefinition("player_reduced_max_res", "-{0}% max res", 35, 20, 10, 15), 12f)
        };

        var runtime = new MapModifierRuntime(rolledMods);

        var playerStats = new DefensiveStats
        {
            FireResistance = 85f,
            MaxFireResistance = 75f,
            ColdResistance = 85f,
            MaxColdResistance = 75f
        };

        var modifiedStats = runtime.ApplyToDefensiveStats(playerStats);
        AssertEqual(63f, modifiedStats.MaxFireResistance, 0.001f, "Max Fire Res should be 75 - 12 = 63%");
        AssertEqual(63f, modifiedStats.MaxColdResistance, 0.001f, "Max Cold Res should be 75 - 12 = 63%");
        AssertEqual(63f, modifiedStats.GetEffectiveResistance(DamageType.Fire), 0.001f, "Effective Fire Res capped at 63%");
    }

    private static void TestMapModifierRuntimeMonsterDamage()
    {
        var rolledMods = new List<RolledMapModifier>
        {
            new(new MapModifierDefinition("extra_damage_cold", "Extra Cold", 15, 10, 30, 50), 40f)
        };

        var runtime = new MapModifierRuntime(rolledMods);
        var pool = runtime.ModifyMonsterDamage(100f, DamageType.Physical);

        AssertEqual(100f, pool.Physical.Average, 0.001f, "Base Physical should remain 100");
        AssertEqual(40f, pool.Cold.Average, 0.001f, "Should add 40% extra damage as Cold (40)");
    }

    private static void TestMapModifierRuntimeAilmentImmunities()
    {
        var rolledMods = new List<RolledMapModifier>
        {
            new(new MapModifierDefinition("cannot_be_chilled", "Cannot be Chilled", 20, 10, 0, 0), 1f),
            new(new MapModifierDefinition("cannot_be_frozen", "Cannot be Frozen", 20, 10, 0, 0), 1f)
        };

        var runtime = new MapModifierRuntime(rolledMods);
        AssertTrue(!runtime.CanInflictAilmentOnMonster(AilmentType.Chill), "Monsters cannot be chilled");
        AssertTrue(!runtime.CanInflictAilmentOnMonster(AilmentType.Freeze), "Monsters cannot be frozen");
        AssertTrue(runtime.CanInflictAilmentOnMonster(AilmentType.Ignite), "Monsters can still be ignited");
    }

    private static void TestMapModifierRuntimeDamageReflect()
    {
        var rolledMods = new List<RolledMapModifier>
        {
            new(new MapModifierDefinition("monster_reflect_elemental", "Reflect 18% Elemental", 40, 15, 15, 20), 18f)
        };

        var runtime = new MapModifierRuntime(rolledMods);
        var hitBreakdown = new HitDamageBreakdown(
            physical: 100f,
            fire: 200f,
            cold: 100f,
            lightning: 0f,
            chaos: 0f
        );

        var reflect = runtime.CalculateReflectedDamage(hitBreakdown);
        AssertTrue(reflect.HasReflectedDamage, "Should reflect elemental damage");
        AssertEqual(0f, reflect.ReflectedPhysical, 0.001f, "Physical is not reflected");
        AssertEqual(36f, reflect.ReflectedFire, 0.001f, "18% of 200 Fire = 36");
        AssertEqual(18f, reflect.ReflectedCold, 0.001f, "18% of 100 Cold = 18");
        AssertEqual(54f, reflect.TotalReflectedDamage, 0.001f, "Total reflected = 36 + 18 = 54");
    }

    // ─────────────────────────────────────────────
    //  1.4d Tests: Targeting Strategy
    // ─────────────────────────────────────────────

    private static void TestTargetingPriorityHierarchy()
    {
        var targeting = new TargetingStrategy();

        var trash = new MonsterInstance(1, "Trash", MonsterRarity.Normal, 1, 100f, DamageType.Physical, 5, 10, 1, 5, 130, 50, 10, 1, 1);
        var magic = new MonsterInstance(2, "Magic", MonsterRarity.Magic, 1, 200f, DamageType.Physical, 5, 10, 1, 5, 130, 50, 20, 1, 1);
        var rare = new MonsterInstance(3, "Rare", MonsterRarity.Rare, 1, 500f, DamageType.Physical, 5, 10, 1, 5, 130, 50, 50, 1, 1);
        var boss = new MonsterInstance(4, "Boss", MonsterRarity.Boss, 1, 2000f, DamageType.Physical, 5, 10, 1, 5, 130, 50, 200, 1, 1);

        var candidates = new List<MonsterInstance> { trash, magic, rare, boss };

        var target = targeting.SelectTarget(candidates);
        AssertTrue(target?.InstanceId == boss.InstanceId, "Targeting must prioritize Boss over all others");

        candidates.Remove(boss);
        target = targeting.SelectTarget(candidates);
        AssertTrue(target?.InstanceId == rare.InstanceId, "Targeting must prioritize Rare over Magic/Normal");

        candidates.Remove(rare);
        target = targeting.SelectTarget(candidates);
        AssertTrue(target?.InstanceId == magic.InstanceId, "Targeting must prioritize Magic over Normal");
    }

    private static void TestTargetingStickiness()
    {
        var targeting = new TargetingStrategy();

        var mob1 = new MonsterInstance(1, "Mob1", MonsterRarity.Normal, 1, 100f, DamageType.Physical, 5, 10, 1, 5, 130, 50, 10, 1, 1);
        var mob2 = new MonsterInstance(2, "Mob2", MonsterRarity.Normal, 1, 80f, DamageType.Physical, 5, 10, 1, 5, 130, 50, 10, 1, 1);

        var candidates = new List<MonsterInstance> { mob1, mob2 };

        // Currently attacking mob1
        var target = targeting.SelectTarget(candidates, currentTarget: mob1);
        AssertTrue(target?.InstanceId == mob1.InstanceId, "Must remain sticky on mob1 while alive");

        // Higher priority monster enters combat (Rare)
        var rare = new MonsterInstance(3, "Rare", MonsterRarity.Rare, 1, 300f, DamageType.Physical, 5, 10, 1, 5, 130, 50, 50, 1, 1);
        candidates.Add(rare);

        target = targeting.SelectTarget(candidates, currentTarget: mob1);
        AssertTrue(target?.InstanceId == rare.InstanceId, "Must switch target when higher priority monster enters");
    }

    private static void TestTargetingAoECleave()
    {
        var targeting = new TargetingStrategy();

        var mob1 = new MonsterInstance(1, "Mob1", MonsterRarity.Normal, 1, 100f, DamageType.Physical, 5, 10, 1, 5, 130, 50, 10, 1, 1);
        var mob2 = new MonsterInstance(2, "Mob2", MonsterRarity.Normal, 1, 100f, DamageType.Physical, 5, 10, 1, 5, 130, 50, 10, 1, 1);
        var mob3 = new MonsterInstance(3, "Mob3", MonsterRarity.Normal, 1, 100f, DamageType.Physical, 5, 10, 1, 5, 130, 50, 10, 1, 1);

        var candidates = new List<MonsterInstance> { mob1, mob2, mob3 };

        var aoeTargets = targeting.SelectAoETargets(candidates, primaryTarget: mob1, maxTargets: 2);
        AssertEqual(2, aoeTargets.Count, 0.001f, "AoE maxTargets 2 must return exactly 2 targets");
        AssertTrue(aoeTargets[0].InstanceId == mob1.InstanceId, "Index 0 must be primary target");
    }

    // ─────────────────────────────────────────────
    //  1.4e Tests: Death & Recovery Flow
    // ─────────────────────────────────────────────

    private static void TestDeathExpPenaltyAndMetrics()
    {
        var player = new PlayerState();
        player.Initialize(maxLife: 1000f, maxEnergyShield: 0f, maxMana: 200f);

        // Give player level 5 with 500 EXP
        player.AddExperience(PlayerState.GetExperienceForLevel(1) + 500f);

        float req = player.ExperienceToNextLevel;
        float expBefore = player.CurrentExperience;

        var deathHandler = new DeathHandler();
        var recap = deathHandler.HandleDeath(player, "Tidal Behemoth");

        AssertEqual(1, deathHandler.TotalDeaths, 0.001f, "Total deaths should be 1");
        AssertEqual(1, deathHandler.MapDeaths, 0.001f, "Map deaths should be 1");

        float expectedPenalty = req * 0.10f; // 10% penalty
        AssertEqual(expectedPenalty, recap.ExpLost, 0.1f, "Penalty must equal 10% of level requirement");
        AssertEqual(MathF.Max(0f, expBefore - expectedPenalty), player.CurrentExperience, 0.1f, "EXP deducted correctly");
        AssertTrue(deathHandler.IsAwaitingRespawn, "Should be awaiting respawn");
    }

    private static void TestAutoRespawnAfterDelay()
    {
        var player = new PlayerState();
        player.Initialize(maxLife: 1000f, maxEnergyShield: 500f, maxMana: 300f);

        // Player takes lethal damage
        player.ApplyDamage(2000f, false, false, 0f);
        AssertTrue(!player.IsAlive, "Player should be dead (0 HP)");

        var ailments = new AilmentTracker();
        ailments.ApplyAilment(new AilmentInstance(AilmentType.Ignite, DamageType.Fire, 100f, 0f, 4f, false));
        AssertEqual(1, ailments.ActiveAilments.Count, 0.001f, "Ailment active before death");

        var deathHandler = new DeathHandler { RespawnDelay = 2.0f };
        deathHandler.HandleDeath(player);

        // Tick 1.0s -> not yet respawned
        bool respawnedAt1s = deathHandler.Tick(1.0f, player, ailments);
        AssertTrue(!respawnedAt1s, "Should not respawn before 2.0s");
        AssertTrue(!player.IsAlive, "Player should still be dead at 1.0s");

        // Tick another 1.1s (total 2.1s >= 2.0s) -> respawn occurs!
        bool respawnedAt2s = deathHandler.Tick(1.1f, player, ailments);
        AssertTrue(respawnedAt2s, "Should respawn when 2.0s elapsed");
        AssertTrue(player.IsAlive, "Player must be alive after respawn");
        AssertEqual(1000f, player.CurrentLife, 0.001f, "Life should be refilled to max (1000)");
        AssertEqual(500f, player.CurrentEnergyShield, 0.001f, "ES should be refilled to max (500)");
        AssertEqual(300f, player.CurrentMana, 0.001f, "Mana should be refilled to max (300)");
        AssertEqual(0, ailments.ActiveAilments.Count, 0.001f, "All ailments must be cleansed upon respawn");
    }

    private static void TestEmergencyPortalEscape()
    {
        var player = new PlayerState();
        player.Initialize(maxLife: 1000f, maxEnergyShield: 0f, maxMana: 200f);

        var deathHandler = new DeathHandler();
        bool portalUsed = false;
        deathHandler.OnEmergencyPortalUsed += () => portalUsed = true;

        bool escapeSuccess = deathHandler.TryTriggerEmergencyPortal(player);
        AssertTrue(escapeSuccess, "Escape should succeed while alive");
        AssertTrue(portalUsed, "OnEmergencyPortalUsed event should fire");

        // If player is dead, emergency portal cannot be used
        player.ApplyDamage(2000f, false, false, 0f);
        deathHandler.HandleDeath(player);
        bool escapeWhileDead = deathHandler.TryTriggerEmergencyPortal(player);
        AssertTrue(!escapeWhileDead, "Emergency portal cannot be used while dead");
    }
}
