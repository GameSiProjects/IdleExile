# AGENTS.md — Agent Development & Architecture Guide

Welcome, Agent! This file outlines the architectural principles, tech stack, conventions, and operational guidelines for developing **Idle Exile**. Read this document carefully before making changes or proposing new features.

---

## 1. Project Overview & Philosophy

* **Project Name:** Idle Exile
* **Genre:** Minimalist 2D Idle/Incremental ARPG inspired by *Path of Exile (PoE)*.
* **Form Factor:** Dynamic Resizable Desktop Companion (`640 x 320` px Compact Idle to `640 x 780` px Expanded Theorycrafting), docked flush above the taskbar with fixed bottom-anchored Combat Viewport and upward drawer expansion (`keepBottom = true`).
* **Core Philosophy:** *"Zero APM, 100% Theorycrafting"* — Player does not micro-manage combat; all depth lies in build construction, socketed support gems, passive tree pathing, and gear crafting.

### Non-Negotiable Game Design Rules
1. **No Campaign / No Acts:** Never introduce Act 1–10 story quests. Progression starts directly in the **Map Device** from Level 1 (*Immediate Mapping*).
2. **Decoupled Gem System (PoE 2 Model):** Equipment has **NO sockets**. Active Gems themselves contain support slots (up to 5 support slots). Support gems are 100% automatically linked (no *Orb of Fusing*).
3. **No Gold:** Economy is strictly currency-driven (*Scroll of Wisdom, Transmute, Alteration, Chaos, Exalt, Divine, etc.*).
4. **PoE Mathematical Precision:** Strictly distinguish between additive `Increased/Reduced` and multiplicative `More/Less`. Never conflate them.
5. **English Display & Godot Localization Standard:** All in-game text must be displayed in English by default. All UI text, tooltips, messages, and labels must be stored in `localization/translations.csv` and resolved via Godot's localization system (`Loc.Tr()` / `Loc.Format()`), allowing seamless multilingual switching without code refactoring. Pure C# core logic (`src/Core/`) must only contain domain-level English text and never depend on Godot nodes.

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
├── project.godot                     # Godot engine config (Window, low processor mode, gl_compat, i18n)
├── IdleExile.csproj                  # .NET 8 C# project config
├── nuget.config                      # Local Godot NuGet package source configuration
├── icon.svg                          # Dark fantasy portal icon
│
├── localization/                     # Godot Localization database & compiled binaries
│   ├── translations.csv              # Master translation table (id, en, vi)
│   ├── translations.en.translation   # Compiled English translation resource
│   └── translations.vi.translation   # Compiled Vietnamese translation resource
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
│   └── 06_godot_technical_spec.md   # C# architecture, window profile, offline math, i18n
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
        ├── LocalizationService.cs    # Godot TranslationServer wrapper (Loc.Tr, Loc.Format, Loc.SetLocale)
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

### 4.4. Release Build Cleanliness & Debug Code Policy (CRITICAL & NON-NEGOTIABLE)
* **Zero Debug Artifacts in Release Builds:** Debug telemetry labels, diagnostic coordinate overlays, developer cheats/shortcuts, and temporary testing hooks MUST NEVER exist or be accessible in Release builds.
* **Strict Preprocessor Gating:**
  * All debug UI elements, diagnostic fields, and telemetry methods MUST be encapsulated within `#if DEBUG` ... `#endif` directives.
  * In Godot C# presentation scripts, additionally gate runtime execution with `if (!OS.IsDebugBuild()) return;` as a secondary safety barrier:
    ```csharp
    #if DEBUG
    if (OS.IsDebugBuild())
    {
        // Debug telemetry HUD updates / overlay rendering
    }
    #endif
    ```
  * In Release configuration (`dotnet build -c Release`), all debug code, variables, and allocations are 100% stripped by the compiler, guaranteeing zero binary footprint and zero CPU/GPU overhead.
* **No Temporary Debug Hacks in Production:** Never commit hardcoded mock coordinates, simulation bypasses, or testing overrides into production simulation loops.

### 4.5. In-Game Language & Localization Architecture (Godot TranslationServer)
* **Default Language is English (`en`):** All visual labels, buttons, tooltips, modal dialogs, and verdicts MUST default to English.
* **Godot Localization Integration:**
  * Master source file is `localization/translations.csv` using RFC 4180 CSV (`id,en,vi`).
  * Godot compiles this file into `translations.en.translation` and `translations.vi.translation`, registered in `project.godot` under `[internationalization]`.
  * In presentation code (`src/UI/`), always use `Loc.Tr("KEY")` or `Loc.Format("KEY", args)` (via `IdleExile.UI.Loc`).
  * In Godot scene files (`scenes/*.tscn`), labels can use either direct English text matching translation keys or explicit translation IDs.
* **Core Separation:** `src/Core/` must NOT depend on Godot translation APIs. All core records, error strings, and data definitions must be clean, idiomatic English.

