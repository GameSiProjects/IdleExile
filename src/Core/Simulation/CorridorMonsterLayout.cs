using System;
using System.Collections.Generic;
using System.Linq;
using IdleExile.Core.Models;

namespace IdleExile.Core.Simulation;

/// <summary>
/// Corridor Monster Layout Engine (Task 2.7).
/// Generates and manages the sequential placement of monster packs and League Encounters
/// along the linear world corridor of length L_map.
///
/// Canonical Progression (docs/07_combat_ui_and_viewport.md Section 3.2.D):
/// 1. Pack 1 (~17%): Vanguard Swarm (Horizontal Line or V-Shape blocking the lane).
/// 2. Pack 2 (~37%): Rare Commander (Golden Aura) with Phalanx / Shield Wall escort.
/// 3. Encounter (~56%): Roadside League Encounter (Essence Monolith, Breach Crevice, or Sulphite Vein).
/// 4. Pack 3 (~71%): Ambush / Flankers (Pincer formation from left & right walls).
/// 5. Pack 4 (~81%): Gate Phalanx (Dense elite guard immediately before Fog Gate at 85%).
/// 6. Boss Pack (~93%): Map Boss Chamber in the final arena.
///
/// Pure C# — zero Godot dependencies.
/// </summary>
public sealed class CorridorMonsterLayout
{
    // ─────────────────────────────────────────────
    //  Canonical Progress Ratios
    // ─────────────────────────────────────────────
    public const float Pack1VanguardRatio = 0.17f;
    public const float Pack2CommanderRatio = 0.37f;
    public const float EncounterRatio = 0.56f;
    public const float Pack3FlankersRatio = 0.71f;
    public const float Pack4GatePhalanxRatio = 0.81f;
    public const float BossChamberRatio = 0.93f;

    // ─────────────────────────────────────────────
    //  Corridor Boundary Constants
    // ─────────────────────────────────────────────
    public const float MinPlayableX = ExileMovementController.MinCorridorX; // 40f
    public const float MaxPlayableX = ExileMovementController.MaxCorridorX; // 400f
    public const float CenterX = ExileMovementController.DefaultCenterX;    // 220f
    public const float DefaultCorridorWidth = ExileMovementController.DefaultCorridorWidth; // 440f

    // ─────────────────────────────────────────────
    //  Properties
    // ─────────────────────────────────────────────
    /// <summary>Total length of the world corridor in pixels.</summary>
    public float MapLength { get; }

    /// <summary>All structured monster packs arranged sequentially along the corridor.</summary>
    public IReadOnlyList<CorridorPack> Packs { get; }

    /// <summary>Roadside League Encounter instance generated for this corridor run, if any.</summary>
    public LeagueEncounterInstance? Encounter { get; }

    /// <summary>Final boss pack situated in the Boss Arena Chamber.</summary>
    public CorridorPack? BossPack => Packs.FirstOrDefault(p => p.FormationType == CorridorPackFormationType.BossChamber);

    /// <summary>Total monsters across all packs in the corridor.</summary>
    public int TotalMonstersCount => Packs.Sum(p => p.Count);

    /// <summary>Total number of packs in the corridor including boss pack.</summary>
    public int PackCount => Packs.Count;

    // ─────────────────────────────────────────────
    //  Constructor
    // ─────────────────────────────────────────────
    public CorridorMonsterLayout(
        float mapLength,
        IReadOnlyList<CorridorPack> packs,
        LeagueEncounterInstance? encounter = null)
    {
        MapLength = mapLength > 0f ? mapLength : ExileMovementController.DefaultMapLength;
        Packs = packs ?? Array.Empty<CorridorPack>();
        Encounter = encounter;
    }

    // ─────────────────────────────────────────────
    //  Layout Generation Pipeline
    // ─────────────────────────────────────────────

