# 09. Hệ Thống Trang Bị Vũ Khí (Weapon Equipment System)

**Tài liệu Thiết kế Hệ thống (GDD - System Specification)**  
**Dự án:** *Idle Exile*  
**Vị trí:** Senior Game Systems & Combat Designer  
**Form Factor:** Mini-Window Desktop Companion (`440 x 780` px)  
**Kế hoạch triển khai:** [WEAPON_SYSTEM_IMPLEMENTATION_PLAN.md](../technical/WEAPON_SYSTEM_IMPLEMENTATION_PLAN.md)

---

## 1. TỔNG QUAN & TRIẾT LÝ THIẾT KẾ CỐT LÕI (CORE PHILOSOPHY)

Trong các tựa game ARPG chuyên sâu như *Path of Exile*, vũ khí là linh hồn của nhân vật, quyết định trực tiếp công thức sát thương cơ bản và phương thức tương tác đòn đánh. Đối với **Idle Exile** — một tựa game *"Zero APM, 100% Theorycrafting"* chạy trên cửa sổ mini đồng hành, hệ thống vũ khí được xây dựng dựa trên 4 trụ cột thiết kế:

1. **Tuân thủ triệt để mô hình PoE 2 — Trang bị hoàn toàn không có Socket (Decoupled Sockets):**
   * Vũ khí và trang bị **hoàn toàn KHÔNG chứa Sockets**. Các khe cắm đá hỗ trợ (Support Slots) nằm trực tiếp trên từng viên **Active Skill Gem**.
   * Thiết kế này giải phóng người chơi khỏi sự ức chế kinh điển: nhặt được vũ khí chỉ số cao nhưng không dám dùng vì "thiếu socket / lệch liên kết". 100% sức mạnh của vũ khí tập trung vào **Base Stats (Chỉ số cơ bản)**, **Implicit Modifier (Thuộc tính nội tại của phôi)**, và **Affix Pool (Tiền tố / Hậu tố)**.

2. **Toán học PoE chuẩn xác — Phân biệt rành mạch Local vs Global Modifiers:**
   * **Local Modifiers:** Tác động trực tiếp vào thông số cơ sở của chính vũ khí (Local Flat Damage, Local % Increased Physical Damage, Local Attack Speed, Local Crit). Đây là thành phần được nhân lũy kế đầu tiên tạo nên Base DPS trước khi đi vào bảng nội tại.
   * **Global Modifiers:** Tác động lên toàn bộ nhân vật (Global Spell Damage, Global Crit Multiplier, Extra Physical as Element, v.v.).

3. **Phân cực rõ rệt giữa Vũ Khí Đòn Đánh (Attack) và Vũ Khí Phép Thuật (Caster):**
   * *Attack Weapons (Kiếm, Rìu, Cung, Chùy, Vuốt)*: Sát thương của skill đòn đánh lấy trực tiếp từ Base Damage của vũ khí trên tay.
   * *Caster Weapons (Gậy phép/Wand, Pháp trượng/Sceptre, Quyền trượng/Staff)*: Sát thương của spell lấy từ Base Damage của Gem, vũ khí đóng vai trò cung cấp % Spell Damage, Cast Speed và đặc biệt là `+X to Level of all Spell Gems`.

4. **Tối ưu hóa hiển thị Mini-Window (`440 x 780` px):**
   * Giảm thiểu thao tác kiểm tra, tự động tính toán sẵn **pDPS (Physical DPS)**, **eDPS (Elemental DPS)**, và **Total DPS** trực quan trên Tooltip.
   * Hiển thị so sánh nhanh (Stat Delta: xanh tăng, đỏ giảm) giúp người chơi Idle ra quyết định trang bị chỉ trong 1 giây.

---

## 2. CẤU HÌNH TAY CẦM & PHÂN LOẠI VŨ KHÍ (HAND SLOTS & BASE TAXONOMY)

### 2.1. Cấu Hình Tay Cầm (Hand Slot Configurations)

Nhân vật sở hữu 2 ô trang bị vũ khí: **Main Hand (Tay chính)** và **Off Hand (Tay phụ)**.

