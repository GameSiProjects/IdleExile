using System;
using System.Collections.Generic;
using System.Linq;
using IdleExile.Core.Models;

namespace IdleExile.Core.Simulation;

/// <summary>
/// Configuration for a specific map run, combining the base MapDefinition
/// with any rolled MapModifiers. Consumed by <see cref="WaveSpawner"/> to
/// determine monster scaling, pack composition, and bonus rewards.
/// </summary>
public sealed class MapRunConfig
{
    /// <summary>The base map definition from data/maps.json.</summary>
    public MapDefinition Map { get; }

    /// <summary>Rolled map modifier instances with their specific rolled values.</summary>
    public IReadOnlyList<RolledMapModifier> Modifiers { get; }

    /// <summary>
    /// Effective monster level for this run.
    /// Equals Map.MonsterLevel for normal maps, or player level for T0 fail-safe.
    /// </summary>
    public int EffectiveMonsterLevel { get; }

    /// <summary>
    /// Total Item Quantity bonus: Map base + sum of modifier bonuses.
    /// Used to multiply drop rates.
    /// </summary>
    public int TotalQuantity { get; }

    /// <summary>
    /// Total Pack Size: Map base + sum of modifier bonuses.
    /// Determines how many monsters spawn per wave.
    /// </summary>
    public int TotalPackSize { get; }

    public MapRunConfig(MapDefinition map, IReadOnlyList<RolledMapModifier> modifiers, int? overrideMonsterLevel = null)
    {
        Map = map;
        Modifiers = modifiers;

        EffectiveMonsterLevel = overrideMonsterLevel ?? map.MonsterLevel;
        TotalQuantity = map.BaseQuantity + modifiers.Sum(m => m.Definition.BonusQuantity);
        TotalPackSize = map.BasePackSize + modifiers.Sum(m => m.Definition.BonusPackSize);
    }
}

/// <summary>
/// A map modifier with its specific rolled value within [Min, Max] range.
/// </summary>
public sealed record RolledMapModifier(
    MapModifierDefinition Definition,
    float RolledValue
)
{
    /// <summary>Formats the modifier name with the rolled value (e.g. "Monsters deal 42% extra damage as Cold").</summary>
    public string FormattedName => string.Format(Definition.NameTemplate, RolledValue);
}

/// <summary>
/// A spawned wave of monsters representing one encounter in a map tick sequence.
/// Contains the monster pack to be fought and metadata about the wave.
/// </summary>
public sealed class MonsterWave
{
    /// <summary>Sequential wave number within the current map run (1-based).</summary>
    public int WaveNumber { get; init; }

    /// <summary>All monsters in this wave, including pack leader if any.</summary>
    public IReadOnlyList<MonsterInstance> Monsters { get; init; } = Array.Empty<MonsterInstance>();

    /// <summary>Total number of monsters in this wave.</summary>
    public int Count => Monsters.Count;

    /// <summary>True if this is the final boss wave.</summary>
    public bool IsBossWave { get; init; }

    /// <summary>Corridor pack metadata and spatial formation (Task 2.7).</summary>
    public CorridorPack? Pack { get; init; }

    /// <summary>Formation type of the pack in this wave.</summary>
    public CorridorPackFormationType? Formation => Pack?.FormationType;

    /// <summary>Associated roadside League Encounter, if this wave represents one.</summary>
    public LeagueEncounterInstance? Encounter => Pack?.Encounter;

    /// <summary>True if this wave belongs to a roadside League Encounter.</summary>
    public bool IsEncounterWave => Pack?.IsEncounterPack == true;

    /// <summary>Returns all living monsters in this wave.</summary>
    public IEnumerable<MonsterInstance> Alive => Monsters.Where(m => m.IsAlive);

    /// <summary>True when all monsters in this wave are dead.</summary>
    public bool IsCleared => !Monsters.Any(m => m.IsAlive);
}