    /// <summary>
    /// Generates the complete sequential corridor layout with packs, formations, and roadside encounter.
    /// </summary>
    /// <param name="config">Map run configuration (modifiers, pack size, monster level).</param>
    /// <param name="createMonster">Factory method to instantiate a scaled MonsterInstance.</param>
    /// <param name="mapLength">Corridor total length L_map.</param>
    /// <param name="random">Optional seeded random instance.</param>
    /// <param name="forceEncounter">Optional encounter override for targeted testing.</param>
    public static CorridorMonsterLayout Generate(
        MapRunConfig config,
        Func<MonsterRarity, string?, MonsterInstance> createMonster,
        float mapLength = ExileMovementController.DefaultMapLength,
        Random? random = null,
        LeagueEncounterType? forceEncounter = null)
    {
        var rng = random ?? Random.Shared;
        var packs = new List<CorridorPack>();

        // Scaling factor from map pack size modifier (100% = 1.0)
        float packSizeFactor = Math.Max(0.5f, config.TotalPackSize / 100f);

        // ═════════════════════════════════════════════
        //  Pack 1: Vanguard Trash Swarm (~17%)
        // ═════════════════════════════════════════════
        float y1 = mapLength * Pack1VanguardRatio;
        var vanguardCenter = new Vector2D(CenterX, y1);
        bool useVee = rng.NextDouble() < 0.5;
        var pack1Formation = useVee ? CorridorPackFormationType.VanguardVee : CorridorPackFormationType.VanguardLine;

        int vanguardCount = Math.Max(4, (int)MathF.Round(6 * packSizeFactor));
        var vanguardMobs = new List<MonsterInstance>(vanguardCount);
        for (int i = 0; i < vanguardCount; i++)
        {
            vanguardMobs.Add(createMonster(MonsterRarity.Normal, null));
        }

        if (useVee)
        {
            PositionVanguardVee(vanguardMobs, vanguardCenter, width: 280f, depth: 55f);
        }
        else
        {
            PositionVanguardLine(vanguardMobs, vanguardCenter, width: 290f, jitter: 10f, rng);
        }

        packs.Add(new CorridorPack(
            packIndex: 1,
            formationType: pack1Formation,
            centerPosition: vanguardCenter,
            progressRatio: Pack1VanguardRatio,
            monsters: vanguardMobs
        ));

        // ═════════════════════════════════════════════
        //  Pack 2: Rare Commander & Phalanx (~37%)
        // ═════════════════════════════════════════════
        float y2 = mapLength * Pack2CommanderRatio;
        var commanderCenter = new Vector2D(CenterX, y2);

        var commander = createMonster(MonsterRarity.Rare, null);
        int lieutenantCount = rng.NextDouble() < 0.6 ? 2 : 1;
        int minionCount = Math.Max(3, (int)MathF.Round(4 * packSizeFactor));

        var commanderMinions = new List<MonsterInstance>();
        for (int i = 0; i < lieutenantCount; i++)
        {
            commanderMinions.Add(createMonster(MonsterRarity.Magic, null));
        }
        for (int i = 0; i < minionCount; i++)
        {
            commanderMinions.Add(createMonster(MonsterRarity.Normal, null));
        }

        PositionCommanderPhalanx(commander, commanderMinions, commanderCenter);

        var pack2All = new List<MonsterInstance> { commander };
        pack2All.AddRange(commanderMinions);

        packs.Add(new CorridorPack(
            packIndex: 2,
            formationType: CorridorPackFormationType.CommanderPhalanx,
            centerPosition: commanderCenter,
            progressRatio: Pack2CommanderRatio,
            monsters: pack2All
        ));

        // ═════════════════════════════════════════════
        //  Mid-Corridor League Encounter (~56%)
        // ═════════════════════════════════════════════
        float yEnc = mapLength * EncounterRatio;
        bool isLeftSide = rng.NextDouble() < 0.5;
        float encX = isLeftSide ? 75f : 365f;
        var encPos = new Vector2D(encX, yEnc);

        LeagueEncounterType encType = forceEncounter ?? RollEncounterType(rng);
        var (encounterInstance, encounterPack) = CreateLeagueEncounter(
            encType,
            encPos,
            packIndex: 3,
            progressRatio: EncounterRatio,
            packSizeFactor,
            createMonster,
            rng
        );

        packs.Add(encounterPack);

        // ═════════════════════════════════════════════
        //  Pack 3: Ambush / Flankers (~71%)
        // ═════════════════════════════════════════════
        float y3 = mapLength * Pack3FlankersRatio;
        var flankerCenter = new Vector2D(CenterX, y3);

        int flankPerSide = Math.Max(2, (int)MathF.Round(3 * packSizeFactor));
        var leftFlank = new List<MonsterInstance>();
        var rightFlank = new List<MonsterInstance>();

        for (int i = 0; i < flankPerSide; i++)
        {
            leftFlank.Add(createMonster(i == 0 && rng.NextDouble() < 0.4 ? MonsterRarity.Magic : MonsterRarity.Normal, null));
            rightFlank.Add(createMonster(i == 0 && rng.NextDouble() < 0.4 ? MonsterRarity.Magic : MonsterRarity.Normal, null));
        }

        PositionFlankerPincer(leftFlank, rightFlank, flankerCenter);

        var pack3All = new List<MonsterInstance>();
        pack3All.AddRange(leftFlank);
        pack3All.AddRange(rightFlank);

        packs.Add(new CorridorPack(
            packIndex: 4,
            formationType: CorridorPackFormationType.FlankerPincer,
            centerPosition: flankerCenter,
            progressRatio: Pack3FlankersRatio,
            monsters: pack3All
        ));

        // ═════════════════════════════════════════════
        //  Pack 4: Gate Phalanx (~81%, right before 85% Fog Gate)
        // ═════════════════════════════════════════════
        float y4 = mapLength * Pack4GatePhalanxRatio;
        var gateCenter = new Vector2D(CenterX, y4);

        int gateGuardCount = Math.Max(4, (int)MathF.Round(6 * packSizeFactor));
        var gateGuards = new List<MonsterInstance>();

        // Frontline has 2 Magic captains
        gateGuards.Add(createMonster(MonsterRarity.Magic, "Gate Captain"));
        gateGuards.Add(createMonster(MonsterRarity.Magic, "Gate Warden"));
        for (int i = 2; i < gateGuardCount; i++)
        {
            gateGuards.Add(createMonster(MonsterRarity.Normal, "Phalanx Guard"));
        }

        PositionGatePhalanx(gateGuards, gateCenter);

        packs.Add(new CorridorPack(
            packIndex: 5,
            formationType: CorridorPackFormationType.GatePhalanx,
            centerPosition: gateCenter,
            progressRatio: Pack4GatePhalanxRatio,
            monsters: gateGuards
        ));

        // ═════════════════════════════════════════════
        //  Boss Chamber (~93%)
        // ═════════════════════════════════════════════
        float yBoss = mapLength * BossChamberRatio;
        var bossCenter = new Vector2D(CenterX, yBoss);

        string bossName = config.Map.BossName ?? $"Guardian of {config.Map.Name}";
        var boss = createMonster(MonsterRarity.Boss, bossName);

        int addCount = 3 + rng.Next(2);
        var bossAdds = new List<MonsterInstance>();
        for (int i = 0; i < addCount; i++)
        {
            bossAdds.Add(createMonster(MonsterRarity.Normal, "Boss Escort"));
        }

        PositionBossChamber(boss, bossAdds, bossCenter);

        var bossPackAll = new List<MonsterInstance> { boss };
        bossPackAll.AddRange(bossAdds);

        packs.Add(new CorridorPack(
            packIndex: 6,
            formationType: CorridorPackFormationType.BossChamber,
            centerPosition: bossCenter,
            progressRatio: BossChamberRatio,
            monsters: bossPackAll
        ));

        return new CorridorMonsterLayout(mapLength, packs, encounterInstance);
    }

