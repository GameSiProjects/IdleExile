# Idle Exile — Documentation Index & Agent Navigation Map

Welcome to the **Idle Exile** documentation directory. All documentation has been organized into two structured subdirectories to enable fast, unambiguous retrieval for AI agents and human developers alike:

* **[`design/`](design/README.md)**: Game Design Documentation (GDD) — Mechanics, gameplay loops, balance mathematics, economy, items, passive tree, and UX wireframes.
* **[`technical/`](technical/README.md)**: Technical Architecture & Execution Plans — Godot 4 .NET architecture, C# core engine specs, implementation plans, and execution roadmaps.

---

## 🗂 Directory Architecture

```
docs/
├── README.md                                  # This master navigation index
│
├── design/                                    # GAME DESIGN SPECIFICATIONS (GDD)
│   ├── README.md                              # Design section index & quick-reference
│   ├── 01_game_overview.md                    # Philosophy, core loop, desktop companion pillars
│   ├── 02_mapping_system.md                   # Map Device, tiers T1-T16+, Endless Abyss (20%)
│   ├── 03_combat_and_stats.md                 # Damage formulas (more vs inc), ailments, defenses
│   ├── 04_gems_and_passives.md                # Decoupled gem model (PoE 2), passive skill tree
│   ├── 05_items_and_crafting.md               # Affixes, currency economy, pure RNG slamming
│   ├── 07_combat_ui_and_viewport.md           # 2D arena viewport, layout allocations, FCT
│   ├── 08_first_time_user_experience.md       # Onboarding, progressive disclosure, starter flow
│   ├── 09_weapon_equipment_system.md          # Weapon classes, local vs global mods, DPS math
│   ├── 10_starter_skills_design.md            # Cleave, Fireball, Split Arrow targeting & mechanics
│   ├── 11_equipment_system_design.md          # 10-slot budget, item bases, unique item theory
│   ├── 13_build_archetypes_specification.md   # 7 iconic build archetypes & meta theory
│   ├── 14_unique_items_and_niche_builds.md    # Build-enabling unique mechanics & downsides
│   └── 15_game_hub_and_sanctuary.md           # The Crucible Sanctuary, Smelter & DPS meter
│
└── technical/                                 # TECHNICAL ARCHITECTURE & PLANS
    ├── README.md                              # Technical section index & execution guide
    ├── 06_godot_technical_spec.md             # Godot 4 C# architecture, window docking, low CPU
    ├── 12_master_implementation_roadmap.md    # Strategic phased roadmap & milestone checklist
    ├── BUILD_ARCHETYPES_IMPLEMENTATION_PLAN.md # 7 build archetypes & gem pipeline plan
    ├── DUAL_PROGRESSION_AND_CRAFTING_PLAN.md   # Endless Abyss & fossil crafting plan
    ├── FTUE_IMPLEMENTATION_PLAN.md            # 1-click starter archetype & onboarding plan
    └── WEAPON_SYSTEM_IMPLEMENTATION_PLAN.md   # Weapon models & calculation engine plan
```

---

## ⚡ Quick Navigation for Agents

When implementing features or debugging, use this cheat sheet to quickly find the authoritative document:

| Need Information About... | Category | Authoritative Document |
| :--- | :---: | :--- |
| **High Concept, Zero APM, Core Pillars** | Design | [`design/01_game_overview.md`](design/01_game_overview.md) |
| **Mapping Engine, Tiers, Endless Abyss, Map Queue** | Design | [`design/02_mapping_system.md`](design/02_mapping_system.md) |
| **Damage Formula (`More` vs `Increased`), Ailments, Defenses** | Design | [`design/03_combat_and_stats.md`](design/03_combat_and_stats.md) |
| **Decoupled Gems (PoE 2), Passive Tree, Keystones** | Design | [`design/04_gems_and_passives.md`](design/04_gems_and_passives.md) |
| **Currency Orbs, Prefix/Suffix Rules, Fossils** | Design | [`design/05_items_and_crafting.md`](design/05_items_and_crafting.md) |
| **Mini-Window Viewport Layout, FCT, Edge Glows** | Design | [`design/07_combat_ui_and_viewport.md`](design/07_combat_ui_and_viewport.md) |
| **First-Time User Experience (FTUE), Progressive Disclosure** | Design | [`design/08_first_time_user_experience.md`](design/08_first_time_user_experience.md) |
| **Weapon Classes, Local vs Global Mods, DPS Math** | Design | [`design/09_weapon_equipment_system.md`](design/09_weapon_equipment_system.md) |
| **Starter Skills (Cleave, Fireball, Split Arrow)** | Design | [`design/10_starter_skills_design.md`](design/10_starter_skills_design.md) |
| **10-Slot Equipment Identity, Armour Archetypes** | Design | [`design/11_equipment_system_design.md`](design/11_equipment_system_design.md) |
| **7 Build Archetypes, Offense/Defense Scaling** | Design | [`design/13_build_archetypes_specification.md`](design/13_build_archetypes_specification.md) |
| **Unique Items, Build-Enabling Downsides** | Design | [`design/14_unique_items_and_niche_builds.md`](design/14_unique_items_and_niche_builds.md) |
| **The Crucible Sanctuary Hub, Smelter, Real-Time DPS Meter** | Design | [`design/15_game_hub_and_sanctuary.md`](design/15_game_hub_and_sanctuary.md) |
| **Godot 4 Architecture, Pure C# Core, JSON Data Pipeline** | Technical | [`technical/06_godot_technical_spec.md`](technical/06_godot_technical_spec.md) |
| **Master Phased Implementation Roadmap & Milestones** | Technical | [`technical/12_master_implementation_roadmap.md`](technical/12_master_implementation_roadmap.md) |
| **Build Archetypes Implementation Plan** | Technical | [`technical/BUILD_ARCHETYPES_IMPLEMENTATION_PLAN.md`](technical/BUILD_ARCHETYPES_IMPLEMENTATION_PLAN.md) |
| **Dual Progression (Abyss & Fossil) Implementation Plan** | Technical | [`technical/DUAL_PROGRESSION_AND_CRAFTING_PLAN.md`](technical/DUAL_PROGRESSION_AND_CRAFTING_PLAN.md) |
| **FTUE Onboarding Implementation Plan** | Technical | [`technical/FTUE_IMPLEMENTATION_PLAN.md`](technical/FTUE_IMPLEMENTATION_PLAN.md) |
| **Weapon System Implementation Plan** | Technical | [`technical/WEAPON_SYSTEM_IMPLEMENTATION_PLAN.md`](technical/WEAPON_SYSTEM_IMPLEMENTATION_PLAN.md) |

---

## 📌 Non-Negotiable Architecture Rules (Reminder for Agents)

1. **Separation of Concerns:** Code under `src/Core/` must NEVER inherit from or reference `Godot.Node`. Pure C# logic only.
2. **Data-Driven:** Game content and balance values must reside in `data/*.json`, not hardcoded in C#.
3. **No Campaign / No Acts:** Progression starts directly in the Map Device at Level 1 (*Immediate Mapping*).
4. **Decoupled Gem System:** Equipment has zero sockets. Active gems contain up to 5 automatically linked support slots.
5. **No Gold:** The economy runs strictly on Path of Exile crafting currencies (*Scroll of Wisdom, Transmute, Alteration, Chaos, Exalt, Divine, etc.*).
6. **PoE Mathematical Precision:** Never conflate additive `Increased/Reduced` with multiplicative `More/Less`.
