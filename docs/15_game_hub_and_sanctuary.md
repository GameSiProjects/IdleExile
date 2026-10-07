# 15. Thiết Kế Game Hub / Hideout (The Crucible Sanctuary) & Bộ Đếm DPS Thực Chiến (Combat DPS Counter)

Tài liệu này đặc tả toàn diện kiến trúc thiết kế của **The Crucible Sanctuary (Thánh Địa Lò Luyện Cổ Đại)** — đóng vai trò là Game Hub / Hideout trung tâm trong **Idle Exile**, đồng thời quy định cơ chế hoạt động của **Bộ Đếm DPS Thời Gian Thực Khi Chiến Đấu (In-Combat Real-Time DPS Meter)** thay thế hoàn toàn cọc gỗ giả lập.

---

## 1. Triết Lý Thiết Kế & Định Vị Bối Cảnh (Core Philosophy & Lore)

### 1.1. Bối Cảnh Tối Giản (Minimalist Dark Fantasy Lore)
> *"Kẻ Lưu Đày (Exile) không sở hữu quán trọ, thành phố hay dân cư ồn ào. Nơi trú ẩn duy nhất của bạn là **The Crucible Sanctuary** — một gian phòng đá cổ phong ấn lơ lửng giữa ranh giới của các chiều không gian dị giới.*  
> *Tại đây, cỗ máy **Crucible Map Device** ngàn năm xoay vần tỏa ra ánh sáng huyền bí, ngọn lửa trại Eldritch cháy bất diệt, và chiếc Đe Lò Luyện (**Crucible Anvil**) sẵn sàng tiếp nhận những viên ngọc ma thuật để biến đổi vũ khí trước chuyến viễn chinh tiếp theo."*

### 1.2. Trụ Cột Thiết Kế Hub Cho Cửa Sổ Desktop Companion
1. **"Zero APM, Zero Navigation Friction":**
   * Người chơi không phải điều khiển nhân vật đi bộ hay click chuột vào từng NPC giao dịch như các game ARPG truyền thống.
   * Mọi cơ chế tại Sanctuary đều được truy cập tức thì bằng **1-click trên thanh Tab Drawer** hoặc **phím tắt cục bộ (In-Window Hotkeys)**.
2. **Safe Haven Tuyệt Đối (Khu Vực An Nghỉ & Hồi Phục):**
   * Khi ở Sanctuary: Nhân vật được bảo hộ 100%, tự động hồi phục toàn bộ Life, Energy Shield, Mana và nạp đầy 5 bình dược phẩm (Flasks).
   * Không có quái vật tiếp cận, không có nguy cơ chết hay mất 10% EXP.
3. **Trung Tâm Tự Động Hóa & Chuẩn Bị Chiến Lược (Automation & Preparation Center):**
   * Quản lý hàng đợi bản đồ (Map Queue 10–15 slots) và tự động chạy liên tục (Continuous Auto-Run).
   * Vận hành Lò Nung Bụi Giả Kim (**Thaumaturgic Smelter**) tự động nung chảy trang bị rác từ Loot Filter thành tài nguyên giá trị.
   * Kết nối nhanh đến Trạm Địa Chấn Biển Sâu (**The Abyssal Resonator**) để tiêu hao Sulphite trong trục 20% Endless Abyss.
4. **Tuyệt Đối Trung Thành Với Kinh Tế & Chế Tạo Pure PoE (Không Deterministic Mod Crafting):**
   * **Loại bỏ hoàn toàn cơ chế Deterministic Mod Crafting (không có bàn chế tạo ép mod đảm bảo 100% bằng công thức cố định)**.
   * Toàn bộ tiến trình tùy biến trang bị giữ trọn vẹn sự phấn khích của **RNG Currency Slamming** (Transmute, Alteration, Chaos, Exalt, Annul, Divine, Vaal) và **Hóa Thạch Biển Sâu (Fossil Resonator Crafting)**. Đe Rèn Lò Luyện chỉ đóng vai trò là bàn thao tác chuyên dụng hỗ trợ đập đồ và xem trước chỉ số so sánh (Smart Stat Delta).
