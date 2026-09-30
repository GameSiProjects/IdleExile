# 12. Lộ Trình Triển Khai Toàn Diện (Master Implementation Roadmap)

Tài liệu này là **Bản đồ chỉ dẫn thực thi chiến lược (Strategic Execution Master Plan)** của dự án **Idle Exile**. Tài liệu tổng hợp toàn bộ các phân hệ kỹ thuật và thiết kế từ GDD (`docs/01` đến `docs/11`), xác lập thứ tự ưu tiên, phụ thuộc kỹ thuật (dependencies), các mốc phát hành (milestones) và phân kỳ chi tiết từ nền tảng đến bản hoàn thiện.

---

## 1. Triết Lý & Nguyên Tắc Thực Thi (Execution Philosophy)

```
┌────────────────────────────────────────────────────────────────────────┐
│                        IDLE EXILE ARCHITECTURE                         │
├──────────────────────────────────┬─────────────────────────────────────┤
│   CORE SIMULATION (Pure C#)      │     PRESENTATION LAYER (Godot 4)    │
│   • Hoàn toàn decoupled khỏi UI  │     • Mini-Window Viewport 440x780  │
│   • Tick Engine 30Hz chuẩn xác   │     • Camera2D Smooth Follow        │
│   • Chạy được Headless & Offline │     • Diagnostic Clarity trong 1s   │
│   • Tiêu thụ <1% CPU, <150MB RAM │     • Dopamine Loot Beams & Audio   │
└──────────────────────────────────┴─────────────────────────────────────┘
```

### 3 Nguyên Tắc Bất Di Bất Dịch:
1. **Kiến trúc tách rời tuyệt đối (Decoupled Core):** Toàn bộ thư mục `src/Core/` là C# thuần (.NET 8), không tham chiếu bất kỳ class nào của `Godot.Node`. Điều này đảm bảo tính năng kiểm thử tự động (Unit Test), chạy giả lập không đầu (Headless), và thuật toán offline tức thì $O(1)$.
2. **Dữ liệu dẫn dắt (Data-Driven Development):** Không hardcode chỉ số trong code. Mọi dữ liệu về gems, affixes, maps, passives, monsters đều lưu trong `data/*.json`.
3. **Tiến trình từng bước có thể chơi được ngay (Playable Vertical Slice):** Mỗi giai đoạn triển khai đều phải tạo ra một sản phẩm chạy được (`dotnet build` 0 cảnh báo, 0 lỗi; Godot headless chạy thoát mã 0).

---

## 2. Bảng Theo Dõi Trạng Thái Hiện Tại (Current Status Audit)

| Phân Hệ / Tính Năng | Trạng Thái | Vị Trí Triển Khai | Ghi Chú |
| :--- | :---: | :--- | :--- |
| **GDD Toàn Diện (Docs 01 - 11)** | ✅ Hoàn thành | `docs/` | Đặc tả chi tiết mọi hệ thống game |
| **Hạ tầng C# & Data JSON** | ✅ Hoàn thành | `data/`, `src/Core/Models/` | Load JSON qua `System.Text.Json` |
| **Toán học Sát thương & Phòng thủ** | ✅ Hoàn thành | `src/Core/Services/` | Increased vs More, 5 lớp phòng ngự |
| **Hợp nhất Combat (`ICombatEntity`)** | ✅ Hoàn thành | `src/Core/Models/`, `Services/` | `CombatAttackService`, 30Hz loop |
| **Giao diện Chiến đấu Cơ bản (UI)** | ✅ Hoàn thành | `scenes/BattleViewport.tscn` | Header, Vitometer, Floating Text |
| **Hệ thống 10 Ô Đồ & Thuộc Tính Mềm**| ✅ Hoàn thành | `src/Core/State/`, `src/UI/` | `EquippedGearState`, Smart Tooltip |
| **Map Hành Lang Tuyến Tính & Camera** | 🔄 Đang triển khai | `docs/02`, `docs/03`, `docs/07` | Chuẩn bị tích hợp vào Simulation & UI |
| **Bộ Sinh Đồ & Bộ Lọc Loot Filter** | ⏳ Chưa làm | `src/Core/Services/` | `ItemGenerator`, `LootFilterService` |
| **FTUE & Bộ 3 Kỹ Năng Khởi Đầu** | ⏳ Chưa làm | `docs/08`, `docs/10` | Juggernaut, Elementalist, Deadeye |
| **Hệ Thống Gem PoE 2 & Passive Tree** | ⏳ Chưa làm | `data/`, `src/Core/State/` | Active Gem cắm 5 Support, Passive Web |
| **Vực Sâu Vô Tận & Chế Tạo Hóa Thạch**| ⏳ Chưa làm | `docs/DUAL_PROGRESSION_...` | Delve vô hạn, Sulphite, 1-Click Anvil |
| **Offline Progress O(1) & Polish** | ⏳ Chưa làm | `src/Core/Simulation/` | Snapshot Benchmark, Audio, Đóng gói |