```
                    ┌─────────────────────────┐
                    │  CẤU HÌNH VŨ KHÍ CHÍNH  │
                    └────────────┬────────────┘
                                 │
         ┌───────────────────────┼───────────────────────┐
         ▼                       ▼                       ▼
  [ One-Handed + Shield ]  [ Dual-Wield (1H + 1H) ]  [ Two-Handed / Bow+Quiver ]
  - Thiên hướng: Sinh tồn   - Thiên hướng: Tốc độ     - Thiên hướng: Sát thương lớn
  - +Armour / Evasion / ES  - +10% More Attack Speed  - Sát thương 1 đòn (Base Hit) cực cao
  - +% Block Chance         - +15% Chance to Block    - Dành cho Slam, DoT nặng, hoặc Cung
```

#### Quy Tắc Cơ Học Cụ Thể:
* **Dual-Wielding (Cầm 2 vũ khí 1 tay):**
  * Nhân vật nhận nội tại cơ học vĩnh viễn:
    * **`10% More Attack Speed`** (nhân trực tiếp vào tốc độ vung đòn).
    * **`15% Additional Chance to Block Attack Damage`** (tăng khả năng đỡ đòn đòn đánh).
  * *Cơ chế vung đòn:* Khi dùng kỹ năng đòn đánh đơn mục tiêu thông thường, đòn đánh sẽ luân phiên (alternate) giữa Main Hand và Off Hand ở mỗi tick tấn công.
* **Two-Handed Melee (Vũ khí cận chiến 2 tay):**
  * Chiếm cả 2 ô trang bị (ô Off Hand tự động khóa mờ).
  * Base Damage gấp 1.7x – 2.0x so với vũ khí 1 tay cùng cấp, tối ưu tuyệt đối cho lối chơi Slam diện rộng hoặc gây sát thương chảy máu (Bleed) cực nặng.
* **Bow & Quiver (Cung & Ống tên):**
  * Cung (Bow) là vũ khí 2 tay tầm xa, nhưng **được phép trang bị thêm Ống tên (Quiver)** tại ô Off Hand.
  * Ống tên hoạt động như một món trang bị stat-stick (cung cấp thêm đạn, sát thương nguyên tố, crit, tốc độ).

---

### 2.2. Phân Lớp Vũ Khí & Thuộc Tính Nội Tại (Weapon Classes & Implicits)

Mỗi phân lớp vũ khí mang một đặc tính chiến thuật riêng biệt gắn liền với thuộc tính chính (**Str**, **Dex**, **Int**) và sở hữu một **Implicit Modifier** xuất hiện mặc định trên phôi:

| Phân Loại Vũ Khí | Yêu Cầu Thuộc Tính | Base APS | Base Crit | Implicit Mặc Định Của Phôi | Định Hướng Xây Dựng Build |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Thrusting Sword (Foil)** | Str / Dex | 1.60 | 5.5% | `+25% to Global Critical Strike Multiplier` | Melee Crit, Impale, Fast Bleed |
| **One-Handed Axe** | Str / Dex | 1.35 | 5.0% | *Không có (Base Phys cao hơn kiếm)* | Pure Physical, Bleed Sunder |
| **One-Handed Mace** | Pure Str | 1.25 | 5.0% | `+15% Reduced Enemy Stun Threshold` | Heavy Stun, Boneshatter, Armor Stacking |
| **Claw (Vuốt)** | Dex / Int | 1.60 | 6.0% | `+35 to 45 Life gained for each Enemy hit` | Đánh nhanh hồi máu (LGoH), Poison, Nightblade |
| **Dagger (Dao găm)** | Dex / Int | 1.45 | 6.5% | `+40% to Global Critical Strike Chance` | Assassin Crit, Blade Trap, Poison Crit |
| **Wand (Đũa phép)** | Pure Int | 1.40 | 7.0% | `20% to 35% Increased Spell Damage` | Pure Caster Spells, Spitter, Power Siphon |
| **Sceptre (Pháp trượng)** | Str / Int | 1.25 | 6.0% | `25% to 40% Increased Elemental Damage` | Elemental Spells, Ignite, Righteous Fire |
| **Two-Handed Sword** | Str / Dex | 1.40 | 5.0% | `+30% to Accuracy Rating` | Cận chiến 2 tay cân bằng, Cyclone, Sweep |
| **Two-Handed Axe** | Pure Str | 1.25 | 5.0% | *Base Physical Damage cao nhất game* | Ground Slam, Earthshatter, Big Bleed |
| **Two-Handed Mace** | Pure Str | 1.15 | 5.0% | `30% Increased Stun Duration on Enemies` | Stun-lock Boss, Heavy Slam |
| **Bow (Cung)** | Pure Dex | 1.45 | 5.0% | *Không có (Nhận Implicit từ Quiver)* | Ranged Attack, Tornado Shot, Bleed Bow |
| **Staff / Warstaff** | Str / Int | 1.25 | 6.0% | `+18% to 25% Chance to Block Attacks/Spells` | Battlemage, Staff Crit, Caster phòng thủ |

