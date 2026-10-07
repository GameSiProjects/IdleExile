# 10. Thiết Kế Chi Tiết Bộ Ba Kỹ Năng Khởi Đầu: Cleave, Fireball, Split Arrow

**Tài liệu Thiết Kế Hệ Thống & Kỹ Năng Chiến Đấu (GDD - Combat Skills Specification)**  
**Dự án:** *Idle Exile*  
**Vị trí:** Senior Game Systems & Combat Designer  
**Form Factor:** Mini-Window Desktop Companion (`440 x 780` px)  
**Tài liệu liên quan:** [03_combat_and_stats.md](03_combat_and_stats.md), [04_gems_and_passives.md](04_gems_and_passives.md), [08_first_time_user_experience.md](08_first_time_user_experience.md), [09_weapon_equipment_system.md](09_weapon_equipment_system.md)

---

## 1. TỔNG QUAN & TRIẾT LÝ THIẾT KẾ CỐT LÕI (CORE PHILOSOPHY)

Trong *Idle Exile*, trải nghiệm chiến đấu tuân thủ nguyên lý tối thượng: **"Zero APM, 100% Theorycrafting"**. Người chơi không trực tiếp click chuột để tung chiêu hay né đạn; toàn bộ chiều sâu cơ học nằm ở việc lựa chọn kỹ năng chủ lực, gắn kết các viên ngọc hỗ trợ (Support Gems), phối hợp trang bị vũ khí và định tuyến bảng nội tại.

Ba kỹ năng khởi đầu (**Cleave**, **Fireball**, **Split Arrow**) đại diện cho ba trụ cột thuộc tính kinh điển của *Path of Exile*:
* **Đỏ (Strength) — Cleave:** Biểu tượng của sức mạnh cận chiến đập tan vòng vây (Melee AoE Cleave), tận dụng vũ khí hạng nặng và cơ chế Dual-Wield độc nhất.
* **Lam (Intelligence) — Fireball:** Biểu tượng của phép thuật hủy diệt tầm xa (Spell Projectile AoE), kết hợp sát thương bộc phát và thiêu đốt diện rộng (Ignite).
* **Lục (Dexterity) — Split Arrow:** Biểu tượng của sự chuẩn xác và tốc độ xạ thủ (Bow Projectile Spread), dọn dẹp bầy quái phân tán bằng loạt tiễn hình quạt với cơ chế kiểm soát không-shotgun chuẩn mực.

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                    TAM GIÁC KỸ NĂNG KHỞI NGUYÊN (STARTER TRIAD)              │
├───────────────────────┬──────────────────────────────┬───────────────────────┤
│    CLEAVE (Strength)  │     FIREBALL (Intelligence)  │  SPLIT ARROW (Dexter) │
├───────────────────────┼──────────────────────────────┼───────────────────────┤
│ • Cận chiến hình cung │ • Phép phóng nổ diện rộng    │ • Loạt tiễn hình quạt │
│ • Quét quái áp sát    │ • Dồn sát thương đơn & cụm   │ • Quét sạch quái dàn  │
│ • Cơ chế Dual-Wield   │ • Thiêu đốt lan tỏa (Ignite) │ • Cơ chế Non-Shotgun  │
│ • Ăn theo vũ khí 100% │ • Tăng tiến cấp độ Gem thuần │ • Ăn theo Cung + Tên  │
└───────────────────────┴──────────────────────────────┴───────────────────────┘
```

### Tiêu Chuẩn Thiết Kế Cho Mini-Window (`440 x 208` px Arena):
1. **Định Danh Trực Quan Tức Thì (Diagnostic Readability):** Người chơi liếc mắt vào cửa sổ phụ chỉ trong 0.5 giây phải nhận ra ngay Exile đang dùng kỹ năng gì qua hình thái đạn đạo/vệt chém và màu sắc nguyên tố chuẩn PoE.
2. **Cân Bằng Tiêu Thụ Mana & Tốc Độ Xuất Chiêu:** Đảm bảo với lượng Mana cơ bản (50 - 100 Mana) và tốc độ hồi phục ban đầu, nhân vật không bị nghẽn đòn đánh trong vòng lặp Idle 30Hz.
3. **Hiệu Ứng Đột Phá Khi Khảm Support Gem (PoE 2 Aha! Moment):** Đúng như kịch bản FTUE Step 5, mỗi kỹ năng khi được gắn Support Gem tương ứng (`Added Fire`, `Ignite Proliferation`, `Multiple Projectiles`) phải biến đổi rõ rệt cả về mặt toán học lẫn hiệu ứng thị giác trên sàn đấu 2D.

---

## 2. KỸ NĂNG CẬN CHIẾN: CLEAVE (CHÉM QUÉT VÒNG CUNG)

```
       [Quái 1]       [Quái 2]
           \             /
            \   180°    /
             \ Bán Kính/
              \  45px /
               \     /
                ▼   ▼
            (  EXILE  )
