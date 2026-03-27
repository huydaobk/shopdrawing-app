# PRD — Xuất PDF (ExportPdf) v1.1

**Plugin:** Shop Drawing Plugin (AutoCAD .NET)
**Command:** `SD_EXPORT`
**Trạng thái:** ✅ Confirmed — Sẵn sàng code

---

## 1. Mục tiêu

Xuất **toàn bộ Layout tabs** của bản vẽ sang 1 file PDF multipage, trực tiếp từ palette.
Hỗ trợ chọn máy in, plot style, và nhiều khổ giấy khác nhau trên từng trang.

---

## 2. Phạm vi (Scope v1)

| Hạng mục | v1 |
|----------|----|
| Xuất tất cả layouts ra 1 PDF | ✅ |
| Mỗi layout = 1 trang | ✅ |
| Nhiều khổ giấy khác nhau (per-layout) | ✅ |
| Chọn thư mục + tên file | ✅ |
| Khi file tồn tại: hỏi ghi đè hoặc auto-version | ✅ |
| Layout trống → bỏ qua | ✅ |
| Thứ tự theo tab layout | ✅ |
| Chọn máy in (PC3 plotter) | ✅ |
| Chọn Plot Style (CTB) | ✅ |
| Mở PDF sau khi xong | ✅ |
| Tạo file `SD_Black.ctb` | ✅ |
| Watermark | ❌ v2 |
| Chọn từng layout | ❌ v2 |

---

## 3. Luồng người dùng

```
[Ribbon] Xuất PDF
    ↓
[Palette SD_EXPORT]
    ├─ Máy in:   [▼ DWG To PDF.pc3    ]
    ├─ PlotStyle:[▼ SD_Black.ctb       ]
    ├─ Thư mục: [C:\...\output] [📁]
    ├─ Tên file: [W1-ShopDrg.pdf     ]
    ├─ ☑ Mở PDF sau khi xong
    └─ [📄 Xuất PDF]
            ↓
    Kiểm tra file tồn tại?
    ├─ Chưa có → xuất luôn
    └─ Đã có → Dialog:
          [Ghi đè] [Tự thêm _v2] [Huỷ]
            ↓
    Loop từng layout:
    ├─ Layout trống? → skip
    └─ Plot từng trang vào PDF
            ↓
    Xong → "✅ Đã xuất 5 trang → W1-ShopDrg.pdf"
    + Mở PDF (nếu tick)
```

---

## 4. Plot Style — `SD_Black.ctb`

### 4.1 Tại sao tạo mới

`monochrome.ctb` mặc định của AutoCAD không có phân cấp nét phù hợp với shopdrawing.
`SD_Black.ctb` được thiết kế theo **ISO/DIN pen hierarchy (7 nét)**, cross-map trực tiếp với
màu layer của Shop Drawing Plugin.

### 4.2 Quy tắc SD_Black — Tối ưu theo ISO/DIN

**7 cấp nét phân cấp:**

| Nét | Lineweight | Mục đích |
|-----|-----------|---------|
| Ultra thin | 0.09mm | Hatch screening (nền) |
| Very thin | 0.13mm | Chi tiết rất nhỏ |
| Thin | 0.18mm | Dim, text, annotation |
| Medium thin | 0.25mm | Đường phụ thông thường |
| Medium | 0.35mm | Outline panel, opening |
| Heavy | 0.50mm | Outline tường chính |
| Extra heavy | 0.70mm | Section cut (dự phòng v2) |

**Bảng mapping màu → nét (cross-map với layer thực tế của plugin):**