#### Trang Bị Tay Phụ (Off-Hand Items):
* **Shields (Khiên):** Cung cấp các chỉ số phòng ngự gốc:
  * *Tower Shield (Str):* Armour cơ sở rất cao, Implicit `+X to Maximum Life`.
  * *Buckler (Dex):* Evasion cơ sở cao, Implicit `+X% Chance to Suppress Spell Damage`.
  * *Spirit Shield (Int):* Energy Shield cao, Implicit `+X% Increased Spell Damage`.
  * *Round Shield (Str/Dex):* Cân bằng Armour/Evasion, Implicit `+X% Chance to Block Attacks`.
* **Quiver (Ống tên):**
  * *Broadhead Quiver:* `Adds (6-10) to (12-15) Physical Damage to Attacks`.
  * *Penetrating Quiver:* `Arrows Pierce an additional Target`.
  * *Spike-Point Quiver:* `+25% to Global Critical Strike Multiplier`.

---

## 3. CƠ CHẾ TOÁN HỌC & CÔNG THỨC SÁT THƯƠNG (MATHEMATICAL DAMAGE ENGINE)

### 3.1. Các Thông Số Cơ Bản Của Vũ Khí
Mỗi vũ khí có 3 chỉ số nền tảng hiển thị trên đầu thẻ vật phẩm:
1. **Damage Range (Khoảng sát thương):** $\text{Min Damage} - \text{Max Damage}$ (Physical, Fire, Cold, Lightning, Chaos).
2. **Attacks Per Second (APS):** Tần suất vung đòn cơ sở trong 1 giây.
3. **Critical Strike Chance:** Tỉ lệ kích hoạt đòn chí mạng cơ bản (thường từ `5.0%` đến `7.5%`).

---

### 3.2. Local Modifiers vs Global Modifiers

Đây là nguyên lý quan trọng nhất trong việc định giá vũ khí của dòng game PoE:

* **Local Modifiers (Thuộc tính cục bộ):** Thay đổi trực tiếp các con số in trên thân vũ khí.
  * `% Increased Physical Damage` (Local)
  * `Adds X to Y Physical Damage` (Local Flat Phys)
  * `Adds X to Y Fire/Cold/Lightning Damage` (Local Flat Ele)
  * `% Increased Attack Speed` (Local APS)
  * `% Increased Critical Strike Chance` (Local Crit)

```
Ví dụ thực tế:
Phôi gốc (Base): Jewelled Foil
- Min Phys: 32, Max Phys: 60
- Base APS: 1.60
- Base Crit: 5.5%

Vũ khí roll được các dòng Local:
- [Prefix] Adds (20-35) to (45-60) Physical Damage (Roll: +25 to +50)
- [Prefix] 150% Increased Physical Damage
- [Suffix] 25% Increased Attack Speed

=> CHỈ SỐ LOCAL MỚI CỦA VŨ KHÍ:
- Computed Min Phys = (32 + 25) * (1 + 1.50) = 142.5
- Computed Max Phys = (60 + 50) * (1 + 1.50) = 275.0
- Computed APS      = 1.60 * (1 + 0.25)       = 2.00
- Computed Crit     = 5.5% (chưa có mod)      = 5.5%

=> pDPS của Cây Kiếm:
  Average Phys = (142.5 + 275.0) / 2 = 208.75
  pDPS = 208.75 * 2.00 = 417.5 pDPS!
```

