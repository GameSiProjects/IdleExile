# Kế Hoạch Triển Khai: Tiến Trình Kép & Chế Tạo Hóa Thạch Tinh Gọn
# (Dual-Axis Progression & Streamlined Fossil Crafting Implementation Plan)

Tài liệu này chi tiết hóa lộ trình kỹ thuật (Technical Roadmap) dưới dạng checklist để hiện thực hóa:
1. **Hệ thống Auto-Mapping Zero-APM (Map Queue 10 Slots & Continuous Run)**.
2. **Vực Sâu Vô Tận: The Endless Abyss (Endless Delve Benchmark & Sulphite Loop)**.
3. **Chế tạo Hóa Thạch Tinh Gọn (Streamlined Fossil Crafting 1-Click trên Bàn Chế Tạo)**.

---

## 🏗️ Nguyên Tắc Kiến Trúc Bắt Buộc

* **Tách biệt hoàn toàn (Separation of Concerns):** Toàn bộ logic tính toán độ sâu, hàng đợi Map, kho Hóa thạch và công thức trọng số chế tạo nằm tại `src/Core/` (Pure C# .NET 8, tuyệt đối không kế thừa `Godot.Node`).
* **Không Hardcode dữ liệu:** Danh sách Hóa thạch, chỉ số trọng số Affix và công thức scale độ sâu lưu tại `data/fossils.json` và `data/abyss.json`.
* **Zero APM & Tối ưu Viewport 440x780:** Mọi thao tác chế tạo và chạy bản đồ phải được thiết kế dạng 1-Click, không kéo-thả phức tạp, không làm tràn giao diện mini.

---

## 📋 Checklist Triển Khai Chi Tiết

### Giai đoạn 1: Dữ Liệu & Data Pipeline (JSON & Core Models)
> **Mục tiêu:** Xây dựng nguồn chân lý (Source of Truth) cho Hóa thạch và Vực sâu vô tận.

- [ ] **Task 1.1: Tạo `data/fossils.json`**
  - [ ] Khai báo 7 Hóa thạch cơ bản: `Pristine`, `Metallic`, `Scorched`, `Frigid`, `Dense`, `Corroded`, `Sanctified`.
  * Khai báo tag modifier weights: `Multipliers` (ví dụ `"Life": 10.0`), `BlockedTags` (ví dụ `["Defences"]`), `MinTierBias`.
- [ ] **Task 1.2: Tạo `data/abyss.json`**
  - [ ] Khai báo công thức scale độ sâu ($HP$, $Damage$, $SulphiteCost$).
  - [ ] Khai báo các cột mốc mở khóa (Depth 150 ➔ Fossil Slot 2; Depth 300 ➔ Fossil Slot 3).
  - [ ] Khai báo bảng rơi Hóa thạch theo độ sâu (Fossil Drop Tables).
- [ ] **Task 1.3: C# Models trong `src/Core/Models/`**
  - [ ] `FossilDefinition.cs`: Record bất biến map với `data/fossils.json`.
  - [ ] `AbyssDepthConfig.cs`: Record cấu hình độ sâu và chi phí Sulphite.
  - [ ] `CraftingAnvilState.cs`: Trạng thái mở khóa các slots và hóa thạch đang chọn.
  - [ ] `MapQueueItem.cs` & `MapQueueConfig.cs`: Cấu hình 10 slots hàng đợi map.
- [ ] **Task 1.4: Cập nhật `DatabaseLoader.cs`**
  - [ ] Thêm `LoadFossils()` và `LoadAbyssConfig()` vào pipeline khởi động.

---

### Giai đoạn 2: Tự Động Hóa Mapping Zero-APM (Map Queue & Continuous Run)
> **Mục tiêu:** Giải phóng hoàn toàn thao tác tay cho người chơi khi cày Map.

- [ ] **Task 2.1: Bộ Quản Lý Hàng Đợi Map (`MapQueueManager.cs`)**
  - [ ] Khay chứa 10 ô `MapQueueItem`.
  - [ ] Hỗ trợ thêm nhanh từ Map Stash (`EnqueueMap`), xóa hoặc đổi vị trí.
  - [ ] Sự kiện `OnMapQueueAdvanced`: Tự động nạp map kế tiếp sau 3 giây delay dọn loot.
- [ ] **Task 2.2: Dịch Vụ Cày Bản Đồ Liên Tục (`ContinuousMappingService.cs`)**
  - [ ] Chế độ tự động tìm Map trong Stash khi hàng đợi trống (theo Tier đã chọn).
  - [ ] **Map Safety Filter:** Kiểm tra và loại bỏ map có mod cấm kỵ (Reflect Phys/Ele, No Regen/Leech) phù hợp với build nhân vật.
  - [ ] **Fail-safe fallback:** Tự động kích hoạt *The Endless Hollow (Tier 0)* khi hết sạch Map hợp lệ.
- [ ] **Task 2.3: Tích Hợp Vào `CombatEngine.cs`**
  - [ ] Khi sự kiện `OnMapCompleted` phát sinh: Nếu bật Queue hoặc Continuous Run, tự gọi `StartNextMap()` sau độ trễ an toàn.
  - [ ] Không ngắt quãng vòng lặp combat nếu người chơi đang treo máy làm việc.

---

### Giai đoạn 3: Vực Sâu Vô Tận — The Endless Abyss (Core Simulation)
> **Mục tiêu:** Xây dựng hệ sinh thái Delve vô tận làm thước đo sức mạnh và nguồn rơi Hóa thạch.

- [ ] **Task 3.1: Nền Kinh Tế Sulphite trong `PlayerState.cs`**
  - [ ] Bổ sung chỉ số `CurrentSulphite`, `MaxSulphiteCapacity` vào tài nguyên nhân vật.
  - [ ] Cơ chế rơi quặng Sulphite khi chạy Yellow/Red Maps (từ Tier 6 trở lên).
- [ ] **Task 3.2: Dịch Vụ Mô Phỏng Vực Sâu (`AbyssSimulationService.cs`)**
  - [ ] Tính toán chỉ số quái theo Depth:
    $$\text{HP Multiplier} = 1.0 + (Depth \times 0.08)^{1.35}$$
    $$\text{Damage Multiplier} = 1.0 + (Depth \times 0.05)^{1.25}$$
  - [ ] Tính toán tiêu hao Sulphite trên mỗi Node: $\text{Cost} = \text{BaseCost} \times \sqrt{Depth}$.
  - [ ] Ghi nhận kỷ lục `DeepestDepthReached` và phát cờ mở khóa cột mốc (Milestones).
- [ ] **Task 3.3: Bộ Rơi Hóa Thạch Độc Quyền (`FossilDropCoordinator.cs`)**
  - [ ] Quái vật trong Abyss có tỉ lệ rơi các loại Hóa thạch theo độ sâu.
  - [ ] Khi nhặt được: Tự động cộng vào kho Hóa thạch chuyên dụng (`PlayerState.FossilInventory`), không chiếm ô túi đồ.

---

### Giai đoạn 4: Động Cơ Chế Tạo Hóa Thạch Tinh Gọn (Streamlined Crafting Engine)
> **Mục tiêu:** Hiện thực hóa cơ chế chế tạo định hướng 1-Click tại Bàn Chế Tạo (Crafting Anvil).

- [ ] **Task 4.1: Tính Toán Trọng Số Affix (`FossilAffixWeightCalculator.cs`)**
  - [ ] Lấy danh sách 1–3 Hóa thạch đang chọn.
  - [ ] Nhân hệ số với các Tag được buff (ví dụ: Tag "Life" $\times 10$).
  - [ ] Triệt tiêu hoàn toàn các Tag bị cấm (gán Weight = 0).
  - [ ] Đẩy xác suất các dòng bậc cao nếu có `Sanctified Fossil`.
- [ ] **Task 4.2: Dịch Vụ Chế Tạo Hóa Thạch (`FossilCraftingService.cs`)**
  - [ ] Phương thức `PreviewOutcome(ItemInstance item, List<FossilDefinition> fossils)`: Trả về dự đoán trực quan (nhóm chỉ số tăng mạnh, nhóm bị khóa) cho UI.
  - [ ] Phương thức `ExecuteCraft(ItemInstance item, List<FossilDefinition> fossils, PlayerState player)`:
    - Kiểm tra số lượng Hóa thạch và Currency xúc tác (*Orb of Alchemy* hoặc *Chaos Orb*).
    - Tiêu hao nguyên liệu.
    - Roll lại các dòng Affix theo bảng trọng số đã biến đổi.
    - Trả về `CraftResult` (Thành công, Món đồ mới, Danh sách dòng mới).

---

### Giai đoạn 5: Giao Diện Người Dùng Trên Viewport 440x780 (Godot UI)
> **Mục tiêu:** Thiết kế UI trực quan, không ma sát, chuẩn kích thước Mini-Window.

- [ ] **Task 5.1: Giao Diện Bàn Chế Tạo (`CraftingAnvilView.cs` & `.tscn`)**
  - [ ] Ô đặt trang bị trung tâm với khung viền kim loại cổ điển.
  - [ ] 3 ô Fossil Slots tròn: Hiển thị icon Hóa thạch đã chọn; nếu chưa mở khóa (Depth < 150/300) hiển thị ổ khóa mờ kèm chú thích *"Yêu cầu Depth X"*.
  - [ ] Bảng dự báo thời gian thực: Danh sách dòng màu xanh lá (+Tăng) và đỏ gạch (Khóa).
  - [ ] Nút `[CHẾ TẠO]` 1-Click to bản, hiển thị rõ số lượng Currency xúc tác tiêu tốn.
- [ ] **Task 5.2: Giao Diện Vực Sâu Vô Tận (`AbyssDashboardView.cs`)**
  - [ ] Hiển thị độ sâu hiện tại (Depth Level) bằng font số to nổi bật.
  - [ ] Thanh nhiên liệu Sulphite màu vàng lân tinh (`ProgressBar`).
  - [ ] Nút "Lặn Sâu (Descend)" 1-Click để chuyển Arena sang chế độ Abyss Run.
- [ ] **Task 5.3: Khay Hàng Đợi Map (`MapQueueBarView.cs`)**
  - [ ] Thanh 10 ô mini nằm gọn dưới Map Header.
  - [ ] Nút bật/tắt công tắc `Continuous Auto-Run [ON/OFF]`.

---

### Giai đoạn 6: Kiểm Thử Toàn Diện & Tối Ưu (Testing & Verification)
> **Mục tiêu:** Bảo đảm tính toán chính xác 100%, 0 bug và không rò rỉ tài nguyên.

- [ ] **Task 6.1: Unit Tests Cho Fossil Math (`FossilCraftingTests.cs`)**
  - [ ] Test chặn tag: Sử dụng `Pristine Fossil` trên 10,000 lần roll đồ -> Xác nhận 100% không có bất kỳ dòng Armour/Evasion/ES nào xuất hiện.
  - [ ] Test tăng trọng số: Xác nhận tỉ lệ ra dòng Life tăng tối thiểu 8x–10x so với roll Chaos thông thường.
- [ ] **Task 6.2: Unit Tests Cho Abyss Scaling (`AbyssScalingTests.cs`)**
  - [ ] Kiểm tra công thức HP/Damage ở Depth 1, 100, 300, 1000 xem có đúng hàm số mũ không.
  - [ ] Kiểm tra logic trừ Sulphite và phát cờ mở khóa Slot 2, Slot 3.
- [ ] **Task 6.3: Unit Tests Cho Map Queue (`MapQueueTests.cs`)**
  - [ ] Kiểm tra tự động chuyển map khi hoàn thành, kiểm tra Fallback về T0 khi hết map.
- [ ] **Task 6.4: Headless Verification**
  - [ ] Chạy lệnh `dotnet build` đạt 0 Warning, 0 Error.
  - [ ] Chạy Godot Headless console kiểm tra nạp `data/fossils.json` sạch sẽ.
