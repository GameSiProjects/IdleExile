# 05. Trang Bị, Tiền Tệ & Chế Tạo (Items & Crafting)

Hệ thống kinh tế của **Idle Exile** hoàn toàn trung thành với triết lý không dùng vàng (Gold-less economy) của *Path of Exile*. Tiền tệ vừa là vật phẩm giao dịch, vừa là công cụ định hình số phận của trang bị và ngọc kỹ năng.

> **Lưu ý thiết kế cốt lõi (PoE 2 Alignment):**  
> Trang bị (Vũ khí, Áo giáp, Mũ, Găng, Giày, Trang sức) **hoàn toàn KHÔNG có Sockets**. Toàn bộ sức mạnh trang bị tập trung 100% vào **Chỉ số cơ bản (Base Stats)** và **Các dòng thuộc tính (Affixes)**. Người chơi có thể tự do thay đồ ngay khi nhặt được mà không bao giờ bị "kẹt liên kết kỹ năng".

---

## 1. Cấu Trúc Trang Bị & Phẩm Cấp Dòng Thuộc Tính (Equipment & Affix Architecture)

> **Tài liệu đặc tả chi tiết chuyên sâu:**  
> • Xem toàn diện kiến trúc 10 ô trang bị & ma trận ngân sách tại: **[11_equipment_system_design.md](11_equipment_system_design.md)**  
> • Xem chi tiết hệ thống vũ khí & công thức tính DPS tại: **[09_weapon_equipment_system.md](09_weapon_equipment_system.md)**

### 1.1. Bố Cục 10 Ô Trang Bị Chuẩn (10 Equipment Slots)
Nhân vật trang bị tối đa 10 món đồ với ngân sách thuộc tính phân định rành mạch:
* **Vũ khí & Tay cầm:** `Main Hand` (Vũ khí chính) và `Off Hand` (Vũ khí phụ / Khiên / Bao tên Quiver). Cầm 2 vũ khí 1H kích hoạt nội tại Dual-Wield (+10% More APS, +15% Block).
* **Phòng thủ thân thể:** `Helmet` (Mũ), `Body Armour` (Áo giáp), `Gloves` (Găng tay), `Boots` (Giày), `Belt` (Thắt lưng).
* **Trang sức (Jewellery):** `Amulet` (Dây chuyền) và `Ring 1`, `Ring 2` (2 Nhẫn).

### 1.2. Phân Cấp Phẩm Vị (Item Rarity)
Mọi trang bị trong game đều tuân theo phân cấp nghiêm ngặt:

```
[ Normal (Trắng) ] ──(0 Affix)── Phôi trắng nguyên bản dùng để chế tạo
       │
       ▼
[ Magic (Xanh) ]   ──(Tối đa 1 Prefix + 1 Suffix)
       │
       ▼
[ Rare (Vàng) ]    ──(Tối đa 3 Prefixes + 3 Suffixes = Tổng cộng 6 Affixes)
       │
       ▼
[ Unique (Cam) ]   ──(Chỉ số cố định, sở hữu cơ chế độc nhất bẻ cong quy tắc)
```

### 1.3. Cấu Trúc Tiền Tố & Hậu Tố (Prefix & Suffix Rules)
* **Prefix (Tiền tố):** Thường mang các chỉ số phòng thủ và sát thương chính:
  * Flat Life / Energy Shield (+Máu tối đa, +Khiên năng lượng).
  * Flat Physical/Elemental Damage (+Sát thương cơ bản cho vũ khí).
  * % Increased Physical Damage / % Increased Armour / Evasion / ES.
  * +1 to Level of Skill Gems (Amulet, Vũ khí phép).
* **Suffix (Hậu tố):** Thường mang các chỉ số tiện ích, tốc độ và kháng tính:
  * Kháng nguyên tố & Hỗn mang: +% Fire / Cold / Lightning / Chaos Resistance.
  * Tốc độ: Attack Speed, Cast Speed.
  * Chí mạng: Critical Strike Chance, Critical Strike Multiplier.
  * Tiện ích: +% Movement Speed (Boots), Spell Suppression.
  * *(Lưu ý: Các dòng thuộc tính lẻ lặt vặt như +12 Dex/Int bị loại bỏ khỏi Suffix pool để chống loãng affix. Thuộc tính chỉ tồn tại ở Implicits phôi đồ hoặc Unique).*