* **Global Modifiers (Thuộc tính toàn cục):** Không đổi số trên thân vũ khí, mà đưa vào công thức của nhân vật:
  * `+X% to Global Critical Strike Multiplier`
  * `X% Increased Spell Damage`
  * `+1 to Level of all Spell Skill Gems`
  * `Gain X% of Physical Damage as Extra Fire Damage`

---

### 3.3. Công Thức Tính DPS Chuẩn Cho Vũ Khí (Weapon DPS Breakdown)

Để tối ưu hóa trải nghiệm nhàn rỗi (Idle), hệ thống tự động bóc tách các dòng DPS:

$$\text{Avg Damage} = \frac{\text{Min Damage} + \text{Max Damage}}{2}$$

$$\text{pDPS (Physical DPS)} = \text{Avg Phys} \times \text{Computed APS}$$

$$\text{eDPS (Elemental DPS)} = \left(\text{Avg Fire} + \text{Avg Cold} + \text{Avg Lightning}\right) \times \text{Computed APS}$$

$$\text{cDPS (Chaos DPS)} = \text{Avg Chaos} \times \text{Computed APS}$$

$$\text{Total Weapon DPS} = \text{pDPS} + \text{eDPS} + \text{cDPS}$$

Khi một kỹ năng đòn đánh (Attack Skill) được kích hoạt:  
$$\text{Skill Base Damage} = \text{Weapon Damage Range (Computed)}$$
Sau đó mới nhân với các hệ số của nhân vật:
$$\text{Final Damage} = \text{Skill Base} \times \left(1 + \sum \text{Increased}_{\text{Tree, Gear}}\right) \times \prod \left(1 + \text{More}_{\text{Gems, Buffs}}\right) \times \text{Crit Multi}$$

---

## 4. BẢNG PHÂN BỔ THUỘC TÍNH (AFFIX POOL ARCHITECTURE)

Vũ khí tuân thủ quy tắc chuẩn: Tối đa **3 Prefixes (Tiền tố)** và **3 Suffixes (Hậu tố)**.

### 4.1. Nhóm Prefix (Định Hình Sát Thương)

| Mã Định Danh | Tên Thể Hiện (PoE Tier 1) | Loại Mod | Min ilvl | Giá Trị Tier 1 | Ý Nghĩa / Mục Đích |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `weapon_percent_phys_t1` | **The Dictator's** | Local % Phys | 83 | `170% to 179% Inc Phys` | Trọng tâm của vũ khí Physical |
| `weapon_flat_phys_t1` | **Flaring** | Local Flat Phys | 77 | `Adds (27-38) to (56-68) Phys` | Nâng đáy và trần Base Phys |
| `weapon_hybrid_phys_acc_t1` | **Runesmith's** | Local Hybrid | 83 | `75% to 79% Inc Phys / +135 Acc` | Mảnh ghép thứ 3 của Physical Trinity |
| `weapon_flat_fire_t1` | **Cremating** | Local Flat Fire | 82 | `Adds (85-115) to (180-210) Fire` | Tri-Elemental Attack Builds |
| `weapon_flat_cold_t1` | **Polar** | Local Flat Cold | 82 | `Adds (72-98) to (150-175) Cold` | Tri-Elemental, Freeze/Chill |
| `weapon_flat_lightning_t1` | **Electrocuting** | Local Flat Lightning | 82 | `Adds (15-25) to (290-330) Light` | Tri-Elemental, Shock Max Roll |
| `weapon_all_spell_gems_t1` | **All-Encompassing** | Global Gem Level | 85 | `+1 to Level of all Spell Gems` | Thánh tích cho Caster Spells |
| `weapon_percent_spell_dmg_t1`| **Archmage's** | Global % Spell | 84 | `100% to 109% Inc Spell Damage` | Caster Spells Scale |

