# 📋 PRD: ShopDrawing Tender Module
**Ngày tạo:** 2026-03-20  
**Version:** 1.2 (Final)  
**Author:** Brainstorm session (20+ Q&A rounds)

---

## 1. Tổng quan

### 1.1. Vấn đề
Hiện tại quy trình chào giá sandwich panel đang làm **hoàn toàn bằng Excel thủ công**:
- Đo kích thước vách trên bản vẽ CAD → ghi ra giấy → nhập Excel
- Tính toán khối lượng panel, phụ kiện bằng công thức Excel
- Dễ sai số, tốn thời gian, mỗi dự án phải làm lại từ đầu

### 1.2. Giải pháp
Thêm module **Tender** vào ShopDrawing Plugin — cho phép:
- **Pick vách trực tiếp từ CAD** (Polyline/Line) → tự động lấy chiều dài hoặc diện tích
- **Nhập dữ liệu trong dialog dạng Excel** — thao tác nhanh, trực quan
- **Auto-tính BOM** (panel + phụ kiện) từ kích thước vách
- **Xuất Excel** khi hoàn thành

### 1.3. Phân biệt Tender vs ShopDrawing

| | ShopDrawing (hiện tại) | Tender (mới) |
|---|---|---|
| **Giai đoạn** | Thi công | Chào giá |
| **Đầu vào** | Vẽ chi tiết từng panel | Nhập nhanh kích thước vách |
| **Mức chi tiết** | Từng panel, joint, opening | Tổng khối lượng ước tính |
| **Thời gian** | Nhiều giờ | Vài phút |
| **Đầu ra** | Drawing + BOM sản xuất | Bảng khối lượng + Excel |

### 1.4. Đối tượng sử dụng
- Kỹ sư triển khai (primary user)

---

## 2. Phạm vi triển khai

### 🚀 Phase 1 (MVP):
- **Palette:** Control panel compact (dockable)
- **Dialog Spec:** Quản lý spec cho dự án (fork từ ShopDrawing)
- **Dialog BOM:** Nhập vách + opening + BOM tổng hợp (panel + phụ kiện)
- **AutoCAD integration:** 2 chế độ pick (chiều dài / diện tích), zoom/highlight
- **Excel export** (flat rows, không merge)

### 🎁 Phase 2 (Future):
- Tab 3 trong BOM dialog: Bảng đơn giá vật tư
- Tab 4: Tổng hợp chào giá
- Enhanced Excel-like: Drag-fill, multi-select
- v2.0: Đánh giá WebView2 + spreadsheet library nếu cần formatting/formulas

---

## 3. Kiến trúc UI

### 3.1. Tổng quan: Palette + Dialog

```
┌─ Palette: Tender (dockable) ────────────────┐
│  📊 TENDER - CHÀO GIÁ                       │
│  Dự án: [_______________]                    │
│  Khách hàng: [_______________]               │
│                                              │
│  ┌──────────────────────────────────────────┐│
│  │ 📋 Quản Lý Spec              [Mở →]     ││
│  │    Khai báo spec cho dự án               ││
│  ├──────────────────────────────────────────┤│
│  │ 📊 Quản Lý Khối Lượng        [Mở →]     ││
│  │    Nhập vách, opening, tính BOM          ││
│  └──────────────────────────────────────────┘│
│                                              │
│  ┌─ Footer ─────────────────────────────────┐│
│  │ Tổng: 25 vách | 1,250 m² | ~327 tấm     ││
│  │ (Chưa có dữ liệu khi chưa nhập)        ││
│  └──────────────────────────────────────────┘│
│       [📥 Xuất Excel]  [💾 Lưu dự án]       │
└──────────────────────────────────────────────┘
         │                    │
         ▼                    ▼
   SpecManagerDialog    TenderBomDialog
   (fork spec list)     (2 tabs + toolbar)
```

**Palette dùng PaletteSet** — giống pattern ShopDrawing, SmartDim, Layout:
- Toggle show/hide bằng `SD_TENDER` command
- Dock được bên cạnh drawing area
- Footer auto-update khi đóng dialog BOM

### 3.2. Spec Management — Fork Pattern

