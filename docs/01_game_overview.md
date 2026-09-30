# 01. Tổng Quan Dự Án & Triết Lý Thiết Kế

## 1. High Concept

**Idle Exile** là tựa game nhập vai nhàn rỗi (Idle/Incremental ARPG) được thiết kế cho hệ máy PC (phát triển bằng **Godot Engine**), chạy dưới dạng một **cửa sổ mini (Mini-Window Companion App)** tiện lợi.

Trò chơi chắt lọc tinh hoa lớn nhất của *Path of Exile*: **chiều sâu cơ chế, sự tự do trong việc xây dựng nhân vật (theorycrafting), và nền kinh tế vật phẩm dựa trên currency**. Game loại bỏ hoàn toàn các rào cản về thao tác tay (APM), đồ họa 3D nặng nề và chiến dịch cốt truyện (Acts) dài dòng, cho phép người chơi nhảy thẳng vào vòng xoáy cày cuốc bản đồ (Mapping) từ giây phút đầu tiên.

---

## 2. Các Trụ Cột Thiết Kế (Core Design Pillars)

### Pillar 1: "Zero APM, Infinite Theorycrafting"
* Không yêu cầu điều khiển di chuyển hay né đòn vi mô.
* Tính toán chiến thắng dựa hoàn toàn vào việc người chơi setup nhân vật: cân bằng giữa **Effective DPS** (sát thương thực tế) và nhiều tầng **Defensive Layers** (các lớp phòng thủ chồng lấn).
* Tự động hóa hoàn toàn các kỹ năng phụ trợ (Trigger Gems, Curses, Auras, Guard Skills).

### Pillar 2: "Immediate Mapping — Không Cốt Truyện, Không Act"
* Đa số người chơi ARPG xem chiến dịch cốt truyện (Acts 1–10) là rào cản buồn ngủ trước khi đến phần hấp dẫn nhất: **Endgame Mapping**.
* **Idle Exile cắt bỏ 100% campaign**:
  * Người chơi xuất hiện ngay tại căn phòng chứa **Cỗ Máy Dịch Chuyển (Map Device)** với một nhân vật Lv 1 và vài tấm đá bản đồ sơ khai (Tier 1 Map Slates).
  * Toàn bộ tiến trình lên cấp, mở khóa hệ thống, và săn lùng trang bị đều diễn ra trực tiếp qua các Map với độ khó tăng dần.

### Pillar 3: "True Path of Exile DNA"
* **Bảng nội tại thụ động (Passive Skill Web)** khổng lồ kết nối đa hướng.
* **Hệ thống Kỹ năng độc lập (PoE 2 Gem Sockets)**: Trang bị thuần túy chứa Stats (thay đồ tự do); Active Gem tự chứa các khe Support cắm trực tiếp không cần nối dây.
* **Kinh tế Currency thuần túy**: Không có vàng (Gold). Mọi giao dịch và nâng cấp đồ đều dùng các loại ngọc ma thuật (Transmutation, Alteration, Chaos, Exalted, Divine...).
* **Phân biệt công thức toán học nghiêm ngặt**: *Increased/Reduced* (cộng dồn) vs *More/Less* (nhân hệ số mũ).

### Pillar 4: "Mini-Window & Low-Friction Companion"
* Giao diện tối giản 2D, kích thước nhỏ gọn (chuẩn đề xuất: `440 x 780` px hoặc `480 x 820` px), có tùy chọn ghim nổi lên trên (`Always on Top`).
* Tối ưu hóa cực nhẹ cho Godot 4: tiêu thụ <1% CPU và <150MB RAM ở chế độ treo nền, không làm ảnh hưởng khi người chơi làm việc, lướt web hoặc chơi game khác.
* Âm thanh phản hồi xúc giác (Dopamine audio drops): Tiếng "ting" rền vang của loot filter khi rớt đồ xịn.

---