> **Khái niệm "Physical Trinity" (Bộ Ba Vật Lý Thần Thánh):**  
> Một món vũ khí vật lý tối thượng (Mirror-tier) bắt buộc phải quy tụ 3 Prefixes: **Flat Phys T1 + % Phys T1 + Hybrid Phys/Acc T1**. Khi 3 dòng này tương tác nhân chéo, pDPS của vũ khí sẽ tăng từ 150 lên hơn 500+ pDPS!

---

### 4.2. Nhóm Suffix (Định Hình Tốc Độ & Chí Mạng)

| Mã Định Danh | Tên Thể Hiện (PoE Tier 1) | Loại Mod | Min ilvl | Giá Trị Tier 1 | Ý Nghĩa / Mục Đích |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `weapon_attack_speed_t1` | **Of Celebration** | Local Speed | 76 | `26% to 27% Increased Attack Speed` | Tăng trực tiếp tần suất vung đòn |
| `weapon_local_crit_chance_t1`| **Of Destruction** | Local Crit | 73 | `35% to 38% Inc Crit Chance` | Tăng % Crit gốc của phôi (5% -> 6.9%) |
| `weapon_cast_speed_t1` | **Of Fame** | Global Speed | 76 | `23% to 25% Increased Cast Speed` | Caster Speed |
| `weapon_global_crit_multi_t1`| **Of Unleashed Power** | Global Multi | 73 | `+35% to +38% Crit Multiplier` | Tăng sát thương đòn chí mạng |
| `weapon_dot_multiplier_t1` | **Of Haunting** | Global Multi | 78 | `+26% to +30% to Damage over Time Multi`| Bleed / Poison / Ignite builds |
| `weapon_elemental_res_t1` | **Of the Phoenix/Polar** | Global Res | 72 | `+46% to +48% to [Fire/Cold/Light] Res` | Bổ sung kháng tính phòng thủ |

---

## 5. BỐN TRƯỜNG PHÁI VŨ KHÍ TIÊU BIỂU (ARCHETYPES)

```
                    ┌───────────────────────────────┐
                    │    4 ARCHETYPES VŨ KHÍ CHÍNH   │
                    └───────────────┬───────────────┘
                                    │
    ┌────────────────┬──────────────┴────────────────┬────────────────┐
    ▼                ▼                               ▼                ▼
[ Pure Physical ]  [ Tri-Elemental ]           [ Pure Caster ]   [ DoT / Chaos ]
- Flat Phys T1     - Flat Fire T1              - +1 All Spells   - DoT Multi T1
- % Inc Phys T1    - Flat Cold T1              - % Spell Dmg T1  - Flat Phys/Chaos
- Attack Speed T1  - Flat Lightning T1         - Cast Speed T1   - Bleed / Poison
- Tối ưu Impale    - Attack Speed T1           - Spell Crit T1   - Bỏ qua Armor/ES
```

1. **Pure Physical Weapon (Vũ Khí Cận Chiến Thuần Vật Lý):**
   * *Phôi mẫu:* Vaal Axe (2H) hoặc Jewelled Foil (1H).
   * *Affixes mục tiêu:* Flat Phys T1 + % Phys T1 + Hybrid Phys/Acc T1 + Attack Speed T1.
   * *Cơ chế kết hợp:* Đòn đánh nặng, phá giáp (Overwhelm Physical Damage Reduction), tích lũy điểm ghim giáo (*Impale*).
2. **Tri-Elemental Attack Weapon (Vũ Khí Tam Nguyên Tố Tốc Đánh):**
   * *Phôi mẫu:* Imperial Claw (1H - hồi máu khi đánh trúng) hoặc Thicket Bow (2H - tốc 1.50).
   * *Affixes mục tiêu:* Flat Fire T1 + Flat Cold T1 + Flat Lightning T1 + Attack Speed T1 + Local Crit T1.
   * *Cơ chế kết hợp:* Gây cả 3 Ailments cùng lúc (*Ignite*, *Freeze*, *Shock +50% Damage taken*).
