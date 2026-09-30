# 03. Cơ Chế Chiến Đấu & Tính Toán Chỉ Số (Combat & Stats)

Điểm ăn tiền lớn nhất của *Path of Exile* nằm ở mô hình toán học chặt chẽ. **Idle Exile** tái hiện chính xác cấu trúc này nhằm đem lại cảm giác tối ưu hóa số liệu (number-crunching) đỉnh cao cho người chơi.

---

## 1. Công Thức Tính Sát Thương Chuẩn (Damage Calculation)

Mọi nguồn sát thương trong game đều tuân theo công thức phân cấp:

$$\text{Final Damage} = \text{Base Damage} \times \left(1 + \sum \text{Increased} - \sum \text{Reduced}\right) \times \prod (1 + \text{More}) \times \prod (1 - \text{Less}) \times \text{Crit Multi}$$

### 1.1. Phân biệt "Increased" vs "More"
* **Increased / Reduced (Cộng dồn số học):**
  * Tất cả các dòng mang chữ `Increased` trên bảng nội tại, đồ đạc, đá ngọc được cộng dồn lại với nhau trước khi nhân với Base.
  * Càng có nhiều `Increased`, giá trị biên tăng thêm (diminishing return tương đối) càng giảm.
* **More / Less (Nhân hệ số mũ):**
  * Mỗi nguồn mang chữ `More` được tính là một phép nhân độc lập.
  * *Ví dụ:* Nếu nhân vật có 2 Support Gems, một gem cho `30% More Damage` và một gem cho `40% More Damage`:
    $$\text{Hệ số} = 1.30 \times 1.40 = 1.82\quad (+82\%\ \text{thực tế, không phải 70\%})$$

### 1.2. 5 Loại Sát Thương (Damage Types)
1. **Physical (Vật lý):** Nền tảng của các đòn cận chiến và cung tên; có thể gây *Bleed* và *Impale*.
2. **Fire (Hỏa):** Sát thương cơ bản cao, có cơ hội gây *Ignite* (sát thương đốt theo thời gian - DoT).
3. **Cold (Băng):** Làm chậm tốc độ hành động của quái (*Chill*) hoặc đóng băng hoàn toàn (*Freeze*).
4. **Lightning (Lôi):** Dao động sát thương rất rộng (vd: 1 – 200), có khả năng gây *Shock* (khiến quái nhận thêm tới +50% sát thương từ mọi nguồn).
5. **Chaos (Hỗn mang):** Bỏ qua hoàn toàn Energy Shield của đối thủ (đánh trực tiếp vào Life), có khả năng gây *Poison* (cộng dồn vô hạn số tầng độc).

---

## 2. Các Lớp Phòng Thủ (Defensive Layers)

Để sinh tồn ở các Tier Map cao, nhân vật không thể chỉ "dồn máu" mà bắt buộc phải kết hợp nhiều lớp phòng thủ:

```
                  ┌──────────────────────────────┐
                  │ Đòn Đánh / Phép Của Quái Vật │
                  └──────────────┬───────────────┘
                                 │
                                 ▼
                     [ 1. Né Tránh (Evasion) ]  ──(Thành công)──► Không dính đòn
                                 │
                                 ▼ (Trượt né)
                     [ 2. Đỡ Đòn (Block / Spell Block) ] ──(Thành công)──► Triệt tiêu sát thương
                                 │
                                 ▼ (Không đỡ được)
                     [ 3. Spell Suppression ]  ──(Phép)──► Giảm ngay 50% sát thương
                                 │
                                 ▼
           [ 4. Giáp & Kháng (Armour & Resistances) ] ──► Giảm trừ theo hệ số %
                                 │
                                 ▼
           [ 5. Effective Health Pool (EHP) ] ──► Trừ vào Energy Shield -> Mana (MoM) -> Life
```

### 2.1. Kháng Nguyên Tố & Hỗn Mang (Resistances)
* **Base Cap:** Mặc định tối đa là **75%**.
* **Overcap & Max Resistances:** Người chơi có thể tìm cách đẩy giới hạn này lên tới **90%** qua các node đặc biệt trên bảng Passive hoặc đồ cao cấp.
* **Cơ chế Trừ Kháng:** Quái vật ở Red Maps có mod trừ kháng; nếu người chơi chỉ vừa đủ 75% sẽ bị tụt kháng xuống mức nguy hiểm -> Khuyến khích tích lũy "Overcapped Resistance".