```
Khi mở "Quản Lý Spec" lần đầu:
  → Copy spec list từ panel_specs.json (ShopDrawing) → tender project
  → Tender có bản spec RIÊNG, edit độc lập
  → ShopDrawing sửa spec thoải mái, KHÔNG ảnh hưởng Tender
  → Tender sửa spec cũng KHÔNG ảnh hưởng ShopDrawing
```

Reuse `SpecManagerDialog` class, chỉ truyền data source khác (tender-specific).

### 3.3. Dialog "Quản Lý Khối Lượng" (TenderBomDialog)

```
┌─ Quản Lý Khối Lượng Đấu Thầu ──────────────────────────────────────┐
│  [Tab 1: Nhập Vách & Opening]  [Tab 2: BOM Tổng Hợp]               │
│                                                                      │
│  Tab 1:                                                              │
│  ┌─ Toolbar ───────────────────────────────────────────────────────┐ │
│  │[+ Thêm][📏 Pick Dài][📐 Pick DT][🗑 Xóa][Tầng: ▼ filter]     │ │
│  └─────────────────────────────────────────────────────────────────┘ │
│  ┌─ DataGrid Vách (editable, Excel-like) ──────────────────────────┐│
│  │STT│Tầng│Tên │Dài(mm) │Cao(mm)│Spec │Khổ │DT tường│Op.R│Op.C│Op│││
│  │   │    │vách│        │       │     │tấm │  (m²)  │ộng │ao  │SL│││
│  │ 1 │ T1 │W-A1│ 10,000 │ 4,000 │ISO-T│1100│ 40.00  │2000│2500│ 1│││
│  │ 2 │ T1 │W-A1│        │       │     │    │        │1500│1200│ 2│││
│  │ 3 │ T1 │W-A2│  8,000 │ 4,000 │ISO-T│1100│ 32.00  │    │    │  │││
│  │...│    │    │        │       │     │    │        │    │    │  │││
│  └─────────────────────────────────────────────────────────────────┘│
│  │... (tiếp)  │DT Opening│ DT Net │ Số tấm │ CAD Handle (ẩn) │    ││
│  │            │  (m²)    │  (m²)  │ước tính │                 │    ││
│  │            │  5.00    │ 35.00  │  ~10    │ <polyline_1>    │    ││
│  │            │  3.60    │        │         │                 │    ││
│  │            │  0.00    │ 32.00  │  ~8     │                 │    ││
│                                                                      │
│  ┌─ Footer ────────────────────────────────────────────────────────┐ │
│  │ Tổng: 25 vách | DT tường: 1,250 m² | DT net: 1,100 m² | ~327 │ │
│  └─────────────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────────────┘
```

#### Cấu trúc dòng — Flat Rows (Vách + Opening chung bảng)

Mỗi vách = 1+ dòng:
- **Dòng chính (wall row):** STT, Tầng, Tên vách, Dài, Cao, Spec, Khổ tấm, DT tường
- **Dòng opening (nếu có):** Cùng STT/Tên vách nhưng chỉ fill cột Opening (Rộng, Cao, SL) + DT Opening

Computed columns (auto-calc, readonly):
- DT tường = Dài × Cao / 10⁶
- DT Opening = Σ(Op.Rộng × Op.Cao × Op.SL) / 10⁶
- DT Net = DT tường - DT Opening
- Số tấm = ⌈(Dài - ΣOp.Rộng) / Khổ tấm⌉

**Excel-like features (MVP):**
- ✅ Click cell → edit trực tiếp
- ✅ Tab/Enter → nhảy sang cell kế tiếp
- ✅ Delete selected rows
- ✅ Copy/Paste rows (basic text)

---

## 4. Dữ liệu đầu vào

### 4.1. Thông tin dự án (trên Palette)
| Field | Kiểu | Mô tả |
|-------|------|--------|
| Tên dự án | string | "Kho lạnh ABC" |
| Khách hàng | string | "Cty TNHH XYZ" |

### 4.2. Danh sách vách + Opening (DataGrid editable)

