# 04. Hệ Thống Kỹ Năng & Bảng Nội Tại (Gems & Passives)

Hai yếu tố tạo nên sự biến ảo vô hạn của *Path of Exile* là **hệ thống phối ghép ngọc (Gems System)** và **bảng nội tại hình mạng nhện (Passive Skill Web)**. 

Nhằm tối ưu hóa cho trải nghiệm **treo máy (Idle)** và giao diện **cửa sổ mini**, hệ thống ngọc được thiết kế theo mô hình **Path of Exile 2 (PoE 2)**: **Tách biệt hoàn toàn Sockets ra khỏi Trang bị**.

---

## 1. Hệ Thống Kỹ Năng Độc Lập (PoE 2 Decoupled Gem System)

### 1.1. Triết Lý: Trang Bị Thuần Stats — Kỹ Năng Tự Chứa Lỗ
* **Trang bị KHÔNG có Sockets:** Vũ khí, Áo giáp, Mũ, Găng tay, Giày hoàn toàn không có lỗ khảm. Khi người chơi nhặt được trang bị có chỉ số tốt hơn, họ có thể **thay ngay lập tức** mà không bị phụ thuộc hay làm gián đoạn chuỗi liên kết kỹ năng.
* **Active Gem tự chứa các khe Support (Gems Have Sockets):**  
  Người chơi lắp Active Gem vào **Khay Kỹ Năng (Skills Panel)**. Bản thân viên Active Gem đó sẽ sở hữu các lỗ khảm (Support Slots) bên trong nó để gắn các Support Gems đi kèm.
* **100% Tự Động Liên Kết (No Fusing):**  
  Mọi Support Gem gắn vào Active Gem đều được tự động liên kết (Linked) ngay lập tức. Game loại bỏ hoàn toàn cơ chế quay nối dây ngẫu nhiên phiền phức (*Orb of Fusing*).

```
Khay Kỹ Năng (Skill Slot 1)
┌────────────────────────────────────────────────────────┐
│ [ Active Gem: Fireball (Lam) ]                         │
│   ├── [Support 1: Greater Multiple Proj (Lục)]         │ ──► Bắn 5 quả cầu lửa hình quạt
│   ├── [Support 2: Fork (Lục)]                          │ ──► Trúng mục tiêu chẻ làm 2
│   ├── [Support 3: Ignite Proliferation (Lam)]          │ ──► Lây lan hiệu ứng thiêu đốt
│   ├── [Support 4: Added Fire Damage (Đỏ)]              │ ──► Tăng mạnh sát thương Hỏa
│   └── [Support 5: Controlled Destruction (Lam)]        │ ──► Tăng More Spell Damage
└────────────────────────────────────────────────────────┘
  (Tương đương một thiết lập 6-Link thần thánh trong PoE 1)
```

---

## 2. Phân Bổ Khay Kỹ Năng Cho AI Tự Động Chiến Đấu (Skill Loadout Matrix)

Để nhân vật tự động chiến đấu thông minh, mượt mà mà không làm rối loạn luồng xuất chiêu, giao diện cung cấp các khay kỹ năng với vai trò chuyên biệt:

| Khay Kỹ Năng (Slot) | Vai Trò & Hành Vi Của AI | Ví Dụ Kỹ Năng Phù Hợp |
| :--- | :--- | :--- |
| **Slot 1: Primary DPS (Kỹ năng Chính)** | Xả chiêu liên tục theo tốc độ đánh/thi triển (Attack/Cast Speed). Đây là nguồn sát thương dọn quái chủ lực. | *Fireball*, *Cleave*, *Spark*, *Cyclone*. |
| **Slot 2: Secondary / Cooldown** | Tự động thi triển khi gặp quái Tinh Anh (Rare), Quái Hồn (Tormented), hoặc Map Boss để dồn sát thương/làm chậm. | *Frost Bomb* (giảm kháng), *Flame Wall*, *Decoy Totem*. |
| **Slot 3: Automated Trigger** | Tự động kích hoạt khi thỏa mãn điều kiện chiến đấu mà không cần thao tác tay. | *Cast when Damage Taken + Steelskin/Molten Shell* (khi tụt máu), *Automation + Blood Rage*. |
| **Slot 4 & 5: Auras / Reservoirs** | Bật vĩnh viễn, giam giữ (Reserve) một lượng % Mana nhất định để cung cấp chỉ số phòng thủ/tấn công toàn diện. | *Determination* (+Giáp), *Grace* (+Né tránh), *Hatred* (Tăng sát thương Băng). |

---

## 3. Quy Tắc Nâng Cấp & Màu Sắc Lỗ Khảm Trên Gem

1. **Số lượng Support Slots trên Active Gem:**
   * Mặc định khi mới nhặt được: Active Gem có **2 khe cắm Support**.
   * Dùng **Jeweller's Orb** trực tiếp lên viên Active Gem để quay số lượng khe cắm từ **2 lên tối đa 5 khe** (tương đương 6-Link).
