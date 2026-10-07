# 13. Thiết Kế Hệ Thống Build Archetypes & Định Hình Meta Chiến Đấu

**Tài liệu Thiết Kế Hệ Thống Chiến Đấu & Cân Bằng (GDD - Build Archetypes & Combat Meta Specification)**  
**Dự án:** *Idle Exile*  
**Vị trí:** Senior Game Systems & Combat Designer  
**Form Factor:** Mini-Window Desktop Companion (`640 x 320` px Compact đến `640 x 780` px Expanded)  
**Tài liệu liên quan:** [03_combat_and_stats.md](03_combat_and_stats.md), [04_gems_and_passives.md](04_gems_and_passives.md), [05_items_and_crafting.md](05_items_and_crafting.md), [09_weapon_equipment_system.md](09_weapon_equipment_system.md), [10_starter_skills_design.md](10_starter_skills_design.md)

---

## 1. TỔNG QUAN & TRIẾT LÝ THIẾT KẾ (CORE PHILOSOPHY)

Trong *Idle Exile*, toàn bộ trải nghiệm chiến đấu vận hành theo nguyên lý tối thượng: **"Zero APM, 100% Theorycrafting"**. 

Ở một tựa game ARPG truyền thống như *Path of Exile (PoE)*, người chơi có thể dựa vào kỹ năng cá nhân (bấm phím kiting, lướt né đòn boss, bấm bình máu đúng nhịp) để bù đắp cho một nhân vật thiếu phòng thủ. Tuy nhiên, trong *Idle Exile*, **Exile tự động di chuyển và tung chiêu trong hành lang 2D 30Hz**. Người chơi không can thiệp trực tiếp vào từng cú né hay vung kiếm. Nếu nhân vật gục ngã trước quái vật hoặc Boss, đó 100% là do **khiếm khuyết trong khâu tính toán Theorycrafting**.

Do đó, một **Build Archetype** trong *Idle Exile* không đơn thuần là tên gọi lớp nhân vật, mà là một **hệ thống toán học khép kín** phối hợp giữa 4 trụ cột:
1. **Kỹ năng Chủ lực & Ngọc Hỗ trợ (Decoupled Gem System)**.
2. **Phôi Trang bị & Chỉ số Cục bộ Vũ khí (Weapon Bases & Local Modifiers)**.
3. **Đồ thị Bảng Nội Tại & Điểm Đột Biến (Passive Web & Keystones)**.
4. **Bộ ba Sinh tồn Bắt buộc (Mitigation + Avoidance + Recovery)**.

```
                      [ 1. Delivery & Clear Pattern ]
                         (Sweep, Projectile, DoT, Aura)
                                      ▲
                                      │
[ 4. Resource & Trigger Loop ] ◄──────┼──────► [ 2. Damage Scaling Vector ]
 (Mana, Leech, LGoH, CWDT)            │           (Impale, Crit, Ignite, Poison)
                                      ▼
                      [ 3. Defensive Triad (EHP) ]
                         (Mitigation + Avoidance + Recovery)
```

---

## 2. KHUNG PHÂN TÍCH 4 TRỤC TOÁN HỌC (THE 4-VECTOR FRAMEWORK)

Mọi build archetype được đưa vào game bắt buộc phải vượt qua bài kiểm tra 4 trục toán học:

### 2.1. Trục 1: Hình thái Xả Sát Thương (Delivery & Clear Pattern)
* **Screen Sweep / Corridor Clear (Dọn hành lang):** Quái vật xuất hiện theo đợt từ 3 - 8 con trong hành lang hẹp 440px. Kỹ năng phải có khả năng dọn quái diện rộng (AoE Arc, Projectile Spread, Proliferation, hoặc Chain/Pierce) để không bị quái áp sát tràn ngập.
* **Single-Target Bossing (Diệt Boss Crucible):** Khi bước vào Boss Arena, quái con biến mất, chỉ còn một Boss duy nhất với lượng EHP khổng lồ. Build phải có cơ chế dồn DPS đơn mục tiêu hoặc tích lũy sát thương DoT lũy tiến.

### 2.2. Trục 2: Vector Tăng Trưởng Sát Thương (Damage Scaling Vector)
Tách bạch triệt để các công thức khuếch đại sát thương theo chuẩn PoE:
$$\text{Final Damage} = \text{Base Damage} \times (1 + \sum \text{Increased} - \sum \text{Reduced}) \times \prod (1 + \text{More}) \times \prod (1 - \text{Less})$$
* **Hit-Based Crit:** Phụ thuộc vào Base Crit vũ khí, tích lũy Crit Chance (tối đa 100%) và Critical Strike Multiplier.
* **Non-Crit Resolute Technique:** Bỏ qua hoàn toàn Crit và Accuracy, tối đa hóa sát thương cơ bản và hệ số More.
* **Ailment DoT (Ignite, Bleed, Poison):** Sát thương duy trì theo thời gian, bỏ qua chỉ số giáp thông thường.