/// <summary>
/// Generates monster waves for map runs. Handles stat scaling by monster level,
/// rarity multipliers, pack composition, and boss spawning.
///
/// Follows PoE's exponential scaling curves where monster HP and damage
/// grow exponentially with level, creating natural difficulty gates.
///
/// Pure C# — no Godot dependency.
/// </summary>
public sealed class WaveSpawner
{
    // ─────────────────────────────────────────────
    //  Constants: PoE-Inspired Scaling Curves
    // ─────────────────────────────────────────────

    // Base stats at level 1 (baseline for all scaling)
    private const float BaseMonsterLife = 18f;
    private const float BaseMonsterDamageMin = 2f;
    private const float BaseMonsterDamageMax = 5f;
    private const float BaseMonsterExperience = 5f;
    private const float BaseMonsterAccuracy = 30f;

    // Exponential growth rates per level (PoE-accurate)
    // HP grows ~3.5% per level compounding → level 80 monster has ~1200x base HP
    private const float LifeGrowthRate = 0.035f;
    // Damage grows ~2.8% per level compounding
    private const float DamageGrowthRate = 0.028f;
    // Experience grows ~3.2% per level compounding
    private const float ExperienceGrowthRate = 0.032f;
    // Accuracy grows ~2.0% per level compounding
    private const float AccuracyGrowthRate = 0.020f;

    // ─── Rarity Multipliers (how much each rarity boosts base stats) ───

    /// <summary>Life multiplier by rarity: Normal 1x, Magic 2x, Rare 4.5x, Boss 20x.</summary>
    private static readonly float[] RarityLifeMultiplier = [1.0f, 2.0f, 4.5f, 20.0f];

    /// <summary>Damage multiplier by rarity: Normal 1x, Magic 1.3x, Rare 1.8x, Boss 3.0x.</summary>
    private static readonly float[] RarityDamageMultiplier = [1.0f, 1.3f, 1.8f, 3.0f];

    /// <summary>Experience multiplier by rarity: Normal 1x, Magic 3x, Rare 8x, Boss 50x.</summary>
    private static readonly float[] RarityExpMultiplier = [1.0f, 3.0f, 8.0f, 50.0f];

    /// <summary>Item Quantity multiplier by rarity: Normal 1x, Magic 1.5x, Rare 3x, Boss 5x.</summary>
    private static readonly float[] RarityQuantityMultiplier = [1.0f, 1.5f, 3.0f, 5.0f];

    /// <summary>Item Rarity multiplier by rarity: Normal 1x, Magic 2x, Rare 4x, Boss 8x.</summary>
    private static readonly float[] RarityRarityMultiplier = [1.0f, 2.0f, 4.0f, 8.0f];

    // ─── Pack Composition Weights ───

    /// <summary>Base number of trash mobs in a standard pack (before pack size scaling).</summary>
    private const int BasePackTrashCount = 6;

    /// <summary>Total waves of trash before boss spawns (the 70% clearing phase).</summary>
    private const int BaseTrashWaveCount = 8;

    /// <summary>Chance (0-100) for a wave to contain a Magic monster as pack leader.</summary>
    private const float MagicMonsterChance = 30f;

    /// <summary>Chance (0-100) for a wave to contain a Rare monster as pack leader.</summary>
    private const float RareMonsterChance = 12f;

    // ─── Monster Name Pools (cosmetic) ───

    private static readonly string[] TrashMonsterNames =
    [
        "Skeletal Warrior", "Rotting Shambler", "Bone Archer",
        "Plague Rat", "Hollow Sentinel", "Carrion Crawler",
        "Ashen Husk", "Voidling", "Cursed Mariner",
        "Stone Golem", "Spectral Guard", "Blight Wretch"
    ];

    private static readonly string[] MagicMonsterPrefixes =
    [
        "Blazing", "Frozen", "Charged", "Venomous",
        "Swift", "Brutal", "Cursed", "Ironclad"
    ];

