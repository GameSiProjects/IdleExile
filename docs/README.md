# Idle Exile - Game Design Documentation (GDD)

Tài liệu thiết kế tổng thể cho dự án **Idle Exile** — một tựa game nhập vai nhàn rỗi (Idle/Incremental ARPG) lấy cảm hứng từ chiều sâu cơ chế của *Path of Exile*, phát triển trên engine **Godot 4**.

---

## 📑 Danh mục Tài liệu (Documentation Index)

1. **[01. Tổng quan Dự án & Triết lý Thiết kế (`01_game_overview.md`)](01_game_overview.md)**
   - High Concept, Core Pillars.
   - Vòng lặp gameplay cốt lõi (Core Loop).
   - Tiếp cận tối giản cốt truyện & Khởi đầu ngay bằng hệ thống Mapping.
   - Định dạng cửa sổ mini (Mini-Window Companion).

2. **[02. Hệ thống Bản đồ & Vực Sâu Vô Tận (`02_mapping_system.md`)](02_mapping_system.md)**
   - Kiến trúc tiến trình kép (Dual-Axis Progression): Mapping cốt lõi (80%) + The Endless Abyss (20%).
   - Hệ thống Bản đồ từ cấp độ 1 (Tier 1 đến Tier 17+) & Cơ chế Fail-safe chống kẹt (Tier 0).
   - Cơ chế Item hóa Map (Roll Mods, Rủi ro vs Lợi nhuận) & Tự động hóa Zero-APM (Map Queue 10 slots & Continuous Auto-Run).
   - Vực Sâu Vô Tận (The Endless Abyss): Leo Depth vô tận, Benchmark EHP/DPS, Vòng lặp nhiên liệu Sulphite & Nguồn rơi độc quyền Hóa thạch (Abyssal Fossils).
   - Atlas Passive Tree định hình lối cày cuốc.

3. **[03. Cơ chế Chiến đấu & Tính toán Chỉ số (`03_combat_and_stats.md`)](03_combat_and_stats.md)**
   - Công thức tính toán sát thương: *Increased/Reduced* vs *More/Less*.
   - 5 hệ nguyên tố & Hiệu ứng trạng thái (Ailments).
   - Các lớp phòng thủ (Armour curve, Evasion entropy, Energy Shield, Resistances, Block, Suppression).
   - Tick-based Combat Simulation.

4. **[04. Kỹ năng & Bảng Nội tại (`04_gems_and_passives.md`)](04_gems_and_passives.md)**
   - Hệ thống Kỹ năng độc lập (PoE 2 Decoupled Gem Sockets).
   - Khay kỹ năng tự động hóa (Skill Loadout Matrix) cho AI treo máy.
   - Cây nội tại khổng lồ (Passive Web), Notables & Keystones định hình build.
   - Masteries & Jewel Sockets.

5. **[05. Trang bị, Tiền tệ & Chế tạo (`05_items_and_crafting.md`)](05_items_and_crafting.md)**
   - Cấu trúc Affix chuẩn (Prefix / Suffix, Item Level, Mod Tiers).
   - Trang bị thuần Stats (không phụ thuộc Sockets, thay đồ tự do).
   - Hệ thống Currency Orbs (Nền kinh tế không tiền vàng) & Cơ chế Vaal/Corrupt.
   - Chế tạo định hướng tinh gọn: Hóa thạch (Streamlined Fossil Crafting kết hợp Anvil Slots từ The Endless Abyss).
   - Loot Filter thông minh tích hợp & Kho đồ tự động (Affinity Stash).