    // ─────────────────────────────────────────────
    //  Formation Positioning Algorithms
    // ─────────────────────────────────────────────

    /// <summary>
    /// Arranges Vanguard mobs in a horizontal defensive line spanning the corridor.
    /// </summary>
    public static void PositionVanguardLine(
        IReadOnlyList<MonsterInstance> mobs,
        Vector2D center,
        float width = 280f,
        float jitter = 8f,
        Random? rng = null)
    {
        if (mobs.Count == 0) return;
        var random = rng ?? Random.Shared;

        float leftX = MathF.Max(MinPlayableX + 15f, center.X - width * 0.5f);
        float rightX = MathF.Min(MaxPlayableX - 15f, center.X + width * 0.5f);
        float stepX = mobs.Count > 1 ? (rightX - leftX) / (mobs.Count - 1) : 0f;

        for (int i = 0; i < mobs.Count; i++)
        {
            float x = mobs.Count == 1 ? center.X : leftX + i * stepX;
            float yOffset = ((float)random.NextDouble() * 2f - 1f) * jitter;
            mobs[i].Position = new Vector2D(
                Math.Clamp(x, MinPlayableX, MaxPlayableX),
                center.Y + yOffset
            );
        }
    }

    /// <summary>
    /// Arranges Vanguard mobs in a V-shaped wedge pointing toward the approaching Exile (apex at lowest Y).
    /// </summary>
    public static void PositionVanguardVee(
        IReadOnlyList<MonsterInstance> mobs,
        Vector2D center,
        float width = 280f,
        float depth = 50f)
    {
        if (mobs.Count == 0) return;

        // Apex mob at center.X and apex Y (center.Y - depth * 0.5)
        float apexY = center.Y - depth * 0.5f;
        mobs[0].Position = new Vector2D(center.X, apexY);

        int remaining = mobs.Count - 1;
        if (remaining <= 0) return;

        int pairs = (remaining + 1) / 2;
        float xStep = (width * 0.5f) / MathF.Max(1, pairs);
        float yStep = depth / MathF.Max(1, pairs);

        int mobIdx = 1;
        for (int p = 1; p <= pairs && mobIdx < mobs.Count; p++)
        {
            float deltaX = p * xStep;
            float wingY = apexY + p * yStep;

            // Left wing
            mobs[mobIdx++].Position = new Vector2D(
                Math.Clamp(center.X - deltaX, MinPlayableX, MaxPlayableX),
                wingY
            );

            // Right wing
            if (mobIdx < mobs.Count)
            {
                mobs[mobIdx++].Position = new Vector2D(
                    Math.Clamp(center.X + deltaX, MinPlayableX, MaxPlayableX),
                    wingY
                );
            }
        }
    }