    private static readonly string[] RareMonsterNames =
    [
        "Grothak the Fleshripper", "Xul'thane, Soulbinder",
        "Azra the Undying", "Krivax, Web-Spinner",
        "Morvael, Ashen Herald", "Drek'thar, Bone King",
        "Solara, Lightning Fang", "Vex'ahlia, Doomwhisper"
    ];

    // ─── Rare Monster Aura Pool ───

    public static readonly MonsterAuraModifier[] AuraPool =
    [
        new("haste", "Allies have +25% Action Speed", 0f, 25f, 0f),
        new("extra_fire", "Allies deal extra Fire damage", 15f, 0f, 0f),
        new("extra_cold", "Allies deal extra Cold damage", 15f, 0f, 0f),
        new("extra_lightning", "Allies deal extra Lightning damage", 15f, 0f, 0f),
        new("fortify", "Allies have +20% Elemental Resistance", 0f, 0f, 20f),
        new("frenzy", "Allies have +15% Action Speed, deal extra damage", 10f, 15f, 0f),
        new("berserker", "Allies deal significantly more damage", 25f, 0f, 0f),
        new("warding", "Allies have +35% Elemental Resistance", 0f, 0f, 35f)
    ];

    // ─────────────────────────────────────────────
    //  Instance State
    // ─────────────────────────────────────────────

    private readonly Random _random;
    private long _nextInstanceId;
    private int _currentWaveNumber;

    /// <summary>Map run configuration for the current map.</summary>
    public MapRunConfig? CurrentConfig { get; private set; }

    /// <summary>Structured sequential corridor layout for the active map run (Task 2.7).</summary>
    public CorridorMonsterLayout? CurrentLayout { get; private set; }

    /// <summary>Total waves expected before boss (trash clearing phase).</summary>
    public int TotalTrashWaves { get; private set; }

    /// <summary>True once all trash waves AND the boss wave have been spawned.</summary>
    public bool IsMapComplete { get; private set; }

    /// <summary>True once the boss wave has been spawned.</summary>
    public bool BossSpawned { get; private set; }

    // ─────────────────────────────────────────────
    //  Events
    // ─────────────────────────────────────────────

    /// <summary>Fired when a new wave is spawned. Args: the spawned wave.</summary>
    public event Action<MonsterWave>? OnWaveSpawned;

    /// <summary>Fired when the boss wave is spawned.</summary>
    public event Action<MonsterWave>? OnBossWaveSpawned;

    /// <summary>Fired when all waves (including boss) have been spawned.</summary>
    public event Action? OnMapCompleted;

    // ─────────────────────────────────────────────
    //  Constructor
    // ─────────────────────────────────────────────

    public WaveSpawner(Random? random = null)
    {
        _random = random ?? new Random();
        _nextInstanceId = 1;
    }

    // ─────────────────────────────────────────────
    //  Map Initialization
    // ─────────────────────────────────────────────

    /// <summary>
    /// Initializes the spawner for a new map run.
    /// Resets wave counter, generates sequential corridor layout based on map config.
    /// Must be called before <see cref="SpawnNextWave"/>.
    /// </summary>
    public void BeginMap(
        MapRunConfig config,
        float mapLength = ExileMovementController.DefaultMapLength,
        LeagueEncounterType? forceEncounter = null)
    {
        CurrentConfig = config;
        _currentWaveNumber = 0;
        IsMapComplete = false;
        BossSpawned = false;

        // Task 2.7: Generate structured sequential corridor layout
        CurrentLayout = CorridorMonsterLayout.Generate(
            config,
            (rarity, overrideName) => CreateMonster(rarity, config, overrideName),
            mapLength,
            _random,
            forceEncounter
        );

        TotalTrashWaves = CurrentLayout.Packs.Count(p => p.FormationType != CorridorPackFormationType.BossChamber);
    }