5. **Thước Đo Sức Mạnh Thực Chiến (Không Cọc Gỗ - Real-Time Combat DPS Meter):**
   * Loại bỏ cọc gỗ giả lập (Target Dummy) trong Hideout để giữ không gian thanh tịnh và tránh số liệu DPS ảo tách rời thực tế.
   * Đưa vào **Bộ Đếm DPS Chiến Đấu Thời Gian Thực (In-Combat Real-Time DPS Meter)**: Đo lường chính xác lượng sát thương thực tế gây ra trên quái vật trong quá trình vượt Map hoặc vượt tầng Abyss (tính đến toàn bộ giáp, kháng, số lượng mục tiêu và hiệu ứng Ailment DoT cộng dồn).

---

## 2. Kiến Trúc Hai Trạng Thái Sàn Đấu (The Dual-State Viewport Paradigm)

Sàn đấu 2D ở đáy cửa sổ (`640 x 172` px) vận hành theo cơ chế **Hai Trạng Thái Liền Mạch (Seamless Dual-State)**, triệt tiêu hoàn toàn màn hình tải (Loading Screen):

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           2D VIEWPORT PARADIGM                              │
├──────────────────────────────────────┬──────────────────────────────────────┤
│ 1. EXPEDITION COMBAT STATE           │ 2. SANCTUARY IDLE STATE              │
│    (Chế độ Vượt Map / Biển Sâu)      │    (Chế độ Thánh Địa Nghỉ Dưỡng)     │
├──────────────────────────────────────┼──────────────────────────────────────┤
│ • Nhân vật di chuyển liên tục (+X)   │ • Nhân vật đứng thiền định / nghỉ ngơi│
│ • Quái vật xuất hiện theo cụm/hành lang│ • Cỗ máy Crucible xoay nhẹ ở giữa  │
│ • Đạn bay 360°, quái nổ tung, máu văng │ • Lửa trại Eldritch bập bùng ấm áp │
│ • Thanh Vitometer dao động liên tục   │ • Vitometer đầy 100% (An Toàn)      │
│ • BỘ ĐẾM COMBAT DPS ĐANG HOẠT ĐỘNG   │ • Đe Rèn & Rương Đồ lấp lánh tĩnh mịch│
│ • Nút [EMERGENCY PORTAL (Space)]     │ • Nút [START EXPEDITION (Space)]     │
└──────────────────────────────────────┴──────────────────────────────────────┘
```

### Chi Tiết Thị Giác Khi Ở Chế Độ Sanctuary
* **Tọa độ nhân vật:** Ghim tại trung tâm trang trọng $(X = 240\text{px}, Y = 86\text{px})$, hướng nhìn về cỗ máy bản đồ.
* **Hiệu ứng môi trường:**
  * Lớp nền đá hoa cương cổ kính xám tro kết hợp các đường ron rãnh cổ ngữ runic phát ánh sáng xanh ngọc bích / vàng kim huyền bí.
  * Đốm than hồng từ ngọn lửa trại Eldritch bay lơ lửng nhẹ nhàng theo trục thẳng đứng.
  * Vòng tròn hào quang (Auras) của nhân vật thu nhỏ lại ở chế độ tiết kiệm năng lượng (Idle Shimmer).

---

## 3. Các Phân Khu Chức Năng Cốt Lõi Tại Sanctuary

```text
┌─────────────────────────────────────────────────────────────────┐
│                    THE CRUCIBLE SANCTUARY                       │
├────────────────────────────────┬────────────────────────────────┤
│ 1. CRUCIBLE MAP DEVICE         │ 2. THE CRUCIBLE ANVIL          │
│ • 4/5 Slots Map & Scarabs      │ • Đe thao tác Currency & Fossil│
│ • Khay Map Queue (10 slots)    │ • Xem trước so sánh Stat Delta │
│ • Continuous Auto-Run Toggle   │ • (Không công thức mod cố định)│
├────────────────────────────────┼────────────────────────────────┤
│ 3. THAUMATURGIC SMELTER        │ 4. THE ABYSSAL RESONATOR       │
│ • Tự động hóa nung rác lọc đồ  │ • Cổng tiến vào Endless Abyss  │
│ • Chuyển hóa thành Bụi Giả Kim │ • Trữ lượng Voltaxic Sulphite  │
│ • Tăng Item Quantity cho Map   │ • Kỷ lục độ sâu (Depth Record) │
├────────────────────────────────┴────────────────────────────────┤
│ 5. ATLAS TROPHY PEDESTAL                                        │
│ • Đài vinh danh: Pinnacle Boss Trophies (Maven, Exarch, Eater)  │
│ • Khảm ngọc Voidstones gia tăng cấp độ toàn bộ Atlas            │
└─────────────────────────────────────────────────────────────────┘
```

### Module 1: Cỗ Máy Bản Đồ Cổ Đại (The Crucible Map Device)
* Tích hợp đồng bộ với Tab `[ 🗺 ATLAS ]` và thanh `MapDeviceBar`.
* **4/5 Khay Đặt Bản Đồ (Aperture Slots):**
  * 1 ô trung tâm cho Map Slate (Đá bản đồ).
  * 3 ô phụ (mở rộng lên 4 ô ở Endgame) dành cho các mảnh đá gia tăng (Sacrifice Fragments, Scarabs, Divine Vessels).
* **Tự Động Hóa Vượt Bậc:**
  * **Map Queue (10–15 Slots):** Cho phép người chơi xếp sẵn hàng loạt Map đã roll mod từ Stash vào hàng đợi.
  * **Continuous Auto-Run:** Khi bật tùy chọn này, ngay khi dọn xong 1 Map và hết 3 giây nhặt đồ, hệ thống tự động khởi chạy bản đồ tiếp theo mà không cần người chơi can thiệp.

### Module 2: Đe Rèn Lò Luyện (The Crucible Anvil) — Pure Currency & Fossil Workspace
* **Triết lý:** Tôn trọng 100% kinh tế dựa trên tiền tệ của Path of Exile. **Không áp dụng công thức Crafting Bench ép dòng định sẵn (Deterministic)**.
* **Giao diện thao tác tập trung:**
  * Cung cấp một khay trung tâm để đặt trang bị mục tiêu.
  * Hiển thị bảng chọn Currency tiện lợi: *Transmute, Alteration, Augmentation, Regal, Chaos, Exalted, Annulment, Divine, Vaal*.
  * Tích hợp khay Hóa Thạch Biển Sâu (**Abyssal Resonator**): Cho phép cắm từ 1 đến 4 Hóa Thạch (Fossils) vào Khuôn đúc (Resonators) để điều chỉnh trọng số (Weighting) theo đúng cơ chế đã đặc tả ở [05_items_and_crafting.md](05_items_and_crafting.md).
* **Smart Stat Delta Preview (So Sánh Trực Quan Trước / Sau):**
  * Khi đập Currency hoặc Hóa Thạch, hệ thống hiển thị bảng so sánh biến động chỉ số xanh/đỏ (Delta) tức thì giữa trang bị cũ và trang bị vừa tạo ra, giúp người chơi nhanh chóng quyết định reroll tiếp hay giữ lại.

### Module 3: Lò Nung Thu Hồi Bụi Giả Kim (Thaumaturgic Smelter)
*Lấy cảm hứng từ cơ chế Kingsmarch của bản cập nhật PoE 3.25 Settlers of Kalguur:*
* **Vấn đề giải quyết:** Khi chạy tự động hàng chục Map, Loot Filter ẩn hàng trăm món trang bị Normal/Magic/Rare không đạt tiêu chuẩn. Người chơi thường bỏ lỡ lượng phôi đồ khổng lồ này.
* **Cơ chế vận hành:**
  * Người chơi kích hoạt tính năng `[✓] Auto-Smelt Filtered Items`.
  * Toàn bộ trang bị rơi ra bị bộ lọc Loot Filter giấu đi sẽ được lò nung tự động thu gom và phân rã thành **Thaumaturgic Dust (Bụi Giả Kim)** theo công thức:
    $$\text{Dust} = \text{Item Level} \times \text{Rarity Multiplier} \times (\text{Số lượng Affixes})$$
* **Ứng Dụng Của Bụi Giả Kim (Dust Economy):**
  1. **Map Dust Infusion (Phun sương bản đồ):** Tiêu tốn 2,500 Dust để tăng thêm $+5\%$ đến $+15\%$ Item Quantity cho 5 bản đồ tiếp theo.
  2. **Thaumaturgic Transmutation (Ngưng tụ Currency):** Tích lũy 10,000 Dust để đổi lấy các mảnh tiền tệ quý giá (*1x Orb of Unmaking* để hoàn điểm Passive hoặc *1x Chaos Orb*).

### Module 4: Trạm Địa Chấn Biển Sâu (The Abyssal Resonator / Niko's Drill)
* Điểm neo vững chắc cho **Trục phụ 20% (The Endless Abyss)**:
* Hiển thị bình chứa **Voltaxic Sulphite Tank** (dung lượng nâng cấp từ 1,000 đến 50,000).
* Hiển thị độ sâu kỷ lục (**Deepest Depth Reached**) và hệ số gia tăng quái vật theo độ sâu.
* Cung cấp nút chuyển đổi 1-click `[Descend to Abyss]` đưa sàn đấu 2D chuyển sang cảnh xe mỏ thám hiểm lao xuống lòng đất tối tăm.

### Module 5: Đài Cổ Tích & Kỳ Tích Atlas (Atlas Hall of Relics)
* Nơi lưu trữ và tôn vinh những thành tựu Endgame đỉnh cao:
  * Khảm 4 viên **Voidstones** thu thập từ các Boss Tối Thượng, nâng toàn bộ Map trên Atlas lên Tier 16.
  * Tượng đài Boss Tối Hậu: *Eater of Worlds, Searing Exarch, The Maven, The Shaper, The Elder*.
  * Khi hạ gục một Pinnacle Boss, ngọn đuốc tương ứng tại Sanctuary sẽ bùng sáng vĩnh viễn, mở khóa danh hiệu và buff bị động thẩm mỹ (Aesthetic Glow).

---

## 4. Bộ Đếm DPS Thời Gian Thực Khi Chiến Đấu (In-Combat Real-Time DPS Meter)

Thay vì một cọc gỗ đứng yên đơn điệu trong Hideout cho ra những con số sát thương lý thuyết thiếu thực tế, **Idle Exile tích hợp trực tiếp Bộ Đếm DPS Thời Gian Thực (In-Combat Real-Time DPS Meter)** vào lõi mô phỏng chiến đấu `CombatEngine` (30Hz).

### 4.1. Thuật Toán Cửa Sổ Trượt Thời Gian (Sliding Window Algorithm)
Bộ đếm hoạt động liên tục trong mọi nhịp combat (Map hoặc Abyss) với cửa sổ trượt $T_{\text{window}} = 3.0\text{s}$:

```
     t - 3.0s                                              t (Hiện tại)
        │◄──────────────── 3.0s Cửa Sổ Trượt ───────────────►│
        ├──────────┬──────────┬──────────┬──────────┬─────────┤
        │ Tick 1   │ Tick 2   │ Tick 3   │ ...      │ Tick 90 │
        └──────────┴──────────┴──────────┴──────────┴─────────┘
                   ∑ Sát thương thực tế gây ra (Hit + DoT)