6. **[06. Kiến trúc Kỹ thuật C# & JSON trên Godot 4 (`06_godot_technical_spec.md`)](06_godot_technical_spec.md)**
   - Thiết lập Mini-Window (Kích thước `440x780`, Always-On-Top, Tiết kiệm tài nguyên).
   - Kiến trúc Data-Driven C# (.NET 8) nạp trực tiếp từ JSON Database (`System.Text.Json`).
   - Tách biệt Logic Simulation (Tick Engine 30Hz) và Rendering (Canvas/Control).
   - Thuật toán tính toán Offline Progress (Treo máy khi tắt game) theo Snapshot Record ($O(1)$) siêu tốc bằng C#.
   - Hệ thống Bản địa hóa & Đa ngôn ngữ (Godot Localization `TranslationServer`, file dịch tập trung `translations.csv` với mặc định 100% tiếng Anh).

7. **[07. Thiết Kế Giao Diện Chiến Đấu (`07_combat_ui_and_viewport.md`)](07_combat_ui_and_viewport.md)**
   - Triết lý UX: Diagnostic Clarity, Visceral Combat Feedback, Low Resource Footprint.
   - Bố cục không gian Mini-Window 440x780 và phân bổ pixel 300px Viewport.
   - 5 phân vùng: Map Threat Header, 2D Combat Arena, Vitometer (Life/ES/Mana/Flasks), Floating Combat Text & Loot Drop Beams, Macro Controls.
   - Bảng mã màu UI Tokens & Cấu trúc Scene Tree Godot 4.

8. **[08. Trải Nghiệm Người Chơi Ban Đầu (`08_first_time_user_experience.md`)](08_first_time_user_experience.md)**
   - Triết lý FTUE: Time-to-Action < 15s, "Show, Don't Tell" không dùng modal lock.
   - 3 Starter Archetypes: Juggernaut, Elementalist, Deadeye (1-Click Pick).
   - Tiến trình 10 phút đầu tiên: 3 khoảnh khắc "Aha!" (Drop "Ting!", Gem Socketing PoE 2, Map Sustain).
   - Mở khóa giao diện lũy tiến (Progressive Disclosure) & Cơ chế Fail-Safe chống kẹt tài nguyên.
   - Kế hoạch triển khai kỹ thuật chi tiết: **[FTUE_IMPLEMENTATION_PLAN.md](FTUE_IMPLEMENTATION_PLAN.md)**.

9. **[09. Hệ Thống Trang Bị Vũ Khí (`09_weapon_equipment_system.md`)](09_weapon_equipment_system.md)**
   - Cấu hình trang bị Main Hand / Off Hand, cơ chế Dual-Wield (+10% More APS, +15% Block) & Bow + Quiver.
   - Phân lớp vũ khí & Implicit đặc trưng (Foil, Axe, Mace, Claw, Dagger, Wand, Sceptre, Staff, Bow).
   - Toán học vũ khí PoE: Phân tách triệt để Local Modifiers (đổi thân vũ khí) vs Global Modifiers.
   - Công thức tính pDPS, eDPS, Total DPS & Bộ ba vật lý thần thánh (Physical Trinity).
   - 4 Archetypes vũ khí tiêu biểu, vòng lặp chế tạo tiền tệ (Currency Crafting Loops).
   - Giao diện Mini-Window 440x780, Smart Tooltip và so sánh nhanh chỉ số (Stat Delta).
   - Kế hoạch triển khai kỹ thuật chi tiết: **[WEAPON_SYSTEM_IMPLEMENTATION_PLAN.md](WEAPON_SYSTEM_IMPLEMENTATION_PLAN.md)**.

10. **[10. Thiết Kế Bộ Ba Kỹ Năng Khởi Đầu (`10_starter_skills_design.md`)](10_starter_skills_design.md)**
    - Thiết kế chi tiết 3 kỹ năng starter: Cleave (Str), Fireball (Int), Split Arrow (Dex).
    - Quy tắc toán học, vũ khí yêu cầu, hành vi targeting trong CombatEngine 30Hz.
    - Cơ chế Dual-Wield Cleave (chém cả 2 tay đồng thời), Fireball Ignite Proliferation, Split Arrow Non-Shotgunning.
    - Ma trận so sánh & cân bằng, hiệu ứng thị giác 2D Arena & âm thanh (SFX), đồng bộ dữ liệu `data/gems.json`.

11. **[11. Thiết Kế Toàn Diện Hệ Thống Trang Bị (`11_equipment_system_design.md`)](11_equipment_system_design.md)**
    - Ma trận phân bổ ngân sách 10 ô trang bị (10-Slot Identity & Stat Budget).
    - Phân lớp phôi đồ cơ bản (Pure Armour, Evasion, ES và Hybrid bases) cùng Implicits đặc thù.
    - Toán học Affix chuyên sâu: Ranh giới bất biến giữa Local Modifiers và Global Modifiers.
    - Triết lý đồ Độc Bản (Unique Items): Không chạy đua chỉ số thô, tập trung mở khóa cơ chế Build-Enabling.
    - Vòng đời trang bị (Early -> Mid -> Endgame) và tối ưu hóa trải nghiệm Mini-Window 440x780 (Smart Tooltip Delta, Auto-Affinity Stash).

12. **[12. Lộ Trình Triển Khai Toàn Diện (`12_master_implementation_roadmap.md`)](12_master_implementation_roadmap.md)**
    - Bản đồ chiến lược 8 giai đoạn từ nền tảng đến bản hoàn thiện.
    - 4 mốc phát hành lớn: M1 (Vertical Slice), M2 (Closed Alpha), M3 (Open Beta), M4 (Golden Master v1.0).
    - Phân tích chi tiết tiến độ, phụ thuộc kỹ thuật (dependencies) và ma trận quản trị rủi ro.

13. **[Kế Hoạch Triển Khai Kỹ Thuật: Tiến Trình Kép & Hóa Thạch (`DUAL_PROGRESSION_AND_CRAFTING_PLAN.md`)](DUAL_PROGRESSION_AND_CRAFTING_PLAN.md)**
    - Checklist chi tiết 6 giai đoạn phát triển: JSON Data Pipeline, Auto-Mapping Queue, The Endless Abyss Engine, Streamlined Fossil Crafting Anvil, Viewport UI và Unit Tests.
