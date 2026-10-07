# Technical Architecture & Implementation Plans — `docs/technical/`

This directory houses all Technical Architecture Specifications, Engine Blueprints, and Phased Execution Plans for **Idle Exile**.

---

## 📑 Technical Document Index

### 1. Architecture & Master Roadmap

* **[`06_godot_technical_spec.md`](06_godot_technical_spec.md)** — *Godot 4 .NET (C#) & JSON Architecture*
  - **Runtime & Stack:** Godot 4.3+ (.NET / C# 12, .NET 8 runtime) with `gl_compatibility` renderer.
  - **Decoupled Core:** `src/Core/` is 100% Pure C# with zero `Godot.Node` dependencies, enabling instant headless simulation and statistical offline benchmarking.
  - **Data Pipeline:** `System.Text.Json` hydration pattern loading `data/*.json` into strongly-typed records/classes.
  - **Window Controller:** Dynamic mini-window desktop companion (`640 x 320` px Compact to `640 x 780` px Expanded) with bottom-anchored viewport and upward drawer expansion (`keepBottom = true`).
  - **Localization:** Godot `TranslationServer` wrapper (`Loc.Tr()`, `Loc.Format()`) reading compiled RFC 4180 CSV binary resources.
  - **Performance Targets:** <1% CPU in idle/background mode, <150MB RAM footprint.

* **[`12_master_implementation_roadmap.md`](12_master_implementation_roadmap.md)** — *Master Phased Implementation Roadmap*
  - **Strategic Phased Roadmap:** Dependencies, priority ordering, milestones, and release checklists.
  - **Subsystem Breakdown:** Combat Engine, Viewport UI, 10-Slot Gear Matrix, Gem Pipeline, Endless Abyss, and Offline Progress.

---

### 2. Phased Execution Plans (Ready-to-Execute Prompts)

* **[`BUILD_ARCHETYPES_IMPLEMENTATION_PLAN.md`](BUILD_ARCHETYPES_IMPLEMENTATION_PLAN.md)** — *7 Build Archetypes Technical Execution Plan*
  - 5-phase breakdown for implementing the 7 core meta archetypes: Bleed Slammer, Ignite Prolif, Crit Projectile, Poison Claw, Walking Calamity (RF), Archmage Storm, and Dread Necromancer.
  - Active and support gem definitions, minion math reference frames, trigger mechanisms, and unit test suites.
  - *Related Design Spec:* [`../design/13_build_archetypes_specification.md`](../design/13_build_archetypes_specification.md)

* **[`DUAL_PROGRESSION_AND_CRAFTING_PLAN.md`](DUAL_PROGRESSION_AND_CRAFTING_PLAN.md)** — *Dual Progression & Streamlined Fossil Crafting Plan*
  - Dual-Axis gameplay loop: Core Mapping (80%) + The Endless Abyss (20%).
  - 10-slot automated Map Queue with continuous auto-run and fail-safe T0 sustain.
  - Endless Delve benchmark: Sulphite capacity, depth scaling formulas ($HP \propto Depth^{1.35}$, $DMG \propto Depth^{1.25}$), and exclusive fossil drops.
  - 1-Click Streamlined Fossil Crafting on the Anvil with weighted affix calculations.
  - *Related Design Specs:* [`../design/02_mapping_system.md`](../design/02_mapping_system.md), [`../design/05_items_and_crafting.md`](../design/05_items_and_crafting.md)

* **[`FTUE_IMPLEMENTATION_PLAN.md`](FTUE_IMPLEMENTATION_PLAN.md)** — *First-Time User Experience Technical Plan*
  - Phased plan for player onboarding: 1-Click Starter Archetype Pick (Juggernaut, Elementalist, Deadeye).
  - Deterministic seeded drops (first magic monster drops support gem, 12th monster drops currency).
  - Non-blocking guidance (`UiGuidanceManager`: Golden pulse & micro-tooltips) and progressive feature gatekeeping.
  - *Related Design Spec:* [`../design/08_first_time_user_experience.md`](../design/08_first_time_user_experience.md)

* **[`WEAPON_SYSTEM_IMPLEMENTATION_PLAN.md`](WEAPON_SYSTEM_IMPLEMENTATION_PLAN.md)** — *Weapon System Technical Plan*
  - Data architecture for 15+ weapon bases (`data/weapon_bases.json`) covering 1H, 2H, Bow, Wand, Dagger, Staff, Claw, and Shield types.
  - Local vs Global modifier calculation engine and cached pDPS/eDPS/Total DPS recalculation.
  - Dual-wielding mechanics (+10% More APS, +15% Block) and off-hand slot locking.
  - Integration with smart tooltip comparison engine.
  - *Related Design Spec:* [`../design/09_weapon_equipment_system.md`](../design/09_weapon_equipment_system.md)

---

## ⚡ Agent Implementation Guidelines

1. **Pure C# Core Separation:** All algorithmic, model, and simulation logic belongs in `src/Core/`. Never import `Godot` namespaces into `src/Core/`.
2. **Data-Driven Configuration:** Content definitions belong in `data/*.json`. C# classes should only define structures and hydration parsers.
3. **Strict Preprocessor Gating:** Debug telemetry, overlays, or test helpers must be gated behind `#if DEBUG` ... `#endif` and `if (!OS.IsDebugBuild()) return;`.
4. **Build Verification:** Every step or task completion must verify compilation with `dotnet build` (0 warnings, 0 errors) and headless Godot tests.