### 2.2. Công Thức Giáp (Armour Formula)
Giáp trong PoE không giảm % cố định, mà hiệu quả phụ thuộc vào độ lớn của đòn đánh:

$$\% \text{Damage Reduction} = \frac{\text{Armour}}{\text{Armour} + 5 \times \text{Incoming Damage}}$$

* Giáp cực kỳ hiệu quả trước các đòn đánh nhỏ và liên tục của quái đông.
* Nhưng khi đối đầu với cú dập khổng lồ của Map Boss, lượng giảm trừ của giáp sẽ tụt dốc -> Buộc người chơi phải có thêm các lớp chuyển đổi sát thương (*Physical Damage taken as Elemental*).

### 2.3. Cơ Chế Né Tránh Không Random (Evasion Entropy)
* Evasion trong game không dùng xác suất độc lập (RNG thuần) để tránh trường hợp người chơi "xui xẻo" bị đánh trúng liên tiếp 3 đòn chí mạng.
* Hệ thống sử dụng một bộ đếm **Entropy (0 - 99)**: Mỗi lần quái tấn công, giá trị Chance to Hit của quái sẽ được cộng vào bộ đếm. Khi chạm ngưỡng 100, đòn đánh chắc chắn trúng và bộ đếm trừ đi 100. Đảm bảo trải nghiệm né đòn mượt mà, công bằng.

### 2.4. Energy Shield (ES) & Chaos Inoculation
* **Energy Shield:** Hoạt động như lớp máu thứ hai trên thanh máu chính. Tự động sạc lại đầy với tốc độ cao sau 2 giây không nhận sát thương.
* **Tương tác Sát thương Chaos:** Sát thương Chaos mặc định xuyên thẳng qua ES để đánh vào Life. Người chơi phải lựa chọn:
  * Hoặc dùng đồ có dòng *"Chaos Damage does not bypass Energy Shield"*.
  * Hoặc lấy Keystone **Chaos Inoculation (CI)**: Máu tối đa khóa ở mức 1, nhưng hoàn toàn miễn nhiễm 100% với sát thương Chaos.

### 2.5. Hệ Thống Thuộc Tính Mềm & Thưởng Tự Nhiên (Soft Attributes Scaling)
Nhằm duy trì triết lý **Zero-Friction** cho một tựa game Idle Mini-Window:
* **Không Khóa Đồ (No Stat Requirements):** Mọi trang bị và gem chỉ yêu cầu Level nhân vật. Điểm Str/Dex/Int không dùng làm điều kiện cứng để mang đồ.
* **Hệ Số Thưởng Tự Nhiên (Inherent Passives):**
  * **Mỗi 10 Strength:** Tăng `+5 Máu tối đa` và `+2% Giáp (Armour)`.
  * **Mỗi 10 Dexterity:** Tăng `+20 Độ chính xác (Accuracy)` và `+2% Né tránh (Evasion)`.
  * **Mỗi 10 Intelligence:** Tăng `+5 Mana tối đa` và `+2% Khiên năng lượng (Energy Shield)`.
* **Attribute Stacking Archetypes:** Các điểm thuộc tính này là tài nguyên cơ sở cho các build endgame đặc thù (Unique Items chuyển đổi thuộc tính thành sát thương phẳng hoặc chỉ số phòng ngự chuyên sâu).

---

## 3. Mô Phỏng Chiến Đấu Theo Nhịp (Tick-Based Combat Engine)

Vì là game treo máy 2D tối giản, toàn bộ combat được tính toán theo mô hình nhịp thời gian cố định (**Fixed Ticks**):

* **Tần số chuẩn:** `30 Ticks / giây` (1 Tick = ~33.33ms — Chuẩn PoE Server Tick).
* **Lý do lựa chọn 30 Ticks/giây:**
  * **Độ chính xác cao cho cơ chế PoE:** Độ phân giải 33.33ms mô phỏng chính xác các build có Attack/Cast Speed cao (5–10 hits/s), Cooldown Recovery Breakpoints (như Cast on Crit), và hạn chế tối đa sai số làm tròn (Quantization Error).
  * **Bảo toàn tốc độ cày khi treo nền:** Khi thu nhỏ cửa sổ hoặc để game chạy nền, Godot có thể hạ FPS đồ họa xuống 10–15 FPS (`OS.LowProcessorUsageMode`). Do logic chạy trên nhịp Fixed Tick độc lập, nhân vật không bao giờ bị "chậm thời gian" (slow-motion).
  * **Hiệu năng siêu nhẹ:** Với ngôn ngữ C# (.NET 8), tính toán toán học thuần ở 30 Hz chỉ tiêu tốn ~0.2% CPU.