---

## 3. Lộ Trình 8 Giai Đoạn Chi Tiết (8-Phase Master Roadmap)

```
[Phase 1: Core Combat] ──► [Phase 2: Linear Viewport] ──► [Phase 3: Loot & Filter]
                                                                   │
                                                                   ▼
[Phase 6: Auto-Mapping] ◄── [Phase 5: Gems & Tree] ◄── [Phase 4: FTUE Starters]
         │
         ▼
[Phase 7: Endless Abyss] ──► [Phase 8: Offline, Polish & Launch]
```

---

### 🟢 Giai Đoạn 1: Lõi Mô Phỏng Chiến Đấu & Toán Học (Combat Core)
> **Mục tiêu:** Xây dựng cỗ máy tính toán chiến đấu 30 Ticks/giây thuần C# vững chắc như bàn thạch.

- [x] **1.1: Bộ Tính Toán Sát Thương Chuẩn PoE (`DamageCalculator.cs`)**
  - [x] Công thức phân cấp: $\text{Base} \times (1 + \sum \text{Inc} - \sum \text{Red}) \times \prod (1 + \text{More}) \times \prod (1 - \text{Less})$.
  - [x] 5 hệ sát thương: Physical, Fire, Cold, Lightning, Chaos.
  - [x] Xử lý đòn chí mạng (Crit Chance 0-100%, Crit Multiplier).
  - [x] Hiệu ứng trạng thái Ailments (Ignite, Chill, Freeze, Shock, Bleed, Poison).
- [x] **1.2: Mô Hình Hóa Nhân Vật & 5 Lớp Phòng Thủ (`PlayerState.cs`, `DefensiveLayers.cs`)**
  - [x] Effective Health Pool (EHP): Life, Energy Shield, Mana.
  - [x] Công thức giáp phi tuyến tính (Armour Damage Reduction curve).
  - [x] Evasion Entropy Counter (0 - 99) chống xui xẻo RNG.
  - [x] Đỡ đòn (Block/Spell Block) & Spell Suppression (giảm 50% dmg phép).
  - [x] Sạc lại ES sau 2s không nhận sát thương & Keystone Chaos Inoculation (1 HP, miễn nhiễm Chaos).
- [x] **1.3: Quái Vật & Quy Tắc Sinh Cụm Quái (`MonsterInstance.cs`, `WaveSpawner.cs`)**
  - [x] Phân cấp quái: Normal (Trash), Magic (Blue), Rare (Yellow + Aura mod), Map Boss.
  - [x] Scaling chỉ số quái theo Monster Level & Map Tier từ `data/maps.json`.
- [x] **1.4: Hạ Tầng Chiến Đấu Runtime**
  - [x] `AilmentTracker.cs`: Quản lý thời lượng, DoT tick-down, non-stacking vs stacking rules.
  - [x] `SkillLoadout.cs` & `ActionTimeAccumulator.cs`: Tích lũy thời gian xuất chiêu theo Attack/Cast Speed.
  - [x] `MapModifierRuntime.cs`: Áp dụng các mod bất lợi của map (Reflect, -Max Res, Added Ele).
  - [x] `TargetingStrategy.cs`: Tự động ngắm bắn theo Threat Scoring (Boss > Rare > Magic > Normal).
  - [x] `DeathHandler.cs`: Phạt -10% EXP khi chết, tự hồi sinh sau 2s delay.
- [x] **1.5: Vòng Lặp Tick Engine 30Hz (`CombatEngine.cs`, `TickManager.cs`)**
  - [x] Tần số cố định 30 Ticks/giây (~33.33ms — chuẩn PoE Server Tick).
  - [x] 5 pha xử lý: Regen & DoT $\to$ Timers & Buffs $\to$ Player Action $\to$ Monster Action $\to$ Death & Drop.
  - [x] Hệ thống C# Events phát tín hiệu ra Viewport UI.
- [x] **1.6: Hợp Nhất Cơ Chế Tấn Công Đối Xứng (`ICombatEntity`, `CombatAttackService.cs`)**
  - [x] Contract chung `ICombatEntity` cho cả Player và Monster.
  - [x] Đóng gói `AttackPayload` và hàm xử lý chuẩn hóa `CombatAttackService.ExecuteHit`.
