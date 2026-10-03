using System;
using System.Collections.Generic;
using System.Linq;
using IdleExile.Core.Models;
using IdleExile.Core.Simulation;
using IdleExile.Core.State;

namespace IdleExile.Core.Tests;

/// <summary>
/// Comprehensive unit tests for Task 2.7: Sequential Corridor Monster Layout (CorridorMonsterLayout).
/// Validates:
/// 1. Canonical Sequential Pack Order (Pack 1 -> Pack 2 -> League Encounter -> Pack 3 -> Pack 4 -> Boss).
/// 2. Vanguard Formations (Horizontal Line & V-Shape Wedge).
/// 3. Rare Commander & Phalanx (Golden aura, center-rear positioning, front guard arc).
/// 4. Flanker Pincer (Left wall & right wall ambush flanking positions).
/// 5. Gate Phalanx (Elite shield wall before Fog Gate).
/// 6. Roadside League Encounters (Essence Monolith, Breach Crevice, Sulphite Vein).
/// 7. Proximity Trigger & Dormant Essence Awakening.
/// 8. 30Hz CombatEngine full corridor integration.
/// </summary>
public static class CorridorMonsterLayoutTests
{
    public static void RunAll()
    {
        Console.WriteLine("[Tests] Running CorridorMonsterLayoutTests (Task 2.7)...");

        TestCanonicalSequentialPackOrder();
        TestVanguardFormations();
        TestCommanderPhalanxStructure();
        TestFlankerPincerPositions();
        TestGatePhalanxShieldWall();
        TestLeagueEncounterVariants();
        TestProximityTriggerAndEssenceAwakening();
        TestCombatEngineCorridorIntegration();
        TestMonsterAggroOnlyWhenCloseAndMarchingWhenOutOfCombat();
        TestPackAlertAndDamageAggroTriggers();
        TestResumingMarchTowardsEndAfterPackClear();

        Console.WriteLine("[Tests] All CorridorMonsterLayoutTests PASSED successfully!");
    }