    // ─────────────────────────────────────────────
    //  Wave Spawning
    // ─────────────────────────────────────────────

    /// <summary>
    /// Spawns the next wave of monsters for the current map run.
    /// Returns null if the map is already complete.
    ///
    /// Progression:
    /// - Waves 1 to TotalTrashWaves: Sequential packs along corridor (Vanguard, Commander, Encounter, Flankers, Gate Phalanx).
    /// - Final wave: Boss Chamber pack.
    /// </summary>
    public MonsterWave? SpawnNextWave()
    {
        if (CurrentConfig == null || IsMapComplete)
            return null;

        _currentWaveNumber++;

        // 1. Task 2.7: Pull from structured CorridorMonsterLayout if available
        if (CurrentLayout != null && _currentWaveNumber <= CurrentLayout.Packs.Count)
        {
            var pack = CurrentLayout.Packs[_currentWaveNumber - 1];
            bool isBoss = pack.FormationType == CorridorPackFormationType.BossChamber;

            if (isBoss)
            {
                BossSpawned = true;
                IsMapComplete = true;
            }

            var wave = new MonsterWave
            {
                WaveNumber = _currentWaveNumber,
                Monsters = pack.Monsters,
                IsBossWave = isBoss,
                Pack = pack
            };

            if (isBoss)
            {
                OnBossWaveSpawned?.Invoke(wave);
                OnWaveSpawned?.Invoke(wave);
                OnMapCompleted?.Invoke();
            }
            else
            {
                OnWaveSpawned?.Invoke(wave);
            }

            return wave;
        }

        // 2. Legacy fallback
        // Boss wave
        if (_currentWaveNumber > TotalTrashWaves && !BossSpawned)
        {
            BossSpawned = true;
            IsMapComplete = true;

            var bossWave = SpawnBossWave();
            OnBossWaveSpawned?.Invoke(bossWave);
            OnWaveSpawned?.Invoke(bossWave);
            OnMapCompleted?.Invoke();
            return bossWave;
        }

        // Map with no boss — mark complete after trash waves
        if (_currentWaveNumber > TotalTrashWaves)
        {
            IsMapComplete = true;
            OnMapCompleted?.Invoke();
            return null;
        }

        // Normal trash wave (with possible Magic/Rare leader)
        var legacyWave = SpawnTrashWave();
        OnWaveSpawned?.Invoke(legacyWave);
        return legacyWave;
    }

    /// <summary>
    /// Returns the clear progress of the trash clearing phase as a fraction (0.0 to 1.0).
    /// Reaches 1.0 when all trash waves have been spawned (boss is next).
    /// </summary>
    public float GetClearProgress()
    {
        if (TotalTrashWaves <= 0) return 1f;
        return Math.Clamp((float)Math.Min(_currentWaveNumber, TotalTrashWaves) / TotalTrashWaves, 0f, 1f);
    }

    // ─────────────────────────────────────────────
    //  Internal: Trash Wave Generation
    // ─────────────────────────────────────────────

    private MonsterWave SpawnTrashWave()
    {
        var config = CurrentConfig!;
        var monsters = new List<MonsterInstance>();

        // Determine pack composition: roll for Magic or Rare leader
        bool hasMagicLeader = _random.NextDouble() * 100.0 < MagicMonsterChance;
        bool hasRareLeader = !hasMagicLeader && _random.NextDouble() * 100.0 < RareMonsterChance;

        // Pack size scales with map pack size modifier
        int trashCount = Math.Max(1, (int)MathF.Round(BasePackTrashCount * (config.TotalPackSize / 100f)));

        // Spawn trash mobs
        for (int i = 0; i < trashCount; i++)
        {
            monsters.Add(CreateMonster(MonsterRarity.Normal, config));
        }

        // Spawn pack leader if rolled
        if (hasRareLeader)
        {
            monsters.Add(CreateMonster(MonsterRarity.Rare, config));
        }
        else if (hasMagicLeader)
        {
            monsters.Add(CreateMonster(MonsterRarity.Magic, config));
        }

        LayoutWavePositions(monsters, isBossWave: false);

        return new MonsterWave
        {
            WaveNumber = _currentWaveNumber,
            Monsters = monsters,
            IsBossWave = false
        };
    }