- [x] **1.7: Hình Học Không Gian 2D Top-Down (`SpatialCombatService.cs`)**
  - [x] Khoảng cách Euclid và các tầm đánh chuẩn: Melee 35px, AoE 70px, Screen 220px.
  - [x] Hit-testing: Radial AoE ($R_{\text{eff}} = R_{\text{base}}\sqrt{1 + \text{Inc}/100}$), Cones & Arcs ($60^\circ - 90^\circ$).
  - [x] Multi-Vector Projectiles Spread & Chain jumping ($R_{\text{chain}} \le 60\text{px}$).

---

### 🟡 Giai Đoạn 2: Viewport Hành Lang Tuyến Tính & Camera2D
> **Mục tiêu:** Đưa nhân vật vào hành lang kéo dài đơn hướng, Camera2D bám theo bước chân thực tế, triệt tiêu hoàn toàn cuộn sàn giả lập.

- [x] **2.1: Layout Khung Viewport Mini-Window (`scenes/BattleViewport.tscn`, `BattleViewport.cs`)**
  - [x] Khung nhìn 440 x 300 px tại nửa trên cửa sổ.
  - [x] 4 container phân tầng: Header, ArenaViewport, Vitometer, LootToast.
- [x] **2.2: Map Header & Threat Telemetry (`MapHeaderBar.cs`)**
  - [x] Tên Map, Tier badge phân màu (Trắng, Vàng, Đỏ, Tím).
  - [x] Threat Badges cảnh báo nhanh (Reflect, -Max Res, No Leech).
  - [x] Thanh tiến trình cọc mốc map (`ProgressBar`: 0% -> 100%).
- [x] **2.3: Player Vitometer Đa Lớp (`VitometerView.cs`)**
  - [x] Thanh máu kép (Layered Life + ES + Guard Skill viền vàng).
  - [x] Thanh Mana & Mana Reservation phân tách vùng xích aura.
  - [x] Khay 5 bình thuốc (Flask Rack) hiển thị nạp Charges và viền sáng Auto-Instill.
- [x] **2.4: Số Sát Thương Nổi 5 Màu (`FloatingTextManager.cs`)**
  - [x] Object pool cho nhãn sát thương nổi (tránh GC allocation).
  - [x] Phân màu 5 hệ PoE + hiệu ứng giật nảy to bản khi Crit (Pop & Shake).
- [x] **2.5: Bộ Điều Khiển Di Chuyển Thế Giới Thực (`ExileMovementController.cs`)**
  - [x] Tọa độ thực $P_E(t) = (X_{\text{world}}, Y_{\text{world}})$ tịnh tiến dọc hành lang dài $L_{\text{map}}$.
  - [x] Vận tốc thực tế scale theo chỉ số `% Movement Speed`.
  - [x] Máy trạng thái tự động Zero-APM: `Marching` $\to$ `Engaging` (dừng giữ cự ly tối ưu xả skill) $\to$ `Advancing`.
- [ ] **2.6: Hệ Thống Camera2D Smooth Follow (`ArenaCamera.cs`)**
  - [ ] Camera2D bám theo vị trí thực của Exile với thuật toán Lerp làm mượt.
  - [ ] Khoảng nhìn trước (`Look-ahead Offset`) về phía trước hướng hành quân.
- [ ] **2.7: Bố Trí Cụm Quái Tuần Tự Dọc Hành Lang (`CorridorMonsterLayout`)**
  - [ ] Pack 1: Bầy quái thường chắn đường (Vanguard Swarm).
  - [ ] Pack 2: Quái Tinh Anh (Rare Commander có vòng hào quang hoàng kim).
  - [ ] Encounter: Vết nứt Breach, mạch Sulphite hoặc Essence phong ấn bên rìa đường.
  - [ ] Pack 3, 4: Đội hình hộ vệ hoặc bọc hậu (Phalanx / Flankers).
- [ ] **2.8: Cổng Sương Mù & Đấu Trường Boss Cuối Đường (Boss Room Chamber)**
  - [ ] Cổng sương mù (Fog Gate) phong tỏa tại mốc 85% chiều dài map.
  - [ ] Bước qua cổng: Camera2D khóa cố định vào tâm phòng Boss (Arena Lock).
  - [ ] Map Boss thức tỉnh kèm thanh máu lớn trên đầu (Overhead Boss Bar).
  - [ ] Đòn Boss Slam chí mạng: Vòng đỏ cảnh báo co dần 1.4s (Telegraph wind-up).
- [ ] **2.9: Hiệu Ứng Dopamine Loot Rơi Đồ**
  - [ ] Cột sáng Shader (Drop Beam) chiếu từ trần xuống đúng tọa độ quái chết rơi đồ hiếm.
  - [ ] Banner Loot Toast trượt từ đỉnh Viewport kèm âm thanh "Ting!" đanh thép.
