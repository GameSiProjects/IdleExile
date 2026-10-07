# 07. Thiết Kế Giao Diện Chiến Đấu (Combat Interface & Top-Down Viewport)

Tài liệu này đặc tả chi tiết giao diện chiến đấu (**Combat UI / Viewport**) của **Idle Exile**, định dạng chuẩn cho cửa sổ phụ dạng **Dynamic Desktop Companion (`640 x 320` px Compact đến `640 x 780` px Expanded)** phát triển trên **Godot 4 .NET (C#)**.

> **Quyết Định Thiết Kế Trọng Tâm (Senior Game Design Shift):**  
> 1. **Chuẩn Bề Ngang 640px (1/3 Màn Hình Full HD & Windows 11 Snap Assist):** Chiều ngang $640\text{px}$ chiếm chính xác 1/3 chiều rộng màn hình $1920\times 1080$, tương thích hoàn hảo với tính năng Snap 1/3 - 2/3 của Windows 11, để lại $1280\text{px}$ (độ phân giải 720p hoàn chỉnh) cho công việc, IDE và lướt web.
> 2. **Cố Định Sàn Đấu Ở Đáy Cùng (Bottom-Anchored Persistent Viewport):** Sàn đấu 2D và thanh sinh mệnh Vitometer luôn nằm **cố định 100% tại một vị trí vật lý duy nhất sát mép thanh Windows Taskbar** trong cả Compact Mode và Expanded Mode, triệt tiêu hoàn toàn hiện tượng "nhảy sàn đấu" (Visual Anchor Invariance).
> 3. **Bản Đồ Hành Lang Ngang Tuyến Tính (2D Horizontal Linear Corridor & Look-Ahead):** Hành lang trải dài theo trục $+X$ ($W = 640\text{px}$, $H = 200\text{px}$). Nhân vật (Exile) di chuyển thực sự trong thế giới.
> 4. **Trọng Tâm Lệch Trái & Forward Buffer 450px:** Camera ghim Exile tại $X_{\text{screen}} \approx 190\text{px}$ ($30\%$ chiều ngang), tạo khoảng nhìn trước lên tới $450\text{px}$, vượt xa tầm nhận thức quái ($260\text{px}$) và tầm bắn ($180\text{px}$), triệt tiêu hoàn toàn tình trạng quái nhảy bổ vào mặt hay đạn bắn từ ngoài màn hình (Off-screen projectiles).
> 5. **Ngăn Kéo Menu Nở Lên Phía Trên (Upward Drawer Pattern):** Khi bấm mở các Tab (Gear, Gems, Atlas, Tree, Stash), cửa sổ mở rộng ngược lên phía trên ($keepBottom = true$). Toàn bộ menu hiển thị ở nửa trên màn hình, người chơi vừa nghiên cứu build vừa quan sát nhân vật dọn map mượt mà ở đáy.

---

## 1. Triết Lý Thiết Kế & Định Hướng UX/UI

### 1.1. Mục Tiêu Cốt Lõi (Core UX Objectives)
* **Diagnostic Clarity (Chẩn đoán tức thì trong 1 giây):**
  * Người chơi không micro-control nhân vật (Zero APM), nhưng mọi thông tin sinh tử phải hiển thị tức thì khi người chơi liếc nhìn: Lượng EHP (Máu + Energy Shield), Mod quái/Map nguy hiểm, Vòng tròn cảnh báo tử thần (Boss Slam AoE Telegraph), Tiến trình cọc mốc dọc đường, và đồ rơi hiếm có.
* **Visceral Top-Down ARPG Feedback (Phản hồi xúc giác & thị giác chuẩn PoE):**
  * Hành lang Top-Down 2D thỏa mãn: Quái bị đóng băng (*Freeze*) vỡ vụn thành mảnh băng vụn, đòn chí mạng (*Crit*) bật nảy số to kèm vệt sáng vàng kim, hiệu ứng *Bleed* tóe máu trên mặt sàn đá cổ.
  * Đường đạn 360° phản ánh chính xác các gem hỗ trợ: *GMP (bắn chùm 5 tia hình rẽ quạt)*, *Fork (tách đôi khi trúng quái)*, *Pierce (xuyên táo xuyên tâm quái vật)*, *Chain (nảy giật zíc zắc giữa các cụm quái)*.
  * **Dopamine Drop Beams:** Cột sáng rực rỡ chiếu thẳng từ trên trời xuống đúng tọa độ quái vừa ngã gục trong thế giới kèm âm thanh "Ting!" đanh thép khi rớt đồ hiếm qua Loot Filter (Divine, Exalted, 6-Link).
* **Low Resource Footprint (Tối ưu tài nguyên cực hạn):**
  * Tận dụng `gl_compatibility` và `LowProcessorUsageMode` của Godot 4.
  * Tách biệt hoàn toàn giữa `CombatEngine` (tính toán ngầm 30Hz) và Render Viewport (vẽ 2D qua CanvasItem/TileMap nhẹ, hỗ trợ giảm FPS khi minimize).

---

## 2. Bố Cục Không Gian & Phân Tầng Pixel Động (Dynamic Adaptive Layout)

Ứng dụng hỗ trợ chuyển đổi linh hoạt giữa **Chế độ Treo Máy Mini (Compact Mode: 640 x 320 px)** và **Chế độ Nghiên Cứu Đầy Đủ (Expanded Mode: 640 x 780 px)** với thuật toán neo đáy Taskbar (keepBottom = true):

### 2.1. Wireframe Chế Độ Thu Nhỏ: Compact Mode (640 x 320 px)
Cố định sát góc Taskbar, chỉ hiển thị sàn đấu ngang siêu rộng và thanh điều khiển tối thiểu:

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

### 2.2. Wireframe Chế Độ Phóng To: Expanded Mode (640 x 780 px)
Khi bấm `[⤢ Expand]`, `[🛡 Gear]`, hoặc phím tắt `I`/`P`/`M`/`S`, cửa sổ **tự động nở lên phía trên**; **Sàn đấu và Vitometer giữ nguyên vị trí cố định dưới đáy**, toàn bộ Tab Workspace mở ra ở nửa trên:

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

## 3. Chi Tiết 5 Phân Vùng Trong Combat Interface

### 3.1. Map Header & Threat Telemetry (Thanh Bản Đồ & Nguy Cơ)
* **Tên Map & Bậc (Tier):**
  * Màu chữ theo độ hiếm: Trắng (Normal), Xanh lam (Magic), Vàng hoàng kim (Rare), Tím sẫm (Corrupted).
  * Icon Tier badge: White (T1–T5), Yellow (T6–T10), Red (T11–T15), Pinnacle/Void (T16+).
* **Affix Threat Badges (Icon cảnh báo nhanh):**
  * ⚠️ `Reflect Phys / Ele`: Khiên nứt đỏ cảnh báo đòn đánh tự phản sát thương.
  * ❄️ `-Max Res`: Khiên băng chỉ báo tụt trần kháng nguyên tố.
  * 🩸 `No Leech`: Giọt máu gạch chéo cảnh báo build hút máu.
  * Hover chuột để xem danh sách chi tiết toàn bộ Mod của Map kèm `+% IIQ / IIR / Pack Size`.
* **Thanh Tiến Trình Tuyến Tính (Map Progress Bar):**
  * Phản ánh trung thực tỉ lệ quãng đường thực tế mà Exile đã vượt qua: $\text{Progress} = \text{Clamp01}(X_E / L_{\text{boss\_gate}})$.
  * Đánh dấu vị trí các mốc **League Encounter** trên hành lang và biểu tượng **Cổng Boss (Boss Gate)** ở mốc cuối thanh.

---

### 3.2. Sàn Đấu 2D Top-Down & Hành Lang Bản Đồ Tuyến Tính

#### A. Không Gian Hành Lang Thế Giới & Bề Ngang 640px (World-Space Linear Corridor)
* **Góc nhìn:** **2D Top-Down / Angled Bird's-Eye (~60° Tilt)** chuẩn ARPG.
* **Không gian thế giới thực:**
  * Bản đồ là một hành lang trải dài theo trục ngang $+X$ ($X_{\text{world}} \in [0, L_{\text{map}}]$ với $L_{\text{map}} \approx 2400 - 3000\text{px}$), chiều cao hành lang $H_{\text{corridor}} \approx 200\text{px}$, bề rộng khung nhìn $W_{\text{viewport}} = 640\text{px}$.
  * **Tuyệt đối không áp dụng cuộn sàn (No fake floor scrolling / conveyor belt):** Mặt sàn đá cổ, các rãnh tường, vật cản, quái vật và đồ rơi đều được đặt tại **tọa độ thế giới thực ($X_{\text{world}}, Y_{\text{world}}$)**.
* **Môi trường & Không gian thị giác:**
  * Mặt sàn gạch đá hầm ngục (`#13161F` và ron viền `#1C202B`) trải dài liên tục từ trái sang phải.
  * Viền tối tự nhiên 2 bên trần và sàn hành lang (Vignette & Shadow Walls) tạo cảm giác chiều sâu hầm ngục, tăng độ tương phản giúp đọc rõ đạn đạo và vòng cảnh báo.
  * Hiệu ứng tàn tro / đom đóm dạ quang lơ lửng trong không gian thế giới.

#### B. Di Chuyển Thế Giới Thực Của Nhân Vật & Camera2D Forward Buffer 450px
* **Di chuyển thực sự của Nhân vật (Real Character Movement):**
  * Nhân vật (Exile) thay đổi `Position` thực sự trong Scene 2D theo trục $+X$, không đứng chôn chân tại một điểm cố định.
  * Vận tốc di chuyển thực tế (`Movement Speed` $\approx 120 - 220\text{ px/s}$) quyết định tốc độ lướt qua các khoảng cách trống giữa các cụm quái.
* **Máy Trạng Thái Tự Động (Zero-APM Character Controller):**
  1. **Hành quân (Marching / Advancing):** Khi không có quái vật trong tầm nhận thức, Exile chủ động tiến bước dọc theo hướng hành lang sang phải.
  2. **Tiếp cận & Giữ khoảng cách (Engaging / Combat Staging):** Khi phát hiện cụm quái phía trước lọt vào tầm đánh hiệu dụng (35px với Melee, 150-180px với Ranged/Spell), Exile dừng lại hoặc xoay quanh cụm để công kích.
  3. **Tiếp tục hành quân:** Khi dọn sạch toàn bộ quái trong cụm, Exile tiếp tục dấn bước tới cụm tiếp theo.
  4. **Bước vào phòng Boss:** Khi chạm mốc cuối hành lang, Exile bước qua cổng sương mù phong ấn.
* **Hệ Thống Camera2D Bám Theo & Forward Buffer:**
  * `Camera2D` gắn thuật toán Smooth Interpolation (Lerp) bám sát vị trí thực của Exile.
  * Camera áp dụng **Look-ahead Offset lớn ($+80\text{px}$ theo trục $+X$)**, ghim Exile tại $X_{\text{screen}} \approx 190\text{px}$ ($30\%$ chiều ngang).
  * **Tầm nhìn phía trước đạt $450\text{px}$**, vượt xa `PerceptionRange` ($260\text{px}$). Quái vật luôn xuất hiện trọn vẹn trong tầm mắt người chơi trước $1.4 - 1.8\text{ giây}$ trước khi vào tầm giao chiến.
* **Xoay Hướng Tự Động 360° (Auto-Facing & Dynamic Aiming Vector):**
  * Nhân vật tự động tính toán vector hướng tới mục tiêu ưu tiên:
    $$\vec{V}_{\text{target}} = \text{TargetWorldPos} - \text{ExileWorldPos}$$
  * Xoay góc mặt mượt mà về phía mục tiêu đang công kích.
  * Khi xuất chiêu (Cast/Attack), Exile có độ giật lùi nhẹ (Micro-Recoil) ngược chiều vector tấn công tạo lực phản hồi thị giác mạnh mẽ.
#### C. Hình Học Kỹ Năng Đa Chiều (360° Skill Geometry)
Dù hành lang có hướng tiến chính, độ rộng 2D vẫn mở khóa toàn bộ các hình thái kỹ năng trứ danh của *Path of Exile*:
1. **Radial / Nova (360° Tỏa Tròn):**
   * *Righteous Fire (RF):* Vòng lửa thiêu đốt rực rỡ bao quanh Exile ở bán kính $R = 45\text{px}$.
   * *Frost Nova / Shock Nova:* Sóng xung kích băng giá / sấm sét nổ bung thành vòng tròn lan tỏa từ tâm ra ngoài mép sàn đá.
2. **Cones & Sweeps (Hình Nón & Quạt Quét):**
   * *Cleave / Ground Slam / Sunder:* Sóng chấn động mở rộng theo góc quạt $60^\circ - 90^\circ$ về phía trước Exile dọc hành lang.
3. **Multi-Vector Projectiles (Chùm Đạn Đa Hướng):**
   * *Greater Multiple Projectiles (GMP):* Xả 5 tia đạn đồng thời theo góc quạt $\pm 30^\circ$ bao phủ toàn bộ bầy quái trước mặt.
   * *Spark:* Các tia điện nảy bật chân thực khỏi 2 vách tường hành lang theo định luật phản xạ góc 2D.
   * *Pierce / Chain:* Tia đạn xuyên táo theo đường thẳng hoặc giật xích qua lại giữa các quái vật phân bổ rải rác xung quanh sàn đấu.
4. **Visual Clamping (Trần hiển thị):**
   * Giới hạn hiển thị tối đa **12 đạn đồng thời** trên màn hình để chống rác thị giác (Visual Clutter). Logic tính toán sát thương trong `CombatEngine` vẫn chạy đủ 100% số đạn.
   * Mã màu chuẩn 5 hệ: Vật lý (Trắng bạc), Hỏa (Đỏ cam), Băng (Xanh băng dạ quang), Lôi (Vàng nhọn sắc), Hỗn mang (Tím độc).

#### D. Bố Trí Cụm Quái Tuần Tự Dọc Hành Lang (Sequential Pack Placement)
Quái vật không rớt từ trên trời xuống ngẫu nhiên, mà được bố trí thành từng bầy tại các tọa độ thế giới cố định phía trước:
* **Cụm 1 — Tiền Đồn (Vanguard Trash Swarm):** Quái thường (Trash mobs) dàn hàng ngang hoặc hình chữ V chắn lối đi.
* **Cụm 2 — Thủ Lĩnh & Hộ Vệ (Rare Commander & Phalanx):** Quái Tinh Anh (Rare Monster có hào quang hoàng kim) đứng giữa các cận vệ Magic/Normal, kích hoạt mod aura tăng tốc độ hoặc sát thương.
* **Encounter Đặc Biệt Giữa Đường:** Vết nứt Breach, Mạch Sulphite, hoặc Quái Tinh Chất (Essence) phong ấn nằm bên rìa hành lang.
* **Cụm 3, 4 — Đội Hình Đột Kích (Ambush / Flankers):** Quái cơ động áp sát từ hai bên sườn tạo thế gọng kìm (Pincer).

#### E. Cổng Phong Ấn & Phòng Boss Cuối Bản Đồ (End-of-Map Boss Room)
* **Cổng Phong Ấn (Fog Gate / Sealed Threshold):**
  * Nằm ở mốc cuối của hành lang (khoảng 80% – 85% chiều dài map).
  * Khi Exile bước chân qua ngưỡng cổng, hiệu ứng sương mù/cửa đá sập xuống phong tỏa lối thoát.
* **Khóa Khung Nhìn Đấu Trường Boss (Arena Camera Lock):**
  * Camera2D dừng chế độ bám trôi dọc hành lang và chuyển sang neo cố định tại trung tâm **Phòng Boss (Boss Chamber Arena)**.
* **Giao Tranh Map Boss & Slam Telegraph:**
  * Map Boss kích thước khổng lồ ($1.8\times$) xuất hiện kèm thanh máu lớn (Boss Overhead Health Bar) viền vàng.
  * Boss thi triển đòn Slam chí mạng:
    * **Vòng tròn đỏ nguy hiểm (Static Outer Danger Zone - $\text{Alpha} = 0.22$):** Bán kính $R = 50 - 70\text{px}$.
    * **Vòng co đếm ngược (Shrinking Ring):** Vòng hào quang co về tâm trong **1.2s – 1.5s**.
    * **Tín hiệu khẩn cấp:** Người chơi có thể phản xạ bấm **Emergency Portal (Phím `Space`)** tháo chạy nếu build không đủ EHP chịu đòn.
* **Kết Thúc & Bùng Nổ Phần Thưởng (Loot Explosion):**
  * Khi Boss gục ngã, toàn bộ đồ rơi giá trị cao bật nảy kèm cột sáng (Drop Beams) và âm thanh "Ting!".
  * Cổng Portal hoàn thành xuất hiện, hệ thống tự động chuẩn bị sang map tiếp theo theo cơ chế Auto-Mapping.```

---

### 3.3. Bộ Đo Sinh Mệnh & Phòng Thủ (Player Vitometer)
* **Layered Health Bar (Máu & ES kết hợp):**
  * **Life Bar:** Nền đỏ sẫm biểu thị máu gốc.
  * **Energy Shield (ES) Bar:** Lớp phủ màu xanh cyan dạ quang bọc ngoài thanh máu. Khi bị đánh, ES vỡ trước trừ khi bị sát thương Chaos xuyên thẳng vào máu.
  * **Guard Skill Layer:** Viền vàng óng ánh khi *Molten Shell* hoặc *Steelskin* đang có hiệu lực.
* **Mana & Mana Reservation Bar:**
  * Màu xanh lam cổ điển.
  * Đoạn Mana bị xích/mờ biểu thị % Mana bị khóa bởi Aura (Mana Reservation). Phần còn lại là Mana thực tế có thể dùng.
* **Quick Flask Rack (Dàn 5 Bình Thuốc):**
  * 5 vị trí bình thuốc: `[Life] [Quicksilver] [Granite] [Diamond] [Quartz]`.
  * Hiển thị vạch tích lũy Charges tự nạp khi hạ quái.
  * Hiệu ứng viền phát sáng khi bình tự kích hoạt (Auto-Instill).```

---

### 3.4. Số Sát Thương Nổi & Dopamine Loot Stream (Floating Combat Text & Drops)
* **Mã màu chuẩn sát thương:**
  * ⚪ **Vật lý (Physical):** Trắng bạc (`#E0E0E0`).
  * 🔥 **Hỏa (Fire):** Đỏ cam rực lửa (`#FF5722`).
  * ❄️ **Băng (Cold):** Xanh băng giá (`#40C4FF`).
  * ⚡ **Lôi (Lightning):** Vàng nhọn sắc (`#FFEA00`).
  * 🧪 **Hỗn mang (Chaos):** Tím độc (`#D500F9`).
  * ⭐ **Chí mạng (Critical Strike):** Chữ phóng to 16px, viền vàng kim, hiệu ứng rung nảy (Pop & Shake).
* **Loot Toast & Beam Notification:**
  * Khi rơi vật phẩm giá trị cao qua Loot Filter (Tier 1 Currency: Divine/Exalted, Unique hiếm, 6-Link Base):
    * Một thanh biểu ngữ (Toast Banner) viền đỏ/vàng trượt xuống từ mép trên Arena.
    * Cột sáng (**Drop Beam**) chiếu thẳng từ đỉnh trần xuống đúng tọa độ 2D của quái vừa ngã xuống.
    * Phát âm thanh xúc giác kinh điển: **"TING!"**.```

---

### 3.5. Nút Khẩn Cấp & Công Cụ Tương Tác Vĩ Mô (Macro Controls)
* **Emergency Portal (Phím tắt: `Space`):**
  * Nút bấm biểu tượng cổng dịch chuyển màu xanh lam phát sáng ở thanh công cụ đáy.
  * Giúp người chơi tẩu thoát ngay về Hideout khi sắp chết ở Red Map nhằm tránh hình phạt mất 10% EXP.
* **Combat Telemetry & Real-Time DPS Meter (`[?]` Inspector / Quick Footer):**
  * Hiển thị chỉ số sát thương thực tế thời gian thực (`DPS: 142.5K • Peak: 389K`) tính theo thuật toán cửa sổ trượt 3.0s (xem chi tiết tại [15_game_hub_and_sanctuary.md](15_game_hub_and_sanctuary.md)).
  * Khi bấm mở rộng hoặc di chuột: Hiển thị phân tách tỉ lệ % nguồn sát thương (Physical, Fire, Cold, Lightning, Chaos, DoT), tỷ lệ trúng đòn (Accuracy Rate) và tần suất đòn đánh (APS).
  * Hỗ trợ chẩn đoán nguyên nhân tử vong (Death Recap) khi nhân vật bị hạ gục.


---

## 4. Bảng Bảng Màu Chuẩn (Color Palette Tokens)

| Token Name | HEX Code | Ý Nghĩa Sử Dụng |
| :--- | :--- | :--- |
| `BgDark` | `#0D0E12` | Nền chính của ứng dụng |
| `PanelBg` | `#16181F` | Nền của các hộp chứa dữ liệu, khung Arena |
| `FloorTile` | `#11131A` | Mặt sàn đá Top-Down |
| `FloorGrout`| `#1A1D28` | Đường chỉ ron rãnh đá mặt sàn 2D |
| `BorderGold` | `#AF8B4E` | Viền kim loại phong cách Path of Exile |
| `LifeRed` | `#B82E2E` | Màu thanh máu Life |
| `ES_Cyan` | `#00E5FF` | Màu khiên năng lượng Energy Shield |
| `ManaBlue` | `#1E88E5` | Màu năng lượng Mana |
| `ManaReserved`| `#0D47A1` | Màu vùng Mana bị khóa bởi Aura |
| `ExpBar` | `#76FF03` | Thanh kinh nghiệm EXP |
| `LootTier1` | `#FF1744` | Tia sáng và biểu ngữ đồ Thần thánh (Divine / Mirror / Tier 0 Unique) |
| `LootTier2` | `#FFC107` | Tia sáng rơi đồ Chaos Orb / Exalted / 6-Link |```

---

## 5. Cấu Trúc Scene Tree Cho Godot 4 (`Main.tscn` & `BattleViewport.tscn`)

### 5.1. Cấu Trúc Khung Chính Đảo Tầng (`scenes/Main.tscn`)
```
Main (Control, custom_minimum_size: Vector2(640, 320))
└── MainLayout (VBoxContainer)
    ├── TopHeaderBar (HBoxContainer, 36px - Title, Class/Lv, Resize, Multi-Monitor, Close)
    │
    ├── TabWorkspaceContainer (MarginContainer, SizeFlags: Vertical Expand)
    │   └── [Interactive Views: EquipmentView, GemView, AtlasView, PassiveTreeView, StashView]
    │       (Hiển thị ở NỬA TRÊN khi ở Expanded Mode; ẩn hoàn toàn khi ở Compact Mode)
    │
    ├── SubTabBarContainer (HBoxContainer, 36px - Bản lề đóng/mở & chuyển đổi các Sub-Tabs)
    │   └── [TabGearBtn, TabSkillsBtn, TabAtlasBtn, TabPassivesBtn, TabStashBtn]
    │
    ├── CombatViewportContainer (Control, 248px - Bọc Persistent BattleViewport)
    │   └── BattleViewport (Control, custom_minimum_size: Vector2(640, 248) - CỐ ĐỊNH Ở ĐÁY)
    │
    └── BottomMacroBar (HBoxContainer, 40px - Sát Taskbar: Emergency Portal, DPS, Auto-Run)
```

### 5.2. Cấu Trúc Viewport Chiến Đấu (`scenes/BattleViewport.tscn`)
```
BattleViewport (Control, custom_minimum_size: Vector2(640, 248))
├── BackgroundRect (ColorRect - #0D0E12)
│
├── MapHeaderContainer (VBoxContainer, 32px)
│   ├── MapInfoHBox (HBoxContainer - TierBadge, MapName, Threat Badges, IIQ Bonus)
│   └── MapProgressBar (ProgressBar - 0% to 100%)
│
├── ArenaSubViewportContainer (SubViewportContainer, 172px)
│   └── Arena2D (Node2D - Script: Arena2D.cs, 640 x 172 px)
│       ├── ArenaCamera2D (Camera2D - Script: ArenaCamera.cs, LookAhead +80px)
│       ├── WorldMapCorridor (Node2D - Horizontal Dungeon Stone Floor & Vistas)
│       │   ├── AmbientDustLayer (Node2D)
│       │   ├── BossGateThreshold (Node2D - Fog Gate at 85%)
│       │   └── BossRoomChamber (Node2D - Final Arena at 93%)
│       ├── MonsterPool (Node2D - Object pooled 2D enemies)
│       ├── ExileNode (Node2D - True World Movement, Auto-Facing, Hood, Staff Tip)
│       ├── ProjectilePool (Node2D - Pooled 360° combat projectiles)
│       ├── TelegraphLayer (Node2D - 2D Circular Boss Slam AoE Rings)
│       ├── DropBeamLayer (Node2D - Shader beams for rare loot)
│       └── FloatingCombatTextLayer (Control - Script: FloatingTextManager.cs)
│
├── VitometerPanel (PanelContainer, 44px)
│   └── VitometerHBox (HBoxContainer - Life/ES Bar, 5x FlaskSlots, Mana/Reservation Bar)
│
└── LootToastOverlay (PanelContainer - Animated top toast)
```

