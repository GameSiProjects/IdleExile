# Game Design Documentation (GDD) — `docs/design/`

This directory houses all Game Design Specifications for **Idle Exile** — detailing game balance, mathematical formulas, systems, content tables, itemization, and user experience wireframes.

---

## 📑 Design Document Index

1. **[`01_game_overview.md`](01_game_overview.md)** — *Overview & Design Philosophy*
   - High concept, core pillars ("Zero APM, 100% Theorycrafting").
   - Core gameplay loop: Map Device -> Auto-Combat -> Loot Filter -> Stash -> Anvil Crafting.
   - Immediate Mapping from Level 1 (zero campaign / acts).

2. **[`02_mapping_system.md`](02_mapping_system.md)** — *Mapping System & The Endless Abyss*
   - Dual-Axis progression: Core Mapping (80%) + The Endless Abyss (20%).
   - Tiers T1 to T16+ with Fail-Safe T0 sustain.
   - Map itemization (magic/rare mods, pack size, quantity).
   - Map Queue (10–15 slots) and Continuous Auto-Run.

3. **[`03_combat_and_stats.md`](03_combat_and_stats.md)** — *Combat Math & Defensive Layers*
   - Strict mathematical formula: additive `Increased/Reduced` vs multiplicative `More/Less`.
   - 5 damage types: Physical, Fire, Cold, Lightning, Chaos.
   - Elemental & physical ailments: Bleed, Ignite, Shock, Chill, Freeze, Poison.
   - Defensive triad: Armour rating curve, Evasion entropy table, Energy Shield, Resistances, Block, Spell Suppression.
   - Spatial combat geometry: Hitboxes, circular AoE, cone cleave, forward line pierce.

4. **[`04_gems_and_passives.md`](04_gems_and_passives.md)** — *Decoupled Gem System & Passive Tree Web*
   - PoE 2 decoupled socket model: Gear has 0 sockets; active gems contain up to 5 auto-linked support slots.
   - Passive skill graph: Small nodes, Notables, Keystones, Masteries.
   - Attribute travel nodes (Strength, Dexterity, Intelligence).

5. **[`05_items_and_crafting.md`](05_items_and_crafting.md)** — *Items, Currency & Pure Crafting*
   - Affix model: Prefix and Suffix pools, item levels, mod tiers.
   - Zero-Gold economy: Pure currency orbs (Transmute, Alteration, Chaos, Exalt, Annul, Divine, Vaal).
   - Abyssal Fossil crafting: Weighted modifier biases and blocked mod groups.
   - Loot filter integration & Affinity Stash tabs.

6. **[`07_combat_ui_and_viewport.md`](07_combat_ui_and_viewport.md)** — *Combat Viewport & Layout Architecture*
   - Dynamic companion form factor (`640 x 320` px Compact to `640 x 780` px Expanded).
   - Persistent bottom-anchored 2D arena viewport with upward drawer expansion (`keepBottom = true`).
   - Look-ahead camera with forward buffer (+80px).
   - Floating Combat Text (FCT) and tier 1 drop beams with Ting sound.

7. **[`08_first_time_user_experience.md`](08_first_time_user_experience.md)** — *FTUE & Progressive Disclosure*
   - 1-Click Starter Archetype Pick: Juggernaut (Str), Elementalist (Int), Deadeye (Dex).
   - First 10 minutes onboarding flow and initial map activation.
   - Implementation plan: [`../technical/FTUE_IMPLEMENTATION_PLAN.md`](../technical/FTUE_IMPLEMENTATION_PLAN.md).

8. **[`09_weapon_equipment_system.md`](09_weapon_equipment_system.md)** — *Weapon System & Equipment Mechanics*
   - Dual-wielding (+10% More APS, +15% Block) and two-handed / bow configurations.
   - Local modifiers (weapon base changes) vs Global modifiers.
   - Physical trinity & DPS calculation.
   - Implementation plan: [`../technical/WEAPON_SYSTEM_IMPLEMENTATION_PLAN.md`](../technical/WEAPON_SYSTEM_IMPLEMENTATION_PLAN.md).

9. **[`10_starter_skills_design.md`](10_starter_skills_design.md)** — *Starter Skill Gems Design*
   - Cleave (Melee Physical), Fireball (Spell Fire Proliferation), Split Arrow (Bow Physical/Pierce).
   - Targeting logic, attack/cast animations, and gem tags.

10. **[`11_equipment_system_design.md`](11_equipment_system_design.md)** — *10-Slot Equipment Matrix*
    - Helmet, Body Armour, Gloves, Boots, Main Hand, Off Hand, Amulet, Belt, Ring 1, Ring 2.
    - Base defense archetypes: Pure Armour, Pure Evasion, Pure Energy Shield, and Hybrids.
    - Soft attribute requirements and smart tooltip comparison engine.

11. **[`13_build_archetypes_specification.md`](13_build_archetypes_specification.md)** — *7 Iconic Build Archetypes*
    - Bleed Slammer, Ignite Prolif, Crit Projectile, Poison Claw, Walking Calamity (RF), Archmage Storm, Dread Necromancer.
    - Implementation plan: [`../technical/BUILD_ARCHETYPES_IMPLEMENTATION_PLAN.md`](../technical/BUILD_ARCHETYPES_IMPLEMENTATION_PLAN.md).

12. **[`14_unique_items_and_niche_builds.md`](14_unique_items_and_niche_builds.md)** — *Unique Items & Build Enablers*
    - Philosophy of uniques: Build-enabling mechanics with trade-offs / downsides.
    - 8 iconic unique items supporting niche archetypes.

13. **[`15_game_hub_and_sanctuary.md`](15_game_hub_and_sanctuary.md)** — *The Crucible Sanctuary & Real-Time DPS Meter*
    - Dual-state viewport: Expedition Combat vs Sanctuary Idle (meditation at X=240, Y=86).
    - Hub management panels: Crucible Anvil (RNG slamming), Thaumaturgic Smelter (dust economy), Abyssal Resonator, Pinnacle Relics.
    - Real-time in-combat sliding window (3.0s) DPS meter.