| Cột | Kiểu | Editable | Mô tả |
|-----|------|----------|--------|
| STT | auto | ❌ | Số thứ tự |
| Hạng mục | dropdown | ✅ | Vách / Trần |
| Tầng | string | ✅ | Tầng/khu vực |
| Tên vách | string | ✅ | VD: "W-A1" |
| Chiều dài (mm) | double | ✅ | Pick từ CAD hoặc nhập tay |
| Chiều cao (mm) | double | ✅ | Nhập tay |
| Spec | dropdown | ✅ | Chọn từ tender spec list |
| Khổ tấm (mm) | dropdown | ✅ | PanelWidthOptions |
| DT tường (m²) | computed | ❌ | Dài × Cao / 10⁶ |
| Op. Rộng (mm) | double | ✅ | Rộng opening |
| Op. Cao (mm) | double | ✅ | Cao opening |
| Op. SL | int | ✅ | Số lượng opening |
| DT Opening (m²) | computed | ❌ | R × C × SL / 10⁶ |
| DT Net (m²) | computed | ❌ | DT tường - ΣDT Opening |
| Số tấm | computed | ❌ | ước tính |
| CAD Handle | hidden | ❌ | ObjectId cho zoom |

### 4.3. Accessories Data (trong Tab 2 của BOM Dialog)

Mỗi spec có danh sách phụ kiện riêng. User có thể **thêm/sửa/xóa** dòng.

| Cột | Kiểu | Mô tả |
|-----|------|--------|
| Tên phụ kiện | string | VD: "Flashing đỉnh", "Vít TEK" |
| Đơn vị | string | md, m², cái, bộ |
| Quy tắc tính | dropdown | Xem bảng quy tắc |
| Hệ số | double | Mặc định 1.0 |

**Quy tắc tính phụ kiện:**

| Quy tắc | Công thức | Áp dụng cho |
|----------|-----------|-------------|
| `PER_WALL_LENGTH` | Chiều dài vách × hệ số | Flashing đỉnh, chân |
| `PER_WALL_HEIGHT` | Chiều cao vách × hệ số | - |
| `PER_PANEL_QTY` | Số tấm × hệ số | Vít TEK (12 vít/tấm) |
| `PER_JOINT_LENGTH` | (Số tấm - 1) × Cao × hệ số | Sealant mối nối dọc |
| `PER_OPENING_PERIMETER` | Chu vi opening × hệ số | Viền opening |
| `PER_OPENING_QTY` | Số opening × hệ số | Bộ viền cửa |
| `PER_NET_AREA` | Diện tích net × hệ số | - |
| `FIXED_PER_WALL` | 1 per vách × hệ số | Phụ kiện cố định |

**Phụ kiện mặc định** (user chỉnh sửa tự do):

| Phụ kiện | Đơn vị | Quy tắc | Hệ số |
|----------|--------|---------|-------|
| Flashing đỉnh (top) | md | PER_WALL_LENGTH | 1.0 |
| Flashing chân (base) | md | PER_WALL_LENGTH | 1.0 |
| Flashing nối dọc (joint) | md | PER_JOINT_LENGTH | 1.0 |
| Viền opening (trim) | md | PER_OPENING_PERIMETER | 1.0 |
| Vít TEK | cái | PER_PANEL_QTY | 12.0 |
| Sealant mối nối | md | PER_JOINT_LENGTH | 1.0 |
| Sealant opening | md | PER_OPENING_PERIMETER | 1.0 |
| Rivets | cái | PER_WALL_LENGTH | 6.0 |

---

## 5. AutoCAD Integration

### 5.1. Pick Wall — 2 Chế Độ

| Chế độ | Nút | Pick gì | Lấy gì | Auto-fill |
|--------|-----|---------|--------|-----------|
| **📏 Pick chiều dài** | [📏 Pick Dài] | Line hoặc Polyline (open/closed) | Tổng chiều dài | Cột "Chiều dài" |
| **📐 Pick diện tích** | [📐 Pick DT] | Polyline **closed** (rectangle) | DT + tách cạnh | "Chiều dài" + "Chiều cao" + "DT tường" |