    private MonsterWave SpawnBossWave()
    {
        var config = CurrentConfig!;
        var monsters = new List<MonsterInstance>();

        // Boss itself
        string bossName = config.Map.BossName ?? $"Guardian of {config.Map.Name}";
        monsters.Add(CreateMonster(MonsterRarity.Boss, config, bossName));

        // Boss add pack: 3-4 trash mobs accompanying the boss
        int addCount = 3 + (_random.Next(2)); // 3 or 4
        for (int i = 0; i < addCount; i++)
        {
            monsters.Add(CreateMonster(MonsterRarity.Normal, config));
        }

        LayoutWavePositions(monsters, isBossWave: true);

        return new MonsterWave
        {
            WaveNumber = _currentWaveNumber,
            Monsters = monsters,
            IsBossWave = true
        };
    }

    /// <summary>
    /// Arranges spawned monsters in 2D Top-Down formations:
    /// - Boss Wave: Boss at Northeast/East (320, 95), minions escorting.
    /// - Commander & Phalanx: Rare monster stands back-right with minions forming a perimeter.
    /// - Surrounding Swarm: Trash mobs surround Exile (220, 104) in a 360-degree ring.
    /// </summary>
    private static void LayoutWavePositions(List<MonsterInstance> monsters, bool isBossWave)
    {
        if (monsters.Count == 0) return;

        Vector2D center = IdleExile.Core.Services.SpatialCombatService.DefaultExilePosition; // (220, 104)

        if (isBossWave)
        {
            monsters[0].Position = new Vector2D(320f, 95f);
            for (int i = 1; i < monsters.Count; i++)
            {
                float addAngle = (i * MathF.Tau / Math.Max(1, monsters.Count - 1)) + 0.5f;
                float ax = Math.Clamp(320f + MathF.Cos(addAngle) * 55f, 50f, 390f);
                float ay = Math.Clamp(95f + MathF.Sin(addAngle) * 45f, 30f, 178f);
                monsters[i].Position = new Vector2D(ax, ay);
            }
            return;
        }

        var leader = monsters.FirstOrDefault(m => m.Rarity == MonsterRarity.Rare || m.Rarity == MonsterRarity.Magic);
        if (leader != null && leader.Rarity == MonsterRarity.Rare)
        {
            leader.Position = new Vector2D(310f, 85f);
            var minions = monsters.Where(m => m != leader).ToList();
            for (int i = 0; i < minions.Count; i++)
            {
                float angle = (i * MathF.Tau / Math.Max(1, minions.Count)) + 0.2f;
                float rx = 40f + (i % 2) * 15f;
                float ry = 30f + (i % 2) * 12f;
                float mx = Math.Clamp(leader.Position.X + MathF.Cos(angle) * rx, 40f, 400f);
                float my = Math.Clamp(leader.Position.Y + MathF.Sin(angle) * ry, 28f, 180f);
                minions[i].Position = new Vector2D(mx, my);
            }
            return;
        }

        // Surrounding Swarm: 360-degree radial ring around Exile
        for (int i = 0; i < monsters.Count; i++)
        {
            float angle = (i * MathF.Tau / Math.Max(1, monsters.Count)) + 0.35f;
            float radiusX = 110f + (i % 3) * 22f;
            float radiusY = 55f + (i % 2) * 18f;

            float x = Math.Clamp(center.X + MathF.Cos(angle) * radiusX, 40f, 400f);
            float y = Math.Clamp(center.Y + MathF.Sin(angle) * radiusY, 28f, 180f);
            monsters[i].Position = new Vector2D(x, y);
        }
    }

