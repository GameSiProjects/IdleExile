# 07. Thiết Kế Giao Diện Chiến Đấu (Combat Interface & Top-Down Viewport)

Tài liệu này đặc tả chi tiết giao diện chiến đấu (**Combat UI / Viewport**) của **Idle Exile**, định dạng chuẩn cho cửa sổ phụ dạng **Mini-Window Desktop Companion (`440 x 780` px)** phát triển trên **Godot 4 .NET (C#)**.

> **Quyết Định Thiết Kế Trọng Tâm (Senior Game Design Shift):**  
> Chuyển đổi toàn diện sang **Góc Nhìn Từ Trên Xuống Dọc Hành Lang Bản Đồ Tuyến Tính (2D Top-Down Linear Corridor & Camera Follow)**:
> 1. **Bản Đồ Kéo Dài Đơn Hướng (Directional Linear Corridor):** Bản đồ mở rộng theo một hướng trong không gian thế giới thực ($X_{\text{world}}, Y_{\text{world}}$), tái hiện cảm giác dọn map kinh điển (*Strand, Canyon, Promenade*) với chuỗi cụm quái tuần tự và kết thúc bằng Phòng Boss (End-of-Map Boss Room).
> 2. **Tuyệt Đối Không Áp Dụng Cuộn Sàn (No Fake Parallax/Floor Scrolling):** Mặt sàn, chướng ngại vật, quái vật và đồ rơi đều nằm tại tọa độ thế giới thực. Nhân vật (Exile) **thực sự di chuyển tịnh tiến** trong thế giới game (`Position` thay đổi liên tục).
> 3. **Camera2D Bám Theo Nhân Vật (Camera Tracking):** Camera2D di chuyển mượt mà bám theo vị trí thực của Exile, duy trì góc quan sát tối ưu (kèm khoảng nhìn trước Look-ahead) để người chơi luôn thấy bầy quái đang chờ phía trước.
> 4. **Giữ Vững Giá Trị Không Gian 360°:** Dù bản đồ có hướng đi chính (Macro Direction), hành lang vẫn có độ rộng 2D ($W \approx 280 - 440\text{px}$) cho phép bầy quái bao vây, quái cơ động bọc hậu, và phát huy 100% sức mạnh của các kỹ năng 360° (Righteous Fire, Nova, GMP, Cleave).```

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
  * Tách biệt hoàn toàn giữa `CombatEngine` (tính toán ngầm 30Hz) và Render Viewport (vẽ 2D qua CanvasItem/TileMap nhẹ, hỗ trợ giảm FPS khi minimize).```

---

## 2. Bố Cục Không Gian & Phân Tầng Pixel Động (Dynamic Adaptive Layout)

Ứng dụng hỗ trợ chuyển đổi linh hoạt giữa **Chế độ Treo Máy Mini (Compact Mode: 440 x 380 px)** và **Chế độ Nghiên Cứu Đầy Đủ (Expanded Mode: 440 x 780 px)** với thuật toán neo đáy Taskbar (keepBottom = true):

### 2.1. Wireframe Chế Độ Thu Nhỏ: Compact Mode (440 x 380 px)
Dành cho người chơi vừa làm việc/chơi game khác vừa treo map:

```text
┌─────────────────────────────────────────────────────────┐  Y: 0
│ [IE] IDLE EXILE       [Pin] [⤢ Expand] [🖥 Screen] [X]  │  Header: 40px
├─────────────────────────────────────────────────────────┤  Y: 40
│ [MAP] T14 Strand Map • Rare (+68% IIQ)   [████░░ 68%] ☠ │  Map Meta: 34px
├─────────────────────────────────────────────────────────┤  Y: 74
│                                                         │
│        [ 2D TOP-DOWN VIEWPORT (Camera2D Following) ]    │
│   ┌─────────────────────────────────────────────────┐   │
│   │  [Mob: Skel] ↖           ▲ (Hướng)    [Archer]  │   │  Top-Down Viewport:
│   │                  *CRIT* 12.4k                   │   │  208px
│   │         ══►           ▲                         │   │  (Sàn đá, đạn 360°,
│   │  [Mob] ──►     [ EXILE ]  (360° AURA) ◄── [Mob] │   │   quái vật, Boss Slam
│   │         (⭕ Red Shrinking Ring 1.4s)             │   │   AoE Telegraph)
│   └─────────────────────────────────────────────────┘   │
│   [LOOT TOAST]: ★ Exalted Orb dropped! [Ting!]          │
├─────────────────────────────────────────────────────────┤  Y: 282
│ [LIFE / ES]                    [GUARD / FLASKS]  [MANA] │
│ 3,450 / 3,450 HP               [1][2][3][4][5]   82/820 │  Vitometer: 50px
├─────────────────────────────────────────────────────────┤  Y: 332
│ [🌀 EMERGENCY PORTAL (Space)]     DPS: 142.5K | [🛡 Gear]│  Quick Footer: 48px
└─────────────────────────────────────────────────────────┘  Y: 380
```

### 2.2. Wireframe Chế Độ Phóng To: Expanded Mode (440 x 780 px)
Khi bấm [⤢ Expand] hoặc [🛡 Gear], cửa sổ **tự động nở lên phía trên** (cạnh đáy giữ nguyên sát thanh Taskbar) mở ra toàn bộ khay tương tác:

```text
┌─────────────────────────────────────────────────────────┐  Y: 0
│ [IE] IDLE EXILE       [Pin] [⤡ Min] [🖥 Screen] [X]     │  Header: 40px
├─────────────────────────────────────────────────────────┤  Y: 40
│ [MAP] T14 Strand Map • Rare (+68% IIQ)   [████░░ 68%] ☠ │  Map Meta: 34px
├─────────────────────────────────────────────────────────┤  Y: 74
│        [ 2D TOP-DOWN VIEWPORT (Camera2D Following) ]    │  Top-Down Arena:
│        (Xem trực tiếp hiệu quả thay đồ trong combat)    │  208px
├─────────────────────────────────────────────────────────┤  Y: 282
│ [LIFE / ES]                    [GUARD / FLASKS]  [MANA] │  Vitometer: 50px
├─────────────────────────────────────────────────────────┤  Y: 332
│ [ 🛡 GEAR ]  |  [ 💎 GEMS ]  |  [ 🗺 ATLAS ]  |  [ 🌳 ]  │  Sub-Tabs Bar: 36px
├─────────────────────────────────────────────────────────┤  Y: 368
│                                                         │
│                  TAB CONTENT AREA                       │
│    - Bảng trang bị 10 ô (Doll)                          │
│    - Smart Tooltip hiển thị Delta (▲ EHP / ▲ DPS)       │  Tab Body: 372px
│    - Rương đồ (Stash) / Bàn ép hóa thạch (Anvil)        │
│                                                         │
├─────────────────────────────────────────────────────────┤  Y: 740
│ [🌀 EMERGENCY PORTAL (Space)]          DPS: 142.5K      │  Footer: 40px
└─────────────────────────────────────────────────────────┘  Y: 780
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
  * Phản ánh trung thực tỉ lệ quãng đường thực tế mà Exile đã vượt qua: $\text{Progress} = \text{Clamp01}(Y_E / L_{\text{boss\_gate}})$.
  * Đánh dấu vị trí các mốc **League Encounter** trên hành lang và biểu tượng **Cổng Boss (Boss Gate)** ở mốc cuối thanh.```

---

### 3.2. Sàn Đấu 2D Top-Down & Hành Lang Bản Đồ Tuyến Tính

#### A. Không Gian Hành Lang Thế Giới & Triệt Tiêu Cuộn Sàn Giả (World-Space Linear Corridor)
* **Góc nhìn:** **2D Top-Down / Angled Bird's-Eye (~60° Tilt)** chuẩn ARPG.
* **Không gian thế giới thực:**
  * Bản đồ là một hành lang trải dài theo trục dọc (ví dụ: $Y_{\text{world}} \in [0, L_{\text{map}}]$ với $L_{\text{map}} \approx 1800 - 3000\text{px}$) hoặc trục định hướng, bề rộng hành lang $W_{\text{corridor}} \approx 280 - 440\text{px}$.
  * **Tuyệt đối không áp dụng cuộn sàn (No fake floor scrolling / conveyor belt):** Mặt sàn đá cổ, các rãnh tường, vật cản, quái vật và đồ rơi đều được đặt tại **tọa độ thế giới thực ($X_{\text{world}}, Y_{\text{world}}$)**.
* **Môi trường & Không gian thị giác:**
  * Mặt sàn gạch đá hầm ngục (`#13161F` và ron viền `#1C202B`) trải dài liên tục.
  * Viền tối tự nhiên 2 bên vách hành lang (Vignette & Shadow Walls) tạo cảm giác chiều sâu hầm ngục, tăng độ tương phản giúp đọc rõ đạn đạo và vòng cảnh báo.
  * Hiệu ứng tàn tro / đom đóm dạ quang lơ lửng trong không gian thế giới.

#### B. Di Chuyển Thế Giới Thực Của Nhân Vật & Camera2D Tracking
* **Di chuyển thực sự của Nhân vật (Real Character Movement):**
  * Nhân vật (Exile) thay đổi `Position` thực sự trong Scene 2D, không đứng chôn chân tại một điểm cố định.
  * Tốc độ di chuyển thực tế (`Movement Speed`) quyết định tốc độ lướt qua các khoảng cách trống giữa các pack quái.
* **Máy Trạng Thái Tự Động (Zero-APM Character Controller):**
  1. **Hành quân (Marching / Advancing):** Khi không có quái vật trong tầm nhận thức, Exile chủ động tiến bước dọc theo hướng hành lang.
  2. **Tiếp cận & Giữ khoảng cách (Engaging / Combat Staging):** Khi phát hiện cụm quái phía trước lọt vào tầm đánh hiệu dụng (35px với Melee, 150-180px với Ranged/Spell), Exile dừng lại hoặc xoay quanh cụm để công kích.
  3. **Tiếp tục hành quân:** Khi dọn sạch toàn bộ quái trong cụm, Exile tiếp tục dấn bước tới cụm tiếp theo.
  4. **Bước vào phòng Boss:** Khi chạm mốc cuối hành lang, Exile bước qua cổng sương mù phong ấn.
* **Hệ Thống Camera2D Bám Theo (Camera Follow System):**
  * `Camera2D` gắn thuật toán Smooth Interpolation (Lerp) bám sát vị trí thực của Exile.
  * Camera có khoảng lệch nhìn trước (**Look-ahead Offset**) theo hướng di chuyển để người chơi luôn nhìn thấy bầy quái đang chờ sẵn phía trước trong khung nhìn $440 \times 208\text{px}$.
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
* **Combat Telemetry Drawer (`[?]` Inspector):**
  * Nút nhỏ mở popup phân tích nhanh: *Effective DPS, Tỉ lệ né đòn thực tế (Entropy Rate), Nguyên nhân tử vong (Death Recap)*.```

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

## 5. Cấu Trúc Scene Tree Cho Godot 4 (`BattleViewport.tscn`)

```
BattleViewport (Control, custom_minimum_size: Vector2(440, 300))
├── BackgroundRect (ColorRect - #0D0E12)
│
├── MapHeaderContainer (VBoxContainer)
│   ├── MapInfoHBox (HBoxContainer)
│   │   ├── TierBadge (TextureRect / Label: "T14")
│   │   ├── MapNameLabel (Label: "Strand Map")
│   │   ├── ModIconsContainer (HBoxContainer - Threat Badges)
│   │   └── QuantityBonusLabel (Label: "IIQ: +72%")
│   └── MapProgressBar (ProgressBar - 0% to 100%)
│
├── ArenaSubViewportContainer (SubViewportContainer)
│   └── Arena2D (Node2D - Script: Arena2D.cs)
│       ├── ArenaCamera2D (Camera2D - Script: ArenaCamera.cs, Smooth Follow ExileNode)
│       ├── WorldMapCorridor (Node2D - Linear Dungeon Stone Floor & Wall Vistas)
│       │   ├── AmbientDustLayer (Node2D - Drifting Atmospheric Motes in World Space)
│       │   ├── BossGateThreshold (Node2D - Fog Gate / Sealed Threshold)
│       │   └── BossRoomChamber (Node2D - End-of-Map Boss Arena)
│       ├── MonsterPool (Node2D - Object pooled 2D enemies placed at World Coordinates)
│       ├── ExileNode (Node2D - True World Movement, 360° Dynamic Aiming, Hood, Staff Tip)
│       ├── ProjectilePool (Node2D - Pooled 360° combat projectiles in World Space)
│       ├── TelegraphLayer (Node2D - 2D Circular Boss Slam AoE Rings)
│       ├── DropBeamLayer (Node2D - Shader beams for rare loot at 2D dropped World Positions)
│       └── FloatingCombatTextLayer (Control - Script: FloatingTextManager.cs)
│
├── VitometerPanel (PanelContainer)
│   └── VitometerHBox (HBoxContainer)
│       ├── HealthVBox (VBoxContainer)
│       │   ├── LifeAndESOverlayBar (ProgressBar)
│       │   └── DefensesSummaryLabel ("EHP: 14.2k | Block: 75%")
│       ├── FlasksContainer (HBoxContainer - 5x FlaskSlot Controls)
│       └── ManaVBox (VBoxContainer)
│           ├── ManaReservedOverlayBar (ProgressBar)
│           └── UnreservedManaLabel ("Mana: 82 / 820")
│
└── LootToastOverlay (PanelContainer - Animated top banner)
```