**Chế độ "Pick diện tích" chi tiết:**
- Chỉ nhận Polyline closed (hình chữ nhật = mặt đứng vách)
- Cạnh dài nhất → **chiều dài vách**
- Cạnh ngắn nhất → **chiều cao vách**
- Nếu Polyline không phải hình chữ nhật (nhiều đỉnh) → chỉ fill diện tích, user nhập tay dài/cao

**Workflow Pick:**
```
1. User click nút Pick trên dialog
2. Dialog Hide tạm
3. User pick Polyline/Line trong drawing
4. Plugin đọc length hoặc area
5. Tạo dòng mới trong DataGrid + auto-fill
6. Lưu ObjectId/Handle cho zoom
7. Dialog Show lại
```

**Entity types:**
- `Line` → khoảng cách 2 điểm
- `Polyline` (open) → tổng chiều dài
- `Polyline` (closed/rectangle) → tách cạnh hoặc lấy area

### 5.2. Zoom to Wall
```
Khi user click dòng trong DataGrid:
→ Nếu có CAD Handle → ZoomTo + Highlight (flash entity)
→ Nếu không có Handle → không làm gì
```

### 5.3. Command & Ribbon
- Command: `SD_TENDER` (toggle Palette visibility)
- Ribbon: **Đã có sẵn** — icon `icon_tender.png` + button "Tender" trong `RibbonInitializer.cs`
- Icon: `Resources/Icons/icon_tender.png` (embedded resource, đã có trong .csproj)

---

## 6. Đầu ra — BOM Tổng Hợp (Tab 2 trong Dialog)

### 6.1. Bảng Panel
Nhóm theo Spec + Tầng:

| Cột | Mô tả |
|-----|--------|
| Tầng | Tầng/khu vực |
| Spec | Loại panel |
| Số vách | Đếm vách |
| Tổng chiều dài (m) | Σ chiều dài |
| Chiều cao (mm) | Chiều cao |
| DT tường (m²) | Σ DT tường |
| DT opening (m²) | Σ DT opening |
| DT net (m²) | Σ DT net |
| Số tấm ước tính | Σ số tấm |

### 6.2. Bảng Phụ kiện
Tổng hợp từ tất cả vách, nhóm theo phụ kiện:

| Cột | Mô tả |
|-----|--------|
| Phụ kiện | Tên |
| Đơn vị | md / cái / bộ |
| Khối lượng | Tổng |

---

## 7. Excel Output Format

### Cấu trúc: 1 Sheet duy nhất, dòng phẳng (flat rows)

```
┌────┬──────┬───────┬───────┬───────┬──────┬──────┬────────┬───────┬───────┬────┬──────────┬────────┬────────┐
│STT │ Tầng │Tên    │Dài    │Cao    │ Spec │ Khổ  │DT tường│Op.Rộng│Op.Cao │Op. │DT Opening│ DT Net │Số tấm  │
│    │      │vách   │(mm)   │(mm)   │      │(mm)  │ (m²)   │ (mm)  │ (mm)  │SL  │  (m²)    │  (m²)  │ước tính│
├────┼──────┼───────┼───────┼───────┼──────┼──────┼────────┼───────┼───────┼────┼──────────┼────────┼────────┤
│ 1  │ T1   │ W-A1  │10,000 │ 4,000 │ISO-TT│ 1100 │ 40.00  │ 2,000 │ 2,500 │  1 │   5.00   │        │        │
│ 2  │ T1   │ W-A1  │       │       │      │      │        │ 1,500 │ 1,200 │  2 │   3.60   │        │        │
│    │      │       │       │       │      │      │        │       │       │    │ Σ= 8.60  │ 31.40  │  ~10   │
├────┼──────┼───────┼───────┼───────┼──────┼──────┼────────┼───────┼───────┼────┼──────────┼────────┼────────┤
│ 3  │ T1   │ W-A2  │ 8,000 │ 4,000 │ISO-TT│ 1100 │ 32.00  │       │       │    │   0.00   │ 32.00  │  ~8    │
├────┼──────┼───────┼───────┼───────┼──────┼──────┼────────┼───────┼───────┼────┼──────────┼────────┼────────┤
│ 4  │ T1   │ W-B1  │12,000 │ 4,000 │ISO-TT│ 1100 │ 48.00  │ 2,000 │ 2,000 │  1 │   4.00   │        │        │
│    │      │       │       │       │      │      │        │       │       │    │ Σ= 4.00  │ 44.00  │  ~11   │
├────┴──────┴───────┴───────┴───────┴──────┴──────┴────────┴───────┴───────┴────┴──────────┴────────┴────────┤
│                        TỔNG TẦNG 1:  30,000mm        120.00   Σ 12.60  107.40   ~29                       │
╠═══════════════════════════════════════════════════════════════════════════════════════════════════════════════╣
│ 5  │ T2   │ W-C1  │...    │...    │...   │...   │...     │...    │...    │... │ ...      │ ...    │  ...   │
├────┴──────┴───────┴───────┴───────┴──────┴──────┴────────┴───────┴───────┴────┴──────────┴────────┴────────┤
│                        TỔNG TẦNG 2:  ...                                                                   │
╠═══════════════════════════════════════════════════════════════════════════════════════════════════════════════╣
│                        TỔNG CỘNG:    ...                                                                   │
╠═══════════════════════════════════════════════════════════════════════════════════════════════════════════════╣
│                                                                                                            │
│  📦 BẢNG PHỤ KIỆN                                                                                         │
├────┬──────────────────────────┬────────┬──────────┬──────────────────────────────────────────────────────────┤
│STT │ Phụ kiện                 │Đơn vị  │Khối lượng│ Ghi chú                                                │
│ 1  │ Flashing đỉnh            │  md    │   30.0   │                                                        │
│ 2  │ Flashing chân            │  md    │   30.0   │                                                        │
│ 3  │ Vít TEK                  │  cái   │   348    │                                                        │
│...                                                                                                         │
└────┴──────────────────────────┴────────┴──────────┴──────────────────────────────────────────────────────────┘
```