### 2.3. Trục 3: Bộ Ba Phòng Thủ Bắt Buộc (The Defensive Triad)
Trong chế độ Zero-APM, một build chỉ có HP/ES dày là **chắc chắn thất bại**. Build phải sở hữu đủ 3 lớp:
1. **Mitigation (Giảm thương thuần):**
   * Physical: Giáp (`Armour`) theo công thức phi tuyến tính chống đòn đánh nhỏ/vừa, cộng thêm Endurance Charges.
   * Elemental/Chaos: Kháng nguyên tố chạm mốc trần chuẩn 75% (`Elemental Resistances`).
   * Spell Damage: Tỉ lệ triệt tiêu sát thương phép (`Spell Suppression`) giảm 50% sát thương phép nhận vào.
2. **Avoidance (Tránh đòn):**
   * Né tránh (`Evasion`) vận hành theo cơ chế Entropy không ngẫu nhiên tuyệt đối, giúp tránh các chuỗi đòn đánh vật lý liên tiếp.
   * Đỡ đòn (`Block` Attacks / Spells) vô hiệu hóa 100% sát thương của đòn bị đỡ.
3. **Recovery (Cơ chế Hồi phục tức thời & duy trì):**
   * Hồi máu/giây (`Life Regeneration` % hoặc Flat).
   * Hút sinh lực (`Life Leech`) có trần hồi phục mỗi giây.
   * Hồi máu mỗi đòn đánh trúng (`Life Gain on Hit - LGoH`) — Cực kỳ mạnh trong chiến đấu tự động với tốc độ đánh cao.

### 2.4. Trục 4: Vòng Lặp Tài Nguyên & Kích Hoạt Tự Động (Resource & Automation)
* **Mana Economy:** Tốc độ tấn công (APS) cao kết hợp nhiều Support Gems sẽ nhân vọt Mana Cost ($130\% \times 140\% \times 150\%$). Build phải có giải pháp duy trì mana tự động (Mana Leech, Mana on Hit, hoặc Keystones như *Eldritch Battery*, *Blood Magic*).
* **Automated Guard Triggers:** Sử dụng khay kỹ năng `Slot 3 (Trigger)` gắn `Cast when Damage Taken Support (CWDT)` để tự động bung khiên bảo hộ (*Steelskin*, *Molten Shell*) khi máu tụt dưới ngưỡng nguy hiểm.

---

## 3. MA TRẬN 7 BUILD ARCHETYPES CHIẾN LƯỢC (CORE ARCHETYPE MATRIX)

```
┌─────────────────────────────────────────────────────────────────────────────────────────────────┐
│                                 BẢNG MA TRẬN 7 BUILD ARCHETYPES                                 │
├───────────────────┬──────────────┬──────────────────┬───────────────────────┬───────────────────┤
│ Archetype         │ Thuộc Tính   │ Kỹ Năng / Phôi   │ Cơ Chế Sát Thương     │ Trụ Cột Phòng Thủ │
├───────────────────┼──────────────┼──────────────────┼───────────────────────┼───────────────────┤
│ 1. Bleed Slammer  │ Pure Str     │ Cleave / 2H Axe  │ Resolute Tech + Bleed │ Armour + Max Life │
│ 2. Ignite Burner  │ Pure Int     │ Fireball / Wand  │ Avatar of Fire + DoT  │ ES / MoM + Ignite │
│ 3. Crit Deadeye   │ Pure Dex     │ Split Arrow / Bow│ Crit Multi + Projectile│ Evasion + Suppress│
│ 4. Poison Claw    │ Dex / Int    │ Viper / Claw     │ Poison Stacking + APS │ LGoH + Elusive    │
│ 5. Walking Plague │ Str / Int    │ RF / Death Aura  │ Fire DoT Aura (0 Click)│ Max Fire Res+Regen│
│ 6. Archmage Mana  │ Pure Int     │ Lightning / Staff│ Mana Spent as Added Dmg│ Mana-as-Life (MoM)│
│ 7. Dread Necro    │ Pure Int/Str │ Zombie / Wand    │ Minion Army + Scaling │ Meat-Shield Taunt │
└───────────────────┴──────────────┴──────────────────┴───────────────────────┴───────────────────┘
```