- [ ] **2.10: Phím Khẩn Cấp & Điều Khiển Vĩ Mô (`MacroControlsBar.cs`)**
  - [ ] Nút "Emergency Portal" màu xanh cổng dịch chuyển và phím tắt `Space` để tẩu thoát.
  - [ ] Nút Inspector `[?]` hiển thị phân tích combat & Death Recap khi tử nạn.

---

### 🔵 Giai Đoạn 3: Hệ Thống Trang Bị 10 Ô, Rơi Đồ & Bộ Lọc Tự Động (Loot & Economy)
> **Mục tiêu:** Tạo nên nền kinh tế Currency và vòng lặp nhặt - lọc - so sánh đồ gây nghiện.

- [x] **3.1: Mô Hình Hóa 10 Ô Trang Bị & Thuộc Tính Mềm (`EquippedGearState.cs`, `ArmourBaseDefinition.cs`)**
  - [x] Quản lý 10 slots cố định: MainHand, OffHand, Helmet, BodyArmour, Gloves, Boots, Belt, Amulet, Ring1, Ring2.
  - [x] Thuộc tính mềm: Mọi trang bị chỉ yêu cầu Level nhân vật, không có rào cản stat cứng.
  - [x] Thưởng nội tại: 10 Str (+5 Life, +2% Armour), 10 Dex (+20 Acc, +2% Eva), 10 Int (+5 Mana, +2% ES).
- [x] **3.2: Giao Diện Trang Bị Mini-Window & Smart Tooltip So Sánh (`EquipmentView.cs`)**
  - [x] Bố cục 10 ô trực quan kèm icon phân lớp vũ khí (⚔️, 🏹, 🪄, 🛡️).
  - [x] Khay Candidate Drawer hỗ trợ Off-Hand: Khiên, Ống tên Quiver, Dual-Wield vũ khí 1 tay.
  - [x] Smart Tooltip hiển thị Delta EHP (▲/▼) và Delta DPS tức thì trong 1.5 giây.
- [ ] **3.3: Bộ Sinh Trang Bị Ngẫu Nhiên (`ItemGenerator.cs`)**
  - [ ] Sinh đồ theo cấp độ quái (`ilvl`) và Bậc Map (Tier 1 – Tier 17+).
  - [ ] Thuật toán Roll Affixes dựa trên `data/affixes.json` (Prefix/Suffix pool, weights, tier gating).
  - [ ] Quy tắc chống loãng: Loại bỏ các dòng stat thô khỏi Suffix ngẫu nhiên, chỉ xuất hiện qua Implicits/Tree/Uniques.
  - [ ] Hỗ trợ phôi đồ Unique cơ bản và trang bị định hình lối chơi Attribute Stacking.
- [ ] **3.4: Nền Kinh Tế Tiền Tệ PoE (Currency Drop Engine)**
  - [ ] Rơi Currency theo trọng số chuẩn (*Scroll $\to$ Transmute $\to$ Alteration $\to$ Regal $\to$ Chaos $\to$ Exalt $\to$ Divine*).
  - [ ] Không có tiền vàng (No Gold), toàn bộ giao dịch dùng ngọc đập đồ.
- [ ] **3.5: Bộ Lọc Đồ Tự Động Tốc Độ Cao (`LootFilterService.cs`)**
  - [ ] Bộ lọc dựa trên biểu thức LINQ tối ưu cực nhanh ($O(1)$ allocation).
  - [ ] 3 cấp độ nghiêm ngặt: Regular (Nhặt hết) $\to$ Strict (Bỏ đồ trắng/xanh thấp) $\to$ Uber-Strict (Chỉ nhặt T1 Currency & phôi đồ xịn).
- [ ] **3.6: Rương Đồ Tự Động Phân Loại (Affinity Stash)**
  - [ ] Tự động chuyển vật phẩm nhặt được vào đúng ngăn tủ chuyên biệt: Currency Tab, Gear Tab, Map Tab, Fossil Tab.
  - [ ] Nút thao tác nhanh 1-Click: [Mặc Ngay], [Khóa Đồ], [Chuyển Rương].

---

### 🟣 Giai Đoạn 4: Trải Nghiệm Ban Đầu (FTUE) & Bộ Ba Kỹ Năng Starter
> **Mục tiêu:** Người chơi mới vào game hiểu ngay cơ chế và tham gia chiến đấu trong vòng 15 giây đầu tiên mà không bị ngộp.