**Đặc điểm:**
- ✅ Flat rows — không merge cell → dễ xử lý số liệu trong Excel
- ✅ Vách có opening → nhiều dòng (1 dòng chính + n dòng opening)
- ✅ Vách không opening → 1 dòng duy nhất
- ✅ Tổng per tầng + tổng cộng
- ✅ Phụ kiện liệt kê phía dưới, cùng sheet

---

## 8. Kiến trúc kỹ thuật

### 8.1. Files mới

```
ShopDrawing.Plugin/
├── Models/
│   ├── TenderWall.cs           # Vách chào giá (bao gồm opening list)
│   ├── TenderOpening.cs        # Opening cho tender
│   ├── TenderAccessory.cs      # Phụ kiện template + quy tắc tính
│   └── TenderProject.cs        # Wrapper: project info + walls + specs + accessories
├── Core/
│   ├── TenderBomCalculator.cs  # Tính BOM từ wall data
│   ├── TenderProjectManager.cs # Save/Load JSON project
│   └── AccessoryDataManager.cs # Quản lý accessory templates per spec
├── UI/
│   ├── TenderPaletteControl.cs # WPF UserControl (PaletteSet)
│   └── TenderBomDialog.cs      # WPF Window (DataGrid + 2 tabs)
└── Commands/
    └── (update ShopDrawingCommands.cs — thêm SD_TENDER + _tenderPaletteSet)
```

### 8.2. Tái sử dụng từ ShopDrawing

| Component | Cách dùng |
|-----------|-----------|
| `PanelSpec` model | Dropdown chọn spec |
| `SpecConfigManager` | Load spec list (fork vào tender project) |
| `SpecManagerDialog` | Reuse class, data source riêng per tender project |
| `ExcelExporter` pattern | Export NPOI |
| `RibbonInitializer` | **Đã có sẵn** — icon + button "Tender" |
| WPF DataGrid patterns | Copy từ WasteManagerDialog |
| PaletteSet pattern | Copy từ ShopDrawingCommands.cs |

### 8.3. Data persistence

```
public/Resources/
├── panel_specs.json             # Existing — ShopDrawing spec (nguồn fork)
├── tender_accessories.json      # NEW — default accessory templates
└── tender_projects/             # NEW — saved tender projects
    ├── project_001.json         # Chứa: project info + walls + specs + accessories
    └── project_002.json
```

---

## 9. Công thức tính toán

