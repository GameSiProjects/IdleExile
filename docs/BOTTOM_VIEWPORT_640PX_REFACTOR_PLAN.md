# BOTTOM_VIEWPORT_640PX_REFACTOR_PLAN.md — Kế Hoạch Triển Khai Kiến Trúc 640px Sàn Đấu Cố Định Ở Đáy

Tài liệu này xác lập **Kế hoạch hành động chi tiết (Actionable Execution Plan)** được chia nhỏ thành **từng Prompt độc lập, tuần tự (Prompt-by-Prompt Execution)** nhằm tái cấu trúc giao diện **Idle Exile** sang chuẩn **Bề ngang 640px (1/3 Màn hình Full HD) kết hợp Sàn đấu 2D cố định dưới đáy (Bottom-Anchored Persistent Viewport)** theo thảo luận Game Design tại [docs/07_combat_ui_and_viewport.md](file:///e:/Projects/Antigravity/IdleExile/docs/07_combat_ui_and_viewport.md) và [docs/06_godot_technical_spec.md](file:///e:/Projects/Antigravity/IdleExile/docs/06_godot_technical_spec.md).

---

## 1. TỔNG QUAN CHIẾN LƯỢC & MỤC TIÊU THIẾT KẾ

### 1.1. Hai Trụ Cột Thay Đổi Cốt Lõi
1. **Bề Ngang 640px — Tỉ Lệ Vàng Desktop Companion:**
   * Chiếm chính xác $1/3$ chiều ngang màn hình Full HD ($1920 / 3 = 640\text{px}$).
   * Tương thích hoàn hảo tính năng Windows 11 Snap Assist ($1/3$ game, $2/3$ không gian làm việc $1280\text{px}$ chuẩn 720p).
   * Tạo **Forward Buffer 450px** phía trước mặt nhân vật, triệt tiêu 100% hiện tượng quái nhảy bổ vào mặt (Pop-in) và đạn bắn từ ngoài màn hình (Off-screen projectiles).
2. **Cố Định Sàn Đấu Dưới Đáy (Bottom-Anchored Viewport & Upward Drawer):**
   * Sàn đấu 2D và thanh Máu/Mana luôn nằm **cố định 100% tại một vị trí vật lý duy nhất sát thanh Windows Taskbar** trong cả Compact Mode và Expanded Mode.
   * Triệt tiêu hoàn toàn hiện tượng sàn đấu bị "nhảy vị trí" lên cao 400px khi mở menu (Visual Anchor Invariance).
   * Khi mở các Tab (Gear, Gems, Atlas, Tree, Stash), menu trượt mở lên trên giống như một chiếc ngăn kéo (Dashboard Drawer).

---

## 2. MA TRẬN PHÂN BỔ PIXEL & KHUNG HÌNH (PIXEL BUDGET SPECIFICATION)

### 2.1. Compact Mode (`640 x 320` px) — Treo Máy Tinh Gọn Sát Taskbar
```text
┌─────────────────────────────────────────────────────────────────┐  Y: 0
│ [IE] IDLE EXILE  Lv.72 Juggernaut     [Pin] [⤢ Expand] [🖥] [X] │  Compact Header: 32px
├─────────────────────────────────────────────────────────────────┤  Y: 32
│ [MAP] T14 Strand Map • Rare (+68% IIQ)   [████████░░░░░░ 68%] ☠ │  Map Meta & Progress: 28px
│                                                                 │
│            2D TOP-DOWN COMBAT ARENA (640 x 176 px)              │
│   ◄── 190px ──► [EXILE] ├──────────────── 450px ──────────────► │  Horizontal Corridor:
│                        │       *CRIT* 14.8k                     │  176px
│   [Aura 360°]          │  ══►  ══►  ══►     [Trash] [Rare Boss] │  (Forward buffer 450px)
│                                                                 │
│ [LIFE / ES] 3,450 / 3,450 HP        [1][2][3][4][5]   MANA: 820 │  Vitometer: 44px
├─────────────────────────────────────────────────────────────────┤  Y: 280
│ [🌀 EMERGENCY PORTAL (Space)]           DPS: 142.5K | [🛡 Gear] │  Quick Footer: 40px
└─────────────────────────────────────────────────────────────────┘  Y: 320 (Sát Taskbar)
```

### 2.2. Expanded Mode (`640 x 780` px) — Ngăn Kéo Theorycrafting Nở Lên Trên
```text
┌─────────────────────────────────────────────────────────────────┐  Y: 0
│ [IE] IDLE EXILE  Lv.72 Juggernaut        [Pin] [⤡ Min] [🖥] [X] │  Top Header: 36px
├─────────────────────────────────────────────────────────────────┤  Y: 36
│                                                                 │
│                 INTERACTIVE TAB WORKSPACE                       │
│                                                                 │
│   • Tab Gear: Paperdoll 10 ô bên trái (280px) + Stats & Smart   │  Workspace Drawer
│               Delta Tooltip so sánh trực tiếp bên phải (340px)  │  (Nở lên phía trên):
│   • Tab Gems: 1 Active Gem + 5 Support Slots hình cánh cung     │  424px
│   • Tab Atlas: Khay Map Device & Map Queue 10 slots             │
│   • Tab Passives: Cây nội tại 55+ nodes rộng rãi, bao quát      │
│   • Tab Stash: 4 ngăn rương chuyên dụng (Currency/Gear/Maps)   │
│                                                                 │
├─────────────────────────────────────────────────────────────────┤  Y: 460
│ [ 🛡 GEAR ]   [ 💎 GEMS ]   [ 🗺 ATLAS ]   [ 🌳 TREE ]   [📦]   │  Sub-Tabs Bar (Bản lề): 36px
├─────────────────────────────────────────────────────────────────┤  Y: 496
│ [MAP BANNER] T14 Strand Map • Rare (+68%)   [████████░░ 68%] ☠   │  Map Header: 32px
│                                                                 │
│               2D TOP-DOWN COMBAT VIEWPORT (640 x 172 px)        │  PERSISTENT VIEWPORT
│   ◄── 190px ──► [EXILE] ├──────────────── 450px ──────────────► │  (CỐ ĐỊNH Ở ĐÁY):
│                        │       *CRIT* 14.8k                     │  172px
│   [Aura 360°]          │  ══►  ══►  ══►     [Trash] [Rare Boss] │
│                                                                 │
│ [LIFE / ES] 3,450 / 3,450 HP        [1][2][3][4][5]   MANA: 820 │  Vitometer: 44px
├─────────────────────────────────────────────────────────────────┤  Y: 740
│ [🌀 EMERGENCY PORTAL (Space)]           DPS: 142.5K | Auto-Run  │  Bottom Macro Bar: 40px
└─────────────────────────────────────────────────────────────────┘  Y: 780 (Sát Taskbar)
```

---

## 3. LỘ TRÌNH 6 PROMPT THI CÔNG TUẦN TỰ (PROMPT-BY-PROMPT ROADMAP)

```
[Prompt 1: Cấu Hình Window & Engine 640px]
                 │
                 ▼
[Prompt 2: Đảo Tầng Scene Tree Main.tscn (Sàn Đấu Xuống Đáy)]
                 │
                 ▼
[Prompt 3: Mở Rộng Kích Thước Viewport & Arena2D Sang 640px]
                 │
                 ▼
[Prompt 4: Cân Chỉnh Camera Look-Ahead (+80px) & Forward Buffer 450px]
                 │
                 ▼
[Prompt 5: Tối Ưu Bố Cục Các Tab Workspace Theo Bề Ngang 640px]
                 │
                 ▼
[Prompt 6: Cập Nhật & Kiểm Thử Toàn Diện Test Suite MainLayoutTests]
```

---

### 🟢 PROMPT 1: Cấu Hình Cửa Sổ Căn Bản & Engine 640px

**Mục tiêu:** Cập nhật các hằng số kích thước cửa sổ hệ thống, cấu hình Godot Engine sang chuẩn bề ngang 640px và bảo đảm thuật toán `keepBottom = true` duy trì đáy cửa sổ sát Taskbar.

* **Các file cần can thiệp:**
  - `project.godot`:
    - `display/window/size/viewport_width = 640`
    - `display/window/size/viewport_height = 780`
    - `display/window/size/min_size = Vector2I(560, 320)`
  - `src/UI/DesktopWindow.cs`:
    - Cập nhật `_window.MinSize = new Vector2I(560, 320);`
  - `src/UI/WindowController.cs`:
    - `CompactWindowSize = new Vector2I(640, 320)`
    - `ExpandedWindowSize = new Vector2I(640, 780)`
    - `WideWindowSize = new Vector2I(960, 780)`
    - `WindowResolutionMode` threshold nhận diện Compact vs Expanded ($Y < 500\text{px}$).

* **Nội dung Prompt yêu cầu AI thực hiện:**
  > *"Hãy cập nhật cấu hình cửa sổ và kích thước engine của Idle Exile sang chuẩn 640px:  
  > 1. Sửa `project.godot` để `viewport_width = 640`, `viewport_height = 780`, `min_size = Vector2i(560, 320)`.  
  > 2. Sửa `DesktopWindow.cs` cập nhật `MinSize` thành `(560, 320)`.  
  > 3. Sửa `WindowController.cs`: Đổi `CompactWindowSize` thành `(640, 320)`, `ExpandedWindowSize` thành `(640, 780)`, `WideWindowSize` thành `(960, 780)`.  
  > 4. Kiểm tra thuật toán `keepBottom = true` bảo đảm khi chuyển giữa 320px và 780px, cạnh đáy cửa sổ luôn dính chặt trên Taskbar.  
  > 5. Chạy `dotnet build` bảo đảm 0 warnings, 0 errors."*

* **Tiêu chí nghiệm thu (Definition of Done):**
  - [x] `dotnet build` biên dịch thành công.
  - [x] `CompactWindowSize` là 640x320, `ExpandedWindowSize` là 640x780.

---

### 🟢 PROMPT 2: Đảo Tầng Scene Tree `Main.tscn` (Đưa Sàn Đấu Xuống Đáy Cố Định)

**Mục tiêu:** Tái cấu trúc thứ tự phân tầng VBox trong `scenes/Main.tscn` và logic điều khiển trong `src/UI/Main.cs` để Sàn đấu (Combat Viewport) cố định ở đáy, Workspace mở lên trên khi Expanded.

* **Các file cần can thiệp:**
  - `scenes/Main.tscn`:
    - Thứ tự các Control trong `MainLayout (VBoxContainer)`:
      1. `TopHeaderBar` (36px): Luôn ở đỉnh cửa sổ.
      2. `TabWorkspaceContainer` (MarginContainer, SizeFlags: Vertical Expand): Nằm ngay dưới TopHeaderBar. Ẩn trong Compact Mode, hiển thị khi Expanded.
      3. `SubTabBarContainer` (36px): Nằm ngay trên Combat Viewport, đóng vai trò bản lề phân cách giữa Workspace và Sàn đấu. Ẩn trong Compact Mode, hiển thị khi Expanded.
      4. `CombatViewportContainer` (Control, 248px): Bọc `BattleViewport`. Luôn hiển thị 100% trong cả 2 mode.
      5. `BottomMacroBar` (40px): Luôn ở đáy cùng sát Taskbar (`[🌀 Emergency Portal]`, DPS).
  - `src/UI/Main.cs`:
    - Cập nhật `OnWindowResized`:
      - Khi `isExpanded == false`: Ẩn `TabWorkspaceContainer` và `SubTabBarContainer`.
      - Khi `isExpanded == true`: Hiện `TabWorkspaceContainer` và `SubTabBarContainer`, khôi phục active tab.
      - `BattleViewport` duy trì `Visible = true` bất biến.
    - Cập nhật hiệu ứng chuyển tab `ApplyTabVisibility`: Cập nhật `GetTabRestingPosition()` phù hợp với vị trí mới của Workspace ở nửa trên cửa sổ.

* **Nội dung Prompt yêu cầu AI thực hiện:**
  > *"Hãy tái cấu trúc Scene Tree `scenes/Main.tscn` và logic hiển thị trong `src/UI/Main.cs` theo kiến trúc Bottom-Anchored Viewport:  
  > 1. Trong `Main.tscn`, sắp xếp lại `MainLayout (VBoxContainer)` theo đúng thứ tự 5 tầng từ trên xuống dưới:  
  >    - Tầng 1: `TopHeaderBar` (36px)  
  >    - Tầng 2: `TabWorkspaceContainer` (V-Expand, chứa các tab Gear/Gems/Atlas/Tree/Stash)  
  >    - Tầng 3: `SubTabBarContainer` (36px, bản lề các nút tab)  
  >    - Tầng 4: `CombatViewportContainer` (248px, bọc Persistent `BattleViewport` cố định ở đáy)  
  >    - Tầng 5: `BottomMacroBar` (40px, nút Emergency Portal sát đáy Taskbar)  
  > 2. Trong `Main.cs`, cập nhật `OnWindowResized` và `ApplyTabVisibility` để khi mở rộng cửa sổ (Expanded), Workspace nở lên ở Tầng 2 mà Sàn đấu Tầng 4 vẫn nằm nguyên vẹn sát mép đáy Taskbar.  
  > 3. Cập nhật vị trí và tween animation trượt tab mượt mà.  
  > 4. Chạy `dotnet build` và kiểm tra không lỗi."*

* **Tiêu chí nghiệm thu:**
  - [x] Sàn đấu nằm ở nửa dưới, các tab nằm ở nửa trên khi mở rộng.
  - [x] Thu nhỏ Compact Mode chỉ còn: Header + Sàn đấu + Macro Bar.
  - [x] `dotnet build` 0 cảnh báo, 0 lỗi.

---

### 🟢 PROMPT 3: Mở Rộng Viewport Chiến Đấu & Arena2D Sang 640px

**Mục tiêu:** Mở rộng toàn bộ khung nhìn sàn đấu 2D từ 440px sang 640px, cập nhật vẽ nền đá, ron gạch, trần hầm ngục, và thanh Vitometer.

* **Các file cần can thiệp:**
  - `scenes/BattleViewport.tscn`:
    - `custom_minimum_size = Vector2(640, 248)`
    - `ArenaSubViewportContainer`: width = 640px, height = 172px.
    - `MapHeaderContainer`: mở rộng bề ngang 640px, thanh Map Progress Bar dài 640px.
    - `VitometerPanel`: HBox trải rộng 640px, phân bố cân đối thanh Máu/ES (trái), 5 Flask slots (giữa), và Mana (phải).
  - `src/UI/BattleViewport.cs`:
    - Hằng số kích thước và căn chỉnh layout theo 640px.
  - `src/UI/Components/Arena2D.cs`:
    - `public const float ArenaWidth = 640f;`
    - `public const float ArenaHeight = 172f;` (hoặc 200f).
    - `CenterPosition = new Vector2(320f, 86f);`
    - Cập nhật thuật toán vẽ sàn đá thủ tục (`DrawCorridorFloor`): sinh số cột gạch theo chiều ngang 640px.
    - Cập nhật viền tối 2 bên trần/sàn (`Vignette`), hiệu ứng hạt bụi (`AmbientDust`) phân bổ trong phạm vi $X \in [0, 640]$.

* **Nội dung Prompt yêu cầu AI thực hiện:**
  > *"Hãy nâng cấp kích thước của `BattleViewport.tscn`, `BattleViewport.cs` và `Arena2D.cs` sang chuẩn bề ngang 640px:  
  > 1. Trong `BattleViewport.tscn`, chỉnh `custom_minimum_size` thành `Vector2(640, 248)`. Điều chỉnh Map Header và Vitometer dàn trải đều trên bề ngang 640px.  
  > 2. Trong `Arena2D.cs`, đổi `ArenaWidth = 640f;` và `ArenaHeight = 172f;`, tâm màn hình `CenterPosition = (320f, 86f)`.  
  > 3. Cập nhật các hàm vẽ đồ họa thủ tục (`DrawCorridorFloor`, `DrawVignette`, `DrawAmbientMotes`) để lấp đầy khung nhìn 640px mượt mà, không bị hụt ron đá hay đứt đoạn hiệu ứng.  
  > 4. Kiểm tra biên dịch `dotnet build` sạch sẽ."*

* **Tiêu chí nghiệm thu:**
  - [ ] Khung sàn đấu hiển thị hoàn hảo ở kích thước 640px mà không bị méo tỷ lệ hay co giật đồ họa.
  - [ ] Vitometer và Map Bar căn lề chuẩn mực.

---

### 🟢 PROMPT 4: Cân Chỉnh Camera Look-Ahead (+80px) & Forward Buffer 450px

**Mục tiêu:** Cài đặt vị trí nhân vật lệch trái 30% ($X \approx 190\text{px}$) bằng Look-ahead offset $+80\text{px}$, tạo khoảng nhìn trước 450px giúp người chơi làm chủ tình huống hoàn toàn khi mapping.

* **Các file cần can thiệp:**
  - `src/UI/Components/ArenaCamera.cs`:
    - Cập nhật `LookAheadOffset = new Vector2(80f, 0f);`
    - Cập nhật thuật toán `WorldToScreen` và `ScreenToWorld` dựa trên `viewportCenter = (320f, 86f)`.
    - Khi ở `ArenaCameraMode.CorridorFollow`: Nhân vật giữ khoảng cách ổn định ở $X_{\text{screen}} \approx 190\text{px}$.
    - Khi ở `ArenaCameraMode.BossArenaLock`: Camera tự động lerp về trung tâm phòng Boss ($X = 320\text{px}$) để bao quát đấu trường 360°.
  - `src/Core/Simulation/ExileMovementController.cs`:
    - `DefaultCorridorWidth = 640f;`
    - Đảm bảo `ForwardDirection = (1f, 0f)` và `PerceptionRange = 260f` tương thích hoàn hảo với tầm nhìn 450px.
  - `src/Core/Simulation/CorridorMonsterLayout.cs`:
    - Đảm bảo các cụm quái tuần tự (Packs 1–4) và League Encounter kích hoạt mượt mà khi người chơi nhìn thấy trước từ xa.

* **Nội dung Prompt yêu cầu AI thực hiện:**
  > *"Hãy cân chỉnh hệ thống Camera và khoảng nhìn Look-Ahead cho hành lang 640px:  
  > 1. Trong `ArenaCamera.cs`, đổi `LookAheadOffset = new Vector2(80f, 0f);` để nhân vật Exile chạy ổn định ở khoảng $X \approx 190\text{px}$ (30% mép trái), tạo ra vùng nhìn trước (Forward Buffer) lên tới 450px.  
  > 2. Đảm bảo chế độ `BossArenaLock` lerp mượt mà về tâm phòng Boss $X = 320\text{px}$.  
  > 3. Cập nhật `ExileMovementController.cs` với `DefaultCorridorWidth = 640f`.  
  > 4. Kiểm tra các phép biến đổi tọa độ `WorldToScreen` và `ScreenToWorld` chuẩn xác cho đạn đạo, hiệu ứng nảy sát thương và vệt sáng rơi đồ.  
  > 5. Chạy `dotnet build` và bảo đảm không có hồi quy."*

* **Tiêu chí nghiệm thu:**
  - [ ] Nhân vật chạy ở vị trí $X \approx 190\text{px}$, quái vật xuất hiện từ mép phải cách 450px.
  - [ ] Không có đạn bắn từ ngoài màn hình; người chơi có 1.5s nhìn rõ cụm quái trước khi giao tranh.

---

### 🟢 PROMPT 5: Tối Ưu Bố Cục Các Tab Workspace Theo Bề Ngang 640px

**Mục tiêu:** Nâng cấp layout của 5 Sub-Tabs bên trong `TabWorkspaceContainer` để tận dụng triệt để không gian 640px, xóa bỏ các ngăn kéo cuộn dọc chật hẹp.

* **Các file cần can thiệp:**
  - `scenes/EquipmentView.tscn` & `src/UI/Components/EquipmentView.cs`:
    - Đặt **Búp bê nhân vật (Paperdoll 10 ô)** bên trái (~280px).
    - Đặt **Survival Benchmarks** và **Smart Tooltip Delta** song song bên phải (~340px) $\rightarrow$ Xem đồ và so sánh tức thì không cần cuộn trang.
    - Thanh **Candidate Drawer** dàn trải ngang 640px phía dưới khay so sánh.
  - `scenes/PassiveTreeView.tscn` & `src/UI/Components/PassiveTreeView.cs`:
    - Mở rộng canvas vẽ cây nội tại lên 640px bề ngang $\rightarrow$ Bao quát 2–3 chòm sao cùng lúc.
  - `scenes/GemView.tscn` & `src/UI/Components/GemView.cs`:
    - Căn giữa khay 1 Active Gem + 5 Support Gems cánh cung với bán kính thoáng đãng trong 640px.
  - `scenes/AtlasView.tscn` & `src/UI/Components/AtlasView.cs`:
    - Dàn trải 10 ô Map Queue thành hàng ngang đẹp mắt không bị tràn viền.
  - `scenes/StashView.tscn` & `src/UI/Components/StashView.cs`:
    - Lưới ô rương 10x6 căn giữa rộng rãi.

* **Nội dung Prompt yêu cầu AI thực hiện:**
  > *"Hãy tối ưu hóa bố cục của các Tab Workspace để khai thác tối đa bề ngang 640px:  
  > 1. Trong `EquipmentView`: Tái cấu trúc thành layout 2 cột song song (Side-by-Side) với Paperdoll 10 ô bên trái (~280px) và Chỉ số Survival + Smart Tooltip Delta so sánh bên phải (~340px).  
  > 2. Trong `PassiveTreeView`: Mở rộng viewport canvas cây nội tại lên 640px, cập nhật tâm căn giữa.  
  > 3. Trong `GemView`: Căn giữa cụm Socket Ring và các thanh chỉ số trên khung 640px.  
  > 4. Trong `AtlasView`: Dàn hàng 10 ô Map Queue thoáng đãng, dễ click.  
  > 5. Trong `StashView`: Mở rộng lưới ô chứa đồ 10x6.  
  > 6. Biên dịch dự án bằng `dotnet build`."*

* **Tiêu chí nghiệm thu:**
  - [ ] Cả 5 Tab tận dụng trọn vẹn bề ngang 640px, không còn cảm giác nhồi nhét.
  - [ ] Trang bị có thể so sánh trực tiếp song song với đồ đang mặc.

---

### 🟢 PROMPT 6: Cập Nhật & Kiểm Thử Toàn Diện Test Suite (`MainLayoutTests.cs`)

**Mục tiêu:** Cập nhật 100% các bài Unit Test và Integration Tests tự động trong `src/UI/MainLayoutTests.cs` để xác nhận tính toàn vẹn của chuẩn 640px và sàn đấu cố định đáy.

* **Các file cần can thiệp:**
  - `src/UI/MainLayoutTests.cs`:
    - Cập nhật các hằng số kiểm thử: `CompactWindowSize = (640, 320)`, `ExpandedWindowSize = (640, 780)`.
    - Kiểm thử thứ tự phân tầng VBox trong `MainLayout`: `TopHeaderBar` $\rightarrow$ `TabWorkspaceContainer` $\rightarrow$ `SubTabBarContainer` $\rightarrow$ `CombatViewportContainer` $\rightarrow$ `BottomMacroBar`.
    - Kiểm thử tính bất biến của sàn đấu: `BattleViewport.Visible == true` ở cả Compact và 5 Tab Expanded.
    - Kiểm thử thuật toán `keepBottom = true` giữ nguyên cạnh đáy Taskbar.
    - Kiểm thử chuyển đổi phím tắt (`Space`, `I`, `P`, `M`, `S`, `Tab`, `Esc`).
    - Kiểm thử tọa độ Look-ahead và Forward buffer.

* **Nội dung Prompt yêu cầu AI thực hiện:**
  > *"Hãy cập nhật và chạy bộ kiểm thử tự động trong `MainLayoutTests.cs` để xác thực toàn bộ thay đổi 640px Bottom-Viewport:  
  > 1. Sửa các test cases kiểm tra kích thước cửa sổ thành 640x320 (Compact) và 640x780 (Expanded).  
  > 2. Bổ sung bài test kiểm tra thứ tự Scene Tree của MainLayout (Workspace ở trên, Sàn đấu ở đáy).  
  > 3. Kiểm tra tính liên tục của BattleViewport (không bị ẩn ở bất kỳ tab nào).  
  > 4. Chạy lệnh kiểm thử headless:  
  >    `& 'E:\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe' --headless --scene 'res://scenes/Main.tscn' --quit-after 50`  
  > 5. Bảo đảm bản build đạt 0 warning, 0 error và toàn bộ test suites đều PASS."*

* **Tiêu chí nghiệm thu:**
  - [ ] 100% tests trong `MainLayoutTests.cs` vượt qua thành công.
  - [ ] Engine khởi động và thoát sạch sẽ với exit code 0.

---

## 4. MA TRẬN THEO DÕI TIẾN ĐỘ THI CÔNG

| Prompt | Tên Hạng Mục | Trạng Thái | File Trọng Tâm |
| :---: | :--- | :---: | :--- |
| **Prompt 1** | Cấu Hình Cửa Sổ & Engine 640px | ✅ Hoàn thành | `project.godot`, `DesktopWindow.cs`, `WindowController.cs` |
| **Prompt 2** | Đảo Tầng Scene Tree `Main.tscn` | ✅ Hoàn thành | `scenes/Main.tscn`, `src/UI/Main.cs` |
| **Prompt 3** | Mở Rộng Viewport & Arena2D 640px | 📋 Sẵn sàng | `BattleViewport.tscn`, `Arena2D.cs` |
| **Prompt 4** | Camera Look-Ahead & Buffer 450px | 📋 Sẵn sàng | `ArenaCamera.cs`, `ExileMovementController.cs` |
| **Prompt 5** | Tối Ưu Bố Cục 5 Sub-Tabs 640px | 📋 Sẵn sàng | `EquipmentView.cs`, `PassiveTreeView.cs`, `StashView.cs` |
| **Prompt 6** | Kiểm Thử Tự Động Toàn Diện | 📋 Sẵn sàng | `src/UI/MainLayoutTests.cs` |

---
*Kế hoạch này là nguồn sự thật duy nhất cho đợt nâng cấp giao diện 640px Bottom-Viewport của Idle Exile.*