## 3. Vòng Lặp Trò Chơi Cốt Lõi: Tiến Trình Kép (Dual-Axis Loop)

Idle Exile vận hành dựa trên kiến trúc 2 trục tương hỗ: **Trục chính Mapping (80%)** cung cấp tài nguyên, phôi đồ và nhiên liệu; **Trục phụ Endless Abyss (20%)** đóng vai trò thước đo EHP/DPS vô tận và cung cấp công cụ Target-Craft tối thượng.

```
┌─────────────────────────────────────────────────────────────┐
│ 1. CHUẨN BỊ & XẾP HÀNG ĐỢI (Zero-APM Preparation)           │
│    • Roll Affixes cho Map (Tăng Quantity, Rarity, Pack Size) │
│    • Nạp vào Map Queue (10 slots) hoặc bật Continuous Run   │
└──────────────────────────────┬──────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────┐
│ 2. VƯỢT MAP TỰ ĐỘNG (Auto-Run Simulation - 80% Time)        │
│    • Hành quân dọc hành lang, dọn cụm quái & Encounters     │
│    • Thu hoạch Voltaxic Sulphite từ các mỏ địa chấn         │
│    • Đột nhập Boss Room, diệt Map Boss & Loot Explosion     │
└───────────────┬──────────────────────────────┬──────────────┘
                │                              │
                ▼ (Thu nạp Sulphite)           ▼ (Base Items, Maps, Currency)
┌───────────────────────────────┐ ┌───────────────────────────┐
│ 2B. THE ENDLESS ABYSS (20%)   │ │ 3. THU THẬP & LỌC ĐỒ      │
│ • Tiêu hao Sulphite đào sâu   │ │ • Loot Filter tự động     │
│ • Độ sâu Depth 1 -> Depth ∞   │ │ • Map Sustain bậc cao hơn │
│ • Benchmark EHP & DPS build   │ │ • Affinity Stash phân loại│
│ • Rơi độc quyền: HÓA THẠCH    │ └─────────────┬─────────────┘
└───────────────┬───────────────┘               │
                │ (Abyssal Fossils)             │
                ▼                               ▼
┌─────────────────────────────────────────────────────────────┐
│ 4. TỐI ƯU HÓA & CHẾ TẠO ĐỈNH CAO (Theorycrafting & Craft)   │
│    • Phân bổ điểm Passive Tree & Atlas Passive Tree         │
│    • Ghép Support Gems vào Active Gems (PoE 2 Model)         │
│    • Craft đồ định hướng bằng Hóa Thạch (Fossil Slots)       │
│    • Phá vỡ trần sức mạnh để chinh phục Tier Map & Depth mới│
└──────────────────────────────┬──────────────────────────────┘
                               │
                               └─────────► Quay lại Bước 1
```

---

## 4. Bối Cảnh Tối Giản (Minimalist Lore)

Thay vì những đoạn hội thoại kéo dài, bối cảnh chỉ phục vụ cơ chế chơi:

> *"Bạn là một Kẻ Lưu Đày (Exile) vô danh tỉnh dậy bên trong một phế tích cổ đại bị phong ấn. Trước mặt bạn chỉ có duy nhất một cỗ máy kỳ bí: **The Crucible Map Device** và những mảnh đá khắc ấn ngàn năm.*  
> *Mỗi mảnh đá mở ra một vết rách không gian dẫn đến những cõi giới hỗn mang đầy quái vật và kho báu. Cách duy nhất để tồn tại và trở nên bất tử là nuốt chửng năng lượng từ các chiều không gian đó, đẩy cỗ máy đến những giới hạn tột cùng."*

Không có NPC giao nhiệm vụ rườm rà. Mọi tiến trình cốt truyện đều được hiển thị qua các cột mốc **Atlas Milestones** (ví dụ: *Tiêu diệt Boss Tier 5*, *Mở khóa nhánh Keystone vũ trụ*, *Đánh bại Guardian of the Void*).
