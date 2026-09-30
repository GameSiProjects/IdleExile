# 08. Trải Nghiệm Người Chơi Ban Đầu (First Time User Experience - FTUE)

Tài liệu này quy định chi tiết thiết kế luồng trải nghiệm người chơi trong 10 phút đầu tiên (**First 10-Minute Experience Flow**) của **Idle Exile**. 

Mục tiêu cốt lõi: Đưa người chơi tiếp cận ngay lập tức với tinh hoa của *Path of Exile* (chiều sâu build, phối ghép ngọc, nhặt đồ dopamine) trên form-factor **cửa sổ mini 440 x 780 px**, đồng thời triệt tiêu hoàn toàn sự mệt mỏi của cốt truyện 10 Act và các rào cản thao tác tay (Zero APM).

---

## 1. Triết Lý Thiết Kế & Các Chỉ Số Đo Lường (Design Pillars & KPIs)

### 1.1. Bốn Trụ Cột FTUE (Design Pillars)
1. **Time-to-Action < 15 giây:**  
   Từ khi khởi động ứng dụng tới khi nhân vật vung nhát chém hoặc bắn cầu lửa đầu tiên trong đấu trường: **tuyệt đối không quá 15 giây**.
2. **"Show, Don't Tell" — Ngữ Cảnh Thay Vì Rào Cản (Contextual Cues over Modal Locks):**  
   Tuyệt đối **không sử dụng popup chặn màn hình (Modal Dialogs)** bắt người chơi phải click "Next" liên tục. Mọi dẫn dắt đều thông qua:
   * **Golden Pulse:** Viền sáng vàng nhấp nháy tinh tế trên UI element cần tương tác.
   * **Micro-Tooltip:** Chú thích ngắn gọn (tối đa 10 chữ), tự động biến mất sau 4 giây hoặc sau khi click.
3. **Ba Khoảnh Khắc "Aha!" Trong 5 Phút Đầu (The 3 "Aha!" Moments):**
   * **Aha 1 (Loot Dopamine):** Âm thanh kim loại *"Ting!"* rền vang đặc trưng của PoE khi vật phẩm và Currency đầu tiên rớt ra từ quái vật.
   * **Aha 2 (PoE 2 Decoupled Gem Sockets):** Cắm Support Gem trực tiếp vào Active Gem mà không cần đục lỗ hay nối dây trên trang bị; kỹ năng trong Arena đổi hình thái và hiệu ứng tức thì.
   * **Aha 3 (Currency Crafting):** Sử dụng một viên *Orb of Transmutation* biến đổi trực tiếp một món đồ trắng thành đồ ma thuật (Magic Item) có thuộc tính.
4. **Mở Khóa Giao Diện Lũy Tiến (Progressive Disclosure):**  
   Không để người chơi choáng ngợp bởi hàng chục tab và bảng nội tại nghìn node ngay phút đầu. Giao diện mở rộng từng bước dựa theo cấp độ và cột mốc tài nguyên.

### 1.2. Chỉ Số Thành Công (FTUE KPIs)
* **Time-to-First-Kill:** $\le 18$ giây.
* **D1 FTUE Completion Rate:** Tỉ lệ người chơi hoàn thành Map đầu tiên và cắm thành công viên Support Gem đầu tiên $\ge 85\%$.
* **Map Loop Retention:** Tỉ lệ người chơi kích hoạt Map thứ 2 ngay sau khi nhặt hòm thưởng Boss $\ge 90\%$.
* **Always-On-Top Adoption:** Số người chơi bấm nút **Pin** ghim cửa sổ trong 10 phút đầu $\ge 60\%$.

---

## 2. Khởi Tạo & 3 Khuôn Mẫu Ban Đầu (Starter Archetypes)

Khi mở game lần đầu, thay vì màn hình tạo nhân vật và đặt tên phức tạp, người chơi được chào đón bằng giao diện tối giản chọn **1 trong 3 Khối Đá Linh Hồn (Soul Slates)** đại diện cho 3 thuộc tính cốt lõi của PoE:

```
┌─────────────────────────────────────────────────────────────┐
│                 THỨC TỈNH TẠI CRUCIBLE                      │
│      "Chọn một mảnh ký ức để mở cổng cõi giới hỗn mang"     │
├─────────────────────────────────────────────────────────────┤
│  [ SLATE 1: SỨC MẠNH ]   [ SLATE 2: PHÉP THUẬT ]  [ SLATE 3: NHANH NHẸN ] │
│     Juggernaut              Elementalist             Deadeye        │
│   (Đỏ - Strength)          (Lam - Intelligence)     (Lục - Dex)     │
│   Kỹ năng: Cleave          Kỹ năng: Fireball        Kỹ năng: Split  │
│   • Cận chiến chém lan     • Cầu lửa nổ AoE         • Mưa tên 3 tia │
│   • Giáp & Máu dày         • Khiên Năng Lượng       • Né tránh cao  │
└─────────────────────────────────────────────────────────────┘
```

### Chi Tiết Cấu Hình Starter Kits (Data-Driven Defaults):

| Archetype | Active Gem Khởi Đầu | Vũ Khí Khởi Điểm (Equipped) | Thuộc Tính / Phòng Thủ | Currency Khởi Điểm | Map Slates Tặng Sẵn | Support Gem Sẽ Rớt Ở Phút 4 |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Juggernaut (Đỏ)** | `Cleave` (Physical, Melee, AoE) | Dual-Wield 2x `Rusted Sword` (1H Sword, 4-9 Phys, 1.45 APS) | 120 Life, 25 Armour, +15% Dual-Wield Block | 3x Scroll of Wisdom, 1x Orb of Transmutation | 3x `Tier 1: Strand` | `Added Fire Damage Support` (Đỏ) |
| **Elementalist (Lam)** | `Fireball` (Fire, Spell, AoE) | `Driftwood Wand` (1H Wand, 4-7 Phys, 1.40 APS, +10% Spell Dmg) | 90 Life, 45 Energy Shield, 10% Fire Res | 3x Scroll of Wisdom, 1x Orb of Transmutation | 3x `Tier 1: Strand` | `Ignite Proliferation Support` (Lam) |
| **Deadeye (Lục)** | `Split Arrow` (Physical, Bow, Projectile) | `Crude Bow` (2H Bow, 5-13 Phys, 1.40 APS) | 105 Life, 25% Evasion Chance | 3x Scroll of Wisdom, 1x Orb of Transmutation | 3x `Tier 1: Strand` | `Multiple Projectiles Support` (Lục) |

Sau khi click chọn 1 Archetype (chỉ tốn **1 click duy nhất**), cửa sổ chuyển thẳng vào giao diện chiến đấu `Main.tscn` mà không cần tải cảnh (load scene) nặng nề. Toàn bộ vũ khí, ngọc kỹ năng và kho đồ ban đầu được gán tức thời vào bộ nhớ C# `PlayerState`.

---

## 3. Tiến Trình 10 Phút Đầu Tiên (The 10-Minute Golden Timeline)

```
[00:00 - 00:15] Bước 1: Thức Tỉnh & Chọn 1-Click Archetype
       │
       ▼
[00:15 - 00:45] Bước 2: Kích Hoạt Crucible & Bản Đồ Tier 1 Đầu Tiên
       │
       ▼
[00:45 - 02:00] Bước 3: Đợt Quái Đầu Tiên & Dopamine Loot Drop "Ting!"
       │
       ▼
[02:00 - 03:30] Bước 4: Lên Cấp 2 — Trải Nghiệm Điểm Bảng Nội Tại (Passive Web)
       │
       ▼
[03:30 - 05:00] Bước 5: Khoảnh Khắc "Aha!" — Ghép Support Gem Chuẩn PoE 2
       │
       ▼
[05:00 - 07:00] Bước 6: Đụng Độ Boss Bản Đồ & Xác Nhận Chuỗi Map Sustain
       │
       ▼
[07:00 - 10:00] Bước 7: Bàn Giao Chế Độ Companion (Pin & Treo Máy Chạy Nền)
```

---

### Chi Tiết Từng Giai Đoạn Trải Nghiệm:

#### Bước 1: Thức Tỉnh & Khởi Tạo Tức Thì (00:00 – 00:15)
* Màn hình mở ra với tông màu tối huyền bí.
* 3 thẻ Archetype xuất hiện mượt mà với hiệu ứng Fade-in.
* Người chơi click 1 thẻ. Dữ liệu nạp ngay vào bộ nhớ C# `PlayerState` và `SkillLoadout`.