| ACI | Màu | Layer SD dùng | Lineweight | Ghi chú |
|-----|-----|---------------|-----------|---------| 
| 1 | Red | DetailPlacer, dim detail | **0.13mm** | Fine detail |
| 2 | Yellow | General lines | **0.18mm** | ISO thin |
| 3 | Green | `Defpoints` (viewport frame) | **Không plot** | ⚡ AutoCAD system layer — không bao giờ print, không bị tắt ảnh hưởng content |
| 4 | Cyan | `SD_DIM_PANEL`, elevation line | **0.18mm** | Dim = thin |
| 5 | Blue | General med | **0.25mm** | ISO medium-thin |
| 6 | Magenta | `SD_DIM_OPENING` | **0.18mm** | Dim = thin |
| 7 | White/Black | `SD_TITLE` (viền ngoài KT), `SD_TITLE_FRAME` (kẻ nội bộ), `SD_TITLE_TEXT` (text) | **0.50mm / 0.35mm / 0.18mm** | Theo sub-layer |
| 8 | Dark gray | `SD_TAG` text | **0.18mm** | Annotation |
| 9 | Lt gray | Phụ nhạt | **0.13mm** | Very thin |
| 30 | Orange | `SD_OPENING` outline | **0.35mm** | Opening nổi rõ |
| 251 | Very dark | `SD_WALL` | **0.50mm** | Tường heavy |
| 252 | Dark gray | Panel border | **0.35mm** | Panel outline |
| 253 | Gray | Hatch filling | **0.09mm** | Hatch screening |
| 254 | Lt gray | Non-plot | **0.00mm** | ⚡ Không in |
| 255 | White | Background | **0.25mm** | Standard |
| 10–250 (còn lại) | — | Chung | **0.18mm** | Safe default |

> **Tất cả màu đều in đen** (output color = Black).

### 4.3 Nguồn tham khảo

- ISO 128 / DIN 15-2: Lineweight hierarchy for technical drawings
- FirstInArchitecture.co.uk: 7-pen lineweight best practices
- CADTutor.net: ISO CTB color mapping 1-10

### 4.4 Cách tạo CTB file

- **Option A (Đơn giản):** Copy `monochrome.ctb` làm base → patch lineweight values.
  - CTB = ASCII text format (không phải binary) → có thể chỉnh trực tiếp.
- **Option B (Programmatic):** Dùng AutoCAD API `PlotStyleTableRecord`.

**Đường dẫn cài đặt:**
```
%APPDATA%\Autodesk\AutoCAD 2026\R25.1\enu\Plotters\Plot Styles\SD_Black.ctb
```
**Version control:**
```
c:\my_project\shopdrawing-app\resources\plotstyles\SD_Black.ctb
```
`PlotStyleInstaller.cs` tự copy vào thư mục AutoCAD khi plugin load.

---

## 5. Logic kỹ thuật

### 5.1 Plotter list

Scan `%APPDATA%\...\Plotters\*.pc3` + `%PROGRAMDATA%\...\*.pc3` → hiển thị tên file (bỏ đuôi `.pc3`).

```csharp
PlotSettingsValidator.Current.SetPlotConfigurationName(
    plotSettings, plotterName + ".pc3", null);
```

### 5.2 Plot Style list

Scan `%APPDATA%\...\Plot Styles\*.ctb` → hiển thị tên.

```csharp
PlotSettingsValidator.Current.SetCurrentStyleSheet(plotSettings, "SD_Black.ctb");
```

### 5.3 Per-layout paper size

```csharp
// Layout Manager dùng SetClosestMediaName để set khổ giấy chính xác (v5.1)
// Khi export, PdfExportEngine giữ nguyên CanonicalMediaName đã sẵn có trên layout
// và chỉ thay đổi Plotter + PlotStyle, KHÔNG reset media name.
string currentMedia = layout.CanonicalMediaName;
try {
    psv.SetPlotConfigurationName(ps, options.PlotterName, currentMedia);
} catch {
    psv.SetPlotConfigurationName(ps, options.PlotterName, null); // fallback
}
```

> ⚠️ Canonical media name dùng thứ tự **Portrait** (vd: `297.00_x_420.00` cho A3).
> Không nên match bằng string dimension — dùng `SetClosestMediaName(W, H, MM, true)`.

