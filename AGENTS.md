# AGENTS.md — Agent Development & Architecture Guide

Welcome, Agent! This file outlines the architectural principles, tech stack, conventions, and operational guidelines for developing **Idle Exile**. Read this document carefully before making changes or proposing new features.

---

## 1. Project Overview & Philosophy

* **Project Name:** Idle Exile
* **Genre:** Minimalist 2D Idle/Incremental ARPG inspired by *Path of Exile (PoE)*.
* **Form Factor:** Mini-Window Desktop Companion (`440 x 780` px viewport), runs unobtrusively in a corner while the user works or games.
* **Core Philosophy:** *"Zero APM, 100% Theorycrafting"* — Player does not micro-manage combat; all depth lies in build construction, socketed support gems, passive tree pathing, and gear crafting.

### Non-Negotiable Game Design Rules
1. **No Campaign / No Acts:** Never introduce Act 1–10 story quests. Progression starts directly in the **Map Device** from Level 1 (*Immediate Mapping*).
2. **Decoupled Gem System (PoE 2 Model):** Equipment has **NO sockets**. Active Gems themselves contain support slots (up to 5 support slots). Support gems are 100% automatically linked (no *Orb of Fusing*).
3. **No Gold:** Economy is strictly currency-driven (*Scroll of Wisdom, Transmute, Alteration, Chaos, Exalt, Divine, etc.*).
4. **PoE Mathematical Precision:** Strictly distinguish between additive `Increased/Reduced` and multiplicative `More/Less`. Never conflate them.

---

## 2. Technology Stack & Environment

