using System;
using System.Collections.Generic;
using IdleExile.Core.Models;

namespace IdleExile.Core.State;

/// <summary>
/// Manages player equipped gear across all 10 fixed equipment slots defined in docs/11_equipment_system_design.md:
/// MainHand, OffHand, Helmet, BodyArmour, Gloves, Boots, Belt, Amulet, Ring1, Ring2.
/// Enforces Path of Exile gear constraints (2H lockout, Bow+Quiver, Dual-Wielding bonuses).
/// Pure C# state model — zero Godot dependencies.
/// </summary>
public sealed class EquippedGearState
{
    public WeaponItemInstance? MainHand { get; private set; }
    public IGearItem? OffHand { get; private set; }
    public ArmourItemInstance? Helmet { get; private set; }
    public ArmourItemInstance? BodyArmour { get; private set; }
    public ArmourItemInstance? Gloves { get; private set; }
    public ArmourItemInstance? Boots { get; private set; }
    public AccessoryItemInstance? Belt { get; private set; }
    public AccessoryItemInstance? Amulet { get; private set; }
    public AccessoryItemInstance? Ring1 { get; private set; }
    public AccessoryItemInstance? Ring2 { get; private set; }

    /// <summary>Fired when any equipment slot changes.</summary>
    public event Action? OnEquipmentChanged;

    /// <summary>Returns true if OffHand is blocked by a non-bow Two-Handed weapon in Main Hand.</summary>
    public bool IsOffHandDisabledByTwoHander =>
        MainHand != null &&
        MainHand.BaseDefinition.HandRequirement == HandSlotRequirement.TwoHanded &&
        MainHand.BaseDefinition.WeaponClass != WeaponClass.Bow;

    /// <summary>True when wielding two one-handed weapons simultaneously.</summary>
    public bool IsDualWielding =>
        MainHand != null &&
        OffHand is WeaponItemInstance offWeapon &&
        MainHand.BaseDefinition.HandRequirement == HandSlotRequirement.OneHanded &&
        offWeapon.BaseDefinition.HandRequirement == HandSlotRequirement.OneHanded &&
        MainHand.BaseDefinition.WeaponClass != WeaponClass.Shield &&
        offWeapon.BaseDefinition.WeaponClass != WeaponClass.Shield;

    /// <summary>Multiplicative APS factor from dual wielding (+10% More APS).</summary>
    public float DualWieldMoreAttackSpeed => IsDualWielding ? 1.10f : 1.0f;

    /// <summary>Additive block chance granted natively by dual wielding (+15% Block).</summary>
    public float DualWieldBlockBonus => IsDualWielding ? 15.0f : 0f;

    /// <summary>
    /// Returns the item currently equipped in the specified slot.
    /// </summary>
    public IGearItem? GetItem(GearSlot slot) => slot switch
    {
        GearSlot.MainHand => MainHand,
        GearSlot.OffHand => OffHand,
        GearSlot.Helmet => Helmet,
        GearSlot.BodyArmour => BodyArmour,
        GearSlot.Gloves => Gloves,
        GearSlot.Boots => Boots,
        GearSlot.Belt => Belt,
        GearSlot.Amulet => Amulet,
        GearSlot.Ring1 => Ring1,
        GearSlot.Ring2 => Ring2,
        _ => null
    };

    /// <summary>
    /// Equips a gear item into a specified or appropriate slot.
    /// Returns true if successfully equipped, false otherwise.
    /// </summary>
    public bool Equip(GearSlot slot, IGearItem item)
    {
        if (item == null)
            return false;

        switch (slot)
        {
            case GearSlot.MainHand:
                if (item is WeaponItemInstance mainWeapon)
                    return EquipMainHand(mainWeapon);
                return false;

            case GearSlot.OffHand:
                return EquipOffHand(item);

            case GearSlot.Helmet:
                if (item is ArmourItemInstance helm && helm.BaseDefinition.ArmourSlot == ArmourSlot.Helmet)
                {
                    Helmet = helm;
                    OnEquipmentChanged?.Invoke();
                    return true;
                }
                return false;

            case GearSlot.BodyArmour:
                if (item is ArmourItemInstance body && body.BaseDefinition.ArmourSlot == ArmourSlot.BodyArmour)
                {
                    BodyArmour = body;
                    OnEquipmentChanged?.Invoke();
                    return true;
                }
                return false;

            case GearSlot.Gloves:
                if (item is ArmourItemInstance gloves && gloves.BaseDefinition.ArmourSlot == ArmourSlot.Gloves)
                {
                    Gloves = gloves;
                    OnEquipmentChanged?.Invoke();
                    return true;
                }
                return false;

            case GearSlot.Boots:
                if (item is ArmourItemInstance boots && boots.BaseDefinition.ArmourSlot == ArmourSlot.Boots)
                {
                    Boots = boots;
                    OnEquipmentChanged?.Invoke();
                    return true;
                }
                return false;

            case GearSlot.Belt:
                if (item is AccessoryItemInstance belt && belt.BaseDefinition.AccessorySlot == AccessorySlot.Belt)
                {
                    Belt = belt;
                    OnEquipmentChanged?.Invoke();
                    return true;
                }
                return false;

            case GearSlot.Amulet:
                if (item is AccessoryItemInstance amulet && amulet.BaseDefinition.AccessorySlot == AccessorySlot.Amulet)
                {
                    Amulet = amulet;
                    OnEquipmentChanged?.Invoke();
                    return true;
                }
                return false;

            case GearSlot.Ring1:
                if (item is AccessoryItemInstance ring1 && ring1.BaseDefinition.AccessorySlot == AccessorySlot.Ring)
                {
                    Ring1 = ring1;
                    OnEquipmentChanged?.Invoke();
                    return true;
                }
                return false;

            case GearSlot.Ring2:
                if (item is AccessoryItemInstance ring2 && ring2.BaseDefinition.AccessorySlot == AccessorySlot.Ring)
                {
                    Ring2 = ring2;
                    OnEquipmentChanged?.Invoke();
                    return true;
                }
                return false;

            default:
                return false;
        }
    }