```

$$\text{Effective DPS} = \frac{\sum_{i=1}^{N} \text{Damage Dealt in Last 3.0s}}{3.0\text{s}}$$

* **Sát thương tính toán:** Là sát thương **thực tế** quái vật phải gánh chịu sau khi đã trừ đi Giáp (Armour), Kháng nguyên tố (Resistances), và Kháng Hỗn Mang (Chaos Res), bao gồm:
  * **Hit Damage:** Đòn đánh trực tiếp từ vũ khí, phép thuật, đòn Crit.
  * **Ailment DoT Damage:** Sát thương theo thời gian từ Chảy máu (Bleed), Thiêu đốt (Ignite), Độc (Poison ticks).

### 4.2. Các Chỉ Số Đo Lường Chính (Core DPS Metrics)
Bộ đếm theo dõi đồng thời 3 chỉ số sinh tử:
1. **Current DPS (DPS tức thời):** Cập nhật mỗi 0.25 giây theo cửa sổ trượt 3.0s, phản ánh tốc độ dọn quái hiện tại.
2. **Peak DPS (DPS đỉnh điểm trong Map):** Lưu lại kỷ lục sát thương/giây cao nhất đạt được trong lượt chạy Map đó (thường xảy ra khi nổ xác quái đông hoặc dồn sát thương Crit vào bầy quái tinh anh).
3. **Map Average DPS (DPS trung bình cả lượt):** Tổng sát thương chia cho thời gian chạy từ lúc mở cổng đến khi diệt Boss.

### 4.3. Bảng Phân Tách Thuộc Tính Sát Thương (Damage Type Breakdown)
Khi người chơi di chuột vào ô DPS hoặc bấm vào nút mở rộng, một Tooltip chẩn đoán xuất hiện phân tách chi tiết cơ cấu sát thương:

```text
┌───────────────────────────────────────────────────┐
│ COMBAT TELEMETRY & DPS BREAKDOWN                  │
├───────────────────────────────────────────────────┤
│ • Current DPS:    142,580 /s                      │
│ • Peak Map DPS:   389,120 /s                      │
│ • Average DPS:    118,400 /s                      │
├───────────────────────────────────────────────────┤
│ THÀNH PHẦN NGUỒN SÁT THƯƠNG:                      │
│ 🗡 Physical:      42% (60,200/s)                  │
│ 🔥 Fire (Ignite): 38% (54,180/s - 12 stacks)      │
│ ⚡ Lightning:     15% (21,387/s - 32% Shock)      │
│ 🧪 Chaos (Poison): 5% (6,813/s)                   │
├───────────────────────────────────────────────────┤
│ HIỆU SUẤT ĐÒN ĐÁNH:                               │
│ • Tỉ lệ đánh trúng (Accuracy):     100%           │
│ • Tần suất đòn đánh (APS):         3.45 hits/s    │
│ • Tỉ lệ chí mạng thực tế (Crit):   48.2%          │
└───────────────────────────────────────────────────┘
```

### 4.4. Vị Trí Hiển Thị Trên Giao Diện (UI Integration)
* **Ở Chế độ Compact Mode (`640 x 320` px):**
  * Hiển thị tại Quick Footer sát đáy:  
    `[🌀 EMERGENCY PORTAL (Space)]      DPS: 142.5K • Peak: 389K | [🛡 Gear]`
  * Chữ số DPS phát sáng nhẹ ánh vàng kim khi vượt qua kỷ lục Peak DPS.
* **Ở Chế độ Expanded Mode (`640 x 780` px):**
  * Tích hợp ngay dưới thanh trạng thái tiến độ Map hoặc trên thanh Vitometer, người chơi vừa tinh chỉnh Gem / Passive vừa quan sát biến động DPS thực tế khi nhân vật đang giao tranh.

---

## 5. Tiến Trình Phát Triển Thánh Địa (Sanctuary Progression)

Thánh Địa Crucible không cố định mà **tự động biến đổi diện mạo thị giác và nâng cấp công năng** tương ứng với bậc Map cao nhất mà người chơi chinh phục:

| Bậc Atlas | Cấp Độ Thánh Địa | Thay Đổi Trực Quan Trong 2D Viewport | Công Năng Mở Khóa |
| :--- | :--- | :--- | :--- |
| **White Maps**<br>*(T1 - T5)* | **Tier I: Hollow Crypt (Phế Tích Đổ Nát)** | Tường đá rêu phong nứt nẻ, cỗ máy Crucible đóng bụi mờ, ngọn lửa trại leo lét. | • Map Device cơ bản (4 slots)<br>• Map Queue: 10 slots<br>• Đe Rèn Lò Luyện cơ bản (Currency Orbs) |
| **Yellow Maps**<br>*(T6 - T10)* | **Tier II: Awakened Foundry (Lò Luyện Thức Tỉnh)** | Sàn đá được dọn sạch, các đường ron rãnh cổ ngữ sáng rực màu xanh ngọc bích. | • Mở khóa **Trạm Địa Chấn Biển Sâu (Abyss)**<br>• Mở khóa **Thaumaturgic Smelter** (Nung rác)<br>• Khay Hóa Thạch (Fossil Resonators 1-2 slots) |
| **Red Maps**<br>*(T11 - T15)* | **Tier III: Eldritch Sanctum (Điện Thờ Dị Giới)** | Bầu trời phía sau hé lộ vết nứt không gian vũ trụ, các mảnh đá lơ lửng chống trọng lực. | • Mở rộng **Map Queue lên 15 slots**<br>• Khay Hóa Thạch cao cấp (3-4 slots Resonators)<br>• Tăng 50% tốc độ nung Bụi Giả Kim |
| **Pinnacle / Uber**<br>*(T16+)* | **Tier IV: Celestial Void (Thánh Đường Tinh Tú)** | Sàn đá mạ vàng kim, cỗ máy Map Device mở rộng ánh hào quang cực đại, 4 ngọn đuốc Boss rực cháy. | • Mở khóa **Slot Map thứ 5 (5th Device Slot)**<br>• Đài vinh danh **Pinnacle Trophies**<br>• Tối đa hóa công suất Bụi Giả Kim |

---

## 6. Thiết Kế Trải Nghiệm Người Dùng (Wireframe UI & Hotkeys)

### 6.1. Wireframe Chế Độ Mở Rộng: Expanded Mode (`640 x 780` px) Khi Mở Tab Sanctuary
Khi bấm phím `H` hoặc tab `[ 🏛 SANCTUARY ]`, nửa trên cửa sổ mở ra bảng điều hành Thánh Địa:

```text
┌─────────────────────────────────────────────────────────────────┐  Y: 0
│ [IE] IDLE EXILE  Lv.82 Juggernaut        [Pin] [⤡ Min] [🖥] [X] │  Header: 36px
├─────────────────────────────────────────────────────────────────┤  Y: 36
│                    THE CRUCIBLE SANCTUARY                       │
│                                                                 │
│  ┌─────────────────────────────┐ ┌───────────────────────────┐  │
│  │ ⚒ THE CRUCIBLE ANVIL        │ │ 🌪 THAUMATURGIC SMELTER   │  │
│  │ [Place Item to Reroll]      │ │ Dust: 14,200 / 20,000     │  │
│  │ Currency: [Chaos] [Exalt]   │ │ [✓] Auto-Smelt Filtered   │  │
│  │ Fossils:  [Metallic] [Prist]│ │ [Infuse 5 Maps (+10% IIQ)]│  │
│  │ [Smart Stat Delta Preview]  │ │                           │  │
│  └─────────────────────────────┘ └───────────────────────────┘  │
│  ┌─────────────────────────────┐ ┌───────────────────────────┐  │
│  │ ⛏ THE ABYSSAL RESONATOR     │ │ 🏆 PINNACLE RELICS        │  │
│  │ Sulphite: 6,850 / 10,000    │ │ • Eater of Worlds: [DEAD] │  │
│  │ Deepest Depth: 185m         │ │ • Searing Exarch:  [DEAD] │  │
│  │ [ DESCEND TO ABYSS (20%) ]  │ │ • The Maven:       [OPEN] │  │
│  └─────────────────────────────┘ └───────────────────────────┘  │
│                                                                 │
├─────────────────────────────────────────────────────────────────┤  Y: 460
│ [ 🛡 GEAR ] [ 💎 GEMS ] [ 🗺 ATLAS ] [ 🌳 TREE ] [ 🏛 SANCTUARY ]│  Tabs Bar: 36px
├─────────────────────────────────────────────────────────────────┤  Y: 496
│ [SANCTUARY] The Crucible • Safe Haven [Queue: 8 Maps Ready]     │  Status: 32px
│                                                                 │
│             2D SANCTUARY VIEWPORT (640 x 172 px)                │  Persistent Viewport
│   ◄── 190px ──► [EXILE] (Meditation) ◄── [Crucible Device] ──► │  (Cố định đáy)
│                                                                 │
│ [LIFE / ES] 5,420 / 5,420 HP (Full)      MANA: 1,280 (Full)     │  Vitometer: 44px
├─────────────────────────────────────────────────────────────────┤  Y: 744
│ [ 🚀 LAUNCH QUEUE (Space) ]       Current DPS: 0 (In Sanctuary) │  Footer: 36px
└─────────────────────────────────────────────────────────────────┘  Y: 780
```

### 6.2. Hệ Thống Phím Tắt Tương Tác Cục Bộ (In-Window Shortcuts)
Tuân thủ tuyệt đối quy định không hook toàn cục hệ điều hành (`AGENTS.md`):
* `H`: Chuyển đổi nhanh giữa tab hiện tại và **Sanctuary Overview**.
* `Space`:
  * *Khi đang trong Map:* Kích hoạt **Emergency Portal** tháo chạy an toàn về Sanctuary.
  * *Khi đang ở Sanctuary:* Kích hoạt **Khởi Chạy Bản Đồ** (Launch Next Map).
* `M`: Mở nhanh **Crucible Map Device & Atlas Tree**.
* `S`: Mở nhanh **Kho Đồ Tự Động (Affinity Stash)**.
* `A`: Mở nhanh **Đe Rèn Lò Luyện (Crucible Anvil)**.

---

## 7. Kiến Trúc Kỹ Thuật Mã Nguồn (Technical Architecture)

Đảm bảo nguyên tắc tách biệt tuyệt đối giữa Pure C# logic (`src/Core/`) và Godot UI presentation (`src/UI/`):

```
IdleExile/
├── data/
│   ├── sanctuary_tiers.json         # Cấu hình 4 cấp độ tiến trình Sanctuary & unlock triggers
│   └── smelter_recipes.json         # Tỷ lệ quy đổi item rác sang Thaumaturgic Dust
│
├── src/
│   ├── Core/                        # PURE C# (Zero Godot dependencies!)
│   │   ├── Models/
│   │   │   ├── SanctuaryState.cs    # Lưu trữ cấp độ Hub, lượng Dust, cấu hình tự động
│   │   │   └── CombatDpsSnapshot.cs # Struct dữ liệu đo DPS (Effective, Peak, Average, Breakdown)
│   │   └── Simulation/
│   │       ├── CombatDpsTracker.cs  # Thuật toán cửa sổ trượt 3.0s tính toán DPS thời gian thực
│   │       └── SmelterService.cs    # Tự động hóa phân rã trang bị rác ra Thaumaturgic Dust
│   │
│   └── UI/                          # GODOT ENGINE PRESENTATION
│       ├── Components/
│       │   ├── SanctuaryView.cs     # Controller giao diện Tab Sanctuary
│       │   ├── Arena2D.cs           # Mở rộng RenderState: ViewportState.Combat vs ViewportState.Sanctuary
│       │   └── CombatTelemetryPopup.cs # Popup hiển thị chi tiết Breakdown thuộc tính DPS
│       └── Main.cs                  # Điều phối trạng thái chuyển cảnh giữa Expedition & Sanctuary
```

### Mã Giả Thuật Toán Đo DPS (`CombatDpsTracker.cs`):
```csharp
namespace IdleExile.Core.Simulation;

