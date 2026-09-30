# 02. Hệ Thống Bản Đồ (Mapping System) & Vực Sâu Vô Tận (The Endless Abyss)

Trong **Idle Exile**, trò chơi áp dụng mô hình tiến trình kép **Dual-Axis Progression (Mô hình 80/20)**:
* **Trục Chính (Primary Core Loop - 80%): Hệ Thống Bản Đồ (Atlas Mapping)** — Trục tiến trình trung tâm từ Level 1, nơi diễn ra vòng lặp craft Map, săn đồ, cày Currency và xây dựng chiến lược qua Atlas Passive Tree.
* **Trục Thử Thách Vô Tận (Endless Benchmark & Target Crafting - 20%): Vực Sâu Vô Tận (The Endless Abyss - Cơ chế Delve)** — Thước đo giới hạn tối thượng cho EHP và DPS của build, đồng thời là nguồn độc quyền cung cấp Hóa thạch (Fossils) và Khuôn đúc (Resonators).

---

## 1. Khởi Đầu & Cơ Chế Chống Kẹt (Fail-Safe Mechanism)

Khi bắt đầu ở Level 1:
* Người chơi được tặng sẵn **3x Tier 1 Map: Overgrown Beach** và một số viên Currency cơ bản (*Orb of Transmutation*, *Scroll of Wisdom*).
* **Cơ chế Fail-Safe (The Endless Hollow - Tier 0):**  
  Để đảm bảo người chơi không bao giờ bị "kẹt tài nguyên" (soft-lock khi hết sạch Map):
  * Cỗ máy Map Device luôn tích hợp sẵn một bản đồ vô hạn: **The Endless Hollow (Tier 0 / White Map)**.
  * Bản đồ này có quái vật tự cân bằng theo cấp độ hiện tại của nhân vật, không có mod nguy hiểm, tỉ lệ rớt đồ cơ bản và **đảm bảo 100% tỉ lệ rớt ít nhất 1 mảnh Tier 1 Map** sau khi hạ gục 50 quái vật.

---

## 2. Cấu Trúc Các Bậc Bản Đồ (Map Tiers)

Map được chia thành các phân vùng màu sắc và độ khó:

| Phân Vùng Map | Bậc (Tiers) | Cấp Quái (Monster Lv) | Đặc Điểm & Yêu Cầu Nhân Vật |
| :--- | :--- | :--- | :--- |
| **White Maps (Sơ Cấp)** | Tier 1 – Tier 5 | Lv 1 – Lv 45 | Giai đoạn làm quen, xây dựng các liên kết 3-Link, tích lũy ngọc kháng cơ bản. |
| **Yellow Maps (Trung Cấp)** | Tier 6 – Tier 10 | Lv 46 – Lv 70 | Quái bắt đầu có sát thương nguyên tố mạnh; yêu cầu kháng nguyên tố 75% và ít nhất 4-Link. Mở khóa *The Endless Abyss*. |
| **Red Maps (Cao Cấp)** | Tier 11 – Tier 15 | Lv 71 – Lv 85 | Cần 5-Link/6-Link, có các lớp phòng thủ phụ (Spell Suppression, Evasion/Armour cao, Curse). |
| **Pinnacle Maps (Tối Hậu)** | Tier 16 | Lv 86 – Lv 95 | Nơi rớt đồ Influence, phôi đồ cấp cao, chìa khóa mở Boss Tối Thượng (Pinnacle Bosses). |
| **Uber / Void Maps** | Tier 17+ | Lv 100+ | Thử thách cực hạn dành cho các build hoàn hảo (Min-Maxed). |

---

## 3. Bản Đồ Như Một Món Trang Bị (Itemized Maps)

Tương tự trang bị, bản thân các viên đá bản đồ (Map Slates) là những vật phẩm có thể **craft bằng Currency**:

```
[Normal Map (Trắng)]
  │
  ├─(Orb of Transmutation)─► [Magic Map (Xanh: 1-2 Mods)]
  │                              │
  │                              └─(Regal Orb)─► [Rare Map (Vàng: 3-6 Mods)]
  │                                                     │
  └─(Orb of Alchemy / Chaos)───────────────────────────┘
                                                        │
                                                        └─(Vaal Orb)─► [Corrupted Map (8-Mod hoặc Unidentified)]
```