---

### ARCHETYPE 1: THE UNSTOPPABLE JUGGERNAUT (BLEED SLAMMER)

* **Triết lý:** "Đập một phát nát một đàn". Chậm rãi, vững như bàn thạch, miễn nhiễm rủi ro trượt đòn. Đây là archetype **League Starter** số 1 với sàn sức mạnh cao nhất.
* **Gốc thuộc tính:** Pure Strength (Marauder).
* **Vũ khí tối ưu:** **Two-Handed Axe** (Rìu hai tay sở hữu Base Physical Damage cao nhất game).

```
Khay Kỹ Năng (Skill Setup):
├── Slot 1 (Primary DPS): Cleave
│     ├── Support 1: Melee Physical Damage Support (Đỏ - 35% More Phys)
│     ├── Support 2: Brutality Support (Đỏ - 40% More Phys, Không gây Ele)
│     ├── Support 3: Chance to Bleed Support (Đỏ - Flat Phys + 25% Bleed Chance)
│     └── Support 4: Concentrated Effect Support (Lam - 30% More Area Dmg)
├── Slot 2 (Secondary/Debuff): Seismic Slam / Intimidating Cry (Gây Intimidate quái chịu thêm 10% sát thương)
├── Slot 3 (Trigger): Cast when Damage Taken + Molten Shell (Cộng hàng nghìn Armor khi nhận đòn)
└── Slot 4 & 5 (Auras): Determination (+Armour phẳng & % More Armour) + Pride (Quái xung quanh nhận thêm Phys)
```

* **Định tuyến Nội tại (Passive Pathing):**
  * **Keystone Trọng Tâm:** `Resolute Technique` (Đòn đánh luôn trúng 100%, không bao giờ Crit). Giải phóng hoàn toàn gánh nặng tìm chỉ số Accuracy trên trang bị.
  * **Notables:** `Juggernaut's Bastion` (+Armour, +% Max Life), `Bloodletting` (+Bleed Damage, sát thương tăng thêm lên kẻ địch đang chảy máu).
* **Cơ chế Phòng thủ:**
  * Giáp đạt 15,000 - 25,000 Armour.
  * Hồi máu theo % Life Regen mỗi giây (4% - 8% Max Life/s).
* **Đánh giá Cân bằng:**
  * *Sàn sức mạnh (Floor):* Rất cao. Chạy tốt từ T1 đến T12 chỉ với đồ Magic/Rare nhặt được.
  * *Trần sức mạnh (Ceiling):* Trung bình. Giới hạn bởi tốc độ đánh thấp và cơ chế Bleed không cộng dồn vô hạn.

---

### ARCHETYPE 2: FLAME OVERLORD (IGNITE PROLIFERATION ELEMENTALIST)

* **Triết lý:** "Một tia lửa thiêu rụi cả đạo quân". Vua dọn dẹp hành lang hẹp nhờ cơ chế cháy lây lan từ xác quái vật.
* **Gốc thuộc tính:** Pure Intelligence (Witch).
* **Vũ khí tối ưu:** **Wand** (Đũa phép tăng Spell Damage) hoặc **Sceptre** (Pháp trượng có Implicit tăng Elemental Damage).

```
Khay Kỹ Năng (Skill Setup):
├── Slot 1 (Primary DPS): Fireball
│     ├── Support 1: Ignite Proliferation Support (Lam - Lây lan thiêu đốt)
│     ├── Support 2: Added Fire Damage Support (Đỏ - Thêm sát thương Hỏa)
│     ├── Support 3: Burning Damage Support (Đỏ - 35% More Burning Damage)
│     └── Support 4: Deadly Ailments Support (Lục - Tăng mạnh DoT, giảm sát thương Hit)
├── Slot 2 (Secondary/Debuff): Flame Wall (Tạo tường lửa cộng thêm Hỏa sát thương cho đạn bay qua)
├── Slot 3 (Trigger): Cast when Damage Taken + Steelskin (Tạo lớp khiên hấp thụ 500-1200 sát thương)
└── Slot 4 & 5 (Auras): Anger (Thêm sát thương Hỏa) + Purity of Elements (+Kháng và miễn nhiễm Ailments)
```

* **Định tuyến Nội tại (Passive Pathing):**
  * **Keystone Trọng Tâm:** `Avatar of Fire` (Chuyển 50% Phys/Cold/Lightning thành Fire, cấm tuyệt đối Non-Fire Damage).
  * **Notables:** `Flame Covenant` (+Fire Damage, +Ignite Chance), `Arcane Potency` (+Spell Damage, +Energy Shield).