```

### 2.1. Bản Sắc & Thẻ Kỹ Năng (Tags & Identity)
* **Tên hiển thị:** Cleave
* **Màu sắc:** Đỏ (Strength Gem)
* **Tags:** `["attack", "melee", "aoe", "physical"]`
* **Vũ khí tương thích (Weapon Restrictions):** Rìu (Axes) hoặc Kiếm (Swords) — Một tay (1H), Hai tay (2H), hoặc Song thủ (Dual-Wield). Không thể thi triển nếu cầm Chùy, Gậy, Cung hoặc Tay không (Unarmed).
* **Mô tả cốt lõi:** *"Vung vũ khí cận chiến theo một góc quạt rộng hình bán nguyệt phía trước, chém nát tất cả kẻ thù trong tầm quét."*

### 2.2. Thông Số Toán Học & Cấp Độ Tiến Trình (Stats & Scaling)

| Chỉ số kỹ năng | Level 1 (Khởi điểm) | Level 10 (Mid-game) | Level 20 (Endgame) | Ghi chú thiết kế |
| :--- | :--- | :--- | :--- | :--- |
| **Damage Multiplier (% Base)** | **125%** | **165%** | **215%** | Nhân trực tiếp với Base Damage của vũ khí |
| **Attack Speed Multiplier** | 100% | 100% | 100% | Đồng bộ hoàn toàn với APS của phôi vũ khí |
| **Mana Cost (Năng lượng tiêu tốn)** | **6** | **8** | **11** | Tiêu hao vừa phải, an toàn cho build máu/giáp |
| **Bán kính quét (Sweep Radius)** | 45 px | 50 px | 58 px | Tỉ lệ chuẩn trong sàn đấu 2D 440x208 px |
| **Góc quét quạt (Sweep Arc)** | 180° | 180° | 180° | Nửa cung tròn hướng theo Facing Angle của Exile |
| **Hiệu lực gây Chảy máu (Bleed Chance)** | 0% cơ sở | 0% cơ sở | 0% cơ sở | Kích hoạt qua vũ khí Rìu hoặc Support Gem |

### 2.3. Cơ Chế Dual-Wielding Đặc Thù Chuẩn PoE (Dual-Wield Cleave Mechanics)
Cleave sở hữu cơ chế kinh điển của *Path of Exile*:
* Khi trang bị **Dual-Wield** (cầm 2 vũ khí 1 tay thuộc hệ Kiếm/Rìu):
  1. Đòn đánh chém đồng thời **cả hai vũ khí cùng một lúc** trong một đòn vung duy nhất (không luân phiên từng tay như đòn đánh thường).
  2. Sát thương mỗi vũ khí được tính ở mức **60% Base Damage** của vũ khí đó, tổng cộng tạo thành **120% tổng sát thương đòn đánh**.
  3. Hưởng trọn vẹn nội tại của Dual-Wield: **`10% More Attack Speed`** và **`15% Additional Chance to Block`**.
  4. Hai vũ khí kiểm tra tỷ lệ bạo kích (Crit Chance) và hiệu ứng trạng thái (Ailments) độc lập, sau đó gộp tổng sát thương thành một số nổi (Damage Number) duy nhất trên mỗi kẻ thù.

### 2.4. Hành Vi Tự Động Hóa Trong CombatEngine 30Hz
1. **Targeting:** AI tự động xác định quái vật gần nhất nằm trong tầm 60px.
2. **Auto-Facing:** Exile xoay mặt về phía mục tiêu trọng tâm.
3. **AoE Detection:** Tất cả quái vật sống nằm trong góc 180° và khoảng cách $\le 45\text{px}$ đều được tính là trúng đòn (không giới hạn số lượng mục tiêu tối đa trong góc quét).
4. **Damage Pipeline:** Sát thương vật lý tính toán qua công thức Armor Mitigation của từng con quái; nếu có ngọc hỗ trợ nguyên tố, sát thương nguyên tố tính qua bảng kháng (Resistances).

### 2.5. Hiệu Ứng Thị Giác, Âm Thanh & Đột Phá Hỗ Trợ (Visual, SFX & Gem Synergy)
* **Đồ họa cơ bản:** Một vệt chém bán nguyệt sắc bén màu ánh bạc (Silver-White Arc Mesh) bung ra từ vị trí Exile theo hướng mặt, kèm các tia lửa tóe trên sàn đá và vệt bụi lốc nhỏ.
* **Âm thanh:** Tiếng xé gió rít mạnh *"Whoosh!"* kết hợp tiếng kim loại chém ngọt *"Shing-Clang!"*.
* **Đột phá khi gắn `Added Fire Damage Support` (FTUE Step 5):**
  * Vệt chém chuyển ngay sang màu đỏ rực rực lửa pha vàng cam (`#FF5522`).
  * 35% sát thương vật lý được cộng thêm dưới dạng Hỏa sát thương (`Gain 35% Physical as Extra Fire`).
  * Quái vật bị chém trúng phát ra tia lửa bốc cháy và chịu hiệu ứng Thiêu Đốt (Ignite DoT).