### 4.6. In-Window Shortcut Policy (Zero System-Wide OS Hooking)
* **Local Input Only:** All keyboard shortcuts (e.g. `Space` Emergency Portal, `I`/`G` Gear, `P` Passives, `M` Map Device, `S` Stash, `Tab`/`F11` Window Mode Toggle, `Esc`) MUST only be handled locally when the window is focused via Godot's `_UnhandledKeyInput`.
* **Zero System-Wide OS Hooking:** Never register OS-level global hooks (such as Win32 `RegisterHotKey`). This eliminates shortcut collisions with IDEs, terminals, and user work tools, prevents false positive antivirus flags, and strictly preserves the "Zero APM, 100% Theorycrafting" companion philosophy.

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
| **GDD Documentation** | Complete | `docs/01_*.md` to `docs/14_*.md` |
| **Execution Task List** | Tracking | `TASKS.md` |
| **Project & Engine Setup** | Complete | `project.godot`, `IdleExile.csproj`, `nuget.config` |
| **Localization & English UI Standard** | Complete | `localization/translations.csv`, `src/UI/LocalizationService.cs` |
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
| **Sequential Corridor Monster Layout (Task 2.7)** | Complete | `src/Core/Simulation/CorridorMonsterLayout.cs` |
| **10-Slot Equipment & Soft Attributes (Task 3.1)** | Complete | `src/Core/Models/`, `src/Core/State/EquippedGearState.cs` |
| **Smart Tooltip Comparison Engine (Task 3.4)** | Complete | `src/Core/Services/EquipmentComparisonService.cs` |
| **Mini-Window Equipment View (Task 3.4)** | Complete | `scenes/EquipmentView.tscn`, `src/UI/Components/EquipmentView.cs` |
| **Decoupled Gem View (UI Task 3.1)** | Complete | `scenes/GemView.tscn`, `src/UI/Components/GemView.cs` |
| **Gem Tag Matching Validator (UI Task 3.2)** | Complete | `src/Core/Services/GemCompatibilityValidator.cs`, `src/UI/Components/GemView.cs` |
| **Crucible Map Device & Atlas View (Task 4.1)** | Complete | `scenes/AtlasView.tscn`, `src/UI/Components/AtlasView.cs` |
| **Map Queue (10 Slots - Task 4.2)** | Complete | `src/Core/Simulation/MapQueueManager.cs`, `AtlasView.cs` |
| **Item Generator & Affix Roller** | Planned | `src/Core/Services/ItemGenerator.cs` |
| **Loot Filter System** | Planned | `src/Core/Services/LootFilterService.cs` |
| **Mini-Window 2D Battle Viewport UI** | Complete (Tasks 2.1 - 2.8) | `scenes/BattleViewport.tscn`, `src/UI/BattleViewport.cs`, `src/UI/Components/` |
| **Dynamic Window Controller (reference/windows)** | Complete | `src/UI/DesktopWindow.cs`, `src/UI/WindowController.cs` |
| **Boss Room Chamber & Arena Lock (Task 2.8)** | Complete | `src/UI/Components/ArenaCamera.cs`, `src/UI/Components/Arena2D.cs` |
| **Passive Skill Tree Graph (Task 5.1)** | Complete | `data/passive_tree.json`, `src/Core/Models/PassiveTreeDefinition.cs`, `src/Core/State/PassiveTreeState.cs`, `scenes/PassiveTreeView.tscn`, `src/UI/Components/PassiveTreeView.cs` |
| **Affinity Stash View (Task 5.2)** | Complete | `data/currencies.json`, `data/fossils.json`, `src/Core/State/AffinityStashState.cs`, `scenes/StashView.tscn`, `src/UI/Components/StashView.cs` |
| **Tab Micro-Animations (Task 6.1)** | Complete | `scenes/Main.tscn`, `src/UI/Main.cs`, `src/UI/MainLayoutTests.cs` |
| **Edge Glow Alerts & Ting Sound (Task 6.2)** | Complete | `src/UI/Components/EdgeGlowAlert.cs`, `src/UI/Components/DropSoundSynthesizer.cs`, `src/UI/MainLayoutTests.cs` |
| **In-Window Shortcuts (Task 6.3)** | Complete | `src/UI/Main.cs`, `src/UI/MainLayoutTests.cs`, `localization/translations.csv` |
| **Crucible Sanctuary & Hub Tiers (Prompt 1)** | Complete | `data/sanctuary_tiers.json`, `src/Core/Models/SanctuaryState.cs` |
| **In-Combat Real-Time DPS Meter (Prompt 2)** | Complete | `src/Core/Models/CombatDpsSnapshot.cs`, `src/Core/Simulation/CombatDpsTracker.cs` |
| **Thaumaturgic Smelter & Dust Economy (Prompt 3)** | Complete | `data/smelter_recipes.json`, `src/Core/Services/SmelterService.cs`, `src/Core/Models/EquipmentItem.cs` |
| **Dual-State Viewport & Combat DPS UI (Prompt 4)** | Complete | `src/UI/Components/Arena2D.cs`, `src/UI/BattleViewport.cs`, `scenes/Main.tscn` |

---

## 7. Tone and Communication with the User

* Responses should be concise, professional, and system-oriented.
* Always refer to established GDD terms (Prefix/Suffix, ilvl, EHP, More vs Increased, Keystones).
* When proposing code changes, test build validity with `dotnet build` before finishing.
