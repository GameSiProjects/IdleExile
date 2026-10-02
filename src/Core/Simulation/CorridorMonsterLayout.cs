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
    public const float MinPlayableY = ExileMovementController.MinCorridorY; // 32f
    public const float MaxPlayableY = ExileMovementController.MaxCorridorY; // 176f
    public const float CenterY = ExileMovementController.DefaultCenterY;    // 104f
    public const float DefaultCorridorHeight = ExileMovementController.DefaultCorridorHeight; // 208f

    // Backward compatibility aliases
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
        float x1 = mapLength * Pack1VanguardRatio;
        var vanguardCenter = new Vector2D(x1, CenterY);
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
            PositionVanguardVee(vanguardMobs, vanguardCenter, height: 110f, depth: 55f);
        }
        else
        {
            PositionVanguardLine(vanguardMobs, vanguardCenter, height: 115f, jitter: 10f, rng);
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
        float x2 = mapLength * Pack2CommanderRatio;
        var commanderCenter = new Vector2D(x2, CenterY);

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
        float xEnc = mapLength * EncounterRatio;
        bool isTopSide = rng.NextDouble() < 0.5;
        float encY = isTopSide ? (MinPlayableY + 15f) : (MaxPlayableY - 15f);
        var encPos = new Vector2D(xEnc, encY);

        LeagueEncounterType encType = forceEncounter ?? RollEncounterType(rng);
        var (encounterInstance, encounterPack) = CreateLeagueEncounter(
            encType,
            encPos,
            packIndex: 3,
            progressRatio: EncounterRatio,
            packSizeFactor,
            createMonster,
            rng,
            mapLength
        );

        packs.Add(encounterPack);

        // ═════════════════════════════════════════════
        //  Pack 3: Ambush / Flankers (~71%)
        // ═════════════════════════════════════════════
        float x3 = mapLength * Pack3FlankersRatio;
        var flankerCenter = new Vector2D(x3, CenterY);

        int flankPerSide = Math.Max(2, (int)MathF.Round(3 * packSizeFactor));
        var topFlank = new List<MonsterInstance>();
        var bottomFlank = new List<MonsterInstance>();

        for (int i = 0; i < flankPerSide; i++)
        {
            topFlank.Add(createMonster(i == 0 && rng.NextDouble() < 0.4 ? MonsterRarity.Magic : MonsterRarity.Normal, null));
            bottomFlank.Add(createMonster(i == 0 && rng.NextDouble() < 0.4 ? MonsterRarity.Magic : MonsterRarity.Normal, null));
        }

        PositionFlankerPincer(topFlank, bottomFlank, flankerCenter);

        var pack3All = new List<MonsterInstance>();
        pack3All.AddRange(topFlank);
        pack3All.AddRange(bottomFlank);

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
        float x4 = mapLength * Pack4GatePhalanxRatio;
        var gateCenter = new Vector2D(x4, CenterY);

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
        float xBoss = mapLength * BossChamberRatio;
        var bossCenter = new Vector2D(xBoss, CenterY);

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
    /// Arranges Vanguard mobs in a vertical defensive line spanning across the corridor.
    /// </summary>
    public static void PositionVanguardLine(
        IReadOnlyList<MonsterInstance> mobs,
        Vector2D center,
        float height = 115f,
        float jitter = 8f,
        Random? rng = null)
    {
        if (mobs.Count == 0) return;
        var random = rng ?? Random.Shared;

        float topY = MathF.Max(MinPlayableY + 10f, center.Y - height * 0.5f);
        float bottomY = MathF.Min(MaxPlayableY - 10f, center.Y + height * 0.5f);
        float stepY = mobs.Count > 1 ? (bottomY - topY) / (mobs.Count - 1) : 0f;

        for (int i = 0; i < mobs.Count; i++)
        {
            float y = mobs.Count == 1 ? center.Y : topY + i * stepY;
            float xOffset = ((float)random.NextDouble() * 2f - 1f) * jitter;
            mobs[i].Position = new Vector2D(
                center.X + xOffset,
                Math.Clamp(y, MinPlayableY, MaxPlayableY)
            );
        }
    }

    /// <summary>
    /// Arranges Vanguard mobs in a V-shaped wedge pointing left toward the approaching Exile (apex at lowest X).
    /// </summary>
    public static void PositionVanguardVee(
        IReadOnlyList<MonsterInstance> mobs,
        Vector2D center,
        float height = 110f,
        float depth = 50f)
    {
        if (mobs.Count == 0) return;

        // Apex mob at apex X (center.X - depth * 0.5f) and center.Y
        float apexX = center.X - depth * 0.5f;
        mobs[0].Position = new Vector2D(apexX, center.Y);

        int remaining = mobs.Count - 1;
        if (remaining <= 0) return;

        int pairs = (remaining + 1) / 2;
        float yStep = (height * 0.5f) / MathF.Max(1, pairs);
        float xStep = depth / MathF.Max(1, pairs);

        int mobIdx = 1;
        for (int p = 1; p <= pairs && mobIdx < mobs.Count; p++)
        {
            float deltaY = p * yStep;
            float wingX = apexX + p * xStep;

            // Top wing
            mobs[mobIdx++].Position = new Vector2D(
                wingX,
                Math.Clamp(center.Y - deltaY, MinPlayableY, MaxPlayableY)
            );

            // Bottom wing
            if (mobIdx < mobs.Count)
            {
                mobs[mobIdx++].Position = new Vector2D(
                    wingX,
                    Math.Clamp(center.Y + deltaY, MinPlayableY, MaxPlayableY)
                );
            }
        }
    }

    /// <summary>
    /// Positions a Rare Commander at the center-rear (+X), with minions forming a perimeter protective arc in front (facing left toward incoming Exile).
    /// </summary>
    public static void PositionCommanderPhalanx(
        MonsterInstance commander,
        IReadOnlyList<MonsterInstance> minions,
        Vector2D center)
    {
        // Commander sits slightly back to the right along +X
        commander.Position = new Vector2D(center.X + 28f, center.Y);

        if (minions.Count == 0) return;

        // Minions form a shield arc in front (lower X, closer to advancing Exile)
        float arcRadius = 40f;
        float startAngle = MathF.PI * 0.65f;
        float endAngle = MathF.PI * 1.35f;
        float angleStep = minions.Count > 1 ? (endAngle - startAngle) / (minions.Count - 1) : 0f;

        for (int i = 0; i < minions.Count; i++)
        {
            float angle = minions.Count == 1 ? MathF.PI : startAngle + i * angleStep;
            float mx = commander.Position.X + MathF.Cos(angle) * (arcRadius + (i % 2) * 6f);
            float my = commander.Position.Y + MathF.Sin(angle) * (arcRadius + (i % 2) * 5f);

            minions[i].Position = new Vector2D(
                mx,
                Math.Clamp(my, MinPlayableY, MaxPlayableY)
            );
        }
    }

    /// <summary>
    /// Positions Flankers split into two groups hugging the top and bottom corridor walls.
    /// </summary>
    public static void PositionFlankerPincer(
        IReadOnlyList<MonsterInstance> topFlank,
        IReadOnlyList<MonsterInstance> bottomFlank,
        Vector2D center)
    {
        // Top flank hugs top wall
        float topWallY = MinPlayableY + 12f;
        float topStepX = topFlank.Count > 1 ? 55f / (topFlank.Count - 1) : 0f;
        for (int i = 0; i < topFlank.Count; i++)
        {
            float x = center.X - 25f + i * topStepX;
            topFlank[i].Position = new Vector2D(x, Math.Clamp(topWallY + (i % 2) * 8f, MinPlayableY, MaxPlayableY));
        }

        // Bottom flank hugs bottom wall
        float bottomWallY = MaxPlayableY - 12f;
        float bottomStepX = bottomFlank.Count > 1 ? 55f / (bottomFlank.Count - 1) : 0f;
        for (int i = 0; i < bottomFlank.Count; i++)
        {
            float x = center.X - 25f + i * bottomStepX;
            bottomFlank[i].Position = new Vector2D(x, Math.Clamp(bottomWallY - (i % 2) * 8f, MinPlayableY, MaxPlayableY));
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

        // Arrayed in two tight ranks: frontline and backline along X
        int half = (guards.Count + 1) / 2;
        float height = 110f;
        float topY = center.Y - height * 0.5f;
        float stepY = half > 1 ? height / (half - 1) : 0f;

        for (int i = 0; i < guards.Count; i++)
        {
            bool isFrontRank = i < half;
            int rankIdx = isFrontRank ? i : i - half;
            float y = topY + rankIdx * stepY + (isFrontRank ? 0f : stepY * 0.5f);
            float x = center.X + (isFrontRank ? -14f : 16f);

            guards[i].Position = new Vector2D(
                x,
                Math.Clamp(y, MinPlayableY, MaxPlayableY)
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
            float rx = 45f + (i % 2) * 10f;
            float ry = 35f + (i % 2) * 8f;

            adds[i].Position = new Vector2D(
                center.X + MathF.Cos(angle) * rx,
                Math.Clamp(center.Y + MathF.Sin(angle) * ry, MinPlayableY, MaxPlayableY)
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
        Random rng,
        float mapLength = ExileMovementController.DefaultMapLength)
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
                        Math.Clamp(position.X + MathF.Cos(angle) * 32f, 40f, mapLength - 40f),
                        Math.Clamp(position.Y + MathF.Sin(angle) * 28f, MinPlayableY, MaxPlayableY)
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
                        Math.Clamp(position.X + MathF.Cos(angle) * r, 40f, mapLength - 40f),
                        Math.Clamp(position.Y + MathF.Sin(angle) * r, MinPlayableY, MaxPlayableY)
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
                        Math.Clamp(position.X + MathF.Cos(angle) * 35f, 40f, mapLength - 40f),
                        Math.Clamp(position.Y + MathF.Sin(angle) * 28f, MinPlayableY, MaxPlayableY)
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