public class CombatDpsTracker
{
    private const float WindowDuration = 3.0f;
    private readonly Queue<(float Timestamp, float Damage, DamageType Type)> _damageEvents = new();
    
    public float CurrentDps { get; private set; }
    public float PeakDps { get; private set; }
    public float TotalDamageDealt { get; private set; }
    public float CombatDuration { get; private set; }

    public void RecordDamage(float timestamp, float damage, DamageType type)
    {
        _damageEvents.Enqueue((timestamp, damage, type));
        TotalDamageDealt += damage;
    }

    public void Update(float currentTimestamp, float delta)
    {
        CombatDuration += delta;

        // Loại bỏ các sự kiện sát thương nằm ngoài cửa sổ trượt 3.0s
        while (_damageEvents.Count > 0 && currentTimestamp - _damageEvents.Peek().Timestamp > WindowDuration)
        {
            _damageEvents.Dequeue();
        }

        float sumDamageInWindow = 0f;
        foreach (var evt in _damageEvents)
        {
            sumDamageInWindow += evt.Damage;
        }

        CurrentDps = WindowDuration > 0f ? sumDamageInWindow / WindowDuration : 0f;
        if (CurrentDps > PeakDps)
        {
            PeakDps = CurrentDps;
        }
    }

    public void ResetMapSession()
    {
        _damageEvents.Clear();
        CurrentDps = 0f;
        PeakDps = 0f;
        TotalDamageDealt = 0f;
        CombatDuration = 0f;
    }
}
```

---

## 8. Kết Luận

Bản thiết kế này:
1. **Loại bỏ cơ chế Deterministic Mod Crafting**, giữ trọn vẹn sự phấn khích của hệ thống tiền tệ và Hóa thạch nguyên bản của *Path of Exile*.
2. **Loại bỏ cọc gỗ giả lập**, thay thế bằng **Bộ Đếm DPS Chiến Đấu Thời Gian Thực (In-Combat Real-Time DPS Meter)** phản ánh chính xác 100% hiệu năng thực chiến của mọi build.
3. Biến **The Crucible Sanctuary** thành trái tim vận hành không thể thiếu cho trải nghiệm *Desktop Companion* mượt mà, tiện lợi và đậm chất lý thuyết chuyên sâu.