3. **Pure Caster Wand / Sceptre (Vũ Khí Pháp Thuật Thuần Phép):**
   * *Phôi mẫu:* Imbued Wand (Int) hoặc Opal Sceptre (Str/Int).
   * *Affixes mục tiêu:* `+1 to Level of all Spell Skill Gems` + `100% Increased Spell Damage` + `Cast Speed` + `Spell Crit Multi`.
   * *Cơ chế kết hợp:* Gia tăng sát thương gốc trực tiếp cho Active Spell Gems.
4. **Damage Over Time Specialist (Vũ Khí Sát Thương Theo Thời Gian):**
   * *Phôi mẫu:* Citadel Bow hoặc Gemini Claw.
   * *Affixes mục tiêu:* `+Physical/Chaos Damage over Time Multiplier` + `% Increased Damage over Time`.
   * *Cơ chế kết hợp:* Chuyên trị các đòn Bleed, Poison (cộng dồn vô hạn số tầng độc).

> [!NOTE]
> **Tương Tác Giữa Vũ Khí & Build Archetypes:**  
> Để nắm rõ cách 4 trường phái vũ khí này phối hợp với Ngọc kỹ năng và Cây nội tại trong 6 Đại Diện Build Archetypes chính của game (Juggernaut, Elementalist, Deadeye, Poison Assassin, Righteous Fire, Archmage), tham khảo tài liệu:  
> 👉 **[13. Thiết Kế Hệ Thống Build Archetypes & Định Hình Meta Chiến Đấu (13_build_archetypes_specification.md)](13_build_archetypes_specification.md)**.

---


## 6. VÒNG LẶP CHẾ TẠO VŨ KHÍ (CRAFTING LOOPS & CURRENCY)

Vũ khí trong *Idle Exile* không được bán trong cửa hàng bằng vàng. Thay vào đó, toàn bộ tiến trình nâng cấp trang bị vận hành thông qua các viên ngọc ma thuật (Currency Orbs):

```
[ Săn Phôi Trắng ilvl Cao (Normal) ]
             │
             ▼  (Orb of Transmutation + Orb of Augmentation)
[ Đồ Magic: 1 Prefix + 1 Suffix ] ◄──┐
             │                       │ (Orb of Alteration nếu ra dòng rác)
             ▼ (Hit được Flat Phys T1 hoặc Attack Speed T1)
      [ Dùng Regal Orb ]
             │
             ▼
[ Đồ Rare: 3 Dòng Chuẩn ] ──(Nếu ra dòng rác)──► [ Orb of Scouring: Tẩy về Trắng làm lại ]
             │
             ▼ (Nếu ra dòng ngon)
[ Exalted Orb Slam ] ──► Đập thêm dòng thứ 4, thứ 5
             │
             ▼
[ Divine Orb ] ──► Tinh chỉnh giá trị số của các dòng đạt ngưỡng cao nhất (Max Roll)
             │
             ▼
[ Vaal Orb (Endgame Gamble) ] ──► Corrupt vĩnh viễn món đồ để tìm Implicit Thần Thoại:
                                  - "Adds an additional Arrow"
                                  - "+0.8% to Base Critical Strike Chance"
                                  - (Hoặc phá hủy/khóa vĩnh viễn món đồ nếu thất bại)
```

---

## 7. ĐẶC TẢ GIAO DIỆN & TRẢI NGHIỆM NGƯỜI DÙNG (UI/UX 440x780 COMPANION)