    // ─────────────────────────────────────────────
    //  Internal: Single Monster Creation
    // ─────────────────────────────────────────────

    /// <summary>
    /// Creates a single monster with stats fully scaled by monster level and rarity.
    /// Uses PoE-style exponential scaling: stat = base * e^(growthRate * level) * rarityMultiplier.
    /// </summary>
    private MonsterInstance CreateMonster(MonsterRarity rarity, MapRunConfig config, string? overrideName = null)
    {
        int level = config.EffectiveMonsterLevel;
        int rarityIndex = (int)rarity;

        // ── Exponential level scaling ──
        float lifeScale = MathF.Exp(LifeGrowthRate * level);
        float damageScale = MathF.Exp(DamageGrowthRate * level);
        float expScale = MathF.Exp(ExperienceGrowthRate * level);
        float accuracyScale = MathF.Exp(AccuracyGrowthRate * level);

        // ── Rarity multipliers ──
        float lifeMulti = RarityLifeMultiplier[rarityIndex];
        float damageMulti = RarityDamageMultiplier[rarityIndex];
        float expMulti = RarityExpMultiplier[rarityIndex];

        // ── Final scaled stats ──
        float maxLife = BaseMonsterLife * lifeScale * lifeMulti;
        float damageMin = BaseMonsterDamageMin * damageScale * damageMulti;
        float damageMax = BaseMonsterDamageMax * damageScale * damageMulti;
        float experience = BaseMonsterExperience * expScale * expMulti;
        float accuracy = BaseMonsterAccuracy * accuracyScale;

        // ── Name generation ──
        string name = overrideName ?? GenerateName(rarity);

        // ── Offensive detail ──
        DamageType primaryType = DamageType.Physical;
        float attacksPerSecond = rarity switch
        {
            MonsterRarity.Normal => 1.0f + _random.NextSingle() * 0.3f,  // 1.0-1.3 APS
            MonsterRarity.Magic => 1.2f + _random.NextSingle() * 0.3f,   // 1.2-1.5 APS
            MonsterRarity.Rare => 1.0f + _random.NextSingle() * 0.4f,    // 1.0-1.4 APS (slower but harder hitting)
            MonsterRarity.Boss => 0.7f + _random.NextSingle() * 0.3f,    // 0.7-1.0 APS (slow, devastating)
            _ => 1.0f
        };

        float critChance = rarity switch
        {
            MonsterRarity.Normal => 5f,
            MonsterRarity.Magic => 5f,
            MonsterRarity.Rare => 8f,
            MonsterRarity.Boss => 10f,
            _ => 5f
        };

        float critMultiplier = rarity switch
        {
            MonsterRarity.Normal => 130f,   // 1.3x
            MonsterRarity.Magic => 130f,
            MonsterRarity.Rare => 140f,     // 1.4x
            MonsterRarity.Boss => 150f,     // 1.5x
            _ => 130f
        };

        // ── Monster resistances scale with level (bosses get bonus) ──
        float baseElementalRes = Math.Clamp(level * 0.4f, 0f, 40f);
        float baseChaosRes = Math.Clamp(level * 0.15f, 0f, 25f);

        float resBonus = rarity switch
        {
            MonsterRarity.Rare => 10f,
            MonsterRarity.Boss => 20f,
            _ => 0f
        };

        // ── Aura modifiers for Rare monsters ──
        IReadOnlyList<MonsterAuraModifier>? auras = null;
        if (rarity == MonsterRarity.Rare)
        {
            auras = RollAuraModifiers(level);
        }

        // ── Reward multipliers ──
        float quantityMulti = RarityQuantityMultiplier[rarityIndex];
        float rarityMulti = RarityRarityMultiplier[rarityIndex];

        return new MonsterInstance(
            instanceId: _nextInstanceId++,
            name: name,
            rarity: rarity,
            monsterLevel: level,
            maxLife: maxLife,
            primaryDamageType: primaryType,
            damageMin: damageMin,
            damageMax: damageMax,
            attacksPerSecond: attacksPerSecond,
            critChance: critChance,
            critMultiplier: critMultiplier,
            accuracy: accuracy,
            baseExperience: experience,
            itemQuantityMultiplier: quantityMulti,
            itemRarityMultiplier: rarityMulti,
            auraModifiers: auras)
        {
            FireResistance = Math.Clamp(baseElementalRes + resBonus, 0f, 75f),
            ColdResistance = Math.Clamp(baseElementalRes + resBonus, 0f, 75f),
            LightningResistance = Math.Clamp(baseElementalRes + resBonus, 0f, 75f),
            ChaosResistance = Math.Clamp(baseChaosRes + resBonus, 0f, 75f)
        };
    }

