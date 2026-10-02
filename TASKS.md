# TASKS.md — Kế Hoạch Triển Khai Idle Exile (Task Execution Tracker)

Tài liệu này theo dõi tiến độ thực hiện các tính năng của **Idle Exile**, đặc biệt tập trung vào việc hiện thực hóa **Combat Engine (C# Core)**, **Giao Diện Chiến Đấu (Battle Viewport UI)** theo thiết kế tại [07_combat_ui_and_viewport.md](file:///e:/Projects/Antigravity/IdleExile/docs/07_combat_ui_and_viewport.md) và kế hoạch nâng cấp giao diện tại [UI_OVERHAUL_IMPLEMENTATION_PLAN.md](file:///e:/Projects/Antigravity/IdleExile/docs/UI_OVERHAUL_IMPLEMENTATION_PLAN.md).

---

## 📊 Tổng Quan Trạng Thái (Project Milestone Status)

* **Trạng thái chung:** Sẵn sàng bước vào giai đoạn phát triển Combat Engine & UI.
* **Quy tắc bất di bất dịch:** 
  * `src/Core/` là C# thuần (Pure C#), tuyệt đối không kế thừa `Godot.Node`.
  * `src/UI/` chỉ nhận Event/Action từ Core để cập nhật đồ họa.
  * Mọi thay đổi code đều phải vượt qua `dotnet build` với `0 Warning, 0 Error`.

---

## 🎯 Phase 1: Combat Simulation & Math Core (Pure C#)

> **Mục tiêu:** Xây dựng lõi mô phỏng chiến đấu 30 Ticks/giây (~33.33ms), không phụ thuộc Godot scene tree.

- [x] **Task 1.1: Bộ Tính Toán Sát Thương Chuẩn PoE (`DamageCalculator.cs`)**
  - [x] Hiện thực công thức: `Base * (1 + sum(Increased) - sum(Reduced)) * prod(1 + More) * prod(1 - Less)`.
  - [x] Hỗ trợ 5 loại sát thương: Physical, Fire, Cold, Lightning, Chaos.
  - [x] Xử lý đòn đánh chí mạng: `Crit Chance (0 - 100%)` và `Crit Multiplier`.
  - [x] Tính toán hiệu ứng trạng thái (Ailments): Ignite (DoT), Chill/Freeze, Shock (+% DMG taken), Bleed, Poison.
  - [x] *Vị trí:* `src/Core/Services/DamageCalculator.cs`

- [x] **Task 1.2: Mô Hình Hóa Nhân Vật & Các Lớp Phòng Thủ (`PlayerState.cs`, `DefensiveLayers.cs`)**
  - [x] Quản lý Effective Health Pool (EHP): Life, Energy Shield, Mana.
  - [x] Cơ chế trừ sát thương của Giáp: `% Reduction = Armour / (Armour + 5 * Damage)`.
  - [x] Cơ chế né tránh không ngẫu nhiên (Evasion Entropy Counter: 0 - 99).
  - [x] Cơ chế Đỡ đòn (Block / Spell Block) và Spell Suppression (giảm 50% dmg phép).
  - [x] Cơ chế sạc lại Energy Shield (sau 2s không nhận sát thương) và Chaos Inoculation (1 HP, miễn nhiễm Chaos).
  - [x] Kháng nguyên tố: Tính toán Overcapped Resistance và Max Resistance cap (75% - 90%).
  - [x] *Vị trí:* `src/Core/State/PlayerState.cs`, `src/Core/Models/DefensiveStats.cs`, `src/Core/Services/DefensiveLayers.cs`

- [x] **Task 1.3: Quái Vật & Tạo Đợt Quái (`MonsterInstance.cs`, `WaveSpawner.cs`)**
  - [x] Định nghĩa quái vật: Trash, Magic (Blue), Rare (Yellow - Aura modifiers), Map Boss.
  - [x] Sinh quái theo Map Tier (Stats scale theo monster level từ `data/maps.json`).
  - [x] Tính toán tỉ lệ Pack Size và Map Modifiers.
  - [x] *Vị trí:* `src/Core/Models/MonsterInstance.cs`, `src/Core/Simulation/WaveSpawner.cs`

- [x] **Task 1.4: Hạ Tầng Chiến Đấu Prerequisite (Combat Infrastructure)**
  - [x] **1.4a: Ailment Tracker (`AilmentTracker.cs`)**
    - [x] Quản lý danh sách active ailments trên mỗi entity (player & monster).
    - [x] Tick-down duration mỗi 33.33ms, remove khi hết hạn.
    - [x] DoT application: Ignite/Bleed DPS trừ HP mỗi tick. Poison stacking tổng DPS.
    - [x] Non-stacking refresh: Ignite/Shock/Chill giữ instance mạnh nhất, thay thế yếu hơn.
    - [x] Chill/Freeze effect: Giảm `AttacksPerSecond` của target theo `EffectPercent`.
    - [x] *Vị trí:* `src/Core/Simulation/AilmentTracker.cs`
  - [x] **1.4b: Skill Loadout & Action Time (`SkillLoadout.cs`, `ActionTimeAccumulator.cs`)**
    - [x] Loadout: 1 Active Gem + tối đa 5 Support Gems linked (PoE 2 model).
    - [x] Build `DamageModifierContext` từ gem stats + passive stats + gear stats.
    - [x] Action Time: Mỗi tick cộng dồn `1/30s`; khi đạt `1/AttackSpeed` → trigger attack.
    - [x] Cooldown tracker cho Guard Skills và Flask auto-instill.
    - [x] *Vị trí:* `src/Core/State/SkillLoadout.cs`, `src/Core/Simulation/ActionTimeAccumulator.cs`
  - [x] **1.4c: Map Modifier Runtime Effects (`MapModifierRuntime.cs`)**
    - [x] Parse `RolledMapModifier` → modify `DefensiveStats` snapshot (e.g. -Max Res).
    - [x] Apply monster damage conversion (e.g. "40% extra damage as Cold").
    - [x] Ailment immunity flags (e.g. "Monsters cannot be Chilled").
    - [x] Reflect damage pipeline hook.
    - [x] *Vị trí:* `src/Core/Simulation/MapModifierRuntime.cs`
  - [x] **1.4d: Auto-Targeting Strategy (`TargetingStrategy.cs`)**
    - [x] Idle-mode target selection: Player auto-targets nearest → AoE cleave all in range.
    - [x] Priority: Boss > Rare > Magic > Normal. Tự chuyển target khi current target chết.
    - [x] *Vị trí:* `src/Core/Simulation/TargetingStrategy.cs`
  - [x] **1.4e: Death & Recovery Flow (`DeathHandler.cs`)**
    - [x] XP penalty on death: -10% current level EXP (chuẩn PoE).
    - [x] Death counter tracking (cho OfflineCalculator death rate benchmark).
    - [x] Auto-respawn sau 2s delay: Refill tất cả resource pools, clear active ailments.
    - [x] Emergency Portal: Thoát map về Town, bảo toàn toàn bộ loot đã nhặt.
    - [x] *Vị trí:* `src/Core/Simulation/DeathHandler.cs`

- [x] **Task 1.5: Vòng Lặp Tick Engine 30Hz (`CombatEngine.cs`, `TickManager.cs`)**
  - [x] Vòng lặp 30 Ticks / giây (~33.33ms mỗi tick — chuẩn PoE Server Tick).
  - [x] Thứ tự xử lý 5 pha chuẩn: Regen & DoT -> Timers & Buffs -> Player Action -> Monster Action -> Death & Drop.
  - [x] Tích lũy thời gian hành động chính xác cao (Attack/Cast Speed phân giải 33.33ms).
  - [x] Cung cấp các C# Events: `OnTickProcessed`, `OnDamageDealt`, `OnPlayerDamaged`, `OnMonsterKilled`, `OnLootDropped`, `OnPlayerDied`.
  - [x] *Vị trí:* `src/Core/Simulation/CombatEngine.cs`, `src/Core/Simulation/TickManager.cs`

- [x] **Task 1.5b: Hình Học Không Gian & Tính Toán Combat Top-Down (Docs 03 Section 4)**
  - [x] Khoảng cách Euclid & Vùng tầm đánh (Melee <= 35px, Medium AoE <= 70px, Screen <= 220px).
  - [x] Radial AoE & công thức gia tăng bán kính: $R_{\text{effective}} = R_{\text{base}} \times \sqrt{1 + \sum \% \text{Increased AoE} / 100}$.
  - [x] Cones & Arcs: $\cos(\Delta \theta) \ge \cos(\alpha / 2)$ và $d \le L_{\text{range}}$ (Cleave / Ground Slam).
  - [x] Multi-Vector Projectile Spread ($\theta_k = \theta_{\text{aim}} + (k - \frac{N-1}{2})\Delta \phi$) & Chain ($R_{\text{chain}} \le 60\text{px}$).
  - [x] AI Target Selection theo Threat Scoring: $\text{Score} = W_{\text{Rarity}} \times 1000 + W_{\text{Boss}} \times 5000 + \frac{500}{d + 1} + \text{ClusterBonus}$.
  - [x] Bố trí quái vật 2D Top-Down trong `WaveSpawner` (Surrounding Swarm, Commander & Phalanx, Boss).
  - [x] Đồng bộ vị trí và vector đạn với `Arena2D` và `BattleViewport`.
  - [x] *Vị trí:* `src/Core/Services/SpatialCombatService.cs`, `src/Core/Models/Vector2D.cs`, `TargetingStrategy.cs`, `SpatialGeometryTests.cs`

- [x] **Task 1.5c: Hợp Nhất Cơ Chế Tấn Công Đối Xứng (Unified Combat & Attack System)**
  - [x] Mô hình hóa thực thể chiến đấu chung qua contract `ICombatEntity`: Cả PlayerState và MonsterInstance đều có MaxLife, Defenses (`DefensiveStats`), Ailments (`AilmentTracker`), `DoesAttackHit()`, `ApplyDamage()`.
  - [x] Đóng gói đòn đánh thành `AttackPayload`: Base damage pool 5 hệ, DamageModifierContext, IsAttack/IsSpell, Accuracy, Range.
  - [x] Hiện thực dịch vụ xử lý đòn đánh hợp nhất `CombatAttackService.ExecuteHit`:
    - Khuếch đại sát thương nếu mục tiêu bị dính Shock (Shock Debuff Amplifier).
    - Tính toán sát thương, chí mạng và trạng thái hiệu ứng qua `DamageCalculator.RollHit`.
    - Hấp thụ khiên phòng thủ (Guard Skills) nếu kích hoạt.
    - Đi qua 5 lớp phòng thủ chuẩn PoE qua `DefensiveLayers.ProcessHit` (Evasion, Block, Suppression, Armour, Resistances, EHP).
    - Áp dụng các Ailment gây ra lên `target.Ailments` (Ignite, Chill, Freeze, Shock, Bleed, Poison).
  - [x] Tái cấu trúc Phase 3 (Player Action) và Phase 4 (Monster Action) trong `CombatEngine.cs` dùng chung pipeline thay vì code rời rạc.
  - [x] Bộ kiểm thử toàn diện: `UnifiedCombatSystemTests.cs`.
  - [x] *Vị trí:* `src/Core/Models/ICombatEntity.cs`, `AttackPayload.cs`, `CombatAttackResult.cs`, `src/Core/Services/CombatAttackService.cs`, `UnifiedCombatSystemTests.cs`

- [ ] **Task 1.6: Thuật Toán Offline Progress Theo Snapshot Record (`OfflineCalculator.cs`)**
  - [ ] > **Dependency:** Task 1.5 (CombatEngine) + Task 3.1 + Task 3.2 phải hoàn thành trước.
  - [ ] Ghi nhận hiệu năng thực tế Online (Live Benchmark): Average Clear Time, Death Rate, Map Consumption.
  - [ ] Thuật toán ước lượng tức thì $O(1)$ khi khởi động lại game.
  - [ ] Tự động tiêu thụ Map trong Stash (fallback T0), tính Net EXP (trừ phạt chết) và gộp rolls rơi đồ qua Loot Filter.
  - [ ] *Vị trí:* `src/Core/Simulation/OfflineCalculator.cs`

---

## 🎨 Phase 2: Combat Viewport & UI Implementation (Godot 4)

> **Mục tiêu:** Xây dựng giao diện hiển thị 300px tại nửa trên cửa sổ `Main.tscn` theo đúng thiết kế `07_combat_ui_and_viewport.md`.

- [x] **Task 2.1: Khởi Tạo Scene & Layout Khung (`scenes/BattleViewport.tscn`)**
  - [x] Tạo node gốc `BattleViewport` (Control, size 440 x 300 px).
  - [x] Phân chia các container: `MapHeaderContainer`, `ArenaViewportContainer`, `VitometerPanel`, `LootToastOverlay`.
  - [x] Tích hợp vào `scenes/Main.tscn` thay thế cho `ViewportPlaceholder`.
  - [x] *Vị trí:* `scenes/BattleViewport.tscn`, `src/UI/BattleViewport.cs`

- [x] **Task 2.2: Map Header & Threat Telemetry Component**
  - [x] Hiển thị Tên Map, Tier badge (màu Trắng/Vàng/Đỏ/Tím theo độ hiếm).
  - [x] Các icon Threat Badges cảnh báo nhanh (Reflect, -Max Res, No Leech) kèm tooltip.
  - [x] Thanh tiến trình quái dọn dẹp (`ProgressBar`: 0% -> 100% để spawn Boss).
  - [x] *Vị trí:* `src/UI/Components/MapHeaderBar.cs`

- [x] **Task 2.3: 2D Top-Down Combat Arena & Entity Rendering**
  - [x] Mặt sàn đá cổ Top-Down (Dungeon Stone Tiles / Flagstones) với viền bóng tối Vignette & bụi tàn tro trôi dạt 2D.
  - [x] Nhân vật Kẻ Lưu Đày (Exile) ngự trị trung tâm (Focal Anchor 220, 104) với hào quang Aura 360° và cơ chế Auto-Facing tự xoay hướng mục tiêu.
  - [x] Hiển thị bầy quái vật bố trí 2D đa hướng (Trash Swarm / Magic / Rare golden aura / Boss overhead HP bar).
  - [x] Hiển thị đạn đạo 360° (Projectiles): Vệt tia sáng tối giản (streaks), mã màu theo 5 hệ sát thương PoE, visual clamping tối đa 12 đạn đồng thời.
  - [x] Render Vũ Khí 2D Top-Down theo từng phân lớp (Swords, Dual-Wield, Bow & Quiver, Wand/Sceptre, Axes, Maces, Staves, Claws/Daggers, Shields) với hiệu ứng vung chém / giương cung khi tấn công.
  - [x] Boss Slam Telegraph Indicator: Vòng tròn cảnh báo đỏ mờ 2D co dần (1.2s–1.5s wind-up) cho đòn chí mạng (>70% EHP), tạo cửa sổ kích hoạt Emergency Portal (`Space`).
  - [x] *Vị trí:* `src/UI/Components/Arena2D.cs`

- [x] **Task 2.4: Bộ Đo Sinh Mệnh & Phòng Thủ (Player Vitometer)**
  - [x] Thanh máu kép (Layered Health Bar): Máu đỏ (Life) + Khiên dạ quang (Energy Shield) + Viền vàng (Guard Skill).
  - [x] Thanh Mana & Mana Reservation: Phân tách vùng Mana bị khóa bởi Aura và Mana dùng được.
  - [x] Khay 5 bình thuốc (Flask Rack): Hiển thị tích nạp Charges và viền sáng khi Auto-Instill kích hoạt.
  - [x] *Vị trí:* `src/UI/Components/VitometerView.cs`

- [x] **Task 2.5: Floating Combat Text Pool (Số Sát Thương Nổi)**
  - [x] Object pool cho các nhãn sát thương bay nổi để tránh GC (Garbage Collection).
  - [x] Phân màu chuẩn PoE: Trắng (Phys), Đỏ (Fire), Xanh lơ (Cold), Vàng (Lightning), Tím (Chaos).
  - [x] Sát thương chí mạng (Crit): Chữ to 16px, hiệu ứng bật nảy (Pop & Shake) và viền vàng.
  - [x] *Vị trí:* `src/UI/Components/FloatingTextManager.cs`

- [ ] **Task 2.6: Dopamine Loot Toast & Drop Beams**
  - [ ] Hiệu ứng cột sáng (Drop Beam) chiếu thẳng xuống điểm rơi của quái khi rớt đồ hiếm.
  - [ ] Banner thông báo (Toast) trượt từ mép trên Arena hiển thị tên item (Divine Orb, 6-Link...).
  - [ ] Tích hợp âm thanh "Ting!" đanh thép.
  - [ ] *Vị trí:* `src/UI/Components/LootToastView.cs`

- [ ] **Task 2.7: Emergency Portal & Điều Khiển Vĩ Mô**
  - [ ] Nút "Emergency Portal" màu xanh cổng dịch chuyển và phím tắt `Space`.
  - [ ] Nút Inspector `[?]` hiển thị popup tổng kết combat / Death Recap khi nhân vật ngã xuống.
  - [ ] *Vị trí:* `src/UI/Components/MacroControlsBar.cs`

- [x] **Task 2.8: Kiến Trúc Cửa Sổ Động & Điều Khiển Màn Hình (Dynamic Window Controller)**
  - [x] Tích hợp `DesktopWindow.cs` & `WindowController.cs` (tham chiếu từ `reference/windows/`).
  - [x] Chế độ Compact Mode (`440 x 380` px) treo máy tinh gọn và Expanded Mode (`440 x 780` px) theorycrafting.
  - [x] Thuật toán neo đáy Taskbar `keepBottom = true` (cửa sổ nở lên phía trên, giữ nguyên chân đế).
  - [x] Tích hợp Win32 `SHAppBarMessage` tự động căn chỉnh trên Taskbar và `MoveToNextScreen()` đa màn hình.
  - [x] Tích hợp các nút điều khiển trên Header (`ResizeModeButton` `[⤡ Min]`/`[⤢ Expand]`, `ScreenCycleButton` `[🖥]`).
  - [x] *Vị trí:* `src/UI/DesktopWindow.cs`, `src/UI/WindowController.cs`, `scenes/Main.tscn`, `src/UI/Main.cs`

---

## 💎 Phase 3: Hệ Thống Trang Bị 10 Slots & Nền Kinh Tế Vật Phẩm

> **Mục tiêu:** Hiện thực hóa kiến trúc trang bị 10 ô cố định (theo [11_equipment_system_design.md](docs/11_equipment_system_design.md)), kết nối quái chết với bộ sinh đồ `ItemGenerator`, lọc đồ tự động `LootFilterService` và hiển thị trên giao diện mini-window.

- [x] **Task 3.1: Mô Hình Hóa Trang Bị Phòng Thủ & Thuộc Tính Mềm (`ArmourBaseDefinition.cs`, `EquippedGearState.cs`)**
  - [x] Áp dụng **Phương án C (Thuộc tính mềm)**: Mọi trang bị chỉ yêu cầu Level nhân vật, không có rào cản chỉ số cứng (Zero Stat-Locking).
  - [x] Tạo `ArmourBaseDefinition.cs` và `ArmourItemInstance.cs` (tính toán Local Flat/Inc Armour, Evasion, Energy Shield).
  - [x] Tạo `AccessoryItemInstance.cs` cho Amulet, Rings, Belt (Implicit Modifiers, Global Affixes).
  - [x] Mở rộng `EquippedGearState.cs` quản lý trọn vẹn 10 slots: MainHand, OffHand, Helmet, BodyArmour, Gloves, Boots, Belt, Amulet, Ring1, Ring2.
  - [x] Tích hợp công thức thưởng nội tại của Thuộc Tính Mềm vào `PlayerState.cs`: 10 Str (+5 Life, +2% Armour), 10 Dex (+20 Acc, +2% Eva), 10 Int (+5 Mana, +2% ES).
  - [x] *Vị trí:* `src/Core/Models/`, `src/Core/State/EquippedGearState.cs`

- [ ] **Task 3.2: Bộ Sinh Trang Bị & Vật Phẩm Rơi (`ItemGenerator.cs`)**
  - [ ] Sinh trang bị ngẫu nhiên theo `ilvl` và Map Tier (Normal -> Magic -> Rare).
  - [ ] Thuật toán Roll Affixes dựa trên `data/affixes.json` (phân định Prefix/Suffix pool, ilvl tier gating, trọng số weights).
  - [ ] Tuân thủ quy tắc chống loãng Affix: Loại bỏ các dòng +Str/Dex/Int đơn lẻ khỏi Suffix pool ngẫu nhiên (chỉ xuất hiện qua Implicits, Passive Tree, hoặc Unique items).
  - [ ] Rơi Currency theo trọng số độ hiếm PoE chuẩn (Scroll -> Transmute -> Alteration -> Regal -> Chaos -> Exalt -> Divine).
  - [ ] Hỗ trợ phôi đồ Unique cơ bản và trang bị định hình lối chơi *Attribute Stacking*.
  - [ ] *Vị trí:* `src/Core/Services/ItemGenerator.cs`

- [ ] **Task 3.3: Bộ Lọc Đồ Tự Động & Rương Phân Loại (`LootFilterService.cs`, `AffinityStash.cs`)**
  - [ ] Bộ lọc dựa trên LINQ tốc độ cao ($O(1)$ allocation).
  - [ ] 3 cấp độ nghiêm ngặt: Regular -> Strict -> Uber-Strict.
  - [ ] Tự động chuyển vật phẩm nhặt được vào đúng ngăn tủ chuyên dụng (Currency Tab, Gear Tab, Map Tab, Fossil Tab).
  - [ ] *Vị trí:* `src/Core/Services/LootFilterService.cs`

- [x] **Task 3.4: Giao Diện Trang Bị Mini-Window & Smart Tooltip So Sánh (`EquipmentView.cs`)**
  - [x] Bố cục 10 slots đồ trực quan trên giao diện 440x780 px kèm icon phân lớp vũ khí (⚔️, 🏹, 🪄, 🛡️).
  - [x] Smart Tooltip hiển thị trọn vẹn Base Stats vũ khí (Phys Damage, Crit Chance, APS, Base DPS) và định dạng chuẩn Implicit Modifier PoE (`✦ (Implicit)`).
  - [x] Khay Candidate Drawer hỗ trợ trang bị tay phụ (Off-Hand) linh hoạt: Shields, Quivers, và Dual-Wield 1H Weapons.
  - [x] Smart Tooltip hiển thị Delta EHP (▲/▼) và Delta DPS tức thì trong 1.5 giây.
  - [x] Nút thao tác nhanh: [Mặc Ngay], [Khóa Đồ], [Chuyển Rương].
  - [x] *Vị trí:* `src/UI/Components/EquipmentView.cs`

---

## ⚡ Phase 4: Tối Ưu Hóa & Kiểm Thử Toàn Diện

- [ ] **Task 4.1: Tối Ưu Hóa Tiêu Thụ Tài Nguyên (Resource Profiling)**
  - [ ] Kiểm tra đo đạc CPU ở chế độ chạy nền (<1% CPU trên Windows).
  - [ ] Đảm bảo bộ nhớ RAM duy trì ổn định dưới 150MB.
  - [ ] Tối ưu GC pressure trong hot path: Object pooling cho DamageModifierContext, HitResult, AilmentList. Mục tiêu: < 1KB/s steady-state GC allocation khi combat.
- [ ] **Task 4.2: Unit Tests Cho Math & Combat Loop**
  - [ ] Viết xUnit test kiểm tra công thức Increased vs More: Đảm bảo kết quả chính xác 100%.
  - [ ] Viết test cho Evasion Entropy, Armour reduction và Energy Shield recharge.
- [ ] **Task 4.3: Kiểm Thử Headless & CI Pipeline**
  - [ ] Chạy Godot Console Headless verification: Không ném Exception, thoát sạch mã 0.
- [ ] **Task 4.4: Integration Test — Full Combat Loop**
  - [ ] Spawn wave → player attacks → monsters attack → defensive pipeline → death → loot drop → next wave.
  - [ ] Kiểm tra 100 map runs headless, verify: 0 exceptions, EXP tăng, loot drops đúng distribution.
  - [ ] Benchmark: Measure ticks/second, GC allocations/second, peak memory.

---

## ⛏️ Phase 5: Dual-Axis Progression & Streamlined Fossil Crafting

> **Chi tiết kế hoạch triển khai đầy đủ:** Xem tại [DUAL_PROGRESSION_AND_CRAFTING_PLAN.md](docs/DUAL_PROGRESSION_AND_CRAFTING_PLAN.md).

- [ ] **Task 5.1: Pipeline Dữ Liệu Hóa Thạch & Vực Sâu**
  - [ ] Tạo `data/fossils.json` (7 loại Hóa thạch cơ bản + hệ số trọng số tag Affix).
  - [ ] Tạo `data/abyss.json` (Công thức scale vô tận: HP $\propto Depth^{1.35}$, DMG $\propto Depth^{1.25}$, mốc mở khóa Depth 150/300).
  - [ ] Cập nhật `DatabaseLoader.cs` và các C# Models trong `src/Core/Models/`.
- [ ] **Task 5.2: Tự Động Hóa Mapping Zero-APM**
  - [ ] `MapQueueManager.cs`: Hàng đợi 10 slots map tự động nối chuyến (3s delay).
  - [ ] `ContinuousMappingService.cs`: Tự tìm map trong Stash + Map Safety Filter né Reflect.
  - [ ] Fail-safe fallback về *The Endless Hollow (T0)*.
- [ ] **Task 5.3: Vực Sâu Vô Tận (The Endless Abyss Core)**
  - [ ] Quản lý năng lượng `CurrentSulphite` / `MaxSulphiteCapacity` trong `PlayerState.cs`.
  - [ ] `AbyssSimulationService.cs`: Scale độ sâu vô hạn, theo dõi `DeepestDepthReached`.
  - [ ] Rơi độc quyền Hóa thạch vào `FossilInventory`.
- [ ] **Task 5.4: Động Cơ Chế Tạo Hóa Thạch 1-Click**
  - [ ] `FossilAffixWeightCalculator.cs`: Biến đổi trọng số tag (x10 dòng ưu tiên, triệt tiêu dòng cấm).
  - [ ] `FossilCraftingService.cs`: 1-Click craft trên Bàn Chế Tạo (Anvil) với 1-3 slots theo Depth.
- [ ] **Task 5.5: Giao Diện Người Dùng Mini-Window (440x780)**
  - [ ] `CraftingAnvilView.cs`: Khay chế tạo 3 Fossil slots trực quan kèm dự báo thời gian thực.
  - [ ] `AbyssDashboardView.cs`: Hiển thị Depth to bản, bình Sulphite và nút Lặn Sâu.
  - [ ] `MapQueueBarView.cs`: Khay 10 slots mini + nút bật tắt Continuous Run.