### 9.1. Số tấm ước tính
```csharp
double effectiveLength = wallLength - totalOpeningWidth;
int panelCount = (int)Math.Ceiling(effectiveLength / panelWidth);
panelCount = Math.Max(0, panelCount);
```

### 9.2. Diện tích
```csharp
double wallArea = wallLength * wallHeight / 1_000_000.0;       // m²
double openingArea = Σ(op.Width * op.Height * op.Qty) / 1e6;   // m²
double netArea = wallArea - openingArea;
```

### 9.3. Phụ kiện (ví dụ)
```csharp
// Vít TEK, rule=PER_PANEL_QTY, factor=12.0
double qty = panelCount * 12.0;

// Sealant mối nối, rule=PER_JOINT_LENGTH, factor=1.0
double qty = (panelCount - 1) * wallHeight / 1000.0;  // mét dài

// Viền opening, rule=PER_OPENING_PERIMETER, factor=1.0
double qty = Σ((op.Width + op.Height) * 2 * op.Qty) / 1000.0;
```

---

## 10. Phân kỳ triển khai

### Phase 1 — MVP

| Bước | Task | Ưu tiên |
|------|------|---------|
| 1 | Models (TenderWall, TenderOpening, TenderAccessory, TenderProject) | HIGH |
| 2 | TenderBomCalculator + AccessoryDataManager | HIGH |
| 3 | TenderPaletteControl (Palette UI) | HIGH |
| 4 | TenderBomDialog — Tab 1 (DataGrid vách + opening, editable) | HIGH |
| 5 | TenderBomDialog — Tab 2 (BOM panel + phụ kiện, readonly) | HIGH |
| 6 | Pick wall từ CAD — 2 chế độ (chiều dài / diện tích) | HIGH |
| 7 | Zoom/Highlight wall | MEDIUM |
| 8 | Spec fork (copy spec list vào project) | MEDIUM |
| 9 | Excel export — flat rows (NPOI) | MEDIUM |
| 10 | Save/Load project (JSON) | MEDIUM |
| 11 | SD_TENDER command registration | LOW |

### Phase 1.1 — Enhanced Excel-like
- Drag-fill series
- Multi-cell selection
- Better keyboard shortcuts

### Phase 2 — Pricing (future)
- Tab 3: Đơn giá vật tư
- Tab 4: Tổng hợp chào giá

### Phase 3 — Full Spreadsheet (evaluate)
- WebView2 + Luckysheet nếu cần formatting/formulas trong dialog

---

## 11. Verification Plan

### Build
```
dotnet build ShopDrawing.Plugin/ShopDrawing.Plugin.csproj
```

### Manual Testing (trong AutoCAD)
1. NETLOAD DLL → gõ `SD_TENDER` → palette mở/đóng
2. Nhập tên dự án + khách hàng
3. Click "Quản Lý Spec" → dialog spec mở, edit, lưu
4. Click "Quản Lý Khối Lượng" → dialog BOM mở
5. Thêm vách tay → computed columns cập nhật (DT, số tấm)
6. Thêm opening cho vách → DT opening + DT net cập nhật
7. Pick Line từ CAD → chiều dài auto-fill
8. Pick closed Polyline → chiều dài + chiều cao + DT auto-fill
9. Click vách có Handle → CAD zoom tới
10. Tab 2 → BOM panel + phụ kiện hiển thị đúng
11. Xuất Excel → mở file, kiểm tra flat rows + phụ kiện
12. Lưu dự án → đóng/mở lại → data còn nguyên

---

## 12. Rủi ro & Giảm thiểu

| Rủi ro | Giảm thiểu |
|--------|------------|
| Ước tính số tấm không chính xác | Ghi rõ "ước tính" trên dialog + Excel |
| Polyline closed không phải hình chữ nhật | Chỉ fill DT, user nhập tay dài/cao |
| Dialog BOM quá phức tạp | Phase 1 chỉ 2 tab, giữ simple |
| Phụ kiện khác nhau per dự án | Accessory data sheet cho phép edit tự do |
| Spec bị lệch giữa Tender và ShopDrawing | Fork pattern: mỗi bên quản lý độc lập |