### Công thức Rủi ro vs Lợi nhuận (Risk vs Reward)
Mỗi dòng thuộc tính bất lợi (Affix) đập vào Map sẽ tăng tương ứng các chỉ số thưởng:
* **+% Item Quantity (Số lượng đồ rơi)**: Tăng lượng Currency, phôi đồ và quặng Sulphite.
* **+% Item Rarity (Độ hiếm đồ)**: Tăng tỉ lệ rớt đồ Magic, Rare và Unique.
* **+% Monster Pack Size (Mật độ quái vật)**: Tăng số lượng quái xuất hiện trên mỗi nhịp (tick) -> Tăng tốc độ farm EXP và nạp bình Flask.

### Bảng Map Modifiers tiêu biểu:
* **Mods Thân thiện (Tăng Pack/Reward cơ bản):**
  * *Monsters have +25% increased Attack & Cast Speed* (+12% Quantity, +8% Rarity).
  * *Monsters deal 40% extra damage as Cold* (+15% Quantity, +10% Pack Size).
* **Mods Thử thách Build (Build Checks):**
  * *Players cannot Leech Life or Mana* (+25% Quantity, +15% Pack Size) -> Triệt tiêu các build dựa vào Leech thuần túy.
  * *Players have -15% to all Maximum Resistances* (+35% Quantity, +20% Pack Size) -> Thử thách khả năng Overcapped Resistances.
  * *Monsters reflect 18% of Physical/Elemental Damage* (+40% Quantity) -> Sát thủ với build không có Anti-Reflect.

---

## 4. Cơ Chế Chạy Map Tự Động & Hàng Đợi (Zero-APM Auto-Mapping)

Để đảm bảo triết lý **Zero APM** của một Desktop Companion (người chơi không phải click tay chọn và nạp map mỗi 45 giây):

### 4.1. Khay Hàng Đợi Bản Đồ (Map Device Queue)
* Cỗ máy Map Device có **10 ô hàng đợi (Queue Slots)**.
* Người chơi có thể xếp sẵn một chuỗi 10 bản đồ đã được craft kỹ càng.
* Khi kết thúc một bản đồ, hệ thống tự động khởi động bản đồ tiếp theo sau 3 giây độ trễ (delay) để hoàn tất việc nhặt đồ qua Loot Filter.

### 4.2. Chế Độ Cày Liên Tục (Continuous Auto-Sustain Mode)
* Nếu hàng đợi trống, người chơi có thể bật tùy chọn **Continuous Run**:
  * Thiết bị tự động tìm kiếm trong **Map Stash Tab** các bản đồ cùng Tier hoặc theo cấu hình điều kiện (Preset).
  * **Bộ lọc an toàn (Map Safety Filter):** Tự động phát hiện và bỏ qua các bản đồ có mod phản đòn (Reflect) hoặc không thể hồi phục (No Regen) nếu build của người chơi không tương thích.
  * Khi hết sạch map hợp lệ, cỗ máy tự động kích hoạt **The Endless Hollow (Tier 0)** để duy trì farm ngọc và tìm thêm map mới.

### 4.3. Tiến Trình Một Lượt Chạy (Run Simulation)
Mỗi bản đồ diễn ra trong khoảng 30 – 75 giây:
1. **Khởi đầu (Incursion):** Nhận hiệu ứng từ Map Affixes.
2. **Dọn quái (Trash Clearing - 70% thời lượng):** Quái ra theo từng đợt (waves), nhân vật tự xả kỹ năng AOE và kích hoạt buff phòng ngự.
3. **Sự kiện đặc biệt (Random League Encounters - 15% cơ hội):**
   * **Breach:** Quái vật dồn dập trong 15s với lượng drop gấp 3 lần.
   * **Harbinger:** Thả mảnh vỡ Currency đặc biệt khi chết.
   * **Voltaxic Sulphite Geyser:** Phun trào tài nguyên Sulphite để nạp năng lượng cho *The Endless Abyss*.
4. **Map Boss:** Đụng độ Boss bản đồ với lượng máu lớn. Hạ gục Boss để kích hoạt Loot Explosion, nhận điểm **Atlas Passive** và rớt Map cấp cao hơn.

---

## 5. Bảng Nội Tại Bản Đồ (Atlas Passive Tree)

Khác với bảng nội tại nhân vật, **Atlas Passive Tree** cho phép người chơi tùy biến chiến lược cày cuốc theo ý muốn:

* **Nhánh Map Sustain:** Tăng cơ hội quái vật rơi Map bậc cao hơn (+% chance for Maps to drop 1 tier higher).
* **Nhánh Currency Specialization:** Tăng xác suất rơi Chaos Orb, Exalted Orb, Divine Orb.
* **Nhánh Abyss Fueling:** Tăng sản lượng *Voltaxic Sulphite* thu được từ mỗi mỏ địa chấn trong map (+% Sulphite Capacity & Drop).
* **Nhánh Mechanic Focus (Breach / Essences):** Tăng tần suất xuất hiện vết nứt không gian hoặc quái tinh chất phong ấn.
* **Nhánh Bossing:** Tăng 50% độ khó và máu của Map Boss nhưng nhân đôi tỉ lệ rơi phôi đồ cao cấp và Unique hiếm.

---

## 6. Vực Sâu Vô Tận: The Endless Abyss (Endless Delve Mechanic)

Nếu Mapping là cỗ máy tạo dựng tài nguyên và tiến trình hàng ngày, thì **The Endless Abyss** chính là đấu trường tối thượng để kiểm tra độ trâu bò (EHP) và hỏa lực (DPS) của mọi build mà không có trần giới hạn.

```
┌────────────────────────────────────────────────────────┐
│ MAPPING (Cày Cuốc Thường Nhật)                         │
│ • Thu thập: Voltaxic Sulphite                          │
└──────────────────────────┬─────────────────────────────┘
                           │ (Nạp nhiên liệu)
                           ▼
┌────────────────────────────────────────────────────────┐
│ THE ENDLESS ABYSS (Delve Vô Tận)                       │
│ • Depth 1 ──────────────────────────────► Depth ∞       │
│ • Độ sâu tăng: Monster HP & DMG tăng theo hàm số mũ    │
│ • Thước đo EHP / DPS thực chiến (Kỷ lục cá nhân)       │
│ • Mở khóa các khe cắm Hóa Thạch trên Crafting Anvil    │
│ • RƠI ĐỘC QUYỀN: HÓA THẠCH (ABYSSAL FOSSILS)           │
│   (Nguyên liệu Target-Craft trang bị hoàn hảo)         │
└────────────────────────────────────────────────────────┘
```

### 6.1. Vòng Lặp Nhiên Liệu (Sulphite Economy)
* **Khai thác:** Khi chạy các bản đồ từ Tier 6 (Yellow Maps) trở lên, quái vật và các mạch địa chấn sẽ rơi ra **Voltaxic Sulphite**.
* **Tiêu hao:** Mỗi tầng lặn sâu (Depth Node) trong Abyss đòi hỏi một lượng Sulphite tương ứng với độ sâu.
* Thiết kế này giữ cho **cả hai hệ thống đều thiết yếu**: Người chơi không thể bỏ qua Mapping (vì cần Sulphite và Base Items), nhưng cũng không thể bỏ qua Abyss (vì cần Fossils để hoàn thiện trang bị Endgame).

### 6.2. Công Thức Scale Độ Sâu (Depth Scaling)
Độ khó trong The Endless Abyss tăng không giới hạn:
$$\text{Monster HP Multiplier} = 1.0 + (Depth \times 0.08)^{1.35}$$
$$\text{Monster Damage Multiplier} = 1.0 + (Depth \times 0.05)^{1.25}$$

* Ở các tầng sâu (Depth 300+), quái vật bắt đầu nhận thêm các thuộc tính xuyên kháng (*% Elemental Penetration*), đánh xuyên giáp (*Physical as Extra Elemental*), và miễn nhiễm một số hiệu ứng khống chế.
* Đây là môi trường hoàn hảo để người chơi khoe kỷ lục chiều sâu (*Deepest Depth Achieved*) trên Profile hoặc Bảng Xếp Hạng.

### 6.3. Khai Thác Hóa Thạch & Mở Khóa Chế Tạo (Fossils & Anvil Slots)
The Endless Abyss là nguồn **duy nhất** rơi các viên Hóa Thạch Tinh Chế và mở khóa năng lực chế tạo cao cấp:
* **Mở khóa ô cắm Hóa Thạch:** Vượt qua các mốc độ sâu (Depth 150, Depth 300) sẽ mở rộng khay cắm Hóa Thạch trên **Bàn Chế Tạo (Crafting Anvil)** lên tối đa 3 slots.
* **Fossils (Hóa thạch):** Rơi từ quái vật Abyss, tự động tích lũy vào kho và cho phép người chơi chọn cắm trực tiếp vào trang bị mà **không cần thông qua khuôn đúc (Resonators)**.
* Chi tiết công thức chế tạo xem tại [05_items_and_crafting.md](05_items_and_crafting.md).