* **Engine:** Godot 4.7.2+ Mono (.NET / C# edition).
* **Language & Runtime:** C# 12 / .NET 8 (nullable reference types enabled).
* **Data Format:** JSON (`System.Text.Json`) located in `data/`.
* **Rendering Method:** `gl_compatibility` (ultra-low memory footprint and instant startup).
* **Local Godot Executable:**
  * GUI: `E:\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe`
  * Console / Headless: `E:\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe`

---

## 3. Directory Layout

```
IdleExile/
├── AGENTS.md                         # This guide
├── project.godot                     # Godot engine config (Window, low processor mode, gl_compat)
├── IdleExile.csproj                  # .NET 8 C# project config
├── nuget.config                      # Local Godot NuGet package source configuration
├── icon.svg                          # Dark fantasy portal icon
│
├── data/                             # SOURCE OF TRUTH for all balance & content
│   ├── affixes.json                  # Prefixes & Suffixes definitions and tier roll ranges
│   ├── gems.json                     # Active & Support Gems, tags, stat multipliers
│   └── maps.json                     # Map tiers (T0-T16+) and Map Modifiers
│
├── docs/                             # Complete Game Design Documentation (GDD)
│   ├── README.md                     # Documentation index
│   ├── 01_game_overview.md           # Pillars, loop, minimalist lore
│   ├── 02_mapping_system.md          # Immediate mapping, itemized maps, atlas
│   ├── 03_combat_and_stats.md        # Damage formulas, defensive layers, ticks
│   ├── 04_gems_and_passives.md       # PoE 2 gem sockets, skill matrix, passive web
│   ├── 05_items_and_crafting.md      # Affixes, currency economy, loot filter
│   └── 06_godot_technical_spec.md   # C# architecture, window profile, offline math
│
├── scenes/                           # Godot scene files (.tscn)
│   └── Main.tscn                     # Main companion UI layout
│
└── src/
    ├── Core/                         # PURE C# LOGIC (No Godot Node dependencies!)
    │   ├── Models/                   # Strongly-typed records and enums
    │   │   ├── AffixDefinition.cs
    │   │   ├── GemDefinition.cs
    │   │   └── MapDefinition.cs
    │   ├── Services/                 # Business logic, calculations, database loading
    │   │   └── DatabaseLoader.cs
    │   ├── Simulation/               # Tick engine, combat, offline progress calculation
    │   └── State/                    # Player character state, inventory, atlas progress
    │
    └── UI/                           # Godot Node C# Scripts (Views, Tooltips, Controls)
        └── Main.cs                   # Top-level window & scene controller
```

---

## 4. Architectural Rules for Agents

### 4.1. Separation of Concerns (Crucial!)
* **`src/Core/` MUST NOT inherit from or depend on `Godot.Node`.**
  * Core models, simulation logic, loot tables, and math must remain pure C# classes/records.
  * This allows headless simulation, unit testing, and instant statistical offline calculations without touching UI or scene trees.
* **`src/UI/` handles presentation only:**
  * Subscribes to events/signals from `src/Core/`.
  * Renders 2D battle representation, floating text, tooltips, and tab views.

### 4.2. Data-Driven Development (No Hardcoding)
* **Never hardcode game stats or balance values in C# code.**
* Always store them in `data/*.json`.
* When adding new affixes, gems, passives, or maps:
  1. Add entry to corresponding file in `data/`.
  2. Verify or extend C# models in `src/Core/Models/`.
  3. Update `DatabaseLoader.cs` if new collections are introduced.

### 4.3. C# Coding Conventions
* Use **file-scoped namespaces**: `namespace IdleExile.Core.Models;`
* Use **`record`** for immutable data structures (especially parsed JSON data).
* Use explicit nullable annotations (`string?`, `BossName?`).
* Leverage **LINQ** for querying collections (e.g., Loot Filter evaluation, gem tag matching).
* Maintain clean naming: PascalCase for public members/methods, `_camelCase` for private fields.

---

## 5. Development & Build Commands

When making changes to C# or Godot files, always test using the following commands:

### Compile C# Project
```powershell
dotnet build
```
*Expected: 0 Warning(s), 0 Error(s).*

### Run Godot Headless Verification
```powershell
& "E:\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe" --headless --scene "res://scenes/Main.tscn" --quit-after 50
```
*Expected: Prints loaded database stats and exits cleanly with code 0.*

---

## 6. Current Implementation Status & Roadmap

| Feature / Subsystem | Status | Location |
| :--- | :--- | :--- |
| **GDD Documentation** | Complete | `docs/01_*.md` to `docs/12_*.md` |
| **Execution Task List** | Tracking | `TASKS.md` |
| **Project & Engine Setup** | Complete | `project.godot`, `IdleExile.csproj`, `nuget.config` |
| **JSON Data Pipeline** | Complete | `data/`, `src/Core/Models/`, `DatabaseLoader.cs` |
| **Damage Calculator (Increased vs More)** | Complete | `src/Core/Services/DamageCalculator.cs` |
| **Defensive Layers & Player State** | Complete | `src/Core/Services/DefensiveLayers.cs`, `PlayerState.cs` |
| **Monster Spawning & Pack Sizing** | Complete | `src/Core/Models/MonsterInstance.cs`, `WaveSpawner.cs` |
| **Combat Infrastructure (Task 1.4)** | Complete | `src/Core/Simulation/` (Ailments, Target, Death, Runtime) |
| **Skill Loadout & Support Linking** | Complete | `src/Core/State/SkillLoadout.cs` |
| **Combat & Tick Engine (30Hz Simulation)** | Complete | `src/Core/Simulation/CombatEngine.cs`, `TickManager.cs` |
| **Unified Combat & Attack System** | Complete | `src/Core/Models/ICombatEntity.cs`, `AttackPayload.cs`, `CombatAttackService.cs` |
| **Spatial Geometry & 2D Math (Section 4)** | Complete | `src/Core/Services/SpatialCombatService.cs`, `Vector2D.cs` |
| **Real-World Movement Controller (Task 2.5)** | Complete | `src/Core/Simulation/ExileMovementController.cs` |
| **10-Slot Equipment & Soft Attributes (Task 3.1)** | Complete | `src/Core/Models/`, `src/Core/State/EquippedGearState.cs` |
| **Smart Tooltip Comparison Engine (Task 3.4)** | Complete | `src/Core/Services/EquipmentComparisonService.cs` |
| **Mini-Window Equipment View (Task 3.4)** | Complete | `scenes/EquipmentView.tscn`, `src/UI/Components/EquipmentView.cs` |
| **Item Generator & Affix Roller** | Planned | `src/Core/Services/ItemGenerator.cs` |
| **Loot Filter System** | Planned | `src/Core/Services/LootFilterService.cs` |
| **Mini-Window 2D Battle Viewport UI** | Complete (Tasks 2.1 - 2.7) | `scenes/BattleViewport.tscn`, `src/UI/BattleViewport.cs`, `src/UI/Components/` |
| **Passive Skill Tree Graph** | Planned | `data/passive_tree.json`, `src/Core/Models/` |

---

## 7. Tone and Communication with the User

* Responses should be concise, professional, and system-oriented.
* Always refer to established GDD terms (Prefix/Suffix, ilvl, EHP, More vs Increased, Keystones).
* When proposing code changes, test build validity with `dotnet build` before finishing.