### 1.4. Phân Biệt Toán Học: Local Modifiers vs Global Modifiers
* **Local Modifiers:** Nằm trên Vũ khí hoặc Áo giáp/Khiên có nhãn `local`, tác động trực tiếp vào cơ sở tính toán của chính món đồ đó (Base Damage, Base Armour/ES) trước khi đưa vào các phép nhân tổng.
* **Global Modifiers:** Nằm trên Trang sức, Thắt lưng, Bảng nội tại, tác động lên toàn bộ nhân vật theo công thức phân cấp:
  $$\text{Final Damage} = \text{Weapon Base} \times \left(1 + \sum \text{Global Increased}\right) \times \prod (1 + \text{More})$$

### 1.5. Cấp Độ Vật Phẩm (Item Level - ilvl)
* Vật phẩm rớt ở Map càng cao thì `ilvl` càng lớn.
* `ilvl` quyết định Tier cao nhất của dòng có thể xuất hiện (ví dụ: dòng *Tier 1 Flat Life (+120-129 Life)* chỉ có thể xuất hiện trên phôi đồ `ilvl 86+` rơi từ Endgame Boss/T16+ Maps).

### 1.6. Hệ Thống Thuộc Tính Mềm (Soft Attributes - Phương Án C)
* **100% Không Khóa Đồ (Zero Stat-Locking):** Mọi trang bị và ngọc kỹ năng **CHỈ yêu cầu Cấp độ Nhân vật (Character Level)**. Người chơi tự do thay trang bị không lo bị khóa chéo đồ.
* **Thưởng Nội Tại Của Điểm Thuộc Tính (Inherent Passives):**
  * **10 Strength:** `+5 Máu tối đa` và `+2% Giáp (Armour)`.
  * **10 Dexterity:** `+20 Độ chính xác (Accuracy)` và `+2% Né tránh (Evasion)`.
  * **10 Intelligence:** `+5 Mana tối đa` và `+2% Khiên năng lượng (Energy Shield)`.
* **Chiều Sâu Endgame:** Mở đường cho các build *Attribute Stacking* (Strength Stacker dồn sát thương lửa, Intelligence Stacker dồn ES khổng lồ) thông qua các món đồ Unique đặc biệt.

---

## 2. Hệ Thống Tiền Tệ Ma Thuật (Currency Orbs)

Không có khái niệm cày vàng để mua đồ trong shop. Tiền tệ là các viên ngọc với chức năng cụ thể:

| Tên Currency | Chức Năng Chế Tạo (Crafting Effect) | Mục Tiêu Tác Động | Mức Độ Quý |
| :--- | :--- | :--- | :--- |
| **Scroll of Wisdom** | Giám định trang bị chưa xác định (Unidentified). | Trang bị / Map | Phổ thông |
| **Orb of Transmutation** | Biến 1 món đồ Normal (Trắng) thành Magic (Xanh) với 1–2 affixes. | Trang bị / Map | Phổ thông |
| **Orb of Alteration** | Xóa và tạo ngẫu nhiên lại toàn bộ affixes trên đồ Magic. | Trang bị / Map | Phổ thông |
| **Orb of Augmentation** | Đập thêm 1 dòng affix vào đồ Magic nếu đồ đó mới chỉ có 1 dòng. | Trang bị / Map | Phổ thông |
| **Regal Orb** | Nâng cấp đồ Magic thành đồ Rare (Vàng), giữ nguyên các dòng cũ và thêm 1 dòng mới. | Trang bị / Map | Trung bình |
| **Orb of Alchemy** | Biến thẳng 1 món đồ Normal thành Rare với 4 đến 6 dòng ngẫu nhiên. | Trang bị / Map | Trung bình |
| **Chaos Orb** | Tiền tệ chuẩn quốc tế: Xóa và quay ngẫu nhiên lại toàn bộ dòng trên đồ Rare. | Trang bị / Map | Cao |
| **Exalted Orb (Slam)** | Đập thêm 1 dòng affix ngẫu nhiên vào đồ Rare (nếu đồ chưa đủ 6 dòng). | Trang bị | Rất cao |
| **Divine Orb** | Giữ nguyên các dòng đang có, chỉ quay lại giá trị số bên trong phạm vi của từng dòng. | Trang bị | Cực cao |
| **Orb of Scouring** | Tẩy trắng toàn bộ affixes, đưa trang bị quay về trạng thái Normal. | Trang bị / Map | Trung bình |
| **Jeweller's Orb** | Mở rộng số lượng khe cắm Support trên **Active Gem** (từ 2 lên tối đa 5 khe). | **Active Gem** | Trung bình |
| **Chromatic Orb** | Đổi ngẫu nhiên màu sắc các khe cắm Support trên **Active Gem** theo tỷ lệ Str/Dex/Int. | **Active Gem** | Phổ thông |
| **Orb of Regret** | Cung cấp điểm hoàn trả (Respec Point) để tẩy và cộng lại điểm trên Passive Tree. | Bảng Nội Tại | Trung bình |
| **Vaal Orb (Corrupt)** | **Nguy cơ cao - Phần thưởng lớn:** Đóng băng vĩnh viễn món đồ hoặc Gem không thể craft tiếp. Có thể tạo dòng ẩn thần thánh (Implicit), nâng cấp Gem lên Lv 21, hoặc phá hủy món đồ. | Mọi vật phẩm | Thần thoại |