- [ ] **4.1: Màn Chọn 3 Starter Archetypes 1-Click (`StarterSelectionView.cs`)**
  - [ ] **Juggernaut (Đỏ / Strength):** Chuyên cận chiến, kỹ năng *Cleave*, vũ khí Kiếm/Rìu, trâu bò giáp dày.
  - [ ] **Elementalist (Xanh lam / Intelligence):** Chuyên phép thuật, kỹ năng *Fireball*, vũ khí Gậy Wand, thiêu đốt Ignite.
  - [ ] **Deadeye (Xanh lục / Dexterity):** Chuyên cung thủ, kỹ năng *Split Arrow*, vũ khí Cung + Quiver, bắn chùm đạn tầm xa.
- [ ] **4.2: Tích Hợp Chi Tiết Bộ Ba Kỹ Năng Khởi Đầu (Docs 10)**
  - [ ] *Cleave:* Chém hình quạt $80^\circ$, chém đồng thời 2 tay nếu Dual-Wield.
  - [ ] *Fireball:* Cầu lửa nổ lan diện rộng (Radial AoE 40px) và để lại vũng thiêu đốt DoT.
  - [ ] *Split Arrow:* Bắn 5 mũi tên rẽ quạt, cấm cơ chế shotgun bẩn, tối ưu dọn bầy quái rộng.
- [ ] **4.3: Mở Khóa Giao Diện Lũy Tiến (Progressive Disclosure)**
  - [ ] Phút 0: Chỉ hiển thị Viewport đánh quái + 1 Tab Trang bị.
  - [ ] Phút 3: Rơi bản đồ đầu tiên $\to$ Mở khóa Tab Map Device.
  - [ ] Phút 5: Lên cấp $\to$ Mở khóa Tab Passive Tree.
  - [ ] Phút 10: Rớt Hóa thạch $\to$ Mở khóa Tab Crafting Anvil.
- [ ] **4.4: Gói Tài Nguyên Khởi Đầu & Cơ Chế Chống Kẹt (Fail-Safe)**
  - [ ] Tặng sẵn 3x Tier 1 Map: *Overgrown Beach* và Currency cơ bản (*Transmute, Wisdom*).
  - [ ] Cỗ máy Map Device luôn tích hợp sẵn *The Endless Hollow (Tier 0)* không bao giờ cạn kiệt, đảm bảo 100% rớt lại Map T1.

---

### 🟠 Giai Đoạn 5: Hệ Thống Ngọc PoE 2 & Cây Nội Tại Khổng Lồ (Gems & Passives)
> **Mục tiêu:** Hiện thực hóa cốt lõi của triết lý *"100% Theorycrafting"*.

- [ ] **5.1: Hệ Thống Ngọc Kỹ Năng Độc Lập (PoE 2 Gem Sockets Model)**
  - [ ] Trang bị 100% KHÔNG có socket, người chơi thay đồ tự do không sợ mất kỹ năng.
  - [ ] Active Gem tự sở hữu tối đa 5 khe cắm Support Gem.
  - [ ] 100% tự động liên kết (Auto-linked, khai tử hoàn toàn *Orb of Fusing*).
- [ ] **5.2: Tích Hợp Các Support Gems Biểu Tượng**
  - [ ] *Greater Multiple Projectiles (GMP):* +4 tia đạn, góc xòe $\pm 30^\circ$, 25% Less Damage.
  - [ ] *Faster Attacks / Faster Casting:* +35% More Attack/Cast Speed.
  - [ ] *Melee Physical Damage:* +40% More Melee Physical Damage.
  - [ ] *Added Fire Damage:* Nhận 35% Physical Damage as Extra Fire Damage.
  - [ ] *Increased Critical Strikes:* +2% Base Crit Chance, +60% Increased Crit Chance.
- [ ] **5.3: Bảng Nội Tại Khổng Lồ Dạng Đồ Thị (`data/passive_tree.json`)**
  - [ ] Mô hình hóa Graph C#: Nodes, Connections, Allocations, Pathfinding.
  - [ ] 3 xuất phát điểm nối thông nhau: Marauder (Str) $\to$ Witch (Int) $\to$ Ranger (Dex).
- [ ] **5.4: Hệ Thống Notables & Keystones Biến Đổi Cơ Chế**
  - [ ] *Chaos Inoculation:* Máu tối đa khóa ở 1 HP, miễn nhiễm 100% sát thương Chaos.
  - [ ] *Point Blank:* +30% More sát thương đạn ở tầm gần, giảm dần về -30% ở tầm xa.
  - [ ] *Resolute Technique:* Đòn đánh không bao giờ trượt né, nhưng không thể gây chí mạng.
