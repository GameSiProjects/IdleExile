# 06. Kiến Trúc Kỹ Thuật: C# (.NET 8) & JSON Trong Godot 4

Tài liệu này đặc tả kiến trúc kỹ thuật của **Idle Exile** trên nền tảng **Godot 4 .NET (C#)** kết hợp **Hệ thống dữ liệu JSON (Data-Driven)**, tối ưu cho ứng dụng **Mini-Window Desktop Companion**.

---

## 1. Quyết Định Công Nghệ (Technology Stack Decisions)

* **Game Engine:** Godot 4.3+ (.NET / C# version).
* **Ngôn ngữ Logic:** **C# 12 / .NET 8**.
  * Tận dụng trình biên dịch JIT của .NET cho hiệu năng tính toán toán học vượt trội (nhanh hơn GDScript từ 5x đến 10x).
  * Sử dụng **LINQ** và cú pháp hướng đối tượng mạnh mẽ (`records`, `enums`, `interfaces`, `generics`) để mô hình hóa hệ thống cơ chế phức tạp kiểu PoE.
  * An toàn kiểu dữ liệu tuyệt đối (Compile-time Type Safety).
* **Định Dạng Dữ Liệu:** **JSON** (`System.Text.Json`).
  * Toàn bộ Database (Gems, Affixes, Passive Web, Maps) lưu dạng file JSON con người có thể đọc/sửa dễ dàng.
  * Tự động Deserialize trực tiếp vào C# Strongly-typed Records/Classes khi game khởi động (Hydration Pattern).

---

## 2. Cấu Trúc Thư Mục Dự Án (Project Solution Structure)

```
IdleExile/
├── project.godot                     # Cấu hình Godot Engine (Cửa sổ, gl_compat, i18n)
├── IdleExile.csproj                  # Project file C# (.NET 8)
├── localization/                     # Hệ thống Bản địa hóa (Godot Localization)
│   ├── translations.csv              # Bảng chuỗi dịch tập trung (id, en, vi)
│   ├── translations.en.translation   # File binary dịch tiếng Anh (mặc định)
│   └── translations.vi.translation   # File binary dịch tiếng Việt
├── docs/                             # Tài liệu Game Design & Kỹ thuật
├── data/                             # Dữ liệu JSON gốc (Source of Truth)
│   ├── affixes.json                  # Bảng tra cứu Prefix / Suffix
│   ├── base_items.json               # Các phôi đồ cơ bản
│   ├── gems.json                     # Active & Support Gems
│   ├── maps.json                     # Danh sách Map và Map Modifiers
│   └── passive_tree.json             # Đồ thị mạng lưới điểm nội tại
│
└── src/
    ├── Core/                         # Logic thuần C#, KHÔNG phụ thuộc Godot Nodes
    │   ├── Models/                   # Data structures, Enums, Records
    │   │   ├── AffixDefinition.cs
    │   │   ├── ItemInstance.cs
    │   │   ├── GemDefinition.cs
    │   │   └── PassiveNode.cs
    │   ├── Services/                 # Dịch vụ nạp dữ liệu & tiện ích
    │   │   ├── DatabaseLoader.cs     # Nạp JSON bằng System.Text.Json
    │   │   ├── DamageCalculator.cs   # Công thức Increased vs More
    │   │   └── SmartDeltaFormatter.cs# Định dạng so sánh chỉ số chuẩn tiếng Anh
    │   ├── Simulation/               # Vòng lặp chiến đấu nhịp
    │   │   ├── TickManager.cs        # 30 Ticks / giây (~33.33ms)
    │   │   ├── CombatEngine.cs
    │   │   └── OfflineCalculator.cs  # Ước lượng tiến trình offline theo Snapshot Record O(1)
    │   └── State/                    # Trạng thái người chơi & rương đồ
    │       ├── PlayerState.cs
    │       ├── InventoryManager.cs
    │       └── AtlasProgress.cs
    │
    └── UI/                           # Godot C# Scripts gắn vào Scene Nodes
        ├── LocalizationService.cs    # Wrapper Godot TranslationServer (Loc.Tr, Loc.Format)
        ├── WindowController.cs       # Always-on-top, quản lý cửa sổ mini
        ├── BattleViewport.cs         # Vẽ combat 2D tối giản
        ├── Tooltips/                 # Hover tooltip hiển thị đồ chuẩn PoE
        └── Components/               # EquipmentView, GemView, AtlasView, VitometerView
```

---

## 3. Kiến Trúc Dữ Liệu JSON & C# (Data Pipeline)

### 3.1. Cấu Trúc JSON Mẫu (`data/affixes.json`)
```json
[
  {
    "id": "flat_maximum_life",
    "nameTemplate": "{0} to Maximum Life",
    "type": "Prefix",
    "tier": 1,
    "minIlvl": 86,
    "minValue": 120,
    "maxValue": 129,
    "tags": ["life", "resource", "defenses"]
  },
  {
    "id": "fire_resistance",
    "nameTemplate": "+{0}% to Fire Resistance",
    "type": "Suffix",
    "tier": 1,
    "minIlvl": 84,
    "minValue": 46,
    "maxValue": 48,
    "tags": ["fire", "resistance", "elemental"]
  }
]
```

### 3.2. C# Strongly-Typed Models
```csharp
namespace IdleExile.Core.Models;

public enum AffixType
{
    Prefix,
    Suffix
}

public sealed record AffixDefinition(
    string Id,
    string NameTemplate,
    AffixType Type,
    int Tier,
    int MinIlvl,
    float MinValue,
    float MaxValue,
    string[] Tags
);
```

### 3.3. Dịch Vụ Nạp Dữ Liệu Bằng C# (`DatabaseLoader.cs`)
```csharp
using System.Text.Json;
using System.IO;
using Godot;

namespace IdleExile.Core.Services;

public static class DatabaseLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static Dictionary<string, AffixDefinition> LoadAffixes(string filePath)
    {
        // Chuyển đường dẫn Godot res:// sang đường dẫn hệ thống thực tế nếu cần
        string globalPath = ProjectSettings.GlobalizePath(filePath);
        string json = File.ReadAllText(globalPath);

        var list = JsonSerializer.Deserialize<List<AffixDefinition>>(json, JsonOptions);
        return list?.ToDictionary(a => a.Id) ?? new();
    }
}
```

---

## 4. Tận Dụng Sức Mạnh C# Cho Logic PoE

### 4.1. Lọc Đồ Nhanh Bằng LINQ (Loot Filter Implementation)
Hệ thống Loot Filter được lập trình cực kỳ tinh tế nhờ LINQ:

```csharp
public bool ShouldKeepItem(ItemInstance item, LootFilterSettings filter)
{
    // Bỏ qua đồ trắng/xanh nếu chế độ Strict bật
    if (filter.Strictness >= FilterStrictness.Strict && item.Rarity < ItemRarity.Rare)
        return false;

    // Luôn nhặt nếu có 6 links
    if (item.Sockets.MaxLinkCount >= 6) return true;

    // Giữ đồ nếu có Tier 1 kháng hoặc máu
    bool hasHighTierDefenses = item.Affixes
        .Any(a => a.Tier == 1 && (a.Tags.Contains("life") || a.Tags.Contains("resistance")));

    return hasHighTierDefenses;
}
```

### 4.2. Tính Toán Sát Thương Chuẩn "Increased" vs "More"
```csharp
public float CalculateFinalDamage(
    float baseDamage, 
    IEnumerable<float> increasedModifiers, 
    IEnumerable<float> reducedModifiers, 
    IEnumerable<float> moreModifiers, 
    IEnumerable<float> lessModifiers)
{
    float totalIncreased = increasedModifiers.Sum();
    float totalReduced = reducedModifiers.Sum();
    float additiveSum = 1.0f + (totalIncreased - totalReduced) / 100.0f;
    additiveSum = Math.Max(0.0f, additiveSum);

    float multiplicative = 1.0f;
    foreach (var more in moreModifiers)
        multiplicative *= (1.0f + more / 100.0f);
    foreach (var less in lessModifiers)
        multiplicative *= (1.0f - less / 100.0f);

    return baseDamage * additiveSum * multiplicative;
}
```

---

## 5. Tối Ưu Cửa Sổ Mini & Kiến Trúc Cửa Sổ Động (Dynamic Desktop Companion Architecture)

Nhằm đáp ứng linh hoạt giữa **chế độ treo máy rảnh tay (Zero-APM Hands-off Mapping)** và **chế độ nghiên cứu đồ đạc chuyên sâu (Deep Theorycrafting)**, hệ thống cửa sổ của **Idle Exile** được xây dựng theo kiến trúc **Dynamic Resizable Desktop Companion** (tham chiếu từ [reference/windows](file:///e:/Projects/Antigravity/IdleExile/reference/windows/)):

### 5.1. Các Chế Độ Kích Thước Cửa Sổ (Window Resolution Modes)
1. **Compact Mode (`440 x 380` px) — Treo Máy Tinh Gọn:**
   * Chiều cao chỉ 380px, chiếm một góc nhỏ unobtrusive trên desktop (ngay trên thanh Windows Taskbar).
   * Tập trung hiển thị: Header siêu gọn, thanh tiến trình Map, sàn đấu 2D Top-Down (Arena), thanh máu/mana kép (Vitometer), và nút Emergency Portal.
2. **Expanded Mode (`440 x 780` px) — Soi Đồ & Theorycrafting:**
   * Chiều cao 780px dọc chuẩn.
   * Mở rộng upward hiển thị toàn diện các Sub-Tabs: Bảng trang bị 10 ô (Gear View), Smart Tooltip Delta so sánh chỉ số, Quản lý Gem liên kết (PoE 2 model), Cây nội tại (Passive Tree), Rương đồ (Stash).
3. **Wide Dual-Column Mode (`800 x 780` px) — Màn Hình Rộng Đa Cột:**
   * Dành cho màn hình 2K/4K: Cột trái hiển thị Combat Viewport & Map Device, cột phải hiển thị Trang bị & Cây Passive song song.
4. **Freeform Resizing (Kéo Dãn Tự Do):**
   * Cho phép người chơi tự do kéo góc cạnh với `MinSize = Vector2I(400, 340)`. Bố cục UI thích ứng tự động qua hệ thống Anchor và Containers của Godot.

### 5.2. Thuật Toán Giữ Cố Định Chân Cửa Sổ (`keepBottom = true`)
Khi người chơi chuyển đổi giữa chế độ Compact và Expanded (hoặc khi click mở bảng Gear):
$$\Delta Y = \text{NewHeight} - \text{CurrentHeight}$$
$$\text{NewWindowY} = \text{CurrentWindowY} - \Delta Y$$
* **Nguyên tắc then chốt:** Cửa sổ **nở lên phía trên** và **thu lại xuống phía dưới**. Cạnh đáy của cửa sổ luôn nằm bất biến ngay sát mép thanh Taskbar, không bao giờ bị trượt mất nút bấm hay tụt xuống dưới màn hình!

### 5.3. Cầu Nối Nền Tảng Windows & DisplayServer (`DesktopWindow.cs`)
* Tích hợp Win32 API (`user32.dll`, `shell32.dll`) kết hợp Godot `DisplayServer`:
  * `SHAppBarMessage(ABM_GETTASKBARPOS)`: Xác định tọa độ và kích thước thanh Windows Taskbar (dưới đáy, bên trái, phải hay đỉnh).
  * `PositionAboveTaskbar(screen, dock)`: Tự động neo cửa sổ vào góc dưới bên phải (hoặc giữa) phía trên thanh Taskbar.
  * `MoveToNextScreen()`: Chuyển cửa sổ tuần tự qua các màn hình phụ khi dùng đa màn hình (Multi-Monitor setups).
  * `AlwaysOnTop`: Ghim cửa sổ nổi trên các ứng dụng khác (`HWND_TOPMOST` / `DisplayServer.WindowFlags.AlwaysOnTop`).
  * `LowProcessorUsageMode = true`: Đảm bảo khi chạy nền companion, game ăn **< 1% CPU** và RAM dưới 150MB.

### 5.4. Thành Phần Điều Khiển (`WindowController.cs`)
Node C# gắn vào Scene Tree cung cấp các Signals và API trực quan:
* `ToggleExpand(bool keepBottom = true)`
* `SetResolutionMode(WindowResolutionMode mode, bool keepBottom = true)`
* `CycleResolution(bool keepBottom = true)`
* `PositionAboveTaskbar()`
* `MoveToNextScreen()`
* Hỗ trợ kéo thả cửa sổ tự do không viền (`HandleInput`).

### 5.5. Quy Chuẩn Xử Lý Phím Tắt (In-Window Shortcuts Only — Zero OS Hooking)
* **Quy Tắc Kiến Trúc & Quyền Riêng Tư:**
  * Toàn bộ phím tắt điều hướng và tương tác chỉ được tiếp nhận và xử lý khi cửa sổ game đang có focus (In-Window Focus) thông qua Godot `_UnhandledKeyInput(InputEvent @event)`.
  * **Tuyệt đối KHÔNG đăng ký System-wide Global Hotkeys (OS-level hooks như Win32 `RegisterHotKey`)**:
    * Không chiếm dụng tổ hợp phím của các phần mềm làm việc khác (Visual Studio, VS Code, Browser, công cụ đồ họa).
    * Giữ tiến trình Desktop Companion sạch sẽ, không kích hoạt cảnh báo sai từ các phần mềm diệt virus (Antivirus / Heuristics), tiêu thụ 0% tài nguyên CPU khi cửa sổ mất focus.
    * Tuân thủ triết lý cốt lõi: *"Zero APM, 100% Theorycrafting"* — phím tắt chỉ đóng vai trò tiện ích điều hướng tab và xem đồ, không phục vụ micro-actions trong combat.
* **Danh Mục Phím Tắt Tiêu Chuẩn (In-Window):**
  * `Space`: Kích hoạt Emergency Portal tẩu thoát an toàn về Hideout.
  * `I` / `G`: Mở nhanh Tab Trang bị (Gear & Inventory).
  * `P`: Mở nhanh Tab Cây Kỹ Năng Nội Tại (Passive Tree).
  * `M`: Mở nhanh Tab Thiết Bị Bản Đồ (Crucible / Atlas Map Device).
  * `S`: Mở nhanh Tab Rương Chuyên Dụng (Affinity Stash).
  * `Tab` / `F11`: Chuyển đổi nhanh giữa Compact Mode (`440x380`) và Expanded Mode (`440x780`).
  * `Esc`: Đóng các modal / tooltip mở rộng, hoặc thu gọn cửa sổ về Compact Mode nếu đang mở Expanded Mode.

---

## 6. Xử Lý Offline Progress Theo Tỉ Lệ & Record (Rate-Based Estimation O(1))

Thay vì chạy mô phỏng chi tiết từng tick (vốn gây tốn tài nguyên không cần thiết khi offline dài ngày), **Idle Exile** áp dụng cơ chế **Live Benchmark Snapshot**:

### 6.1. Thu Thập Snapshot Khi Online
Trong quá trình chơi online, [CombatEngine](file:///E:/Projects/Antigravity/IdleExile/src/Core/Simulation/) liên tục ghi nhận dữ liệu hiệu năng thực tế (Rolling Benchmark trong 5–10 map gần nhất):
* `AverageClearTimeSeconds`: Thời gian trung bình để dọn 1 Map ở Tier hiện tại (ví dụ: 42.5s / T5 Map).
* `DeathRate`: Tỉ lệ tử vong thực tế trên mỗi Map (ví dụ: 1.5% nguy cơ chết do Map Modifier nguy hiểm).
* `AverageMonstersPerMap`: Số lượng quái vật trung bình trên mỗi đợt.

### 6.2. Thuật Toán Tính Toán Tức Thì Khi Mở Game ($O(1)$)
Khi người chơi mở lại game sau nhiều giờ offline:
1. **Xác định thời gian hiệu dụng:**
   $$\text{EffectiveTime} = \min(\text{OfflineTime}, \text{MaxOfflineCap})$$ *(Mặc định cap 12 tiếng).*
2. **Tiêu thụ Map:**
   * Tính số map tối đa có thể cày: $\text{PotentialMaps} = \lfloor \text{EffectiveTime} / \text{AverageClearTimeSeconds} \rfloor$.
   * Tự động tiêu thụ các Map cùng Tier có sẵn trong Stash Tab. Nếu hết Map, hệ thống tự động fallback về cày Map Tier 0 (vô hạn, an toàn).
3. **Kinh nghiệm (EXP):**
   $$\text{Net EXP} = \text{MapsCompleted} \times \text{AverageExpPerMap} \times (1 - \text{DeathRate} \times \text{DeathExpPenalty})$$
   *(Áp dụng trừ 10% EXP cho mỗi lần chết mô phỏng theo chuẩn PoE).*
4. **Phân phối Loot gộp (Aggregated Drop Roll):**
   * Tổng số quái vật ảo đã tiêu diệt: $\text{TotalVirtualKills} = \text{MapsCompleted} \times \text{AverageMonstersPerMap}$.
   * Chạy số lượng roll này qua hệ thống Drop Table kết hợp với **Loot Filter** để sinh trực tiếp danh sách Currency và trang bị giá trị vào Stash Tab mà không cần giả lập vị trí hay chuyển động.
5. **Hiển thị Popup Tổng Kết (Offline Summary Dialog):**
   * Thông báo thời gian offline, số map đã chạy, lượng EXP nhận được, số lần chết và danh sách vật phẩm quý hiếm nhặt được. Toàn bộ thuật toán hoàn tất trong **< 1 mili-giây**.

---

## 7. Quy Tắc Debug Code & Cleanliness Trong Bản Release (Release Build Policy)

Nhằm đảm bảo trải nghiệm người chơi luôn sạch sẽ, bảo mật và hiệu năng cực đại đúng triết lý companion app:
1. **Tuyệt Đối Không Để Debug Code Tồn Tại Trong Bản Release:**
   * Toàn bộ nhãn overlay toạ độ nhân vật (`[DEBUG] P_E: (X, Y) | State | Speed`), telemetry vị trí, phím tắt test hay mock preview không được phép hiển thị hay biên dịch vào bản Release.
2. **Khai Báo Điều Kiện Tiền Xử Lý (`#if DEBUG`):**
   * Mọi trường biến, hàm khởi tạo, hàm cập nhật debug HUD phải nằm trong `#if DEBUG ... #endif`.
   * Trong Godot presentation C#, bổ sung kiểm tra runtime `if (!OS.IsDebugBuild()) return;` để phòng vệ kép:
     ```csharp
     #if DEBUG
     if (OS.IsDebugBuild())
     {
         _battleViewport?.UpdateDebugCoordinates(...);
     }
     #endif
     ```
3. **Kiểm Tra Build:**
   * Bản Release biên dịch qua `dotnet build -c Release` sẽ tự động loại bỏ 100% mã debug khỏi file binary, không tiêu tốn RAM hay chu kỳ CPU.

---

## 8. Kiến Trúc Đa Ngôn Ngữ & Bản Địa Hóa (Localization System - Godot TranslationServer)

Nhằm đáp ứng yêu cầu vận hành chuẩn quốc tế (ngôn ngữ hiển thị mặc định 100% tiếng Anh) đồng thời sẵn sàng mở rộng hỗ trợ tiếng Việt và các ngôn ngữ khác trong tương lai mà không cần tái cấu trúc mã nguồn:

### 8.1. Cơ Sở Dữ Liệu Chuỗi Dịch (`localization/translations.csv`)
* Toàn bộ chuỗi giao diện người dùng, tooltip, huy hiệu so sánh (Delta Badges), thông báo hệ thống được quản lý tập trung trong file `localization/translations.csv` tuân thủ chuẩn RFC 4180.
* Cấu trúc bảng dịch 3 cột: `id,en,vi`:
  ```csv
  id,en,vi
  UI_EQUIP,"Equip","Mặc Ngay"
  UI_EQUIPPED,"Equipped","Đã Mặc"
  UI_UNEQUIP,"Unequip","Tháo Đồ"
  UI_VERDICT_UPGRADE,"▲ UPGRADE","▲ NÂNG CẤP"
  UI_GEM_PO2_DECOUPLED_HINT,"Equipment has no sockets. All skill links reside directly inside Active Gems.\nSelect a gem from inventory below to socket.","Trang bị không có socket. Mọi liên kết kỹ năng nằm trực tiếp trong Active Gem.\nChọn một viên ngọc từ khay bên dưới để lắp vào."
  ```

### 8.2. Cấu Hình Tự Động Biên Dịch Trong `project.godot`
* Godot Engine tự động nhận diện `translations.csv` và biên dịch thành các tài nguyên nhị phân tối ưu:
  * `res://localization/translations.en.translation`
  * `res://localization/translations.vi.translation`
* Khai báo trong `project.godot`:
  ```ini
  [internationalization]
  locale/translations=PackedStringArray("res://localization/translations.en.translation", "res://localization/translations.vi.translation")
  locale/fallback="en"
  ```
* Mặc định ngôn ngữ khởi động và fallback của hệ thống luôn là tiếng Anh (`"en"`).

### 8.3. Dịch Vụ C# Localization (`src/UI/LocalizationService.cs`)
* Cung cấp API tĩnh tiện lợi, hiệu năng cao bao bọc `Godot.TranslationServer`:
  ```csharp
  // Dịch chuỗi tĩnh:
  string text = Loc.Tr("UI_EQUIP"); // "Equip" (en) hoặc "Mặc Ngay" (vi)

  // Định dạng chuỗi tham số động:
  string msg = Loc.Format("UI_GEM_SUPPORT_SOCKET_EMPTY", index + 1);

  // Đổi ngôn ngữ thời gian thực (Runtime Switch):
  Loc.SetLocale("vi"); // hoặc Loc.SetLocale("en");
  ```

### 8.4. Nguyên Tắc Phân Tách Giữa Core Logic và Presentation UI
1. **Lớp Core (`src/Core/`):**
   - Giữ sạch 100% C# thuần túy, tuyệt đối KHÔNG gọi hoặc phụ thuộc vào API Godot `TranslationServer`.
   - Các mô hình dữ liệu (Weapon classes, Affix names, Compatibility reason, Delta verdict constants) lưu trữ trực tiếp bằng thuật ngữ tiếng Anh chuẩn ARPG/PoE (`One-Handed Sword`, `▲ UPGRADE`, `Compatible`).
2. **Lớp Presentation UI (`src/UI/`):**
   - Chịu trách nhiệm hiển thị và phân giải đa ngôn ngữ qua `Loc.Tr()` hoặc gán trực tiếp ID dịch trên các Node UI trong Scene (`.tscn`).
