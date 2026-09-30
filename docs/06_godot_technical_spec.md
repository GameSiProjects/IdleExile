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
├── project.godot                     # Cấu hình Godot Engine
├── IdleExile.csproj                  # Project file C# (.NET 8)
├── docs/                             # Tài liệu Game Design
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
    │   │   └── DamageCalculator.cs   # Công thức Increased vs More
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
        ├── WindowController.cs       # Always-on-top, quản lý cửa sổ mini
        ├── BattleViewport.cs         # Vẽ combat 2D tối giản
        ├── Tooltips/                 # Hover tooltip hiển thị đồ chuẩn PoE
        └── Tabs/                     # InventoryTab, GearTab, AtlasTab, TreeTab
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

## 5. Tối Ưu Cửa Sổ Mini (Companion Window Controller)

C# Script điều khiển cửa sổ mini (`WindowController.cs`):

```csharp
using Godot;

public partial class WindowController : Node
{
    [Export] public bool DefaultAlwaysOnTop = true;

    public override void _Ready()
    {
        // 1. Đặt cửa sổ ở chế độ Always on Top
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.AlwaysOnTop, DefaultAlwaysOnTop);

        // 2. Kích hoạt Low Processor Mode để ăn cực ít CPU lúc treo máy
        OS.LowProcessorUsageMode = true;
        OS.LowProcessorUsageModeSleepUsec = 6900;
    }

    public void ToggleAlwaysOnTop(bool isEnabled)
    {
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.AlwaysOnTop, isEnabled);
    }
}
```

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