    /// <summary>
    /// Equips a weapon into the Main Hand slot, enforcing two-handed and off-hand compatibility.
    /// </summary>
    public bool EquipMainHand(WeaponItemInstance weapon)
    {
        if (weapon == null)
            return false;

        if (weapon.BaseDefinition.HandRequirement == HandSlotRequirement.OffHandOnly)
            return false; // Shields and Quivers cannot be equipped in Main Hand

        MainHand = weapon;

        // If equipping a non-bow Two-Handed weapon, clear off-hand
        if (weapon.BaseDefinition.HandRequirement == HandSlotRequirement.TwoHanded &&
            weapon.BaseDefinition.WeaponClass != WeaponClass.Bow)
        {
            OffHand = null;
        }
        // If equipping a Bow and offhand is not a quiver, clear offhand
        else if (weapon.BaseDefinition.WeaponClass == WeaponClass.Bow &&
                 OffHand is WeaponItemInstance offW &&
                 offW.BaseDefinition.WeaponClass != WeaponClass.Quiver)
        {
            OffHand = null;
        }

        OnEquipmentChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Equips an item into the Off Hand slot, enforcing shield, quiver, and dual-wielding rules.
    /// </summary>
    public bool EquipOffHand(IGearItem item)
    {
        if (item == null)
            return false;

        // Cannot equip off-hand if main hand is a non-bow two-handed weapon
        if (IsOffHandDisabledByTwoHander)
            return false;

        if (item is WeaponItemInstance weapon)
        {
            // Quiver can only be equipped if Main Hand is a Bow
            if (weapon.BaseDefinition.WeaponClass == WeaponClass.Quiver)
            {
                if (MainHand != null && MainHand.BaseDefinition.WeaponClass != WeaponClass.Bow)
                    return false;
            }

            // Two-handed weapons cannot be equipped in off-hand
            if (weapon.BaseDefinition.HandRequirement == HandSlotRequirement.TwoHanded)
                return false;

            OffHand = weapon;
            OnEquipmentChanged?.Invoke();
            return true;
        }
        else if (item is ArmourItemInstance shield && shield.BaseDefinition.ArmourSlot == ArmourSlot.Shield)
        {
            OffHand = shield;
            OnEquipmentChanged?.Invoke();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Unequips the item from the specified slot and returns it.
    /// </summary>
    public IGearItem? Unequip(GearSlot slot)
    {
        IGearItem? prev = null;
        switch (slot)
        {
            case GearSlot.MainHand:
                prev = MainHand;
                MainHand = null;
                break;
            case GearSlot.OffHand:
                prev = OffHand;
                OffHand = null;
                break;
            case GearSlot.Helmet:
                prev = Helmet;
                Helmet = null;
                break;
            case GearSlot.BodyArmour:
                prev = BodyArmour;
                BodyArmour = null;
                break;
            case GearSlot.Gloves:
                prev = Gloves;
                Gloves = null;
                break;
            case GearSlot.Boots:
                prev = Boots;
                Boots = null;
                break;
            case GearSlot.Belt:
                prev = Belt;
                Belt = null;
                break;
            case GearSlot.Amulet:
                prev = Amulet;
                Amulet = null;
                break;
            case GearSlot.Ring1:
                prev = Ring1;
                Ring1 = null;
                break;
            case GearSlot.Ring2:
                prev = Ring2;
                Ring2 = null;
                break;
        }

        if (prev != null)
        {
            OnEquipmentChanged?.Invoke();
        }
        return prev;
    }

    public WeaponItemInstance? UnequipMainHand() => Unequip(GearSlot.MainHand) as WeaponItemInstance;
    public IGearItem? UnequipOffHand() => Unequip(GearSlot.OffHand);

    /// <summary>Returns an enumerable of all currently equipped gear items.</summary>
    public IEnumerable<IGearItem> GetAllEquippedItems()
    {
        if (MainHand != null) yield return MainHand;
        if (OffHand != null) yield return OffHand;
        if (Helmet != null) yield return Helmet;
        if (BodyArmour != null) yield return BodyArmour;
        if (Gloves != null) yield return Gloves;
        if (Boots != null) yield return Boots;
        if (Belt != null) yield return Belt;
        if (Amulet != null) yield return Amulet;
        if (Ring1 != null) yield return Ring1;
        if (Ring2 != null) yield return Ring2;
    }

    // ─────────────────────────────────────────────
    //  Aggregated Defensive & Offensive Calculations
    // ─────────────────────────────────────────────

    public float GetTotalArmour()
    {
        float total = 0f;
        foreach (var item in GetAllEquippedItems())
            total += item.LocalArmour;
        return total;
    }

    public float GetTotalEvasion()
    {
        float total = 0f;
        foreach (var item in GetAllEquippedItems())
            total += item.LocalEvasion;
        return total;
    }

    public float GetTotalFlatLife()
    {
        float total = 0f;
        foreach (var item in GetAllEquippedItems())
            total += item.FlatLifeBonus;
        return total;
    }

    public float GetTotalFlatEnergyShield()
    {
        float total = 0f;
        foreach (var item in GetAllEquippedItems())
            total += item.LocalEnergyShield + item.FlatEnergyShieldBonus;
        return total;
    }

    public float GetTotalFlatMana()
    {
        float total = 0f;
        foreach (var item in GetAllEquippedItems())
            total += item.FlatManaBonus;
        return total;
    }

    public float GetTotalFireRes()
    {
        float total = 0f;
        foreach (var item in GetAllEquippedItems())
            total += item.FireResistanceBonus;
        return total;
    }

    public float GetTotalColdRes()
    {
        float total = 0f;
        foreach (var item in GetAllEquippedItems())
            total += item.ColdResistanceBonus;
        return total;
    }

    public float GetTotalLightningRes()
    {
        float total = 0f;
        foreach (var item in GetAllEquippedItems())
            total += item.LightningResistanceBonus;
        return total;
    }

    public float GetTotalChaosRes()
    {
        float total = 0f;
        foreach (var item in GetAllEquippedItems())
            total += item.ChaosResistanceBonus;
        return total;
    }

    public float GetTotalBlockChance()
    {
        float total = DualWieldBlockBonus;
        foreach (var item in GetAllEquippedItems())
            total += item.LocalBlockChance;
        return total;
    }

    public int GetTotalStrengthBonus()
    {
        int total = 0;
        foreach (var item in GetAllEquippedItems())
            total += item.StrengthBonus;
        return total;
    }

    public int GetTotalDexterityBonus()
    {
        int total = 0;
        foreach (var item in GetAllEquippedItems())
            total += item.DexterityBonus;
        return total;
    }

    public int GetTotalIntelligenceBonus()
    {
        int total = 0;
        foreach (var item in GetAllEquippedItems())
            total += item.IntelligenceBonus;
        return total;
    }

    public float GetTotalIncreasedDamagePercent()
    {
        float total = 0f;
        foreach (var item in GetAllEquippedItems())
            total += item.GlobalIncreasedDamagePercent;
        return total;
    }

    public float GetTotalAttackSpeedPercent()
    {
        float total = 0f;
        foreach (var item in GetAllEquippedItems())
            total += item.GlobalAttackSpeedPercent;
        return total;
    }

    /// <summary>
    /// Computes the effective base attacks per second incorporating dual-wielding and global gear attack speed.
    /// </summary>
    public float GetEffectiveBaseAps()
    {
        float baseAps = MainHand?.ComputedAps ?? 1.20f;

        if (IsDualWielding && OffHand is WeaponItemInstance offW)
        {
            baseAps = ((MainHand!.ComputedAps + offW.ComputedAps) / 2f) * DualWieldMoreAttackSpeed;
        }

        float incAps = GetTotalAttackSpeedPercent();
        return baseAps * (1f + incAps / 100f);
    }

    /// <summary>
    /// Returns the combined base physical damage range from equipped weapons.
    /// </summary>
    public DamageRange GetBasePhysicalDamageRange(bool isCleave = false)
    {
        if (MainHand == null)
        {
            return new DamageRange(8f, 14f); // Default unarmed fallback
        }

        if (isCleave && IsDualWielding && OffHand is WeaponItemInstance offW)
        {
            float min = MainHand.ComputedMinDamage * 0.6f + offW.ComputedMinDamage * 0.6f;
            float max = MainHand.ComputedMaxDamage * 0.6f + offW.ComputedMaxDamage * 0.6f;
            return new DamageRange(min, max);
        }

        return new DamageRange(MainHand.ComputedMinDamage, MainHand.ComputedMaxDamage);
    }
}