### 7.1. Bố Trí Cửa Sổ Trang Bị (Equipment Slots Layout)
* Ô **Main Hand** (bên trái áo giáp) và **Off Hand** (bên phải áo giáp).
* Kích thước chuẩn: $64 \times 64$ px, bo góc 4px, viền kim loại tối màu.
* **Màu viền theo phẩm cấp:**
  * Trắng: Normal
  * Xanh lam (#4A88CF): Magic
  * Vàng ánh kim (#F3C044): Rare
  * Cam sẫm (#AF6025): Unique
* **Trạng thái Two-Handed:** Khi trang bị vũ khí 2 tay vào Main Hand, ô Off Hand hiển thị mờ đục 30% kèm biểu tượng ổ khóa xích.

---

### 7.2. Thẻ Thông Tin Chi Tiết Vũ Khí (Smart Tooltip)

```
┌──────────────────────────────────────────────┐
│  JEWELLED FOIL OF CELEBRATION                │  <- Tên Rare (Vàng ánh kim)
│  Thrusting One-Handed Sword                  │  <- Loại phôi (Xám nhạt)
├──────────────────────────────────────────────┤
│  Physical Damage: 135 to 275                 │  <- Số xanh dương (đã qua Local Mod)
│  Critical Strike Chance: 6.88%               │  <- Đã tính Local Crit
│  Attacks per Second: 1.98                    │  <- Đã tính Local APS
│  Weapon DPS: 405.9 pDPS                      │  <- Tổng hợp nhanh cho game Idle
├──────────────────────────────────────────────┤
│  Requirements: Level 68, 212 Dex             │
├──────────────────────────────────────────────┤
│  +25% to Global Critical Strike Multiplier   │  <- DÒNG IMPLICIT (Nét đứt ngăn cách)
├──────────────────────────────────────────────┤
│  [P1] 174% Increased Physical Damage         │
│  [P1] Adds 28 to 58 Physical Damage          │
│  [P2] 72% Inc Phys / +142 to Accuracy        │
│  [S1] 26% Increased Attack Speed             │
│  [S1] 36% Increased Critical Strike Chance   │
│  [S2] +18% to Fire Resistance                │
├──────────────────────────────────────────────┤
│  Item Level: 84 (Max Tier: T1)               │
└──────────────────────────────────────────────┘
```

* **Dopamine Quick-Compare (So sánh tức thì):**  
  Khi người chơi mở kho đồ hoặc nhặt trang bị, hệ thống tự động so sánh vũ khí mới với vũ khí đang cầm:
  * Hiển thị chỉ số delta màu xanh lá: `▲ +18.4% Total DPS` hoặc đỏ: `▼ -5.2% Total DPS`.

---

## 8. ĐẶC TẢ KỸ THUẬT & KIẾN TRÚC DỮ LIỆU C# (TECHNICAL ARCHITECTURE)

Tuân thủ nguyên tắc cốt lõi trong [AGENTS.md](file:///e:/Projects/Antigravity/IdleExile/AGENTS.md): **Mọi logic mô phỏng, mô hình dữ liệu và công thức toán học đều nằm trong `src/Core/`, hoàn toàn PURE C# (không kế thừa `Godot.Node`)** để phục vụ việc tính toán Headless Simulation và vòng lặp 30Hz mượt mà.

### 8.1. Data Schema JSON (`data/weapon_bases.json`)
```json
[
  {
    "id": "jewelled_foil",
    "name": "Jewelled Foil",
    "weaponType": "OneHandedSword",
    "handRequirement": "OneHanded",
    "baseMinPhys": 32,
    "baseMaxPhys": 60,
    "baseAps": 1.60,
    "baseCritChance": 5.5,
    "reqLevel": 68,
    "reqStr": 0,
    "reqDex": 212,
    "reqInt": 0,
    "implicitAffixId": "foil_implicit_crit_multi",
    "implicitValue": 25
  }
]
```

### 8.2. C# Core Model Cache (`src/Core/Models/WeaponItemInstance.cs`)
Nhằm loại bỏ hiện tượng Garbage Collection (GC) trong combat loop 30Hz:
* Khi vũ khí được tạo ra hoặc thay đổi affixes, gọi phương thức `RecalculateLocalStats()` một lần duy nhất.
* Các thuộc tính `ComputedMinDamage`, `ComputedMaxDamage`, `ComputedAps`, `ComputedCritChance` được lưu trữ sẵn (cached) để `DamageCalculator.cs` truy xuất tức thì ở mỗi tick tấn công.

---
*Tài liệu này là đặc tả thiết kế hoàn chỉnh của hệ thống trang bị vũ khí trong Idle Exile. Xem chi tiết các bước lập trình, cấu trúc file và kiểm thử tại [WEAPON_SYSTEM_IMPLEMENTATION_PLAN.md](../technical/WEAPON_SYSTEM_IMPLEMENTATION_PLAN.md).*
