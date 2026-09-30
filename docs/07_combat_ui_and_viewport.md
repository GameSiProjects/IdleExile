# 07. Thiết Kế Giao Diện Chiến Đấu (Combat Interface & Top-Down Viewport)

Tài liệu này đặc tả chi tiết giao diện chiến đấu (**Combat UI / Viewport**) của **Idle Exile**, định dạng chuẩn cho cửa sổ phụ dạng **Mini-Window Desktop Companion (`440 x 780` px)** phát triển trên **Godot 4 .NET (C#)**.

> **Quyết Định Thiết Kế Trọng Tâm (Senior Game Design Shift):**  
> Chuyển đổi toàn diện từ cơ chế cuộn cảnh ngang (Side-Scrolling / Runner 1D) sang **Góc Nhìn Từ Trên Xuống (2D Top-Down / Isometric Arena)**.  
> *Lý do:* Thể loại ARPG kinh điển nói chung và *Path of Exile* nói riêng lấy cảm hứng cốt lõi từ không gian 2D đa hướng (360 độ). Góc nhìn Top-Down là chìa khóa duy nhất để tái hiện chân thực các trường phái kỹ năng biểu tượng (Cyclone xoay vòng, Righteous Fire vòng lửa, Nova tỏa tròn, Projectile đa hướng, Ground Slam hình nón), đồng thời tạo không gian cho các cơ chế quái vật bủa vây (Swarm / Surrounding), vòng tế đàn Ritual và bàn tay Breach mở rộng.

---

## 1. Triết Lý Thiết Kế & Định Hướng UX/UI

### 1.1. Mục Tiêu Cốt Lõi (Core UX Objectives)
* **Diagnostic Clarity (Chẩn đoán tức thì trong 1 giây):**
  * Người chơi không micro-control nhân vật (Zero APM), nhưng mọi thông tin sinh tử phải hiển thị tức thì khi người chơi liếc nhìn: Lượng EHP (Máu + Energy Shield), Mod quái/Map nguy hiểm, Vòng tròn cảnh báo tử thần (Boss Slam AoE Telegraph), Tiến trình Wave/Boss, và đồ rơi hiếm có.
* **Visceral Top-Down ARPG Feedback (Phản hồi xúc giác & thị giác chuẩn PoE):**
  * Sàn đấu Top-Down 2D tối giản nhưng thỏa mãn: Quái bị đóng băng (*Freeze*) vỡ vụn thành mảnh băng vụn, đòn chí mạng (*Crit*) bật nảy số to kèm vệt sáng vàng kim, hiệu ứng *Bleed* tóe máu trên mặt sàn đá cổ.
  * Đường đạn 360° phản ánh chính xác các gem hỗ trợ: *GMP (bắn chùm 5 tia hình rẽ quạt)*, *Fork (tách đôi khi trúng quái)*, *Pierce (xuyên táo xuyên tâm quái vật)*, *Chain (nảy giật zíc zắc giữa các cụm quái)*.
  * **Dopamine Drop Beams:** Cột sáng rực rỡ chiếu thẳng từ trên trời xuống điểm rơi của quái kèm âm thanh "Ting!" đanh thép khi rớt đồ hiếm qua Loot Filter (Divine, Exalted, 6-Link).
* **Low Resource Footprint (Tối ưu tài nguyên cực hạn):**
  * Tận dụng `gl_compatibility` và `LowProcessorUsageMode` của Godot 4.
  * Tách biệt hoàn toàn giữa `CombatEngine` (tính toán ngầm 30Hz) và Render Viewport (vẽ 2D qua CanvasItem/Shader nhẹ, hỗ trợ giảm FPS khi minimize).

---

## 2. Bố Cục Không Gian & Phân Tầng Pixel (Wireframe: 440 x 780 px)

Khu vực Combat Viewport chiếm **300 px chiều cao** cố định ở nửa trên màn hình (từ `Y: 48` đến `Y: 348`), trong đó **2D Top-Down Arena chiếm 208 px** (từ `Y: 92` đến `Y: 300`) là "sân khấu trực tiếp" neo giữ ánh nhìn và không bao giờ bị che khuất khi chuyển đổi các tab bên dưới:

```text
┌─────────────────────────────────────────────────────────┐  Y: 0
│ [IE] IDLE EXILE       T14 Strand Map     [Pin] [_] [X]  │  Header: 48px
├─────────────────────────────────────────────────────────┤  Y: 48
│ [MAP BANNER]  T14 Strand • Rare (4 Mods)    [IIQ +68%]  │
│ [PROGRESS]    ████████████████░░░░░░░░  68% [BOSS SPAWN]│  Map Meta: 42px
├─────────────────────────────────────────────────────────┤  Y: 90
│                                                         │
│               [ 2D TOP-DOWN COMBAT ARENA (210px) ]      │
│   ┌─────────────────────────────────────────────────┐   │
│   │  [Mob: Skel] ↖              ↑ [Mob: Archer]     │   │
│   │                  *CRIT* 12.4k                   │   │
│   │         ══►           ▲                         │   │
│   │  [Mob] ──►     [ EXILE ]  (360° AURA) ◄── [Mob] │   │  Top-Down Arena:
│   │             (Center Anchor: 220, 104)           │   │  208px
│   │         *SLAM TELEGRAPH* ⚠️                     │   │
│   │         (⭕ Red Shrinking Ring 1.4s)             │   │
│   │  [Mob: Brute] ↙             ↓ [Rare Goliath]    │   │
│   │   HP: ████░░░░               [Golden Aura]      │   │
│   └─────────────────────────────────────────────────┘   │
│   [LOOT TOAST]: ★ Exalted Orb dropped! [Ting!]          │
├─────────────────────────────────────────────────────────┤  Y: 300
│ [LIFE / ES]                    [GUARD / FLASKS]  [MANA] │
│ 3,450 / 3,450 HP               [1][2][3][4][5]   82/820 │  Vitometer: 50px
│ [ES: 1,200] [Fortify: 20]      [Molten Shell]    (90% R)│
├─────────────────────────────────────────────────────────┤  Y: 350
│ [MAP DEVICE] | [GEAR & GEMS] | [PASSIVE TREE] | [STASH] │  Sub-Tabs Bar: 36px
├─────────────────────────────────────────────────────────┤  Y: 386
│                                                         │
│                  TAB CONTENT AREA                       │
│    (Xem đồ, cắm Support Gems, nâng điểm Passive)         │  Tab Body: 364px
│                                                         │
├─────────────────────────────────────────────────────────┤  Y: 750
│ [EMERGENCY PORTAL (Space)]          DPS: 142.5K | 10 T/s│  Footer: 30px
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
* **Thanh Tiến Trình (Pack Progress Bar):**
  * Chạy từ `0% -> 100%`.
  * Có biểu tượng đánh dấu mốc **League Encounter** (Breach, Ritual, Essence) và ngưỡng **Map Boss Spawn** ở cuối thanh.

---

### 3.2. Sàn Đấu 2D Top-Down (2D Top-Down Combat Arena - 208px)

#### A. Góc Nhìn & Không Gian (Perspective & Spatial Arena)
* **Góc nhìn:** **2D Top-Down / Angled Bird's-Eye (~60° Tilt)** chuẩn ARPG (như Path of Exile, Vampire Survivors, Hades).
* **Không gian tương tác:** Mặt sàn 2 chiều hoàn chỉnh ($X \in [0, 440]$, $Y \in [0, 208]$).
* **Môi trường & Mặt sàn (Dungeon Floor & Environment):**
  * Thay thế hiệu ứng cuộn ngang (Parallax Runner) bằng **Mặt sàn đá cổ (Dungeon Stone Tiles / Paved Flagstones)** được vẽ bằng các khối gạch có đường chỉ ron tinh tế (`#13161F` và viền `#1C202B`).
  * Viền tối 4 góc (Vignette & Ambient Occlusion) tạo cảm giác sàn đấu hầm ngục u tối, tăng độ tương phản giúp đọc rõ đạn đạo và vòng cảnh báo.
  * Hiệu ứng bụi tàn tro / đom đóm dạ quang (Ambient Atmospheric Motes) trôi lơ lửng ngẫu nhiên trong không gian 2D đa chiều.

#### B. Vị Trí Nhân Vật (Exile Focal Anchor) & Zero APM Dynamic Facing
* **Tọa độ Neo Trung Tâm (Focal Anchor):**
  * Mặc định Exile ngự trị tại trọng tâm sàn đấu: `Vector2(220f, 104f)`.
  * *Zero APM Philosophy:* Người chơi không cần điều khiển di chuyển nhân vật.
* **Xoay Hướng Tự Động (Auto-Facing & Dynamic Aiming Vector):**
  * Nhân vật tự động tính toán vector hướng tới mục tiêu ưu tiên:
    $$\vec{V}_{\text{target}} = \text{TargetPosition} - \text{ExilePosition}$$
  * Xoay góc mặt mượt mà (Smooth Angle Interpolation) về phía mục tiêu đang công kích.
  * Khi xuất chiêu (Cast/Attack), Exile có độ giật lùi nhẹ (Micro-Recoil) ngược chiều vector tấn công tạo lực phản hồi thị giác mạnh mẽ.
* **Biểu diễn Nhân vật (Top-Down Silhouette):**
  * Vòng tròn hào quang năng lượng (Aura Circle) tỏa sáng đều 360° dưới chân Exile trên mặt sàn đá.
  * Góc nhìn từ trên xuống: Bóng đổ tròn đen mờ, bờ vai áo choàng sẫm màu, mũ trùm bóng đêm (Dark Hood), và tay cầm vũ khí (Gậy phép/Cung/Kiếm) chĩa thẳng về hướng mục tiêu với điểm sáng đầu vũ khí (Wand Tip).

#### C. Hình Học Kỹ Năng Đa Chiều (360° Skill Geometry)
Top-Down mở khóa toàn bộ các hình thái kỹ năng trứ danh của *Path of Exile*:
1. **Radial / Nova (360° Tỏa Tròn):**
   * *Righteous Fire (RF):* Vòng lửa thiêu đốt rực rỡ bao quanh Exile ở bán kính $R = 45\text{px}$.
   * *Frost Nova / Shock Nova:* Sóng xung kích băng giá / sấm sét nổ bung thành vòng tròn lan tỏa từ tâm ra ngoài mép sàn đấu.
2. **Cones & Sweeps (Hình Nón & Quạt Quét):**
   * *Cleave / Ground Slam / Sunder:* Sóng chấn động mở rộng theo góc quạt $60^\circ - 90^\circ$ về phía trước Exile.
3. **Multi-Vector Projectiles (Chùm Đạn Đa Hướng):**
   * *Greater Multiple Projectiles (GMP):* Xả 5 tia đạn đồng thời theo góc quạt $\pm 30^\circ$ bao phủ toàn bộ bầy quái trước mặt.
   * *Spark:* Các tia điện nảy bật chân thực khỏi 4 bức tường biên của sàn đấu theo định luật phản xạ góc 2D.
   * *Pierce / Chain:* Tia đạn xuyên táo theo đường thẳng hoặc giật xích qua lại giữa các quái vật phân bổ rải rác xung quanh sàn đấu.
4. **Visual Clamping (Trần hiển thị):**
   * Giới hạn hiển thị tối đa **12 đạn đồng thời** trên màn hình để chống rác thị giác (Visual Clutter). Logic tính toán sát thương trong `CombatEngine` vẫn chạy đủ 100% số đạn.
   * Mã màu chuẩn 5 hệ: Vật lý (Trắng bạc), Hỏa (Đỏ cam), Băng (Xanh băng dạ quang), Lôi (Vàng nhọn sắc), Hỗn mang (Tím độc).

#### D. Bầy Quái 2D & Đội Hình Xuất Hiện (2D Pack Formations)
Quái vật không xếp hàng dọc đơn điệu từ một phía mà bố trí theo các đội hình Top-Down ARPG đặc trưng:
* **Surrounding Swarm (Vòng Vây 360°):** Quái thường (Trash mobs) áp sát từ 4 góc và 4 cạnh, vây bọc Exile ở trung tâm.
* **Commander & Phalanx (Thủ Lĩnh & Hộ Vệ):** Quái Tinh Anh (Rare Monster có hào quang hoàng kim) đứng lùi phía sau hoặc ở trung tâm, bao quanh bởi đám tiểu yêu Magic/Normal làm bia đỡ đạn.
* **Top-Down League Encounters:**
  * *Breach:* Bàn tay hư không nứt ra ở giữa sàn đấu, vòng tròn tím mở rộng dần ra xung quanh, quái tràn từ bóng tối rìa ngoài vào trong.
  * *Ritual:* Vòng tròn tế đàn máu rực sáng giam giữ đấu trường, quái hồi sinh từ các bệ đá tế đàn.
* **Rarity Visual Tokens:**
  * *Normal:* Kích thước chuẩn ($14 \times 14\text{px}$), mắt đỏ nhỏ.
  * *Magic:* Hào quang xanh lam bao quanh, tăng 15% kích thước.
  * *Rare:* Hào quang vàng hoàng kim rực rỡ, kèm icon mod tinh anh phía trên đầu.
  * *Map Boss:* Kích thước khổng lồ ($1.8\times$), vòng uy áp đỏ thẫm dưới chân, đi kèm thanh máu lớn (Overhead Boss Health Bar) có viền vàng kim loại.

#### E. Boss Slam Telegraph Indicator (Vòng Cảnh Báo Chí Tử 2D Chuẩn Mực)
* Trong không gian Top-Down, Telegraph hiển thị chân thực như các đòn giáng búa trứ danh của *The Shaper*, *Atziri Flameblast*, hay *Brutus*:
  * **Vòng tròn đỏ mờ nguy hiểm (Static Outer Danger Zone - $\text{Alpha} = 0.22$):** Bao trọn vùng bán kính nổ ($R = 40 - 60\text{px}$) trên mặt sàn đá.
  * **Vòng thu tâm đếm ngược (Shrinking Timing Ring):** Vòng hào quang vàng/đỏ co dần từ $1.6\times$ bán kính về đúng tâm trong khoảng thời gian **1.2s – 1.5s (Wind-up)**.
  * **Tâm ngắm cảnh báo (Crosshair Warning):** Dấu chữ thập đỏ ở tâm điểm đòn đánh.
  * **Kích nổ (Detonation):** Khi vòng chạm tâm, phát tiếng nổ trầm đục (*"THUMP!"*), rung màn hình 2D (Screen Shake 3.5px trong 0.15s), bùng tia sóng xung kích và trừ lượng lớn EHP nếu Exile đứng trong bán kính.
  * *Giá trị Zero APM:* Đây là tín hiệu thị giác rõ ràng nhất để người chơi phản xạ bấm **Emergency Portal (Phím `Space`)** tháo chạy về Hideout, bảo toàn 10% EXP khi thấy mình nằm gọn trong tâm chấn đòn đánh chí mạng (>70% EHP).

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
  * Hiệu ứng viền phát sáng khi bình tự kích hoạt (Auto-Instill).

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
    * Phát âm thanh xúc giác kinh điển: **"TING!"**.

---

### 3.5. Nút Khẩn Cấp & Công Cụ Tương Tác Vĩ Mô (Macro Controls)
* **Emergency Portal (Phím tắt: `Space`):**
  * Nút bấm biểu tượng cổng dịch chuyển màu xanh lam phát sáng ở thanh công cụ đáy.
  * Giúp người chơi tẩu thoát ngay về Hideout khi sắp chết ở Red Map nhằm tránh hình phạt mất 10% EXP.
* **Combat Telemetry Drawer (`[?]` Inspector):**
  * Nút nhỏ mở popup phân tích nhanh: *Effective DPS, Tỉ lệ né đòn thực tế (Entropy Rate), Nguyên nhân tử vong (Death Recap)*.

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
| `LootTier2` | `#FFC107` | Tia sáng rơi đồ Chaos Orb / Exalted / 6-Link |

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
│       ├── ArenaFloorBackground (Node2D - Top-Down Paved Tiles & Vignette)
│       ├── AmbientDustLayer (Node2D - Drifting Atmospheric Motes)
│       ├── MonsterPool (Node2D - Object pooled 2D enemy tokens)
│       ├── ExileNode (Node2D - Center Anchor (220, 104), 360° Aura, Hood, Staff Tip)
│       ├── ProjectilePool (Node2D - Pooled 360° combat projectiles)
│       ├── TelegraphLayer (Node2D - 2D Circular Boss Slam AoE Rings)
│       ├── DropBeamLayer (Node2D - Shader beams for rare loot at 2D dropped positions)
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