---

## 3. KỸ NĂNG PHÉP THUẬT: FIREBALL (CẦU LỬA BÙNG NỔ)

```
               [Quái B]
               / 
        [Quái A] ── (VÙNG NỔ AOE BÁN KÍNH 32px)
               \
               [Quái C]
                  ▲
                  │  (Đạn cầu lửa bay thẳng 540 px/s)
                  │
              ( EXILE )
```

### 3.1. Bản Sắc & Thẻ Kỹ Năng (Tags & Identity)
* **Tên hiển thị:** Fireball
* **Màu sắc:** Lam (Intelligence Gem)
* **Tags:** `["spell", "projectile", "fire", "aoe"]`
* **Vũ khí tương thích:** Gậy phép (Wand), Pháp trượng (Sceptre), Quyền trượng (Staff), hoặc Tay không (Unarmed). Không yêu cầu vũ khí có sát thương đòn đánh.
* **Mô tả cốt lõi:** *"Bắn ra một quả cầu lửa rực sáng về phía mục tiêu, phát nổ thành một quầng lửa diện rộng khi va chạm, thiêu rụi kẻ thù xung quanh."*

### 3.2. Thông Số Toán Học & Cấp Độ Tiến Trình (Stats & Scaling)

Khác với kỹ năng đòn đánh, sát thương của Fireball **không phụ thuộc vào chỉ số Base Damage của vũ khí** mà tăng tiến theo cấp độ của chính viên Gem:

| Chỉ số kỹ năng | Level 1 (Khởi điểm) | Level 10 (Mid-game) | Level 20 (Endgame) | Ghi chú thiết kế |
| :--- | :--- | :--- | :--- | :--- |
| **Sát thương Hỏa cơ bản (Base Fire Damage)** | **25 – 38** | **180 – 270** | **1095 – 1642** | Tăng theo hàm số mũ chuẩn PoE (~11% mỗi cấp) |
| **Thời gian thi triển (Base Cast Time)** | **0.75 giây** | **0.75 giây** | **0.75 giây** | Tương đương 1.33 Casts Per Second (CPS) |
| **Mana Cost (Năng lượng tiêu tốn)** | **8** | **15** | **25** | Yêu cầu trang bị bổ trợ Mana Regen hoặc ES |
| **Tỷ lệ chí mạng cơ sở (Base Crit)** | 6.0% | 6.0% | 6.0% | Nền tảng tuyệt vời cho build Spell Crit |
| **Bán kính nổ diện rộng (Explosion Radius)** | 32 px | 34 px | 38 px | Tác động cụm quái đứng sát nhau |
| **Tỷ lệ gây Thiêu Đốt cơ sở (Ignite Chance)** | **25%** | 25% | 25% | Đòn đánh thường đã có 1/4 cơ hội đốt cháy quái |
| **Tốc độ đạn bay (Projectile Speed)** | 540 px/s | 540 px/s | 540 px/s | Đạn bay nhanh, phản hồi thị giác tức thì |