    /// <summary>
    /// Positions a Rare Commander at the center-rear, with minions forming a perimeter protective arc in front.
    /// </summary>
    public static void PositionCommanderPhalanx(
        MonsterInstance commander,
        IReadOnlyList<MonsterInstance> minions,
        Vector2D center)
    {
        // Commander sits slightly back from center along the forward marching axis (+Y)
        commander.Position = new Vector2D(center.X, center.Y + 28f);

        if (minions.Count == 0) return;

        // Minions form a shield arc in front (lower Y, closer to advancing Exile)
        float arcRadius = 45f;
        float startAngle = -MathF.PI * 0.85f;
        float endAngle = -MathF.PI * 0.15f;
        float angleStep = minions.Count > 1 ? (endAngle - startAngle) / (minions.Count - 1) : 0f;

        for (int i = 0; i < minions.Count; i++)
        {
            float angle = minions.Count == 1 ? -MathF.PI * 0.5f : startAngle + i * angleStep;
            float mx = commander.Position.X + MathF.Cos(angle) * (arcRadius + (i % 2) * 8f);
            float my = commander.Position.Y + MathF.Sin(angle) * (arcRadius + (i % 2) * 5f);

            minions[i].Position = new Vector2D(
                Math.Clamp(mx, MinPlayableX, MaxPlayableX),
                my
            );
        }
    }