* **Cơ chế Phòng thủ:**
  * Lớp bảo hộ kép: `Energy Shield (ES)` hấp thụ sát thương trước, kết hợp cơ chế `Elemental Aegis`.
  * Phòng thủ tầm xa: Quái vật chết cháy từ ngoài rìa màn hình trước khi kịp chạm vào Exile.
* **Đánh giá Cân bằng:**
  * *Sàn sức mạnh (Floor):* Cao. Cơ chế Ignite Proliferation giúp vượt qua các đợt quái đông cực kỳ an toàn.
  * *Trần sức mạnh (Ceiling):* Cao. Dễ dàng nâng cấp thông qua trang bị tăng cấp độ Gem (`+1 to Level of all Fire Spell Skill Gems`).

---

### ARCHETYPE 3: WINDRUNNER GALE (CRIT PROJECTILE DEADEYE)

* **Triết lý:** "Mưa tên bão táp, bách phát bách trúng". Tốc độ dọn bản đồ thần tốc nhất game, đánh đổi bằng sinh mệnh mỏng manh nếu bị áp sát.
* **Gốc thuộc tính:** Pure Dexterity (Ranger).
* **Vũ khí tối ưu:** **Bow** (Cung tốc độ cao) + **Quiver** (Ống tên với Implicit `+25% Critical Strike Multiplier` hoặc `Arrows Pierce an additional Target`).

```
Khay Kỹ Năng (Skill Setup):
├── Slot 1 (Primary DPS): Split Arrow
│     ├── Support 1: Greater Multiple Projectiles Support (Lục - Bắn thêm 4 mũi tên)
│     ├── Support 2: Pierce Support (Lục - Mũi tên xuyên qua 3 kẻ thù)
│     ├── Support 3: Increased Critical Strikes Support (Lam - Tăng Base Crit và % Crit)
│     └── Support 4: Added Cold Damage Support (Lục - Thêm Băng, làm chậm và đóng băng quái)
├── Slot 2 (Secondary/Debuff): Sniper's Mark (Nguyền rủa quái Tinh Anh nhận thêm sát thương đạn)
├── Slot 3 (Trigger): Cast when Damage Taken + Frost Wall (Dựng tường băng ngăn quái áp sát)
└── Slot 4 & 5 (Auras): Grace (Tăng vọt chỉ số Evasion) + Herald of Ice (Quái vỡ vụn khi chết gây nổ dây chuyền)
```

* **Định tuyến Nội tại (Passive Pathing):**
  * **Keystone Trọng Tâm:** Không lấy Resolute Technique. Di chuyển sang cụm `Shadow's Veil` để lấy các node tăng Crit Chance, Crit Multi và Né Tránh.
  * **Notables:** `Hunter's Precision` (+Projectile Damage, +Accuracy), `Deadly Infusion` (+Crit Multiplier).
* **Cơ chế Phòng thủ:**
  * **Entropy-based Evasion:** Chỉ số Né Tránh 12,000+ giúp né 70-80% đòn đánh vật lý thông thường.
  * **Spell Suppression (Triệt tiêu phép):** Tích lũy đủ 100% Spell Suppression trên trang bị Dex $\rightarrow$ Luôn giảm 50% mọi sát thương phép nhận vào, triệt tiêu nguy cơ đột tử.
* **Đánh giá Cân bằng:**
  * *Sàn sức mạnh (Floor):* Thấp. Đầu game phụ thuộc vào vũ khí có flat damage và độ chuẩn xác (Accuracy).
  * *Trần sức mạnh (Ceiling):* Cực kỳ cao (God-tier Clear Speed khi đạt đủ đồ Crit).

---

### ARCHETYPE 4: NIGHTBLADE LEECH (POISON CLAW ASSASSIN)

* **Triết lý:** "Chém nhanh như chớp, độc thấm vào tủy". Chuyên gia hạ sát Boss Crucible và các mục tiêu trâu máu nhờ cơ chế Độc cộng dồn vô hạn.
* **Gốc thuộc tính:** Dexterity / Intelligence (Shadow).
* **Vũ khí tối ưu:** **Claws** (Vuốt sở hữu Implicit độc nhất: `+35 to 45 Life gained for each Enemy hit`).