    // ─────────────────────────────────────────────
    //  Internal: Name Generation
    // ─────────────────────────────────────────────

    private string GenerateName(MonsterRarity rarity)
    {
        return rarity switch
        {
            MonsterRarity.Normal => TrashMonsterNames[_random.Next(TrashMonsterNames.Length)],
            MonsterRarity.Magic => $"{MagicMonsterPrefixes[_random.Next(MagicMonsterPrefixes.Length)]} {TrashMonsterNames[_random.Next(TrashMonsterNames.Length)]}",
            MonsterRarity.Rare => RareMonsterNames[_random.Next(RareMonsterNames.Length)],
            MonsterRarity.Boss => "Unknown Boss", // Should always be overridden by MapDefinition.BossName
            _ => "Unknown"
        };
    }

    // ─────────────────────────────────────────────
    //  Internal: Rare Monster Aura Rolling
    // ─────────────────────────────────────────────

    /// <summary>
    /// Rolls 2-4 random aura modifiers for a Rare monster.
    /// Aura bonus damage scales with monster level.
    /// </summary>
    private List<MonsterAuraModifier> RollAuraModifiers(int monsterLevel)
    {
        int auraCount = 2 + _random.Next(3); // 2, 3, or 4 auras
        var selected = new List<MonsterAuraModifier>();
        var usedIndices = new HashSet<int>();

        float levelScale = MathF.Exp(DamageGrowthRate * monsterLevel);

        for (int i = 0; i < auraCount && usedIndices.Count < AuraPool.Length; i++)
        {
            int index;
            do
            {
                index = _random.Next(AuraPool.Length);
            } while (usedIndices.Contains(index));

            usedIndices.Add(index);
            var template = AuraPool[index];

            // Scale flat bonus damage by monster level
            selected.Add(template with
            {
                BonusFlatDamage = template.BonusFlatDamage * levelScale
            });
        }

        return selected;
    }

    // ─────────────────────────────────────────────
    //  Static Utility: Monster Level Scaling Preview
    // ─────────────────────────────────────────────

    /// <summary>
    /// Returns the expected base life of a Normal monster at the given level.
    /// Useful for tooltips, character sheet DPS calculations, and offline estimation.
    /// </summary>
    public static float GetBaseLifeAtLevel(int monsterLevel) =>
        BaseMonsterLife * MathF.Exp(LifeGrowthRate * monsterLevel);

    /// <summary>
    /// Returns the expected average damage of a Normal monster at the given level.
    /// </summary>
    public static float GetAverageDamageAtLevel(int monsterLevel)
    {
        float scale = MathF.Exp(DamageGrowthRate * monsterLevel);
        return (BaseMonsterDamageMin + BaseMonsterDamageMax) * 0.5f * scale;
    }

    /// <summary>
    /// Returns the experience value of a Normal monster at the given level.
    /// </summary>
    public static float GetBaseExperienceAtLevel(int monsterLevel) =>
        BaseMonsterExperience * MathF.Exp(ExperienceGrowthRate * monsterLevel);
}