* Mỗi tick xử lý theo trình tự cố định:
  1. **Regen & DoT Phase:** Hồi máu/mana, trừ sát thương các hiệu ứng thiêu đốt, độc, chảy máu.
  2. **Cooldown & Buff Timers:** Đếm lùi thời gian hồi chiêu của Guard Skills, bình Flask, thời lượng hiệu ứng Buff/Debuff.
  3. **Player Action Phase:** Kiểm tra xem nhân vật đã tích đủ Action Time (dựa trên Attack/Cast Speed) để ra đòn tiếp theo hay chưa.
  4. **Monster Action Phase:** Quái vật thực hiện công kích, áp dụng toàn bộ chuỗi tính toán phòng thủ ở trên.
  5. **Death & Drop Phase:** Xác định quái vật chết, gọi hàm phân phối kinh nghiệm (EXP) và kích hoạt bộ lọc Loot Filter.

> **Lợi thế cho Engine Godot:**  
> Kiến trúc này cho phép tách rời hoàn toàn giữa **Simulation Engine (Chạy ngầm)** và **Render Layer (Hiển thị)**. Giao diện Godot vẫn render mượt mà ở 30/60 FPS qua các hiệu ứng Tween/Interpolation, trong khi logic C# chạy ngầm độc lập và chuẩn xác.

---

## 4. Hình Học Không Gian & Tính Toán Combat Top-Down Tuyến Tính (Spatial Geometry & 2D Math)

Khác với mô hình đấu trường đóng cố định hay cuộn sàn giả lập, kiến trúc chiến đấu của **Idle Exile** đưa toàn bộ tương tác vào **Hành lang bản đồ 2D kéo dài đơn hướng (World-Space Linear Corridor)** trong không gian tọa độ thế giới thực ($X_{\text{world}}, Y_{\text{world}}$):

### 4.1. Hệ Tọa Độ Thế Giới & Cơ Chế Di Chuyển Thực Tế (World Coordinates & True Movement)
* **Không gian Hành lang Thế giới:** Bản đồ mở rộng theo chiều dài $L_{\text{map}}$ (ví dụ: $Y_{\text{world}} \in [0, L_{\text{map}}]$) với độ rộng hành lang $W_{\text{corridor}} \approx 280 - 440\text{px}$.
* **Di chuyển thực sự của Nhân vật (True World Movement - Không Cuộn Sàn):**
  * Nhân vật (Exile) sở hữu vị trí thế giới thực $P_E(t) = (x_E(t), y_E(t))$ biến thiên liên tục theo thời gian khi hành quân, **tuyệt đối không dùng kỹ thuật cuộn sàn giả lập (No fake floor scrolling)**.
  * Vận tốc di chuyển thực tế:
    $$v_{\text{move}} = v_{\text{base}} \times \left(1 + \sum \frac{\% \text{Increased Movement Speed}}{100}\right) \times \prod \left(1 + \frac{\% \text{More Movement Speed}}{100}\right)$$
  * **Máy Trạng Thái Di Chuyển (Movement State Machine):**
    1. **Hành quân (Marching / Advancing):** Khi không có mục tiêu trong tầm đánh, Exile di chuyển tịnh tiến dọc theo hướng bản đồ với vận tốc $v_{\text{move}}$.
    2. **Giao chiến (Engaging / Staging):** Khi cụm quái tiếp theo lọt vào tầm nhận thức/tầm đánh hiệu dụng ($d(P_E, P_M) \le R_{\text{skill}}$), Exile dừng lại hoặc giữ khoảng cách tối ưu để xả skill.
    3. **Tiếp tục tiến bước:** Sau khi dọn sạch cụm quái, Exile tự động tiếp tục hành quân tới cụm kế tiếp.
    4. **Vào phòng Boss (Boss Room Entry):** Khi chạm mốc cổng phong ấn ở cuối hành lang, Exile bước vào trung tâm phòng Boss, camera khóa vị trí vào đấu trường và trận chiến Boss bắt đầu.