- [ ] **5.5: Giao Diện Cây Nội Tại Mini-Window (`PassiveTreeView.cs`)**
  - [ ] Hệ thống Pan & Zoom mượt mà trong không gian tab `440 x 364` px.
  - [ ] Nút tự động tìm đường ngắn nhất (Auto-Pathing) khi click vào Notable ở xa.

---

### 🔴 Giai Đoạn 6: Động Cơ Chạy Map Tuyến Tính Tự Động (Zero-APM Mapping)
> **Mục tiêu:** Người chơi có thể setup chuỗi map rồi để game tự vận hành trơn tru ở góc màn hình.

- [ ] **6.1: Khay Hàng Đợi Map (Map Device Queue 10 Slots)**
  - [ ] Xếp sẵn chuỗi 10 bản đồ đã được craft chỉ số bằng Currency.
  - [ ] Tự động chuyển tiếp map tiếp theo sau 3 giây độ trễ (delay nhặt đồ).
- [ ] **6.2: Chế Độ Cày Liên Tục (Continuous Auto-Sustain Mode)**
  - [ ] Tự động lấy map cùng Tier trong Stash khi hàng đợi trống.
  - [ ] **Map Safety Filter:** Tự động phát hiện và bỏ qua map có mod phản đòn (*Reflect*) hoặc triệt tiêu hồi phục (*No Regen*) xung khắc build.
  - [ ] Tự động kích hoạt *The Endless Hollow (Tier 0)* khi hết sạch map hợp lệ.
- [ ] **6.3: Bảng Nội Tại Bản Đồ (Atlas Passive Tree)**
  - [ ] Tích lũy điểm Atlas Passive khi hạ gục các Map Boss mới.
  - [ ] Nhánh Map Sustain: +% xác suất quái rơi map bậc cao hơn.
  - [ ] Nhánh Currency Specialization: Tăng tỉ lệ rơi Chaos, Exalted, Divine Orb.
  - [ ] Nhánh Abyss Fueling: Tăng sản lượng Voltaxic Sulphite từ các mỏ địa chấn.
  - [ ] Nhánh Bossing: Tăng máu/độ khó Boss đổi lấy gấp đôi đồ hiếm.

---

### ⚫ Giai Đoạn 7: Trục Tiến Trình Thứ Hai — Vực Sâu Vô Tận & Chế Tạo Hóa Thạch
> **Mục tiêu:** Mở rộng endgame vô tận và cung cấp công cụ Target-Craft trang bị tối thượng.

- [ ] **7.1: Nền Kinh Tế Nhiên Liệu Sulphite (Sulphite Engine)**
  - [ ] Khai thác *Voltaxic Sulphite* từ các mạch địa chấn trong map từ Tier 6 trở lên.
  - [ ] Quản lý bình chứa `CurrentSulphite` / `MaxSulphiteCapacity` trong `PlayerState.cs`.
- [ ] **7.2: Vực Sâu Vô Tận (The Endless Abyss - Delve Core)**
  - [ ] Leo độ sâu không giới hạn ($Depth = 1 \to \infty$).
  - [ ] Scale chỉ số theo hàm số mũ: $\text{HP Multiplier} = 1 + (Depth \times 0.08)^{1.35}$, $\text{DMG Multiplier} = 1 + (Depth \times 0.05)^{1.25}$.
  - [ ] Lưu giữ kỷ lục chiều sâu cá nhân (`DeepestDepthReached`).
  - [ ] Rơi độc quyền 7 loại Hóa Thạch Tinh Chế (*Metallic, Scorched, Frigid, Pristine, Dense, Aberrant, Corroded*).
- [ ] **7.3: Bàn Chế Tạo Tinh Gọn 1-Click (Fossil Crafting Anvil)**
  - [ ] Bỏ hoàn toàn khuôn đúc (Resonators). Cắm trực tiếp Hóa thạch vào khay Anvil.
  - [ ] Mở khóa ô cắm lũy tiến: Vượt Depth 150 mở ô thứ 2, Depth 300 mở ô thứ 3.
  - [ ] Bộ tính toán trọng số Affix (`FossilAffixWeightCalculator.cs`): x10 dòng ưu tiên, triệt tiêu dòng cấm.
  - [ ] Dự báo thời gian thực tỉ lệ ra dòng mong muốn trước khi bấm nút Đúc.
- [ ] **7.4: Giao Diện Dashboard Abyss & Bàn Chế Tạo Mini-Window**
  - [ ] `AbyssDashboardView.cs`: Hiển thị chỉ số Depth to bản, bình Sulphite và nút Lặn Sâu.
  - [ ] `CraftingAnvilView.cs`: Khay 3 Fossil slots trực quan kèm hiệu ứng đúc đồ thỏa mãn.