```
Khay Kỹ Năng (Skill Setup):
├── Slot 1 (Primary DPS): Viper Strike / Cobra Lash
│     ├── Support 1: Poison Support (Lục - Tăng cơ hội gây độc và sát thương Độc)
│     ├── Support 2: Multistrike Support (Đỏ - Đánh lặp lại 3 lần với tốc độ tăng vọt)
│     ├── Support 3: Nightblade Support (Lục - Cực kỳ nhiều Crit và hiệu ứng Elusive né đòn)
│     └── Support 4: Vicious Projectiles / Void Manipulation (Lục/Lam - More Chaos Damage)
├── Slot 2 (Secondary/Debuff): Withering Step (Tích tụ tầng Wither làm quái nhận thêm sát thương Chaos)
├── Slot 3 (Trigger): Cast when Damage Taken + Blood Rage (Tự tăng tốc độ đánh và Frenzy Charges)
└── Slot 4 & 5 (Auras): Malevolence (More Damage over Time) + Herald of Agony (Thêm độc và đệ bọ cạp)
```

* **Định tuyến Nội tại (Passive Pathing):**
  * **Keystone Trọng Tâm:** `Perfect Agony` (Chỉ số Critical Strike Multiplier được áp dụng trực tiếp để tăng sát thương Độc).
  * **Notables:** `Noxious Strike` (+Poison Duration, tăng thời gian duy trì độc theo số lượng độc đã gây ra).
* **Cơ chế Phòng thủ Vô Địch Trong Game Idle:**
  * **Life Gain on Hit (LGoH):** Khác với Leech bị giới hạn trần hồi máu mỗi giây, LGoH hồi máu **ngay lập tức** trên mỗi nhát chém trúng đích. Khi Exile đạt APS 3.5 và chém xuyên đàn quái 6 con = 21 hits/giây $\rightarrow$ Hồi phục tức thì $21 \times 40 = 840\text{ Life/giây}$!
  * **Elusive Buff:** Cung cấp 15% cơ hội tránh mọi sát thương và tăng tốc độ di chuyển.
* **Đánh giá Cân bằng:**
  * *Sàn sức mạnh (Floor):* Trung bình.
  * *Trần sức mạnh (Ceiling):* Vô địch về DPS hạ gục Boss đơn lẻ (Single-Target Melt).

---

### ARCHETYPE 5: THE WALKING CALAMITY (RIGHTEOUS FIRE AUTOBOMBER)

* **Triết lý:** "Chân nhân bất lộ tướng, bước đi đâu quái tàn rụi tới đó". Đây là **đỉnh cao của dòng game Idle**, nhân vật hoàn toàn không cần vung đòn, hào quang tự hủy diệt tất cả.
* **Gốc thuộc tính:** Strength / Intelligence (Templar / Marauder).
* **Vũ khí tối ưu:** **Sceptre** (Tăng Elemental Damage và Fire Damage) + **Tower Shield** (Tăng Max Fire Resistance và Armour).

```
Khay Kỹ Năng (Skill Setup):
├── Slot 1 (Primary DPS): Righteous Fire (Aura tự đốt máu bản thân tạo vòng lửa xung quanh)
│     ├── Support 1: Elemental Focus Support (Lam - More Elemental Damage, không gây sốc/đóng băng)
│     ├── Support 2: Increased Area of Effect Support (Lam - Mở rộng bán kính vòng lửa ra 60px)
│     ├── Support 3: Burning Damage Support (Đỏ - More Burning Damage)
│     └── Support 4: Swift Affliction Support (Lục - More Damage over Time)
├── Slot 2 (Secondary/Single-target): Fire Trap (Ném bẫy lửa tạo vũng DoT bồi thêm sát thương khi gặp Boss)
├── Slot 3 (Trigger): Cast when Damage Taken + Wave of Conviction (Tự giảm kháng Lửa của kẻ thù khi bị đánh)
└── Slot 4 & 5 (Auras): Purity of Fire (+Max Fire Resistance) + Determination (+Armour khổng lồ)
```

* **Định tuyến Nội tại (Passive Pathing):**
  * **Keystone Trọng Tâm:** Nhắm tới các node tăng `Maximum Fire Resistance` (+1% to +3% Max Fire Res) và toàn bộ các cụm hồi máu theo phần trăm.
  * **Notables:** `Prismatic Bulwark` (+Elemental Resistances, +Max Life), `Heart of Flame`.
* **Cơ chế Phòng thủ (Bài Toán Sinh Tồn):**
  * Nhân vật liên tục tự thiêu đốt bản thân với sát thương hỏa bằng $90\%$ máu tối đa mỗi giây.
  * **Lời giải toán học:** Khi nâng `Maximum Fire Resistance` lên **$88\% - 90\%$**, lượng sát thương nhận vào giảm xuống chỉ còn $9\% - 10\%$ máu/giây. Lúc này, chỉ số Life Regeneration ($12\% - 15\%$ Max Life/s) sẽ vượt qua lượng tự đốt $\rightarrow$ Nhân vật vừa hồi máu vừa mang hào quang hủy diệt vĩnh viễn.