* **Camera2D Bám Theo Nhân Vật (Camera Tracking):**
  * `Camera2D` trong Viewport bám theo vị trí thực $P_E(t)$ của Exile với thuật toán làm mượt (Smooth Interpolation / Lerp) và khoảng nhìn trước (Look-ahead Offset) hướng về phía trước hành lang:
    $$P_{\text{camera}}(t) = \text{Lerp}\left(P_{\text{camera}}(t - \Delta t),\ P_E(t) + \vec{O}_{\text{lookahead}},\ \lambda \times \Delta t\right)$$
  * Người chơi nhìn thấy Exile thực sự dấn bước qua từng phiến đá, từng chướng ngại vật và từng bầy quái rải rác trên bản đồ.

### 4.2. Khoảng Cách Euclid & Vùng Tầm Đánh (Euclidean Distance & Range)
Khoảng cách giữa Kẻ Lưu Đày (Exile) tại $P_E(x_E, y_E)$ và Quái vật tại $P_M(x_M, y_M)$ được tính toán trong không gian tọa độ thế giới:

$$d(P_E, P_M) = \sqrt{(x_M - x_E)^2 + (y_M - y_E)^2}$$

* **Melee Range:** $d \le 35\text{px}$ (Đòn đánh cận chiến, áp sát).
* **Medium AoE / Aura Radius:** $d \le 70\text{px}$ (Righteous Fire, Warcries, Blasphemy Curses).
* **Ranged / Screen Projectile Range:** $d \le 220\text{px}$ (Tầm bắn cung tên, tia phép thuật quét dọc tầm nhìn Viewport).

### 4.3. Hình Thái Kỹ Năng & Công Thức Va Chạm (Skill Geometries & Hit Testing)
1. **Radial AoE (Hình Tròn Tỏa Đều 360° - Nova / Aura / Slams):**
   * Quái vật nhận sát thương khi: $d(P_{\text{origin}}, P_M) \le R_{\text{AoE}}$.
   * Hỗ trợ chỉ số `Increased Area of Effect` gia tăng bán kính hiệu dụng:
     $$R_{\text{effective}} = R_{\text{base}} \times \sqrt{1 + \sum \% \text{Increased AoE}}$$
2. **Cones & Arcs (Hình Quạt / Sóng Hình Nón - Cleave / Ground Slam / Freezing Pulse):**
   * Quái vật nằm trong góc quạt định hướng $\theta_{\text{facing}}$ với khẩu độ góc $\alpha$ (ví dụ: $\alpha = 75^\circ$):
     $$\cos(\Delta \theta) = \frac{\vec{V}_{\text{facing}} \cdot (P_M - P_E)}{|\vec{V}_{\text{facing}}| \cdot |P_M - P_E|} \ge \cos\left(\frac{\alpha}{2}\right) \quad \text{và} \quad d \le L_{\text{range}}$$
3. **Multi-Vector Projectile Spread (Chùm Đạn Rẽ Quạt - GMP / Split Arrow):**
   * Bắn ra $N$ tia đạn (ví dụ $N = 5$ với Support Gem GMP) phân bổ đều theo các góc lệch:
     $$\theta_k = \theta_{\text{aim}} + \left(k - \frac{N - 1}{2}\right) \times \Delta \phi \quad (k = 0, \dots, N-1)$$
   * Mỗi viên đạn di chuyển độc lập theo vector vận tốc $\vec{v}_k = (S \cos\theta_k, S \sin\theta_k)$ và xử lý xuyên táo (*Pierce*) hoặc nảy xích (*Chain*) sang mục tiêu gần nhất trong bán kính tìm kiếm $R_{\text{chain}} \le 60\text{px}$.

### 4.4. Ưu Tiên Mục Tiêu Tự Động (Autonomous AI Target Selection)
Hệ thống Zero APM tự động chọn mục tiêu theo thuật toán chấm điểm trọng số (Threat Scoring):

$$\text{Threat Score}(M) = W_{\text{Rarity}} \times 1000 + W_{\text{Boss}} \times 5000 + \frac{500}{d(P_E, P_M) + 1} + \text{ClusterBonus}(M)$$

* **Ưu tiên hàng đầu:** Map Boss và Quái Rare mang Aura nguy hiểm.
* **Cụm quái đông (Cluster Bonus):** Các kỹ năng AoE ưu tiên ngắm vào trung tâm khối quái có mật độ cao nhất để tối đa hóa hiệu suất DPS.