---

### ⚪ Giai Đoạn 8: Tính Toán Offline O(1), Tối Ưu Hóa & Phát Hành
> **Mục tiêu:** Đảm bảo trải nghiệm Desktop Companion hoàn hảo, siêu nhẹ và sẵn sàng phát hành.

- [ ] **8.1: Thuật Toán Offline Progress Tức Thì $O(1)$ (`OfflineCalculator.cs`)**
  - [ ] Ghi nhận Snapshot hiệu năng thực tế khi online: Tốc độ vượt map trung bình, tỉ lệ tử vong, mức tiêu hao map.
  - [ ] Khi khởi động lại game sau nhiều giờ: Tính toán tức thì trong 1 phép toán $O(1)$, không cần giả lập hàng triệu tick.
  - [ ] Tự động tiêu thụ map trong kho, tính Net EXP (trừ phạt chết) và gộp rolls rơi đồ qua Loot Filter.
- [ ] **8.2: Tối Ưu Hóa Bộ Nhớ & CPU Cực Hạn**
  - [ ] Kích hoạt `OS.LowProcessorUsageMode` của Godot 4.
  - [ ] Đo kiểm tài nguyên thực tế trên Windows: **CPU < 1%, RAM < 150MB**.
  - [ ] Triệt tiêu hoàn toàn rác bộ nhớ (Zero-allocation hot path trong nhịp 30Hz): Object Pooling cho đạn đạo, nhãn sát thương và dữ liệu đòn đánh (< 1KB/s steady-state GC allocation).
- [ ] **8.3: Hoàn Thiện Âm Thanh Phản Hồi (Audio Dopamine SFX)**
  - [ ] Tiếng "Ting!" rền vang biểu tượng của Loot Filter khi rơi đồ hiếm.
  - [ ] Tiếng vung kiếm chém, nổ cầu lửa, vút tên bắn cung.
  - [ ] Tiếng nổ trầm đục "THUMP!" của đòn Boss Slam và hiệu ứng rung màn hình 2D (Screen Shake 3px).
- [ ] **8.4: Đóng Gói Ứng Dụng Mini-Window**
  - [ ] Build file thực thi độc lập (.exe) kích thước nhỏ gọn.
  - [ ] Tính năng Always-on-top, ghi nhớ vị trí cửa sổ ở góc màn hình.

---

## 4. Các Mốc Phát Hành Lớn (Milestones & Release Gates)

```text
  [M1: PLAYABLE VERTICAL SLICE]  ──► 1 Bản đồ tuyến tính hoàn chỉnh từ đầu đến cuối
               │
               ▼
  [M2: CLOSED ALPHA]             ──► Đủ 3 Archetypes, Rơi đồ 10 slots, Cắm ngọc PoE 2
               │
               ▼
  [M3: OPEN BETA]                ──► Auto-Mapping, Atlas Tree, Endless Abyss & Crafting
               │
               ▼
  [M4: GOLDEN MASTER (V1.0)]     ──► Offline O(1), Tối ưu <1% CPU, Âm thanh, Windows .exe
```

| Mốc | Tên Mốc | Tiêu Chí Đạt Chuẩn (Definition of Done) | Trọng Tâm Trải Nghiệm |
| :---: | :--- | :--- | :--- |
| **M1** | **Vertical Slice** | • Nhân vật tự di chuyển dọc hành lang (Real movement, Camera2D follow).<br>• Dọn 3 pack quái tuần tự $\to$ bước qua Fog Gate $\to$ Đấu Boss $\to$ Boss chết rớt đồ.<br>• Viewport 440x300 hiển thị đủ máu/mana/combat text. | Cảm giác di chuyển và nhịp combat mượt mà, thỏa mãn thị giác. |
| **M2** | **Closed Alpha** | • 3 Starter classes (Cleave, Fireball, Split Arrow).<br>• Rơi đồ ngẫu nhiên qua `ItemGenerator`, nhặt đồ vào 10 slots.<br>• Active gem cắm được 2 Support gems. | Vòng lặp Loot đồ, so sánh chỉ số và cắm ngọc hỗ trợ. |
| **M3** | **Open Beta** | • Chạy chuỗi 10 map tự động (Map Queue & Auto-Sustain).<br>• Bảng nội tại Passive Tree tăng điểm thực tế.<br>• Mở khóa The Endless Abyss và bàn đúc Hóa thạch 1-Click. | Chiều sâu Theorycrafting và cày cuốc dài hạn (Metagame). |
| **M4** | **Golden Master** | • Tính toán Offline tức thì khi tắt máy.<br>• Đạt chuẩn hiệu năng: CPU < 1%, RAM < 150MB.<br>• Âm thanh đầy đủ, đóng gói file `.exe` tiện dụng. | Hoàn thiện Desktop Companion không tì vết. |

