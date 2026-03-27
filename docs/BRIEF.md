# 💡 BRIEF: Shop Drawing Ribbon UI & Export System

**Ngày tạo:** 2026-03-17
**Dự án:** ShopDrawing AutoCAD Plugin (.NET 8 / AutoCAD 2026)

---

## 1. VẤN ĐỀ CẦN GIẢI QUYẾT

Hiện tại plugin chỉ hoạt động qua **Command Line** (`SD_PANEL`, `SD_WALL_CREATE`...).
Cần giao diện **Ribbon CUI** trực quan, chuyên nghiệp, gồm các nút bấm gọi đến
từng nhóm tính năng — giúp user thao tác nhanh và không cần nhớ lệnh.

## 2. KIẾN TRÚC RIBBON

```
┌─ AutoCAD Ribbon ──────────────────────────────────────────────────┐
│  Home  │  Insert  │  Annotate  │ ★ Shop Drawing │  ...          │
└────────┴──────────┴────────────┴─────────────────┴───────────────┘
                                       │
              ┌────────────────────────┴────────────────────────┐
              │            Panel: Shop Drawing Tools            │
              │                                                 │
              │  [📐 Shopdrawing] [📏 Smart Dim]               │
              │  [📄 Layout]      [📤 Xuất bản vẽ]             │
              └─────────────────────────────────────────────────┘
```

### Cấu trúc:
- **1 Tab:** "Shop Drawing"
- **1 Panel:** "Shop Drawing Tools"
- **4 Buttons** — mỗi nút có icon riêng, mỗi nút mở Palette tương ứng

## 3. CHI TIẾT 4 NÚT

### 3.1. 📐 Shopdrawing
| Thuộc tính | Giá trị |
|---|---|
| **Lệnh** | `SD_PANEL` (đã có) |
| **Hành vi** | Mở Palette hiện tại — chọn tường, spec, tạo bản vẽ |
| **Trạng thái** | ✅ Đã triển khai |
| **Phase** | 1 |

### 3.2. 📏 Smart Dimension
| Thuộc tính | Giá trị |
|---|---|
| **Lệnh** | `SD_SMART_DIM` (mới) |
| **Hành vi** | Mở Palette — kết hợp 2 mode: |
| | **Auto**: Click tường → tự nhận diện panel → ghi kích thước tất cả |
| | **Manual**: Wrap lệnh DIM có sẵn + chuẩn hóa style, layer `SD_DIM` |
| **Trạng thái** | 🆕 Cần triển khai |
| **Phase** | 2 |

### 3.3. 📄 Layout
| Thuộc tính | Giá trị |
|---|---|
| **Lệnh** | `SD_LAYOUT` (mới) |
| **Hành vi** | Mở Palette — phân bổ bản vẽ từ Model Space vào Layout tabs |
| | Tự động tạo Layout, đặt Viewport, scale, title block |
| **Trạng thái** | 🆕 Cần triển khai |
| **Phase** | 3 |

### 3.4. 📤 Xuất bản vẽ
| Thuộc tính | Giá trị |
|---|---|
| **Lệnh** | `SD_EXPORT` (mới) |
| **Hành vi** | Mở Palette/Dialog — chọn Layouts → Export PDF |
| | Phục vụ trình duyệt & ban hành sản xuất/thi công |
| **Trạng thái** | 🆕 Cần triển khai |
| **Phase** | 4 |

## 4. LỘ TRÌNH TRIỂN KHAI

```
Phase 1: Ribbon + Shopdrawing         ← Nền tảng, tạo Tab + Panel + gọi lệnh có sẵn
    │
Phase 2: Smart Dimension              ← Đo kích thước tự động + thủ công
    │
Phase 3: Layout                       ← Phân bổ bản vẽ vào Layout tabs
    │
Phase 4: Xuất bản vẽ                  ← Export PDF cho trình duyệt/ban hành
```

### Phase 1 — Ribbon + Shopdrawing (Ưu tiên cao)
- [ ] Tạo class `RibbonInitializer.cs` — đăng ký Tab, Panel, Buttons
- [ ] Tạo icon cho 4 nút (16x16 + 32x32 px)
- [ ] Nút "Shopdrawing" gọi `SD_PANEL`
- [ ] 3 nút còn lại hiện thông báo "Đang phát triển"

### Phase 2 — Smart Dimension
- [ ] Thiết kế Palette Smart Dimension
- [ ] Auto mode: nhận diện panel group → ghi dim chuỗi
- [ ] Manual mode: DIM chuẩn hóa style + layer

### Phase 3 — Layout
- [ ] Thiết kế Palette Layout Manager
- [ ] Tạo Layout tabs tự động từ Model Space
- [ ] Đặt Viewport + scale + title block

### Phase 4 — Xuất bản vẽ
- [ ] Thiết kế Dialog Export
- [ ] Chọn Layout → Export PDF
- [ ] Đặt tên file tự động, chọn plot style, khổ giấy

## 5. THIẾT KẾ KỸ THUẬT SƠ BỘ

### Files cần tạo/sửa (Phase 1):

```
ShopDrawing.Plugin/
├── Commands/
│   └── ShopDrawingCommands.cs    ← MODIFY: thêm lệnh SD_SMART_DIM, SD_LAYOUT, SD_EXPORT
├── UI/
│   └── RibbonInitializer.cs      ← NEW: khởi tạo Ribbon Tab + Panel + Buttons
└── Resources/
    ├── icon_shopdrawing.png       ← NEW: icon 32x32
    ├── icon_smartdim.png          ← NEW: icon 32x32
    ├── icon_layout.png            ← NEW: icon 32x32
    └── icon_export.png            ← NEW: icon 32x32
```

### Ribbon API (AutoCAD .NET):

```csharp
// Tạo Ribbon trong AutoCAD .NET 8:
var ribbonCtrl = ComponentManager.Ribbon;
var tab = new RibbonTab { Title = "Shop Drawing", Id = "SD_TAB" };
var panel = new RibbonPanelSource { Title = "Shop Drawing Tools" };
// Mỗi nút là RibbonButton → CommandHandler gọi lệnh tương ứng
var btnSD = new RibbonButton { Text = "Shopdrawing", CommandParameter = "SD_PANEL" };
```

## 6. RỦI RO & LƯU Ý

| Rủi ro | Giải pháp |
|---|---|
| Ribbon chỉ hiện khi plugin loaded | Đăng ký qua `IExtensionApplication.Initialize()` |
| Icon bị mờ trên Ribbon | Cần icon 32x32 PNG trên nền trong suốt |
| Layout API phức tạp (Phase 3) | Nghiên cứu `LayoutManager` API trước khi implement |

## 7. BƯỚC TIẾP THEO

> Chạy `/plan` để thiết kế chi tiết **Phase 1: Ribbon + Shopdrawing**.