* **Đánh giá Cân bằng:**
  * *Sàn sức mạnh (Floor):* Cực thấp. Đầu game không thể kích hoạt được nếu chưa đủ đồ và chỉ số Kháng/Hồi phục.
  * *Trần sức mạnh (Ceiling):* Cực kỳ thỏa mãn, biểu tượng danh giá của người chơi đạt cấp độ cao (Late-Game Trophy Build).

---

### ARCHETYPE 6: ARCHMAGE STORM (MIND OVER MATTER HIEROPHANT)

* **Triết lý:** "Năng lượng vô tận chuyển hóa thành khiên sinh mệnh và sấm sét". Sử dụng Mana làm nguồn sống thay cho Máu.
* **Gốc thuộc tính:** Pure Intelligence / Strength (Templar / Witch).
* **Vũ khí tối ưu:** **Staff** (Gậy phép 2 tay tăng Spell Damage và Block) hoặc **Wand + Spirit Shield**.

```
Khay Kỹ Năng (Skill Setup):
├── Slot 1 (Primary DPS): Spark / Arc
│     ├── Support 1: Archmage Support (Lam - Tiêu tốn lượng lớn Mana để thêm Flat Lightning Damage khổng lồ)
│     ├── Support 2: Spell Echo Support (Lam - Thi triển lặp lại đòn phép với tốc độ cao)
│     ├── Support 3: Lightning Penetration Support (Lam - Bỏ qua kháng Lôi của kẻ địch)
│     └── Support 4: Increased Critical Strikes Support (Lam - Đẩy mạnh tỉ lệ giật sét chí mạng)
├── Slot 2 (Secondary/Debuff): Sigil of Power (Tạo trận địa sấm sét tăng sát thương khi đứng trong trận)
├── Slot 3 (Trigger): Cast when Damage Taken + Arcane Cloak (Dùng Mana tạo khiên chắn sát thương cực dày)
└── Slot 4 & 5 (Auras): Clarity (Hồi phục hàng trăm Mana mỗi giây) + Wrath (Tăng sát thương Lôi)
```

* **Định tuyến Nội tại (Passive Pathing):**
  * **Keystone Trọng Tâm:** `Mind Over Matter (MoM)` (40% sát thương nhận vào trừ thẳng vào Mana trước khi trừ vào Máu) kết hợp `Agnostic` (Dùng Mana hồi phục thần tốc cho Máu).
  * **Notables:** `Arcane Conflux`, `Deep Thoughts` (+% Maximum Mana, +Mana Regeneration Rate).
* **Cơ chế Phòng thủ:**
  * Bể sinh lực lai: 3,000 Máu + 7,000 Mana = **10,000 Effective HP pool**.
  * Tốc độ hồi năng lượng 600 - 1,000 Mana/giây đóng vai trò như một bình máu khổng lồ hồi liên tục.
* **Đánh giá Cân bằng:**
  * *Sàn sức mạnh (Floor):* Trung bình.
  * *Trần sức mạnh (Ceiling):* Rất cao. Quy mô sát thương tỷ lệ trực tiếp với tổng lượng Mana tối đa của nhân vật.

---

### ARCHETYPE 7: THE DREAD NECROMANCER (LORD OF THE DEAD)

* **Triết lý:** "Đội quân bất tử mở đường, Exile nhàn nhã phía sau". Chuẩn mực tối thượng của phong cách chiến đấu **Zero APM**. Đệ tử tự động tìm diệt mục tiêu và tạo thành bức tường thịt bảo vệ chủ nhân.
* **Gốc thuộc tính:** Pure Intelligence (Witch) hoặc Strength / Intelligence (Templar Guardian).
* **Vũ khí & Trang bị tối ưu:**
  * **Convoking Wand:** Phôi đũa phép độc quyền của Necromancer với Implicit: `Minions deal (15-30)% increased Damage`.
  * **Bone Helmet:** Mũ xương với Implicit: `Minions deal (15-20)% increased Damage`.
  * **Spirit Shield:** Tăng Energy Shield và Minion Damage.