---

## 5. Ma Trận Quản Trị Rủi Ro Kỹ Thuật (Risk Management)

| Rủi Ro Kỹ Thuật | Mức Độ | Ảnh Hưởng | Giải Pháp Phòng Ngừa & Giảm Thiểu |
| :--- | :---: | :--- | :--- |
| **Rác bộ nhớ (GC Spikes) trong vòng lặp 30Hz** | Cao | Gây khựng giật (micro-stutter) làm phiền người chơi khi đang làm việc. | Áp dụng triệt để `struct readonly`, `ObjectPool<T>` cho đạn đạo, số sát thương nổi và dữ liệu đòn đánh. Không cấp phát `new` trong hot path tick. |
| **Rác thị giác (Visual Clutter) trong cửa sổ 440px** | Trung bình | Khó nhìn thấy vòng cảnh báo tử thần của Boss. | Áp dụng Visual Clamping: Tối đa 12 đạn đồng thời, gộp các số sát thương nhảy cùng tick, vòng Telegraph vẽ nổi trên cùng với độ tương phản cao. |
| **Sai lệch toán học giữa Online và Offline** | Cao | Người chơi cảm thấy bị "lừa" hoặc bug cheat EXP khi tắt game. | Dùng chung mô hình toán học: Thuật toán Offline lấy trực tiếp Snapshot hiệu năng thực tế (Clear Time, Death Rate) mà nhân vật vừa chạy online trong 5 phút gần nhất. |
| **Xung đột luồng giữa C# Core và Godot UI** | Thấp | Crash game khi UI đọc dữ liệu lúc Core đang tính toán tick. | Core đẩy sự kiện ra ngoài thông qua C# Events một chiều (`OnTickProcessed`, `OnDamageDealt`, `OnMonsterKilled`). UI chỉ đăng ký lắng nghe (Observer Pattern), không can thiệp ngược vào trạng thái Core giữa tick. |

---

## 6. Checklist Hành Động Ngay (Immediate Sprint Checklist)

> **Mục tiêu ưu tiên số 1 hiện tại:** Hoàn thiện **Milestone M1 — Playable Vertical Slice (Giai Đoạn 2)** để đưa nhân vật thực sự bước đi trên hành lang tuyến tính, Camera2D bám theo và tiến vào phòng Boss.

- [x] **Sprint Task 1: Bộ Điều Khiển Di Chuyển Thế Giới Thực Của Exile**
  - [x] Tạo module tính toán vị trí thế giới thực $P_E(t) = (X_{\text{world}}, Y_{\text{world}})$ trong `src/Core/Simulation/`.
  - [x] Hiện thực máy trạng thái Zero-APM: `Marching` $\to$ `Engaging` $\to$ `Advancing`.
  - [x] Tích hợp vận tốc di chuyển thực tế theo `% Movement Speed` vào nhịp 30Hz.
- [ ] **Sprint Task 2: Camera2D Tracking & Không Gian Hành Lang Viewport**
  - [ ] Thêm node `ArenaCamera2D` vào `scenes/BattleViewport.tscn` với script `ArenaCamera.cs`.
  - [ ] Cấu hình Lerp bám theo `ExileNode` kèm độ lệch nhìn trước `Look-ahead Offset`.
  - [ ] Dựng `WorldMapCorridor` sàn đá hầm ngục trải dài theo chiều dài bản đồ.
- [ ] **Sprint Task 3: Bố Trí Cụm Quái Tuần Tự & Phòng Boss Cuối Bản Đồ**
  - [ ] Cấu hình vị trí Pack quái theo tọa độ thế giới cố định (Pack 1 $\to$ Pack 2 $\to$ League Encounter $\to$ Pack 3).
  - [ ] Thiết lập Cổng sương mù phong tỏa (`BossGateThreshold`) tại mốc 85% map.
  - [ ] Khóa cố định Camera2D vào trung tâm `BossRoomChamber` khi bước qua cổng, kích hoạt Boss Overhead Health Bar và vòng cảnh báo Slam Telegraph.
- [ ] **Sprint Task 4: Kiểm Thử Build & Trải Nghiệm Thực Tế**
  - [ ] Chạy `dotnet build` đạt 0 warning, 0 error.
  - [ ] Kiểm thử chạy thực tế trên Godot Engine: Exile tự di chuyển qua hành lang, dọn dẹp cụm quái, bước qua cổng sương mù và hạ gục Boss.