### 3.3. Hành Vi Tự Động Hóa Trong CombatEngine 30Hz
1. **Targeting Ưu Tiên Mật Độ (Cluster Density Selection):**
   * Thuật toán AI tìm kiếm trong danh sách quái sống mục tiêu nào có nhiều đồng minh nhất đứng trong vòng tròn bán kính 32px xung quanh nó.
   * Nếu có Boss hoặc quái Rare xuất hiện, chuyển mục tiêu ưu tiên vào Boss/Rare.
2. **Bay & Va Chạm (Projectile Travel & Detonation):**
   * Đạn được phóng từ đầu gậy (`WandTip`) với góc hướng thẳng tới mục tiêu.
   * Khi chạm vào mục tiêu đầu tiên trên đường bay: Quả cầu phát nổ tức thì.
   * Toàn bộ quái vật nằm trong bán kính 32px nhận 100% sát thương Hỏa của vụ nổ (Full AoE Damage).
3. **Ignite Damage-over-Time:**
   * Khi cú đánh gây bạo kích hoặc kích hoạt 25% Ignite Chance:
     $$\text{Ignite DPS} = 90\% \times \text{Base Fire Damage}$$
     kéo dài trong 4 giây. DoT này được trừ trực tiếp qua phase Regen/DoT mỗi tick 33.33ms.

### 3.4. Hiệu Ứng Thị Giác, Âm Thanh & Đột Phá Hỗ Trợ (Visual, SFX & Gem Synergy)
* **Đồ họa cơ bản:** Một khối cầu sáng rực lửa 2D bán kính 4px, tâm trắng rực, bao bọc bởi quầng hào quang màu cam đậm và kéo theo đuôi khói than hồng (Ember Particles). Khi va chạm, một vòng sốc lửa (Fiery Shockwave Ring) bán kính 32px bùng nở trong 0.15s kèm hiệu ứng rung màn hình nhẹ (0.8 magnitude).
* **Âm thanh:** Tiếng rít lửa khi phóng *"Fsshh-Woosh!"* và tiếng nổ đanh gọn *"KABOOM!"*.
* **Đột phá khi gắn `Ignite Proliferation Support` (FTUE Step 5):**
  * Vụ nổ tạo ra một vầng hào quang lửa rực sáng liên tục dưới chân quái vật bị cháy (bán kính lây lan 28px).
  * Toàn bộ quái vật chạy ngang qua hoặc đứng gần quái đang bị thiêu đốt đều bị lây nhiễm trạng thái Ignite ngay lập tức mà không cần trúng thêm quả cầu lửa nào khác.
  * Tăng thêm **`20% More Fire Damage`** vào tổng sát thương thiêu đốt.

---

## 4. KỸ NĂNG XẠ THỦ: SPLIT ARROW (MƯA TIỄN HÌNH QUẠT)

```
                 ──► [Quái 1] (Mũi tên 1)
               /
             ───►  [Quái 2] (Mũi tên 2)
     EXILE ────►   [Quái 3] (Mũi tên 3)
             ───►  [Quái 4] (Mũi tên 4)
               \
                 ──► [Quái 5] (Mũi tên 5)
           (Góc quạt 45°, KHÔNG Shotgun cùng mục tiêu)
```