---

## 3. Quy Trình Chế Tạo Tiêu Chuẩn (Crafting Methodology)

Game thủ sẽ trải qua các bước "làm đồ" quen thuộc:
1. **Săn phôi đồ (Base Item):** Tìm đúng loại áo/vũ khí có Implicit ngon và `ilvl 86+`.
2. **Alt-Spamming:** Dùng *Orb of Alteration* và *Augmentation* liên tục cho đến khi đồ Magic có đúng 2 dòng T1 mong muốn.
3. **Regal Hit:** Dùng *Regal Orb* để lên đồ Rare với 3 dòng chuẩn.
4. **Bench Crafting:** Khóa các dòng không mong muốn hoặc craft thêm 1 dòng chỉ số cố định trên bàn chế tạo.
5. **Exalt Slam / Harvest / Eldritch:** Hoàn thiện các dòng còn lại.

---

## 4. Chế Tạo Định Hướng Tinh Gọn: Hóa Thạch (Streamlined Fossil Crafting)

Nhằm tối ưu cho giao diện mini **440x780** và trải nghiệm **Zero-APM / Low-Friction**, Idle Exile **loại bỏ hoàn toàn vật phẩm Khuôn đúc (Resonators)**. Người chơi không cần nhặt hay quản lý khuôn nhiều lỗ. Thay vào đó, toàn bộ cơ chế kết hợp Hóa thạch được tích hợp trực tiếp vào **Bàn Chế Tạo (Crafting Anvil)** với thao tác 1-Click trực quan.

Hóa thạch được khai thác độc quyền từ **The Endless Abyss** (xem [02_mapping_system.md](02_mapping_system.md)) và tự động tích trữ trong hòm mà không chiếm ô túi đồ.

### 4.1. Khay Cắm Hóa Thạch Tích Hợp (Anvil Fossil Slots)
Tại giao diện Crafting Anvil:
* Người chơi đặt món đồ cần chế tạo vào khay.
* Khay chế tạo có sẵn tối đa **3 ô cắm Hóa Thạch (Fossil Slots)**:
  * **Slot 1:** Mở khóa mặc định khi kích hoạt Crafting Anvil (Lv 45).
  * **Slot 2:** Mở khóa sau khi vượt qua **Abyss Depth 150**.
  * **Slot 3:** Mở khóa sau khi vượt qua **Abyss Depth 300** (Dành cho việc chế tác đồ tối thượng Endgame).
* Người chơi chỉ cần tick chọn 1 đến 3 Hóa Thạch mong muốn từ danh sách có sẵn. Giao diện sẽ tự động tính toán và hiển thị ngay các dòng được buff tỉ lệ (x10) hoặc bị khóa (0%).
* Bấm **[CRAFT]**: Hệ thống tiêu hao số Hóa Thạch đã chọn + 1 viên ngọc xúc tác (*Orb of Alchemy* cho đồ Trắng hoặc *Chaos Orb* cho đồ Rare).

```
┌──────────────────────────────────────────┐
│             CRAFTING ANVIL               │
│                                          │
│        [ Astral Plate (Rare) ]           │
│                                          │
│  FOSSIL SLOTS (Mở khóa: 2/3)             │
│  ┌──────────────┐  ┌──────────────┐      │
│  │ Pristine     │  │ Metallic     │ [+]  │
│  │ (Life x10)   │  │ (No Phys)    │ (Lock│
│  └──────────────┘  └──────────────┘      │
│                                          │
│  Dự đoán kết quả:                        │
│  • Tăng cực mạnh: Flat Life, % Max Life  │
│  • Loại bỏ hoàn toàn: Physical Mods      │
│                                          │
│         [ CRAFT (1x Chaos Orb) ]         │
└──────────────────────────────────────────┘
```

