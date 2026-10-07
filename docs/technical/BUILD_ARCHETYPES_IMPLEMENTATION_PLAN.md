# BUILD_ARCHETYPES_IMPLEMENTATION_PLAN.md — Kế Hoạch Triển Khai Kỹ Thuật Hệ Thống Build Archetypes & Meta

Tài liệu này chi tiết hóa lộ trình kỹ thuật và phân rã các tác vụ phát triển thành **từng Prompt thực thi độc lập (Ready-to-Execute Prompts)** để hiện thực hóa toàn bộ **7 Build Archetypes Chiến Lược** theo đặc tả tại [13_build_archetypes_specification.md](../design/13_build_archetypes_specification.md).

---

## 1. NGUYÊN TẮC KIẾN TRÚC & PHÂN TÁCH TRÁCH NHIỆM (ARCHITECTURE CONSTRAINTS)

Tuân thủ nghiêm ngặt các nguyên tắc trong [AGENTS.md](file:///E:/Projects/Antigravity/IdleExile/AGENTS.md):
1. **Phân Tách Tuyệt Đối (Separation of Concerns):**
   * Toàn bộ mô hình dữ liệu, công thức toán học và logic tính toán nằm trong `src/Core/` (100% C# thuần, **không kế thừa hay phụ thuộc `Godot.Node`**).
   * Dữ liệu cân bằng và cấu hình kỹ năng được lưu trữ tập trung tại `data/*.json` (**không hardcode chỉ số trong C#**).
2. **Triết Lý "Zero APM, 100% Theorycrafting":**
   * Không xây dựng bất kỳ cơ chế điều khiển thủ công hay né chiêu bằng tay nào.
   * Tất cả hành vi buff phòng ngự, duy trì tài nguyên, triệu hồi đệ tử và dồn đòn phải hoạt động tự động thông qua quy tắc vòng lặp (Trigger, Leech, LGoH, Minion Reservation, Auras).
3. **PoE Mathematical Precision:**
   * Tách bạch tuyệt đối giữa tăng/giảm cộng dồn (`Increased / Reduced`) và nhân cấp số (`More / Less`).
   * Phân biệt rõ Local Modifiers (thay đổi trực tiếp phôi vũ khí) và Global Modifiers.
   * Minion sở hữu hệ quy chiếu toán học độc lập: Chỉ số người chơi không áp dụng lên đệ tử trừ khi có dòng ghi rõ `Minions deal...`.
4. **Chất Lượng Mã Nguồn & Bản Dịch:**
   * Mọi nhãn hiển thị trong game mặc định tiếng Anh.
   * Sau mỗi Prompt triển khai, mã nguồn phải đạt `dotnet build` với **0 Warning(s), 0 Error(s)**.

---

## 2. LỘ TRÌNH TRIỂN KHAI 5 GIAI ĐOẠN (PHASED WORK BREAKDOWN)

```
┌────────────────────────────────────────────────────────────────────────┐
│ Phase 1: Data Architecture — Gems & Support Links (data/gems.json)    │
│   ├── Bổ sung 6 Active Gems: Viper Strike, Righteous Fire, Spark,      │
│   │   Molten Shell, Raise Zombie, Summon Raging Spirits                │
│   └── Bổ sung 11 Support Gems: Brutality, Poison, Nightblade, Archmage,│
│       CWDT, Pierce, Increased Crit, Ele Focus, Minion Damage,          │
│       Feeding Frenzy, Meat Shield                                      │
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│ Phase 2: Passive Tree Graph Expansion (data/passive_tree.json)         │
│   ├── Tích hợp Keystones: Resolute Technique, Mind Over Matter,        │
│   │   Perfect Agony, Iron Reflexes, Eldritch Battery, Minion Instability│
│   └── Bổ sung các Cụm Nội Tại: Claw LGoH, Mana Reservoir,              │
│       Fire Specialization (+Max Fire Res), Bow Pierce, Minion Army     │
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│ Phase 3: Core Simulation & Combat Engine Pipeline (src/Core/)          │
│   ├── Hỗ trợ cơ chế Trigger tự động Slot 3 (CWDT theo ngưỡng sát thương)│
│   ├── Tích hợp Life Gain on Hit (LGoH) tức thì trong Attack Service    │
│   ├── Cơ chế Righteous Fire: Tự đốt máu & Hào quang thiêu đốt xung quanh│
│   ├── Tích hợp Mind Over Matter (MoM) chia tách sát thương vào Mana    │
│   ├── Cơ chế Resolute Technique (100% Hit, 0% Crit) trong Damage Calc  │
│   └── Thực thể Minion (MinionInstance): Meat-shield, AI di chuyển và   │
│       vòng lặp hồi sinh tự động (PoE 2 Reservation Model)              │
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│ Phase 4: Gear, Affixes & Map Mod Counters (data/affixes.json, maps.json│
│   ├── Bổ sung Affixes: DoT Multi, Flat Mana, % Max Mana, Max Fire Res, │
│   │   Minion Damage, Minion Life, Phôi Convoking Wand & Bone Helmet     │
│   └── Cập nhật Map Modifiers: Reflect Phys/Ele, Cannot Leech,          │
│       Reduced Recovery, Monsters Cannot be Evaded, Monsters Chain      │
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│ Phase 5: Automated Testing Suite & Headless CI Verification            │
│   ├── Tạo ArchetypeSimulationTests.cs kiểm thử tự động 7 Archetypes    │
│   └── Chạy xác minh Headless trên Godot Console đảm bảo 0 lỗi          │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 3. DANH SÁCH CÁC PROMPT CHI TIẾT SẴN SÀNG THỰC THI (READY-TO-EXECUTE PROMPTS)

Dưới đây là 5 Prompt chi tiết, tuần tự, có thể copy trực tiếp vào IDE/CLI để AI Agent thực hiện từng bước mà không cần thêm chỉ dẫn.

---

### PROMPT 1: MỞ RỘNG THƯ VIỆN GEMS CHO 7 BUILD ARCHETYPES

```markdown
### Mục tiêu:
Mở rộng tập tin `data/gems.json` để bổ sung đầy đủ các Active Gems và Support Gems cốt lõi định hình 7 Build Archetypes theo tài liệu `docs/13_build_archetypes_specification.md`.

### Ngữ cảnh & Tập tin tác động:
- Target File: `data/gems.json`
- Kiểm tra tính tương thích: `src/Core/Services/GemCompatibilityValidator.cs` và `src/Core/Models/GemDefinition.cs`

### Nhiệm vụ chi tiết:
1. Bổ sung các **Active Gems** sau vào `data/gems.json`:
   - `viper_strike`: Category "Active", Color "Green", Tags: `["attack", "melee", "chaos", "poison"]`, WeaponRestrictions: `["claw", "dagger", "sword"]`. Base: 130% Damage Multiplier, 100% APS, 60% chance to Poison on Hit.
   - `righteous_fire`: Category "Active", Color "Blue", Tags: `["spell", "fire", "aoe", "dot"]`, WeaponRestrictions: `["sceptre", "wand", "staff"]`. Base: Tự thiêu đốt người chơi 90% Max Life/giây dưới dạng Fire DoT; gây sát thương Fire DoT bằng 35% Max Life/giây lên kẻ địch trong bán kính 60px xung quanh.
   - `spark`: Category "Active", Color "Blue", Tags: `["spell", "projectile", "lightning"]`, WeaponRestrictions: `["wand", "sceptre", "staff"]`. Base: Bắn ra 4 tia sét đạn đạo ngẫu nhiên di chuyển zíc-zắc, tốc độ 450px/s, sát thương Lightning Min/Max.
   - `molten_shell`: Category "Active", Color "Red", Tags: `["spell", "guard", "fire", "duration"]`, WeaponRestrictions: `[]` (Mọi phôi). Base: Cung cấp khiên hấp thụ 75% sát thương nhận vào (tối đa bằng 20% tổng Armour), tồn tại 4.0s, hồi chiêu 4.0s.
   - `raise_zombie`: Category "Active", Color "Blue", Tags: `["spell", "minion"]`, WeaponRestrictions: `["wand", "sceptre", "staff"]`. Base: Giam giữ 25% Mana tối đa, triệu hồi vĩnh viễn 3 Zombies cận chiến (mỗi Zombie có 350 Base Life, đòn chém gây 100% Phys Damage). Tự động hồi sinh sau 3.0s nếu bị hạ gục.
   - `summon_raging_spirit`: Category "Active", Color "Blue", Tags: `["spell", "minion", "fire", "duration"]`, WeaponRestrictions: `["wand", "sceptre", "staff"]`. Base: Triệu hồi các linh hồn lửa bay lượn tìm quái tinh anh/Boss, chuyển 100% Phys sang Fire, tồn tại 5.0s.

2. Bổ sung các **Support Gems** sau vào `data/gems.json`:
   - `cast_when_damage_taken`: Category "Support", Color "Red", RequiredTags: `["spell", "guard"]`. Modifiers: Tự động kích hoạt kỹ năng được liên kết khi nhận đủ 500 sát thương. Mana Multiplier: 100%.
   - `brutality`: Category "Support", Color "Red", RequiredTags: `["physical", "attack", "spell"]`. Modifiers: `morePhysicalDamagePercent: 40.0`, `cannotDealElementalOrChaos: true`. Mana Multiplier: 140%.
   - `chance_to_bleed`: Category "Support", Color "Red", RequiredTags: `["physical", "attack"]`. Modifiers: `extraFlatPhysicalDamage: 12`, `bleedChancePercent: 25.0`. Mana Multiplier: 120%.
   - `poison_support`: Category "Support", Color "Green", RequiredTags: `["chaos", "physical", "attack", "spell"]`. Modifiers: `moreChaosDamagePercent: 25.0`, `poisonChancePercent: 40.0`. Mana Multiplier: 130%.
   - `nightblade_support`: Category "Support", Color "Green", RequiredTags: `["attack", "melee"]`. Modifiers: `criticalStrikeMultiplier: 45.0`, `grantsElusiveOnCrit: true`. WeaponRestrictions: `["claw", "dagger"]`. Mana Multiplier: 130%.
   - `archmage_support`: Category "Support", Color "Blue", RequiredTags: `["spell", "lightning"]`. Modifiers: Tăng Mana Cost lên 5% tổng Unreserved Mana, thêm Flat Lightning Damage tương đương 75% lượng Mana tiêu hao. Mana Multiplier: 150%.
   - `pierce_support`: Category "Support", Color "Green", RequiredTags: `["projectile"]`. Modifiers: `extraPiercingTargets: 3`, `moreProjectileDamagePercent: 15.0`. Mana Multiplier: 130%.
   - `increased_critical_strikes`: Category "Support", Color "Blue", RequiredTags: `["attack", "spell"]`. Modifiers: `baseCritChanceBonus: 2.0`, `increasedCritChancePercent: 60.0`. Mana Multiplier: 120%.
   - `elemental_focus`: Category "Support", Color "Blue", RequiredTags: `["fire", "cold", "lightning", "elemental"]`. Modifiers: `moreElementalDamagePercent: 35.0`, `cannotInflictAilments: true`. Mana Multiplier: 130%.
   - `minion_damage`: Category "Support", Color "Blue", RequiredTags: `["minion"]`. Modifiers: `moreMinionDamagePercent: 35.0`. Mana Multiplier: 130%.
   - `feeding_frenzy`: Category "Support", Color "Blue", RequiredTags: `["minion"]`. Modifiers: `minionsAreAggressive: true`, `moreMinionMovementSpeedPercent: 25.0`, `moreMinionAttackSpeedPercent: 15.0`. Mana Multiplier: 120%.
   - `meat_shield`: Category "Support", Color "Blue", RequiredTags: `["minion"]`. Modifiers: `minionsTauntOnHit: true`, `lessMinionDamageTakenPercent: 20.0`. Mana Multiplier: 120%.

### Tiêu chuẩn nghiệm thu:
- Tập tin `data/gems.json` giữ chuẩn JSON RFC 8259 hợp lệ.
- Chạy `dotnet test` hoặc `DatabaseLoader.LoadAll()` nạp thành công toàn bộ danh sách gem mà không ném ngoại lệ.
- Lệnh kiểm tra: `dotnet build` đạt 0 Error(s), 0 Warning(s).
```

---

### PROMPT 2: TÍCH HỢP KEYSTONES & CỤM NỘI TẠI CHUYÊN BIỆT CHO 7 ARCHETYPES

```markdown
### Mục tiêu:
Cập nhật `data/passive_tree.json` để bổ sung các Keystones thay đổi lối chơi và các Cụm nội tại (Clusters & Notables) cần thiết để hoàn thiện 7 Build Archetypes.

### Ngữ cảnh & Tập tin tác động:
- Target File: `data/passive_tree.json`
- Logic kiểm tra: `src/Core/State/PassiveTreeState.cs` và `src/Core/Models/PassiveTreeDefinition.cs`

### Nhiệm vụ chi tiết:
1. Xác thực và bổ sung các **Keystones** mang tính đột biến:
   - `ks_resolute_technique` (Duelist/Marauder zone): "Your hits can't be Evaded, Never deal Critical Strikes".
   - `ks_mind_over_matter` (Templar/Witch zone): "40% of Damage is taken from Mana before Life".
   - `ks_perfect_agony` (Shadow zone): "Modifiers to Critical Strike Multiplier also apply to Damage Multiplier for Ailments at 50% of their value".
   - `ks_iron_reflexes` (Duelist/Ranger zone): "Converts all Evasion Rating to Armour. Dexterity provides no bonus to Evasion Rating".
   - `ks_eldritch_battery` (Witch zone): "Energy Shield protects Mana instead of Life".
   - `ks_minion_instability` (Witch zone): "Minions explode for 33% of their Maximum Life as Fire Damage when reaching Low Life (< 35%)".

2. Bổ sung các **Cụm Notables & Minor Nodes** mới:
   - **Cụm Vuốt & Hồi Máu (Claw & Poison Cluster - Shadow):**
     * Notable `Noxious Strike`: +25% Poison Duration, +30% Chance to Poison on Hit.
     * Notable `Claw Mastery`: +25 Life gained for each Enemy hit by Attacks, 20% increased Attack Speed with Claws.
   - **Cụm Hào Quang & Kháng Lửa (Fire Purity Cluster - Marauder/Templar):**
     * Notable `Sanctuary of Flame`: +2% to Maximum Fire Resistance, +20% Fire Resistance.
     * Notable `Heart of Flame`: +25% Fire Damage, 15% increased Burning Damage, 1.5% Life Regeneration per second.
   - **Cụm Năng Lượng Thần Chú (Mana Reservoir Cluster - Templar/Witch):**
     * Notable `Deep Thoughts`: +30% Maximum Mana, +40% Mana Regeneration Rate.
     * Notable `Arcane Will`: +50 Flat Maximum Mana, Regenerate 2% of Maximum Mana per second.
   - **Cụm Cung & Bắn Xuyên (Bow Pierce & Mastery - Ranger):**
     * Notable `Piercing Shots`: Projectiles Pierce 2 additional Targets, 25% increased Projectile Damage.
     * Notable `Eagle Eye`: +30% Critical Strike Multiplier with Bows, +20% Accuracy Rating.
   - **Cụm Quân Đoàn Bất Tử (Minion Army Cluster - Witch/Templar):**
     * Notable `Lord of the Dead`: +1 to Maximum number of Raised Zombies, 20% increased Minion Damage, 15% increased Minion Maximum Life.
     * Notable `Death Attunement`: +1 to Maximum number of Spectres, +1 to Maximum number of Raised Zombies.

3. Thiết lập kết nối hai chiều trong mảng `"connections"` của `data/passive_tree.json` đảm bảo thuật toán BFS shortest path từ các Node xuất phát (`start_marauder`, `start_witch`, `start_ranger`) tiếp cận mượt mà đến các cụm mới.

### Tiêu chuẩn nghiệm thu:
- Tập tin `data/passive_tree.json` được định dạng hợp lệ.
- Kiểm tra toàn bộ node mới có tọa độ (`x`, `y`) hiển thị không đè lên các node cũ trong PassiveTreeView.
- Chạy `dotnet test` hoặc `PassiveTreeSystemTests.RunAll()` vượt qua 100% các bài kiểm tra định tuyến.
```

---

### PROMPT 3: CẬP NHẬT CORE COMBAT ENGINE, CƠ CHẾ CWDT, LGoH & MINIONS

```markdown
### Mục tiêu:
Nâng cấp logic mô phỏng chiến đấu trong `src/Core/` để hỗ trợ cơ chế tự động kích hoạt (Cast when Damage Taken), hồi máu tức thì trên đòn đánh (Life Gain on Hit), sát thương tự thiêu đốt (Righteous Fire), phân chia sát thương vào Mana (Mind Over Matter), và thực thể Minion với cơ chế Meat Shield.

### Ngữ cảnh & Tập tin tác động:
- `src/Core/Simulation/CombatEngine.cs`
- `src/Core/Services/CombatAttackService.cs`
- `src/Core/Services/DamageCalculator.cs`
- `src/Core/State/PlayerState.cs`
- `src/Core/Models/DefensiveStats.cs`
- Target Model Mới: `src/Core/Models/MinionInstance.cs`

### Nhiệm vụ chi tiết:
1. **Cơ chế Trigger Khay Slot 3 (Cast when Damage Taken):**
   - Trong `CombatEngine.cs`, theo dõi bộ tích lũy sát thương `_accumulatedDamageTaken`.
   - Khi nhận đòn, nếu tổng sát thương tích lũy vượt qua ngưỡng của Support Gem CWDT (mặc định 500 dmg):
     - Kích hoạt kỹ năng được gán ở `Slot 3 (Trigger)` (ví dụ: `molten_shell` hoặc `steelskin`).
     - Áp dụng trạng thái khiên hấp thụ tạm thời vào `PlayerState`.
     - Reset bộ tích lũy sát thương về 0.

2. **Cơ chế Hồi Phục Tức Thì — Life Gain on Hit (LGoH):**
   - Trong `CombatAttackService.cs`, sau khi tính toán đòn đánh trúng mục tiêu (`hits.Count > 0`):
     - Đọc chỉ số `LifeGainOnHit` từ vũ khí trang bị (đặc biệt là phôi Claw) và từ bảng nội tại.
     - Áp dụng hồi máu trực tiếp: `PlayerState.Heal(hits.Count * lifeGainOnHit)`.
     - LGoH không bị giới hạn bởi trần Max Leech Rate/giây và hồi ngay lập tức ở cùng tick đánh trúng.

3. **Cơ chế Sát Thương Hào Quang — Righteous Fire Loop:**
   - Trong vòng lặp 30Hz của `CombatEngine.cs`:
     - Nếu `Righteous Fire` đang active:
       - Tính lượng sát thương hỏa tự thiêu đốt: `SelfBurn = PlayerState.MaxLife * 0.90f * (1.0f - FireResistance) * delta`.
       - Trừ máu người chơi qua `PlayerState.ApplyDegeneration(SelfBurn)`.
       - Quét tất cả quái vật nằm trong bán kính 60px và áp dụng Fire DoT: `MonsterBurn = PlayerState.MaxLife * 0.35f * FireDamageMultipliers * delta`.

4. **Cơ chế Resolute Technique & Mind Over Matter:**
   - Đảm bảo trong `DamageCalculator.cs`: Nếu nhân vật có `HasResoluteTechnique == true`:
     - Tỉ lệ Crit ép về 0.0% vĩnh viễn (`isCrit = false`).
     - Tỉ lệ Hit ép lên 100% vĩnh viễn (bỏ qua Accuracy và Evasion của mục tiêu).
   - Kiểm tra trong `PlayerState.ApplyDamage()`: Khi `MindOverMatterPercent > 0`:
     - Chia 40% sát thương trừ vào `CurrentMana`, phần còn lại trừ vào `CurrentLife` (nếu hết Mana, lượng dư dồn lại vào Máu).

5. **Cơ chế Thực Thể Minion & Bức Tường Thịt (Minion Meat Shield Pipeline):**
   - Tạo thực thể `MinionInstance.cs` kế thừa `ICombatEntity` (có Health, Armor, DPS riêng).
   - Khi khảm gem `raise_zombie`:
     - Giam giữ 25% Mana của `PlayerState` (`ReservedMana += MaxMana * 0.25f`).
     - Tự động sinh ra 3 Zombies đứng ở phía trước Exile trong hành lang 2D.
   - Cơ chế Meat Shield: Quái vật bắn đạn hoặc đánh cận chiến sẽ ưu tiên nhắm vào Zombie gần nhất (Body Blocking).
   - Khi Zombie chết: Kích hoạt bộ đếm thời gian hồi sinh 3.0s rồi tự động tái sinh lại.
   - Nếu có Keystone `Minion Instability`: Khi Zombie tụt máu < 35%, tự kích nổ gây 33% Max Life thành Fire AoE.

### Tiêu chuẩn nghiệm thu:
- Tuyệt đối KHÔNG sử dụng `Godot.Node` hay `GD.Print` trong `src/Core/`.
- Mã nguồn biên dịch sạch với `dotnet build` (0 cảnh báo, 0 lỗi).
```

---

### PROMPT 4: ĐỒNG BỘ TRANG BỊ, AFFIXES & KHẮC TINH MAP THEO META

```markdown
### Mục tiêu:
Cập nhật `data/affixes.json` và `data/maps.json` để bổ sung các Affixes chuyên dụng phục vụ 7 Archetypes và cấu hình các Map Modifiers có tính khắc chế tương hỗ rõ rệt, thúc đẩy người chơi sử dụng Currency Orbs để reroll map.

### Ngữ cảnh & Tập tin tác động:
- Target Files: `data/affixes.json`, `data/maps.json`
- Schema & Logic: `src/Core/Models/AffixDefinition.cs`, `src/Core/Models/MapDefinition.cs`

### Nhiệm vụ chi tiết:
1. **Mở rộng Affixes trong `data/affixes.json`:**
   - Bổ sung Prefix/Suffix Tiers cho:
     * `weapon_dot_multiplier`: +15% đến +30% Damage over Time Multiplier (dành cho Bleed/Poison/Ignite).
     * `weapon_life_gain_on_hit`: +15 đến +30 Life gained for each Enemy hit by Attacks.
     * `gear_max_fire_res`: +1% đến +2% to Maximum Fire Resistance (dành cho Righteous Fire build).
     * `gear_flat_mana`: +40 đến +120 Maximum Mana.
     * `gear_percent_mana`: 15% đến 25% Increased Maximum Mana (dành cho Archmage/MoM build).
     * `gear_spell_suppression`: +10% đến +22% Chance to Suppress Spell Damage (dành cho Deadeye build).
     * `weapon_minion_damage`: +15% đến +30% increased Minion Damage (dành cho phôi Convoking Wand).
     * `gear_minion_life`: +15% đến +25% increased Minion Maximum Life (dành cho Bone Helmet / Shield).

2. **Cập nhật Map Modifiers trong `data/maps.json`:**
   - Thêm các thuộc tính biến dị (Map Mods) mang tính phân hóa build:
     * `map_reflect_physical`: "Monsters reflect 15% of Physical Damage" (Khắc tinh cứng của Bleed Slammer).
     * `map_reflect_elemental`: "Monsters reflect 15% of Elemental Damage" (Khắc tinh cứng của Ignite Burner, RF, Archmage).
     * `map_no_life_recovery`: "Players have 60% reduced Life and Mana Recovery Rate" (Nguy hiểm cao cho Juggernaut và RF).
     * `map_cannot_leech`: "Cannot Leech Life or Mana from Monsters" (Khắc chế Poison Claw & Archmage).
     * `map_cannot_be_evaded`: "Monsters' Attacks cannot be Evaded" (Vô hiệu hóa Evasion của Deadeye).
     * `map_extra_chaos_res`: "Monsters have +40% to Chaos Resistance" (Giảm sát thương Poison Assassin).
     * `map_monsters_chain_projectiles`: "Monsters' Projectiles Chain 2 additional times" (Khắc tinh cứng của Dread Necromancer vì đạn nảy từ đệ tử vào người Exile).
     * `map_desecrated_ground`: "Monsters create Desecrated Ground on Death dealing Chaos Damage over time" (Làm tan rã bầy Zombie).

3. Đảm bảo mỗi modifier có đầy đủ `stat`, `value`, `rarityWeight`, và phân loại tier tương ứng với Item Level của Bản đồ.

### Tiêu chuẩn nghiệm thu:
- Cả hai tập tin JSON đều tuân thủ đúng định dạng hiện tại của dự án.
- `DatabaseLoader.cs` nạp toàn bộ affixes và maps mới mà không có lỗi thiếu trường (missing fields).
- Lệnh `dotnet build` thành công rực rỡ.
```

---

### PROMPT 5: XÂY DỰNG TEST SUITE KIỂM THỬ TOÀN DIỆN 7 ARCHETYPES

```markdown
### Mục tiêu:
Xây dựng một bộ Unit Test tự động `src/Core/Tests/ArchetypeSimulationTests.cs` (viết bằng Pure C#, theo chuẩn static `RunAll()` hiện có của dự án) để kiểm chứng tính toàn vẹn toán học và cơ chế vận hành của toàn bộ 7 Build Archetypes.

### Ngữ cảnh & Tập tin tác động:
- Target File mới: `src/Core/Tests/ArchetypeSimulationTests.cs`
- Kết nối runner: Gọi `ArchetypeSimulationTests.RunAll()` trong `MainLayoutTests.cs` hoặc test bootstrap tương đương.

### Nhiệm vụ chi tiết:
Viết các bài kiểm tra tự động xác nhận 7 ca kiểm thử sau:
1. **Test_Juggernaut_ResoluteTechnique_And_Bleed():**
   - Nhân vật kích hoạt Resolute Technique: Khởi chạy 100 đòn đánh thử nghiệm, kiểm tra `isCrit` luôn luôn là `false` và `isHit` luôn là `true`.
   - Sát thương Bleed tính đúng theo Base Hit vũ khí 2 tay (đạt chuẩn sát thương vật lý qua thời gian).
2. **Test_Elementalist_IgniteProliferation():**
   - Fireball bắn trúng Dummy Monster 1 gây hiệu ứng Ignite.
   - Kiểm tra Dummy Monster 2 đứng cách đó 25px lập tức bị lây nhiễm trạng thái Ignite mà không cần nhận đòn đánh trực tiếp.
3. **Test_Deadeye_CritMultiplier_And_SpellSuppression():**
   - Đòn Split Arrow trang bị cung Crit đạt hệ số sát thương chí mạng chuẩn ($150\% \times \text{CritMulti}$).
   - Khi nhận đòn sát thương phép 1000 damage, nhân vật có 100% Spell Suppression chỉ nhận 500 damage.
4. **Test_Assassin_Claw_LifeGainOnHit_And_PoisonStacking():**
   - Tạo quái vật với 50,000 HP. Đấu sĩ cầm Claw đánh 10 đòn trúng quái.
   - Kiểm tra quái vật tích lũy đúng 10 stacks Poison (sát thương DoT tăng lũy tiến).
   - Kiểm tra nhân vật hồi phục ngay lập tức $10 \times \text{LGoH}$ vào thanh máu.
5. **Test_RighteousFire_Degeneration_Balance():**
   - Thiết lập nhân vật có 90% Max Fire Res và 10% Life Regen.
   - Chạy 30 ticks mô phỏng: Kiểm tra máu người chơi duy trì ổn định không bị tụt chết, đồng thời quái vật xung quanh nhận sát thương Fire DoT liên tục.
6. **Test_Archmage_MindOverMatter_DamageAbsorption():**
   - Nhân vật có 2000 Life, 2000 Mana, và Mind Over Matter (40%).
   - Tác động 1000 damage: Kiểm tra 400 damage trừ vào Mana và 600 damage trừ vào Life.
7. **Test_Necromancer_Minion_MeatShield_And_Damage():**
   - Khảm `Raise Zombie`: Kiểm tra Mana bị giam giữ 25% và 3 Zombie instances xuất hiện.
   - Quái bắn đạn trúng Zombie: Máu Zombie giảm, máu Exile giữ nguyên (Meat Shield).
   - Zombie tấn công quái: Gây sát thương vật lý với hệ số More từ `Minion Damage Support`.
   - Khi Zombie tụt máu < 35%: `Minion Instability` nổ gây 33% Max Life thành Fire AoE.

### Tiêu chuẩn nghiệm thu:
- Không sử dụng thư viện bên ngoài; chỉ dùng các assert nội bộ (`AssertEqual`, `AssertFloat`, `AssertTrue`).
- Chạy lệnh kiểm tra không lỗi:
  ```powershell
  dotnet build
  & "E:\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe" --headless --scene "res://scenes/Main.tscn" --quit-after 50
  ```
- Tất cả bài test thông báo `PASSED successfully`.
```

---

## 4. MA TRẬN ĐO LƯỜNG THÀNH CÔNG (SUCCESS METRICS & ACCEPTANCE)

| Tiêu Chí Đo Lường | Mục Tiêu Kỹ Thuật | Phương Pháp Xác Minh |
| :--- | :--- | :--- |
| **Tính Toàn Vẹn CSDL** | 100% Gems, Keystones, Affixes và Maps mới được nạp sạch | `DatabaseLoader.LoadAll()` không ném ngoại lệ |
| **Độ Chuẩn Toán Học PoE** | Tách bạch 100% giữa Increased (cộng) và More (nhân) | Kiểm tra qua `DamageCalculatorTests` & `ArchetypeSimulationTests` |
| **Không Lag Giật Hot-path** | Zero GC Allocation trong vòng lặp 30Hz của Trigger/LGoH | Benchmark qua vòng lặp 3,000 ticks headless |
| **Hiệu Quả Bản Địa Hóa** | Toàn bộ định danh kỹ năng, tooltip tiếng Anh chuẩn | Khớp nối từ khóa qua `translations.csv` |
| **Độ Sạch Của Bản Build** | 0 Warning, 0 Error, không có mã debug thừa | `dotnet build -c Release` thành công tuyệt đối |