```
Khay Kỹ Năng (Minion Loadout):
├── Slot 1 (Primary DPS): Raise Zombie (Quân đoàn Zombie cận chiến vĩnh viễn, tự động hồi sinh)
│     ├── Support 1: Minion Damage Support (Lam - 35% More Minion Damage)
│     ├── Support 2: Feeding Frenzy Support (Lam - Minions hung hãn lao tới cắn xé quái)
│     ├── Support 3: Melee Splash Support (Đỏ - Đòn đánh Zombie lan tỏa thành hình tròn)
│     └── Support 4: Minion Speed Support (Lục - Tăng tốc chạy và tốc độ tấn công của Minion)
├── Slot 2 (Secondary / Boss Burner): Summon Raging Spirits (SRS - Hộp sọ lửa tầm nhiệt xả thêm khi gặp Boss)
├── Slot 3 (Trigger): Cast when Damage Taken + Convocation (Kéo ngay đàn Zombie về quây quanh Exile khi nhận đòn)
└── Slot 4 & 5 (Auras): Commander of the Gathering (+Minion Damage & APS) + Determination (+Armour phòng ngự)
```

* **Cơ Chế Vận Hành Chuẩn PoE 2 (Permanent Reservation & Meat Shield):**
  * **Không bấm nút triệu hồi thủ công:** Active Gem `Raise Zombie` giam giữ cố định 25% Mana của Exile. Đàn Zombie luôn xuất hiện sẵn trên sàn đấu 2D. Nếu Zombie chết, thời gian hồi (Respawn Cooldown 3s) sẽ tự động triệu hồi lại mà không cần xác quái (Corpse-less).
  * **Hệ quy chiếu toán học độc lập:** Chỉ số cá nhân của Exile (Player Damage/Crit) không ảnh hưởng tới Minion. Minion sở hữu thanh Máu, Giáp và DPS độc lập, nhận toàn bộ khuếch đại từ các dòng `Minions deal X% increased Damage` và `% More Minion Damage`.
* **Định tuyến Nội tại (Passive Pathing):**
  * **Keystone Trọng Tâm:** `Minion Instability` (Khi máu Minion tụt xuống mức Nguy kịch < 35%, Minion tự phát nổ gây 33% Máu tối đa của nó thành sát thương Hỏa diện rộng).
  * **Notables:** `Lord of the Dead` (+1 Max Zombies, +20% Minion Damage), `Death Attunement` (+1 Spectre, +1 Zombie).
* **Cơ chế Phòng thủ Cốt lõi (The Meat Shield):**
  * **Body Blocking & Taunt:** Đàn Zombie dàn hàng ngang trong hành lang 440px hẹp, hút toàn bộ đòn đánh của quái vật. Đạn đạo của quái bị chặn lại bởi thân xác Zombie trước khi kịp chạm tới Exile.
* **Đánh giá Cân bằng:**
  * *Sàn sức mạnh (Floor):* Rất cao. Vượt trội về độ an toàn trong các đợt cày cuốc ban đầu.
  * *Trần sức mạnh (Ceiling):* Cực kỳ cao khi đầu tư vào đồ có nhiều dòng `+1 to Level of all Minion Skill Gems`.

---

## 4. HỆ THỐNG KHẮC TINH BẢN ĐỒ & KINH TẾ TIỀN TỆ (MAP MOD FRICTION)