2. **Màu sắc khe cắm (Socket Colors):**
   * **Đỏ (Strength):** Gắn các ngọc cận chiến, tăng máu, sát thương vật lý/hỏa.
   * **Xanh Lục (Dexterity):** Gắn các ngọc xạ thủ, tốc độ đánh, đạn đạo, độc, né tránh.
   * **Xanh Lam (Intelligence):** Gắn các ngọc phép thuật, diện rộng (AoE), lôi, khiên năng lượng.
   * **Trắng (Prismatic):** Khe cắm vạn năng, gắn được mọi loại màu Support Gem.
   * Dùng **Chromatic Orb** lên Active Gem để đổi ngẫu nhiên màu sắc các khe cắm phụ trợ dựa trên thiên hướng thuộc tính (Str/Dex/Int) của gem đó. *(Lưu ý: Ngọc kỹ năng tuân theo Phương án C - Thuộc tính mềm: không đòi hỏi chỉ số cứng để kích hoạt, chỉ yêu cầu Cấp độ nhân vật).*
3. **Hệ thống Thẻ Kỹ Năng (Gem Tags System):**
   * Support Gem chỉ có hiệu lực nếu có chung ít nhất 1 Tag hợp lệ với Active Gem.
   * *Ví dụ:* Support `Concentrated Effect` (có tag *AoE*) sẽ kích hoạt cho `Firestorm` (có tag *AoE*), nhưng vô tác dụng nếu gắn cho đòn cận chiến đơn mục tiêu `Heavy Strike`.

---

## 4. Bảng Kỹ Năng Nội Tại Khổng Lồ (Passive Skill Web)

Bảng kỹ năng nội tại hoạt động độc lập với hệ thống trang bị và ngọc, là đồ thị mạng lưới liên kết hàng trăm node phân nhánh từ tâm:

```
                         [ WITCH ] (Intelligence)
                            ▲
                           / \
                          /   \
  [ TEMPLAR ] (Str/Int)  /     \  [ SHADOW ] (Dex/Int)
           \            /       \            /
            \          / [SCION] \          /
             \        /   (Tâm)   \        /
              \      /             \      /
     [ MARAUDER ] ───────────────────── [ RANGER ]
     (Strength)                         (Dexterity)
```

### 4.1. Phân Loại Node
1. **Minor Nodes (Node Nhỏ):** Cung cấp chỉ số cơ bản (+10 Strength, +5% Max Life, +8% Physical Damage).
2. **Notables (Node Trung Cấp):** Tăng các cụm sức mạnh chuyên biệt (+25% Critical Strike Multiplier, tăng tốc độ di chuyển, tăng diện tích hiệu ứng AoE).
3. **Keystones (Trọng Điểm Đột Biến Lối Chơi):** Định hình hoàn toàn cách vận hành của build:

| Tên Keystone | Hiệu Ứng Thay Đổi Bản Chất | Phù Hợp Cho Build |
| :--- | :--- | :--- |
| **Chaos Inoculation (CI)** | Máu tối đa trở thành 1. Miễn nhiễm 100% với sát thương Chaos. | Build thuần Khiên Năng Lượng (Energy Shield). |
| **Resolute Technique** | Đòn đánh không bao giờ trượt (100% Hit Chance). Không bao giờ gây sát thương chí mạng (No Crit). | Build Cận chiến cày cuốc, tiết kiệm đồ accuracy và crit. |
| **Iron Reflexes** | Toàn bộ chỉ số Né Tránh (Evasion) chuyển đổi thành Giáp (Armour). | Build Đỡ đòn trâu bò, giảm thiểu tối đa sát thương vật lý. |
| **Mind Over Matter (MoM)** | 30% sát thương nhận vào sẽ trừ thẳng vào Năng lượng (Mana) trước khi trừ vào Máu. | Build kết hợp Mana Regen cao để làm lớp giáp sinh lực. |
| **Avatar of Fire** | 50% sát thương Vật lý, Băng, Lôi được chuyển hóa thành Hỏa. Nhân vật không thể gây sát thương ngoài Hỏa. | Build thuần Hỏa / Thiêu đốt (Pure Fire/Ignite). |
| **Eldritch Battery** | Energy Shield bảo vệ Năng lượng (Mana) thay vì bảo vệ Máu. | Cho phép bật nhiều Aura ngốn 100% mana mà vẫn xả được kỹ năng. |

### 4.2. Masteries & Jewel Sockets
* **Mastery Nodes:** Khi học trọn vẹn một cụm kỹ năng (Cluster), người chơi chọn 1 hiệu ứng đặc thù (ví dụ: *Life Mastery: +50 Maximum Life* hoặc *Fire Mastery: Đòn thiêu đốt hồi máu*).
* **Jewel Sockets:** Các ô tròn trên bảng cho phép khảm các viên ngọc nhặt từ Map để tăng cường chỉ số hoặc biến đổi thuộc tính của các node lân cận.

---

## 5. Định Hình Meta & Phối Ghép Build Hoàn Chỉnh

Để tìm hiểu chi tiết cách kết hợp giữa hệ thống Ngọc Kỹ Năng, Trang Bị và Cây Nội Tại nhằm tạo nên **6 Build Archetypes Chiến Lược** (Bleed Juggernaut, Ignite Elementalist, Crit Deadeye, Poison Claw Assassin, Righteous Fire Autobomber, Archmage Hierophant), xem tài liệu chi tiết:
👉 **[13. Thiết Kế Hệ Thống Build Archetypes & Định Hình Meta Chiến Đấu (13_build_archetypes_specification.md)](13_build_archetypes_specification.md)**.