    /// <summary>
    /// Positions Flankers split into two groups hugging the left and right corridor walls.
    /// </summary>
    public static void PositionFlankerPincer(
        IReadOnlyList<MonsterInstance> leftFlank,
        IReadOnlyList<MonsterInstance> rightFlank,
        Vector2D center)
    {
        // Left flank hugs left wall (MinPlayableX + ~20px)
        float leftWallX = MinPlayableX + 25f;
        float leftStepY = leftFlank.Count > 1 ? 55f / (leftFlank.Count - 1) : 0f;
        for (int i = 0; i < leftFlank.Count; i++)
        {
            float y = center.Y - 25f + i * leftStepY;
            leftFlank[i].Position = new Vector2D(leftWallX + (i % 2) * 12f, y);
        }

        // Right flank hugs right wall (MaxPlayableX - ~20px)
        float rightWallX = MaxPlayableX - 25f;
        float rightStepY = rightFlank.Count > 1 ? 55f / (rightFlank.Count - 1) : 0f;
        for (int i = 0; i < rightFlank.Count; i++)
        {
            float y = center.Y - 25f + i * rightStepY;
            rightFlank[i].Position = new Vector2D(rightWallX - (i % 2) * 12f, y);
        }
    }

    /// <summary>
    /// Positions a dense Phalanx shield wall across the corridor right before the Fog Gate.
    /// </summary>
    public static void PositionGatePhalanx(
        IReadOnlyList<MonsterInstance> guards,
        Vector2D center)
    {
        if (guards.Count == 0) return;

        // Arrayed in two tight ranks: frontline and backline
        int half = (guards.Count + 1) / 2;
        float width = 230f;
        float leftX = center.X - width * 0.5f;
        float stepX = half > 1 ? width / (half - 1) : 0f;

        for (int i = 0; i < guards.Count; i++)
        {
            bool isFrontRank = i < half;
            int rankIdx = isFrontRank ? i : i - half;
            float x = leftX + rankIdx * stepX + (isFrontRank ? 0f : stepX * 0.5f);
            float y = center.Y + (isFrontRank ? -14f : 16f);

            guards[i].Position = new Vector2D(
                Math.Clamp(x, MinPlayableX, MaxPlayableX),
                y
            );
        }
    }

    /// <summary>
    /// Positions the Map Boss with escorting adds in the Boss Chamber arena.
    /// </summary>
    public static void PositionBossChamber(
        MonsterInstance boss,
        IReadOnlyList<MonsterInstance> adds,
        Vector2D center)
    {
        boss.Position = center;

        for (int i = 0; i < adds.Count; i++)
        {
            float angle = (i * MathF.Tau / Math.Max(1, adds.Count)) + 0.4f;
            float rx = 52f + (i % 2) * 12f;
            float ry = 36f + (i % 2) * 8f;

            adds[i].Position = new Vector2D(
                Math.Clamp(center.X + MathF.Cos(angle) * rx, MinPlayableX, MaxPlayableX),
                center.Y + MathF.Sin(angle) * ry
            );
        }
    }

    // ─────────────────────────────────────────────
    //  Internal: League Encounter Factory
    // ─────────────────────────────────────────────

    private static LeagueEncounterType RollEncounterType(Random rng)
    {
        double roll = rng.NextDouble();
        if (roll < 0.40) return LeagueEncounterType.EssenceMonolith;
        if (roll < 0.75) return LeagueEncounterType.BreachCrevice;
        return LeagueEncounterType.SulphiteVein;
    }