### 4.1. Bản Sắc & Thẻ Kỹ Năng (Tags & Identity)
* **Tên hiển thị:** Split Arrow
* **Màu sắc:** Xanh Lục (Dexterity Gem)
* **Tags:** `["attack", "bow", "projectile", "physical"]`
* **Vũ khí tương thích:** Bắt buộc trang bị Cung (Bow) và Bao tên (Quiver). Không thể dùng với súng, nỏ, dao găm hoặc vũ khí cận chiến.
* **Mô tả cốt lõi:** *"Bắn ra một loạt nhiều mũi tên cùng lúc theo hình cánh quạt, càn quét toàn bộ đội hình kẻ thù trên diện rộng."*

### 4.2. Thông Số Toán Học & Cấp Độ Tiến Trình (Stats & Scaling)

| Chỉ số kỹ năng | Level 1 (Khởi điểm) | Level 10 (Mid-game) | Level 20 (Endgame) | Ghi chú thiết kế |
| :--- | :--- | :--- | :--- | :--- |
| **Damage Multiplier (% Base)** | **110%** | **145%** | **180%** | Tác động theo sát thương cơ bản của Cung |
| **Attack Speed Multiplier** | 100% | 100% | 100% | Ăn theo tốc độ bắn cực nhanh của Cung (1.3 - 1.5 APS) |
| **Mana Cost (Năng lượng tiêu tốn)** | **5** | **7** | **10** | Rất tiết kiệm năng lượng, phù hợp xả đạn liên hồi |
| **Số lượng mũi tên (Arrow Count)** | **4 mũi tên** | **6 mũi tên** | **8 mũi tên** | Tự động tăng thêm tên khi Gem tăng cấp |
| **Góc mở hình quạt (Fan Spread Angle)**| 45° | 52° | 60° | Góc mở rộng dần theo số lượng mũi tên |
| **Tốc độ tên bay (Arrow Speed)** | **720 px/s** | **720 px/s** | **720 px/s** | Nhanh nhất trong game, găm đích gần như tức thì |
| **Quy tắc Shotgunning** | **KHÔNG** | **KHÔNG** | **KHÔNG** | 1 mục tiêu chỉ nhận sát thương từ tối đa 1 mũi tên |

### 4.3. Cơ Chế Non-Shotgunning Chuẩn PoE
* **Nguyên tắc bất di bất dịch:** Trong cùng một loạt bắn (Volley), một quái vật dù có kích thước to đến đâu hoặc đứng sát Exile đến mức nào cũng **chỉ trúng tối đa 1 mũi tên**. Các mũi tên khác cùng loạt sẽ bay xuyên qua hoặc trượt sang kẻ thù khác.
* **Ý nghĩa thiết kế:**
  * Giữ cho Split Arrow đóng đúng vai trò là **"Vua dọn quái dàn mỏng" (Trash Swarm Clearing King)**, ngăn chặn việc người chơi đứng sát Boss để gây sát thương gấp 8 lần (Shotgun exploit).
  * Với mục tiêu đơn độc (Map Boss), Split Arrow chỉ gây sát thương như 1 phát bắn thông thường (110% - 180% Base Damage), thúc đẩy người chơi phối hợp khay kỹ năng phụ (Secondary Skill) hoặc hỗ trợ cắm thêm ngọc Bleed / Puncture.

### 4.4. Hành Vi Tự Động Hóa Trong CombatEngine 30Hz
1. **Targeting:** AI tìm vị trí trung tâm trọng tâm của toàn bộ đàn quái (Centroid of Monster Pack).
2. **Fan Distribution:** Loạt mũi tên được tính toán chia đều góc:
   $$\theta_i = \text{FacingAngle} - \frac{\text{SpreadAngle}}{2} + i \times \frac{\text{SpreadAngle}}{N - 1}$$
   với $N$ là tổng số mũi tên và $i \in [0, N-1]$.
3. **Collision Check:** Mỗi mũi tên di chuyển độc lập trong danh sách Projectile Pool; mũi tên chạm mục tiêu đầu tiên sẽ kích hoạt Hit, tiêu biến mũi tên đó và đánh dấu mục tiêu đã bị trúng đạn của loạt này.

