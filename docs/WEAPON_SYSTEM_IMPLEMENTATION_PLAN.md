# WEAPON_SYSTEM_IMPLEMENTATION_PLAN.md — Kế Hoạch Triển Khai Kỹ Thuật Hệ Thống Trang Bị Vũ Khí

Tài liệu này chi tiết hóa lộ trình kỹ thuật và phân rã cấu trúc công việc (Work Breakdown Structure - WBS) để hiện thực hóa **Hệ Thống Trang Bị Vũ Khí (Weapon Equipment System)** theo đặc tả tại [09_weapon_equipment_system.md](09_weapon_equipment_system.md).

---

## 1. NGUYÊN TẮC KIẾN TRÚC & PHÂN TÁCH TRÁCH NHIỆM (SEPARATION OF CONCERNS)

Tuân thủ nghiêm ngặt quy định tại [AGENTS.md](file:///e:/Projects/Antigravity/IdleExile/AGENTS.md):

* **`src/Core/` (Pure C# — 100% No Godot Dependency):**
  * Lưu trữ cấu trúc phôi vũ khí (`WeaponBaseDefinition`), thực thể vũ khí (`WeaponItemInstance`), và trạng thái 2 tay trang bị (`EquippedGearState`).
  * Thực thi công thức toán học Local Modifiers (`RecalculateLocalStats()`) và tính toán pDPS / eDPS / Total DPS.
  * Tích hợp vào `DamageCalculator.cs` và `CombatEngine.cs` để cung cấp sát thương cơ sở cho đòn đánh và hệ số cho phép thuật.
  * Sinh vũ khí ngẫu nhiên và roll dòng affixes trong `ItemGenerator.cs`.
  * Có thể kiểm thử 100% bằng Unit Tests độc lập hoặc chạy Headless không cần mở cửa sổ đồ họa.

* **`src/UI/` (Godot 4 Presentation Layer):**
  * Hiển thị 2 ô trang bị Main Hand / Off Hand trên cửa sổ nhân vật mini (`EquipmentSlotsView.cs`).
  * Hiển thị thẻ thông tin chi tiết vũ khí kèm so sánh tăng/giảm DPS tức thì (`WeaponTooltipView.cs`).
  * Xử lý visual feedback: khóa mờ ô Off Hand khi cầm vũ khí 2 tay, hiệu ứng viền màu theo phẩm cấp (Normal, Magic, Rare, Unique).

---

## 2. PHÂN RÃ CÔNG VIỆC THEO GIAI ĐOẠN (PHASED WORK BREAKDOWN STRUCTURE)

```
┌────────────────────────────────────────────────────────────────────────┐
│ Phase 1: Data Architecture & JSON Schemas                              │
│   ├── [x] Task 1.1: Tạo data/weapon_bases.json (15+ Phôi vũ khí & Phụ) │
│   ├── [x] Task 1.2: Bổ sung Local & Caster Affixes vào data/affixes.json│
│   └── [x] Task 1.3: Cập nhật DatabaseLoader.cs để nạp Weapon Bases     │
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│ Phase 2: Core Domain Models & Local Stat Cache (Pure C#)               │
│   ├── [x] Task 2.1: Model WeaponBaseDefinition & WeaponEnums           │
│   ├── [x] Task 2.2: Model WeaponItemInstance & Hàm RecalculateLocalStats│
│   └── [x] Task 2.3: Model EquippedGearState & Quy tắc Dual-Wield / 2H  │
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│ Phase 3: Item Generation & Affix Roller Integration (Pure C#)          │
│   ├── [ ] Task 3.1: Cơ chế Roll Phôi theo ilvl & Map Tier              │
│   ├── [ ] Task 3.2: Bộ lọc Affix Tags cho Vũ Khí (Attack vs Caster)    │
│   └── [ ] Task 3.3: Kết nối Quái Chết -> Rơi Vũ Khí (Loot Drop Hook)   │
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│ Phase 4: Combat Engine & Damage Pipeline Integration                   │
│   ├── [x] Task 4.1: Tích hợp Base Damage Vũ Khí vào DamageCalculator   │
│   ├── [x] Task 4.2: Cơ chế Alternate Attack & Buff Dual Wielding       │
│   └── [x] Task 4.3: Tích hợp Global Caster Mods vào Spell Gems         │
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│ Phase 5: UI Presentation & Smart Tooltip (Godot 4)                     │
│   ├── [ ] Task 5.1: Giao diện 2 Ô Trang Bị Main/Off Hand (64x64 px)    │
│   ├── [ ] Task 5.2: Thẻ Tooltip Chi Tiết (Phân giải pDPS/eDPS/Tiers)   │
│   └── [ ] Task 5.3: Hệ thống So Sánh Sát Thương Tức Thì (Stat Delta)   │
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│ Phase 6: Unit Testing, Headless CI & Tối Ưu Hiệu Năng                  │
│   ├── [ ] Task 6.1: Unit Test Suite cho Công thức Toán Local Modifiers │
│   ├── [ ] Task 6.2: Unit Test cho Dual Wield & Ràng buộc Two-Handed    │
│   ├── [ ] Task 6.3: Kiểm tra Zero-Allocation trong Hot-path 30Hz       │
│   └── [ ] Task 6.4: Chạy Headless Verification trên Godot Console      │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 3. CHI TIẾT CÁC TÁC VỤ KỸ THUẬT (TASK SPECIFICATIONS)

### Giai Đoạn 1: Cấu Trúc Dữ Liệu & Nạp JSON (Data Layer)

#### Task 1.1: Tạo `data/weapon_bases.json`
* **Mục tiêu:** Định nghĩa danh sách phôi vũ khí chuẩn trải dài từ cấp độ 1 đến 85+.
* **Các phôi bắt buộc:**
  * *1H Melee:* `rusted_sword` (Early), `gladius` (Mid), `jewelled_foil` (Endgame - Crit Multi implicit).
  * *1H Axe:* `boarding_axe` (Early), `siege_axe` (Endgame - High Base Phys).
  * *1H Mace:* `club` (Early), `behemoth_mace` (Endgame - Stun implicit).
  * *Claw:* `nailed_fist` (Early), `imperial_claw` (Endgame - Life gain on hit implicit).
  * *Dagger:* `glass_shank` (Early), `platinum_kris` (Endgame - Global Crit Chance implicit).
  * *Caster:* `driftwood_wand` (Early), `imbued_wand` (Endgame - Spell Dmg implicit), `opal_sceptre` (Endgame - Elemental Dmg implicit).
  * *2H Melee:* `corroded_blade` (Early), `vaal_axe` (Endgame - Max Base Phys), `piledriver` (Endgame - Stun duration).
  * *Bow & Quiver:* `crude_bow` (Early), `imperial_bow` (Endgame), `broadhead_quiver`, `spike_point_quiver`.
  * *Shields:* `tower_shield` (Str), `buckler` (Dex), `spirit_shield` (Int).

#### Task 1.2: Bổ sung Affixes chuyên dụng cho Vũ Khí vào `data/affixes.json`
* Bổ sung các Prefix:
  * `weapon_percent_phys_t1` đến `t5` (`tags: ["damage", "physical", "attack", "weapon", "local"]`).
  * `weapon_flat_phys_t1` đến `t5` (Flat min/max physical local).
  * `weapon_hybrid_phys_acc_t1` đến `t3` (% Phys + Flat Accuracy).
  * `weapon_flat_fire/cold/lightning_t1` đến `t3` (Flat elemental damage).
  * `weapon_all_spell_gems_t1` (`+1 to Level of all Spell Skill Gems`).
  * `weapon_percent_spell_dmg_t1` (`tags: ["damage", "spell", "caster", "global"]`).
* Bổ sung các Suffix:
  * `weapon_attack_speed_t1` đến `t5` (`tags: ["speed", "attack", "weapon", "local"]`).
  * `weapon_local_crit_chance_t1` đến `t3` (`tags: ["critical", "attack", "weapon", "local"]`).
  * `weapon_global_crit_multi_t1` đến `t3` (`tags: ["critical", "global"]`).
  * `weapon_cast_speed_t1` đến `t3` (`tags: ["speed", "spell", "caster", "global"]`).

#### Task 1.3: Cập nhật `DatabaseLoader.cs`
* Bổ sung `IReadOnlyDictionary<string, WeaponBaseDefinition> WeaponBases` vào Database Loader.
* Viết cơ chế tự động validate: Tất cả các `implicitAffixId` nếu có phải tồn tại trong từ điển Affixes.

---

### Giai Đoạn 2: Mô Hình C# Thuần & Cache Sát Thương (Core Models)

#### Task 2.1: `WeaponBaseDefinition.cs` & Enums
```csharp
namespace IdleExile.Core.Models;

public enum WeaponClass
{
    OneHandedSword, OneHandedAxe, OneHandedMace, Claw, Dagger, Wand, Sceptre,
    TwoHandedSword, TwoHandedAxe, TwoHandedMace, Bow, Staff,
    Shield, Quiver
}

public enum HandSlotRequirement
{
    OneHanded,
    TwoHanded,
    OffHandOnly
}

public sealed record WeaponBaseDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public WeaponClass Class { get; init; }
    public HandSlotRequirement HandRequirement { get; init; }
    public float BaseMinPhys { get; init; }
    public float BaseMaxPhys { get; init; }
    public float BaseAps { get; init; }
    public float BaseCritChance { get; init; }
    public int ReqLevel { get; init; }
    public int ReqStr { get; init; }
    public int ReqDex { get; init; }
    public int ReqInt { get; init; }
    public string? ImplicitAffixId { get; init; }
    public float ImplicitValue { get; init; }
}
```

#### Task 2.2: `WeaponItemInstance.cs` & `RecalculateLocalStats()`
* Lưu danh sách Prefix / Suffix đã roll (`RolledAffix`).
* Triển khai phương thức tính toán cache:
  ```csharp
  public void RecalculateLocalStats()
  {
      // 1. Cộng dồn Flat Phys / Flat Ele
      // 2. Nhân hệ số % Inc Phys / % Inc Ele Local
      // 3. Nhân hệ số % Inc Attack Speed Local
      // 4. Nhân hệ số % Inc Crit Chance Local
      // 5. Cập nhật ComputedMinDamage, ComputedMaxDamage, ComputedAps, ComputedCritChance, ComputedDps
  }
  ```
* Đảm bảo: Chỉ tính lại khi đồ được nhặt, nâng cấp (crafting), hoặc đổi ngọc; tuyệt đối không tính lại trong mỗi tick 30Hz của combat loop.

#### Task 2.3: `EquippedGearState.cs` & Xử Lý Hai Tay
* Quản lý 2 slot: `WeaponItemInstance? MainHand` và `WeaponItemInstance? OffHand`.
* Thuật toán kiểm tra hợp lệ:
  * Nếu `MainHand` là `TwoHanded` và không phải `Bow` -> `OffHand` bắt buộc là `null`.
  * Nếu `MainHand` là `Bow` -> `OffHand` chỉ chấp nhận `Quiver` hoặc `null`.
  * Nếu cả 2 tay đều cầm vũ khí 1-Handed (Dual-Wielding) -> Kích hoạt cờ `IsDualWielding = true`.
* Cung cấp các getter tiện ích:
  * `float DualWieldMoreAttackSpeed => IsDualWielding ? 1.10f : 1.0f;`
  * `float DualWieldBlockBonus => IsDualWielding ? 15.0f : 0f;`

---

### Giai Đoạn 3: Sinh Đồ Rơi & Tích Hợp Loot (Item Generator)

#### Task 3.1: Logic Sinh Phôi Vũ Khí theo Cấp Độ
* Trong `src/Core/Services/ItemGenerator.cs`:
  * Nhận đầu vào `monsterLevel` và `itemLevel`.
  * Lọc danh sách phôi từ `DatabaseLoader.WeaponBases` thỏa mãn `ReqLevel <= monsterLevel`.
  * Sử dụng thuật toán chọn ngẫu nhiên có trọng số (Weighted Random) ưu tiên phôi cấp cao tương ứng.

#### Task 3.2: Bộ Lọc Thẻ Affix Phù Hợp Cho Vũ Khí
* Đảm bảo tính hợp lý (PoE Rule Enforcement):
  * Vũ khí Cận Chiến (Melee) không thể roll ra dòng Spell Caster độc quyền.
  * Đũa phép (Wand) không thể roll ra dòng cận chiến chém/chặt.
  * Cung tên (Bow) chỉ roll các dòng tầm xa/projectile.
* Giới hạn số lượng dòng:
  * Normal: 0 dòng.
  * Magic: 1 Prefix + 1 Suffix.
  * Rare: 2 đến 3 Prefixes + 2 đến 3 Suffixes.

#### Task 3.3: Móc Nối Quái Rơi Đồ
* Trong `src/Core/Simulation/CombatEngine.cs`, khi quái chết:
  * Dựa trên Drop Rate (ví dụ 15% quái Magic/Rare rơi trang bị), sinh ra một thực thể `WeaponItemInstance` bọc trong `LootDrop.Equipment`.

---

### Giai Đoạn 4: Tích Hợp Vòng Lặp Chiến Đấu (Combat Engine Integration)

#### Task 4.1: Sát Thương Đòn Đánh từ Vũ Khí Trong `DamageCalculator.cs`
* Cập nhật công thức tính Base Damage:
  * Trước đây: Dùng thông số cố định của archetype/gem.
  * Bây giờ: Nếu kỹ năng có tag `Attack`, Base Damage lấy trực tiếp từ `MainHand.ComputedMinDamage` đến `MainHand.ComputedMaxDamage`. Nếu không có vũ khí, lấy Unarmed Base (2–4 Physical).

#### Task 4.2: Cơ Chế Vung Đòn Luân Phiên (Alternate Weapon Swings)
* Khi `IsDualWielding == true`:
  * `PlayerState` lưu cờ nhịp đánh: `bool _swingWithMainHand = true;`
  * Ở mỗi đòn tấn công, lấy thông số của tay hiện tại để tính toán, sau đó đảo cờ: `_swingWithMainHand = !_swingWithMainHand;`
  * Đảm bảo tính linh hoạt khi người chơi cầm 1 cây kiếm nhanh và 1 cây chùy chậm.

#### Task 4.3: Truyền Thuộc Tính Global Vào `DamageModifierContext.cs`
* Vũ khí có các dòng Global (ví dụ: `+25% Global Crit Multiplier` của phôi kiếm, `+1 to Level of all Spell Skill Gems` của Wand):
  * Tự động cộng dồn vào `DamageModifierContext` khi kích hoạt kỹ năng phép.

---

### Giai Đoạn 5: Giao Diện Người Dùng & Tooltip (Godot 4 UI)

#### Task 5.1: `EquipmentSlotsView.cs` (Kích Thước Mini-Window)
* Tọa lạc tại tab "Character / Inventory" (kích thước tối đa $440 \times 320$ px cho khu vực đồ).
* 2 nút bấm/ô vuông kích thước $64 \times 64$ px:
  * Nút bên trái: `Slot_MainHand`
  * Nút bên phải: `Slot_OffHand`
* Vẽ icon đồ họa tối giản đại diện cho từng loại vũ khí (Kiếm, Rìu, Cung, Gậy, Khiên).
* Vẽ viền phát sáng theo màu phẩm cấp (`ColorHex`).

#### Task 5.2: `WeaponTooltipView.cs` (Thẻ Thông Tin Toàn Diện)
* Hiển thị pop-over thông minh khi di chuột hoặc chạm giữ trên điện thoại/mini-window.
* Chia rõ 5 phân khu:
  1. Header: Tên món đồ + Tên phôi (Màu vàng Rare / Xanh Magic).
  2. Stat Block: Damage Range, Attack Speed, Critical Strike Chance, và tổng pDPS / eDPS.
  3. Requirements: Cấp độ, Điểm Str/Dex/Int cần thiết.
  4. Implicit Section: Ngăn cách bằng nét gạch đứt mờ.
  5. Affixes Section: Liệt kê rõ `[P1]`, `[P2]`, `[S1]`, `[S2]` kèm giá trị và tên gọi.

#### Task 5.3: So Sánh Nhanh Tức Thì (Quick Stat Delta)
* Khi nhặt được hoặc trỏ vào vũ khí mới trong túi đồ:
  * So sánh với vũ khí đang trang bị ở Main Hand:
    * `DPS Delta = NewWeapon.ComputedDps - CurrentWeapon.ComputedDps`
    * `APS Delta = NewWeapon.ComputedAps - CurrentWeapon.ComputedAps`
  * Hiển thị dòng tóm tắt màu xanh lá hoặc đỏ ở cuối tooltip:
    * Ví dụ: `▲ +24.5% DPS | ▲ +0.25 APS` hoặc `▼ -8.2% DPS`.

---

### Giai Đoạn 6: Kiểm Thử Toàn Diện & Tối Ưu Hóa (Testing & Verification)

#### Task 6.1: Unit Test Suite Cho Công Thức Local Modifiers
* Viết test kiểm tra:
  * Phôi có Base Phys $30-50$, roll `+100% Inc Phys` và `+10-20 Flat Phys` -> Kết quả chính xác: Min = $(30+10)*2 = 80$, Max = $(50+20)*2 = 140$.
  * Kiểm tra pDPS, eDPS và Total DPS tính toán đúng đến 2 chữ số thập phân.

#### Task 6.2: Unit Test Cho Ràng Buộc Tay Cầm & Dual Wield
* Test không cho phép gắn Khiên vào Off Hand nếu Main Hand cầm Rìu 2 tay.
* Test cho phép gắn Quiver vào Off Hand khi Main Hand cầm Cung.
* Test kích hoạt chuẩn `10% More APS` và `15% Block` khi Dual-Wielding.

#### Task 6.3: Kiểm Tra Bộ Nhớ & Garbage Collection (Hot-Path Profiling)
* Chạy mô phỏng chiến đấu 10,000 đòn đánh liên tục:
  * Đo đạc lượng RAM cấp phát (GC Allocation).
  * Mục tiêu: **0 Byte GC allocation** phát sinh từ việc truy xuất chỉ số vũ khí trong combat loop (nhờ toàn bộ đã được cache trước trong `Computed*`).

#### Task 6.4: Chạy Headless Verification
* Chạy Godot Console kiểm thử tích hợp:
  ```powershell
  & "E:\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe" --headless --scene "res://scenes/Main.tscn" --quit-after 100
  ```
* Đảm bảo: 0 Exception, 0 Warning, thoát mã 0.

---

## 4. TIÊU CHÍ NGHIỆM THU HOÀN THÀNH (ACCEPTANCE CRITERIA)

| Hạng Mục | Tiêu Chí Đạt Chuẩn (Definition of Done) |
| :--- | :--- |
| **Data Integrity** | `weapon_bases.json` tải đủ 15 phôi vũ khí; 100% Affix IDs tham chiếu hợp lệ; không ném Exception khi khởi động game. |
| **Mathematical Accuracy** | Phân biệt chính xác 100% giữa Local Modifiers (đổi thông số vũ khí) và Global Modifiers (đưa vào nhân vật); công thức pDPS / eDPS khớp số học. |
| **PoE 2 Compliance** | 0 Socket xuất hiện trên trang bị vũ khí; việc tháo/lắp vũ khí không ảnh hưởng tới ngọc kỹ năng đã liên kết. |
| **Combat Mechanics** | Cầm 2 vũ khí 1 tay nhận đúng `10% More APS` và `15% Block`; đòn đánh vung luân phiên giữa 2 tay; cầm vũ khí 2 tay khóa đúng ô phụ. |
| **Performance** | Không tạo Garbage Collection trong vòng lặp chiến đấu 30Hz; RAM duy trì ổn định dưới 150MB; CPU background < 1%. |
| **Visual Polish** | Tooltip hiển thị đầy đủ thông tin phân tầng và chỉ số so sánh tức thì (Stat Delta) phù hợp không gian 440px mà không bị tràn màn hình. |

---
*Kế hoạch này là kim chỉ nam kỹ thuật cho toàn bộ Phase tiếp theo của dự án Idle Exile.*