Nếu layout không có PageSetup: fallback `ISO full bleed A3`.

### 5.4 Multi-sheet PDF

```csharp
using var engine = PlotFactory.CreatePublishEngine();
engine.BeginDocument(plotInfo, docName, null, 1, true, filePath);
foreach (var layout in layouts)
{
    engine.BeginPage(pageInfo, plotProgress, true, null);
    engine.BeginGenerateGraphics(null);
    engine.EndGenerateGraphics(null);
    engine.EndPage(null);
}
engine.EndDocument(null);
```

### 5.5 File conflict handling

```
if (File.Exists(outputPath)):
    → Dialog 3 nút: [Ghi đè] [Tự thêm _v2] [Huỷ]
    
"Tự thêm _v2" → scan: nếu _v2 tồn tại → _v3, v.v. đến _v99
```

### 5.6 Thread Safety

- Plot engine: **main CAD thread** (`doc.LockDocument()`).
- Progress UI update: `Application.MainWindow.Dispatcher.Invoke`.

---

## 6. UI — Export Palette

```
┌────────────────────────────────────────┐
│  XUẤT PDF                              │
├────────────────────────────────────────┤
│  Máy in:     [▼ DWG To PDF.pc3      ] │
│  PlotStyle:  [▼ SD_Black.ctb        ] │
├────────────────────────────────────────┤
│  Thư mục:  [C:\...\output    ] [📁]  │
│  Tên file: [NhaXuong-SD.pdf  ]        │
├────────────────────────────────────────┤
│  ☑ Mở PDF sau khi xong                │
├────────────────────────────────────────┤
│  [     📄 Xuất PDF     ]              │
├────────────────────────────────────────┤
│  Status: Đang xuất trang 3/7...       │
└────────────────────────────────────────┘
```

### UI Controls

| Control | Logic |
|---------|-------|
| ComboBox Máy in | Scan PC3 files lúc load palette |
| ComboBox PlotStyle | Scan CTB files lúc load palette |
| TextBox Thư mục | Mặc định folder chứa DWG |
| Button `📁` | `FolderBrowserDialog` |
| TextBox Tên file | Mặc định `<DWGName>.pdf` |
| CheckBox Mở PDF | Mặc định ☑ |
| Button Xuất | Trigger export |
| TextBlock Status | Progress realtime |

---

## 7. Xử lý lỗi

| Trường hợp | Xử lý |
|------------|-------|
| File PDF đang mở | "Đóng file PDF đang mở trước" |
| Layout trống | Bỏ qua, đếm vào log |
| Plot engine lỗi | Log lỗi, hiện thông báo, không crash |
| Huỷ giữa chừng | Xóa file PDF tạm |
| Không có layout nào | "Bản vẽ không có Layout nào" |

---

## 8. Files cần tạo / sửa

| File | Action | Mô tả |
|------|--------|-------|
| `Core/PdfExportEngine.cs` | **MỚI** | Logic plot, per-layout, multi-sheet |
| `UI/ExportPdfPaletteControl.cs` | **MỚI** | WPF palette compact |
| `Commands/ShopDrawingCommands.cs` | **SỬA** | Thêm lệnh `SD_EXPORT` |
| `resources/plotstyles/SD_Black.ctb` | **MỚI** | Plot style file |
| `Core/PlotStyleInstaller.cs` | **MỚI** | Copy CTB vào AutoCAD folder khi load |

---

## 9. Kế hoạch kiểm thử

| Test | Expected |
|------|----------|
| DWG có 5 layout (mix A1+A3) | PDF 5 trang, khổ giấy đúng |
| DWG chỉ có Model | Thông báo "Không có Layout" |
| File PDF đã tồn tại | Dialog 3 nút hiện ra |
| Chọn `Tự thêm _v2` | File `_v2.pdf` tạo ra |
| Huỷ giữa chừng | Không file tạm nào còn lại |
| Layout trống giữa chuỗi | Bị skip, các layout khác vẫn xuất |
