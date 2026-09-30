using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using IdleExile.Core.Models;

namespace IdleExile.Core.Services;

public class DatabaseLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public Dictionary<string, AffixDefinition> Affixes { get; private set; } = new();
    public Dictionary<string, WeaponBaseDefinition> WeaponBases { get; private set; } = new();
    public Dictionary<string, ArmourBaseDefinition> ArmourBases { get; private set; } = new();
    public Dictionary<string, AccessoryBaseDefinition> AccessoryBases { get; private set; } = new();
    public Dictionary<string, GemDefinition> Gems { get; private set; } = new();
    public Dictionary<string, MapDefinition> Maps { get; private set; } = new();
    public List<MapModifierDefinition> MapModifiers { get; private set; } = new();

    public void LoadAll()
    {
        LoadAffixes();
        LoadWeaponBases();
        LoadArmourBases();
        LoadAccessoryBases();
        LoadGems();
        LoadMaps();
        ValidateWeaponBases();
        GD.Print($"[DatabaseLoader] Loaded {Affixes.Count} affixes, {WeaponBases.Count} weapons, {ArmourBases.Count} armours, {AccessoryBases.Count} accessories, {Gems.Count} gems, {Maps.Count} maps.");
    }

    private void LoadAffixes()
    {
        string json = FileAccess.GetFileAsString("res://data/affixes.json");
        if (string.IsNullOrWhiteSpace(json))
        {
            GD.PrintErr("[DatabaseLoader] Could not read res://data/affixes.json");
            return;
        }

        var list = JsonSerializer.Deserialize<List<AffixDefinition>>(json, JsonOptions);
        Affixes = list?.ToDictionary(a => a.Id) ?? new();
    }

    private void LoadGems()
    {
        string json = FileAccess.GetFileAsString("res://data/gems.json");
        if (string.IsNullOrWhiteSpace(json))
        {
            GD.PrintErr("[DatabaseLoader] Could not read res://data/gems.json");
            return;
        }

        var list = JsonSerializer.Deserialize<List<GemDefinition>>(json, JsonOptions);
        Gems = list?.ToDictionary(g => g.Id) ?? new();
    }

    private void LoadMaps()
    {
        string json = FileAccess.GetFileAsString("res://data/maps.json");
        if (string.IsNullOrWhiteSpace(json))
        {
            GD.PrintErr("[DatabaseLoader] Could not read res://data/maps.json");
            return;
        }

        var root = JsonSerializer.Deserialize<MapDatabaseRoot>(json, JsonOptions);
        if (root != null)
        {
            Maps = root.Maps.ToDictionary(m => m.Id);
            MapModifiers = root.MapModifiers;
        }
    }

    private void LoadWeaponBases()
    {
        string json = FileAccess.GetFileAsString("res://data/weapon_bases.json");
        if (string.IsNullOrWhiteSpace(json))
        {
            GD.PrintErr("[DatabaseLoader] Could not read res://data/weapon_bases.json");
            return;
        }

        var list = JsonSerializer.Deserialize<List<WeaponBaseDefinition>>(json, JsonOptions);
        WeaponBases = list?.ToDictionary(w => w.Id) ?? new();
    }

    private void LoadArmourBases()
    {
        string json = FileAccess.GetFileAsString("res://data/armour_bases.json");
        if (string.IsNullOrWhiteSpace(json))
        {
            GD.PrintErr("[DatabaseLoader] Could not read res://data/armour_bases.json");
            return;
        }

        var list = JsonSerializer.Deserialize<List<ArmourBaseDefinition>>(json, JsonOptions);
        ArmourBases = list?.ToDictionary(a => a.Id) ?? new();
    }

    private void LoadAccessoryBases()
    {
        string json = FileAccess.GetFileAsString("res://data/accessory_bases.json");
        if (string.IsNullOrWhiteSpace(json))
        {
            GD.PrintErr("[DatabaseLoader] Could not read res://data/accessory_bases.json");
            return;
        }

        var list = JsonSerializer.Deserialize<List<AccessoryBaseDefinition>>(json, JsonOptions);
        AccessoryBases = list?.ToDictionary(a => a.Id) ?? new();
    }

    private void ValidateWeaponBases()
    {
        int missingCount = 0;
        foreach (var weapon in WeaponBases.Values)
        {
            if (!string.IsNullOrWhiteSpace(weapon.ImplicitAffixId) && !Affixes.ContainsKey(weapon.ImplicitAffixId))
            {
                GD.PrintErr($"[DatabaseLoader] Validation Warning: Weapon '{weapon.Id}' references missing ImplicitAffixId '{weapon.ImplicitAffixId}'.");
                missingCount++;
            }
        }

        if (missingCount == 0 && WeaponBases.Count > 0)
        {
            GD.Print($"[DatabaseLoader] Validation Success: All {WeaponBases.Count} weapon bases verified.");
        }
    }
}
