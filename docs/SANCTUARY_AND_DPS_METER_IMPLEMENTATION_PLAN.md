# SANCTUARY_AND_DPS_METER_IMPLEMENTATION_PLAN.md — Kế Hoạch Triển Khai Kỹ Thuật Hub & Bộ Đếm DPS Thực Chiến

Tài liệu này chi tiết hóa lộ trình kỹ thuật và phân rã các tác vụ phát triển thành **từng Prompt thực thi độc lập (Ready-to-Execute Prompts)** để hiện thực hóa toàn diện hệ thống **The Crucible Sanctuary (Game Hub/Hideout)** và **Bộ Đếm DPS Chiến Đấu Thời Gian Thực (In-Combat Real-Time DPS Meter)** theo đặc tả tại [docs/15_game_hub_and_sanctuary.md](15_game_hub_and_sanctuary.md).

---

## 1. NGUYÊN TẮC KIẾN TRÚC & PHÂN TÁCH TRÁCH NHIỆM (ARCHITECTURE CONSTRAINTS)

Tuân thủ nghiêm ngặt các nguyên tắc trong [AGENTS.md](file:///E:/Projects/Antigravity/IdleExile/AGENTS.md):
1. **Phân Tách Tuyệt Đối (Separation of Concerns):**
   * Toàn bộ cấu trúc dữ liệu (`SanctuaryState`, `CombatDpsSnapshot`), thuật toán cửa sổ trượt đo đạc DPS (`CombatDpsTracker`), và dịch vụ nung rác (`SmelterService`) nằm hoàn toàn trong `src/Core/` (100% C# thuần, **không kế thừa hoặc phụ thuộc vào `Godot.Node`**).
   * Dữ liệu các bậc tiến trình của Sanctuary được lưu trữ tại `data/sanctuary_tiers.json` (**không hardcode chỉ số trong C#**).
   * Lớp trình diễn `src/UI/` chỉ nhận dữ liệu / sự kiện từ Core để vẽ 2D Canvas, cập nhật nhãn, và hiển thị thanh đo.
2. **Triết Lý "Zero APM, 100% Theorycrafting":**
   * Sanctuary là nơi an nghỉ an toàn (Safe Haven) và điều phối chiến lược 1-click; không yêu cầu đi bộ tìm NPC.
   * Chuyển đổi trạng thái giữa *Expedition Combat* và *Sanctuary Idle* diễn ra tự động và mượt mà trên cùng một Viewport 2D (`640 x 172` px) ở đáy cửa sổ.
3. **Tuân Thủ Kinh Tế & Chế Tạo Pure PoE (Không Deterministic Mod Crafting):**
   * Tuyệt đối không xây dựng bàn ép mod có công thức định sẵn 100%.
   * Toàn bộ thao tác nâng cấp đồ tại Đe Lò Luyện (**Crucible Anvil**) tuân thủ tỷ lệ tiền tệ ngẫu nhiên (RNG Currency Slamming) và Hóa Thạch Biển Sâu (Fossil Resonator Crafting) kết hợp xem trước **Smart Stat Delta Preview**.
4. **Đo Lường Sát Thương Thực Chiến (Real-Time In-Combat DPS Meter):**
   * Loại bỏ hoàn toàn cọc gỗ tĩnh (Target Dummy) trong Hideout.
   * Toàn bộ chỉ số DPS được tính toán theo thời gian thực trực tiếp trong các nhịp chiến đấu (30Hz) của `CombatEngine` bằng thuật toán Cửa sổ trượt 3.0 giây (Sliding Window), phản ánh chính xác sát thương sau khi trừ Giáp/Kháng và cộng dồn Ailment DoT.
5. **Tiêu Chuẩn Mã Nguồn & Hiệu Năng:**
   * Giao diện và mã nguồn mặc định 100% tiếng Anh, hỗ trợ đa ngôn ngữ qua `Loc.Tr()`.
   * Giữ vững mức tiêu thụ tài nguyên cực thấp (<1% CPU, <150MB RAM).

---

## 2. LỘ TRÌNH TRIỂN KHAI 5 GIAI ĐOẠN (PHASED WORK BREAKDOWN)

```
┌────────────────────────────────────────────────────────────────────────┐
│ Phase 1: Data Architecture & Core Models (data/ & src/Core/Models/)    │
│   ├── data/sanctuary_tiers.json (Định nghĩa 4 bậc tiến trình Sanctuary)│
│   ├── src/Core/Models/SanctuaryState.cs (Cấp độ Hub, Dust, cấu hình)   │
│   └── src/Core/Models/CombatDpsSnapshot.cs (Struct snapshot DPS)       │
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│ Phase 2: In-Combat Real-Time DPS Meter Engine (src/Core/Simulation/)   │
│   ├── src/Core/Simulation/CombatDpsTracker.cs (Sliding Window 3.0s)    │
│   └── Tích hợp vào CombatEngine.cs (Ghi nhận Hit/DoT damage, Peak DPS) │
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│ Phase 3: Thaumaturgic Smelter & Dust Economy (src/Core/Services/)      │
│   ├── src/Core/Services/SmelterService.cs (Thu gom item bị filter ẩn)  │
│   └── Quy đổi trang bị rác ra Thaumaturgic Dust & buff Quantity Map   │
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│ Phase 4: Dual-State Viewport Presentation (src/UI/)                    │
│   ├── Mở rộng Arena2D.cs: Hỗ trợ ViewportState.Combat vs Sanctuary     │
│   ├── Vẽ bối cảnh phòng đá cổ Crucible & đốm lửa Eldritch bập bùng     │
│   └── Hiển thị chỉ số Real-Time DPS trên thanh Quick Footer/Telemetry  │
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│ Phase 5: Sanctuary Workspace UI & In-Window Hotkeys (src/UI/Components)│
│   ├── Tạo scenes/SanctuaryView.tscn & src/UI/Components/SanctuaryView.cs│
│   ├── The Crucible Anvil (Bàn thao tác Currency/Fossils + Stat Delta)  │
│   ├── Tích hợp Tab [ 🏛 SANCTUARY ] vào Main.cs                        │
│   └── Gán phím tắt cục bộ: H (Hub/Sanctuary), A (Anvil), Space (Launch)│
└────────────────────────────────────────────────────────────────────────┘
```

---

## 3. DANH SÁCH CÁC PROMPT CHI TIẾT SẴN SÀNG THỰC THI (READY-TO-EXECUTE PROMPTS)

---

### PROMPT 1: DỮ LIỆU TIẾN TRÌNH SANCTUARY & CẤU TRÚC MÔ HÌNH CORE

```markdown
### Mục tiêu:
Thiết lập schema dữ liệu JSON và các C# Record/Class đại diện cho trạng thái của The Crucible Sanctuary và cấu trúc đo đạc Combat DPS trong Core logic thuần túy.

### Ngữ cảnh & Tập tin tác động:
- Target File 1: `data/sanctuary_tiers.json` (Mới)
- Target File 2: `src/Core/Models/SanctuaryState.cs` (Mới)
- Target File 3: `src/Core/Models/CombatDpsSnapshot.cs` (Mới)
- Reference: `src/Core/Services/DatabaseLoader.cs`

### Nhiệm vụ chi tiết:
1. Tạo tập tin `data/sanctuary_tiers.json` cấu hình 4 bậc tiến trình:
   - `tier_1`: Name "Hollow Crypt", RequiredMapTier 1, MapSlots 4, QueueSlots 10, SmelterUnlocked false, AbyssUnlocked false.
   - `tier_2`: Name "Awakened Foundry", RequiredMapTier 6, MapSlots 4, QueueSlots 10, SmelterUnlocked true, AbyssUnlocked true.
   - `tier_3`: Name "Eldritch Sanctum", RequiredMapTier 11, MapSlots 4, QueueSlots 15, SmelterUnlocked true, AbyssUnlocked true, SmelterSpeedBonus 0.5.
   - `tier_4`: Name "Celestial Void", RequiredMapTier 16, MapSlots 5, QueueSlots 15, SmelterUnlocked true, AbyssUnlocked true, SmelterSpeedBonus 1.0, PinnacleTrophiesUnlocked true.

2. Tạo `src/Core/Models/CombatDpsSnapshot.cs`:
   - Định nghĩa `public record CombatDpsSnapshot`:
     - `float CurrentDps`: Sát thương thực tế/giây trong cửa sổ 3.0s.
     - `float PeakDps`: Kỷ lục DPS cao nhất trong Map hiện tại.
     - `float AverageDps`: DPS trung bình toàn bộ phiên chạy Map.
     - `float PhysicalPercent`, `float FirePercent`, `float ColdPercent`, `float LightningPercent`, `float ChaosPercent`: Tỷ lệ % các nguồn sát thương.
     - `float HitRateAps`: Tần suất đánh trúng thực tế.
     - `float AccuracyPercent`: Tỷ lệ đánh trúng.

3. Tạo `src/Core/Models/SanctuaryState.cs`:
   - Lưu trữ:
     - `int CurrentTier`: Bậc hiện tại (1-4).
     - `long ThaumaturgicDust`: Lượng bụi giả kim tích lũy.
     - `bool AutoSmeltActive`: Trạng thái bật/tắt tự động nung đồ lọc.
     - `int InfusedMapsRemaining`: Số map còn lại được hưởng buff Dust.
     - `float InfusedQuantityBonus`: % IIQ gia tăng (ví dụ 0.10f cho +10%).
     - Các sự kiện `Action? OnSanctuaryUpdated`.

4. Cập nhật `DatabaseLoader.cs` để nạp danh sách `SanctuaryTierDefinition` từ `data/sanctuary_tiers.json`.

### Tiêu chuẩn nghiệm thu:
- File JSON hợp lệ RFC 8259, nạp trơn tru qua `DatabaseLoader`.
- Các models nằm trong namespace `IdleExile.Core.Models`, không phụ thuộc `Godot.Node`.
```

---

### PROMPT 2: XÂY DỰNG BỘ ĐẾM COMBAT DPS THỜI GIAN THỰC (SLIDING WINDOW 3.0S)

```markdown
### Mục tiêu:
Xây dựng thuật toán cửa sổ trượt đo đạc sát thương thực chiến thời gian thực trong lõi `CombatEngine` (30Hz), loại bỏ nhu cầu sử dụng cọc gỗ.

### Ngữ cảnh & Tập tin tác động:
- Target File 1: `src/Core/Simulation/CombatDpsTracker.cs` (Mới)
- Target File 2: `src/Core/Simulation/CombatEngine.cs` (Cập nhật tích hợp)
- Reference: `src/Core/Models/CombatDpsSnapshot.cs`, `src/Core/Simulation/CombatAttackService.cs`

### Nhiệm vụ chi tiết:
1. Xây dựng `src/Core/Simulation/CombatDpsTracker.cs`:
   - Sử dụng hàng đợi `Queue<(float Timestamp, float Damage, DamageType Type)>`.
   - Cung cấp phương thức `RecordDamage(float timestamp, float damage, DamageType type)`.
   - Cung cấp phương thức `Update(float currentTime, float delta)`:
     - Tự động dequeue các phần tử có `currentTime - Timestamp > 3.0f`.
     - Tính tổng sát thương trong cửa sổ 3.0s $\rightarrow$ suy ra `CurrentDps`.
     - Cập nhật `PeakDps` nếu `CurrentDps > PeakDps`.
     - Tính toán tỷ lệ phần trăm phân bổ sát thương theo `DamageType` (Physical, Fire, Cold, Lightning, Chaos).
   - Cung cấp phương thức `GetSnapshot()` trả về `CombatDpsSnapshot`.
   - Cung cấp phương thức `ResetForNewMap()` để đặt lại các chỉ số khi bước vào Map mới.

2. Tích hợp vào `CombatEngine.cs`:
   - Khởi tạo `public CombatDpsTracker DpsTracker { get; } = new();`.
   - Trong luồng xử lý tấn công (`CombatAttackService` hoặc nơi quái vật nhận sát thương thực tế `ApplyDamage`):
     - Gọi `DpsTracker.RecordDamage(_elapsedTime, actualDamageTaken, payload.Type)`.
   - Trong `CombatEngine.Update(float delta)`: Gọi `DpsTracker.Update(_elapsedTime, delta)`.
   - Khi gọi `StartMap()`: Gọi `DpsTracker.ResetForNewMap()`.

### Tiêu chuẩn nghiệm thu:
- Logic tính toán chạy thuần C# 30Hz, không phân bổ rác bộ nhớ (Zero Allocation trong Update loop nếu tái sử dụng struct/buffer).
- Khi quái nhận sát thương, `CurrentDps` phản ánh đúng giá trị sát thương trung bình trong 3 giây gần nhất.
```

---

### PROMPT 3: LÒ NUNG THAUMATURGIC SMELTER & TỰ ĐỘNG HÓA DUST ECONOMY

```markdown
### Mục tiêu:
Hiện thực hóa cơ chế Lò Nung Thu Hồi Bụi Giả Kim (Thaumaturgic Smelter) tự động thu gom các trang bị bị bộ lọc Loot Filter giấu đi để phân rã thành tài nguyên Bụi Giả Kim.

### Ngữ cảnh & Tập tin tác động:
- Target File 1: `src/Core/Services/SmelterService.cs` (Mới)
- Reference: `src/Core/Models/SanctuaryState.cs`, `src/Core/Models/EquipmentItem.cs`

### Nhiệm vụ chi tiết:
1. Xây dựng `src/Core/Services/SmelterService.cs`:
   - Phương thức `CalculateDustYield(EquipmentItem item)`:
     - Tính sản lượng Dust dựa trên Item Level, Rarity (Normal: 1x, Magic: 3x, Rare: 10x) và số lượng affixes.
   - Phương thức `ProcessFilteredDrop(EquipmentItem droppedItem, SanctuaryState state)`:
     - Nếu `state.AutoSmeltActive == true`, tự động cộng lượng Dust tính toán được vào `state.ThaumaturgicDust`.
     - Kích hoạt sự kiện `OnItemSmelted(int dustGained, string itemName)`.
   - Phương thức `TryInfuseNextMaps(SanctuaryState state, int cost = 2500, int mapCount = 5, float bonusQuantity = 0.10f)`:
     - Kiểm tra nếu `state.ThaumaturgicDust >= cost`: Trừ Dust, thiết lập `state.InfusedMapsRemaining = mapCount` và `state.InfusedQuantityBonus = bonusQuantity`. Trả về `true`.
   - Phương thức `TryTransmuteToCurrency(SanctuaryState state, string currencyId, int cost)`:
     - Cho phép đổi 10,000 Dust lấy 1 *Orb of Unmaking* hoặc 1 *Chaos Orb*.

2. Tích hợp hook với hệ thống Loot Drop:
   - Khi quái chết sinh ra vật phẩm không vượt qua tiêu chuẩn Loot Filter hiển thị, tự động chuyển món đồ đó qua `SmelterService.ProcessFilteredDrop()`.

### Tiêu chuẩn nghiệm thu:
- Quản lý logic Dust 100% độc lập, hỗ trợ lưu/nạp vào Save Data của người chơi.
```

---

### PROMPT 4: TRÌNH DIỄN DUAL-STATE VIEWPORT TRÊN ARENA 2D & HIỂN THỊ COMBAT DPS

```markdown
### Mục tiêu:
Cập nhật `Arena2D.cs` và `BattleViewport.cs` để hỗ trợ 2 trạng thái sàn đấu (Expedition vs Sanctuary) và hiển thị Bộ Đếm DPS thực chiến trên giao diện.

### Ngữ cảnh & Tập tin tác động:
- Target File 1: `src/UI/Components/Arena2D.cs`
- Target File 2: `src/UI/BattleViewport.cs`
- Reference: `src/Core/Models/CombatDpsSnapshot.cs`, `docs/15_game_hub_and_sanctuary.md`

### Nhiệm vụ chi tiết:
1. Mở rộng `Arena2D.cs`:
   - Thêm enum `public enum ViewportMode { ExpeditionCombat, SanctuaryIdle }`.
   - Thuộc tính `public ViewportMode CurrentMode { get; set; } = ViewportMode.SanctuaryIdle;`.
   - Khi ở `ViewportMode.SanctuaryIdle`:
     - Ẩn toàn bộ quái vật, đường đạn, hiệu ứng máu me.
     - Vẽ phòng đá cổ Crucible: Mặt sàn đá hoa cương tĩnh lặng, cỗ máy Map Device xoay nhẹ ở tọa độ giữa $(X = 320, Y = 86)$, ngọn lửa trại Eldritch với than hồng bay lơ lửng.
     - Exile đứng ở tư thế nghỉ ngơi/thiền định tại tọa độ $(X = 240, Y = 86)$, Aura thu nhỏ dịu nhẹ.
   - Khi ở `ViewportMode.ExpeditionCombat`: Render bình thường như hiện tại.

2. Cập nhật `BattleViewport.cs`:
   - Nhận dữ liệu từ `CombatEngine.DpsTracker.GetSnapshot()`.
   - Hiển thị trên thanh Quick Footer:
     - Nhãn `DPS: {snapshot.CurrentDps:N0} • Peak: {snapshot.PeakDps:N0}`.
   - Tạo Tooltip chẩn đoán khi hover vào nhãn DPS:
     - Hiển thị tỷ lệ phần trăm phân bổ 5 nguồn sát thương (Phys, Fire, Cold, Lightning, Chaos) và tần suất đòn đánh (APS).
   - Khi ở trạng thái Sanctuary: Cập nhật nhãn trạng thái thành `Crucible Online • Sanctuary Safe Haven (100% Recovered)`.

### Tiêu chuẩn nghiệm thu:
- Chuyển đổi qua lại giữa Expedition và Sanctuary mượt mà, không giật lag.
- Nhãn DPS cập nhật trơn tru mỗi nhịp mà không làm giảm tốc độ khung hình.
```

---

### PROMPT 5: XÂY DỰNG GIAO DIỆN SANCTUARY VIEW & THE CRUCIBLE ANVIL WORKSPACE

```markdown
### Mục tiêu:
Xây dựng Scene và Script cho Tab `[ 🏛 SANCTUARY ]`, bao gồm bàn thao tác đập đồ thuần túy **The Crucible Anvil** (không có Deterministic Crafting) kèm tính năng **Smart Stat Delta Preview**.

### Ngữ cảnh & Tập tin tác động:
- Target File 1: `scenes/SanctuaryView.tscn` (Mới)
- Target File 2: `src/UI/Components/SanctuaryView.cs` (Mới)
- Target File 3: `src/UI/Main.cs` (Tích hợp Tab và Hotkeys)
- Reference: `src/UI/Components/EquipmentView.cs`, `src/Core/Services/EquipmentComparisonService.cs`

### Nhiệm vụ chi tiết:
1. Tạo `scenes/SanctuaryView.tscn` và `SanctuaryView.cs`:
   - Bố cục lưới điều hành gồm 4 bảng điều khiển:
     - **The Crucible Anvil (Bảng trái trên):**
       - Khay đặt 1 món trang bị mục tiêu.
       - Hàng nút Currency nhanh (*Transmute, Alteration, Chaos, Exalt, Annul, Divine, Vaal*).
       - Khay Hóa Thạch (Fossils & Resonators): Chọn 1-4 Hóa Thạch để điều chỉnh trọng số trước khi đập.
       - **Smart Stat Delta Preview:** So sánh trước/sau khi đập đồ (màu xanh cho chỉ số tăng, đỏ cho chỉ số giảm).
       - **KHÔNG hiển thị bất kỳ danh sách công thức ép mod cố định nào**.
     - **Thaumaturgic Smelter (Bảng phải trên):**
       - Hiển thị lượng Thaumaturgic Dust hiện có.
       - Checkbox `[✓] Auto-Smelt Filtered Items`.
       - Nút `[Infuse 5 Maps (+10% IIQ)]` và nút đổi Dust lấy Currency.
     - **The Abyssal Resonator (Bảng trái dưới):**
       - Hiển thị thanh trữ lượng Voltaxic Sulphite và độ sâu kỷ lục.
       - Nút 1-click `[Descend to Endless Abyss]`.
     - **Atlas Relics & Hall of Fame (Bảng phải dưới):**
       - Hiển thị 4 viên Voidstones và trạng thái hạ gục các Pinnacle Bosses.

2. Tích hợp vào `Main.cs`:
   - Thêm nút tab `[ 🏛 SANCTUARY ]` trên thanh Tab Bar bản lề.
   - Xử lý phím tắt cục bộ (In-Window Hotkeys):
     - `H`: Chuyển đổi nhanh sang Tab Sanctuary.
     - `A`: Mở trực tiếp The Crucible Anvil trong Sanctuary.
     - `Space`: Khi đang ở Sanctuary, kích hoạt khởi chạy bản đồ tiếp theo trong Map Queue.

### Tiêu chuẩn nghiệm thu:
- Giao diện hiển thị sắc nét ở kích thước Expanded `640 x 780` px.
- Thao tác đập đồ Currency và Fossil hoạt động trơn tru với phản hồi so sánh chỉ số rõ ràng.
```

---

## 4. KẾT LUẬN & HƯỚNG DẪN THỰC THI

Kế hoạch này phân định ranh giới kỹ thuật rành mạch:
* **Core Logic:** `CombatDpsTracker` và `SmelterService` hoàn toàn độc lập, có thể kiểm thử tự động (Unit Testable).
* **UI/UX:** Tận dụng 100% Viewport hiện hữu với hai trạng thái liền mạch, tôn trọng nguyên tắc Zero APM và form factor Desktop Companion.
* **Quy chuẩn thực thi:** Lần lượt chạy từ **Prompt 1 $\rightarrow$ Prompt 5** để hoàn tất tính năng.