    private static (LeagueEncounterInstance Encounter, CorridorPack Pack) CreateLeagueEncounter(
        LeagueEncounterType type,
        Vector2D position,
        int packIndex,
        float progressRatio,
        float packSizeFactor,
        Func<MonsterRarity, string?, MonsterInstance> createMonster,
        Random rng)
    {
        switch (type)
        {
            case LeagueEncounterType.EssenceMonolith:
            default:
            {
                string[] essenceMods =
                [
                    "Essence of Hatred (+35% Extra Cold Damage)",
                    "Essence of Zeal (+30% Action Speed)",
                    "Essence of Wrath (+35% Extra Lightning Damage)",
                    "Essence of Greed (+80% Item Rarity)"
                ];
                string chosenMod = essenceMods[rng.Next(essenceMods.Length)];

                var encounter = new LeagueEncounterInstance(
                    LeagueEncounterType.EssenceMonolith,
                    "Essence Monolith",
                    position,
                    triggerRadius: 125f,
                    essenceModifierName: chosenMod,
                    rewardDescription: "Drops guaranteed High-Tier Essence & Rare Equipment"
                );

                var rareMonster = createMonster(MonsterRarity.Rare, $"Imprisoned {rarePrefix(chosenMod)} Terror");
                rareMonster.Position = position;

                int addCount = Math.Max(3, (int)MathF.Round(4 * packSizeFactor));
                var mobs = new List<MonsterInstance> { rareMonster };

                for (int i = 0; i < addCount; i++)
                {
                    var minion = createMonster(MonsterRarity.Normal, "Crystalline Husk");
                    float angle = (i * MathF.Tau / addCount) + 0.3f;
                    minion.Position = new Vector2D(
                        Math.Clamp(position.X + MathF.Cos(angle) * 32f, MinPlayableX, MaxPlayableX),
                        position.Y + MathF.Sin(angle) * 28f
                    );
                    mobs.Add(minion);
                }

                var pack = new CorridorPack(
                    packIndex,
                    CorridorPackFormationType.EncounterAmbush,
                    position,
                    progressRatio,
                    mobs,
                    encounter,
                    isDormant: true // Essence begins frozen/dormant until approached
                );

                return (encounter, pack);
            }

            case LeagueEncounterType.BreachCrevice:
            {
                var encounter = new LeagueEncounterInstance(
                    LeagueEncounterType.BreachCrevice,
                    "Clasped Breach Hand",
                    position,
                    triggerRadius: 135f,
                    rewardDescription: "Unleashes Breachlings & drops Breach Splinters"
                );

                int breachCount = Math.Max(5, (int)MathF.Round(7 * packSizeFactor));
                var mobs = new List<MonsterInstance>();

                // 1 Magic invader
                var invader = createMonster(MonsterRarity.Magic, "Xoph's Vanguard");
                invader.Position = position;
                mobs.Add(invader);

                for (int i = 0; i < breachCount; i++)
                {
                    var breachling = createMonster(MonsterRarity.Normal, "Breachling");
                    float angle = (i * MathF.Tau / breachCount) + 0.2f;
                    float r = 28f + (i % 2) * 14f;
                    breachling.Position = new Vector2D(
                        Math.Clamp(position.X + MathF.Cos(angle) * r, MinPlayableX, MaxPlayableX),
                        position.Y + MathF.Sin(angle) * r
                    );
                    mobs.Add(breachling);
                }

                var pack = new CorridorPack(
                    packIndex,
                    CorridorPackFormationType.EncounterAmbush,
                    position,
                    progressRatio,
                    mobs,
                    encounter,
                    isDormant: false
                );

                return (encounter, pack);
            }

            case LeagueEncounterType.SulphiteVein:
            {
                var encounter = new LeagueEncounterInstance(
                    LeagueEncounterType.SulphiteVein,
                    "Voltaxic Sulphite Deposit",
                    position,
                    triggerRadius: 120f,
                    rewardDescription: "+100% Flask Refill & Subterranean Azurite"
                );

                int beastCount = Math.Max(4, (int)MathF.Round(5 * packSizeFactor));
                var mobs = new List<MonsterInstance>();

                for (int i = 0; i < beastCount; i++)
                {
                    var beast = createMonster(i == 0 ? MonsterRarity.Magic : MonsterRarity.Normal, "Subterranean Burrower");
                    float angle = (i * MathF.Tau / beastCount) + 0.4f;
                    beast.Position = new Vector2D(
                        Math.Clamp(position.X + MathF.Cos(angle) * 35f, MinPlayableX, MaxPlayableX),
                        position.Y + MathF.Sin(angle) * 28f
                    );
                    mobs.Add(beast);
                }

                var pack = new CorridorPack(
                    packIndex,
                    CorridorPackFormationType.EncounterAmbush,
                    position,
                    progressRatio,
                    mobs,
                    encounter,
                    isDormant: false
                );

                return (encounter, pack);
            }
        }

        static string rarePrefix(string mod)
        {
            if (mod.Contains("Hatred")) return "Glacial";
            if (mod.Contains("Zeal")) return "Swift";
            if (mod.Contains("Wrath")) return "Surging";
            return "Golden";
        }
    }
}