### 4.2. Các Loại Hóa Thạch Tiêu Biểu (Abyssal Fossils)
Mỗi hóa thạch điều chỉnh trọng số (Weight) của các nhóm Affix một cách rõ ràng:

| Tên Hóa Thạch | Tác Dụng Điều Chỉnh Trọng Số Affix | Mục Tiêu Tối Ưu |
| :--- | :--- | :--- |
| **Pristine Fossil** | **Tăng 10x trọng số dòng Máu (Life)**; **Chặn hoàn toàn (0x) các dòng Defences** (Armour/Evasion/ES). | Áo giáp/Nhẫn thuần Máu khủng. |
| **Metallic Fossil** | **Tăng 10x trọng số Sát thương & Kháng Lôi (Lightning)**; **Chặn hoàn toàn các dòng Vật Lý (Physical)**. | Vũ khí sét hoặc đồ kháng sét. |
| **Scorched Fossil** | **Tăng 10x trọng số Hỏa (Fire)**; **Chặn hoàn toàn các dòng Băng (Cold)**. | Đồ Fire DoT / Ignite. |
| **Frigid Fossil** | **Tăng 10x trọng số Băng (Cold)**; **Chặn hoàn toàn các dòng Hỏa (Fire)**. | Đồ Freeze / Cold Crit. |
| **Dense Fossil** | **Tăng 10x trọng số Defences (Armour/Evasion/Energy Shield)**; **Chặn hoàn toàn dòng Máu (Life)**. | Áo giáp thuần ES (CI builds). |
| **Corroded Fossil** | **Chặn hoàn toàn các dòng Nguyên tố (Fire/Cold/Lightning)**. | Chế tác vũ khí sát thương Chaos/Physical thuần. |
| **Sanctified Fossil** | **Tăng mạnh trọng số các dòng bậc cao (Tier 1/Tier 2)**; Làm giảm dòng Tier thấp. | Tinh chỉnh chất lượng Affix cho đồ cuối game. |

---

## 5. Bộ Lọc Nhặt Đồ & Rương Chứa Tự Động (Loot Filter & Affinity Stash)

Đối với game treo máy (Idle), nếu không có bộ lọc tốt, túi đồ sẽ bị quá tải trong vòng 2 phút.

### 5.1. Hệ Thống Loot Filter Tích Hợp
* Người chơi có thể chọn cấp độ lọc đồ ngay trong cài đặt:
  * **Cấp 1 - Regular:** Nhặt tất cả mọi thứ.
  * **Cấp 2 - Strict:** Bỏ qua đồ trắng và xanh; chỉ nhặt đồ Rare có `ilvl` cao, Gem có chất lượng (Quality), Active Gem 4-5 slots, và Currency.
  * **Cấp 3 - Uber Strict:** Ẩn hoàn toàn đồ Rare rác; chỉ nhặt các phôi đồ Tier 1, Map bậc cao, Active Gem 5 slots, và các Currency từ Chaos Orb trở lên, cùng toàn bộ Hóa thạch (Fossils).
* **Âm thanh cảnh báo (Drop Sounds):** Tích hợp hiệu ứng âm thanh riêng cho từng nhóm giá trị vật phẩm (tiếng chuông nhẹ cho Chaos/Fossil, tiếng vang ngân rền cho Divine/Exalted/Mirror).

### 5.2. Kho Chứa Tự Động (Affinity Stash)
* Mọi vật phẩm sau khi được nhặt qua Loot Filter sẽ tự động bay vào đúng ngăn tủ chuyên dụng:
  * **Currency Tab:** Tự gom thành các cọc số lượng lớn (Stack size 5000+).
  * **Map Tab:** Phân chia theo từng Tier (Tier 1 đến Tier 16) và tên bản đồ, tích hợp nút nạp nhanh vào Map Queue.
  * **Fossil Tab:** Chứa và tự động xếp chồng toàn bộ Hóa thạch theo từng loại, liên kết trực tiếp vào Bàn Chế Tạo.
  * **Gem Tab & Gear Tab:** Lưu trữ phôi đồ và ngọc kỹ năng.