#### Bước 2: Kích Hoạt Bản Đồ Tier 1 Đầu Tiên (00:15 – 00:45)
* **Giao diện:**
  * Sàn đấu 2D [Arena2D](file:///e:/Projects/Antigravity/IdleExile/src/UI/Components/Arena2D.cs) đang tối, cỗ máy Crucible ở giữa phát xung ánh sáng nhẹ.
  * Tấm đá `Strand Map (Tier 1)` đã được tự động nạp sẵn vào khay kích hoạt của Map Device.
  * Nút **[ACTIVATE MAP]** phát viền sáng vàng (*Golden Pulse*).
* **Hành vi người chơi:** Nhấn nút **[ACTIVATE MAP]**.
* **Phản hồi nghe nhìn:**
  * Âm thanh rách không gian rền vang. Cổng dịch chuyển hư không bùng sáng ở giữa đấu trường.
  * Nhân vật xuất hiện tại tọa độ trung tâm (220, 104) với vòng sáng Aura bao quanh.
  * Bầy quái cấp 1 (Dune Sand-spitters / Beach Zombies) bắt đầu tiến vào từ rìa màn hình.
  * Nhân vật tự động quay mặt hướng về mục tiêu gần nhất ([Auto-Facing](file:///e:/Projects/Antigravity/IdleExile/src/UI/Components/Arena2D.cs)) và thi triển kỹ năng chính liên tục.
  * Thanh [Vitometer](file:///e:/Projects/Antigravity/IdleExile/src/UI/Components/VitometerView.cs) dao động: Máu tụt nhẹ khi quái đánh trúng, Mana tiêu hao và hồi phục theo từng đòn đánh.
  * **Tâm lý người chơi:** Nhận ra ngay đây là tựa game thuần túy Zero APM — nhân vật tự động chiến đấu thông minh.

#### Bước 3: Đợt Quái Đầu & Âm Thanh Loot Drop "Ting!" (00:45 – 02:00)
* Quái Tier 1 có lượng máu vừa phải, bị tiêu diệt sau 1–2 đòn đánh để tạo cảm giác càn quét đã tay.
* Số sát thương nổi ([FloatingTextManager](file:///e:/Projects/Antigravity/IdleExile/src/UI/Components/FloatingTextManager.cs)) bay lên liên tục theo mã màu: Trắng (Vật lý), Đỏ (Hỏa).
* **Cột mốc Drop định sẵn (Guaranteed Seeded Drop tại quái thứ 12):**
  * Quái ngã xuống, một cột sáng trắng (Drop Beam) chiếu thẳng xuống điểm rơi.
  * Âm thanh *"Ting!"* kim loại vang lên đanh thép.
  * Banner Loot Toast xuất hiện ở mép trên: `[Rusty Sword - Magic]` và `[Orb of Transmutation x1]`.
  * Bộ lọc đồ tự động thu thập vào kho đồ mà người chơi không cần click nhặt thủ công.

#### Bước 4: Lên Cấp 2 & Bảng Nội Tại (Passive Web) (02:00 – 03:30)
* Nhân vật đạt đủ EXP cấp 1 $\rightarrow$ Hiệu ứng cột sáng vàng bừng lên quanh nhân vật: **LEVEL UP! (Lv 2)**.
* Tab **[Passive Tree]** ở nửa dưới cửa sổ nhấp nháy viền vàng kèm huy hiệu `(1 Point)`.
* **Cơ chế chống ngợp (Anti-Overwhelm Camera System):**
  * Khi mở Passive Tree lần đầu, hệ thống chỉ hiển thị phóng to (Zoom 150%) đúng **cụm 3 node xuất phát** của Class đã chọn. Toàn bộ mạng lưới còn lại chìm trong bóng tối (Fog of War).
  * 3 node gợi ý rõ ràng:
    * Node A: `+12% Physical Damage` (hoặc Spell Damage).
    * Node B: `+10 Maximum Life` & `+4% Increased Life`.
    * Node C: `+15 Armour / Evasion`.
* **Phản hồi sức mạnh tức thì (Instant Power Gratification):**
  * Người chơi click cộng điểm vào `+12% Physical Damage`.
  * Chỉ số **Effective DPS** trên thanh thông số nhảy ngay từ `18 DPS` lên `24 DPS`.
  * Đòn đánh tiếp theo trong Arena lập tức hiện số sát thương to hơn rõ rệt.

#### Bước 5: Khoảnh Khắc "Aha!" — Ghép Support Gem Chuẩn PoE 2 (03:30 – 05:00)
* Tiêu diệt bầy quái Tinh Anh (Magic Blue Pack đầu tiên) $\rightarrow$ Đảm bảo rớt **1x Support Gem** tương thích:
  * Juggernaut nhận: `Added Fire Damage Support` (Đỏ).
  * Elementalist nhận: `Ignite Proliferation Support` (Lam).
  * Deadeye nhận: `Multiple Projectiles Support` (Lục).
* **Gợi mở UX trực quan:**
  * Tab **[Skills]** mở khóa và nhấp nháy sáng.
  * Khi chuyển sang tab Skills, người chơi nhìn thấy Active Gem đang có **2 khe cắm Support trống** viền màu tương ứng.
* **Hành vi người chơi:** Double-click hoặc kéo thả viên Support Gem vào khe trống của Active Gem.
* **Đột phá thị giác & cơ chế:**
  * Ngay lập tức, đòn đánh trong Arena thay đổi:
    * Nhát chém `Cleave` bốc cháy đỏ rực, thiêu đốt quái xung quanh.
    * Hoặc quả cầu `Fireball` nổ lan tỏa đốm lửa bùng cháy sang cả đàn quái.
    * Hoặc mũi tên `Split Arrow` bắn thêm 2 tia đạn bay rộng hơn.
  * **Thông điệp truyền tải:** *Không cần đục lỗ trang bị, không cần nối dây Orb of Fusing; ngọc kỹ năng tự chứa lỗ và liên kết 100% tự động!*

#### Bước 6: Map Boss & Vòng Lặp Bền Vững (Map Sustain) (05:00 – 07:00)
* Thanh tiến trình [MapHeaderBar](file:///e:/Projects/Antigravity/IdleExile/src/UI/Components/MapHeaderBar.cs) đạt 100%.
* Quái thường tản ra, sàn đấu rung chuyển nhẹ, Map Boss `Tidal Behemoth` xuất hiện với thanh máu tím viền vàng.
* **Boss Slam Telegraph:**
  * Boss giơ cao nắm đấm, vòng tròn cảnh báo đỏ mờ [Arena2D Indicator](file:///e:/Projects/Antigravity/IdleExile/src/UI/Components/Arena2D.cs) co dần trong 1.5 giây.
  * Đòn đập giáng xuống: Thanh Guard Skill `Steelskin` kích hoạt làm lóe viền vàng trên thanh máu, hấp thụ toàn bộ đòn đánh.
* **Hạ Boss — Loot Explosion:**
  * Boss nổ tung, rơi ra: 1 món đồ Rare (Vàng), 1x Chaos Orb, và đặc biệt là **2x Map Đá Tiếp Theo: `Tier 1: Strand` & `Tier 2: Arid Lake`**.
  * Dòng chữ chiến thắng: `Map Cleared! Atlas Bonus: +1% Map Drop Chance`.
  * **Tâm lý:** Người chơi thấy rơi thêm 2 bản đồ mới $\rightarrow$ Xóa bỏ triệt để nỗi sợ bị kẹt hay thiếu bản đồ.

#### Bước 7: Bàn Giao Chế Độ Companion (07:00 – 10:00)
* Một banner thông báo thanh lịch trượt ra cạnh nút ghim cửa sổ:  
  *“Bạn đã làm chủ cõi giới đầu tiên. Hãy Ghim (Pin) cửa sổ ở góc màn hình và tiếp tục làm việc, Idle Exile sẽ đồng hành cùng bạn.”*
* Nút **[Auto-Repeat Next Map]** xuất hiện, cho phép cỗ máy tự động đưa Map tiếp theo vào chạy liên tục mà không cần can thiệp.

---

## 4. Hệ Thống Dẫn Dắt Bằng Ngữ Cảnh (Non-Intrusive Guidance System)

| Yếu Tố UX | Quy Cách Thể Hiện | Mục Đích Sư Phạm |
| :--- | :--- | :--- |
| **Golden Pulse (Xung Ánh Sáng Vàng)** | Shader viền sáng co giãn chu kỳ 1.2s quanh Button/Tab mục tiêu. | Thu hút mắt người chơi mà không khóa hành vi hay che khuất thông tin. |
| **Micro-Tooltips** | Khung chữ nền đen viền vàng, font 11px, tối đa 12 từ. Tự tắt sau 4s. | Hướng dẫn hành động tức thời: *"Cắm ngọc phụ trợ vào khe để tăng sức mạnh"*. |
| **Audio Signatures** | Mỗi sự kiện lớn có âm thanh độc bản: tiếng rách cổng (Portal), tiếng kim loại rớt đồ (*"Ting!"*), tiếng đập đá ghép ngọc (*"Snap!"*). | Kích thích phản xạ có điều kiện (Dopamine conditioning), tạo cảm giác thân thuộc như PoE gốc. |

---

## 5. Mở Khóa Giao Diện Lũy Tiến (Progressive Disclosure Schedule)

Để tránh hiện tượng mệt mỏi nhận thức (Cognitive Fatigue), các tab và tính năng được phân phối mở khóa theo cấp độ:

```
[Level 1]  ──► Arena Viewport + Map Device + Vitals Bar (Trải nghiệm auto-combat)
[Level 2]  ──► Tab: Passive Tree (Cụm node xuất phát cơ bản)
[Level 3]  ──► Tab: Inventory & Khay Trang Bị (Trải nghiệm đồ Magic/Rare)
[Level 4]  ──► Tab: Skills Panel (Trải nghiệm cắm Support Gems)
[Level 5]  ──► Slot Kỹ Năng Thứ 2 (Secondary / Cooldown Skill)
[Level 8]  ──► Tab: Loot Filter (Tùy chọn độ nghiêm ngặt: Regular / Strict)
[Level 10] ──► Tab: Atlas Passive Tree (Định hình chiến lược cày cuốc bản đồ)
```

Các tab chưa mở khóa sẽ hiển thị biểu tượng ổ khóa nhỏ màu xám kèm tooltip: *“Mở khóa khi đạt Cấp X”*.

---

## 6. Cơ Chế Fail-Safe & Chống Kẹt Cho Người Chơi Mới

1. **Bảo Vệ Tử Vong (Death Protection at Low Level):**  
   * Nhân vật ở Level 1 – Level 5 khi ngã xuống **không bị trừ 10% EXP**.
   * Tự động hồi sinh tại chỗ sau 2 giây với đầy đủ Máu/Mana, kích hoạt khiên miễn nhiễm sát thương 3 giây để người chơi dọn nốt bầy quái.
2. **Cơ Chế Bản Đồ Dự Phòng (The Endless Hollow - Tier 0):**  
   * Nếu người chơi lỡ tay vứt hết hoặc sử dụng hết các tấm Map Slates trong rương: Nút bản đồ vĩnh viễn **The Endless Hollow (Tier 0)** trên Map Device sẽ sáng lên.
   * Bản đồ vô tận này quái yếu, không có mod nguy hiểm, và **đảm bảo rơi 1 tấm Map Tier 1 sau khi hạ 50 quái**.
3. **Cảnh Báo Lắp Sai Thẻ Ngọc (Gem Tag Mismatch Alert):**  
   * Nếu người chơi thử gắn Support Gem không có tag trùng với Active Gem (ví dụ gắn Support đạn bay vào kỹ năng cận chiến), khe cắm sẽ rung nhẹ và hiển thị viền đỏ kèm dòng chữ: *“Không khớp Tag: Support này không hỗ trợ kỹ năng hiện tại”*.

---

## 7. Tổng Kết

Bằng cách áp dụng chặt chẽ luồng FTUE này:
* Người chơi được đưa vào trải nghiệm cốt lõi **ngay trong 15 giây đầu**.
* Hiểu rõ cơ chế ngọc PoE 2 và kinh tế Currency **trước phút thứ 5**.
* Tự tin ghim cửa sổ mini và để game tự động chạy nền bền vững **sau phút thứ 7**.