    private static void AssertEqual(float expected, float actual, float tolerance = 0.001f, string message = "")
    {
        if (MathF.Abs(expected - actual) > tolerance)
        {
            throw new Exception($"Assertion failed: Expected {expected:F4}, got {actual:F4}. {message}");
        }
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception($"Assertion failed: {message}");
        }
    }

    private static MapRunConfig CreateTestConfig(int packSize = 100)
    {
        var mapDef = new MapDefinition(
            Id: "strand",
            Name: "Strand Map",
            Tier: 1,
            MonsterLevel: 1,
            IsFailSafe: false,
            BaseQuantity: 10,
            BasePackSize: packSize,
            BossName: "Tidal Behemoth",
            Description: "Windswept shore"
        );
        return new MapRunConfig(mapDef, Array.Empty<RolledMapModifier>());
    }

    // ─────────────────────────────────────────────
    //  1. Canonical Sequential Pack Order Tests
    // ─────────────────────────────────────────────

    private static void TestCanonicalSequentialPackOrder()
    {
        var spawner = new WaveSpawner(new Random(101));
        var config = CreateTestConfig();
        spawner.BeginMap(config, mapLength: 2400f);

        var layout = spawner.CurrentLayout;
        AssertTrue(layout != null, "Layout must be generated on BeginMap");
        AssertTrue(layout!.Packs.Count >= 6, $"Layout must contain at least 6 packs, got {layout.Packs.Count}");

        // Pack 1: Vanguard
        var p1 = layout.Packs[0];
        AssertTrue(p1.FormationType == CorridorPackFormationType.VanguardLine || p1.FormationType == CorridorPackFormationType.VanguardVee,
            $"Pack 1 must be Vanguard, got {p1.FormationType}");
        AssertTrue(p1.CenterPosition.X >= 350f && p1.CenterPosition.X <= 450f, $"Pack 1 X ({p1.CenterPosition.X}) must be ~408px");

        // Pack 2: Commander Phalanx
        var p2 = layout.Packs[1];
        AssertTrue(p2.FormationType == CorridorPackFormationType.CommanderPhalanx, $"Pack 2 must be CommanderPhalanx, got {p2.FormationType}");
        AssertTrue(p2.CenterPosition.X >= 800f && p2.CenterPosition.X <= 950f, $"Pack 2 X ({p2.CenterPosition.X}) must be ~888px");

        // Pack 3: League Encounter
        var p3 = layout.Packs[2];
        AssertTrue(p3.FormationType == CorridorPackFormationType.EncounterAmbush, $"Pack 3 must be EncounterAmbush, got {p3.FormationType}");
        AssertTrue(p3.IsEncounterPack, "Pack 3 must be flagged as an encounter pack");
        AssertTrue(p3.Encounter != null, "Pack 3 must have an associated LeagueEncounterInstance");
        AssertTrue(p3.CenterPosition.X >= 1250f && p3.CenterPosition.X <= 1450f, $"Pack 3 X ({p3.CenterPosition.X}) must be ~1344px");

        // Pack 4: Flanker Pincer
        var p4 = layout.Packs[3];
        AssertTrue(p4.FormationType == CorridorPackFormationType.FlankerPincer, $"Pack 4 must be FlankerPincer, got {p4.FormationType}");
        AssertTrue(p4.CenterPosition.X >= 1600f && p4.CenterPosition.X <= 1800f, $"Pack 4 X ({p4.CenterPosition.X}) must be ~1704px");

        // Pack 5: Gate Phalanx
        var p5 = layout.Packs[4];
        AssertTrue(p5.FormationType == CorridorPackFormationType.GatePhalanx, $"Pack 5 must be GatePhalanx, got {p5.FormationType}");
        AssertTrue(p5.CenterPosition.X >= 1850f && p5.CenterPosition.X <= 2020f, $"Pack 5 X ({p5.CenterPosition.X}) must be ~1944px");
        AssertTrue(p5.CenterPosition.X < 2400f * 0.85f, "Gate Phalanx must sit before the 85% Fog Gate threshold");

        // Pack 6: Boss Chamber
        var p6 = layout.Packs[5];
        AssertTrue(p6.FormationType == CorridorPackFormationType.BossChamber, $"Pack 6 must be BossChamber, got {p6.FormationType}");
        AssertTrue(p6.CenterPosition.X >= 2150f && p6.CenterPosition.X <= 2300f, $"Pack 6 X ({p6.CenterPosition.X}) must be ~2232px");

        // Strict X monotonicity for pack centers
        for (int i = 0; i < layout.Packs.Count - 1; i++)
        {
            AssertTrue(layout.Packs[i].CenterPosition.X < layout.Packs[i + 1].CenterPosition.X,
                $"Packs must strictly advance along corridor: Pack {i + 1} X ({layout.Packs[i].CenterPosition.X}) < Pack {i + 2} X ({layout.Packs[i + 1].CenterPosition.X})");
        }

        // Bug Fix Verification: ALL monsters within Pack 3 must spawn along forward map direction (+X, ~1344px), NEVER on the left (< 1000px)
        foreach (var mob in p3.Monsters)
        {
            AssertTrue(mob.Position.X >= 1200f && mob.Position.X <= 1500f,
                $"Pack 3 monster '{mob.Name}' X ({mob.Position.X:F1}) must spawn along forward map progression (~1344px), never on the left (< 1000px)");
            AssertTrue(mob.Position.Y >= 32f && mob.Position.Y <= 176f,
                $"Pack 3 monster '{mob.Name}' Y ({mob.Position.Y:F1}) must remain within corridor vertical bounds (32-176)");
        }
    }

    // ─────────────────────────────────────────────
    //  2. Vanguard Formation Geometry Tests
    // ─────────────────────────────────────────────

    private static void TestVanguardFormations()
    {
        var spawner = new WaveSpawner(new Random(202));
        var config = CreateTestConfig();

        // Test VanguardLine positioning
        var lineMobs = new List<MonsterInstance>();
        for (int i = 0; i < 5; i++)
        {
            lineMobs.Add(new MonsterInstance(i + 1, "Skeleton", MonsterRarity.Normal, 1, 100f, DamageType.Physical, 2f, 4f, 1f, 5f, 130f, 50f, 10f, 1f, 1f));
        }
        var center = new Vector2D(400f, 104f);
        CorridorMonsterLayout.PositionVanguardLine(lineMobs, center, height: 110f, jitter: 0f);

        for (int i = 0; i < lineMobs.Count; i++)
        {
            AssertTrue(lineMobs[i].Position.Y >= 32f && lineMobs[i].Position.Y <= 176f, $"Line mob {i} Y ({lineMobs[i].Position.Y}) within corridor");
            AssertEqual(400f, lineMobs[i].Position.X, 0.01f, $"Line mob {i} X must be at center X");
        }
        AssertTrue(lineMobs[0].Position.Y < lineMobs[^1].Position.Y, "Line mobs must be distributed vertically from top to bottom");

        // Test VanguardVee positioning
        var veeMobs = new List<MonsterInstance>();
        for (int i = 0; i < 5; i++)
        {
            veeMobs.Add(new MonsterInstance(i + 10, "Crawler", MonsterRarity.Normal, 1, 100f, DamageType.Physical, 2f, 4f, 1f, 5f, 130f, 50f, 10f, 1f, 1f));
        }
        CorridorMonsterLayout.PositionVanguardVee(veeMobs, center, height: 110f, depth: 50f);

        // Apex mob at center Y and lowest X (apexX = 400 - 25 = 375)
        AssertEqual(375f, veeMobs[0].Position.X, 0.01f, "Vee apex mob must be at apex X");
        AssertEqual(104f, veeMobs[0].Position.Y, 0.01f, "Vee apex mob must be at central lane Y (104)");

        // Wing mobs must have X > apex X
        for (int i = 1; i < veeMobs.Count; i++)
        {
            AssertTrue(veeMobs[i].Position.X > veeMobs[0].Position.X, $"Vee wing mob {i} X ({veeMobs[i].Position.X}) must lag behind apex");
            AssertTrue(veeMobs[i].Position.Y >= 32f && veeMobs[i].Position.Y <= 176f, $"Vee wing mob {i} Y ({veeMobs[i].Position.Y}) within corridor");
        }
    }

    // ─────────────────────────────────────────────
    //  3. Rare Commander & Phalanx Structure Tests
    // ─────────────────────────────────────────────

    private static void TestCommanderPhalanxStructure()
    {
        var spawner = new WaveSpawner(new Random(303));
        var config = CreateTestConfig();
        spawner.BeginMap(config, mapLength: 2400f);

        var pack2 = spawner.CurrentLayout!.Packs[1];
        AssertEqual((int)CorridorPackFormationType.CommanderPhalanx, (int)pack2.FormationType, 0f, "Pack 2 must be CommanderPhalanx");

        var commander = pack2.Monsters.FirstOrDefault(m => m.Rarity == MonsterRarity.Rare);
        AssertTrue(commander != null, "Pack 2 must contain a Rare Commander");
        AssertTrue(commander!.AuraModifiers.Count > 0, "Rare Commander must possess at least one AuraModifier");

        // Commander is at center Y and rear (+X)
        AssertEqual(104f, commander.Position.Y, 0.01f, "Commander must be centered vertically");

        // Minions are stationed in a protective shield arc in front (X <= commander.X)
        var minions = pack2.Monsters.Where(m => m != commander).ToList();
        AssertTrue(minions.Count >= 4, $"Phalanx must contain at least 4 escort minions, got {minions.Count}");
        foreach (var minion in minions)
        {
            AssertTrue(minion.Position.X <= commander.Position.X,
                $"Phalanx minion X ({minion.Position.X}) must form perimeter in front of Commander ({commander.Position.X})");
            AssertTrue(minion.Position.Y >= 32f && minion.Position.Y <= 176f, "Phalanx minion must be within corridor height");
        }
    }

    // ─────────────────────────────────────────────
    //  4. Flanker Pincer Position Tests
    // ─────────────────────────────────────────────

    private static void TestFlankerPincerPositions()
    {
        var spawner = new WaveSpawner(new Random(404));
        var config = CreateTestConfig();
        spawner.BeginMap(config, mapLength: 2400f);

        var flankPack = spawner.CurrentLayout!.Packs.First(p => p.FormationType == CorridorPackFormationType.FlankerPincer);
        AssertTrue(flankPack.Monsters.Count >= 4, "Flanker pack must have multiple mobs");

        int topCount = 0;
        int bottomCount = 0;

        foreach (var mob in flankPack.Monsters)
        {
            if (mob.Position.Y <= 60f)
            {
                topCount++;
            }
            else if (mob.Position.Y >= 148f)
            {
                bottomCount++;
            }
        }

        AssertTrue(topCount >= 2, $"Flankers must have at least 2 mobs hugging top wall, got {topCount}");
        AssertTrue(bottomCount >= 2, $"Flankers must have at least 2 mobs hugging bottom wall, got {bottomCount}");
    }

    // ─────────────────────────────────────────────
    //  5. Gate Phalanx Shield Wall Tests
    // ─────────────────────────────────────────────

    private static void TestGatePhalanxShieldWall()
    {
        var spawner = new WaveSpawner(new Random(505));
        var config = CreateTestConfig();
        spawner.BeginMap(config, mapLength: 2400f);

        var gatePack = spawner.CurrentLayout!.Packs.First(p => p.FormationType == CorridorPackFormationType.GatePhalanx);
        AssertTrue(gatePack.Monsters.Count >= 4, "Gate Phalanx must have at least 4 guards");

        // Must have Magic captains
        int magicCount = gatePack.Monsters.Count(m => m.Rarity == MonsterRarity.Magic);
        AssertTrue(magicCount >= 2, $"Gate Phalanx must have at least 2 Magic gate wardens, got {magicCount}");

        // Arrayed across the corridor right before Fog Gate (85% = 2040px)
        float fogGateX = 2400f * 0.85f;
        AssertTrue(gatePack.CenterPosition.X < fogGateX, $"Gate Phalanx ({gatePack.CenterPosition.X}) must stand before Fog Gate ({fogGateX})");
        AssertTrue(gatePack.CenterPosition.X >= 1800f, "Gate Phalanx must be positioned near the end of the corridor");
    }

    // ─────────────────────────────────────────────
    //  6. Roadside League Encounter Variant Tests
    // ─────────────────────────────────────────────

    private static void TestLeagueEncounterVariants()
    {
        var config = CreateTestConfig();

        // 1. Essence Monolith
        var spawnerEssence = new WaveSpawner(new Random(601));
        spawnerEssence.BeginMap(config, mapLength: 2400f, forceEncounter: LeagueEncounterType.EssenceMonolith);
        var encEssence = spawnerEssence.CurrentLayout!.Encounter;
        AssertTrue(encEssence != null, "Essence encounter must exist");
        AssertTrue(encEssence!.Type == LeagueEncounterType.EssenceMonolith, "Must be Essence Monolith");
        AssertTrue(!string.IsNullOrEmpty(encEssence.EssenceModifierName), "Essence must have an EssenceModifierName");
        AssertTrue(encEssence.State == LeagueEncounterState.Dormant, "Essence must start Dormant");

        var essencePack = spawnerEssence.CurrentLayout.Packs.First(p => p.IsEncounterPack);
        AssertTrue(essencePack.IsDormant, "Essence pack must start dormant/frozen");
        foreach (var m in essencePack.Monsters)
        {
            AssertTrue(m.Position.X >= 1200f && m.Position.X <= 1500f,
                $"Essence monster '{m.Name}' X ({m.Position.X:F1}) must be near ~1344px, never on the left");
            AssertTrue(m.Position.Y >= 32f && m.Position.Y <= 176f,
                $"Essence monster '{m.Name}' Y ({m.Position.Y:F1}) must be within corridor lane (32-176)");
        }

        // 2. Breach Crevice
        var spawnerBreach = new WaveSpawner(new Random(602));
        spawnerBreach.BeginMap(config, mapLength: 2400f, forceEncounter: LeagueEncounterType.BreachCrevice);
        var encBreach = spawnerBreach.CurrentLayout!.Encounter;
        AssertTrue(encBreach != null && encBreach.Type == LeagueEncounterType.BreachCrevice, "Breach encounter must exist");
        var breachPack = spawnerBreach.CurrentLayout.Packs.First(p => p.IsEncounterPack);
        AssertTrue(breachPack.Monsters.Any(m => m.Name.Contains("Breachling") || m.Name.Contains("Vanguard")), "Breach pack must contain Breachlings");
        foreach (var m in breachPack.Monsters)
        {
            AssertTrue(m.Position.X >= 1200f && m.Position.X <= 1500f,
                $"Breach monster '{m.Name}' X ({m.Position.X:F1}) must be near ~1344px, never on the left");
            AssertTrue(m.Position.Y >= 32f && m.Position.Y <= 176f,
                $"Breach monster '{m.Name}' Y ({m.Position.Y:F1}) must be within corridor lane (32-176)");
        }

        // 3. Sulphite Vein
        var spawnerSulphite = new WaveSpawner(new Random(603));
        spawnerSulphite.BeginMap(config, mapLength: 2400f, forceEncounter: LeagueEncounterType.SulphiteVein);
        var encSulphite = spawnerSulphite.CurrentLayout!.Encounter;
        AssertTrue(encSulphite != null && encSulphite.Type == LeagueEncounterType.SulphiteVein, "Sulphite encounter must exist");
        var sulphitePack = spawnerSulphite.CurrentLayout.Packs.First(p => p.IsEncounterPack);
        foreach (var m in sulphitePack.Monsters)
        {
            AssertTrue(m.Position.X >= 1200f && m.Position.X <= 1500f,
                $"Sulphite beast '{m.Name}' X ({m.Position.X:F1}) must be near ~1344px, never on the left");
            AssertTrue(m.Position.Y >= 32f && m.Position.Y <= 176f,
                $"Sulphite beast '{m.Name}' Y ({m.Position.Y:F1}) must be within corridor lane (32-176)");
        }
    }

    // ─────────────────────────────────────────────
    //  7. Proximity Trigger & Dormant Essence Awakening Tests
    // ─────────────────────────────────────────────

    private static void TestProximityTriggerAndEssenceAwakening()
    {
        var config = CreateTestConfig();
        var spawner = new WaveSpawner(new Random(701));
        spawner.BeginMap(config, mapLength: 2400f, forceEncounter: LeagueEncounterType.EssenceMonolith);

        var pack = spawner.CurrentLayout!.Packs.First(p => p.IsEncounterPack);
        var encounter = pack.Encounter!;
        AssertTrue(pack.IsDormant, "Pack must start Dormant");
        AssertTrue(encounter.State == LeagueEncounterState.Dormant, "Encounter must start Dormant");

        // Exile far away (e.g. at Y = 400)
        bool triggeredFar = encounter.TryTrigger(new Vector2D(220f, 400f));
        AssertTrue(!triggeredFar, "Encounter must not trigger when Exile is far away");
        AssertTrue(pack.IsDormant, "Pack must remain dormant");

        // Exile steps within trigger radius (encounter pos is around Y = 1344)
        bool triggeredNear = encounter.TryTrigger(encounter.Position + new Vector2D(0f, 80f));
        AssertTrue(triggeredNear, "Encounter must trigger within trigger radius (80px <= 125px)");
        AssertTrue(encounter.State == LeagueEncounterState.Active, "Encounter state must be Active");

        pack.WakeUp();
        AssertTrue(!pack.IsDormant, "Pack must be awakened");

        // Clear encounter
        encounter.Complete();
        AssertTrue(encounter.State == LeagueEncounterState.Completed, "Encounter state must be Completed");
    }

    // ─────────────────────────────────────────────
    //  8. 30Hz CombatEngine Corridor Progression Tests
    // ─────────────────────────────────────────────

    private static void TestCombatEngineCorridorIntegration()
    {
        var player = new PlayerState();
        var loadout = new SkillLoadout();
        var engine = new CombatEngine(player, loadout);

        var config = CreateTestConfig();
        engine.StartMap(config);

        AssertTrue(engine.CurrentLayout != null, "CombatEngine must expose CurrentLayout");
        AssertTrue(engine.CurrentPack != null, "CombatEngine must expose active CurrentPack");
        AssertTrue(engine.CurrentPack!.PackIndex == 1, "First active pack must be Pack 1 (Vanguard)");

        bool encounterTriggeredEventFired = false;
        engine.OnLeagueEncounterTriggered += args =>
        {
            encounterTriggeredEventFired = true;
        };

        // Advance ticks: verify player movement marches down corridor
        float dt = 1f / 30f;
        for (int i = 0; i < 30; i++)
        {
            engine.ProcessTick(dt);
        }

        AssertTrue(engine.Movement.DistanceTraveled > 0f, "Exile must have marched forward along corridor");
        AssertTrue(engine.ExilePosition.X > 40f, "Exile position X must have advanced");
        AssertTrue(!encounterTriggeredEventFired, "Encounter must not fire yet during initial 30 ticks at X < 100");
    }

    // ─────────────────────────────────────────────
    //  9. Range-Gated Aggro & Out-of-Combat Marching
    // ─────────────────────────────────────────────

    private static void TestMonsterAggroOnlyWhenCloseAndMarchingWhenOutOfCombat()
    {
        var player = new PlayerState();
        player.Initialize(maxLife: 100f, maxEnergyShield: 0f, maxMana: 100f);

        var loadout = new SkillLoadout();
        // Setup Melee Strike skill with 10 mana cost
        var gem = new GemDefinition(
            Id: "heavy_strike",
            Name: "Heavy Strike",
            Category: "Active",
            Color: "Red",
            Tags: new[] { "Attack", "Melee", "Physical" },
            RequiredTags: null,
            BaseStats: new Dictionary<string, object>
            {
                ["attackSpeedMultiplier"] = 100f,
                ["manaCost"] = 10f,
                ["baseDamageMin"] = 20f,
                ["baseDamageMax"] = 30f,
                ["damageType"] = "Physical"
            },
            Modifiers: null,
            Description: "A heavy melee strike."
        );
        loadout.PrimarySkill.SetActiveGem(gem);

        var engine = new CombatEngine(player, loadout);

        // Distant monster positioned at X = 400 (AggroRange = 37.5, initial dist = 360px from Exile at X = 40)
        var distantMonster = new MonsterInstance(
            instanceId: 901,
            name: "Distant Skeleton",
            rarity: MonsterRarity.Normal,
            monsterLevel: 1,
            maxLife: 100f,
            primaryDamageType: DamageType.Physical,
            damageMin: 10f,
            damageMax: 15f,
            attacksPerSecond: 1.0f,
            critChance: 0f,
            critMultiplier: 100f,
            accuracy: 100f,
            baseExperience: 10f,
            itemQuantityMultiplier: 1f,
            itemRarityMultiplier: 1f,
            initialPosition: new Vector2D(400f, 104f)
        );

        var wave = new MonsterWave
        {
            WaveNumber = 1,
            Monsters = new[] { distantMonster }
        };

        var config = CreateTestConfig();
        engine.StartMap(config);
        engine.SetCurrentWave(wave);
        engine.ExilePosition = new Vector2D(40f, 104f);

        bool damageDealtFired = false;
        bool playerDamagedFired = false;
        engine.OnDamageDealt += args => damageDealtFired = true;
        engine.OnPlayerDamaged += args => playerDamagedFired = true;

        float dt = 1f / 30f;

        // 1. Advance 20 ticks (0.66s): Exile moves from X=40 to ~120px. Dist is 280px (> 37.5px AggroRange)
        for (int i = 0; i < 20; i++)
        {
            engine.ProcessTick(dt);
        }

        AssertTrue(!distantMonster.IsAggroed, "Monster must NOT be aggroed while Exile is far away (> AggroRange)");
        AssertEqual(400f, distantMonster.Position.X, 0.01f, "Monster must hold its formation position while unaggroed");
        AssertTrue(!damageDealtFired, "Player must NOT attack or damage monsters that are out of range");
        AssertTrue(!playerDamagedFired, "Unaggroed monster must NOT attack the player");
        AssertEqual(100f, player.CurrentMana, 0.01f, "Player must NOT spend mana while out of combat");
        AssertTrue(engine.Movement.CurrentState == ExileMovementState.Marching, "Exile must be Marching while out of combat");
        AssertTrue(!engine.IsEngagedInCombat, "IsEngagedInCombat must be false while marching between encounters");
        AssertTrue(engine.ExilePosition.X > 40f, "Exile must automatically move forward towards the end of the map (+X)");

        // 2. Advance more ticks until Exile is within AggroRange (<= 37.5px)
        // At speed 120px/s, Exile moves 260px over 65 ticks (400 - 380 = 20px <= 37.5px)
        for (int i = 0; i < 65; i++)
        {
            engine.ProcessTick(dt);
        }

        AssertTrue(distantMonster.IsAggroed, "Monster must aggro once Exile approaches within AggroRange");
        AssertTrue(distantMonster.Position.X < 400f, "Monster must advance towards Exile once aggroed");

        // 3. Advance until in melee combat range (optimalRange = 35px)
        for (int i = 0; i < 20; i++)
        {
            engine.ProcessTick(dt);
        }

        AssertTrue(engine.IsEngagedInCombat, "Engine must be engaged in combat once in reach");
        AssertTrue(damageDealtFired, "Player must unleash attack once target is in weapon reach");
        AssertTrue(player.CurrentMana < 100f, "Player must spend mana on attack once in reach");
    }

    private static void TestPackAlertAndDamageAggroTriggers()
    {
        var player = new PlayerState();
        var loadout = new SkillLoadout();
        var engine = new CombatEngine(player, loadout);

        // Pack of 2 monsters separated by 30px (< 120px alert radius)
        var m1 = new MonsterInstance(1, "Mob1", MonsterRarity.Normal, 1, 100f, DamageType.Physical, 5, 10, 1, 0, 100, 100, 10, 1, 1, initialPosition: new Vector2D(350f, 90f));
        var m2 = new MonsterInstance(2, "Mob2", MonsterRarity.Normal, 1, 100f, DamageType.Physical, 5, 10, 1, 0, 100, 100, 10, 1, 1, initialPosition: new Vector2D(350f, 120f));

        var wave = new MonsterWave { WaveNumber = 1, Monsters = new[] { m1, m2 } };
        engine.StartMap(CreateTestConfig());
        engine.SetCurrentWave(wave);
        engine.ExilePosition = new Vector2D(40f, 104f);

        AssertTrue(!m1.IsAggroed && !m2.IsAggroed, "Both monsters start unaggroed");

        // Inflict damage on m1 directly (e.g. from ranged skill / DoT)
        m1.TakeDamage(10f, DamageType.Physical);
        AssertTrue(m1.IsAggroed, "Taking damage must immediately aggro m1");
        AssertTrue(!m2.IsAggroed, "m2 not yet alerted prior to Phase 4 tick");

        // Process 1 tick: Phase 4 alerts nearby pack members
        engine.ProcessTick(1f / 30f);

        AssertTrue(m2.IsAggroed, "Nearby pack member m2 (within 120px) must be alerted and aggroed");
    }

    private static void TestResumingMarchTowardsEndAfterPackClear()
    {
        var player = new PlayerState();
        var loadout = new SkillLoadout();
        var engine = new CombatEngine(player, loadout);

        // Mob placed at contact range
        var mob = new MonsterInstance(10, "Target", MonsterRarity.Normal, 1, 50f, DamageType.Physical, 2, 4, 1, 0, 100, 100, 10, 1, 1, initialPosition: new Vector2D(60f, 104f));
        var wave = new MonsterWave { WaveNumber = 1, Monsters = new[] { mob } };

        engine.StartMap(CreateTestConfig());
        engine.SetCurrentWave(wave);
        engine.ExilePosition = new Vector2D(40f, 104f);

        // Step 1 tick to engage target
        engine.ProcessTick(1f / 30f);
        AssertTrue(mob.IsAggroed, "Mob within reach must be aggroed");

        // Slay the monster
        mob.TakeDamage(100f, DamageType.Physical);
        AssertTrue(!mob.IsAlive, "Mob is dead");

        float xBefore = engine.ExilePosition.X;

        // Process tick: Phase 5 clears pack, transitions state
        engine.ProcessTick(1f / 30f);
        AssertTrue(engine.Movement.CurrentEngagedTarget == null, "Engaged target must be cleared");

        // Process next tick: Exile resumes marching forward along corridor (+X)
        engine.ProcessTick(1f / 30f);

        AssertTrue(engine.Movement.CurrentState == ExileMovementState.Marching, "State must resume to Marching");
        AssertTrue(engine.ExilePosition.X >= xBefore, "Exile must continue moving towards end of map (+X)");
        AssertTrue(!engine.IsEngagedInCombat, "Exile must be out of combat while marching between packs");
    }
}