Để ngăn chặn một build duy nhất thống trị toàn bộ game (tử huyệt triệt tiêu tính khám phá), hệ thống Bản đồ ([data/maps.json](file:///E:/Projects/Antigravity/IdleExile/data/maps.json)) thiết lập các thuộc tính biến dị (Map Modifiers) có tính khắc chế tương hỗ rõ rệt:

| Map Modifier (Thuộc Tính Bản Đồ) | Archetype Bị Vô Hiệu Hóa / Nguy Hiểm Cao | Phản Ứng Của Người Chơi (Theorycrafting & Economy) |
| :--- | :--- | :--- |
| **Physical Reflect (Phản sát thương Vật lý)** | *Archetype 1 (Bleed Slammer), Archetype 3 (Bow)* | **Không thể chạy được.** Phải dùng *Orb of Scouring* + *Orb of Alchemy* hoặc *Chaos Orb* để reroll map. |
| **Elemental Reflect (Phản sát thương Nguyên tố)** | *Archetype 2 (Fireball), Archetype 5 (RF), Archetype 6 (Archmage)* | **Không thể chạy được.** Đòn đánh nguyên tố sẽ dội ngược lại giết chết Exile trong 1 cú nổ. Bắt buộc reroll. |
| **Cannot Leech Life / Mana** | *Archetype 4 (Poison Claw), Archetype 6 (Archmage)* | Cực kỳ nguy hiểm do mất nguồn hồi phục chính. Người chơi phải chuyển sang lắp bình Flask tự động hoặc đổi map. |
| **Players have 60% Reduced Life Recovery Rate** | *Archetype 1 (Juggernaut), Archetype 5 (Righteous Fire)* | Vô hiệu hóa khả năng tự hồi máu. Đối với RF, mod này sẽ khiến nhân vật tự đốt chết chính mình. |
| **Monsters cannot be Evaded / Accuracy Cap** | *Archetype 3 (Crit Deadeye)* | Vô hiệu hóa lớp Evasion, biến Deadeye thành bia tập bắn. Cần bổ sung trang bị tăng Giáp hoặc Spell Suppression. |
| **Monsters have +40% Chaos Resistance** | *Archetype 4 (Poison Claw)* | Làm giảm 50% DPS thực tế; người chơi cần khảm thêm Support Gem giảm kháng (*Withering Touch*). |
| **Monsters fire 2 additional Projectiles and Chain** | *Archetype 7 (Dread Necromancer)* | Đạn quái bắn vào đàn Zombie sẽ nảy (Chain) thẳng vào Exile đứng phía sau. Nguy cơ đột tử rất cao. |
| **Monsters create Desecrated Ground on Death** | *Archetype 7 (Dread Necromancer)* | Vũng axit Chaos trên sàn làm tan rã bầy Zombie trước khi kịp tiếp cận quái. |

---

## 5. KẾ HOẠCH TRIỂN KHAI DỮ LIỆU & KIỂM THỬ (IMPLEMENTATION ROADMAP)

Để đưa toàn bộ 7 Archetypes vào trạng thái hoạt động thực tế trên nền tảng C# và Godot:

### 5.1. Dữ liệu Kỹ năng (`data/gems.json`)
* [x] Đã có: `cleave`, `fireball`, `split_arrow`, `added_fire_damage`, `ignite_proliferation`, `multiple_projectiles`, `melee_physical_damage`, `concentrated_effect`.
* [ ] Cần bổ sung:
  * Active Gems: `viper_strike` (Claw Poison), `righteous_fire` (Fire Aura), `spark` (Lightning Spell), `molten_shell` (Guard Skill), `raise_zombie` (Minion Summon), `summon_raging_spirit` (Homing Minion).
  * Support Gems: `cast_when_damage_taken` (Trigger), `brutality` (Pure Phys), `poison_support` (Chaos DoT), `nightblade_support` (Crit/Claw), `archmage_support` (Mana scaling), `elemental_focus` (No Ailments More Dmg), `minion_damage` (More Minion Dmg), `feeding_frenzy` (Aggressive Minions), `meat_shield` (Defensive Minions).

### 5.2. Dữ liệu Bảng Nội Tại (`data/passive_tree.json`)
* [x] Đã có: `ks_resolute_technique`, `ks_avatar_of_fire`, `ks_chaos_inoculation`, các cụm xuất phát Marauder, Witch, Ranger.
* [ ] Cần bổ sung:
  * Keystones: `ks_mind_over_matter`, `ks_perfect_agony`, `ks_iron_reflexes`, `ks_eldritch_battery`, `ks_minion_instability`.
  * Clusters: Cụm Vũ khí Vuốt & Hồi máu trên đòn đánh (`Claw & LGoH Mastery`), Cụm Tăng trần Kháng Hỏa (`Fire Specialization`), Cụm Mở rộng Năng lượng (`Mana Reservoir`), Cụm Quân Đoàn Bất Tử (`Lord of the Dead`, `Death Attunement`).

### 5.3. Pipeline Tính Toán Sát Thương & Thực Thể Minion (`src/Core/Services/`)
* [x] Đã hoàn thiện tính toán *Increased* vs *More* ([DamageCalculator.cs](file:///E:/Projects/Antigravity/IdleExile/src/Core/Services/DamageCalculator.cs)).
* [x] Đã tích hợp Ailments (Bleed, Ignite, Poison) ([CombatAttackService.cs](file:///E:/Projects/Antigravity/IdleExile/src/Core/Services/CombatAttackService.cs)).
* [ ] Cần kiểm tra tích hợp: Logic kích hoạt tự động theo ngưỡng máu cho `Slot 3 (Trigger)` trong [CombatEngine.cs](file:///E:/Projects/Antigravity/IdleExile/src/Core/Simulation/CombatEngine.cs).
* [ ] Cần tích hợp: Thực thể Minion (`MinionInstance.cs`) kế thừa `ICombatEntity` với AI tự động tiến lên chặn quái và gây sát thương độc lập.

