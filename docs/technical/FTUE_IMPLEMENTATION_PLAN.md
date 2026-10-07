# FTUE_IMPLEMENTATION_PLAN.md — Kế Hoạch Triển Khai Kỹ Thuật FTUE

Tài liệu này chi tiết hóa lộ trình kỹ thuật và phân rã công việc (Work Breakdown Structure - WBS) để hiện thực hóa luồng trải nghiệm người chơi ban đầu (**First Time User Experience - FTUE**) theo đặc tả tại [08_first_time_user_experience.md](../design/08_first_time_user_experience.md).

---

## 1. Nguyên Tắc Kiến Trúc & Phân Tách Trách Nhiệm (Separation of Concerns)

Theo quy định bất di bất dịch tại `AGENTS.md`:
* **`src/Core/` (Pure C#):**
  * Quản lý trạng thái tiến trình FTUE (`FtueStep`, `FtueState`).
  * Định nghĩa mẫu khuôn nhân vật khởi đầu (`StarterArchetypeDefinition`).
  * Xử lý logic rơi đồ định sẵn (Seeded Drops: Quái thứ 12 rớt Currency, quái Magic rớt Support Gem).
  * Không phụ thuộc vào `Godot.Node`, có thể kiểm thử tự động 100% bằng Unit Tests.
* **`src/UI/` (Godot 4 Nodes & Canvas):**
  * Giao diện chọn Archetype 1-click (`StarterSelectionModal.cs`).
  * Hệ thống hiệu ứng dẫn đường không chặn tương tác (`UiGuidanceManager.cs` - Golden Pulse Tween & Micro-Tooltips).
  * Trình khóa/mở giao diện lũy tiến (`FeatureGatekeeper.cs` - Khóa tab theo Level).
  * Phát âm thanh xúc giác (*"Ting!"*, *"Snap!"*, *"Level Up!"*).

---

## 2. Phân Rã Công Việc Chi Tiết (WBS & Technical Tasks)

```
┌────────────────────────────────────────────────────────────────────────┐
│ Phase 1: Core Logic & FTUE State Machine (Pure C#) — [COMPLETED]       │
│   ├── [x] Task 1.1: Starter Archetype Models & Initial Kits            │
│   ├── [x] Task 1.2: FTUE State Tracker & Step Transitions              │
│   └── [x] Task 1.3: Deterministic Seeded Drop Rules                    │
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│ Phase 2: Archetype Selection UI & Initial Bootstrap (Godot 4)          │
│   ├── [x] Task 2.1: StarterSelectionModal Scene & Layout               │
│   ├── [x] Task 2.2: 1-Click Archetype Dispatch & PlayerState Init      │
│   └── Task 2.3: First-boot Auto-loading of Tier 1 Strand Map           │
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│ Phase 3: Contextual Guidance & Audio Feedback                          │
│   ├── Task 3.1: UiGuidanceManager (Golden Pulse & Micro-Tooltips)      │
│   ├── Task 3.2: Sound Effect Manager & Dopamine Drop Audio             │
│   └── Task 3.3: Combat Arena Visual Mutation on Gem Socketing          │
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│ Phase 4: Progressive Disclosure & Feature Gatekeeper                   │
│   ├── Task 4.1: FeatureGatekeeper Component (Tab Unlocking by Level)   │
│   ├── Task 4.2: Passive Tree Fog of War / Focus Camera Mode            │
│   └── Task 4.3: Low-Level Death Protection & Tier 0 Fallback Handling  │
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│ Phase 5: Testing, Headless CI & Validation                             │
│   ├── Task 5.1: Unit Test Suite for FTUE Step Transitions              │
│   └── Task 5.2: Full Headless Simulation Verification                  │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Lộ Trình Triển Khai Từng Giai Đoạn (Implementation Details)

### Phase 1: Core Logic & FTUE State Machine (Pure C#)

#### [x] Task 1.1: Starter Archetype Models & Initial Kits
* **Tạo file:** `src/Core/Models/StarterArchetypeDefinition.cs` (Đã hoàn thành)
* **Nội dung:**
  * Enum: `StarterArchetype { Juggernaut, Elementalist, Deadeye }`.
  * Record: `StarterArchetypeDefinition` bao gồm:
    * `StarterArchetype Id`
    * `string DisplayName`, `string Subtitle`, `string Description`
    * `string PrimaryAttribute`, `string ColorHex`
    * `string ActiveGemId` (`"cleave"`, `"fireball"`, `"split_arrow"`)
    * `string GuaranteedSupportGemId` (`"added_fire_damage"`, `"ignite_proliferation"`, `"multiple_projectiles"`)
    * `float BaseMaxLife`, `float BaseMaxEnergyShield`, `float BaseMaxMana`
    * `DefensiveStats BaseDefenses` (Armour, Evasion, Block, Resistances)
    * `IReadOnlyList<string> InitialInventory`, `IReadOnlyDictionary<string, int> InitialCurrencies`, `IReadOnlyList<string> InitialMaps`
  * Static catalog: `StarterArchetypeCatalog` (`All`, `Get()`, `TryGet()`).
  * Unit test suite: `src/Core/Tests/StarterArchetypeTests.cs` (Đã pass 100%).

#### [x] Task 1.2: FTUE State Tracker & Step Transitions
* **Tạo file:** `src/Core/Simulation/FtueManager.cs` (Đã hoàn thành)
* **Nội dung:**
  * Quản lý các bước trong enum `FtueStep`:
    * `AwaitingArchetypeSelect` (0) $\rightarrow$ Chưa chọn nhân vật.
    * `AwaitingFirstMapActivate` (1) $\rightarrow$ Đã có Map T1 trong khay, đợi nhấn nút kích hoạt.
    * `FirstCombatWave` (2) $\rightarrow$ Đang đánh quái đợt đầu.
    * `FirstLootDropped` (3) $\rightarrow$ Quái thứ 12 rớt đồ, cần thông báo Loot.
    * `FirstLevelUpReached` (4) $\rightarrow$ Đạt Lv 2, cần mở Passive Tree.
    * `SupportGemAcquired` (5) $\rightarrow$ Quái Magic rớt Support Gem, cần hướng dẫn ghép gem.
    * `MapBossEncounter` (6) $\rightarrow$ Boss T1 xuất hiện.
    * `FtueCompleted` (7) $\rightarrow$ Đã hoàn thành Map đầu tiên, mở tính năng Companion & Auto-Map.
  * Cung cấp C# Events: `OnFtueStepChanged(FtueStep)`, `OnFtueTransition(from, to)`, `OnArchetypeSelected(def)`, `OnSupportGemSocketed()`.
  * Hỗ trợ chỉ dẫn ngữ cảnh: `GetCurrentInstruction()` trả về gợi ý ngắn cho UI guidance.
  * Unit test suite: `src/Core/Tests/FtueManagerTests.cs` (Đã pass 100%).

#### [x] Task 1.3: Deterministic Seeded Drop Rules
* **Tạo file:** `src/Core/Models/LootDrop.cs`, `src/Core/Simulation/SeededDropCoordinator.cs` (Đã hoàn thành)
* **Cơ chế triển khai:**
  * **Model LootDrop:** Lưu trữ `Id`, `DisplayName`, `Type` (Currency, Equipment, Gem, Map), `Rarity` (Normal, Magic, Rare, Unique), `Quantity`, `ColorHex`, `IsSeeded`.
  * **Quái vật thứ 12 trong Map đầu tiên:** Đảm bảo rơi 1x Magic Equipment phù hợp với Archetype (Sword / Wand / Bow) + 1x `Orb of Transmutation`, kích hoạt chuyển bước FTUE `FirstLootDropped`.
  * **Đợt quái Magic đầu tiên:** Đảm bảo rơi 1x Support Gem tương thích (`added_fire_damage`, `ignite_proliferation`, hoặc `multiple_projectiles`), kích hoạt bước `SupportGemAcquired`.
  * **Map Boss (Tidal Behemoth):** Đảm bảo rơi 2x Map Slates (`1x Strand T1`, `1x Arid Lake T2`), 1x `Chaos Orb`, 1x Rare Armor (`gilded_cuirass`), kích hoạt bước `FtueCompleted`.
  * **Fail-Safe Mechanism (Endless Hollow - T0):** Mỗi 50 quái vật bị hạ gục đảm bảo rớt ít nhất 1x Tier 1 Map Slate (`strand`).
  * **Event System:** Phát `OnLootDropped` và `OnLootBatchDropped` phục vụ UI Loot Toast và Drop Beams.
  * **Unit test suite:** `src/Core/Tests/SeededDropTests.cs` (Đã pass 100%).

---

### Phase 2: Archetype Selection UI & Initial Bootstrap (Godot 4)

#### [x] Task 2.1: StarterSelectionModal Scene & Layout
* **Tạo file:** `scenes/StarterSelectionModal.tscn`, `src/UI/StarterSelectionModal.cs` (Đã hoàn thành)
* **Bố cục (440 x 780 px):**
  * Nền tối `Color(0.035, 0.035, 0.051, 0.96)` che phủ toàn bộ viewport.
  * Tiêu đề: *"THE CRUCIBLE • THỨC TỈNH"* và *"CHỌN BẢN THỂ KHỞI NGUYÊN"*.
  * 3 thẻ lớn dọc (Cards) tương ứng:
    * **Juggernaut** (Viền đỏ - Strength): Icon `STR`, kỹ năng Cleave, máu 120, giáp 25, tặng Added Fire Damage.
    * **Elementalist** (Viền lam - Intelligence): Icon `INT`, kỹ năng Fireball, máu 90, ES 45, kháng hỏa +10%, tặng Ignite Proliferation.
    * **Deadeye** (Viền lục - Dexterity): Icon `DEX`, kỹ năng Split Arrow, máu 105, né tránh 25%, tặng Lesser Multiple Projectiles.
  * Hiệu ứng chuyển động mượt mà:
    * Fade-in khi xuất hiện (Alpha 0 $\rightarrow$ 1 trong 0.25s).
    * Hover feedback: Phóng to nhẹ 1.02x card khi trỏ chuột vào button.
    * Fade-out mượt khi người chơi chọn (Alpha 1 $\rightarrow$ 0 trong 0.22s) và phát tín hiệu `OnArchetypeSelected`.
  * Unit test suite: `StarterSelectionModalTests` (Đã pass 100%).

#### [x] Task 2.2: 1-Click Archetype Dispatch & PlayerState Init
* **Tích hợp:** `src/UI/Main.cs`, `src/Core/State/PlayerState.cs`, `src/UI/StarterSelectionModal.cs` (Đã hoàn thành)
* **Quy trình hoạt động:**
  * Khi mở game lần đầu (`Ftue.CurrentStep == AwaitingArchetypeSelect`), `Main.cs` tự động khởi tạo và hiển thị `StarterSelectionModal.tscn` đè lên toàn bộ cửa sổ mini.
  * Người chơi click 1 thẻ Archetype:
    * Modal ghi nhận, vô hiệu hóa các nút còn lại để chống spam click, phát âm thanh chọn và hiệu ứng fade-out mượt (0.25s) trước khi `QueueFree()`.
    * `Main.cs` nhận sự kiện `OnStarterArchetypeChosen`:
      1. Gọi `Ftue.SelectArchetype(archetype)` $\rightarrow$ Chuyển trạng thái FTUE sang `AwaitingFirstMapActivate`.
      2. Gọi `Player.BootstrapFromArchetype(archetype)` $\rightarrow$ Thiết lập Life/ES/Mana, nạp 3x `Tier 1: Strand Map`, 3x `Scroll of Wisdom`, 1x `Orb of Transmutation` vào kho đồ.
      3. Cập nhật thanh sinh mệnh [VitometerView](file:///e:/Projects/Antigravity/IdleExile/src/UI/Components/VitometerView.cs) với các thông số EHP/Armour/Evasion thực tế.
      4. Thiết lập bản đồ Strand Map Tier 1 trên [MapHeaderBar](file:///e:/Projects/Antigravity/IdleExile/src/UI/Components/MapHeaderBar.cs) sẵn sàng kích hoạt.
  * **Unit test suite:** `StarterSelectionModalTests.TestTask22PlayerStateBootstrapIntegration` (Đã pass 100%).

#### [x] Task 2.3: First-boot Auto-loading of Tier 1 Strand Map
* **Tạo file / Tích hợp:** `scenes/MapDeviceBar.tscn`, `src/UI/Components/MapDeviceBar.cs`, `src/UI/Main.cs` (Đã hoàn thành)
* **Cơ chế triển khai:**
  * **MapDeviceBar Control Component:** Khay điều khiển Map Device mini (416 x 68 px) gồm:
    * Huy hiệu Tier (`T1`), tên bản đồ `Strand Map` với màu Rarity tương ứng, và thông số bonus (`Qty: +10% | Mobs: 100%`).
    * Nút chính **[⚔ ACTIVATE MAP]**: kích hoạt mở cổng bản đồ.
    * Nút phụ **[ESCAPE (Space)]**: Cổng khẩn cấp (Emergency Portal) cho phép rút lui về hideout/Crucible an toàn.
  * **Auto-loading:** Ngay sau khi người chơi chọn Starter Archetype ở Modal, `Main.cs` tự động lấy `Tier 1: Strand Map` nạp vào khay `MapDeviceBar.SlotMap(mapDef, "Normal")`.
  * **Golden Pulse Guidance:** Kích hoạt hiệu ứng Tween phát sáng viền vàng nhịp nhàng (`SetGoldenPulse(true)`) trên nút `[ACTIVATE MAP]` để dẫn dắt người chơi vào thao tác tiếp theo mà không làm nghẽn UI.
  * **Dispatch & Combat Transition:**
    * Khi click `[ACTIVATE MAP]`, tắt Golden Pulse, đổi trạng thái sang `PORTAL ACTIVE`.
    * Chuyển trạng thái FTUE: `Ftue.ActivateFirstMap()` đưa FTUE từ `AwaitingFirstMapActivate` (1) sang `FirstCombatWave` (2).
    * `_battleViewport` tải cấu hình Strand Map và tự động gọi `WaveSpawner.SpawnNextWave()` để sinh đợt quái đầu tiên trên bãi biển, đồng thời phát động đòn đánh đầu tiên của Exile phù hợp với Archetype.
  * **Unit test suite:** `MapDeviceBarTests` (`TestSceneInstantiationAndDefaults`, `TestSlotMapAndGoldenPulse`, `TestActivationDispatch`, `TestTask23FtueMapActivationIntegration` — Đã pass 100%).

---

### Phase 3: Contextual Guidance & Audio Feedback

#### Task 3.1: UiGuidanceManager (Golden Pulse & Micro-Tooltips)
* **Tạo file:** `src/UI/Components/UiGuidanceManager.cs`
* **Chức năng:**
  * `HighlightElement(Control targetNode, string tooltipText)`:
    * Tạo một Tween dao động Alpha từ 0.3 đến 1.0 trên viền vàng bao quanh `targetNode`.
    * Hiển thị một Micro-Tooltip nhỏ (font 11px) phía trên targetNode với nội dung hướng dẫn ngắn.
  * `ClearHighlight()`: Gỡ bỏ hiệu ứng ngay khi người chơi thực hiện thao tác tương tác.

#### Task 3.2: Sound Effect Manager & Dopamine Drops
* **Tạo file:** `src/UI/AudioManager.cs`
* **Âm thanh trọng điểm:**
  * `Sfx_LootTing`: Tiếng kim loại đanh thép khi rớt đồ/currency (chuẩn FilterBlade sound).
  * `Sfx_GemSocket`: Tiếng click đanh gọn khi gắn Support Gem vào Active Gem.
  * `Sfx_LevelUp`: Tiếng vang bừng sáng khi lên cấp.
  * `Sfx_PortalOpen`: Tiếng hum rền vang khi mở cổng map.

#### Task 3.3: Combat Arena Visual Mutation on Gem Socketing
* Kết nối sự kiện `SkillLoadout.OnSupportGemLinked`:
  * Nếu gắn `Added Fire Damage`: Tăng hiệu ứng lửa trên vệt chém [Arena2D](file:///e:/Projects/Antigravity/IdleExile/src/UI/Components/Arena2D.cs).
  * Nếu gắn `Multiple Projectiles`: Số đạn bay của nhân vật từ 1 tăng thành 3 đạn tỏa hình quạt.

---

### Phase 4: Progressive Disclosure & Feature Gatekeeper

#### Task 4.1: FeatureGatekeeper Component (Tab Unlocking by Level)
* **Tạo file:** `src/UI/FeatureGatekeeper.cs`
* **Quản lý trạng thái các Tab:**
  * `Level 1`: Mở Arena + Map Device + Vitals. Các tab khác mờ đi (Disabled), hiển thị icon ổ khóa.
  * `Level 2`: Mở tab **Passive Tree**.
  * `Level 3`: Mở tab **Inventory / Crafting**.
  * `Level 4`: Mở tab **Skills Panel**.
  * `Level 8`: Mở tab **Loot Filter**.
  * `Level 10`: Mở tab **Atlas Tree**.

#### Task 4.2: Passive Tree Fog of War / Focus Camera Mode
* Khi mở Passive Tree ở Lv 2:
  * Camera tự động focus vào tọa độ node gốc của Class đã chọn.
  * Che mờ (Dimmed 80%) các nhánh ngoài phạm vi 2 bước nhảy, chỉ làm nổi bật 3 node xuất phát đầu tiên.

#### Task 4.3: Low-Level Death Protection & Tier 0 Fallback Handling
* Trong [DeathHandler.cs](file:///e:/Projects/Antigravity/IdleExile/src/Core/Simulation/DeathHandler.cs):
  * Kiểm tra `if (player.Level <= 5)`: Bỏ qua bước trừ 10% EXP.
  * Tự động hồi sinh sau 2s kèm khiên bất tử tạm thời 3s.
* Trong Map Device:
  * Nếu số lượng Map Slates trong Inventory và Stash = 0: Nút `The Endless Hollow (Tier 0)` tự động bật sáng.

---

### Phase 5: Testing, Headless CI & Validation

#### Task 5.1: Unit Test Suite for FTUE Step Transitions
* **Tạo file:** `tests/FtueTests.cs` (hoặc `src/Core/Tests/FtueTests.cs`)
* **Kiểm thử logic thuần:**
  * Chọn Juggernaut $\rightarrow$ Kiểm tra Inventory có đủ 3x Strand Map, 1x Cleave gem.
  * Quái #12 chết $\rightarrow$ Kiểm tra Drop event có đúng 1x Transmute và 1x Magic Sword.
  * Level 2 đạt được $\rightarrow$ Trạng thái chuyển sang `FirstLevelUpReached`.
  * Support Gem được gắn $\rightarrow$ DPS tính toán trong DamageCalculator tăng đúng hệ số.

#### Task 5.2: Full Headless Simulation Verification
* Chạy Godot Headless console kiểm tra:
  ```powershell
  & "E:\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe" --headless --scene "res://scenes/Main.tscn" --quit-after 100
  ```
* Kỳ vọng: Hoàn thành 100 frame khởi động ban đầu với 0 lỗi, 0 exception.

---

## 4. Bảng Ước Tính Tiến Độ & Thứ Tự Thực Thi

| Giai Đoạn | Nhiệm Vụ | Thời Gian Ước Tính | Phụ Thuộc (Dependencies) |
| :--- | :--- | :---: | :--- |
| **Phase 1** | Pure C# FTUE State & Starter Archetypes | 2 giờ | Đã có `DatabaseLoader`, `PlayerState` |
| **Phase 2** | UI Modal Chọn Archetype 1-Click | 2 giờ | Phase 1 |
| **Phase 3** | Guidance Manager (Golden Pulse) & SFX Hook | 2.5 giờ | Phase 2, `Arena2D` |
| **Phase 4** | Feature Gatekeeper & Mở Khóa Tab Lũy Tiến | 2 giờ | Phase 1, `Main.tscn` |
| **Phase 5** | Unit Tests & Headless CI Verification | 1.5 giờ | Toàn bộ các Phase trên |
| **Tổng Cộng** | **Hoàn thiện trọn vẹn FTUE** | **~10 giờ làm việc** | |

---

## 5. Kết Nối Vào Kế Hoạch Chung (`TASKS.md`)

Kế hoạch này sẽ được bổ sung vào `TASKS.md` dưới dạng **Phase 2.8: First Time User Experience (FTUE) Implementation**, thực thi ngay sau khi hoàn thiện [CombatEngine.cs](file:///e:/Projects/Antigravity/IdleExile/src/Core/Simulation/CombatEngine.cs) (Task 1.5) và trước khi triển khai hệ thống Loot Filter toàn diện (Phase 3).
