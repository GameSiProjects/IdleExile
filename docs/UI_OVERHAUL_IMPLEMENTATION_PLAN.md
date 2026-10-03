# UI_OVERHAUL_IMPLEMENTATION_PLAN.md — Kế Hoạch Triển Khai Giao Diện Mới (UI Overhaul Roadmap)

Tài liệu này xác lập **Lộ trình triển khai chi tiết (Actionable Execution Plan)** cho đợt nâng cấp toàn diện giao diện người dùng (**UI Layout & Viewport Overhaul**) của **Idle Exile**, hiện thực hóa kiến trúc **Dynamic Resizable Desktop Companion** kết hợp **Persistent Combat Viewport & Tabbed Workspace** theo chuẩn thiết kế tại [docs/07_combat_ui_and_viewport.md](file:///e:/Projects/Antigravity/IdleExile/docs/07_combat_ui_and_viewport.md) và [docs/06_godot_technical_spec.md](file:///e:/Projects/Antigravity/IdleExile/docs/06_godot_technical_spec.md).

---

## 1. Mục Tiêu Chiến Lược Của UI Mới (Core Objectives)

1. **Khắc phục triệt để "Cái chết của Idle ARPG":**
   * Trong UI cũ, khi mở bảng trang bị thì sàn đấu bị ẩn hoàn toàn (`_battleViewport.Visible = false`).
   * UI mới đảm bảo **Combat Viewport luôn luôn hiển thị (Persistent & Non-Blocking)** ở nửa trên màn hình. Người chơi vừa thay đồ, cắm gem hay cộng điểm passive vẫn nhìn thấy rõ quái chết, đạn bay, máu tụt và đồ rớt rực rỡ.
2. **Triệt tiêu trùng lặp & Rác thị giác:**
   * Hợp nhất thông tin bản đồ: Bỏ thanh `MapDeviceBar` rời rạc ở đáy, tích hợp tinh gọn vào `MapHeaderBar` của Viewport và đưa khay kích hoạt map vào `Tab Atlas`.
   * Gỡ bỏ các card debug tĩnh (`StatusCard`) khỏi giao diện người dùng cuối, chuyển vào phím tắt chẩn đoán (`F3` hoặc `#if DEBUG`).
3. **Tương thích tuyệt đối cơ chế Dynamic Window (`keepBottom = true`):**
   * **Compact Mode (`440 x 380` px):** Tối ưu cho chế độ rảnh tay, chỉ hiển thị Combat Viewport và thanh phím tắt thoát hiểm khẩn cấp.
   * **Expanded Mode (`440 x 780` px):** Mở rộng upward hiển thị khay Sub-Tabs tương tác bên dưới mà chân cửa sổ vẫn dính chặt trên thanh Taskbar.
   * **Wide Mode (`800 x 780` px):** Tự động chuyển layout đa cột (Side-by-side) khi người dùng mở rộng bề ngang.

---

## 2. Ma Trận Ngân Sách Pixel & Phân Bố Không Gian (Pixel Budget Allocation)

### 2.1. Chế Độ Expanded Mode (`440 x 780` px) — Chiều Cao 780px Chuẩn
```text
┌─────────────────────────────────────────────────────────┐  Y: 0
│ [IE] IDLE EXILE  Lv.72 Juggernaut     [Pin] [⤡ Min] [X] │  Header Bar: 36px
├─────────────────────────────────────────────────────────┤  Y: 36
│ [MAP BANNER] T14 Strand Map • Rare (+68%) [████░░ 68%] ☠│  Map Header: 32px
│                                                         │
│        2D TOP-DOWN ARENA (Horizontal Left-to-Right)     │  Arena Viewport: 180px
│        [EXILE] ═══════► Tầm nhìn 240px ═══════► [MOBS]  │
│                                                         │
│ [LIFE / ES] 3450/3450 HP   [1][2][3][4][5]   MANA: 82/820│  Vitometer: 48px
├─────────────────────────────────────────────────────────┤  Y: 296
│ [ 🛡 GEAR ]  [ 💎 GEMS ]  [ 🗺 ATLAS ]  [ 🌳 TREE ]     │  Sub-Tab Strip: 34px
├─────────────────────────────────────────────────────────┤  Y: 330
│                                                         │
│                 INTERACTIVE TAB CONTENT                 │
│                                                         │
│   • Tab Gear: 10 ô trang bị (170px) + Smart Delta (150px)│  Active Tab Workspace:
│   • Tab Gems: 1 Active Gem + 5 Support Slots            │  410px
│   • Tab Atlas: Khay Map Device & Map Queue 10 slots     │
│   • Tab Tree: Constellation Clusters & Search Filter    │
│                                                         │
├─────────────────────────────────────────────────────────┤  Y: 740
│ [🌀 EMERGENCY PORTAL (Space)]      DPS: 142.5K | Auto-Run│  Bottom Macro Bar: 40px
└─────────────────────────────────────────────────────────┘  Y: 780
```

### 2.2. Chế Độ Compact Mode (`440 x 380` px) — Treo Máy Tinh Gọn
Khi thu nhỏ, khu vực `Interactive Tab Content` tự động ẩn, cửa sổ co lại xuống phía dưới:
* **Header Bar:** 36px
* **Persistent Combat Viewport:** 260px (Map Header 32px + Arena 180px + Vitometer 48px)
* **Bottom Macro Bar:** 44px (`[🌀 Emergency Portal]` + `[⤢ Expand]` + `[🛡 Gear]`)
* **Tổng cộng:** `36 + 260 + 44 = 340 ~ 380 px`.

---

## 3. Lộ Trình 6 Giai Đoạn Triển Khai (6-Phase Execution Roadmap)

```
[Phase 1: Shell Re-architecture] ──► [Phase 2: Tab Strip & Equipment] ──► [Phase 3: PoE 2 Gem View]
                                                                                   │
                                                                                   ▼
[Phase 6: Polish & Dopamine FX] ◄── [Phase 5: Passive Tree View] ◄── [Phase 4: Atlas & Map Queue]
```

---

### 🟡 Phase 1: Tái Cấu Trúc Khung Khung Gốc (`Main.tscn` & Scene Shell)
> **Mục tiêu:** Xây dựng khung xương UI 5 tầng cố định, loại bỏ debug card rườm rà, tích hợp mượt mà với `WindowController`.

- [x] **Task 1.1: Tái cấu trúc VBox chính trong `scenes/Main.tscn`**
  - Chuyển `MainLayout` thành 5 khu vực phân tầng:
    1. `TopHeaderBar` (36px): Title, Level/Class, Nút AlwaysOnTop Pin, Nút Resize `[⤡ Min]/[⤢ Expand]`, Nút Multi-Monitor `[🖥]`.
    2. `CombatViewportContainer` (260px): Cố định luôn hiển thị, bọc `BattleViewport`.
    3. `SubTabBarContainer` (34px): Thanh điều hướng tab phong cách Segmented Bar tối giản.
    4. `TabWorkspaceContainer` (Size flags: Vertical Expand): Vùng nạp scene các tab tương tác.
    5. `BottomMacroBar` (40px): Nút Emergency Portal màu xanh dạ quang và Telemetry tóm tắt.
- [x] **Task 1.2: Triệt tiêu Card Debug (`StatusCard`)**
  - Đưa thông tin đếm Database (`Db.Affixes.Count...`) vào cửa sổ chẩn đoán (`DiagnosticModal.tscn` / `F12`) và tooltip nhãn nhỏ dưới footer.
  - Trả lại 100% diện tích chiều dọc cho gameplay và tab workspace.
  - Tuân thủ nghiêm ngặt Debug Code Policy (AGENTS.md Section 4.4): Gated `#if DEBUG` và `OS.IsDebugBuild()`, bảo đảm zero overhead ở Release builds.
- [x] **Task 1.3: Cập nhật `Main.cs` điều khiển hiển thị theo Mode**
  - Bắt tín hiệu `WindowController.OnWindowResized`:
    - Nếu là `Compact`: Ẩn `SubTabBarContainer` và `TabWorkspaceContainer`.
    - Nếu là `Expanded`: Hiện đầy đủ và phục hồi tab gần nhất.

---

### 🟢 Phase 2: Bố Cục Lại Bảng Trang Bị 10 Ô Vừa Vặn 410px (`EquipmentView.tscn`)
> **Mục tiêu:** Thu gọn `EquipmentView` từ chiều cao 680px xuống tối đa 410px để vừa khít trong không gian tab mà không bị cuộn trang khó chịu.

- [x] **Task 2.1: Tối ưu kích thước Doll Người Giấy (10 Slots)**
  - Thu gọn độ cao cụm Doll từ 230px xuống **170px**:
    - Hàng 1 (Nón): 52x30px
    - Hàng 2 (Vũ khí 1 - Thân áo - Vũ khí 2): 52x30px
    - Hàng 3 (Găng - Giày): 52x28px
    - Hàng 4 (Dây chuyền - Nhẫn 1 - Nhẫn 2 - Thắt lưng): 46x26px
  - Cụm Survival Benchmark (EHP, Armour, Evasion, Resists) đặt song song bên phải Doll.
- [x] **Task 2.2: Thiết kế Smart Tooltip Delta dạng Sticky Drawer (140px)**
  - Đặt ngay bên dưới Doll, hiển thị trực tiếp đồ đang chọn:
    - 2 dòng chỉ số quyết định: **▲/▼ EHP** và **▲/▼ DPS** (Font to bản màu xanh lá/đỏ).
    - Danh sách Affixes hiển thị dạng cuộn mượt hoặc tóm tắt các dòng T1/T2.
- [ ] **Task 2.3: Thanh Action Buttons & Candidate Drawer Mini (60px)**
  - Hàng nút bấm 1-Click: `[Mặc Ngay]` (Xanh lục), `[Khóa Đồ]` (Vàng hổ phách), `[Cất Rương]` (Xanh lam).
  - Khay Candidate Drawer nằm ngang cuộn êm các đồ nhặt được cùng phân loại slot để so sánh nhanh trong 1.5 giây.

---

### 🔵 Phase 3: Hiện Thực Hóa Tab Ngọc Kỹ Năng PoE 2 (`scenes/GemView.tscn`)
> **Mục tiêu:** Tạo giao diện khay ngọc kỹ năng độc lập (PoE 2 Model): Trang bị không có socket; Active Gem chứa tối đa 5 khe Support tự động liên kết.

- [ ] **Task 3.1: Xây dựng Scene `GemView.tscn` và Controller `GemView.cs`**
  - Trung tâm: Khay cắm **Active Skill Gem** to bản (hiển thị icon nguyên tố: Đỏ/Lửa, Xanh/Băng, Vàng/Lôi, Xanh lá/Vật lý).
  - Vòng cung 5 khe cắm **Support Gems** liên kết tự động (Auto-Linked sockets) tỏa tròn xung quanh Active Gem.
- [ ] **Task 3.2: Bộ Thẩm Định Tương Thích Ngọc (Gem Tag Matching Validator)**
  - Khi hover hoặc chọn Support Gem, tự động làm sáng các Active Gem có tag tương thích (ví dụ: *Support Melee Physical Damage* chỉ sáng khi cắm cùng *Cleave*).
  - Hiển thị trực tiếp hệ số nhân sát thương tổng hợp: $\prod (1 + \text{More Damage})$.

---

### 🟣 Phase 4: Tab Bản Đồ & Hàng Đợi Tự Động (`scenes/AtlasView.tscn`)
> **Mục tiêu:** Hợp nhất Cỗ máy bản đồ (Map Device), danh sách Map Tiers (T1–T16+) và khay hàng đợi tự động chạy map (Map Queue 10 slots).

- [ ] **Task 4.1: Xây dựng Scene `AtlasView.tscn` và Controller `AtlasView.cs`**
  - Khay kích hoạt Map Device ở trung tâm với vòng xoay cổ ngữ và nút `[⚔ KÍCH HOẠT BẢN ĐỒ]`.
  - Hiển thị đầy đủ Map Modifiers (Quantity, Pack Size, Mod nguy hiểm Reflect/-Max Res).
- [ ] **Task 4.2: Tích hợp Hàng Đợi Map Queue (10 Slots - Task 5.2 GDD)**
  - Khay 10 ô chứa sẵn bản đồ đã roll affixes.
  - Tự động kích hoạt map tiếp theo sau 3 giây delay khi hoàn thành map cũ, mang lại trải nghiệm **True Zero-APM Endless Mapping**.

---

### 🟠 Phase 5: Tab Cây Nội Tại Thu Nhỏ & Rương Phân Loại (`PassiveTreeView`, `StashView`)
> **Mục tiêu:** Mang lại khả năng theorycrafting chuyên sâu trên khung nhìn hẹp 440px.

- [ ] **Task 5.1: Tab Cây Nội Tại Thu Nhỏ (`PassiveTreeView.tscn`)**
  - Hệ thống chòm sao nội tại (Constellation Clusters) có thể Zoom & Drag.
  - Thanh tìm kiếm thông minh (Search Bar): Gõ `"Life"`, `"Damage"`, `"Resist"` tự động highlight các nhánh phù hợp và đề xuất lộ trình nâng điểm ngắn nhất.
- [ ] **Task 5.2: Tab Rương Đồ Chuyên Dụng (Affinity Stash View)**
  - Phân loại 4 ngăn tủ chuyên biệt: *Currency Tab, Gear Tab, Map Tab, Fossil Tab*.
  - Bộ nút chuyển đồ nhanh 1-Click: Tự động dọn sạch hòm đồ sau mỗi chuyến map.

---

### ⚡ Phase 6: Đánh Bóng Hiệu Ứng Thị Giác & Trải Nghiệm Xúc Giác (Polish & Dopamine FX)
> **Mục tiêu:** Đạt chuẩn thẩm mỹ AAA cho desktop companion app.

- [ ] **Task 6.1: Hiệu ứng chuyển Tab mượt mà (Micro-Animations)**
  - Chuyển tab mượt mà bằng hiệu ứng Fade In / Slide ngang nhẹ trong 0.15s, không gây giật lag.
- [ ] **Task 6.2: Viền Hào Quang Thông Báo (Edge Glow Alerts)**
  - Khi rớt đồ Tier 1 (Divine/Exalt/6-Link): Viền trên của cửa sổ nhấp nháy ánh vàng hoàng kim kèm âm thanh "Ting!" đanh thép.
  - Khi thăng cấp (Level Up) hoặc có điểm Passive chưa cộng: Icon Tab Tree phát sáng nhẹ nhàng thôi thúc người chơi mở tab.
- [ ] **Task 6.3: Phím Tắt Tiện Lợi (Global Hotkeys)**
  - `Space`: Kích hoạt Emergency Portal tẩu thoát ngay lập tức.
  - `G`: Mở nhanh Tab Gear.
  - `M`: Mở nhanh Tab Map Device / Atlas.
  - `P`: Mở nhanh Tab Passive Tree.
  - `F11`: Chuyển đổi nhanh Compact Mode $\leftrightarrow$ Expanded Mode.

---

## 4. Tiêu Chí Nghiệm Thu Chất Lượng (Quality Gates)

| Tiêu Chí | Ngưỡng Yêu Cầu |
| :--- | :--- |
| **Không Che Sàn Đấu** | Combat Viewport duy trì 100% hiển thị ở mọi Tab trong Expanded Mode. |
| **Hiệu Năng & CPU** | Tiêu thụ < 1.0% CPU trên Windows ở chế độ treo nền (Low Processor Mode). |
| **Bộ Nhớ RAM** | Tổng dung lượng RAM duy trì ổn định dưới 150MB. |
| **Độ Sạch Bản Build** | Tuyệt đối 0 cảnh báo (0 Warnings), 0 lỗi (0 Errors) khi chạy `dotnet build`. |
| **Kiểm Thử Tự Động** | Vượt qua 100% các bài Headless Unit Tests cho cả Logic và UI Controller. |