### 4.5. Hiệu Ứng Thị Giác, Âm Thanh & Đột Phá Hỗ Trợ (Visual, SFX & Gem Synergy)
* **Đồ họa cơ bản:** 4 tia sáng mảnh, dài 14px, đầu nhọn màu trắng bạc khí động học, thân kéo vệt sáng xanh lục mờ (`#55FF88`), phóng vụt đi với góc quạt 45°.
* **Âm thanh:** Tiếng bật dây cung đanh gọn *"Thwip-Twang!"* kết hợp chuỗi tiếng tên cắm phập vào mục tiêu liên tiếp *"Thud-Thud-Thud!"*.
* **Đột phá khi gắn `Lesser Multiple Projectiles Support (LMP)` (FTUE Step 5):**
  * Số lượng mũi tên tăng từ **4 lên 6 mũi tên** ngay tại cấp 1.
  * Góc quạt mở rộng từ 45° lên 60°, bao phủ gần như toàn bộ nửa bên phải của sàn đấu 2D.
  * Mặc dù chịu phạt `-15% Less Projectile Damage`, nhưng diện tích bao phủ tăng thêm 50%, giúp dọn sạch hoàn toàn các đợt quái phân tán chỉ trong 1 đến 2 nhịp bắn.

---

## 5. BẢNG MA TRẬN SO SÁNH & CÂN BẰNG TOÀN DIỆN (COMPARISON & BALANCE MATRIX)

Bảng so sánh trực quan giữa 3 kỹ năng khởi đầu nhằm đảm bảo không có kỹ năng nào bị lép vế hoàn toàn (No Dominant Strategy):

| Tiêu chí thiết kế | CLEAVE (Cận chiến) | FIREBALL (Phép thuật) | SPLIT ARROW (Xạ thủ) |
| :--- | :--- | :--- | :--- |
| **Hệ thuộc tính chính** | **Strength (Đỏ)** | **Intelligence (Lam)** | **Dexterity (Lục)** |
| **Nguồn sát thương cơ sở** | 100% ăn theo Vũ khí (Sword/Axe) | 100% ăn theo Cấp độ viên Gem | 100% ăn theo Cung & Bao tên |
| **Tốc độ thi triển cơ sở** | Theo APS vũ khí (1.2 – 1.4/s) | 0.75s Cast Time (1.33/s) | Theo APS Cung (1.3 – 1.5/s) |
| **Cơ chế tác động không gian** | Bán nguyệt 180° quanh Exile | Đạn bay $\rightarrow$ Nổ tròn AoE | Cánh quạt tia đạn phân nhánh |
| **Độ bao phủ quái vật (Coverage)**| Cận cảnh, góc rộng 45px | Tầm xa, cụm tập trung 32px | Tầm cực xa, phân tán 60° |
| **Hiệu ứng khống chế / DoT** | Gây Chảy máu (Bleed) | Gây Thiêu đốt (Ignite) | Đẩy lùi (Knockback) / Shock |
| **Sát thương đơn mục tiêu (Boss)**| **Rất Tốt** (Đặc biệt khi Dual-Wield)| **Xuất Sắc** (Ignite dồn dập) | **Trung Bình** (Bị giới hạn Non-Shotgun) |
| **Hiệu suất dọn quái rác (Swarm)** | Tốt (Nếu quái áp sát) | Tốt (Nếu quái đứng cụm) | **Vô Địch** (Quét sạch quái phân tán) |
| **Chi phí Năng lượng (Mana Drain)**| Thấp (6 - 11 Mana) | Cao (8 - 25 Mana) | Thấp (5 - 10 Mana) |
| **Độ phụ thuộc vũ khí nhặt được** | **Rất Cao** (Phải up vũ khí liên tục)| **Thấp** (Chỉ cần tăng cấp Gem) | **Cao** (Cần Cung có pDPS tốt) |

---

## 6. ĐỒNG BỘ DỮ LIỆU JSON (`data/gems.json`)

Để hệ thống C# Simulation và Visual Rendering có thể nạp chính xác các tham số thiết kế trên mà không bị hardcode, cấu trúc của `data/gems.json` được chuẩn hóa bổ sung các trường chuyên biệt:

```json
[
  {
    "id": "cleave",
    "name": "Cleave",
    "category": "Active",
    "color": "Red",
    "tags": ["attack", "melee", "aoe", "physical"],
    "weaponRestrictions": ["sword", "axe"],
    "baseStats": {
      "damageMultiplier": 125.0,
      "damageType": "Physical",
      "attackSpeedMultiplier": 100.0,
      "manaCost": 6,
      "sweepAngle": 180.0,
      "aoeRadius": 45.0,
      "dualWieldDamagePercent": 60.0
    },
    "description": "Swings melee weapons in a 180-degree arc, hitting all enemies in range. Strikes with both weapons when dual-wielding."
  },
  {
    "id": "fireball",
    "name": "Fireball",
    "category": "Active",
    "color": "Blue",
    "tags": ["spell", "projectile", "fire", "aoe"],
    "weaponRestrictions": ["wand", "sceptre", "staff"],
    "baseStats": {
      "baseDamageMin": 25,
      "baseDamageMax": 38,
      "damageType": "Fire",
      "castTime": 0.75,
      "criticalStrikeChance": 6.0,
      "manaCost": 8,
      "projectileSpeed": 540.0,
      "aoeRadius": 32.0,
      "baseIgniteChance": 25.0
    },
    "description": "Unleashes a ball of fire towards a target, exploding on impact to deal fire damage in a 32px area with 25% ignite chance."
  },
  {
    "id": "split_arrow",
    "name": "Split Arrow",
    "category": "Active",
    "color": "Green",
    "tags": ["attack", "bow", "projectile", "physical"],
    "weaponRestrictions": ["bow"],
    "baseStats": {
      "damageMultiplier": 110.0,
      "damageType": "Physical",
      "attackSpeedMultiplier": 100.0,
      "manaCost": 5,
      "projectileCount": 4,
      "projectileSpeed": 720.0,
      "spreadAngle": 45.0,
      "canShotgun": false
    },
    "description": "Fires 4 arrows simultaneously in a 45-degree spread. Arrows from the same volley cannot hit the same target."
  }
]
```

---

## 7. KẾ HOẠCH TRIỂN KHAI & LIÊN KẾT MÃ NGUỒN (CODE INTEGRATION ROADMAP)

1. **Cập nhật `data/gems.json`:** Bổ sung các trường `weaponRestrictions`, `aoeRadius`, `sweepAngle`, `projectileCount`, `projectileSpeed`, `spreadAngle`, `canShotgun`.
2. **Cập nhật `src/Core/State/SkillLoadout.cs`:**
   - Hỗ trợ phương thức truy xuất `GetAoeRadius()`, `GetProjectileCount()`, `GetSpreadAngle()`.
   - Kiểm tra tương thích vũ khí đang trang bị với `weaponRestrictions`.
3. **Cập nhật `src/Core/Simulation/CombatEngine.cs`:**
   - Trong Phase 3 (Player Action):
     - Nếu Active Gem là `Cleave` $\rightarrow$ Gọi quét quái theo góc quạt bán kính `aoeRadius`. Nếu đang Dual-Wield, áp dụng hệ số gộp 2 vũ khí.
     - Nếu Active Gem là `Fireball` $\rightarrow$ Tìm cụm quái mật độ cao nhất, gây sát thương bán kính nổ `aoeRadius` quanh mục tiêu va chạm.
     - Nếu Active Gem là `Split Arrow` $\rightarrow$ Áp dụng `Targeting.SelectMultiTargets` với số lượng mục tiêu tối đa bằng `projectileCount`, tuân thủ triệt để nguyên tắc Non-Shotgun.
4. **Cập nhật `src/UI/Components/Arena2D.cs`:**
   - Bổ sung hàm vẽ vệt chém `DrawMeleeArcSlash(...)` cho Cleave.
   - Bổ sung hiệu ứng nổ `SpawnExplosionBlast(...)` cho Fireball.
   - Bổ sung hàm bắn chùm tiễn đa góc `SpawnFanProjectiles(...)` cho Split Arrow.
